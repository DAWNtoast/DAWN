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
            Assert.That(Find<Text>("Tavern Dossier Page Counter").text, Is.EqualTo("第 4 / 4 页"));
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
            Assert.That(Find<RectTransform>("Card Workshop Chest Stage").gameObject.activeSelf, Is.True);
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Battle Card Upgrade Panel"), Is.False);
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.DeckBuilder, CoreGameplayPreviewPort.GrayGull, 0);
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
            Find<RectTransform>("Deck Builder Panel");
        }

        [Test]
        public void OpeningChestThenRefreshingRestoresTheConfiguredWorkshopInsteadOfTheLastSubpage()
        {
            CreatePreview(CoreGameplayPreviewScreen.CardWorkshop, CoreGameplayPreviewPort.GrayGull, 0);
            foreach (int chest in new[] { 3, 0, 1, 2 })
            {
                Find<Button>("Open Deck Chest " + chest).onClick.Invoke();
                Find<RectTransform>(chest == 3 ? "Special Card Collection Panel" : "Battle Card Upgrade Panel");
                controller.RefreshEditorPreview();
                Assert.That(controller.HasCompleteEditorPreview, Is.True);
                Find<RectTransform>("Card Workshop Chest Stage");
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
            savedInventory.TryEquip(0, savedInventory.Grant(savedCannon, 0.94f).InstanceId);
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
            Assert.That(controller.EquippedComponent(ComponentSlot.Cannon).Id, Is.EqualTo(sample.Id));
            Assert.That(controller.Session.HasNavigator, Is.True);
            Assert.That(controller.Session.HasCarpenter, Is.True);
            Assert.That(host.GetComponentsInChildren<Text>(true).Any(item => item.name == "Editor Preview Sample Notice"), Is.False);
            foreach (string id in expected) Assert.That(controller.Session.GetSpecialCardExperience(id), Is.Zero);
            foreach (int filter in new[] { 1, 2, 0 })
            {
                Find<Button>("Special Card Filter " + filter).onClick.Invoke();
                CollectionAssert.AreEquivalent(expected, controller.Session.AcquiredSpecialCardIds);
                Assert.That(host.GetComponentsInChildren<Text>(true).Any(item => item.name == "Special Card Collection Empty"), Is.False);
            }
            controller.RefreshEditorPreview();
            CollectionAssert.AreEquivalent(expected, controller.Session.AcquiredSpecialCardIds);
            foreach (KeyValuePair<string, string> pair in before)
                Assert.That(PlayerPrefs.GetString(pair.Key), Is.EqualTo(pair.Value), pair.Key + " must not be read into or rewritten by the preview fixture.");
        }

        private void AssertUpgradeStage(int stage)
        {
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
            Find<RectTransform>("Battle Card Upgrade Panel");
            FieldInfo selected = typeof(CoreGameplayDemoController).GetField("selectedBattleDeckStage", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That((NavalBattleStage)selected.GetValue(controller), Is.EqualTo((NavalBattleStage)stage));
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Special Card Collection Panel"), Is.False);
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
