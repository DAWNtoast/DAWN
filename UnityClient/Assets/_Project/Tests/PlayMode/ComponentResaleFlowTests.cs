using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator ComponentResale_InspectIdleItemWithoutShopActionsAndReturnToSamePage()
        {
            yield return LoadComponentResaleFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            ComponentArtEntry entry = ComponentResaleFixtureEntry();
            for (int index = 0; index < 6; index++) controller.GrantOwnedComponent(entry);
            decimal gold = controller.Session.ProfileGold;
            string[] inventory = controller.OwnedComponents.Select(item => item.InstanceId + ":" + item.ShipIndex).ToArray();
            ClickAcceptanceButton("Port Hotspot ComponentShop"); yield return null;
            ClickAcceptanceButton("Open Component Resale"); yield return null;
            ClickAcceptanceButton("Component Resale Next"); yield return null;
            Button sale = VisibleComponentSaleButtons().Single();
            string instanceId = sale.name.Substring("Select Component Sale ".Length);
            string page = GameObject.Find("Component Resale Page").GetComponent<Text>().text;
            Button row = ActiveButton("Component Resale Row " + instanceId);
            AssertResalePointerTarget("Component Resale Icon " + instanceId, row);
            AssertResalePointerTarget("Component Resale Name " + instanceId, row);
            AssertResalePointerTarget(sale.name, sale);
            ExecuteEvents.Execute(row.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            yield return null;
            Assert.That(GameObject.Find("Component Resale Panel"), Is.Null);
            Assert.That(GameObject.Find("Component Detail Name").GetComponent<Text>().text, Is.EqualTo(entry.Name));
            Assert.That(ActiveButton("Return To Port Hub").GetComponentInChildren<Text>().text, Is.EqualTo("返回出售列表"));
            GameObject detail = GameObject.Find("Component Shop Detail");
            Assert.That(detail.GetComponentsInChildren<Button>(true).Any(button => button.name.StartsWith("Purchase ")), Is.False);
            NavalBattleVisualCapture.Capture(controller, "07_Component_Resale_ReadOnly_Detail.png",
                "出售列表打开对应部件详情，不显示购买或装配按钮", "Component Shop Detail");
            foreach (string tab in new[] { "Effects", "Story", "SpecialCards", "Overview" })
            {
                ClickAcceptanceButton("ComponentDossierPage" + tab); yield return null;
                Assert.That(GameObject.Find("Component Dossier " + tab + " Page"), Is.Not.Null);
                Assert.That(detail.GetComponentsInChildren<Button>(true).Any(button => button.name.StartsWith("Purchase ")), Is.False);
            }
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            CollectionAssert.AreEqual(inventory, controller.OwnedComponents.Select(item => item.InstanceId + ":" + item.ShipIndex).ToArray());
            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            Assert.That(GameObject.Find("Component Resale Page").GetComponent<Text>().text, Is.EqualTo(page));
            sale = VisibleComponentSaleButtons().Single();
            Assert.That(sale.name, Is.EqualTo("Select Component Sale " + instanceId));
            AssertResalePointerTarget(sale.name, sale);
            ExecuteEvents.Execute(sale.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            yield return null;
            Assert.That(GameObject.Find("Component Sale Confirmation"), Is.Not.Null);
            Assert.That(GameObject.Find("Component Shop Detail"), Is.Null);
            ClickAcceptanceButton("Cancel Component Sale"); yield return null;
            ClickAcceptanceButton("Component Resale Row " + instanceId); yield return null;
            ClickAcceptanceButton("Next Port Facility"); yield return null;
            ClickAcceptanceButton("Previous Port Facility"); yield return null;
            Assert.That(GameObject.Find("Component Resale Panel"), Is.Null);
            Assert.That(GameObject.Find("Component Shop Detail"), Is.Null);
            Button hero = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Single(button => button.gameObject.activeInHierarchy && button.name.StartsWith("Component Shop Hero "));
            hero.onClick.Invoke(); yield return null;
            Button[] actions = GameObject.Find("Component Shop Detail").GetComponentsInChildren<Button>();
            Assert.That(actions.Count(button => button.name.StartsWith("Purchase Component ")), Is.EqualTo(1));
            Assert.That(actions.Count(button => button.name.StartsWith("Purchase And Equip Component ")), Is.EqualTo(1));
            Assert.That(ActiveButton("Return To Port Hub").GetComponentInChildren<Text>().text, Is.EqualTo("返回部件陈列"));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            CollectionAssert.AreEqual(inventory, controller.OwnedComponents.Select(item => item.InstanceId + ":" + item.ShipIndex).ToArray());
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertResalePointerTarget(string targetName, Button expected)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform target = GameObject.Find(targetName).GetComponent<RectTransform>();
            Canvas canvas = target.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 position = RectTransformUtility.WorldToScreenPoint(camera, target.TransformPoint(target.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { position = position };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, targetName);
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),
                Is.SameAs(expected.gameObject), targetName + " must route to its intended action.");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ComponentResale_RealButtonsCancelThenSellOneCopyAndPersistWithoutRepeatedPayment()
        {
            yield return LoadComponentResaleFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            ComponentArtEntry entry = ComponentResaleFixtureEntry();
            OwnedShipComponent first = controller.GrantOwnedComponent(entry);
            OwnedShipComponent retained = controller.GrantOwnedComponent(entry);
            int count = controller.OwnedComponents.Count;
            decimal initialGold = controller.Session.ProfileGold;
            int price = controller.OwnedComponentSalePrice(first.InstanceId);
            Assert.That(price, Is.EqualTo(PortComponentShopCatalog.CalculateSalePrice(entry, controller.Session.CurrentPort)));
            Assert.That(controller.OwnedComponentSalePrice(retained.InstanceId), Is.EqualTo(price), "Copies of the same component have the same resale value.");
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
            Assert.That(controller.OwnedComponents.Single(item => item.InstanceId == retained.InstanceId).ComponentId, Is.EqualTo(entry.Id));
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
            Assert.That(controller.OwnedComponents.Single(item => item.InstanceId == retained.InstanceId).ComponentId, Is.EqualTo(entry.Id));
            ClickAcceptanceButton("Port Hotspot ComponentShop"); yield return null;
            ClickAcceptanceButton("Open Component Resale"); yield return null;
            Assert.That(VisibleComponentSaleButtons().Single().name, Is.EqualTo("Select Component Sale " + retained.InstanceId));
            CaptureComponentResale(controller, "04_Component_Resale_Reload", "重载保留金币、余下部件身份与已售实例删除状态");
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
                controller.GrantOwnedComponent(ComponentResaleFixtureEntry());
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
            OwnedShipComponent spare = controller.GrantOwnedComponent(ComponentResaleFixtureEntry());
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
            OwnedShipComponent spare = controller.GrantOwnedComponent(ComponentResaleFixtureEntry());
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
            OwnedShipComponent spare = controller.GrantOwnedComponent(ComponentResaleFixtureEntry());
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
            string[] inventory = controller.OwnedComponents.Select(item => item.InstanceId + ":" + item.ShipIndex + ":" + item.ComponentId)
                .OrderBy(value => value, System.StringComparer.Ordinal).ToArray();
            string savedInventory = PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey);
            decimal savedGold = ProfileGoldPersistence.Load();
            Assert.That(controller.TrySellOwnedComponent(instanceId), Is.False);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            CollectionAssert.AreEqual(inventory, controller.OwnedComponents.Select(item => item.InstanceId + ":" + item.ShipIndex + ":" + item.ComponentId)
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
