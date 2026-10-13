using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        [UnityTest, Timeout(180000)]
        public IEnumerator MonthlyCalendar_SailingCrossesMonthRestocksEveryPortAndPersistsAcrossVoyages()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            // Boundary fixture only: no GM command, reward injection or private
            // clock advance. The real sailing action must cross the boundary.
            DateTime lastEvening = new DateTime(1, 1, 31, 23, 30, 0);
            double startHours = (lastEvening - CoreVoyageSession.GameClockOrigin).TotalHours;
            Assert.That(session.RestoreGameClock(startHours), Is.True);
            session.Cargo.Water = session.Cargo.Food = 200;
            var art = new ComponentArtCatalog();
            var previousOffers = new Dictionary<string, string[]>();
            foreach (string port in CoreVoyageSession.DestinationPorts)
            {
                foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                    PlayerPrefs.SetInt(CoreGameplayDemoController.MarketPurchaseKey(port, lastEvening, item),
                        PortMarketCatalog.MaxPurchaseQuota(item, 0));
                previousOffers[port] = PortComponentShopCatalog.BuildExpandedStock(art, port, lastEvening)
                    .Select(offer => offer.Id).ToArray();
                foreach (string id in previousOffers[port])
                    PlayerPrefs.SetInt(CoreGameplayDemoController.ComponentShopPurchaseKey(id), 1);
            }
            ClickAcceptanceButton("Port Hotspot Market"); yield return null;
            yield return OpenMarketTrade("water");
            Assert.That(CalendarMarketRemaining(controller, session.CurrentPort, PortMarketItem.Water), Is.Zero);
            Assert.That(ActiveButton("Confirm Market Buy water").IsInteractable(), Is.False);
            yield return OpenMarketItem("wood");
            Assert.That(MarketInfoLabel("Market Quota wood").text, Does.Contain("已购 200"));
            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            yield return StartCalendarVoyageAndSail(controller);
            Assert.That(session.GameDateTime.Month, Is.EqualTo(2));
            Assert.That(session.ElapsedGameHours, Is.EqualTo(startHours + session.TotalHours).Within(.000001d));
            double afterFirstSail = session.ElapsedGameHours;
            ClickAcceptanceButton("Safe Return"); yield return null;
            ClickAcceptanceButton("Return To Port"); yield return null;
            Assert.That(session.ElapsedGameHours, Is.GreaterThanOrEqualTo(afterFirstSail));
            double savedHours = session.ElapsedGameHours;
            DateTime savedDate = session.GameDateTime;

            // Read all six ports through the real controller's quota calculation;
            // no port visit is needed to initialize a new monthly batch.
            foreach (string port in CoreVoyageSession.DestinationPorts)
            {
                foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                {
                    int newStock = PortMarketCatalog.DayState(port, item, savedDate).Stock;
                    Assert.That(CalendarMarketRemaining(controller, port, item), Is.EqualTo(newStock), port + "/" + item);
                    Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey(port, lastEvening, item)),
                        Is.EqualTo(PortMarketCatalog.MaxPurchaseQuota(item, 0)), "New month must not rewrite historical receipts.");
                }
                var next = PortComponentShopCatalog.BuildExpandedStock(art, port, savedDate);
                Assert.That(next.All(offer => !previousOffers[port].Contains(offer.Id)), Is.True, port);
                Assert.That(next.All(offer => PlayerPrefs.GetInt(CoreGameplayDemoController.ComponentShopPurchaseKey(offer.Id)) == 0), Is.True, port);
            }
            ClickAcceptanceButton("Port Hotspot Market"); yield return null;
            yield return OpenMarketItem("water");
            Assert.That(ActiveButton("Market Buy water").IsInteractable(), Is.True);
            AssertCalendarMarketDisplayedMonth(controller, savedDate);
            Assert.That(MarketInfoLabel("Market Quota water").text,
                Does.Contain("可购 " + CalendarMarketRemaining(controller, session.CurrentPort, PortMarketItem.Water))
                    .And.Contain("已购 0").And.Not.Contain("下次补货"));
            NavalBattleVisualCapture.Capture(controller, "MonthlyCalendar_01_NewMonthMarket.png",
                "月底夹具通过真实航行进入二月；所有港口月库存恢复，历史采购记录保留", "Market Panel");
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            session = controller.Session;
            Assert.That(session.ElapsedGameHours, Is.EqualTo(savedHours));
            Assert.That(session.GameDateTime, Is.EqualTo(savedDate));
            Assert.That(GameObject.Find("Port Hub Time").GetComponent<Text>().text, Does.Contain(CoreVoyageSession.FormatGameDate(savedDate, true)));
            yield return StartCalendarVoyageAndSail(controller, savedHours);
            Assert.That(session.ElapsedGameHours, Is.EqualTo(savedHours + session.TotalHours).Within(.000001d),
                "Only this voyage's duration resets; the campaign calendar must continue.");
            ClickAcceptanceButton("Safe Return"); yield return null;
            ClickAcceptanceButton("Return To Port"); yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator MonthlyCalendar_OneMonthSharesQuotesButNewMonthRejectsPendingPurchase()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            // New campaigns begin on the local date. Arrange a known day in
            // that campaign month so advancing one day cannot cross its end.
            DateTime firstDay = new DateTime(session.GameDateTime.Year, session.GameDateTime.Month, 1);
            double firstDayHours = (firstDay - CoreVoyageSession.GameClockOrigin).TotalHours;
            Assert.That(session.RestoreGameClock(firstDayHours), Is.True);
            Assert.That(typeof(CoreGameplayDemoController).GetField("marketTimeProvider", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(controller), Is.Null, "Use the campaign calendar, not a test-only market clock.");
            ClickAcceptanceButton("Port Hotspot Market"); yield return null;
            yield return SelectMarketStorageMode(controller, MarketPurchaseStorageMode.Ask);
            decimal originalGold = session.ProfileGold;
            decimal cost = PortMarketCatalog.CalculateBuyCost(session.CurrentPort, PortMarketItem.Wood, firstDay, 1);
            yield return OpenMarketTrade("wood");
            ClickAcceptanceButton("Confirm Market Buy wood"); yield return null;
            Button.ButtonClickedEvent sameMonth = ActiveButton("Market Buy To Warehouse").onClick;
            Assert.That(session.RestoreGameClock(firstDayHours + 24d), Is.True); // Next day in the same campaign month.
            sameMonth.Invoke(); yield return null;
            Assert.That(session.ProfileGold, Is.EqualTo(originalGold - cost));
            Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "wood"), Is.EqualTo(1));
            Assert.That(CoreGameplayDemoController.MarketPurchaseKey(session.CurrentPort, firstDay, PortMarketItem.Wood),
                Is.EqualTo(CoreGameplayDemoController.MarketPurchaseKey(session.CurrentPort, session.GameDateTime, PortMarketItem.Wood)));
            yield return OpenMarketTrade("wood");
            ClickAcceptanceButton("Confirm Market Buy wood"); yield return null;
            Button.ButtonClickedEvent oldMonth = ActiveButton("Market Buy To Warehouse").onClick;
            decimal before = session.ProfileGold;
            DateTime nextMonth = firstDay.AddMonths(1);
            Assert.That(session.RestoreGameClock((nextMonth - CoreVoyageSession.GameClockOrigin).TotalHours), Is.True);
            yield return null; // The visible market must notice the rollover itself.
            AssertCalendarMarketDisplayedMonth(controller, nextMonth);
            Assert.That(MarketInfoLabel("Market Quota wood").text,
                Does.Contain("可购 " + CalendarMarketRemaining(controller, session.CurrentPort, PortMarketItem.Wood))
                    .And.Contain("已购 0"));
            oldMonth.Invoke(); yield return null;
            Assert.That(session.ProfileGold, Is.EqualTo(before));
            Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "wood"), Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey(session.CurrentPort, session.GameDateTime, PortMarketItem.Wood)), Is.Zero);
            Assert.That(GameObject.Find("Market Purchase Choice"), Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator MonthlyCalendar_InvestmentAddsStockImmediatelyWithoutResettingPurchaseReceipts()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            string port = session.CurrentPort;
            PrepareInvestmentResources(controller, port);
            DateTime date = session.GameDateTime;
            const int alreadyBought = 37;
            string ledgerKey = CoreGameplayDemoController.MarketPurchaseKey(port, date, PortMarketItem.Wood);
            PlayerPrefs.SetInt(ledgerKey, alreadyBought);
            int oldRemaining = CalendarMarketRemaining(controller, port, PortMarketItem.Wood);
            string[] oldOffers = PortComponentShopCatalog.BuildExpandedStock(new ComponentArtCatalog(), port, date)
                .Select(offer => offer.Id).ToArray();
            PlayerPrefs.SetInt(CoreGameplayDemoController.ComponentShopPurchaseKey(oldOffers[0]), 1);
            yield return OpenInvestmentThroughMarket();
            ClickAcceptanceButton("Confirm Port Investment"); yield return null;
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.PortInvestmentKey(port)), Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetInt(ledgerKey), Is.EqualTo(alreadyBought));
            int added = PortMarketCatalog.MaxPurchaseQuota(PortMarketItem.Wood, 1) - PortMarketCatalog.MaxPurchaseQuota(PortMarketItem.Wood, 0);
            Assert.That(CalendarMarketRemaining(controller, port, PortMarketItem.Wood), Is.EqualTo(oldRemaining + added));
            var expanded = PortComponentShopCatalog.BuildExpandedStock(new ComponentArtCatalog(), port, date, 0, string.Empty,
                PortInvestmentCatalog.RarityThresholdShift(1), 1);
            Assert.That(expanded.Count, Is.EqualTo(PortComponentShopCatalog.StockCount(1)));
            CollectionAssert.AreEqual(oldOffers, expanded.Take(oldOffers.Length).Select(offer => offer.Id).ToArray(),
                "An upgrade adds offers without making already sold components purchasable again.");
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ComponentShopPurchaseKey(oldOffers[0])), Is.EqualTo(1));
            ClickAcceptanceButton("Open Port Market"); yield return null;
            yield return OpenMarketItem("wood");
            Assert.That(MarketInfoLabel("Market Quota wood").text, Does.Contain("已购 37"));
            NavalBattleVisualCapture.Capture(controller, "MonthlyCalendar_02_InvestmentStock.png",
                "真实投资按钮增加当月库存和额度；既有采购记录与售出部件状态不重置", "Market Panel");
            LogAssert.NoUnexpectedReceived();
        }

        private static int CalendarMarketRemaining(CoreGameplayDemoController controller, string port, PortMarketItem item) =>
            (int)typeof(CoreGameplayDemoController).GetMethod("MarketRemaining", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, new object[] { port, item, controller.Session.GameDateTime,
                    PlayerPrefs.GetInt(CoreGameplayDemoController.PortInvestmentKey(port), 0) });

        private static void AssertCalendarMarketDisplayedMonth(CoreGameplayDemoController controller, DateTime date)
        {
            Assert.That(typeof(CoreGameplayDemoController).GetField("marketDisplayedDate",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller),
                Is.EqualTo(new DateTime(date.Year, date.Month, 1)),
                "The market must refresh monthly quotes and quotas after the detail date label is removed.");
        }

        private static IEnumerator StartCalendarVoyageAndSail(CoreGameplayDemoController controller, double? expectedHours = null)
        {
            CoreVoyageSession session = controller.Session;
            session.SetSeed("GAME-CALENDAR-MONTH-BOUNDARY");
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            ClickAcceptanceButton("Start Voyage"); yield return null;
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.TotalHours, Is.Zero);
            if (expectedHours.HasValue) Assert.That(session.ElapsedGameHours, Is.EqualTo(expectedHours.Value));
            // Bypass fixed map nodes only. Generated sea-chart incidents remain
            // active and are handled through their actual skip/choice buttons.
            foreach (CoreMapNode node in session.Map.Nodes) node.Visited = true;
            for (int leg = 0; leg < 4 && session.TotalHours <= .5d; leg++)
            {
                session.SetCourseTo(session.Map.Destination);
                // Stay below the first random-event distance (120–240 nm); a
                // 100 nm leg still advances well beyond the arranged half-hour.
                GameObject.Find("Distance Slider").GetComponent<Slider>().value = 100f;
                ClickAcceptanceButton("Execute Sail"); yield return null;
                yield return WaitForNavigationMotionCompletion(controller);
                for (int incident = 0; incident < 8 && session.Phase == CoreDemoPhase.Encounter; incident++)
                {
                    CoreEncounter encounter = session.ActiveEncounter;
                    if (!encounter.Mandatory && ActiveButton("Skip Encounter")?.IsInteractable() == true)
                    {
                        ClickAcceptanceButton("Skip Encounter"); yield return null;
                        continue;
                    }
                    int choice = Enumerable.Range(0, encounter.Choices.Count)
                        .Where(session.IsChoiceAvailable)
                        .Where(index => !encounter.Choices[index].StartsBattle && !encounter.Choices[index].Effect.ForceReturn)
                        .OrderBy(index => encounter.Choices[index].Hours).DefaultIfEmpty(-1).First();
                    Assert.That(choice, Is.GreaterThanOrEqualTo(0),
                        "Calendar fixture encountered no safe continuation: " + encounter.Id);
                    ClickAcceptanceButton("Encounter Choice " + choice); yield return null;
                }
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            }
            Assert.That(session.TotalHours, Is.GreaterThan(.5d));
        }
    }
}
