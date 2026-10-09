using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class WarehouseEditorPreviewTests
    {
        private GameObject host;
        private CoreGameplayDemoController controller;
        private readonly Dictionary<string, int?> originalInts = new Dictionary<string, int?>();
        private readonly Dictionary<string, string> originalStrings = new Dictionary<string, string>();

        [SetUp]
        public void PreserveAllKeysWrittenByWarehouseSaveProgress()
        {
            Assert.That(Application.isPlaying, Is.False);
            originalInts.Clear();
            originalStrings.Clear();
            foreach (string key in IntKeys())
                originalInts[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
            foreach (string key in StringKeys())
                originalStrings[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
        }

        [TearDown]
        public void RestorePlayerPreferencesEvenIfPreviewPersistenceRegresses()
        {
            if (host != null) Object.DestroyImmediate(host);
            foreach (KeyValuePair<string, int?> entry in originalInts)
            {
                if (entry.Value.HasValue) PlayerPrefs.SetInt(entry.Key, entry.Value.Value);
                else PlayerPrefs.DeleteKey(entry.Key);
            }
            foreach (KeyValuePair<string, string> entry in originalStrings)
            {
                if (entry.Value != null) PlayerPrefs.SetString(entry.Key, entry.Value);
                else PlayerPrefs.DeleteKey(entry.Key);
            }
            PlayerPrefs.Save();
        }

        [Test]
        public void PreviewDisplayRemoveAndPortStorageNeverReplaceTheRealProfile()
        {
            SetDistinctPlayerFixture();
            Dictionary<string, int?> expectedInts = IntKeys().ToDictionary(key => key,
                key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null);
            Dictionary<string, string> expectedStrings = StringKeys().ToDictionary(key => key,
                key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null);
            CreatePreview();
            Assert.That(controller.WarehouseCollection.DisplayedAt(0), Is.EqualTo(CoreWarehouseCollection.TutorialTreasureId));
            Assert.That(host.GetComponentsInChildren<Text>(true).Any(item => item.name == "Editor Preview Warehouse Sample Notice"), Is.False);
            AssertPlayerPreferences(expectedInts, expectedStrings);

            Click("Warehouse Treasure " + CoreWarehouseCollection.TutorialTreasureId);
            Click("Warehouse Display Selected");
            Click("Warehouse Display Slot 5");
            Click("Warehouse Display Selected");
            Assert.That(controller.WarehouseCollection.DisplayedAt(0), Is.Empty);
            Assert.That(controller.WarehouseCollection.DisplayedAt(5), Is.EqualTo(CoreWarehouseCollection.TutorialTreasureId));
            Click("Warehouse Remove Selected");
            Assert.That(controller.WarehouseCollection.Owns(CoreWarehouseCollection.TutorialTreasureId), Is.True);
            Assert.That(controller.WarehouseCollection.IsDisplayed(CoreWarehouseCollection.TutorialTreasureId), Is.False);
            AssertPlayerPreferences(expectedInts, expectedStrings);

            Click("Warehouse Tab Storage");
            Assert.That(host.GetComponentsInChildren<Button>(true).Any(button => button.name == "Warehouse Tab Basics"), Is.False);
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                Assert.That(Find<Button>("Warehouse Storage Row " + PortMarketCatalog.Key(item)).interactable, Is.False,
                    "Editor inventory samples do not execute live cargo transactions.");
            AssertPlayerPreferences(expectedInts, expectedStrings);
            controller.ConfigureEditorPreviewWarehouseStorage(1);
            Find<RectTransform>("Warehouse Cargo Adjustment");
            AssertPlayerPreferences(expectedInts, expectedStrings);
            controller.ConfigureEditorPreviewWarehouseStorage(2);
            Find<RectTransform>("Warehouse Cargo Sale");
            AssertPlayerPreferences(expectedInts, expectedStrings);

            controller.WarehouseCollection.Import(string.Empty, string.Empty);
            controller.Session.RestoreProfile(2, "断锚港");
            controller.RefreshEditorPreview();
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
            CollectionAssert.AreEqual(CoreWarehouseCollection.Catalog.Select(item => item.Id).ToArray(), controller.WarehouseCollection.OwnedIds);
            Assert.That(controller.WarehouseCollection.DisplayedAt(0), Is.EqualTo(CoreWarehouseCollection.TutorialTreasureId));
            Assert.That(controller.WarehouseCollection.DisplayedAt(1), Is.EqualTo(CoreWarehouseCollection.BoatswainBellTreasureId));
            Assert.That(controller.WarehouseCollection.DisplayedAt(2), Is.EqualTo(CoreWarehouseCollection.SealedChartTreasureId));
            Assert.That(controller.WarehouseCollection.DisplayedAt(5), Is.Empty);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(572));
            Assert.That(controller.Session.CurrentPort, Is.EqualTo("灰鸥港"));
            Assert.That(Find<Button>("Warehouse Display Selected").interactable, Is.True);
            Assert.That(host.GetComponentsInChildren<Button>(true).Any(button => button.name == "Warehouse Tab Basics"), Is.False);
            AssertPlayerPreferences(expectedInts, expectedStrings);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LedgerTextsAndButtonsStayInsidePaperOnBothTabs(bool storage)
        {
            CreatePreview();
            if (storage) Click("Warehouse Tab Storage");
            Canvas.ForceUpdateCanvases();
            RectTransform ledger = Find<RectTransform>("Warehouse Ledger");
            ScrollRect[] scrolls = ledger.GetComponentsInChildren<ScrollRect>(true);
            foreach (ScrollRect scroll in scrolls) AssertContained(ledger, scroll.viewport, .1f);
            foreach (float position in new[] { 1f, 0f })
            {
                foreach (ScrollRect scroll in scrolls) scroll.verticalNormalizedPosition = position;
                Canvas.ForceUpdateCanvases();
                foreach (Text text in ledger.GetComponentsInChildren<Text>(true))
                    AssertVisibleContained(ledger, text.rectTransform, .1f);
                foreach (Button button in ledger.GetComponentsInChildren<Button>(true))
                    AssertVisibleContained(ledger, button.GetComponent<RectTransform>(), .1f);
            }
        }

        [Test]
        public void DisplayArtStaysInsideTheCabinetRecessAndLeavesClicksToItsButton()
        {
            CreatePreview();
            Canvas.ForceUpdateCanvases();
            RawImage backdrop = Find<RawImage>("Warehouse Cabinet Art");
            RectTransform background = Find<RectTransform>("Warehouse Background");
            RectTransform ledger = Find<RectTransform>("Warehouse Ledger");
            RectTransform warehousePanel = Find<RectTransform>("Warehouse Panel");
            Assert.That(backdrop.texture, Is.SameAs(Resources.Load<Texture2D>("WarehouseV2/trophy_cabinet_v2_expanded")));
            Assert.That(background.GetComponent<RawImage>().texture,
                Is.SameAs(Resources.Load<Texture2D>("PortFacilities/warehouse_background")));
            Assert.That(backdrop.rectTransform.anchoredPosition, Is.EqualTo(PhaseSurfaceLayout.BackgroundPosition));
            Assert.That(backdrop.uvRect, Is.EqualTo(PhaseSurfaceLayout.BackgroundUv(backdrop.texture)));
            Assert.That(backdrop.rectTransform.sizeDelta, Is.EqualTo(background.sizeDelta),
                "The cabinet art must cover the same surface as Warehouse Background.");
            Assert.That(backdrop.transform.parent, Is.EqualTo(background.transform.parent),
                "The expanded cabinet art must share the phase-content layer with the warehouse background.");
            Assert.That(backdrop.transform.GetSiblingIndex(), Is.LessThan(warehousePanel.transform.GetSiblingIndex()),
                "The warehouse panel and its ledger must stay above the expanded cabinet art.");
            Assert.That(backdrop.transform.GetSiblingIndex(), Is.LessThan(background.transform.GetSiblingIndex()),
                "The expanded cabinet art must stay behind the active Warehouse Background.");
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Warehouse Cabinet Hint"), Is.False);
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Warehouse Collection Safety"), Is.False);
            RawImage item = Find<RawImage>("Warehouse Displayed Art 0");
            RawImage inventoryArt = Find<RawImage>("Warehouse Inventory Art " + CoreWarehouseCollection.TutorialTreasureId);
            Button slot = Find<Button>("Warehouse Display Slot 0");
            RectTransform cabinet = Find<RectTransform>("Warehouse Display Cabinet");
            AssertContained(slot.GetComponent<RectTransform>(), item.rectTransform, 2f);
            AssertContained(cabinet, slot.GetComponent<RectTransform>(), 0.1f);
            ItemIconArt expectedArt = ItemIconCatalog.ForTreasure(CoreWarehouseCollection.Catalog[0]);
            Assert.That(expectedArt.IsAvailable, Is.True);
            Assert.That(item.texture, Is.SameAs(expectedArt.Texture));
            Assert.That(item.uvRect, Is.EqualTo(expectedArt.Uv));
            Assert.That(inventoryArt.texture, Is.SameAs(item.texture));
            Assert.That(inventoryArt.uvRect, Is.EqualTo(item.uvRect));
            Assert.That(item.raycastTarget, Is.False);
            Assert.That(inventoryArt.raycastTarget, Is.False);
            Assert.That(Find<RawImage>("Warehouse Cabinet Art").raycastTarget, Is.False);
            foreach (Text text in Find<RectTransform>("Warehouse Panel").GetComponentsInChildren<Text>(true))
                Assert.That(text.raycastTarget, Is.False, text.name);
            Assert.That(slot.targetGraphic.raycastTarget, Is.True);
            Assert.That(slot.targetGraphic.color.a, Is.EqualTo(0f),
                "The slot hit target must remain invisible so the exhibit sits directly on the painted shelf.");
            Assert.That(slot.colors.normalColor.a, Is.Zero);
            Assert.That(slot.colors.highlightedColor.a, Is.Zero);
            Assert.That(slot.colors.pressedColor.a, Is.Zero);
            Image rim = Find<Image>("Warehouse Slot Rim 0");
            Assert.That(rim.raycastTarget, Is.False);
            Assert.That(rim.color.a, Is.Zero, "Do not add a card-like border around a displayed treasure.");
            Assert.That(cabinet.GetComponentsInChildren<Button>().Length, Is.EqualTo(CoreWarehouseCollection.DisplaySlotCount));
        }

        [Test]
        public void PreviewShowsMultipleOwnedKeepsakesInInventoryAndCabinetSlots()
        {
            CreatePreview();
            string[] ids =
            {
                CoreWarehouseCollection.TutorialTreasureId,
                CoreWarehouseCollection.BoatswainBellTreasureId,
                CoreWarehouseCollection.SealedChartTreasureId,
            };
            Assert.That(controller.WarehouseCollection.OwnedIds.Count, Is.EqualTo(CoreWarehouseCollection.CatalogCount));
            CollectionAssert.AreEqual(ids, controller.WarehouseCollection.OwnedIds.Take(ids.Length));
            for (int slot = 0; slot < ids.Length; slot++)
            {
                Assert.That(controller.WarehouseCollection.DisplayedAt(slot), Is.EqualTo(ids[slot]));
                RawImage display = Find<RawImage>("Warehouse Displayed Art " + slot);
                RawImage inventory = Find<RawImage>("Warehouse Inventory Art " + ids[slot]);
                Assert.That(display.texture, Is.SameAs(inventory.texture));
                Assert.That(display.texture, Is.Not.Null);
            }
            Assert.That(Find<Button>("Warehouse Treasure " + CoreWarehouseCollection.BoatswainBellTreasureId), Is.Not.Null);
            Assert.That(Find<Button>("Warehouse Treasure " + CoreWarehouseCollection.SealedChartTreasureId), Is.Not.Null);
        }

        [Test]
        public void PreviewCanPageThroughTheEntireFortyItemCatalog()
        {
            CreatePreview();
            var reached = new HashSet<string>();
            int pages = 0;
            while (true)
            {
                Button[] tiles = CollectionTiles();
                Assert.That(tiles.Length, Is.InRange(1, 3), "One inventory row must leave the detail area clear.");
                foreach (Button tile in tiles) reached.Add(tile.name.Substring("Warehouse Treasure ".Length));
                AssertCollectionDoesNotOverlapDetails();
                pages++;
                Assert.That(pages, Is.LessThanOrEqualTo(CoreWarehouseCollection.CatalogCount));
                if (!Find<Button>("Warehouse Treasure Page Next").interactable) break;
                Click("Warehouse Treasure Page Next");
            }
            CollectionAssert.AreEquivalent(CoreWarehouseCollection.Catalog.Select(item => item.Id), reached);
            Assert.That(pages, Is.EqualTo((CoreWarehouseCollection.CatalogCount + 2) / 3));
            string last = CoreWarehouseCollection.Catalog[CoreWarehouseCollection.CatalogCount - 1].Id;
            Click("Warehouse Treasure " + last);
            Click("Warehouse Display Slot 5");
            Click("Warehouse Display Selected");
            Assert.That(controller.WarehouseCollection.DisplayedAt(5), Is.EqualTo(last),
                "The final inventory page must still support all six cabinet display slots.");
            Assert.That(Find<RectTransform>("Warehouse Display Cabinet").GetComponentsInChildren<Button>().Length,
                Is.EqualTo(CoreWarehouseCollection.DisplaySlotCount));
            Assert.That(Find<Button>("Warehouse Treasure Page Previous").interactable, Is.True);
            Assert.That(Find<Button>("Warehouse Treasure Page Next").interactable, Is.False);
            while (Find<Button>("Warehouse Treasure Page Previous").interactable) Click("Warehouse Treasure Page Previous");
            Find<Button>("Warehouse Treasure " + CoreWarehouseCollection.TutorialTreasureId);
            Assert.That(controller.WarehouseCollection.DisplayedAt(5), Is.EqualTo(last));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(6)]
        public void CollectionCardsAndHintsNeverCoverTheSelectedArchive(int count)
        {
            CreatePreview();
            controller.WarehouseCollection.Import(string.Join(",", CoreWarehouseCollection.Catalog.Take(count).Select(item => item.Id)), string.Empty);
            Click("Warehouse Tab Valuables");
            Assert.That(CollectionTiles().Length, Is.EqualTo(Math.Min(3, count)));
            AssertCollectionDoesNotOverlapDetails();
            foreach (CoreWarehouseTreasureDefinition treasure in CoreWarehouseCollection.Catalog.Take(Math.Min(3, count)))
            {
                RawImage inventoryArt = Find<RawImage>("Warehouse Inventory Art " + treasure.Id);
                ItemIconArt expectedArt = ItemIconCatalog.ForTreasure(treasure);
                Assert.That(expectedArt.IsAvailable, Is.True, treasure.Name);
                Assert.That(inventoryArt.texture, Is.SameAs(expectedArt.Texture), treasure.Name);
                Assert.That(inventoryArt.uvRect, Is.EqualTo(expectedArt.Uv), treasure.Name);
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        public void RareWarehouseSampleUsesTheRealMerchantHoldWithoutWritingProfile(int state)
        {
            SetDistinctPlayerFixture();
            Dictionary<string, int?> expectedInts = IntKeys().ToDictionary(key => key,
                key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null);
            Dictionary<string, string> expectedStrings = StringKeys().ToDictionary(key => key,
                key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null);
            CreatePreview();
            controller.ConfigureEditorPreviewWarehouseRare(state);
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(1));
            Assert.That(controller.Session.CargoCapacityValue, Is.EqualTo(CoreVoyageSession.Ships[1].CargoCapacity));
            Assert.That(controller.Session.Cargo.UsedVolume, Is.LessThanOrEqualTo(controller.Session.CargoCapacityValue));
            Assert.That(Find<Text>("Warehouse Rare Hint").text, Does.Contain("最多4盏生效"));
            if (state == 1) Find<RectTransform>("Warehouse Cargo Adjustment");
            AssertPlayerPreferences(expectedInts, expectedStrings);
        }

        private Button[] CollectionTiles() => host.GetComponentsInChildren<Button>(true)
            .Where(button => button.name.StartsWith("Warehouse Treasure ", StringComparison.Ordinal) &&
                !button.name.StartsWith("Warehouse Treasure Page ", StringComparison.Ordinal)).ToArray();

        private void AssertCollectionDoesNotOverlapDetails()
        {
            Canvas.ForceUpdateCanvases();
            Button[] tiles = CollectionTiles();
            Rect[] details = new[] { "Warehouse Treasure Title", "Warehouse Treasure Source", "Warehouse Treasure Story" }
                .Select(name => WorldBounds(Find<RectTransform>(name))).ToArray();
            Text hint = host.GetComponentsInChildren<Text>(true).FirstOrDefault(text => text.name == "Warehouse Collection Hint");
            for (int index = 0; index < tiles.Length; index++)
            {
                Rect card = WorldBounds(tiles[index].GetComponent<RectTransform>());
                foreach (Rect detail in details) Assert.That(card.Overlaps(detail), Is.False, tiles[index].name + " covers the archive.");
                if (hint != null) Assert.That(card.Overlaps(WorldBounds(hint.rectTransform)), Is.False, "Hint covers " + tiles[index].name);
                for (int other = index + 1; other < tiles.Length; other++)
                    Assert.That(card.Overlaps(WorldBounds(tiles[other].GetComponent<RectTransform>())), Is.False);
            }
        }

        private static Rect WorldBounds(RectTransform transform)
        {
            var corners = new Vector3[4];
            transform.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners.Min(point => point.x), corners.Min(point => point.y),
                corners.Max(point => point.x), corners.Max(point => point.y));
        }

        [Test]
        public void ExpandedCabinetArtMatchesTheWarehouseBackgroundSurface()
        {
            Texture2D expanded = Resources.Load<Texture2D>("WarehouseV2/trophy_cabinet_v2_expanded");
            Assert.That(expanded, Is.Not.Null);
            Assert.That(expanded.width, Is.EqualTo(1920));
            Assert.That(expanded.height, Is.EqualTo(990));
        }

        [TestCase("WarehouseV2/first_victory_compass_v1", true, true)]
        [TestCase("WarehouseV2/warehouse_boatswain_bell_v1", true, true)]
        [TestCase("WarehouseV2/warehouse_sealed_chart_v1", true, false)]
        [TestCase("WarehouseV2/trophy_cabinet_v1", false, true)]
        public void AuthoredWarehouseTexturesImportWithExpectedAlpha(string resourcePath, bool cutout, bool hasOpaquePixels)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            Assert.That(texture, Is.Not.Null, resourcePath);
            string assetPath = AssetDatabase.GetAssetPath(texture);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            if (cutout)
            {
                Assert.That(importer.DoesSourceTextureHaveAlpha(), Is.True);
                Assert.That(importer.alphaIsTransparency, Is.True);
            }
            Assert.That(texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Texture2D decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(ImageConversion.LoadImage(decoded, File.ReadAllBytes(assetPath), false), Is.True);
                Color32[] pixels = decoded.GetPixels32();
                Assert.That(pixels.Any(pixel => pixel.a == 0), Is.EqualTo(cutout),
                    "The compass is a transparent cutout; the cabinet is an opaque environment.");
                Assert.That(pixels.Any(pixel => pixel.a == 255), Is.EqualTo(hasOpaquePixels),
                    "The authored texture should match its expected opaque-pixel profile.");
                Assert.That(pixels.Any(pixel => pixel.a > 0), Is.True, "The authored texture must contain visible artwork.");
            }
            finally { Object.DestroyImmediate(decoded); }
        }

        private void CreatePreview()
        {
            host = new GameObject("Warehouse Editor Preview Test");
            host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Warehouse, CoreGameplayPreviewPort.GrayGull, 0);
            host.SetActive(true);
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
        }

        private static void AssertVisibleContained(RectTransform parent, RectTransform child, float tolerance)
        {
            Rect LocalBounds(RectTransform item)
            {
                var corners = new Vector3[4]; item.GetWorldCorners(corners);
                Vector3[] local = corners.Select(parent.InverseTransformPoint).ToArray();
                return Rect.MinMaxRect(local.Min(point => point.x), local.Min(point => point.y),
                    local.Max(point => point.x), local.Max(point => point.y));
            }
            Rect visible = LocalBounds(child);
            for (Transform ancestor = child.parent; ancestor != null && ancestor != parent; ancestor = ancestor.parent)
            {
                RectMask2D mask = ancestor.GetComponent<RectMask2D>();
                if (mask == null || !mask.isActiveAndEnabled) continue;
                Rect clip = LocalBounds(mask.rectTransform);
                visible = Rect.MinMaxRect(Mathf.Max(visible.xMin, clip.xMin), Mathf.Max(visible.yMin, clip.yMin),
                    Mathf.Min(visible.xMax, clip.xMax), Mathf.Min(visible.yMax, clip.yMax));
                if (visible.width <= 0f || visible.height <= 0f) return;
            }
            Assert.That(visible.xMin, Is.GreaterThanOrEqualTo(parent.rect.xMin - tolerance), child.name);
            Assert.That(visible.xMax, Is.LessThanOrEqualTo(parent.rect.xMax + tolerance), child.name);
            Assert.That(visible.yMin, Is.GreaterThanOrEqualTo(parent.rect.yMin - tolerance), child.name);
            Assert.That(visible.yMax, Is.LessThanOrEqualTo(parent.rect.yMax + tolerance), child.name);
        }

        private static void AssertContained(RectTransform parent, RectTransform child, float tolerance)
        {
            Vector3[] corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Rect bounds = parent.rect;
            foreach (Vector3 corner in corners)
            {
                Vector3 point = parent.InverseTransformPoint(corner);
                Assert.That(point.x, Is.InRange(bounds.xMin - tolerance, bounds.xMax + tolerance), child.name + " horizontal bounds");
                Assert.That(point.y, Is.InRange(bounds.yMin - tolerance, bounds.yMax + tolerance), child.name + " vertical bounds");
            }
        }

        private T Find<T>(string name) where T : Component
        {
            T result = host.GetComponentsInChildren<T>(true).FirstOrDefault(item => item.name == name);
            Assert.That(result, Is.Not.Null, name);
            return result;
        }

        private void Click(string name)
        {
            Button button = Find<Button>(name);
            Assert.That(button.interactable, Is.True, name);
            button.onClick.Invoke();
        }

        private static void SetDistinctPlayerFixture()
        {
            ProfileGoldPersistence.Save(12345);
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, "白贝港");
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseOwnedKey, string.Empty);
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseDisplayKey, ",,,,,");
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 0);
            PlayerPrefs.SetInt(CoreGameplayDemoController.GuidedTutorialWarehousePendingKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.GuidedTutorialCheckpointKey, 5);
            foreach (string resource in CargoResources) PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey(resource), 321);
        }

        private static void AssertPlayerPreferences(Dictionary<string, int?> integers, Dictionary<string, string> strings)
        {
            foreach (KeyValuePair<string, int?> entry in integers)
            {
                Assert.That(PlayerPrefs.HasKey(entry.Key), Is.EqualTo(entry.Value.HasValue), entry.Key);
                if (entry.Value.HasValue) Assert.That(PlayerPrefs.GetInt(entry.Key), Is.EqualTo(entry.Value.Value), entry.Key);
            }
            foreach (KeyValuePair<string, string> entry in strings)
            {
                Assert.That(PlayerPrefs.HasKey(entry.Key), Is.EqualTo(entry.Value != null), entry.Key);
                if (entry.Value != null) Assert.That(PlayerPrefs.GetString(entry.Key), Is.EqualTo(entry.Value), entry.Key);
            }
        }

        private static readonly string[] CargoResources =
            { "Water", "Food", "Beer", "Medicine", "Wood", "Iron", "Cloth", "Powder", "Cannonballs", "Gold", "Fame" };

        private static IEnumerable<string> IntKeys()
        {
            yield return CoreGameplayDemoController.ProfileGoldKey;
            yield return CoreGameplayDemoController.SelectedShipKey;
            yield return CoreGameplayDemoController.TutorialCompletedKey;
            yield return CoreGameplayDemoController.TutorialRewardGrantedKey;
            yield return CoreGameplayDemoController.GuidedTutorialCheckpointKey;
            yield return CoreGameplayDemoController.GuidedTutorialWarehousePendingKey;
            foreach (string resource in CargoResources) yield return CoreGameplayDemoController.CargoKey(resource);
        }

        private static IEnumerable<string> StringKeys()
        {
            yield return ProfileGoldPersistence.DecimalKey;
            yield return CoreGameplayDemoController.CurrentPortKey;
            yield return CoreGameplayDemoController.WarehouseOwnedKey;
            yield return CoreGameplayDemoController.WarehouseDisplayKey;
            yield return CoreGameplayDemoController.BattleDeckKey;
            yield return CoreGameplayDemoController.BattleCardUpgradeKey;
            yield return CoreGameplayDemoController.SpecialCardExperienceKey;
            yield return CoreGameplayDemoController.AcquiredSpecialCardsKey;
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                yield return CoreGameplayDemoController.StageBattleDeckKey(stage);
        }
    }
}




