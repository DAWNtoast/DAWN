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
                NavalBattleVisualCapture.Capture(controller, "01_Departure_Provision_Quote.png", "按航期与两天余量报价", "Voyage Setup");
                ClickAcceptanceButton("Departure Buy Provisions"); yield return null;
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold - cost));
                Assert.That(controller.Session.Cargo.Water, Is.EqualTo(quantity));
                Assert.That(controller.Session.Cargo.Food, Is.EqualTo(quantity));
                Assert.That(controller.Session.SelectedSeed, Is.EqualTo(seed));
                Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey("断锚港", now, PortMarketItem.Water)), Is.EqualTo(quantity));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey("断锚港", now, PortMarketItem.Food)), Is.EqualTo(quantity));
                Assert.That(ActiveButton("Departure Buy Provisions").interactable, Is.False);
                ActiveButton("Departure Buy Provisions").onClick.Invoke(); yield return null;
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold - cost), "A repeated callback must not buy more supplies.");
                NavalBattleVisualCapture.Capture(controller, "02_Departure_Provisions_Ready.png", "一键补齐后不会重复采购", "Voyage Setup");
                ClickAcceptanceButton("Adjust Voyage Supplies"); yield return null;
                Assert.That(GameObject.Find("Market Quota water").GetComponent<Text>().text, Does.Contain("已购 " + quantity));
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
                    Assert.That(ActiveButton("Departure Buy Provisions").interactable, Is.False, problem);
                    Assert.That(GameObject.Find("Departure Provision Quote").GetComponent<Text>().text, Does.Contain(problem));
                    ActiveButton("Departure Buy Provisions").onClick.Invoke(); yield return null;
                    Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                    Assert.That(controller.Session.Cargo.Water, Is.Zero);
                    Assert.That(controller.Session.Cargo.Food, Is.Zero);
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
                Assert.That(ActiveButton("Departure Buy Provisions").interactable, Is.False);
                Button oldQuote = ActiveButton("Departure Buy Provisions");
                decimal gold = controller.Session.ProfileGold;
                now = new DateTime(now.Year, now.Month, 1).AddMonths(1).AddSeconds(1);
                oldQuote.onClick.Invoke();
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold), "Crossing a game month must reprice before charging.");
                yield return null;
                Assert.That(ActiveButton("Departure Buy Provisions").interactable, Is.True);
                int quantity = ExpectedProvisionQuantity(controller, 26);
                ClickAcceptanceButton("Departure Buy Provisions"); yield return null;
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold - ProvisionCost(controller, now, quantity, quantity)));
                foreach (PortMarketItem item in new[] { PortMarketItem.Water, PortMarketItem.Food })
                {
                    Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey("断锚港", firstDay, item)), Is.EqualTo(PortMarketCatalog.MaxPurchaseQuota(item, 0)));
                    Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey("断锚港", now, item)), Is.EqualTo(quantity));
                }
                ClickAcceptanceButton("Adjust Voyage Supplies"); yield return null;
                Assert.That(GameObject.Find("Market Summary").GetComponent<Text>().text, Does.Contain(PortMarketCatalog.MonthKey(now)));
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
                Assert.That(GameObject.Find("Departure Provision Quote").GetComponent<Text>().text, Does.Contain("备航 20 天"));
                ClickAcceptanceButton("Departure Buy Provisions"); yield return null;
                Assert.That(controller.Session.Cargo.Water, Is.EqualTo(400));
                Assert.That(controller.Session.Cargo.Food, Is.EqualTo(quantity));
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold - ProvisionCost(controller, now, 0, quantity)));
                Assert.That(controller.Session.TreasureMaps.Count, Is.EqualTo(1), "Buying rations cannot consume the treasure map.");
                Assert.That(controller.Session.SelectedTreasureMap.Id, Is.EqualTo("provision-test"));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey("断锚港", now, PortMarketItem.Water)), Is.Zero);
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
            controller.Session.CrewCountValue * days;
        private static decimal ProvisionCost(CoreGameplayDemoController controller, DateTime date, int water, int food) =>
            PortMarketCatalog.CalculateBuyCost(controller.Session.CurrentPort, PortMarketItem.Water, date, water) +
            PortMarketCatalog.CalculateBuyCost(controller.Session.CurrentPort, PortMarketItem.Food, date, food);
        private static void SetProvisionClock(CoreGameplayDemoController controller, Func<DateTime> clock) =>
            typeof(CoreGameplayDemoController).GetField("marketTimeProvider", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, clock);

        private static Dictionary<string, int?> PreserveProvisionLedger(DateTime day)
        {
            var result = new Dictionary<string, int?>();
            for (int offset = 0; offset < 2; offset++)
                foreach (PortMarketItem item in new[] { PortMarketItem.Water, PortMarketItem.Food })
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
