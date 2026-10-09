using System;
using System.Collections;
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
        // Explicit inventory/port fixtures exercise the real pointer UI and save
        // path. They are transaction tests, not claims of earned voyage rewards.
        [UnityTest, Timeout(120000)]
        public IEnumerator RareWarehouseSale_AllSixMixedSalesAreFixedPriceAtomicAndSurviveTwoReloads()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var session = controller.Session;
            session.RestoreProfile(2000.1234m, "断锚港");
            SetRareWarehouseFixture(session, 2, 2, 2, 2, 2, 2);
            session.PortWarehouse.Restore("v2;" + string.Join(";", RareWarehouseTestKeys.Select(key =>
                "断锚港," + key + ",2;白贝港," + key + ",7")));
            decimal expectedMoney = session.ProfileGold;
            float initialVolume = session.Cargo.UsedVolume;
            yield return OpenRareWarehouse();
            Assert.That(GameObject.Find("Warehouse Rare Hint").GetComponent<Text>().text,
                Does.Contain("高阶捐献").And.Contain("海上事件").And.Contain("最多4盏"));
            foreach (string key in RareWarehouseTestKeys)
            {
                ClickAcceptanceButton("Warehouse Rare Row " + key); yield return null;
                Assert.That(GameObject.Find("Warehouse Item Actions"), Is.Not.Null);
                Assert.That(ActiveButton("Warehouse Adjust Cargo").IsInteractable(), Is.True);
                if (key == "gem") CaptureRareSale(controller, "01_Actions", "珍稀物品提供存取和出售两个入口");
                ClickAcceptanceButton("Warehouse Sell Cargo"); yield return null;
                Assert.That(GameObject.Find("Warehouse Cargo Sale Title").GetComponent<Text>().text,
                    Does.Contain(CoreWarehouseItemCatalog.Find(key).Name));
                Text price = GameObject.Find("Warehouse Cargo Sale Date").GetComponent<Text>();
                Assert.That(price.text, Does.Contain("各港固定回收价 " + CoreRareItemEconomy.SaleUnitPrice(key).ToString("0.####") + " 金币"));
                Assert.That(ActiveButton("Confirm Warehouse Cargo Sale").IsInteractable(), Is.False);
                yield return DragWarehouseSlider(RareSaleSlider("Warehouse Stored Sale Slider"), 1);
                yield return DragWarehouseSlider(RareSaleSlider("Warehouse Ship Sale Slider"), 1);
                Assert.That(session.GetWarehouseCargoAmount(key), Is.EqualTo(2));
                Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, key), Is.EqualTo(2));
                Assert.That(session.ProfileGold, Is.EqualTo(expectedMoney), "Dragging previews both sources without payment.");
                if (key == "gem")
                {
                    CaptureRareSale(controller, "02_Mixed_Quote", "本港与随船各出售一件宝石，合计一百二十金币");
                    Button.ButtonClickedEvent cancelled = ActiveButton("Confirm Warehouse Cargo Sale").onClick;
                    ClickAcceptanceButton("Cancel Warehouse Cargo Sale"); yield return null;
                    cancelled.Invoke(); yield return null;
                    Assert.That(session.Cargo.Gem, Is.EqualTo(2));
                    Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, key), Is.EqualTo(2));
                    Assert.That(session.ProfileGold, Is.EqualTo(expectedMoney));
                    yield return OpenRareSale(key);
                    yield return DragWarehouseSlider(RareSaleSlider("Warehouse Stored Sale Slider"), 1);
                    yield return DragWarehouseSlider(RareSaleSlider("Warehouse Ship Sale Slider"), 1);
                }
                Button.ButtonClickedEvent repeated = ActiveButton("Confirm Warehouse Cargo Sale").onClick;
                ClickAcceptanceButton("Confirm Warehouse Cargo Sale"); yield return null;
                repeated.Invoke(); yield return null;
                expectedMoney += 2m * CoreRareItemEconomy.SaleUnitPrice(key);
                Assert.That(session.ProfileGold, Is.EqualTo(expectedMoney));
                Assert.That(session.GetWarehouseCargoAmount(key), Is.EqualTo(1));
                Assert.That(session.PortWarehouse.GetAmount("断锚港", key), Is.EqualTo(1));
                Assert.That(session.PortWarehouse.GetAmount("白贝港", key), Is.EqualTo(7));
                AssertRareWarehouseSavedSplit(session);
            }
            Assert.That(session.Cargo.UsedVolume, Is.EqualTo(initialVolume - 19.5f).Within(.001f));
            CaptureRareSale(controller, "03_Six_Sales_Saved", "六类各出售两件，所有款项立即入账，外港库存不变");
            string warehouse = session.PortWarehouse.Serialize();
            for (int reload = 0; reload < 2; reload++)
            {
                SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
                controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                session = controller.Session;
                Assert.That(session.ProfileGold, Is.EqualTo(expectedMoney));
                Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouse));
                CollectionAssert.AreEqual(new[] { 1, 1, 1, 1, 1, 1 }, RareWarehouseCargo(session));
                AssertRareWarehouseSavedSplit(session);
            }
            yield return OpenRareWarehouse();
            CaptureRareSale(controller, "04_Reload", "两次重载后出售货物不会从藏宝进度存档复生");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator RareWarehouseSale_PortMonthCargoAndAccountChangesRejectOldQuotes()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var session = controller.Session;
            session.RestoreProfile(2000.1234m, "断锚港");
            SetRareWarehouseFixture(session, 2, 0, 0, 0, 0, 0);
            session.PortWarehouse.Restore("v2;断锚港,gem,3;白贝港,gem,3");
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            SetProvisionClock(controller, () => now);
            yield return OpenRareWarehouse();
            foreach (string changed in new[] { "port", "date", "cargo", "account" })
            {
                yield return OpenRareSale("gem");
                yield return DragWarehouseSlider(RareSaleSlider("Warehouse Stored Sale Slider"), 1);
                yield return DragWarehouseSlider(RareSaleSlider("Warehouse Ship Sale Slider"), 1);
                Button.ButtonClickedEvent stale = ActiveButton("Confirm Warehouse Cargo Sale").onClick;
                if (changed == "port") session.RestoreProfile(session.ProfileGold, "白贝港");
                else if (changed == "date") now = now.AddMonths(1);
                else if (changed == "cargo") session.Cargo.Gem++;
                else session.RestoreProfile(session.ProfileGold + .0001m, session.CurrentPort);
                decimal money = session.ProfileGold;
                int carried = session.Cargo.Gem;
                string stored = session.PortWarehouse.Serialize();
                stale.Invoke(); yield return null;
                Assert.That(session.ProfileGold, Is.EqualTo(money), changed);
                Assert.That(session.Cargo.Gem, Is.EqualTo(carried), changed);
                Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(stored), changed);
                if (changed == "port")
                {
                    session.RestoreProfile(money, "断锚港");
                    ClickAcceptanceButton("Cancel Warehouse Cargo Sale"); yield return null;
                }
                else Assert.That(GameObject.Find("Warehouse Cargo Sale"), Is.Null, "An obsolete quote closes for a fresh selection.");
                stale.Invoke(); yield return null;
                Assert.That(session.ProfileGold, Is.EqualTo(money));
            }
            CaptureRareSale(controller, "05_Expired_Quote", "跨港、跨月、货物或余额变化均拒绝旧确认");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator RareWarehouseSale_AccountLimitPreventsLossBeforeConfirmation()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var session = controller.Session;
            session.RestoreProfile(CoreEconomyBalance.MaximumProfileGold - 59.9999m, "断锚港");
            SetRareWarehouseFixture(session, 1, 0, 0, 0, 0, 0);
            session.PortWarehouse.Restore("v2;断锚港,gem,1");
            decimal money = session.ProfileGold;
            yield return OpenRareWarehouse();
            yield return OpenRareSale("gem");
            yield return DragWarehouseSlider(RareSaleSlider("Warehouse Ship Sale Slider"), 1);
            Assert.That(ActiveButton("Confirm Warehouse Cargo Sale").IsInteractable(), Is.False);
            Assert.That(GameObject.Find("Warehouse Cargo Sale Status").GetComponent<Text>().text, Does.Contain("账户上限"));
            CaptureRareSale(controller, "06_Account_Limit", "账户无法容纳完整回收款时禁用确认，货物不扣除");
            ClickAcceptanceButton("Cancel Warehouse Cargo Sale"); yield return null;
            Assert.That(session.ProfileGold, Is.EqualTo(money));
            Assert.That(session.Cargo.Gem, Is.EqualTo(1));
            Assert.That(session.PortWarehouse.GetAmount("断锚港", "gem"), Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        private static Slider RareSaleSlider(string name) => GameObject.Find(name).GetComponent<Slider>();
        private static IEnumerator OpenRareSale(string key)
        {
            ClickAcceptanceButton("Warehouse Rare Row " + key); yield return null;
            ClickAcceptanceButton("Warehouse Sell Cargo"); yield return null;
            Assert.That(GameObject.Find("Warehouse Cargo Sale"), Is.Not.Null);
        }
        private static void CaptureRareSale(CoreGameplayDemoController controller, string stage, string detail)
        {
            string root = GameObject.Find("Warehouse Cargo Sale") != null ? "Warehouse Cargo Sale" :
                GameObject.Find("Warehouse Item Actions") != null ? "Warehouse Item Actions" : "Warehouse Panel";
            NavalBattleVisualCapture.Capture(controller, "RareWarehouseSale_" + stage + ".png", "珍稀出售 UI 夹具 · " + detail, root);
        }
    }
}
