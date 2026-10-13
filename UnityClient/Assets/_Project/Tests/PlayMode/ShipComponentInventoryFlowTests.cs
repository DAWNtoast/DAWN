using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator ComponentShopHeader_RetainedHeaderRestoresOtherFacilitiesAndShopReadouts()
        {
            yield return LoadComponentPurchaseFixture();
            ActiveButton("Port Hotspot ComponentShop").onClick.Invoke();
            yield return null;
            GameObject header = GameObject.Find("Port Facility Header");
            RectTransform title = GameObject.Find("Port Facility Title").GetComponent<RectTransform>();
            Vector2 shopPosition = title.anchoredPosition;
            Assert.That(ActiveButton("Previous Port Facility").GetComponentInChildren<Text>().text, Is.EqualTo("酒馆"));
            Assert.That(ActiveButton("Next Port Facility").GetComponentInChildren<Text>().text, Is.EqualTo("卡组工坊"));
            Assert.That(GameObject.Find("Port Facility Gold").GetComponent<Text>().text, Does.StartWith("金币  "));
            ActiveButton("Next Port Facility").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Port Facility Header"), Is.SameAs(header));
            Assert.That(title.anchoredPosition, Is.Not.EqualTo(shopPosition));
            Assert.That(GameObject.Find("Port Facility Gold").GetComponent<Text>().text, Does.Contain("总金币"));
            Assert.That(ActiveButton("Previous Port Facility").GetComponentInChildren<Text>().text, Does.StartWith("← "));
            ActiveButton("Previous Port Facility").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Port Facility Header"), Is.SameAs(header));
            Assert.That(title.anchoredPosition, Is.EqualTo(shopPosition));
            Assert.That(GameObject.Find("Port Facility Gold").GetComponent<Text>().text, Does.StartWith("金币  "));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ComponentPurchase_BuyOnlyStoresOneItemWithoutChangingFlagshipAndPersists()
        {
            return VerifyComponentPurchase(false);
        }

        [UnityTest]
        public IEnumerator ComponentPurchase_BuyAndEquipReplacesOneItemAndPersists()
        {
            return VerifyComponentPurchase(true);
        }

        [UnityTest]
        public IEnumerator ComponentPurchase_UnownedShipDisablesEquipButKeepsInventoryPurchaseAvailable()
        {
            yield return LoadComponentPurchaseFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            PortComponentShopStockItem offer = SelectComponentPurchaseOffer(controller);
            string key = CoreGameplayDemoController.ShipOwnedKey(1);
            bool existed = PlayerPrefs.HasKey(key);
            int value = PlayerPrefs.GetInt(key, 0);
            try
            {
                PlayerPrefs.DeleteKey(key);
                Assert.That(controller.Session.SelectShip(1), Is.True);
                yield return OpenComponentPurchaseOffer(offer);
                Button purchase = ActiveButton("Purchase Component " + offer.Id);
                Button equip = ActiveButton("Purchase And Equip Component " + offer.Id);
                Assert.That(purchase.interactable, Is.True);
                Assert.That(equip.interactable, Is.False);
                decimal gold = controller.Session.ProfileGold;
                int count = controller.OwnedComponents.Count;
                string inventory = PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey);
                equip.onClick.Invoke();
                yield return null;
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                Assert.That(controller.OwnedComponents.Count, Is.EqualTo(count));
                Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey), Is.EqualTo(inventory));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ComponentShopPurchaseKey(offer.Id), 0), Is.Zero);
            }
            finally
            {
                if (existed) PlayerPrefs.SetInt(key, value); else PlayerPrefs.DeleteKey(key);
            }
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator VerifyComponentPurchase(bool equipAfterPurchase)
        {
            yield return LoadComponentPurchaseFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            PortComponentShopStockItem offer = SelectComponentPurchaseOffer(controller);
            int shipIndex = controller.Session.SelectedShipIndex;
            OwnedShipComponent previous = controller.InstalledShipComponent(shipIndex, offer.Entry.Slot);
            string previousIdentity = previous.InstanceId;
            float previousQuality = previous.Quality;
            string[] originalIdentities = controller.OwnedComponents.Select(item => item.InstanceId).ToArray();
            float[] originalStats = ComponentPurchaseShipStats(controller);
            string[] originalCards = ComponentPurchaseBonusCards(controller);
            decimal originalGold = controller.Session.ProfileGold;
            yield return OpenComponentPurchaseOffer(offer);

            Button purchase = ActiveButton("Purchase Component " + offer.Id);
            Button equip = ActiveButton("Purchase And Equip Component " + offer.Id);
            Assert.That(purchase.interactable, Is.True);
            Assert.That(equip.interactable, Is.True);
            Assert.That(purchase.GetComponentInChildren<Text>().text, Does.StartWith("购买 · "));
            Assert.That(equip.GetComponentInChildren<Text>().text, Does.StartWith("购买后装配 · "));
            Button.ButtonClickedEvent originalPurchase = purchase.onClick;
            Button.ButtonClickedEvent originalEquip = equip.onClick;
            (equipAfterPurchase ? originalEquip : originalPurchase).Invoke();
            yield return null;

            OwnedShipComponent bought = controller.OwnedComponents.Single(item => !originalIdentities.Contains(item.InstanceId));
            string boughtIdentity = bought.InstanceId;
            Assert.That(controller.OwnedComponents.Count, Is.EqualTo(originalIdentities.Length + 1));
            Assert.That(bought.ComponentId, Is.EqualTo(offer.Entry.Id));
            Assert.That(bought.Quality, Is.EqualTo(offer.Quality).Within(0.0001f));
            Assert.That(bought.ShipIndex, Is.EqualTo(equipAfterPurchase ? shipIndex : -1));
            Assert.That(previous.ShipIndex, Is.EqualTo(equipAfterPurchase ? -1 : shipIndex));
            Assert.That(previous.Quality, Is.EqualTo(previousQuality));
            Assert.That(controller.InstalledShipComponent(shipIndex, offer.Entry.Slot).InstanceId,
                Is.EqualTo(equipAfterPurchase ? boughtIdentity : previousIdentity));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(originalGold - offer.Price));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ComponentShopPurchaseKey(offer.Id)), Is.EqualTo(1));
            Assert.That(ActiveButton("Purchase Component " + offer.Id).interactable, Is.False);
            Assert.That(ActiveButton("Purchase And Equip Component " + offer.Id).interactable, Is.False);
            if (equipAfterPurchase)
            {
                Assert.That(ComponentPurchaseShipStats(controller), Is.Not.EqualTo(originalStats));
                string specialCardId = CoreSpecialCardCatalog.ForComponent(offer.Entry.Id).Card.Id;
                Assert.That(ComponentPurchaseBonusCards(controller).Any(card => card.EndsWith(":" + specialCardId)), Is.True);
            }
            else
            {
                CollectionAssert.AreEqual(originalStats, ComponentPurchaseShipStats(controller),
                    "Buying into inventory must leave all current ship statistics unchanged.");
                CollectionAssert.AreEqual(originalCards, ComponentPurchaseBonusCards(controller),
                    "An idle component must not unlock its equipped special card.");
            }

            // Saved UnityEvents survive the old buttons' deferred destruction.
            // Replaying either route must not charge again or create another item.
            string purchasedInventory = PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey);
            originalPurchase.Invoke();
            originalEquip.Invoke();
            originalPurchase.Invoke();
            yield return null;
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(originalGold - offer.Price));
            Assert.That(controller.OwnedComponents.Count, Is.EqualTo(originalIdentities.Length + 1));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey), Is.EqualTo(purchasedInventory));

            if (equipAfterPurchase)
            {
                Assert.That(controller.TryEquipOwnedComponent(shipIndex, previousIdentity), Is.True);
                originalEquip.Invoke();
                originalPurchase.Invoke();
                yield return null;
                Assert.That(controller.InstalledShipComponent(shipIndex, offer.Entry.Slot).InstanceId, Is.EqualTo(previousIdentity),
                    "An already purchased offer's queued equip callback must not undo a later refit.");
                Assert.That(bought.ShipIndex, Is.EqualTo(-1));
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(originalGold - offer.Price));
                Assert.That(controller.OwnedComponents.Count, Is.EqualTo(originalIdentities.Length + 1));
                Assert.That(controller.TryEquipOwnedComponent(shipIndex, boughtIdentity), Is.True);
            }

            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            bought = controller.OwnedComponents.Single(item => item.InstanceId == boughtIdentity);
            previous = controller.OwnedComponents.Single(item => item.InstanceId == previousIdentity);
            Assert.That(controller.OwnedComponents.Count, Is.EqualTo(originalIdentities.Length + 1));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(originalGold - offer.Price));
            Assert.That(bought.Quality, Is.EqualTo(offer.Quality).Within(0.0001f));
            Assert.That(bought.ShipIndex, Is.EqualTo(equipAfterPurchase ? shipIndex : -1));
            Assert.That(previous.ShipIndex, Is.EqualTo(equipAfterPurchase ? -1 : shipIndex));
            Assert.That(previous.Quality, Is.EqualTo(previousQuality));
            Assert.That(controller.InstalledShipComponent(shipIndex, offer.Entry.Slot).InstanceId,
                Is.EqualTo(equipAfterPurchase ? boughtIdentity : previousIdentity));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ComponentShopPurchaseKey(offer.Id)), Is.EqualTo(1));
            ActiveButton("Port Hotspot ComponentShop").onClick.Invoke();
            yield return null;
            yield return FindComponentPurchaseOffer(offer);
            Assert.That(ActiveButton("Component Shop Offer " + offer.Id).interactable, Is.True);
            ActiveButton("Component Shop Offer " + offer.Id).onClick.Invoke();
            yield return null;
            Assert.That(ActiveButton("Purchase Component " + offer.Id).interactable, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ComponentPurchase_BothButtonsRejectInsufficientGoldWithoutMutatingInventory()
        {
            yield return LoadComponentPurchaseFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            PortComponentShopStockItem offer = SelectComponentPurchaseOffer(controller);
            controller.Session.RestoreProfile(offer.Price - 1, offer.PortName);
            yield return OpenComponentPurchaseOffer(offer);
            Button purchase = ActiveButton("Purchase Component " + offer.Id);
            Button equip = ActiveButton("Purchase And Equip Component " + offer.Id);
            Assert.That(purchase.interactable, Is.False);
            Assert.That(equip.interactable, Is.False);
            Button.ButtonClickedEvent purchaseCallback = purchase.onClick, equipCallback = equip.onClick;
            string inventory = PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey);
            float[] stats = ComponentPurchaseShipStats(controller);
            string[] cards = ComponentPurchaseBonusCards(controller);
            purchaseCallback.Invoke();
            equipCallback.Invoke();
            yield return null;
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(offer.Price - 1));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ComponentShopPurchaseKey(offer.Id)), Is.Zero);
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey), Is.EqualTo(inventory));
            CollectionAssert.AreEqual(stats, ComponentPurchaseShipStats(controller));
            CollectionAssert.AreEqual(cards, ComponentPurchaseBonusCards(controller));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ComponentPurchase_BothOldCallbacksRejectPortMonthAndBatchChanges()
        {
            foreach (string change in new[] { "port", "month", "batch" })
            {
                yield return LoadComponentPurchaseFixture();
                CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                PortComponentShopStockItem offer = SelectComponentPurchaseOffer(controller);
                yield return OpenComponentPurchaseOffer(offer);
                Button.ButtonClickedEvent purchase = ActiveButton("Purchase Component " + offer.Id).onClick;
                Button.ButtonClickedEvent equip = ActiveButton("Purchase And Equip Component " + offer.Id).onClick;
                decimal gold = controller.Session.ProfileGold;
                string inventory = PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey);
                int count = controller.OwnedComponents.Count;
                if (change == "port")
                    controller.Session.RestoreProfile(gold, offer.PortName == "断锚港" ? "白贝港" : "断锚港");
                else if (change == "month")
                    Assert.That(controller.Session.RestoreGameClock((controller.Session.GameDateTime.AddMonths(1) -
                        CoreVoyageSession.GameClockOrigin).TotalHours), Is.True);
                else
                    PlayerPrefs.SetInt(CoreGameplayDemoController.GameMasterShopRefreshKey(offer.PortName), offer.RefreshRevision + 1);
                purchase.Invoke();
                equip.Invoke();
                yield return null;
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold), change);
                Assert.That(controller.OwnedComponents.Count, Is.EqualTo(count), change);
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ComponentShopPurchaseKey(offer.Id)), Is.Zero, change);
                Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey), Is.EqualTo(inventory), change);
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator LoadComponentPurchaseFixture()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            // Purchase-flow tests exercise replacement semantics; opt this
            // fixture into a populated loadout explicitly now that real new
            // profiles start with a bare hull.
            PopulateBareStarterForFixture(Object.FindFirstObjectByType<CoreGameplayDemoController>());
        }

        private static PortComponentShopStockItem SelectComponentPurchaseOffer(CoreGameplayDemoController controller)
        {
            ComponentArtCatalog catalog = new ComponentArtCatalog();
            foreach (string port in new[] { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港" })
            {
                PortComponentShopStockItem offer = PortComponentShopCatalog.BuildExpandedStock(catalog, port,
                    controller.Session.GameDateTime).FirstOrDefault(item => item.Entry.Rarity >= ComponentRarity.Rare &&
                    controller.EquippedComponent(item.Entry.Slot)?.Id != item.Entry.Id);
                if (offer == null) continue;
                controller.Session.RestoreProfile(10000, port);
                return offer;
            }
            Assert.Fail("A regional shop must offer a rare component different from the installed component.");
            return null;
        }

        private static IEnumerator OpenComponentPurchaseOffer(PortComponentShopStockItem offer)
        {
            ActiveButton("Port Hotspot ComponentShop").onClick.Invoke();
            yield return null;
            yield return FindComponentPurchaseOffer(offer);
            Button display = ActiveButton("Component Shop Offer " + offer.Id);
            Assert.That(display.interactable, Is.True);
            display.onClick.Invoke();
            yield return null;
        }

        private static IEnumerator FindComponentPurchaseOffer(PortComponentShopStockItem offer)
        {
            ActiveButton("Component Shop Category " + offer.Entry.Slot).onClick.Invoke();
            yield return null;
            for (int page = 0; page < 8 && ActiveButton("Component Shop Offer " + offer.Id) == null; page++)
            {
                Button next = ActiveButton("Component Shop Next Display");
                Assert.That(next, Is.Not.Null);
                Assert.That(next.interactable, Is.True, "The requested offer must exist in this month's showroom.");
                next.onClick.Invoke();
                yield return null;
            }
            Assert.That(ActiveButton("Component Shop Offer " + offer.Id), Is.Not.Null);
        }

        private static float[] ComponentPurchaseShipStats(CoreGameplayDemoController controller)
        {
            return new[] { controller.Session.MaxHull, controller.Session.Hull, controller.Session.Armor,
                controller.Session.Firepower, controller.Session.SpeedKnots, controller.Session.VisionBonusNm };
        }

        private static string[] ComponentPurchaseBonusCards(CoreGameplayDemoController controller)
        {
            return System.Enum.GetValues(typeof(NavalBattleStage)).Cast<NavalBattleStage>()
                .SelectMany(stage => controller.Session.GetBonusBattleCards(stage)
                    .Select(index => stage + ":" + CoreVoyageSession.CardLibrary[index].Id)).ToArray();
        }

        [UnityTest]
        public IEnumerator ComponentInventory_UnreadableRecordReturnsToMenuWithoutOverwritingProgress()
        {
            foreach (string invalid in new[] { "", "{broken", "{\"version\":99,\"items\":[]}", "{\"version\":2,\"items\":[null]}" })
            {
                PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
                PlayerPrefs.SetString(CoreGameplayDemoController.ComponentInventoryKey, invalid);
                SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
                yield return null;
                yield return null;
                yield return null;
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
                Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey), Is.EqualTo(invalid));
                Assert.That(Object.FindFirstObjectByType<CoreGameplayDemoController>(), Is.Null);
            }
            // Removing the invalid record is the same state as an explicitly reset
            // new game; normal legacy migration must still work afterwards.
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.ComponentInventoryKey);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            Assert.That(Object.FindFirstObjectByType<CoreGameplayDemoController>().OwnedComponents, Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ComponentInventory_MigratesLegacyFlagshipThenPreservesEmptySlotsAndQualityAcrossReload()
        {
            ComponentArtCatalog catalog = new ComponentArtCatalog();
            ComponentArtEntry rare = catalog.Entries.First(item => item.Slot == ComponentSlot.Cannon && item.Rarity == ComponentRarity.Rare);
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.SelectedShipKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(1), 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.ComponentLoadoutKey(ComponentSlot.Cannon), rare.Id);
            PlayerPrefs.SetInt(CoreGameplayDemoController.ComponentConditionKey(ComponentSlot.Cannon), 93);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.OwnedComponents.Count, Is.EqualTo(10));
            OwnedShipComponent item = controller.InstalledShipComponent(1, ComponentSlot.Cannon);
            string identity = item.InstanceId;
            Assert.That(item.ComponentId, Is.EqualTo(rare.Id));
            Assert.That(item.Quality, Is.EqualTo(0.93f).Within(0.0001f));
            Assert.That(controller.ShipComponent(0, ComponentSlot.Cannon).Id, Is.Not.EqualTo(rare.Id));
            Assert.That(controller.TryUnequipShipComponent(1, ComponentSlot.Cannon), Is.True);
            Assert.That(controller.TryUnequipShipComponent(1, ComponentSlot.Cannon), Is.False);
            Assert.That(controller.TryEquipOwnedComponent(0, identity), Is.True);
            Assert.That(controller.TryEquipOwnedComponent(1, identity), Is.False);
            Assert.That(controller.TryUnequipShipComponent(3, ComponentSlot.Bow), Is.False);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.OwnedComponents.Count, Is.EqualTo(10));
            Assert.That(controller.EquippedComponent(ComponentSlot.Cannon), Is.Null);
            Assert.That(controller.InstalledShipComponent(0, ComponentSlot.Cannon).InstanceId, Is.EqualTo(identity));
            Assert.That(controller.InstalledShipComponent(0, ComponentSlot.Cannon).Quality, Is.EqualTo(0.93f).Within(0.0001f));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ComponentInventory_RefitsRefreshSpecialCardsAndRejectSailingAndUnownedShips()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            ComponentArtEntry rare = new ComponentArtCatalog().Entries.First(item => item.Slot == ComponentSlot.Cannon && item.Rarity == ComponentRarity.Rare);
            OwnedShipComponent first = controller.GrantOwnedComponent(rare, 0.96f);
            OwnedShipComponent second = controller.GrantOwnedComponent(rare, 1.09f);
            Assert.That(controller.TryEquipOwnedComponent(3, first.InstanceId), Is.False);
            Assert.That(controller.TryEquipOwnedComponent(0, first.InstanceId), Is.True);
            Assert.That(controller.TryEquipOwnedComponent(0, second.InstanceId), Is.True);
            Assert.That(first.ShipIndex, Is.EqualTo(-1));
            Assert.That(first.Quality, Is.EqualTo(0.96f));
            CoreSpecialCardDefinition definition = CoreSpecialCardCatalog.ForComponent(rare.Id);
            Assert.That(System.Enum.GetValues(typeof(NavalBattleStage)).Cast<NavalBattleStage>()
                .Any(stage => controller.Session.GetBonusBattleCards(stage).Any(index => CoreVoyageSession.CardLibrary[index].Id == definition.Card.Id)), Is.True);
            Assert.That(controller.TryUnequipShipComponent(0, ComponentSlot.Cannon), Is.True);
            Assert.That(System.Enum.GetValues(typeof(NavalBattleStage)).Cast<NavalBattleStage>()
                .Any(stage => controller.Session.GetBonusBattleCards(stage).Any(index => CoreVoyageSession.CardLibrary[index].Id == definition.Card.Id)), Is.False);
            controller.Session.StartVoyage();
            Assert.That(controller.Session.Phase, Is.Not.EqualTo(CoreDemoPhase.Port));
            Assert.That(controller.TryEquipOwnedComponent(0, first.InstanceId), Is.False);
            Assert.That(controller.TryUnequipShipComponent(0, ComponentSlot.Bow), Is.False);
            Assert.That(controller.OwnedComponents.Count, Is.EqualTo(7));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
