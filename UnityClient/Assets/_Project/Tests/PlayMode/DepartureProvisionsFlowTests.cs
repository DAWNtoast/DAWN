using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
        [UnityTest]
        public IEnumerator DepartureProvisions_BuyAtMarketPricesShareQuotaAndPersistWithoutDeparting()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            var ledger = PreserveProvisionLedger(now);
            try
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                SetProvisionClock(controller, () => now);
                ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
                int quantity = ExpectedProvisionQuantity(controller, 26);
                decimal cost = ProvisionCost(controller, now, quantity, quantity);
                decimal gold = controller.Session.ProfileGold;
                string seed = CoreVoyageSession.CreateChartSeed(0, controller.Session.CurrentPort, "白贝港");
                yield return EnterDepartureChartSeed(seed);
                Assert.That(ActiveButton("Departure Buy Provisions").interactable, Is.True);
                yield return SetRecommendedDepartureWaterFood();
                NavalBattleVisualCapture.Capture(controller, "01_Departure_Provision_Quote.png", "基础物资面板按航期与两天余量报价", "Departure Basic Supplies");
                Button.ButtonClickedEvent purchasedCallback = ActiveButton("Departure Basic Supplies Purchase").onClick;
                ClickAcceptanceButton("Departure Basic Supplies Purchase"); yield return null;
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold - cost));
                Assert.That(controller.Session.Cargo.Water, Is.EqualTo(quantity));
                Assert.That(controller.Session.Cargo.Food, Is.EqualTo(quantity));
                Assert.That(controller.Session.SelectedSeed, Is.EqualTo(seed));
                Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey("断锚港", now, PortMarketItem.Water)), Is.EqualTo(quantity));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey("断锚港", now, PortMarketItem.Food)), Is.EqualTo(quantity));
                Assert.That(ActiveButton("Departure Basic Supplies Purchase").interactable, Is.False);
                purchasedCallback.Invoke(); yield return null;
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold - cost), "A repeated callback must not buy more supplies.");
                ClickAcceptanceButton("Departure Basic Supplies Close"); yield return null;
                Assert.That(ActiveButton("Departure Buy Provisions").interactable, Is.True,
                    "Players can always reopen the purchase panel to buy another basic item.");
                NavalBattleVisualCapture.Capture(controller, "02_Departure_Provisions_Ready.png", "购买成功并关闭后保留出航配置", "Voyage Setup");
                ClickAcceptanceButton("Adjust Voyage Supplies"); yield return null;
                yield return OpenMarketItem("water");
                Assert.That(MarketInfoLabel("Market Quota water").text, Does.Contain("已购 " + quantity));
                NavalBattleVisualCapture.Capture(controller, "03_Daily_Market_Shared_Purchases.png", "出航采购与市场共享当月余量", "Market Panel");
                SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
                controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(controller.Session.Cargo.Water, Is.EqualTo(quantity));
                Assert.That(controller.Session.Cargo.Food, Is.EqualTo(quantity));
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold - cost));
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }

        [UnityTest]
        public IEnumerator DepartureProvisions_BlocksShortMoneyFullHoldOrExhaustedMonthlyQuotaWithoutPartialPurchase()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            var ledger = PreserveProvisionLedger(now);
            try
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                SetProvisionClock(controller, () => now);
                ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
                foreach (string problem in new[] { "金币不足", "货舱空间不足", "本月水粮库存或额度不足" })
                {
                    controller.Session.RestoreProfile(problem == "金币不足" ? 0 : 2000, "断锚港");
                    controller.Session.Cargo.Wood = problem == "货舱空间不足" ? 60 : 0;
                    PlayerPrefs.SetInt(CoreGameplayDemoController.MarketPurchaseKey("断锚港", now, PortMarketItem.Food),
                        problem == "本月水粮库存或额度不足" ? PortMarketCatalog.MaxPurchaseQuota(PortMarketItem.Food, 0) : 0);
                    ClickAcceptanceButton("Route 0"); yield return null;
                    decimal gold = controller.Session.ProfileGold;
                    Assert.That(ActiveButton("Departure Buy Provisions").interactable, Is.True, problem);
                    yield return SetExplicitDepartureWaterFood(controller, 26);
                    Assert.That(ActiveButton("Departure Basic Supplies Purchase").interactable, Is.False, problem);
                    Assert.That(GameObject.Find("Departure Provision Quote"), Is.Null,
                        "Provisioning remains available without the removed detailed summary.");
                    ActiveButton("Departure Basic Supplies Purchase").onClick.Invoke(); yield return null;
                    Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                    Assert.That(controller.Session.Cargo.Water, Is.Zero);
                    Assert.That(controller.Session.Cargo.Food, Is.Zero);
                    ClickAcceptanceButton("Departure Basic Supplies Close"); yield return null;
                }
                NavalBattleVisualCapture.Capture(controller, "04_Departure_Daily_Quota_Exhausted.png", "额度不足明确提示且不部分扣款", "Voyage Setup");
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }

        [UnityTest]
        public IEnumerator DepartureProvisions_NewMonthRefreshesExhaustedStockAndRequotesBeforeCharging()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddMonths(1).AddMinutes(-1);
            DateTime firstDay = now;
            var ledger = PreserveProvisionLedger(now);
            try
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                SetProvisionClock(controller, () => now);
                foreach (PortMarketItem item in new[] { PortMarketItem.Water, PortMarketItem.Food })
                    PlayerPrefs.SetInt(CoreGameplayDemoController.MarketPurchaseKey("断锚港", now, item), PortMarketCatalog.MaxPurchaseQuota(item, 0));
                ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
                yield return SetExplicitDepartureWaterFood(controller, 26);
                Assert.That(ActiveButton("Departure Basic Supplies Purchase").interactable, Is.False);
                Button oldQuote = ActiveButton("Departure Basic Supplies Purchase");
                decimal gold = controller.Session.ProfileGold;
                now = new DateTime(now.Year, now.Month, 1).AddMonths(1).AddSeconds(1);
                oldQuote.onClick.Invoke();
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold), "Crossing a game month must reprice before charging.");
                yield return null;
                Assert.That(ActiveButton("Departure Basic Supplies Purchase").interactable, Is.True);
                int quantity = ExpectedProvisionQuantity(controller, 26);
                ClickAcceptanceButton("Departure Basic Supplies Purchase"); yield return null;
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold - ProvisionCost(controller, now, quantity, quantity)));
                foreach (PortMarketItem item in new[] { PortMarketItem.Water, PortMarketItem.Food })
                {
                    Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey("断锚港", firstDay, item)), Is.EqualTo(PortMarketCatalog.MaxPurchaseQuota(item, 0)));
                    Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey("断锚港", now, item)), Is.EqualTo(quantity));
                }
                ClickAcceptanceButton("Departure Basic Supplies Close"); yield return null;
                ClickAcceptanceButton("Adjust Voyage Supplies"); yield return null;
                Assert.That(typeof(CoreGameplayDemoController).GetField("marketDisplayedDate", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(controller), Is.EqualTo(new DateTime(now.Year, now.Month, 1)),
                    "Opening the market after restocking must quote the new game month.");
                AssertMarketIconCatalog(controller);
                NavalBattleVisualCapture.Capture(controller, "05_Market_After_Midnight.png", "次月库存恢复并独立记账", "Market Panel");
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }

        [UnityTest]
        public IEnumerator DepartureProvisions_TreasureUsesItsOwnVoyageLengthAndOnlyBuysMissingFood()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            var ledger = PreserveProvisionLedger(now);
            try
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                SetProvisionClock(controller, () => now);
                controller.Session.RestoreTreasureProgress(new CoreTreasureProgress {
                    pirateExperience = 100, selectedMapId = "provision-test",
                    maps = new[] { new CoreTreasureMapSave { id = "provision-test", seed = "PROVISION-TREASURE", rankId = "weathered" } }
                });
                controller.Session.Cargo.Water = 400;
                ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
                int quantity = ExpectedProvisionQuantity(controller, 20);
                decimal gold = controller.Session.ProfileGold;
                yield return SetExplicitDepartureWaterFood(controller, 20);
                Assert.That(ActiveButton("Departure Basic Supplies Purchase").GetComponentInChildren<Text>().text,
                    Does.Contain(PortMarketCatalog.FormatPrice(ProvisionCost(controller, now, 0, quantity)) + " 金"),
                    "The remaining purchase action quotes the treasure voyage's own missing provisions.");
                ClickAcceptanceButton("Departure Basic Supplies Purchase"); yield return null;
                Assert.That(controller.Session.Cargo.Water, Is.EqualTo(400));
                Assert.That(controller.Session.Cargo.Food, Is.EqualTo(quantity));
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold - ProvisionCost(controller, now, 0, quantity)));
                Assert.That(controller.Session.TreasureMaps.Count, Is.EqualTo(1), "Buying rations cannot consume the treasure map.");
                Assert.That(controller.Session.SelectedTreasureMap.Id, Is.EqualTo("provision-test"));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey("断锚港", now, PortMarketItem.Water)), Is.Zero);
                ClickAcceptanceButton("Departure Basic Supplies Close"); yield return null;
                NavalBattleVisualCapture.Capture(controller, "06_Treasure_Departure_Provisions.png", "藏宝航期独立计算，只补缺少的食物", "Voyage Setup");
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }

        private IEnumerator LoadProvisioningFixture()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            ProfileGoldPersistence.Save(2000);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewRecruitmentKey(0), 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            VoyageCargo cargo = Object.FindFirstObjectByType<CoreGameplayDemoController>().Session.Cargo;
            cargo.Water = cargo.Food = cargo.Beer = cargo.Medicine = cargo.Wood = cargo.Iron = cargo.Cloth = cargo.Powder = cargo.Cannonballs = cargo.Gold = 0;
        }

        private static int ExpectedProvisionQuantity(CoreGameplayDemoController controller, int days) =>
            (int)Math.Ceiling(controller.Session.EstimateDailyRationConsumption(
                controller.Session.EffectiveCrewCount, controller.Session.HasCook) * days - 1e-9d);

        // Purchase validation tests intentionally retain a complete water/food draft even
        // when a recommendation would trim it to fit the hold or the market quota.
        private static IEnumerator SetExplicitDepartureWaterFood(CoreGameplayDemoController controller, int days)
        {
            ClickAcceptanceButton("Departure Buy Provisions"); yield return null;
            var quantities = (Dictionary<PortMarketItem, int>)typeof(CoreGameplayDemoController)
                .GetField("departureBasicSupplyQuantities", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            foreach (PortMarketItem item in PortMarketCatalog.AllItems) quantities[item] = 0;
            int target = ExpectedProvisionQuantity(controller, days);
            quantities[PortMarketItem.Water] = Math.Max(0, target - controller.Session.Cargo.Water);
            quantities[PortMarketItem.Food] = Math.Max(0, target - controller.Session.Cargo.Food);
            typeof(CoreGameplayDemoController).GetMethod("RefreshDepartureBasicSupplies", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, new object[] { string.Empty });
            yield return null;
        }
        private static decimal ProvisionCost(CoreGameplayDemoController controller, DateTime date, int water, int food) =>
            PortMarketCatalog.CalculateBuyCost(controller.Session.CurrentPort, PortMarketItem.Water, date, water) +
            PortMarketCatalog.CalculateBuyCost(controller.Session.CurrentPort, PortMarketItem.Food, date, food);
        private static void SetProvisionClock(CoreGameplayDemoController controller, Func<DateTime> clock) =>
            typeof(CoreGameplayDemoController).GetField("marketTimeProvider", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, clock);

        private static IEnumerator SetRecommendedDepartureWaterFood()
        {
            ClickAcceptanceButton("Departure Buy Provisions"); yield return null;
            ClickAcceptanceButton("Departure Basic Supplies Recommend"); yield return null;
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
            {
                if (item == PortMarketItem.Water || item == PortMarketItem.Food) continue;
                string key = PortMarketCatalog.Key(item);
                for (int count = 0; DepartureBasicSupplyQuantity(key) > 0 && count < 100; count++)
                    ClickAcceptanceButton("Departure Basic Supply Decrease " + key);
                Assert.That(DepartureBasicSupplyQuantity(key), Is.Zero, key);
            }
        }

        private static IEnumerator PurchaseRecommendedDepartureWaterFood()
        {
            yield return SetRecommendedDepartureWaterFood();
            ClickAcceptanceButton("Departure Basic Supplies Purchase"); yield return null;
            ClickAcceptanceButton("Departure Basic Supplies Close"); yield return null;
        }

        private static int DepartureBasicSupplyQuantity(string key) =>
            int.Parse(GameObject.Find("Departure Basic Supply Quantity " + key).GetComponent<Text>().text,
                System.Globalization.CultureInfo.InvariantCulture);

        private static Dictionary<string, int?> PreserveProvisionLedger(DateTime day)
        {
            var result = new Dictionary<string, int?>();
            for (int offset = 0; offset < 2; offset++)
                foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                {
                    string key = CoreGameplayDemoController.MarketPurchaseKey("断锚港", day.AddMonths(offset), item);
                    result[key] = PlayerPrefs.HasKey(key) ? (int?)PlayerPrefs.GetInt(key) : null;
                    PlayerPrefs.DeleteKey(key);
                }
            return result;
        }
        private static void RestoreProvisionLedger(Dictionary<string, int?> ledger)
        {
            foreach (var item in ledger)
                if (item.Value.HasValue) PlayerPrefs.SetInt(item.Key, item.Value.Value);
                else PlayerPrefs.DeleteKey(item.Key);
            PlayerPrefs.Save();
        }
    }
}
