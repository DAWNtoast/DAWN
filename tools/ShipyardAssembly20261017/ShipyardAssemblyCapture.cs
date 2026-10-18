// Install only in the isolated validation project's Editor directory.
// Builds production UI in disposable PreviewScenes. Never saves scenes or profiles.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    public static class ShipyardAssemblyCapture
    {
        private const int Layer = 31;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] public sealed class Report
        {
            public string capturedAtUtc = DateTime.UtcNow.ToString("O");
            public string projectPath;
            public string scope = "24 production fullscreen equipment frames: ships 0 and 4, empty / inventory / selected / filtered, 1920x1080, 1280x1024 and 2560x1080. Filtered reuses the Selected fixture with the Bow category filter active to inspect selected category, slot and inventory text contrast. Disposable Editor PreviewScenes with actual enabled CanvasScaler. Fixture inventory and selection are memory-only. Ship 4 remains the production unowned preview; its selected comparison is arranged directly for visual review. Verifies solid opaque full-canvas backing, market paper, original port header, and visible controls inside the paper below that header. Header/paper visibility is verified by rendering pixel differences when each is temporarily hidden. The page reference rectangle may include blank margin outside its visible content. Install/unload actions stay disabled in Editor. No scene, asset or PlayerPrefs saves.";
            public List<Frame> frames = new List<Frame>();
        }
        [Serializable] public sealed class Frame
        {
            public string file, shipName, variant, error, screenMatchMode;
            public int ship, width, height, inventoryCount, paperVisiblePixels, headerVisiblePixels;
            public bool scalerVerified, shipGeometryVerified, overlayVerified, contentVerified, paperVerified, headerVerified, installEnabled;
            public string paperSprite;
            public Color overlayColor;
            public float scaleFactor, expectedScaleFactor;
            public Vector2 displaySize, canvasSize;
            public Rect canvasPixelRect, cameraPixelRect, overlayScreen, paperScreen, headerScreen, detailScreen, pageScreen, visiblePageScreen, shipWindowLocal, shipArtLocal, shipWindowScreen, shipArtScreen;
            public List<Element> elements = new List<Element>();
            public List<string> contentViolations = new List<string>();
        }
        [Serializable] public sealed class Element
        {
            public string name, kind, text;
            public int fontSize;
            public bool raycastTarget;
            public Rect screenRect, localRect;
        }

        public static void CaptureFromCommandLine()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stopped Editor required.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Graphics required; omit -nographics.");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (!project.Replace('\\', '/').StartsWith("D:/Game/tmp/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Run this helper only in an isolated project below D:/Game/tmp.");
            string[] args = Environment.GetCommandLineArgs();
            int key = Array.IndexOf(args, "-shipyardAssemblyCapturePath");
            if (key < 0 || key + 1 >= args.Length || !Path.IsPathRooted(args[key + 1]))
                throw new ArgumentException("Pass -shipyardAssemblyCapturePath NEW_ABSOLUTE_ARTIFACTS_DIRECTORY.");
            string output = Path.GetFullPath(args[key + 1]);
            if (!output.Replace('\\', '/').StartsWith("D:/Game/Artifacts/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Capture directory must be below D:/Game/Artifacts.");
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Use a new or empty capture directory.");
            Directory.CreateDirectory(output);
            var report = new Report { projectPath = project };
            foreach (int ship in new[] { 4, 0 })
                foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 1024), new Vector2Int(2560, 1080) })
                    foreach (string variant in new[] { "Empty", "Inventory", "Selected", "Filtered" })
                        report.frames.Add(new Frame { ship = ship, shipName = CoreVoyageSession.Ships[ship].Name,
                            width = size.x, height = size.y, variant = variant,
                            file = "Ship_" + ship.ToString("D2") + "_" + variant + "_" + size.x + "x" + size.y + ".png" });
            foreach (Frame frame in report.frames)
            {
                try { Capture(output, frame); }
                catch (Exception exception) { frame.error = exception.ToString(); Debug.LogWarning(frame.file + ": " + exception.Message); }
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                Debug.Log("SHIPYARD_ASSEMBLY_FRAME: " + frame.file + " " + (string.IsNullOrEmpty(frame.error) ? "OK" : "FAILED"));
            }
            WriteGallery(output, report);
            if (report.frames.Any(frame => !string.IsNullOrEmpty(frame.error) || !frame.scalerVerified || !frame.shipGeometryVerified || !frame.overlayVerified || !frame.contentVerified || !frame.paperVerified || !frame.headerVerified))
                throw new InvalidOperationException("Fullscreen assembly capture incomplete; inspect report.json.");
            Debug.Log("SHIPYARD_ASSEMBLY_CAPTURE_OK: " + report.frames.Count + " frames in " + output);
        }

        private static void ArrangeFixture(CoreGameplayDemoController controller, Frame frame)
        {
            var catalog = new ComponentArtCatalog();
            int serial = 0;
            var inventory = new OwnedShipComponentInventory(catalog, () => "assembly-review-" + serial++);
            inventory.EnsureLoadout(0);
            if (frame.ship != 0) inventory.EnsureLoadout(frame.ship);
            OwnedShipComponent selected = null;
            if (frame.variant != "Empty")
            {
                selected = inventory.Grant(catalog.GetById("bow_freewake_lance"));
                foreach (ComponentSlot slot in new[] { ComponentSlot.Sail, ComponentSlot.Cannon, ComponentSlot.Plating })
                    inventory.Grant(catalog.Entries.First(entry => entry.Slot == slot && entry.Rarity == ComponentRarity.Rare));
            }
            Set(controller, "componentInventory", inventory);
            // Filtered keeps the Selected comparison while highlighting the Bow
            // category, installed slot and matching inventory row in all 6 views.
            Set(controller, "selectedShipyardComponentInstanceId", frame.variant == "Selected" || frame.variant == "Filtered" ? selected.InstanceId : null);
            Set(controller, "selectedShipyardComponentSlot", ComponentSlot.Bow);
            Set(controller, "shipyardInventoryShowAll", frame.variant != "Filtered");
            Set(controller, "shipyardInventoryPage", 0);
            // RefreshEditorPreview would replace the deterministic fixture.
            // Rebuild only production view objects using the current in-memory state.
            typeof(CoreGameplayDemoController).GetMethod("BuildSelectedEditorPreview", Private).Invoke(controller, null);
            frame.inventoryCount = inventory.Items.Count(item => item.ShipIndex < 0);
        }

        private static void Set(CoreGameplayDemoController controller, string name, object value)
        {
            FieldInfo field = typeof(CoreGameplayDemoController).GetField(name, Private);
            if (field == null) throw new MissingFieldException(name);
            field.SetValue(controller, value);
        }

        private static void Capture(string output, Frame frame)
        {
            Scene scene = default;
            RenderTexture target = null, previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                var host = new GameObject("Disposable Shipyard Assembly Review");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                var controller = host.AddComponent<CoreGameplayDemoController>();
                var port = (CoreGameplayPreviewPort)CoreShipMarketCatalog.HomePortIndex(frame.ship);
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Shipyard, port, 0);
                host.SetActive(true);
                controller.ConfigureEditorShipyard(frame.ship, true, 2);
                ArrangeFixture(controller, frame);
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(item => item.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (!scaler.enabled) throw new InvalidOperationException("Production CanvasScaler disabled.");
                foreach (Transform transform in canvas.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = Layer;
                var cameraHost = new GameObject("Disposable Shipyard Assembly Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false; camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black; camera.orthographic = true; camera.orthographicSize = 5f;
                camera.nearClipPlane = .1f; camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0, 0, -1000); camera.cullingMask = 1 << Layer;
                camera.allowHDR = camera.allowMSAA = false;
                target = new RenderTexture(frame.width, frame.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create(); camera.targetTexture = target; camera.aspect = frame.width / (float)frame.height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 100f;
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", Private);
                if (handle == null) throw new MissingMethodException("CanvasScaler.Handle");
                for (int pass = 0; pass < 4; pass++)
                {
                    Canvas.ForceUpdateCanvases(); handle.Invoke(scaler, null);
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                frame.displaySize = canvas.renderingDisplaySize;
                frame.canvasSize = canvas.GetComponent<RectTransform>().rect.size; frame.scaleFactor = canvas.scaleFactor;
                frame.canvasPixelRect = canvas.pixelRect; frame.cameraPixelRect = camera.pixelRect;
                float expectedScale = Mathf.Min(frame.width / scaler.referenceResolution.x, frame.height / scaler.referenceResolution.y);
                frame.expectedScaleFactor = expectedScale; frame.screenMatchMode = scaler.screenMatchMode.ToString();
                frame.scalerVerified = scaler.enabled && scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand &&
                    Mathf.Abs(frame.displaySize.x - frame.width) < .5f && Mathf.Abs(frame.displaySize.y - frame.height) < .5f &&
                    Mathf.Abs(frame.canvasPixelRect.width - frame.width) < .5f && Mathf.Abs(frame.canvasPixelRect.height - frame.height) < .5f &&
                    Mathf.Abs(frame.cameraPixelRect.width - frame.width) < .5f && Mathf.Abs(frame.cameraPixelRect.height - frame.height) < .5f &&
                    Mathf.Abs(frame.scaleFactor - expectedScale) < .001f && Mathf.Abs(frame.canvasSize.x - frame.width / expectedScale) < 1f &&
                    Mathf.Abs(frame.canvasSize.y - frame.height / expectedScale) < 1f;
                if (!frame.scalerVerified) throw new InvalidOperationException("Actual render target/scaler geometry mismatch.");
                RectTransform overlay = canvas.GetComponentsInChildren<RectTransform>().Single(item => item.name == "Shipyard Equipment Overlay");
                Image backdrop = overlay.GetComponent<Image>();
                frame.overlayScreen = ScreenBounds(camera, overlay);
                frame.overlayColor = backdrop.color;
                frame.overlayVerified = SameRect(frame.overlayScreen, new Rect(0f, 0f, frame.width, frame.height)) &&
                    backdrop.sprite == null && backdrop.color.a >= .999f && backdrop.raycastTarget;
                if (!frame.overlayVerified) throw new InvalidOperationException("Assembly overlay must be a full-canvas solid opaque raycast target.");
                Image paper = overlay.GetComponentsInChildren<Image>().Single(item => item.name == "Shipyard Equipment Paper");
                RectTransform header = canvas.GetComponentsInChildren<RectTransform>().Single(item => item.name == "Port Facility Header");
                Graphic[] headerGraphics = header.GetComponentsInChildren<Graphic>().Where(IsVisibleGraphic).ToArray();
                frame.paperScreen = ScreenBounds(camera, paper.rectTransform); frame.headerScreen = ScreenBounds(camera, header);
                frame.paperSprite = paper.sprite != null ? paper.sprite.name : null;
                frame.paperVerified = IsVisibleGraphic(paper) && paper.sprite != null && paper.sprite == MarketShopSkin.Paper &&
                    Contains(frame.overlayScreen, frame.paperScreen) && frame.paperScreen.yMax <= frame.headerScreen.yMin + .5f && paper.depth > backdrop.depth;
                frame.headerVerified = header.gameObject.activeInHierarchy && headerGraphics.Length > 0 &&
                    Contains(frame.overlayScreen, frame.headerScreen) && headerGraphics.All(graphic => graphic.depth > paper.depth && graphic.depth > backdrop.depth);
                if (!frame.paperVerified || !frame.headerVerified)
                    throw new InvalidOperationException("Market paper must sit beneath the visible original port header and render above the fullscreen backing.");
                RectTransform detail = canvas.GetComponentsInChildren<RectTransform>().Single(item => item.name == "Shipyard Detail");
                RectTransform page = detail.GetComponentsInChildren<RectTransform>().Single(item => item.name == "Shipyard Equipment Page");
                frame.detailScreen = ScreenBounds(camera, detail); frame.pageScreen = ScreenBounds(camera, page);
                var expectedContent = new Rect((frame.width - 1920f * expectedScale) * .5f,
                    (frame.height - 1080f * expectedScale) * .5f, 1920f * expectedScale, 1080f * expectedScale);
                Graphic[] visiblePage = page.GetComponentsInChildren<Graphic>().Where(IsVisibleGraphic).ToArray();
                Rect[] pageBounds = visiblePage.Select(graphic => ScreenBounds(camera, graphic.rectTransform)).ToArray();
                frame.visiblePageScreen = Union(pageBounds);
                Graphic[] visibleContent = detail.GetComponentsInChildren<Graphic>().Where(IsVisibleGraphic).ToArray();
                foreach (Graphic graphic in visibleContent)
                {
                    Rect bounds = ScreenBounds(camera, graphic.rectTransform);
                    if (!Contains(frame.paperScreen, bounds) || bounds.yMax > frame.headerScreen.yMin + .5f || graphic.depth <= paper.depth)
                        frame.contentViolations.Add(graphic.name + " screen=" + bounds + " depth=" + graphic.depth + " paperDepth=" + paper.depth);
                }
                frame.contentVerified = detail.parent == overlay && page.parent == detail &&
                    SameRect(frame.detailScreen, expectedContent) && visiblePage.Length > 0 &&
                    frame.contentViolations.Count == 0;
                if (!frame.contentVerified) throw new InvalidOperationException("Assembly controls and visible page content must fit inside the paper below the port header: " + string.Join("; ", frame.contentViolations.Take(8)));
                RectTransform window = detail.GetComponentsInChildren<RectTransform>().Single(item => item.name == "Shipyard Dossier Ship Window");
                RawImage ship = window.GetComponentsInChildren<RawImage>().Single(item => item.name ==
                    (frame.ship == 0 ? "Shipyard Dossier Modular Ship Base" : "Shipyard Dossier Ship Art " + frame.ship));
                frame.shipWindowLocal = LocalBounds(detail, window); frame.shipWindowScreen = ScreenBounds(camera, window);
                frame.shipArtLocal = LocalBounds(detail, ship.rectTransform); frame.shipArtScreen = ScreenBounds(camera, ship.rectTransform);
                frame.shipGeometryVerified = frame.shipArtLocal.width > 1f && frame.shipArtLocal.height > 1f &&
                    frame.shipArtLocal.xMin >= frame.shipWindowLocal.xMin - .02f && frame.shipArtLocal.xMax <= frame.shipWindowLocal.xMax + .02f &&
                    frame.shipArtLocal.yMin >= frame.shipWindowLocal.yMin - .02f && frame.shipArtLocal.yMax <= frame.shipWindowLocal.yMax + .02f;
                if (!frame.shipGeometryVerified) throw new InvalidOperationException("Vessel artwork must fit within the ship window.");
                frame.installEnabled = detail.GetComponentsInChildren<Button>().Single(item => item.name == "Shipyard Equip Component").interactable;
                if (frame.installEnabled) throw new InvalidOperationException("Editor preview unexpectedly allows installation.");
                foreach (Graphic graphic in detail.GetComponentsInChildren<Graphic>(false).Concat(headerGraphics).Concat(new Graphic[] { paper }).Distinct())
                {
                    Text text = graphic as Text;
                    frame.elements.Add(new Element { name = graphic.name, kind = graphic.GetType().Name,
                        text = text != null ? text.text : null, fontSize = text != null ? text.fontSize : 0,
                        raycastTarget = graphic.raycastTarget, screenRect = ScreenBounds(camera, graphic.rectTransform),
                        localRect = LocalBounds(detail, graphic.rectTransform) });
                }
                RenderTexture.active = target; pixels = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0); pixels.Apply(false, false);
                File.WriteAllBytes(Path.Combine(output, frame.file), pixels.EncodeToPNG());
                Color32[] baseline = pixels.GetPixels32();
                frame.paperVisiblePixels = VisiblePixelChanges(camera, target, baseline, frame.paperScreen, visible => paper.enabled = visible);
                frame.headerVisiblePixels = VisiblePixelChanges(camera, target, baseline, frame.headerScreen,
                    visible => { foreach (Graphic graphic in headerGraphics) graphic.enabled = visible; });
                frame.paperVerified &= frame.paperVisiblePixels > Mathf.Max(16f, frame.paperScreen.width * frame.paperScreen.height * .001f);
                frame.headerVerified &= frame.headerVisiblePixels > Mathf.Max(16f, frame.headerScreen.width * frame.headerScreen.height * .001f);
                if (!frame.paperVerified || !frame.headerVerified)
                    throw new InvalidOperationException("Paper or port header contributes no meaningful visible pixels to the captured frame.");
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }

        private static bool SameRect(Rect a, Rect b) => Mathf.Abs(a.x - b.x) < .02f && Mathf.Abs(a.y - b.y) < .02f &&
            Mathf.Abs(a.width - b.width) < .02f && Mathf.Abs(a.height - b.height) < .02f;
        private static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin - .5f && inner.xMax <= outer.xMax + .5f &&
            inner.yMin >= outer.yMin - .5f && inner.yMax <= outer.yMax + .5f;
        private static bool IsVisibleGraphic(Graphic graphic) => graphic.isActiveAndEnabled && graphic.color.a > .001f &&
            (!(graphic is Text text) || !string.IsNullOrEmpty(text.text));
        private static Rect Union(Rect[] rectangles) => rectangles.Length == 0 ? new Rect() : Rect.MinMaxRect(
            rectangles.Min(rect => rect.xMin), rectangles.Min(rect => rect.yMin), rectangles.Max(rect => rect.xMax), rectangles.Max(rect => rect.yMax));
        private static void RenderCamera(Camera camera, RenderTexture target)
        {
            Canvas.ForceUpdateCanvases();
            if (GraphicsSettings.currentRenderPipeline != null)
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            else camera.Render();
        }
        private static int VisiblePixelChanges(Camera camera, RenderTexture target, Color32[] baseline, Rect area, Action<bool> setVisible)
        {
            Texture2D hidden = null;
            try
            {
                setVisible(false); RenderCamera(camera, target);
                RenderTexture.active = target;
                hidden = new Texture2D(target.width, target.height, TextureFormat.RGB24, false, false);
                hidden.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); hidden.Apply(false, false);
                Color32[] hiddenPixels = hidden.GetPixels32();
                int changed = 0;
                for (int y = Mathf.Max(0, Mathf.CeilToInt(area.yMin)); y < Mathf.Min(target.height, Mathf.FloorToInt(area.yMax)); y++)
                    for (int x = Mathf.Max(0, Mathf.CeilToInt(area.xMin)); x < Mathf.Min(target.width, Mathf.FloorToInt(area.xMax)); x++)
                    {
                        int at = y * target.width + x;
                        Color32 before = baseline[at], after = hiddenPixels[at];
                        if (Mathf.Abs(before.r - after.r) + Mathf.Abs(before.g - after.g) + Mathf.Abs(before.b - after.b) > 12) changed++;
                    }
                return changed;
            }
            finally
            {
                setVisible(true); RenderCamera(camera, target);
                if (hidden != null) Object.DestroyImmediate(hidden);
            }
        }
        private static Rect LocalBounds(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            Vector3[] local = corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(local.Min(v => v.x), local.Min(v => v.y), local.Max(v => v.x), local.Max(v => v.y));
        }
        private static Rect ScreenBounds(Camera camera, RectTransform child)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            Vector3[] screen = corners.Select(camera.WorldToScreenPoint).ToArray();
            return Rect.MinMaxRect(screen.Min(v => v.x), screen.Min(v => v.y), screen.Max(v => v.x), screen.Max(v => v.y));
        }
        private static void WriteGallery(string output, Report report)
        {
            var html = new StringBuilder("<!doctype html><html lang=\"zh-CN\"><meta charset=\"utf-8\"><title>全屏配件装配</title><style>body{background:#0d1b20;color:#f0e6cf;font:16px system-ui;margin:28px}main{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:24px}figure{margin:0}img{width:100%;height:auto}a{color:#c5a36d}figcaption{padding:10px 0}p{color:#a3b4b0}</style><h1>全屏配件装配</h1><p>生产 UI、真实 CanvasScaler、全屏不透明底色、市场纸张与保留顶部栏。检查装配内容位于纸张内、顶部栏下；通过临时隐藏后的像素差异验证纸张和顶部栏实际可见。编辑器只读样本，选配状态通过内存构造；实际装配操作另由 PlayMode 测试验证。</p><main>");
            foreach (Frame frame in report.frames)
            {
                string caption = System.Net.WebUtility.HtmlEncode(frame.shipName + " · " + frame.variant + " · " + frame.width + "×" + frame.height);
                html.Append("<figure><a href=\"").Append(frame.file).Append("\"><img loading=\"lazy\" src=\"").Append(frame.file).Append("\"></a><figcaption>").Append(caption);
                if (!string.IsNullOrEmpty(frame.error)) html.Append(" · FAILED");
                html.Append("</figcaption></figure>");
            }
            html.Append("</main></html>");
            File.WriteAllText(Path.Combine(output, "index.html"), html.ToString(), new UTF8Encoding(false));
        }
    }
}
