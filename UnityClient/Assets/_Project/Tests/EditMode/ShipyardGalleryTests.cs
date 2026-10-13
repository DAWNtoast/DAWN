using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class ShipyardGalleryTests
    {
        private GameObject host;
        private CoreGameplayDemoController controller;
        [TearDown] public void Cleanup() { if (host != null) Object.DestroyImmediate(host); }
        private void Preview(int index, bool detail, int page = 0)
        {
            host = new GameObject("Shipyard Gallery Test");
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Shipyard, CoreGameplayPreviewPort.GrayGull, 0);
            controller.ConfigureEditorShipyard(index, detail, page);
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
        }
        private T Find<T>(string name) where T : Component => host.GetComponentsInChildren<T>(true).Single(item => item.name == name);

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)]
        public void EveryVesselHasWaterContactIndependentOfHoverAndNoNavyRectangle(int index)
        {
            Preview(index, false);
            Button ship = Find<Button>("Shipyard Ship " + index);
            Assert.That(ship.interactable, Is.True, "Owned and unaffordable vessels remain inspectable.");
            Assert.That(host.GetComponentsInChildren<Button>(true).Count(item => item.name.StartsWith("Shipyard Ship ")), Is.EqualTo(1));
            Graphic art = ship.targetGraphic;
            Assert.That(art, Is.TypeOf<RawImage>());
            Assert.That(art.raycastTarget, Is.True);
            if (index > 0) Assert.That(art.GetComponent<BattleShipCutout>().OwnedMaterial, Is.Not.Null);
            BattleShipWaterGraphic water = Find<BattleShipWaterGraphic>("Shipyard Water Contact");
            Assert.That(water.raycastTarget, Is.False);
            Assert.That(water.rectTransform.anchoredPosition.y, Is.InRange(-160f, -130f));
            RawImage hull = (RawImage)art;
            Vector3 waterContact = water.rectTransform.InverseTransformPoint(
                hull.rectTransform.TransformPoint(ShipWaterlineLayout.HullContactLocal(hull)));
            Assert.That(waterContact.y, Is.EqualTo(-4f).Within(0.1f), "Atlas padding must not lift the keel above the harbour water.");
            RectMask2D clip = Find<RectMask2D>("Shipyard Waterline Clip");
            Assert.That(art.transform.IsChildOf(clip.transform), Is.True, "The hull and equipment share their waterline clip.");
            foreach (RawImage layer in ship.GetComponentsInChildren<RawImage>(true))
                Assert.That(layer.transform.IsChildOf(clip.transform), Is.True);
            Assert.That(clip.rectTransform.anchoredPosition.y - clip.rectTransform.sizeDelta.y * 0.5f, Is.EqualTo(-153f));
            Vector3 position = art.transform.position;
            Color original = art.color;
            EventTrigger trigger = ship.GetComponent<EventTrigger>();
            trigger.triggers.Single(item => item.eventID == EventTriggerType.PointerEnter).callback.Invoke(new BaseEventData(null));
            Assert.That(art.color, Is.EqualTo(original), "Hover must not brighten the vessel art.");
            Assert.That(Find<Image>("Shipyard Berth Glow " + index).color.a, Is.Zero, "The berth must not paint a hover glow.");
            Assert.That(ship.GetComponentsInChildren<Outline>().All(outline => !outline.enabled), Is.True);
            Assert.That(art.transform.position, Is.EqualTo(position));
            trigger.triggers.Single(item => item.eventID == EventTriggerType.PointerExit).callback.Invoke(new BaseEventData(null));
            Assert.That(art.color, Is.EqualTo(original));
            Assert.That(ship.GetComponentsInChildren<Outline>().All(outline => !outline.enabled), Is.True);
            controller.RefreshEditorPreview();
            Assert.That(Find<Button>("Shipyard Ship " + index), Is.Not.Null, "Refresh retains the chosen ship index.");
        }

        [TestCase(0, 0)] [TestCase(1, 0)] [TestCase(2, 0)] [TestCase(3, 0)]
        [TestCase(0, 1)] [TestCase(1, 1)] [TestCase(2, 1)] [TestCase(3, 1)]
        [TestCase(0, 2)] [TestCase(1, 2)] [TestCase(2, 2)] [TestCase(3, 2)]
        [TestCase(4, 0)] [TestCase(5, 0)] [TestCase(6, 0)] [TestCase(7, 0)]
        [TestCase(8, 0)] [TestCase(9, 0)] [TestCase(10, 0)] [TestCase(11, 0)]
        public void DossierSeparatesPagesAndKeepsReadableControlsInsideParchment(int index, int page)
        {
            Preview(index, true, page);
            AssertCurrentPage(page);
            foreach (string tab in new[] { "Performance", "Background", "Equipment" })
            {
                Button button = Find<Button>("Shipyard Dossier Tab " + tab);
                Assert.That(button.GetComponentInChildren<Text>().fontSize, Is.GreaterThanOrEqualTo(23));
            }
            RectTransform detail = Find<RectTransform>("Shipyard Detail");
            Canvas.ForceUpdateCanvases();
            foreach (Button button in detail.GetComponentsInChildren<Button>(true))
            {
                Vector3[] corners = new Vector3[4];
                button.GetComponent<RectTransform>().GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 local = detail.InverseTransformPoint(corner);
                    Assert.That(local.y, Is.InRange(-346f, 360f), button.name + " must stay inside the paper margins.");
                    Assert.That(local.x, Is.InRange(-735.99f, 735.99f), button.name + " must stay inside the paper sides.");
                }
            }
            decimal gold = controller.Session.ProfileGold;
            int active = controller.Session.SelectedShipIndex;
            if (page == (int)ShipyardDossierTab.Equipment)
                Assert.That(host.GetComponentsInChildren<Button>(true).Any(button => button.name == "Confirm Shipyard Vessel"), Is.False);
            else
                Find<Button>("Confirm Shipyard Vessel").onClick.Invoke();
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold), "Editor preview must not buy ships.");
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(active));
            controller.RefreshEditorPreview();
            Assert.That(controller.EditorPreviewScreen, Is.EqualTo(CoreGameplayPreviewScreen.ShipyardDetail));
            Assert.That(Find<Text>("Shipyard Dossier Name").text, Is.EqualTo(ShatteredSail.Core.Gameplay.CoreVoyageSession.Ships[index].Name));
            AssertCurrentPage(page);
        }
        [TestCase(2)]
        public void PerformancePageUsesFiveNormalizedBars(int index)
        {
            Preview(index, true, 0);
            float[] maximums = {
                CoreVoyageSession.Ships.Max(ship => ship.MaxHull), CoreVoyageSession.Ships.Max(ship => ship.Armor),
                CoreVoyageSession.Ships.Max(ship => ship.Firepower), CoreVoyageSession.Ships.Max(ship => ship.SpeedKnots),
                CoreVoyageSession.Ships.Max(ship => ship.CargoCapacity),
            };
            float[] values =
            {
                CoreVoyageSession.Ships[index].MaxHull,
                CoreVoyageSession.Ships[index].Armor,
                CoreVoyageSession.Ships[index].Firepower,
                CoreVoyageSession.Ships[index].SpeedKnots,
                CoreVoyageSession.Ships[index].CargoCapacity,
            };
            for (int stat = 0; stat < maximums.Length; stat++)
            {
                Image track = Find<Image>("Shipyard Stat Bar " + stat);
                Image fill = Find<Image>("Shipyard Stat Bar Fill " + stat);
                bool full = values[stat] >= maximums[stat] - 0.001f;
                float innerWidth = track.rectTransform.rect.width -
                    (track.rectTransform.rect.height - fill.rectTransform.rect.height);
                Assert.That(fill.rectTransform.rect.width / innerWidth,
                    Is.EqualTo(Mathf.Clamp01(values[stat] / maximums[stat])).Within(0.001f),
                    "Each stat must use the same fleet-wide scale within the bar's available width.");
                if (full)
                    Assert.That(fill.color.g, Is.GreaterThan(fill.color.r), "A maximum stat uses the green fill.");
                else
                    Assert.That(fill.color.r, Is.GreaterThan(fill.color.g), "A below-maximum stat uses the yellow fill.");
                Assert.That(Find<Text>("Shipyard Stat Delta " + stat).text, Is.Empty,
                    "An unmodified vessel should not show a component delta.");
            }
        }

        [Test]
        public void DossierTabsAreInspectableInPreviewWithoutPurchasingOrChangingTheFlagship()
        {
            Preview(1, true);
            decimal gold = controller.Session.ProfileGold;
            int flagship = controller.Session.SelectedShipIndex;
            string[] tabs = { "Performance", "Background", "Equipment", "Performance" };
            for (int index = 0; index < tabs.Length; index++)
            {
                Find<Button>("Shipyard Dossier Tab " + tabs[index]).onClick.Invoke();
                AssertCurrentPage(index % 3);
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(flagship));
            }
        }

        [Test]
        public void EquipmentPreviewCannotPersistUnloadActionsOrReadPlayerVesselOwnership()
        {
            string inventory = PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey, "missing");
            bool hadInventory = PlayerPrefs.HasKey(CoreGameplayDemoController.ComponentInventoryKey);
            decimal gold = ProfileGoldPersistence.Load();
            int selected = PlayerPrefs.GetInt(CoreGameplayDemoController.SelectedShipKey, -1);
            Preview(0, true, 2);
            foreach (ComponentSlot slot in System.Enum.GetValues(typeof(ComponentSlot)))
            {
                Button unload = Find<Button>("Shipyard Unequip Component " + slot);
                Assert.That(unload.interactable, Is.False);
                unload.onClick.Invoke();
                Assert.That(controller.ShipComponent(0, slot), Is.Not.Null, "An editor inspection must not alter the sample loadout.");
            }
            controller.ConfigureEditorShipyard(3, true, 2);
            Assert.That(Find<Text>("Shipyard Equipment Installed Heading").text, Does.Contain("购买后"),
                "Preview ownership must be deterministic and must not borrow the player's persisted vessel flags.");
            Assert.That(Find<Button>("Shipyard Equip Component").interactable, Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.ComponentInventoryKey), Is.EqualTo(hadInventory));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey, "missing"), Is.EqualTo(inventory));
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(gold));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.SelectedShipKey, -1), Is.EqualTo(selected));
        }

        private void AssertCurrentPage(int page)
        {
            Text[] text = host.GetComponentsInChildren<Text>(true);
            Assert.That(text.Count(item => item.name.StartsWith("Shipyard Dossier Stat ")), Is.EqualTo(page == 0 ? 5 : 0),
                "Performance figures must be isolated from the background and equipment pages.");
            Assert.That(text.Any(item => item.name == "Shipyard Dossier Specialization"), Is.False);
            Assert.That(text.Any(item => item.name == "Shipyard Dossier Story"), Is.EqualTo(page == 1));
            if (page == 1)
            {
                Text story = Find<Text>("Shipyard Dossier Story");
                Assert.That(story.text.Length, Is.GreaterThan(60));
                AssertReadableParagraph(story);
            }
            Assert.That(host.GetComponentsInChildren<Button>(true).Count(item => item.name.StartsWith("Shipyard Equipment Slot ")),
                Is.EqualTo(page == 2 ? 5 : 0));
        }

        private static void AssertReadableParagraph(Text label)
        {
            Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(23));
            Assert.That(label.resizeTextForBestFit, Is.False);
            Canvas.ForceUpdateCanvases();
            Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height), label.name + " must not truncate.");
        }
    }
}
