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
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    // Staged into the disposable validation project only. Never saves scenes or assets.
    public static class DepartureDockOpenBerthCapture
    {
        private const int Width = 1920, Height = 1080, Layer = 31, ThumbWidth = 480, ThumbHeight = 270;
        private static readonly string[] Slugs = { "broken_anchor", "white_shell", "bronze_bell", "salt_crown", "twilight_tide", "gray_gull" };
        private static readonly MethodInfo Render = typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic);
        [Serializable] private sealed class Report
        {
            public string createdUtc;
            public int expectedPages;
            public int[] selectedPorts, selectedShips;
            public List<Page> pages = new List<Page>();
        }
        [Serializable] private sealed class Page
        {
            public string port, ship, file, background, foreground, hull;
            public int shipIndex, backgroundWidth, backgroundHeight;
            public float waterlineError, waterEffectContactError;
            public Color tint;
            public bool passed;
        }
        public static void Capture()
        {
            string data = Application.dataPath.Replace('\\', '/');
            if (!data.StartsWith("D:/Game/tmp/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Use only a disposable validation project under D:/Game/tmp.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Capture requires a stopped editor with graphics enabled.");
            string[] arguments = Environment.GetCommandLineArgs();
            int outputIndex = Array.IndexOf(arguments, "-dockCaptureOutput");
            if (outputIndex < 0 || outputIndex + 1 >= arguments.Length) throw new InvalidOperationException("Specify -dockCaptureOutput.");
            string output = Path.GetFullPath(arguments[outputIndex + 1]);
            if (!output.Replace('\\', '/').StartsWith("D:/Game/Artifacts/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Capture output must stay under repository Artifacts.");
            Directory.CreateDirectory(output);
            int[] ports = Selection(arguments, "-dockCapturePorts", Slugs.Length);
            int[] ships = Selection(arguments, "-dockCaptureShips", CoreVoyageSession.Ships.Count);
            var report = new Report { createdUtc = DateTime.UtcNow.ToString("o"), expectedPages = ports.Length * ships.Length, selectedPorts = ports, selectedShips = ships };
            foreach (int port in ports) CapturePort(port, ships, output, report);
            File.WriteAllText(Path.Combine(output, "capture-report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("DEPARTURE_OPEN_BERTH_CAPTURE: " + report.pages.Count + " current-source production UI screenshots saved to " + output);
        }

        private static int[] Selection(string[] arguments, string name, int count)
        {
            int index = Array.IndexOf(arguments, name);
            if (index < 0 || index + 1 >= arguments.Length || arguments[index + 1] == "all") return Enumerable.Range(0, count).ToArray();
            int[] result = arguments[index + 1].Split(',').Select(int.Parse).Distinct().ToArray();
            if (result.Length == 0 || result.Any(item => item < 0 || item >= count)) throw new InvalidOperationException("Invalid " + name + " selection.");
            return result;
        }

        private static void CapturePort(int port, int[] ships, string output, Report report)
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            GameObject host = null, cameraHost = null;
            RenderTexture target = null, previous = RenderTexture.active;
            int columns = Mathf.Min(4, ships.Length), rows = Mathf.CeilToInt(ships.Length / (float)columns);
            var sheet = new Texture2D(ThumbWidth * columns, ThumbHeight * rows, TextureFormat.RGBA32, false);
            try
            {
                cameraHost = new GameObject("Open Berth Validation Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
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
                for (int slot = 0; slot < ships.Length; slot++)
                {
                    int shipIndex = ships[slot];
                    host = new GameObject("Open Berth Capture " + port + " " + shipIndex);
                    host.SetActive(false);
                    SceneManager.MoveGameObjectToScene(host, scene);
                    var controller = host.AddComponent<CoreGameplayDemoController>();
                    controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.VoyageDeparture, (CoreGameplayPreviewPort)port, 0);
                    host.SetActive(true);
                    if (!controller.HasCompleteEditorPreview) throw new InvalidOperationException("Incomplete departure preview.");
                    controller.Session.TryDepositAllPortCargo();
                    if (!controller.Session.SelectShip(shipIndex)) throw new InvalidOperationException("Cannot preview ship " + shipIndex);
                    Render.Invoke(controller, null);
                    ConfigureCanvas(host, camera);
                    Canvas.ForceUpdateCanvases();
                    // Batch capture has no normal frame between prefab creation, font atlas
                    // growth and rendering; refresh the labels against the completed atlas.
                    for (int pass = 0; pass < 2; pass++)
                    {
                        foreach (Text label in host.GetComponentsInChildren<Text>()) label.SetAllDirty();
                        Canvas.ForceUpdateCanvases();
                    }
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                    report.pages.Add(ValidateAndSave(host, controller, target, sheet, port, shipIndex, slot, columns, rows, output));
                    UnityEngine.Object.DestroyImmediate(host);
                    host = null;
                }
                sheet.Apply();
                string suffix = ships.Length == CoreVoyageSession.Ships.Count ? "_all_12_ships" : "_ships_" + string.Join("_", ships.Select(item => item.ToString("D2")));
                File.WriteAllBytes(Path.Combine(output, Slugs[port] + suffix + ".png"), sheet.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (cameraHost != null) UnityEngine.Object.DestroyImmediate(cameraHost);
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                UnityEngine.Object.DestroyImmediate(sheet);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void ConfigureCanvas(GameObject host, Camera camera)
        {
            Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(item => item.name == CoreGameplayDemoController.EditorPreviewCanvasName);
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.Expand) throw new InvalidOperationException("Unexpected production canvas scaling.");
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

        private static Page ValidateAndSave(GameObject host, CoreGameplayDemoController controller, RenderTexture target,
            Texture2D sheet, int port, int shipIndex, int slot, int columns, int rows, string output)
        {
            RawImage background = Find<RawImage>(host, "Voyage Background");
            DepartureDockForegroundGraphic foreground = Find<DepartureDockForegroundGraphic>(host, "Departure Dock Foreground");
            RectTransform ship = Find<RectTransform>(host, "Starter Modular Ship");
            RawImage hull = ship.GetComponentsInChildren<RawImage>().Single(item => item.name.EndsWith(" Base") || item.name.EndsWith(" Hull"));
            RectTransform water = Find<RectTransform>(host, "Departure Dock Water Contact");
            float waterlineError = Mathf.Abs(water.InverseTransformPoint(hull.rectTransform.TransformPoint(ShipWaterlineLayout.HullContactLocal(hull))).y);
            if (foreground.mainTexture != background.texture || hull.texture == background.texture || waterlineError > .1f)
                throw new InvalidOperationException("Independent vessel / regional foreground / waterline validation failed.");
            RectTransform inspect = Find<RectTransform>(host, "Inspect Departure Vessel");
            float waterEffectContactError = 0f;
            foreach (string name in new[] { "Departure Dock Water Blend", "Departure Dock Water Ripple" })
            {
                DepartureDockWaterContactGraphic effect = Find<DepartureDockWaterContactGraphic>(host, name);
                Vector2 contact = effect.rectTransform.InverseTransformPoint(hull.rectTransform.TransformPoint(ShipWaterlineLayout.HullContactLocal(hull)));
                waterEffectContactError = Mathf.Max(waterEffectContactError, Vector2.Distance(effect.HullContact, contact));
                bool ripple = name.EndsWith("Ripple");
                if (effect.transform.parent != inspect.parent || effect.raycastTarget || effect.IsRippleLayer != ripple ||
                    effect.HullWidth <= 0f || waterEffectContactError > .1f || (!ripple && effect.mainTexture != background.texture) ||
                    effect.transform.GetSiblingIndex() <= inspect.GetSiblingIndex() || effect.transform.GetSiblingIndex() >= foreground.transform.GetSiblingIndex())
                    throw new InvalidOperationException(name + " is not aligned with the current vessel and regional background.");
            }
            foreach (string name in new[] { "Departure Dock Vessel Controls", "Departure Dock Crew Readout Panel" })
            {
                Image panel = Find<Image>(host, name);
                if (panel.raycastTarget || BoundsIn(inspect.parent, panel.rectTransform).Overlaps(BoundsIn(inspect.parent, inspect)))
                    throw new InvalidOperationException(name + " must remain outside the ship's water-space hit region.");
            }
            string file = Path.Combine(output, Slugs[port] + "_ship_" + shipIndex.ToString("D2") + "_1920x1080.png");
            RenderTexture.active = target;
            var pixels = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            try
            {
                pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(file, pixels.EncodeToPNG());
                Color32[] source = pixels.GetPixels32(), thumbnail = new Color32[ThumbWidth * ThumbHeight];
                for (int y = 0; y < ThumbHeight; y++)
                    for (int x = 0; x < ThumbWidth; x++) thumbnail[y * ThumbWidth + x] = source[(y * 4 + 2) * Width + x * 4 + 2];
                sheet.SetPixels32(slot % columns * ThumbWidth, (rows - 1 - slot / columns) * ThumbHeight, ThumbWidth, ThumbHeight, thumbnail);
            }
            finally { UnityEngine.Object.DestroyImmediate(pixels); }
            return new Page { port = controller.Session.CurrentPort, ship = controller.Session.SelectedShip.Name, shipIndex = shipIndex, file = file,
                background = AssetDatabase.GetAssetPath(background.texture), foreground = AssetDatabase.GetAssetPath(foreground.mainTexture),
                hull = hull.texture.name, backgroundWidth = background.texture.width, backgroundHeight = background.texture.height,
                waterlineError = waterlineError, waterEffectContactError = waterEffectContactError, tint = hull.color, passed = true };
        }

        private static T Find<T>(GameObject host, string name) where T : Component => host.GetComponentsInChildren<T>().Single(item => item.name == name);
        private static Rect BoundsIn(Transform parent, RectTransform child)
        {
            Vector3[] corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector2 min = parent.InverseTransformPoint(corners[0]), max = min;
            foreach (Vector3 corner in corners) { Vector2 point = parent.InverseTransformPoint(corner); min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
