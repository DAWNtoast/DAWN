using System;
using System.Collections;
using System.Globalization;
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
        // Inventory and port arrangements are isolated fixtures, not earned-play claims.
        // All tested transfers use visible buttons and real pointer slider drags.
        // The shared SetUp/TearDown already preserves both TreasureProgressKey and
        // PortWarehouseCargoKey, along with the account and ordinary cargo keys.
        private static readonly string[] RareWarehouseTestKeys =
            { "gem", "ancientmaterial", "relic", "blackpearl", "ancientcompass", "lantern" };
        private static readonly double[] RareWarehouseTestVolumes = { 5d, 1.5d, 5d, 4d, 3d, 1d };

        [UnityTest, Timeout(120000)]
        public IEnumerator RareWarehouse_TabsSixRowsCancelEndpointsAndReloadPreserveCargo()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var session = controller.Session;
            SetRareWarehouseFixture(session, 3, 4, 1, 2, 1, 3);
            session.Cargo.Wood = 5;
            string port = session.CurrentPort;
            int[] initial = RareWarehouseCargo(session);
            float used = session.Cargo.UsedVolume;
            decimal gold = session.ProfileGold;
            yield return OpenPortStorage();
            Assert.That(ActiveRareWarehouseRows("Warehouse Storage Row "), Is.EqualTo(9));
            Assert.That(ActiveRareWarehouseRows("Warehouse Rare Row "), Is.Zero);
            ClickAcceptanceButton("Warehouse Tab Rare"); yield return null;
            Assert.That(ActiveRareWarehouseRows("Warehouse Rare Row "), Is.EqualTo(6));
            Assert.That(ActiveRareWarehouseRows("Warehouse Storage Row "), Is.Zero);
            for (int i = 0; i < RareWarehouseTestKeys.Length; i++)
            {
                string key = RareWarehouseTestKeys[i];
                Assert.That(ActiveButton("Warehouse Rare Row " + key), Is.Not.Null);
                Assert.That(GameObject.Find("Warehouse Rare Stored " + key).GetComponent<Text>().text, Does.Contain("0"));
                Assert.That(GameObject.Find("Warehouse Rare Carried " + key).GetComponent<Text>().text,
                    Does.Contain(initial[i].ToString(CultureInfo.InvariantCulture)));
                Assert.That(GameObject.Find("Warehouse Rare Volume " + key).GetComponent<Text>().text,
                    Does.Contain((initial[i] * RareWarehouseTestVolumes[i]).ToString("0.00", CultureInfo.InvariantCulture)));
            }
            foreach (string excluded in new[] { "curse", "gold", "maps", "fragments" })
                Assert.That(GameObject.Find("Warehouse Rare Row " + excluded), Is.Null);
            CaptureRareWarehouse(controller, "01_Six_Rare_Rows", "珍稀六行与基础九行分别显示，体积为真实随船占用");
            ClickAcceptanceButton("Warehouse Tab Storage"); yield return null;
            Assert.That(ActiveRareWarehouseRows("Warehouse Storage Row "), Is.EqualTo(9));
            ClickAcceptanceButton("Warehouse Tab Rare"); yield return null;

            yield return OpenRareWarehouseAdjustment("gem");
            Assert.That(GameObject.Find("Warehouse Item Actions"), Is.Null, "Selecting transfer closes the action picker.");
            Assert.That(GameObject.Find("Warehouse Sell Cargo"), Is.Null);
            Slider slider = RareWarehouseSlider();
            Assert.That(slider.wholeNumbers, Is.True);
            Assert.That(slider.value, Is.Zero);
            Assert.That(slider.minValue, Is.EqualTo(-3));
            Assert.That(slider.maxValue, Is.Zero);
            Assert.That(ActiveButton("Confirm Warehouse Cargo Adjustment").IsInteractable(), Is.False);
            yield return DragWarehouseSlider(slider, slider.minValue);
            CollectionAssert.AreEqual(initial, RareWarehouseCargo(session), "Dragging only previews the transfer.");
            Assert.That(session.PortWarehouse.GetAmount(port, "gem"), Is.Zero);
            Button.ButtonClickedEvent cancelled = ActiveButton("Confirm Warehouse Cargo Adjustment").onClick;
            CaptureRareWarehouse(controller, "02_Adjustment_Cancel_Preview", "最左端预览全部卸下，取消前库存与体积不变");
            ClickAcceptanceButton("Cancel Warehouse Cargo Adjustment"); yield return null;
            cancelled.Invoke(); yield return null;
            CollectionAssert.AreEqual(initial, RareWarehouseCargo(session));
            Assert.That(session.Cargo.UsedVolume, Is.EqualTo(used).Within(.001f));

            yield return OpenRareWarehouseAdjustment("gem");
            yield return DragWarehouseSlider(RareWarehouseSlider(), -3);
            Button.ButtonClickedEvent depositAgain = ActiveButton("Confirm Warehouse Cargo Adjustment").onClick;
            ClickAcceptanceButton("Confirm Warehouse Cargo Adjustment"); yield return null;
            depositAgain.Invoke(); yield return null;
            Assert.That(session.Cargo.Gem, Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount(port, "gem"), Is.EqualTo(3));
            Assert.That(session.Cargo.UsedVolume, Is.EqualTo(used - 15f).Within(.001f));
            AssertRareWarehouseSavedSplit(session);
            CaptureRareWarehouse(controller, "03_Gems_Stored", "确认一次释放十五舱位，旧确认不会再次转移");

            yield return OpenRareWarehouseAdjustment("gem");
            Assert.That(RareWarehouseSlider().maxValue, Is.EqualTo(3));
            yield return DragWarehouseSlider(RareWarehouseSlider(), 3);
            Button.ButtonClickedEvent withdrawAgain = ActiveButton("Confirm Warehouse Cargo Adjustment").onClick;
            ClickAcceptanceButton("Confirm Warehouse Cargo Adjustment"); yield return null;
            withdrawAgain.Invoke(); yield return null;
            CollectionAssert.AreEqual(initial, RareWarehouseCargo(session));
            Assert.That(session.PortWarehouse.GetAmount(port, "gem"), Is.Zero);
            Assert.That(session.Cargo.UsedVolume, Is.EqualTo(used).Within(.001f));
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            string warehouse = session.PortWarehouse.Serialize();
            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CollectionAssert.AreEqual(initial, RareWarehouseCargo(controller.Session));
            Assert.That(controller.Session.PortWarehouse.Serialize(), Is.EqualTo(warehouse));
            Assert.That(controller.Session.Cargo.UsedVolume, Is.EqualTo(used).Within(.001f));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            yield return OpenRareWarehouse();
            CaptureRareWarehouse(controller, "04_Transfer_Reload", "返回港口并重载后，随船与本港库存总量守恒");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator RareWarehouse_WithdrawalFitsRealVolumeAndRejectsChangedHoldQuote()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var session = controller.Session;
            SetRareWarehouseFixture(session, 0, 0, 0, 0, 0, 0);
            string port = session.CurrentPort;
            Assert.That(session.PortWarehouse.Restore("v2;" + port + ",gem,5"), Is.True);
            session.Cargo.Wood = Mathf.FloorToInt(session.CargoCapacityValue) - 7;
            yield return OpenRareWarehouse();
            yield return OpenRareWarehouseAdjustment("gem");
            Assert.That(RareWarehouseSlider().maxValue, Is.EqualTo(1), "Seven free volume can hold only one five-volume gem.");
            yield return DragWarehouseSlider(RareWarehouseSlider(), 1);
            Button.ButtonClickedEvent stale = ActiveButton("Confirm Warehouse Cargo Adjustment").onClick;
            // The gem balances are unchanged; another material fills the quoted free space.
            session.Cargo.Wood += 3;
            float fullBefore = session.Cargo.UsedVolume;
            stale.Invoke(); yield return null;
            Assert.That(session.Cargo.Gem, Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount(port, "gem"), Is.EqualTo(5));
            Assert.That(session.Cargo.UsedVolume, Is.EqualTo(fullBefore).Within(.001f));
            // Reload only the fixture scene, then establish a fresh quote with exactly seven free units.
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>(); session = controller.Session;
            SetRareWarehouseFixture(session, 0, 0, 0, 0, 0, 0);
            foreach (PortMarketItem item in PortMarketCatalog.AllItems) SetInvestmentCargo(session.Cargo, item, 0);
            Assert.That(session.PortWarehouse.Restore("v2;" + port + ",gem,5"), Is.True);
            session.Cargo.Wood = Mathf.FloorToInt(session.CargoCapacityValue) - 7;
            yield return OpenRareWarehouse();
            yield return OpenRareWarehouseAdjustment("gem");
            yield return DragWarehouseSlider(RareWarehouseSlider(), RareWarehouseSlider().maxValue);
            CaptureRareWarehouse(controller, "05_Withdrawal_Truncated", "正极按真实剩余舱位截断为一颗宝石，不按仓储五颗强行装船");
            ClickAcceptanceButton("Confirm Warehouse Cargo Adjustment"); yield return null;
            Assert.That(session.Cargo.Gem, Is.EqualTo(1));
            Assert.That(session.PortWarehouse.GetAmount(port, "gem"), Is.EqualTo(4));
            Assert.That(session.Cargo.UsedVolume, Is.EqualTo(session.CargoCapacityValue - 2).Within(.001f));
            yield return OpenRareWarehouseAdjustment("gem");
            Assert.That(RareWarehouseSlider().maxValue, Is.Zero);
            Assert.That(ActiveButton("Confirm Warehouse Cargo Adjustment").IsInteractable(), Is.False);
            ActiveButton("Confirm Warehouse Cargo Adjustment").onClick.Invoke(); yield return null;
            Assert.That(session.Cargo.Gem, Is.EqualTo(1));
            Assert.That(session.PortWarehouse.GetAmount(port, "gem"), Is.EqualTo(4));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator RareWarehouse_IdenticalPortStocksAndDepartureRejectOldCallbacks()
        {
            const string firstPort = "断锚港", otherPort = "白贝港";
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var session = controller.Session;
            SetRareWarehouseFixture(session, 2, 0, 0, 0, 0, 0);
            session.RestoreProfile(session.ProfileGold, firstPort);
            Assert.That(session.PortWarehouse.Restore("v2;断锚港,gem,2;白贝港,gem,2"), Is.True);
            yield return OpenRareWarehouse();
            yield return OpenRareWarehouseAdjustment("gem");
            yield return DragWarehouseSlider(RareWarehouseSlider(), 2);
            Button.ButtonClickedEvent oldPort = ActiveButton("Confirm Warehouse Cargo Adjustment").onClick;
            session.RestoreProfile(session.ProfileGold, otherPort);
            string inventoryBefore = session.PortWarehouse.Serialize();
            oldPort.Invoke(); yield return null;
            Assert.That(session.Cargo.Gem, Is.EqualTo(2));
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(inventoryBefore), "Identical inventories must not make a quote portable across ports.");

            // A fresh scene/port fixture avoids using a stale modal to navigate.
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>(); session = controller.Session;
            SetRareWarehouseFixture(session, 2, 0, 0, 0, 0, 0);
            foreach (PortMarketItem item in PortMarketCatalog.AllItems) SetInvestmentCargo(session.Cargo, item, 0);
            session.RestoreProfile(session.ProfileGold, otherPort);
            Assert.That(session.PortWarehouse.Restore("v2;断锚港,gem,2;白贝港,gem,2"), Is.True);
            yield return OpenRareWarehouse();
            yield return OpenRareWarehouseAdjustment("gem");
            yield return DragWarehouseSlider(RareWarehouseSlider(), 2);
            Button.ButtonClickedEvent repeat = ActiveButton("Confirm Warehouse Cargo Adjustment").onClick;
            ClickAcceptanceButton("Confirm Warehouse Cargo Adjustment"); yield return null;
            repeat.Invoke(); oldPort.Invoke(); yield return null;
            Assert.That(session.Cargo.Gem, Is.EqualTo(4));
            Assert.That(session.PortWarehouse.GetAmount(otherPort, "gem"), Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount(firstPort, "gem"), Is.EqualTo(2));
            CaptureRareWarehouse(controller, "06_Port_Isolation", "新港当前确认只取本港宝石，旧港回调和重复确认均无效");
            yield return OpenRareWarehouseAdjustment("gem");
            yield return DragWarehouseSlider(RareWarehouseSlider(), -4);
            Button.ButtonClickedEvent cancelledBeforeSailing = ActiveButton("Confirm Warehouse Cargo Adjustment").onClick;
            ClickAcceptanceButton("Cancel Warehouse Cargo Adjustment"); yield return null;
            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            // Supply arrangement belongs to the fixture; departure itself uses the visible UI.
            session.Cargo.Water = session.Cargo.Food = 120;
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            Assert.That(ActiveButton("Start Voyage").IsInteractable(), Is.True);
            ClickAcceptanceButton("Start Voyage"); yield return null;
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            int[] atSea = RareWarehouseCargo(session);
            string storedAtSea = session.PortWarehouse.Serialize();
            cancelledBeforeSailing.Invoke(); repeat.Invoke(); yield return null;
            CollectionAssert.AreEqual(atSea, RareWarehouseCargo(session));
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(storedAtSea));
            Assert.That(session.PortWarehouse.GetAmount(firstPort, "gem"), Is.EqualTo(2));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator RareWarehouse_StoreAllReleasesSpaceWithoutMovingCurseMoneyOrTreasureProgress()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var session = controller.Session;
            SetRareWarehouseFixture(session, 3, 4, 1, 2, 1, 3);
            session.Cargo.Wood = 9; session.Cargo.Water = session.Cargo.Food = 20;
            session.Cargo.Gold = 73; session.Cargo.Fame = 11;
            CoreTreasureProgress progress = session.CaptureTreasureProgress();
            progress.curses = 7; progress.fragments = 5;
            progress.maps = new[] { new CoreTreasureMapSave { id = "rare-warehouse-fixture", seed = "RARE-WAREHOUSE-FIXTURE", rankId = "weathered" } };
            progress.selectedMapId = progress.maps[0].id;
            session.RestoreTreasureProgress(progress);
            string port = session.CurrentPort;
            string otherPort = port == "白贝港" ? "断锚港" : "白贝港";
            Assert.That(session.PortWarehouse.Restore("v2;" + otherPort + ",gem,8;" + port + ",gem,2"), Is.True);
            int[] original = RareWarehouseCargo(session);
            double released = original.Select((amount, i) => amount * RareWarehouseTestVolumes[i]).Sum();
            float beforeUsed = session.Cargo.UsedVolume;
            InvestmentResourceSnapshot basics = SnapshotInvestmentResources(session);
            decimal gold = session.ProfileGold;
            yield return OpenRareWarehouse();
            Button.ButtonClickedEvent oldAll = ActiveButton("Warehouse Store All Rare").onClick;
            Assert.That(ActiveButton("Warehouse Store All Rare").IsInteractable(), Is.True);
            CaptureRareWarehouse(controller, "07_Before_Store_All", "六类珍稀可一键卸货，诅咒和金币不是仓储货物");
            ClickAcceptanceButton("Warehouse Store All Rare"); yield return null;
            oldAll.Invoke(); yield return null;
            CollectionAssert.AreEqual(new int[6], RareWarehouseCargo(session));
            for (int i = 0; i < RareWarehouseTestKeys.Length; i++)
                Assert.That(session.PortWarehouse.GetAmount(port, RareWarehouseTestKeys[i]), Is.EqualTo(original[i] + (i == 0 ? 2 : 0)));
            Assert.That(session.PortWarehouse.GetAmount(otherPort, "gem"), Is.EqualTo(8));
            Assert.That(session.Cargo.UsedVolume, Is.EqualTo(beforeUsed - released).Within(.001d));
            AssertInvestmentResourcesEqual(session, basics);
            AssertRareWarehouseExcluded(session, progress, gold, 73, 11);
            Assert.That(ActiveButton("Warehouse Store All Rare").IsInteractable(), Is.False);
            AssertRareWarehouseSavedSplit(session);
            CaptureRareWarehouse(controller, "08_After_Store_All", "只卸下六种珍稀，空出真实舱位，地图碎片与诅咒不变");
            string stored = session.PortWarehouse.Serialize();
            for (int reload = 0; reload < 2; reload++)
            {
                ClickAcceptanceButton("Return To Port Hub"); yield return null;
                SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
                controller = Object.FindFirstObjectByType<CoreGameplayDemoController>(); session = controller.Session;
                oldAll.Invoke(); yield return null;
                CollectionAssert.AreEqual(new int[6], RareWarehouseCargo(session));
                Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(stored));
                Assert.That(session.Cargo.UsedVolume, Is.EqualTo(beforeUsed - released).Within(.001d));
                AssertInvestmentResourcesEqual(session, basics);
                AssertRareWarehouseExcluded(session, progress, gold, 73, 11);
                AssertRareWarehouseSavedSplit(session);
                yield return OpenRareWarehouse();
            }
            CaptureRareWarehouse(controller, "09_Reload_No_Duplicate", "连续两次读档后宝物仍在本港仓库，TreasureProgress 没有重新复制到船上");
            LogAssert.NoUnexpectedReceived();
        }

        private static void SetRareWarehouseFixture(CoreVoyageSession session, int gems, int ancient, int relics,
            int pearls, int compasses, int lanterns)
        {
            session.Cargo.Gem = gems; session.Cargo.AncientMaterial = ancient; session.Cargo.Relic = relics;
            session.Cargo.BlackPearl = pearls; session.Cargo.AncientCompass = compasses; session.Cargo.Lantern = lanterns;
        }

        private static int[] RareWarehouseCargo(CoreVoyageSession session) => new[] {
            session.Cargo.Gem, session.Cargo.AncientMaterial, session.Cargo.Relic,
            session.Cargo.BlackPearl, session.Cargo.AncientCompass, session.Cargo.Lantern };

        private static int ActiveRareWarehouseRows(string prefix) =>
            Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Count(button => button.name.StartsWith(prefix, StringComparison.Ordinal));

        private static IEnumerator OpenRareWarehouse()
        {
            ClickAcceptanceButton("Port Hotspot Warehouse"); yield return null;
            ClickAcceptanceButton("Warehouse Tab Rare"); yield return null;
            Assert.That(ActiveRareWarehouseRows("Warehouse Rare Row "), Is.EqualTo(6));
        }

        private static IEnumerator OpenRareWarehouseAdjustment(string key)
        {
            ClickAcceptanceButton("Warehouse Rare Row " + key); yield return null;
            Assert.That(GameObject.Find("Warehouse Item Actions"), Is.Not.Null);
            Assert.That(ActiveButton("Warehouse Sell Cargo").IsInteractable(), Is.True);
            ClickAcceptanceButton("Warehouse Adjust Cargo"); yield return null;
            Assert.That(GameObject.Find("Warehouse Cargo Adjustment"), Is.Not.Null);
            Assert.That(RareWarehouseSlider().value, Is.Zero);
        }

        private static Slider RareWarehouseSlider() => GameObject.Find("Warehouse Cargo Adjustment Slider").GetComponent<Slider>();

        private static void AssertRareWarehouseSavedSplit(CoreVoyageSession session)
        {
            CoreTreasureProgress saved = JsonUtility.FromJson<CoreTreasureProgress>(PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey));
            Assert.That(saved, Is.Not.Null);
            CollectionAssert.AreEqual(RareWarehouseCargo(session), new[] {
                saved.gems, saved.ancientMaterials, saved.relics, saved.blackPearls, saved.ancientCompasses, saved.lanterns },
                "The treasure save records only current carried goods, never the banked copy.");
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.PortWarehouseCargoKey), Is.EqualTo(session.PortWarehouse.Serialize()));
        }

        private static void AssertRareWarehouseExcluded(CoreVoyageSession session, CoreTreasureProgress expected,
            decimal gold, int cargoGold, int fame)
        {
            CoreTreasureProgress current = session.CaptureTreasureProgress();
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(session.Cargo.Gold, Is.EqualTo(cargoGold));
            Assert.That(session.Cargo.Fame, Is.EqualTo(fame));
            Assert.That(current.curses, Is.EqualTo(expected.curses));
            Assert.That(current.fragments, Is.EqualTo(expected.fragments));
            Assert.That(current.selectedMapId, Is.EqualTo(expected.selectedMapId));
            CollectionAssert.AreEqual(expected.maps.Select(map => map.id + "/" + map.seed + "/" + map.rankId),
                current.maps.Select(map => map.id + "/" + map.seed + "/" + map.rankId));
        }

        private static void CaptureRareWarehouse(CoreGameplayDemoController controller, string stage, string detail) =>
            NavalBattleVisualCapture.Capture(controller, "RareWarehouse_" + stage + ".png", "珍稀仓储 UI 夹具 · " + detail,
                GameObject.Find("Warehouse Cargo Adjustment") != null ? "Warehouse Cargo Adjustment" : "Warehouse Panel");
    }
}
