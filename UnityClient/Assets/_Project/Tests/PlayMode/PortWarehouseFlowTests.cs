using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        // Port changes and cargo loading below are explicit arrival-boundary
        // fixtures. Only storage, withdrawal, investment and saving use real UI
        // transactions; this suite does not claim to have sailed these routes.
        [UnityTest, Timeout(120000)]
        public IEnumerator PortWarehouse_TwoDeliveryFixturesAccumulateLocallyThenInvestmentSpendsOnlyThisPortAndReloads()
        {
            const string investmentPort = "白贝港", otherPort = "断锚港";
            yield return LoadProvisioningFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            PortInvestmentCost cost = PortInvestmentCatalog.CostForLevel(investmentPort, 1);
            var materials = new Dictionary<PortMarketItem, int> {
                { PortMarketItem.Wood, cost.Wood }, { PortMarketItem.Iron, cost.Iron },
                { cost.RequiredItem, cost.RequiredQuantity }
            };
            session.RestoreProfile(cost.Gold + 1000, investmentPort);
            foreach (var material in materials)
            {
                Assert.That(material.Value, Is.GreaterThan(1), "The fixture splits each required material across two deliveries.");
                SetInvestmentCargo(session.Cargo, material.Key, material.Value / 2);
            }
            yield return OpenPortStorage();
            foreach (var material in materials)
            {
                string key = PortMarketCatalog.Key(material.Key);
                yield return StoreAllVisibleCargo(controller, key);
                Assert.That(session.PortWarehouse.GetAmount(investmentPort, key), Is.EqualTo(material.Value / 2));
                Assert.That(session.GetMarketCargoAmount(key), Is.Zero);
            }
            CapturePortWarehouse(controller, "01_First_Delivery", "第一趟到港夹具将一半建设材料存入白贝港");
            ClickAcceptanceButton("Return To Port Hub"); yield return null;

            // A different port has no access to the first delivery. Deposit a
            // matching material there to prove the later debit is also scoped.
            session.RestoreProfile(session.ProfileGold, otherPort);
            session.Cargo.Wood = 7;
            yield return OpenPortStorage();
            foreach (var material in materials)
                Assert.That(session.PortWarehouse.GetAmount(otherPort, PortMarketCatalog.Key(material.Key)), Is.Zero);
            yield return StoreAllVisibleCargo(controller, "wood");
            Assert.That(session.PortWarehouse.GetAmount(otherPort, "wood"), Is.EqualTo(7));
            foreach (var material in materials)
                Assert.That(session.PortWarehouse.GetAmount(investmentPort, PortMarketCatalog.Key(material.Key)), Is.EqualTo(material.Value / 2));
            CapturePortWarehouse(controller, "02_Other_Port_Isolated", "切港夹具验证断锚库存独立，白贝已存材料不会随船出现");
            ClickAcceptanceButton("Return To Port Hub"); yield return null;

            session.RestoreProfile(session.ProfileGold, investmentPort);
            foreach (var material in materials)
                SetInvestmentCargo(session.Cargo, material.Key, material.Value - material.Value / 2);
            yield return OpenPortStorage();
            foreach (var material in materials)
            {
                string key = PortMarketCatalog.Key(material.Key);
                yield return StoreAllVisibleCargo(controller, key);
                Assert.That(session.PortWarehouse.GetAmount(investmentPort, key), Is.EqualTo(material.Value));
                Assert.That(session.GetPortInvestmentMaterialAmount(key), Is.EqualTo(material.Value));
            }
            CapturePortWarehouse(controller, "03_Second_Delivery_Ready", "第二趟到港夹具补足白贝本地建设材料");
            ClickAcceptanceButton("Return To Port Hub"); yield return null;

            // Keep real on-ship alternatives available so the test proves the
            // warehouse-priority rule, rather than succeeding with an empty hold.
            foreach (var material in materials) SetInvestmentCargo(session.Cargo, material.Key, 7);
            InvestmentResourceSnapshot shipBefore = SnapshotInvestmentResources(session);
            yield return OpenInvestmentThroughMarket();
            Assert.That(ActiveButton("Confirm Port Investment").IsInteractable(), Is.True);
            NavalBattleVisualCapture.Capture(controller, "PortWarehouse_04_Investment_Quote.png",
                "投资事务夹具 · 本港已分批积攒足额材料，同时保留随船备料", "Port Investment Panel");
            ClickAcceptanceButton("Confirm Port Investment"); yield return null;
            Assert.That(session.ProfileGold, Is.EqualTo(shipBefore.gold - cost.Gold));
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                Assert.That(session.GetMarketCargoAmount(PortMarketCatalog.Key(item)), Is.EqualTo(shipBefore.cargo[item]),
                    "Full local storage must be used before touching ship cargo: " + item);
            foreach (var material in materials)
                Assert.That(session.PortWarehouse.GetAmount(investmentPort, PortMarketCatalog.Key(material.Key)), Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount(otherPort, "wood"), Is.EqualTo(7));
            AssertInvestmentLevels(new HashSet<string> { investmentPort });
            InvestmentResourceSnapshot afterInvestment = SnapshotInvestmentResources(session);

            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            session = controller.Session;
            Assert.That(session.CurrentPort, Is.EqualTo(investmentPort));
            AssertInvestmentResourcesEqual(session, afterInvestment);
            foreach (var material in materials)
                Assert.That(session.PortWarehouse.GetAmount(investmentPort, PortMarketCatalog.Key(material.Key)), Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount(otherPort, "wood"), Is.EqualTo(7));
            AssertInvestmentLevels(new HashSet<string> { investmentPort });
            yield return OpenPortStorage();
            CapturePortWarehouse(controller, "05_Reload_Local_Investment", "重载保留本港扣料、另一港库存、随船备料和投资等级");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator PortWarehouse_WithdrawalUsesRemainingHoldWhenLessThanOneBatchFits()
        {
            yield return LoadProvisioningFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            session.Cargo.Wood = 10;
            yield return OpenPortStorage();
            yield return StoreAllVisibleCargo(controller, "wood");
            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            session.Cargo.Wood = Mathf.FloorToInt(session.CargoCapacityValue) - 2;
            yield return OpenPortStorage();
            yield return ShowPortStorageItem("wood");
            yield return OpenWarehouseCargoAdjustment("wood");
            Slider adjustment = GameObject.Find("Warehouse Cargo Adjustment Slider").GetComponent<Slider>();
            Assert.That(adjustment.maxValue, Is.EqualTo(2));
            yield return DragWarehouseSlider(adjustment, 2);
            ClickAcceptanceButton("Confirm Warehouse Cargo Adjustment"); yield return null;
            Assert.That(session.Cargo.UsedVolume, Is.EqualTo(session.CargoCapacityValue).Within(.001f));
            Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "wood"), Is.EqualTo(8));
            yield return OpenWarehouseCargoAdjustment("wood");
            Assert.That(GameObject.Find("Warehouse Cargo Adjustment Slider").GetComponent<Slider>().maxValue, Is.Zero);
            Assert.That(ActiveButton("Confirm Warehouse Cargo Adjustment").IsInteractable(), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator PortWarehouse_OverCapacityWithdrawalCannotLoseStoredOrOnboardCargo()
        {
            yield return LoadProvisioningFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            string port = session.CurrentPort;
            session.Cargo.Wood = 10;
            yield return OpenPortStorage();
            yield return StoreAllVisibleCargo(controller, "wood");
            Assert.That(session.PortWarehouse.GetAmount(port, "wood"), Is.EqualTo(10));
            yield return OpenWarehouseCargoAdjustment("wood");
            yield return DragWarehouseSlider(GameObject.Find("Warehouse Cargo Adjustment Slider").GetComponent<Slider>(), 10);
            Button.ButtonClickedEvent previouslyAffordable = ActiveButton("Confirm Warehouse Cargo Adjustment").onClick;
            ClickAcceptanceButton("Cancel Warehouse Cargo Adjustment"); yield return null;
            ClickAcceptanceButton("Return To Port Hub"); yield return null;

            // Wood occupies one volume unit. Leave less than one unit free in a
            // valid hold, so even a single unit cannot be withdrawn.
            session.Cargo.Wood = Mathf.FloorToInt(session.CargoCapacityValue);
            Assert.That(session.Cargo.UsedVolume, Is.LessThanOrEqualTo(session.CargoCapacityValue));
            yield return OpenPortStorage();
            yield return ShowPortStorageItem("wood");
            InvestmentResourceSnapshot before = SnapshotInvestmentResources(session);
            yield return OpenWarehouseCargoAdjustment("wood");
            Assert.That(GameObject.Find("Warehouse Cargo Adjustment Slider").GetComponent<Slider>().maxValue, Is.Zero);
            Button blocked = ActiveButton("Confirm Warehouse Cargo Adjustment");
            Assert.That(blocked.IsInteractable(), Is.False);
            previouslyAffordable.Invoke(); blocked.onClick.Invoke(); yield return null;
            AssertInvestmentResourcesEqual(session, before);
            Assert.That(session.PortWarehouse.GetAmount(port, "wood"), Is.EqualTo(10));
            CapturePortWarehouse(controller, "06_Withdrawal_No_Room", "满舱取出被拒绝，旧按钮与禁用按钮回调均不丢货");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator PortWarehouse_StaleStoreAndWithdrawCallbacksCannotActInAnotherPort()
        {
            const string sourcePort = "白贝港", destinationPort = "断锚港";
            yield return LoadProvisioningFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            session.RestoreProfile(session.ProfileGold, sourcePort);
            session.Cargo.Wood = 10;
            yield return OpenPortStorage();
            yield return StoreAllVisibleCargo(controller, "wood");
            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            session.Cargo.Wood = 10;
            yield return OpenPortStorage();
            yield return ShowPortStorageItem("wood");
            yield return OpenWarehouseCargoAdjustment("wood");
            yield return DragWarehouseSlider(GameObject.Find("Warehouse Cargo Adjustment Slider").GetComponent<Slider>(), -10);
            Button.ButtonClickedEvent oldStore = ActiveButton("Confirm Warehouse Cargo Adjustment").onClick;
            ClickAcceptanceButton("Cancel Warehouse Cargo Adjustment"); yield return null;
            yield return OpenWarehouseCargoAdjustment("wood");
            yield return DragWarehouseSlider(GameObject.Find("Warehouse Cargo Adjustment Slider").GetComponent<Slider>(), 10);
            Button.ButtonClickedEvent oldWithdraw = ActiveButton("Confirm Warehouse Cargo Adjustment").onClick;
            ClickAcceptanceButton("Cancel Warehouse Cargo Adjustment"); yield return null;
            ClickAcceptanceButton("Return To Port Hub"); yield return null;

            session.RestoreProfile(session.ProfileGold, destinationPort);
            yield return OpenPortStorage();
            yield return StoreAllVisibleCargo(controller, "wood");
            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            session.Cargo.Wood = 10;
            yield return OpenPortStorage();
            yield return ShowPortStorageItem("wood");
            // Both ports and the ship deliberately have identical quantities.
            // Rejecting the old callbacks must not depend on a quantity mismatch.
            Assert.That(session.PortWarehouse.GetAmount(sourcePort, "wood"), Is.EqualTo(10));
            Assert.That(session.PortWarehouse.GetAmount(destinationPort, "wood"), Is.EqualTo(10));
            InvestmentResourceSnapshot before = SnapshotInvestmentResources(session);
            oldStore.Invoke(); oldWithdraw.Invoke(); yield return null;
            AssertInvestmentResourcesEqual(session, before);
            Assert.That(session.PortWarehouse.GetAmount(sourcePort, "wood"), Is.EqualTo(10));
            Assert.That(session.PortWarehouse.GetAmount(destinationPort, "wood"), Is.EqualTo(10));
            yield return OpenWarehouseCargoAdjustment("wood");
            yield return DragWarehouseSlider(GameObject.Find("Warehouse Cargo Adjustment Slider").GetComponent<Slider>(), 10);
            ClickAcceptanceButton("Confirm Warehouse Cargo Adjustment"); yield return null;
            Assert.That(session.Cargo.Wood, Is.EqualTo(20));
            Assert.That(session.PortWarehouse.GetAmount(destinationPort, "wood"), Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount(sourcePort, "wood"), Is.EqualTo(10));
            CapturePortWarehouse(controller, "07_Stale_Port_Callbacks", "旧港存取回调无效，新港当前按钮只取本港货物");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator PortWarehouse_LegacyGlobalKeepsakeMigratesOnceToSavedCurrentPortWithoutCopies()
        {
            const string legacyPort = "白贝港", otherPort = "断锚港";
            string id = CoreWarehouseCollection.TutorialTreasureId;
            // Shared SetUp clears new scoped storage before arranging this old
            // global-only save. Original user keys are restored by TearDown.
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, legacyPort);
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseOwnedKey, id);
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseDisplayKey, ",," + id + ",,,");
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session.CurrentPort, Is.EqualTo(legacyPort));
            CollectionAssert.AreEqual(new[] { id }, controller.WarehouseCollection.OwnedIds);
            Assert.That(controller.WarehouseCollection.DisplayedAt(2), Is.EqualTo(id));
            ClickAcceptanceButton("Port Hotspot Warehouse"); yield return null;
            CapturePortWarehouse(controller, "08_Legacy_Collection_Migrated", "旧全局罗盘及第三展位仅迁入旧存档当前白贝港");
            ClickAcceptanceButton("Return To Port Hub"); yield return null;

            controller.Session.RestoreProfile(controller.Session.ProfileGold, otherPort);
            controller.Session.Cargo.Wood = 1;
            yield return OpenPortStorage();
            Assert.That(controller.WarehouseCollection.OwnedIds, Is.Empty);
            yield return StoreAllVisibleCargo(controller, "wood"); // real save in another port
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session.CurrentPort, Is.EqualTo(otherPort));
            Assert.That(controller.WarehouseCollection.OwnedIds, Is.Empty);
            Assert.That(controller.Session.PortWarehouse.GetAmount(otherPort, "wood"), Is.EqualTo(1));

            var visitedPorts = new HashSet<string>(controller.Session.CaptureCollectionProgress().visitedPorts);
            foreach (string port in CoreVoyageSession.DestinationPorts.Where(port => port != legacyPort))
            {
                int previousVisits = visitedPorts.Count;
                visitedPorts.Add(port);
                var expectedNewRewards = new List<string>();
                if (previousVisits < 3 && visitedPorts.Count >= 3) expectedNewRewards.Add("first_berth_drydock_nail");
                if (previousVisits < 6 && visitedPorts.Count >= 6) expectedNewRewards.Add("six_port_anchor_seal");
                controller.Session.RestoreProfile(controller.Session.ProfileGold, port);
                ClickAcceptanceButton("Port Hotspot Warehouse"); yield return null;
                // Visiting new ports legitimately earns voyage milestones. Only
                // these new rewards may appear; the migrated compass stays unique.
                CollectionAssert.AreEquivalent(expectedNewRewards, controller.WarehouseCollection.OwnedIds,
                    "Only newly earned port-visit rewards may appear on first visit: " + port);
                Assert.That(controller.WarehouseCollection.Owns(id), Is.False,
                    "Legacy ownership must not be copied on first visit: " + port);
                Assert.That(controller.WarehouseCollection.IsDisplayed(id), Is.False);
                ClickAcceptanceButton("Return To Port Hub"); yield return null;
            }
            controller.Session.RestoreProfile(controller.Session.ProfileGold, legacyPort);
            controller.Session.Cargo.Wood = 1;
            yield return OpenPortStorage();
            CollectionAssert.AreEqual(new[] { id }, controller.WarehouseCollection.OwnedIds);
            Assert.That(controller.WarehouseCollection.DisplayedAt(2), Is.EqualTo(id));
            yield return StoreAllVisibleCargo(controller, "wood");
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session.CurrentPort, Is.EqualTo(legacyPort));
            CollectionAssert.AreEqual(new[] { id }, controller.WarehouseCollection.OwnedIds);
            Assert.That(controller.WarehouseCollection.DisplayedAt(2), Is.EqualTo(id));
            Assert.That(controller.Session.PortWarehouse.GetAmount(legacyPort, "wood"), Is.EqualTo(1));
            Assert.That(controller.Session.PortWarehouse.GetAmount(otherPort, "wood"), Is.EqualTo(1));
            ClickAcceptanceButton("Port Hotspot Warehouse"); yield return null;
            CapturePortWarehouse(controller, "09_Legacy_Migration_Reload", "多港访问与保存重载后，罗盘仍唯一归属白贝港并保留第三展位");
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator OpenPortStorage()
        {
            ClickAcceptanceButton("Port Hotspot Warehouse"); yield return null;
            ClickAcceptanceButton("Warehouse Tab Storage"); yield return null;
            Assert.That(GameObject.Find("Warehouse Panel"), Is.Not.Null);
        }

        private static IEnumerator ShowPortStorageItem(string key)
        {
            Assert.That(ActiveButton("Warehouse Storage Row " + key), Is.Not.Null, "All nine materials must be visible without paging: " + key);
            Assert.That(GameObject.Find("Warehouse Storage Stored " + key), Is.Not.Null);
            Assert.That(GameObject.Find("Warehouse Storage Carried " + key), Is.Not.Null);
            yield break;
        }

        private static IEnumerator StoreAllVisibleCargo(CoreGameplayDemoController controller, string key)
        {
            yield return ShowPortStorageItem(key);
            CoreVoyageSession session = controller.Session;
            string port = session.CurrentPort;
            int beforeCargo = session.GetMarketCargoAmount(key);
            if (beforeCargo == 0) yield break;
            long beforeStored = session.PortWarehouse.GetAmount(port, key);
            decimal beforeGold = session.ProfileGold;
            yield return OpenWarehouseCargoAdjustment(key);
            Slider slider = GameObject.Find("Warehouse Cargo Adjustment Slider").GetComponent<Slider>();
            Assert.That(slider.minValue, Is.EqualTo(-beforeCargo));
            Assert.That(slider.value, Is.Zero);
            yield return DragWarehouseSlider(slider, slider.minValue);
            Assert.That(session.GetMarketCargoAmount(key), Is.EqualTo(beforeCargo), "Dragging only previews the delivery.");
            ClickAcceptanceButton("Confirm Warehouse Cargo Adjustment"); yield return null;
            Assert.That(session.GetMarketCargoAmount(key), Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount(port, key), Is.EqualTo(beforeStored + beforeCargo));
            Assert.That(session.ProfileGold, Is.EqualTo(beforeGold), "Storage transfers are not sales.");
        }

        private static void CapturePortWarehouse(CoreGameplayDemoController controller, string stage, string detail) =>
            NavalBattleVisualCapture.Capture(controller, "PortWarehouse_" + stage + ".png",
                "分港仓储 UI 事务夹具 · " + detail, "Warehouse Panel");
    }
}
