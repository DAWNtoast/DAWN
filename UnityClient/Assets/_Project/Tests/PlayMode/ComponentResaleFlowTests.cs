using System.Collections;
using System.Linq;
using System.Reflection;
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
        [UnityTest, Timeout(120000)]
        public IEnumerator ComponentResale_RealButtonsCancelThenSellOneCopyAndPersistWithoutRepeatedPayment()
        {
            yield return LoadComponentResaleFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            ComponentArtEntry entry = ComponentResaleFixtureEntry();
            OwnedShipComponent first = controller.GrantOwnedComponent(entry, 0.93f);
            OwnedShipComponent retained = controller.GrantOwnedComponent(entry, 1.08f);
            int count = controller.OwnedComponents.Count;
            decimal initialGold = controller.Session.ProfileGold;
            int price = controller.OwnedComponentSalePrice(first.InstanceId);
            Assert.That(price, Is.EqualTo(PortComponentShopCatalog.CalculateSalePrice(entry, controller.Session.CurrentPort, first.Quality)));
            Assert.That(price, Is.GreaterThan(0));

            ClickAcceptanceButton("Port Hotspot ComponentShop"); yield return null;
            ClickAcceptanceButton("Open Component Resale"); yield return null;
            Assert.That(GameObject.Find("Component Resale Panel"), Is.Not.Null);
            Assert.That(VisibleComponentSaleButtons().Length, Is.EqualTo(2), "Equipped fittings must not appear in the idle resale inventory.");
            CaptureComponentResale(controller, "01_Component_Resale_Inventory", "部件商店真实入口列出两个独立闲置实例");
            ClickAcceptanceButton("Select Component Sale " + first.InstanceId); yield return null;
            Assert.That(GameObject.Find("Component Sale Confirmation"), Is.Not.Null);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(initialGold), "Selecting an item is not consent to sell it.");
            Assert.That(controller.OwnedComponents.Count, Is.EqualTo(count));
            Button.ButtonClickedEvent cancelledConfirmation = ActiveButton("Confirm Component Sale").onClick;
            ClickAcceptanceButton("Cancel Component Sale"); yield return null;
            cancelledConfirmation.Invoke(); yield return null;
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(initialGold));
            Assert.That(controller.OwnedComponents.Count, Is.EqualTo(count), "A retained callback from a cancelled confirmation cannot sell the item.");

            ClickAcceptanceButton("Select Component Sale " + first.InstanceId); yield return null;
            CaptureComponentResale(controller, "02_Component_Resale_Confirmation", "确认层展示要出售的单件部件与回购报价");
            Button.ButtonClickedEvent confirmedCallback = ActiveButton("Confirm Component Sale").onClick;
            ClickAcceptanceButton("Confirm Component Sale"); yield return null;
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(initialGold + price));
            Assert.That(controller.OwnedComponents.Count, Is.EqualTo(count - 1));
            Assert.That(controller.OwnedComponents.Any(item => item.InstanceId == first.InstanceId), Is.False);
            Assert.That(controller.OwnedComponents.Single(item => item.InstanceId == retained.InstanceId).Quality, Is.EqualTo(1.08f));
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(initialGold + price));
            string saved = PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey);
            confirmedCallback.Invoke(); confirmedCallback.Invoke(); yield return null;
            Assert.That(controller.TrySellOwnedComponent(first.InstanceId), Is.False);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(initialGold + price));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey), Is.EqualTo(saved));
            CaptureComponentResale(controller, "03_Component_Resale_Sold", "确认只移除指定实例并即时入金，重复旧回调无效");

            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(initialGold + price));
            Assert.That(controller.OwnedComponents.Count, Is.EqualTo(count - 1));
            Assert.That(controller.OwnedComponents.Any(item => item.InstanceId == first.InstanceId), Is.False);
            Assert.That(controller.OwnedComponents.Single(item => item.InstanceId == retained.InstanceId).Quality, Is.EqualTo(1.08f));
            ClickAcceptanceButton("Port Hotspot ComponentShop"); yield return null;
            ClickAcceptanceButton("Open Component Resale"); yield return null;
            Assert.That(VisibleComponentSaleButtons().Single().name, Is.EqualTo("Select Component Sale " + retained.InstanceId));
            CaptureComponentResale(controller, "04_Component_Resale_Reload", "重载保留金币、余下部件品质与已售实例删除状态");
            ClickAcceptanceButton("Back To Component Shop Sales"); yield return null;
            Assert.That(ActiveButton("Open Component Resale"), Is.Not.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ComponentResale_PaginatesFiveIdleItemsAndClampsAfterSellingTheLastPage()
        {
            yield return LoadComponentResaleFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            for (int index = 0; index < 6; index++)
                controller.GrantOwnedComponent(ComponentResaleFixtureEntry(), 0.94f + index * 0.02f);
            ClickAcceptanceButton("Port Hotspot ComponentShop"); yield return null;
            ClickAcceptanceButton("Open Component Resale"); yield return null;
            Assert.That(VisibleComponentSaleButtons().Length, Is.EqualTo(5));
            CaptureComponentResale(controller, "05_Component_Resale_Full_Page", "五行部件完整展示，分页入口保持可见");
            string[] pageOne = VisibleComponentSaleButtons().Select(button => button.name).ToArray();
            ClickAcceptanceButton("Component Resale Next"); yield return null;
            Assert.That(VisibleComponentSaleButtons().Length, Is.EqualTo(1));
            string lastName = VisibleComponentSaleButtons().Single().name;
            Assert.That(pageOne, Does.Not.Contain(lastName));
            ClickAcceptanceButton("Component Resale Previous"); yield return null;
            CollectionAssert.AreEquivalent(pageOne, VisibleComponentSaleButtons().Select(button => button.name).ToArray());
            ClickAcceptanceButton("Component Resale Next"); yield return null;
            ClickAcceptanceButton(lastName); yield return null;
            ClickAcceptanceButton("Confirm Component Sale"); yield return null;
            Assert.That(VisibleComponentSaleButtons().Length, Is.EqualTo(5), "Removing the sixth item must return to the remaining page.");
            CollectionAssert.AreEquivalent(pageOne, VisibleComponentSaleButtons().Select(button => button.name).ToArray());
            Button next = ActiveButton("Component Resale Next");
            Assert.That(next == null || !next.IsInteractable(), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ComponentResale_LeavingShopClearsConfirmationAndInvalidatesOldCallback()
        {
            yield return LoadComponentResaleFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            OwnedShipComponent spare = controller.GrantOwnedComponent(ComponentResaleFixtureEntry(), 1f);
            decimal gold = controller.Session.ProfileGold;
            ClickAcceptanceButton("Port Hotspot ComponentShop"); yield return null;
            ClickAcceptanceButton("Open Component Resale"); yield return null;
            ClickAcceptanceButton("Select Component Sale " + spare.InstanceId); yield return null;
            Button.ButtonClickedEvent oldConfirmation = ActiveButton("Confirm Component Sale").onClick;
            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            ClickAcceptanceButton("Port Hotspot ComponentShop"); yield return null;
            Assert.That(GameObject.Find("Component Sale Confirmation"), Is.Null);
            Assert.That(GameObject.Find("Component Resale Panel"), Is.Null);
            Assert.That(ActiveButton("Open Component Resale"), Is.Not.Null);
            oldConfirmation.Invoke(); yield return null;
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            Assert.That(controller.OwnedComponents.Any(item => item.InstanceId == spare.InstanceId), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ComponentResale_GuardsAllEquippedShipsMissingItemsAccountLimitTutorialPreviewAndSailing()
        {
            yield return LoadComponentResaleFixture(true);
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            OwnedShipComponent spare = controller.GrantOwnedComponent(ComponentResaleFixtureEntry(), 1.04f);
            foreach (OwnedShipComponent installed in controller.OwnedComponents.Where(item => item.ShipIndex >= 0).ToArray())
                AssertComponentSaleRejected(controller, installed.InstanceId);
            AssertComponentSaleRejected(controller, "unknown-instance");
            AssertComponentSaleRejected(controller, null);

            int price = controller.OwnedComponentSalePrice(spare.InstanceId);
            controller.Session.RestoreProfile(CoreEconomyBalance.MaximumProfileGold - price + 1, controller.Session.CurrentPort);
            AssertComponentSaleRejected(controller, spare.InstanceId);
            controller.Session.RestoreProfile(500, controller.Session.CurrentPort);
            FieldInfo tutorial = typeof(CoreGameplayDemoController).GetField("tutorialActive", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(tutorial, Is.Not.Null);
            tutorial.SetValue(controller, true);
            try { AssertComponentSaleRejected(controller, spare.InstanceId); }
            finally { tutorial.SetValue(controller, false); }
            FieldInfo preview = typeof(CoreGameplayDemoController).GetField("editorPreviewBuilding", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(preview, Is.Not.Null);
            preview.SetValue(controller, true);
            try { AssertComponentSaleRejected(controller, spare.InstanceId); }
            finally { preview.SetValue(controller, false); }

            controller.Session.StartVoyage();
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            AssertComponentSaleRejected(controller, spare.InstanceId);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ComponentResale_ExactRemainingAccountCapacityAcceptsFullPaymentOnce()
        {
            yield return LoadComponentResaleFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            OwnedShipComponent spare = controller.GrantOwnedComponent(ComponentResaleFixtureEntry(), 1f);
            int price = controller.OwnedComponentSalePrice(spare.InstanceId);
            controller.Session.RestoreProfile(CoreEconomyBalance.MaximumProfileGold - price, controller.Session.CurrentPort);
            Assert.That(controller.TrySellOwnedComponent(spare.InstanceId), Is.True);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(CoreEconomyBalance.MaximumProfileGold));
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(CoreEconomyBalance.MaximumProfileGold));
            AssertComponentSaleRejected(controller, spare.InstanceId);
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator LoadComponentResaleFixture(bool ownSecondShip = false)
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            ProfileGoldPersistence.Save(500);
            if (ownSecondShip) PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(1), 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
        }

        private static ComponentArtEntry ComponentResaleFixtureEntry() => new ComponentArtCatalog().ExpandedEntries
            .First(entry => entry.Slot == ComponentSlot.Cannon && entry.Rarity == ComponentRarity.Rare);

        private static Button[] VisibleComponentSaleButtons() => Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .Where(button => button.gameObject.activeInHierarchy && button.name.StartsWith("Select Component Sale ", System.StringComparison.Ordinal)).ToArray();

        private static void AssertComponentSaleRejected(CoreGameplayDemoController controller, string instanceId)
        {
            decimal gold = controller.Session.ProfileGold;
            string[] inventory = controller.OwnedComponents.Select(item => item.InstanceId + ":" + item.ShipIndex + ":" + item.Quality)
                .OrderBy(value => value, System.StringComparer.Ordinal).ToArray();
            string savedInventory = PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey);
            decimal savedGold = ProfileGoldPersistence.Load();
            Assert.That(controller.TrySellOwnedComponent(instanceId), Is.False);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            CollectionAssert.AreEqual(inventory, controller.OwnedComponents.Select(item => item.InstanceId + ":" + item.ShipIndex + ":" + item.Quality)
                .OrderBy(value => value, System.StringComparer.Ordinal).ToArray());
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey), Is.EqualTo(savedInventory));
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(savedGold));
        }

        private static void CaptureComponentResale(CoreGameplayDemoController controller, string name, string caption)
        {
            NavalBattleVisualCapture.Capture(controller, name + ".png", caption,
                GameObject.Find("Component Sale Confirmation") != null ? "Component Sale Confirmation" : "Component Resale Panel");
        }
    }
}
