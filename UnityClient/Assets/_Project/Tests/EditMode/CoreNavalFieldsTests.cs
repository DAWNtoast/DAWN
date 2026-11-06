using System;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreNavalFieldsTests
    {
        [Test]
        public void Placement_ChecksAnchorRangeAndDoesNotTriggerUnderShips()
        {
            var session = NewBattle();
            int hull = session.Hull;
            Assert.That(session.CanPlaceNavalField(CoreNavalFieldKind.Fire, true, 3, 1, false, 0, 0), Is.Empty);
            Assert.That(session.CanPlaceNavalField(CoreNavalFieldKind.Fire, true, 4, 1, false, 0, 0), Is.Not.Empty);
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Fire, true, 0, 0, false, 0, 0), Is.True);
            Assert.That(session.Hull, Is.EqualTo(hull));
            Assert.That(session.Battle.NavalFields.TriggerLedger, Is.Empty);
            Assert.That(session.Battle.NavalFields.Cells.Count, Is.EqualTo(9));
            Assert.That(session.GetNavalFieldRemainingTurns(session.GetNavalFieldAt(0, 0)), Is.EqualTo(5));
        }

        [Test]
        public void Fire_EnteringNeverHurtsAndTurnEndAddsRemainingDurationOnce()
        {
            var session = NewBattle();
            int hull = session.Hull;
            Place(session, CoreNavalFieldKind.Fire, 1, 0);
            Assert.That(session.PushNavalShip(true, 1, 0), Is.True);
            Assert.That(session.PushNavalShip(true, 1, 0), Is.True);
            Assert.That(session.PushNavalShip(true, -1, 0), Is.True);
            Assert.That(session.NavalFieldsEndTurn(true), Is.False);
            Assert.That(session.Hull, Is.EqualTo(hull));
            Assert.That(session.Battle.PlayerBurnStacks, Is.EqualTo(5));
            Assert.That(session.Battle.NavalFields.TriggerLedger.Count, Is.EqualTo(1));
            session.NavalFieldsEndTurn(true);
            Assert.That(session.Battle.PlayerBurnStacks, Is.EqualTo(5));
        }

        [Test]
        public void Fire_UnderfootSettlesAtEndAndCoverageDoesNotRefreshLedger()
        {
            var session = NewBattle();
            int hull = session.Hull;
            Place(session, CoreNavalFieldKind.Fire, 0, 0);
            session.NavalFieldsEndTurn(true);
            Place(session, CoreNavalFieldKind.Blood, 0, 0);
            Place(session, CoreNavalFieldKind.Fire, 0, 0);
            session.NavalFieldsEndTurn(true);
            Assert.That(session.Hull, Is.EqualTo(hull));
            Assert.That(session.Battle.PlayerBurnStacks, Is.EqualTo(5));
            session.NavalFieldsBeginTurn(true);
            session.NavalFieldsEndTurn(true);
            Assert.That(session.Hull, Is.EqualTo(hull));
            Assert.That(session.Battle.PlayerBurnStacks, Is.EqualTo(10), "The field owner's turn does not reduce its duration.");
        }

        [Test]
        public void Fire_BurnPreventionStopsNewStacksWithoutSpendingGuardOrGeneratingTide()
        {
            var session = NewBattle();
            int hull = session.Hull;
            Place(session, CoreNavalFieldKind.Fire, 1, 0);
            session.GrantNavalFireImmunity(true);
            session.PushNavalShip(true, 1, 0);
            session.PushNavalShip(true, 1, 0);
            session.GrantNavalFireImmunity(true);
            Assert.That(session.Battle.NavalFields.Player.BurnPreventionExpiresAtOwnerTurn, Is.EqualTo(2));
            session.Battle.PlayerBlock = 2;
            Set(session.Battle.Schools, "TidePaidBlock", 2);
            session.PushNavalShip(true, 0, 1);
            session.NavalFieldsEndTurn(true);
            Assert.That(session.Hull, Is.EqualTo(hull));
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(2));
            Assert.That(session.Battle.Schools.TidePaidBlock, Is.EqualTo(2));
            Assert.That(session.Battle.Schools.TidePower, Is.Zero);
            Assert.That(session.Battle.PlayerBurnStacks, Is.Zero);
            Assert.That(session.Battle.NavalFields.TriggerLedger.Count, Is.EqualTo(1));
        }

        [Test]
        public void Fields_ExpireAtOpposingTurnEndAndNotAtOwnerStart()
        {
            var session = NewBattle();
            Place(session, CoreNavalFieldKind.Blood, 0, 0);
            session.NavalFieldsBeginTurn(true);
            Assert.That(session.Battle.PlayerBlock, Is.Zero);
            Assert.That(session.Battle.NavalFields.Cells.Count, Is.EqualTo(4));
            session.Battle.PlayerBlock = 0;
            session.NavalFieldsEndTurn(true);
            session.NavalFieldsBeginTurn(false);
            session.NavalFieldsEndTurn(false);
            session.NavalFieldsBeginTurn(true);
            Assert.That(session.Battle.NavalFields.Cells.Count, Is.EqualTo(4));
            session.Battle.PlayerBlock = 0;
            session.NavalFieldsEndTurn(true);
            session.NavalFieldsBeginTurn(false);
            session.NavalFieldsEndTurn(false);
            Assert.That(session.GetNavalFieldRemainingTurns(session.GetNavalFieldAt(0, 0)), Is.EqualTo(3));
            for (int round = 0; round < 3; round++)
            {
                session.NavalFieldsBeginTurn(false);
                session.NavalFieldsEndTurn(false);
            }
            Assert.That(session.Battle.NavalFields.Cells, Is.Empty);
            Assert.That(session.Battle.PlayerBlock, Is.Zero);
        }

        [Test]
        public void RefractionOnlyTriggersAtTurnStartWithoutPiercingOrMovementMana()
        {
            var session = NewBattle();
            Place(session, CoreNavalFieldKind.Arcane, 1, 0);
            session.NavalFieldsBeginTurn(false);
            session.PushNavalShip(true, 1, 0);
            Assert.That(session.Battle.Schools.TemporaryMana, Is.Zero);
            int before = session.Battle.Schools.Mana;
            session.NavalFieldsEndTurn(false);
            session.NavalFieldsBeginTurn(true);
            Assert.That(session.Battle.Schools.Mana, Is.EqualTo(before + 2));
            Place(session, CoreNavalFieldKind.Arcane, 2, 0);
            session.PushNavalShip(true, 1, 0);
            Assert.That(session.Battle.Schools.Mana, Is.EqualTo(before + 2));
            Assert.That(session.Battle.Schools.TemporaryMana, Is.Zero);
            Assert.That(session.Battle.NavalFields.Player.ArcanePiercing, Is.Zero);
        }

        [Test]
        public void PartialCoverageKeepsInstanceIdentityAndExpiry()
        {
            var session = NewBattle();
            Place(session, CoreNavalFieldKind.Thunder, 1, 0);
            int instance = session.GetNavalFieldAt(1, 0).InstanceId;
            Place(session, CoreNavalFieldKind.Fire, 2, 0);
            Assert.That(session.Battle.NavalFields.Cells.FindAll(cell => cell.InstanceId == instance).Count, Is.EqualTo(40));
            Assert.That(session.GetNavalFieldRemainingTurns(session.GetNavalFieldAt(1, 0)), Is.EqualTo(5));
            Assert.That(session.GetNavalFieldAt(2, 0).Kind, Is.EqualTo(CoreNavalFieldKind.Fire));
        }

        [Test]
        public void MovingFirePreservesExpiryAndCoordinateLedger()
        {
            var session = NewBattle();
            Place(session, CoreNavalFieldKind.Fire, 1, 0);
            session.PushNavalShip(true, 1, 0);
            session.PushNavalShip(true, -1, 0);
            int hull = session.Hull;
            Assert.That(session.MoveNavalFireCell(true, 1, 0, 1, -1), Is.True);
            Assert.That(session.GetNavalFieldRemainingTurns(session.GetNavalFieldAt(1, -1)), Is.EqualTo(5));
            Assert.That(session.MoveNavalFireCell(true, 1, -1, 1, 0), Is.True);
            session.PushNavalShip(true, 1, 0);
            Assert.That(session.Hull, Is.EqualTo(hull));
        }

        [Test]
        public void WaterPush_ExecutesOnceAndDoesNotCreateArrivalCredit()
        {
            var session = NewBattle();
            Place(session, CoreNavalFieldKind.Tide, 1, 0, 1, 0);
            Assert.That(session.PushNavalShip(true, 1, 0), Is.True);
            Assert.That(session.Battle.PlayerPositionMeters.X, Is.EqualTo(100f));
            session.NavalFieldsEndTurn(true);
            Assert.That(session.Battle.PlayerPositionMeters.X, Is.EqualTo(200f));
            Assert.That(session.Battle.NavalArrivalActionPoints, Is.Zero);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(1));
            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.False);
            Assert.That(session.Battle.NavalFields.TriggerLedger.Count, Is.EqualTo(1));
        }

        [Test]
        public void WaterPush_FireArrivalDoesNotHurtAndBlockedAttemptUsesQuota()
        {
            var session = NewBattle();
            int hull = session.Hull;
            Place(session, CoreNavalFieldKind.Tide, 1, 0, 1, 0);
            Place(session, CoreNavalFieldKind.Fire, 2, 0);
            session.PushNavalShip(true, 1, 0);
            session.NavalFieldsEndTurn(true);
            Assert.That(session.Battle.PlayerPositionMeters.X, Is.EqualTo(200f));
            Assert.That(session.Hull, Is.EqualTo(hull));
            Assert.That(session.Battle.PlayerBurnStacks, Is.Zero, "The end-turn field is selected before the current pushes the ship.");
            session.NavalFieldsBeginTurn(true);
            session.NavalFieldsEndTurn(true);
            Assert.That(session.Battle.PlayerBurnStacks, Is.EqualTo(5));
            var blocked = NewBattle();
            SetPose(blocked, false, 200, 0, 180);
            Place(blocked, CoreNavalFieldKind.Tide, 1, 0, 1, 0);
            blocked.PushNavalShip(true, 1, 0);
            blocked.NavalFieldsEndTurn(true);
            Assert.That(blocked.Battle.PlayerPositionMeters.X, Is.EqualTo(100f));
            Assert.That(blocked.Battle.NavalFields.TriggerLedger, Does.Contain("P:K:3"));
        }

        [Test]
        public void Anchor_IsAnAffectedPlayerChoiceAndRejectedPushConsumesIt()
        {
            var session = NewBattle();
            session.GrantNavalAnchor(true);
            Assert.That(session.PushNavalShip(true, 1, 0), Is.False);
            Assert.That(session.Battle.NavalFields.Player.AnchorCharges, Is.Zero);
            session.GrantNavalAnchor(true);
            session.PlayerRefuseNavalForcedMovement = false;
            Assert.That(session.PushNavalShip(true, 1, 0), Is.True);
            Assert.That(session.Battle.NavalFields.Player.AnchorCharges, Is.EqualTo(1));
        }

        [Test]
        public void Thunder_AddsChainAtEveryLandingAndDoesNotInterruptMovement()
        {
            var session = NewBattle();
            Place(session, CoreNavalFieldKind.Thunder, 1, 0);
            Set(session.Battle, "NavalActionPoints", 3);
            Assert.That(session.TryAppendNavalMovementSegment(new VoyagePoint(300, 0), out string reason), Is.True, reason);
            CoreNavalMovementFieldPreview preview = session.NavalMovementFieldPreview;
            Assert.That(preview.StopsAtThunder, Is.False);
            Assert.That(preview.ActiveSteps, Is.EqualTo(3));
            Assert.That(preview.Damage, Is.Zero);
            Assert.That(preview.FinalThunderChain, Is.EqualTo(15));
            Assert.That(session.CommitNavalMovement(out reason), Is.True, reason);
            Assert.That(session.Battle.PlayerPositionMeters.X, Is.EqualTo(300f));
            Assert.That(session.Battle.NavalActionPoints, Is.Zero);
            Assert.That(session.Battle.NavalArrivalActionPoints, Is.Zero);
            Assert.That(session.Battle.Schools.ThunderChain, Is.EqualTo(15));
        }

        [Test]
        public void MovementSlow_ZeroLengthCannotConsumeItAndExtraPointsPermitMovement()
        {
            var session = NewBattle();
            session.ApplyNavalMovementSlow(true);
            Assert.That(session.TryAppendNavalMovementStep(new VoyagePoint(100, 0), out _), Is.False);
            Assert.That(session.CommitNavalMovement(out _), Is.False);
            Assert.That(session.Battle.NavalFields.Player.MovementSlow, Is.EqualTo(1));
            Set(session.Battle, "NavalActionPoints", 2);
            Assert.That(session.TryAppendNavalMovementStep(new VoyagePoint(100, 0), out string reason), Is.True, reason);
            session.CommitNavalMovement(out reason);
            Assert.That(session.Battle.NavalFields.Player.MovementSlow, Is.Zero);
            Assert.That(session.Battle.NavalArrivalActionPoints, Is.EqualTo(1));
        }

        [Test]
        public void LethalFieldEndsRouteBeforeFollowingCells()
        {
            var session = NewBattle();
            Set(session, "Hull", 1);
            Set(session.Battle.Schools, "ThunderChain", 45);
            Place(session, CoreNavalFieldKind.Thunder, 1, 0);
            Set(session.Battle, "NavalActionPoints", 2);
            session.TryAppendNavalMovementSegment(new VoyagePoint(200, 0), out _);
            Assert.That(session.NavalMovementFieldPreview.Sinks, Is.True);
            CoreBattleState battle = session.Battle;
            int revision = battle.TacticalRevision;
            Assert.That(session.CommitNavalMovement(out _), Is.True);
            Assert.That(session.Phase, Is.Not.EqualTo(CoreDemoPhase.Battle));
            Assert.That(session.Battle, Is.Null);
            Assert.That(battle.PlayerPositionMeters.X, Is.EqualTo(100f));
            Assert.That(battle.TacticalRevision, Is.EqualTo(revision + 1));
            CoreNavalTacticalMovement movement = battle.LastTacticalMovement;
            Assert.That(movement, Is.Not.Null);
            Assert.That(movement.Revision, Is.EqualTo(battle.TacticalRevision));
            Assert.That(movement.Frames.Count, Is.GreaterThan(1));
            Assert.That(movement.Frames[0].PlayerPositionMeters.X, Is.Zero);
            Assert.That(movement.Frames[movement.Frames.Count - 1].PlayerPositionMeters.X, Is.EqualTo(100f));
            foreach (CoreNavalTacticalFrame frame in movement.Frames)
            {
                Assert.That(frame.PlayerPositionMeters.X, Is.InRange(0f, 100f), "The untraveled cell at 200 must never enter the terminal route.");
                Assert.That(frame.PlayerPositionMeters.Y, Is.Zero);
            }
            Assert.That(session.CommitNavalMovement(out _), Is.False);
            Assert.That(battle.LastTacticalMovement, Is.SameAs(movement), "A second callback cannot replace the terminal result.");
            Assert.That(battle.TacticalRevision, Is.EqualTo(revision + 1));
        }

        [Test]
        public void LethalFieldAfterTurnEndCurrentPush_PreservesActualForcedStep()
        {
            var session = NewBattle();
            Set(session, "Hull", 1);
            Set(session.Battle.Schools, "ThunderChain", 45);
            Place(session, CoreNavalFieldKind.Tide, 1, 0, 1, 0);
            Place(session, CoreNavalFieldKind.Thunder, 2, 0);
            Set(session.Battle, "NavalActionPoints", 1);
            Assert.That(session.TryAppendNavalMovementStep(new VoyagePoint(100, 0), out string reason), Is.True, reason);
            CoreBattleState battle = session.Battle;
            int revision = battle.TacticalRevision;
            Assert.That(session.CommitNavalMovement(out reason), Is.True, reason);
            Assert.That(session.Battle, Is.SameAs(battle));
            Assert.That(session.ConfirmNavalArrivalHeading(0, out reason), Is.True, reason);
            revision = battle.TacticalRevision;
            Assert.That(session.NavalFieldsEndTurn(true), Is.True);
            Assert.That(session.Battle, Is.Null);
            Assert.That(session.Phase, Is.Not.EqualTo(CoreDemoPhase.Battle));
            Assert.That(battle.PlayerPositionMeters.X, Is.EqualTo(200f), "The current pushes once at turn end into lethal thunder.");
            Assert.That(battle.TacticalRevision, Is.EqualTo(revision + 1));
            CoreNavalTacticalMovement movement = battle.LastTacticalMovement;
            Assert.That(movement, Is.Not.Null);
            Assert.That(movement.Revision, Is.EqualTo(battle.TacticalRevision));
            Assert.That(movement.Frames[0].PlayerPositionMeters.X, Is.EqualTo(100f));
            Assert.That(movement.Frames[movement.Frames.Count - 2].PlayerPositionMeters.X, Is.EqualTo(100f));
            Assert.That(movement.Frames[movement.Frames.Count - 1].PlayerPositionMeters.X, Is.EqualTo(200f));
            foreach (CoreNavalTacticalFrame frame in movement.Frames)
                Assert.That(frame.PlayerPositionMeters.X, Is.InRange(0f, 200f));
        }

        [Test]
        public void FieldSnapshot_IsDeepAndRejectsInvalidLedgerWithoutMutation()
        {
            var session = NewBattle();
            Place(session, CoreNavalFieldKind.Fire, 1, 0);
            session.PushNavalShip(true, 1, 0);
            CoreNavalFieldState snapshot = session.CaptureNavalFields();
            int hull = session.Hull;
            session.ClearNavalFields();
            Assert.That(session.RestoreNavalFields(snapshot), Is.True);
            snapshot.Cells[0].X = 100;
            session.PushNavalShip(true, -1, 0);
            session.PushNavalShip(true, 1, 0);
            Assert.That(session.Hull, Is.EqualTo(hull));
            Assert.That(session.GetNavalFieldAt(1, 0), Is.Not.Null);
            snapshot = session.CaptureNavalFields();
            snapshot.TriggerLedger.Add("invalid");
            Assert.That(session.RestoreNavalFields(snapshot), Is.False);
            Assert.That(session.Battle.NavalFields.TriggerLedger, Does.Not.Contain("invalid"));
        }

        [Test]
        public void CurseDomainAddsMarksAtEndAndReplacementCannotTriggerTwice()
        {
            var session = NewBattle();
            SetPose(session, false, 200, 0, 180);
            Place(session, CoreNavalFieldKind.Curse, 2, 0);
            Assert.That(session.NavalFieldCurseBonus(false, 0), Is.Zero);
            session.NavalFieldsEndTurn(false);
            Assert.That(session.Battle.Schools.CurseMarks, Is.EqualTo(5));
            Place(session, CoreNavalFieldKind.Curse, 2, 0);
            Assert.That(session.NavalFieldCurseBonus(false, 6), Is.Zero);
            session.NavalFieldsEndTurn(false);
            Assert.That(session.Battle.Schools.CurseMarks, Is.EqualTo(5));
        }

        [Test]
        public void EnemyPath_CrossesFireWithoutEntryDamageAndReportsStandingBurn()
        {
            var session = NewBattle();
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Fire, false, 3, -1, false, 0, 0), Is.True);
            MethodInfo plan = typeof(CoreVoyageSession).GetMethod("BuildNavalFieldEnemyPath", BindingFlags.Instance | BindingFlags.NonPublic);
            var detour = (System.Collections.Generic.List<CoreNavalMovementNode>)plan.Invoke(session, new object[] { 300f, null });
            Assert.That(detour.Count, Is.GreaterThan(1));
            Assert.That(session.GetEnemyNavalFieldPreview(300f).Damage, Is.Zero);
            var urgent = (System.Collections.Generic.List<CoreNavalMovementNode>)plan.Invoke(session, new object[] { 100f, null });
            Assert.That(urgent.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(session.GetEnemyNavalFieldIntent(0f), Does.Contain("燃烧").And.Not.Contain("场地伤害"));
        }
        [Test]
        public void RealTurnFlow_BloodPoolHasNoStandingBenefitAndLastsFiveOpposingTurns()
        {
            var session = NewBattle();
            Place(session, CoreNavalFieldKind.Blood, 0, 0);
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.PlayerBlock, Is.Zero);
            Assert.That(session.Battle.NavalFields.Player.BloodAttackBonus, Is.Zero);
            Assert.That(session.Battle.NavalFields.Cells.Count, Is.EqualTo(4));
            for (int round = 1; round < 5; round++) Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.NavalFields.Cells, Is.Empty);
            Assert.That(session.Battle.PlayerBlock, Is.Zero);
            Assert.That(session.Battle.NavalFields.Player.BloodAttackBonus, Is.Zero);
        }
        [Test]
        public void CommittedThunderRoute_DoesNotPreviewPreviouslySailedRoute()
        {
            var session = NewBattle();
            Place(session, CoreNavalFieldKind.Thunder, 1, 0);
            Set(session.Battle, "NavalActionPoints", 3);
            session.TryAppendNavalMovementSegment(new VoyagePoint(300, 0), out _);
            session.CommitNavalMovement(out _);
            Assert.That(session.NavalMovementFieldPreview.ActiveSteps, Is.Zero);
            Assert.That(session.NavalMovementFieldPreview.FinalPositionMeters.X, Is.EqualTo(300));
            Assert.That(session.NavalMovementFieldPreview.Events, Is.Empty);
        }

        [Test]
        public void BlockedPush_DoesNotDiscardUncommittedRouteOrChangeRevision()
        {
            var session = NewBattle();
            SetPose(session, false, 0, 100, 180);
            Assert.That(session.TryAppendNavalMovementStep(new VoyagePoint(100, 0), out _), Is.True);
            int revision = session.Battle.TacticalRevision;
            Assert.That(session.PushNavalShip(true, 0, 1), Is.False);
            Assert.That(session.NavalMovementPlannedSteps, Is.EqualTo(1));
            Assert.That(session.Battle.TacticalRevision, Is.EqualTo(revision));
        }

        [Test]
        public void EnemyPreview_UsesUpcomingGuardResetWithoutMutatingLiveBattle()
        {
            var session = NewBattle();
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Fire, false, 3, -1, false, 0, 0), Is.True);
            session.Battle.EnemyBlock = 20;
            int hull = session.Battle.EnemyHull;
            CoreNavalMovementFieldPreview preview = session.GetEnemyNavalFieldPreview(100);
            Assert.That(preview.HullDamage, Is.Zero);
            Assert.That(session.Battle.EnemyBlock, Is.EqualTo(20));
            Assert.That(session.Battle.NavalFields.TriggerLedger, Is.Empty);
            session.Battle.EnemyBlock = 0;
            session.NavalFieldsBeginTurn(false);
            typeof(CoreVoyageSession).GetMethod("AdvanceTacticalEnemy", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { 100f, null });
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(hull - preview.HullDamage));
            Assert.That(session.Battle.EnemyPositionMeters.X, Is.EqualTo(preview.FinalPositionMeters.X));
            Assert.That(session.Battle.EnemyPositionMeters.Y, Is.EqualTo(preview.FinalPositionMeters.Y));
        }

        [Test]
        public void EnemyPreview_DiscardsFieldsDueAtPlayerEndWithoutChangingThemEarly()
        {
            var session = NewBattle();
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Fire, false, 3, -1, false, 0, 0), Is.True);
            session.Battle.NavalFields.CompletedPlayerTurns = 4;
            Assert.That(session.GetEnemyNavalFieldPreview(100).Damage, Is.Zero);
            Assert.That(session.Battle.NavalFields.Cells.Count, Is.EqualTo(9));
            session.NavalFieldsEndTurn(true);
            Assert.That(session.Battle.NavalFields.Cells, Is.Empty);
        }

        [Test]
        public void Snapshot_RejectsInconsistentInstanceLifetimeAndInvalidImmunity()
        {
            var session = NewBattle();
            Place(session, CoreNavalFieldKind.Fire, 1, 0);
            CoreNavalFieldState snapshot = session.CaptureNavalFields();
            snapshot.Cells[0].ExpiresAtOwnerTurn = 200;
            Assert.That(session.RestoreNavalFields(snapshot), Is.False);
            snapshot = session.CaptureNavalFields();
            snapshot.Player.FireImmunityCharges = -1;
            Assert.That(session.RestoreNavalFields(snapshot), Is.False);
            session.Battle.Stage = NavalBattleStage.Boarding;
            Assert.That(session.RestoreNavalFields(session.CaptureNavalFields()), Is.False);
        }
        [TestCase(true)]
        [TestCase(false)]
        public void EnemyStandingFireKeepsTheJustExecutedAttackOrGuardInItsActionLog(bool cannon)
        {
            var session = new CoreVoyageSession();
            session.SetSeed("STANDING-FIRE-LOG");
            session.StartVoyage();
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(session.EndBattleTurn(), Is.True);
            SetPose(session, true, 0, 0, 0);
            int enemyCell = cannon ? 2 : 6;
            SetPose(session, false, enemyCell * 100, 0, 180);
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Fire, false, enemyCell, 0, false, 0, 0), Is.True);
            session.Battle.PlayerBlock = 100;
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull;
            // Exercise both hull damage and a fully absorbed fire settlement.
            if (cannon)
            {
                session.Battle.NavalExpansion.RuneClamp = true;
                session.Battle.NavalExpansion.RuneClampReduction = 6;
            }
            int enemyHull = session.Battle.EnemyHull, playerHull = session.Hull;
            string intent = session.GetEnemyIntent();
            Assert.That(intent, Does.Contain(cannon ? "炮击伤害" : "格挡设为"));

            Assert.That(session.EndBattleTurn(), Is.True);

            Assert.That(session.Battle.EnemyPositionMeters, Is.EqualTo(new VoyagePoint(enemyCell * 100, 0)));
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(enemyHull), "New field burn cannot deal damage this turn.");
            Assert.That(session.Battle.EnemyBlock, Is.EqualTo(cannon ? 0 : 2));
            Assert.That(session.Hull, Is.EqualTo(playerHull));
            Assert.That(session.Battle.LastEnemyGuardAbsorbed,
                Is.EqualTo(cannon ? (CoreTrainingTargets.All[0].CannonDamage * 125 + 99) / 100 : 0));
            Assert.That(session.Battle.LastAction, Does.StartWith(session.Battle.EnemyName + "："));
            Assert.That(session.Battle.LastAction, Does.Contain(intent));
            Assert.That(session.Battle.LastAction, Does.Contain("敌舰停留火场，获得 4 层燃烧。"));
        }

        [Test]
        public void EnemyMovementFireWaitsUntilTurnEndAndRepeatedSettlementDoesNotDuplicateIt()
        {
            CoreVoyageSession session = NewBattle();
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Fire, false, 4, 0, false, 0, 0), Is.True);
            session.Battle.LastAction = "上一轮的动作";
            Assert.That(session.PushNavalShip(false, -1, 0), Is.True);
            Assert.That(session.Battle.LastAction, Is.EqualTo("上一轮的动作"));
            Assert.That(session.NavalFieldsEndTurn(false), Is.False);
            Assert.That(session.Battle.LastAction, Does.Contain("敌舰停留火场，获得 5 层燃烧。"));
            string action = session.Battle.LastAction;
            Assert.That(session.NavalFieldsEndTurn(false), Is.False);
            Assert.That(session.Battle.LastAction, Is.EqualTo(action));
        }
        private static CoreVoyageSession NewBattle()
        {
            var session = new CoreVoyageSession();
            session.SetSeed("NAVAL-FIELDS-TEST");
            session.StartVoyage();
            var encounter = new CoreEncounter("fields_test", "测试敌舰", "海战", "场地测试", true, false, 1, 70);
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { encounter, false });
            session.Battle.Stage = NavalBattleStage.Naval;
            session.Battle.PlayerBlock = session.Battle.EnemyBlock = 0;
            SetPose(session, true, 0, 0, 0);
            SetPose(session, false, 500, 0, 180);
            return session;
        }
        private static void Place(CoreVoyageSession session, CoreNavalFieldKind kind, int x, int y, int dx = 0, int dy = 0)
        {
            VoyagePoint original = session.Battle.PlayerPositionMeters;
            float heading = session.Battle.PlayerHeadingDegrees;
            if (kind == CoreNavalFieldKind.Fire) SetPose(session, true, (x + 1) * 100, (y + 1) * 100, heading);
            Assert.That(session.PlaceNavalField(kind, true, x, y, false, dx, dy), Is.True, session.CanPlaceNavalField(kind, true, x, y, false, dx, dy));
            if (kind == CoreNavalFieldKind.Fire) SetPose(session, true, original.X, original.Y, heading);
        }
        private static void Set(object target, string name, object value) => target.GetType().GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        private static void SetPose(CoreVoyageSession session, bool player, float x, float y, float heading) =>
            typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session.Battle, new object[] { player, new VoyagePoint(x, y), heading });
    }
}




