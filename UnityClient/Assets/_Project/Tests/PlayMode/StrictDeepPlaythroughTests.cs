using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Content;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(14400000)]
        public IEnumerator StrictEconomy_AllLegendsAndThreeTreasureRanks_RealUI()
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-navalVisualCapturePath");
            if (at < 0) Assert.Ignore("Opt-in: this full-campaign economy/capture audit can run for four hours; supply -navalVisualCapturePath to run it.");
            Assert.That(at + 1, Is.LessThan(args.Length), "Missing -navalVisualCapturePath output directory.");
            Assert.That(string.IsNullOrWhiteSpace(args[at + 1]) || args[at + 1].StartsWith("-", StringComparison.Ordinal),
                Is.False, "Expected an output directory after -navalVisualCapturePath.");
            string output = Path.GetFullPath(args[at + 1]);
            Directory.CreateDirectory(output);
            StrictDeepDriver driver = null;
            var stack = new Stack<IEnumerator>();
            stack.Push(StrictDeepStory(output, value => driver = value));
            try
            {
                while (stack.Count > 0)
                {
                    object next = null; bool moved;
                    try
                    {
                        var routine = stack.Peek(); moved = routine.MoveNext();
                        if (moved) next = routine.Current;
                        else { (routine as IDisposable)?.Dispose(); stack.Pop(); }
                    }
                    catch (Exception error)
                    {
                        if (driver != null) { driver.Record("DRIVER STOP: " + error); driver.Shot("Driver_Stop"); }
                        File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString());
                        throw;
                    }
                    if (!moved) continue;
                    if (next is IEnumerator nested) stack.Push(nested); else yield return next;
                }
            }
            finally { while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose(); driver?.Record("RUN ENDED"); }
        }

        private IEnumerator StrictDeepStory(string output, Action<StrictDeepDriver> setDriver)
        {
            var args = Environment.GetCommandLineArgs(); int resume = Array.IndexOf(args, "-strictResumePath");
            StrictDeepCheckpoint checkpoint = null;
            if (resume >= 0)
            {
                checkpoint = JsonUtility.FromJson<StrictDeepCheckpoint>(File.ReadAllText(args[resume + 1]));
                Assert.That(checkpoint.phase, Is.EqualTo("Port"), "Only earned settled port checkpoints may resume.");
                foreach (var item in checkpoint.ints) PlayerPrefs.SetInt(item.key, item.value);
                foreach (var item in checkpoint.strings) PlayerPrefs.SetString(item.key, item.value);
                SceneManager.LoadScene("CoreGameplayDemo"); yield return null; yield return null;
            }
            else yield return NewGame_FirstTreasureMap_TraversesRealTutorialAndCapturesScenes();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var driver = new StrictDeepDriver(controller, output, checkpoint); setDriver(driver);
            if (resume >= 0) driver.Shot("Resumed_Earned_Port");
            driver.Record(resume >= 0 ? "Resume exact earned settled checkpoint; no edited values" : "Fresh tutorial completed; no GM assistance");
            yield return driver.EnsureHub(); driver.Checkpoint();
            if (resume < 0) yield return driver.InspectAllowedGmRefresh();
            int budgetIndex = Array.IndexOf(args, "-strictMaxCycles");
            int cycleBudget = budgetIndex >= 0 ? int.Parse(args[budgetIndex + 1]) : 140;
            for (int voyage = 0; voyage < cycleBudget && !driver.Complete; voyage++)
            {
                var session = controller.Session;
                driver.Record("PORT CYCLE " + driver.cycles);
                if (driver.IsShipAudit) yield return driver.RefreshAllowedMarketIfNeeded();
                yield return driver.SellCargo();
                yield return driver.BuyLargerLocalShip();
                yield return driver.RecruitTreasureCrew();
                yield return driver.InvestForSuppliesIfNeeded();
                yield return driver.UpgradeBuild();
                driver.Checkpoint();
                bool treasure = !driver.MustTradeAfterSupplyInvestment && (session.TreasureFragments >= 4 || session.TreasureMaps.Count > 0) && session.PirateLevel >= 2;
                if (treasure)
                {
                    yield return driver.CraftAndDepartTreasure();
                    if (session.Phase == CoreDemoPhase.Sailing) yield return driver.Navigate("treasure");
                }
                else
                {
                    int port = Enumerable.Range(0, CoreVoyageSession.DestinationPorts.Count).First(i => CoreVoyageSession.DestinationPorts[i] == session.CurrentPort);
                    int destination = new[] { 2, 0, 3, 4, 5, 1 }[port];
                    bool legendary = driver.Legends.Length < 5 && driver.LegendReady;
                    bool explore = !legendary && !driver.MustTradeAfterSupplyInvestment && session.ProfileGold > 1200 && driver.cycles % 3 != 0;
                    string goal = legendary ? "legendary" : explore ? "explore" : "trade";
                    int route = legendary ? 2 : explore ? 0 : 1;
                    string seed = goal == "trade" ? driver.FindEarnedChart(port, destination, route) : goal == "legendary" ? driver.FindLegendaryChart() : null;
                    if (seed != null && CoreVoyageSession.TryParseChartSeed(seed, out CoreSeaChartDefinition chart)) destination = Enumerable.Range(0, CoreVoyageSession.DestinationPorts.Count).First(i => CoreVoyageSession.DestinationPorts[i] == chart.OtherPort(session.CurrentPort));
                    yield return driver.PrepareAndDepart(route, destination, seed);
                    if (session.Phase == CoreDemoPhase.Sailing) yield return driver.Navigate(goal);
                }
                if (session.Phase == CoreDemoPhase.Settlement) yield return driver.Settle();
                else if (session.Phase == CoreDemoPhase.Port)
                {
                    driver.Record("Departure unavailable with actual funds/stock; inspect evidence");
                    driver.Shot("Departure_Unavailable");
                    Assert.Fail("Natural progression cannot depart; see quota, cargo and funds in action record.");
                }
                driver.cycles++; driver.Checkpoint();
                if (driver.IsShipAudit && driver.ConsecutiveDefeats >= 4 && driver.LegendReady)
                { driver.Record("AUDIT STOP: four real defeats with the paid endgame deck; preserve failures, no victory injection."); break; }
            }
            driver.Shot("Final_Port");
            driver.Record("FINAL objectives=" + driver.Complete);
            if (driver.IsShipAudit) yield break; // Failure is an observed result of this comparison, not a forced victory.
            Assert.That(driver.Legends.Length, Is.EqualTo(5));
            Assert.That(driver.completedRanks.Distinct().Count(), Is.EqualTo(3));
        }

        private sealed partial class StrictDeepDriver
        {
            private readonly CoreGameplayDemoController controller;
            private CoreVoyageSession Session => controller.Session;
            private readonly string output;
            private int sequence, shot;
            public bool IsShipAudit { get; private set; }
            private int[] shipPurchasePlan = Array.Empty<int>();
            private int shipsPurchased;
            public int ConsecutiveDefeats { get; private set; }
            private static readonly int[] AuditUpgradeCards = { 14, 15, 16, 17, 1, 13, 7 };
            public bool LegendReady => AuditUpgradeCards.All(i => Session.GetBattleCard(i).UpgradeLevel >= 2);
            public int cycles;
            public readonly List<string> completedRanks = new List<string>();
            private readonly List<StrictDeepCraft> crafted = new List<StrictDeepCraft>();
            public string[] Legends => Session.CaptureSeaChartProgress().charts.SelectMany(c => c.defeatedBosses).Where(id => id.StartsWith("legendary_pirate_")).Distinct().ToArray();
            public bool Complete => Legends.Length == 5 && completedRanks.Distinct().Count() == 3;
            public StrictDeepDriver(CoreGameplayDemoController value, string folder, StrictDeepCheckpoint checkpoint, bool noGmFirstTreasure = false)
            {
                controller = value; output = folder;
                NoGmFirstTreasure = noGmFirstTreasure;
                Assert.That(!NoGmFirstTreasure || checkpoint == null, Is.True, "The no-GM acceptance starts only from the actual new-game tutorial.");
                string[] args = Environment.GetCommandLineArgs(); int planIndex = Array.IndexOf(args, "-strictShipPlan");
                IsShipAudit = planIndex >= 0;
                if (IsShipAudit && args[planIndex + 1] != "none") shipPurchasePlan = args[planIndex + 1].Split(',').Select(int.Parse).ToArray();
                if (checkpoint != null) { cycles = checkpoint.cycles; shipsPurchased = checkpoint.shipsPurchased; completedRanks.AddRange(checkpoint.completedRanks); crafted.AddRange(checkpoint.crafted); }
            }
            public void Record(string action)
            {
                if (NoGmFirstTreasure) Assert.That(controller.IsGameMasterOpen, Is.False, "The no-GM acceptance must never open the GM panel.");
                string stop = Path.Combine(output, "stop-driver.txt");
                if (File.Exists(stop) && Session.Phase == CoreDemoPhase.Port) { File.Delete(stop); Checkpoint(); throw new InvalidOperationException("Operator stopped the test driver at port; retain actual checkpoint, restore user profile via fixture."); }
                var state = new StrictDeepRecord {
                    sequence = sequence++, utc = DateTime.UtcNow.ToString("O"), realtime = Time.realtimeSinceStartup, action = action,
                    phase = Session.Phase.ToString(), port = Session.CurrentPort, gold = (double)Session.ProfileGold, voyageGold = Session.Cargo.Gold,
                    water = Session.Cargo.Water, food = Session.Cargo.Food, powder = Session.Cargo.Powder, cannonballs = Session.Cargo.Cannonballs,
                    hull = Session.Hull, maxHull = Session.MaxHull, ship = Session.SelectedShipIndex, experience = Session.PirateExperience,
                    level = Session.PirateLevel, fragments = Session.TreasureFragments, pendingFragments = Session.VoyageTreasureFragments,
                    maps = Session.TreasureMaps.Count, seed = Session.Map?.Seed ?? "", encounter = Session.ActiveEncounter?.Id ?? "",
                    shipPurchases = shipsPurchased, shipPlan = IsShipAudit ? string.Join(",", shipPurchasePlan) : "unrestricted",
                    x = Session.Position.X, y = Session.Position.Y, distance = Session.TotalDistanceNm,
                    legendWins = Legends, completedRanks = completedRanks.ToArray(), cycle = cycles,
                    stage = Session.Battle?.Stage.ToString() ?? "", turn = Session.Battle?.Turn ?? 0,
                    enemyHull = Session.Battle?.EnemyHull ?? 0, enemyCrew = Session.Battle?.EnemyBoardingHealth ?? 0,
                    playerCrew = Session.Battle?.PlayerBoardingHealth ?? 0, energy = Session.Battle?.Energy ?? 0, hand = Session.Battle == null ? "" : string.Join(";", Session.Battle.Hand.Select(i => i + ":" + Session.GetBattleCard(i).Name + "/energy" + Session.GetBattleCardEnergyCost(i) + "/damage" + Session.GetBattleCard(i).Damage + "/draw" + Session.GetBattleCard(i).Draw)), playerBlock = Session.Battle?.PlayerBlock ?? 0
                };
                string json = JsonUtility.ToJson(state);
                File.AppendAllText(Path.Combine(output, "strict-actions.jsonl"), json + "\n", new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(output, "strict-status.json"), JsonUtility.ToJson(state, true), new UTF8Encoding(false));
            }
            public void Shot(string name, string root = "Phase Content")
            {
                if (GameObject.Find(root) == null) root = "Phase Content";
                if (GameObject.Find(root) == null) return;
                string safe = new string(name.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_').Take(100).ToArray());
                NavalBattleVisualCapture.Capture(controller, "Strict_" + (shot++).ToString("0000") + "_" + safe + ".png", "严格经济实玩 · " + name, root);
            }
            public IEnumerator Click(string name)
            {
                Assert.That(name, Is.Not.EqualTo("Game Master Set Gold"));
                if (NoGmFirstTreasure)
                {
                    Assert.That(name.IndexOf("Game Master", StringComparison.OrdinalIgnoreCase), Is.LessThan(0), "All GM actions are forbidden in this acceptance.");
                    yield return WaitForNavigationMotionCompletion(controller);
                }
                var visible = ActiveButton(name);
                if (visible != null)
                {
                    foreach (var scroll in visible.GetComponentsInParent<ScrollRect>())
                    {
                        Canvas.ForceUpdateCanvases();
                        var viewport = scroll.viewport ?? (RectTransform)scroll.transform;
                        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, visible.transform);
                        float range = scroll.content.rect.height - viewport.rect.height;
                        if (range <= 0) continue;
                        float delta = bounds.max.y > viewport.rect.yMax ? bounds.max.y - viewport.rect.yMax : bounds.min.y < viewport.rect.yMin ? bounds.min.y - viewport.rect.yMin : 0;
                        if (Math.Abs(delta) > .1f) { scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + delta / range); yield return null; }
                    }
                }
                Record("Before click " + name);
                ClickAcceptanceButton(name);
                yield return null;
                float limit = Time.realtimeSinceStartup + 20;
                while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < limit) yield return null;
                Assert.That(controller.IsBattleAnimationPlaying, Is.False, "Battle animation timeout");
                if (NoGmFirstTreasure)
                {
                    yield return WaitForNavigationMotionCompletion(controller);
                    if (GameObject.Find("Treasure Story Overlay") != null)
                    {
                        yield return DismissTreasureStoryIfPresent(controller);
                        Record("After ordinary Treasure Story Skip; no encounter resolved or game time advanced.");
                    }
                }
                yield return null;
                Record("After click " + name);
                if (Session.Phase == CoreDemoPhase.Port) Checkpoint();
            }
            public void Checkpoint()
            {
                if (Session.Phase != CoreDemoPhase.Port) return;
                var checkpoint = new StrictDeepCheckpoint { phase = "Port", cycles = cycles, shipsPurchased = shipsPurchased, completedRanks = completedRanks.ToArray(), crafted = crafted.ToArray() };
                var ints = MetaIntKeys().Concat(new[] { CoreGameplayDemoController.TutorialCompletedKey, CoreGameplayDemoController.TutorialRewardGrantedKey, CoreGameplayDemoController.ProfileGoldKey });
                foreach (string key in ints.Distinct()) if (PlayerPrefs.HasKey(key)) checkpoint.ints.Add(new StrictDeepInt { key = key, value = PlayerPrefs.GetInt(key) });
                var strings = MetaStringKeys().Concat(new[] { CoreGameplayDemoController.CurrentPortKey }).Concat(Enum.GetValues(typeof(ComponentSlot)).Cast<ComponentSlot>().Select(CoreGameplayDemoController.ComponentLoadoutKey));
                foreach (string key in strings.Distinct()) if (PlayerPrefs.HasKey(key)) checkpoint.strings.Add(new StrictDeepString { key = key, value = PlayerPrefs.GetString(key) });
                File.WriteAllText(Path.Combine(output, "earned-port-checkpoint.json"), JsonUtility.ToJson(checkpoint, true), new UTF8Encoding(false));
            }
            public string FindEarnedChart(int from, int to, int route)
            {
                var archive = JsonUtility.FromJson<StrictDeepArchive>(PlayerPrefs.GetString(CoreGameplayDemoController.RecordedSeaChartsKey, "{}"));
                return archive?.charts?.FirstOrDefault(c => !c.tutorialWaters && c.routeIndex == route &&
                    (c.departurePort == CoreVoyageSession.DestinationPorts[from] && c.destinationPort == CoreVoyageSession.DestinationPorts[to] ||
                     c.destinationPort == CoreVoyageSession.DestinationPorts[from] && c.departurePort == CoreVoyageSession.DestinationPorts[to]))?.seed;
            }
            public string FindLegendaryChart()
            {
                var archive = JsonUtility.FromJson<StrictDeepArchive>(PlayerPrefs.GetString(CoreGameplayDemoController.RecordedSeaChartsKey, "{}"));
                return archive?.charts?.Where(c => c.routeIndex == 2 && (c.departurePort == Session.CurrentPort || c.destinationPort == Session.CurrentPort))
                    .OrderByDescending(c => Session.CaptureSeaChartProgress().charts.FirstOrDefault(p => p.seed == c.seed)?.defeatedBosses.Count(id => id.StartsWith("legendary_pirate_")) ?? 0)
                    .FirstOrDefault()?.seed;
            }
            public IEnumerator Settle()
            {
                Record("SETTLEMENT outcome=" + Session.Settlement.Outcome + "; fragments=" + Session.Settlement.BankedFragments + "; XP=" + Session.Settlement.BankedPirateExperience + "; protection=" + Session.Settlement.ProtectionRatio + "; treasure=" + Session.Settlement.TreasureCompleted);
                if (ActiveButton("Skip Settlement Reveal")?.IsInteractable() == true) yield return Click("Skip Settlement Reveal");
                if (Session.IsTreasureVoyage && Session.Settlement.TreasureCompleted)
                {
                    string rank = Session.ActiveTreasureMap.RankId;
                    completedRanks.Add(rank); Record("TREASURE COMPLETED " + rank + " " + Session.ActiveTreasureMap.Seed);
                }
                if (ActiveButton("Draw Sea Chart")?.IsInteractable() == true) yield return Click("Draw Sea Chart");
                Shot("Settlement_" + Session.Settlement.Outcome, "Settlement Card");
                yield return Click("Return To Port");
                Record("BANKED RETURN");
            }
            public IEnumerator UpgradeBuild()
            {
                yield return EnsureHub();
                if (Session.ProfileGold < 450 || AuditUpgradeCards.All(i => Session.GetBattleCard(i).UpgradeLevel == 3)) yield break;
                int targetUpgrade = AuditUpgradeCards.All(i => Session.GetBattleCard(i).UpgradeLevel >= 2) ? 3 : 2;
                yield return Click("Port Hotspot CardWorkshop"); yield return Click("Open Deck Chest 1");
                foreach (int index in AuditUpgradeCards)
                {
                    var card = Session.GetBattleCard(index);
                    NavalBattleStage stage = card.IsAllowedInStage(NavalBattleStage.Boarding) ? NavalBattleStage.Boarding : card.IsAllowedInStage(NavalBattleStage.Naval) ? NavalBattleStage.Naval : NavalBattleStage.Preparation;
                    yield return Click("Upgrade Stage " + stage);
                    for (int page = 0; ActiveButton("Select Upgrade Card " + index) == null && page < 10; page++)
                    { if (ActiveButton("Next Upgrade Cards")?.IsInteractable() != true) break; yield return Click("Next Upgrade Cards"); }
                    if (ActiveButton("Select Upgrade Card " + index) == null) continue;
                    yield return Click("Select Upgrade Card " + index);
                    while (Session.GetBattleCard(index).UpgradeLevel < targetUpgrade && Session.ProfileGold >= Session.GetCardUpgradeCost(index) + 600 && ActiveButton("Confirm Battle Card Upgrade")?.IsInteractable() == true)
                        yield return Click("Confirm Battle Card Upgrade");
                }
                Shot("Paid_Card_Upgrades", "Battle Card Upgrade Panel");
                if (Session.GetBattleCard(16).UpgradeLevel >= 1)
                {
                    yield return Click("Upgrade Stage Boarding"); yield return Click("Edit Deck From Card Upgrades");
                    yield return SetDeck(NavalBattleStage.Boarding, new[] { 14, 14, 15, 15, 16, 16, 17, 17 });
                    yield return SetDeck(NavalBattleStage.Preparation, new[] { 1, 1, 1, 13, 13, 13, 7, 7 });
                    yield return SetDeck(NavalBattleStage.Naval, new[] { 1, 1, 1, 13, 13, 13, 7, 7 });
                    yield return Click("Back To Card Workshop Chests");
                }
                else yield return Click("Back From Card Upgrades");
                yield return EnsureHub();
            }
            public IEnumerator RecruitTreasureCrew()
            {
                yield return EnsureHub();
                foreach (int profession in new[] { 5, 4 })
                {
                    if (profession == 5 ? Session.HasDiver : Session.HasCook) continue;
                    var crew = CrewNarrativeCatalog.ForPortAndProfession(Session.CurrentPort, profession);
                    if (Session.ProfileGold < crew.ContractCost + 600) continue;
                    yield return Click("Port Hotspot Tavern"); yield return Click("Tavern Crew " + profession);
                    Shot("Treasure_Crew_Offer_" + profession, "Tavern Crew Detail");
                    if (ActiveButton("Recruit Tavern Crew " + profession)?.IsInteractable() == true) yield return Click("Recruit Tavern Crew " + profession);
                    Record("Normal crew recruitment for treasure: " + crew.DisplayName + " / cost=" + crew.ContractCost);
                    yield return Click("Back To Tavern Crew Lineup"); yield return EnsureHub();
                }
            }
            public IEnumerator BuyLargerLocalShip()
            {
                yield return EnsureHub();
                int port = Enumerable.Range(0, CoreVoyageSession.DestinationPorts.Count).First(i => CoreVoyageSession.DestinationPorts[i] == Session.CurrentPort);
                int target = CoreShipMarketCatalog.ShipsForPort(port).Where(i => CoreVoyageSession.Ships[i].CargoCapacity > Session.CargoCapacityValue && Session.ProfileGold >= CoreShipMarketCatalog.PurchaseCost(i) + 700)
                    .OrderByDescending(i => CoreVoyageSession.Ships[i].CargoCapacity).DefaultIfEmpty(-1).First();
                if (IsShipAudit)
                {
                    if (shipsPurchased >= shipPurchasePlan.Length) yield break;
                    target = shipPurchasePlan[shipsPurchased];
                    if (!CoreShipMarketCatalog.IsSoldAtPort(port, target) || Session.ProfileGold < CoreShipMarketCatalog.PurchaseCost(target) + 700 ||
                        Session.Cargo.UsedVolume > CoreVoyageSession.Ships[target].CargoCapacity) yield break;
                }
                if (target < 0) yield break;
                yield return Click("Port Hotspot Shipyard");
                for (int n = 0; ActiveButton("Shipyard Ship " + target) == null && n < CoreVoyageSession.Ships.Count; n++) yield return Click("Shipyard Next Vessel");
                yield return Click("Shipyard Ship " + target); Shot("Local_Ship_Quote_" + target, "Shipyard Detail");
                decimal beforeGold = Session.ProfileGold;
                if (ActiveButton("Confirm Shipyard Vessel")?.IsInteractable() == true) yield return Click("Confirm Shipyard Vessel");
                if (Session.SelectedShipIndex == target && Session.ProfileGold < beforeGold) shipsPurchased++;
                if (IsShipAudit) Assert.That(shipsPurchased, Is.LessThanOrEqualTo(shipPurchasePlan.Length));
                Record("Purchased/select local vessel with earned money: " + Session.SelectedShipIndex);
                Shot("Local_Ship_Purchased_" + target, "Shipyard Detail");
                yield return Click("Back To Shipyard Gallery"); yield return EnsureHub();
            }
            private IEnumerator SetDeck(NavalBattleStage stage, int[] desired)
            {
                yield return Click("Battle Deck Stage Tab " + stage);
                for (int change = 0; change < 30; change++)
                {
                    var current = Session.GetBattleDeck(stage).ToList();
                    int extra = current.Where(i => current.Count(v => v == i) > desired.Count(v => v == i)).DefaultIfEmpty(-1).First();
                    int missing = desired.Where(i => desired.Count(v => v == i) > current.Count(v => v == i)).DefaultIfEmpty(-1).First();
                    if (extra == -1 && missing == -1) break;
                    if (extra >= 0 && current.Count > 8)
                    {
                        // Scroll only the public deck list if its visible row is clipped.
                        var row = ActiveButton("Remove Deck Card " + extra);
                        var scroll = row?.GetComponentInParent<ScrollRect>();
                        if (scroll != null) { Canvas.ForceUpdateCanvases(); yield return null; }
                        yield return Click("Remove Deck Card " + extra);
                    }
                    else if (missing >= 0 && current.Count < 10)
                    {
                        for (int p = 0; ActiveButton("Add Deck Card " + missing) == null && p < 10; p++)
                        { if (ActiveButton("Next Battle Card Collection Page")?.IsInteractable() == true) yield return Click("Next Battle Card Collection Page"); else yield return Click("Previous Battle Card Collection Page"); }
                        yield return Click("Add Deck Card " + missing);
                    }
                    else break;
                }
                Record("Configured legal deck " + stage + ":" + string.Join(",", Session.GetBattleDeck(stage)));
                Shot("Configured_" + stage);
            }
            public IEnumerator CraftAndDepartTreasure()
            {
                yield return EnsureHub();
                yield return BuyResource("powder", 3); yield return BuyResource("cannonballs", 2);
                yield return BuyPairedRations((int)Math.Ceiling(Session.EstimateDailyRationConsumption(Session.CrewCountValue, Session.HasCook) * 26));
                yield return EnsureHub(); yield return Click("Port Hotspot Treasure");
                if (Session.TreasureFragments >= 4 && Session.TreasureMaps.Count < 6)
                {
                    yield return Click("Treasure Craft Map");
                    var map = Session.TreasureMaps.Last();
                    crafted.Add(new StrictDeepCraft { rank = map.RankId, seed = map.Seed });
                    Record("CRAFT RESULT " + map.RankId + " " + map.Seed); Shot("Craft_" + map.RankId, "Treasure Folio");
                }
                else yield return Click("Treasure Open Folio");
                int index = Enumerable.Range(0, Session.TreasureMaps.Count).OrderBy(i => completedRanks.Contains(Session.TreasureMaps[i].RankId) ? 1 : 0).First();
                yield return Click("Treasure Map Select " + index);
                Record("Treasure actual chosen rank=" + Session.TreasureMaps[index].RankId);
                yield return Click("Treasure Equip And Depart"); Shot("Treasure_Departure", "Voyage Setup");
                if (ActiveButton("Departure Buy Provisions")?.IsInteractable() == true) yield return Click("Departure Buy Provisions");
                if (ActiveButton("Start Voyage")?.IsInteractable() == true) yield return Click("Start Voyage");
            }
        }
        [Serializable] private sealed class StrictDeepRecord
        {
            // JSON audit display only; Core and balance assertions use decimal.
            public double gold;
            public int sequence, cycle, shipPurchases, voyageGold, water, food, powder, cannonballs, hull, maxHull, ship, experience, level, fragments, pendingFragments, maps, turn, enemyHull, enemyCrew, playerCrew, energy, playerBlock;
            public float realtime, x, y, distance; public string utc, action, phase, port, seed, encounter, stage, hand, shipPlan; public string[] legendWins, completedRanks;
        }
        [Serializable] private sealed class StrictDeepCheckpoint { public string phase; public int cycles, shipsPurchased; public string[] completedRanks; public StrictDeepCraft[] crafted; public List<StrictDeepInt> ints = new List<StrictDeepInt>(); public List<StrictDeepString> strings = new List<StrictDeepString>(); }
        [Serializable] private sealed class StrictDeepInt { public string key; public int value; }
        [Serializable] private sealed class StrictDeepString { public string key, value; }
        [Serializable] private sealed class StrictDeepCraft { public string rank, seed; }
        [Serializable] private sealed class StrictDeepArchive { public StrictDeepChart[] charts; }
        [Serializable] private sealed class StrictDeepChart { public string seed, departurePort, destinationPort; public int routeIndex; public bool tutorialWaters; }
    }
}










