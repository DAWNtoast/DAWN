using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    // Copy only into the disposable validation project. No scene or asset saves.
    public static class RegionalDepartureDockCapture
    {
        private const string Project = "D:/Game/tmp/departure-chart-inline-20261014";
        private const string Output = "D:/Game/Artifacts/RegionalDepartureDocks20261016/validation/screenshots";
        private const int Width = 1920, Height = 1080, Layer = 31;
        private static readonly string[] Slugs = { "broken_anchor", "white_shell", "bronze_bell", "salt_crown", "twilight_tide", "gray_gull" };
        [Serializable] private sealed class Report { public List<Page> pages = new List<Page>(); }
        [Serializable] private sealed class Page
        {
            public string port, file, background, foreground, hull;
            public bool passed;
        }
        public static void Capture()
        {
            if (!Application.dataPath.Replace('\\', '/').Equals(Project + "/Assets", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Use only the disposable validation project.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Capture requires a stopped editor with graphics enabled.");
            Directory.CreateDirectory(Output);
            var report = new Report();
            for (int port = 0; port < Slugs.Length; port++) report.pages.Add(CapturePort(port));
            File.WriteAllText(Path.Combine(Output, "capture-report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("REGIONAL_DEPARTURE_CAPTURE: six production UI screenshots saved to " + Output);
        }
        private static void ConfigureCamera(GameObject host, Scene scene, Camera camera, RenderTexture target)
        {
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .06f, .075f, 1f);
            camera.orthographic = true;
            camera.aspect = Width / (float)Height;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 2000f;
            camera.transform.position = new Vector3(0, 0, -1000);
            camera.cullingMask = 1 << Layer;
            camera.scene = scene;
            camera.allowHDR = camera.allowMSAA = false;
            camera.targetTexture = target;
            ConfigureCanvas(host, camera);
        }

        private static void ConfigureCanvas(GameObject host, Camera camera)
        {
            Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(item => item.name == CoreGameplayDemoController.EditorPreviewCanvasName);
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.Expand)
                throw new InvalidOperationException("Unexpected production canvas scaling.");
            float scale = Mathf.Min(Width / scaler.referenceResolution.x, Height / scaler.referenceResolution.y);
            Vector2 logicalSize = new Vector2(Width / scale, Height / scale);
            scaler.enabled = false;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.scaleFactor = 1f;
            RectTransform root = canvas.GetComponent<RectTransform>();
            root.sizeDelta = logicalSize;
            root.position = Vector3.zero;
            root.localScale = Vector3.one;
            camera.orthographicSize = logicalSize.y * .5f;
            foreach (Transform item in canvas.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = Layer;
        }

        private static Page RenderPage(GameObject host, CoreGameplayDemoController controller, Camera camera, RenderTexture target, int port)
        {
            Canvas.ForceUpdateCanvases();
            if (GraphicsSettings.currentRenderPipeline != null)
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            else camera.Render();
            RawImage background = host.GetComponentsInChildren<RawImage>().Single(item => item.name == "Voyage Background");
            DepartureDockForegroundGraphic foreground = host.GetComponentInChildren<DepartureDockForegroundGraphic>();
            RawImage hull = host.GetComponentsInChildren<RawImage>().Single(item => item.name.EndsWith(" Base") || item.name.EndsWith(" Hull"));
            if (foreground == null || foreground.mainTexture != background.texture || hull.texture == background.texture)
                throw new InvalidOperationException("Independent vessel / regional foreground validation failed.");
            string file = Path.Combine(Output, Slugs[port] + "_1920x1080.png");
            SaveFrame(target, file);
            return new Page { port = controller.Session.CurrentPort, file = file,
                background = AssetDatabase.GetAssetPath(background.texture),
                foreground = AssetDatabase.GetAssetPath(foreground.mainTexture), hull = hull.texture.name, passed = true };
        }

        private static void SaveFrame(RenderTexture target, string file)
        {
            RenderTexture.active = target;
            var pixels = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            try
            {
                pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(file, pixels.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(pixels); }
        }

        private static Page CapturePort(int port)
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            GameObject host = null, cameraHost = null;
            RenderTexture target = null, previous = RenderTexture.active;
            try
            {
                host = new GameObject("Regional Departure Capture");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.VoyageDeparture, (CoreGameplayPreviewPort)port, 0);
                host.SetActive(true);
                if (!controller.HasCompleteEditorPreview) throw new InvalidOperationException("Incomplete departure preview.");
                cameraHost = new GameObject("Regional Departure Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                ConfigureCamera(host, scene, camera, target);
                return RenderPage(host, controller, camera, target, port);
            }
            finally
            {
                RenderTexture.active = previous;
                if (cameraHost != null) UnityEngine.Object.DestroyImmediate(cameraHost);
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
