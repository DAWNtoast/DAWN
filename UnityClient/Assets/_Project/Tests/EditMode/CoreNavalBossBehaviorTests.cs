using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreNavalBossBehaviorTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static IEnumerable<string> BossIds => CoreRouteBossCatalog.All.Select(x => x.Id)
            .Concat(CoreLegendaryWarships.All.Select(x => x.Id)).Concat(new[] { CoreBattleBalance.TreasureBossId });

        [TestCaseSource(nameof(BossIds))]
        public void EveryCatalogBossHasFiveToSevenNamedStepsThatRepeatWithoutAnImplicitDamageRamp(string id)
        {
            CoreVoyageSession s = Start(id);
            int length = Move(Plan(s)).CycleLength;
            Assert.That(length, Is.InRange(5, 7));
            var names = new List<string>();
            var attacks = new List<int>();
            for (int turn = 1; turn <= length * 2; turn++)
            {
                SetStep(s, turn);
                object plan = Plan(s); CoreNavalBossMove move = Move(plan);
                Assert.That(move, Is.Not.Null, id);
                Assert.That(move.CycleStep, Is.EqualTo((turn - 1) % length + 1));
                Assert.That(move.Name, Is.Not.Empty); Assert.That(move.NextName, Is.Not.Empty);
                if (turn <= length) { names.Add(move.Name); attacks.Add(Int(plan, "Attack")); }
                else
                {
                    Assert.That(move.Name, Is.EqualTo(names[turn - length - 1]));
                    Assert.That(Int(plan, "Attack"), Is.EqualTo(attacks[turn - length - 1]), "Only explicit resources may change the repeated action's damage.");
                }
                if (turn > 1) Assert.That(names[(turn - 2) % length] == move.Name, Is.False);
                Assert.That(CoreBattleIntentPresentation.Build(s).Single(x => x.Id == "enemy_boss_cycle").Name, Is.EqualTo(move.Name));
            }
            Assert.That(names.Distinct().Count(), Is.EqualTo(length));
            for (int turn = 1; turn <= length; turn++)
            {
                SetStep(s, turn);
                Assert.That(Move(Plan(s)).NextName, Is.EqualTo(names[turn % length]));
            }
            s.Battle.Stage = NavalBattleStage.Preparation;
            Assert.That(Move(Plan(s)), Is.Null, "Preparation must not spend a naval cycle action.");
            s.Battle.Stage = NavalBattleStage.Boarding;
            Assert.That(Move(Plan(s)), Is.Null, "Boarding keeps its independently authored behavior.");
        }

        [TestCaseSource(nameof(BossIds))]
        public void PublicIntentIsReadOnlyAndItsAttackMatchesRealEndTurnForEachStep(string id)
        {
            for (int step = 1; step <= Move(Plan(Start(id))).CycleLength; step++)
            {
                CoreVoyageSession s = Start(id, step);
                s.Battle.PlayerBlock = 17;
                string before = Snapshot(s), intent = s.GetEnemyIntent();
                var icons = CoreBattleIntentPresentation.Build(s);
                for (int n = 0; n < 3; n++)
                {
                    Assert.That(s.GetEnemyIntent(), Is.EqualTo(intent));
                    Assert.That(IconSnapshot(CoreBattleIntentPresentation.Build(s)), Is.EqualTo(IconSnapshot(icons)));
                    Assert.That(Snapshot(s), Is.EqualTo(before), id + " step " + step);
                }
                CoreNavalBossMove move = Move(Plan(s));
                int quote = icons.Where(x => x.Kind == CoreBattleIntentKind.Cannon).Sum(x => x.Amount ?? 0);
                int piercing = Math.Min(quote, move.PiercingDamage);
                int expectedAbsorbed = Math.Min(17, quote - piercing);
                int hull = s.Hull;
                Assert.That(s.EndBattleTurn(), Is.True);
                Assert.That(hull - s.Hull, Is.EqualTo(quote - expectedAbsorbed), id + " step " + step);
                Assert.That(s.Battle.LastEnemyGuardAbsorbed, Is.EqualTo(expectedAbsorbed));
            }
        }

        [TestCase("ordinary")]
        [TestCase("training")]
        [TestCase("tutorial")]
        public void NonCatalogNavalEncountersKeepTheirExistingPlanner(string kind)
        {
            var s = new CoreVoyageSession();
            if (kind == "training") Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            else
            {
                string id = kind == "tutorial" ? CoreGuidedTutorialContent.BossEventId : "boss_behavior_ordinary_fixture";
                Call(s, "StartBattle", new CoreEncounter(id, "测试遭遇", "海战", "", kind == "tutorial", false, 0, 0), kind == "tutorial");
            }
            s.Battle.Stage = NavalBattleStage.Naval; Pose(s, 400);
            Assert.That(Move(Plan(s)), Is.Null);
            Assert.That(CoreBattleIntentPresentation.Build(s).Any(x => x.Id == "enemy_boss_cycle"), Is.False);
        }

        [Test]
        public void AProfileWithMatchingNumbersIsNotAccidentallyTreatedAsACatalogBoss()
        {
            var s = Start("boss_route_shattered_sail_reef"); var p = s.Battle.Profile;
            s.Battle.Profile = new CoreBattleProfile(p.BossKind, p.Archetype, p.EnemyHull, p.CannonDamage,
                p.MeleeDamage, p.Guard, p.InitialGuard, p.OrdinaryCrewCount, p.BowQuality, p.HullQuality,
                p.RamChance, p.RamRangeMeters, healthMultiplier: 1m);
            Assert.That(Move(Plan(s)), Is.Null);
        }

        [Test]
        public void OutOfRangeApproachDoesNotSkipTheAdvertisedFirstCycleAction()
        {
            var s = Start("boss_route_shattered_sail_reef", 1, 600);
            Assert.That(Move(Plan(s)), Is.Null);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyNavalBossActionsCompleted, Is.Zero);
            Pose(s, 400);
            Assert.That(Move(Plan(s)).CycleStep, Is.EqualTo(1));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyNavalBossActionsCompleted, Is.EqualTo(1));
            Pose(s, 400);
            Assert.That(Move(Plan(s)).CycleStep, Is.EqualTo(2));
        }

        [TestCase("boss_route_moon_tide_ruins_arcane")]
        [TestCase("legendary_pirate_last_horizon")]
        public void ArcaneCapacityActionFollowsRefillAndDoesNotImmediatelyFillTheNewCapacity(string id)
        {
            var s = Start(id, 1); var ship = s.Battle.NavalFields.Enemy;
            ship.ManaLimit = 3; ship.Mana = 99;
            CoreNavalBossMove move = Move(Plan(s));
            Assert.That(move.ManaLimitGain, Is.GreaterThan(0));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(ship.ManaLimit, Is.EqualTo(3 + move.ManaLimitGain));
            Assert.That(ship.Mana, Is.EqualTo(3 + move.ManaGain), "Refill happens before the capacity action; excess expires.");
            SetStep(s, 2); Pose(s, 400); ship.Mana = 99;
            CoreNavalFieldCell field = s.NavalFieldAt(s.Battle.EnemyPositionMeters);
            int expectedMana = ship.ManaLimit + Math.Max(0, s.GetNavalFieldRemainingTurns(field) - 1) + Move(Plan(s)).ManaGain;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(ship.Mana, Is.EqualTo(expectedMana), "The following enemy start refills the larger limit and grants the surviving refraction field's mana.");
        }

        [TestCase("boss_route_moon_tide_ruins_arcane", CoreNavalFieldKind.Arcane, 1)]
        [TestCase("boss_route_moon_tide_ruins_arcane", CoreNavalFieldKind.Arcane, 2)]
        [TestCase("boss_route_golden_tide_trade", CoreNavalFieldKind.Blood, 1)]
        [TestCase("boss_route_golden_tide_trade", CoreNavalFieldKind.Blood, 2)]
        public void BeneficialFieldExpiryAtPlayerEndKeepsRecoveryMovementIntentAndExecutionAligned(
            string id, CoreNavalFieldKind kind, int remaining)
        {
            var s = Start(id, Move(Plan(Start(id))).CycleLength, 400);
            s.Battle.PhaseTurn = 2; // Recovery action on a normal, non-rush naval round.
            Assert.That(s.PlaceNavalField(kind, false, 3, 0, false, 0, 0), Is.True);
            foreach (CoreNavalFieldCell cell in s.Battle.NavalFields.Cells)
                cell.ExpiresAtOwnerTurn = s.Battle.NavalFields.CompletedPlayerTurns + remaining;
            Assert.That(s.Battle.NavalFields.PlayerActing, Is.True);
            string before = Snapshot(s);
            object plan = Plan(s);
            float meters = (float)Field(plan, "Move");
            if (remaining == 1) Assert.That(meters, Is.GreaterThan(0), "The expired field cannot hold the boss in place.");
            // Surviving fields are weighed against their drawbacks by the shared movement policy.
            CoreNavalMovementFieldPreview preview = s.GetEnemyNavalFieldPreview(meters);
            var icons = CoreBattleIntentPresentation.Build(s);
            int quotedSteps = icons.Where(x => x.Id == "enemy_move").Sum(x => x.Amount ?? 0);
            Assert.That(quotedSteps, Is.EqualTo(preview.ActiveSteps));
            Assert.That(Snapshot(s), Is.EqualTo(before));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(s.Battle.EnemyPositionMeters.X, Is.EqualTo(preview.EndTurnPositionMeters.X));
            Assert.That(s.Battle.EnemyPositionMeters.Y, Is.EqualTo(preview.EndTurnPositionMeters.Y));
        }

        [Test]
        public void ArcaneFinisherQuotesPostStartRefractionManaAndSpendsThatPoolAfterAttack()
        {
            var s = Start("boss_route_moon_tide_ruins_arcane", 6);
            // This checks the explicit finisher action on a normal naval round.
            s.Battle.PhaseTurn = 2;
            s.Battle.NavalFields.Enemy.ManaLimit = 6; s.Battle.NavalFields.Enemy.Mana = 99;
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Arcane, false, 3, 0, false, 0, 0), Is.True);
            CoreNavalFieldCell field = s.GetNavalFieldAt(4, 0);
            int layersAfterPlayerEnd = s.GetNavalFieldRemainingTurns(field) - 1;
            object plan = Plan(s); CoreNavalBossMove move = Move(plan);
            int expectedRaw = Int(plan, "Attack") + (6 + layersAfterPlayerEnd) * move.ManaDamagePerPoint;
            int quote = CannonQuote(s);
            Assert.That(quote, Is.EqualTo(CoreNavalStatRules.ReduceIncomingCannonDamage(expectedRaw, s.EffectiveArmor)));
            int hull = s.Hull;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(hull - s.Hull, Is.EqualTo(quote));
            Assert.That(s.Battle.NavalFields.Enemy.Mana, Is.Zero);
            Assert.That(s.Battle.NavalFields.Enemy.ManaLimit, Is.EqualTo(6));
        }

        [Test]
        public void TideBossBuildsResourceOnlyFromDamageActuallyAbsorbedByItsTidalGuard()
        {
            var s = Start("boss_route_shattered_sail_reef_tide", 1);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyTidalGuard, Is.GreaterThan(0));
            Assert.That(s.Battle.EnemyTidePower, Is.Zero, "Gaining guard alone produces no tide.");
            Pose(s, 400);
            int marked = s.Battle.EnemyTidalGuard;
            int block = s.Battle.EnemyBlock;
            CoreBattleCard card = s.GetBattleCard(CardIndex("card_chain_shot"));
            int quoted = s.GetBattleCardDamage(card, NavalBattleStage.Naval);
            Play(s, card.Id);
            int absorbed = block - s.Battle.EnemyBlock;
            Assert.That(absorbed, Is.EqualTo(Math.Min(block, quoted)));
            Assert.That(s.Battle.EnemyTidePower, Is.EqualTo(Math.Min(marked, absorbed)));
            int tide = s.Battle.EnemyTidePower;
            s.Battle.EnemyBlock = 100; s.Battle.EnemyTidalGuard = 0;
            Play(s, card.Id);
            Assert.That(s.Battle.EnemyTidePower, Is.EqualTo(tide), "Ordinary guard cannot create tide.");
        }

        [Test]
        public void TideBossDoesNotGainResourceFromUnusedGuardAndSpendsItsStoredPoolOnCounterattack()
        {
            var s = Start("boss_route_shattered_sail_reef_tide", 1);
            s.EndBattleTurn(); SetStep(s, 2); Pose(s, 400); s.EndBattleTurn();
            Assert.That(s.Battle.EnemyTidePower, Is.Zero);
            SetStep(s, 4); Pose(s, 400); s.Battle.EnemyTidePower = 24;
            object plan = Plan(s); CoreNavalBossMove move = Move(plan);
            int expected = CoreNavalStatRules.ReduceIncomingCannonDamage(Int(plan, "Attack") + 24 * move.TideDamagePercent / 100, s.EffectiveArmor);
            int quote = CannonQuote(s), hull = s.Hull;
            Assert.That(quote, Is.EqualTo(expected));
            s.EndBattleTurn();
            Assert.That(hull - s.Hull, Is.EqualTo(quote));
            Assert.That(s.Battle.EnemyTidePower, Is.Zero);
        }

        [Test]
        public void TideBossBurnAbsorptionProducesOnlyTheActuallyAbsorbedAmount()
        {
            var s = Start("boss_route_shattered_sail_reef_tide", 1);
            int guard = Move(Plan(s)).TidalGuard;
            s.Battle.EnemyBurnDamage = s.Battle.EnemyBurnTurns = guard + 7;
            int enemyHull = s.Battle.EnemyHull;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyTidePower, Is.EqualTo(guard));
            Assert.That(s.Battle.EnemyTidalGuard, Is.Zero);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(enemyHull - 7));
        }

        [Test]
        public void EnemyResourceHudAndTidalGuardStatusReflectRealAbsorptionWithoutMutatingIt()
        {
            var s = Start("legendary_pirate_tide_requiem", 1);
            Assert.That(s.EndBattleTurn(), Is.True);
            Pose(s, 400);
            Play(s, "card_chain_shot");
            int tide = s.Battle.EnemyTidePower;
            int markedGuard = Math.Min(s.Battle.EnemyBlock, s.Battle.EnemyTidalGuard);
            Assert.That(tide, Is.GreaterThan(0)); Assert.That(markedGuard, Is.GreaterThan(0));
            string before = Snapshot(s);
            string resources = CoreBattleStatusPresentation.BuildResourceSummary(s);
            Assert.That(resources, Does.Not.Contain("潮势"));
            Assert.That(resources, Does.Contain("敌方法力 " + s.Battle.NavalFields.Enemy.Mana + "/" + s.Battle.NavalFields.Enemy.ManaLimit));
            Assert.That(resources, Does.Not.Contain("连锁"));
            var statuses = CoreBattleStatusPresentation.Build(s, false);
            CoreBattleStatus tidal = statuses.Single(x => x.Id == "tide_guard");
            Assert.That(tidal.Name, Is.EqualTo("蓄潮格挡"));
            Assert.That(tidal.Value, Is.EqualTo(markedGuard.ToString()));
            Assert.That(tidal.Tooltip, Does.Contain("实际吸收 1 点伤害").And.Contain("获得 1 点潮势"));
            Assert.That(statuses.Single(x => x.Id == "tide_power").Value, Is.EqualTo(tide.ToString()));
            Assert.That(statuses.Any(x => x.Id == "thunder_chain"), Is.EqualTo(s.Battle.NavalFields.Enemy.ThunderChain > 0));
            Assert.That(CoreBattleStatusPresentation.BuildResourceSummary(s), Is.EqualTo(resources));
            Assert.That(Snapshot(s), Is.EqualTo(before));
        }

        [Test]
        public void ThunderSetupPlacesItsAdvertisedOwnedSquareAndAddsOnlyItsAttackChain()
        {
            var s = Start("boss_route_golden_tide_trade_thunder", 1);
            CoreNavalBossMove move = Move(Plan(s)); var position = s.Battle.EnemyPositionMeters;
            Assert.That(s.Battle.NavalFields.Cells, Is.Empty); Assert.That(s.EndBattleTurn(), Is.True);
            var cells = s.Battle.NavalFields.Cells.Where(x => x.Kind == CoreNavalFieldKind.Thunder).ToArray();
            int size = CoreVoyageSession.GetNavalFieldSize(CoreNavalFieldKind.Thunder);
            Assert.That(cells.Length, Is.EqualTo(size * size)); Assert.That(cells.All(x => !x.PlayerOwned), Is.True);
            Assert.That(cells.All(x => s.GetNavalFieldRemainingTurns(x) == move.ThunderFieldTurns), Is.True);
            int anchorX = cells.Min(x => x.X), anchorY = cells.Min(x => x.Y);
            Assert.That(Math.Abs(anchorX - (int)(position.X / 100)) + Math.Abs(anchorY - (int)(position.Y / 100)), Is.LessThanOrEqualTo(4));
            Assert.That(s.NavalFieldAt(position)?.Kind, Is.EqualTo(CoreNavalFieldKind.Thunder));
            Assert.That(s.Battle.NavalFields.Enemy.ThunderChain, Is.EqualTo(move.ChainGain));
        }

        [TestCase(-400, 0, 180f)]
        [TestCase(400, 0, 0f)]
        [TestCase(0, -400, 270f)]
        [TestCase(0, 400, 90f)]
        public void ThunderExpansionRetainsItsExistingFieldChainInAllCardinalDirections(int playerX, int playerY, float heading)
        {
            var s = Start("boss_route_golden_tide_trade_thunder", 1);
            Call(s.Battle, "SetTacticalPose", true, new VoyagePoint(playerX, playerY), 90f);
            Call(s.Battle, "SetTacticalPose", false, new VoyagePoint(0, 0), heading);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.NavalFieldAt(s.Battle.EnemyPositionMeters)?.Kind, Is.EqualTo(CoreNavalFieldKind.Thunder));
            int previousChain = s.Battle.NavalFields.Enemy.ThunderChain;
            CoreNavalBossMove second = Move(Plan(s));
            Assert.That(second.FieldAction, Is.EqualTo(CoreNavalBossFieldAction.Expand));
            VoyagePoint position = s.Battle.EnemyPositionMeters;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyPositionMeters, Is.EqualTo(position), "Expanding a safe owned field does not force a chase step.");
            Assert.That(s.Battle.NavalFields.Enemy.ThunderChain, Is.EqualTo(previousChain + second.ChainGain),
                "The existing chain survives the enemy start and the action adds only its authored increment.");
            Assert.That(s.NavalFieldAt(s.Battle.EnemyPositionMeters)?.Kind, Is.EqualTo(CoreNavalFieldKind.Thunder));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ThunderFinisherPreservesChainOnlyInsideTheField(bool inField)
        {
            var s = Start("boss_route_golden_tide_trade_thunder", 6, 100);
            s.Battle.PhaseTurn = 2; // Ordinary field benefits; rush immunity has its own regression coverage.
            s.Battle.NavalFields.Enemy.ThunderChain = 10;
            if (inField) Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Thunder, false, 0, -1, false, 0, 0), Is.True);
            int quote = CannonQuote(s), hull = s.Hull;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(hull - s.Hull, Is.EqualTo(quote));
            Assert.That(s.Battle.NavalFields.Enemy.ThunderChain, Is.EqualTo(inField ? 11 : 0),
                "Outside the field the finisher clears the complete chain after its attack; the field preserves all of it.");
        }

        [TestCase(false, 2)]
        [TestCase(true, 2)]
        [TestCase(false, 6)]
        [TestCase(true, 6)]
        public void ThunderOverloadRemainsARealDamageRiskAfterTheBossAttack(bool immune, int step)
        {
            var s = Start("boss_route_golden_tide_trade_thunder", step, 100);
            s.Battle.PhaseTurn = 2; // Test per-kind damage immunity independently of full rush immunity.
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Thunder, false, 0, -1, false, 0, 0), Is.True);
            var ship = s.Battle.NavalFields.Enemy; ship.ThunderChain = 49;
            if (immune) { ship.FieldImmunityMask = 1 << (int)CoreNavalFieldKind.Thunder; ship.FieldImmunityExpires = 99; }
            s.Battle.PlayerBlock = 10000; int enemyHull = s.Battle.EnemyHull;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.LastEnemyGuardAbsorbed, Is.GreaterThan(0));
            Assert.That(ship.ThunderChain, Is.EqualTo(50));
            Assert.That(enemyHull - s.Battle.EnemyHull, Is.EqualTo(immune ? 0 : CoreThunderRules.OverloadDamage));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BloodCostIsOptionalButTheBonusRequiresFullPayment(bool canPay)
        {
            var s = Start("boss_route_golden_tide_trade", 2, 100);
            object plan = Plan(s); CoreNavalBossMove move = Move(plan);
            s.Battle.EnemyHull = move.BloodCost + (canPay ? 100 : 0);
            int before = s.Battle.EnemyHull;
            int expected = CoreNavalStatRules.ReduceIncomingCannonDamage(Int(plan, "Attack") + (canPay ? move.BloodDamageBonus : 0), s.EffectiveArmor);
            Assert.That(CannonQuote(s), Is.EqualTo(expected));
            int hull = s.Hull; Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(hull - s.Hull, Is.EqualTo(expected));
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(before - (canPay ? move.BloodCost : 0)));
        }

        [Test]
        public void BloodPoolConvertsOnlyTheBossActualLifePaymentIntoGuard()
        {
            var s = Start("boss_route_golden_tide_trade", 2, 100);
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Blood, false, 0, -1, false, 0, 0), Is.True);
            // Keep one passive-loss turn after the player end so the weaker route
            // guardian still benefits from holding the pool instead of leaving it.
            s.NavalFieldAt(s.Battle.EnemyPositionMeters).ExpiresAtOwnerTurn =
                s.Battle.NavalFields.CompletedPlayerTurns + 2;
            int cost = Move(Plan(s)).BloodCost, before = s.Battle.EnemyHull;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(before - cost - 1), "The enemy pool has one turn left after the player end.");
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(2 * cost), "Only the active payment rebates guard; passive pool loss does not.");
        }

        [Test]
        public void BloodPaymentCanTriggerImmediateCurseExecutionBeforeTheBossFires()
        {
            var s = Start("boss_route_golden_tide_trade", 2, 100);
            CoreBattleState battle = s.Battle;
            SetHull(s, s.MaxHull);
            int cost = Move(Plan(s)).BloodCost;
            Assert.That(cost, Is.GreaterThan(0));
            battle.EnemyHull = 100; battle.Schools.CurseMarks = battle.Schools.CurseTurns = 100 - cost;
            Assert.That(CannonQuote(s), Is.Zero, "Public intent must not promise an attack cancelled by the life payment.");
            int hull = s.Hull;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Hull, Is.EqualTo(hull), "The life payment triggers execution before the cannon damage.");
            Assert.That(battle.EnemyHull, Is.Zero); Assert.That(battle.EnemyNavalExecutedByCurse, Is.True);
        }

        [Test]
        public void BloodBossLeavingCannonRangeDoesNotQuoteOrPayALethalLifeCost()
        {
            var s = Start("boss_route_golden_tide_trade", 2, 400);
            Call(s.Battle, "SetTacticalPose", false, new VoyagePoint(400, 0), 0f);
            // Staying in this domain would execute the boss at its turn end, so
            // its legal avoidance path leaves the four-cell firing radius.
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Curse, false, 3, 0, false, 0, 0), Is.True);
            s.Battle.EnemyHull = 100; s.Battle.Schools.CurseMarks = s.Battle.Schools.CurseTurns = 99;
            s.Battle.EnemyLoadedShot = 7;
            object plan = Plan(s);
            Assert.That(Move(plan).BloodCost, Is.GreaterThan(0));
            var movement = s.GetEnemyNavalFieldPreview((float)Field(plan, "Move"));
            Assert.That(CoreVoyageSession.NavalGridDistance(s.Battle.PlayerPositionMeters, movement.FinalPositionMeters), Is.GreaterThan(4));
            string before = Snapshot(s);
            var intent = CoreBattleIntentPresentation.Build(s);
            Assert.That(intent.Any(x => x.Id == "enemy_out_of_range"), Is.True);
            Assert.That(intent.Any(x => x.Id == "enemy_boss_payment_execution"), Is.False);
            Assert.That(CannonQuote(s), Is.Zero);
            Assert.That(s.GetEnemyIntent(), Does.Not.Contain("后会触发已有咒印"));
            Assert.That(Snapshot(s), Is.EqualTo(before));
            int hull = s.Hull;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Hull, Is.EqualTo(hull));
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(100));
            Assert.That(s.Battle.EnemyNavalExecutedByCurse, Is.False);
            Assert.That(s.Battle.EnemyLoadedShot, Is.EqualTo(7));
            Assert.That(s.Battle.EnemyNavalBossActionsCompleted, Is.EqualTo(1));
        }

        [TestCase(0, false)]
        [TestCase(6, false)]
        [TestCase(6, true)]
        public void BloodLifestealUsesActualHullLostAndRespectsHealingBan(int loss, bool banned)
        {
            var s = Start("boss_route_golden_tide_trade", 5, 100);
            s.Battle.EnemyHull -= 100;
            if (banned) s.ApplyBattleHealingBan(false, 2);
            int percent = Move(Plan(s)).LifeStealPercent, quote = CannonQuote(s);
            Assert.That(quote, Is.GreaterThanOrEqualTo(loss));
            s.Battle.PlayerBlock = quote - loss;
            int enemyHull = s.Battle.EnemyHull, hull = s.Hull;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(hull - s.Hull, Is.EqualTo(loss));
            Assert.That(s.Battle.EnemyHull - enemyHull, Is.EqualTo(banned ? 0 : loss * percent / 100));
        }

        [Test]
        public void BloodLifestealDoesNotCountOverkill()
        {
            var s = Start("boss_route_golden_tide_trade", 5, 100);
            s.Battle.EnemyHull -= 100; SetHull(s, 3);
            CoreBattleState battle = s.Battle;
            int percent = Move(Plan(s)).LifeStealPercent, enemyHull = battle.EnemyHull;
            Assert.That(CannonQuote(s), Is.GreaterThan(3));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Hull, Is.Zero);
            Assert.That(battle.EnemyHull - enemyHull, Is.EqualTo(3 * percent / 100));
        }

        [Test]
        public void CurseApplicationImmediatelyExecutesInsteadOfWaitingForAnotherTurn()
        {
            var s = Start("boss_route_moon_tide_ruins", 1, 100);
            int marks = Move(Plan(s)).Marks;
            SetHull(s, CannonQuote(s) + marks);
            CoreBattleState battle = s.Battle;
            Assert.That(s.Battle.PlayerCurseMarks, Is.Zero);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Hull, Is.Zero); Assert.That(battle.PlayerNavalExecutedByCurse, Is.True);
        }

        [Test]
        public void CurseDamageBypassesGuardWhileOrdinaryDamageRemainsBlockable()
        {
            var s = Start("boss_route_moon_tide_ruins", 5, 100);
            CoreNavalBossMove move = Move(Plan(s));
            s.Battle.PlayerBlock = 10000; int hull = s.Hull, quote = CannonQuote(s);
            Assert.That(move.PiercingDamage, Is.GreaterThan(0));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(hull - s.Hull, Is.EqualTo(Math.Min(quote, move.PiercingDamage)));
            Assert.That(s.Battle.LastEnemyGuardAbsorbed, Is.EqualTo(quote - Math.Min(quote, move.PiercingDamage)));
        }

        [Test]
        public void FireAppliesBurnAfterItsAttackAndExistingBurnSettlesBeforeTheNextEnemyAction()
        {
            var s = Start("boss_route_shattered_sail_reef", 2, 100);
            int burn = Move(Plan(s)).Burn; s.Battle.PlayerBlock = 10000;
            int hull = s.Hull; Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Hull, Is.EqualTo(hull)); Assert.That(s.Battle.PlayerBurnStacks, Is.EqualTo(burn));
            s.ClearNavalFields(); // Isolate existing burn from the newly introduced field action.
            SetStep(s, 6); Pose(s, 100);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Hull, Is.EqualTo(hull - burn));
            Assert.That(s.Battle.PlayerBurnStacks, Is.EqualTo(burn - 1));
        }

        private static CoreVoyageSession Start(string id, int step = 1, int distance = 400)
        {
            var s = new CoreVoyageSession(); s.SetSeed("BOSS-BEHAVIOR-TEST"); s.StartVoyage();
            CoreEncounter encounter = CoreRouteBossCatalog.Find(id)?.CreateEncounter() ??
                CoreLegendaryWarships.Find(id)?.CreateEncounter() ?? CoreLegendaryWarships.CreateTreasureEncounter();
            Call(s, "StartBattle", encounter, true);
            s.Battle.Stage = NavalBattleStage.Naval; SetStep(s, step);
            SetHull(s, 10000); s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 10000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = s.Battle.EnemyLoadedShot = 0;
            Pose(s, distance); return s;
        }
        private static void Pose(CoreVoyageSession s, int distance)
        {
            Call(s.Battle, "SetTacticalPose", true, new VoyagePoint(0, 0), 90f);
            Call(s.Battle, "SetTacticalPose", false, new VoyagePoint(distance, 0), 90f);
        }
        private static object Plan(CoreVoyageSession s) => Call(s, "BuildEnemyPlan");
        private static void SetStep(CoreVoyageSession s, int step)
        {
            s.Battle.PhaseTurn = 2; // Cycle behavior is independent of scheduled immunity and rapid movement.
            s.Battle.EnemyNavalBossActionsCompleted = step - 1;
        }
        private static CoreNavalBossMove Move(object plan) => (CoreNavalBossMove)Field(plan, "BossMove");
        private static object Field(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(value);
        private static int Int(object value, string name) => (int)Field(value, name);
        private static object Call(object value, string name, params object[] args) => value.GetType().GetMethod(name, Private).Invoke(value, args);
        private static int CannonQuote(CoreVoyageSession s) => CoreBattleIntentPresentation.Build(s).Where(x => x.Kind == CoreBattleIntentKind.Cannon).Sum(x => x.Amount ?? 0);
        private static void SetHull(CoreVoyageSession s, int hull) => typeof(CoreVoyageSession).GetProperty("Hull").SetValue(s, hull);
        private static int CardIndex(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).First(i => CoreVoyageSession.CardLibrary[i].Id == id);
        private static void Play(CoreVoyageSession s, string id)
        {
            s.Battle.Energy = 100; s.Cargo.Powder = s.Cargo.Cannonballs = s.Cargo.Wood = s.Cargo.Medicine = 100;
            s.Battle.Hand.Clear(); s.Battle.Hand.Add(CardIndex(id));
            Assert.That(s.PlayCard(0), Is.True, id);
        }
        private static string IconSnapshot(IReadOnlyList<CoreBattleIntent> items) => string.Join("|", items.Select(x => x.Id + ":" + x.Amount + ":" + x.Tooltip));
        private static string Snapshot(CoreVoyageSession s)
        {
            var b = s.Battle; var ship = b.NavalFields.Enemy;
            return string.Join("|", new object[] { s.Hull,b.Stage,b.Turn,b.PhaseTurn,b.EnemyHull,b.PlayerBlock,b.EnemyBlock,
                b.EnemyLoadedShot,b.PlayerPositionMeters.X,b.PlayerPositionMeters.Y,b.EnemyPositionMeters.X,b.EnemyPositionMeters.Y,
                b.Energy,b.Schools.Mana,b.Schools.ThunderChain,b.Schools.CurseMarks,b.PlayerBurnStacks,b.PlayerCurseMarks,
                ship.Mana,ship.ManaLimit,ship.ThunderChain,b.EnemyTidePower,b.EnemyTidalGuard,b.NavalFields.CompletedPlayerTurns,
                b.NavalFields.CompletedEnemyTurns,b.NavalFields.NextInstanceId,b.DecisionSeed,b.EnemyNavalBossActionsCompleted,
                string.Join(";", b.NavalFields.Cells.Select(x => x.Kind + ":" + x.X + ":" + x.Y + ":" + x.ExpiresAtOwnerTurn)) });
        }
    }
}
