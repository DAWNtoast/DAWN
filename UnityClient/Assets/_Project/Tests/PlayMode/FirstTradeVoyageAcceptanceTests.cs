using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
        [UnityTest, Timeout(1200000)]
        public IEnumerator NewGame_FirstTradeVoyage_CompletesThroughRealActions()
        {
            var ledger = new Dictionary<string, int?>();
            foreach (string port in CoreVoyageSession.DestinationPorts)
                for (int day = 0; day < 2; day++)
                    foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                    {
                        string key = CoreGameplayDemoController.MarketPurchaseKey(port, CoreVoyageSession.GameClockOrigin.AddMonths(day), item);
                        ledger[key] = PlayerPrefs.HasKey(key) ? (int?)PlayerPrefs.GetInt(key) : null;
                        PlayerPrefs.DeleteKey(key);
                    }
            var audit = new RealTradeAudit();
            var routines = new Stack<IEnumerator>();
            routines.Push(RunFirstTradeVoyage(audit));
            try
            {
                // Flatten so an assertion in any nested UI action can save the
                // actual failure state before propagating to the test runner.
                while (routines.Count > 0)
                {
                    object yielded = null;
                    bool moved;
                    try
                    {
                        IEnumerator routine = routines.Peek();
                        moved = routine.MoveNext();
                        if (moved) yielded = routine.Current;
                        else { (routine as IDisposable)?.Dispose(); routines.Pop(); }
                    }
                    catch (Exception error)
                    {
                        audit.failure = error.ToString();
                        audit.Record("FAILED: " + error.Message, null);
                        audit.Shot("Failure", "Phase Content");
                        audit.Save();
                        throw;
                    }
                    if (!moved) continue;
                    if (yielded is IEnumerator nested) { routines.Push(nested); continue; }
                    yield return yielded;
                }
            }
            finally
            {
                while (routines.Count > 0) (routines.Pop() as IDisposable)?.Dispose();
                audit.Save();
                RestoreProvisionLedger(ledger);
            }
        }

        private IEnumerator RunFirstTradeVoyage(RealTradeAudit audit)
        {
            yield return NewGame_FirstTreasureMap_TraversesRealTutorialAndCapturesScenes();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            audit.controller = controller;
            CoreVoyageSession session = controller.Session;
            Assert.That(controller.IsTutorialActive, Is.False);
            Assert.That(session.SelectedShipIndex, Is.Zero, "Keep the actual starter sloop.");
            audit.initialProfileGold = (double)(session.ProfileGold);
            audit.originPort = session.CurrentPort;
            PortMarketItem tradeItem;
            int desiredQuantity;
            if (audit.originPort == "白贝港")
            { tradeItem = PortMarketItem.Beer; desiredQuantity = 60; audit.destinationPort = "断锚港"; }
            else if (audit.originPort == "断锚港")
            { tradeItem = PortMarketItem.Wood; desiredQuantity = 30; audit.destinationPort = "白贝港"; }
            else throw new AssertionException("No reviewed starter trade manifest for actual tutorial port: " + audit.originPort);
            Assert.That(audit.destinationPort, Is.Not.EqualTo(audit.originPort));
            audit.tradeItemKey = PortMarketCatalog.Key(tradeItem);
            audit.tradeItemName = PortMarketCatalog.Name(tradeItem);
            audit.tradeCargoBeforePurchase = session.GetMarketCargoAmount(audit.tradeItemKey);
            audit.Record("Real new-game tutorial completed; no GM or profile relocation", null);
            audit.Shot("Tutorial_Completed", "Treasure Panel");
            yield return audit.Click("Return To Port Hub");
            yield return audit.Click("Port Hotspot Market");

            // Read the actual market quota and hold. Keep tutorial food and the
            // original four beer; the White Shell specialty needs only 4.8 hold.
            string quotaText = GameObject.Find("Market Quota " + audit.tradeItemKey).GetComponent<Text>().text;
            Match quota = Regex.Match(quotaText, @"可购\s+(\d+)");
            Assert.That(quota.Success, Is.True, "Read the visible purchase allowance: " + quotaText);
            int freeHoldUnits = (int)Math.Floor((session.CargoCapacityValue - session.Cargo.UsedVolume + .001f) / PortMarketCatalog.CargoVolume(tradeItem));
            int ammunitionNeeded = Math.Max(0, RealTradeAudit.CannonballTarget - session.Cargo.Cannonballs);
            decimal ammunitionQuote = PortMarketCatalog.CalculateBuyCost(session.CurrentPort, PortMarketItem.Cannonballs,
                session.GameDateTime, ammunitionNeeded);
            decimal preparationReserve = RealTradeAudit.RequiredCannonUpgradeCost(session) + ammunitionQuote + 60m;
            int affordableTradeUnits = (int)Math.Max(0m, decimal.Floor((session.ProfileGold - preparationReserve) /
                PortMarketCatalog.BuyUnitPrice(session.CurrentPort, tradeItem, session.GameDateTime)));
            int tradeUnits = Math.Min(desiredQuantity, Math.Min(affordableTradeUnits,
                Math.Min(int.Parse(quota.Groups[1].Value), freeHoldUnits)));
            Assert.That(tradeUnits, Is.GreaterThan(0), "No real quota or hold for the origin port's specialty.");
            yield return audit.MarketQuantity(audit.tradeItemKey, tradeUnits);
            audit.Shot("Cargo_Purchase_Quote", "Market Panel");
            decimal purchaseGold = session.ProfileGold;
            yield return audit.Click("Market Buy " + audit.tradeItemKey);
            audit.tradeQuantityPurchased = session.GetMarketCargoAmount(audit.tradeItemKey) - audit.tradeCargoBeforePurchase;
            audit.tradePurchaseCost = (double)(purchaseGold - session.ProfileGold);
            Assert.That(audit.tradeQuantityPurchased, Is.EqualTo(tradeUnits));
            audit.Record("Trade manifest from actual port " + audit.originPort + ": purchased " + tradeUnits + " " + audit.tradeItemKey + "; " + quotaText, null);
            audit.Shot("Cargo_Purchased", "Market Panel");
            if (ammunitionNeeded > 0)
            {
                yield return audit.MarketQuantity("cannonballs", ammunitionNeeded);
                decimal ammunitionGold = session.ProfileGold;
                yield return audit.Click("Market Buy cannonballs");
                Assert.That(session.Cargo.Cannonballs, Is.EqualTo(RealTradeAudit.CannonballTarget));
                Assert.That(session.ProfileGold, Is.EqualTo(ammunitionGold - ammunitionQuote));
                audit.ammunitionCost = (double)(ammunitionGold - session.ProfileGold);
            }

            yield return audit.Click("Return To Port Hub");
            yield return audit.PrepareCannonDefense();
            yield return audit.Click("Port Hotspot Voyage");
            yield return audit.Click("Route 1");
            int destinationIndex = Enumerable.Range(0, CoreVoyageSession.DestinationPorts.Count)
                .Single(index => CoreVoyageSession.DestinationPorts[index] == audit.destinationPort);
            yield return audit.Click("Departure Destination " + destinationIndex);
            Assert.That(session.SelectedRouteIndex, Is.EqualTo(1));
            Assert.That(session.SelectedDestinationPort, Is.EqualTo(audit.destinationPort));
            Assert.That(session.SelectedDestinationPort, Is.Not.EqualTo(session.CurrentPort));
            string tradeSeed = CoreVoyageSession.CreateChartSeed(1, audit.originPort, audit.destinationPort);
            yield return audit.SelectSeed(tradeSeed);
            // Buy missing route provisions only when the actual joint quote fits
            // money, hold and quota. A disabled quote is evidence, not permission
            // to replace cargo or money; the player may still choose to sail.
            Button provisions = ActiveButton("Departure Buy Provisions");
            if (provisions != null && provisions.IsInteractable())
            {
                decimal gold = session.ProfileGold;
                yield return audit.Click("Departure Buy Provisions");
                audit.provisionCost += (double)(gold - session.ProfileGold);
            }
            else audit.Record("Departure provisions unavailable/already sufficient: " +
                GameObject.Find("Departure Provision Quote")?.GetComponent<Text>()?.text, null);
            audit.Shot("Departure_Trade_Manifest", "Voyage Setup");
            audit.tradeCargoAtDeparture = session.GetMarketCargoAmount(audit.tradeItemKey);
            audit.goldBeforeDeparture = (double)(session.ProfileGold);
            audit.componentsBeforeVoyage = RealTradeAudit.ComponentSnapshot(controller);
            yield return audit.Click("Start Voyage");
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.Map.Seed, Is.EqualTo(tradeSeed));
            audit.Shot("Trade_Sea_Start", "Navigation Chart");
            yield return audit.SailToDestination();

            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            if (ActiveButton("Skip Settlement Reveal")?.IsInteractable() == true)
                yield return audit.Click("Skip Settlement Reveal");
            audit.settlementOutcome = session.Settlement.Outcome.ToString();
            audit.settlementGoldReward = session.Settlement.BankedGold;
            audit.settlementWoodReward = session.Settlement.BankedWood;
            audit.settlementProtection = session.Settlement.ProtectionRatio;
            audit.Record("Actual settlement; reward gold is separate from market sale proceeds", null);
            audit.Shot("Trade_Actual_Settlement", "Settlement Card");
            bool arrived = session.Settlement.Outcome == VoyageOutcome.ArrivedAtPort && session.Settlement.Destination == audit.destinationPort;
            yield return audit.Click("Return To Port");
            audit.Shot("Trade_Returned_Port", "Phase Content");
            Assert.That(arrived && session.CurrentPort == audit.destinationPort, Is.True,
                "The attempt did not reach " + audit.destinationPort + ". Its real outcome and costs are preserved; no success state was injected.\n" +
                audit.FailureSummary());
            Assert.That(session.BossDefeated, Is.True, "The route blockade must have been defeated through real cards.");
            audit.tradeCargoAtArrival = session.GetMarketCargoAmount(audit.tradeItemKey);
            yield return audit.Click("Port Hotspot Market");
            audit.Shot("Destination_Cargo_Sale_Quote", "Market Panel");
            int remainingManifest = Math.Min(audit.tradeQuantityPurchased,
                Math.Max(0, audit.tradeCargoAtArrival - audit.tradeCargoBeforePurchase));
            while (remainingManifest > 0)
            {
                int amount = Math.Min(10, remainingManifest);
                yield return audit.MarketQuantity(audit.tradeItemKey, amount);
                decimal gold = session.ProfileGold;
                int cargo = session.GetMarketCargoAmount(audit.tradeItemKey);
                yield return audit.Click("Market Sell " + audit.tradeItemKey);
                int sold = cargo - session.GetMarketCargoAmount(audit.tradeItemKey);
                audit.tradeSaleRevenue += (double)(session.ProfileGold - gold);
                audit.tradeQuantitySold += sold;
                remainingManifest -= sold;
                Assert.That(sold, Is.EqualTo(amount), "A sale must remove exactly the displayed real cargo.");
            }
            audit.Shot("Trade_Sale_Complete", "Market Panel");
            audit.Record("Trade cargo sold; starting cargo retained and settlement rewards excluded from commodity proceeds", null);

            var originalIds = new HashSet<string>(audit.componentsBeforeVoyage.Select(item => item.instanceId));
            OwnedShipComponent loot = controller.OwnedComponents.FirstOrDefault(item => item.ShipIndex == -1 && !originalIds.Contains(item.InstanceId));
            if (loot == null)
                audit.Record("No new uninstalled component from this voyage; no old tutorial component sold", null);
            else
            {
                audit.componentSaleInstanceId = loot.InstanceId;
                yield return audit.Click("Return To Port Hub");
                yield return audit.Click("Port Hotspot ComponentShop");
                yield return audit.Click("Open Component Resale");
                for (int page = 0; ActiveButton("Select Component Sale " + loot.InstanceId) == null && page < controller.OwnedComponents.Count; page++)
                    yield return audit.Click("Component Resale Next");
                Text name = GameObject.Find("Component Resale Name " + loot.InstanceId)?.GetComponent<Text>();
                Assert.That(name, Is.Not.Null, "The new loot must be visibly listed before selecting it.");
                audit.componentSaleName = name.text.Split('\n')[0];
                decimal gold = session.ProfileGold;
                int price = controller.OwnedComponentSalePrice(loot.InstanceId);
                audit.Shot("New_Loot_Component_Resale", "Component Resale Panel");
                yield return audit.Click("Select Component Sale " + loot.InstanceId);
                audit.Shot("New_Loot_Component_Confirmation", "Component Sale Confirmation");
                Assert.That(session.ProfileGold, Is.EqualTo(gold), "Selecting the loot must not sell it before confirmation.");
                yield return audit.Click("Confirm Component Sale");
                audit.componentSaleRevenue = (double)(session.ProfileGold - gold);
                Assert.That(audit.componentSaleRevenue, Is.EqualTo(price));
                Assert.That(controller.OwnedComponents.Any(item => item.InstanceId == loot.InstanceId), Is.False);
                Assert.That(originalIds.IsSubsetOf(controller.OwnedComponents.Select(item => item.InstanceId)), Is.True,
                    "Every component owned before this voyage must be retained.");
                audit.Shot("New_Loot_Component_Sold", "Component Resale Panel");
                audit.Record("Sold only this voyage's new idle component: " + audit.componentSaleName, null);
            }

            decimal expectedGold = session.ProfileGold;
            int expectedCargo = session.GetMarketCargoAmount(audit.tradeItemKey);
            audit.componentsAfterSales = RealTradeAudit.ComponentSnapshot(controller);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            audit.controller = controller;
            session = controller.Session;
            Assert.That(session.ProfileGold, Is.EqualTo(expectedGold), "All trade, settlement and component proceeds must persist exactly once.");
            Assert.That(session.CurrentPort, Is.EqualTo(audit.destinationPort));
            Assert.That(session.GetMarketCargoAmount(audit.tradeItemKey), Is.EqualTo(expectedCargo));
            Assert.That(controller.OwnedComponents.Count, Is.EqualTo(audit.componentsAfterSales.Count));
            foreach (RealTradeComponent expected in audit.componentsAfterSales)
            {
                OwnedShipComponent actual = controller.OwnedComponents.Single(item => item.InstanceId == expected.instanceId);
                Assert.That(actual.ComponentId, Is.EqualTo(expected.componentId));
                Assert.That(actual.Quality, Is.EqualTo(expected.quality));
                Assert.That(actual.ShipIndex, Is.EqualTo(expected.shipIndex));
            }
            audit.reloadVerified = true;
            audit.completed = true;
            audit.Record("Completed real trade voyage and reload verification; profit/loss is reported without a profitability assertion", null);
            audit.Shot("Trade_Reload_Verified", "Phase Content");
            audit.Save();
            LogAssert.NoUnexpectedReceived();
        }

        [Serializable]
        private sealed class RealTradeAudit
        {
            public string scope = "Actual new-game tutorial then starter-sloop trade voyage, with paid cannon preparation and legal deck editing; no GM, profile relocation, direct combat-state edits or hidden-node targeting.";
            public string attribution = "Commodity sale quantity is capped at the purchased manifest and reserves starting cargo. Commodity margin, provision purchases, voyage settlement rewards and the sale of one newly acquired idle component are reported separately. Consumed inherited supplies and battle wear remain visible in actions, not assigned an invented monetary cost.";
            public bool completed, reloadVerified;
            public string failure = "", settlementOutcome = "", originPort = "", destinationPort = "",
                tradeItemKey = "", tradeItemName = "", componentSaleInstanceId = "", componentSaleName = "";
            // JsonUtility report numbers only; all live transaction assertions use decimal.
            public double initialProfileGold, goldBeforeDeparture, finalProfileGold, tradePurchaseCost,
                tradeSaleRevenue, tradeMarginBeforeProvisions, tradeMarginAfterProvisionPurchases, provisionCost, componentSaleRevenue,
                ammunitionCost, cardPreparationCost;
            public int tradeQuantityPurchased, tradeQuantitySold, tradeCargoBeforePurchase, tradeCargoAtDeparture,
                tradeCargoAtArrival, settlementGoldReward, settlementWoodReward;
            public float settlementProtection;
            public List<RealTradeComponent> componentsBeforeVoyage = new List<RealTradeComponent>();
            public List<RealTradeComponent> componentsAfterSales = new List<RealTradeComponent>();
            public List<RealTradeAction> actions = new List<RealTradeAction>();
            [NonSerialized] public CoreGameplayDemoController controller;
            [NonSerialized] private int shotIndex, encounterIndex;
            private CoreVoyageSession Session => controller.Session;
            public const int CannonballTarget = 16;

            public static decimal RequiredCannonUpgradeCost(CoreVoyageSession session)
            {
                decimal cost = 0;
                for (int level = session.GetBattleCard(0).UpgradeLevel; level < 2; level++)
                    cost += CoreBattleUpgradeBalance.CostForNextLevel(level);
                return cost;
            }

            public static List<RealTradeComponent> ComponentSnapshot(CoreGameplayDemoController source) =>
                source.OwnedComponents.Select(item => new RealTradeComponent {
                    instanceId = item.InstanceId, componentId = item.ComponentId,
                    quality = item.Quality, shipIndex = item.ShipIndex
                }).ToList();

            public RealTradeState State()
            {
                if (controller == null) controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                if (controller == null) return new RealTradeState { phase = SceneManager.GetActiveScene().name };
                var s = Session;
                return new RealTradeState {
                    phase = s.Phase.ToString(), port = s.CurrentPort, destination = s.SelectedDestinationPort,
                    encounter = s.ActiveEncounter?.Id ?? "", seed = s.Map?.Seed ?? s.SelectedSeed,
                    profileGold = (double)s.ProfileGold, voyageGold = s.Cargo.Gold, water = s.Cargo.Water, food = s.Cargo.Food,
                    wood = s.Cargo.Wood, beer = s.GetMarketCargoAmount("beer"), medicine = s.Cargo.Medicine, powder = s.Cargo.Powder, cannonballs = s.Cargo.Cannonballs,
                    iron = s.Cargo.Iron, cloth = s.Cargo.Cloth, hull = s.Hull, maxHull = s.MaxHull,
                    ship = s.SelectedShipIndex, route = s.SelectedRouteIndex, crew = s.EffectiveCrewCount,
                    cargoVolume = s.Cargo.UsedVolume, capacity = s.CargoCapacityValue, hours = s.TotalHours,
                    distance = s.TotalDistanceNm, x = s.Position.X, y = s.Position.Y, bossDefeated = s.BossDefeated,
                    battleStage = s.Battle?.Stage.ToString() ?? "", battleTurn = s.Battle?.Turn ?? 0,
                    enemyHull = s.Battle?.EnemyHull ?? 0, enemyCrew = s.Battle?.EnemyBoardingHealth ?? 0,
                    playerCrewHealth = s.Battle?.PlayerBoardingHealth ?? 0, battleDistance = s.Battle?.DistanceMeters ?? 0,
                    gameLog = string.Join("\n", s.LogEntries.Skip(Math.Max(0, s.LogEntries.Count - 8))),
                    ownedComponentIds = string.Join(",", controller.OwnedComponents.Select(item => item.InstanceId))
                };
            }
            public void Record(string action, RealTradeState before)
            {
                actions.Add(new RealTradeAction { sequence = actions.Count, realtime = Time.realtimeSinceStartup,
                    action = action, before = before, after = State() });
                Save();
            }
            public string FailureSummary()
            {
                RealTradeAction departure = actions.LastOrDefault(item => item.action == "Click: Start Voyage");
                return "Outcome=" + settlementOutcome + "; origin=" + originPort + "; expected=" + destinationPort +
                    "; provisionsCost=" + provisionCost + "\nDeparture=" +
                    (departure == null ? "not recorded" : JsonUtility.ToJson(departure.after)) +
                    "\nRecent actions:\n" + string.Join("\n", actions.Skip(Math.Max(0, actions.Count - 12))
                        .Select(item => item.action + " => " + JsonUtility.ToJson(item.after)));
            }
            public void Save()
            {
                if (controller != null) finalProfileGold = (double)Session.ProfileGold;
                tradeMarginBeforeProvisions = tradeSaleRevenue - tradePurchaseCost;
                tradeMarginAfterProvisionPurchases = tradeMarginBeforeProvisions - provisionCost;
                string[] args = Environment.GetCommandLineArgs();
                int auditFlag = Array.IndexOf(args, "-auditActionLogPath");
                if (auditFlag >= 0)
                {
                    Assert.That(auditFlag + 1 < args.Length && !args[auditFlag + 1].StartsWith("-", StringComparison.Ordinal),
                        Is.True, "-auditActionLogPath requires an output directory.");
                    string auditDirectory = Path.GetFullPath(args[auditFlag + 1]);
                    Directory.CreateDirectory(auditDirectory);
                    File.WriteAllText(Path.Combine(auditDirectory, "trade-actions.json"), JsonUtility.ToJson(this, true), new UTF8Encoding(false));
                    return;
                }
                int flag = Array.IndexOf(args, "-navalVisualCapturePath");
                if (flag < 0 || flag + 1 >= args.Length) return;
                string directory = Path.GetFullPath(args[flag + 1]);
                // The shared screenshot helper validates and creates the artifact
                // directory during the real tutorial, before any trade action.
                if (!Directory.Exists(directory)) return;
                File.WriteAllText(Path.Combine(directory, "actions.json"), JsonUtility.ToJson(this, true), new UTF8Encoding(false));
            }
            public void Shot(string key, string root)
            {
                if (controller == null) controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                if (controller == null || GameObject.Find(root) == null) return;
                NavalBattleVisualCapture.Capture(controller, "Trade_" + (shotIndex++).ToString("000") + "_" + key + ".png",
                    "真实新游戏贸易航行 · " + key, root);
            }
            public IEnumerator Click(string name)
            {
                yield return EnsureMarketControlVisible(name);
                RealTradeState before = State();
                Record("Attempt click: " + name, before);
                ClickAcceptanceButton(name);
                yield return null;
                float deadline = Time.realtimeSinceStartup + 15;
                while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(controller.IsBattleAnimationPlaying, Is.False, name + " animation timeout");
                // Sailing commits the encounter before its marker animation
                // renders the corresponding controls. Follow the presented UI.
                yield return WaitForNavigationMotionCompletion(controller);
                yield return null;
                Record("Click: " + name, before);
            }
            public IEnumerator MarketQuantity(string key, int target)
            {
                yield return EnsureMarketItemVisible(key);
                for (int n = 0; n < 100; n++)
                {
                    int current = int.Parse(GameObject.Find("Market Quantity " + key).GetComponent<Text>().text);
                    if (current == target) yield break;
                    yield return Click("Market " + (current < target ? "Increase " : "Decrease ") + key);
                }
                Assert.Fail("Market quantity did not reach " + target);
            }
            public IEnumerator SelectSeed(string seed)
            {
                RealTradeState before = State();
                yield return EnterDepartureChartSeed(seed);
                Assert.That(Session.IsChartSeedSelected, Is.True);
                Assert.That(GameObject.Find("Seed Input").GetComponent<InputField>().text, Is.EqualTo(seed));
                Record("Enter complete chart seed through visible input: " + seed, before);
            }
            public IEnumerator PrepareCannonDefense()
            {
                // The small starter crew has weak boarding effects. Spend only
                // earned money, then configure a legal ranged/repair deck via UI.
                yield return Click("Port Hotspot CardWorkshop");
                yield return Click("Open Deck Chest 1");
                yield return Click("Select Upgrade Card 0");
                while (Session.GetBattleCard(0).UpgradeLevel < 2)
                {
                    int level = Session.GetBattleCard(0).UpgradeLevel;
                    int price = Session.GetCardUpgradeCost(0);
                    decimal gold = Session.ProfileGold;
                    yield return Click("Confirm Battle Card Upgrade");
                    Assert.That(Session.GetBattleCard(0).UpgradeLevel, Is.EqualTo(level + 1));
                    Assert.That(Session.ProfileGold, Is.EqualTo(gold - price));
                    cardPreparationCost += price;
                }
                yield return Click("Edit Deck From Card Upgrades");
                yield return ConfigureDeck(NavalBattleStage.Preparation, new[] { 8, 8, 8, 9, 9, 9, 7, 7 });
                yield return ConfigureDeck(NavalBattleStage.Naval, new[] { 0, 0, 0, 11, 11, 11, 5, 5 });
                yield return Click("Back To Card Workshop Chests");
                yield return Click("Return To Port Hub");
                Assert.That(Session.ProfileGold, Is.GreaterThanOrEqualTo(60m), "Keep the ordinary departure cash reserve after preparation.");
                Record("Paid cannon preparation complete; ammunition and upgrades accounted separately", null);
            }
            private IEnumerator ConfigureDeck(NavalBattleStage stage, int[] desired)
            {
                yield return Click("Battle Deck Stage Tab " + stage);
                for (int change = 0; change < 30; change++)
                {
                    var current = Session.GetBattleDeck(stage).ToList();
                    int extra = current.Where(i => current.Count(v => v == i) > desired.Count(v => v == i)).DefaultIfEmpty(-1).First();
                    int missing = desired.Where(i => desired.Count(v => v == i) > current.Count(v => v == i)).DefaultIfEmpty(-1).First();
                    if (extra < 0 && missing < 0) break;
                    if (extra >= 0 && current.Count > 8)
                        yield return Click("Remove Deck Card " + extra);
                    else if (missing >= 0 && current.Count < 10)
                    {
                        for (int page = 0; ActiveButton("Add Deck Card " + missing) == null && page < 10; page++)
                            yield return Click(ActiveButton("Next Battle Card Collection Page")?.IsInteractable() == true
                                ? "Next Battle Card Collection Page" : "Previous Battle Card Collection Page");
                        yield return Click("Add Deck Card " + missing);
                    }
                    else Assert.Fail("No legal deck edit can progress toward " + stage);
                }
                CollectionAssert.AreEquivalent(desired, Session.GetBattleDeck(stage), "Deck edits must commit exactly the visible selection.");
            }
            public IEnumerator SailToDestination()
            {
                var visits = new Dictionary<string, int>();
                var probed = new HashSet<int>();
                bool searchingPortApproach = false;
                for (int step = 0; step < 250 && Session.Phase != CoreDemoPhase.Settlement; step++)
                {
                    if (Session.Phase == CoreDemoPhase.Battle) { yield return Battle(); continue; }
                    if (Session.Phase == CoreDemoPhase.Encounter)
                    {
                        string key = Session.ActiveEncounter.Id + ":" + Session.Position.X + ":" + Session.Position.Y;
                        visits.TryGetValue(key, out int count); visits[key] = count + 1;
                        Assert.That(count, Is.LessThan(4), "The same unresolved encounter keeps reopening.");
                        yield return Encounter(); continue;
                    }
                    Assert.That(Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
                    CoreMapNode visibleBoss = Session.Map.Nodes.FirstOrDefault(n => n.IsChartVisible && n.IsBoss && !n.Visited);
                    if (!Session.BossDefeated && Distance(Session.Position, Session.Map.Destination) <= 5)
                        searchingPortApproach = true;
                    if (!Session.BossDefeated && visibleBoss != null)
                        yield return Click("Event Node " + visibleBoss.Id);
                    else if (Session.BossDefeated || !searchingPortApproach)
                        yield return Click("Destination Port");
                    else
                    {
                        // Look for the blockade near the public destination. This
                        // inspects explored flags, never undiscovered encounter IDs
                        // or their coordinates, and uses ordinary route previews.
                        int next = Enumerable.Range(0, Session.Map.CellCount)
                            .Where(i => !probed.Contains(i) && !Session.Map.ExploredCells[i])
                            .Where(i => Distance(Session.Map.CellCenter(i % CoreVoyageMap.GridColumns, i / CoreVoyageMap.GridColumns), Session.Map.Destination) < 20)
                            .OrderBy(i => Distance(Session.Position, Session.Map.CellCenter(i % CoreVoyageMap.GridColumns, i / CoreVoyageMap.GridColumns)))
                            .DefaultIfEmpty(-1).First();
                        Assert.That(next, Is.GreaterThanOrEqualTo(0), "No visible boss and no nearby unobserved port approach remains.");
                        probed.Add(next);
                        VoyagePoint point = Session.Map.CellCenter(next % CoreVoyageMap.GridColumns, next / CoreVoyageMap.GridColumns);
                        RealTradeState before = State();
                        Vector2 local = new Vector2(Mathf.Lerp(-580, 580, point.X / 100f), Mathf.Lerp(-315, 315, point.Y / 64f));
                        yield return ClickDestinationTestGraphic(GameObject.Find("Map Course Input"), local);
                        Record("Chart exploration click near public port: " + point.X + "," + point.Y, before);
                    }
                    if (Session.Phase != CoreDemoPhase.Sailing) continue;
                    SailingForecast forecast = Session.GetForecast();
                    Record("Visible route forecast: " + forecast.ActualDistanceNm + " nm, " + forecast.Hours +
                        " hours, water " + forecast.WaterCost + ", food " + forecast.FoodCost + ", blocked=" + forecast.Blocked, null);
                    if (step % 5 == 0) Shot("Navigation_" + step, "Navigation Chart");
                    Assert.That(forecast.Blocked, Is.False, "The selected public route has no navigable preview.");
                    // No forced return or provision injection: executing the real
                    // route retains the production rescue/defeat rules if exhausted.
                    yield return Click("Execute Sail");
                }
                Assert.That(Session.Phase, Is.EqualTo(CoreDemoPhase.Settlement), "Trade voyage exceeded its bounded navigation budget.");
            }
            private IEnumerator Encounter()
            {
                CoreEncounter encounter = Session.ActiveEncounter;
                Shot("Encounter_" + (encounterIndex++) + "_" + encounter.Id, "Encounter Card");
                if (!encounter.Mandatory && ActiveButton("Skip Encounter")?.IsInteractable() == true &&
                    !encounter.Choices.Any(c => c.Effect.Water > 0 || c.Effect.Food > 0 || c.Effect.Hull > 0))
                { yield return Click("Skip Encounter"); yield break; }
                int choice = Enumerable.Range(0, encounter.Choices.Count).Where(Session.IsChoiceAvailable)
                    .OrderByDescending(i => {
                        CoreEncounterChoice c = encounter.Choices[i];
                        return (encounter.BossKind == CoreBossKind.Route && c.StartsBattle ? 100000 : 0) +
                            Math.Max(0, c.Effect.Water) + Math.Max(0, c.Effect.Food) + c.Effect.Hull * 4 - c.Hours * 3 -
                            (c.StartsBattle ? 1000 : 0) - (c.Effect.ForceReturn ? 100000 : 0);
                    }).DefaultIfEmpty(-1).First();
                Assert.That(choice, Is.GreaterThanOrEqualTo(0), "No available real encounter choice: " + encounter.Id);
                Record("Visible encounter decision: " + encounter.Id + " / " + encounter.Choices[choice].Label, null);
                yield return Click("Encounter Choice " + choice);
            }
            private IEnumerator Battle()
            {
                Shot("Battle_Start", "Phase Content");
                string lastTurn = ""; int playedInTurn = 0;
                for (int action = 0; action < 350 && Session.Phase == CoreDemoPhase.Battle; action++)
                {
                    CoreBattleState battle = Session.Battle;
                    string turn = battle.Stage + ":" + battle.Turn;
                    if (turn != lastTurn) { lastTurn = turn; playedInTurn = 0; Shot("Battle_" + turn.Replace(':', '_'), "Phase Content"); }
                    int selected = -1; double best = 0;
                    if (playedInTurn++ < 24)
                        for (int hand = 0; hand < battle.Hand.Count; hand++)
                        {
                            if (!Session.CanPlayCard(hand)) continue;
                            double score = TradeBattleCardScore(battle.Hand[hand]);
                            if (score > best) { best = score; selected = hand; }
                        }
                    yield return Click(selected >= 0 ? "Battle Card " + selected : "End Battle Turn");
                }
                Record("Battle ended through real card actions", null);
                Shot("Battle_Result", "Phase Content");
                Assert.That(Session.Phase, Is.Not.EqualTo(CoreDemoPhase.Battle), "Battle exceeded legal-action budget.");
            }

            public double TradeBattleCardScore(int cardIndex)
            {
                CoreBattleState battle = Session.Battle;
                CoreBattleCard card = Session.GetBattleCard(cardIndex);
                CoreBattleCardEffects effects = Session.GetBattleCardEffects(card, battle.Stage);
                bool boarding = battle.Stage == NavalBattleStage.Boarding;
                bool preparing = battle.Stage == NavalBattleStage.Preparation;
                int health = boarding ? battle.PlayerBoardingHealth : Session.Hull;
                int maxHealth = boarding ? battle.PlayerMaxBoardingHealth : Session.MaxHull;
                int enemyHealth = boarding ? battle.EnemyBoardingHealth : battle.EnemyHull;
                Match intent = Regex.Match(Session.GetEnemyIntent() ?? "", "(?:炮击：伤害 |白刃突击：船员伤害 )(\\d+)");
                int incoming = intent.Success ? int.Parse(intent.Groups[1].Value) : 0;
                int defenseNeed = Math.Max(0, (preparing ? 14 : incoming) - battle.PlayerBlock);
                int repair = Math.Min(maxHealth - health, effects.Repair);
                int block = Math.Min(defenseNeed, effects.Block + (effects.Block > 0 && !boarding ? battle.PlayerDeckBuff : 0));
                double score = repair * (health < maxHealth * .55 ? 2.5 : .65) + block * (health <= incoming * 2 ? 2 : .9);
                double divisor = Session.GetBattleCardEnergyCost(cardIndex) + .65;
                int damage = effects.Damage + (card.Action == NavalBattleCardAction.Cannon
                    ? Math.Max(0, (Session.EffectiveFirepower - 8) / 2) + battle.PlayerLoadedShot : 0);
                if (damage >= enemyHealth + battle.EnemyBlock) return 10000 / divisor;
                if (!preparing && !boarding && card.Action == NavalBattleCardAction.Cannon)
                {
                    // Fire while in range if the displayed hand still affords
                    // an escape. A free veteran-navigator retreat must not move
                    // every turn outside cannon range before a free shot.
                    int escapeCost = battle.Hand.Where(i => Session.GetBattleCard(i).Action == NavalBattleCardAction.Retreat)
                        .Select(Session.GetBattleCardEnergyCost).DefaultIfEmpty(int.MaxValue).Min();
                    if (Session.GetBattleCardEnergyCost(cardIndex) <= battle.Energy - escapeCost)
                        return 1000 / divisor;
                }
                if (!preparing) score += damage * 1.7;
                if (card.Draw > 0 && battle.Hand.Count < 7) score += card.Draw * (battle.Hand.Count < 4 ? 6 : 2);
                bool evadeRam = !boarding && !preparing && Session.EnemyWillRam;
                bool maneuver = card.Action == NavalBattleCardAction.Approach || card.Action == NavalBattleCardAction.Circle ||
                    card.Action == NavalBattleCardAction.Turn || card.Action == NavalBattleCardAction.Navigator || card.Action == NavalBattleCardAction.Parallel;
                if (evadeRam && maneuver) return 0;
                switch (card.Action)
                {
                    case NavalBattleCardAction.Polish:
                        if (battle.PlayerDeckBuff < card.StrategyLimit) score += preparing ? 35 : 9;
                        break;
                    case NavalBattleCardAction.Load:
                    case NavalBattleCardAction.PrecisionLoad:
                        if (battle.PlayerLoadedShot < card.StrategyLimit) score += preparing ? 24 : 8;
                        break;
                    case NavalBattleCardAction.Approach:
                    case NavalBattleCardAction.Circle:
                    case NavalBattleCardAction.Turn:
                        if (battle.DistanceMeters > 350) score += 40;
                        else if (battle.Alignment == NavalBattleAlignment.HeadOn && battle.DistanceMeters <= 100 && card.Action != NavalBattleCardAction.Approach) score += 14;
                        break;
                    case NavalBattleCardAction.Navigator:
                    case NavalBattleCardAction.Parallel:
                        if (battle.DistanceMeters > 350) score += 44;
                        else if (battle.Alignment == NavalBattleAlignment.HeadOn && battle.DistanceMeters <= 100) score += 14;
                        break;
                    case NavalBattleCardAction.Retreat:
                        if (!preparing && !boarding && battle.DistanceMeters <= 350)
                            return (battle.DistanceMeters + card.MovementMeters > 350 ? 85 : evadeRam ? 110 : 35) / divisor;
                        return 0;
                    case NavalBattleCardAction.FireRounds:
                        if (battle.PlayerFireRounds == 0) score += 18;
                        break;
                }
                if (preparing && score <= card.Draw * 6 && battle.PlayerDeckBuff >= 6 &&
                    battle.PlayerLoadedShot >= 12 && battle.DistanceMeters <= 350 && battle.PlayerBlock >= 14) return 0;
                return score / divisor;
            }
            private static float Distance(VoyagePoint a, VoyagePoint b) => Vector2.Distance(new Vector2(a.X, a.Y), new Vector2(b.X, b.Y));
        }

        [Serializable] private sealed class RealTradeAction
        { public int sequence; public float realtime; public string action; public RealTradeState before, after; }
        [Serializable] private sealed class RealTradeComponent
        { public string instanceId, componentId; public int shipIndex; public float quality; }
        [Serializable] private sealed class RealTradeState
        {
            public string phase, port, destination, encounter, seed, battleStage, gameLog, ownedComponentIds;
            // Display-only JSON field; gameplay balances retain decimal precision.
            public double profileGold;
            public int voyageGold, water, food, wood, beer, medicine, powder, cannonballs, iron, cloth,
                hull, maxHull, ship, route, crew, battleTurn, enemyHull, enemyCrew, playerCrewHealth;
            public float cargoVolume, capacity, distance, x, y, battleDistance;
            public double hours;
            public bool bossDefeated;
        }
    }
}
