using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Content;
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
        public IEnumerator RecommendedSupplyUi_UsesSelectedCrewAndCookDailyConsumption()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var cook = CrewNarrativeCatalog.ForPortAndProfession(controller.Session.CurrentPort, 4);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(cook.Id), 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewRecruitmentKey(4), 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.ActiveCrewVariantKey(4), cook.Id);
            PlayerPrefs.SetString(CoreGameplayDemoController.CrewLocationKeyForReset(cook.Id), controller.Session.CurrentPort);
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            if (!controller.Session.HasCook)
            {
                ClickAcceptanceButton("Configure Voyage Crew"); yield return null;
                yield return ClickDepartureCrewChoice(4);
                ClickAcceptanceButton("Confirm Departure Crew"); yield return null;
            }
            yield return SetOrdinaryDepartureCount(controller, 3);
            Assert.That(controller.Session.HasCook, Is.True);
            int crew = controller.Session.EffectiveCrewCount;
            Assert.That(crew, Is.LessThan(controller.Session.CrewCountValue));
            int target = (int)Math.Ceiling(controller.Session.EstimateDailyRationConsumption(crew, true) * 26 - 1e-9d);
            Assert.That(controller.Session.RecommendedSupplyTarget(VoyageSupplyType.Water), Is.EqualTo(target));
            Assert.That(target, Is.LessThan(controller.Session.RecommendedSupplyTarget(VoyageSupplyType.Water, crew, false)));
            VoyageSupplyOrder expected = controller.Session.CreateRecommendedSupplyOrder(crew, true);
            decimal gold = controller.Session.ProfileGold;
            ClickAcceptanceButton("Departure Buy Provisions"); yield return null;
            ClickAcceptanceButton("Departure Basic Supplies Recommend"); yield return null;
            AssertRecommendedDepartureOrder(expected);
            Assert.That(DepartureBasicSupplyQuantity("water"), Is.EqualTo(target));
            Assert.That(GameObject.Find("Departure Basic Supplies Hint").GetComponent<Text>().text, Does.Contain("已按航程"));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            Assert.That(controller.Session.Cargo.Water, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RecommendedSupplyUi_BothHandlersUseTheSameCoreOrder()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            controller.Session.Cargo.Water = 30;
            controller.Session.Cargo.Powder = 7;
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            yield return SetOrdinaryDepartureCount(controller, 4);
            VoyageSupplyOrder expected = controller.Session.CreateRecommendedSupplyOrder();
            ClickAcceptanceButton("Departure Buy Provisions"); yield return null;
            ClickAcceptanceButton("Departure Basic Supplies Recommend"); yield return null;
            AssertRecommendedDepartureOrder(expected);
            ClickAcceptanceButton("Departure Basic Supplies Close"); yield return null;
            // The retained maintenance handler has no current navigation entry; invoke its
            // real implementation directly, while departure above uses the visible buttons.
            VoyageSupplyOrder maintenance = InvokeRecommendedMaintenanceOrder(controller);
            foreach (VoyageSupplyType type in CoreVoyageSession.PurchasableSupplies)
                Assert.That(maintenance[type], Is.EqualTo(expected[type]), type.ToString());
            Assert.That(RecommendedMaintenanceNotice(controller), Does.Contain("推荐清单"));
            Assert.That(controller.Session.Cargo.Water, Is.EqualTo(30));
            Assert.That(controller.Session.Cargo.Powder, Is.EqualTo(7));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RecommendedSupplyUi_MonthlyQuotaTrimsBothDraftsAndWarns()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            var ledger = PreserveProvisionLedger(now);
            try
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                SetProvisionClock(controller, () => now);
                foreach (PortMarketItem item in new[] { PortMarketItem.Water, PortMarketItem.Powder })
                    PlayerPrefs.SetInt(CoreGameplayDemoController.MarketPurchaseKey(controller.Session.CurrentPort, now, item),
                        PortMarketCatalog.MaxPurchaseQuota(item, 0));
                ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
                decimal gold = controller.Session.ProfileGold;
                ClickAcceptanceButton("Departure Buy Provisions"); yield return null;
                ClickAcceptanceButton("Departure Basic Supplies Recommend"); yield return null;
                Assert.That(DepartureBasicSupplyQuantity("water"), Is.Zero);
                Assert.That(DepartureBasicSupplyQuantity("powder"), Is.Zero);
                Assert.That(DepartureBasicSupplyQuantity("food"), Is.GreaterThan(0));
                Assert.That(GameObject.Find("Departure Basic Supplies Hint").GetComponent<Text>().text,
                    Does.Contain("未达到完整推荐储备"));
                ClickAcceptanceButton("Departure Basic Supplies Close"); yield return null;
                VoyageSupplyOrder maintenance = InvokeRecommendedMaintenanceOrder(controller);
                Assert.That(maintenance[VoyageSupplyType.Water], Is.Zero);
                Assert.That(maintenance[VoyageSupplyType.Powder], Is.Zero);
                Assert.That(maintenance[VoyageSupplyType.Food], Is.GreaterThan(0));
                Assert.That(RecommendedMaintenanceNotice(controller), Does.Contain("未达到完整推荐储备"));
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                Assert.That(controller.Session.Cargo.UsedVolume, Is.Zero);
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }

        [UnityTest]
        public IEnumerator RecommendedSupplyUi_FullHoldFitsDraftAndWarnsWithoutDiscardingCargo()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            controller.Session.Cargo.Wood = 75;
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            double held = controller.Session.Cargo.UsedVolume;
            decimal gold = controller.Session.ProfileGold;
            VoyageSupplyOrder expected = controller.Session.CreateRecommendedSupplyOrder();
            ClickAcceptanceButton("Departure Buy Provisions"); yield return null;
            ClickAcceptanceButton("Departure Basic Supplies Recommend"); yield return null;
            AssertRecommendedDepartureOrder(expected);
            double volume = held;
            foreach (VoyageSupplyType type in CoreVoyageSession.PurchasableSupplies)
                volume += expected[type] * PortMarketCatalog.CargoVolume(RecommendedMarketItem(type));
            Assert.That(volume, Is.LessThanOrEqualTo(controller.Session.CargoCapacityValue + .001d));
            Assert.That(DepartureBasicSupplyQuantity("water"), Is.LessThan(controller.Session.RecommendedSupplyTarget(VoyageSupplyType.Water)));
            Assert.That(GameObject.Find("Departure Basic Supplies Hint").GetComponent<Text>().text,
                Does.Contain("未达到完整推荐储备"));
            ClickAcceptanceButton("Departure Basic Supplies Close"); yield return null;
            VoyageSupplyOrder maintenance = InvokeRecommendedMaintenanceOrder(controller);
            foreach (VoyageSupplyType type in CoreVoyageSession.PurchasableSupplies)
                Assert.That(maintenance[type], Is.EqualTo(expected[type]), type.ToString());
            Assert.That(RecommendedMaintenanceNotice(controller), Does.Contain("未达到完整推荐储备"));
            Assert.That(controller.Session.Cargo.UsedVolume, Is.EqualTo(held));
            Assert.That(controller.Session.Cargo.Wood, Is.EqualTo(75));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            LogAssert.NoUnexpectedReceived();
        }

        private static PortMarketItem RecommendedMarketItem(VoyageSupplyType type) =>
            (PortMarketItem)Enum.Parse(typeof(PortMarketItem), type.ToString());

        private static void AssertRecommendedDepartureOrder(VoyageSupplyOrder expected)
        {
            foreach (VoyageSupplyType type in CoreVoyageSession.PurchasableSupplies)
                Assert.That(DepartureBasicSupplyQuantity(PortMarketCatalog.Key(RecommendedMarketItem(type))),
                    Is.EqualTo(expected[type]), type.ToString());
            foreach (string key in new[] { "beer", "iron", "cloth" })
                Assert.That(DepartureBasicSupplyQuantity(key), Is.Zero, key);
        }

        private static VoyageSupplyOrder InvokeRecommendedMaintenanceOrder(CoreGameplayDemoController controller)
        {
            typeof(CoreGameplayDemoController).GetMethod("FillStandardSupplyOrder", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
            return (VoyageSupplyOrder)typeof(CoreGameplayDemoController).GetField("pendingSupplyOrder",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        }

        private static string RecommendedMaintenanceNotice(CoreGameplayDemoController controller) =>
            (string)typeof(CoreGameplayDemoController).GetField("portNotice", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
    }
}
