using System;
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
        [UnityTest]
        public IEnumerator DepartureBasicSupplies_NineItemBasketOnlyBuysAndSharesPersistentMarketLedger()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            var ledger = PreserveProvisionLedger(now);
            try
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                SetProvisionClock(controller, () => now);
                foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                    SetDepartureBasicSupplyHeld(controller.Session.Cargo, item, (int)item + 1);
                ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
                Button entry = ActiveButton("Departure Buy Provisions");
                Assert.That(entry.GetComponentInChildren<Text>().text, Is.EqualTo("购买基础物资"));
                Assert.That(ActiveButton("Adjust Voyage Supplies").GetComponentInChildren<Text>().text, Is.EqualTo("前往市场"));
                decimal gold = controller.Session.ProfileGold;
                var held = PortMarketCatalog.AllItems.ToDictionary(PortMarketCatalog.Key,
                    item => controller.Session.GetMarketCargoAmount(PortMarketCatalog.Key(item)));
                ClickAcceptanceButton("Departure Buy Provisions"); yield return null;
                GameObject modal = GameObject.Find("Departure Basic Supplies");
                Assert.That(modal, Is.Not.Null);
                Assert.That(GameObject.Find("Departure Basic Supplies Overlay"), Is.Not.Null);
                Assert.That(ActiveButton("Departure Basic Supplies Purchase").interactable, Is.False,
                    "Opening the panel never preselects a purchase or spends gold.");
                Assert.That(modal.GetComponentsInChildren<Button>().Any(button =>
                    button.name.Contains("Sell") || button.GetComponentInChildren<Text>()?.text.Contains("卖出") == true), Is.False);
                decimal total = 0m;
                foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                {
                    string key = PortMarketCatalog.Key(item);
                    AssertItemIcon("Departure Basic Supply Icon " + key, ItemIconCatalog.ForCargo(key), 72f);
                    Assert.That(GameObject.Find("Departure Basic Supply Held " + key).GetComponent<Text>().text,
                        Does.Contain(held[key].ToString()), key);
                    Assert.That(DepartureBasicSupplyQuantity(key), Is.Zero, key);
                    ActiveButton("Departure Basic Supply Decrease " + key).onClick.Invoke();
                    Assert.That(DepartureBasicSupplyQuantity(key), Is.Zero, "A minus callback at zero cannot sell " + key);
                    ClickAcceptanceButton("Departure Basic Supply Increase " + key);
                    Assert.That(DepartureBasicSupplyQuantity(key), Is.EqualTo(1), key);
                    Assert.That(controller.Session.GetMarketCargoAmount(key), Is.EqualTo(held[key]),
                        "Selecting a purchase cannot mutate cargo: " + key);
                    total += PortMarketCatalog.CalculateBuyCost(controller.Session.CurrentPort, item, now, 1, controller.Session.Cargo.Fame);
                    GameObject endurance = GameObject.Find("Departure Basic Supply Endurance " + key);
                    Assert.That(endurance != null, Is.EqualTo(item == PortMarketItem.Water || item == PortMarketItem.Food), key);
                    if (endurance != null) Assert.That(endurance.GetComponent<Text>().text, Does.Contain("天"), key);
                }
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                NavalBattleVisualCapture.Capture(controller, "BasicSupplies_01_NineItemBasket.png",
                    "九种物资仅加入待购数量，水粮单独显示供应天数", "Departure Basic Supplies");
                ClickAcceptanceButton("Departure Basic Supplies Purchase"); yield return null;
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold - total));
                Assert.That(GameObject.Find("Departure Basic Supplies"), Is.SameAs(modal));
                foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                {
                    string key = PortMarketCatalog.Key(item);
                    Assert.That(controller.Session.GetMarketCargoAmount(key), Is.EqualTo(held[key] + 1), key);
                    Assert.That(DepartureBasicSupplyQuantity(key), Is.Zero, key);
                    Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey(controller.Session.CurrentPort, now, item)),
                        Is.EqualTo(1), key);
                    Assert.That(controller.Session.PortWarehouse.GetAmount(controller.Session.CurrentPort, key), Is.Zero,
                        "Departure purchases always enter the ship: " + key);
                }
                ClickAcceptanceButton("Departure Basic Supplies Close"); yield return null;
                ClickAcceptanceButton("Adjust Voyage Supplies"); yield return null;
                yield return OpenMarketItem("wood");
                Assert.That(MarketInfoLabel("Market Quota wood").text, Does.Contain("已购 1"));
                SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
                controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                {
                    string key = PortMarketCatalog.Key(item);
                    Assert.That(controller.Session.GetMarketCargoAmount(key), Is.EqualTo(held[key] + 1), key);
                }
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold - total));
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }

        [UnityTest]
        public IEnumerator DepartureBasicSupplies_RecommendationUsesShortfallsWithoutDiscardingExistingCargo()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            VoyageCargo cargo = controller.Session.Cargo;
            cargo.Water = 40; cargo.Food = 60; cargo.Beer = 3; cargo.Medicine = 1;
            cargo.Wood = 3; cargo.Iron = 2; cargo.Cloth = 4; cargo.Powder = 4; cargo.Cannonballs = 2;
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            VoyageSupplyOrder recommended = controller.Session.CreateRecommendedSupplyOrder();
            var before = PortMarketCatalog.AllItems.ToDictionary(PortMarketCatalog.Key,
                item => controller.Session.GetMarketCargoAmount(PortMarketCatalog.Key(item)));
            decimal gold = controller.Session.ProfileGold;
            ClickAcceptanceButton("Departure Buy Provisions"); yield return null;
            ClickAcceptanceButton("Departure Basic Supplies Recommend"); yield return null;
            var expected = new Dictionary<string, int> {
                { "water", recommended[VoyageSupplyType.Water] }, { "food", recommended[VoyageSupplyType.Food] }, { "beer", 0 },
                { "medicine", recommended[VoyageSupplyType.Medicine] }, { "wood", recommended[VoyageSupplyType.Wood] }, { "iron", 0 }, { "cloth", 0 },
                { "powder", recommended[VoyageSupplyType.Powder] }, { "cannonballs", recommended[VoyageSupplyType.Cannonballs] }
            };
            foreach (var item in expected)
            {
                Assert.That(DepartureBasicSupplyQuantity(item.Key), Is.EqualTo(item.Value), item.Key);
                Assert.That(controller.Session.GetMarketCargoAmount(item.Key), Is.EqualTo(before[item.Key]), item.Key);
            }
            ClickAcceptanceButton("Departure Basic Supply Increase beer");
            Assert.That(DepartureBasicSupplyQuantity("beer"), Is.EqualTo(1));
            ClickAcceptanceButton("Departure Basic Supplies Recommend");
            Assert.That(DepartureBasicSupplyQuantity("beer"), Is.Zero, "Recommendation replaces the draft without selling held beer.");
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            NavalBattleVisualCapture.Capture(controller, "BasicSupplies_02_RecommendedShortfalls.png",
                "建议数量扣除船舱现有物资，设定数量不会扣款", "Departure Basic Supplies");
            Button.ButtonClickedEvent oldIncrease = ActiveButton("Departure Basic Supply Increase wood").onClick;
            Button.ButtonClickedEvent oldPurchase = ActiveButton("Departure Basic Supplies Purchase").onClick;
            ClickAcceptanceButton("Departure Basic Supplies Close"); yield return null;
            oldIncrease.Invoke(); oldPurchase.Invoke();
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            foreach (var item in before) Assert.That(controller.Session.GetMarketCargoAmount(item.Key), Is.EqualTo(item.Value), item.Key);
            ClickAcceptanceButton("Departure Buy Provisions"); yield return null;
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                Assert.That(DepartureBasicSupplyQuantity(PortMarketCatalog.Key(item)), Is.Zero,
                    "Reopening starts a new basket and ignores retired callbacks.");
            oldIncrease.Invoke(); oldPurchase.Invoke();
            Assert.That(DepartureBasicSupplyQuantity("wood"), Is.Zero);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            LogAssert.NoUnexpectedReceived();
        }

        private static void SetDepartureBasicSupplyHeld(VoyageCargo cargo, PortMarketItem item, int quantity)
        {
            switch (item)
            {
                case PortMarketItem.Water: cargo.Water = quantity; break;
                case PortMarketItem.Food: cargo.Food = quantity; break;
                case PortMarketItem.Beer: cargo.Beer = quantity; break;
                case PortMarketItem.Medicine: cargo.Medicine = quantity; break;
                case PortMarketItem.Wood: cargo.Wood = quantity; break;
                case PortMarketItem.Iron: cargo.Iron = quantity; break;
                case PortMarketItem.Cloth: cargo.Cloth = quantity; break;
                case PortMarketItem.Powder: cargo.Powder = quantity; break;
                case PortMarketItem.Cannonballs: cargo.Cannonballs = quantity; break;
            }
        }
    }
}
