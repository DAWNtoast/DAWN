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
        private static IEnumerator EnsureMarketItemVisible(string key)
        {
            int target = Enumerable.Range(0, PortMarketCatalog.AllItems.Count)
                .Single(index => PortMarketCatalog.Key(PortMarketCatalog.AllItems[index]) == key);
            for (int attempt = 0; GameObject.Find("Market Quantity Input " + key) == null && attempt < 10; attempt++)
            {
                int first = Enumerable.Range(0, PortMarketCatalog.AllItems.Count)
                    .First(index => GameObject.Find("Market Quantity Input " + PortMarketCatalog.Key(PortMarketCatalog.AllItems[index])) != null);
                string control = target < first ? "Market Previous Page" : "Market Next Page";
                Assert.That(ActiveButton(control)?.IsInteractable(), Is.True, "Reach " + key + " through market pagination.");
                ClickAcceptanceButton(control);
                yield return null;
            }
            Assert.That(GameObject.Find("Market Quantity Input " + key), Is.Not.Null, key);
        }

        private static IEnumerator EnsureMarketControlVisible(string name)
        {
            foreach (string action in new[] { "Market Buy ", "Market Sell ", "Market Increase ", "Market Decrease " })
            {
                if (!name.StartsWith(action, StringComparison.Ordinal)) continue;
                string key = name.Substring(action.Length);
                if (PortMarketCatalog.AllItems.Any(item => PortMarketCatalog.Key(item) == key))
                    yield return EnsureMarketItemVisible(key);
                yield break;
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator MarketPagination_KeepsQuantitiesModeAndLedgerWhileRejectingRetiredCallbacks()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            var ledger = PreserveMarketStorageLedgers(now);
            int? previousSize = GameReadability.OverrideLevel;
            try
            {
                GameReadability.OverrideLevel = 0;
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CoreVoyageSession session = controller.Session;
                SetProvisionClock(controller, () => now);
                ClickAcceptanceButton("Port Hotspot Market"); yield return null;
                yield return SelectMarketStorageMode(controller, MarketPurchaseStorageMode.Warehouse);
                yield return EnterMarketQuantity("water", "7");
                Button.ButtonClickedEvent retiredWater = ActiveButton("Market Buy water").onClick;
                Button.ButtonClickedEvent retiredNext = ActiveButton("Market Next Page").onClick;
                decimal before = session.ProfileGold;
                ClickAcceptanceButton("Market Next Page"); yield return null;
                Assert.That(GameObject.Find("Market Page Label").GetComponent<Text>().text, Is.EqualTo("第 2 / 3 页"));
                retiredWater.Invoke(); retiredNext.Invoke();
                Assert.That(session.ProfileGold, Is.EqualTo(before));
                Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "water"), Is.Zero);
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey(session.CurrentPort, now, PortMarketItem.Water)), Is.Zero);
                Assert.That(GameObject.Find("Market Page Label").GetComponent<Text>().text, Is.EqualTo("第 2 / 3 页"));
                yield return EnterMarketQuantity("wood", "7");
                decimal cost = PortMarketCatalog.CalculateBuyCost(session.CurrentPort, PortMarketItem.Wood, now, 7);
                ClickAcceptanceButton("Market Buy wood"); yield return null;
                Assert.That(GameObject.Find("Market Page Label").GetComponent<Text>().text, Is.EqualTo("第 2 / 3 页"), "Trading retains the displayed page.");
                Assert.That(session.ProfileGold, Is.EqualTo(before - cost));
                Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "wood"), Is.EqualTo(7));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey(session.CurrentPort, now, PortMarketItem.Wood)), Is.EqualTo(7));
                ClickAcceptanceButton("Market Next Page"); yield return null;
                Assert.That(ActiveButton("Market Next Page").interactable, Is.False);
                Assert.That(GameObject.Find("Market Row cannonballs"), Is.Not.Null);
                yield return EnsureMarketItemVisible("water");
                Assert.That(MarketQuantityField("water").text, Is.EqualTo("7"));
                Assert.That(controller.MarketStorageMode, Is.EqualTo(MarketPurchaseStorageMode.Warehouse));
                yield return EnterMarketQuantity("water", "0");
                yield return EnsureMarketItemVisible("wood");
                Assert.That(MarketQuantityField("wood").text, Is.EqualTo("7"));
                yield return EnsureMarketItemVisible("water");
                Assert.That(MarketQuantityField("water").text, Is.EqualTo("0"));
                Assert.That(ActiveButton("Market Buy water").interactable, Is.False, "Paging cannot convert a rejected input into a purchase.");
                yield return EnsureMarketItemVisible("cannonballs");
                Button.ButtonClickedEvent previousPort = ActiveButton("Market Buy cannonballs").onClick;
                ClickAcceptanceButton("Return To Port Hub"); yield return null;
                // Explicit location fixture verifies paging ownership, not navigation.
                session.RestoreProfile(session.ProfileGold, "白贝港");
                ClickAcceptanceButton("Port Hotspot Market"); yield return null;
                Assert.That(GameObject.Find("Market Page Label").GetComponent<Text>().text, Is.EqualTo("第 1 / 3 页"));
                previousPort.Invoke();
                Assert.That(session.ProfileGold, Is.EqualTo(before - cost));
                Assert.That(session.PortWarehouse.GetAmount("白贝港", "wood"), Is.Zero);
                LogAssert.NoUnexpectedReceived();
            }
            finally { GameReadability.OverrideLevel = previousSize; RestoreProvisionLedger(ledger); }
        }
    }
}
