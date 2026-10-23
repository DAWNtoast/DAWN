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

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)]
        public void OpeningEitherDossierPageKeepsTheHarbourVesselInItsOriginalCentralBerth(int index)
        {
            Preview(index, false);
            Canvas.ForceUpdateCanvases();
            RectTransform originalPanel = Find<RectTransform>("Shipyard Panel");
            Button inspect = Find<Button>("Shipyard Ship " + index);
            Rect originalVessel = BoundsIn(originalPanel, inspect.GetComponent<RectTransform>());
            RawImage[] originalLayers = inspect.GetComponentsInChildren<RawImage>(true);
            var layerBounds = originalLayers.ToDictionary(layer => layer.name, layer => BoundsIn(originalPanel, layer.rectTransform));
            var layerTextures = originalLayers.ToDictionary(layer => layer.name, layer => layer.texture);
            var layerUvs = originalLayers.ToDictionary(layer => layer.name, layer => layer.uvRect);
            string[] waterNames = { "Shipyard Water Shadow", "Shipyard Water Contact" };
            var waterBounds = waterNames.ToDictionary(name => name, name => BoundsIn(originalPanel, Find<RectTransform>(name)));
            Assert.That(inspect.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(Vector2.zero));
            inspect.onClick.Invoke();

            foreach (string tab in new[] { "Performance", "Background" })
            {
                Find<Button>("Shipyard Dossier Tab " + tab).onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                RectTransform panel = Find<RectTransform>("Shipyard Panel");
                RectTransform vessel = Find<RectTransform>("Shipyard Ship " + index);
                RectTransform detail = Find<RectTransform>("Shipyard Detail");
                AssertRect(BoundsIn(panel, vessel), originalVessel, tab + " harbour vessel placement");
                Assert.That(vessel.parent, Is.SameAs(panel));
                Assert.That(vessel.GetSiblingIndex(), Is.LessThan(detail.GetSiblingIndex()), "The central vessel remains behind the sheet.");
                Assert.That(vessel.GetComponent<Button>(), Is.Null, "The background vessel must not consume dossier clicks.");
                RawImage[] layers = vessel.GetComponentsInChildren<RawImage>(true);
                CollectionAssert.AreEquivalent(layerBounds.Keys, layers.Select(layer => layer.name));
                foreach (RawImage layer in layers)
                {
                    AssertRect(BoundsIn(panel, layer.rectTransform), layerBounds[layer.name], tab + " " + layer.name);
                    Assert.That(layer.texture, Is.SameAs(layerTextures[layer.name]));
                    Assert.That(layer.uvRect, Is.EqualTo(layerUvs[layer.name]));
                    Assert.That(layer.raycastTarget, Is.False);
                }
                foreach (string waterName in waterNames)
                    AssertRect(BoundsIn(panel, Find<RectTransform>(waterName)), waterBounds[waterName], tab + " " + waterName);
                RawImage portrait = Find<RawImage>("Shipyard Dossier Portrait");
                Assert.That(portrait.transform.IsChildOf(detail), Is.True);
                Assert.That(layers.Any(layer => layer.texture == portrait.texture), Is.False,
                    "The framed portrait is a separate painting from the harbour ship atlas.");
            }
        }

        [TestCase(0, 0)] [TestCase(1, 0)] [TestCase(2, 0)] [TestCase(3, 0)]
        [TestCase(0, 1)] [TestCase(1, 1)] [TestCase(2, 1)] [TestCase(3, 1)]
        [TestCase(0, 2)] [TestCase(1, 2)] [TestCase(2, 2)] [TestCase(3, 2)]
        [TestCase(4, 0)] [TestCase(5, 0)] [TestCase(6, 0)] [TestCase(7, 0)]
        [TestCase(8, 0)] [TestCase(9, 0)] [TestCase(10, 0)] [TestCase(11, 0)]
        [TestCase(8, 1)] [TestCase(8, 2)] [TestCase(10, 1)] [TestCase(10, 2)]
        public void DossierSeparatesPagesAndKeepsReadableControlsInsideTheirSurface(int index, int page)
        {
            Preview(index, true, page);
            AssertCurrentPage(page);
            if (page == (int)ShipyardDossierTab.Equipment)
            {
                AssertEquipmentWindow(index);
                controller.RefreshEditorPreview();
                Assert.That(controller.EditorPreviewScreen, Is.EqualTo(CoreGameplayPreviewScreen.ShipyardDetail));
                AssertCurrentPage(page);
                AssertEquipmentWindow(index);
                return;
            }
            foreach (string tab in new[] { "Performance", "Background", "Equipment" })
            {
                Button button = Find<Button>("Shipyard Dossier Tab " + tab);
                Assert.That(button.GetComponentInChildren<Text>().fontSize, Is.GreaterThanOrEqualTo(23));
            }
            RectTransform detail = Find<RectTransform>("Shipyard Detail");
            Canvas.ForceUpdateCanvases();
            Button back = Find<Button>("Return To Port Hub");
            RectTransform header = Find<RectTransform>("Port Facility Header");
            Assert.That(back.transform.parent, Is.SameAs(header), "The dossier return belongs in the existing upper-left facility header.");
            Assert.That(back.GetComponentInChildren<Text>().text, Is.EqualTo("返回船坞"));
            Assert.That(host.GetComponentsInChildren<Button>(true).Count(button => button.name == "Return To Port Hub"), Is.EqualTo(1));
            Assert.That(detail.GetComponentsInChildren<Button>(true).Any(button =>
                button.name.StartsWith("Back To ") || button.GetComponentInChildren<Text>(true)?.text.Contains("返回") == true), Is.False,
                "The scroll must not retain a second return button.");
            AssertContained(header.rect, BoundsIn(header, back.GetComponent<RectTransform>()), "Return control stays inside the shared header.");
            RectTransform window = Find<RectTransform>("Shipyard Dossier Ship Window");
            Rect windowBounds = BoundsIn(detail, window);
            RawImage frame = Find<RawImage>("Shipyard Dossier Frame");
            Assert.That(frame.texture, Is.SameAs(new PortArtCatalog().DossierFrame),
                "Performance and background share the crew dossier's illustrated parchment.");
            CrewArtChromaKey chroma = frame.GetComponent<CrewArtChromaKey>();
            Assert.That(chroma, Is.Not.Null, "The blue outside and portrait window must remain transparent.");
            Assert.That(chroma.OwnedMaterial, Is.Not.Null);
            Assert.That(chroma.KeyTolerance, Is.EqualTo(.12f));
            Assert.That(frame.transform.GetSiblingIndex(), Is.GreaterThan(window.GetSiblingIndex()),
                "The ornamental portrait rim must render over the vessel window.");
            Assert.That(frame.raycastTarget, Is.False, "The ornamental frame must never block controls.");
            Assert.That(detail.GetComponentsInChildren<Transform>(true).Any(item => item.name.StartsWith("Shipyard Workbench")), Is.False,
                "The restored parchment dossier must not retain the dark workbench surface, grid or labels.");
            AssertContained(frame.rectTransform.rect, BoundsIn(frame.rectTransform, window),
                "The original ship window stays inside the parchment frame.");
            RawImage[] paintings = window.GetComponentsInChildren<RawImage>(true);
            Assert.That(paintings.Length, Is.EqualTo(1), "Every dossier uses one independent painting, not the harbour vessel layers.");
            Assert.That(paintings[0].name, Is.EqualTo("Shipyard Dossier Portrait"));
            Assert.That(paintings[0].texture, Is.SameAs(new PortArtCatalog().ShipDossierPortrait(CoreVoyageSession.Ships[index].Id)),
                "Every ship must use its own dedicated portrait.");
            Assert.That(window.GetComponent<RectMask2D>(), Is.Not.Null);
            foreach (RawImage painting in paintings)
            {
                AssertContained(BoundsIn(window, painting.rectTransform), window.rect, painting.name + " must cover the portrait window without exposed harbour scenery.");
                Assert.That(painting.raycastTarget, Is.False, "The dossier ship is an illustration, not a button.");
                Assert.That(painting.GetComponent<BattleShipCutout>(), Is.Null, "The portrait retains its painted background.");
                float sourceAspect = painting.texture.width * Mathf.Abs(painting.uvRect.width) /
                    (painting.texture.height * Mathf.Abs(painting.uvRect.height));
                Assert.That(painting.rectTransform.rect.width / painting.rectTransform.rect.height,
                    Is.EqualTo(sourceAspect).Within(.025f), painting.name + " must retain its painted aspect ratio.");
            }
            foreach (string field in new[] { "Shipyard Dossier Name", "Shipyard Dossier Role" })
            {
                Text label = Find<Text>(field);
                Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width + .1f),
                    field + " must fit without wrapping or shrinking.");
                Assert.That(BoundsIn(detail, label.rectTransform).Overlaps(windowBounds), Is.False,
                    field + " stays in the right writing bay.");
            }
            foreach (Button button in detail.GetComponentsInChildren<Button>(true))
            {
                Vector3[] corners = new Vector3[4];
                button.GetComponent<RectTransform>().GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 local = detail.InverseTransformPoint(corner);
                    Assert.That(local.y, Is.InRange(-275f, 325f), button.name + " must stay inside the paper margins.");
                    Assert.That(local.x, Is.InRange(-810f, 780f), button.name + " must stay inside the paper sides.");
                }
                Assert.That(BoundsIn(detail, button.GetComponent<RectTransform>()).Overlaps(windowBounds), Is.False,
                    button.name + " must not cover the framed vessel.");
            }
            foreach (Graphic content in detail.GetComponentsInChildren<Graphic>(true).Where(item =>
                item.name.StartsWith("Shipyard Dossier Stat ") || item.name.StartsWith("Shipyard Stat ") ||
                item.name == "Shipyard Dossier Story" || item.name == "Shipyard Dossier Story Title"))
                Assert.That(BoundsIn(detail, content.rectTransform).Overlaps(windowBounds), Is.False,
                    content.name + " must remain separate from the framed vessel.");
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

        [TestCase(0)] [TestCase(4)] [TestCase(10)]
        public void DossierTabChangesKeepThePortraitWindowAndPaintingPositionAndScale(int index)
        {
            Preview(index, true);
            Canvas.ForceUpdateCanvases();
            RectTransform originalDetail = Find<RectTransform>("Shipyard Detail");
            RectTransform originalWindow = Find<RectTransform>("Shipyard Dossier Ship Window");
            Rect windowBounds = BoundsIn(originalDetail, originalWindow);
            Vector2 windowPosition = originalWindow.anchoredPosition;
            Vector2 windowSize = originalWindow.sizeDelta;
            RawImage[] originalPaintings = originalWindow.GetComponentsInChildren<RawImage>(true);
            var paintingBounds = originalPaintings.ToDictionary(item => item.name, item => BoundsIn(originalDetail, item.rectTransform));
            var paintingScales = originalPaintings.ToDictionary(item => item.name, item => item.transform.parent.localScale);
            // This measured pre-redesign window also protects against moving all
            // three pages together and accidentally making tab-to-tab checks pass.
            AssertRect(windowBounds, new Rect(-763.57947f, -254.99884f, 411.28781f, 486.78564f), "Original ship window");

            foreach (string tab in new[] { "Background", "Performance" })
            {
                Find<Button>("Shipyard Dossier Tab " + tab).onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                RectTransform detail = Find<RectTransform>("Shipyard Detail");
                RectTransform window = Find<RectTransform>("Shipyard Dossier Ship Window");
                Assert.That(Vector2.Distance(window.anchoredPosition, windowPosition), Is.LessThan(.01f), tab + " window position");
                Assert.That(Vector2.Distance(window.sizeDelta, windowSize), Is.LessThan(.01f), tab + " window dimensions");
                AssertRect(BoundsIn(detail, window), windowBounds, tab + " window bounds");
                RawImage[] paintings = window.GetComponentsInChildren<RawImage>(true);
                CollectionAssert.AreEquivalent(paintingBounds.Keys, paintings.Select(item => item.name), tab + " ship layers");
                foreach (RawImage painting in paintings)
                {
                    AssertRect(BoundsIn(detail, painting.rectTransform), paintingBounds[painting.name], tab + " " + painting.name);
                    Assert.That(Vector3.Distance(painting.transform.parent.localScale, paintingScales[painting.name]), Is.LessThan(.001f),
                        tab + " must keep the painted vessel scale");
                }
            }
            Find<Button>("Shipyard Dossier Tab Equipment").onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            AssertEquipmentWindow(index);
            Find<Button>("Close Shipyard Equipment").onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            AssertCurrentPage((int)ShipyardDossierTab.Performance);
            AssertRect(BoundsIn(Find<RectTransform>("Shipyard Detail"), Find<RectTransform>("Shipyard Dossier Ship Window")),
                windowBounds, "Closing the separate equipment window restores the original dossier ship placement");
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
                if (tabs[index] == "Equipment")
                {
                    Find<Button>("Close Shipyard Equipment").onClick.Invoke();
                    AssertCurrentPage((int)ShipyardDossierTab.Background);
                }
            }
        }

        private void AssertEquipmentWindow(int index)
        {
            Canvas.ForceUpdateCanvases();
            Image background = Find<Image>("Shipyard Equipment Overlay");
            Assert.That(background.sprite, Is.Null, "The opaque backing must hide scenery beyond the market-style paper.");
            Assert.That(background.color.a, Is.EqualTo(1f));
            Assert.That(background.raycastTarget, Is.True, "The full-screen surface must block underlying harbour interaction.");
            RectTransform canvas = (RectTransform)background.GetComponentInParent<Canvas>().rootCanvas.transform;
            AssertContained(BoundsIn(canvas, background.rectTransform), canvas.rect, "The background covers the actual canvas.");
            RectTransform detail = Find<RectTransform>("Shipyard Detail");
            Assert.That(detail.IsChildOf(background.transform), Is.True);
            Image paper = Find<Image>("Shipyard Equipment Paper");
            Assert.That(paper.sprite, Is.SameAs(MarketShopSkin.Paper), "The equipment workspace shares the market's paper surface.");
            Assert.That(paper.color.a, Is.EqualTo(1f));
            Assert.That(paper.raycastTarget, Is.False, "The paper decoration must not block the equipment controls.");
            RectTransform header = Find<RectTransform>("Port Facility Header");
            Assert.That(header.gameObject.activeInHierarchy, Is.True, "The existing facility header remains visible.");
            Assert.That(host.GetComponentsInChildren<Graphic>(true).Any(item => item.name == "Shipyard Dossier Frame"), Is.False,
                "The equipment window must not reuse the parchment dossier.");
            Assert.That(detail.GetComponentsInChildren<Button>(true).Any(button => button.name.StartsWith("Shipyard Dossier Tab ")), Is.False);
            Assert.That(Find<Text>("Shipyard Dossier Name").text, Is.EqualTo(CoreVoyageSession.Ships[index].Name));
            Assert.That(Find<Button>("Close Shipyard Equipment").GetComponentInChildren<Text>().text, Does.Contain("返回船只档案"));
            RectTransform vessel = Find<RectTransform>("Shipyard Dossier Ship Window");
            Rect vesselBounds = BoundsIn(detail, vessel);
            RawImage[] paintings = vessel.GetComponentsInChildren<RawImage>(true);
            Assert.That(paintings, Is.Not.Empty);
            foreach (RawImage painting in paintings)
            {
                AssertContained(vessel.rect, BoundsIn(vessel, painting.rectTransform), painting.name + " fits inside the vessel preview.");
                Assert.That(painting.raycastTarget, Is.False);
                float sourceAspect = painting.texture.width * Mathf.Abs(painting.uvRect.width) /
                    (painting.texture.height * Mathf.Abs(painting.uvRect.height));
                Assert.That(painting.rectTransform.rect.width / painting.rectTransform.rect.height,
                    Is.EqualTo(sourceAspect).Within(.025f));
            }
            foreach (Button button in detail.GetComponentsInChildren<Button>(true))
            {
                Rect bounds = BoundsIn(detail, button.GetComponent<RectTransform>());
                AssertContained(detail.rect, bounds, button.name + " must remain inside the full-screen content area.");
                AssertContained(paper.rectTransform.rect, BoundsIn(paper.rectTransform, button.GetComponent<RectTransform>()),
                    button.name + " must stay on the market-style paper.");
                Assert.That(bounds.Overlaps(vesselBounds), Is.False, button.name + " must not cover the vessel illustration.");
                Assert.That(bounds.Overlaps(BoundsIn(detail, header)), Is.False, button.name + " leaves the existing facility header clear.");
            }
            Assert.That(detail.GetComponentsInChildren<Button>(true).Any(button => button.name == "Confirm Shipyard Vessel"), Is.False);
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
            Assert.That(Find<Text>("Shipyard Equipment Help").text, Does.Contain("购买此船后"),
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

        private static Rect BoundsIn(RectTransform parent, RectTransform child)
        {
            Vector3[] corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector3[] local = corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(local.Min(point => point.x), local.Min(point => point.y),
                local.Max(point => point.x), local.Max(point => point.y));
        }

        private static void AssertRect(Rect actual, Rect expected, string message)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(.01f), message + " x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(.01f), message + " y");
            Assert.That(actual.width, Is.EqualTo(expected.width).Within(.01f), message + " width");
            Assert.That(actual.height, Is.EqualTo(expected.height).Within(.01f), message + " height");
        }

        private static void AssertContained(Rect outer, Rect inner, string message)
        {
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - .1f), message);
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + .1f), message);
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - .1f), message);
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + .1f), message);
        }
    }
}
