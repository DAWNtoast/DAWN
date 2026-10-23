using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class RoyalShipyardGalleryTests
    {
        private GameObject host;
        private CoreGameplayDemoController controller;

        [TearDown]
        public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        [Test]
        public void InspectActionOpensTheSelectedVesselWithoutBuyingOrChangingTheFlagship()
        {
            Preview(1);
            CoreVoyageSession owner = controller.Session;
            decimal gold = owner.ProfileGold;
            int flagship = owner.SelectedShipIndex;
            Assert.That(flagship, Is.Not.EqualTo(1), "Inspect a vessel other than the current flagship.");
            Button inspect = Find<Button>("Shipyard Inspect Vessel");
            Assert.That(inspect.interactable, Is.True);

            inspect.onClick.Invoke();

            Assert.That(controller.EditorPreviewScreen, Is.EqualTo(CoreGameplayPreviewScreen.ShipyardDetail));
            Assert.That(Find<Text>("Shipyard Dossier Name").text, Is.EqualTo(CoreVoyageSession.Ships[1].Name));
            // Preview rebuilding creates a new session. Check the actual callback owner too,
            // so a mutation cannot be concealed by the preview's deterministic reset.
            Assert.That(owner.ProfileGold, Is.EqualTo(gold));
            Assert.That(owner.SelectedShipIndex, Is.EqualTo(flagship));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(flagship));
        }

        [Test]
        public void RetiredInspectActionCannotOpenADossierAfterBrowsingToAnotherVessel()
        {
            Preview(0);
            Button.ButtonClickedEvent retired = Find<Button>("Shipyard Inspect Vessel").onClick;
            Button next = Find<Button>("Shipyard Next Vessel");
            Assert.That(next.interactable, Is.True);
            next.onClick.Invoke();
            Button current = Find<Button>("Shipyard Inspect Vessel");
            CoreVoyageSession currentSession = controller.Session;
            string selectedName = host.GetComponentsInChildren<Text>(true)
                .Single(text => text.name.StartsWith("Shipyard Ship Name ")).text;
            Assert.That(selectedName, Is.Not.EqualTo(CoreVoyageSession.Ships[0].Name));

            retired.Invoke();

            Assert.That(controller.EditorPreviewScreen, Is.EqualTo(CoreGameplayPreviewScreen.Shipyard));
            Assert.That(controller.Session, Is.SameAs(currentSession), "A stale callback must not rebuild or replace the current view.");
            Assert.That(Find<Button>("Shipyard Inspect Vessel"), Is.SameAs(current));
            Assert.That(host.GetComponentsInChildren<Text>(true).Any(text => text.name == "Shipyard Dossier Name"), Is.False);
            current.onClick.Invoke();
            Assert.That(Find<Text>("Shipyard Dossier Name").text, Is.EqualTo(selectedName), "The replacement action inspects the newly browsed vessel.");
        }

        [Test]
        public void ReusedHeaderRestoresBeforeNavigationAndKeepsExactlyOneOriginalCallback()
        {
            host = new GameObject("Royal Shipyard Header Lifecycle Test", typeof(RectTransform));
            GameObject prefab = Resources.Load<GameObject>("UI/PortFacilityHeader");
            Assert.That(prefab, Is.Not.Null);
            GameObject instance = Object.Instantiate(prefab, host.transform, false);
            instance.name = "Port Facility Header";
            RectTransform header = instance.GetComponent<RectTransform>();
            Text title = header.Find("Port Facility Title").GetComponent<Text>();
            title.text = "皇家船坞";
            Font originalFont = title.font;
            FontStyle originalStyle = title.fontStyle;
            float originalSpacing = title.lineSpacing;
            Image regionalFrame = header.Find("Port Facility Frame").GetComponent<Image>();
            Button next = header.Find("Next Port Facility").GetComponent<Button>();
            Vector2 originalPosition = next.GetComponent<RectTransform>().anchoredPosition;
            var originalNavigation = new Button.ButtonClickedEvent();
            int navigationCount = 0;
            originalNavigation.AddListener(() =>
            {
                navigationCount++;
                Assert.That(regionalFrame.enabled, Is.True, "Restore before the destination facility binds its header.");
                Assert.That(header.Find("Royal Shipyard Header Decorations").gameObject.activeSelf, Is.False);
                Assert.That(next.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(originalPosition));
                Assert.That(title.font, Is.SameAs(originalFont));
                Assert.That(title.fontStyle, Is.EqualTo(originalStyle));
                Assert.That(title.lineSpacing, Is.EqualTo(originalSpacing));
                title.text = "断锚酒馆";
            });
            next.onClick = originalNavigation;

            // Reopening a dossier can apply the same skin repeatedly on a retained header.
            RoyalShipyardHeaderSkin.Apply((RectTransform)host.transform);
            RoyalShipyardHeaderSkin.Apply((RectTransform)host.transform);
            Assert.That(regionalFrame.enabled, Is.False);
            Assert.That(title.font, Is.SameAs(GameTypography.InterfaceFont));
            Assert.That(title.fontStyle, Is.EqualTo(FontStyle.Normal), "The authored title face must not receive synthetic bold.");
            next.onClick.Invoke();

            Assert.That(navigationCount, Is.EqualTo(1));
            Assert.That(next.onClick, Is.SameAs(originalNavigation));
            Assert.That(title.text, Is.EqualTo("断锚酒馆"));
            next.onClick.Invoke();
            Assert.That(navigationCount, Is.EqualTo(2), "The cached header's next navigation must remain usable.");

            title.text = "皇家船坞";
            RoyalShipyardHeaderSkin.Apply((RectTransform)host.transform);
            instance.SetActive(false);
            Assert.That(regionalFrame.enabled, Is.True, "Parking the cached header restores it without destroying its canvas or controls.");
            Assert.That(next.onClick, Is.SameAs(originalNavigation));
            Assert.That(header.Find("Royal Shipyard Header Decorations").gameObject.activeSelf, Is.False);
        }

        private void Preview(int index)
        {
            host = new GameObject("Royal Shipyard Gallery Test");
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Shipyard, CoreGameplayPreviewPort.GrayGull, 0);
            controller.ConfigureEditorShipyard(index, false);
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
        }

        private T Find<T>(string name) where T : Component =>
            host.GetComponentsInChildren<T>(true).Single(component => component.name == name);
    }
}
