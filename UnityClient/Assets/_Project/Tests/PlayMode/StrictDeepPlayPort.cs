using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        private sealed partial class StrictDeepDriver
        {
            private readonly Dictionary<string, int> tradeManifest = new Dictionary<string, int>();
            private readonly Dictionary<string, int> tradeBaseline = new Dictionary<string, int>();
            private int portPreparationIndex;

            // All mutations in this driver go through the existing rendered UI.
            // Reading cargo, price catalogues and visible quotas is observation,
            // never a licence to replace money, inventory or daily receipts.
            public IEnumerator EnsureHub()
            {
                Assert.That(Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
                foreach (string cancel in new[] { "Close Game Master", "Close Chart Seed Warning", "Cancel Market Purchase",
                    "Cancel Warehouse Cargo Adjustment", "Cancel Warehouse Cargo Sale", "Cancel Warehouse Item Actions",
                    "Cancel Component Sale" })
                    if (PortButton(cancel)?.IsInteractable() == true) yield return Click(cancel);
                if (PortButton("Port Hotspot Market") != null) yield break;
                if (PortButton("Back To Voyage Setup")?.IsInteractable() == true) yield return Click("Back To Voyage Setup");
                if (PortButton("Return To Port Hub")?.IsInteractable() == true) yield return Click("Return To Port Hub");
                if (PortButton("Confirm Port Investment") != null)
                    yield return Click("Return To Port Hub");
                Assert.That(PortButton("Port Hotspot Market"), Is.Not.Null, "Cannot reach the hub through current visible controls.");
            }

            private IEnumerator OpenStrictMarket()
            {
                if (PortButton("Market Shop Supplies") == null)
                {
                    yield return EnsureHub();
                    yield return Click("Port Hotspot Market");
                }
                // Cargo purchased for this voyage must actually board the ship.
                yield return OpenMarketTrade("water");
                for (int n = 0; controller.MarketStorageMode != MarketPurchaseStorageMode.Ship && n < 3; n++)
                    yield return Click("Market Detail Storage Mode");
                Assert.That(controller.MarketStorageMode, Is.EqualTo(MarketPurchaseStorageMode.Ship));
            }

            private static Button PortButton(string name) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .FirstOrDefault(button => button.gameObject.activeInHierarchy && button.name == name);

            private static Text PortText(string name) => UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None)
                .FirstOrDefault(label => label.gameObject.activeInHierarchy && label.name == name);

            private static PortMarketItem PortItem(string key) => PortMarketCatalog.AllItems.Single(item => PortMarketCatalog.Key(item) == key);

            private static int VisibleMarketRemaining(string key)
            {
                if (PortText("Market Quota " + key) == null)
                {
                    if (PortButton("Market Item Icon " + key) == null)
                    {
                        Button shop = PortButton("Market Shop " + MarketSceneLayout.ShopForItem(PortItem(key)));
                        Assert.That(shop, Is.Not.Null, "Read stock through the visible shop entrance: " + key);
                        Assert.That(shop.IsInteractable(), Is.True);
                        shop.onClick.Invoke();
                    }
                    Button item = PortButton("Market Item Icon " + key);
                    Assert.That(item, Is.Not.Null, "Read stock through the selected shop's visible item: " + key);
                    Assert.That(item.IsInteractable(), Is.True);
                    item.onClick.Invoke();
                }
                if (PortText("Market Quota " + key) == null)
                {
                    Button inventory = PortButton("Market Inventory Toggle");
                    Assert.That(inventory, Is.Not.Null, "Open the selected item's inventory through its visible control: " + key);
                    Assert.That(inventory.IsInteractable(), Is.True);
                    inventory.onClick.Invoke();
                }
                string caption = PortText("Market Quota " + key)?.text ?? string.Empty;
                Match match = Regex.Match(caption, @"可购\s+(\d+)");
                Assert.That(match.Success, Is.True, "No visible market allowance: " + caption);
                return int.Parse(match.Groups[1].Value);
            }

            private IEnumerator SetStrictMarketQuantity(string key, int target, bool purchase = true)
            {
                yield return OpenMarketTrade(key, purchase);
                Slider slider = MarketQuantityField(key);
                Assert.That(slider, Is.Not.Null);
                Assert.That(slider.IsInteractable(), Is.True);
                Assert.That(target, Is.InRange(0, Mathf.RoundToInt(slider.maxValue)));
                Record("Adjust actual market quantity slider " + key + "=" + target);
                slider.value = target;
                yield return null;
                Assert.That(slider.value, Is.EqualTo(target), "The visible market must retain the requested legal quantity.");
            }

            // Target is total carried inventory, not an injected increment.
            // Insufficient money, space or daily allowance is logged and retained.
            public IEnumerator BuyResource(string key, int target)
            {
                yield return OpenStrictMarket();
                PortMarketItem item = PortItem(key);
                int missing = Math.Max(0, target - Session.GetMarketCargoAmount(key));
                if (missing == 0) yield break;
                int fits = Math.Max(0, (int)Math.Floor((Session.CargoCapacityValue - Session.Cargo.UsedVolume + .001f) / PortMarketCatalog.CargoVolume(item)));
                int quantity = Math.Min(missing, Math.Min(VisibleMarketRemaining(key), fits));
                DateTime day = PricingDate;
                decimal unit = PortMarketCatalog.BuyUnitPrice(Session.CurrentPort, item, day);
                if (unit > 0m) quantity = (int)Math.Min(quantity, decimal.Floor(Session.ProfileGold / unit));
                if (quantity <= 0)
                {
                    Record("Provision unavailable: " + key + " target=" + target + ", carried=" + Session.GetMarketCargoAmount(key) +
                        ", quota=" + VisibleMarketRemaining(key) + ", hold=" + Session.Cargo.UsedVolume + "/" + Session.CargoCapacityValue);
                    yield break;
                }
                yield return BuyStrictMarketBatch(item, quantity, false);
            }

            private IEnumerator BuyStrictMarketBatch(PortMarketItem item, int quantity, bool trade)
            {
                string key = PortMarketCatalog.Key(item);
                yield return SetStrictMarketQuantity(key, quantity);
                if (PortButton("Confirm Market Buy " + key)?.IsInteractable() != true)
                {
                    Record("Displayed purchase is disabled; no override: " + key + " x" + quantity);
                    yield break;
                }
                int before = Session.GetMarketCargoAmount(key);
                decimal gold = Session.ProfileGold;
                decimal quotedCost = NoGmFirstTreasure ? PortMarketCatalog.CalculateBuyCost(Session.CurrentPort, item, PricingDate, quantity) : 0m;
                yield return Click("Confirm Market Buy " + key);
                int received = Session.GetMarketCargoAmount(key) - before;
                Assert.That(received, Is.EqualTo(quantity), "A real market purchase must deliver its displayed quantity.");
                Assert.That(Session.ProfileGold, Is.LessThan(gold));
                if (NoGmFirstTreasure) Assert.That(Session.ProfileGold, Is.EqualTo(gold - quotedCost), "The game-date quote must exactly match the real deduction.");
                if (trade)
                {
                    tradeManifest[key] = received;
                    tradeBaseline[key] = before;
                    if (NoGmFirstTreasure) { tradeOriginPort = Session.CurrentPort; PaidTradeUnitsBought += received; }
                }
                Record((trade ? "Trade purchase " : "Provision purchase ") + key + " x" + received +
                    "; paid=" + (gold - Session.ProfileGold) + "; port=" + Session.CurrentPort);
            }

            private IEnumerator BuyPairedRations(int target)
            {
                // Plan both sources together, then buy through the normal two UI
                // actions. Every last unit is useful; the default lot is not a minimum.
                int water = 0, food = 0;
                int waterLimit = Math.Min(Math.Max(0, target - Session.Cargo.Water), VisibleMarketRemaining("water"));
                int foodLimit = Math.Min(Math.Max(0, target - Session.Cargo.Food), VisibleMarketRemaining("food"));
                DateTime day = PricingDate;
                decimal waterPrice = PortMarketCatalog.BuyUnitPrice(Session.CurrentPort, PortMarketItem.Water, day);
                decimal foodPrice = PortMarketCatalog.BuyUnitPrice(Session.CurrentPort, PortMarketItem.Food, day);
                decimal cost = 0m;
                double volume = Session.Cargo.UsedVolume;
                while (water < waterLimit || food < foodLimit)
                {
                    bool addWater = Session.Cargo.Water + water <= Session.Cargo.Food + food;
                    if (Session.Cargo.Water + water >= target) addWater = false;
                    if (Session.Cargo.Food + food >= target) addWater = true;
                    if (addWater ? water >= waterLimit : food >= foodLimit) break;
                    decimal extraCost = addWater ? waterPrice : foodPrice;
                    double extraVolume = addWater ? .07d : .09d;
                    if (cost + extraCost > Session.ProfileGold || volume + extraVolume > Session.CargoCapacityValue + .001d) break;
                    if (addWater) water++; else food++;
                    cost += extraCost; volume += extraVolume;
                }
                Record("Paired actual ration plan: target=" + target + "; water +" + water + "; food +" + food +
                    "; exact quote=" + cost + "; loaded volume=" + volume.ToString("0.00", CultureInfo.InvariantCulture));
                if (water > 0) yield return BuyStrictMarketBatch(PortMarketItem.Water, water, false);
                if (food > 0) yield return BuyStrictMarketBatch(PortMarketItem.Food, food, false);
            }

            public IEnumerator PrepareAndDepart(int route, int destination, string recordedSeed = null)
            {
                Assert.That(route, Is.InRange(0, CoreVoyageSession.Routes.Count - 1));
                Assert.That(destination, Is.InRange(0, CoreVoyageSession.DestinationPorts.Count - 1));
                string targetPort = CoreVoyageSession.DestinationPorts[destination];
                Assert.That(targetPort, Is.Not.EqualTo(Session.CurrentPort));
                tradeManifest.Clear(); tradeBaseline.Clear();
                yield return OpenStrictMarket();

                // Reserve combat supplies before filling the hold with cargo.
                bool usesCannon = Session.GetBattleDeck(NavalBattleStage.Naval)
                    .Any(index => Session.GetBattleCard(index).Action == NavalBattleCardAction.Cannon);
                yield return BuyResource("wood", 10);
                yield return BuyResource("medicine", 3);
                yield return BuyResource("powder", usesCannon ? (route == 2 ? 10 : 6) : 3);
                yield return BuyResource("cannonballs", usesCannon ? (NoGmFirstTreasure ? RealTradeAudit.CannonballTarget : route == 2 ? 16 : 10) : 2);
                if (NoGmFirstTreasure && usesCannon)
                    Assert.That(Session.Cargo.Cannonballs, Is.GreaterThanOrEqualTo(RealTradeAudit.CannonballTarget),
                        "Do not repeat under-provisioned cannon battles: retain the actual stock/funds shortage.");

                // A paired purchase keeps neither water nor food crowding out
                // its counterpart. Use the full complement and currently available
                // cook; the displayed departure quote is recorded again before sailing.
                int days = route == 2 ? 20 : route == 0 ? 18 : 8;
                double daily = Session.EstimateDailyRationConsumption(Session.CrewCountValue, Session.HasCook);
                int target = (int)Math.Ceiling(daily * days);
                yield return BuyPairedRations(target);
                double rationDays = daily > 0 ? Math.Min(Session.Cargo.Water, Session.Cargo.Food) / daily : 0;
                Record("Departure reserve: requested=" + days + " days; full-crew/current-cook available=" + rationDays.ToString("0.00") +
                    "; actual water=" + Session.Cargo.Water + ", food=" + Session.Cargo.Food);
                // A limited hold is evidence of a provisioning problem, never
                // permission to create free cargo. Avoid buying trade goods if
                // the requested voyage reserve could not be assembled.
                if (rationDays >= days)
                {
                    DateTime day = PricingDate;
                    var candidates = PortMarketCatalog.AllItems.Where(item => item != PortMarketItem.Water && item != PortMarketItem.Food)
                        .Select(item => new { Item = item, Margin = PortMarketCatalog.SellUnitPrice(targetPort, item, day) -
                            PortMarketCatalog.BuyUnitPrice(Session.CurrentPort, item, day) })
                        .Where(candidate => candidate.Margin > 0m)
                        .OrderByDescending(candidate => candidate.Margin / (decimal)PortMarketCatalog.CargoVolume(candidate.Item)).ToArray();
                    foreach (var candidate in candidates)
                    {
                        string key = PortMarketCatalog.Key(candidate.Item);
                        int fits = Math.Max(0, (int)Math.Floor((Session.CargoCapacityValue - Session.Cargo.UsedVolume - 5f) /
                            PortMarketCatalog.CargoVolume(candidate.Item)));
                        int quantity = Math.Min(VisibleMarketRemaining(key), fits);
                        decimal budget = Math.Max(0, Session.ProfileGold - 80);
                        while (quantity > 0 && PortMarketCatalog.CalculateBuyCost(Session.CurrentPort, candidate.Item, day, quantity) > budget) quantity--;
                        decimal sale = 0m;
                        for (int remaining = quantity; remaining > 0; remaining -= 10)
                            sale += PortMarketCatalog.CalculateSellRevenue(targetPort, candidate.Item, day, Math.Min(10, remaining));
                        decimal purchase = PortMarketCatalog.CalculateBuyCost(Session.CurrentPort, candidate.Item, day, quantity);
                        if (quantity <= 0 || sale <= purchase) continue;
                        Record("Trade plan: " + Session.CurrentPort + " -> " + targetPort + "; " + key + " x" + quantity +
                            "; quoted purchase=" + purchase + "; sale in actual market lots=" + sale + "; excludes voyage costs and losses.");
                        yield return BuyStrictMarketBatch(candidate.Item, quantity, true);
                    }
                }
                Shot("Port_" + portPreparationIndex++ + "_Purchased", "Market Panel");
                yield return EnsureHub();
                yield return Click("Port Hotspot Voyage");
                yield return SetDepartureChartMode(false);
                yield return Click("Route " + route);
                yield return Click("Departure Destination " + destination);
                Assert.That(Session.SelectedDestinationPort, Is.EqualTo(targetPort));
                if (!string.IsNullOrEmpty(recordedSeed)) yield return EnterDepartureChartSeed(recordedSeed);
                Assert.That(Session.SelectedRouteIndex, Is.EqualTo(route), "The recorded chart must match the requested trading route.");
                Assert.That(Session.SelectedDestinationPort, Is.EqualTo(targetPort), "The chart must lead to the quoted sale port.");
                Shot("Port_Ready_To_Depart", "Voyage Setup");
                Record("Actual departure provision action: " + (PortButton("Departure Buy Provisions")?.GetComponentInChildren<Text>()?.text ?? "missing"));
                Assert.That(Session.Cargo.UsedVolume, Is.LessThanOrEqualTo(Session.CargoCapacityValue + .001f));
                Assert.That(Math.Min(Session.Cargo.Water, Session.Cargo.Food), Is.GreaterThanOrEqualTo(Session.CrewCountValue), "Stop automatic departure: insufficient real water/food; preserve port for investment or normal stocking.");
                Assert.That(PortButton("Start Voyage")?.IsInteractable(), Is.True, "The real departure button rejects this voyage.");
                yield return Click("Start Voyage");
                Assert.That(Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
                Record("Departed by UI: " + Session.Map.DepartureName + " -> " + Session.Map.DestinationName + "; seed=" + Session.Map.Seed);
            }

            public IEnumerator SellCargo()
            {
                yield return OpenStrictMarket();
                foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                {
                    if (item == PortMarketItem.Water || item == PortMarketItem.Food) continue;
                    string key = PortMarketCatalog.Key(item);
                    bool usesCannon = Session.GetBattleDeck(NavalBattleStage.Naval)
                        .Any(index => Session.GetBattleCard(index).Action == NavalBattleCardAction.Cannon);
                    int reserve = key == "wood" ? 10 : key == "medicine" ? 3 : key == "beer" ? 4 :
                        key == "powder" ? (usesCannon ? 10 : 3) : key == "cannonballs" ? (usesCannon ? 16 : 2) : 0;
                    int available = Math.Max(0, Session.GetMarketCargoAmount(key) - reserve);
                    if (tradeManifest.TryGetValue(key, out int manifested))
                        Record("Manifest arrival " + key + ": purchased=" + manifested + "; remaining above starting inventory=" +
                            Math.Min(manifested, Math.Max(0, Session.GetMarketCargoAmount(key) - tradeBaseline[key])));
                    int soldTotal = 0;
                    int arrivedTradeUnits = tradeManifest.TryGetValue(key, out int purchased) ?
                        Math.Min(purchased, Math.Max(0, Session.GetMarketCargoAmount(key) - tradeBaseline[key])) : 0;
                    decimal revenue = 0m;
                    for (int n = 0; available > 0 && n < 1000; n++)
                    {
                        int quantity = Math.Min(10, available);
                        yield return SetStrictMarketQuantity(key, quantity, false);
                        quantity = Mathf.RoundToInt(MarketQuantityField(key).value);
                        if (quantity <= 0 || quantity > available) break;
                        if (PortButton("Confirm Market Sell " + key)?.IsInteractable() != true) break;
                        int before = Session.GetMarketCargoAmount(key);
                        decimal gold = Session.ProfileGold;
                        yield return Click("Confirm Market Sell " + key);
                        int sold = before - Session.GetMarketCargoAmount(key);
                        Assert.That(sold, Is.EqualTo(quantity));
                        soldTotal += sold; revenue += Session.ProfileGold - gold; available -= sold;
                    }
                    if (NoGmFirstTreasure && Session.CurrentPort != tradeOriginPort)
                        CrossPortTradeUnitsSold += Math.Min(soldTotal, arrivedTradeUnits);
                    if (soldTotal > 0) Record("Real market sale " + key + " x" + soldTotal + "; received=" + revenue +
                        "; retained=" + Session.GetMarketCargoAmount(key) + "; includes earned cargo where present, no resources granted.");
                }
                tradeManifest.Clear(); tradeBaseline.Clear();
                Shot("Port_Cargo_Sold", "Market Panel");
                yield return EnsureHub();
            }
        }
    }
}


