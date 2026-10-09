using System;
using System.Collections;
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
        [UnityTest, Timeout(120000)]
        public IEnumerator MarketQuantity_DefaultHundredUsesLastStockAndTypedSevenPersistsExactGold()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            var ledger = PreserveMarketStorageLedgers(now);
            try
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CoreVoyageSession session = controller.Session;
                SetProvisionClock(controller, () => now);
                int foodAllowance = Math.Min(PortMarketCatalog.DayState(session.CurrentPort, PortMarketItem.Food, now).Stock,
                    PortMarketCatalog.MaxPurchaseQuota(PortMarketItem.Food, 0));
                PlayerPrefs.SetInt(CoreGameplayDemoController.MarketPurchaseKey(session.CurrentPort, now, PortMarketItem.Food), foodAllowance - 37);
                ClickAcceptanceButton("Port Hotspot Market"); yield return null;
                Assert.That(MarketQuantityField("water").text, Is.EqualTo("100"));
                Assert.That(MarketQuantityField("food").text, Is.EqualTo("37"));
                decimal gold = session.ProfileGold;
                decimal foodCost = PortMarketCatalog.BuyUnitPrice(session.CurrentPort, PortMarketItem.Food, now) * 37;
                ClickAcceptanceButton("Market Buy food"); yield return null;
                Assert.That(session.Cargo.Food, Is.EqualTo(37));
                Assert.That(session.ProfileGold, Is.EqualTo(gold - foodCost));
                Assert.That(MarketQuantityField("food").text, Is.EqualTo("37"));
                Assert.That(MarketQuantityField("food").IsInteractable(), Is.True,
                    "Exhausting purchases must leave quantity editing available for carried cargo.");
                Assert.That(ActiveButton("Market Buy food").IsInteractable(), Is.False);
                Assert.That(ActiveButton("Market Sell food").IsInteractable(), Is.True,
                    "Exhausting purchases must not block selling carried food.");
                NavalBattleVisualCapture.Capture(controller, "MarketQuantity_01_Last_37_Food.png",
                    "余量37默认买37，金额按实际单位计算；买空后仍可出售船货", "Market Panel");

                Button.ButtonClickedEvent oldHundred = ActiveButton("Market Buy water").onClick;
                yield return EnterMarketQuantity("water", "7", false);
                oldHundred.Invoke();
                Assert.That(session.Cargo.Water, Is.Zero, "A quote captured before typing cannot spend money.");
                decimal waterCost = PortMarketCatalog.BuyUnitPrice(session.CurrentPort, PortMarketItem.Water, now) * 7;
                Assert.That(waterCost, Is.GreaterThan(0m).And.LessThan(1m));
                Assert.That(ActiveButton("Market Buy water").GetComponentInChildren<Text>().text,
                    Is.EqualTo("<size=22>购入 7</size>\n<size=22>−" + PortMarketCatalog.FormatPrice(waterCost) + " 金</size>"));
                Button.ButtonClickedEvent sevenQuote = ActiveButton("Market Buy water").onClick;
                ClickAcceptanceButton("Market Buy water"); yield return null;
                sevenQuote.Invoke(); yield return null;
                decimal expected = gold - foodCost - waterCost;
                Assert.That(session.Cargo.Water, Is.EqualTo(7));
                Assert.That(session.ProfileGold, Is.EqualTo(expected));
                Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(expected));
                AssertMarketQuantityHeader(expected);
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey(session.CurrentPort, now, PortMarketItem.Water)), Is.EqualTo(7));
                NavalBattleVisualCapture.Capture(controller, "MarketQuantity_02_Seven_Water_Exact_Gold.png",
                    "直接输入7水后一次点击购买，显示精确小数余额；重复旧回调不扣款", "Market Panel");
                SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
                controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(expected));
                Assert.That(controller.Session.Cargo.Water, Is.EqualTo(7));
                Assert.That(controller.Session.Cargo.Food, Is.EqualTo(37));
                AssertMarketQuantityHeader(expected);
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator MarketQuantity_AskTypedSevenEachDestinationCancelAndInvalidInputAreAtomic()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            var ledger = PreserveMarketStorageLedgers(now);
            try
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CoreVoyageSession session = controller.Session;
                SetProvisionClock(controller, () => now);
                ClickAcceptanceButton("Port Hotspot Market"); yield return null;
                yield return SelectMarketStorageMode(controller, MarketPurchaseStorageMode.Ask);
                decimal gold = session.ProfileGold;
                foreach (string invalid in new[] { "0", "-7", "9999999999" })
                {
                    yield return EnterMarketQuantity("water", invalid, false);
                    Assert.That(ActiveButton("Market Buy water").interactable, Is.False, invalid);
                    ActiveButton("Market Buy water").onClick.Invoke();
                    Assert.That(session.ProfileGold, Is.EqualTo(gold));
                    Assert.That(GameObject.Find("Market Purchase Choice"), Is.Null);
                }
                yield return EnterMarketQuantity("water", "0");
                Assert.That(MarketQuantityField("water").text, Is.EqualTo("0"));
                Assert.That(ActiveButton("Market Buy water").interactable, Is.False,
                    "Ending an edit cannot turn zero into a chargeable purchase.");
                yield return EnterMarketQuantity("water", "7");
                decimal waterCost = PortMarketCatalog.BuyUnitPrice(session.CurrentPort, PortMarketItem.Water, now) * 7;
                ClickAcceptanceButton("Market Buy water"); yield return null;
                Assert.That(session.ProfileGold, Is.EqualTo(gold));
                Assert.That(GameObject.Find("Market Purchase Choice Quote").GetComponent<Text>().text,
                    Does.Contain("×7").And.Contain(PortMarketCatalog.FormatPrice(waterCost)));
                Button.ButtonClickedEvent cancelled = ActiveButton("Market Buy To Warehouse").onClick;
                ClickAcceptanceButton("Cancel Market Purchase"); yield return null;
                cancelled.Invoke();
                Assert.That(session.ProfileGold, Is.EqualTo(gold));

                ClickAcceptanceButton("Market Buy water"); yield return null;
                Button.ButtonClickedEvent otherDestination = ActiveButton("Market Buy To Ship").onClick;
                ClickAcceptanceButton("Market Buy To Warehouse"); yield return null;
                otherDestination.Invoke();
                Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "water"), Is.EqualTo(7));
                Assert.That(session.Cargo.Water, Is.Zero);
                Assert.That(session.ProfileGold, Is.EqualTo(gold - waterCost));
                yield return EnterMarketQuantity("food", "7");
                decimal foodCost = PortMarketCatalog.BuyUnitPrice(session.CurrentPort, PortMarketItem.Food, now) * 7;
                ClickAcceptanceButton("Market Buy food"); yield return null;
                NavalBattleVisualCapture.Capture(controller, "MarketQuantity_03_Ask_Seven_Food.png",
                    "7份食物询问入库去向，显示完整精确金额，选择前不扣款", "Market Purchase Choice");
                ClickAcceptanceButton("Market Buy To Ship"); yield return null;
                decimal expected = gold - waterCost - foodCost;
                Assert.That(session.Cargo.Food, Is.EqualTo(7));
                Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "food"), Is.Zero);
                Assert.That(session.ProfileGold, Is.EqualTo(expected));
                Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(expected));
                AssertMarketQuantityHeader(expected);
                foreach (PortMarketItem item in new[] { PortMarketItem.Water, PortMarketItem.Food })
                    Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey(session.CurrentPort, now, item)), Is.EqualTo(7));
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }

        private static InputField MarketQuantityField(string key) =>
            GameObject.Find("Market Quantity Input " + key).GetComponent<InputField>();

        private static IEnumerator EnterMarketQuantity(string key, string quantity, bool endEdit = true)
        {
            yield return EnsureMarketItemVisible(key);
            InputField input = MarketQuantityField(key);
            yield return ClickDestinationTestGraphic(input.gameObject, Vector2.zero);
            Assert.That(input.isFocused, Is.True);
            input.ProcessEvent(Event.KeyboardEvent("^a"));
            input.ProcessEvent(Event.KeyboardEvent("backspace"));
            foreach (char character in quantity)
                input.ProcessEvent(new Event { type = EventType.KeyDown, character = character, keyCode = KeyCode.None });
            Assert.That(input.text, Is.EqualTo(quantity));
            if (endEdit) input.DeactivateInputField();
            yield return null;
        }

        private static void AssertMarketQuantityHeader(decimal expected)
        {
            GameObject readout = GameObject.Find("Port Facility Gold") ?? GameObject.Find("Port Hub Gold");
            Assert.That(readout, Is.Not.Null);
            Text gold = readout.GetComponent<Text>();
            Assert.That(gold.text, Does.EndWith(PortMarketCatalog.FormatPrice(expected)));
        }
    }
}
