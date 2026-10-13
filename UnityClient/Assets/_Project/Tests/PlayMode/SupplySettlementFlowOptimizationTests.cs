using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator SupplySettlementOptimization_CrewSliderKeepsDragSurfaceAndDoesNotPersistDraft()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            ClickAcceptanceButton("Configure Voyage Crew"); yield return null;
            Slider slider = GameObject.Find("Departure Ordinary Crew Slider").GetComponent<Slider>();
            GameObject setup = GameObject.Find("Voyage Setup");
            int[] objects = setup.GetComponentsInChildren<Transform>(true).Select(item => item.GetInstanceID()).ToArray();
            decimal gold = controller.Session.ProfileGold;
            int cargoSave = PlayerPrefs.GetInt(CoreGameplayDemoController.CargoKey("Water"), -1);
            for (int index = 0; index < 20; index++)
            {
                slider.value = index % 2 == 0 ? 1 : slider.maxValue;
                Assert.That(GameObject.Find("Departure Ordinary Crew Slider").GetComponent<Slider>(), Is.SameAs(slider));
                Assert.That(controller.Session.SelectedOrdinaryCrew, Is.EqualTo((int)slider.value));
                Assert.That(GameObject.Find("Departure Ordinary Crew Hint").GetComponent<Text>().text,
                    index % 2 == 0 ? Does.Contain("不足半载") : Does.Contain("已达半载"));
            }
            CollectionAssert.AreEqual(objects, setup.GetComponentsInChildren<Transform>(true).Select(item => item.GetInstanceID()).ToArray());
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.CargoKey("Water"), -1), Is.EqualTo(cargoSave));
            int selected = (int)slider.value;
            Slider.SliderEvent oldDrag = slider.onValueChanged;
            ClickAcceptanceButton("Confirm Departure Crew"); yield return null;
            oldDrag.Invoke(0);
            Assert.That(controller.Session.SelectedOrdinaryCrew, Is.EqualTo(selected), "A retired drag callback cannot change the confirmed crew.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SupplySettlementOptimization_ProvisionsRejectRetiredQuoteAndRepriceInPlace()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            var ledger = PreserveProvisionLedger(now);
            try
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                SetProvisionClock(controller, () => now);
                controller.Session.RestoreTreasureProgress(new CoreTreasureProgress
                    { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(CoreVoyageSession.Routes[1].UnlockLevel) });
                ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
                Button retired = ActiveButton("Departure Buy Provisions");
                Button.ButtonClickedEvent oldClick = retired.onClick;
                ClickAcceptanceButton("Route 1"); yield return null;
                Assert.That(controller.Session.SelectedRouteIndex, Is.EqualTo(1));
                decimal gold = controller.Session.ProfileGold;
                oldClick.Invoke();
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                Assert.That(controller.Session.Cargo.Water, Is.Zero);
                Assert.That(controller.Session.Cargo.Food, Is.Zero);
                Assert.That(GameObject.Find("Departure Basic Supplies"), Is.Null,
                    "A replaced entry callback cannot reopen a stale departure purchase panel.");
                yield return SetRecommendedDepartureWaterFood();
                Button current = ActiveButton("Departure Basic Supplies Purchase");
                Button.ButtonClickedEvent currentClick = current.onClick;
                GameObject panel = GameObject.Find("Voyage Setup");
                now = now.AddMonths(1);
                current.onClick.Invoke();
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold), "A new month must display the new quote before charging.");
                Assert.That(ActiveButton("Departure Basic Supplies Purchase"), Is.SameAs(current));
                Assert.That(GameObject.Find("Voyage Setup"), Is.SameAs(panel));
                int provisionDays = CoreVoyageSession.Routes[controller.Session.SelectedRouteIndex].RecommendedVoyageDays + 2;
                int required = ExpectedProvisionQuantity(controller, provisionDays);
                Assert.That(current.GetComponentInChildren<Text>().text,
                    Does.Contain(PortMarketCatalog.FormatPrice(ProvisionCost(controller, now, required, required)) + " 金"),
                    "The retained purchase action must show the refreshed monthly price.");
                Assert.That(GameObject.Find("Departure Provision Quote"), Is.Null);
                yield return null;
                Assert.That(ActiveButton("Departure Basic Supplies Purchase"), Is.SameAs(current), "The month observer must not immediately rebuild a quote already refreshed.");
                current.onClick.Invoke(); yield return null;
                decimal paidGold = controller.Session.ProfileGold;
                Assert.That(paidGold, Is.LessThan(gold));
                int water = controller.Session.Cargo.Water;
                currentClick.Invoke(); oldClick.Invoke();
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(paidGold));
                Assert.That(controller.Session.Cargo.Water, Is.EqualTo(water));
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }

        [UnityTest]
        public IEnumerator SupplySettlementOptimization_ProvisionsFailureKeepsControlsAndNeverPartiallyPays()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            controller.Session.RestoreProfile(0, controller.Session.CurrentPort);
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            yield return SetRecommendedDepartureWaterFood();
            Button buy = ActiveButton("Departure Basic Supplies Purchase");
            GameObject setup = GameObject.Find("Voyage Setup");
            Assert.That(buy.interactable, Is.False);
            for (int count = 0; count < 3; count++) buy.onClick.Invoke();
            Assert.That(ActiveButton("Departure Basic Supplies Purchase"), Is.SameAs(buy));
            Assert.That(GameObject.Find("Voyage Setup"), Is.SameAs(setup));
            Assert.That(controller.Session.ProfileGold, Is.Zero);
            Assert.That(controller.Session.Cargo.Water, Is.Zero);
            Assert.That(controller.Session.Cargo.Food, Is.Zero);
            Assert.That(buy.interactable, Is.False, "The retained purchase action stays disabled when funds are insufficient.");
            Assert.That(GameObject.Find("Departure Provision Quote"), Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SupplySettlementOptimization_DrawChartKeepsRevealAndWarehouseChoiceThenConfirmsOnce()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return CreateWarehouseSettlementFixture(controller, VoyageOutcome.SafeReturn);
            var session = controller.Session;
            Assert.That(session.CanDrawSeaChart, Is.True);
            SettlementRevealController reveal = Object.FindFirstObjectByType<SettlementRevealController>();
            GameObject card = GameObject.Find("Settlement Card");
            ClickAcceptanceButton("Settlement Store All Cargo"); yield return null;
            AssertSettlementStorageSelected(true);
            string savedCharts = PlayerPrefs.GetString(CoreGameplayDemoController.RecordedSeaChartsKey, string.Empty);
            Button draw = ActiveButton("Draw Sea Chart");
            Button.ButtonClickedEvent oldDraw = draw.onClick;
            ClickAcceptanceButton("Draw Sea Chart"); yield return null;
            Assert.That(Object.FindFirstObjectByType<SettlementRevealController>(), Is.SameAs(reveal));
            Assert.That(GameObject.Find("Settlement Card"), Is.SameAs(card));
            Assert.That(ActiveButton("Draw Sea Chart"), Is.SameAs(draw));
            Assert.That(draw.interactable, Is.False);
            Assert.That(draw.GetComponentInChildren<Text>().text, Is.EqualTo("海图已绘制"));
            Assert.That(GameObject.Find("Settlement Protection").GetComponent<Text>().text, Does.Contain("确认后收录"));
            AssertSettlementStorageSelected(true);
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.RecordedSeaChartsKey, string.Empty), Is.EqualTo(savedCharts));
            Button.ButtonClickedEvent oldFinish = ActiveButton("Return To Port").onClick;
            ClickAcceptanceButton("Return To Port"); yield return null;
            decimal gold = session.ProfileGold;
            string warehouse = session.PortWarehouse.Serialize();
            string charts = PlayerPrefs.GetString(CoreGameplayDemoController.RecordedSeaChartsKey, string.Empty);
            oldDraw.Invoke(); oldFinish.Invoke(); oldFinish.Invoke();
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouse));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.RecordedSeaChartsKey, string.Empty), Is.EqualTo(charts));
            Assert.That(charts, Is.Not.EqualTo(savedCharts));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
