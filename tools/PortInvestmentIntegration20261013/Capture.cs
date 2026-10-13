using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    // Install only in the isolated validation project. All balances here are
    // in-memory art fixtures, not gameplay progress; no transaction is invoked.
    public static class InvestmentSceneCapture
    {
        [Serializable] private sealed class Report
        {
            public bool passed;
            public string scope = "Actual production investment UI in private Editor PreviewScenes, using Expand scaling at 1080p, 4K and 16:10. Six port labels, affordability, shortfall and exact large balances. GraphicRaycaster probes invoke no callbacks. Editor preview intentionally disables purchases; live PlayMode tests cover transactions, six-row rare tiers and maximum level.";
            public List<Page> pages = new List<Page>();
        }
        [Serializable] private sealed class Page
        {
            public string state, port, file, background;
            public int width, height;
            public bool passed;
            public List<Label> labels = new List<Label>();
            public List<Probe> probes = new List<Probe>();
            public List<string> errors = new List<string>();
        }
        [Serializable] private sealed class Label
        {
            public string name, text;
            public Rect bounds;
            public float pixelFontSize;
        }
        [Serializable] private sealed class Probe
        {
            public string target, hit;
            public Vector2 point;
            public bool passed;
        }

        public static void Run()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (!string.Equals(project, "D:/Game/tmp/port-investment-layout-20261011", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Requires the dedicated port-investment validation project.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Use stopped Editor with graphics enabled.");
            string[] args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-investmentCapturePath");
            string output = option >= 0 && option + 1 < args.Length ? args[option + 1] : null;
            if (string.IsNullOrWhiteSpace(output) || !Path.IsPathRooted(output) || Directory.Exists(output))
                throw new InvalidOperationException("Supply a new absolute -investmentCapturePath directory.");
            Directory.CreateDirectory(output);
            var report = new Report();
            try
            {
                foreach (string state in new[] { "Affordable", "Shortfall", "LargeBalances" })
                    Capture(report, output, state, CoreGameplayPreviewPort.BrokenAnchor, 1920, 1080);
                Capture(report, output, "Affordable", CoreGameplayPreviewPort.BrokenAnchor, 3840, 2160);
                Capture(report, output, "Affordable", CoreGameplayPreviewPort.BrokenAnchor, 1280, 800);
                foreach (CoreGameplayPreviewPort port in new[] { CoreGameplayPreviewPort.WhiteShell, CoreGameplayPreviewPort.BronzeBell,
                    CoreGameplayPreviewPort.SaltCrown, CoreGameplayPreviewPort.TwilightTide, CoreGameplayPreviewPort.GrayGull })
                    Capture(report, output, "Affordable", port, 1920, 1080);
                report.passed = report.pages.Count == 10 && report.pages.All(page => page.passed);
            }
            finally { File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true)); }
            if (!report.passed) throw new InvalidOperationException("Investment scene capture validation failed; inspect report.json.");
            Debug.Log("INVESTMENT_SCENE_CAPTURE_OK: " + report.pages.Count + " verified frames; " + output);
        }

        private static void Capture(Report report, string output, string state, CoreGameplayPreviewPort port, int width, int height)
        {
            var page = new Page { state = state, port = port.ToString(), width = width, height = height,
                file = "investment_" + port.ToString().ToLowerInvariant() + "_" + state.ToLowerInvariant() + "_" + width + "x" + height + ".png" };
            report.pages.Add(page);
            Scene scene = EditorSceneManager.NewPreviewScene();
            GameObject host = null, cameraHost = null, eventHost = null;
            RenderTexture target = null, previous = RenderTexture.active;
            try
            {
                host = new GameObject("Investment Scene Capture Host"); host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Maintenance, port, 0);
                host.SetActive(true);
                if (!controller.HasCompleteEditorPreview) throw new InvalidOperationException("Production preview incomplete.");
                Arrange(controller, state);
                typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                string balances = Snapshot(controller.Session);
                cameraHost = new GameObject("Investment Capture Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.aspect = width / (float)height;
                camera.nearClipPlane = .1f; camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0, 0, -1000); camera.cullingMask = 1 << 31;
                camera.scene = scene; camera.allowHDR = camera.allowMSAA = false;
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create(); camera.targetTexture = target;
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(item => item.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize || scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.Expand)
                    throw new InvalidOperationException("Unexpected production CanvasScaler.");
                float scale = Mathf.Min(width / scaler.referenceResolution.x, height / scaler.referenceResolution.y);
                Vector2 logicalSize = new Vector2(width / scale, height / scale);
                scaler.enabled = false; canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera; canvas.scaleFactor = 1;
                RectTransform root = canvas.GetComponent<RectTransform>(); root.sizeDelta = logicalSize;
                root.position = Vector3.zero; root.localScale = Vector3.one;
                camera.orthographicSize = logicalSize.y * .5f;
                foreach (Transform item in canvas.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 31;
                eventHost = new GameObject("Investment Capture EventSystem", typeof(EventSystem));
                SceneManager.MoveGameObjectToScene(eventHost, scene);
                Canvas.ForceUpdateCanvases();
                if (GraphicsSettings.currentRenderPipeline != null)
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                else camera.Render();
                Validate(page, controller, camera, scale, canvas.GetComponent<GraphicRaycaster>(), eventHost.GetComponent<EventSystem>());
                if (Snapshot(controller.Session) != balances) page.errors.Add("Read-only capture changed the arranged balances.");
                RenderTexture.active = target;
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                try
                {
                    texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                    File.WriteAllBytes(Path.Combine(output, page.file), texture.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
                page.passed = page.errors.Count == 0;
            }
            catch (Exception error) { page.errors.Add(error.ToString()); page.passed = false; }
            finally
            {
                RenderTexture.active = previous;
                if (eventHost != null) UnityEngine.Object.DestroyImmediate(eventHost);
                if (cameraHost != null) UnityEngine.Object.DestroyImmediate(cameraHost);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void Arrange(CoreGameplayDemoController controller, string state)
        {
            CoreVoyageSession session = controller.Session;
            string port = session.CurrentPort;
            PortInvestmentCost cost = PortInvestmentCatalog.CostForLevel(port, 1);
            session.RestoreProfile(state == "LargeBalances" ? 2147483646.4321m : 2100.4321m, port);
            session.Cargo.Wood = 3; session.Cargo.Iron = 4;
            typeof(VoyageCargo).GetProperty(cost.RequiredItem.ToString()).SetValue(session.Cargo, state == "Shortfall" ? 2 : 3);
            int large = state == "LargeBalances" ? int.MaxValue : 80;
            string special = PortMarketCatalog.Key(cost.RequiredItem);
            int specialStored = state == "Shortfall" ? cost.RequiredQuantity - 3 : large;
            if (!session.PortWarehouse.Restore("v2;" + port + ",wood," + large + ";" + port + ",iron," + large + ";" + port + "," + special + "," + specialStored))
                throw new InvalidOperationException("Invalid capture inventory fixture.");
        }

        private static void Validate(Page page, CoreGameplayDemoController controller, Camera camera, float scale,
            GraphicRaycaster raycaster, EventSystem events)
        {
            Transform root = controller.transform;
            RawImage background = root.GetComponentsInChildren<RawImage>(false).Single(item => item.name == "Maintenance Background");
            page.background = AssetDatabase.GetAssetPath(background.texture);
            if (background.texture == null || background.texture.name != "port_investment_scene_internal_v1" || background.uvRect != new Rect(0, 0, 1, 1))
                page.errors.Add("The approved scene must render with complete UV coordinates.");
            foreach (Text label in root.GetComponentsInChildren<Text>(false).Where(item => item.name.StartsWith("Port Investment ")))
            {
                Rect bounds = Bounds(label.rectTransform, camera);
                page.labels.Add(new Label { name = label.name, text = label.text, bounds = bounds, pixelFontSize = label.fontSize * scale });
                if (label.preferredWidth > label.rectTransform.rect.width + .5f && label.horizontalOverflow == HorizontalWrapMode.Overflow)
                    page.errors.Add("Text exceeds its column: " + label.name + " / " + label.text);
                if (label.preferredHeight > label.rectTransform.rect.height + .5f && !label.resizeTextForBestFit)
                    page.errors.Add("Text exceeds its line height: " + label.name);
                if (bounds.xMin < -1 || bounds.yMin < -1 || bounds.xMax > page.width + 1 || bounds.yMax > page.height + 1)
                    page.errors.Add("Label outside screen: " + label.name);
            }
            foreach (string name in new[] { "Confirm Port Investment", "Open Port Market", "Open Investment Warehouse", "Return To Port Hub" })
            {
                Button button = root.GetComponentsInChildren<Button>(false).Single(item => item.name == name);
                Rect bounds = Bounds(button.GetComponent<RectTransform>(), camera);
                var probe = new Probe { target = name, point = bounds.center }; page.probes.Add(probe);
                var results = new List<RaycastResult>();
                raycaster.Raycast(new PointerEventData(events) { position = probe.point }, results);
                Button hit = results.Count == 0 ? null : results[0].gameObject.GetComponentInParent<Button>();
                probe.hit = hit == null ? "NONE" : hit.name; probe.passed = hit == button;
                if (!probe.passed) page.errors.Add("Control is obscured: " + name + " hit " + probe.hit);
                if (bounds.xMin < -1 || bounds.yMin < -1 || bounds.xMax > page.width + 1 || bounds.yMax > page.height + 1)
                    page.errors.Add("Control outside screen: " + name);
            }
            Text ready = root.GetComponentsInChildren<Text>(false).Single(item => item.name == "Port Investment Readiness");
            if (ready.text.Contains("材料不足") != (page.state == "Shortfall")) page.errors.Add("Readiness differs from the arranged resource state.");
            Text project = root.GetComponentsInChildren<Text>(false).Single(item => item.name == "Port Investment Project Name");
            if (!project.text.Contains(controller.Session.CurrentPort) || !project.text.Contains(PortInvestmentCatalog.NameFor(controller.Session.CurrentPort)))
                page.errors.Add("Project plaque does not match the current port.");
        }

        private static string Snapshot(CoreVoyageSession session) => session.ProfileGold + "|" + session.PortWarehouse.Serialize() + "|" +
            string.Join(",", CoreWarehouseItemCatalog.All.Select(item => item.Key + "=" + session.GetWarehouseCargoAmount(item.Key)));
        private static Rect Bounds(RectTransform rect, Camera camera)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in corners) { Vector2 point = camera.WorldToScreenPoint(corner); min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
