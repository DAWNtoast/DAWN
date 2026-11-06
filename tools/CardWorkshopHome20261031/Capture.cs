// Installed only into the borrowed validation project. No production scene or save writes.
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
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    public static class CardWorkshopHomeCapture20261031
    {
        private const int Layer = 31;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly string[] Pages = { "Collection", "DeckBuilder", "Packs", "Upgrade" };
        private static readonly string[] Labels = { "查看已有卡牌", "组建牌组", "打开卡包", "卡牌强化" };
        private static readonly Dictionary<string, Vector2> HomeHotspotUv = new Dictionary<string, Vector2>();
        private static bool HomeOnly;
        private static bool SubpagesOnly;
        private static bool CardPacksOnly;
        private static bool CardPackArtOnly;
        private static readonly Dictionary<string, PackArt> PackArtSources = new Dictionary<string, PackArt>();
        [Serializable] public sealed class Report
        {
            public string capturedAtUtc = DateTime.UtcNow.ToString("O");
            public string project;
            public string scope = "Actual production scene-style workshop home and four subpages in disposable Editor PreviewScenes. Production CanvasScaler enabled, no saved scenes or player progress writes. Entrance, return-home and incomplete-deck fixtures remain in memory.";
            public List<Frame> frames = new List<Frame>();
        }
        [Serializable] public sealed class Frame
        {
            public string page, file, error, headerTexture, homeTexture, hoverEntrance;
            public int width, height;
            public bool verifiedScaler, verifiedSettings, verifiedNavigation, verifiedReadOnly, verifiedIncompleteDeckGuard, verifiedSceneHome, verifiedHomeAlignment, verifiedLetterbox, verifiedHover, verifiedNameplateClicks, verifiedSubpageBounds, verifiedSixCardGrid, verifiedUpgradePicker;
            public int nameplateRaycastSamples;
            public string packScenario;
            public bool verifiedPackInventory, verifiedPackOpening, verifiedPackDuplicates, verifiedPackContinuation, verifiedPackStaleCallback, verifiedPackBounds, verifiedNinePlus;
            public int[] resultCardIndexes;
            public int packCountBefore, packCountAfter, additionalCopiesBefore, additionalCopiesAfter, actualOwnedCopies;
            public string packArtId;
            public bool verifiedPackArt, verifiedArtSelectionReadOnly, verifiedArtClickPassThrough, verifiedArtCentered, verifiedNoProbabilityUi;
            public List<PackArt> packArt = new List<PackArt>();
            public float scaleFactor;
            public Vector2 displaySize;
            public Rect homeBackgroundBounds, viewportBounds, bodyBounds;
            public List<Element> elements = new List<Element>();
        }
        [Serializable] public sealed class Element
        {
            public string name, text;
            public bool interactable;
            public Rect screenBounds;
        }
        [Serializable] public sealed class PackArt
        {
            public string id, path, sha256;
            public int width, height, transparentPixels, visiblePixels;
        }
        public static void CaptureFromCommandLine()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stopped editor required.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Graphics required; omit -nographics.");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (!project.Equals("D:/Game/tmp/shipyard-assembly-20261017", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Use the assigned isolated validation project.");
            string[] args = Environment.GetCommandLineArgs(); int option = Array.IndexOf(args, "-workshopHomeOutput");
            HomeOnly = Array.IndexOf(args, "-workshopHomeOnly") >= 0;
            SubpagesOnly = Array.IndexOf(args, "-workshopSubpagesOnly") >= 0;
            CardPacksOnly = Array.IndexOf(args, "-workshopCardPacksOnly") >= 0;
            CardPackArtOnly = Array.IndexOf(args, "-workshopCardPackArtOnly") >= 0;
            if (new[] { HomeOnly, SubpagesOnly, CardPacksOnly, CardPackArtOnly }.Count(value => value) > 1) throw new ArgumentException("Choose one capture scope.");
            if (option < 0 || option + 1 >= args.Length || !Path.IsPathRooted(args[option + 1]))
                throw new ArgumentException("Pass -workshopHomeOutput ABSOLUTE_DIRECTORY.");
            string output = Path.GetFullPath(args[option + 1]);
            if (!output.Replace('\\', '/').StartsWith("D:/Game/outputs/card-workshop-home-20261031/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Output escaped task evidence directory.");
            Directory.CreateDirectory(output);
            if (Directory.EnumerateFiles(output, "*.png").Any() || File.Exists(Path.Combine(output, "report.json")))
                throw new IOException("Preserve previous evidence; choose a new run.");
            var report = new Report { project = project };
            if (CardPacksOnly) report.scope = "Real production card-pack UI: seven inventory types, three independent card rewards including duplicates, exact inventory/copy changes, continuation and stale-callback guards, and collection 9+ display. In-memory fixtures in disposable PreviewScenes; profile writes remain disabled.";
            if (CardPackArtOnly) report.scope = "Seven authored transparent card-pack textures: exact catalog/list/selection binding, distinct source SHA256 and alpha, centered main artwork without probability UI, real icon-center selection with unchanged stock, and three-card opening regression. Disposable PreviewScenes; no profile or scene writes.";
            if (SubpagesOnly) report.scope = "Redesigned production workshop subpages in disposable Editor PreviewScenes, with original scene-home entrance and return navigation checks. Production CanvasScaler enabled; no scene or player progress writes.";
            else if (HomeOnly) report.scope = "Production workshop home with port-style nameplates in disposable Editor PreviewScenes. Real uGUI raycasts at label centers and edges, edge clicks, return-home navigation, viewport and hover checks. No scene or player progress writes.";
            HomeHotspotUv.Clear();
            PackArtSources.Clear();
            if (CardPackArtOnly)
            {
                foreach (CoreCardPackDefinition pack in CoreCardPackCatalog.All)
                    report.frames.Add(new Frame { page = "Packs", packScenario = "Art", packArtId = pack.Id, width = 1920, height = 1080, file = "CardPackArt_" + pack.Id + "_1920x1080.png" });
                report.frames.Add(new Frame { page = "Packs", packScenario = "Art", packArtId = "neutral", width = 1280, height = 720, file = "CardPackArt_neutral_1280x720.png" });
                report.frames.Add(new Frame { page = "Packs", packScenario = "Art", packArtId = "neutral", width = 1280, height = 960, file = "CardPackArt_neutral_1280x960.png" });
            }
            else if (CardPacksOnly)
            {
                foreach (int width in new[] { 1920, 1280 })
                    foreach (string scenario in new[] { "Inventory", "Results", "NinePlus" })
                        report.frames.Add(new Frame { page = scenario == "NinePlus" ? "Collection" : "Packs", packScenario = scenario,
                            width = width, height = width * 9 / 16, file = "CardPacks_" + scenario + "_" + width + "x" + width * 9 / 16 + ".png" });
                report.frames.Add(new Frame { page = "Packs", packScenario = "Results", width = 1280, height = 960, file = "CardPacks_Results_1280x960.png" });
            }
            else foreach (int width in new[] { 1920, 1280 })
                foreach (string page in HomeOnly ? new[] { "Home" } : SubpagesOnly ? Pages : new[] { "Home" }.Concat(Pages))
                    report.frames.Add(new Frame { page = page, width = width, height = width * 9 / 16,
                        file = page + "_" + width + "x" + width * 9 / 16 + ".png" });
            if (CardPacksOnly || CardPackArtOnly) { }
            else if (SubpagesOnly)
            {
                report.frames.Add(new Frame { page = "DeckBuilder", width = 1280, height = 960, file = "DeckBuilder_1280x960.png" });
                report.frames.Add(new Frame { page = "Upgrade", width = 1280, height = 960, file = "Upgrade_1280x960.png" });
            }
            else
            {
                report.frames.Add(new Frame { page = "Home", width = 1280, height = 960, file = "Home_1280x960.png" });
                report.frames.Add(new Frame { page = "Home", width = 1920, height = 1080, hoverEntrance = "Upgrade", file = "Home_Hover_1920x1080.png" });
            }
            foreach (Frame frame in report.frames)
            {
                try { Capture(output, frame); Debug.Log("WORKSHOP_HOME_OK: " + frame.file); }
                catch (Exception error)
                {
                    while (error is TargetInvocationException && error.InnerException != null) error = error.InnerException;
                    frame.error = error.ToString(); Debug.LogWarning("WORKSHOP_HOME_FAILED: " + frame.file + " " + error.Message);
                }
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            }
            int failed = report.frames.Count(frame => !string.IsNullOrEmpty(frame.error));
            Debug.Log("WORKSHOP_HOME_COMPLETE: " + (report.frames.Count - failed) + " / " + report.frames.Count);
            if (failed > 0) throw new InvalidOperationException("Workshop page verification failed; inspect report.json.");
        }
        private static void Set(CoreGameplayDemoController controller, string field, object value)
        {
            FieldInfo target = typeof(CoreGameplayDemoController).GetField(field, Private);
            if (target == null) throw new MissingFieldException(field);
            target.SetValue(controller, value);
        }
        private static void Invoke(CoreGameplayDemoController controller, string method)
            => typeof(CoreGameplayDemoController).GetMethod(method, Private).Invoke(controller, null);
        private static Button Button(GameObject host, string name)
            => host.GetComponentsInChildren<Button>(false).Single(button => button.name == name);
        private static bool Exists(GameObject host, string name)
            => host.GetComponentsInChildren<Transform>(false).Any(item => item.name == name);
        private static void Click(GameObject host, string name)
        {
            Button button = Button(host, name);
            if (!button.interactable) throw new InvalidOperationException("Expected enabled button: " + name);
            button.onClick.Invoke();
        }
        private static void VerifyHome(GameObject host, Frame frame)
        {
            if (host.GetComponentsInChildren<Button>(false).Any(button => button.name.StartsWith("Workshop Page ", StringComparison.Ordinal)))
                throw new InvalidOperationException("The scene home must not be a subpage tab bar.");
            if (Exists(host, "Workshop Preview Collection") || Exists(host, "Deck Builder Panel") || Exists(host, "Battle Card Upgrade Panel") || Exists(host, "Workshop Packs Panel"))
                throw new InvalidOperationException("The scene home leaked a child page.");
            for (int i = 0; i < Pages.Length; i++)
            {
                Button entrance = Button(host, "Workshop Entrance " + Pages[i]);
                if (!entrance.interactable || !entrance.GetComponentsInChildren<Text>().Any(text => text.text == Labels[i]))
                    throw new InvalidOperationException("Invalid home entrance: " + Pages[i]);
            }
            RawImage backdrop = host.GetComponentsInChildren<RawImage>(false).Single(image => image.name == "Workshop Home Background");
            frame.homeTexture = AssetDatabase.GetAssetPath(backdrop.texture);
            if (backdrop.texture == null || string.IsNullOrEmpty(frame.homeTexture) || backdrop.raycastTarget)
                throw new InvalidOperationException("Scene home needs an imported, input-transparent background.");
            frame.verifiedSceneHome = true;
        }
        private static void VerifyNavigation(CoreGameplayDemoController controller, GameObject host, Frame frame)
        {
            VerifyHome(host, frame);
            decimal gold = controller.Session.ProfileGold;
            int[][] decks = Enum.GetValues(typeof(NavalBattleStage)).Cast<NavalBattleStage>()
                .Select(stage => controller.Session.GetBattleDeck(stage).ToArray()).ToArray();
            string upgrades = string.Join(";", controller.Session.CardUpgradeLevels.OrderBy(pair => pair.Key).Select(pair => pair.Key + "=" + pair.Value));
            for (int i = 0; i < Pages.Length; i++)
            {
                Click(host, "Workshop Entrance " + Pages[i]);
                Button(host, "Workshop Return Home");
                foreach (string page in Pages) Button(host, "Workshop Page " + page);
                for (int label = 0; label < Pages.Length; label++)
                    if (!Button(host, "Workshop Page " + Pages[label]).GetComponentsInChildren<Text>().Any(text => text.text == Labels[label]))
                        throw new InvalidOperationException("Unexpected workshop navigation label: " + Pages[label]);
                if (Pages[i] == "DeckBuilder" && !Exists(host, "Deck Builder Panel"))
                    throw new InvalidOperationException("Deck page did not open the production deck editor.");
                if (Pages[i] == "Upgrade" && !Exists(host, "Battle Card Upgrade Panel"))
                    throw new InvalidOperationException("Upgrade page did not open the production upgrade comparison.");
                if (Pages[i] == "Packs")
                {
                    if (!host.GetComponentsInChildren<Text>(false).Any(text => text.text.Contains("暂无") && text.text.Contains("卡包")))
                        throw new InvalidOperationException("Pack page needs its explicit empty state.");
                    if (Exists(host, "Deck Builder Panel") || Exists(host, "Battle Card Upgrade Panel"))
                        throw new InvalidOperationException("Pack page leaked another page's content.");
                }
                Click(host, "Workshop Return Home");
                VerifyHome(host, frame);
            }
            frame.verifiedNavigation = true;
            int stageIndex = 0;
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                if (!decks[stageIndex++].SequenceEqual(controller.Session.GetBattleDeck(stage)))
                    throw new InvalidOperationException("Changing workshop pages modified a deck.");
            frame.verifiedReadOnly = gold == controller.Session.ProfileGold && upgrades == string.Join(";", controller.Session.CardUpgradeLevels.OrderBy(pair => pair.Key).Select(pair => pair.Key + "=" + pair.Value));
            if (!frame.verifiedReadOnly) throw new InvalidOperationException("Changing pages modified gold or upgrades.");

            Click(host, "Workshop Entrance DeckBuilder");
            NavalBattleStage draftStage = (NavalBattleStage)typeof(CoreGameplayDemoController).GetField("selectedBattleDeckStage", Private).GetValue(controller);
            Set(controller, "deckBuilderDraft", controller.Session.GetBattleDeck(draftStage).Take(CoreVoyageSession.MinimumBaseBattleDeckSize - 1).ToList());
            Set(controller, "deckBuilderDraftSession", controller.Session);
            Set(controller, "deckBuilderDraftStage", draftStage);
            Set(controller, "deckBuilderDraftPort", controller.Session.CurrentPort);
            Invoke(controller, "Render");
            // Invoke disabled callbacks too: stale/programmatic clicks must respect the same draft guard.
            foreach (string page in new[] { "Collection", "Packs", "Upgrade" })
            {
                Button(host, "Workshop Page " + page).onClick.Invoke();
                if (!Exists(host, "Deck Builder Panel")) throw new InvalidOperationException("Incomplete deck escaped through " + page);
            }
            Button(host, "Workshop Return Home").onClick.Invoke();
            if (!Exists(host, "Deck Builder Panel")) throw new InvalidOperationException("Incomplete deck escaped through return-home.");
            frame.verifiedIncompleteDeckGuard = true;
            Invoke(controller, "ClearDeckBuilderDraft");
            Invoke(controller, "Render");
            Click(host, "Workshop Return Home");
            VerifyHome(host, frame);
            if (frame.page != "Home") Click(host, "Workshop Entrance " + frame.page);
        }
        private static Rect ScreenBounds(RectTransform rect, Camera camera)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 lower = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 upper = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return new Rect(lower, upper - lower);
        }
        private static void RenderGeometry(GameObject host, Canvas canvas, CanvasScaler scaler, Camera camera, RenderTexture target)
        {
            MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", Private);
            for (int pass = 0; pass < 3; pass++)
            {
                Canvas.ForceUpdateCanvases(); handle.Invoke(scaler, null);
                foreach (WorkshopResponsiveSurface surface in host.GetComponentsInChildren<WorkshopResponsiveSurface>(false)) surface.ApplyLayout();
                foreach (Transform item in canvas.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = Layer;
                Canvas.ForceUpdateCanvases();
                if (GraphicsSettings.currentRenderPipeline != null) RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                else camera.Render();
            }
        }
        private static bool Contains(Rect outer, Rect inner, float tolerance = .75f) =>
            inner.xMin >= outer.xMin - tolerance && inner.xMax <= outer.xMax + tolerance &&
            inner.yMin >= outer.yMin - tolerance && inner.yMax <= outer.yMax + tolerance;
        private static void VerifySubpageGeometry(GameObject host, Camera camera, Frame frame)
        {
            string rootName = frame.page == "DeckBuilder" ? "Deck Builder Panel" : frame.page == "Packs" ? "Workshop Packs Panel" :
                frame.page == "Upgrade" ? "Card Workshop Upgrade Workspace" : "Card Workshop Preview Panel";
            RectTransform root = host.GetComponentsInChildren<RectTransform>(false).Single(item => item.name == rootName);
            frame.bodyBounds = ScreenBounds(root, camera);
            if (!Contains(new Rect(0, 0, frame.width, frame.height), frame.bodyBounds))
                throw new InvalidOperationException("Subpage body escaped the window.");
            foreach (Button tab in host.GetComponentsInChildren<Button>(false).Where(button => button.name.StartsWith("Workshop Page ", StringComparison.Ordinal) || button.name == "Workshop Return Home"))
                if (ScreenBounds((RectTransform)tab.transform, camera).yMin < frame.bodyBounds.yMax - .75f)
                    throw new InvalidOperationException("Page navigation overlaps the subpage body: " + tab.name);
            foreach (CardSchoolFrameGraphic card in root.GetComponentsInChildren<CardSchoolFrameGraphic>(false))
                if (!Contains(frame.bodyBounds, ScreenBounds(card.rectTransform, camera)))
                    throw new InvalidOperationException("Card frame escaped the subpage body: " + card.name);
            frame.verifiedSubpageBounds = true;
            if ((CardPacksOnly || CardPackArtOnly) && frame.page == "Packs")
            {
                RectTransform library = root.GetComponentsInChildren<RectTransform>(false).Single(item => item.name == "Workshop Packs Library");
                Rect[] rows = library.GetComponentsInChildren<Button>(false).Where(button => button.name.StartsWith("Workshop Pack Select ", StringComparison.Ordinal))
                    .Select(button => ScreenBounds((RectTransform)button.transform, camera)).ToArray();
                if (rows.Length != 7 || rows.Any(row => !Contains(ScreenBounds(library, camera), row)))
                    throw new InvalidOperationException("Seven pack rows must remain inside their inventory panel.");
                for (int left = 0; left < rows.Length; left++)
                    for (int right = left + 1; right < rows.Length; right++)
                        if (rows[left].Overlaps(rows[right])) throw new InvalidOperationException("Pack inventory rows overlap.");
                if (frame.packScenario == "Results")
                {
                    Rect[] cards = root.GetComponentsInChildren<CardSchoolFrameGraphic>(false).Select(card => ScreenBounds(card.rectTransform, camera)).ToArray();
                    if (cards.Length != 3) throw new InvalidOperationException("Pack receipt must show three complete production card frames.");
                    for (int left = 0; left < cards.Length; left++)
                        for (int right = left + 1; right < cards.Length; right++)
                            if (cards[left].Overlaps(cards[right])) throw new InvalidOperationException("Pack receipt card frames overlap.");
                }
                frame.verifiedPackBounds = true;
            }
            if (frame.page == "Collection" || frame.page == "DeckBuilder")
            {
                string collectionName = frame.page == "Collection" ? "Workshop Preview Collection" : "Deck Builder Collection";
                RectTransform collection = root.GetComponentsInChildren<RectTransform>(false).Single(item => item.name == collectionName);
                Rect[] cards = collection.GetComponentsInChildren<CardSchoolFrameGraphic>(false).Select(card => ScreenBounds(card.rectTransform, camera)).ToArray();
                if (cards.Length != 6 || cards.Select(rect => Mathf.Round(rect.center.x)).Distinct().Count() != 3 || cards.Select(rect => Mathf.Round(rect.center.y)).Distinct().Count() != 2)
                    throw new InvalidOperationException("Collection must show six complete cards in three columns and two rows.");
                for (int left = 0; left < cards.Length; left++)
                    for (int right = left + 1; right < cards.Length; right++)
                        if (cards[left].Overlaps(cards[right])) throw new InvalidOperationException("Collection card frames overlap.");
                frame.verifiedSixCardGrid = true;
            }
            if (frame.page == "Upgrade")
            {
                RectTransform library = root.GetComponentsInChildren<RectTransform>(false).Single(item => item.name == "Workshop Upgrade Library");
                RectTransform detail = root.GetComponentsInChildren<RectTransform>(false).Single(item => item.name == "Battle Card Upgrade Panel");
                Button[] choices = library.GetComponentsInChildren<Button>(false).Where(button => button.name.StartsWith("Select Upgrade Card ", StringComparison.Ordinal)).ToArray();
                if (choices.Length != 6 || detail.GetComponentsInChildren<CardSchoolFrameGraphic>(false).Length != 2)
                    throw new InvalidOperationException("Upgrade page needs six list choices and two full comparison cards.");
                Rect[] rows = choices.Select(button => ScreenBounds((RectTransform)button.transform, camera)).ToArray();
                if (rows.Select(rect => Mathf.Round(rect.center.x)).Distinct().Count() != 1 || rows.Select(rect => Mathf.Round(rect.center.y)).Distinct().Count() != 6)
                    throw new InvalidOperationException("Upgrade choices must form one vertical list.");
                foreach (Rect row in rows) if (!Contains(ScreenBounds(library, camera), row)) throw new InvalidOperationException("Upgrade row escaped its list panel.");
                if (ScreenBounds(library, camera).xMax >= ScreenBounds(detail, camera).xMin)
                    throw new InvalidOperationException("Upgrade list and comparison panels overlap.");
                frame.verifiedUpgradePicker = true;
            }
        }
        private static void VerifyNameplateClicks(CoreGameplayDemoController controller, GameObject host, Canvas canvas,
            CanvasScaler scaler, Camera camera, RenderTexture target, Frame frame)
        {
            decimal gold = controller.Session.ProfileGold;
            string upgrades = string.Join(";", controller.Session.CardUpgradeLevels.OrderBy(pair => pair.Key).Select(pair => pair.Key + "=" + pair.Value));
            int[][] decks = Enum.GetValues(typeof(NavalBattleStage)).Cast<NavalBattleStage>().Select(stage => controller.Session.GetBattleDeck(stage).ToArray()).ToArray();
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            foreach (string page in Pages)
            {
                Button entrance = Button(host, "Workshop Entrance " + page);
                PortNameplateGraphic plate = entrance.GetComponentsInChildren<PortNameplateGraphic>(false)
                    .Single(item => item.name == "Workshop Entrance Nameplate " + page);
                Rect rect = plate.rectTransform.rect;
                Vector2[] points = { rect.center, new Vector2(rect.xMin + 4f, rect.center.y), new Vector2(rect.xMax - 4f, rect.center.y),
                    new Vector2(rect.center.x, rect.yMax - 4f), new Vector2(rect.center.x, rect.yMin + 8f) };
                PointerEventData pointer = null;
                foreach (Vector2 point in points)
                {
                    pointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left,
                        position = RectTransformUtility.WorldToScreenPoint(camera, plate.rectTransform.TransformPoint(point)) };
                    var hits = new List<RaycastResult>(); raycaster.Raycast(pointer, hits);
                    if (hits.Count == 0 || hits[0].gameObject.GetComponentInParent<Button>() != entrance || !entrance.IsInteractable())
                        throw new InvalidOperationException("Nameplate center/edge did not hit its entrance: " + page + " local " + point);
                    pointer.pointerCurrentRaycast = pointer.pointerPressRaycast = hits[0];
                    frame.nameplateRaycastSamples++;
                }
                // Execute the final edge hit through uGUI, proving that its live button opens the child page.
                ExecuteEvents.Execute(entrance.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(entrance.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(entrance.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                string expected = page == "Collection" ? "Workshop Collection Detail" : page == "DeckBuilder" ? "Deck Builder Panel" : page == "Packs" ? "Workshop Packs Panel" : "Battle Card Upgrade Panel";
                if (!Exists(host, expected)) throw new InvalidOperationException("Nameplate edge click did not open " + page);
                Click(host, "Workshop Return Home");
                VerifyHome(host, frame);
                RenderGeometry(host, canvas, scaler, camera, target);
            }
            int stageIndex = 0;
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                if (!decks[stageIndex++].SequenceEqual(controller.Session.GetBattleDeck(stage))) throw new InvalidOperationException("Nameplate navigation changed a deck.");
            frame.verifiedReadOnly = gold == controller.Session.ProfileGold && upgrades == string.Join(";", controller.Session.CardUpgradeLevels.OrderBy(pair => pair.Key).Select(pair => pair.Key + "=" + pair.Value));
            if (!frame.verifiedReadOnly) throw new InvalidOperationException("Nameplate navigation changed progress.");
            frame.verifiedNameplateClicks = frame.nameplateRaycastSamples == Pages.Length * 5;
        }
        private static int AdditionalCardCopies(CoreVoyageSession session) => session.CaptureCardPackProgress().cards.Sum(item => item.count);
        private static void PrepareCardPackFixture(CoreGameplayDemoController controller, GameObject host, Frame frame)
        {
            if (frame.packScenario == "NinePlus")
            {
                int index = int.Parse(host.GetComponentsInChildren<Button>(false).First(button => button.name.StartsWith("Select Upgrade Card ", StringComparison.Ordinal)).name.Substring("Select Upgrade Card ".Length));
                CoreCardPackProgress progress = controller.Session.CaptureCardPackProgress();
                progress.cards = new[] { new CoreCardCopySave { id = CoreVoyageSession.CardLibrary[index].Id, count = 10 } };
                if (!controller.Session.RestoreCardPackProgress(progress)) throw new InvalidOperationException("Collection quantity fixture rejected.");
                Invoke(controller, "Render");
                frame.actualOwnedCopies = controller.Session.GetBaseBattleCardOwnedCopies(index);
                Text count = host.GetComponentsInChildren<Text>(false).Single(text => text.name == "Workshop Owned Count " + index);
                if (frame.actualOwnedCopies < 10 || !count.text.Contains("9+")) throw new InvalidOperationException("Collection must retain actual quantity while displaying 9+: " + count.text);
                frame.verifiedNinePlus = true;
                return;
            }
            foreach (CoreCardPackDefinition pack in CoreCardPackCatalog.All)
                if (!controller.Session.GrantCardPacks(pack.Id, 2)) throw new InvalidOperationException("Pack fixture grant failed: " + pack.Id);
            // Choose a deterministic receipt with a repeated card so screenshots prove all three draws remain separate.
            if (frame.packScenario == "Results")
            {
                CoreCardPackProgress original = controller.Session.CaptureCardPackProgress();
                bool duplicate = false;
                for (uint seed = 1; seed <= 4096; seed++)
                {
                    original.randomState = seed;
                    if (!controller.Session.RestoreCardPackProgress(original) || !controller.Session.TryOpenCardPack("fire", out int[] cards, out string reason))
                        throw new InvalidOperationException("Receipt fixture rejected.");
                    if (cards.Distinct().Count() == cards.Length) continue;
                    frame.resultCardIndexes = cards;
                    duplicate = true;
                    break;
                }
                if (!duplicate || !controller.Session.RestoreCardPackProgress(original)) throw new InvalidOperationException("Could not prepare repeated-card receipt.");
            }
            Invoke(controller, "Render");
        }
        private static void RaycastClick(GameObject host, Canvas canvas, CanvasScaler scaler, Camera camera, RenderTexture target, string name, Vector2? screenPosition = null)
        {
            RenderGeometry(host, canvas, scaler, camera, target);
            Button button = Button(host, name);
            var pointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left,
                position = screenPosition ?? RectTransformUtility.WorldToScreenPoint(camera, ((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center)) };
            var hits = new List<RaycastResult>();
            canvas.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
            if (hits.Count == 0 || hits[0].gameObject.GetComponentInParent<Button>() != button || !button.IsInteractable())
                throw new InvalidOperationException("Real center click is blocked: " + name);
            pointer.pointerCurrentRaycast = pointer.pointerPressRaycast = hits[0];
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            RenderGeometry(host, canvas, scaler, camera, target);
        }
        private static void ExerciseCardPackFixture(CoreGameplayDemoController controller, GameObject host, Canvas canvas,
            CanvasScaler scaler, Camera camera, RenderTexture target, Frame frame)
        {
            if (frame.packScenario == "NinePlus") return;
            foreach (CoreCardPackDefinition pack in CoreCardPackCatalog.All)
            {
                if (controller.Session.GetCardPackCount(pack.Id) != 2 || !Button(host, "Workshop Pack Select " + pack.Id).interactable)
                    throw new InvalidOperationException("All seven pack inventories must be selectable.");
                Text count = host.GetComponentsInChildren<Text>(false).Single(text => text.name == "Workshop Pack Count " + pack.Id);
                if (count.text != "2") throw new InvalidOperationException("Pack inventory label mismatches real stock.");
            }
            frame.verifiedPackInventory = CoreCardPackCatalog.All.Count == 7;
            RaycastClick(host, canvas, scaler, camera, target, "Workshop Pack Select fire");
            if (frame.packScenario == "Inventory") return;
            frame.packCountBefore = controller.Session.GetCardPackCount("fire");
            frame.additionalCopiesBefore = AdditionalCardCopies(controller.Session);
            Button.ButtonClickedEvent staleOpen = Button(host, "Workshop Packs Open Selected").onClick;
            RaycastClick(host, canvas, scaler, camera, target, "Workshop Packs Open Selected");
            frame.packCountAfter = controller.Session.GetCardPackCount("fire");
            frame.additionalCopiesAfter = AdditionalCardCopies(controller.Session);
            int[] actual = (int[])typeof(CoreGameplayDemoController).GetField("workshopPackResultCards", Private).GetValue(controller);
            if (frame.packCountAfter != frame.packCountBefore - 1 || frame.additionalCopiesAfter != frame.additionalCopiesBefore + 3 ||
                actual == null || !actual.SequenceEqual(frame.resultCardIndexes)) throw new InvalidOperationException("Pack transaction did not consume exactly one pack and grant three expected draws.");
            frame.verifiedPackOpening = true;
            staleOpen.Invoke();
            if (controller.Session.GetCardPackCount("fire") != frame.packCountAfter || AdditionalCardCopies(controller.Session) != frame.additionalCopiesAfter)
                throw new InvalidOperationException("Stale open callback consumed another pack.");
            frame.verifiedPackStaleCallback = true;
            for (int slot = 0; slot < 3; slot++)
            {
                string prefix = "Workshop Pack Result " + slot;
                if (!Exists(host, prefix + " Slot") || !Exists(host, prefix + " Art") || !Exists(host, prefix + " Name") || !Exists(host, prefix + " Effect"))
                    throw new InvalidOperationException("A repeated draw was merged or its full card face is missing.");
            }
            frame.verifiedPackDuplicates = actual.Distinct().Count() < 3;
            if (!frame.verifiedPackDuplicates) throw new InvalidOperationException("Duplicate fixture lost its repeated draw.");
        }
        private static void VerifyCardPackContinuation(CoreGameplayDemoController controller, GameObject host, Canvas canvas,
            CanvasScaler scaler, Camera camera, RenderTexture target, Frame frame)
        {
            if (frame.packScenario != "Results") return;
            RaycastClick(host, canvas, scaler, camera, target, "Workshop Packs Open Again");
            if (controller.Session.GetCardPackCount("fire") != 0 || AdditionalCardCopies(controller.Session) != frame.additionalCopiesAfter + 3)
                throw new InvalidOperationException("Continue-opening did not grant exactly three cards.");
            Button disabled = Button(host, "Workshop Packs Open Again");
            if (disabled.interactable) throw new InvalidOperationException("Empty pack type still offers continuation.");
            disabled.onClick.Invoke();
            if (controller.Session.GetCardPackCount("fire") != 0 || AdditionalCardCopies(controller.Session) != frame.additionalCopiesAfter + 3)
                throw new InvalidOperationException("Disabled callback mutated card stock.");
            RaycastClick(host, canvas, scaler, camera, target, "Workshop Packs Back To List");
            if (Button(host, "Workshop Packs Open Selected").interactable) throw new InvalidOperationException("Empty type can be opened from its list.");
            RaycastClick(host, canvas, scaler, camera, target, "Workshop Pack Select neutral");
            Button.ButtonClickedEvent oldPageOpen = Button(host, "Workshop Packs Open Selected").onClick;
            RaycastClick(host, canvas, scaler, camera, target, "Workshop Packs View Collection");
            if (!Exists(host, "Workshop Collection Detail")) throw new InvalidOperationException("Pack result cannot return to collection.");
            oldPageOpen.Invoke();
            if (controller.Session.GetCardPackCount("neutral") != 2) throw new InvalidOperationException("An old page callback opened a pack after leaving.");
            frame.verifiedPackContinuation = true;
        }
        private static PackArt ReadPackArt(string id, Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            if (path != "Assets/_Project/Resources/CardWorkshopV2/card_pack_" + id + "_v1.png")
                throw new InvalidOperationException("Wrong authored pack resource: " + id + " -> " + path);
            if (PackArtSources.TryGetValue(path, out PackArt cached)) return cached;
            byte[] bytes = File.ReadAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "..", path)));
            Texture2D readable = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!readable.LoadImage(bytes, false)) throw new InvalidOperationException("Could not decode authored pack PNG: " + path);
                Color32[] pixels = readable.GetPixels32();
                var art = new PackArt { id = id, path = path, width = readable.width, height = readable.height,
                    transparentPixels = pixels.Count(pixel => pixel.a == 0), visiblePixels = pixels.Count(pixel => pixel.a > 0) };
                if (art.transparentPixels == 0 || art.visiblePixels == 0)
                    throw new InvalidOperationException("Pack PNG needs both real transparency and visible artwork: " + path);
                using (var sha = System.Security.Cryptography.SHA256.Create()) art.sha256 = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
                PackArtSources.Add(path, art);
                return art;
            }
            finally { Object.DestroyImmediate(readable); }
        }
        private static void ExerciseCardPackArt(CoreGameplayDemoController controller, GameObject host, Canvas canvas,
            CanvasScaler scaler, Camera camera, RenderTexture target, Frame frame)
        {
            RenderGeometry(host, canvas, scaler, camera, target);
            string inventory = JsonUtility.ToJson(controller.Session.CaptureCardPackProgress());
            decimal gold = controller.Session.ProfileGold;
            var catalog = (CardWorkshopArtCatalog)typeof(CoreGameplayDemoController).GetField("cardWorkshopArt", Private).GetValue(controller);
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            foreach (CoreCardPackDefinition pack in CoreCardPackCatalog.All)
            {
                Texture2D texture = catalog.CardPack(pack.Id);
                if (texture == null) throw new InvalidOperationException("Missing distinct pack artwork: " + pack.Id);
                RawImage icon = host.GetComponentsInChildren<RawImage>(false).Single(item => item.name == "Workshop Pack List Art " + pack.Id);
                Button row = Button(host, "Workshop Pack Select " + pack.Id);
                if (icon.texture != texture || icon.raycastTarget || !Contains(ScreenBounds((RectTransform)row.transform, camera), ScreenBounds(icon.rectTransform, camera)))
                    throw new InvalidOperationException("Pack list art binding, input passthrough or bounds failed: " + pack.Id);
                var pointer = new PointerEventData(null) { position = RectTransformUtility.WorldToScreenPoint(camera, icon.rectTransform.TransformPoint(icon.rectTransform.rect.center)) };
                var hits = new List<RaycastResult>(); raycaster.Raycast(pointer, hits);
                if (hits.Count == 0 || hits[0].gameObject.GetComponentInParent<Button>() != row)
                    throw new InvalidOperationException("Pack icon blocks its row click: " + pack.Id);
                frame.packArt.Add(ReadPackArt(pack.Id, texture));
            }
            if (frame.packArt.Count != 7 || frame.packArt.Select(art => art.path).Distinct().Count() != 7 || frame.packArt.Select(art => art.sha256).Distinct().Count() != 7)
                throw new InvalidOperationException("Seven pack types must use seven different authored PNG resources.");
            RawImage selectedIcon = host.GetComponentsInChildren<RawImage>(false).Single(item => item.name == "Workshop Pack List Art " + frame.packArtId);
            Vector2 iconCenter = RectTransformUtility.WorldToScreenPoint(camera, selectedIcon.rectTransform.TransformPoint(selectedIcon.rectTransform.rect.center));
            RaycastClick(host, canvas, scaler, camera, target, "Workshop Pack Select " + frame.packArtId, iconCenter);
            RawImage main = host.GetComponentsInChildren<RawImage>(false).Single(item => item.name == "Workshop Packs Selected Pack Art");
            if (main.texture != catalog.CardPack(frame.packArtId) || main.raycastTarget)
                throw new InvalidOperationException("Selected main art differs from its list/catalog resource or blocks input.");
            RectTransform table = host.GetComponentsInChildren<RectTransform>(false).Single(item => item.name == "Workshop Packs Opening Table");
            if (!Contains(ScreenBounds(table, camera), ScreenBounds(main.rectTransform, camera)))
                throw new InvalidOperationException("Selected pack artwork escaped its table.");
            if (Vector2.Distance(main.rectTransform.anchoredPosition, new Vector2(0f, 56f)) > .01f ||
                Mathf.Abs(ScreenBounds(main.rectTransform, camera).center.x - ScreenBounds(table, camera).center.x) > .5f)
                throw new InvalidOperationException("Selected pack artwork must remain centered on its opening table.");
            if (host.GetComponentsInChildren<Text>(false).Any(label => label.text.Contains("概率")) ||
                host.GetComponentsInChildren<Transform>(false).Any(item =>
                    item.name.StartsWith("Workshop Pack Odds ", StringComparison.Ordinal) ||
                    item.name.StartsWith("Workshop Pack School Odds ", StringComparison.Ordinal) ||
                    item.name == "Workshop Packs View Odds" || item.name == "Workshop Packs Close Odds"))
                throw new InvalidOperationException("Retired probability headings or controls remain on the pack page.");
            if (inventory != JsonUtility.ToJson(controller.Session.CaptureCardPackProgress()) || gold != controller.Session.ProfileGold)
                throw new InvalidOperationException("Choosing artwork changed inventory or gold.");
            frame.verifiedPackArt = frame.verifiedArtSelectionReadOnly = frame.verifiedArtClickPassThrough =
                frame.verifiedArtCentered = frame.verifiedNoProbabilityUi = true;
        }
        private static void VerifyArtPackOpening(CoreGameplayDemoController controller, GameObject host, Canvas canvas,
            CanvasScaler scaler, Camera camera, RenderTexture target, Frame frame)
        {
            frame.packCountBefore = controller.Session.GetCardPackCount(frame.packArtId);
            frame.additionalCopiesBefore = AdditionalCardCopies(controller.Session);
            RaycastClick(host, canvas, scaler, camera, target, "Workshop Packs Open Selected");
            frame.packCountAfter = controller.Session.GetCardPackCount(frame.packArtId);
            frame.additionalCopiesAfter = AdditionalCardCopies(controller.Session);
            int[] cards = (int[])typeof(CoreGameplayDemoController).GetField("workshopPackResultCards", Private).GetValue(controller);
            if (frame.packCountAfter != frame.packCountBefore - 1 || frame.additionalCopiesAfter != frame.additionalCopiesBefore + 3 || cards == null || cards.Length != 3 ||
                host.GetComponentsInChildren<CardSchoolFrameGraphic>(false).Length != 3)
                throw new InvalidOperationException("Artwork integration broke the three-card opening transaction or result frames.");
            if (!Button(host, "Workshop Packs Open Again").IsInteractable() || !Button(host, "Workshop Packs View Collection").IsInteractable())
                throw new InvalidOperationException("Pack result actions were blocked by artwork.");
            frame.resultCardIndexes = cards;
            RaycastClick(host, canvas, scaler, camera, target, "Workshop Packs Back To List");
            if (host.GetComponentsInChildren<RawImage>(false).Single(item => item.name == "Workshop Packs Selected Pack Art").texture == null)
                throw new InvalidOperationException("Selected pack artwork disappeared after returning from results.");
            frame.verifiedPackOpening = true;
        }
        private static void Capture(string output, Frame frame)
        {
            Scene scene = default; RenderTexture target = null, previous = RenderTexture.active; Texture2D pixels = null;
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                var host = new GameObject("Disposable Workshop Home Capture"); host.SetActive(false); SceneManager.MoveGameObjectToScene(host, scene);
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.CardWorkshop, CoreGameplayPreviewPort.BrokenAnchor, 1);
                host.SetActive(true);
                // Keep all editor-mode mutation guards enabled while exercising production callbacks.
                Set(controller, "editorPreviewBuilding", true);
                if (HomeOnly) VerifyHome(host, frame); else VerifyNavigation(controller, host, frame);
                if (CardPacksOnly || CardPackArtOnly) PrepareCardPackFixture(controller, host, frame);
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(item => item.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (!scaler.enabled) throw new InvalidOperationException("Production CanvasScaler disabled.");
                var cameraHost = new GameObject("Disposable Workshop Home Camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.orthographicSize = 5; camera.nearClipPlane = .1f; camera.farClipPlane = 2000;
                camera.transform.position = new Vector3(0, 0, -1000); camera.cullingMask = 1 << Layer; camera.allowHDR = false; camera.allowMSAA = false;
                target = new RenderTexture(frame.width, frame.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create(); camera.targetTexture = target; camera.rect = new Rect(0, 0, 1, 1); camera.aspect = frame.width / (float)frame.height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 100;
                if (CardPacksOnly) ExerciseCardPackFixture(controller, host, canvas, scaler, camera, target, frame);
                if (CardPackArtOnly) ExerciseCardPackArt(controller, host, canvas, scaler, camera, target, frame);
                if (HomeOnly)
                {
                    RenderGeometry(host, canvas, scaler, camera, target);
                    VerifyNameplateClicks(controller, host, canvas, scaler, camera, target, frame);
                }
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", Private);
                for (int pass = 0; pass < 5; pass++)
                {
                    Canvas.ForceUpdateCanvases(); handle.Invoke(scaler, null);
                    foreach (WorkshopResponsiveSurface surface in host.GetComponentsInChildren<WorkshopResponsiveSurface>(false)) surface.ApplyLayout();
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty();
                    foreach (Transform item in canvas.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = Layer;
                    Canvas.ForceUpdateCanvases();
                    if (!string.IsNullOrEmpty(frame.hoverEntrance))
                    {
                        Button hovered = Button(host, "Workshop Entrance " + frame.hoverEntrance);
                        RectTransform hoveredRect = (RectTransform)hovered.transform;
                        var pointer = new PointerEventData(null) {
                            position = RectTransformUtility.WorldToScreenPoint(camera, hoveredRect.TransformPoint(hoveredRect.rect.center)),
                            pointerEnter = hovered.gameObject,
                            pointerCurrentRaycast = new RaycastResult { module = canvas.GetComponent<GraphicRaycaster>(), gameObject = hovered.gameObject }
                        };
                        pointer.pointerPressRaycast = pointer.pointerCurrentRaycast;
                        ExecuteEvents.Execute(hovered.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                        Color labelColor = hovered.GetComponentInChildren<Text>().color;
                        frame.verifiedHover = Mathf.Abs(labelColor.r - 1f) < .001f && Mathf.Abs(labelColor.g - .86f) < .001f && Mathf.Abs(labelColor.b - .52f) < .001f;
                        if (!frame.verifiedHover) throw new InvalidOperationException("Home hotspot hover did not highlight the label.");
                        Canvas.ForceUpdateCanvases();
                    }
                    if (GraphicsSettings.currentRenderPipeline != null) RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                frame.displaySize = canvas.renderingDisplaySize; frame.scaleFactor = canvas.scaleFactor;
                float wr = frame.width / scaler.referenceResolution.x, hr = frame.height / scaler.referenceResolution.y;
                float expected = scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand ? Mathf.Min(wr, hr) : scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Shrink ? Mathf.Max(wr, hr) : Mathf.Pow(2, Mathf.Lerp(Mathf.Log(wr, 2), Mathf.Log(hr, 2), scaler.matchWidthOrHeight));
                frame.verifiedScaler = Mathf.Abs(frame.displaySize.x - frame.width) < .5f && Mathf.Abs(frame.displaySize.y - frame.height) < .5f && Mathf.Abs(canvas.scaleFactor - expected) < .001f;
                if (!frame.verifiedScaler) throw new InvalidOperationException("Render target did not drive the production CanvasScaler.");
                if (SubpagesOnly || CardPacksOnly || CardPackArtOnly) VerifySubpageGeometry(host, camera, frame);
                Button settings = canvas.GetComponentsInChildren<Button>(false).Single(button => button.name.EndsWith("Settings", StringComparison.Ordinal));
                RectTransform settingsRect = (RectTransform)settings.transform;
                frame.verifiedSettings = Mathf.Abs(settingsRect.rect.width - NauticalHeaderSkin.IconSize) < .01f && Mathf.Abs(settingsRect.rect.height - NauticalHeaderSkin.IconSize) < .01f && Mathf.Abs(settingsRect.anchoredPosition.x + NauticalHeaderSkin.SettingsRightOffset) < .01f && Mathf.Abs(settingsRect.anchoredPosition.y + NauticalHeaderSkin.SettingsTopOffset) < .01f;
                if (!frame.verifiedSettings) throw new InvalidOperationException("Shared 96x96 header settings geometry changed.");
                NauticalHeaderGraphic wood = canvas.GetComponentsInChildren<NauticalHeaderGraphic>(false).Single(graphic => graphic.enabled && graphic.name == "Header Brass Rules");
                frame.headerTexture = AssetDatabase.GetAssetPath(wood.mainTexture);
                if (string.IsNullOrEmpty(frame.headerTexture) || wood.raycastTarget) throw new InvalidOperationException("Regional header texture or input passthrough failed.");
                if (frame.page == "Home")
                {
                    RawImage backdrop = host.GetComponentsInChildren<RawImage>(false).Single(image => image.name == "Workshop Home Background");
                    frame.homeBackgroundBounds = ScreenBounds(backdrop.rectTransform, camera);
                    frame.viewportBounds = ScreenBounds(FixedGameViewport.Bounds(canvas), camera);
                    if (Vector2.Distance(frame.homeBackgroundBounds.position, frame.viewportBounds.position) > .5f || Vector2.Distance(frame.homeBackgroundBounds.size, frame.viewportBounds.size) > .5f || backdrop.uvRect != new Rect(0, 0, 1, 1))
                        throw new InvalidOperationException("Scene background must fill the same uncropped fixed game viewport as its hotspots.");
                    foreach (string page in Pages)
                    {
                        Rect hotspot = ScreenBounds((RectTransform)Button(host, "Workshop Entrance " + page).transform, camera);
                        Vector2 uv = new Vector2((hotspot.center.x - frame.homeBackgroundBounds.xMin) / frame.homeBackgroundBounds.width,
                            (hotspot.center.y - frame.homeBackgroundBounds.yMin) / frame.homeBackgroundBounds.height);
                        if (HomeHotspotUv.TryGetValue(page, out Vector2 reference) && Vector2.Distance(reference, uv) > .001f)
                            throw new InvalidOperationException("Hotspot moved relative to scene art at this aspect ratio: " + page);
                        HomeHotspotUv[page] = uv;
                    }
                    frame.verifiedHomeAlignment = true;
                }
                foreach (Button button in canvas.GetComponentsInChildren<Button>(false))
                {
                    var bounds = ScreenBounds((RectTransform)button.transform, camera);
                    frame.elements.Add(new Element { name = button.name, text = string.Join(" | ", button.GetComponentsInChildren<Text>().Select(text => text.text)), interactable = button.interactable, screenBounds = bounds });
                    if ((button.name.StartsWith("Workshop Page ", StringComparison.Ordinal) || button.name.StartsWith("Workshop Entrance ", StringComparison.Ordinal) || button.name == "Workshop Return Home") && (bounds.xMin < 0 || bounds.xMax > frame.width + .5f || bounds.yMin < 0 || bounds.yMax > frame.height + .5f))
                        throw new InvalidOperationException("Workshop navigation escaped viewport: " + button.name);
                }
                RenderTexture.active = target;
                pixels = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0); pixels.Apply(false, false);
                if (frame.page == "Home" && frame.width == 1280 && frame.height == 960)
                {
                    if (Mathf.Abs(frame.viewportBounds.yMin - 120f) > .5f || Mathf.Abs(frame.viewportBounds.yMax - 840f) > .5f)
                        throw new InvalidOperationException("4:3 viewport did not retain centered 16:9 geometry.");
                    foreach (int y in new[] { 20, 940 })
                    {
                        Color bar = pixels.GetPixel(640, y);
                        if (bar.r > .02f || bar.g > .02f || bar.b > .02f)
                            throw new InvalidOperationException("4:3 letterbox is not black.");
                    }
                    frame.verifiedLetterbox = true;
                }
                Color32[] samples = pixels.GetPixels32(); int low = 255, high = 0;
                for (int i = 0; i < samples.Length; i += 997) { int brightness = (samples[i].r + samples[i].g + samples[i].b) / 3; low = Math.Min(low, brightness); high = Math.Max(high, brightness); }
                if (high - low < 20) throw new InvalidOperationException("Nearly blank frame.");
                File.WriteAllBytes(Path.Combine(output, frame.file), pixels.EncodeToPNG());
                if (CardPacksOnly) VerifyCardPackContinuation(controller, host, canvas, scaler, camera, target, frame);
                if (CardPackArtOnly) VerifyArtPackOpening(controller, host, canvas, scaler, camera, target, frame);
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }
    }
}
