using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ShatteredSail.Editor
{
    /// <summary>
    /// Grounding/layout review for every port facility, plus the six regional
    /// shops/boathouses, all five mounts, and deterministic one-to-five-item showrooms.
    /// Never enters Play Mode, invokes purchase callbacks, changes PlayerPrefs,
    /// saves a scene, or rewrites a project asset. All preview UI is temporary.
    /// </summary>
    public static class PortGroundingAcceptance
    {
        private const int Width = 1920;
        private const int Height = 1080;
        private const int Layer = 31;
        private static readonly DateTime FixtureDate = new DateTime(2026, 9, 1);
        private static readonly CoreGameplayPreviewScreen[] PortScreens =
        {
            CoreGameplayPreviewScreen.PortHub,
            CoreGameplayPreviewScreen.Warehouse,
            CoreGameplayPreviewScreen.Shipyard,
            CoreGameplayPreviewScreen.ShipyardDetail,
            CoreGameplayPreviewScreen.Tavern,
            CoreGameplayPreviewScreen.TavernCrewDetail,
            CoreGameplayPreviewScreen.ComponentShop,
            CoreGameplayPreviewScreen.ComponentShopDetail,
            CoreGameplayPreviewScreen.CardWorkshop,
            CoreGameplayPreviewScreen.DeckBuilder,
            CoreGameplayPreviewScreen.Dispatch,
            CoreGameplayPreviewScreen.DispatchDeployment,
            CoreGameplayPreviewScreen.TreasureMap,
            CoreGameplayPreviewScreen.Maintenance,
            CoreGameplayPreviewScreen.VoyageDeparture,
        };

        [Serializable]
        private sealed class Review
        {
            public string capturedAtUtc;
            public int width = Width;
            public int height = Height;
            public string scope = "All Gray Gull port pages; six regional shops and maintenance docks; five type-specific and five stock-count showroom fixtures.";
            public string fixtureNote = "Showroom fixtures are temporary, synthetic display samples, NOT live daily stock. No purchase callbacks or save writes are invoked.";
            public int expectedCaptureCount = 41;
            public int successfulCaptureCount;
            public List<Record> captures = new List<Record>();
            public List<string> errors = new List<string>();
        }

        [Serializable]
        private sealed class Record
        {
            public int shipIndex;
            public bool hoveredShip;
            public string group;
            public string port;
            public string screen;
            public int routeIndex;
            public string file;
            public bool completePreview;
            public bool syntheticInventory;
            public string sampleSlot;
            public int sampleStockCount;
            public int displayPage;
            public List<string> sampleEntryIds = new List<string>();
            public List<string> sampleOfferIds = new List<string>();
            public List<string> visibleEntryIds = new List<string>();
            public List<string> visibleOfferIds = new List<string>();
            public int uiObjectCount;
            public List<string> notableObjects = new List<string>();
            public string error;
        }

        private sealed class CaptureCase
        {
            public int shipIndex;
            public bool hoveredShip;
            public CoreGameplayPreviewPort port;
            public CoreGameplayPreviewScreen screen;
            public int routeIndex;
            public ComponentSlot? sampleSlot;
            public int sampleStockCount;
            public int displayPage;
            public string group;
        }

        [MenuItem("Shattered Sail/Art Review/Capture Port Scene Grounding")]
        public static void CaptureFromMenu()
        {
            string directory = Capture(ResolveOutputDirectory());
            Debug.Log("Port grounding gallery: " + directory);
            EditorUtility.RevealInFinder(Path.Combine(directory, "index.html"));
        }

        /// <summary>
        /// -executeMethod ShatteredSail.Editor.PortGroundingAcceptance.CaptureFromCommandLine
        /// -groundingCapturePath D:/Game/UnityClient/Artifacts/PortGrounding/review-01
        /// Optional: -groundingScreen CardWorkshop captures that page in all six ports.
        /// ComponentShop also includes deterministic one-to-five-item stock fixtures.
        /// Graphics must be enabled: do not pass -nographics.
        /// </summary>
        public static void CaptureFromCommandLine()
        {
            Debug.Log("PORT_GROUNDING_ACCEPTANCE: " + Capture(ResolveOutputDirectory()));
        }

        public static string Capture(string outputDirectory)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before running grounding acceptance.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Grounding screenshots require graphics. Remove -nographics.");

            string directory = Path.GetFullPath(outputDirectory);
            // A capture set is versioned by its directory. Refuse to silently
            // replace an existing review, especially the earlier RegionalPortsV1.
            if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
                throw new IOException("Capture directory is not empty. Use a new -groundingCapturePath: " + directory);
            Directory.CreateDirectory(directory);

            List<CaptureCase> cases = SelectedCases().ToList();
            Review report = new Review { capturedAtUtc = DateTime.UtcNow.ToString("O"), expectedCaptureCount = cases.Count };
            if (cases.All(item => item.screen == CoreGameplayPreviewScreen.Shipyard || item.screen == CoreGameplayPreviewScreen.ShipyardDetail))
            {
                report.scope = "Four afloat vessels, four hover states, and four parchment dossiers in the enlarged water gallery.";
                report.fixtureNote = "Temporary editor preview with 572 demo gold; hover callbacks only. No purchase, flagship change, or PlayerPrefs writes.";
            }
            if (cases.Select(item => item.screen).Distinct().Count() == 1)
                report.scope = "Focused six-port review: " + cases[0].screen + "; all dispatch deployment frames and shop stock-count fixtures when applicable.";
            Scene previewScene = EditorSceneManager.NewPreviewScene();
            RenderTexture previousActive = RenderTexture.active;
            GameObject host = null;
            GameObject cameraHost = null;
            RenderTexture target = null;
            try
            {
                host = new GameObject("Port Grounding Acceptance Host") { hideFlags = HideFlags.HideAndDontSave };
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, previewScene);
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                host.SetActive(true);

                cameraHost = new GameObject("Port Grounding Acceptance Camera", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(cameraHost, previewScene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.025f, 0.06f, 0.075f, 1f);
                camera.orthographic = true;
                camera.orthographicSize = Height * 0.5f;
                camera.aspect = Width / (float)Height;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0f, 0f, -1000f);
                camera.cullingMask = 1 << Layer;
                camera.scene = previewScene;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                    { name = "Port Grounding Acceptance Target" };
                target.Create();
                camera.targetTexture = target;

                foreach (CaptureCase item in cases)
                {
                    Record record = new Record
                    {
                        shipIndex = item.shipIndex,
                        hoveredShip = item.hoveredShip,
                        group = item.group,
                        port = PortName(item.port),
                        screen = item.screen.ToString(),
                        routeIndex = item.routeIndex,
                        syntheticInventory = item.sampleSlot.HasValue || item.sampleStockCount > 0,
                        sampleSlot = item.sampleSlot.HasValue ? item.sampleSlot.Value.ToString() : string.Empty,
                        sampleStockCount = item.sampleSlot.HasValue ? 5 : item.sampleStockCount,
                        displayPage = item.displayPage,
                        file = item.port + "_" + item.screen +
                            ((item.screen == CoreGameplayPreviewScreen.Shipyard || item.screen == CoreGameplayPreviewScreen.ShipyardDetail) ? "_Ship" + item.shipIndex : string.Empty) +
                            (item.hoveredShip ? "_Hover" : string.Empty) +
                            (item.screen == CoreGameplayPreviewScreen.DispatchDeployment ? "_Mission" + item.routeIndex : string.Empty) +
                            (item.sampleSlot.HasValue ? "_Fixture_" + item.sampleSlot.Value :
                                item.sampleStockCount > 0 ? "_Fixture_Count" + item.sampleStockCount : string.Empty) +
                            (item.sampleStockCount > 3 ? "_Page" + (item.displayPage + 1) : string.Empty) + ".png",
                    };
                    report.captures.Add(record);
                    try
                    {
                        controller.ConfigureEditorPreview(item.screen, item.port, item.routeIndex);
                        if (item.screen == CoreGameplayPreviewScreen.Shipyard || item.screen == CoreGameplayPreviewScreen.ShipyardDetail)
                            controller.ConfigureEditorShipyard(item.shipIndex, item.screen == CoreGameplayPreviewScreen.ShipyardDetail);
                        if (item.hoveredShip)
                        {
                            UnityEngine.EventSystems.EventTrigger trigger = host.GetComponentsInChildren<UnityEngine.EventSystems.EventTrigger>(true)
                                .Single(value => value.name == "Shipyard Ship " + item.shipIndex);
                            trigger.triggers.Single(entry => entry.eventID == UnityEngine.EventSystems.EventTriggerType.PointerEnter)
                                .callback.Invoke(new UnityEngine.EventSystems.BaseEventData(null));
                        }
                        if (!controller.HasCompleteEditorPreview)
                            throw new InvalidOperationException("Preview is missing its required screen root.");
                        if (record.syntheticInventory)
                            ApplyShowroomFixture(controller, host, item.sampleSlot, record.sampleStockCount, record);
                        record.visibleEntryIds = host.GetComponentsInChildren<UnityEngine.UI.RawImage>(true)
                            .Where(image => image.name.StartsWith("Component Shop Model ", StringComparison.Ordinal))
                            .Select(image => image.name.Substring("Component Shop Model ".Length)).ToList();
                        record.visibleOfferIds = host.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                            .Where(button => button.name.StartsWith("Component Shop Offer ", StringComparison.Ordinal))
                            .Select(button => button.name.Substring("Component Shop Offer ".Length)).ToList();
                        if (record.syntheticInventory && !record.visibleOfferIds.SequenceEqual(record.sampleOfferIds.Skip(record.displayPage * 3).Take(3)))
                            throw new InvalidOperationException("The captured showroom page does not contain the expected fixture offers.");
                        Canvas canvas = host.GetComponentInChildren<Canvas>();
                        if (canvas == null) throw new InvalidOperationException("No preview Canvas was created.");
                        canvas.renderMode = RenderMode.WorldSpace;
                        canvas.worldCamera = camera;
                        RectTransform rect = canvas.GetComponent<RectTransform>();
                        rect.sizeDelta = new Vector2(Width, Height);
                        rect.position = Vector3.zero;
                        rect.localScale = Vector3.one;
                        Transform[] objects = canvas.GetComponentsInChildren<Transform>(true);
                        foreach (Transform child in objects) child.gameObject.layer = Layer;
                        record.uiObjectCount = objects.Length;
                        record.notableObjects = objects.Select(o => o.name).Where(IsNotable).Distinct().ToList();
                        // Best-fit text may grow/repack the shared dynamic font
                        // atlas during its first build. Settle the invalidated
                        // text meshes before saving the inspection frame.
                        for (int frame = 0; frame < 3; frame++)
                        {
                            foreach (UnityEngine.UI.Text text in canvas.GetComponentsInChildren<UnityEngine.UI.Text>(true)) text.SetAllDirty();
                            Canvas.ForceUpdateCanvases();
                            Render(camera, target);
                        }
                        SaveFrame(target, Path.Combine(directory, record.file));
                        record.completePreview = true;
                        report.successfulCaptureCount += 1;
                    }
                    catch (Exception exception)
                    {
                        Exception cause = exception is TargetInvocationException invocation && invocation.InnerException != null
                            ? invocation.InnerException : exception;
                        record.error = cause.Message;
                        report.errors.Add(record.port + " / " + record.screen + " / " + record.sampleSlot + ": " + cause.Message);
                        Debug.LogException(cause);
                    }
                }
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (cameraHost != null) UnityEngine.Object.DestroyImmediate(cameraHost);
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
                EditorSceneManager.ClosePreviewScene(previewScene);
                File.WriteAllText(Path.Combine(directory, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                WriteGallery(directory, report);
                WriteChecklist(directory, report);
            }
            if (report.errors.Count > 0 || report.successfulCaptureCount != report.expectedCaptureCount)
                throw new InvalidOperationException("Grounding review is incomplete. See " + Path.Combine(directory, "report.json"));
            return directory;
        }

        private static IEnumerable<CaptureCase> SelectedCases()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < arguments.Length; index++)
            {
                if (arguments[index] != "-groundingScreen") continue;
                if (!Enum.TryParse(arguments[index + 1], out CoreGameplayPreviewScreen screen) || !PortScreens.Contains(screen))
                    throw new ArgumentException("Unknown port facility for -groundingScreen: " + arguments[index + 1]);
                if (screen == CoreGameplayPreviewScreen.Shipyard || screen == CoreGameplayPreviewScreen.ShipyardDetail)
                    return Enumerable.Range(0, 4).SelectMany(ship => new[]
                    {
                        new CaptureCase { port = CoreGameplayPreviewPort.GrayGull, screen = CoreGameplayPreviewScreen.Shipyard, shipIndex = ship, group = "水上船坞 · 四种船型" },
                        new CaptureCase { port = CoreGameplayPreviewPort.GrayGull, screen = CoreGameplayPreviewScreen.Shipyard, shipIndex = ship, hoveredShip = true, group = "悬停高亮 · 四种船型" },
                        new CaptureCase { port = CoreGameplayPreviewPort.GrayGull, screen = CoreGameplayPreviewScreen.ShipyardDetail, shipIndex = ship, group = "船只档案 · 四种船型" },
                    });
                IEnumerable<CaptureCase> regionalCases = Enum.GetValues(typeof(CoreGameplayPreviewPort)).Cast<CoreGameplayPreviewPort>()
                    .SelectMany(port => Enumerable.Range(0, screen == CoreGameplayPreviewScreen.DispatchDeployment ? 3 : 1)
                        .Select(route => new CaptureCase { port = port, screen = screen, routeIndex = route, group = "六港 · " + screen }));
                return screen == CoreGameplayPreviewScreen.ComponentShop
                    ? regionalCases.Concat(StockCountCases()).Concat(RegionalFurnitureCases()) : regionalCases;
            }
            return Cases();
        }

        private static IEnumerable<CaptureCase> Cases()
        {
            foreach (CoreGameplayPreviewScreen screen in PortScreens)
                yield return new CaptureCase { port = CoreGameplayPreviewPort.GrayGull, screen = screen, group = "灰鸥港 · 全部功能" };
            foreach (CoreGameplayPreviewPort port in Enum.GetValues(typeof(CoreGameplayPreviewPort)))
            {
                if (port == CoreGameplayPreviewPort.GrayGull) continue;
                yield return new CaptureCase { port = port, screen = CoreGameplayPreviewScreen.ComponentShop, group = "六港 · 商店与后勤对照" };
                yield return new CaptureCase { port = port, screen = CoreGameplayPreviewScreen.Maintenance, group = "六港 · 商店与后勤对照" };
            }
            foreach (ComponentSlot slot in Enum.GetValues(typeof(ComponentSlot)))
                for (int page = 0; page < 2; page++)
                    yield return new CaptureCase
                    {
                        port = CoreGameplayPreviewPort.GrayGull,
                        screen = CoreGameplayPreviewScreen.ComponentShop,
                        sampleSlot = slot,
                        sampleStockCount = 5,
                        displayPage = page,
                        group = "部件类型 · 固定验收样例（非商店库存）",
                    };
            foreach (CaptureCase item in StockCountCases()) yield return item;
        }

        private static IEnumerable<CaptureCase> RegionalFurnitureCases()
        {
            // The same five loads exercise all thirty regional supports and six
            // sign shapes, independent of the current month's random inventory.
            foreach (CoreGameplayPreviewPort port in Enum.GetValues(typeof(CoreGameplayPreviewPort)))
                if (port != CoreGameplayPreviewPort.GrayGull)
                    for (int page = 0; page < 2; page++)
                        yield return new CaptureCase { port = port, screen = CoreGameplayPreviewScreen.ComponentShop,
                            sampleStockCount = 5, displayPage = page, group = "六港 · 五类地区家具固定样例" };
        }

        private static IEnumerable<CaptureCase> StockCountCases()
        {
            for (int count = 1; count <= 5; count++)
                for (int page = 0; page < (count > 3 ? 2 : 1); page++)
                    yield return new CaptureCase
                    {
                        port = CoreGameplayPreviewPort.GrayGull,
                        screen = CoreGameplayPreviewScreen.ComponentShop,
                        sampleStockCount = count,
                        displayPage = page,
                        group = "库存数量 · 固定验收样例（非商店库存）",
                    };
        }

        private static void ApplyShowroomFixture(CoreGameplayDemoController controller, GameObject host, ComponentSlot? fixedSlot, int count, Record record)
        {
            RectTransform panel = host.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == "Component Shop Panel");
            if (panel == null) throw new InvalidOperationException("Component Shop Panel is unavailable for the fixture.");
            MethodInfo build = typeof(CoreGameplayDemoController).GetMethod("BuildComponentShopShowroom", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(RectTransform), typeof(IReadOnlyList<PortComponentShopStockItem>), typeof(DateTime), typeof(int) }, null);
            if (build == null) throw new InvalidOperationException("The showroom fixture contract changed: expected BuildComponentShopShowroom(panel, stock, date, soldCount).");

            ComponentArtCatalog catalog = new ComponentArtCatalog();
            ComponentRarity[] rarities = (ComponentRarity[])Enum.GetValues(typeof(ComponentRarity));
            ComponentSlot[] mixedSlots = { ComponentSlot.Bow, ComponentSlot.Sail, ComponentSlot.Instrument, ComponentSlot.Plating, ComponentSlot.Cannon };
            List<PortComponentShopStockItem> stock = new List<PortComponentShopStockItem>();
            for (int index = 0; index < count; index++)
            {
                ComponentSlot slot = fixedSlot ?? mixedSlots[index];
                ComponentRarity rarity = rarities[index];
                ComponentArtEntry entry = catalog.Entries.First(e => e.Slot == slot && e.Rarity == rarity);
                string id = "grounding-fixture|" + (fixedSlot.HasValue ? "slot" : "count" + count) + "|" + slot + "|" + rarity;
                stock.Add(new PortComponentShopStockItem(id, controller.Session.CurrentPort, PortComponentShopCatalog.MonthKey(FixtureDate),
                    entry, 1f, PortComponentShopCatalog.CalculatePrice(entry, controller.Session.CurrentPort, 1f)));
                record.sampleEntryIds.Add(entry.Id);
                record.sampleOfferIds.Add(id);
            }
            // Only the throwaway preview's showroom children are removed. The
            // port background/header and all persistent scene objects stay intact.
            for (int index = panel.childCount - 1; index >= 0; index -= 1)
                UnityEngine.Object.DestroyImmediate(panel.GetChild(index).gameObject);
            build.Invoke(controller, new object[] { panel, stock, FixtureDate, 0 });
            if (record.displayPage > 0)
            {
                UnityEngine.UI.Button next = host.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                    .FirstOrDefault(button => button.name == "Component Shop Next Display");
                if (next == null || !next.interactable)
                    throw new InvalidOperationException("The stock-count fixture requires an enabled next-display control.");
                // Exercise the real page transition without selecting or buying an
                // offer. The callback must preserve this fixture's stock by itself.
                next.onClick.Invoke();
                FieldInfo pageField = typeof(CoreGameplayDemoController).GetField("componentShopDisplayPage", BindingFlags.Instance | BindingFlags.NonPublic);
                if (pageField == null || (int)pageField.GetValue(controller) != record.displayPage)
                    throw new InvalidOperationException("The showroom next control did not advance to the expected page.");
            }
            SetLabel(host, "Component Shop Stock Header", fixedSlot.HasValue
                ? "陈列融合验收 · " + ComponentArtCatalog.SlotName(fixedSlot.Value) + " · 第 " + (record.displayPage + 1) + " 页 · 普通 / 精品 / 稀有 / 史诗 / 传说"
                : "陈列地面验收 · " + count + " 件库存 · 第 " + (record.displayPage + 1) + " 页 · 部件全貌 / 独立立牌 / 无展示架");
            SetLabel(host, "Component Shop Regional Rule", "固定美术验收样例 · 不代表本港真实每日库存 · 不可据此购买 · 不改动玩家存档");
        }

        private static void SetLabel(GameObject host, string name, string value)
        {
            Transform item = host.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            if (item == null) return;
            Component text = item.GetComponent("Text");
            if (text != null) text.GetType().GetProperty("text")?.SetValue(text, value, null);
        }

        private static bool IsNotable(string name)
        {
            return name.IndexOf("Mount", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Model", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Board", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Plaque", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Shadow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.EndsWith("Background", StringComparison.Ordinal);
        }

        private static void Render(Camera camera, RenderTexture target)
        {
            if (GraphicsSettings.currentRenderPipeline != null)
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            else camera.Render();
        }

        private static void SaveFrame(RenderTexture target, string file)
        {
            RenderTexture.active = target;
            Texture2D image = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
            try
            {
                image.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                image.Apply(false, false);
                Color32[] pixels = image.GetPixels32();
                int dark = 255, bright = 0;
                for (int index = 0; index < pixels.Length; index += 997)
                {
                    int level = (pixels[index].r + pixels[index].g + pixels[index].b) / 3;
                    dark = Math.Min(dark, level); bright = Math.Max(bright, level);
                }
                if (bright - dark < 20) throw new InvalidOperationException("Frame is nearly uniform; the Canvas may not have rendered.");
                File.WriteAllBytes(file, image.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }

        private static void WriteGallery(string directory, Review report)
        {
            StringBuilder html = new StringBuilder("<!doctype html><html lang=\"zh-CN\"><meta charset=\"utf-8\"><title>港口场景融合验收</title>");
            html.Append("<style>body{background:#13202a;color:#f4e5c5;font:16px system-ui;margin:32px}h2{margin-top:40px}section{display:grid;grid-template-columns:repeat(auto-fit,minmax(420px,1fr));gap:18px}figure{margin:0;padding:10px;background:#26343c;border:1px solid #67543c}img{width:100%;height:auto}figcaption{padding:8px}a{color:#edba63}small{display:block;color:#becad0}p{max-width:1200px;line-height:1.6}</style>");
            html.Append("<h1>港口场景融合验收</h1><p>").Append(report.successfulCaptureCount).Append(" / ").Append(report.expectedCaptureCount)
                .Append(" 张 · 1920 × 1080 · 编辑模式独立预览场景 · 不写入玩家存档。</p><p>验收重点：物品与支架接触、船只水线、牌子与场景材质、前后遮挡、角色比例、避免浮空矩形面板。");
            html.Append("<a href=\"report.json\">结构化报告</a> · <a href=\"checklist.md\">人工视觉检查清单</a></p>");
            foreach (string group in report.captures.Select(r => r.group).Distinct())
            {
                html.Append("<h2>").Append(Escape(group)).Append("</h2><section>");
                IEnumerable<Record> records = report.captures.Where(r => r.group == group);
                // Also show Gray Gull beside the other five ports without taking
                // duplicate screenshots or overwriting the all-facilities pass.
                if (group == "六港 · 商店与后勤对照")
                    records = records.Concat(report.captures.Where(r => r.port == "灰鸥港" && !r.syntheticInventory &&
                        (r.screen == "ComponentShop" || r.screen == "Maintenance")));
                foreach (Record record in records)
                {
                    html.Append("<figure>");
                    if (record.completePreview)
                        html.Append("<a href=\"").Append(Escape(record.file)).Append("\"><img loading=\"lazy\" src=\"").Append(Escape(record.file)).Append("\"></a>");
                    html.Append("<figcaption>").Append(Escape(record.port)).Append(" · ").Append(Escape(record.screen));
                    if (record.syntheticInventory) html.Append(" · ").Append(Escape(string.IsNullOrEmpty(record.sampleSlot)
                        ? record.sampleStockCount + " 件库存" : record.sampleSlot)).Append(" · 第 ").Append(record.displayPage + 1).Append(" 页")
                        .Append("<small>固定样例，非真实库存；检查部件与立牌无遮挡、中央陈列区、背景货架避让；不应出现商人或展示架。</small>");
                    if (!record.completePreview) html.Append("<small>捕获失败：").Append(Escape(record.error)).Append("</small>");
                    html.Append("</figcaption></figure>");
                }
                html.Append("</section>");
            }
            File.WriteAllText(Path.Combine(directory, "index.html"), html.ToString(), new UTF8Encoding(false));
        }

        private static void WriteChecklist(string directory, Review report)
        {
            StringBuilder md = new StringBuilder("# 港口场景融合 · 人工视觉验收\n\n");
            md.Append("捕获成功 ").Append(report.successfulCaptureCount).Append(" / ").Append(report.expectedCaptureCount).Append("。截图成功并不等于视觉检查通过，下列检查由人工看图完成。\n\n");
            md.Append("- [ ] 灰鸥港全部功能：物品/桌椅/船体与场景比例自然，不浮空。\n- [ ] 六港商店：仅保留部件与区域立牌，没有商人、展示架及其残留阴影；立牌不遮挡部件主体。\n- [ ] 五种部件样例：船首、风帆、火炮、覆板、仪器完整可见；保留原始形状方向；环绕光不形成方框。\n- [ ] 一至五件库存样例：部件与立牌居中分组陈列，避开两侧背景杂物；彼此不重叠，立牌文字在木板范围内。\n- [ ] 六港后勤：左侧检查板已移除；左侧放大的船体与水线连贯；右侧放大的采购板落在前景码头，六种物资全部可见，不需滚动。\n- [ ] 酒馆：人物头顶名称准确，桌面遮挡自然；档案羊皮纸与肖像融合。\n- [ ] 卡组/派遣/藏宝图：入口摆件融入地面或桌面；详细页面排版清晰。\n- [ ] 所有顶部标题与按钮无互相遮挡；低对比背景上的文字仍可辨认。\n\n## 捕获文件\n\n");
            foreach (Record record in report.captures)
                md.Append("- [").Append(record.port).Append(" · ").Append(record.screen).Append(record.syntheticInventory
                    ? " · 样例 " + (string.IsNullOrEmpty(record.sampleSlot) ? record.sampleStockCount + " 件库存" : record.sampleSlot) + " · 第 " + (record.displayPage + 1) + " 页" : string.Empty)
                    .Append("](").Append(record.file).Append(") — ").Append(record.completePreview ? "已捕获，待视觉检查" : "失败：" + record.error).Append('\n');
            File.WriteAllText(Path.Combine(directory, "checklist.md"), md.ToString(), new UTF8Encoding(false));
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        private static string PortName(CoreGameplayPreviewPort port)
        {
            return PortFacilityArtCatalog.SupportedRegionalPorts[(int)port];
        }

        private static string ResolveOutputDirectory()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < args.Length; index += 1)
                if (args[index] == "-groundingCapturePath") return Path.GetFullPath(Path.Combine(project, args[index + 1]));
            return Path.Combine(project, "Artifacts", "PortGrounding", "review-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        }
    }
}
