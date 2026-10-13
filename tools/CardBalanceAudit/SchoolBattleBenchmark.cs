using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Tools.CardBalanceAudit
{
    // This is a deterministic, observable captain using the real Core battle engine.
    // Reflection skips navigation only. No hand, health, enemy, RNG or school-resource
    // state is overwritten after StartBattle. Every subsequent action uses a public rule.
    public static class SchoolBattleBenchmark
    {
        private const int TurnLimit = 100, ActionLimit = 64;
        private static readonly MethodInfo StartBattle = typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly string[] Schools = { "ordinary", "fire", "arcane", "curse", "tide", "thunder", "blood" };
        private const string BenchmarkVersion = "school-review-20261012-v3";
        private static readonly string[] Enemies = { "event_reef_coral_ambush", "gold_customs", "moon_ghost",
            "boss_route_shattered_sail_reef", "boss_route_golden_tide_trade", "boss_route_moon_tide_ruins",
            CoreBattleBalance.TreasureBossId, "legendary_pirate_iron_oath", "legendary_pirate_thunder_crown",
            "legendary_pirate_red_widow", "legendary_pirate_ember_ark", "legendary_pirate_tide_requiem" };
        private static readonly int[] Routes = { 0, 1, 2, 0, 1, 2, 2, 2, 2, 2, 2, 2 };
        private static readonly string[][] Support = {
            new[] { "card_deck_polish", "card_load_shot", "card_full_sail_turn", "card_full_sail_turn", "card_approach", "card_parallel_course", "card_brace", "card_brace" },
            new[] { "card_chain_shot", "card_cannon_barrage", "card_full_sail_turn", "card_retreat", "card_emergency_repair", "card_parallel_course" },
            new[] { "card_boarding_hook", "card_crew_thrust", "card_cutlass_raid", "card_crew_guard", "card_crew_rally", "card_crew_bandage" }
        };
        private static readonly string[][] OrdinaryReplacement = {
            new[] { "card_deck_polish", "card_load_shot" },
            new[] { "card_chain_shot", "card_cannon_barrage", "card_brace", "card_evasive_maneuver" },
            new[] { "card_boarding_hook", "card_crew_thrust", "card_cutlass_raid", "card_crew_guard" }
        };
        private static readonly Dictionary<string, int> Index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .ToDictionary(i => CoreVoyageSession.CardLibrary[i].Id, i => i);
        private static readonly Dictionary<string, CardUse> Uses = new Dictionary<string, CardUse>();
        private sealed class CardUse
        {
            public string School, Tactic, Id, Source; public int Rank, Seen, Played, ResourceBlocked, EnergyBlocked, RangeBlocked;
        }
        private sealed class Result
        {
            public string School, Tactic, Enemy, Seed, Outcome, FinalStage, Source;
            public int Rank, Turns, Cards, SchoolCards, NoLegalCardTurns, DeclinedTurns, HullLoss, CrewLoss,
                HullHealed, CrewHealed, BoardingCarryover, ResourceLockedTurns, Energy, Powder, Balls, Medicine, Guard, Rams, Hull, Crew, EnemyHull, EnemyCrew;
            public string Difficulty;
            public double EnemyHullFraction, EnemyCrewFraction;
            public bool Won => Outcome == "win";
            public object[] Values() => new object[] { School, Rank, Tactic, Enemy, Seed, Outcome, Turns, Cards, SchoolCards,
                NoLegalCardTurns, DeclinedTurns, HullLoss, CrewLoss, HullHealed, CrewHealed, Energy, Powder, Balls, Medicine,
                Guard, Rams, Hull, Crew, EnemyHull, EnemyCrew, FinalStage, Difficulty, BoardingCarryover,
                ResourceLockedTurns, EnemyHullFraction, EnemyCrewFraction, Source };
        }
        public static int Main(string[] args)
        {
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                int seeds = args.Length > 0 ? Math.Max(1, int.Parse(args[0])) : 16;
                string output = args.Length > 1 ? args[1] : "SchoolBattleBenchmark";
                bool sourceSweep = args.Length > 2 && args[2] == "sources";
                Directory.CreateDirectory(output);
                var results = new List<Result>();
                if (sourceSweep)
                {
                    // Isolate each real source in its matching basic school deck. A
                    // crew source occupies one berth; components add no crew. This
                    // sweep is compared per source across versions, never pooled
                    // with the no-source school control as a claim of +30% power.
                    foreach (var source in CoreSpecialCardCatalog.All)
                    {
                        string school = source.Card.SchoolId.Length == 0 ? "ordinary" : source.Card.SchoolId;
                        for (int rank = 0; rank <= 3; rank++)
                            foreach (string tactic in new[] { "cannon", "boarding" })
                                foreach (int enemy in new[] { 5, 6 })
                                    for (int seed = 0; seed < seeds; seed++)
                                        results.Add(Run(school, rank, tactic, enemy, "SCHOOL-BALANCE-" + seed.ToString("D3"), source));
                        Console.WriteLine(source.Card.Id + ": " + results.Count + " source battles complete");
                    }
                }
                else foreach (string school in Schools)
                {
                    for (int rank = 0; rank <= 3; rank++)
                        foreach (string tactic in new[] { "cannon", "boarding" })
                            for (int enemy = 0; enemy < Enemies.Length; enemy++)
                                for (int seed = 0; seed < seeds; seed++)
                                    results.Add(Run(school, rank, tactic, enemy, "SCHOOL-BALANCE-" + seed.ToString("D3")));
                    Console.WriteLine(school + ": " + results.Count + " battles complete");
                }
                WriteCsv(Path.Combine(output, "battles.csv"), "school,rank,tactic,enemy,seed,outcome,turns,cards,schoolCards,noLegalCardTurns,declinedTurns,hullDamageTaken,crewDamageTaken,hullHealed,crewHealed,energySpent,powderUsed,cannonballsUsed,medicineUsed,guardAbsorbed,playerRams,hull,crewHealth,enemyHull,enemyCrew,finalStage,difficulty,boardingCarryover,resourceLockedTurns,enemyHullFraction,enemyCrewFraction,sourceCardId", results.Select(r => r.Values()));
                WriteCsv(Path.Combine(output, "summary.csv"), "school,rank,tactic,battles,winRate,meanTurns,meanWinTurns,meanLossTurns,meanHullDamage,meanCrewDamage,meanBoardingCarryover,meanCards,meanSchoolCards,meanNoLegalCardTurns,meanDeclinedTurns,meanResourceLockedTurns,watchdogs",
                    results.GroupBy(r => new { r.School, r.Rank, r.Tactic }).Select(g => new object[] { g.Key.School, g.Key.Rank, g.Key.Tactic, g.Count(), g.Average(r => r.Won ? 1d : 0d), g.Average(r => r.Turns), ConditionalMeanTurns(g, true), ConditionalMeanTurns(g, false), g.Average(r => r.HullLoss), g.Average(r => r.CrewLoss), g.Average(r => r.BoardingCarryover), g.Average(r => r.Cards), g.Average(r => r.SchoolCards), g.Average(r => r.NoLegalCardTurns), g.Average(r => r.DeclinedTurns), g.Average(r => r.ResourceLockedTurns), g.Count(r => r.Outcome.Contains("watchdog")) }));
                WriteCsv(Path.Combine(output, "matchups.csv"), "school,rank,tactic,enemy,difficulty,battles,winRate,meanWinTurns,meanHullDamage,meanCrewDamage,meanResourceLockedTurns,meanEnemyHullFraction,meanEnemyCrewFraction,watchdogs",
                    results.GroupBy(r => new { r.School, r.Rank, r.Tactic, r.Enemy, r.Difficulty }).Select(g => new object[] { g.Key.School, g.Key.Rank, g.Key.Tactic, g.Key.Enemy, g.Key.Difficulty, g.Count(), g.Average(r => r.Won ? 1d : 0d), ConditionalMeanTurns(g, true), g.Average(r => r.HullLoss), g.Average(r => r.CrewLoss), g.Average(r => r.ResourceLockedTurns), g.Average(r => r.EnemyHullFraction), g.Average(r => r.EnemyCrewFraction), g.Count(r => r.Outcome.Contains("watchdog")) }));
                WriteCsv(Path.Combine(output, "card-use.csv"), "school,rank,tactic,cardId,turnStartHandObservations,plays,resourceBlockedObservations,energyBlockedObservations,rangeBlockedObservations,sourceCardId",
                    Uses.Values.OrderBy(u => u.School).ThenBy(u => u.Rank).ThenBy(u => u.Tactic).ThenBy(u => u.Id).Select(u => new object[] { u.School, u.Rank, u.Tactic, u.Id, u.Seen, u.Played, u.ResourceBlocked, u.EnergyBlocked, u.RangeBlocked, u.Source }));
                if (sourceSweep)
                    WriteCsv(Path.Combine(output, "source-summary.csv"), "sourceCardId,school,rank,tactic,battles,winRate,meanWinTurns,meanHullDamage,meanCrewDamage,sourcePlays,sourceResourceBlockedObservations",
                        results.GroupBy(r => new { r.Source, r.School, r.Rank, r.Tactic }).Select(g => new object[] { g.Key.Source, g.Key.School, g.Key.Rank, g.Key.Tactic, g.Count(), g.Average(r => r.Won ? 1d : 0d), ConditionalMeanTurns(g, true), g.Average(r => r.HullLoss), g.Average(r => r.CrewLoss), Uses.Values.Where(u => u.Source == g.Key.Source && u.Id == g.Key.Source && u.Rank == g.Key.Rank && u.Tactic == g.Key.Tactic).Sum(u => u.Played), Uses.Values.Where(u => u.Source == g.Key.Source && u.Id == g.Key.Source && u.Rank == g.Key.Rank && u.Tactic == g.Key.Tactic).Sum(u => u.ResourceBlocked) }));
                WriteCsv(Path.Combine(output, "coverage.csv"), "cardId,school,isSource,turnStartHandObservations,plays",
                    CoreVoyageSession.CardLibrary.Select(c => new object[] { c.Id, c.SchoolId, CoreVoyageSession.IsSpecialBattleCard(c.Id), Uses.Values.Where(u => u.Id == c.Id).Sum(u => u.Seen), Uses.Values.Where(u => u.Id == c.Id).Sum(u => u.Played) }));
                WriteReport(output, results, seeds, sourceSweep);
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }

        private static CoreVoyageSession Create(string school, int rank, int route, string seed, CoreSpecialCardDefinition source)
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            s.SelectShip(2); s.SelectRoute(route); s.SetSeed(seed);
            s.ConfigureCrewCapabilities(false, false, false, false, false, false);
            int specialCrew = source != null && source.SourceKind == CoreSpecialCardSourceKind.Crew ? 1 : 0;
            s.ConfigureDepartureCrew(s.CrewCountValue - specialCrew, specialCrew);
            s.ConfigureBattleEquipment(2f, 2f);
            s.ConfigureComponentBonuses(0, 0, 0, 0, 0);
            s.ConfigureSpecialCardSources(specialCrew > 0 ? new[] { source.SourceId } : Array.Empty<string>(),
                source != null && source.SourceKind == CoreSpecialCardSourceKind.Component ? new[] { source.SourceId } : Array.Empty<string>());
            s.ConfigureCardUpgrades(CoreVoyageSession.CardLibrary.Where(c => !CoreVoyageSession.IsSpecialBattleCard(c.Id)).ToDictionary(c => c.Id, c => rank));
            if (source != null) s.RestoreSpecialCardExperience(new Dictionary<string, int> { [source.Card.Id] = rank == 0 ? 0 : rank == 1 ? 120 : rank == 2 ? 360 : 840 });
            int schoolCardCount = 0;
            for (int i = 0; i < 3; i++)
            {
                var stage = (NavalBattleStage)i;
                string[] schoolIds = school == "ordinary" ? OrdinaryReplacement[i] : CoreVoyageSession.CardLibrary
                    .Where(c => c.SchoolId == school && c.RequiredCrew.Length == 0 && c.RequiredComponent.Length == 0 && c.IsAllowedInStage(stage))
                    .Select(c => c.Id).ToArray();
                if (schoolIds.Length != OrdinaryReplacement[i].Length) throw new InvalidOperationException("Unexpected basic school stage layout: " + school);
                schoolCardCount += schoolIds.Length;
                int[] deck = Support[i].Concat(schoolIds).Select(id => Index[id]).ToArray();
                if (deck.Length != 10 || !s.SetBattleDeck(stage, deck)) throw new InvalidOperationException("Illegal fixture deck: " + school + " " + stage);
                int expectedBonus = source != null && source.Card.IsAllowedInStage(stage) ? 1 : 0;
                if (s.GetBonusBattleCards(stage).Count != expectedBonus) throw new InvalidOperationException("Uncontrolled bonus source");
            }
            if (schoolCardCount != 10) throw new InvalidOperationException("Fixture must cover every basic school card exactly once");
            s.Cargo.Water = s.Cargo.Food = 20; s.Cargo.Wood = 18; s.Cargo.Medicine = 10;
            s.Cargo.Beer = s.Cargo.Iron = s.Cargo.Cloth = 0; s.Cargo.Powder = 25; s.Cargo.Cannonballs = 40;
            if (s.Cargo.UsedVolume > s.CargoCapacityValue + .001f) throw new InvalidOperationException("Overloaded fixture");
            s.StartVoyage();
            if (s.Phase != CoreDemoPhase.Sailing) throw new InvalidOperationException("Departure rejected");
            return s;
        }

        private static Result Run(string school, int rank, string tactic, int enemy, string seed, CoreSpecialCardDefinition source = null)
        {
            var s = Create(school, rank, Routes[enemy], seed, source);
            var node = s.Map.Nodes.Single(n => n.Encounter.Id == Enemies[enemy]);
            if (StartBattle == null) throw new InvalidOperationException("Missing battle entry");
            StartBattle.Invoke(s, new object[] { node.Encounter, node.IsBoss });
            var b = s.Battle;
            var profile = CoreBattleBalance.Resolve(node.Encounter, Routes[enemy], node.IsBoss);
            if (b.EnemyMaxHull != profile.EnemyHull || b.EnemyMaxBoardingHealth != profile.BoardingHealth ||
                b.Hand.Count + b.DrawPile.Count + b.DiscardPile.Count != 10 + s.GetBonusBattleCards(NavalBattleStage.Preparation).Count) throw new InvalidOperationException("Invalid real battle initialization");
            var r = new Result { School = school, Rank = rank, Tactic = tactic, Enemy = Enemies[enemy], Seed = seed,
                Source = source?.Card.Id ?? string.Empty,
                Difficulty = profile.BossKind == CoreBossKind.Legendary ? "legendary" : profile.BossKind == CoreBossKind.Treasure ? "treasure" : profile.BossKind == CoreBossKind.Route ? "route-boss" : "regular" };
            bool actionLimit = false;
            for (int turn = 0; turn < TurnLimit && s.Phase == CoreDemoPhase.Battle; turn++)
            {
                r.Turns++;
                ObserveHand(s, r);
                bool anyLegalAtStart = Enumerable.Range(0, b.Hand.Count).Any(i => s.CanPlayCard(i));
                if (!anyLegalAtStart && Enumerable.Range(0, b.Hand.Count).Any(i => IsResourceReason(s.CardUnavailableReason(i)))) r.ResourceLockedTurns++;
                int cardsBefore = r.Cards;
                int action;
                for (action = 0; action < ActionLimit && s.Phase == CoreDemoPhase.Battle; action++)
                {
                    int h = s.Hull, c = b.PlayerBoardingHealth; var stage = b.Stage;
                    if (tactic == "boarding" && s.CanBoard(out _) && s.TryBoard()) { Observe(s, b, r, h, c, stage); continue; }
                    if (tactic == "boarding" && s.Hull > 28 && s.CanRam(out _))
                    {
                        if (!s.TryRam()) throw new InvalidOperationException("Legal ram rejected");
                        r.Rams++; r.Energy++; Observe(s, b, r, h, c, stage); continue;
                    }
                    int hand = BestCard(s, tactic);
                    if (hand < 0) break;
                    int cardIndex = b.Hand[hand]; var card = s.GetBattleCard(cardIndex);
                    r.Energy += s.GetBattleCardEnergyCost(cardIndex); r.Powder += card.PowderCost; r.Balls += card.CannonballCost;
                    int medicine = s.Cargo.Medicine;
                    if (!s.PlayCard(hand)) throw new InvalidOperationException("Legal card rejected: " + card.Id);
                    r.Cards++; if (card.SchoolId.Length > 0) r.SchoolCards++;
                    GetUse(r, card.Id).Played++; r.Medicine += Math.Max(0, medicine - s.Cargo.Medicine);
                    Observe(s, b, r, h, c, stage);
                }
                if (action == ActionLimit && s.Phase == CoreDemoPhase.Battle) { actionLimit = true; break; }
                if (r.Cards == cardsBefore) { if (!anyLegalAtStart) r.NoLegalCardTurns++; else r.DeclinedTurns++; }
                if (s.Phase == CoreDemoPhase.Battle)
                {
                    int h = s.Hull, c = b.PlayerBoardingHealth; var stage = b.Stage;
                    if (!s.EndBattleTurn()) throw new InvalidOperationException("Legal end turn rejected");
                    r.Guard += b.LastEnemyGuardAbsorbed; Observe(s, b, r, h, c, stage);
                }
            }
            r.Outcome = s.Phase == CoreDemoPhase.Sailing ? "win" : actionLimit ? "action-watchdog" : s.Phase == CoreDemoPhase.Settlement ? "loss" : "turn-watchdog";
            r.Hull = s.Hull; r.Crew = b.PlayerBoardingHealth; r.EnemyHull = b.EnemyHull; r.EnemyCrew = b.EnemyBoardingHealth; r.FinalStage = b.Stage.ToString();
            r.EnemyHullFraction = (double)b.EnemyHull / b.EnemyMaxHull;
            r.EnemyCrewFraction = (double)b.EnemyBoardingHealth / b.EnemyMaxBoardingHealth;
            return r;
        }

        private static void Observe(CoreVoyageSession s, CoreBattleState b, Result r, int hull, int crew, NavalBattleStage previousStage)
        {
            r.HullLoss += Math.Max(0, hull - s.Hull); r.HullHealed += Math.Max(0, s.Hull - hull);
            // Exclude naval-to-boarding initialization from damage/healing. Compute
            // the exact public-rule carryover separately so an enemy's immediate
            // boarding first strike still counts as crew damage.
            if (previousStage != NavalBattleStage.Boarding && b.Stage == NavalBattleStage.Boarding)
            {
                int navalDamage = Math.Min(s.MaxHull, Math.Max(0, b.NavalStartPlayerHull - Math.Max(0, s.Hull)));
                long denominator = 2L * s.MaxHull;
                long numerator = (long)b.PlayerMaxBoardingHealth * (denominator - navalDamage);
                crew = (int)Math.Max(1L, Math.Min(b.PlayerMaxBoardingHealth, (numerator + denominator - 1L) / denominator));
                r.BoardingCarryover += b.PlayerMaxBoardingHealth - crew;
            }
            if (b.Stage != NavalBattleStage.Boarding) return;
            r.CrewLoss += Math.Max(0, crew - b.PlayerBoardingHealth);
            r.CrewHealed += Math.Max(0, b.PlayerBoardingHealth - crew);
        }
        private static bool IsResourceReason(string reason) => reason.Contains("法力不足") || reason.Contains("附咒层数不足") || reason.Contains("支付生命") || reason.Contains("火药不足") || reason.Contains("炮弹不足");
        private static object ConditionalMeanTurns(IEnumerable<Result> rows, bool won)
        {
            var selected = rows.Where(r => r.Won == won && !r.Outcome.Contains("watchdog")).ToArray();
            return selected.Length == 0 ? (object)string.Empty : selected.Average(r => r.Turns);
        }
        private static CardUse GetUse(Result r, string id)
        {
            string key = r.School + "/" + r.Rank + "/" + r.Tactic + "/" + id + "/" + r.Source;
            if (!Uses.TryGetValue(key, out CardUse use)) Uses[key] = use = new CardUse { School = r.School, Rank = r.Rank, Tactic = r.Tactic, Id = id, Source = r.Source };
            return use;
        }
        private static void ObserveHand(CoreVoyageSession s, Result r)
        {
            for (int i = 0; i < s.Battle.Hand.Count; i++)
            {
                var u = GetUse(r, s.GetBattleCard(s.Battle.Hand[i]).Id); u.Seen++;
                string reason = s.CardUnavailableReason(i);
                if (IsResourceReason(reason)) u.ResourceBlocked++;
                else if (reason.Contains("行动点")) u.EnergyBlocked++;
                else if (reason.Contains("距离")) u.RangeBlocked++;
            }
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
            var match = Regex.Match(s.GetEnemyIntent() ?? "", "(?:炮击：伤害 |白刃突击：船员伤害 |敌方先手：船员伤害 )(\\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }
        private static double Score(CoreVoyageSession s, int index, string tactic)
        {
            var b = s.Battle; var card = s.GetBattleCard(index); var fx = s.GetBattleCardEffects(card, b.Stage); var e = card.SchoolEffect; var st = b.Schools;
            bool boarding = b.Stage == NavalBattleStage.Boarding, preparing = b.Stage == NavalBattleStage.Preparation;
            bool boardFirst = tactic == "boarding", burning = b.EnemyBurnTurns > 0 && b.EnemyBurnDamage > 0;
            int hp = boarding ? b.PlayerBoardingHealth : s.Hull, maxHp = boarding ? b.PlayerMaxBoardingHealth : s.MaxHull;
            int enemyHp = boarding ? b.EnemyBoardingHealth : b.EnemyHull;
            int attack = Incoming(s), cost = s.GetBattleCardEnergyCost(index), damage = s.GetBattleCardDamage(card, b.Stage);
            int need = preparing ? Math.Max(0, 18 - b.PlayerBlock) : Math.Max(0, attack - b.PlayerBlock);
            double scale = boarding ? (double)fx.BoardingScale : 1d;
            double healWeight = hp < maxHp * .45 ? 2.5 : .9, attackWeight = !boarding && boardFirst ? .65 : 1.6;
            double blockWeight = hp <= attack * 2 ? 2.4 : 1.1;
            double value = Math.Min(Math.Max(0, maxHp - hp), fx.Repair) * healWeight + Math.Min(need, fx.Block + (fx.Block > 0 && !boarding ? b.PlayerDeckBuff : 0)) * blockWeight;
            if (damage >= enemyHp + b.EnemyBlock && !preparing) return 10000d / (cost + .7);
            if (!preparing) value += Math.Min(enemyHp + b.EnemyBlock, damage) * attackWeight;
            double drawValue = b.Energy > cost ? (b.Hand.Count <= 3 ? 9 : b.Hand.Count <= 6 ? 5 : 1) : .2;
            value += card.Draw * drawValue;
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
                        if (b.DistanceMeters > (boardFirst ? 60 : 350)) value += Math.Min(card.MovementMeters, b.DistanceMeters - (boardFirst ? 60 : 350)) * .3 + 10;
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
                    case CoreSchoolMechanic.ArcaneCharge:
                        int gained = Math.Min(e.Power, Math.Max(0, Math.Max(12, e.Limit) - st.Mana));
                        value += gained * (st.Mana < 4 ? 5 : st.Mana < 8 ? 2.5 : .8);
                        if (gained > 0 && b.Hand.Select(i => s.GetBattleCard(i)).Any(c => c.SchoolEffect != null && c.SchoolEffect.Threshold > st.Mana && c.SchoolEffect.Threshold <= st.Mana + gained && c.SchoolId == "arcane")) value += 12;
                        break;
                    case CoreSchoolMechanic.ArcaneRelease:
                    case CoreSchoolMechanic.ArcaneWard:
                    case CoreSchoolMechanic.ArcaneMend: value -= e.Threshold * (st.Mana <= e.Threshold ? 1.1 : .4); break;
                    case CoreSchoolMechanic.ArcaneEcho: value += e.Power * drawValue - e.Threshold * .6; break;
                    case CoreSchoolMechanic.CurseMark:
                        int marks = Math.Min(e.Power, Math.Max(0, Math.Max(6, e.Limit) - st.CurseMarks));
                        value += marks * (preparing ? 4 : 6) + Math.Max(0, duration - st.CurseTurns) * st.CurseMarks;
                        value += Math.Min(attack, attack * marks * e.Secondary / 100d) * blockWeight;
                        break;
                    case CoreSchoolMechanic.CurseDetonate: value -= st.CurseMarks * (st.CurseTurns > 1 ? 2 : .5); break;
                    case CoreSchoolMechanic.CurseDrain: value -= e.Threshold * .8; break;
                    case CoreSchoolMechanic.TideWard: if (attack > 0 && b.PlayerBlock + fx.Block >= Math.Max(1, e.Threshold)) value += Math.Max(0, power - (st.TideCounterTurns > 1 ? st.TideCounter : 0)) * 1.4; break;
                    case CoreSchoolMechanic.TideShift:
                        if (st.TideStance != e.Threshold || st.TideStanceTurns <= 1)
                            value += e.Threshold == 1 ? power * (preparing ? 1.5 : 2) : e.Threshold == 2 ? Math.Min(attack > 0 ? attack : 10, power) * 1.5 : (b.PlayerBlock + fx.Block > 0 ? power * 1.4 : power * .3);
                        break;
                    case CoreSchoolMechanic.TideCleanse: value += b.EnemyLoadedShot * .8; break;
                    case CoreSchoolMechanic.ThunderPrime: value += Math.Min(e.Power, Math.Max(0, Math.Max(6, e.Limit) - st.ThunderChain)) * (b.Energy > cost ? 5 : .1); break;
                    case CoreSchoolMechanic.ThunderFinish: if (b.Energy > cost && st.ThunderChain < 3) value -= 9; break;
                    case CoreSchoolMechanic.ThunderEcho: if (!st.ThunderEchoUsed && st.ThunderChain >= Math.Max(1, e.Threshold)) value += e.Power * drawValue; break;
                    case CoreSchoolMechanic.BloodRisk:
                    case CoreSchoolMechanic.BloodWard: value -= e.Threshold * healWeight; break;
                    case CoreSchoolMechanic.BloodDrain: value += Math.Min(maxHp - hp, Math.Min(e.Limit > 0 ? e.Limit : int.MaxValue, Math.Max(0, damage - b.EnemyBlock) * e.Power / 100d)) * healWeight; break;
                }
                if (e.Movement > 0 && !boarding)
                {
                    if (evade) return 0;
                    if (b.DistanceMeters > (boardFirst ? 60 : 350)) value += Math.Min(card.MovementMeters, b.DistanceMeters - (boardFirst ? 60 : 350)) * .3 + 10;
                    if (boardFirst && b.DistanceMeters <= 120 && b.Alignment != NavalBattleAlignment.Parallel) value += 65;
                }
                if (e.Movement < 0 && !boarding)
                { if (evade) value += 100 + card.MovementMeters * .1; else if (boardFirst || b.DistanceMeters > 350 - card.MovementMeters) value -= 25; }
            }
            return value / (cost + .7);
        }
        private static void WriteCsv(string path, string header, IEnumerable<object[]> rows)
        {
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(true)))
            { writer.WriteLine(header); foreach (var row in rows) writer.WriteLine(string.Join(",", row.Select(x => "\"" + Convert.ToString(x, CultureInfo.InvariantCulture).Replace("\"", "\"\"") + "\""))); }
        }
        private static void WriteReport(string output, List<Result> rows, int seeds, bool sourceSweep)
        {
            var t = new StringBuilder();
            t.AppendLine("# Six-school real battle benchmark");
            t.AppendLine();
            if (sourceSweep)
            {
                t.AppendLine("SOURCE SWEEP: " + CoreSpecialCardCatalog.All.Count + " individually configured crew/component cards x 4 ranks x 2 tactics x third-route/treasure bosses x " + seeds + " paired seeds = " + rows.Count + " battles. Each source is added through ConfigureSpecialCardSources before departure, with its own source XP rank restored and its matching basic school deck. Crew sources occupy one real berth (21 ordinary + 1 special); component sources keep 22 ordinary crew. Source battles are separate from the no-source fixture described below and must be compared per source, never used as a pooled school power ratio. source-summary.csv reports outcomes, source plays and resource locks per source/rank/tactic across both opponents. A zero observed play count remains visible and is not replaced with a forced draw. coverage.csv lists all library cards, including zero observations and zero plays.");
                t.AppendLine();
            }
            t.AppendLine("Version: " + BenchmarkVersion + ". Fixture: 7 decks x 4 ranks x 2 tactics x " + Enemies.Length + " opponents x " + seeds + " fixed paired seeds = " + rows.Count + " real Core battles. Opponents include all three route bosses, the third-chart treasure guardian, and all five legendary warships at their real authored stats. Pirate level 10 is restored before departure; the same patrol ship (155 hull / 22 ordinary crew), equipment quality 2, no specialist/source cards, no component bonuses, and finite supplies (25 powder / 40 cannonballs / 10 medicine / 18 wood / 20 food / 20 water) are used for every deck. Each school contributes all 10 of its unlocked basic cards once across their legal phases (2 preparation / 4 naval / 4 boarding), with identical support cards filling each phase to 10. The ordinary control replaces those slots with ordinary cards. All deck legality, ammunition, mana, health costs, shuffling, stage transitions, guard, critical hits, and enemy actions use production rules.");
            t.AppendLine();
            t.AppendLine("The captain reads public card quotes and current enemy intent. Its heuristic values resource generation, combo activation, damage over time, guard, healing, draw and movement. It never injects cards/resources during combat. Only map navigation is skipped by invoking the normal StartBattle entry point. Source cards and custom optimal deck construction are outside this fixture. Greedy policy, shared support density, overkill, and matchups affect results; win rate is not a linear card-power measurement and cannot establish a universal 30% advantage. Results must be compared using the same benchmark version and seeds.");
            t.AppendLine();
            t.AppendLine("Damage columns are cumulative observed health decreases (including voluntary blood/ram costs), not final health deficit. Naval damage carried into initial boarding health is reported separately as boardingCarryover, never as healing or a second crew damage hit. Immediate enemy boarding first-strike damage remains crew damage. No-legal-card turns count turns starting without any legal card and ending without a card played. Declined turns count turns in which legal cards existed but the heuristic found no useful play. Resource-locked turns start without a legal card and with at least one visible resource denial. Per-card blocked columns are turn-start hand observations of the first reported denial, not distinct draws or failed play attempts. Watchdogs signal an incomplete simulation, never a win. meanWinTurns excludes defeats/watchdogs, so an early defeat cannot look like faster victory. Zero wins produce a blank meanWinTurns.");
            t.AppendLine();
            t.AppendLine("| Deck | Battles | Win rate | Win turns | Hull damage | Crew damage | Cards | School cards | Resource-locked turns | Watchdogs |");
            t.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
            foreach (var g in rows.GroupBy(r => r.School)) t.AppendLine(string.Format(CultureInfo.InvariantCulture, "| {0} | {1} | {2:P1} | {3:F2} | {4:F2} | {5:F2} | {6:F2} | {7:F2} | {8:F2} | {9} |", g.Key, g.Count(), g.Average(r => r.Won ? 1d : 0), ConditionalMeanTurns(g, true), g.Average(r => r.HullLoss), g.Average(r => r.CrewLoss), g.Average(r => r.Cards), g.Average(r => r.SchoolCards), g.Average(r => r.ResourceLockedTurns), g.Count(r => r.Outcome.Contains("watchdog"))));
            t.AppendLine();
            t.AppendLine("`battles.csv` contains every seed/matchup; `summary.csv` separates ranks and tactics; `matchups.csv` preserves each enemy's difficulty and remaining HP fractions; `card-use.csv` records use and card-lock observations; `core-source-sha256.txt` identifies the compiled production source; `benchmark-source-sha256.txt` verifies that before/after used the identical captain and fixture. Re-run with `RunBattleBenchmark.ps1 -Seeds 16 -Output <new-directory>`; `-CoreSource <snapshot-directory>` reproduces a saved source snapshot. This is an offline Core audit executable, not a packaged game build.");
            File.WriteAllText(Path.Combine(output, "report.md"), t.ToString(), new UTF8Encoding(true));
        }
    }
}

