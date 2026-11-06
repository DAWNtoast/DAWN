using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreNavalBossFieldBehavior20261026Tests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static IEnumerable<string> BossIds => CoreRouteBossCatalog.All.Select(x => x.Id)
            .Concat(CoreLegendaryWarships.All.Select(x => x.Id)).Concat(new[] { CoreBattleBalance.TreasureBossId });
        private static IEnumerable<object[]> PlacementCases
        {
            get
            {
                string[] ids = { "boss_route_shattered_sail_reef", "boss_route_moon_tide_ruins_arcane",
                    "boss_route_moon_tide_ruins", "boss_route_shattered_sail_reef_tide",
                    "boss_route_golden_tide_trade_thunder", "boss_route_golden_tide_trade", CoreBattleBalance.TreasureBossId };
                int[,] positions = { { 4, 0 }, { -4, 0 }, { 0, 4 }, { 0, -4 },
                    { 2, 2 }, { -2, 2 }, { 2, -2 }, { -2, -2 } };
                foreach (string id in ids)
                    for (int i = 0; i < positions.GetLength(0); i++)
                        yield return new object[] { id, positions[i, 0], positions[i, 1] };
            }
        }

        [TestCaseSource(nameof(BossIds))]
        public void EveryBossOpensItsOwnFieldThenUsesBothExtensionAndExpansion(string id)
        {
            var s = Start(id);
            CoreNavalFieldKind expected = SchoolField(id);
            int length = Move(Plan(s)).CycleLength;
            var fieldActions = new HashSet<CoreNavalBossFieldAction>();
            for (int step = 1; step <= length * 2; step++)
            {
                s.Battle.EnemyNavalBossActionsCompleted = step - 1;
                CoreNavalBossMove move = Move(Plan(s));
                Assert.That(move, Is.Not.Null, id);
                if (move.FieldKind.HasValue) Assert.That(move.FieldKind, Is.EqualTo(expected), id + " action " + step);
                Assert.That(move.FieldAction == CoreNavalBossFieldAction.Place, Is.EqualTo((step - 1) % length == 0));
                fieldActions.Add(move.FieldAction);
            }
            CollectionAssert.Contains(fieldActions, CoreNavalBossFieldAction.Extend);
            CollectionAssert.Contains(fieldActions, CoreNavalBossFieldAction.Expand);
            s.Battle.EnemyNavalBossActionsCompleted = 0;
            object plan = Plan(s);
            CoreNavalBossMove opening = Move(plan);
            var landing = s.GetEnemyNavalFieldPreview((float)PlanValue(plan, "Move")).FinalPositionMeters;
            Assert.That(s.Battle.NavalFields.Cells, Is.Empty);
            Assert.That(s.EndBattleTurn(), Is.True);
            AssertOwnedField(s, expected, opening.FieldTurns, landing);
            Assert.That(opening.FieldTurns, Is.EqualTo(id == "boss_route_shattered_sail_reef" ? 2 :
                expected == CoreNavalFieldKind.Thunder ? 5 : 3));
            Assert.That(s.Battle.EnemyNavalBossActionsCompleted, Is.EqualTo(1));
        }

        [TestCase("boss_route_shattered_sail_reef", 2, 1, new[] { 2, 2, 1 })]
        [TestCase("legendary_pirate_ember_ark", 3, 2, new[] { 3, 4, 3 })]
        public void FireFieldChainKeepsItsIdentityAndRealLifetimeThroughPlacementExtensionAndExpansion(
            string id, int placementTurns, int extensionTurns, int[] remainingAfterAction)
        {
            var s = Start(id);
            var actions = new[] { CoreNavalBossFieldAction.Place, CoreNavalBossFieldAction.Extend, CoreNavalBossFieldAction.Expand };
            int[] cellCounts = { 9, 9, 17 };
            int instance = 0;
            Func<CoreNavalFieldState, string> footprint = state => string.Join(";", state.Cells.OrderBy(c => c.X).ThenBy(c => c.Y)
                .Select(c => c.X + ":" + c.Y + ":" + c.Kind + ":" + c.PlayerOwned + ":" + c.InstanceId + ":" + c.ExpiresAtOwnerTurn));
            for (int step = 0; step < 3; step++)
            {
                CoreNavalFieldState live = s.Battle.NavalFields;
                int completed = live.CompletedPlayerTurns;
                long eventSequence = live.PresentationSequence;
                string before = Snapshot(s);
                object plan = Plan(s);
                CoreNavalBossMove move = Move(plan);
                Assert.That(move.CycleLength, Is.EqualTo(6));
                Assert.That(move.CycleStep, Is.EqualTo(step + 1));
                Assert.That(move.FieldAction, Is.EqualTo(actions[step]));
                string intent = s.GetEnemyIntent();
                if (step == 0)
                {
                    Assert.That(move.FieldTurns, Is.EqualTo(placementTurns));
                    Assert.That(intent, Does.Contain("持续 " + placementTurns + " 个玩家回合"));
                }
                if (step == 1)
                {
                    Assert.That(move.FieldExtendTurns, Is.EqualTo(extensionTurns));
                    Assert.That(intent, Does.Contain("整处 " + extensionTurns + " 回合").And.Contain("本次延长 9 格"));
                }
                if (step == 2)
                {
                    Assert.That(move.FieldExpansionCells, Is.EqualTo(8));
                    Assert.That(intent, Does.Contain("本次新增 8 格"));
                }
                var landing = s.GetEnemyNavalFieldPreview((float)PlanValue(plan, "Move")).FinalPositionMeters;
                // The intent's field copy first settles the pending player end. Never
                // compare raw expiry with the pre-end owner clock or inject that clock.
                object context = Call(s, "CreateNavalFieldPreviewContext", false);
                var preview = (CoreNavalFieldState)context.GetType().GetField("State", Hidden | BindingFlags.Public).GetValue(context);
                Call(s, "PreviewNavalBossPlacedField", preview, landing, s.Battle.PlayerPositionMeters);
                Assert.That(preview.CompletedPlayerTurns, Is.EqualTo(completed + 1));
                Assert.That(preview.Cells.Count, Is.EqualTo(cellCounts[step]));
                Assert.That(preview.Cells.All(c => c.ExpiresAtOwnerTurn - preview.CompletedPlayerTurns == remainingAfterAction[step]), Is.True);
                Assert.That(preview.PresentationSequence, Is.Zero);
                Assert.That(s.GetEnemyIntent(), Is.EqualTo(intent));
                Assert.That(Snapshot(s), Is.EqualTo(before), "Repeated intent and preview must not mutate the live clock, fields or events.");

                Assert.That(s.EndBattleTurn(), Is.True);
                Assert.That(s.Battle.NavalFields, Is.SameAs(live));
                Assert.That(live.CompletedPlayerTurns, Is.EqualTo(completed + 1));
                Assert.That(s.Battle.EnemyNavalBossActionsCompleted, Is.EqualTo(step + 1));
                Assert.That(footprint(live), Is.EqualTo(footprint(preview)));
                Assert.That(live.Cells.All(c => !c.PlayerOwned && c.Kind == CoreNavalFieldKind.Fire &&
                    s.GetNavalFieldRemainingTurns(c) == remainingAfterAction[step]), Is.True);
                if (step == 0) instance = live.Cells[0].InstanceId;
                Assert.That(live.Cells.All(c => c.InstanceId == instance), Is.True, "Extension and expansion must preserve the original field, not silently recast it.");
                CoreNavalFieldChangeKind expectedChange = step == 1 ? CoreNavalFieldChangeKind.Extended : CoreNavalFieldChangeKind.Placed;
                Assert.That(Enumerable.Range(0, live.PresentationEventCount).Select(live.PresentationEventAt)
                    .Count(e => e.Sequence > eventSequence && e.Change == expectedChange && e.InstanceId == instance),
                    Is.EqualTo(step == 2 ? 8 : 9));
            }
            int burn = s.Battle.PlayerBurnStacks;
            Assert.That(burn, Is.GreaterThan(0));
            for (int elapsed = 1; elapsed <= remainingAfterAction[2]; elapsed++)
            {
                Assert.That(s.NavalFieldsEndTurn(true), Is.False);
                Assert.That(s.Battle.NavalFields.Cells.All(c => s.GetNavalFieldRemainingTurns(c) == remainingAfterAction[2] - elapsed), Is.True);
                if (elapsed < remainingAfterAction[2]) s.NavalFieldsBeginTurn(true);
            }
            Assert.That(s.Battle.NavalFields.Cells, Is.Empty);
            Assert.That(s.Battle.PlayerBurnStacks, Is.GreaterThanOrEqualTo(burn), "Expiring a fire field must not cleanse existing burn.");
            Assert.That(s.Battle.EnemyNavalBossActionsCompleted, Is.EqualTo(3), "Expiry must follow real player-end settlement without another boss action.");
        }

        [TestCaseSource(nameof(PlacementCases))]
        public void FieldTargetingWorksInEveryQuadrantWithoutIllegalAnchors(string id, int x, int y)
        {
            var s = Start(id);
            Pose(s, false, x, y);
            object plan = Plan(s);
            CoreNavalBossMove move = Move(plan);
            var player = s.Battle.PlayerPositionMeters;
            var landing = s.GetEnemyNavalFieldPreview((float)PlanValue(plan, "Move")).FinalPositionMeters;
            Assert.That(s.EndBattleTurn(), Is.True);
            CoreNavalFieldKind kind = SchoolField(id);
            AssertOwnedField(s, kind, move.FieldTurns, landing);
            if (kind == CoreNavalFieldKind.Fire || kind == CoreNavalFieldKind.Curse || kind == CoreNavalFieldKind.Tide)
            {
                CoreNavalFieldCell target = s.NavalFieldAt(player);
                Assert.That(target?.Kind, Is.EqualTo(kind), id + " must threaten the player's occupied cell.");
                if (kind == CoreNavalFieldKind.Tide)
                {
                    Assert.That(Math.Abs(target.DirectionX) + Math.Abs(target.DirectionY), Is.EqualTo(1));
                    float towardEnemy = target.DirectionX * (landing.X - player.X) + target.DirectionY * (landing.Y - player.Y);
                    Assert.That(towardEnemy, Is.GreaterThan(0), "The current draws the player toward the boss.");
                }
                else if (CanCoverTargetWithoutCaster(player, landing, kind))
                    Assert.That(s.NavalFieldAt(landing)?.Kind, Is.Not.EqualTo(kind), "An offensive placement must avoid its caster when a legal anchor permits it.");
            }
            else Assert.That(s.NavalFieldAt(landing)?.Kind, Is.EqualTo(kind), "The resource field must cover its caster.");
        }

        [TestCaseSource(nameof(BossIds))]
        public void NewFieldLifetimeCountsPlayerEndsExactlyOnceAndExpiresWithoutAnotherBossAction(string id)
        {
            var s = Start(id);
            int duration = Move(Plan(s)).FieldTurns;
            Assert.That(s.EndBattleTurn(), Is.True);
            int instance = s.Battle.NavalFields.Cells.Single(c => c.X == s.Battle.NavalFields.Cells.Min(p => p.X)
                && c.Y == s.Battle.NavalFields.Cells.Min(p => p.Y)).InstanceId;
            for (int elapsed = 1; elapsed <= duration; elapsed++)
            {
                Assert.That(s.NavalFieldsEndTurn(true), Is.False);
                var remaining = s.Battle.NavalFields.Cells.Where(c => c.InstanceId == instance).ToArray();
                if (elapsed == duration) Assert.That(remaining, Is.Empty);
                else Assert.That(remaining.All(c => s.GetNavalFieldRemainingTurns(c) == duration - elapsed), Is.True);
                int clock = s.Battle.NavalFields.CompletedPlayerTurns;
                Assert.That(s.NavalFieldsEndTurn(true), Is.False);
                Assert.That(s.Battle.NavalFields.CompletedPlayerTurns, Is.EqualTo(clock), "Repeated end settlement cannot shorten a field twice.");
                if (elapsed < duration) s.NavalFieldsBeginTurn(true);
            }
            Assert.That(s.Battle.EnemyNavalBossActionsCompleted, Is.EqualTo(1));
        }

        [TestCaseSource(nameof(BossIds))]
        public void EveryActionHasReadOnlyIntentAndDealsItsAdvertisedDamageWithAVisibleRecoveryWindow(string id)
        {
            int length = Move(Plan(Start(id))).CycleLength;
            var quoted = new int[length];
            for (int step = 1; step <= length; step++)
            {
                var s = Start(id, step);
                s.Battle.PlayerBlock = 11;
                s.Battle.EnemyTidePower = 20;
                s.Battle.NavalFields.Enemy.ManaLimit = 3;
                object plan = Plan(s);
                CoreNavalBossMove move = Move(plan);
                string before = Snapshot(s);
                string intent = s.GetEnemyIntent();
                string icons = IconSnapshot(s);
                quoted[step - 1] = CannonQuote(s);
                for (int read = 0; read < 3; read++)
                {
                    Assert.That(s.GetEnemyIntent(), Is.EqualTo(intent));
                    Assert.That(IconSnapshot(s), Is.EqualTo(icons));
                    s.GetEnemyNavalFieldPreview((float)PlanValue(plan, "Move"));
                    Assert.That(Snapshot(s), Is.EqualTo(before), id + " action " + step);
                }
                int piercing = Math.Min(quoted[step - 1], move.PiercingDamage);
                int absorbed = Math.Min(s.Battle.PlayerBlock, quoted[step - 1] - piercing);
                int hull = s.Hull;
                Assert.That(s.EndBattleTurn(), Is.True);
                Assert.That(hull - s.Hull, Is.EqualTo(quoted[step - 1] - absorbed), id + " action " + step);
                Assert.That(s.Battle.LastEnemyGuardAbsorbed, Is.EqualTo(absorbed));
            }
            Assert.That(quoted[length - 2], Is.GreaterThan(quoted[length - 1]), id + " must expose a lower-damage recovery after its burst.");
        }

        [TestCaseSource(nameof(BossIds))]
        public void PlacementReplacesOldDamageModifiersBeforeBothTheQuoteAndTheShot(string id)
        {
            var s = Start(id);
            s.Battle.NavalFields.Enemy.ManaLimit = 0;
            object plan = Plan(s);
            CoreNavalBossMove move = Move(plan);
            var landing = s.GetEnemyNavalFieldPreview((float)PlanValue(plan, "Move")).FinalPositionMeters;
            var player = s.Battle.PlayerPositionMeters;
            AddCell(s, CoreNavalFieldKind.Fire, landing, true, 20);
            AddCell(s, CoreNavalFieldKind.Arcane, player, false, 20);
            // New hazards can change the authored move request; derive the actual
            // firing position after placing them, before asserting terrain multipliers.
            plan = Plan(s); move = Move(plan);
            landing = s.GetEnemyNavalFieldPreview((float)PlanValue(plan, "Move")).FinalPositionMeters;
            string before = Snapshot(s);
            int quote = CannonQuote(s);
            Assert.That(Snapshot(s), Is.EqualTo(before));
            int hull = s.Hull;
            Assert.That(s.EndBattleTurn(), Is.True);
            // Read the committed footprint at the attack locations, independently
            // of the preview helper. End-turn currents may subsequently move a ship.
            CoreNavalFieldKind? attacker = s.NavalFieldAt(landing)?.Kind;
            CoreNavalFieldKind? defender = s.NavalFieldAt(player)?.Kind;
            int raw = (int)PlanValue(plan, "Attack") + move.PiercingDamage;
            if (move.BloodCost > 0) raw += move.BloodDamageBonus;
            decimal multiplier = (attacker == CoreNavalFieldKind.Fire ? 1.25m : 1m) *
                (defender == CoreNavalFieldKind.Arcane ? 1.25m : defender == CoreNavalFieldKind.Curse ? .75m : 1m);
            int expected = CoreNavalStatRules.ReduceIncomingCannonDamage((int)Math.Ceiling(raw * multiplier), s.EffectiveArmor);
            Assert.That(quote, Is.EqualTo(expected), id);
            Assert.That(hull - s.Hull, Is.EqualTo(expected), id);
        }

        [TestCaseSource(nameof(BossIds))]
        public void RapidMovementOccursEveryFiveTurnsAndKeepsFieldActionsWithoutCleansingOldDebuffs(string id)
        {
            var schedule = Start(id);
            var turns = new List<int>();
            for (int turn = 1; turn <= 10; turn++)
            {
                schedule.Battle.PhaseTurn = turn;
                if (schedule.EnemyNavalBossRushThisTurn) turns.Add(turn);
            }
            CollectionAssert.AreEqual(new[] { 5, 10 }, turns);
            foreach (int turn in turns)
            {
                var s = Start(id);
                s.Battle.PhaseTurn = turn;
                s.Battle.EnemyBurnTurns = s.Battle.EnemyBurnDamage = 7;
                s.Battle.Schools.CurseMarks = s.Battle.Schools.CurseTurns = 2;
                object plan = Plan(s);
                CoreNavalBossMove move = Move(plan);
                var landing = s.GetEnemyNavalFieldPreview((float)PlanValue(plan, "Move")).FinalPositionMeters;
                Assert.That(CoreBattleIntentPresentation.Build(s).Any(i => i.Id == "enemy_boss_rush"), Is.True);
                Assert.That(s.EndBattleTurn(), Is.True);
                AssertOwnedField(s, SchoolField(id), move.FieldTurns, landing);
                Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(6), "Existing burn still settles and decays on a rush turn.");
                Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(2), "Rush is not a cleanse and the new field cannot add marks to an immune boss.");
                Assert.That(s.Battle.NavalFields.Enemy.AllFieldEffectsImmune, Is.False, "Immunity ends after this enemy turn.");
            }
        }

        [TestCase("boss_route_golden_tide_trade")]
        [TestCase("legendary_pirate_red_widow")]
        public void ANewBloodPoolChargesItsCasterAtTurnEndWithoutTreatingPassiveLossAsPayment(string id)
        {
            var s = Start(id);
            object plan = Plan(s);
            CoreNavalBossMove move = Move(plan);
            Assert.That(move.LifeStealPercent, Is.Zero, "Opening is setup; lifesteal belongs to its later payoff.");
            int hull = s.Battle.EnemyHull;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(hull - s.Battle.EnemyHull, Is.EqualTo(move.BloodCost + move.FieldTurns));
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo((int)PlanValue(plan, "Guard") + 2 * move.BloodCost));
            int after = s.Battle.EnemyHull;
            Assert.That(s.NavalFieldsEndTurn(false), Is.False);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(after), "The same enemy end cannot drain the new pool twice.");
        }

        [TestCase("boss_route_golden_tide_trade", false)]
        [TestCase("boss_route_golden_tide_trade", true)]
        [TestCase("legendary_pirate_red_widow", false)]
        [TestCase("legendary_pirate_red_widow", true)]
        public void BossLifePaymentKeepsBloodPoolGuardDuringNegativeEffectImmunity(string id, bool rush)
        {
            int payingStep = 0;
            for (int step = 1; step <= Move(Plan(Start(id))).CycleLength; step++)
            {
                CoreNavalBossMove candidate = Move(Plan(Start(id, step)));
                if (candidate.BloodCost > 0 && candidate.LifeStealPercent == 0) { payingStep = step; break; }
            }
            Assert.That(payingStep, Is.GreaterThan(0), "The blood cycle must include a real life-payment action.");
            var s = Start(id, payingStep);
            Pose(s, false, 1, 0);
            s.Battle.PhaseTurn = rush ? 5 : 3; // Full or own-school negative immunity retains the positive rebate.
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Blood, false, 1, -1, false, 0, 0), Is.True);
            object plan = Plan(s);
            CoreNavalBossMove move = Move(plan);
            int hull = s.Battle.EnemyHull;
            Assert.That(s.EndBattleTurn(), Is.True);
            int passiveLoss = 0; // Both schedules protect this blood boss from passive pool loss.
            Assert.That(hull - s.Battle.EnemyHull, Is.EqualTo(move.BloodCost + passiveLoss));
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo((int)PlanValue(plan, "Guard") + 2 * move.BloodCost));
        }

        [TestCaseSource(nameof(BossIds))]
        public void TwelveConsecutiveNavalTurnsAdvanceCyclesAndRetireOldFieldsWithoutResettingCombatState(string id)
        {
            var s = Start(id);
            s.Battle.Turn = s.Battle.PhaseTurn = 1;
            // This fixture's independent ram rolls all exceed the highest catalog
            // chance through turn 12; it isolates sustained naval behavior while
            // the adjacent test separately proves that recovery can still ram.
            typeof(CoreBattleState).GetProperty("DecisionSeed").SetValue(s.Battle, 674639u);
            int firstInstance = -1, casts = 0;
            for (int turn = 1; turn <= 12; turn++)
            {
                int completed = s.Battle.EnemyNavalBossActionsCompleted;
                int nextInstance = s.Battle.NavalFields.NextInstanceId;
                string before = Snapshot(s);
                s.GetEnemyIntent();
                CoreBattleIntentPresentation.Build(s);
                Assert.That(Snapshot(s), Is.EqualTo(before), id + " turn " + turn);
                Assert.That(s.EndBattleTurn(), Is.True);
                Assert.That(s.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval), id + " turn " + turn);
                Assert.That(s.Battle.EnemyNavalBossActionsCompleted, Is.InRange(completed, completed + 1));
                Assert.That(s.Battle.NavalFields.Cells.All(c => s.GetNavalFieldRemainingTurns(c) > 0), Is.True);
                if (s.Battle.NavalFields.NextInstanceId > nextInstance)
                {
                    casts++;
                    if (firstInstance < 0) firstInstance = nextInstance;
                }
            }
            Assert.That(s.Battle.EnemyNavalBossActionsCompleted, Is.GreaterThanOrEqualTo(8));
            Assert.That(casts, Is.GreaterThanOrEqualTo(2));
            Assert.That(s.Battle.NavalFields.Cells.Any(c => c.InstanceId == firstInstance), Is.False,
                "The first field must expire or be overwritten during the continuous fight.");
        }

        [TestCaseSource(nameof(BossIds))]
        public void AWinningRamRollCannotReplaceSetupOrBurstButCanReplaceRecovery(string id)
        {
            var s = Start(id);
            s.Battle.Turn = 1;
            Call(s.Battle, "SetTacticalPose", false, new VoyagePoint(100, 0), 180f);
            bool found = false;
            for (uint seed = 0; seed < 1000; seed++)
            {
                typeof(CoreBattleState).GetProperty("DecisionSeed").SetValue(s.Battle, seed);
                if (!(bool)Call(s, "EnemyChoosesRam")) continue;
                found = true;
                break;
            }
            Assert.That(found, Is.True, "The fixture needs a legal head-on ram and a winning deterministic roll.");
            int length = Move(Plan(s)).CycleLength;
            for (int step = 1; step < length; step++)
            {
                s.Battle.EnemyNavalBossActionsCompleted = step - 1;
                Assert.That(Move(Plan(s))?.CycleStep, Is.EqualTo(step));
                Assert.That(s.EnemyWillRam, Is.False, id + " action " + step);
            }
            s.Battle.EnemyNavalBossActionsCompleted = length - 1;
            Assert.That(s.EnemyWillRam, Is.True);
            Assert.That(Move(Plan(s)), Is.Null);
            s.Battle.PhaseTurn = 5;
            Assert.That(s.EnemyWillRam, Is.False, "Rush remains a naval pursuit and cannot become an unannounced ram.");
            Assert.That(Move(Plan(s))?.CycleStep, Is.EqualTo(length));
            s.Battle.PhaseTurn = 2;
            Call(s.Battle, "SetTacticalPose", false, new VoyagePoint(100, 0), 90f);
            Assert.That(s.EnemyWillRam, Is.False, "A winning roll cannot bypass the existing contact geometry.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BloodPaymentGuardIsAdvertisedWithoutConsumingRuneClampAndMatchesTheRealAction(bool clamped)
        {
            var s = Start("boss_route_golden_tide_trade", 2);
            s.Battle.PhaseTurn = 3; // Own-school protection isolates the positive rebate from avoidance of passive loss.
            Pose(s, false, 1, 0);
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Blood, false, 1, -1, false, 0, 0), Is.True);
            CoreNavalBossMove move = Move(Plan(s));
            int cannonBeforeClamp = CannonQuote(s);
            s.Battle.NavalExpansion.RuneClamp = clamped;
            s.Battle.NavalExpansion.RuneClampReduction = clamped ? 3 : 0;
            int expectedGuard = Math.Max(0, 2 * move.BloodCost - (clamped ? 3 : 0));
            string before = Snapshot(s);
            for (int read = 0; read < 3; read++)
            {
                Assert.That(GuardQuote(s), Is.EqualTo(expectedGuard));
                Assert.That(s.GetEnemyIntent(), Does.Contain("行动后格挡 " + expectedGuard));
                Assert.That(CannonQuote(s), Is.EqualTo(cannonBeforeClamp));
                Assert.That(s.Battle.NavalExpansion.RuneClamp, Is.EqualTo(clamped));
                Assert.That(s.Battle.NavalExpansion.RuneClampReduction, Is.EqualTo(clamped ? 3 : 0));
                Assert.That(Snapshot(s), Is.EqualTo(before));
            }
            int hull = s.Hull;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(hull - s.Hull, Is.EqualTo(cannonBeforeClamp));
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(expectedGuard));
            Assert.That(s.Battle.NavalExpansion.RuneClamp, Is.False);
            Assert.That(s.Battle.NavalExpansion.RuneClampReduction, Is.Zero);
            Call(s, "PayBattleHealthCost", false, 5);
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(expectedGuard + 10), "The consumed clamp must not reduce a second payment's pool guard.");
        }

        [TestCase("insufficient_hull")]
        [TestCase("curse_execution")]
        [TestCase("out_of_range")]
        public void BloodPoolGuardIsNotAdvertisedWhenTheLifePaymentCannotBenefitTheBoss(string condition)
        {
            var s = Start("boss_route_golden_tide_trade", 2);
            Pose(s, false, 1, 0);
            int cost = Move(Plan(s)).BloodCost;
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Blood, false, 1, -1, false, 0, 0), Is.True);
            if (condition == "insufficient_hull") s.Battle.EnemyHull = cost;
            else if (condition == "curse_execution")
            {
                s.Battle.EnemyHull = 100;
                s.Battle.Schools.CurseMarks = s.Battle.Schools.CurseTurns = 100 - cost;
            }
            else
            {
                s.Battle.NavalFields.Cells.Clear();
                Pose(s, false, 4, 0);
                Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Blood, false, 3, 0, false, 0, 0), Is.True);
                Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Tide, true, 0, 0, false, -1, 0), Is.True);
                Assert.That(Move(Plan(s)), Is.Null, "The pending current puts the player outside cannon range, requiring pursuit instead of payment.");
            }
            object plan = Plan(s);
            string before = Snapshot(s);
            int normalGuard = (int)PlanValue(plan, "Guard");
            Assert.That(GuardQuote(s), Is.EqualTo(normalGuard), condition + " cannot promise a blood-pool rebate.");
            s.GetEnemyIntent();
            Assert.That(Snapshot(s), Is.EqualTo(before));
            if (condition == "curse_execution")
                Assert.That(CoreBattleIntentPresentation.Build(s).Any(i => i.Id == "enemy_boss_payment_execution"), Is.True);
        }

        private static bool CanCoverTargetWithoutCaster(VoyagePoint target, VoyagePoint caster, CoreNavalFieldKind kind)
        {
            int tx = (int)Math.Round(target.X / 100), ty = (int)Math.Round(target.Y / 100);
            int cx = (int)Math.Round(caster.X / 100), cy = (int)Math.Round(caster.Y / 100);
            int size = CoreVoyageSession.GetNavalFieldSize(kind);
            for (int x = tx - size + 1; x <= tx; x++)
                for (int y = ty - size + 1; y <= ty; y++)
                {
                    if (Math.Abs(x - cx) + Math.Abs(y - cy) > 4) continue;
                    if (!CoreVoyageSession.IsNavalFieldSeaCell(x, y) ||
                        !CoreVoyageSession.IsNavalFieldSeaCell(x + size - 1, y + size - 1)) continue;
                    if (cx < x || cx >= x + size || cy < y || cy >= y + size) return true;
                }
            return false;
        }

        private static void AssertOwnedField(CoreVoyageSession s, CoreNavalFieldKind kind, int turns, VoyagePoint caster)
        {
            CoreNavalFieldCell[] cells = s.Battle.NavalFields.Cells.Where(c => !c.PlayerOwned && c.Kind == kind).ToArray();
            int size = CoreVoyageSession.GetNavalFieldSize(kind);
            Assert.That(cells.Length, Is.EqualTo(size * size));
            Assert.That(cells.Select(c => c.InstanceId).Distinct().Count(), Is.EqualTo(1));
            Assert.That(cells.All(c => s.GetNavalFieldRemainingTurns(c) == turns), Is.True);
            Assert.That(cells.Select(c => c.X + ":" + c.Y).Distinct().Count(), Is.EqualTo(size * size));
            int x = cells.Min(c => c.X), y = cells.Min(c => c.Y);
            Assert.That(Math.Abs(x - (int)Math.Round(caster.X / 100)) + Math.Abs(y - (int)Math.Round(caster.Y / 100)), Is.LessThanOrEqualTo(4));
            Assert.That(cells.All(c => CoreVoyageSession.IsNavalFieldSeaCell(c.X, c.Y)), Is.True);
        }

        private static CoreNavalFieldKind SchoolField(string id)
        {
            string school = CoreRouteBossCatalog.Find(id)?.SchoolId ?? CoreLegendaryWarships.Find(id)?.SchoolId;
            switch (school)
            {
                case "fire": return CoreNavalFieldKind.Fire;
                case "arcane": return CoreNavalFieldKind.Arcane;
                case "curse": return CoreNavalFieldKind.Curse;
                case "thunder": return CoreNavalFieldKind.Thunder;
                case "blood": return CoreNavalFieldKind.Blood;
                default: return CoreNavalFieldKind.Tide;
            }
        }

        private static CoreVoyageSession Start(string id, int step = 1)
        {
            var s = new CoreVoyageSession();
            s.ConfigureComponentBonuses(10000, -1000, 0, 0, 0);
            s.SetSeed("BOSS-FIELD-BEHAVIOR-20261026");
            s.StartVoyage();
            CoreEncounter encounter = CoreRouteBossCatalog.Find(id)?.CreateEncounter()
                ?? CoreLegendaryWarships.Find(id)?.CreateEncounter() ?? CoreLegendaryWarships.CreateTreasureEncounter();
            Call(s, "StartBattle", encounter, true);
            s.Battle.Stage = NavalBattleStage.Naval;
            // Keep cycle tests independent of the separate immunity / rapid-movement schedule.
            s.Battle.PhaseTurn = 2;
            s.Battle.EnemyNavalBossActionsCompleted = step - 1;
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(s, 10000);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 10000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = s.Battle.EnemyLoadedShot = 0;
            s.Battle.NavalFields.Enemy.Mana = s.Battle.NavalFields.Enemy.ManaLimit = 0;
            Pose(s, true, 0, 0); Pose(s, false, 4, 0);
            return s;
        }

        private static void AddCell(CoreVoyageSession s, CoreNavalFieldKind kind, VoyagePoint position, bool owner, int turns)
        {
            var state = s.Battle.NavalFields;
            state.Cells.Add(new CoreNavalFieldCell { Kind = kind, PlayerOwned = owner,
                X = (int)Math.Round(position.X / 100), Y = (int)Math.Round(position.Y / 100),
                InstanceId = state.NextInstanceId++, ExpiresAtOwnerTurn = turns +
                    (owner ? state.CompletedEnemyTurns : state.CompletedPlayerTurns) });
        }
        private static void Pose(CoreVoyageSession s, bool player, int x, int y) =>
            Call(s.Battle, "SetTacticalPose", player, new VoyagePoint(x * 100, y * 100), 90f);
        private static object Plan(CoreVoyageSession s) => Call(s, "BuildEnemyPlan");
        private static CoreNavalBossMove Move(object plan) => (CoreNavalBossMove)PlanValue(plan, "BossMove");
        private static object PlanValue(object plan, string name) => plan.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(plan);
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Hidden).Invoke(target, args);
        private static int CannonQuote(CoreVoyageSession s) => CoreBattleIntentPresentation.Build(s)
            .Where(i => i.Kind == CoreBattleIntentKind.Cannon).Sum(i => i.Amount ?? 0);
        private static int GuardQuote(CoreVoyageSession s) => CoreBattleIntentPresentation.Build(s)
            .Where(i => i.Id == "enemy_guard").Sum(i => i.Amount ?? 0);
        private static string IconSnapshot(CoreVoyageSession s) => string.Join("|", CoreBattleIntentPresentation.Build(s)
            .Select(i => i.Id + ":" + i.Amount + ":" + i.Tooltip));
        private static string Snapshot(CoreVoyageSession s)
        {
            var b = s.Battle; var f = b.NavalFields;
            return string.Join("|", new object[] { s.Hull, b.EnemyHull, b.PlayerBlock, b.EnemyBlock,
                b.PhaseTurn, b.Turn, b.TacticalRevision, b.EnemyLoadedShot, b.EnemyTidePower, b.EnemyTidalGuard,
                b.EnemyNavalBossActionsCompleted, b.DecisionSeed, b.PlayerPositionMeters.X, b.PlayerPositionMeters.Y,
                b.PlayerHeadingDegrees, b.EnemyPositionMeters.X, b.EnemyPositionMeters.Y, b.EnemyHeadingDegrees,
                b.PlayerBurnStacks, b.EnemyBurnTurns, b.PlayerCurseMarks, b.Schools.CurseMarks,
                f.PlayerTurn, f.EnemyTurn, f.CompletedPlayerTurns, f.CompletedEnemyTurns, f.PresentationSequence,
                f.NextInstanceId, string.Join(";", f.TriggerLedger),
                string.Join(";", f.Player.GetType().GetFields().OrderBy(x => x.Name).Select(x => x.Name + "=" + x.GetValue(f.Player))),
                string.Join(";", f.Enemy.GetType().GetFields().OrderBy(x => x.Name).Select(x => x.Name + "=" + x.GetValue(f.Enemy))),
                string.Join(";", f.Cells.Select(c => c.Kind + ":" + c.X + ":" + c.Y + ":" + c.PlayerOwned + ":" + c.InstanceId + ":" + c.ExpiresAtOwnerTurn)) });
        }
    }
}
