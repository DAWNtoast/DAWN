using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        // Opt-in bounded progression audit. The existing fixture preserves the
        // player's profile. After New Game, only ordinary UI actions mutate it.
        // Tutorial helpers use simulated Button/Slider input; this is PlayMode
        // acceptance, not an OS-input or standalone-Player performance claim.
        [UnityTest, Timeout(1860000)]
        public IEnumerator StrictEconomy_FirstCraftedTreasure_NoGm_RealUI()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-noGmFirstTreasureAuditPath");
            if (at < 0) Assert.Ignore("Opt-in: supply -noGmFirstTreasureAuditPath <new absolute directory>; bounded to 40 actual departures / 30 minutes.");
            Assert.That(at + 1, Is.LessThan(args.Length));
            Assert.That(Path.IsPathRooted(args[at + 1]), Is.True, "Use an absolute audit directory.");
            Assert.That(Array.IndexOf(args, "-strictResumePath"), Is.LessThan(0), "No checkpoint import in a fresh no-GM acceptance.");
            Assert.That(Array.IndexOf(args, "-strictShipPlan"), Is.LessThan(0), "The comparative ship audit has different stop and GM rules.");
            string output = Path.GetFullPath(args[at + 1]);
            Assert.That(Directory.Exists(output) && Directory.GetFileSystemEntries(output).Length > 0, Is.False,
                "Use an empty directory; existing progression evidence is never replaced.");
            Directory.CreateDirectory(output);
            var audit = new NoGmFirstTreasureAudit { startedUtc = DateTime.UtcNow.ToString("O") };
            double start = Time.realtimeSinceStartupAsDouble;
            var observedMaps = new HashSet<CoreVoyageMap>();
            StrictDeepDriver driver = null;
            var routines = new Stack<IEnumerator>();
            routines.Push(NoGmFirstTreasureStory(output, audit, value => driver = value));
            try
            {
                while (routines.Count > 0)
                {
                    bool moved;
                    object yielded = null;
                    try
                    {
                        audit.elapsedSeconds = Time.realtimeSinceStartupAsDouble - start;
                        if (driver != null)
                        {
                            audit.paidTradeUnitsBought = driver.PaidTradeUnitsBought;
                            audit.crossPortTradeUnitsSold = driver.CrossPortTradeUnitsSold;
                        }
                        Assert.That(audit.elapsedSeconds, Is.LessThan(1800d), "No-GM progression exceeded its 30-minute wall-clock budget; retain the actual shortage/route evidence.");
                        CoreGameplayDemoController current = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                        if (current != null && current.Session != null)
                        {
                            Assert.That(current.IsGameMasterOpen, Is.False, "The no-GM acceptance must never enter the GM panel.");
                            if (current.Session.Phase == CoreDemoPhase.Sailing && observedMaps.Add(current.Session.Map))
                            {
                                audit.departures++;
                                File.AppendAllText(Path.Combine(output, "departures.jsonl"), JsonUtility.ToJson(new NoGmDeparture {
                                    number = audit.departures, seed = current.Session.Map.Seed,
                                    gameDate = current.Session.GameDateTime.ToString("O"), tutorial = current.IsTutorialActive,
                                    port = current.Session.CurrentPort, treasure = current.Session.IsTreasureVoyage }) + "\n");
                                Assert.That(audit.departures, Is.LessThanOrEqualTo(40), "Actual departures, including the tutorial, must stay within 40.");
                            }
                        }
                        IEnumerator routine = routines.Peek();
                        moved = routine.MoveNext();
                        if (moved) yielded = routine.Current;
                        else { (routine as IDisposable)?.Dispose(); routines.Pop(); }
                    }
                    catch (Exception error)
                    {
                        audit.failure = error.ToString();
                        try { driver?.Record("NO-GM FAILED: " + error.Message); } catch { /* Keep the original assertion. */ }
                        throw;
                    }
                    if (!moved) continue;
                    if (yielded is IEnumerator nested) routines.Push(nested);
                    else yield return yielded;
                }
                audit.passed = true;
            }
            finally
            {
                while (routines.Count > 0) (routines.Pop() as IDisposable)?.Dispose();
                audit.elapsedSeconds = Time.realtimeSinceStartupAsDouble - start;
                audit.completedUtc = DateTime.UtcNow.ToString("O");
                File.WriteAllText(Path.Combine(output, "no-gm-first-treasure-result.json"), JsonUtility.ToJson(audit, true));
            }
        }

        private IEnumerator NoGmFirstTreasureStory(string output, NoGmFirstTreasureAudit audit, Action<StrictDeepDriver> setDriver)
        {
            yield return NewGame_FirstTreasureMap_TraversesRealTutorialAndCapturesScenes();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var driver = new StrictDeepDriver(controller, output, null, true);
            setDriver(driver);
            CoreVoyageSession session = controller.Session;
            int tutorialExperience = session.PirateExperience;
            Assert.That(session.TreasureMaps, Is.Empty, "The first crafted map must originate in this run.");
            Assert.That(session.CompletedTreasureSeeds, Is.Empty);
            driver.Record("NO-GM START: tutorial completed through ordinary controls; no external checkpoint, gold, stock, rank or seed injection.");
            while (audit.departures < 40 && driver.completedRanks.Count == 0)
            {
                driver.Record("NO-GM PORT CYCLE " + driver.cycles + "; actual departures=" + audit.departures + "; game date=" + session.GameDateTime.ToString("O"));
                yield return driver.SellCargo();
                yield return driver.PrepareNoGmCannonBuild();
                yield return driver.BuyLargerLocalShip();
                yield return driver.RecruitTreasureCrew();
                // The starter's ranged build is paid before merchandise; the
                // full-campaign boarding upgrades would consume this budget.
                yield return driver.InvestForSuppliesIfNeeded();
                yield return driver.IdentifyEarnedTreasureFragments();
                if (driver.CrossPortTradeUnitsSold > 0 && driver.FirstCraftedMap == null &&
                    AcceptanceReadyTreasureBoard(session) >= 0 && session.PirateLevel >= 2)
                {
                    yield return driver.CraftFirstNoGmMap();
                    audit.firstMapId = driver.FirstCraftedMap.Id;
                    audit.firstMapSeed = driver.FirstCraftedMap.Seed;
                    audit.firstMapRank = driver.FirstCraftedMap.RankId;
                }
                bool treasure = false;
                if (driver.FirstCraftedMap != null)
                {
                    yield return driver.PrepareFirstNoGmTreasure();
                    treasure = driver.FirstTreasureReady;
                }
                string origin = session.CurrentPort;
                if (treasure)
                {
                    yield return driver.DepartFirstNoGmTreasure();
                    yield return driver.Navigate("treasure");
                    Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
                    Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.TreasureRecovered),
                        "The first crafted map must succeed; a later replacement map cannot hide its failure.");
                    Assert.That(session.Settlement.TreasureCompleted, Is.True);
                    Assert.That(session.ActiveTreasureMap.Seed, Is.EqualTo(audit.firstMapSeed));
                }
                else
                {
                    int port = Enumerable.Range(0, CoreVoyageSession.DestinationPorts.Count).Single(i => CoreVoyageSession.DestinationPorts[i] == origin);
                    int destination = new[] { 2, 0, 3, 4, 5, 1 }[port];
                    bool explore = driver.FirstCraftedMap == null && !driver.MustTradeAfterSupplyInvestment &&
                        driver.CrossPortTradeUnitsSold > 0 && session.ProfileGold > 1200 && driver.cycles % 3 != 0;
                    int route = explore ? 0 : 1;
                    string seed = explore ? null : driver.FindEarnedChart(port, destination, route);
                    yield return driver.PrepareAndDepart(route, destination, seed);
                    yield return driver.Navigate(explore ? "explore" : "trade");
                }
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement), "A real departure must finish in a real settlement.");
                bool arrived = session.Settlement.Outcome == VoyageOutcome.ArrivedAtPort;
                bool failed = session.Settlement.Outcome == VoyageOutcome.Failure;
                yield return driver.Settle();
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
                if (arrived && session.CurrentPort != origin) audit.crossPortArrivals++;
                driver.cycles++;
                driver.Checkpoint();
                audit.consecutiveFailedVoyages = failed ? audit.consecutiveFailedVoyages + 1 : 0;
                driver.Record("NO-GM settled failure streak=" + audit.consecutiveFailedVoyages);
                Assert.That(audit.consecutiveFailedVoyages, Is.LessThan(3),
                    "Three consecutive real voyage failures: stop this strategy and retain actual funds, stock, equipment and combat evidence; do not change balance or inject a victory.");
            }
            audit.paidTradeUnitsBought = driver.PaidTradeUnitsBought;
            audit.crossPortTradeUnitsSold = driver.CrossPortTradeUnitsSold;
            Assert.That(driver.FirstCraftedMap, Is.Not.Null, "No map crafted within the real progression budget; inspect funds, fragments, levels and stock in the action log.");
            Assert.That(driver.completedRanks.Count, Is.EqualTo(1), "The first treasure was not completed within 40 actual departures.");
            Assert.That(audit.crossPortArrivals, Is.GreaterThan(0));
            Assert.That(audit.paidTradeUnitsBought, Is.GreaterThan(0));
            Assert.That(audit.crossPortTradeUnitsSold, Is.GreaterThan(0), "Trade must include purchased cargo sold at another reached port.");
            Assert.That(session.PirateExperience, Is.GreaterThan(tutorialExperience), "Growth must be earned after the tutorial.");
            Assert.That(session.CompletedTreasureSeeds.Count(seed => seed == audit.firstMapSeed), Is.EqualTo(1));
            Assert.That(session.TreasureMaps.Any(map => map.Id == audit.firstMapId || map.Seed == audit.firstMapSeed), Is.False);
            driver.Record("NO-GM FIRST TREASURE BANKED; normal save/continue verification follows.");
            string treasureBefore = JsonUtility.ToJson(session.CaptureTreasureProgress());
            string cargoBefore = NoGmCargoFingerprint(session.Cargo);
            decimal goldBefore = session.ProfileGold;
            double clockBefore = session.ElapsedGameHours;
            int shipBefore = session.SelectedShipIndex;
            string portBefore = session.CurrentPort;
            string scopeBefore = PlayerPrefs.GetString(CoreGameplayDemoController.WorldEconomyScopeKey);
            // Use the visible save/return/continue route, never a Prefs replay or
            // direct Core restore. A second reload detects duplicate backfills.
            for (int reload = 0; reload < 2; reload++)
            {
                ClickAcceptanceButton("Port Hub Settings"); yield return null;
                ClickAcceptanceButton("Return To Main Menu"); yield return null;
                ClickAcceptanceButton("Confirm Return To Main Menu"); yield return null; yield return null;
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
                ClickAcceptanceButton("Continue Game"); yield return null; yield return null;
                controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(controller, Is.Not.Null);
                session = controller.Session;
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
                Assert.That(session.CurrentPort, Is.EqualTo(portBefore));
                Assert.That(session.SelectedShipIndex, Is.EqualTo(shipBefore));
                Assert.That(session.ProfileGold, Is.EqualTo(goldBefore));
                Assert.That(session.ElapsedGameHours, Is.EqualTo(clockBefore));
                Assert.That(NoGmCargoFingerprint(session.Cargo), Is.EqualTo(cargoBefore));
                Assert.That(JsonUtility.ToJson(session.CaptureTreasureProgress()), Is.EqualTo(treasureBefore));
                Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WorldEconomyScopeKey), Is.EqualTo(scopeBefore));
                Assert.That(session.CompletedTreasureSeeds.Count(seed => seed == audit.firstMapSeed), Is.EqualTo(1));
                Assert.That(session.TreasureMaps.Any(map => map.Id == audit.firstMapId || map.Seed == audit.firstMapSeed), Is.False);
                audit.reloadsVerified++;
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static string NoGmCargoFingerprint(VoyageCargo cargo) => string.Join(",", new[] {
            cargo.Water, cargo.Food, cargo.Beer, cargo.Medicine, cargo.Wood, cargo.Iron, cargo.Cloth,
            cargo.Powder, cargo.Cannonballs, cargo.Gold, cargo.Fame, cargo.Gem, cargo.AncientMaterial,
            cargo.Relic, cargo.BlackPearl, cargo.AncientCompass, cargo.Lantern, cargo.Curse });

        private sealed partial class StrictDeepDriver
        {
            public bool NoGmFirstTreasure { get; }
            private DateTime PricingDate => NoGmFirstTreasure ? Session.GameDateTime : DateTime.Now.Date;
            private string tradeOriginPort;
            public int PaidTradeUnitsBought { get; private set; }
            public int CrossPortTradeUnitsSold { get; private set; }
            public CoreTreasureMapRecord FirstCraftedMap { get; private set; }
            public bool FirstTreasureReady { get; private set; }
            private RealTradeAudit noGmCombatReference;

            public IEnumerator PrepareNoGmCannonBuild()
            {
                Assert.That(NoGmFirstTreasure, Is.True);
                int[] preparation = { 8, 8, 8, 9, 9, 9, 7, 7 };
                int[] naval = { 0, 0, 0, 11, 11, 11, 5, 5 };
                bool prepared = Session.GetBattleCard(0).UpgradeLevel >= 2 &&
                    preparation.OrderBy(i => i).SequenceEqual(Session.GetBattleDeck(NavalBattleStage.Preparation).OrderBy(i => i)) &&
                    naval.OrderBy(i => i).SequenceEqual(Session.GetBattleDeck(NavalBattleStage.Naval).OrderBy(i => i));
                if (prepared) yield break;
                yield return EnsureHub();
                decimal upgrades = RealTradeAudit.RequiredCannonUpgradeCost(Session);
                decimal ammunition = PortMarketCatalog.CalculateBuyCost(Session.CurrentPort, PortMarketItem.Cannonballs,
                    PricingDate, Math.Max(0, RealTradeAudit.CannonballTarget - Session.Cargo.Cannonballs));
                decimal repairSupplies = PortMarketCatalog.CalculateBuyCost(Session.CurrentPort, PortMarketItem.Wood,
                    PricingDate, Math.Max(0, 10 - Session.Cargo.Wood)) +
                    PortMarketCatalog.CalculateBuyCost(Session.CurrentPort, PortMarketItem.Medicine,
                        PricingDate, Math.Max(0, 3 - Session.Cargo.Medicine));
                Record("NO-GM paid ranged preparation quote: card0 to level2=" + upgrades + "; ammunition to16=" + ammunition +
                    "; repair supplies=" + repairSupplies + "; departure cash reserve=60; actual gold=" + Session.ProfileGold);
                Assert.That(Session.ProfileGold, Is.GreaterThanOrEqualTo(upgrades + ammunition + repairSupplies + 60m),
                    "Earned funds cannot cover the starter's legal ranged build and ordinary departure reserve.");
                yield return Click("Port Hotspot CardWorkshop"); yield return Click("Workshop Preview Stage Naval"); yield return Click("Open Battle Card Upgrades");
                yield return Click("Select Upgrade Card 0");
                while (Session.GetBattleCard(0).UpgradeLevel < 2)
                {
                    int level = Session.GetBattleCard(0).UpgradeLevel;
                    int cost = Session.GetCardUpgradeCost(0);
                    decimal before = Session.ProfileGold;
                    yield return Click("Confirm Battle Card Upgrade");
                    Assert.That(Session.GetBattleCard(0).UpgradeLevel, Is.EqualTo(level + 1));
                    Assert.That(Session.ProfileGold, Is.EqualTo(before - cost));
                }
                yield return Click("Back From Card Upgrades"); yield return Click("Edit Stage Deck Naval");
                yield return SetDeck(NavalBattleStage.Preparation, preparation);
                yield return SetDeck(NavalBattleStage.Naval, naval);
                CollectionAssert.AreEquivalent(preparation, Session.GetBattleDeck(NavalBattleStage.Preparation));
                CollectionAssert.AreEquivalent(naval, Session.GetBattleDeck(NavalBattleStage.Naval));
                yield return Click("Back To Card Workshop Chests"); yield return EnsureHub();
                Record("NO-GM legal ranged build ready: card0 level=" + Session.GetBattleCard(0).UpgradeLevel +
                    "; paid=" + upgrades + "; actual gold=" + Session.ProfileGold + "; no boarding rush.");
            }

            private IEnumerator FightNoGmCannon()
            {
                // Reuse the currently passing trade acceptance's public-intent
                // scorer. Only this mode changes strategy; actual cards, costs,
                // range checks, damage and enemy turns remain production UI.
                if (noGmCombatReference == null) noGmCombatReference = new RealTradeAudit { controller = controller };
                CoreBattleState fought = Session.Battle;
                string lastTurn = string.Empty;
                int playedInTurn = 0, actions = 0;
                for (; actions < 350 && Session.Phase == CoreDemoPhase.Battle; actions++)
                {
                    CoreBattleState battle = Session.Battle;
                    string turn = battle.Stage + ":" + battle.Turn;
                    if (turn != lastTurn)
                    {
                        lastTurn = turn; playedInTurn = 0;
                        Record("NO-GM ranged battle intent: " + Session.GetEnemyIntent() + "; distance=" + battle.DistanceMeters +
                            "; cannonballs=" + Session.Cargo.Cannonballs + "; powder=" + Session.Cargo.Powder);
                    }
                    int selected = -1;
                    double best = 0;
                    if (playedInTurn++ < 24)
                        for (int hand = 0; hand < battle.Hand.Count; hand++)
                        {
                            if (!Session.CanPlayCard(hand) || !StrictEnabled("Battle Card " + hand)) continue;
                            double score = noGmCombatReference.TradeBattleCardScore(battle.Hand[hand]);
                            if (score > best) { best = score; selected = hand; }
                        }
                    yield return Click(selected >= 0 ? "Battle Card " + selected : "End Battle Turn");
                }
                Record("NO-GM ranged battle ended: actions=" + actions + "; phase=" + Session.Phase +
                    "; outcome=" + (Session.Settlement?.Outcome.ToString() ?? "none") +
                    "; real playerCrew=" + fought.PlayerBoardingHealth + "; real enemyHull=" + fought.EnemyHull);
                Assert.That(Session.Phase, Is.Not.EqualTo(CoreDemoPhase.Battle), "The legal ranged strategy reached its 350-action battle limit.");
            }

            public IEnumerator CraftFirstNoGmMap()
            {
                Assert.That(NoGmFirstTreasure, Is.True);
                Assert.That(FirstCraftedMap, Is.Null);
                Assert.That(Session.TreasureMaps, Is.Empty);
                yield return EnsureHub(); yield return Click("Port Hotspot Treasure");
                int readyBoard = AcceptanceReadyTreasureBoard(Session);
                Assert.That(readyBoard, Is.GreaterThanOrEqualTo(0), "The first map requires four identified fragments belonging to that same puzzle.");
                string rankId = CoreTreasureHuntCatalog.Ranks[readyBoard].Id;
                int fragments = Session.GetIdentifiedTreasureFragments(rankId), unidentified = Session.TreasureFragments;
                yield return Click("Treasure Route " + readyBoard);
                Assert.That(Session.TreasureMaps.Count, Is.EqualTo(1));
                Assert.That(Session.GetIdentifiedTreasureFragments(rankId), Is.EqualTo(fragments - CoreTreasureHuntCatalog.FragmentCost));
                Assert.That(Session.TreasureFragments, Is.EqualTo(unidentified));
                FirstCraftedMap = Session.TreasureMaps[0];
                Assert.That(FirstCraftedMap.RankId, Is.EqualTo(rankId));
                Assert.That(PortText("Treasure Map Details 0")?.text, Does.Contain(FirstCraftedMap.Seed)
                    .And.Contain(FirstCraftedMap.Rank.Name).And.Contain("探索度"));
                Record("FIRST REAL CRAFT fixed permanently: " + FirstCraftedMap.Id + "; rank=" + FirstCraftedMap.RankId +
                    "; suggestedDays=" + FirstCraftedMap.Rank.RecommendedVoyageDays + "; fragmentsSpent=" + (fragments - Session.GetIdentifiedTreasureFragments(rankId)));
                // The completed puzzle creates a seed in inventory. Ordinary
                // departures remain available while its preparation is pending.
                Assert.That(Session.SelectedTreasureMap, Is.Null);
                Assert.That(Session.TreasureMaps.Single().Id, Is.EqualTo(FirstCraftedMap.Id));
                yield return Click("Treasure Close Folio"); yield return EnsureHub();
            }

            public IEnumerator PrepareFirstNoGmTreasure()
            {
                FirstTreasureReady = false;
                Assert.That(Session.TreasureMaps.Count, Is.EqualTo(1));
                Assert.That(Session.TreasureMaps[0].Id, Is.EqualTo(FirstCraftedMap.Id));
                int days = FirstCraftedMap.Rank.RecommendedVoyageDays * 2 + 2;
                int target = Session.CrewCountValue * days;
                Record("FIRST TREASURE preparation: rank=" + FirstCraftedMap.RankId + "; suggestedDays=" +
                    FirstCraftedMap.Rank.RecommendedVoyageDays + "; plannedDays=" + days + "; targetEach=" + target +
                    "; water=" + Session.Cargo.Water + "; food=" + Session.Cargo.Food +
                    "; capacity=" + Session.CargoCapacityValue + "; gameDate=" + Session.GameDateTime.ToString("O"));
                yield return InvestForSuppliesIfNeeded(target);
                yield return OpenStrictMarket();
                var targets = new Dictionary<string, int> { { "water", target }, { "food", target }, { "powder", 12 }, { "cannonballs", 20 } };
                decimal cost = 0m;
                double volume = Session.Cargo.UsedVolume;
                foreach (var item in targets)
                {
                    int missing = Math.Max(0, item.Value - Session.GetMarketCargoAmount(item.Key));
                    int available = VisibleMarketRemaining(item.Key);
                    if (missing > available)
                    {
                        Record("FIRST TREASURE pending normal stock: " + item.Key + "; missing=" + missing + "; quota=" + available + "; keep the same map and trade/cross month normally.");
                        yield return EnsureHub(); yield break;
                    }
                    cost += PortMarketCatalog.CalculateBuyCost(Session.CurrentPort, PortItem(item.Key), PricingDate, missing);
                    volume += missing * (double)PortMarketCatalog.CargoVolume(PortItem(item.Key));
                }
                if (cost > Session.ProfileGold || volume > Session.CargoCapacityValue + .001d)
                {
                    Record("FIRST TREASURE pending earned funds/capacity: quoted=" + cost + "; gold=" + Session.ProfileGold +
                        "; targetVolume=" + volume + "; capacity=" + Session.CargoCapacityValue + "; no injection or rank/seed reroll.");
                    yield return EnsureHub(); yield break;
                }
                foreach (var item in targets) yield return BuyResource(item.Key, item.Value);
                foreach (var item in targets) Assert.That(Session.GetMarketCargoAmount(item.Key), Is.GreaterThanOrEqualTo(item.Value), "Actual paid preparation was incomplete: " + item.Key);
                Assert.That(Session.Cargo.UsedVolume, Is.LessThanOrEqualTo(Session.CargoCapacityValue + .001f));
                Record("FIRST TREASURE supplies ready: targetEach=" + target + "; water=" + Session.Cargo.Water +
                    "; food=" + Session.Cargo.Food + "; volume=" + Session.Cargo.UsedVolume + "/" + Session.CargoCapacityValue);
                yield return EnsureHub();
                FirstTreasureReady = true;
            }

            public IEnumerator DepartFirstNoGmTreasure()
            {
                Assert.That(FirstTreasureReady, Is.True);
                yield return EnsureHub(); yield return Click("Port Hotspot Treasure"); yield return Click("Treasure Open Folio");
                Assert.That(Session.TreasureMaps.Count, Is.EqualTo(1));
                Assert.That(Session.TreasureMaps[0].Id, Is.EqualTo(FirstCraftedMap.Id));
                yield return Click("Treasure Close Folio"); yield return EnsureHub(); yield return Click("Port Hotspot Voyage");
                Assert.That(Session.SelectedTreasureMap, Is.Null);
                yield return Click("Departure Treasure Seed Picker");
                yield return SelectDepartureTreasureOption(0);
                Record("FIRST TREASURE selected seed through departure selector: " + Session.SelectedTreasureMap?.Seed);
                Assert.That(Session.SelectedTreasureMap.Id, Is.EqualTo(FirstCraftedMap.Id));
                Record("FIRST TREASURE departure provision action: " + PortButton("Departure Buy Provisions")?.GetComponentInChildren<Text>()?.text);
                Assert.That(PortButton("Start Voyage")?.IsInteractable(), Is.True, "Only an enabled, ordinarily provisioned departure may consume the first map.");
                yield return Click("Start Voyage");
                Assert.That(Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
                Assert.That(Session.ActiveTreasureMap.Id, Is.EqualTo(FirstCraftedMap.Id));
                Assert.That(Session.ActiveTreasureMap.Seed, Is.EqualTo(FirstCraftedMap.Seed));
                Assert.That(Session.ActiveTreasureMap.RankId, Is.EqualTo(FirstCraftedMap.RankId));
                Assert.That(Session.TreasureMaps, Is.Empty, "The fixed first map must really be consumed.");
            }
        }

        [Serializable] private sealed class NoGmFirstTreasureAudit
        {
            public bool passed;
            public string startedUtc, completedUtc, failure, firstMapId, firstMapSeed, firstMapRank;
            public int departures, crossPortArrivals, paidTradeUnitsBought, crossPortTradeUnitsSold, reloadsVerified, consecutiveFailedVoyages;
            public double elapsedSeconds;
            public string method = "New Game and tutorial UI, ordinary paid trade/progression, real fragment identification until four belong to one puzzle, exactly the first completed puzzle's crafted seed selected at departure, real settlement and two save/continue reloads. No GM, external checkpoint, money/stock injection or seed/rank replacement. PlayMode simulated input; 40 actual departures including tutorial / 30 minutes.";
        }
        [Serializable] private sealed class NoGmDeparture
        {
            public int number;
            public string seed, port, gameDate;
            public bool tutorial, treasure;
        }
    }
}
