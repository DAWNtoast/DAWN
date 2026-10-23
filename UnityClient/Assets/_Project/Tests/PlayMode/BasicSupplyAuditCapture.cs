using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    /// <summary>Opt-in actual UI renders; pass -basicSupplyAuditPath and a new output directory.</summary>
    internal static class BasicSupplyAuditCapture
    {
        [Serializable]
        private sealed class TextReport
        {
            public int width, height, fontRebuilds;
            public List<TextEvidence> texts = new List<TextEvidence>();
        }

        [Serializable]
        private sealed class TextEvidence
        {
            public string name, text;
            public int fontSize, meshVertices;
            public bool culled;
        }

        public static void Capture(CoreGameplayDemoController controller, string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "-basicSupplyAuditPath");
            if (index < 0) return;
            if (index + 1 >= arguments.Length) throw new ArgumentException("Missing basic-supply capture directory.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Basic-supply captures require a graphics device; omit -nographics.");
            string directory = Path.GetFullPath(arguments[index + 1]);
            Directory.CreateDirectory(directory);
            Canvas canvas = controller.GetComponentInChildren<Canvas>().rootCanvas;
            CaptureSize(canvas, Path.Combine(directory, name + "_1920x1080.png"), 1920, 1080);
            CaptureSize(canvas, Path.Combine(directory, name + "_1366x768.png"), 1366, 768);
        }

        private static void CaptureSize(Canvas canvas, string path, int width, int height)
        {
            if (File.Exists(path)) throw new IOException("Capture already exists: " + path);
            RectTransform rect = (RectTransform)canvas.transform;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            bool wasEnabled = scaler != null && scaler.enabled;
            RenderMode mode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            float planeDistance = canvas.planeDistance;
            Vector2 size = rect.sizeDelta;
            Vector3 position = rect.localPosition, scale = rect.localScale;
            Quaternion rotation = rect.localRotation;
            RenderTexture active = RenderTexture.active;
            Transform[] descendants = canvas.GetComponentsInChildren<Transform>(true);
            int[] layers = descendants.Select(child => child.gameObject.layer).ToArray();
            GameObject cameraHost = null;
            RenderTexture target = null;
            Texture2D pixels = null;
            try
            {
                if (scaler != null) scaler.enabled = false;
                cameraHost = new GameObject("Basic Supply Audit Camera", typeof(Camera));
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                // Render the actual fixed 1920x1080 game surface at both display
                // sizes. This preserves its production letterbox/scale rather
                // than treating the smaller output as a 1366-unit logical UI.
                camera.orthographicSize = 540f;
                camera.aspect = width / (float)height;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0f, 0f, -1000f);
                camera.cullingMask = 1 << 31;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                rect.sizeDelta = new Vector2(1920f, 1080f);
                rect.position = Vector3.zero;
                rect.rotation = Quaternion.identity;
                rect.localScale = Vector3.one;
                foreach (Transform child in descendants) child.gameObject.layer = 31;
                Text[] visibleText = canvas.GetComponentsInChildren<Text>()
                    .Where(text => text.IsActive() && !string.IsNullOrWhiteSpace(text.text)).ToArray();
                var fonts = new HashSet<Font>(visibleText.Where(text => text.font != null).Select(text => text.font));
                int fontRebuilds = 0, stablePasses = 0;
                void FontRebuilt(Font font) { if (fonts.Contains(font)) fontRebuilds++; }
                Font.textureRebuilt += FontRebuilt;
                try
                {
                    // A changed quantity or warning can repack a shared dynamic
                    // font atlas during this same-frame offscreen capture. Warm
                    // visible glyphs, then require two complete passes without a
                    // repack so earlier Text meshes cannot retain stale UVs.
                    for (int pass = 0; pass < 8 && stablePasses < 2; pass++)
                    {
                        int before = fontRebuilds;
                        foreach (Text text in visibleText)
                        {
                            if (text.font != null && text.font.dynamic)
                            {
                                int usedSize = text.resizeTextForBestFit ? text.cachedTextGenerator.fontSizeUsedForBestFit : text.fontSize;
                                text.font.RequestCharactersInTexture(text.text, usedSize > 0 ? usedSize : text.fontSize, text.fontStyle);
                            }
                            text.cachedTextGenerator.Invalidate();
                            text.SetAllDirty();
                        }
                        Canvas.ForceUpdateCanvases();
                        if (GraphicsSettings.currentRenderPipeline != null)
                            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                        else camera.Render();
                        stablePasses = before == fontRebuilds ? stablePasses + 1 : 0;
                    }
                }
                finally { Font.textureRebuilt -= FontRebuilt; }
                if (stablePasses < 2) throw new InvalidOperationException("Dynamic fonts did not settle for capture: " + path);
                TextReport report = InspectSupplyText(canvas, width, height, fontRebuilds);
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                pixels.Apply(false, false);
                Color32[] samples = pixels.GetPixels32();
                int minimum = 255, maximum = 0;
                for (int sample = 0; sample < samples.Length; sample += 997)
                {
                    int value = (samples[sample].r + samples[sample].g + samples[sample].b) / 3;
                    minimum = Math.Min(minimum, value); maximum = Math.Max(maximum, value);
                }
                if (maximum - minimum < 20) throw new InvalidOperationException("Basic-supply capture appears blank: " + path);
                using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] png = pixels.EncodeToPNG();
                    output.Write(png, 0, png.Length);
                }
                File.WriteAllText(Path.ChangeExtension(path, ".text.json"), JsonUtility.ToJson(report, true));
            }
            finally
            {
                RenderTexture.active = active;
                canvas.renderMode = mode;
                canvas.worldCamera = previousCamera;
                canvas.planeDistance = planeDistance;
                rect.sizeDelta = size;
                rect.localPosition = position;
                rect.localRotation = rotation;
                rect.localScale = scale;
                for (int child = 0; child < descendants.Length; child++)
                    if (descendants[child] != null) descendants[child].gameObject.layer = layers[child];
                if (scaler != null) scaler.enabled = wasEnabled;
                if (cameraHost != null) Object.DestroyImmediate(cameraHost);
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                Canvas.ForceUpdateCanvases();
            }
        }

        private static TextReport InspectSupplyText(Canvas canvas, int width, int height, int fontRebuilds)
        {
            var report = new TextReport { width = width, height = height, fontRebuilds = fontRebuilds };
            Transform modal = canvas.GetComponentsInChildren<Transform>().Single(item => item.name == "Departure Basic Supplies");
            foreach (Text text in modal.GetComponentsInChildren<Text>())
            {
                if (!text.IsActive() || string.IsNullOrWhiteSpace(text.text)) continue;
                Mesh mesh = text.canvasRenderer.GetMesh();
                var evidence = new TextEvidence { name = text.name, text = text.text, fontSize = text.fontSize,
                    meshVertices = mesh == null ? 0 : mesh.vertexCount, culled = text.canvasRenderer.cull };
                report.texts.Add(evidence);
                if (evidence.culled || evidence.meshVertices == 0)
                    throw new InvalidOperationException("Visible supply text has no rendered mesh: " + text.name + " = " + text.text);
            }
            return report;
        }
    }
}
