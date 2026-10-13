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
    public static class NavalBattleBalanceSimulator
    {
        public enum Tactic { Cannon, Boarding }
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
                HullDamageTaken, CrewDamageTaken, EnemyAttackTurns, ZeroCostCards, EnergySpent, PlayerRams, EnemyRams, WoodUsed, MedicineUsed, FinalStage, BoardingEntry,
            }.Select(value => "\"" + Convert.ToString(value, CultureInfo.InvariantCulture).Replace("\"", "\"\"") + "\""));
        }

        public const string CsvHeader = "seed,enemyId,enemyName,deck,strategy,upgrade,outcome,turns,cards,hull,crewHealth,enemyHull,enemyCrew,initialEnemyHull,initialEnemyCrew,maxCombo,cannonballsUsed,powderUsed,ship,ordinaryCrew,initialHull,initialCrewHealth,minimumHull,hullDamageTaken,crewDamageTaken,enemyAttackTurns,zeroCostCards,energySpent,playerRams,enemyRams,woodUsed,medicineUsed,finalStage,boardingEntry";
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

        public static CoreVoyageSession CreateSession(string seed, int upgrade, bool focusedDeck) =>
            CreateSession(seed, upgrade, focusedDeck, Loadout.Patrol, 2, false);

        public static CoreVoyageSession CreateSession(string seed, int upgrade, bool focusedDeck,
            Loadout loadout, int route, bool tutorial = false)
        {
            if (upgrade < 0 || upgrade > 3) throw new ArgumentOutOfRangeException(nameof(upgrade));
            var session = new CoreVoyageSession();
            session.SelectShip(loadout == Loadout.Starter ? 0 : loadout == Loadout.Patrol ? 2 : 7);
            // This combat fixture skips campaign progression, not route eligibility.
            // A rejected selection would silently simulate the first route instead.
            session.RestoreTreasureProgress(new CoreTreasureProgress
                { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(CoreVoyageSession.Routes[route].UnlockLevel) });
            session.SelectRoute(route);
            if (session.SelectedRouteIndex != route)
                throw new InvalidOperationException("The combat fixture did not select its requested route.");
            session.SetSeed(seed);
            bool specialists = loadout != Loadout.Starter;
            session.ConfigureCrewCapabilities(specialists, specialists, false, false, false, false);
            session.ConfigureDepartureCrew(session.CrewCountValue - (specialists ? 2 : 0), specialists ? 2 : 0);
            session.ConfigureBattleEquipment(specialists ? 2f : 1f, specialists ? 2f : 1f);
            session.ConfigureComponentBonuses(0, 0, 0, 0, 0);
            session.ConfigureSpecialCardSources(specialists ? FixtureCrewIds : Array.Empty<string>(),
                specialists ? FixtureComponentIds : Array.Empty<string>());
            session.ConfigureCardUpgrades(CoreVoyageSession.CardLibrary
                .Where(card => !CoreVoyageSession.IsSpecialBattleCard(card.Id))
                .ToDictionary(card => card.Id, card => upgrade));
            int specialExperience = upgrade == 0 ? 0 : upgrade == 1 ? 120 : upgrade == 2 ? 360 : 840;
            session.RestoreSpecialCardExperience(session.AcquiredSpecialCardIds.ToDictionary(id => id, id => specialExperience));
            if (tutorial) session.ConfigureGuidedTutorialVoyage(CoreGuidedTutorialContent.BossVoyageLeg);
            if (focusedDeck && !tutorial)
            {
                SetDeck(session, NavalBattleStage.Preparation,
                    "card_deck_polish", "card_deck_polish", "card_load_shot", "card_load_shot",
                    "card_full_sail_turn", "card_full_sail_turn", "card_approach", "card_approach",
                    "card_brace", "card_parallel_course");
                SetDeck(session, NavalBattleStage.Naval,
                    "card_chain_shot", "card_chain_shot", "card_load_shot",
                    "card_cannon_barrage", "card_cannon_barrage", "card_deck_polish",
                    "card_full_sail_turn", "card_retreat", "card_emergency_repair", "card_brace");
                SetDeck(session, NavalBattleStage.Boarding,
                    "card_boarding_hook", "card_boarding_hook", "card_crew_thrust", "card_crew_thrust",
                    "card_crew_pistol", "card_cutlass_raid", "card_cutlass_raid", "card_crew_guard",
                    "card_crew_rally", "card_crew_bandage");
            }
            // Full, finite combat provisions. This fixture skips voyage attrition, not
            // ammunition spending. No hull, enemy state or hand is modified after start.
            session.Cargo.Water = 20;
            session.Cargo.Food = 20;
            session.Cargo.Wood = 20;
            session.Cargo.Medicine = 10;
            session.Cargo.Beer = session.Cargo.Iron = session.Cargo.Cloth = 0;
            session.Cargo.Powder = 25;
            session.Cargo.Cannonballs = 40;
            session.StartVoyage();
            return session;
        }

        public static string[] BossIds(string seed = "BALANCE-000") => CreateSession(seed, 0, false).Map.Nodes
            .Where(node => node.IsBoss || node.Encounter.Id.StartsWith("treasure_boss_", StringComparison.Ordinal)
                || node.Encounter.Id.StartsWith("legendary_", StringComparison.Ordinal))
            .Select(node => node.Encounter.Id).OrderBy(id => id).ToArray();

        public static Result Run(string seed, string enemyId, int upgrade, Tactic tactic, bool focusedDeck) =>
            Run(seed, new Scenario(enemyId, 2), upgrade, tactic, focusedDeck, Loadout.Patrol);

        public static Result Run(string seed, Scenario scenario, int upgrade, Tactic tactic, bool focusedDeck, Loadout loadout)
        {
            CoreVoyageSession session = CreateSession(seed, upgrade, focusedDeck, loadout, scenario.Route, scenario.Tutorial);
            CoreMapNode node = session.Map.Nodes.Single(value => value.Encounter.Id == scenario.EnemyId);
            if (StartBattle == null) throw new InvalidOperationException("Core battle entry point missing.");
            // The sole bypass skips navigation. Never rewrite hands, HP, RNG or enemy stats.
            StartBattle.Invoke(session, new object[] { node.Encounter, node.IsBoss });
            CoreBattleState battle = session.Battle;
            CoreBattleProfile profile = CoreBattleBalance.Resolve(node.Encounter, scenario.Route, node.IsBoss, scenario.Tutorial);
            if (battle.EnemyMaxHull != profile.EnemyHull ||
                battle.EnemyMaxBoardingHealth != (scenario.Tutorial ? 9 : profile.BoardingHealth))
                throw new InvalidOperationException("Combat is not using its authored profile.");
            int expectedDeckSize = session.GetBattleDeck(NavalBattleStage.Preparation).Count +
                session.GetBonusBattleCards(NavalBattleStage.Preparation).Count;
            if (!scenario.Tutorial && battle.Hand.Count + battle.DrawPile.Count + battle.DiscardPile.Count != expectedDeckSize)
                throw new InvalidOperationException("Combat did not include the fixture's authored deck and source cards.");
            if (scenario.Tutorial && (!session.IsGuidedBossBattle || battle.Stage != NavalBattleStage.Naval ||
                !battle.Hand.Select(index => session.GetBattleCard(index).Id).SequenceEqual(
                    new[] { "card_retreat", "card_load_shot", "card_brace" })))
                throw new InvalidOperationException("The tutorial must start its authored three-card naval lesson.");
            var result = new Result
            {
                Seed = seed, EnemyId = scenario.EnemyId, EnemyName = node.Encounter.Name,
                Ship = session.SelectedShip.Id, OrdinaryCrew = scenario.Tutorial ? battle.PlayerOrdinaryCrew : session.SelectedOrdinaryCrew,
                InitialHull = session.Hull, InitialCrewHealth = battle.PlayerMaxBoardingHealth, MinimumHull = session.Hull,
                Deck = scenario.Tutorial ? "scripted-tutorial" : focusedDeck ? "focused" : "default", Strategy = tactic, Upgrade = upgrade,
                InitialEnemyHull = battle.EnemyMaxHull, InitialEnemyCrew = battle.EnemyMaxBoardingHealth,
            };
            bool actionWatchdog = false;
            if (scenario.Tutorial) actionWatchdog = !RunTutorialActions(result, session, battle);
            else for (int turn = 0; turn < TurnWatchdog && session.Phase == CoreDemoPhase.Battle; turn++)
            {
                result.Turns++;
                int stageActions = 0;
                for (; stageActions < ActionWatchdog && session.Phase == CoreDemoPhase.Battle; stageActions++)
                {
                    if (tactic == Tactic.Boarding && session.CanBoard(out _) && session.TryBoard())
                    {
                        result.BoardingEntry = "planned";
                        continue;
                    }
                    // Board-first captain may take the initiative by ramming. The gun captain
                    // keeps range instead, so an enemy-forced melee remains a real risk.
                    if (tactic == Tactic.Boarding && session.Hull > 28 && session.CanRam(out _))
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

        // Tutorial calibration follows the authored actions through the same
        // public Core entry points. Ordinary opponents retain the captain AI.
        private static bool RunTutorialActions(Result result, CoreVoyageSession session, CoreBattleState battle)
        {
            NavalBattleStage? previousStage = null;
            int previousPhaseTurn = -1;
            for (int action = 0; action < ActionWatchdog && session.Phase == CoreDemoPhase.Battle; action++)
            {
                if (!session.IsGuidedBossBattle) throw new InvalidOperationException("The tutorial script lost its battle identity.");
                if (previousStage != battle.Stage || previousPhaseTurn != battle.PhaseTurn)
                {
                    result.Turns++;
                    previousStage = battle.Stage;
                    previousPhaseTurn = battle.PhaseTurn;
                }
                int hullBefore = session.Hull, crewBefore = battle.PlayerBoardingHealth;
                GuidedBossActionKind kind = session.GuidedBossExpectedAction;
                if (kind == GuidedBossActionKind.BlockedCard)
                {
                    int hand = battle.Hand.FindIndex(index => session.GetBattleCard(index).Id == session.GuidedBossExpectedCardId);
                    int energy = battle.Energy, powder = session.Cargo.Powder, balls = session.Cargo.Cannonballs;
                    int handCount = battle.Hand.Count, played = battle.CardsPlayedThisTurn;
                    if (session.CanPlayCard(hand) || !session.TryObserveGuidedBossBlockedCard(hand, out _) ||
                        battle.Energy != energy || session.Cargo.Powder != powder || session.Cargo.Cannonballs != balls ||
                        battle.Hand.Count != handCount || battle.CardsPlayedThisTurn != played)
                        throw new InvalidOperationException("The authored failed attempt must advance without card/resource spending.");
                }
                else if (kind == GuidedBossActionKind.Card)
                {
                    int hand = battle.Hand.FindIndex(index => session.GetBattleCard(index).Id == session.GuidedBossExpectedCardId);
                    if (hand < 0) throw new InvalidOperationException("Authored tutorial card missing from the real hand.");
                    int index = battle.Hand[hand];
                    CoreBattleCard card = session.GetBattleCard(index);
                    int energyCost = session.GetBattleCardEnergyCost(index);
                    int wood = session.Cargo.Wood, medicine = session.Cargo.Medicine;
                    if (!session.PlayCard(hand)) throw new InvalidOperationException("Authored legal card was rejected.");
                    result.Cards++;
                    if (energyCost == 0) result.ZeroCostCards++;
                    result.EnergySpent += energyCost;
                    result.CannonballsUsed += card.CannonballCost;
                    result.PowderUsed += card.PowderCost;
                    result.WoodUsed += Math.Max(0, wood - session.Cargo.Wood);
                    result.MedicineUsed += Math.Max(0, medicine - session.Cargo.Medicine);
                    result.MaxCombo = Math.Max(result.MaxCombo, battle.CardsPlayedThisTurn);
                }
                else if (kind == GuidedBossActionKind.Ram)
                {
                    if (!session.TryRam()) throw new InvalidOperationException("Authored legal ram was rejected.");
                    result.PlayerRams++;
                    // The one-point ram immediately deals a fresh boarding hand;
                    // its refreshed energy must not subtract from the real cost.
                    result.EnergySpent++;
                    if (battle.Stage == NavalBattleStage.Boarding) result.BoardingEntry = "player-ram";
                }
                else if (kind == GuidedBossActionKind.EndTurn)
                {
                    bool attack = session.EnemyWillRam || IncomingAttack(session.GetEnemyIntent()) > 0;
                    if (!session.EndBattleTurn()) throw new InvalidOperationException("Authored legal end-turn was rejected.");
                    if (attack) result.EnemyAttackTurns++;
                }
                else throw new InvalidOperationException("An active tutorial battle has no expected action.");
                ObserveDamage(result, session, battle, hullBefore, crewBefore);
            }
            return session.Phase != CoreDemoPhase.Battle;
        }

        private static void ObserveDamage(Result result, CoreVoyageSession session, CoreBattleState battle, int hullBefore, int crewBefore)
        {
            result.MinimumHull = Math.Min(result.MinimumHull, session.Hull);
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
            // Read the production stage and crew-adjusted values, including its
            // rounding, without applying a second simulated boarding multiplier.
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
                    if (battle.DistanceMeters > (tactic == Tactic.Boarding ? 60 : 350)) score += 40;
                    else if (battle.Alignment == NavalBattleAlignment.HeadOn && battle.DistanceMeters <= 100 &&
                        card.Action != NavalBattleCardAction.Approach) score += 14;
                    break;
                case NavalBattleCardAction.Navigator:
                case NavalBattleCardAction.Parallel:
                    if (battle.DistanceMeters > (tactic == Tactic.Boarding ? 60 : 350)) score += 44;
                    else if (tactic == Tactic.Boarding && battle.Alignment != NavalBattleAlignment.Parallel) score += 65;
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
