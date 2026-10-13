using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    // A deterministic captain, not an alternate combat implementation. All damage,
    // resource spending, shuffling and victories come from CoreVoyageSession.
    public static class ShipProgressionSimulator
    {
        public enum Tactic { Cannon, Boarding, Sustain }
        public enum Loadout { Starter, Patrol, Gunship }
        public sealed class Scenario
        {
            public Scenario(string id, int route, bool tutorial = false) { EnemyId = id; Route = route; Tutorial = tutorial; }
            public string EnemyId { get; }
            public int Route { get; }
            public bool Tutorial { get; }
        }

        public static readonly IReadOnlyList<Scenario> CalibrationScenarios = Array.AsReadOnly(new[]
        {
            new Scenario(CoreGuidedTutorialContent.BossEventId, 0, true),
            new Scenario("event_reef_coral_ambush", 0),
            new Scenario("gold_customs", 1),
            new Scenario("moon_ghost", 2),
            new Scenario("boss_route_shattered_sail_reef", 0),
            new Scenario("boss_route_golden_tide_trade", 1),
            new Scenario("boss_route_moon_tide_ruins", 2),
            new Scenario(CoreBattleBalance.TreasureBossId, 2),
            new Scenario("legendary_pirate_red_widow", 2),
            new Scenario("legendary_pirate_thunder_crown", 2),
            new Scenario("legendary_pirate_iron_oath", 2),
            new Scenario("legendary_pirate_ember_ark", 2),
            new Scenario("legendary_pirate_tide_requiem", 2),
        });
        public sealed class Result
        {
            public decimal UpgradeCost;
            public int ShipIndex, MaxBlock, HullHealed, CrewHealed, GuardAbsorbed, Specialists, SpecialistLevel;
            public string Seed, EnemyId, EnemyName, Deck, Outcome, Ship, FinalStage, BoardingEntry = "none";
            public int InitialHull, InitialCrewHealth, OrdinaryCrew, MinimumHull, HullDamageTaken, CrewDamageTaken,
                EnemyAttackTurns, ZeroCostCards, EnergySpent, PlayerRams, EnemyRams, WoodUsed, MedicineUsed;
            public Tactic Strategy;
            public int Upgrade, Turns, Cards, Hull, CrewHealth, EnemyHull, EnemyCrew,
                InitialEnemyHull, InitialEnemyCrew, MaxCombo, CannonballsUsed, PowderUsed;
            public bool Won;
            public string Csv() => string.Join(",", new object[]
            {
                Seed, EnemyId, EnemyName, Deck, Strategy, Upgrade, Outcome, Turns, Cards,
                Hull, CrewHealth, EnemyHull, EnemyCrew, InitialEnemyHull, InitialEnemyCrew,
                MaxCombo, CannonballsUsed, PowderUsed, Ship, OrdinaryCrew, InitialHull, InitialCrewHealth, MinimumHull,
                HullDamageTaken, CrewDamageTaken, EnemyAttackTurns, ZeroCostCards, EnergySpent, PlayerRams, EnemyRams, WoodUsed, MedicineUsed, FinalStage, BoardingEntry, ShipIndex, UpgradeCost, MaxBlock, HullHealed, CrewHealed, GuardAbsorbed, Specialists, SpecialistLevel,
            }.Select(value => "\"" + Convert.ToString(value, CultureInfo.InvariantCulture).Replace("\"", "\"\"") + "\""));
        }

        public const string CsvHeader = "seed,enemyId,enemyName,deck,strategy,upgrade,outcome,turns,cards,hull,crewHealth,enemyHull,enemyCrew,initialEnemyHull,initialEnemyCrew,maxCombo,cannonballsUsed,powderUsed,ship,ordinaryCrew,initialHull,initialCrewHealth,minimumHull,hullDamageTaken,crewDamageTaken,enemyAttackTurns,zeroCostCards,energySpent,playerRams,enemyRams,woodUsed,medicineUsed,finalStage,boardingEntry,shipIndex,upgradeCost,maxBlock,hullHealed,crewHealed,guardAbsorbed,specialists,specialistLevel";
        public const int TurnWatchdog = 100;
        public const int ActionWatchdog = 64;
        public static readonly IReadOnlyList<string> FixtureCrewIds = Array.AsReadOnly(new[]
        {
            "crew_gray_gull_navigator_evelyn", "crew_gray_gull_carpenter_barlow",
        });
        public static readonly IReadOnlyList<string> FixtureComponentIds = Array.AsReadOnly(new[]
        {
            "bow_gilded_mermaid", "sail_guild_silk", "cannon_chain_repeater",
            "plating_gilded_bulwark", "instrument_guild_engine",
        });
        private static readonly MethodInfo StartBattle = typeof(CoreVoyageSession).GetMethod(
            "StartBattle", BindingFlags.NonPublic | BindingFlags.Instance);

        public static decimal LastUpgradeCost;
        public static bool UseCrewScale;
        public static double CrewReference = 20d;
        public static int SpecialistCount;
        public static int SpecialistUpgrade;
        public static CoreVoyageSession CreateSession(string seed, int upgrade, string deck, int shipIndex, int route, bool tutorial = false)
        {
            if (upgrade < 0 || upgrade > 3) throw new ArgumentOutOfRangeException(nameof(upgrade));
            var session = new CoreVoyageSession();
            if (!session.SelectShip(shipIndex)) throw new InvalidOperationException("Invalid selected ship");
            session.SelectRoute(route); session.SetSeed(seed);
            int specialists = Math.Min(SpecialistCount,session.CrewCountValue);
            session.ConfigureCrewCapabilities(specialists >= 3 || tutorial && specialists == 1, specialists >= 6, specialists >= 6, specialists >= 6, specialists >= 2, specialists >= 2);
            session.ConfigureDepartureCrew(session.CrewCountValue-specialists,specialists);
            session.ConfigureBattleEquipment(1f, 1f);
            session.ConfigureComponentBonuses(0, 0, 0, 0, 0);
            var crewIds = new List<string>();
            if (specialists >= 2) crewIds.AddRange(new[] {"crew_gray_gull_cook_pepe", "crew_gray_gull_diver_noya"});
            if (specialists >= 3 || tutorial && specialists == 1) crewIds.Add("crew_gray_gull_navigator_evelyn");
            if (specialists >= 6) crewIds.AddRange(new[] {"crew_gray_gull_carpenter_barlow", "crew_gray_gull_doctor_moer", "crew_gray_gull_gunner_leisa"});
            if (tutorial) {
                crewIds.Clear();
                if (specialists > 0) crewIds.Add("crew_broken_anchor_navigator_lorne");
                if (specialists >= 3) crewIds.AddRange(new[] {"crew_broken_anchor_cook_tam", "crew_broken_anchor_diver_udo"});
            }
            session.ConfigureSpecialCardSources(crewIds, Array.Empty<string>());
            int experience = SpecialistUpgrade == 0 ? 0 : SpecialistUpgrade == 1 ? 120 : SpecialistUpgrade == 2 ? 360 : 840;
            session.RestoreSpecialCardExperience(session.AcquiredSpecialCardIds.ToDictionary(id=>id,id=>experience));
            if (deck != "default")
            {
                SetDeck(session, NavalBattleStage.Preparation,
                    "card_deck_polish", "card_deck_polish", "card_load_shot", "card_load_shot",
                    "card_full_sail_turn", "card_full_sail_turn", "card_approach", "card_approach",
                    "card_brace", "card_parallel_course");
                SetDeck(session, NavalBattleStage.Naval,
                    "card_chain_shot", "card_chain_shot", "card_chain_shot",
                    "card_cannon_barrage", "card_cannon_barrage", "card_cannon_barrage",
                    "card_full_sail_turn", "card_retreat", "card_emergency_repair", "card_brace");
                SetDeck(session, NavalBattleStage.Boarding,
                    "card_boarding_hook", "card_boarding_hook", "card_crew_thrust", "card_crew_thrust",
                    "card_crew_thrust", "card_cutlass_raid", "card_cutlass_raid", "card_crew_guard",
                    "card_crew_rally", "card_crew_bandage");
            }
            if (deck == "sustain")
            {
                SetDeck(session, NavalBattleStage.Preparation,
                    "card_full_sail_turn", "card_full_sail_turn", "card_full_sail_turn",
                    "card_parallel_course", "card_parallel_course", "card_parallel_course", "card_brace", "card_brace");
                SetDeck(session, NavalBattleStage.Naval,
                    "card_full_sail_turn", "card_full_sail_turn", "card_full_sail_turn",
                    "card_parallel_course", "card_parallel_course", "card_parallel_course", "card_brace", "card_brace");
                SetDeck(session, NavalBattleStage.Boarding,
                    "card_crew_thrust", "card_crew_thrust", "card_crew_guard", "card_crew_guard",
                    "card_crew_rally", "card_crew_rally", "card_crew_bandage", "card_crew_bandage");
            }
            session.RestoreProfile(1000000m, session.CurrentPort);
            decimal before = session.ProfileGold;
            var used = Enum.GetValues(typeof(NavalBattleStage)).Cast<NavalBattleStage>()
                .SelectMany(stage => session.GetBattleDeck(stage)).Distinct().ToArray();
            foreach (int index in used)
                for (int i=0;i<upgrade;i++)
                    if (!session.TryUpgradeBattleCard(index, out string reason)) throw new InvalidOperationException(reason);
            LastUpgradeCost = before-session.ProfileGold;
            // Finite supplies fit even the smallest courier; no resource injection after battle starts.
            session.Cargo.Water = session.Cargo.Food = 20;
            session.Cargo.Wood = 18; session.Cargo.Medicine = 10;
            session.Cargo.Beer = session.Cargo.Iron = session.Cargo.Cloth = 0;
            session.Cargo.Powder = 25; session.Cargo.Cannonballs = 40;
            if (session.Cargo.UsedVolume > session.CargoCapacityValue + .001f) throw new InvalidOperationException("Overloaded fixture");
            if (tutorial) session.ConfigureGuidedTutorialVoyage(CoreGuidedTutorialContent.BossVoyageLeg);
            session.StartVoyage();
            if (session.Phase != CoreDemoPhase.Sailing || session.SelectedOrdinaryCrew != session.CrewCountValue-specialists)
                throw new InvalidOperationException("Invalid departure");
            return session;
        }

        public static Result Run(string seed, Scenario scenario, int upgrade, Tactic tactic, string deck, int shipIndex)
        {
            CoreVoyageSession session = CreateSession(seed, upgrade, deck, shipIndex, scenario.Route, scenario.Tutorial);
            CoreMapNode node = session.Map.Nodes.Single(value => value.Encounter.Id == scenario.EnemyId);
            if (StartBattle == null) throw new InvalidOperationException("Core battle entry point missing.");
            // The sole bypass skips navigation. Never rewrite hands, HP, RNG or enemy stats.
            StartBattle.Invoke(session, new object[] { node.Encounter, node.IsBoss });
            CoreBattleState battle = session.Battle;
            CoreBattleProfile profile = CoreBattleBalance.Resolve(node.Encounter, scenario.Route, node.IsBoss);
            if (battle.EnemyMaxHull != profile.EnemyHull || battle.EnemyMaxBoardingHealth != profile.BoardingHealth)
                throw new InvalidOperationException("Combat is not using its authored profile.");
            int expectedDeckSize = session.GetBattleDeck(NavalBattleStage.Preparation).Count +
                session.GetBonusBattleCards(NavalBattleStage.Preparation).Count;
            if (battle.Hand.Count + battle.DrawPile.Count + battle.DiscardPile.Count != expectedDeckSize)
                throw new InvalidOperationException("Combat did not include the fixture's authored deck and source cards.");
            var result = new Result
            {
                Seed = seed, EnemyId = scenario.EnemyId, EnemyName = node.Encounter.Name,
                Ship = session.SelectedShip.Id, OrdinaryCrew = session.SelectedOrdinaryCrew,
                InitialHull = session.Hull, InitialCrewHealth = battle.PlayerMaxBoardingHealth, MinimumHull = session.Hull,
                Deck = deck, Strategy = tactic, Upgrade = upgrade, ShipIndex = shipIndex, UpgradeCost = LastUpgradeCost,
                Specialists = SpecialistCount, SpecialistLevel = SpecialistUpgrade,
                InitialEnemyHull = battle.EnemyMaxHull, InitialEnemyCrew = battle.EnemyMaxBoardingHealth,
            };
            bool actionWatchdog = false;
            for (int turn = 0; turn < TurnWatchdog && session.Phase == CoreDemoPhase.Battle; turn++)
            {
                result.Turns++;
                int stageActions = 0;
                for (; stageActions < ActionWatchdog && session.Phase == CoreDemoPhase.Battle; stageActions++)
                {
                    if (tactic != Tactic.Cannon && session.CanBoard(out _) && session.TryBoard())
                    {
                        result.BoardingEntry = "planned";
                        continue;
                    }
                    // Board-first captain may take the initiative by ramming. The gun captain
                    // keeps range instead, so an enemy-forced melee remains a real risk.
                    if (tactic != Tactic.Cannon && session.Hull > 28 && session.CanRam(out _))
                    {
                        int previousHull = session.Hull, previousCrew = battle.PlayerBoardingHealth;
                        if (!session.TryRam()) throw new InvalidOperationException("Legal ram was rejected.");
                        result.PlayerRams++;
                        if (battle.Stage == NavalBattleStage.Boarding) result.BoardingEntry = "player-ram";
                        result.EnergySpent++;
                        ObserveDamage(result, session, battle, previousHull, previousCrew);
                        continue;
                    }
                    int handIndex = BestCard(session, tactic);
                    if (handIndex < 0) break;
                    int index = battle.Hand[handIndex];
                    CoreBattleCard card = session.GetBattleCard(index);
                    int energyCost = session.GetBattleCardEnergyCost(index);
                    int hullBefore = session.Hull, crewBefore = battle.PlayerBoardingHealth;
                    int woodBefore = session.Cargo.Wood, medicineBefore = session.Cargo.Medicine;
                    if (!session.PlayCard(handIndex)) throw new InvalidOperationException("Selected legal action was rejected.");
                    result.Cards++;
                    if (energyCost == 0) result.ZeroCostCards++;
                    result.EnergySpent += energyCost;
                    // Sum actual card costs; victory loot cannot hide ammunition consumed.
                    result.CannonballsUsed += card.CannonballCost;
                    result.PowderUsed += card.PowderCost;
                    result.WoodUsed += Math.Max(0, woodBefore - session.Cargo.Wood);
                    result.MedicineUsed += Math.Max(0, medicineBefore - session.Cargo.Medicine);
                    ObserveDamage(result, session, battle, hullBefore, crewBefore);
                    result.MaxCombo = Math.Max(result.MaxCombo, battle.CardsPlayedThisTurn);
                }
                if (stageActions >= ActionWatchdog && session.Phase == CoreDemoPhase.Battle)
                {
                    actionWatchdog = true;
                    break;
                }
                if (session.Phase == CoreDemoPhase.Battle)
                {
                    bool ram = session.EnemyWillRam;
                    bool attack = ram || IncomingAttack(session.GetEnemyIntent()) > 0;
                    int hullBefore = session.Hull, crewBefore = battle.PlayerBoardingHealth;
                    if (!session.EndBattleTurn()) throw new InvalidOperationException("Legal end-turn was rejected.");
                    result.GuardAbsorbed += battle.LastEnemyGuardAbsorbed;
                    if (attack) result.EnemyAttackTurns++;
                    if (ram) result.EnemyRams++;
                    if (ram && battle.Stage == NavalBattleStage.Boarding) result.BoardingEntry = "enemy-ram";
                    ObserveDamage(result, session, battle, hullBefore, crewBefore);
                }
            }
            result.FinalStage = battle.Stage.ToString();
            result.Won = session.Phase == CoreDemoPhase.Sailing;
            result.Outcome = result.Won ? "win" : actionWatchdog ? "action-watchdog" :
                session.Phase == CoreDemoPhase.Settlement ? "loss" : "turn-watchdog";
            result.Hull = session.Hull;
            result.CrewHealth = battle.PlayerBoardingHealth;
            result.EnemyHull = battle.EnemyHull;
            result.EnemyCrew = battle.EnemyBoardingHealth;
            return result;
        }

        private static void ObserveDamage(Result result, CoreVoyageSession session, CoreBattleState battle, int hullBefore, int crewBefore)
        {
            result.MinimumHull = Math.Min(result.MinimumHull, session.Hull);
            result.MaxBlock = Math.Max(result.MaxBlock, battle.PlayerBlock);
            result.HullHealed += Math.Max(0, session.Hull - hullBefore);
            result.CrewHealed += Math.Max(0, battle.PlayerBoardingHealth - crewBefore);
            result.HullDamageTaken += Math.Max(0, hullBefore - session.Hull);
            result.CrewDamageTaken += Math.Max(0, crewBefore - battle.PlayerBoardingHealth);
        }

        private static void SetDeck(CoreVoyageSession session, NavalBattleStage stage, params string[] ids)
        {
            int[] indexes = ids.Select(id => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Single(index => CoreVoyageSession.CardLibrary[index].Id == id)).ToArray();
            if (!session.SetBattleDeck(stage, indexes)) throw new InvalidOperationException("Invalid fixture deck: " + stage);
        }

        private static int BestCard(CoreVoyageSession session, Tactic tactic)
        {
            int best = -1;
            double bestScore = 0.01;
            for (int index = 0; index < session.Battle.Hand.Count; index++)
            {
                if (!session.CanPlayCard(index)) continue;
                double score = Score(session, session.Battle.Hand[index], tactic);
                if (score <= bestScore) continue;
                bestScore = score;
                best = index;
            }
            return best;
        }

        private static double Score(CoreVoyageSession session, int cardIndex, Tactic tactic)
        {
            CoreBattleCard card = session.GetBattleCard(cardIndex);
            CoreBattleState battle = session.Battle;
            // The production resolver owns scaling and rounding. The captain only
            // reads the same public values as the card UI; never scale them again.
            CoreBattleCardEffects effect = session.GetBattleCardEffects(card, battle.Stage);
            bool boarding = battle.Stage == NavalBattleStage.Boarding;
            bool preparing = battle.Stage == NavalBattleStage.Preparation;
            int enemyHealth = boarding ? battle.EnemyBoardingHealth : battle.EnemyHull;
            int health = boarding ? battle.PlayerBoardingHealth : session.Hull;
            int maxHealth = boarding ? battle.PlayerMaxBoardingHealth : session.MaxHull;
            int attack = IncomingAttack(session.GetEnemyIntent());
            int defenseNeed = preparing ? Math.Max(0, 14 - battle.PlayerBlock) : Math.Max(0, attack - battle.PlayerBlock);
            int repair = Math.Min(maxHealth - health, effect.Repair);
            int block = Math.Min(defenseNeed, effect.Block + (card.Block > 0 && !boarding ? battle.PlayerDeckBuff : 0));
            double score = repair * (health < maxHealth * .55 ? 2.5 : .65) + block * (health <= attack * 2 ? 2 : .9);
            if (boarding && tactic == Tactic.Sustain)
                score = repair * (health <= attack * 2 ? 3.5 : 1.2) + block * (health <= attack * 2 ? 3.5 : 2.1);
            double divisor = session.GetBattleCardEnergyCost(cardIndex) + .65;
            int damage = session.GetBattleCardDamage(card, battle.Stage);
            if (damage >= enemyHealth + battle.EnemyBlock) return 10000 / divisor;
            if (!preparing && (boarding || tactic == Tactic.Cannon)) score += damage * 1.7;
            if (card.Draw > 0 && battle.Hand.Count < 7) score += card.Draw * (battle.Hand.Count < 4 ? 6 : 2);
            bool evadeRam = !boarding && !preparing && tactic == Tactic.Cannon && session.EnemyWillRam;
            bool maneuver = card.Action == NavalBattleCardAction.Approach || card.Action == NavalBattleCardAction.Circle ||
                card.Action == NavalBattleCardAction.Turn || card.Action == NavalBattleCardAction.Navigator ||
                card.Action == NavalBattleCardAction.Parallel;
            if (evadeRam && maneuver) return 0;
            switch (card.Action)
            {
                case NavalBattleCardAction.Polish:
                    if (battle.PlayerDeckBuff < card.StrategyLimit) score += preparing ? 35 : 9;
                    break;
                case NavalBattleCardAction.Load:
                case NavalBattleCardAction.PrecisionLoad:
                    if (tactic == Tactic.Cannon && battle.PlayerLoadedShot < card.StrategyLimit)
                        score += preparing ? 24 : 8;
                    break;
                case NavalBattleCardAction.Approach:
                case NavalBattleCardAction.Circle:
                case NavalBattleCardAction.Turn:
                    if (battle.DistanceMeters > (tactic != Tactic.Cannon ? 60 : 350)) score += 40;
                    else if (battle.Alignment == NavalBattleAlignment.HeadOn && battle.DistanceMeters <= 100 &&
                        card.Action != NavalBattleCardAction.Approach) score += 14;
                    break;
                case NavalBattleCardAction.Navigator:
                case NavalBattleCardAction.Parallel:
                    if (battle.DistanceMeters > (tactic != Tactic.Cannon ? 60 : 350)) score += 44;
                    else if (tactic != Tactic.Cannon && battle.Alignment != NavalBattleAlignment.Parallel) score += 65;
                    else if (battle.Alignment == NavalBattleAlignment.HeadOn && battle.DistanceMeters <= 100) score += 14;
                    break;
                case NavalBattleCardAction.Retreat:
                    if (evadeRam)
                    {
                        // Use the public structured warning, never infer ram likelihood from translated prose.
                        // A modest escape is valuable even when a second move is needed to leave ram range.
                        score += 110 + Math.Min(card.MovementMeters, session.EnemyRamRangeMeters + 1f - battle.DistanceMeters) * .1;
                        break;
                    }
                    return 0;
                case NavalBattleCardAction.FireRounds:
                    if (tactic == Tactic.Cannon && battle.PlayerFireRounds == 0) score += 18;
                    break;
            }
            // Dynamic costs bound repeated cards. Avoid effectless setup, but do not
            // introduce a per-turn play cap into the simulated game.
            if (preparing && score <= card.Draw * 6 && battle.PlayerDeckBuff >= 6 &&
                battle.PlayerLoadedShot >= 12 && battle.DistanceMeters <= 350 && battle.PlayerBlock >= 14) return 0;
            return score / divisor;
        }

        private static int IncomingAttack(string intent)
        {
            Match match = Regex.Match(intent ?? "", "(?:炮击：伤害 |白刃突击：船员伤害 )(\\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        }
    }
}
