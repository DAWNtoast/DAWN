using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class UiViewTests
    {
        private GameObject host;
        private int? previousReadingLevel;

        [SetUp]
        public void SetUp()
        {
            previousReadingLevel = GameReadability.OverrideLevel;
            GameReadability.OverrideLevel = 0;
            host = new GameObject("UI view lifecycle test", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            GameReadability.OverrideLevel = previousReadingLevel;
        }

        [Test]
        public void RetainedRefreshRequiresTheSameModelLayoutAndLiveTree()
        {
            Transform anchor = null;
            int refreshes = 0;
            var model = new object();
            var state = new UiViewState(model, "page", 0, 0, new Vector2(1920, 1080));
            var view = new UiView(() =>
            {
                anchor = new GameObject("Built region").transform;
                anchor.SetParent(host.transform, false);
            }, () => anchor, UiRefreshRegion.Header, _ => { refreshes++; return true; });

            Assert.That(view.TryRefresh(host.transform, state, UiRefreshRegion.Header), Is.False);
            view.Build(host.transform, state);
            Transform built = anchor;
            Assert.That(view.TryRefresh(host.transform, state, UiRefreshRegion.Header), Is.True);
            Assert.That(anchor, Is.SameAs(built));
            Assert.That(view.TryRefresh(host.transform, state, UiRefreshRegion.Header | UiRefreshRegion.Content), Is.False);
            Assert.That(view.TryRefresh(host.transform, state, UiRefreshRegion.Layout), Is.False);
            Assert.That(view.TryRefresh(host.transform, new UiViewState(new object(), "page", 0, 0, new Vector2(1920, 1080)), UiRefreshRegion.Header), Is.False);
            Assert.That(view.TryRefresh(host.transform, new UiViewState(model, "page", 1, 0, new Vector2(1920, 1080)), UiRefreshRegion.Header), Is.False);
            Assert.That(view.TryRefresh(host.transform, new UiViewState(model, "page", 0, 1, new Vector2(1920, 1080)), UiRefreshRegion.Header), Is.False);
            Assert.That(view.TryRefresh(host.transform, new UiViewState(model, "page", 0, 0, new Vector2(1280, 720)), UiRefreshRegion.Header), Is.False);
            var replacementRoot = new GameObject("Replacement root").transform;
            replacementRoot.SetParent(host.transform, false);
            Assert.That(view.TryRefresh(replacementRoot, state, UiRefreshRegion.Header), Is.False);
            anchor.SetParent(replacementRoot, false);
            replacementRoot.gameObject.SetActive(false);
            Assert.That(view.TryRefresh(host.transform, state, UiRefreshRegion.Header), Is.False);
            Object.DestroyImmediate(anchor.gameObject);
            Assert.That(view.TryRefresh(host.transform, state, UiRefreshRegion.Header), Is.False);
            Assert.That(refreshes, Is.EqualTo(1), "Unsupported/stale requests must not partially refresh a region.");
            view.Invalidate();
            Assert.That(view.TryRefresh(host.transform, state, UiRefreshRegion.Header), Is.False);
        }

        [Test]
        public void PortHeaderRefreshChangesGoldWithoutReplacingTheHeaderOrFacility()
        {
            CoreGameplayDemoController controller = Build(CoreGameplayPreviewScreen.Shipyard);
            Transform header = Find("Port Facility Header");
            Transform facility = Find("Shipyard Panel");
            controller.Session.RestoreProfile(123.45m, controller.Session.CurrentPort);

            Assert.That(Refresh(controller, UiRefreshRegion.Header), Is.True);
            Assert.That(Find("Port Facility Header"), Is.SameAs(header));
            Assert.That(Find("Shipyard Panel"), Is.SameAs(facility));
            Assert.That(Find("Port Facility Gold").GetComponent<Text>().text, Does.Contain("123.45"));
        }

        [TestCase(UiRefreshRegion.Content)]
        [TestCase(UiRefreshRegion.Layout)]
        public void UnsupportedOrLayoutRequestsUseTheFullBuild(UiRefreshRegion dirty)
        {
            CoreGameplayDemoController controller = Build(CoreGameplayPreviewScreen.PortHub);
            int oldHeader = Find("Port Hub").GetInstanceID();
            Assert.That(Refresh(controller, dirty), Is.False);
            Assert.That(Find("Port Hub").GetInstanceID(), Is.Not.EqualTo(oldHeader));
            Assert.That(Refresh(controller, UiRefreshRegion.Header), Is.True,
                "The fallback build must register its replacement view.");
        }

        [Test]
        public void ChangedReadingLayoutAndDestroyedHeaderBothRebuildInsteadOfUsingStaleObjects()
        {
            CoreGameplayDemoController controller = Build(CoreGameplayPreviewScreen.PortHub);
            int oldHeader = Find("Port Hub").GetInstanceID();
            GameReadability.OverrideLevel = 1;
            Assert.That(Refresh(controller, UiRefreshRegion.Header), Is.False);
            Assert.That(Find("Port Hub").GetInstanceID(), Is.Not.EqualTo(oldHeader));
            Assert.That(((RectTransform)Find("Port Hub")).rect.height, Is.EqualTo(PhaseSurfaceLayout.GetHeaderHeight(false)));
            Object.DestroyImmediate(Find("Port Hub").gameObject);
            Assert.That(Refresh(controller, UiRefreshRegion.Header), Is.False);
            Assert.That(Find("Port Hub"), Is.Not.Null);
            Assert.That(Refresh(controller, UiRefreshRegion.Header), Is.True);
        }

        [TestCase("port")]
        [TestCase("reading")]
        [TestCase("viewport")]
        public void BackgroundHeaderUpdatesPreserveOpenWarehouseQuoteAndControlsAfterStateChanges(string changed)
        {
            CoreGameplayDemoController controller = Build(CoreGameplayPreviewScreen.Warehouse);
            controller.ConfigureEditorPreviewWarehouseStorage(2);
            Transform modal = Find("Warehouse Cargo Sale");
            Transform cancel = Find("Cancel Warehouse Cargo Sale");
            Transform header = Find("Port Facility Header");
            Assert.That(modal, Is.Not.Null);
            Assert.That(cancel, Is.Not.Null);
            Button.ButtonClickedEvent click = cancel.GetComponent<Button>().onClick;
            if (changed == "port")
                controller.Session.RestoreProfile(123.45m, "白贝港");
            else if (changed == "reading")
                GameReadability.OverrideLevel = 1;
            else
                ((RectTransform)Find("Phase Content")).sizeDelta += new Vector2(40, 0);

            typeof(CoreGameplayDemoController).GetMethod("UpdateUiViewHeader", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);

            Assert.That(Find("Warehouse Cargo Sale"), Is.SameAs(modal), "Background polling must not discard the active quote.");
            Assert.That(Find("Cancel Warehouse Cargo Sale"), Is.SameAs(cancel));
            Assert.That(cancel.GetComponent<Button>().onClick, Is.SameAs(click));
            Assert.That(Find("Port Facility Header"), Is.SameAs(header));
            Assert.That(cancel.gameObject.activeInHierarchy, Is.True);
            if (changed == "port")
                Assert.That(Find("Port Facility Port").GetComponent<Text>().text, Is.EqualTo("白贝港"));
        }

        [Test]
        public void APhaseChangeRoutesToTheNewViewInsteadOfRefreshingTheOldOne()
        {
            CoreGameplayDemoController controller = Build(CoreGameplayPreviewScreen.Navigation);
            Assert.That(Find("Navigation Chart"), Is.Not.Null);
            Assert.That(controller.Session.SafeReturn(), Is.True);
            Assert.That(Refresh(controller, UiRefreshRegion.Header), Is.False);
            Assert.That(Find("Navigation Chart"), Is.Null);
            Assert.That(Find("Settlement Card"), Is.Not.Null);
        }

        [Test]
        public void BattleRefreshRetainsTheStageButStageChangesRequireAFullBuild()
        {
            CoreGameplayDemoController controller = Build(CoreGameplayPreviewScreen.Battle);
            Transform battleRoot = Find("Battle Card");
            controller.Session.Battle.Energy = 0;
            Assert.That(Refresh(controller, UiRefreshRegion.Battle), Is.True);
            Assert.That(Find("Battle Card"), Is.SameAs(battleRoot));
            Assert.That(Find("Resource 能 Value").GetComponent<Text>().text,
                Is.EqualTo("0/" + controller.Session.Battle.MaxEnergy));
            int oldStage = battleRoot.GetInstanceID();
            controller.Session.Battle.Stage = controller.Session.Battle.Stage == NavalBattleStage.Preparation
                ? NavalBattleStage.Naval : NavalBattleStage.Preparation;
            Assert.That(Refresh(controller, UiRefreshRegion.Battle), Is.False);
            Assert.That(Find("Battle Card").GetInstanceID(), Is.Not.EqualTo(oldStage));
        }

        private CoreGameplayDemoController Build(CoreGameplayPreviewScreen screen)
        {
            var controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(screen, CoreGameplayPreviewPort.GrayGull, 0);
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
            return controller;
        }

        private Transform Find(string id) => UiId.FindOrLegacyName(host.transform, id);

        private static bool Refresh(CoreGameplayDemoController controller, UiRefreshRegion dirty) =>
            (bool)typeof(CoreGameplayDemoController).GetMethod("RequestUiViewRefresh", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, new object[] { dirty });
    }
}
