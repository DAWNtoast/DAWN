using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Core.Tests
{
    public sealed class LegendaryFleetComicPlaybackTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Type RecordType = typeof(CoreGameplayDemoController).Assembly.GetType(
            "ShatteredSail.Presentation.VoyageSuspensionRecord", true);
        private readonly List<GameObject> hosts = new List<GameObject>();
        private string comicReadJson;
        private string comicProfileScope;

        [SetUp]
        public void FreshCampaign()
        {
            comicReadJson = string.Empty;
            comicProfileScope = Guid.NewGuid().ToString("N");
        }

        private LegendaryFleetComicReadProgress ReadProgress() =>
            new LegendaryFleetComicReadProgress(comicProfileScope, () => comicReadJson, json => comicReadJson = json);

        private static IEnumerable<TestCaseData> EveryEntry()
        {
            foreach (CoreLegendaryWarship ship in CoreLegendaryWarships.All)
                foreach (bool treasure in new[] { false, true })
                    yield return new TestCaseData(ship.Id, treasure);
        }

        private static IEnumerable<TestCaseData> EveryVictory()
        {
            foreach (CoreLegendaryWarship ship in CoreLegendaryWarships.All)
                foreach (bool treasure in new[] { false, true })
                    foreach (bool boarding in new[] { false, true })
                        yield return new TestCaseData(ship.Id, treasure, boarding);
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (GameObject host in hosts) if (host != null) Object.DestroyImmediate(host);
            hosts.Clear();
        }

        [TestCaseSource(nameof(EveryEntry))]
        public void BothEntrances_ShowOnlyTheCorrectWordlessPreludeAndBlockBattleInput(string id, bool treasure)
        {
            CoreVoyageSession session = Voyage(id, treasure);
            CoreGameplayDemoController controller = Fixture(session);
            BeginBattle(session, id);
            Build(controller);
            AssertPage(controller, LegendaryFleetComicCatalog.Find(id).Before.ImageResource);
            if (!treasure) CaptureScreen(controller, LegendaryFleetComicCatalog.Find(id).Before.ImageResource);
            Assert.That((bool)typeof(CoreGameplayDemoController).GetProperty("BattleInspectionBlocksInput", Private)
                .GetValue(controller), Is.True);
            Button.ButtonClickedEvent retiredClick = Find<Button>(controller, "Legendary Fleet Comic Advance").onClick;
            Build(controller);
            retiredClick.Invoke();
            AssertPage(controller, LegendaryFleetComicCatalog.Find(id).Before.ImageResource);
            Click(controller);
            Build(controller);
            Assert.That(Overlay(controller), Is.Null, "A completed prelude cannot replay on a battle redraw.");
            Assert.That((bool)typeof(CoreGameplayDemoController).GetProperty("BattleInspectionBlocksInput", Private)
                .GetValue(controller), Is.False);
        }

        [TestCaseSource(nameof(EveryVictory))]
        public void NavalAndBoardingVictories_ShowTheirOwnAfterPageExactlyOnce(string id, bool treasure, bool boarding)
        {
            CoreVoyageSession session = Voyage(id, treasure);
            CoreGameplayDemoController controller = Fixture(session);
            BeginBattle(session, id);
            Build(controller);
            Click(controller);
            Win(session, boarding);
            Build(controller);
            AssertPage(controller, LegendaryFleetComicCatalog.Find(id).After.ImageResource);
            if (!treasure && !boarding) CaptureScreen(controller, LegendaryFleetComicCatalog.Find(id).After.ImageResource);
            Click(controller);
            Build(controller);
            Assert.That(Overlay(controller), Is.Null);
            Assert.That(Field<string>(controller, "pendingLegendaryVictoryId"), Is.Empty);
        }

        [TestCase(false, false)] [TestCase(false, true)]
        [TestCase(true, false)] [TestCase(true, true)]
        public void FailureIncludingMutualDefeat_NeverQueuesAnAfterPage(bool treasure, bool boarding)
        {
            const string id = "legendary_pirate_last_horizon";
            CoreVoyageSession session = Voyage(id, treasure);
            CoreGameplayDemoController controller = Fixture(session);
            BeginBattle(session, id);
            Build(controller);
            Click(controller);
            session.Battle.Stage = boarding ? NavalBattleStage.Boarding : NavalBattleStage.Naval;
            session.Battle.EnemyHull = 0;
            if (boarding) session.Battle.PlayerBoardingHealth = 0;
            else SetProperty(session, "Hull", 0);
            Invoke(session, "ResolveBattleOutcome");
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            Build(controller);
            Assert.That(Overlay(controller), Is.Null);
            Assert.That(Field<string>(controller, "pendingLegendaryVictoryId"), Is.Empty);
        }

        [TestCase(false)] [TestCase(true)]
        public void Retreat_NeverQueuesAnAfterPage(bool treasure)
        {
            const string id = "legendary_pirate_tide_requiem";
            CoreVoyageSession session = Voyage(id, treasure);
            CoreGameplayDemoController controller = Fixture(session);
            BeginBattle(session, id);
            Build(controller);
            Click(controller);
            session.Cargo.Water = session.Cargo.Food = 100;
            Assert.That(session.RetreatFromBattle(), Is.True);
            Build(controller);
            Assert.That(Overlay(controller), Is.Null);
            Assert.That(Field<string>(controller, "pendingLegendaryVictoryId"), Is.Empty);
        }

        [TestCase(false)] [TestCase(true)]
        public void JsonSuspension_RestoresPendingVictoryBeforeItsFirstRenderAndPreservesReadState(bool treasure)
        {
            const string id = "legendary_pirate_last_horizon";
            CoreVoyageSession source = Voyage(id, treasure);
            CoreGameplayDemoController controller = Fixture(source);
            BeginBattle(source, id);
            Build(controller);
            Click(controller);
            Win(source, true);
            // Save at the committed result even if an animation was interrupted
            // before Render had a chance to discover the pending epilogue.
            object record = Capture(controller, source);
            AssertRecordValid(record, true);
            Assert.That((string)RecordType.GetField("pendingLegendaryVictoryId").GetValue(record), Is.EqualTo(id));
            record = JsonUtility.FromJson(JsonUtility.ToJson(record), RecordType);
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint((CoreVoyageCheckpoint)RecordType.GetField("voyage")
                .GetValue(record), out string reason), Is.True, reason);
            CoreGameplayDemoController resumed = Fixture(restored);
            Invoke(resumed, "RestoreTreasureStoryProgress", record);
            Build(resumed);
            AssertPage(resumed, LegendaryFleetComicCatalog.Find(id).After.ImageResource);
            Click(resumed);
            object read = Capture(resumed, restored);
            read = JsonUtility.FromJson(JsonUtility.ToJson(read), RecordType);
            CoreGameplayDemoController reloaded = Fixture(restored);
            Invoke(reloaded, "RestoreTreasureStoryProgress", read);
            Build(reloaded);
            Assert.That(Overlay(reloaded), Is.Null, "A read epilogue must remain read after another reload.");
        }

        [Test]
        public void SamePageRedrawAndDisableResume_PreserveTimerAndRejectStaleCallbacks()
        {
            const string id = "legendary_pirate_iron_oath";
            CoreVoyageSession session = Voyage(id, false);
            CoreGameplayDemoController controller = Fixture(session);
            BeginBattle(session, id);
            Build(controller);
            ComicAutoAdvance timer = Overlay(controller).GetComponent<ComicAutoAdvance>();
            timer.RestoreRemainingSeconds(2.25f);
            Button.ButtonClickedEvent stale = Find<Button>(controller, "Legendary Fleet Comic Advance").onClick;
            controller.gameObject.SetActive(false);
            controller.gameObject.SetActive(true);
            Build(controller);
            stale.Invoke();
            Assert.That(Overlay(controller).GetComponent<ComicAutoAdvance>().RemainingSeconds, Is.EqualTo(2.25f).Within(.001f));
            AssertPage(controller, LegendaryFleetComicCatalog.Find(id).Before.ImageResource);
        }

        [Test]
        public void NewVoyage_DoesNotReplayEitherReadPage()
        {
            const string id = "legendary_pirate_iron_oath";
            CoreVoyageSession session = Voyage(id, false);
            CoreGameplayDemoController controller = Fixture(session);
            BeginBattle(session, id);
            Build(controller);
            Click(controller);
            Win(session, false);
            Build(controller);
            Click(controller);
            session.SafeReturn();
            session.ReturnToPortAfterSettlement();
            session.Cargo.Water = session.Cargo.Food = 60;
            session.StartVoyage();
            BeginBattle(session, id);
            Build(controller);
            Assert.That(Overlay(controller), Is.Null, "The campaign has already read this prelude.");
            Win(session, false);
            Build(controller);
            Assert.That(Overlay(controller), Is.Null, "The campaign has already read this aftermath.");
        }

        [Test]
        public void ControllerRecreation_SkipsReadPreludeButFirstVictoryStillShowsUnreadAftermath()
        {
            const string id = "legendary_pirate_last_horizon";
            CoreVoyageSession session = Voyage(id, false);
            CoreGameplayDemoController first = Fixture(session);
            BeginBattle(session, id);
            Build(first);
            Click(first);
            CoreGameplayDemoController reloaded = Fixture(session);
            Build(reloaded);
            Assert.That(Overlay(reloaded), Is.Null, "No voyage checkpoint is needed to remember a read page.");
            Win(session, false);
            Build(reloaded);
            AssertPage(reloaded, LegendaryFleetComicCatalog.Find(id).After.ImageResource);
            Click(reloaded);
            CoreGameplayDemoController another = Fixture(session);
            Build(another);
            Assert.That(Overlay(another), Is.Null);
        }

        [Test]
        public void RetreatThenNewVoyage_DoesNotConsumeTheFirstVictoryPage()
        {
            const string id = "legendary_pirate_tide_requiem";
            CoreVoyageSession session = Voyage(id, false);
            CoreGameplayDemoController controller = Fixture(session);
            BeginBattle(session, id);
            Build(controller);
            Click(controller);
            Assert.That(session.RetreatFromBattle(), Is.True);
            session.ReturnToPortAfterSettlement();
            session.Cargo.Water = session.Cargo.Food = 60;
            session.StartVoyage();
            BeginBattle(session, id);
            Build(controller);
            Assert.That(Overlay(controller), Is.Null);
            Win(session, true);
            Build(controller);
            AssertPage(controller, LegendaryFleetComicCatalog.Find(id).After.ImageResource);
        }

        [TestCase(false)] [TestCase(true)]
        public void OlderSuspension_ReadKeysMigrateWithoutErasingNewerCampaignAcknowledgements(bool afterRead)
        {
            const string id = "legendary_pirate_last_horizon";
            const string other = "legendary_pirate_iron_oath";
            ReadProgress().MarkReadStoryKey("legendary:v1:before:" + other);
            CoreVoyageSession session = Voyage(id, false);
            CoreGameplayDemoController controller = Fixture(session);
            object record = Capture(controller, session);
            RecordType.GetField("completedTreasureStoryKeys").SetValue(record, afterRead
                ? new[] { "legendary:v1:before:" + id, "legendary:v1:after:" + id, "intro:old-map" }
                : new[] { "legendary:v1:before:" + id, "intro:old-map" });
            RecordType.GetField("pendingLegendaryVictoryId").SetValue(record, afterRead ? id : string.Empty);
            Invoke(controller, "RestoreTreasureStoryProgress", JsonUtility.FromJson(JsonUtility.ToJson(record), RecordType));
            Assert.That(ReadProgress().HasReadStoryKey("before:" + id), Is.True);
            Assert.That(ReadProgress().HasReadStoryKey("before:" + other), Is.True);
            Assert.That(ReadProgress().HasReadStoryKey("after:" + id), Is.EqualTo(afterRead));
            CoreGameplayDemoController recreated = Fixture(session);
            BeginBattle(session, id);
            Build(recreated);
            Assert.That(Overlay(recreated), Is.Null, "Migration must survive another controller rebuild.");
            Win(session, false);
            Build(recreated);
            if (afterRead) Assert.That(Overlay(recreated), Is.Null);
            else AssertPage(recreated, LegendaryFleetComicCatalog.Find(id).After.ImageResource);
        }

        [Test]
        public void NewCampaignScope_CanReadBothPagesAgain()
        {
            const string id = "legendary_pirate_iron_oath";
            ReadProgress().MarkReadStoryKey("before:" + id);
            ReadProgress().MarkReadStoryKey("after:" + id);
            comicProfileScope = Guid.NewGuid().ToString("N");
            CoreVoyageSession session = Voyage(id, false);
            CoreGameplayDemoController controller = Fixture(session);
            BeginBattle(session, id);
            Build(controller);
            AssertPage(controller, LegendaryFleetComicCatalog.Find(id).Before.ImageResource);
            Click(controller);
            Win(session, false);
            Build(controller);
            AssertPage(controller, LegendaryFleetComicCatalog.Find(id).After.ImageResource);
        }

        [Test]
        public void AnUnfinishedPage_RemainsUnreadAfterReload()
        {
            const string id = "legendary_pirate_iron_oath";
            CoreVoyageSession session = Voyage(id, false);
            CoreGameplayDemoController controller = Fixture(session);
            BeginBattle(session, id);
            Build(controller);
            Assert.That(comicReadJson, Is.Empty);
            CoreGameplayDemoController reloaded = Fixture(session);
            Build(reloaded);
            AssertPage(reloaded, LegendaryFleetComicCatalog.Find(id).Before.ImageResource);
        }

        [Test]
        public void AuthoringPreview_CannotConsumeTheCampaignPage()
        {
            const string id = "legendary_pirate_iron_oath";
            CoreVoyageSession session = Voyage(id, false);
            CoreGameplayDemoController preview = Fixture(session);
            SetField(preview, "editorPreviewBuilding", true);
            BeginBattle(session, id);
            Build(preview);
            Click(preview);
            Assert.That(comicReadJson, Is.Empty);
            CoreGameplayDemoController actual = Fixture(session);
            Build(actual);
            AssertPage(actual, LegendaryFleetComicCatalog.Find(id).Before.ImageResource);
        }

        [Test]
        public void MissingComicArt_CannotConsumeTheCampaignPage()
        {
            CoreGameplayDemoController controller = Fixture(Voyage("legendary_pirate_iron_oath", false));
            Invoke(controller, "BuildTreasureStoryOverlay", "LegendaryFleetComics/missing_test_page",
                "legendary:v1:before:legendary_pirate_iron_oath");
            Assert.That(Overlay(controller), Is.Null);
            Assert.That(comicReadJson, Is.Empty);
        }

        [Test]
        public void LegacySnapshotWithoutComicFields_RemainsReadableWithoutTreasureIntro()
        {
            const string id = "legendary_pirate_last_horizon";
            CoreVoyageSession session = Voyage(id, true);
            CoreGameplayDemoController controller = Fixture(session);
            object legacy = JsonUtility.FromJson("{}", RecordType);
            Invoke(controller, "RestoreTreasureStoryProgress", legacy);
            Build(controller);
            Assert.That(controller.GetComponentsInChildren<ComicPageGraphic>(), Is.Empty);
            Assert.That(Overlay(controller), Is.Null);
        }

        [Test]
        public void PartialSheet_RedrawAndJsonResumeKeepTheCurrentPanelUnread()
        {
            const string id = "legendary_pirate_iron_oath";
            CoreVoyageSession session = Voyage(id, false);
            CoreGameplayDemoController controller = Fixture(session);
            BeginBattle(session, id);
            Build(controller);
            Click(controller);
            Win(session, false);
            Build(controller);
            string beforeReadingAftermath = comicReadJson;
            var stale = Find<Button>(controller, "Legendary Fleet Comic Advance").onClick;
            stale.Invoke();
            Build(controller);
            stale.Invoke();
            Assert.That(Field<int>(controller, "treasureStoryPanel"), Is.EqualTo(1));
            Assert.That(comicReadJson, Is.EqualTo(beforeReadingAftermath), "One panel cannot mark all four read.");
            Overlay(controller).GetComponent<ComicAutoAdvance>().RestoreRemainingSeconds(2.25f);
            Build(controller);
            Assert.That(Overlay(controller).GetComponent<ComicAutoAdvance>().RemainingSeconds, Is.EqualTo(2.25f).Within(.001f));
            object record = Capture(controller, session);
            record = JsonUtility.FromJson(JsonUtility.ToJson(record), RecordType);
            CoreGameplayDemoController resumed = Fixture(session);
            Invoke(resumed, "RestoreTreasureStoryProgress", record);
            Build(resumed);
            Assert.That(Field<int>(resumed, "treasureStoryPanel"), Is.EqualTo(1));
            AssertPage(resumed, LegendaryFleetComicCatalog.Find(id).After.ImageResource);
            Click(resumed);
            Build(resumed);
            Assert.That(Overlay(resumed), Is.Null);
            Assert.That(comicReadJson, Is.Not.Empty);
        }

        [TestCase(0)] [TestCase(1)]
        public void TreasureEntryAndLegacyPendingIntro_NeverOpenAComic(int contentVersion)
        {
            const string id = "legendary_pirate_iron_oath";
            CoreVoyageSession session = Voyage(id, true, contentVersion);
            CoreGameplayDemoController controller = Fixture(session);
            Build(controller);
            Assert.That(controller.GetComponentsInChildren<ComicPageGraphic>(), Is.Empty);
            var phase = session.Phase;
            double hours = session.TotalHours;
            object record = Capture(controller, session);
            RecordType.GetField("treasureStoryKey").SetValue(record, "intro:comic-map");
            RecordType.GetField("treasureStoryPage").SetValue(record, 1);
            Invoke(controller, "RestoreTreasureStoryProgress", record);
            Build(controller);
            Assert.That(controller.GetComponentsInChildren<ComicPageGraphic>(), Is.Empty);
            Assert.That(Field<string>(controller, "treasureStoryKey"), Is.Empty);
            Assert.That(session.Phase, Is.EqualTo(phase));
            Assert.That(session.TotalHours, Is.EqualTo(hours));
        }

        [Test]
        public void OptionalComicSnapshotFields_AcceptLegacyAndRejectMalformedProgress()
        {
            CoreVoyageSession session = Voyage("legendary_pirate_last_horizon", false);
            CoreGameplayDemoController controller = Fixture(session);
            object record = Capture(controller, session);
            RecordType.GetField("completedTreasureStoryKeys").SetValue(record, null);
            RecordType.GetField("treasureStoryKey").SetValue(record, null);
            RecordType.GetField("pendingLegendaryVictoryId").SetValue(record, null);
            AssertRecordValid(record, true);
            RecordType.GetField("pendingLegendaryVictoryId").SetValue(record, "unrelated_encounter");
            AssertRecordValid(record, false);
            RecordType.GetField("pendingLegendaryVictoryId").SetValue(record, null);
            RecordType.GetField("treasureStoryPage").SetValue(record, -1);
            AssertRecordValid(record, false);
            RecordType.GetField("treasureStoryPage").SetValue(record, 0);
            RecordType.GetField("treasureStoryPanel").SetValue(record, -1);
            AssertRecordValid(record, false);
            RecordType.GetField("treasureStoryPanel").SetValue(record, 4);
            AssertRecordValid(record, false);
            RecordType.GetField("treasureStoryPanel").SetValue(record, 0);
            RecordType.GetField("completedTreasureStoryKeys").SetValue(record, new[] { "" });
            AssertRecordValid(record, false);
        }

        private CoreGameplayDemoController Fixture(CoreVoyageSession session)
        {
            var host = new GameObject("Legendary Comic Isolated UI Fixture");
            hosts.Add(host);
            host.SetActive(false);
            var controller = host.AddComponent<CoreGameplayDemoController>();
            SetField(controller, "editorPreviewSuppressed", true);
            SetField(controller, "editorPreviewPersistGeneratedUi", true);
            // Exercise the production modal and callbacks, without rebuilding
            // unrelated port/battle widgets or writing any player's profile.
            SetField(controller, "profileSaveBlocked", true);
            SetField(controller, "session", session);
            SetField(controller, "legendaryComicReadProgress", ReadProgress());
            var canvas = new GameObject("Comic Test Canvas", typeof(RectTransform), typeof(Canvas));
            canvas.transform.SetParent(host.transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920, 1080);
            var content = new GameObject("Comic Test Content", typeof(RectTransform));
            content.transform.SetParent(canvas.transform, false);
            SetField(controller, "contentRoot", (RectTransform)content.transform);
            host.SetActive(true);
            return controller;
        }

        private static CoreVoyageSession Voyage(string id, bool treasure, int contentVersion = 1)
        {
            var session = new CoreVoyageSession();
            string rank = new[] { "weathered", "gilded", "moonlit" }.Single(value =>
                CoreLegendaryWarships.CreateTreasureEncounters(value).Any(encounter => encounter.Id == id));
            session.RestoreTreasureProgress(new CoreTreasureProgress {
                pirateExperience = CoreVoyageSession.PirateExperienceForLevel(5),
                maps = new[] { new CoreTreasureMapSave { id = "comic-map", seed = "LEGEND-COMIC-" + id, rankId = rank, contentVersion = contentVersion } }
            });
            session.Cargo.Water = session.Cargo.Food = 60;
            if (treasure)
            {
                Assert.That(session.TrySelectTreasureMap("comic-map", out string reason), Is.True, reason);
                Assert.That(session.TryStartTreasureVoyage(out reason), Is.True, reason);
            }
            else
            {
                session.SelectRoute(2);
                session.SetSeed("LEGEND-COMIC-" + id);
                session.StartVoyage();
            }
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            return session;
        }

        private static void BeginBattle(CoreVoyageSession session, string id)
        {
            CoreMapNode node = session.Map.Nodes.Single(item => item.Encounter.Id == id);
            SetField(session, "activeNode", node);
            SetProperty(session, "ActiveEncounter", node.Encounter);
            SetProperty(session, "Phase", CoreDemoPhase.Encounter);
            Assert.That(session.ResolveChoice(0), Is.True);
        }

        private static void Win(CoreVoyageSession session, bool boarding)
        {
            session.Battle.Stage = boarding ? NavalBattleStage.Boarding : NavalBattleStage.Naval;
            session.Battle.PlayerBoardingHealth = Math.Max(1, session.Battle.PlayerBoardingHealth);
            if (boarding) session.Battle.EnemyBoardingHealth = 0;
            else session.Battle.EnemyHull = 0;
            Invoke(session, "ResolveBattleOutcome");
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
        }

        private static object Capture(CoreGameplayDemoController controller, CoreVoyageSession session)
        {
            object record = Activator.CreateInstance(RecordType);
            RecordType.GetField("format").SetValue(record, "ShatteredSail.VoyageSuspension");
            RecordType.GetField("version").SetValue(record, 1);
            RecordType.GetField("profileScope").SetValue(record, PlayerPrefs.GetString(CoreGameplayDemoController.WorldEconomyScopeKey, ""));
            RecordType.GetField("selectedOrdinaryCrew").SetValue(record, -1);
            RecordType.GetField("departureCrewShipIndex").SetValue(record, -1);
            RecordType.GetField("departureCrewPort").SetValue(record, "");
            RecordType.GetField("selectedSpecialCrew").SetValue(record, Array.Empty<string>());
            RecordType.GetField("standbyCrew").SetValue(record, Array.Empty<string>());
            RecordType.GetField("voyage").SetValue(record, session.CaptureVoyageCheckpoint());
            Invoke(controller, "CaptureTreasureStoryProgress", record);
            return record;
        }

        private static void AssertRecordValid(object record, bool expected)
        {
            Type persistence = RecordType.Assembly.GetType("ShatteredSail.Presentation.VoyageSuspensionPersistence", true);
            object[] arguments = { record, null };
            bool valid = (bool)persistence.GetMethod("Validate", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, arguments);
            Assert.That(valid, Is.EqualTo(expected), arguments[1] as string);
        }

        private static void CaptureScreen(CoreGameplayDemoController controller, string resource)
        {
            string directory = Environment.GetEnvironmentVariable("COMIC_PAGINATION_CAPTURE_DIR") ??
                Environment.GetEnvironmentVariable("LEGENDARY_COMIC_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory)) return;
            directory = Path.GetFullPath(directory);
            Assert.That(directory.StartsWith(@"D:\Game\outputs\legendary-comics-20261031\", StringComparison.OrdinalIgnoreCase) ||
                directory.StartsWith(@"D:\Game\outputs\comic-pagination-20261103\", StringComparison.OrdinalIgnoreCase), Is.True);
            Directory.CreateDirectory(directory);
            string capturePath = Path.Combine(directory, Path.GetFileName(resource) + "_panel" +
                (Field<int>(controller, "treasureStoryPanel") + 1) + ".png");
            // The same page is exercised through several entry/exit flows. Keep
            // one full-resolution production-UI capture of every distinct panel.
            if (File.Exists(capturePath)) return;
            Canvas canvas = controller.transform.Find("Comic Test Canvas").GetComponent<Canvas>();
            ComicPlaybackCapture.Capture(canvas, Overlay(controller).GetComponentInChildren<ComicPageGraphic>(), capturePath);
        }
        private static void AssertPage(CoreGameplayDemoController controller, string resource)
        {
            Transform overlay = Overlay(controller);
            Assert.That(overlay, Is.Not.Null);
            Assert.That(overlay.GetComponentsInChildren<Text>(true), Is.Empty, "Legendary comics are strictly wordless.");
            ComicPageGraphic page = overlay.GetComponentInChildren<ComicPageGraphic>();
            int panel = Field<int>(controller, "treasureStoryPanel");
            Texture2D independent = Resources.Load<Texture2D>(ComicPanelCatalog.HighResolutionPanelResource(resource, panel));
            Texture2D expected = independent != null ? independent : Resources.Load<Texture2D>(resource);
            Assert.That(expected, Is.Not.Null, "The delivered comic resource must exist: " + resource);
            Assert.That(page.texture, Is.SameAs(expected));
            Assert.That(page.uvRect, Is.EqualTo(independent != null
                ? new Rect(0f, 0f, 1f, 1f) : ComicPanelCatalog.QuadPanel(panel)));
            Assert.That(page.GetComponent<AspectRatioFitter>().aspectMode, Is.EqualTo(AspectRatioFitter.AspectMode.FitInParent));
            ComicPlaybackCapture.AssertNativeLayout(page);
        }

        private static Transform Overlay(CoreGameplayDemoController controller) => controller.GetComponentsInChildren<Transform>()
            .FirstOrDefault(item => item.name == "Legendary Fleet Comic Overlay" && item.gameObject.activeInHierarchy);
        private static T Find<T>(CoreGameplayDemoController controller, string name) where T : Component =>
            controller.GetComponentsInChildren<T>().Single(item => item.name == name);
        private static void Click(CoreGameplayDemoController controller)
        {
            // Existing story-flow cases read the whole sheet; each real click now
            // acknowledges one panel, and cannot complete the sheet prematurely.
            int first = Field<int>(controller, "treasureStoryPanel");
            string key = Field<string>(controller, "treasureStoryKey");
            LegendaryFleetComicStory story = LegendaryFleetComicCatalog.All.Single(item =>
                key.EndsWith(":" + item.EncounterId, StringComparison.Ordinal));
            string resource = (key.Contains(":after:") ? story.After : story.Before).ImageResource;
            for (int panel = first; panel < ComicPanelCatalog.QuadPanelCount; panel++)
            {
                Assert.That(Field<int>(controller, "treasureStoryPanel"), Is.EqualTo(panel));
                AssertPage(controller, resource);
                CaptureScreen(controller, resource);
                Find<Button>(controller, "Legendary Fleet Comic Advance").onClick.Invoke();
                if (panel + 1 < ComicPanelCatalog.QuadPanelCount) Build(controller);
            }
        }
        private static void Build(CoreGameplayDemoController controller) => Invoke(controller, "BuildTreasureStoryOverlayIfNeeded");
        private static object Invoke(object target, string method, params object[] arguments) =>
            target.GetType().GetMethod(method, Private).Invoke(target, arguments);
        private static T Field<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
        private static void SetField(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
        private static void SetProperty(object target, string property, object value) => target.GetType().GetProperty(property).SetValue(target, value);
    }
}
