using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Core.Tests
{
    public sealed class OpeningPortComicPlaybackTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<GameObject> hosts = new List<GameObject>();
        private string json;
        private string scope;
        private OpeningPortComicProgress Progress => new OpeningPortComicProgress(scope, () => json, value => json = value);

        [SetUp]
        public void FreshCampaign() { json = string.Empty; scope = Guid.NewGuid().ToString("N"); }

        [TearDown]
        public void Cleanup()
        {
            foreach (GameObject host in hosts) if (host != null) Object.DestroyImmediate(host);
            hosts.Clear();
        }

        private static IEnumerable<TestCaseData> Stories()
        {
            yield return new TestCaseData("opening");
            foreach (ArrivalComicStory story in ArrivalComicCatalog.All)
                yield return new TestCaseData(OpeningPortComicProgress.ArrivalKey(story.Id));
        }

        private static IEnumerable<TestCaseData> Arrivals()
        {
            foreach (ArrivalComicStory story in ArrivalComicCatalog.All)
                foreach (VoyageOutcome outcome in new[] { VoyageOutcome.ArrivedAtPort, VoyageOutcome.SafeReturn, VoyageOutcome.Failure })
                    yield return new TestCaseData(story.PortName, outcome);
        }

        [TestCaseSource(nameof(Stories))]
        public void EveryStory_ShowsOneWordlessProportionalPanelAndCompletesOnce(string key)
        {
            CoreGameplayDemoController controller = Fixture();
            Progress.TryQueue(key);
            Resume(controller);
            string prefix = key == "opening" ? "Opening Comic" : "Arrival Comic";
            CanvasGroup background = Field<RectTransform>(controller, "contentRoot").GetComponent<CanvasGroup>();
            Assert.That(background.interactable, Is.False);
            Assert.That(background.blocksRaycasts, Is.False);
            for (int index = 0; index < OpeningPortComicProgress.PageCount(key); index++)
            {
                string resource = Resource(key, index);
                for (int panel = 0; panel < ComicPanelCatalog.QuadPanelCount; panel++)
                {
                    AssertPage(controller, prefix, resource, index, panel);
                    CaptureScreen(controller, resource + "_panel" + (panel + 1));
                    RectTransform before = Field<RectTransform>(controller, "openingComicOverlay");
                    Resume(controller);
                    Assert.That(Field<RectTransform>(controller, "openingComicOverlay"), Is.SameAs(before), "A redraw cannot reset the current panel.");
                    Assert.That(Progress.HasRead(key), Is.False);
                    Click(controller);
                }
            }
            Assert.That(Field<RectTransform>(controller, "openingComicOverlay"), Is.Null);
            Assert.That(background.interactable, Is.True);
            Assert.That(background.blocksRaycasts, Is.True);
            Assert.That(Progress.HasRead(key), Is.True);
            Assert.That(Progress.TryQueue(key), Is.False);
            Resume(controller);
            Assert.That(Field<RectTransform>(controller, "openingComicOverlay"), Is.Null);
            CoreGameplayDemoController reloaded = Fixture();
            Resume(reloaded);
            Assert.That(Field<RectTransform>(reloaded, "openingComicOverlay"), Is.Null);
        }

        [TestCaseSource(nameof(Stories))]
        public void InterruptionAndReload_ResumeUnfinishedPageAndRejectOldCallbacks(string key)
        {
            CoreGameplayDemoController first = Fixture();
            Progress.TryQueue(key); Resume(first);
            Button.ButtonClickedEvent stale = Advance(first).onClick;
            Click(first);
            Assert.That(Progress.PendingPage, Is.Zero);
            Assert.That(Progress.PendingPanel, Is.EqualTo(1));
            Invoke(first, "CloseOpeningComic");
            Assert.That(Progress.HasRead(key), Is.False);
            CoreGameplayDemoController resumed = Fixture();
            Resume(resumed);
            stale.Invoke();
            Assert.That(Progress.PendingPage, Is.Zero);
            Assert.That(Progress.PendingPanel, Is.EqualTo(1));
            AssertPage(resumed, key == "opening" ? "Opening Comic" : "Arrival Comic", Resource(key, 0), 0, 1);
        }

        [TestCaseSource(nameof(Arrivals))]
        public void OnlyConfirmedSuccessfulArrival_QueuesItsExactPort(string port, VoyageOutcome outcome)
        {
            var session = new CoreVoyageSession();
            var result = new VoyageSettlement { Outcome = outcome, Destination = port };
            SetProperty(session, "Phase", CoreDemoPhase.Settlement);
            SetProperty(session, "Settlement", result);
            CoreGameplayDemoController controller = Fixture(session);
            Invoke(controller, "QueueArrivalComic", result);
            Assert.That(Progress.PendingStoryKey, Is.Empty, "Viewing the settlement has not confirmed the arrival.");
            session.ReturnToPortAfterSettlement();
            Invoke(controller, "QueueArrivalComic", result);
            string expected = outcome == VoyageOutcome.ArrivedAtPort
                ? OpeningPortComicProgress.ArrivalKey(ArrivalComicCatalog.Find(port).Id) : string.Empty;
            Assert.That(Progress.PendingStoryKey, Is.EqualTo(expected));
        }

        [Test]
        public void OldPortLoadAndUnrelatedPorts_DoNotInventArrivalOrOpeningPlayback()
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(500m, "白贝港");
            CoreGameplayDemoController controller = Fixture(session);
            Resume(controller);
            Assert.That(Field<RectTransform>(controller, "openingComicOverlay"), Is.Null);
            session.RestoreProfile(500m, "断锚港");
            Invoke(controller, "QueueArrivalComic", new VoyageSettlement { Outcome = VoyageOutcome.ArrivedAtPort, Destination = "断锚港" });
            Assert.That(Progress.PendingStoryKey, Is.Empty);
        }

        [TestCase(true, false)] [TestCase(false, true)]
        public void FailedSaveAndEditorPreview_DoNotQueueOrPlay(bool blocked, bool preview)
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(500m, "白贝港");
            CoreGameplayDemoController controller = Fixture(session);
            SetField(controller, "profileSaveBlocked", blocked);
            SetField(controller, "editorPreviewBuilding", preview);
            Invoke(controller, "QueueArrivalComic", new VoyageSettlement { Outcome = VoyageOutcome.ArrivedAtPort, Destination = "白贝港" });
            Assert.That(Progress.PendingStoryKey, Is.Empty);
            Progress.TryQueue("opening"); Resume(controller);
            Assert.That(Field<RectTransform>(controller, "openingComicOverlay"), Is.Null);
        }

        [Test]
        public void SameFrameDuplicateClick_AdvancesOnlyOnePanelAndResetsTwelveSeconds()
        {
            CoreGameplayDemoController controller = Fixture();
            Progress.TryQueue("opening"); Resume(controller);
            Advance(controller).onClick.Invoke();
            Advance(controller).onClick.Invoke();
            Assert.That(Progress.PendingPage, Is.Zero);
            Assert.That(Progress.PendingPanel, Is.EqualTo(1));
            Assert.That(Field<RectTransform>(controller, "openingComicOverlay").GetComponent<ComicAutoAdvance>().RemainingSeconds,
                Is.EqualTo(12f).Within(.001f));
        }

        [Test]
        public void Modal_RestoresPreviouslyDisabledNavigationAndPointerState()
        {
            var eventHost = new GameObject("Comic Test Event System", typeof(EventSystem));
            hosts.Add(eventHost);
            EventSystem events = eventHost.GetComponent<EventSystem>();
            events.sendNavigationEvents = false;
            CoreGameplayDemoController controller = Fixture();
            CanvasGroup group = Field<RectTransform>(controller, "contentRoot").gameObject.AddComponent<CanvasGroup>();
            group.interactable = false; group.blocksRaycasts = true;
            Progress.TryQueue("opening"); Resume(controller);
            Assert.That(events.sendNavigationEvents, Is.False);
            Invoke(controller, "CloseOpeningComic");
            Assert.That(events.sendNavigationEvents, Is.False);
            Assert.That(group.interactable, Is.False);
            Assert.That(group.blocksRaycasts, Is.True);
            Assert.That(Progress.PendingPage, Is.Zero);
        }

        private CoreGameplayDemoController Fixture(CoreVoyageSession session = null)
        {
            var host = new GameObject("Opening Arrival Comic Isolated Fixture");
            hosts.Add(host); host.SetActive(false);
            var controller = host.AddComponent<CoreGameplayDemoController>();
            SetField(controller, "editorPreviewSuppressed", true);
            SetField(controller, "editorPreviewPersistGeneratedUi", true);
            SetField(controller, "profileSaveBlocked", true);
            SetField(controller, "session", session ?? new CoreVoyageSession());
            SetField(controller, "openingPortComicProgress", Progress);
            var canvas = new GameObject("Comic Test Canvas", typeof(RectTransform), typeof(Canvas));
            canvas.transform.SetParent(host.transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920, 1080);
            var content = new GameObject("Comic Test Content", typeof(RectTransform));
            content.transform.SetParent(canvas.transform, false);
            SetField(controller, "contentRoot", (RectTransform)content.transform);
            host.SetActive(true);
            SetField(controller, "profileSaveBlocked", false);
            return controller;
        }

        private static void AssertPage(CoreGameplayDemoController controller, string prefix, string resource, int index, int panel)
        {
            RectTransform overlay = Field<RectTransform>(controller, "openingComicOverlay");
            Assert.That(overlay, Is.Not.Null);
            Assert.That(overlay.name, Is.EqualTo(prefix + " Overlay"));
            Assert.That(overlay.GetComponentsInChildren<Text>(true), Is.Empty);
            Assert.That(overlay.GetComponentsInChildren<Button>(true), Has.Length.EqualTo(1));
            ComicPageGraphic page = overlay.GetComponentInChildren<ComicPageGraphic>();
            Texture2D independent = Resources.Load<Texture2D>(ComicPanelCatalog.HighResolutionPanelResource(resource, panel));
            Texture2D expected = independent != null ? independent : Resources.Load<Texture2D>(resource);
            Assert.That(expected, Is.Not.Null, resource);
            Assert.That(page.texture, Is.SameAs(expected));
            Rect source = independent != null ? new Rect(0f, 0f, 1f, 1f) : ComicPanelCatalog.QuadPanel(panel);
            Assert.That(page.uvRect, Is.EqualTo(source));
            Canvas.ForceUpdateCanvases();
            Assert.That(page.rectTransform.rect.width / page.rectTransform.rect.height,
                Is.EqualTo(expected.width * source.width / (expected.height * source.height)).Within(.01f));
            Assert.That(page.rectTransform.rect.width, Is.LessThanOrEqualTo(overlay.rect.width + 1f));
            Assert.That(page.rectTransform.rect.height, Is.LessThanOrEqualTo(overlay.rect.height + 1f));
            Assert.That(Field<int>(controller, "openingComicPageIndex"), Is.EqualTo(index));
            Assert.That(Field<int>(controller, "openingComicPanelIndex"), Is.EqualTo(panel));
            Assert.That(overlay.GetComponent<Canvas>().sortingOrder, Is.EqualTo(1600));
        }

        private static string Resource(string key, int index) => key == "opening"
            ? OpeningComicCatalog.Pages[index].ImageResource : ArrivalComicCatalog.FindById(key.Substring("arrival:".Length)).Pages[index];
        private static Button Advance(CoreGameplayDemoController controller) =>
            Field<RectTransform>(controller, "openingComicOverlay").GetComponentInChildren<Button>();
        private static void Click(CoreGameplayDemoController controller)
        {
            // EditMode runs in a single frame; independent simulated clicks have their own frame guard.
            SetField(controller, "openingComicLastAdvanceFrame", -1);
            Advance(controller).onClick.Invoke();
        }
        private static void Resume(CoreGameplayDemoController controller) => Invoke(controller, "ResumeOpeningPortComic");
        private static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
        private static void SetField(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        private static void SetProperty(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);

        private static void CaptureScreen(CoreGameplayDemoController controller, string resource)
        {
            string directory = Environment.GetEnvironmentVariable("COMIC_PAGINATION_CAPTURE_DIR") ??
                Environment.GetEnvironmentVariable("OPENING_PORT_COMIC_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory)) return;
            directory = Path.GetFullPath(directory);
            Assert.That(directory.StartsWith(@"D:\Game\outputs\opening-port-comics-20261031\", StringComparison.OrdinalIgnoreCase) ||
                directory.StartsWith(@"D:\Game\outputs\comic-pagination-20261103\", StringComparison.OrdinalIgnoreCase), Is.True);
            Directory.CreateDirectory(directory);
            Canvas canvas = controller.transform.Find("Comic Test Canvas").GetComponent<Canvas>();
            ComicPlaybackCapture.Capture(canvas, Field<RectTransform>(controller, "openingComicOverlay").GetComponentInChildren<ComicPageGraphic>(),
                Path.Combine(directory, Path.GetFileName(resource) + ".png"));
        }
    }
}