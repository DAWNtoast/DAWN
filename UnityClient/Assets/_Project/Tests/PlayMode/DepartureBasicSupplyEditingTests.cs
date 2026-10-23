using System;
using System.Collections;
using System.Globalization;
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
        public IEnumerator DepartureBasicSupplies_DirectInputAndTenStepsQuoteExactDiscountedBasket()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            var ledger = PreserveProvisionLedger(now);
            try
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                SetProvisionClock(controller, () => now);
                controller.Session.Cargo.Fame = 200;
                ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
                ClickAcceptanceButton("Departure Buy Provisions"); yield return null;
                decimal gold = controller.Session.ProfileGold;
                GameObject modal = GameObject.Find("Departure Basic Supplies");
                ClickAcceptanceButton("Departure Basic Supply Increase Ten water");
                ClickAcceptanceButton("Departure Basic Supply Increase water");
                Assert.That(DepartureBasicSupplyQuantity("water"), Is.EqualTo(11));
                ClickAcceptanceButton("Departure Basic Supply Decrease Ten water");
                Assert.That(DepartureBasicSupplyQuantity("water"), Is.EqualTo(1));
                SetBasicSupplyInput("water", "24");
                SetBasicSupplyInput("food", "23");
                SetBasicSupplyInput("cannonballs", "12");
                Assert.That(GameObject.Find("Departure Basic Supplies"), Is.SameAs(modal), "Editing must retain the modal and field focus.");
                Assert.That(controller.Session.Cargo.Water, Is.Zero);
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold), "Typing and committing input never purchase.");
                decimal total = 0m;
                foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                {
                    string key = PortMarketCatalog.Key(item);
                    int quantity = DepartureBasicSupplyQuantity(key);
                    decimal unit = PortMarketCatalog.BuyUnitPrice(controller.Session.CurrentPort, item, now, controller.Session.Cargo.Fame);
                    decimal subtotal = PortMarketCatalog.CalculateBuyCost(controller.Session.CurrentPort, item, now, quantity, controller.Session.Cargo.Fame);
                    Assert.That(GameObject.Find("Departure Basic Supply Unit Price " + key).GetComponent<Text>().text,
                        Is.EqualTo("单价 " + PortMarketCatalog.FormatPrice(unit) + " 金"), key);
                    Assert.That(GameObject.Find("Departure Basic Supply Subtotal " + key).GetComponent<Text>().text,
                        Is.EqualTo("小计 " + PortMarketCatalog.FormatPrice(subtotal) + " 金"), key);
                    total += subtotal;
                }
                Assert.That(ActiveButton("Departure Basic Supplies Purchase").GetComponentInChildren<Text>().text,
                    Is.EqualTo("购买 · " + PortMarketCatalog.FormatPrice(total) + " 金"));
                BasicSupplyAuditCapture.Capture(controller, "01-edited-basket");
                ClickAcceptanceButton("Departure Basic Supplies Purchase"); yield return null;
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold - total));
                Assert.That(controller.Session.Cargo.Water, Is.EqualTo(24));
                Assert.That(controller.Session.Cargo.Food, Is.EqualTo(23));
                Assert.That(controller.Session.Cargo.Cannonballs, Is.EqualTo(12));
                Assert.That(BasicSupplyInput("water").text, Is.EqualTo("0"));
                Assert.That(GameObject.Find("Departure Basic Supply Subtotal water").GetComponent<Text>().text, Is.EqualTo("小计 0 金"));
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }

        [UnityTest]
        public IEnumerator DepartureBasicSupplies_InvalidInputCannotBuyAndRetiredInputCannotChangeNewBasket()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            ClickAcceptanceButton("Departure Buy Provisions"); yield return null;
            decimal gold = controller.Session.ProfileGold;
            SetBasicSupplyInput("water", "5");
            Button.ButtonClickedEvent oldPurchase = ActiveButton("Departure Basic Supplies Purchase").onClick;
            foreach (string invalid in new[] { "", "-1", "not a number", "999999999999999999999999999" })
            {
                SetBasicSupplyInput("water", invalid);
                Assert.That(ActiveButton("Departure Basic Supplies Purchase").interactable, Is.False, invalid);
                oldPurchase.Invoke();
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold), invalid);
                Assert.That(controller.Session.Cargo.Water, Is.Zero, invalid);
            }
            SetBasicSupplyInput("water", "");
            BasicSupplyAuditCapture.Capture(controller, "02-invalid-quantity");
            ClickAcceptanceButton("Departure Basic Supply Decrease Ten water");
            Assert.That(DepartureBasicSupplyQuantity("water"), Is.Zero, "Bulk decrease clamps at zero and repairs invalid input.");
            SetBasicSupplyInput("water", "17");
            InputField.OnChangeEvent oldChange = BasicSupplyInput("water").onValueChanged;
            InputField.EndEditEvent oldEnd = BasicSupplyInput("water").onEndEdit;
            Button.ButtonClickedEvent oldTen = ActiveButton("Departure Basic Supply Increase Ten water").onClick;
            ClickAcceptanceButton("Departure Basic Supplies Close"); yield return null;
            oldChange.Invoke("40"); oldEnd.Invoke("40"); oldTen.Invoke(); oldPurchase.Invoke();
            ClickAcceptanceButton("Departure Buy Provisions"); yield return null;
            oldChange.Invoke("40"); oldEnd.Invoke("40"); oldTen.Invoke(); oldPurchase.Invoke();
            Assert.That(DepartureBasicSupplyQuantity("water"), Is.Zero);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            SetBasicSupplyInput("water", "");
            ClickAcceptanceButton("Departure Basic Supplies Recommend");
            Assert.That(int.TryParse(BasicSupplyInput("water").text, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int recommendation), Is.True, "Recommendation replaces an invalid draft.");
            Assert.That(recommendation, Is.GreaterThanOrEqualTo(0));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DepartureBasicSupplies_BulkInputClampsQuotaAndPreservesCapacityValidation()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            var ledger = PreserveProvisionLedger(now);
            try
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                SetProvisionClock(controller, () => now);
                ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
                ClickAcceptanceButton("Departure Buy Provisions"); yield return null;
                string quotaKey = CoreGameplayDemoController.MarketPurchaseKey(controller.Session.CurrentPort, now, PortMarketItem.Water);
                int maximum = (int)typeof(CoreGameplayDemoController).GetMethod("DepartureBasicSupplyLimit",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { PortMarketItem.Water, now });
                Assert.That(maximum, Is.GreaterThan(3));
                PlayerPrefs.SetInt(quotaKey, maximum - 3);
                SetBasicSupplyInput("water", "9999999999");
                Assert.That(DepartureBasicSupplyQuantity("water"), Is.EqualTo(3));
                Assert.That(GameObject.Find("Departure Basic Supplies Hint").GetComponent<Text>().text, Does.Contain("最多可购 3"));
                ActiveButton("Departure Basic Supply Increase Ten water").onClick.Invoke();
                Assert.That(DepartureBasicSupplyQuantity("water"), Is.EqualTo(3));
                SetBasicSupplyInput("water", "9999999999");
                BasicSupplyAuditCapture.Capture(controller, "03-stock-limit");
                ClickAcceptanceButton("Departure Basic Supply Decrease Ten water");
                Assert.That(DepartureBasicSupplyQuantity("water"), Is.Zero);
                controller.Session.Cargo.Water = int.MaxValue - 1;
                SetBasicSupplyInput("water", "9999999999");
                Assert.That(DepartureBasicSupplyQuantity("water"), Is.EqualTo(1), "Cargo int overflow must also cap direct input.");
                Assert.That(ActiveButton("Departure Basic Supplies Purchase").interactable, Is.False);
                decimal gold = controller.Session.ProfileGold;
                ActiveButton("Departure Basic Supplies Purchase").onClick.Invoke();
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                Assert.That(controller.Session.Cargo.Water, Is.EqualTo(int.MaxValue - 1));
                BasicSupplyAuditCapture.Capture(controller, "04-capacity-block");
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }

        private static InputField BasicSupplyInput(string key) =>
            GameObject.Find("Departure Basic Supply Quantity Input " + key).GetComponent<InputField>();

        private static void SetBasicSupplyInput(string key, string value)
        {
            InputField input = BasicSupplyInput(key);
            // Exercise callbacks with hostile paste/legacy values as well as
            // strings normally admitted by the integer keyboard validator.
            input.SetTextWithoutNotify(value);
            input.onValueChanged.Invoke(value);
            input.onEndEdit.Invoke(value);
        }
    }
}
