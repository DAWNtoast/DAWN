using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class UiIdTests
    {
        private GameObject host;
        private int? previousReadingLevel;

        [SetUp]
        public void SetUp()
        {
            previousReadingLevel = GameReadability.OverrideLevel;
            GameReadability.OverrideLevel = 0;
            host = new GameObject("UI identity test", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            GameReadability.OverrideLevel = previousReadingLevel;
        }

        [Test]
        public void IdentitySurvivesRenameAndReparentWithinItsView()
        {
            Transform originalGroup = Child(host.transform, "Original group");
            Transform newGroup = Child(host.transform, "New group");
            Transform control = Child(originalGroup, "Original hierarchy name");
            UiId identity = UiId.Bind(control.gameObject, "voyage.depart");
            control.name = "Localized hierarchy caption";
            control.SetParent(newGroup, false);

            Assert.That(identity.Value, Is.EqualTo("voyage.depart"));
            Assert.That(UiId.Find(host.transform, "voyage.depart"), Is.SameAs(control));
            Assert.That(UiId.Find(originalGroup, "voyage.depart"), Is.Null);
            Assert.That(UiId.GetOrLegacyName(control), Is.EqualTo("voyage.depart"));
        }

        [Test]
        public void LookupsAreScopedAndAmbiguousIdsNeverSelectTheFirstControl()
        {
            Transform left = Child(host.transform, "Left view");
            Transform right = Child(host.transform, "Right view");
            Transform first = Child(left, "First action");
            Transform second = Child(right, "Second action");
            UiId.Bind(first.gameObject, "confirm");
            UiId.Bind(second.gameObject, "confirm");
            Child(host.transform, "confirm");

            Assert.That(UiId.Find(left, "confirm"), Is.SameAs(first));
            Assert.That(UiId.Find(right, "confirm"), Is.SameAs(second));
            Assert.That(UiId.Find(host.transform, "confirm"), Is.Null);
            Assert.That(UiId.FindOrLegacyName(host.transform, "confirm"), Is.Null,
                "Duplicate IDs must not fall through to a third element with a legacy name.");
        }

        [Test]
        public void RetiredViewsAreExcludedBeforeDestroyAndDestroyedObjectsLeaveNoStaleLookup()
        {
            Transform retired = Child(host.transform, "Retired screen");
            Transform oldButton = Child(retired, "Old action");
            UiId.Bind(oldButton.gameObject, "confirm");
            retired.gameObject.SetActive(false);
            Transform live = Child(host.transform, "New action");
            UiId.Bind(live.gameObject, "confirm");

            Assert.That(UiId.Find(host.transform, "confirm"), Is.SameAs(live));
            Assert.That(UiId.Find(host.transform, "confirm", true), Is.Null);
            Object.DestroyImmediate(retired.gameObject);
            Assert.That(UiId.Find(host.transform, "confirm", true), Is.SameAs(live));
            Object.DestroyImmediate(live.gameObject);
            Assert.That(UiId.Find(host.transform, "confirm"), Is.Null);
        }

        [Test]
        public void InactivePreviewsRequireExplicitOptInAndTheScopeRootCanBeAResult()
        {
            UiId.Bind(host, "screen");
            host.SetActive(false);
            Assert.That(UiId.Find(host.transform, "screen"), Is.Null);
            Assert.That(UiId.Find(host.transform, "screen", true), Is.SameAs(host.transform));
        }

        [Test]
        public void LegacyFallbackIsExplicitUniqueAndCannotOverrideABoundIdentity()
        {
            Transform legacy = Child(host.transform, "confirm");
            Assert.That(UiId.Find(host.transform, "confirm"), Is.Null);
            Assert.That(UiId.FindOrLegacyName(host.transform, "confirm"), Is.SameAs(legacy));
            Transform bound = Child(host.transform, "Renamed action");
            UiId.Bind(bound.gameObject, "confirm");
            Assert.That(UiId.FindOrLegacyName(host.transform, "confirm"), Is.SameAs(bound));
            Object.DestroyImmediate(bound.gameObject);
            UiId.Bind(legacy.gameObject, "cancel");
            Assert.That(UiId.FindOrLegacyName(host.transform, "confirm"), Is.Null,
                "A bound element's old hierarchy name is no longer an alias.");
            Object.DestroyImmediate(legacy.GetComponent<UiId>());
            Child(host.transform, "confirm");
            Assert.That(UiId.FindOrLegacyName(host.transform, "confirm"), Is.Null);
        }

        [Test]
        public void ExplicitRebindingChangesTheActionWithoutAccumulatingComponentsOrChangingItsName()
        {
            Transform control = Child(host.transform, "Retained button");
            UiId first = UiId.Bind(control.gameObject, "Battle Card 2");
            UiId second = UiId.Bind(control.gameObject, "Battle Card 1");
            Assert.That(second, Is.SameAs(first));
            Assert.That(control.GetComponents<UiId>(), Has.Length.EqualTo(1));
            Assert.That(control.name, Is.EqualTo("Retained button"));
            Assert.That(UiId.Find(host.transform, "Battle Card 2"), Is.Null);
            Assert.That(UiId.Find(host.transform, "Battle Card 1"), Is.SameAs(control));
        }

        [Test]
        public void EmptyIdsAreRejectedWithoutAddingAComponent()
        {
            Assert.Throws<ArgumentException>(() => UiId.Bind(host, " "));
            Assert.Throws<ArgumentNullException>(() => UiId.Bind(null, "confirm"));
            Assert.That(host.GetComponent<UiId>(), Is.Null);
            Assert.That(UiId.Find(null, "confirm"), Is.Null);
            Assert.That(UiId.GetOrLegacyName(null), Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TutorialFocusAndClickGuardsUseStableIdsWithAuthoredSceneFallback(bool legacy)
        {
            var controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Navigation, CoreGameplayPreviewPort.GrayGull, 0);
            Button east = host.GetComponentsInChildren<Button>().Single(button => button.name == "Heading East");
            Button west = host.GetComponentsInChildren<Button>().Single(button => button.name == "Heading West");
            Assert.That(east.GetComponent<UiId>(), Is.Not.Null, "The production factory must bind the control.");
            if (legacy)
                Object.DestroyImmediate(east.GetComponent<UiId>());
            else
            {
                east.name = "Renamed and regrouped heading control";
                east.transform.SetParent(Child(east.transform.parent, "New control group"), false);
            }
            // Keep this focused on tutorial routing: invoke harmless callbacks,
            // never voyage actions or player-profile persistence.
            int allowedInvocations = 0;
            int blockedInvocations = 0;
            east.onClick.RemoveAllListeners();
            west.onClick.RemoveAllListeners();
            east.onClick.AddListener(() => allowedInvocations++);
            west.onClick.AddListener(() => blockedInvocations++);
            Set(controller, "tutorialActive", true);
            Set(controller, "guidedTutorialStep", GuidedTutorialStep.FirstSail);
            Set(controller, "guidedTutorialNavigationInputStep", GuidedTutorialNavigationInputStep.Heading);
            Invoke(controller, "BuildGuidedTutorialOverlay");

            Assert.That(east.IsInteractable(), Is.True);
            Assert.That(west.IsInteractable(), Is.False);
            Assert.That(host.GetComponentsInChildren<GuidedTutorialFocusGraphic>().Any(focus => focus.Target == east.transform), Is.True);
            east.onClick.Invoke();
            west.onClick.Invoke();
            Assert.That(allowedInvocations, Is.EqualTo(1));
            Assert.That(blockedInvocations, Is.Zero);
            east.gameObject.SetActive(false);
            east.onClick.Invoke();
            Assert.That(allowedInvocations, Is.EqualTo(1), "A cached callback from a retired view stays guarded.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ShipyardTutorialCanFocusAndInspectTheFlagshipAfterTheVesselBecomesAControl(bool legacy)
        {
            var controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Shipyard, CoreGameplayPreviewPort.GrayGull, 0);
            string actionId = "Shipyard Ship " + controller.Session.SelectedShipIndex;
            Button inspect = host.GetComponentsInChildren<Button>().Single(button => button.name == actionId);
            Assert.That(inspect.GetComponent<UiId>().Value, Is.EqualTo(actionId),
                "The floating-vessel decoration must be rebound when it becomes the inspect action.");
            if (legacy) Object.DestroyImmediate(inspect.GetComponent<UiId>());
            else inspect.name = "Flagship inspect control";
            int inspections = 0;
            inspect.onClick.RemoveAllListeners();
            inspect.onClick.AddListener(() => inspections++);
            Set(controller, "tutorialActive", true);
            Set(controller, "guidedTutorialStep", GuidedTutorialStep.Shipyard);
            Invoke(controller, "BuildGuidedTutorialOverlay");

            Assert.That(inspect.IsInteractable(), Is.True);
            Assert.That(host.GetComponentsInChildren<GuidedTutorialFocusGraphic>().Any(focus => focus.Target == inspect.transform), Is.True);
            inspect.onClick.Invoke();
            Assert.That(inspections, Is.EqualTo(1));
        }

        private static Transform Child(Transform parent, string name)
        {
            var result = new GameObject(name, typeof(RectTransform)).transform;
            result.SetParent(parent, false);
            return result;
        }

        private static void Set(CoreGameplayDemoController controller, string name, object value) =>
            typeof(CoreGameplayDemoController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, value);

        private static void Invoke(CoreGameplayDemoController controller, string name) =>
            typeof(CoreGameplayDemoController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
    }
}
