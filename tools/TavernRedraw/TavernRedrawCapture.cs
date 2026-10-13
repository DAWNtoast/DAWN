using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    // Install only in this task's disposable project. No source asset or scene writes.
    public static class TavernRedrawCapture
    {
        private const int Width = 1920, Height = 1080, Layer = 31;
        public static void CaptureSavedFromCommandLine()
        {
            throw new System.NotSupportedException("UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.");
#if false // Historical implementation; this target is retired.
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-tavernSavedCapturePath");
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
                throw new ArgumentException("An absolute -tavernSavedCapturePath is required.");
            MethodInfo capture = typeof(UIPreviewSceneAcceptance).GetMethod("Capture", BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(string), typeof(string), typeof(string[]), typeof(string) }, null);
            if (capture == null) throw new MissingMethodException("UIPreviewSceneAcceptance.Capture with explicit references");
            // Existing capture opens saved PreviewScenes and rejects rebuilding incomplete authored UI.
            capture.Invoke(null, new object[] { args[index + 1], "Assets/_Project/Scenes/UIPreviewsForMe",
                new[] { "UI_Tavern.unity", "UITavern/UITavern_Roster.unity", "TutorialVoyage/08_Tavern.unity", "UI_TavernCrewDetail.unity" }, null });

#endif
        }
        [Serializable] private sealed class Review
        {
            public string utc, scope = "Six production-built regional Tavern pages; non-Play private PreviewScene; imported fallback checked; no scene/assets/PlayerPrefs writes.";
            public int width = Width, height = Height, hotspotCount;
            public List<Art> textures = new List<Art>();
            public List<Page> pages = new List<Page>();
            public List<string> errors = new List<string>();
        }
        [Serializable] private sealed class Art
        {
            public string resourcePath, assetPath, sha256, format, graphicsFormat, filterMode, wrapMode;
            public int sourceWidth, sourceHeight, width, height, maxTextureSize, mipmapCount;
            public bool readable, standaloneOverride, passed;
        }
        [Serializable] private sealed class Page
        {
            public string port, file, textureResourcePath;
            public bool completePreview;
            public Rect backgroundUv;
            public List<Hotspot> hotspots = new List<Hotspot>();
        }
        [Serializable] private sealed class Hotspot
        {
            public int index;
            public string name, status;
            public bool interactable;
            public Rect authoredNormalizedBounds, screenBounds, nameScreenBounds, statusScreenBounds;
            public Vector2 localPosition, size;
        }

        public static void CaptureFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-tavernRedrawCapturePath");
            if (at < 0 || at + 1 >= args.Length || !Path.IsPathRooted(args[at + 1]))
                throw new ArgumentException("An absolute -tavernRedrawCapturePath is required.");
            string output = Path.GetFullPath(args[at + 1]);
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Capture output must be new or empty.");
            if (Application.isPlaying || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Use non-Play editor with graphics enabled.");
            Directory.CreateDirectory(output);
            var report = new Review { utc = DateTime.UtcNow.ToString("O") };
            Scene scene = EditorSceneManager.NewPreviewScene();
            GameObject host = null, cameraHost = null;
            RenderTexture target = null, oldTarget = RenderTexture.active;
            try
            {
                var catalog = new PortFacilityArtCatalog();
                InspectArt(report, "PortFacilities/tavern_background_seated_v2", catalog.Background(PortFacilityArtCatalog.TavernFacilityIndex));
                host = new GameObject("Tavern Redraw Review Host");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                var controller = host.AddComponent<CoreGameplayDemoController>();
                host.SetActive(true);
                cameraHost = new GameObject("Tavern Redraw Review Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                var camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.025f, .06f, .075f, 1f);
                camera.orthographic = true;
                camera.orthographicSize = Height / 2f;
                camera.aspect = Width / (float)Height;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0, 0, -1000);
                camera.cullingMask = 1 << Layer;
                camera.scene = scene;
                camera.allowHDR = camera.allowMSAA = false;
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                camera.targetTexture = target;
                foreach (CoreGameplayPreviewPort port in Enum.GetValues(typeof(CoreGameplayPreviewPort)))
                {
                    var page = new Page { file = port + "_Tavern.png" };
                    report.pages.Add(page);
                    try
                    {
                        controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Tavern, port, 0);
                        page.port = controller.Session.CurrentPort;
                        page.completePreview = controller.HasCompleteEditorPreview;
                        if (!page.completePreview) throw new InvalidOperationException("Incomplete production Tavern preview.");
                        var canvas = host.GetComponentInChildren<Canvas>();
                        if (canvas == null) throw new InvalidOperationException("No production Canvas.");
                        canvas.renderMode = RenderMode.WorldSpace;
                        canvas.worldCamera = camera;
                        var canvasRect = canvas.GetComponent<RectTransform>();
                        canvasRect.sizeDelta = new Vector2(Width, Height);
                        canvasRect.position = Vector3.zero;
                        canvasRect.localScale = Vector3.one;
                        foreach (Transform item in canvas.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = Layer;
                        Canvas.ForceUpdateCanvases();
                        var background = host.GetComponentsInChildren<RawImage>(true).Single(item => item.name == "Tavern Background");
                        page.textureResourcePath = PortFacilityArtCatalog.RegionalBackgroundResourcePath(PortFacilityArtCatalog.TavernFacilityIndex, page.port);
                        page.backgroundUv = background.uvRect;
                        if (background.color != Color.white) throw new InvalidOperationException("Background is unexpectedly tinted.");
                        InspectArt(report, page.textureResourcePath, background.texture as Texture2D);
                        for (int index = 0; index < 6; index++)
                        {
                            var button = host.GetComponentsInChildren<Button>(true).Single(item => item.name == "Tavern Crew " + index);
                            var label = host.GetComponentsInChildren<Text>(true).Single(item => item.name == "Tavern Crew Name " + index);
                            var status = host.GetComponentsInChildren<Text>(true).Single(item => item.name == "Tavern Crew Status " + index);
                            var rect = button.GetComponent<RectTransform>();
                            page.hotspots.Add(new Hotspot {
                                index = index, name = label.text, status = status.text, interactable = button.interactable,
                                authoredNormalizedBounds = TavernCrewLayoutCatalog.NormalizedBounds(page.port, index),
                                screenBounds = ScreenBounds(rect, camera), nameScreenBounds = ScreenBounds(label.rectTransform, camera),
                                statusScreenBounds = ScreenBounds(status.rectTransform, camera), localPosition = rect.anchoredPosition, size = rect.rect.size
                            });
                            report.hotspotCount++;
                        }
                        if (GraphicsSettings.currentRenderPipeline != null)
                            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                        else camera.Render();
                        SaveFrame(target, Path.Combine(output, page.file));
                    }
                    catch (Exception error) { report.errors.Add(port + ": " + error); Debug.LogException(error); }
                }
            }
            finally
            {
                RenderTexture.active = oldTarget;
                if (cameraHost != null) UnityEngine.Object.DestroyImmediate(cameraHost);
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                EditorSceneManager.ClosePreviewScene(scene);
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                WriteGallery(output, report);
            }
            if (report.errors.Count > 0 || report.hotspotCount != 36 || report.textures.Count != 7 || report.textures.Any(item => !item.passed))
                throw new InvalidOperationException("Tavern review failed. Inspect report.json; errors=" + report.errors.Count);
            Debug.Log("TAVERN_REDRAW_REVIEW: six pages, 36 hotspots, seven imported textures passed. " + output);
        }

        private static void InspectArt(Review review, string path, Texture2D texture)
        {
            var item = new Art { resourcePath = path };
            review.textures.Add(item);
            if (texture == null) { review.errors.Add(path + ": missing texture"); return; }
            item.assetPath = AssetDatabase.GetAssetPath(texture);
            var importer = AssetImporter.GetAtPath(item.assetPath) as TextureImporter;
            if (importer == null) { review.errors.Add(path + ": missing importer"); return; }
            importer.GetSourceTextureWidthAndHeight(out item.sourceWidth, out item.sourceHeight);
            var platform = importer.GetPlatformTextureSettings("Standalone");
            item.standaloneOverride = platform.overridden;
            item.maxTextureSize = platform.overridden ? platform.maxTextureSize : importer.maxTextureSize;
            item.width = texture.width; item.height = texture.height;
            item.format = texture.format.ToString(); item.graphicsFormat = texture.graphicsFormat.ToString();
            item.filterMode = texture.filterMode.ToString(); item.wrapMode = texture.wrapMode.ToString();
            item.mipmapCount = texture.mipmapCount; item.readable = texture.isReadable;
            using (var source = File.OpenRead(item.assetPath))
            using (var sha = SHA256.Create()) item.sha256 = BitConverter.ToString(sha.ComputeHash(source)).Replace("-", "").ToLowerInvariant();
            item.passed = texture.format == TextureFormat.BC7 && !texture.isReadable && texture.mipmapCount == 1 &&
                texture.filterMode == FilterMode.Bilinear && texture.wrapMode == TextureWrapMode.Clamp &&
                Math.Abs(texture.width - item.sourceWidth) <= 2 && Math.Abs(texture.height - item.sourceHeight) <= 2;
            if (!item.passed) review.errors.Add(path + ": unexpected format/sampling/readability/mipmaps or source resolution lost on import");
        }

        private static Rect ScreenBounds(RectTransform rect, Camera camera)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var a = camera.WorldToScreenPoint(corners[0]); var b = camera.WorldToScreenPoint(corners[2]);
            return new Rect(a.x, Height - b.y, b.x - a.x, b.y - a.y);
        }
        private static void SaveFrame(RenderTexture target, string path)
        {
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
            try
            {
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); image.Apply(false, false);
                Color32[] pixels = image.GetPixels32(); int low = 255, high = 0;
                for (int i = 0; i < pixels.Length; i += 997) { int value = (pixels[i].r + pixels[i].g + pixels[i].b) / 3; low = Math.Min(low, value); high = Math.Max(high, value); }
                if (high - low < 20) throw new InvalidOperationException("Screenshot is nearly uniform.");
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }
        private static void WriteGallery(string output, Review report)
        {
            var html = new StringBuilder("<!doctype html><meta charset=utf-8><title>Tavern redraw review</title><style>body{background:#172126;color:#eedbb7;font:16px system-ui;margin:24px}figure{max-width:1500px;margin:32px auto}a{color:#ebc785}.frame{position:relative}img{width:100%;display:block}svg{position:absolute;inset:0;width:100%;height:100%;pointer-events:none}rect{fill:none;stroke:#00ffd5;stroke-width:2}text{fill:#00ffd5;font:22px sans-serif;paint-order:stroke;stroke:#000;stroke-width:3}.hide svg{display:none}</style><h1>Six-port Tavern redraw</h1><p>Production UI screenshots and 36 measured character hit areas. <a href=report.json>Texture and geometry report</a></p><label><input type=checkbox checked onchange=\"document.body.classList.toggle('hide',!this.checked)\"> Show character hit areas</label>");
            foreach (Page page in report.pages)
            {
                html.Append("<figure><figcaption>").Append(page.port).Append("</figcaption><div class=frame><img src=\"").Append(page.file).Append("\"><svg viewBox=\"0 0 1920 1080\">");
                foreach (Hotspot hotspot in page.hotspots)
                {
                    Rect r = hotspot.screenBounds;
                    html.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "<rect x=\"{0:F1}\" y=\"{1:F1}\" width=\"{2:F1}\" height=\"{3:F1}\"/><text x=\"{0:F1}\" y=\"{4:F1}\">{5}</text>", r.x, r.y, r.width, r.height, r.y + 25, hotspot.index);
                }
                html.Append("</svg></div></figure>");
            }
            File.WriteAllText(Path.Combine(output, "index.html"), html.ToString(), new UTF8Encoding(false));
        }
    }
}
