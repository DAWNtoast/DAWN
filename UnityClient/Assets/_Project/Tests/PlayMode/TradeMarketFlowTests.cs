using System;
using System.Collections;
using System.Collections.Generic;
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
        // This is an isolated market UI quotation/transaction test. The port
        // switch is a fixture arrangement, not an assertion of sailing a route.
        [UnityTest, Timeout(120000)]
        public IEnumerator TradeMarket_LocalResaleLosesMoneyAndImportedWoodPaysImmediatelyThenPersists()
        {
            const string sourcePort = "断锚港", destinationPort = "白贝港";
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            Dictionary<string, int?> ledger = PreserveTradeMarketLedger(now);
            try
            {
                yield return LoadProvisioningFixture();
                CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                SetProvisionClock(controller, () => now);
                Assert.That(controller.Session.CurrentPort, Is.EqualTo(sourcePort));
                Assert.That(controller.Session.Cargo.Wood, Is.Zero);
                Assert.That(controller.Session.Cargo.Gold, Is.Zero);
                ClickAcceptanceButton("Port Hotspot Market"); yield return null;

                yield return OpenMarketTrade("wood");
                Assert.That(MarketQuantityField("wood").value, Is.EqualTo(1));
                yield return EnterMarketQuantity("wood", "40");
                decimal initialGold = controller.Session.ProfileGold;
                decimal buyCost = PortMarketCatalog.CalculateBuyCost(sourcePort, PortMarketItem.Wood, now, 40);
                decimal localSale = PortMarketCatalog.CalculateSellRevenue(sourcePort, PortMarketItem.Wood, now, 10);
                decimal importedSale = PortMarketCatalog.CalculateSellRevenue(destinationPort, PortMarketItem.Wood, now, 10);
                Assert.That(localSale, Is.LessThan(PortMarketCatalog.CalculateBuyCost(sourcePort, PortMarketItem.Wood, now, 10)),
                    "Buying and immediately reselling in the same port must lose money.");
                Assert.That(importedSale, Is.GreaterThan(PortMarketCatalog.CalculateBuyCost(sourcePort, PortMarketItem.Wood, now, 10)),
                    "The importing port must pay more than this producing port charges.");
                Assert.That(GameObject.Find("Market Quantity wood").GetComponent<Text>().text, Is.EqualTo("40"));
                Assert.That(GameObject.Find("Market Trade Total").GetComponent<Text>().text,
                    Does.Contain(PortMarketCatalog.FormatPrice(buyCost)));
                CaptureTradeMarket(controller, "01_Source_Quote", "断锚港木材 40 单位的实际购买报价");

                ClickAcceptanceButton("Confirm Market Buy wood"); yield return null;
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(initialGold - buyCost));
                Assert.That(controller.Session.Cargo.Wood, Is.EqualTo(40));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey(sourcePort, now, PortMarketItem.Wood)), Is.EqualTo(40));
                Assert.That(MarketInfoLabel("Market Quota wood").text, Does.Contain("已购 40"));
                CaptureTradeMarket(controller, "02_Source_Purchase", "实际按钮购入木材 40，扣款并计入当月额度");

                yield return EnterMarketQuantity("wood", "10", purchase: false);
                Assert.That(GameObject.Find("Market Trade Total").GetComponent<Text>().text,
                    Does.Contain(PortMarketCatalog.FormatPrice(localSale)));
                ClickAcceptanceButton("Confirm Market Sell wood"); yield return null;
                decimal afterLocalSale = initialGold - buyCost + localSale;
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(afterLocalSale));
                Assert.That(controller.Session.Cargo.Wood, Is.EqualTo(30));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey(sourcePort, now, PortMarketItem.Wood)), Is.EqualTo(40),
                    "Selling cannot replenish the monthly purchase allowance.");
                CaptureTradeMarket(controller, "03_Local_Resale", "本港回售 10 单位按较低买回价入账，采购额度不返还");

                ClickAcceptanceButton("Return To Port Hub"); yield return null;
                // Explicit fixture-only relocation: no hidden sailing, GM money,
                // or traversal/arrival claim is made by this transaction test.
                controller.Session.RestoreProfile(afterLocalSale, destinationPort);
                ClickAcceptanceButton("Port Hotspot Market"); yield return null;
                Assert.That(GameObject.Find("Port Facility Port").GetComponent<Text>().text, Does.Contain(destinationPort));
                yield return EnterMarketQuantity("wood", "10", purchase: false);
                Assert.That(GameObject.Find("Market Trade Total").GetComponent<Text>().text,
                    Does.Contain(PortMarketCatalog.FormatPrice(importedSale)));
                CaptureTradeMarket(controller, "04_Import_Quote", "夹具切换至白贝港后的高价收购报价；未模拟航路");

                for (int sale = 1; sale <= 3; sale++)
                {
                    decimal goldBefore = controller.Session.ProfileGold;
                    yield return EnterMarketQuantity("wood", "10", purchase: false);
                    ClickAcceptanceButton("Confirm Market Sell wood"); yield return null;
                    Assert.That(controller.Session.ProfileGold, Is.EqualTo(goldBefore + importedSale),
                        "Market proceeds go directly to the profile at the displayed price; do not apply voyage's 95% protection.");
                    Assert.That(controller.Session.Cargo.Wood, Is.EqualTo(30 - sale * 10));
                    Assert.That(controller.Session.Cargo.Gold, Is.Zero, "Market income must not remain pending in voyage gold.");
                    Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
                    Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(controller.Session.ProfileGold));
                    Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.CargoKey("Wood")), Is.EqualTo(30 - sale * 10));
                }
                decimal finalGold = afterLocalSale + 3 * importedSale;
                Assert.That(finalGold, Is.GreaterThan(initialGold), "Exporting the remaining wood must offset the deliberate local-resale loss.");
                yield return OpenMarketItem("wood");
                yield return OpenMarketTrade("wood", false);
                Assert.That(ActiveButton("Confirm Market Sell wood").IsInteractable(), Is.False);
                AssertTradeMarketLedgers(now);
                CaptureTradeMarket(controller, "05_Imported_Sale", "白贝港卖出余下 30 单位，三笔足额即时入金且货物清零");

                SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
                yield return null; yield return null;
                controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                SetProvisionClock(controller, () => now);
                Assert.That(controller.Session.CurrentPort, Is.EqualTo(destinationPort));
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(finalGold));
                Assert.That(controller.Session.Cargo.Wood, Is.Zero);
                Assert.That(controller.Session.Cargo.Gold, Is.Zero);
                Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
                AssertTradeMarketLedgers(now);
                ClickAcceptanceButton("Port Hotspot Market"); yield return null;
                yield return OpenMarketItem("wood");
                yield return OpenMarketTrade("wood", false);
                Assert.That(ActiveButton("Confirm Market Sell wood").IsInteractable(), Is.False);
                CaptureTradeMarket(controller, "06_Reload_Persistence", "重载保留白贝港、全部交易金币、空木材库存及两港采购记录");
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }

        private static Dictionary<string, int?> PreserveTradeMarketLedger(DateTime day)
        {
            var ledger = new Dictionary<string, int?>();
            foreach (string port in new[] { "断锚港", "白贝港" })
                for (int offset = 0; offset < 2; offset++)
                    foreach (PortMarketItem item in new[] { PortMarketItem.Wood, PortMarketItem.Water, PortMarketItem.Food })
                    {
                        string key = CoreGameplayDemoController.MarketPurchaseKey(port, day.AddMonths(offset), item);
                        ledger[key] = PlayerPrefs.HasKey(key) ? (int?)PlayerPrefs.GetInt(key) : null;
                        PlayerPrefs.DeleteKey(key);
                    }
            return ledger;
        }

        private static void AssertTradeMarketLedgers(DateTime day)
        {
            foreach (string port in new[] { "断锚港", "白贝港" })
                foreach (PortMarketItem item in new[] { PortMarketItem.Wood, PortMarketItem.Water, PortMarketItem.Food })
                {
                    int expected = port == "断锚港" && item == PortMarketItem.Wood ? 40 : 0;
                    Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey(port, day, item)), Is.EqualTo(expected),
                        port + " / " + item + " purchase ledger");
                }
        }

        private static void CaptureTradeMarket(CoreGameplayDemoController controller, string stage, string detail)
        {
            NavalBattleVisualCapture.Capture(controller, "TradeMarket_" + stage + ".png",
                "市场 UI 报价与事务夹具 · " + detail, "Market Panel");
        }
    }
}
