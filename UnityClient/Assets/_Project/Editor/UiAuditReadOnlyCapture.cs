// Temporary audit helper only. Install in an isolated validation project's Editor folder.
// Never saves scenes/assets and never enters PlayMode or invokes gameplay callbacks.
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
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    public static class UiAuditReadOnlyCapture
    {
        private const string Source = "Assets/_Project/Scenes/UIPreviewsForMe";
        private const int Layer = 31;
        [Serializable] public sealed class MenuReport
        {
            public string scope = "Current MainMenuController, disposable Editor PreviewScenes, actual enabled CanvasScaler and ScreenSpaceCamera target. No manual canvas scale/size assignment. Accepted only if renderingDisplaySize, pixelRect and scaleFactor match requested target. Text geometry includes all active labels, including Home labels behind an open modal; it is not an occlusion-aware accessibility test.";
            public string capturedAtUtc;
            public List<MenuFrame> frames = new List<MenuFrame>();
        }
        [Serializable] public sealed class MenuFrame
        {
            public string state, variant, file, error;
            public int width, height;
            public bool verifiedScaler;
            public Vector2 renderingDisplaySize, logicalCanvasSize;
            public Rect canvasPixelRect, cameraPixelRect;
            public float scaleFactor, expectedScaleFactor;
            public string screenMatchMode;
            public List<TextGeometry> text = new List<TextGeometry>();
            public List<PaperFrame> papers = new List<PaperFrame>();
        }
        [Serializable] public sealed class PaperFrame
        {
            public string path, texture, coveredBy;
            public Rect paperRect, safeRect;
            public List<PaperContent> content = new List<PaperContent>();
        }
        [Serializable] public sealed class PaperContent
        {
            public string path, kind;
            public Rect visibleBounds;
            public bool touchesFrame;
        }
        [Serializable] public sealed class TextGeometry
        {
            public string path, content;
            public bool meshAvailable, meshOutsideTarget, rectOutsideTarget;
            public Rect rectPixels, meshPixels;
            public int meshVertexCount, fontSize;
        }

        public static void CaptureAuthoredFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            RequireEditor();
            string output = Output();
            string project = Directory.GetParent(Application.dataPath).FullName;
            string[] top = Directory.GetFiles(Path.Combine(project, Source), "UI_*.unity", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            string[] references = top.Concat(new[] {"UIBattle/UIBattle_BoardingHook.unity", "UIBattle/UIBattle_BoardingLadder.unity", "UIBattle/UIBattle_BoardingRam.unity"}).ToArray();
            MethodInfo method = typeof(UIPreviewSceneAcceptance).GetMethod("Capture", BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(string), typeof(string), typeof(string[]) }, null);
            if (method == null) throw new MissingMethodException("UIPreviewSceneAcceptance.Capture(string,string,string[])");
            try { method.Invoke(null, new object[] { output, Source, references }); }
            catch (TargetInvocationException e) { throw e.InnerException ?? e; }
            Debug.Log("UI_AUDIT_AUTHORED_OK: " + references.Length + " frames at " + output);
        }

        public static void CaptureMenusFromCommandLine()
        {
            RequireEditor();
            string output = Output();
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Output must be new/empty: " + output);
            Directory.CreateDirectory(output);
            var report = new MenuReport { capturedAtUtc = DateTime.UtcNow.ToString("O") };
            int[,] resolutions = { {1920,1080}, {1280,720}, {1280,1024}, {3440,1440}, {3840,1080} };
            for (int i = 0; i < resolutions.GetLength(0); i++)
                foreach (string state in new[] {"Home", "Settings", "Achievements"})
                {
                    var frame = new MenuFrame { state = state, width = resolutions[i,0], height = resolutions[i,1] };
                    frame.file = state + "_" + frame.width + "x" + frame.height + ".png";
                    report.frames.Add(frame);
                    try { CaptureMenu(output, frame); }
                    catch (Exception e) { frame.error = e.ToString(); Debug.LogWarning(frame.file + ": " + e.Message); }
                    File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
                }
            if (report.frames.Any(f => !string.IsNullOrEmpty(f.error)))
                throw new InvalidOperationException("Some menu targets were not verified. Inspect report.json; do not classify them as passed.");
            Debug.Log("UI_AUDIT_MENUS_OK: " + report.frames.Count + " frames at " + output);
        }

        public static void CaptureFreshShopFromCommandLine()
        {
            RequireEditor();
            string output = Output();
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Output must be empty.");
            Directory.CreateDirectory(output);
            var frame = new MenuFrame { state = "ComponentShop", width = 1920, height = 1080, file = "ComponentShop_Fresh.png" };
            CaptureMenu(output, frame);
            File.WriteAllText(Path.Combine(output,"report.json"), JsonUtility.ToJson(frame,true));
        }

        public static void CaptureFreshScreensFromCommandLine()
        {
            RequireEditor();
            string output = Output();
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Output must be empty.");
            Directory.CreateDirectory(output);
            var report = new MenuReport { capturedAtUtc = DateTime.UtcNow.ToString("O") };
            foreach (CoreGameplayPreviewScreen screen in Enum.GetValues(typeof(CoreGameplayPreviewScreen)))
                foreach (Vector2Int size in new[] {new Vector2Int(1920,1080),new Vector2Int(1280,720)})
                {
                    var frame = new MenuFrame { state = screen.ToString(), width = size.x, height = size.y,
                        file = screen + "_" + size.x + "x" + size.y + ".png" };
                    report.frames.Add(frame);
                    try { CaptureMenu(output, frame); }
                    catch (Exception e) { frame.error = e.ToString(); Debug.LogWarning(frame.file + ": " + e.Message); }
                    File.WriteAllText(Path.Combine(output,"report.json"), JsonUtility.ToJson(report,true));
                }
            if (report.frames.Any(f => !string.IsNullOrEmpty(f.error))) throw new InvalidOperationException("Fresh UI capture failed; inspect report.");
        }

        public static void CaptureParchmentFramesFromCommandLine()
        {
            RequireEditor();
            string output = Output();
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Output must be new/empty.");
            Directory.CreateDirectory(output);
            var report = new MenuReport { capturedAtUtc = DateTime.UtcNow.ToString("O"),
                scope = "Disposable current-code Editor previews, actual CanvasScaler at 1920x1080, 1280x720 and 1280x1024. Includes paper sibling backdrops, rendered glyph meshes, full control bounds, and scroll clipping. Safe areas project the authored inner rules through the current nine-slice geometry with six logical pixels of clearance. Gameplay is not advanced and no saves are written. Hidden/occluded screens and GM require separate verification." };
            var states = Enum.GetNames(typeof(CoreGameplayPreviewScreen)).Select(s => new[] { s, "" }).ToList();
            states.AddRange(new[] {
                new[] { "Achievements", "" }, new[] { "Settings", "" },
                new[] { "NewGameConfirm", "" }, new[] { "SaveNotice", "" },
                new[] { "CardUpgrade", "LastPage" },
                new[] { "VoyageDeparture", "Crew" }, new[] { "VoyageDeparture", "TreasureSeed" },
                new[] { "VoyageDeparture", "TreasureSeedFull" }, new[] { "VoyageDeparture", "ChartWarning" },
                new[] { "TreasureMap", "Folio" }, new[] { "TreasureMap", "History" },
                new[] { "TreasureMap", "FolioDecoded" }, new[] { "TreasureMap", "FolioReceipt" },
                new[] { "Warehouse", "Storage" }, new[] { "Warehouse", "Rare" },
                new[] { "Warehouse", "Catalog" }, new[] { "Warehouse", "CatalogLast" },
                new[] { "Market", "MarketDetail" }, new[] { "Market", "MarketInventory" },
                new[] { "Market", "MarketBuy" },
                new[] { "Market", "MarketSell" }, new[] { "Market", "MarketChoice" },
                new[] { "Warehouse", "Actions" }, new[] { "Warehouse", "Adjustment" }, new[] { "Warehouse", "Sale" },
                new[] { "Navigation", "Cargo" }, new[] { "PortHub", "ReturnMenu" },
                new[] { "Navigation", "StoryFirst" }, new[] { "Navigation", "StorySecond" }
            });
            foreach (string[] state in states)
                foreach (Vector2Int size in new[] { new Vector2Int(1920,1080), new Vector2Int(1280,720), new Vector2Int(1280,1024) })
                {
                    var frame = new MenuFrame { state = state[0], variant = state[1], width = size.x, height = size.y,
                        file = state[0] + (state[1].Length == 0 ? "" : "_" + state[1]) + "_" + size.x + "x" + size.y + ".png" };
                    report.frames.Add(frame);
                    try { CaptureMenu(output, frame); }
                    catch (Exception e) { frame.error = e.ToString(); Debug.LogWarning(frame.file + ": " + e.Message); }
                    File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
                }
            if (report.frames.Any(f => !string.IsNullOrEmpty(f.error)))
                throw new InvalidOperationException("Parchment captures contain failures; inspect report.json.");
            int violations = report.frames.Sum(f => f.papers.Sum(p => p.content.Count(c => c.touchesFrame)));
            Debug.Log("PARCHMENT_FRAME_AUDIT: " + report.frames.Count + " captures; " + violations + " boundary warnings.");
        }

        private static void ArrangeParchmentVariant(GameObject host, MenuFrame frame)
        {
            if (string.IsNullOrEmpty(frame.variant)) return;
            var controller = host.GetComponent<CoreGameplayDemoController>();
            void Click(string name) => host.GetComponentsInChildren<Button>(false).Single(b => b.name == name).onClick.Invoke();
            object Invoke(string name, params object[] args)
            {
                MethodInfo method = typeof(CoreGameplayDemoController).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                    .SingleOrDefault(m => m.Name == name && m.GetParameters().Length == args.Length &&
                        m.GetParameters().Select((p,i) => args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(matches => matches));
                if (method == null) throw new MissingMethodException(name);
                try { return method.Invoke(controller, args); }
                catch (TargetInvocationException e) { throw e.InnerException ?? e; }
            }
            switch (frame.variant)
            {
                case "MarketDetail": case "MarketInventory": case "MarketBuy": case "MarketSell": case "MarketChoice":
                    // Read-only fixture arrangement: no purchase/sale callbacks or player preferences.
                    controller.Session.Cargo.Water = 264;
                    typeof(CoreGameplayDemoController).GetField("marketSelectedItem", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(controller, (PortMarketItem?)PortMarketItem.Water);
                    typeof(CoreGameplayDemoController).GetField("marketTradeSelecting", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(controller, frame.variant == "MarketBuy" || frame.variant == "MarketSell" || frame.variant == "MarketChoice");
                    typeof(CoreGameplayDemoController).GetField("marketTradeBuying", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(controller, frame.variant != "MarketSell");
                    typeof(CoreGameplayDemoController).GetField("marketInventoryExpanded", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(controller, frame.variant == "MarketInventory");
                    Invoke("RefreshMarketFacilityContents");
                    if (frame.variant == "MarketChoice")
                    {
                        DateTime now = controller.Session.GameDateTime;
                        int quantity = (int)Invoke("MarketQuantity", PortMarketItem.Water);
                        int level = (int)Invoke("PortInvestmentLevel", controller.Session.CurrentPort);
                        object purchase = Invoke("CreateMarketPurchaseQuote", PortMarketItem.Water, quantity,
                            PortMarketCatalog.CalculateBuyCost(controller.Session.CurrentPort, PortMarketItem.Water, now, quantity, controller.Session.Cargo.Fame),
                            now, level, 0, (int)Invoke("MarketRemaining", controller.Session.CurrentPort, PortMarketItem.Water, now, level));
                        Invoke("BuildMarketPurchaseChoice", purchase);
                    }
                    break;
                case "LastPage":
                    for (int page = 0; page < 3; page++) Click("Next Upgrade Cards");
                    break;
                case "Crew": Click("Configure Voyage Crew"); break;
                case "BasicSupplies":
                    RectTransform departureSetup = host.GetComponentsInChildren<RectTransform>(true)
                        .Single(item => item.name == "Voyage Setup");
                    Invoke("OpenDepartureBasicSupplies", departureSetup);
                    break;
                case "TreasureSeed": Click("Departure Treasure Seed Picker"); break;
                case "TreasureSeedFull":
                    controller.Session.RestoreTreasureProgress(new ShatteredSail.Core.Gameplay.CoreTreasureProgress {
                        pirateExperience = 1500, maps = Enumerable.Range(0,6).Select(index =>
                            new ShatteredSail.Core.Gameplay.CoreTreasureMapSave { id = "frame-audit-" + index,
                                rankId = index % 2 == 0 ? "weathered" : "gilded", seed = new string('W',63) + index }).ToArray() });
                    Invoke("Render"); Click("Departure Treasure Seed Picker"); break;
                case "ChartWarning": Invoke("ShowDepartureChartWarning", "示例种子与当前港口不匹配。"); break;
                case "Folio": Click("Treasure Open Folio"); break;
                case "History": Click("Treasure Open Folio"); Click("Treasure Open Completion"); break;
                case "FolioDecoded": controller.ConfigureEditorTreasureState(TreasureMapPreviewState.MapDecoded); break;
                case "FolioReceipt": controller.ConfigureEditorTreasureState(TreasureMapPreviewState.RewardClaim); break;
                case "Storage": Click("Warehouse Tab Storage"); break;
                case "Rare": Click("Warehouse Tab Rare"); break;
                case "Catalog": case "CatalogLast":
                    Click("Warehouse Collection Catalog");
                    if (frame.variant == "CatalogLast")
                        while (host.GetComponentsInChildren<Button>(false).Single(b => b.name == "Warehouse Catalog Page Next").interactable)
                            Click("Warehouse Catalog Page Next");
                    break;
                case "Actions": case "Adjustment": case "Sale":
                    Click("Warehouse Tab Storage");
                    object quote = Invoke("CreateWarehouseCargoQuote", PortMarketItem.Water, true);
                    if (frame.variant == "Adjustment") Invoke("BuildWarehouseCargoAdjustment", quote, 0);
                    else if (frame.variant == "Sale") Invoke("BuildWarehouseCargoSale", quote, 0, 0);
                    else Invoke("BuildWarehouseCargoActions", quote);
                    break;
                case "Cargo": Invoke("OpenVoyageCargo"); break;
                case "ReturnMenu": Invoke("OpenPortSettings"); Click("Return To Main Menu"); break;
                case "StoryFirst": case "StorySecond":
                    controller.ConfigureEditorFlowState(GameplayFlowPreviewState.TreasureNavigation);
                    Invoke("BuildTreasureStoryOverlayIfNeeded");
                    if (frame.variant == "StorySecond") Click("Treasure Story Advance");
                    break;
                default: throw new ArgumentException("Unknown audit variant: " + frame.variant);
            }
        }

        public static void CaptureNavigationHudFromCommandLine()
        {
            RequireEditor();
            string output = Output();
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Output must be new/empty: " + output);
            Directory.CreateDirectory(output);
            var report = new MenuReport
            {
                capturedAtUtc = DateTime.UtcNow.ToString("O"),
                scope = "Fresh navigation and encounter HUDs in disposable Editor PreviewScenes with the production CanvasScaler. No scene or asset saves.",
            };
            foreach (CoreGameplayPreviewScreen screen in new[] { CoreGameplayPreviewScreen.Navigation, CoreGameplayPreviewScreen.Encounter })
                foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) })
                {
                    var frame = new MenuFrame
                    {
                        state = screen.ToString(), width = size.x, height = size.y,
                        file = screen + "_" + size.x + "x" + size.y + ".png",
                    };
                    report.frames.Add(frame);
                    try { CaptureMenu(output, frame); }
                    catch (Exception e) { frame.error = e.ToString(); Debug.LogWarning(frame.file + ": " + e.Message); }
                    File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
                }
            if (report.frames.Any(frame => !string.IsNullOrEmpty(frame.error)))
                throw new InvalidOperationException("Navigation HUD capture failed; inspect report.json.");
            Debug.Log("NAVIGATION_HUD_CAPTURE_OK: " + report.frames.Count + " frames at " + output);
        }

        private static void CaptureMenu(string output, MenuFrame frame)
        {
            Scene scene = default;
            RenderTexture rt = null;
            RenderTexture previousActive = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                GameObject host = new GameObject("Disposable UI Audit Main Menu");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                bool shop = Enum.TryParse(frame.state, out CoreGameplayPreviewScreen screen);
                if (shop) host.AddComponent<CoreGameplayDemoController>().ConfigureEditorPreview(screen, CoreGameplayPreviewPort.GrayGull, 0);
                else host.AddComponent<MainMenuController>();
                host.SetActive(true);
                if (!shop && frame.state == "Achievements")
                    host.GetComponentsInChildren<Button>(true).Single(button => button.name == "Achievements").onClick.Invoke();
                ArrangeParchmentVariant(host, frame);
                string canvasName = shop ? CoreGameplayDemoController.EditorPreviewCanvasName : MainMenuController.EditorPreviewCanvasName;
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(c => c.name == canvasName);
                if (frame.state == "SaveNotice")
                {
                    string savedName = canvas.name;
                    try
                    {
                        canvas.name = "Main Menu Canvas";
                        typeof(MainMenuController).GetMethod("ShowSaveNotice", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(host.GetComponent<MainMenuController>(), new object[] {
                                "示例存档提示：航程记录暂不可用，请返回菜单重新读取。\n本页用于检查长文本与关闭按钮的边框留白。" });
                    }
                    finally { canvas.name = savedName; }
                }
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (!scaler.enabled) throw new InvalidOperationException("Production CanvasScaler unexpectedly disabled.");
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == "Settings Panel") child.gameObject.SetActive(frame.state == "Settings");
                    if (child.name == "Achievements Panel") child.gameObject.SetActive(frame.state == "Achievements");
                    if (child.name == "New Game Confirmation") child.gameObject.SetActive(frame.state == "NewGameConfirm");
                    child.gameObject.layer = Layer;
                }
                GameObject cameraHost = new GameObject("Disposable UI Audit Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0,0,-1000);
                camera.cullingMask = 1 << Layer;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                rt = new RenderTexture(frame.width, frame.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                rt.Create();
                camera.targetTexture = rt;
                camera.rect = new Rect(0,0,1,1);
                camera.aspect = frame.width / (float)frame.height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 100f;
                // No assignment to canvas.scaleFactor, RectTransform size or localScale.
                // Calling the original scaler Handle preserves production calculations.
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
                if (handle == null) throw new MissingMethodException("CanvasScaler.Handle");
                for (int pass = 0; pass < 4; pass++)
                {
                    Canvas.ForceUpdateCanvases();
                    handle.Invoke(scaler, null);
                    foreach (Text label in canvas.GetComponentsInChildren<Text>(true)) label.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = rt });
                    else camera.Render();
                }
                frame.renderingDisplaySize = canvas.renderingDisplaySize;
                frame.canvasPixelRect = canvas.pixelRect;
                frame.cameraPixelRect = camera.pixelRect;
                frame.logicalCanvasSize = canvas.GetComponent<RectTransform>().rect.size;
                frame.scaleFactor = canvas.scaleFactor;
                frame.screenMatchMode = scaler.screenMatchMode.ToString();
                float wr = frame.width / scaler.referenceResolution.x, hr = frame.height / scaler.referenceResolution.y;
                frame.expectedScaleFactor = scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand ? Mathf.Min(wr,hr) :
                    scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Shrink ? Mathf.Max(wr,hr) :
                    Mathf.Pow(2, Mathf.Lerp(Mathf.Log(wr,2), Mathf.Log(hr,2), scaler.matchWidthOrHeight));
                frame.verifiedScaler = scaler.enabled &&
                    Mathf.Abs(frame.renderingDisplaySize.x-frame.width)<0.5f && Mathf.Abs(frame.renderingDisplaySize.y-frame.height)<0.5f &&
                    Mathf.Abs(frame.canvasPixelRect.width-frame.width)<0.5f && Mathf.Abs(frame.canvasPixelRect.height-frame.height)<0.5f &&
                    Mathf.Abs(frame.cameraPixelRect.width-frame.width)<0.5f && Mathf.Abs(frame.cameraPixelRect.height-frame.height)<0.5f &&
                    Mathf.Abs(frame.scaleFactor-frame.expectedScaleFactor)<0.001f &&
                    Mathf.Abs(frame.logicalCanvasSize.x-frame.width/frame.scaleFactor)<1f &&
                    Mathf.Abs(frame.logicalCanvasSize.y-frame.height/frame.scaleFactor)<1f;
                if (!frame.verifiedScaler)
                    throw new InvalidOperationException("Target texture did not drive actual scaler/display geometry. Refuse false multi-resolution capture. Read recorded display/scale fields and use fixed Game View instead.");
                foreach (Text label in canvas.GetComponentsInChildren<Text>(false))
                {
                    if (!label.enabled || string.IsNullOrEmpty(label.text)) continue;
                    var geometry = new TextGeometry { path = PathOf(label.transform, canvas.transform), content = label.text, fontSize = label.fontSize };
                    var corners = new Vector3[4]; label.rectTransform.GetWorldCorners(corners);
                    geometry.rectPixels = Bounds(corners.Select(v => (Vector2)camera.WorldToScreenPoint(v)));
                    geometry.rectOutsideTarget = Outside(geometry.rectPixels, frame.width, frame.height);
                    Mesh mesh = label.canvasRenderer.GetMesh();
                    try
                    {
                        geometry.meshVertexCount = mesh == null ? 0 : mesh.vertexCount;
                        geometry.meshAvailable = mesh != null && mesh.vertexCount > 0;
                        if (geometry.meshAvailable)
                        {
                            geometry.meshPixels = Bounds(mesh.vertices.Select(v => (Vector2)camera.WorldToScreenPoint(label.transform.TransformPoint(v))));
                            geometry.meshOutsideTarget = Outside(geometry.meshPixels, frame.width, frame.height);
                        }
                    }
                    finally { /* CanvasRenderer owns the returned mesh. */ }
                    frame.text.Add(geometry);
                }
                RecordParchmentGeometry(canvas, frame);
                RenderTexture.active = rt;
                pixels = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0,0,frame.width,frame.height), 0,0);
                pixels.Apply(false,false);
                Color32[] samples = pixels.GetPixels32(); int low=255,high=0;
                for (int i=0;i<samples.Length;i+=997) { int b=(samples[i].r+samples[i].g+samples[i].b)/3; low=Math.Min(low,b); high=Math.Max(high,b); }
                if (high-low<20) throw new InvalidOperationException("Nearly blank frame; render not accepted.");
                File.WriteAllBytes(Path.Combine(output,frame.file), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
            }
        }

        private static void RecordParchmentGeometry(Canvas canvas, MenuFrame frame)
        {
            Image[] surfaces = canvas.GetComponentsInChildren<Image>(false).Where(i => i.enabled && i.color.a >= .65f &&
                i.fillCenter && (i.sprite == NauticalSurfaceSkin.ReadingPaper || i.sprite == NauticalSurfaceSkin.InvestmentPaper ||
                i.sprite == NauticalSurfaceSkin.CollectionPaper)).ToArray();
            foreach (Image paper in surfaces)
            {
                RectTransform rect = paper.rectTransform;
                Vector4 source = paper.sprite == NauticalSurfaceSkin.ReadingPaper ? new Vector4(60,67,60,62) :
                    paper.sprite == NauticalSurfaceSkin.InvestmentPaper ? new Vector4(43,51,43,46) : new Vector4(45,64,45,49);
                Vector4 border = paper.sprite.border;
                float ppu = paper.pixelsPerUnit * paper.pixelsPerUnitMultiplier;
                float Map(float distance, float edge, float opposite, float textureSize, float actualSize) =>
                    distance <= edge ? distance / ppu : edge / ppu + (distance - edge) *
                        (actualSize - (edge + opposite) / ppu) / (textureSize - edge - opposite);
                Rect safe = Rect.MinMaxRect(rect.rect.xMin + Map(source.x,border.x,border.z,paper.sprite.rect.width,rect.rect.width)+6,
                    rect.rect.yMin + Map(source.y,border.y,border.w,paper.sprite.rect.height,rect.rect.height)+6,
                    rect.rect.xMax - Map(source.z,border.z,border.x,paper.sprite.rect.width,rect.rect.width)-6,
                    rect.rect.yMax - Map(source.w,border.w,border.y,paper.sprite.rect.height,rect.rect.height)-6);
                var entry = new PaperFrame { path = PathOf(rect,canvas.transform), texture = paper.sprite.texture.name,
                    paperRect = rect.rect, safeRect = safe };
                frame.papers.Add(entry);
                // Full scenery/settings illustrations paint over these fallback papers.
                // Their inner rule is invisible, so it cannot be used as a visible boundary.
                foreach (RawImage illustration in rect.GetComponentsInChildren<RawImage>(false))
                {
                    if (!illustration.enabled || illustration.texture == null || illustration.color.a < .99f) continue;
                    Rect drawn = Bounds(Corners(illustration.rectTransform).Select(v => (Vector2)rect.InverseTransformPoint(v)));
                    if (drawn.Contains(safe.min) && drawn.Contains(safe.max))
                    { entry.coveredBy = PathOf(illustration.transform,rect); break; }
                }
                if (!string.IsNullOrEmpty(entry.coveredBy)) continue;
                // Card workshop uses a sibling backdrop. Other reading cards own their content.
                Transform scope = rect.GetComponentsInChildren<Text>(true).Length == 0 &&
                    rect.GetComponentsInChildren<Selectable>(true).Length == 0 ? rect.parent : rect;
                bool Owns(Transform item)
                {
                    if (item == rect) return false;
                    for (Transform t = item; t != null && t != scope; t = t.parent)
                        if (t != rect && surfaces.Any(s => s.transform == t)) return false;
                    return true;
                }
                Rect InPaper(RectTransform item) => Bounds(Corners(item).Select(v => (Vector2)rect.InverseTransformPoint(v)));
                void Record(Transform item, string kind, Rect bounds)
                {
                    if (bounds.width <= 0 || bounds.height <= 0 || !rect.rect.Contains(bounds.center)) return;
                    entry.content.Add(new PaperContent { path = PathOf(item,scope), kind = kind, visibleBounds = bounds,
                        touchesFrame = bounds.xMin < safe.xMin-.1f || bounds.yMin < safe.yMin-.1f ||
                            bounds.xMax > safe.xMax+.1f || bounds.yMax > safe.yMax+.1f });
                }
                foreach (Selectable control in scope.GetComponentsInChildren<Selectable>(false))
                    if (control.enabled && Owns(control.transform)) Record(control.transform,"control",InPaper((RectTransform)control.transform));
                foreach (Text label in scope.GetComponentsInChildren<Text>(false))
                {
                    if (!label.enabled || string.IsNullOrEmpty(label.text) || !Owns(label.transform)) continue;
                    Mesh mesh = label.canvasRenderer.GetMesh();
                    if (mesh == null || mesh.vertexCount == 0) continue;
                    Rect bounds = Bounds(mesh.vertices.Select(v => (Vector2)rect.InverseTransformPoint(label.transform.TransformPoint(v))));
                    for (Transform t = label.transform.parent; t != null && t != scope.parent; t = t.parent)
                        if (t.TryGetComponent(out RectMask2D mask) && mask.enabled)
                        {
                            Rect clip = InPaper((RectTransform)t);
                            bounds = Rect.MinMaxRect(Mathf.Max(bounds.xMin,clip.xMin),Mathf.Max(bounds.yMin,clip.yMin),
                                Mathf.Min(bounds.xMax,clip.xMax),Mathf.Min(bounds.yMax,clip.yMax));
                        }
                    Record(label.transform,"glyph",bounds);
                }
            }
        }
        private static Vector3[] Corners(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners); return corners;
        }
        private static string PathOf(Transform item, Transform root)
        {
            var names = new List<string>(); for (Transform t=item;t!=null && t!=root;t=t.parent) names.Insert(0,t.name); return string.Join("/",names);
        }
        private static Rect Bounds(IEnumerable<Vector2> points)
        {
            Vector2[] p=points.ToArray(); return Rect.MinMaxRect(p.Min(v=>v.x),p.Min(v=>v.y),p.Max(v=>v.x),p.Max(v=>v.y));
        }
        private static bool Outside(Rect r,int width,int height) { return r.xMin < -0.5f || r.yMin < -0.5f || r.xMax > width+0.5f || r.yMax > height+0.5f; }
        private static void RequireEditor()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stopped editor required.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("Graphics required; omit -nographics.");
        }
        private static string Output()
        {
            string[] args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,"-uiAuditPath");
            if(index<0 || index+1>=args.Length) throw new ArgumentException("Pass -uiAuditPath NEW_ABSOLUTE_OUTPUT_DIR");
            if(!Path.IsPathRooted(args[index+1]))throw new ArgumentException("Absolute output required.");
            return Path.GetFullPath(args[index+1]);
        }
    }
}
