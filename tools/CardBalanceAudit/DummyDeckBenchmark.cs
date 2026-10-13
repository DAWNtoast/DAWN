using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Tools.CardBalanceAudit
{
    // An explicit training-target experiment. The target must be provided by a
    // Core seam; this tool never replaces turn resolution or mutates combat HP,
    // hands, school resources, ammunition, or enemy actions after battle entry.
    public static partial class DummyDeckBenchmark
    {
        private const string Version = "dummy-decks-20261012-v5-endurance-health";
        private const int TurnLimit = 120, ActionLimit = 64;
        private static readonly string[] Schools = { "ordinary", "fire", "arcane", "curse", "tide", "thunder", "blood" };
        private static readonly int[] Sizes = { 10, 15, 20 };
        private static readonly string[] Tactics = { "cannon", "boarding" };
        private static readonly string[] Supplies = { "full", "none", "no-powder", "no-cannonballs", "no-wood", "no-medicine" };
        private static readonly Dictionary<string, int> Index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .ToDictionary(i => CoreVoyageSession.CardLibrary[i].Id, i => i);
        private static readonly string[][] OrdinaryPriority = {
            new[] { "card_full_sail_turn", "card_parallel_course", "card_deck_polish", "card_load_shot", "card_brace", "card_evasive_maneuver", "card_emergency_repair", "card_approach", "card_circle_enemy", "card_retreat" },
            new[] { "card_full_sail_turn", "card_parallel_course", "card_chain_shot", "card_cannon_barrage", "card_emergency_repair", "card_brace", "card_evasive_maneuver", "card_load_shot", "card_deck_polish", "card_circle_enemy", "card_retreat", "card_approach" },
            new[] { "card_crew_rally", "card_crew_bandage", "card_crew_thrust", "card_crew_guard", "card_boarding_hook", "card_cutlass_raid", "card_crew_pistol" }
        };

        private sealed class Target
        {
            public CoreTrainingTarget Definition;
            public string Id => Definition.Id;
            public int Hull => Definition.Hull;
            public int CrewHealth => Definition.BoardingHealth;
            public int Cannon => Definition.CannonDamage;
            public int Melee => Definition.MeleeDamage;
            public int Guard => Definition.Guard;
            public int Heal => Definition.Repair;
        }
        private static readonly Target[] Targets = CoreTrainingTargets.All.Select(t => new Target { Definition = t }).ToArray();
        private sealed class Deck
        {
            public string Id, School, Reason, Recipe = "balanced"; public int Size, Copies, OrdinaryCrew = 22; public int[][] Stages;
            public int[] RequestedSizes; public CoreSpecialCardDefinition Source;
            public bool Valid => string.IsNullOrEmpty(Reason);
        }
        private sealed class CardUse
        {
            public string Deck, Tactic, Supply, CardId; public int Rank, Seen, Played, PowderBlocked, BallsBlocked, WoodBlocked, MedicineBlocked, SchoolBlocked;
        }
        private static readonly Dictionary<string, CardUse> Uses = new Dictionary<string, CardUse>();
        private sealed class Result
        {
            public string Deck, School, Tactic, Supply, Target, Seed, Outcome, Stage, Recipe, SourceCardId, SourceKind, PhaseSizes;
            public int OrdinaryCrew, EnergyRecovered;
            public int InitialHull, MaxHull, InitialCrew, MaxCrew, MinHull, MinCrew,
                HullBloodPaid, CrewBloodPaid, HullGrossLoss, CrewGrossLoss, HullEffectiveHealing, CrewEffectiveHealing;
            public int Size, Copies, Rank, Turns, Cards, SchoolCards, Powder, Balls, Wood, Medicine, Energy,
                InitialPowder, InitialBalls, InitialWood, InitialMedicine, HullLoss, CrewLoss, BoardingCarryover,
                HullHealed, CrewHealed, Hull, Crew, EnemyHull, EnemyCrew, NoLegalTurns, DeclinedTurns,
                ResourceLockedTurns, BoardingFallbacks;
            public bool Won => Outcome == "win";
            public int TotalSupplies => Powder + Balls + Wood + Medicine;
            public int NetHullLoss => InitialHull - Hull;
            public int NetCrewLoss => InitialCrew - Crew;
            public double NetHullLossFraction => (double)NetHullLoss / MaxHull;
            public double NetCrewLossFraction => (double)NetCrewLoss / MaxCrew;
            public double HullRemainingFraction => (double)Hull / MaxHull;
            public double CrewRemainingFraction => (double)Crew / MaxCrew;
            public bool CriticalVictory => Won && (HullRemainingFraction <= .25 || CrewRemainingFraction <= .25);
            public double VictoryHealthScore => Won ? 100d * (HullRemainingFraction + CrewRemainingFraction) / 2 : 0d;
            public object[] Values() => new object[] { Deck, School, Size, Copies, Rank, Tactic, Supply, Target, Seed, Outcome,
                Turns, Cards, SchoolCards, InitialPowder, InitialBalls, InitialWood, InitialMedicine, Powder, Balls, Wood, Medicine, TotalSupplies, Energy,
                HullLoss, CrewLoss, BoardingCarryover, HullHealed, CrewHealed, Hull, Crew, EnemyHull, EnemyCrew,
                NoLegalTurns, DeclinedTurns, ResourceLockedTurns, BoardingFallbacks, Stage, Recipe, PhaseSizes, SourceCardId, SourceKind, OrdinaryCrew, EnergyRecovered,
                InitialHull, MaxHull, InitialCrew, MaxCrew, NetHullLoss, NetCrewLoss, NetHullLossFraction, NetCrewLossFraction,
                HullRemainingFraction, CrewRemainingFraction, MinHull, MinCrew, (double)MinHull / MaxHull, (double)MinCrew / MaxCrew,
                HullBloodPaid, CrewBloodPaid, HullGrossLoss, CrewGrossLoss, HullEffectiveHealing, CrewEffectiveHealing,
                HullGrossLoss - HullBloodPaid, CrewGrossLoss - CrewBloodPaid, CriticalVictory, VictoryHealthScore };
        }

        public static int Main(string[] args)
        {
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                int seeds = args.Length > 0 ? Math.Max(1, int.Parse(args[0])) : 4;
                string output = args.Length > 1 ? args[1] : "DummyDeckBenchmark";
                string mode = args.Length > 2 ? args[2].ToLowerInvariant() : "baseline";
                if (mode == "merge-endurance" || mode == "merge-endurancesources") { MergeEnduranceParts(output, args[5], seeds, mode.Substring(6), args.Length > 6 ? args[6] : string.Empty); return 0; }
                if (mode.StartsWith("merge-", StringComparison.Ordinal)) { MergeParts(output, args[5], seeds, mode.Substring(6), args.Length > 6 ? args[6] : string.Empty); return 0; }
                bool manifestOnly = mode == "manifest", pilot = mode == "pilot", search = mode == "search", holdout = mode == "holdout", sources = mode == "sources" || mode == "endurancesources";
                bool endurance = mode == "endurance" || mode == "endurancesources";
                int seedOffset = args.Length > 3 ? int.Parse(args[3]) : 0;
                int shardIndex = args.Length > 5 ? int.Parse(args[5]) : 0;
                int shardCount = args.Length > 6 ? int.Parse(args[6]) : 1;
                if (shardCount < 1 || shardIndex < 0 || shardIndex >= shardCount) throw new ArgumentException("Invalid paired-case shard");
                Directory.CreateDirectory(output);
                if (mode == "trace") { ReplayCases(output, args[4]); return 0; }
                if (!new[] { "manifest", "pilot", "baseline", "search", "holdout", "sources", "endurance", "endurancesources" }.Contains(mode)) throw new ArgumentException("Unknown mode");
                var decks = search || holdout ? BuildSearchDecks() : sources ? BuildSourceDecks() : BuildDecks();
                string schoolFilter = args.Length > 7 ? args[7] : string.Empty;
                if (schoolFilter.Length > 0) decks = decks.Where(d => schoolFilter.Split(',').Contains(d.School)).ToList();
                var selected = holdout ? File.ReadAllLines(args[4]).Skip(1).Where(l => l.Length > 0).Select(l => l.Split('\t')).ToArray() : null;
                if (holdout && seedOffset < 1000) throw new ArgumentException("Holdout seeds must be offset at least 1000 from training seeds");
                WriteDeckManifest(output, decks);
                WriteCsv(Path.Combine(output, "targets.csv"), "target,hull,crewHealth,cannonBaseDamage,meleeBaseDamage,guardPerEnemyTurn,healPerEnemyTurn",
                    Targets.Select(t => new object[] { t.Id, t.Hull, t.CrewHealth, t.Cannon, t.Melee, t.Guard, t.Heal }));
                if (endurance) { RunEnduranceMatrix(output, decks, seeds, seedOffset, shardIndex, shardCount, sources); return 0; }
                if (manifestOnly)
                {
                    foreach (var deck in decks.Where(d => d.Valid)) Create(deck, 0, "full", "DECK-MANIFEST-VALIDATION");
                    Console.WriteLine("Manifest: " + decks.Count(d => d.Valid) + " Core-validated feasible / " + decks.Count + " requested decks."); return 0;
                }
                var rows = new List<Result>();
                int pairedCase = 0;
                foreach (var deck in decks.Where(d => d.Valid && (!pilot || d.Size == 10)))
                {
                    foreach (int rank in pilot || sources ? new[] { 0, 3 } : new[] { 0, 1, 2, 3 })
                        foreach (string tactic in Tactics)
                        {
                            if (holdout && !selected.Any(a => a[0] == deck.Id && a[1] == rank.ToString() && a[2] == tactic)) continue;
                            foreach (var target in search ? new[] { Targets[1], Targets[3] } : Targets)
                                for (int seed = 0; seed < seeds; seed++)
                                {
                                    // Keep every supply condition for a paired seed in one shard.
                                    if (pairedCase++ % shardCount != shardIndex) continue;
                                    foreach (string supply in pilot || search || sources ? new[] { "full", "none" } : Supplies)
                                        rows.Add(Run(deck, rank, tactic, target, supply, "DECK-SUPPLY-" + (seed + seedOffset).ToString("D4")));
                                }
                        }
                    Console.WriteLine(deck.Id + ": " + rows.Count + " training battles complete");
                }
                WriteResults(output, rows, seeds, decks, mode);
                if (search && shardCount == 1) SelectRecipes(output, rows);
                if (sources && shardCount == 1) WriteSourceComparison(output, rows);
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }

        private static List<Deck> BuildDecks()
        {
            var result = new List<Deck>();
            foreach (string school in Schools)
                foreach (int copies in school == "ordinary" ? new[] { 0 } : new[] { 1, 2 })
                    foreach (int size in Sizes)
                        result.Add(BuildDeck(school, copies, new[] { size, size, size }, "balanced"));
            return result;
        }

        private static Deck BuildDeck(string school, int copies, int[] sizes, string recipe)
        {
                        string sizeId = sizes.Distinct().Count() == 1 ? "size" + sizes[0] : "sizes" + string.Join("-", sizes);
                        var deck = new Deck { Id = school + "-core" + copies + "-" + sizeId + (recipe == "balanced" ? "" : "-" + recipe), School = school, Size = sizes.Distinct().Count() == 1 ? sizes[0] : 0, RequestedSizes = sizes, Copies = copies, Stages = new int[3][], Recipe = recipe };
                        for (int p = 0; p < 3; p++)
                        {
                            int size = sizes[p];
                            var stage = (NavalBattleStage)p;
                            var cards = new List<int>();
                            if (school != "ordinary")
                                foreach (var card in CoreVoyageSession.CardLibrary.Where(c => c.SchoolId == school && c.RequiredCrew.Length == 0 && c.RequiredComponent.Length == 0 && c.IsAllowedInStage(stage)))
                                    for (int copy = 0; copy < copies; copy++) cards.Add(Index[card.Id]);
                            // One pass through the ordinary menu adds distinct roles;
                            // a second pass can add the owned second copy. No card is
                            // ever added more than twice in a phase.
                            // New ordinary base cards enter every legal-phase menu automatically.
                            // Existing priorities are stable; unlisted IDs append before second copies.
                            var menu = OrdinaryPriority[p].Concat(CoreVoyageSession.CardLibrary.Where(c => c.SchoolId.Length == 0 && c.RequiredCrew.Length == 0 && c.RequiredComponent.Length == 0 && c.IsAllowedInStage(stage)).Select(c => c.Id)).Distinct();
                            if (recipe == "economy") menu = menu.OrderBy(id => CoreVoyageSession.CardLibrary[Index[id]].PowderCost + CoreVoyageSession.CardLibrary[Index[id]].CannonballCost + (id == "card_crew_bandage" ? 1 : 0));
                            if (recipe == "defensive") menu = menu.OrderByDescending(id => { var c = CoreVoyageSession.CardLibrary[Index[id]]; return c.Block * 2 + c.Repair * 2 + c.Draw * 4; });
                            for (int copy = 0; copy < 2 && cards.Count < size; copy++)
                                foreach (string id in menu)
                                {
                                    if (cards.Count >= size) break;
                                    int index = Index[id]; var card = CoreVoyageSession.CardLibrary[index];
                                    if (card.SchoolId.Length > 0 || card.RequiredCrew.Length > 0 || card.RequiredComponent.Length > 0 || !card.IsAllowedInStage(stage)) throw new InvalidOperationException("Invalid ordinary menu card: " + id);
                                    cards.Add(index);
                                }
                            deck.Stages[p] = cards.ToArray();
                            if (cards.Count != size) deck.Reason = (deck.Reason ?? "") + stage + ": only " + cards.Count + " legal owned cards for requested " + size + "; ";
                            if (cards.GroupBy(x => x).Any(g => g.Count() > 2)) throw new InvalidOperationException("Owned-copy overflow");
                            if (cards.Any(i => CoreVoyageSession.CardLibrary[i].SchoolId.Length > 0 && CoreVoyageSession.CardLibrary[i].SchoolId != school)) throw new InvalidOperationException("Cross-school card");
                        }
                        return deck;
        }
        private static List<Deck> BuildSearchDecks()
        {
            var result = new List<Deck>(); var signatures = new HashSet<string>();
            foreach (string school in Schools)
                foreach (int copies in school == "ordinary" ? new[] { 0 } : new[] { 1, 2 })
                    foreach (string recipe in new[] { "balanced", "economy", "defensive" })
                    {
                        var sizes = (recipe == "balanced" ? Enumerable.Range(10, 11) : Sizes.AsEnumerable()).Select(n => new[] { n, n, n })
                            .Concat(new[] { new[] { 10, 15, 20 }, new[] { 20, 10, 14 }, new[] { 14, 20, 10 }, new[] { 10, 20, 14 } });
                        foreach (var vector in sizes)
                        {
                            var deck = BuildDeck(school, copies, vector, recipe);
                            string signature = school + ":" + string.Join("/", deck.Stages.Select(a => string.Join(";", a.OrderBy(x => x))));
                            if (!deck.Valid || signatures.Add(signature)) result.Add(deck);
                        }
                    }
            return result;
        }

        private static List<Deck> BuildSourceDecks()
        {
            var decks = new List<Deck>();
            foreach (string school in Schools)
                foreach (int crew in new[] { 21, 22 })
                {
                    var deck = BuildDeck(school, school == "ordinary" ? 0 : 2, new[] { 10, 10, 10 }, "balanced");
                    deck.Id += "-control-crew" + crew; deck.OrdinaryCrew = crew; decks.Add(deck);
                }
            foreach (var source in CoreSpecialCardCatalog.All)
            {
                string school = source.Card.SchoolId.Length == 0 ? "ordinary" : source.Card.SchoolId;
                var deck = BuildDeck(school, school == "ordinary" ? 0 : 2, new[] { 10, 10, 10 }, "balanced");
                deck.Id += "-" + source.Card.Id; deck.Source = source;
                deck.OrdinaryCrew = source.SourceKind == CoreSpecialCardSourceKind.Crew ? 21 : 22; decks.Add(deck);
            }
            return decks;
        }

        private static void WriteDeckManifest(string output, List<Deck> decks)
        {
            WriteCsv(Path.Combine(output, "deck-manifest.csv"), "deck,school,requestedSize,coreCopies,stage,actualSize,schoolCards,schoolFraction,status,reason,cards,recipe,sourceCardId,sourceKind,ordinaryCrew",
                decks.SelectMany(d => Enumerable.Range(0, 3).Select(p => new object[] { d.Id, d.School, d.RequestedSizes[p], d.Copies, ((NavalBattleStage)p).ToString(), d.Stages[p].Length,
                    d.Stages[p].Count(i => CoreVoyageSession.CardLibrary[i].SchoolId.Length > 0), (double)d.Stages[p].Count(i => CoreVoyageSession.CardLibrary[i].SchoolId.Length > 0) / d.Stages[p].Length,
                    d.Valid ? "feasible" : "infeasible", d.Reason ?? "", string.Join(";", d.Stages[p].Select(i => CoreVoyageSession.CardLibrary[i].Id)), d.Recipe, d.Source?.Card.Id ?? "", d.Source?.SourceKind.ToString() ?? "none", d.OrdinaryCrew })));
        }

        private static CoreVoyageSession Create(Deck deck, int rank, string supply, string seed)
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            if (!s.SelectShip(2)) throw new InvalidOperationException("Fixture ship rejected");
            s.SelectRoute(2);
            if (s.SelectedRouteIndex != 2) throw new InvalidOperationException("Fixture route rejected");
            s.SetSeed(seed);
            s.ConfigureCrewCapabilities(false, false, false, false, false, false);
            int specialCrew = deck.Source != null && deck.Source.SourceKind == CoreSpecialCardSourceKind.Crew ? 1 : 0;
            s.ConfigureDepartureCrew(deck.OrdinaryCrew, specialCrew);
            s.ConfigureBattleEquipment(2f, 2f);
            s.ConfigureComponentBonuses(0, 0, 0, 0, 0);
            s.ConfigureSpecialCardSources(specialCrew > 0 ? new[] { deck.Source.SourceId } : Array.Empty<string>(),
                deck.Source != null && deck.Source.SourceKind == CoreSpecialCardSourceKind.Component ? new[] { deck.Source.SourceId } : Array.Empty<string>());
            s.ConfigureCardUpgrades(CoreVoyageSession.CardLibrary.Where(c => !CoreVoyageSession.IsSpecialBattleCard(c.Id)).ToDictionary(c => c.Id, c => rank));
            if (deck.Source != null) s.RestoreSpecialCardExperience(new Dictionary<string, int> { [deck.Source.Card.Id] = rank == 0 ? 0 : rank == 1 ? 120 : rank == 2 ? 360 : 840 });
            for (int p = 0; p < 3; p++)
            {
                var stage = (NavalBattleStage)p;
                foreach (var g in deck.Stages[p].GroupBy(i => i))
                    if (s.GetBaseBattleCardOwnedCopies(g.Key) < g.Count()) throw new InvalidOperationException("Insufficient real ownership");
                if (!s.SetBattleDeck(stage, deck.Stages[p])) throw new InvalidOperationException("Core rejected fixture deck: " + deck.Id + "/" + stage);
                int expectedBonus = deck.Source != null && deck.Source.Card.IsAllowedInStage(stage) ? 1 : 0;
                if (s.GetBonusBattleCards(stage).Count != expectedBonus || s.GetBonusBattleCards(stage).Any(i => s.GetBattleCard(i).Id != deck.Source.Card.Id)) throw new InvalidOperationException("Uncontrolled source card");
            }
            s.Cargo.Water = s.Cargo.Food = 20;
            s.Cargo.Beer = s.Cargo.Iron = s.Cargo.Cloth = 0;
            s.Cargo.Powder = supply == "none" || supply == "no-powder" ? 0 : 25;
            s.Cargo.Cannonballs = supply == "none" || supply == "no-cannonballs" ? 0 : 40;
            s.Cargo.Wood = supply == "none" || supply == "no-wood" ? 0 : 18;
            s.Cargo.Medicine = supply == "none" || supply == "no-medicine" ? 0 : 10;
            if (supply == "triple-full")
            {
                // User-specified training provisions exceed this ship's normal
                // hold. The real Port training API permits the isolated fixture;
                // no sailing, capacity rule change or mid-battle refill occurs.
                s.Cargo.Powder *= 3; s.Cargo.Cannonballs *= 3; s.Cargo.Wood *= 3; s.Cargo.Medicine *= 3;
                if (s.Phase != CoreDemoPhase.Port) throw new InvalidOperationException("Endurance fixture must enter from port");
                return s;
            }
            if (s.Cargo.UsedVolume > s.CargoCapacityValue + .001f) throw new InvalidOperationException("Overloaded fixture");
            s.StartVoyage();
            if (s.Phase != CoreDemoPhase.Sailing) throw new InvalidOperationException("Departure rejected");
            return s;
        }

        private static void StartTarget(CoreVoyageSession session, Target target)
        {
            if (!session.StartTrainingBattle(target.Definition)) throw new InvalidOperationException("Core rejected training battle entry");
        }
        private static Result Run(Deck deck, int rank, string tactic, Target target, string supply, string seed, List<object[]> trace = null,
            CoreVoyageSession activeSession = null, int? enduranceCrewHealth = null, string roundKey = null)
        {
            var s = activeSession ?? Create(deck, rank, supply, seed);
            var r = new Result { Deck = deck.Id, School = deck.School, Size = deck.Size, Copies = deck.Copies, Rank = rank,
                Tactic = tactic, Supply = supply, Target = target.Id, Seed = seed,
                Recipe = deck.Recipe, PhaseSizes = string.Join("/", deck.Stages.Select(a => a.Length)), SourceCardId = deck.Source?.Card.Id ?? "", SourceKind = deck.Source?.SourceKind.ToString() ?? "none", OrdinaryCrew = deck.OrdinaryCrew,
                InitialPowder = s.Cargo.Powder, InitialBalls = s.Cargo.Cannonballs, InitialWood = s.Cargo.Wood, InitialMedicine = s.Cargo.Medicine };
            if (enduranceCrewHealth.HasValue)
            {
                if (!s.StartTrainingBattle(target.Definition, enduranceCrewHealth.Value, roundKey)) throw new InvalidOperationException("Core rejected endurance entry");
            }
            else StartTarget(s, target);
            var b = s.Battle;
            r.InitialHull = r.MinHull = s.Hull; r.MaxHull = s.MaxHull;
            r.InitialCrew = r.MinCrew = b.PlayerBoardingHealth; r.MaxCrew = b.PlayerMaxBoardingHealth;
            if (s.Phase != CoreDemoPhase.Battle || b == null || b.EnemyMaxHull != target.Hull || b.EnemyMaxBoardingHealth != target.CrewHealth ||
                b.Hand.Count + b.DrawPile.Count + b.DiscardPile.Count != deck.Stages[0].Length + s.GetBonusBattleCards(NavalBattleStage.Preparation).Count)
                throw new InvalidOperationException("Training target/deck initialization did not match the manifest");
            bool actionLimit = false, fallbackRecorded = false;
            Trace(s, r, "start", trace);
            for (int turn = 0; turn < TurnLimit && s.Phase == CoreDemoPhase.Battle; turn++)
            {
                r.Turns++;
                Incoming(s); // Validate the public attack intent even when no hand card is playable.
                if (trace == null) ObserveHand(s, r);
                bool anyLegal = Enumerable.Range(0, b.Hand.Count).Any(i => s.CanPlayCard(i));
                if (!anyLegal && Enumerable.Range(0, b.Hand.Count).Any(i => IsResourceReason(s.CardUnavailableReason(i)))) r.ResourceLockedTurns++;
                int cardsBefore = r.Cards, action;
                for (action = 0; action < ActionLimit && s.Phase == CoreDemoPhase.Battle; action++)
                {
                    bool fallback = tactic == "cannon" && !HasSuppliedNavalAttack(s);
                    if (fallback && !fallbackRecorded) { r.BoardingFallbacks++; fallbackRecorded = true; }
                    string effectiveTactic = tactic == "boarding" || fallback ? "boarding" : "cannon";
                    int hull = s.Hull, crew = b.PlayerBoardingHealth; var previousStage = b.Stage;
                    if (effectiveTactic == "boarding" && s.CanBoard(out _))
                    {
                        if (!s.TryBoard()) throw new InvalidOperationException("Legal boarding rejected");
                        Observe(s, b, r, hull, crew, previousStage); Trace(s, r, "board", trace); continue;
                    }
                    int hand = BestCard(s, effectiveTactic);
                    if (hand < 0) break;
                    if (!s.CanPlayCard(hand)) throw new InvalidOperationException("Policy selected an illegal card");
                    int index = b.Hand[hand]; var card = s.GetBattleCard(index);
                    int bloodPayment = card.SchoolEffect != null && (card.SchoolEffect.Mechanic == CoreSchoolMechanic.BloodRisk || card.SchoolEffect.Mechanic == CoreSchoolMechanic.BloodWard)
                        ? Math.Max(0, card.SchoolEffect.Threshold) : 0;
                    int energyCost = s.GetBattleCardEnergyCost(index), energyBefore = b.Energy;
                    r.Energy += energyCost;
                    int powder = s.Cargo.Powder, balls = s.Cargo.Cannonballs, wood = s.Cargo.Wood, medicine = s.Cargo.Medicine;
                    if (!s.PlayCard(hand)) throw new InvalidOperationException("Core rejected a legal card: " + card.Id);
                    r.EnergyRecovered += Math.Max(0, b.Energy - energyBefore + energyCost);
                    r.Cards++; if (card.SchoolId.Length > 0) r.SchoolCards++;
                    if (trace == null) GetUse(r, card.Id).Played++;
                    r.Powder += Math.Max(0, powder - s.Cargo.Powder); r.Balls += Math.Max(0, balls - s.Cargo.Cannonballs); r.Wood += Math.Max(0, wood - s.Cargo.Wood); r.Medicine += Math.Max(0, medicine - s.Cargo.Medicine);
                    Observe(s, b, r, hull, crew, previousStage, bloodPayment);
                    Trace(s, r, "play:" + card.Id, trace);
                }
                if (action == ActionLimit && s.Phase == CoreDemoPhase.Battle) { actionLimit = true; break; }
                if (r.Cards == cardsBefore) { if (anyLegal) r.DeclinedTurns++; else r.NoLegalTurns++; }
                if (s.Phase == CoreDemoPhase.Battle)
                {
                    int hull = s.Hull, crew = b.PlayerBoardingHealth; var previousStage = b.Stage;
                    if (!s.EndBattleTurn()) throw new InvalidOperationException("EndBattleTurn rejected");
                    Observe(s, b, r, hull, crew, previousStage);
                    Trace(s, r, "end-turn", trace);
                }
            }
            r.Outcome = s.TrainingOutcome.ToString() == "Victory" ? "win" : s.TrainingOutcome.ToString() == "Defeat" ? "loss" : actionLimit ? "action-watchdog" : "turn-watchdog";
            r.Hull = s.Hull; r.Crew = b.PlayerBoardingHealth; r.EnemyHull = b.EnemyHull; r.EnemyCrew = b.EnemyBoardingHealth; r.Stage = b.Stage.ToString();
            if (r.Powder > r.InitialPowder || r.Balls > r.InitialBalls || r.Wood > r.InitialWood || r.Medicine > r.InitialMedicine) throw new InvalidOperationException("Spent more supplies than the fixture started with");
            if (r.Powder != r.InitialPowder - s.Cargo.Powder || r.Balls != r.InitialBalls - s.Cargo.Cannonballs || r.Wood != r.InitialWood - s.Cargo.Wood || r.Medicine != r.InitialMedicine - s.Cargo.Medicine) throw new InvalidOperationException("Actual deduction accounting mismatch");
            if (r.NetHullLoss != r.HullGrossLoss - r.HullEffectiveHealing || r.NetCrewLoss != r.BoardingCarryover + r.CrewGrossLoss - r.CrewEffectiveHealing)
                throw new InvalidOperationException("Health conservation mismatch: " + PairKey(r));
            if (r.HullGrossLoss < r.HullBloodPaid || r.CrewGrossLoss < r.CrewBloodPaid || r.MinHull < 0 || r.MinCrew < 0 || r.Hull > r.MaxHull || r.Crew > r.MaxCrew)
                throw new InvalidOperationException("Health measurement invariant: " + PairKey(r));
            return r;
        }

        private static void Trace(CoreVoyageSession s, Result r, string action, List<object[]> rows)
        {
            if (rows == null) return;
            var b = s.Battle;
            rows.Add(new object[] { rows.Count, r.Turns, b.Stage, action, b.Energy, s.Hull, b.PlayerBoardingHealth, b.EnemyHull, b.EnemyBoardingHealth,
                b.PlayerBlock, b.EnemyBlock, b.DistanceMeters, s.Cargo.Powder, s.Cargo.Cannonballs, s.Cargo.Wood, s.Cargo.Medicine, b.Schools.Mana,
                b.Schools.CurseMarks, b.EnemyBurnDamage, b.EnemyBurnTurns, s.TrainingOutcome, string.Join(";", b.Hand.Select(i => s.GetBattleCard(i).Id)),
                b.Schools.ManaLimit, b.Schools.TemporaryMana, b.Schools.TidePower, b.Schools.TidePaidBlock, b.Schools.ThunderChain, b.Schools.ThunderReadyDamage, b.Schools.CurseDamagePerMark, b.Schools.CurseNextTickBonus });
        }

        private static bool HasSuppliedNavalAttack(CoreVoyageSession s) => s.GetBattleDeck(NavalBattleStage.Naval)
            .Concat(s.GetBonusBattleCards(NavalBattleStage.Naval))
            .Any(i => HasNavalDamagePotential(s.GetSupplyAdjustedBattleCard(i)) && s.GetBattleCardPowderCost(i) <= s.Cargo.Powder && s.GetBattleCard(i).CannonballCost <= s.Cargo.Cannonballs);
        private static bool HasNavalDamagePotential(CoreBattleCard card)
        {
            if (card.Damage > 0) return true;
            // School resources can be generated by legal cards; lacking them now
            // is not ammunition exhaustion and must not force boarding early.
            var e = card.SchoolEffect;
            return e != null && e.Power > 0 && (e.Mechanic == CoreSchoolMechanic.ArcaneRelease || e.Mechanic == CoreSchoolMechanic.TideRiposte ||
                e.Mechanic == CoreSchoolMechanic.CurseMark && e.Secondary > 0 || e.Mechanic == CoreSchoolMechanic.CurseDetonate || e.Mechanic == CoreSchoolMechanic.BloodRisk);
        }
        private static bool IsResourceReason(string reason) => reason.Contains("法力不足") || reason.Contains("附咒层数不足") || reason.Contains("先施加咒印") || reason.Contains("潮势不足") || reason.Contains("支付生命") || reason.Contains("火药不足") || reason.Contains("炮弹不足") || reason.Contains("木板不足") || reason.Contains("木材不足") || reason.Contains("药品不足");
        private static CardUse GetUse(Result r, string id)
        {
            string key = r.Deck + "/" + r.Rank + "/" + r.Tactic + "/" + r.Supply + "/" + id;
            if (!Uses.TryGetValue(key, out CardUse use)) Uses[key] = use = new CardUse { Deck = r.Deck, Rank = r.Rank, Tactic = r.Tactic, Supply = r.Supply, CardId = id };
            return use;
        }
        private static void ObserveHand(CoreVoyageSession s, Result r)
        {
            for (int hand = 0; hand < s.Battle.Hand.Count; hand++)
            {
                var use = GetUse(r, s.GetBattleCard(s.Battle.Hand[hand]).Id); use.Seen++;
                string reason = s.CardUnavailableReason(hand);
                if (reason.Contains("火药不足")) use.PowderBlocked++;
                else if (reason.Contains("炮弹不足")) use.BallsBlocked++;
                else if (reason.Contains("木板不足") || reason.Contains("木材不足")) use.WoodBlocked++;
                else if (reason.Contains("药品不足")) use.MedicineBlocked++;
                else if (IsResourceReason(reason)) use.SchoolBlocked++;
            }
        }
        private static void Observe(CoreVoyageSession s, CoreBattleState b, Result r, int hull, int crew, NavalBattleStage previousStage, int bloodPayment = 0)
        {
            if (s.Hull < 0 || b.PlayerBoardingHealth < 0 || b.EnemyHull < 0 || b.EnemyBoardingHealth < 0 || b.Energy < 0 || b.Schools.Mana < 0 || b.Schools.TemporaryMana < 0 || b.Schools.TidePower < 0 ||
                s.Cargo.Powder < 0 || s.Cargo.Cannonballs < 0 || s.Cargo.Wood < 0 || s.Cargo.Medicine < 0) throw new InvalidOperationException("Negative combat HP/resource invariant");
            r.HullLoss += Math.Max(0, hull - s.Hull); r.HullHealed += Math.Max(0, s.Hull - hull);
            int hullPaid = previousStage == NavalBattleStage.Boarding ? 0 : bloodPayment;
            int crewPaid = previousStage == NavalBattleStage.Boarding ? bloodPayment : 0;
            r.HullBloodPaid += hullPaid; r.CrewBloodPaid += crewPaid;
            r.HullGrossLoss += hullPaid + Math.Max(0, hull - hullPaid - s.Hull);
            r.HullEffectiveHealing += Math.Max(0, s.Hull - hull + hullPaid);
            // Core pays blood before healing. Record that intermediate point so
            // a mixed card cannot conceal payment behind same-action recovery.
            r.MinHull = Math.Min(r.MinHull, Math.Min(hull - hullPaid, s.Hull));
            if (previousStage != NavalBattleStage.Boarding && b.Stage == NavalBattleStage.Boarding)
            {
                if (b.TrainingStartingCrewHealth.HasValue)
                {
                    crew -= b.TrainingBoardingCarryover;
                    r.BoardingCarryover += b.TrainingBoardingCarryover;
                }
                else
                {
                    int navalDamage = Math.Min(s.MaxHull, Math.Max(0, b.NavalStartPlayerHull - Math.Max(0, s.Hull)));
                    long denominator = 2L * s.MaxHull, numerator = (long)b.PlayerMaxBoardingHealth * (denominator - navalDamage);
                    crew = (int)Math.Max(1L, Math.Min(b.PlayerMaxBoardingHealth, (numerator + denominator - 1L) / denominator));
                    r.BoardingCarryover += b.PlayerMaxBoardingHealth - crew;
                }
            }
            r.MinCrew = Math.Min(r.MinCrew, Math.Min(crew - crewPaid, b.PlayerBoardingHealth));
            if (b.Stage != NavalBattleStage.Boarding) return;
            r.CrewLoss += Math.Max(0, crew - b.PlayerBoardingHealth); r.CrewHealed += Math.Max(0, b.PlayerBoardingHealth - crew);
            r.CrewGrossLoss += crewPaid + Math.Max(0, crew - crewPaid - b.PlayerBoardingHealth);
            r.CrewEffectiveHealing += Math.Max(0, b.PlayerBoardingHealth - crew + crewPaid);
        }
        private static int BestCard(CoreVoyageSession s, string tactic)
        {
            int best = -1; double value = .05;
            for (int i = 0; i < s.Battle.Hand.Count; i++)
            {
                if (!s.CanPlayCard(i)) continue;
                double score = Score(s, s.Battle.Hand[i], tactic);
                if (score > value) { value = score; best = i; }
            }
            return best;
        }
        private static int Incoming(CoreVoyageSession s)
        {
            string intent = s.GetEnemyIntent() ?? "";
            var match = Regex.Match(intent, "(?:炮击伤害 |白刃伤害 |炮击：伤害 |白刃突击：船员伤害 |敌方先手：船员伤害 )(\\d+)");
            if (s.Phase == CoreDemoPhase.Battle && s.Battle.Profile.TrainingTarget != null &&
                s.Battle.Stage != NavalBattleStage.Preparation && !intent.Contains("敌方无法行动") &&
                !match.Success)
                throw new InvalidOperationException("Unparsed training attack intent: " + intent);
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }
        private static double Score(CoreVoyageSession s, int index, string tactic)
        {
            var b = s.Battle; var card = s.GetSupplyAdjustedBattleCard(index); var fx = s.GetBattleCardEffects(card, b.Stage); var e = card.SchoolEffect; var st = b.Schools;
            bool boarding = b.Stage == NavalBattleStage.Boarding, preparing = b.Stage == NavalBattleStage.Preparation;
            bool boardFirst = tactic == "boarding", burning = b.EnemyBurnTurns > 0 && b.EnemyBurnDamage > 0;
            int targetDistance = boardFirst ? 60 : s.GetBattleDeck(NavalBattleStage.Naval).Select(s.GetBattleCard).Any(c => c.Action == NavalBattleCardAction.Sabotage) ? 150 : 350;
            int hp = boarding ? b.PlayerBoardingHealth : s.Hull, maxHp = boarding ? b.PlayerMaxBoardingHealth : s.MaxHull;
            int enemyHp = boarding ? b.EnemyBoardingHealth : b.EnemyHull;
            int attack = Incoming(s), cost = s.GetBattleCardEnergyCost(index), damage = s.GetBattleCardDamage(card, b.Stage);
            int need = preparing ? Math.Max(0, (b.Profile?.CannonDamage ?? 18) - Math.Max(0, (s.Armor - 4) / 2) - b.PlayerBlock) : Math.Max(0, attack - b.PlayerBlock);
            double scale = boarding ? (double)fx.BoardingScale : 1d;
            double healWeight = hp < maxHp * .45 ? 2.5 : .9, attackWeight = !boarding && boardFirst ? .65 : 1.6;
            double blockWeight = hp <= attack * 2 ? 2.4 : 1.1;
            double value = Math.Min(Math.Max(0, maxHp - hp), fx.Repair) * healWeight + Math.Min(need, fx.Block + (fx.Block > 0 && !boarding ? b.PlayerDeckBuff : 0)) * blockWeight;
            if (damage >= enemyHp + b.EnemyBlock && !preparing) return 10000d / (cost + .7);
            if (!preparing) value += Math.Min(enemyHp + b.EnemyBlock, damage) * attackWeight;
            double drawValue = b.Energy > cost ? (b.Hand.Count <= 3 ? 9 : b.Hand.Count <= 6 ? 5 : 1) : .2;
            value += card.Draw * drawValue;
            value += s.GetBattleCardEnergyRecovery(index) * (b.Hand.Count > 1 ? 10 : 3);
            bool evade = !boarding && !preparing && !boardFirst && s.EnemyWillRam;
            if (e == null)
            {
                switch (card.Action)
                {
                    case NavalBattleCardAction.Polish: value += Math.Min(card.StrategyAmount, Math.Max(0, card.StrategyLimit - b.PlayerDeckBuff)) * (preparing ? 6 : 2); break;
                    case NavalBattleCardAction.Load:
                    case NavalBattleCardAction.PrecisionLoad: if (!boardFirst) value += s.GetBattleLoadedShotIncrease(card) * (preparing ? 1.6 : 1); break;
                    case NavalBattleCardAction.FireRounds: if (!boardFirst && b.PlayerFireRounds < card.StrategyLimit) value += card.BurnDamage * 1.5; break;
                    case NavalBattleCardAction.Retreat: if (evade) value += 100 + card.MovementMeters * .1; else if (boardFirst || b.DistanceMeters > 350 - card.MovementMeters) value -= 25; break;
                    case NavalBattleCardAction.Approach:
                    case NavalBattleCardAction.Circle:
                    case NavalBattleCardAction.Turn:
                    case NavalBattleCardAction.Navigator:
                    case NavalBattleCardAction.Parallel:
                        if (evade) return 0;
                        if (b.DistanceMeters > targetDistance) value += Math.Min(card.MovementMeters, b.DistanceMeters - targetDistance) * .3 + 10;
                        if (boardFirst && b.DistanceMeters <= 120 && b.Alignment != NavalBattleAlignment.Parallel && (card.Action == NavalBattleCardAction.Parallel || card.Action == NavalBattleCardAction.Navigator)) value += 65;
                        break;
                }
            }
            else
            {
                double power = e.Power * scale;
                int duration = Math.Max(1, e.Duration);
                switch (e.Mechanic)
                {
                    case CoreSchoolMechanic.FireLoad: value += Math.Max(0, Math.Min(e.Secondary, e.Limit - b.PlayerFireRounds)) * e.Power * (boardFirst ? .35 : 1); break;
                    case CoreSchoolMechanic.FireIgnite:
                    case CoreSchoolMechanic.FireBoardingSeed:
                    case CoreSchoolMechanic.FireIgniteOrExtend:
                    case CoreSchoolMechanic.FireExtraTick:
                        if (damage > b.EnemyBlock) value += (burning ? Math.Max(0, power - b.EnemyBurnDamage) * duration + (b.EnemyBurnTurns < duration ? power * .5 : 0) : power * Math.Min(duration, 2) + 5) * attackWeight;
                        break;
                    case CoreSchoolMechanic.FireExtend: if (burning && b.EnemyBurnTurns < 3) value += b.EnemyBurnDamage * Math.Min(e.Power, 3 - b.EnemyBurnTurns) * .7; break;
                    case CoreSchoolMechanic.FirePrepareGuard: value += Math.Max(0, power - st.PrepareFireGuard) * .7; break;
                    case CoreSchoolMechanic.FireNaturalGuard: value += Math.Max(0, power - (st.NaturalFireGuardTurns > 1 ? st.NaturalFireGuard : 0)) * (burning ? 1.4 : .7); break;
                    case CoreSchoolMechanic.FireScorch: value += Math.Max(0, power * Math.Max(1, e.Secondary == 0 ? 2 : e.Secondary) - st.ScorchDamage * st.ScorchCharges) * (burning ? 1.4 : .4); break;
                    case CoreSchoolMechanic.ArcaneCharge:
                        int gained = Math.Min(e.Power, Math.Max(0, st.ManaLimit - st.Mana));
                        value += gained * (st.AvailableMana < 4 ? 5 : 2);
                        if (gained > 0 && b.Hand.Select(i => s.GetBattleCard(i)).Any(c => c.SchoolEffect != null && c.SchoolEffect.Threshold > st.AvailableMana && c.SchoolEffect.Threshold <= st.AvailableMana + gained && c.SchoolId == "arcane")) value += 12;
                        break;
                    case CoreSchoolMechanic.ArcaneCapacity:
                        value += Math.Min(e.Power, Math.Max(0, 6 - st.ManaLimit)) * (preparing ? 5 : 1.5);
                        break;
                    case CoreSchoolMechanic.ArcaneTemporary:
                        int handDemand = b.Hand.Where(i => i != index && s.GetBattleCardEnergyCost(i) <= b.Energy - cost).Select(s.GetBattleCard).Where(c => c.SchoolId == "arcane" && c.SchoolEffect != null).Sum(c => c.SchoolEffect.Threshold);
                        value += Math.Min(e.Power, Math.Max(0, handDemand - st.AvailableMana)) * 4;
                        break;
                    case CoreSchoolMechanic.ArcaneRelease:
                    case CoreSchoolMechanic.ArcaneWard:
                    case CoreSchoolMechanic.ArcaneMend: value -= Math.Max(0, e.Threshold - st.TemporaryMana) * (st.AvailableMana <= e.Threshold ? 1.1 : .4); break;
                    case CoreSchoolMechanic.ArcaneEcho: value += e.Power * drawValue - Math.Max(0, e.Threshold - st.TemporaryMana) * .6; break;
                    case CoreSchoolMechanic.CurseMark:
                        double damagePerMark = Math.Max(st.CurseDamagePerMark, Math.Round(e.Secondary * scale, MidpointRounding.AwayFromZero));
                        value += e.Power * damagePerMark * Math.Min(duration, 2) * attackWeight;
                        value += Math.Max(0, duration - st.CurseTurns) * st.CurseMarks * damagePerMark * .5;
                        break;
                    case CoreSchoolMechanic.CurseDetonate: value += st.CurseMarks * power * attackWeight; break;
                    case CoreSchoolMechanic.CurseWard:
                    case CoreSchoolMechanic.CurseDrain: value -= e.Threshold * st.CurseDamagePerMark * Math.Min(2, st.CurseTurns) * .7; break;
                    case CoreSchoolMechanic.TideWard:
                        if (s.GetBattleCardWoodCost(index) > 0) value += Math.Min(Math.Max(0, st.TideLimit - st.TidePower), Math.Min(fx.Block, Math.Max(0, attack - b.PlayerBlock))) * 1.2;
                        break;
                    case CoreSchoolMechanic.TideCleanse: value += b.EnemyLoadedShot * .8; break;
                    case CoreSchoolMechanic.ThunderPrime:
                    case CoreSchoolMechanic.ThunderChain: value += st.ThunderChain < st.ThunderLimit && b.Energy > cost ? 4 : 0; break;
                    case CoreSchoolMechanic.ThunderFinish: if (b.Energy > cost && st.ThunderChain < 3) value -= 9; break;
                    case CoreSchoolMechanic.ThunderEcho: if (!st.ThunderEchoUsed && st.ThunderChain >= Math.Max(1, e.Threshold)) value += e.Power * drawValue; break;
                    case CoreSchoolMechanic.ThunderReady: value += Math.Max(0, e.Power - st.ThunderReadyDamage) * (preparing ? 1.6 : b.Energy > cost ? 1.2 : .2); break;
                    case CoreSchoolMechanic.BloodRisk:
                    case CoreSchoolMechanic.BloodWard: value -= e.Threshold * healWeight; break;
                    case CoreSchoolMechanic.BloodDrain: value += Math.Min(maxHp - hp, Math.Min(e.Limit > 0 ? e.Limit : int.MaxValue, Math.Max(0, damage - b.EnemyBlock) * e.Power / 100d)) * healWeight; break;
                }
                if (e.Movement > 0 && !boarding)
                {
                    if (evade) return 0;
                    if (b.DistanceMeters > targetDistance) value += Math.Min(card.MovementMeters, b.DistanceMeters - targetDistance) * .3 + 10;
                    if (boardFirst && b.DistanceMeters <= 120 && b.Alignment != NavalBattleAlignment.Parallel) value += 65;
                }
                if (e.Movement < 0 && !boarding)
                { if (evade) value += 100 + card.MovementMeters * .1; else if (boardFirst || b.DistanceMeters > 350 - card.MovementMeters) value -= 25; }
            }
            return value / (cost + .7);
        }
        private const string ResultHeader = "deck,school,size,coreCopies,rank,tactic,supply,target,seed,outcome,turns,cards,schoolCards,initialPowder,initialCannonballs,initialWood,initialMedicine,powderUsed,cannonballsUsed,woodUsed,medicineUsed,totalSuppliesUsed,energySpent,hullDamageTaken,crewDamageTaken,boardingCarryover,hullHealed,crewHealed,hull,crewHealth,enemyHull,enemyCrew,noLegalCardTurns,declinedTurns,resourceLockedTurns,boardingFallbacks,finalStage,recipe,phaseSizes,sourceCardId,sourceKind,ordinaryCrew,energyRecovered,initialHull,maxHull,initialCrewHealth,maxCrewHealth,netHullLoss,netCrewLoss,netHullLossFraction,netCrewLossFraction,hullRemainingFraction,crewRemainingFraction,minHull,minCrewHealth,minHullFraction,minCrewFraction,hullBloodPaid,crewBloodPaid,grossHullLossIncludingBlood,grossCrewLossExcludingCarryoverIncludingBlood,effectiveHullRecovered,effectiveCrewRecovered,hullOtherDamage,crewOtherDamage,criticalVictory,victoryHealthScore";
        private static void WriteResults(string output, List<Result> rows, int seeds, List<Deck> decks, string mode)
        {
            const string header = ResultHeader;
            WriteCsv(Path.Combine(output, "battles.csv"), header, rows.Select(r => r.Values()));
            WriteCsv(Path.Combine(output, "zero-supply-wins.csv"), header, rows.Where(r => r.Supply == "none" && r.Won).Select(r => r.Values()));
            WriteCsv(Path.Combine(output, "health-summary.csv"), "deck,school,size,coreCopies,rank,tactic,supply,target,battles,wins,losses,unresolved,winRate,victoryHealthScore,meanWinNetHullLoss,meanWinNetCrewLoss,meanWinNetHullLossFraction,meanWinNetCrewLossFraction,meanWinHullRemainingFraction,meanWinCrewRemainingFraction,criticalWins,criticalWinRate,meanWinHullBloodPaid,meanWinCrewBloodPaid,meanWinHullGrossLoss,meanWinCrewGrossLoss,meanWinHullEffectiveHealing,meanWinCrewEffectiveHealing,meanWinBoardingCarryover,meanWinMinHullFraction,meanWinMinCrewFraction,meanWinTurns,meanWinPowder,meanWinCannonballs,meanWinWood,meanWinMedicine,meanWinTotalSupplies",
                rows.GroupBy(r => new { r.Deck, r.School, r.Size, r.Copies, r.Rank, r.Tactic, r.Supply, r.Target }).Select(g => new object[] {
                    g.Key.Deck, g.Key.School, g.Key.Size, g.Key.Copies, g.Key.Rank, g.Key.Tactic, g.Key.Supply, g.Key.Target, g.Count(), g.Count(r => r.Won), g.Count(r => r.Outcome == "loss"), g.Count(r => r.Outcome.Contains("watchdog")), g.Average(r => r.Won ? 1d : 0d), g.Average(r => r.VictoryHealthScore),
                    MeanWin(g, r => r.NetHullLoss), MeanWin(g, r => r.NetCrewLoss), MeanWin(g, r => r.NetHullLossFraction), MeanWin(g, r => r.NetCrewLossFraction), MeanWin(g, r => r.HullRemainingFraction), MeanWin(g, r => r.CrewRemainingFraction), g.Count(r => r.CriticalVictory), MeanWin(g, r => r.CriticalVictory ? 1d : 0d),
                    MeanWin(g, r => r.HullBloodPaid), MeanWin(g, r => r.CrewBloodPaid), MeanWin(g, r => r.HullGrossLoss), MeanWin(g, r => r.CrewGrossLoss), MeanWin(g, r => r.HullEffectiveHealing), MeanWin(g, r => r.CrewEffectiveHealing), MeanWin(g, r => r.BoardingCarryover), MeanWin(g, r => (double)r.MinHull / r.MaxHull), MeanWin(g, r => (double)r.MinCrew / r.MaxCrew),
                    MeanWin(g, r => r.Turns), MeanWin(g, r => r.Powder), MeanWin(g, r => r.Balls), MeanWin(g, r => r.Wood), MeanWin(g, r => r.Medicine), MeanWin(g, r => r.TotalSupplies) }));
            WriteCsv(Path.Combine(output, "summary.csv"), "deck,school,size,coreCopies,rank,tactic,supply,target,battles,wins,winRate,meanWinTurns,meanWinPowder,meanWinCannonballs,meanWinWood,meanWinMedicine,meanWinTotalSupplies,meanWinHullDamage,meanWinCrewDamage,meanResourceLockedTurns,watchdogs",
                rows.GroupBy(r => new { r.Deck, r.School, r.Size, r.Copies, r.Rank, r.Tactic, r.Supply, r.Target }).Select(g => new object[] {
                    g.Key.Deck, g.Key.School, g.Key.Size, g.Key.Copies, g.Key.Rank, g.Key.Tactic, g.Key.Supply, g.Key.Target, g.Count(), g.Count(r => r.Won), g.Average(r => r.Won ? 1d : 0d),
                    MeanWin(g, r => r.Turns), MeanWin(g, r => r.Powder), MeanWin(g, r => r.Balls), MeanWin(g, r => r.Wood), MeanWin(g, r => r.Medicine), MeanWin(g, r => r.TotalSupplies),
                    MeanWin(g, r => r.HullLoss), MeanWin(g, r => r.CrewLoss), g.Average(r => r.ResourceLockedTurns), g.Count(r => r.Outcome.Contains("watchdog")) }));
            WriteCsv(Path.Combine(output, "card-use.csv"), "deck,rank,tactic,supply,cardId,turnStartHandObservations,plays,powderBlockedObservations,cannonballsBlockedObservations,woodBlockedObservations,medicineBlockedObservations,schoolResourceBlockedObservations",
                Uses.Values.OrderBy(u => u.Deck).ThenBy(u => u.Rank).ThenBy(u => u.Tactic).ThenBy(u => u.Supply).ThenBy(u => u.CardId).Select(u => new object[] { u.Deck, u.Rank, u.Tactic, u.Supply, u.CardId, u.Seen, u.Played, u.PowderBlocked, u.BallsBlocked, u.WoodBlocked, u.MedicineBlocked, u.SchoolBlocked }));
            WriteCsv(Path.Combine(output, "card-coverage.csv"), "cardId,school,isSource,seen,plays",
                CoreVoyageSession.CardLibrary.Select(c => new object[] { c.Id, c.SchoolId, CoreVoyageSession.IsSpecialBattleCard(c.Id), Uses.Values.Where(u => u.CardId == c.Id).Sum(u => u.Seen), Uses.Values.Where(u => u.CardId == c.Id).Sum(u => u.Played) }));
            var full = rows.Where(r => r.Supply == "full").ToDictionary(PairKey);
            WriteCsv(Path.Combine(output, "supply-comparison.csv"), "deck,school,rank,tactic,target,seed,supply,fullOutcome,outcome,winDelta,commonWinTurnsDelta,commonWinPowderDelta,commonWinCannonballsDelta,commonWinWoodDelta,commonWinMedicineDelta,commonWinTotalSuppliesDelta",
                rows.Where(r => r.Supply != "full").Select(r => {
                    var f = full[PairKey(r)]; bool both = r.Won && f.Won;
                    return new object[] { r.Deck, r.School, r.Rank, r.Tactic, r.Target, r.Seed, r.Supply, f.Outcome, r.Outcome, (r.Won ? 1 : 0) - (f.Won ? 1 : 0),
                        both ? (object)(r.Turns - f.Turns) : "", both ? (object)(r.Powder - f.Powder) : "", both ? (object)(r.Balls - f.Balls) : "", both ? (object)(r.Wood - f.Wood) : "", both ? (object)(r.Medicine - f.Medicine) : "", both ? (object)(r.TotalSupplies - f.TotalSupplies) : "" };
                }));
            var report = new StringBuilder();
            report.AppendLine("# Controlled training-target deck/supply audit"); report.AppendLine();
            report.AppendLine("Version: " + Version + ", mode: " + mode + ". " + rows.Count + " training battles; " + rows.Select(r => r.Deck).Distinct().Count() + " tested recipes / " + decks.Count(d => d.Valid) + " feasible recipes / " + decks.Count + " requested recipes; ranks " + string.Join("/", rows.Select(r => r.Rank).Distinct().OrderBy(x => x)) + ", 2 tactics, " + rows.Select(r => r.Target).Distinct().Count() + " fixed Core targets, " + rows.Select(r => r.Supply).Distinct().Count() + " supply conditions, " + seeds + " paired seeds (" + rows.Select(r => r.Seed).OrderBy(x => x).First() + " through " + rows.Select(r => r.Seed).OrderBy(x => x).Last() + "). This is an explicitly configured training-target experiment, not a test against campaign enemies. Card actions, costs, hand cycling, stages, guard, status, healing, and boarding carry-over use the real Core rules.");
            report.AppendLine();
            report.AppendLine("Every base card is owned twice per phase. Each recipe uses one school plus ordinary cards. Baseline phase sizes are 10/15/20; search adds every attainable uniform size 10..20, bounded economy/defensive priority menus and four mixed-stage size vectors. Duplicate card multisets are removed. Impossible recipes remain infeasible in deck-manifest.csv and are not padded with a third copy or source cards. Ordinary menus dynamically include every legal current base-card ID, including the new energy and draw cards. Actual school fraction is reported per phase. Mixed sizes use size=0 and phaseSizes=preparation/naval/boarding.");
            report.AppendLine();
            report.AppendLine("Full supplies are powder 25, cannonballs 40, wood 18 and medicine 10. None sets all four to zero; each no-item condition removes only its named item, including no-wood. Water/food remain 20. Every case uses the same patrol ship, equipment quality 2 and no component bonuses. Main matrices use 22 ordinary crew and no source cards. Sources mode adds exactly one real source through the normal API: crew sources occupy one berth (21 ordinary + 1 special), components keep 22 ordinary; same-ordinary-crew no-source controls are matched separately. Bonus sources do not count toward base deck size. Fixed equipment bonuses isolate card sensitivity, not a complete loadout. Total supplies is the unweighted sum of all four actual inventory deductions; final inventory equality is checked separately for each. Any no-item fallback uses the Core quote and execution.");
            report.AppendLine();
            report.AppendLine("Targets start motionless at 300 m. Their initial HP, per-turn base cannon/melee damage, guard and repair come directly from CoreTrainingTargets and targets.csv. Normal armor, curses, tide effects, burning and boarding health carry-over still apply; the realized damage is not constant. The captain never rams. Cannon preference legally falls back to boarding when the naval deck has no attack that its remaining ammunition can support; boarding preference approaches and uses TryBoard. No cards/HP/mana/ammunition are injected during battle. TrainingOutcome determines wins; no campaign reward can offset measured consumption.");
            report.AppendLine();
            report.AppendLine("Winning-cost columns summarize wins only; no-win cells are blank. Watchdogs are incomplete trials, never wins. Legacy damage/heal columns retain observed action-boundary decreases/increases. New gross loss and effective recovery columns split successful BloodRisk/BloodWard payment before same-action healing; health conservation is verified. Boarding initialization loss is separate from crew damage and included once in net crew loss. Initial/final/max HP and the minimum after actions and blood payment are recorded. Critical victory means either final pool is at most 25% of its own maximum. Victory health score is 100 * mean(win ? (finalHull/maxHull + finalCrew/maxCrew)/2 : 0); losses and watchdogs contribute zero. This transparent reference combines winning and retained health, not supplies or an objective total strength. Card lock columns are the first public unavailability reason at turn start. These deterministic greedy recipes are a bounded comparison, not exhaustive deck optimization or a guarantee that every active card was played. card-coverage.csv includes zero-play and untested IDs.");
            report.AppendLine("This is a single-battle endurance assessment, not a consecutive-battle simulation. Current Core training initialization resets crew health, including boarding entry; repeating that API would incorrectly replenish crew health between bouts. No such reset is presented as a no-healing streak.");
            report.AppendLine("Search uses targets II and IV with full/none conditions. For each school/rank/tactic it selects at most two recipes: highest full-supply win count then lowest win turns; and among equal-best full win counts, lowest winning counted-item cost then strongest no-supply result. Selection never reads holdout outcomes. Holdout uses only the selected recipe/rank/tactic combinations, all five targets and supply conditions, and disjoint seeds numbered at least 1000. This does not exhaust possible card subsets, phase compositions, orderings or action strategies; economy/defensive describe deck priority menus, while the same fixed action scoring applies throughout.");
            report.AppendLine();
            report.AppendLine("| School | Battles | Win rate | Win turns | Win powder | Win balls | Win wood | Win medicine | Win total | Timeouts |");
            report.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
            foreach (var g in rows.Where(r => r.Supply == "full").GroupBy(r => r.School)) report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "| {0} | {1} | {2:P1} | {3:F2} | {4:F2} | {5:F2} | {6:F2} | {7:F2} | {8:F2} | {9} |", g.Key, g.Count(), g.Average(r => r.Won ? 1d : 0d), MeanWin(g, r => r.Turns), MeanWin(g, r => r.Powder), MeanWin(g, r => r.Balls), MeanWin(g, r => r.Wood), MeanWin(g, r => r.Medicine), MeanWin(g, r => r.TotalSupplies), g.Count(r => r.Outcome.Contains("watchdog"))));
            report.AppendLine();
            report.AppendLine("The table is descriptive and includes unequal feasible recipe counts, so use rank/target/size-matched rows in summary.csv for comparisons. supply-comparison.csv pairs every deprivation trial to its identical full-supply seed. traces/ and trace-cases.csv reproduce one strongest-target zero-supply win per school/rank/tactic when available.");
            File.WriteAllText(Path.Combine(output, "report.md"), report.ToString(), new UTF8Encoding(true));
            WriteZeroSupplyTraces(output, rows, decks);
        }
        private static void SelectRecipes(string output, List<Result> rows)
        {
            var selected = new List<string> { "deck\trank\ttactic\tobjective" };
            foreach (var group in rows.GroupBy(r => new { r.School, r.Rank, r.Tactic }))
            {
                var candidates = group.GroupBy(r => r.Deck).Select(g => new {
                    Deck = g.Key, FullWins = g.Count(r => r.Supply == "full" && r.Won), ZeroWins = g.Count(r => r.Supply == "none" && r.Won),
                    Turns = g.Where(r => r.Supply == "full" && r.Won).Select(r => (double)r.Turns).DefaultIfEmpty(9999).Average(),
                    Cost = g.Where(r => r.Supply == "full" && r.Won).Select(r => (double)(r.TotalSupplies)).DefaultIfEmpty(9999).Average()
                }).ToArray();
                var fast = candidates.OrderByDescending(x => x.FullWins).ThenBy(x => x.Turns).ThenBy(x => x.Cost).ThenBy(x => x.Deck).First();
                var cheap = candidates.Where(x => x.FullWins == fast.FullWins).OrderBy(x => x.Cost).ThenByDescending(x => x.ZeroWins).ThenBy(x => x.Turns).ThenBy(x => x.Deck).First();
                selected.Add(fast.Deck + "\t" + group.Key.Rank + "\t" + group.Key.Tactic + "\tfull-wins-then-speed");
                if (cheap.Deck != fast.Deck) selected.Add(cheap.Deck + "\t" + group.Key.Rank + "\t" + group.Key.Tactic + "\tfull-wins-then-economy");
            }
            File.WriteAllLines(Path.Combine(output, "selected-decks.tsv"), selected, new UTF8Encoding(false));
        }
        private static void WriteSourceComparison(string output, List<Result> rows)
        {
            Func<Result, string> key = r => r.School + "/" + r.OrdinaryCrew + "/" + r.Rank + "/" + r.Tactic + "/" + r.Target + "/" + r.Seed + "/" + r.Supply;
            var controls = rows.Where(r => r.SourceCardId.Length == 0).ToDictionary(key);
            WriteCsv(Path.Combine(output, "source-comparison.csv"), "sourceCardId,sourceKind,school,ordinaryCrew,rank,tactic,target,seed,supply,controlOutcome,outcome,winDelta,commonWinTurnsDelta,commonWinPowderDelta,commonWinCannonballsDelta,commonWinWoodDelta,commonWinMedicineDelta,commonWinTotalSuppliesDelta",
                rows.Where(r => r.SourceCardId.Length > 0).Select(r => {
                    var c = controls[key(r)]; bool both = c.Won && r.Won;
                    return new object[] { r.SourceCardId, r.SourceKind, r.School, r.OrdinaryCrew, r.Rank, r.Tactic, r.Target, r.Seed, r.Supply, c.Outcome, r.Outcome, (r.Won ? 1 : 0) - (c.Won ? 1 : 0),
                        both ? (object)(r.Turns - c.Turns) : "", both ? (object)(r.Powder - c.Powder) : "", both ? (object)(r.Balls - c.Balls) : "", both ? (object)(r.Wood - c.Wood) : "", both ? (object)(r.Medicine - c.Medicine) : "", both ? (object)(r.TotalSupplies - c.TotalSupplies) : "" };
                }));
        }
        private static string PairKey(Result r) => r.Deck + "/" + r.Rank + "/" + r.Tactic + "/" + r.Target + "/" + r.Seed;
        private static void MergeParts(string output, string partsRoot, int seeds, string mode, string schoolFilter)
        {
            var directories = Directory.GetDirectories(partsRoot).OrderBy(x => x).ToArray();
            var rows = new List<Result>(); var keys = new HashSet<string>(); string coreHash = null, toolHash = null;
            foreach (string part in directories)
            {
                if (!File.Exists(Path.Combine(part, "completed.txt"))) throw new InvalidOperationException("Incomplete shard: " + part);
                string core = File.ReadAllText(Path.Combine(part, "core-source-sha256.txt")), tool = File.ReadAllText(Path.Combine(part, "benchmark-source-sha256.txt"));
                if (coreHash != null && (core != coreHash || tool != toolHash)) throw new InvalidOperationException("Mixed shard versions");
                coreHash = core; toolHash = tool;
                foreach (var a in ReadCsv(Path.Combine(part, "battles.csv")))
                {
                    Func<string, int> n = name => int.Parse(a[name]);
                    var r = new Result { Deck=a["deck"], School=a["school"], Size=n("size"), Copies=n("coreCopies"), Rank=n("rank"), Tactic=a["tactic"], Supply=a["supply"], Target=a["target"], Seed=a["seed"], Outcome=a["outcome"],
                        Turns=n("turns"), Cards=n("cards"), SchoolCards=n("schoolCards"), InitialPowder=n("initialPowder"), InitialBalls=n("initialCannonballs"), InitialWood=n("initialWood"), InitialMedicine=n("initialMedicine"), Powder=n("powderUsed"), Balls=n("cannonballsUsed"), Wood=n("woodUsed"), Medicine=n("medicineUsed"), Energy=n("energySpent"), HullLoss=n("hullDamageTaken"), CrewLoss=n("crewDamageTaken"), BoardingCarryover=n("boardingCarryover"), HullHealed=n("hullHealed"), CrewHealed=n("crewHealed"), Hull=n("hull"), Crew=n("crewHealth"), EnemyHull=n("enemyHull"), EnemyCrew=n("enemyCrew"), NoLegalTurns=n("noLegalCardTurns"), DeclinedTurns=n("declinedTurns"), ResourceLockedTurns=n("resourceLockedTurns"), BoardingFallbacks=n("boardingFallbacks"), Stage=a["finalStage"], Recipe=a["recipe"], PhaseSizes=a["phaseSizes"], SourceCardId=a["sourceCardId"], SourceKind=a["sourceKind"], OrdinaryCrew=n("ordinaryCrew"), EnergyRecovered=n("energyRecovered") };
                    r.InitialHull=n("initialHull"); r.MaxHull=n("maxHull"); r.InitialCrew=n("initialCrewHealth"); r.MaxCrew=n("maxCrewHealth"); r.MinHull=n("minHull"); r.MinCrew=n("minCrewHealth");
                    r.HullBloodPaid=n("hullBloodPaid"); r.CrewBloodPaid=n("crewBloodPaid"); r.HullGrossLoss=n("grossHullLossIncludingBlood"); r.CrewGrossLoss=n("grossCrewLossExcludingCarryoverIncludingBlood"); r.HullEffectiveHealing=n("effectiveHullRecovered"); r.CrewEffectiveHealing=n("effectiveCrewRecovered");
                    if (r.NetHullLoss != r.HullGrossLoss - r.HullEffectiveHealing || r.NetCrewLoss != r.BoardingCarryover + r.CrewGrossLoss - r.CrewEffectiveHealing || r.NetHullLoss != n("netHullLoss") || r.NetCrewLoss != n("netCrewLoss")) throw new InvalidOperationException("Merged health accounting mismatch");
                    if (!keys.Add(PairKey(r) + "/" + r.Supply)) throw new InvalidOperationException("Duplicate trial key");
                    if (r.TotalSupplies != n("totalSuppliesUsed") || r.Powder < 0 || r.Powder > r.InitialPowder || r.Balls < 0 || r.Balls > r.InitialBalls || r.Wood < 0 || r.Wood > r.InitialWood || r.Medicine < 0 || r.Medicine > r.InitialMedicine) throw new InvalidOperationException("Invalid merged supply accounting");
                    rows.Add(r);
                }
                foreach (var a in ReadCsv(Path.Combine(part, "card-use.csv")))
                {
                    var u = GetUse(new Result { Deck=a["deck"], Rank=int.Parse(a["rank"]), Tactic=a["tactic"], Supply=a["supply"] }, a["cardId"]);
                    u.Seen += int.Parse(a["turnStartHandObservations"]); u.Played += int.Parse(a["plays"]);
                    u.PowderBlocked += int.Parse(a["powderBlockedObservations"]); u.BallsBlocked += int.Parse(a["cannonballsBlockedObservations"]); u.WoodBlocked += int.Parse(a["woodBlockedObservations"]); u.MedicineBlocked += int.Parse(a["medicineBlockedObservations"]); u.SchoolBlocked += int.Parse(a["schoolResourceBlockedObservations"]);
                }
            }
            string[] expectedSupplies = mode == "pilot" || mode == "search" || mode == "sources" ? new[] { "full", "none" } : Supplies;
            foreach (var g in rows.GroupBy(PairKey)) if (!g.Select(r => r.Supply).OrderBy(x => x).SequenceEqual(expectedSupplies.OrderBy(x => x))) throw new InvalidOperationException("Incomplete supply pairing");
            if (rows.Select(r => r.Seed).Distinct().Count() != seeds || Uses.Values.Sum(u => (long)u.Played) != rows.Sum(r => (long)r.Cards)) throw new InvalidOperationException("Merged seed/participation mismatch");
            var decks = mode == "search" || mode == "holdout" ? BuildSearchDecks() : mode == "sources" ? BuildSourceDecks() : BuildDecks();
            if (schoolFilter.Length > 0) decks = decks.Where(d => schoolFilter.Split(',').Contains(d.School)).ToList();
            Directory.CreateDirectory(output); WriteDeckManifest(output, decks);
            File.Copy(Path.Combine(directories[0], "targets.csv"), Path.Combine(output, "targets.csv"), true);
            File.WriteAllText(Path.Combine(output, "core-source-sha256.txt"), coreHash); File.WriteAllText(Path.Combine(output, "benchmark-source-sha256.txt"), toolHash);
            WriteResults(output, rows, seeds, decks, mode);
            if (mode == "search") SelectRecipes(output, rows);
            if (mode == "sources") WriteSourceComparison(output, rows);
            File.WriteAllText(Path.Combine(output, "merge-checks.txt"), "Completed shards: " + directories.Length + "\nUnique persisted trials: " + rows.Count + "\nSupply pairs and participation sums matched.\nTraces replayed against merged results.\n");
            Console.WriteLine("Merged and verified " + rows.Count + " persisted trials.");
        }
        private static IEnumerable<Dictionary<string, string>> ReadCsv(string path)
        {
            using (var reader = new StreamReader(path))
            {
                string[] header = ParseCsvLine(reader.ReadLine()); string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] values = ParseCsvLine(line);
                    if (values.Length != header.Length) throw new InvalidOperationException("CSV width mismatch: " + path);
                    yield return Enumerable.Range(0, header.Length).ToDictionary(i => header[i], i => values[i]);
                }
            }
        }
        private static string[] ParseCsvLine(string line)
        {
            var cells = new List<string>(); var cell = new StringBuilder(); bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"') { if (quoted && i + 1 < line.Length && line[i + 1] == '"') { cell.Append('"'); i++; } else quoted = !quoted; }
                else if (c == ',' && !quoted) { cells.Add(cell.ToString()); cell.Clear(); }
                else cell.Append(c);
            }
            if (quoted) throw new InvalidOperationException("Unterminated CSV quote");
            cells.Add(cell.ToString()); return cells.ToArray();
        }
        private static void ReplayCases(string output, string selection)
        {
            var decks = BuildDecks().Concat(BuildSearchDecks()).Concat(BuildSourceDecks()).Where(d => d.Valid).GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.First());
            var cases = new List<object[]>(); int number = 0;
            foreach (string line in File.ReadAllLines(selection).Skip(1).Where(l => l.Length > 0))
            {
                var a = line.Split('\t'); var trace = new List<object[]>();
                var result = Run(decks[a[0]], int.Parse(a[1]), a[2], Targets.Single(t => t.Id == a[3]), a[4], a[5], trace);
                string[] actual = { result.Outcome, result.Turns.ToString(), result.Cards.ToString(), result.Hull.ToString(), result.Crew.ToString(), result.Powder.ToString(), result.Balls.ToString(), result.Wood.ToString(), result.Medicine.ToString() };
                if (!actual.SequenceEqual(a.Skip(6))) throw new InvalidOperationException("Selected trace differs from original result: " + line);
                string file = (++number).ToString("D3") + "-" + result.School + "-rank" + result.Rank + "-" + result.Outcome + ".csv";
                WriteCsv(Path.Combine(output, file), "step,captainTurn,stage,action,energy,hull,crewHealth,enemyHull,enemyCrew,playerGuard,enemyGuard,distance,powder,cannonballs,wood,medicine,mana,curseMarks,burnDamage,burnTurns,trainingOutcome,handAfterAction,manaLimit,temporaryMana,tidePower,tidePaidBlock,thunderChain,thunderReadyDamage,curseDamagePerMark,curseNextTickBonus", trace);
                cases.Add(new object[] { result.Deck, result.School, result.Rank, result.Tactic, result.Target, result.Supply, result.Seed, result.Outcome, result.Turns, result.Cards, file, "replay-matched" });
            }
            WriteCsv(Path.Combine(output, "trace-cases.csv"), "deck,school,rank,tactic,target,supply,seed,outcome,turns,cards,trace,status", cases);
        }
        private static object MeanWin(IEnumerable<Result> rows, Func<Result, double> value)
        { var wins = rows.Where(r => r.Won).ToArray(); return wins.Length == 0 ? (object)string.Empty : wins.Average(value); }
        private static void WriteZeroSupplyTraces(string output, List<Result> rows, List<Deck> decks)
        {
            string directory = Path.Combine(output, "traces"); Directory.CreateDirectory(directory);
            var cases = new List<object[]>();
            foreach (var group in rows.Where(r => r.Supply == "none" && r.Won).GroupBy(r => new { r.School, r.Rank, r.Tactic }))
            {
                var original = group.OrderByDescending(r => Array.FindIndex(Targets, t => t.Id == r.Target)).ThenBy(r => r.Turns).ThenBy(r => r.Deck).ThenBy(r => r.Seed).First();
                var trace = new List<object[]>();
                var replay = Run(decks.Single(d => d.Id == original.Deck), original.Rank, original.Tactic, Targets.Single(t => t.Id == original.Target), original.Supply, original.Seed, trace);
                if (replay.Outcome != original.Outcome || replay.Turns != original.Turns || replay.Cards != original.Cards || replay.Hull != original.Hull || replay.Crew != original.Crew || replay.Powder != original.Powder || replay.Balls != original.Balls || replay.Wood != original.Wood || replay.Medicine != original.Medicine)
                    throw new InvalidOperationException("Zero-supply replay diverged: " + PairKey(original));
                string file = original.School + "-rank" + original.Rank + "-" + original.Tactic + ".csv";
                WriteCsv(Path.Combine(directory, file), "step,captainTurn,stage,action,energy,hull,crewHealth,enemyHull,enemyCrew,playerGuard,enemyGuard,distance,powder,cannonballs,wood,medicine,mana,curseMarks,burnDamage,burnTurns,trainingOutcome,handAfterAction,manaLimit,temporaryMana,tidePower,tidePaidBlock,thunderChain,thunderReadyDamage,curseDamagePerMark,curseNextTickBonus", trace);
                cases.Add(new object[] { original.Deck, original.School, original.Rank, original.Tactic, original.Target, original.Seed, original.Turns, original.Cards, "traces/" + file, "replay-matched" });
            }
            WriteCsv(Path.Combine(output, "trace-cases.csv"), "deck,school,rank,tactic,target,seed,turns,cards,trace,status", cases);
        }
        private static void WriteCsv(string path, string header, IEnumerable<object[]> rows)
        {
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(true)))
            { writer.WriteLine(header); foreach (var row in rows) writer.WriteLine(string.Join(",", row.Select(x => "\"" + Convert.ToString(x, CultureInfo.InvariantCulture).Replace("\"", "\"\"") + "\""))); }
        }
    }
}

