using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    // This helper is installed only in the disposable validation project.
    // It renders the production UI and never confirms a purchase or writes player saves.
    public static class MarketSceneCapture
    {
        [Serializable] private sealed class Report
        {
            public bool passed;
            public string scope = "Actual production UI in private Editor PreviewScenes. 720p, 1080p, 1440p and 1280x1024 with the production Expand CanvasScaler. No transaction callbacks or save writes. Actual input and transactions are covered separately by PlayMode tests.";
            public List<Page> pages = new List<Page>();
        }
        [Serializable] private sealed class Page
        {
            public string state, file, port, background;
            public int width, height;
            public bool passed;
            public Vector2 referenceResolution;
            public Rect drawer;
            public List<Control> controls = new List<Control>();
            public List<string> errors = new List<string>();
        }
        [Serializable] private sealed class Control
        {
            public string name, label;
            public Rect bounds;
            public bool interactable;
        }

        public static void Run()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (!project.StartsWith("D:/Game/tmp/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Capture requires an isolated project under D:/Game/tmp.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Use stopped Editor with graphics enabled.");
            string output = Environment.GetEnvironmentVariable("MARKET_SCENE_CAPTURE_DIR");
            if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output)) throw new InvalidOperationException("Use a new capture directory.");
            Directory.CreateDirectory(output);
            var report = new Report();
            try
            {
                foreach (Vector2Int size in new[] {new Vector2Int(1280,720), new Vector2Int(1920,1080), new Vector2Int(2560,1440), new Vector2Int(1280,1024)})
                    foreach (string state in new[] {"Closed", "Supplies", "Medicine", "Shipwright", "Ordnance", "Inventory"})
                        Capture(report, output, state, size.x, size.y);
                report.passed = report.pages.Count == 24 && report.pages.All(page => page.passed);
            }
            finally { File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true)); }
            if (!report.passed) throw new InvalidOperationException("Market scene screenshot inspection failed; inspect report.json.");
            Debug.Log("MARKET_SCENE_CAPTURE_OK: " + report.pages.Count + " production frames; " + output);
        }

        private static void Capture(Report report, string output, string state, int width, int height)
        {
            var page = new Page { state = state, width = width, height = height, file = "market_" + state.ToLowerInvariant() + "_" + width + "x" + height + ".png" };
            report.pages.Add(page);
            Scene scene = EditorSceneManager.NewPreviewScene();
            GameObject host = null, cameraHost = null;
            RenderTexture target = null, previous = RenderTexture.active;
            try
            {
                host = new GameObject("Market Scene Capture Host");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Market, CoreGameplayPreviewPort.GrayGull, 0);
                host.SetActive(true);
                if (!controller.HasCompleteEditorPreview) throw new InvalidOperationException("Production preview incomplete.");
                if (state != "Closed")
                {
                    Invoke(controller, "OpenMarketShop", Enum.Parse(typeof(MarketShopKind), state == "Inventory" ? "Supplies" : state));
                    if (state == "Inventory")
                    {
                        typeof(CoreGameplayDemoController).GetField("marketInventoryExpanded", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, true);
                        Invoke(controller, "RefreshMarketFacilityContents");
                    }
                }
                page.port = controller.Session.CurrentPort;
                cameraHost = new GameObject("Market Scene Capture Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.aspect = width / (float)height;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0,0,-1000);
                camera.cullingMask = 1 << 31;
                camera.scene = scene;
                camera.allowHDR = camera.allowMSAA = false;
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                camera.targetTexture = target;
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(item => item.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize || scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.Expand)
                    throw new InvalidOperationException("Unexpected production CanvasScaler.");
                page.referenceResolution = scaler.referenceResolution;
                float scale = Mathf.Min(width / scaler.referenceResolution.x, height / scaler.referenceResolution.y);
                Vector2 logicalSize = new Vector2(width / scale, height / scale);
                scaler.enabled = false;
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                canvas.scaleFactor = 1;
                RectTransform root = canvas.GetComponent<RectTransform>();
                root.sizeDelta = logicalSize;
                root.position = Vector3.zero;
                root.localScale = Vector3.one;
                camera.orthographicSize = logicalSize.y * .5f;
                foreach (Transform item in canvas.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 31;
                Canvas.ForceUpdateCanvases();
                if (GraphicsSettings.currentRenderPipeline != null)
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                else camera.Render();

                RectTransform drawer = host.GetComponentsInChildren<RectTransform>(false).FirstOrDefault(item => item.name == "Market Item Detail");
                if ((drawer != null) != (state != "Closed")) page.errors.Add("Unexpected drawer visibility.");
                if (drawer != null)
                {
                    page.drawer = Bounds(drawer, camera);
                    bool right = state == "Supplies" || state == "Medicine" || state == "Inventory";
                    if (right != (page.drawer.center.x > width * .5f)) page.errors.Add("Drawer appears on wrong side.");
                }
                foreach (Button button in host.GetComponentsInChildren<Button>(false))
                {
                    Rect bounds = Bounds(button.GetComponent<RectTransform>(), camera);
                    page.controls.Add(new Control { name = button.name, label = button.GetComponentInChildren<Text>()?.text,
                        bounds = bounds, interactable = button.interactable });
                    if (bounds.xMin < -1 || bounds.yMin < -1 || bounds.xMax > width + 1 || bounds.yMax > height + 1)
                        page.errors.Add("Control outside screen: " + button.name);
                }
                foreach (string name in new[] {"Market Shop Supplies", "Market Shop Medicine", "Market Shop Shipwright", "Market Shop Ordnance", "Return To Port Hub"})
                    if (!page.controls.Any(item => item.name == name)) page.errors.Add("Missing live control: " + name);
                foreach (string name in new[] {"Market Footer", "Market Title", "Market Summary", "Market Storage Mode", "Market Open Investment"})
                    if (host.GetComponentsInChildren<Transform>(true).Any(item => item.name == name))
                        page.errors.Add("Removed footer content remains: " + name);
                if (drawer != null && !page.controls.Any(item => item.name == "Market Detail Storage Mode"))
                    page.errors.Add("Missing drawer delivery control.");
                RawImage backdrop = host.GetComponentsInChildren<RawImage>(true).FirstOrDefault(item => item.texture != null && item.texture.name == "market_scene_background_v2");
                page.background = backdrop == null ? "MISSING" : AssetDatabase.GetAssetPath(backdrop.texture);
                if (backdrop == null) page.errors.Add("Generated market background was not loaded.");
                RenderTexture.active = target;
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                try
                {
                    texture.ReadPixels(new Rect(0,0,width,height),0,0);
                    texture.Apply();
                    File.WriteAllBytes(Path.Combine(output,page.file),texture.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
                page.passed = page.errors.Count == 0;
            }
            catch (Exception e) { page.errors.Add(e.ToString()); page.passed = false; }
            finally
            {
                RenderTexture.active = previous;
                if (cameraHost != null) UnityEngine.Object.DestroyImmediate(cameraHost);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        private static Rect Bounds(RectTransform rect, Camera camera)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue,float.MaxValue), max = new Vector2(float.MinValue,float.MinValue);
            foreach (Vector3 corner in corners) { Vector2 point = camera.WorldToScreenPoint(corner); min = Vector2.Min(min,point); max = Vector2.Max(max,point); }
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        private static object Invoke(object instance, string name, params object[] args) => typeof(CoreGameplayDemoController)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).Single(method => method.Name == name && method.GetParameters().Length == args.Length)
            .Invoke(instance,args);
    }
}
