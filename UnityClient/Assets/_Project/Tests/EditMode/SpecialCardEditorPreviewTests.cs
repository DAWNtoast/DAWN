using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class SpecialCardEditorPreviewTests
    {
        private GameObject host;
        private CoreGameplayDemoController controller;
        private readonly Dictionary<string, string> preservedStrings = new Dictionary<string, string>();
        private readonly HashSet<string> touchedKeys = new HashSet<string>();

        [TearDown]
        public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
            foreach (string key in touchedKeys)
            {
                if (preservedStrings.TryGetValue(key, out string value)) PlayerPrefs.SetString(key, value);
                else PlayerPrefs.DeleteKey(key);
            }
            touchedKeys.Clear();
            preservedStrings.Clear();
        }

        [Test]
        public void InspectorAndConfigurationExposeAllFourCrewDossierPages()
        {
            FieldInfo field = typeof(CoreGameplayDemoController).GetField("editorPreviewCrewPage", BindingFlags.Instance | BindingFlags.NonPublic);
            UnityEngine.RangeAttribute range = field.GetCustomAttribute<UnityEngine.RangeAttribute>();
            Assert.That(range.min, Is.Zero);
            Assert.That(range.max, Is.EqualTo(3));
            Assert.That(field.GetCustomAttribute<TooltipAttribute>().tooltip, Does.Contain("专属卡牌"));
            CreatePreview(CoreGameplayPreviewScreen.TavernCrewDetail, CoreGameplayPreviewPort.GrayGull, 0);
            controller.ConfigureEditorCrewDossier(0, 3);
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
            Assert.That(Find<RectTransform>("Tavern Dossier SpecialCards Page").gameObject.activeSelf, Is.True);
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Tavern Dossier Page Counter"), Is.False);
            controller.RefreshEditorPreview();
            Assert.That(Find<RectTransform>("Tavern Dossier SpecialCards Page").gameObject.activeSelf, Is.True);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void DirectUpgradePreview_UsesRequestedStageAndRefreshesWithoutLeakingCollectionState(int stage)
        {
            CreatePreview(CoreGameplayPreviewScreen.SpecialCardCollection, CoreGameplayPreviewPort.GrayGull, 0);
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.CardUpgrade, CoreGameplayPreviewPort.GrayGull, stage);
            AssertUpgradeStage(stage);
            controller.RefreshEditorPreview();
            AssertUpgradeStage(stage);
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.CardWorkshop, CoreGameplayPreviewPort.GrayGull, 0);
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
            Assert.That(Find<RectTransform>("Workshop Home Panel").gameObject.activeSelf, Is.True);
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Battle Card Upgrade Panel"), Is.False);
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.DeckBuilder, CoreGameplayPreviewPort.GrayGull, 0);
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
            Find<RectTransform>("Deck Builder Panel");
        }

        [Test]
        public void OpeningActionsThenRefreshingRestoresTheConfiguredWorkshopInsteadOfTheLastSubpage()
        {
            CreatePreview(CoreGameplayPreviewScreen.CardWorkshop, CoreGameplayPreviewPort.GrayGull, 0);
            foreach (int chest in new[] { 3, 0, 1, 2 })
            {
                Find<Button>("Workshop Entrance Collection").onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                if (chest == 3) Find<Button>("Open Special Card Collection").onClick.Invoke();
                else
                {
                    Find<Button>("Workshop Preview Stage " + (NavalBattleStage)chest).onClick.Invoke();
                    Find<Button>("Workshop Page Upgrade").onClick.Invoke();
                }
                Find<RectTransform>(chest == 3 ? "Card Workshop Preview Panel" : "Battle Card Upgrade Panel");
                controller.RefreshEditorPreview();
                Assert.That(controller.HasCompleteEditorPreview, Is.True);
                Find<RectTransform>("Workshop Home Panel");
                Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Workshop Preview Collection"), Is.False);
            }
        }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor, "broken_anchor")]
        [TestCase(CoreGameplayPreviewPort.WhiteShell, "white_shell")]
        [TestCase(CoreGameplayPreviewPort.BronzeBell, "bronze_bell")]
        [TestCase(CoreGameplayPreviewPort.SaltCrown, "salt_crown")]
        [TestCase(CoreGameplayPreviewPort.TwilightTide, "twilight_tide")]
        [TestCase(CoreGameplayPreviewPort.GrayGull, "gray_gull")]
        public void PreviewSamples_AreRegionalDeterministicAndIndependentOfPlayerProgress(CoreGameplayPreviewPort port, string portId)
        {
            ComponentArtCatalog parts = new ComponentArtCatalog();
            ComponentArtEntry sample = parts.Entries.First(item => item.Slot == ComponentSlot.Cannon && item.Rarity == ComponentRarity.Rare);
            ComponentArtEntry savedCannon = parts.Entries.First(item => item.Slot == ComponentSlot.Cannon && item.Rarity == ComponentRarity.Legendary);
            SetFixturePreference(CoreGameplayDemoController.ComponentLoadoutKey(ComponentSlot.Cannon), savedCannon.Id);
            OwnedShipComponentInventory savedInventory = new OwnedShipComponentInventory(parts);
            savedInventory.EnsureLoadout(0);
            savedInventory.TryEquip(0, savedInventory.Grant(savedCannon).InstanceId);
            SetFixturePreference(CoreGameplayDemoController.ComponentInventoryKey, savedInventory.Export());
            SetFixturePreference(CoreGameplayDemoController.SpecialCardExperienceKey, "card_special_" + savedCannon.Id + "=840");
            SetFixturePreference(CoreGameplayDemoController.AcquiredSpecialCardsKey, "card_special_" + savedCannon.Id);
            SetFixturePreference(CoreGameplayDemoController.BattleCardUpgradeKey, "card_brace=3");
            Dictionary<string, string> before = touchedKeys.ToDictionary(key => key, PlayerPrefs.GetString);
            CreatePreview(CoreGameplayPreviewScreen.SpecialCardCollection, port, 0);

            string[] expected = CoreSpecialCardCatalog.All.Where(item =>
                item.SourceId.StartsWith("crew_" + portId + "_navigator_", StringComparison.Ordinal) ||
                item.SourceId.StartsWith("crew_" + portId + "_carpenter_", StringComparison.Ordinal) || item.SourceId == sample.Id)
                .Select(item => item.Card.Id).ToArray();
            Assert.That(expected.Length, Is.EqualTo(3));
            CollectionAssert.AreEquivalent(expected, controller.Session.AcquiredSpecialCardIds);
            AssertCompleteSpecialCollectionLayout();
            Assert.That(controller.EquippedComponent(ComponentSlot.Cannon).Id, Is.EqualTo(sample.Id));
            Assert.That(controller.Session.HasNavigator, Is.True);
            Assert.That(controller.Session.HasCarpenter, Is.True);
            Assert.That(host.GetComponentsInChildren<Text>(true).Any(item => item.name == "Editor Preview Sample Notice"), Is.False);
            foreach (string id in expected) Assert.That(controller.Session.GetSpecialCardExperience(id), Is.Zero);
            foreach (NavalBattleStage stage in new[] { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding })
            {
                Find<Button>("Workshop School Filter neutral").onClick.Invoke();
                Find<Button>("Workshop Preview Stage " + stage).onClick.Invoke();
                Assert.That(Find<Button>("Workshop Page Upgrade").interactable, Is.True);
                Find<Button>("Open Special Card Collection").onClick.Invoke();
                Assert.That(host.GetComponentsInChildren<Button>(true).Any(button => button.name == "Confirm Battle Card Upgrade"), Is.False);
                CollectionAssert.AreEquivalent(expected, controller.Session.AcquiredSpecialCardIds);
                bool hasNeutral = expected.Any(id => CardSchoolArtCatalog.NormalizeSchool(CoreSpecialCardCatalog.FindByCardId(id).Card.SchoolId) == "neutral");
                Assert.That(host.GetComponentsInChildren<Text>(true).Any(item => item.name == "Workshop Preview Empty"), Is.EqualTo(!hasNeutral));
            }
            controller.RefreshEditorPreview();
            CollectionAssert.AreEquivalent(expected, controller.Session.AcquiredSpecialCardIds);
            AssertCompleteSpecialCollectionLayout();
            foreach (KeyValuePair<string, string> pair in before)
                Assert.That(PlayerPrefs.GetString(pair.Key), Is.EqualTo(pair.Value), pair.Key + " must not be read into or rewritten by the preview fixture.");
        }

        [Test]
        public void SpecialCollection_EmptyCatalogKeepsTheWorkshopAndUpgradeDisabled()
        {
            CreatePreview(CoreGameplayPreviewScreen.SpecialCardCollection, CoreGameplayPreviewPort.GrayGull, 0);
            var acquired = (HashSet<string>)typeof(CoreVoyageSession).GetField("acquiredSpecialCards", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(controller.Session);
            acquired.Clear();
            FieldInfo previewBuilding = typeof(CoreGameplayDemoController).GetField("editorPreviewBuilding", BindingFlags.Instance | BindingFlags.NonPublic);
            previewBuilding.SetValue(controller, true);
            try { Find<Button>("Open Special Card Collection").onClick.Invoke(); }
            finally { previewBuilding.SetValue(controller, false); }
            Find<RectTransform>("Card Workshop Preview Panel");
            Assert.That(Find<Text>("Workshop Preview Empty").text, Does.Contain("暂无已获得"));
            Assert.That(host.GetComponentsInChildren<Button>(true).Any(button => button.name == "Confirm Battle Card Upgrade"), Is.False);
            Assert.That(Find<Button>("Previous Upgrade Cards").interactable, Is.False);
            Assert.That(Find<Button>("Next Upgrade Cards").interactable, Is.False);
            Find<RectTransform>("Workshop Collection Detail");
        }

        private void AssertCompleteSpecialCollectionLayout()
        {
            var seen = new HashSet<string>();
            foreach (string school in new[] { "neutral", "fire", "arcane", "curse", "tide", "thunder", "blood" })
            {
                Find<Button>("Workshop School Filter " + school).onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                RectTransform panel = Find<RectTransform>("Workshop Preview Collection");
                Button[] choices = panel.GetComponentsInChildren<Button>(true)
                    .Where(button => button.name.StartsWith("Select Special Card ", StringComparison.Ordinal))
                    .OrderBy(button => button.GetComponent<RectTransform>().anchoredPosition.x).ToArray();
                string[] expected = controller.Session.AcquiredSpecialCardIds.Where(id =>
                    CardSchoolArtCatalog.NormalizeSchool(CoreSpecialCardCatalog.FindByCardId(id).Card.SchoolId) == school).ToArray();
                Assert.That(choices, Has.Length.EqualTo(expected.Length), "Each school shows only its regional sample cards.");
                CollectionAssert.AreEquivalent(expected, choices.Select(choice => controller.Session.GetBattleCard(
                    int.Parse(choice.name.Substring("Select Special Card ".Length))).Id));
                foreach (string id in expected) Assert.That(seen.Add(id), Is.True, "A sample card belongs to one school filter.");
                Rect? previous = null;
                foreach (Button choice in choices)
                {
                    string index = choice.name.Substring("Select Special Card ".Length);
                    RectTransform face = Find<RectTransform>("Special Card " + index + " Face");
                    Assert.That(face.sizeDelta, Is.EqualTo(CardFaceLayout.CardSize));
                    Assert.That(face.localScale, Is.EqualTo(Vector3.one * .78f));
                    AssertCompleteUpgradeFace(face);
                    Rect bounds = UpgradeBoundsIn(panel, face);
                    Assert.That(bounds.width, Is.EqualTo(CardFaceLayout.CardSize.x * .78f).Within(.1f));
                    Assert.That(bounds.height, Is.EqualTo(CardFaceLayout.CardSize.y * .78f).Within(.1f));
                    bounds = UpgradeBoundsIn(panel, face.GetComponentInChildren<CardSchoolFrameGraphic>().rectTransform);
                    Assert.That(bounds.width, Is.EqualTo(ExpectedFrameBounds(face).width * .78f).Within(.1f));
                    Assert.That(bounds.height, Is.EqualTo(ExpectedFrameBounds(face).height * .78f).Within(.1f));
                    if (previous.HasValue)
                    {
                        Assert.That(bounds.center.y, Is.EqualTo(previous.Value.center.y).Within(.1f));
                        Assert.That(bounds.xMin, Is.GreaterThan(previous.Value.xMax), "Collection cards must not overlap.");
                    }
                    previous = bounds;
                }
                Assert.That(Find<Button>("Previous Upgrade Cards").interactable, Is.False);
                Assert.That(Find<Button>("Next Upgrade Cards").interactable, Is.False);
                Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Upgrade Collection Page"), Is.False);
                Outline[] selection = panel.GetComponentsInChildren<CardSchoolFrameGraphic>(true)
                    .Select(frame => frame.GetComponent<Outline>()).Where(outline => outline != null).ToArray();
                Assert.That(selection, Has.Length.EqualTo(choices.Length > 0 ? 1 : 0));
                Assert.That(host.GetComponentsInChildren<Button>(true).Any(button => button.name == "Confirm Battle Card Upgrade"), Is.False);
                Find<RectTransform>("Workshop Collection Detail");
                Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Battle Card Upgrade Panel"), Is.False);
            }
            CollectionAssert.AreEquivalent(controller.Session.AcquiredSpecialCardIds, seen);
            Assert.That(seen, Has.Count.EqualTo(3), "All three regional samples remain reachable through school filters.");
            Find<Button>("Workshop School Filter neutral").onClick.Invoke();
        }

        private void AssertUpgradeStage(int stage)
        {
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
            Find<RectTransform>("Battle Card Upgrade Panel");
            FieldInfo selected = typeof(CoreGameplayDemoController).GetField("selectedBattleDeckStage", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That((NavalBattleStage)selected.GetValue(controller), Is.EqualTo((NavalBattleStage)stage));
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Special Card Collection Panel"), Is.False);
            AssertCompleteCardUpgradeLayout();
        }

        private void AssertCompleteCardUpgradeLayout()
        {
            Canvas.ForceUpdateCanvases();
            RectTransform panel = Find<RectTransform>("Battle Card Upgrade Panel");
            RectTransform collection = Find<RectTransform>("Workshop Upgrade Library");
            Button[] choices = collection.GetComponentsInChildren<Button>(true)
                .Where(button => button.name.StartsWith("Select Upgrade Card ", StringComparison.Ordinal)).ToArray();
            Assert.That(choices.Length, Is.InRange(1, 6), "The upgrade library shows at most six compact choices.");
            NavalBattleStage stage = (NavalBattleStage)typeof(CoreGameplayDemoController)
                .GetField("selectedBattleDeckStage", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            var bounds = new List<Rect>();
            foreach (Button choice in choices)
            {
                string index = choice.name.Substring("Select Upgrade Card ".Length);
                CoreBattleCard card = controller.Session.GetBattleCard(int.Parse(index), stage);
                Assert.That(Find<RawImage>("Upgrade List Art " + index).texture, Is.Not.Null);
                Assert.That(Find<Text>("Upgrade List Name " + index).text, Is.EqualTo(card.Name));
                Assert.That(Find<Text>("Upgrade List Level " + index).text, Is.EqualTo("+" + card.UpgradeLevel));
                Assert.That(choice.GetComponentInChildren<CardSchoolFrameGraphic>(), Is.Null,
                    "The picker reserves full card faces for the comparison panel.");
                Rect row = UpgradeBoundsIn(collection, choice.GetComponent<RectTransform>());
                Assert.That(row.xMin, Is.GreaterThanOrEqualTo(collection.rect.xMin));
                Assert.That(row.xMax, Is.LessThanOrEqualTo(collection.rect.xMax));
                Assert.That(row.yMin, Is.GreaterThanOrEqualTo(collection.rect.yMin));
                Assert.That(row.yMax, Is.LessThanOrEqualTo(collection.rect.yMax));
                Assert.That(bounds.All(previous => !previous.Overlaps(row)), Is.True, "Picker rows cannot overlap.");
                bounds.Add(row);
            }
            Assert.That(bounds.Select(rect => Mathf.Round(rect.center.x)).Distinct().Count(), Is.EqualTo(1));
            Assert.That(bounds.Select(rect => Mathf.Round(rect.center.y)).Distinct().Count(), Is.EqualTo(choices.Length));
            RectTransform current = Find<RectTransform>("Upgrade Comparison Current Face");
            RectTransform next = Find<RectTransform>("Upgrade Comparison Next Face");
            RectTransform arrow = Find<RectTransform>("Upgrade Comparison Arrow");
            Rect currentBounds = UpgradeBoundsIn(panel, current.GetComponentInChildren<CardSchoolFrameGraphic>().rectTransform);
            Rect nextBounds = UpgradeBoundsIn(panel, next.GetComponentInChildren<CardSchoolFrameGraphic>().rectTransform);
            Rect arrowBounds = UpgradeBoundsIn(panel, arrow);
            foreach (RectTransform face in new[] { current, next })
            {
                Assert.That(face.sizeDelta, Is.EqualTo(CardFaceLayout.CardSize));
                Assert.That(face.localScale.x, Is.GreaterThan(0f));
                Assert.That(face.localScale.y, Is.EqualTo(face.localScale.x));
                AssertCompleteUpgradeFace(face);
            }
            Assert.That(current.localScale, Is.EqualTo(next.localScale));
            Assert.That(current.localScale, Is.EqualTo(Vector3.one * 1.3f));
            Assert.That(currentBounds.center.y, Is.EqualTo(nextBounds.center.y).Within(.1f));
            Assert.That(arrowBounds.xMin, Is.GreaterThan(currentBounds.xMax));
            Assert.That(arrowBounds.xMax, Is.LessThan(nextBounds.xMin));
            Assert.That(arrow.GetComponent<Graphic>(), Is.Not.Null);
            Assert.That(arrow.GetComponent<Graphic>().raycastTarget, Is.False);
            Rect name = UpgradeBoundsIn(panel, Find<Text>("Upgrade Selected Name").rectTransform);
            Rect confirm = UpgradeBoundsIn(panel, Find<Button>("Confirm Battle Card Upgrade").GetComponent<RectTransform>());
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Card Upgrade Heading"), Is.False);
            Assert.That(name.yMin, Is.GreaterThan(currentBounds.yMax));
            Assert.That(confirm.yMax, Is.LessThan(currentBounds.yMin));
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Workshop Actions"), Is.False,
                "The upgrade comparison replaces both right-hand entrance sections.");
        }

        private static void AssertCompleteUpgradeFace(RectTransform face)
        {
            CardSchoolFrameGraphic frame = face.GetComponentInChildren<CardSchoolFrameGraphic>();
            Assert.That(frame, Is.Not.Null, face.name);
            Rect expectedBounds = ExpectedFrameBounds(face);
            Assert.That(frame.rectTransform.sizeDelta, Is.EqualTo(expectedBounds.size), face.name);
            Assert.That(frame.rectTransform.anchoredPosition, Is.EqualTo(expectedBounds.center), face.name);
            Assert.That(frame.HasAuthoredTexture, Is.True, face.name);
            RawImage art = face.GetComponentInChildren<RawImage>(true);
            Assert.That(art, Is.Not.Null, face.name);
            Assert.That(art.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(CardFaceLayout.IllustrationLayout(art.GetComponent<RawImage>()).size), face.name);
            Assert.That(art.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(CardFaceLayout.IllustrationLayout(art.GetComponent<RawImage>()).center), face.name);
            Assert.That(face.GetComponentsInChildren<ScrollRect>(true), Is.Empty, face.name + " fits the description on the card");
            CardOrnamentGraphic[] furniture = face.GetComponentsInChildren<CardOrnamentGraphic>(true);
            Assert.That(furniture.Any(item => item.Kind == CardOrnamentKind.Energy), Is.True, face.name);
            Assert.That(furniture.Single(item => item.Kind == CardOrnamentKind.Energy).IsMarkerOnly, Is.True, face.name);
            Assert.That(furniture.Any(item => item.Kind == CardOrnamentKind.NameRibbon), Is.True, face.name);
        }

        private static Rect ExpectedFrameBounds(RectTransform face)
        {
            CardSchoolFrameGraphic frame = face.GetComponentInChildren<CardSchoolFrameGraphic>();
            CardOrnamentGraphic[] markers = face.GetComponentsInChildren<CardOrnamentGraphic>();
            string special = markers.Any(item => item.Kind == CardOrnamentKind.Mana) ? "mana" :
                markers.Any(item => item.Kind == CardOrnamentKind.Health) ? "health" :
                markers.Any(item => item.Kind == CardOrnamentKind.Curse) ? "curse" :
                markers.Any(item => item.Kind == CardOrnamentKind.Chain) ? "chain" : "";
            return CardSchoolArtCatalog.FrameBounds(frame.SchoolId,
                markers.Any(item => item.Kind == CardOrnamentKind.Gunpowder) ? 1 : 0,
                markers.Any(item => item.Kind == CardOrnamentKind.Cannonball) ? 1 : 0, special);
        }

        private static Rect UpgradeBoundsIn(Transform parent, RectTransform child)
        {
            Vector3[] corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector3[] local = corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(local.Min(point => point.x), local.Min(point => point.y),
                local.Max(point => point.x), local.Max(point => point.y));
        }

        private void CreatePreview(CoreGameplayPreviewScreen screen, CoreGameplayPreviewPort port, int route)
        {
            host = new GameObject("Special Card Editor Preview Test");
            host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(screen, port, route);
            host.SetActive(true);
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
        }

        private void SetFixturePreference(string key, string value)
        {
            if (touchedKeys.Add(key) && PlayerPrefs.HasKey(key)) preservedStrings[key] = PlayerPrefs.GetString(key);
            PlayerPrefs.SetString(key, value);
        }

        private T Find<T>(string name) where T : Component
        {
            T result = host.GetComponentsInChildren<T>(true).FirstOrDefault(item => item.name == name);
            Assert.That(result, Is.Not.Null, name);
            return result;
        }
    }
}
