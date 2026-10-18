using System;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreNavalFieldsTests
    {
        [Test]
        public void Placement_ChecksEveryCellAndDoesNotTriggerUnderShips()
        {
            var session = NewBattle();
            int hull = session.Hull;
            Assert.That(session.CanPlaceNavalField(CoreNavalFieldKind.Fire, true, 3, 1, false, 0, 0), Is.Not.Empty);
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Fire, true, 0, 0, false, 0, 0), Is.True);
            Assert.That(session.Hull, Is.EqualTo(hull));
            Assert.That(session.Battle.NavalFields.TriggerLedger, Is.Empty);
            Assert.That(session.Battle.NavalFields.Cells.Count, Is.EqualTo(4));
        }

        [Test]
        public void Fire_ChargesDifferentCellsOnceAndStandingDoesNotChargeAgain()
        {
            var session = NewBattle();
            int hull = session.Hull;
            Place(session, CoreNavalFieldKind.Fire, 1, 0);
            Assert.That(session.PushNavalShip(true, 1, 0), Is.True);
            Assert.That(session.PushNavalShip(true, 1, 0), Is.True);
            Assert.That(session.PushNavalShip(true, -1, 0), Is.True);
            Assert.That(session.NavalFieldsEndTurn(true), Is.False);
            Assert.That(session.Hull, Is.EqualTo(hull - 4));
            Assert.That(session.Battle.NavalFields.TriggerLedger.Count, Is.EqualTo(2));
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
            Assert.That(session.Hull, Is.EqualTo(hull - 2));
            session.NavalFieldsBeginTurn(true);
            session.NavalFieldsEndTurn(true);
            Assert.That(session.Hull, Is.EqualTo(hull - 4));
        }

        [Test]
        public void Fire_ImmunityAndPaidGuardConsumeWithoutGeneratingTide()
        {
            var session = NewBattle();
            int hull = session.Hull;
            Place(session, CoreNavalFieldKind.Fire, 1, 0);
            session.GrantNavalFireImmunity(true);
            session.PushNavalShip(true, 1, 0);
            session.PushNavalShip(true, 1, 0);
            session.GrantNavalFireImmunity(true);
            Assert.That(session.Battle.NavalFields.Player.FireImmunityCharges, Is.Zero);
            session.Battle.PlayerBlock = 2;
            Set(session.Battle.Schools, "TidePaidBlock", 2);
            session.PushNavalShip(true, 0, 1);
            Assert.That(session.Hull, Is.EqualTo(hull));
            Assert.That(session.Battle.PlayerBlock, Is.Zero);
            Assert.That(session.Battle.Schools.TidePaidBlock, Is.Zero);
            Assert.That(session.Battle.Schools.TidePower, Is.Zero);
            Assert.That(session.Battle.NavalFields.TriggerLedger.Count, Is.EqualTo(3));
        }

        [Test]
        public void Fields_ExpireAtSecondSubsequentOwnerStartBeforeBenefits()
        {
            var session = NewBattle();
            Place(session, CoreNavalFieldKind.Blood, 0, 0);
            session.NavalFieldsBeginTurn(true);
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(6));
            Assert.That(session.Battle.NavalFields.Cells.Count, Is.EqualTo(4));
            session.Battle.PlayerBlock = 0;
            session.NavalFieldsBeginTurn(true);
            Assert.That(session.Battle.NavalFields.Cells, Is.Empty);
            Assert.That(session.Battle.PlayerBlock, Is.Zero);
        }

        [Test]
        public void BeneficialFields_OnlyTriggerOnOwnTurnAndShareKindQuota()
        {
            var session = NewBattle();
            Place(session, CoreNavalFieldKind.Arcane, 1, 0);
            session.NavalFieldsBeginTurn(false);
            session.PushNavalShip(true, 1, 0);
            Assert.That(session.Battle.Schools.TemporaryMana, Is.Zero);
            session.NavalFieldsBeginTurn(true);
            Assert.That(session.Battle.Schools.TemporaryMana, Is.EqualTo(1));
            Place(session, CoreNavalFieldKind.Arcane, 2, 0);
            session.PushNavalShip(true, 1, 0);
            Assert.That(session.Battle.Schools.TemporaryMana, Is.EqualTo(1));
            Assert.That(session.Battle.NavalFields.Player.ArcanePiercing, Is.EqualTo(4));
        }

        [Test]
        public void PartialCoverageKeepsInstanceIdentityAndExpiry()
        {
            var session = NewBattle();
            Place(session, CoreNavalFieldKind.Thunder, 1, 0);
            int instance = session.GetNavalFieldAt(1, 0).InstanceId;
            Place(session, CoreNavalFieldKind.Fire, 2, 0);
            Assert.That(session.Battle.NavalFields.Cells.FindAll(cell => cell.InstanceId == instance).Count, Is.EqualTo(1));
            Assert.That(session.GetNavalFieldAt(1, 0).ExpiresAtOwnerTurn, Is.EqualTo(3));
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
            Assert.That(session.GetNavalFieldAt(1, -1).ExpiresAtOwnerTurn, Is.EqualTo(3));
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
            Assert.That(session.Battle.PlayerPositionMeters.X, Is.EqualTo(200f));
            Assert.That(session.Battle.NavalArrivalActionPoints, Is.Zero);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(1));
            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.False);
            Assert.That(session.Battle.NavalFields.TriggerLedger.Count, Is.EqualTo(1));
        }

        [Test]
        public void WaterPush_TriggersDestinationDamageAndBlockedAttemptUsesQuota()
        {
            var session = NewBattle();
            int hull = session.Hull;
            Place(session, CoreNavalFieldKind.Tide, 1, 0, 1, 0);
            Place(session, CoreNavalFieldKind.Fire, 2, 0);
            session.PushNavalShip(true, 1, 0);
            Assert.That(session.Battle.PlayerPositionMeters.X, Is.EqualTo(200f));
            Assert.That(session.Hull, Is.EqualTo(hull - 2));
            var blocked = NewBattle();
            SetPose(blocked, false, 200, 0, 180);
            Place(blocked, CoreNavalFieldKind.Tide, 1, 0, 1, 0);
            blocked.PushNavalShip(true, 1, 0);
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
        public void Thunder_StopsAtFirstCellAndLeavesOnlyArrivalCredit()
        {
            var session = NewBattle();
            Place(session, CoreNavalFieldKind.Thunder, 1, 0);
            Set(session.Battle, "NavalActionPoints", 3);
            Assert.That(session.TryAppendNavalMovementSegment(new VoyagePoint(300, 0), out string reason), Is.True, reason);
            CoreNavalMovementFieldPreview preview = session.NavalMovementFieldPreview;
            Assert.That(preview.StopsAtThunder, Is.True);
            Assert.That(preview.ActiveSteps, Is.EqualTo(1));
            Assert.That(preview.Damage, Is.EqualTo(3));
            Assert.That(session.CommitNavalMovement(out reason), Is.True, reason);
            Assert.That(session.Battle.PlayerPositionMeters.X, Is.EqualTo(100f));
            Assert.That(session.Battle.NavalActionPoints, Is.Zero);
            Assert.That(session.Battle.NavalArrivalActionPoints, Is.EqualTo(2));
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
            Place(session, CoreNavalFieldKind.Fire, 1, 0);
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
        public void LethalFieldAfterCurrentPush_PreservesActualArrivalAndForcedStep()
        {
            var session = NewBattle();
            Set(session, "Hull", 1);
            Place(session, CoreNavalFieldKind.Tide, 1, 0, 1, 0);
            Place(session, CoreNavalFieldKind.Fire, 2, 0);
            Set(session.Battle, "NavalActionPoints", 1);
            Assert.That(session.TryAppendNavalMovementStep(new VoyagePoint(100, 0), out string reason), Is.True, reason);
            CoreBattleState battle = session.Battle;
            int revision = battle.TacticalRevision;
            Assert.That(session.CommitNavalMovement(out reason), Is.True, reason);
            Assert.That(session.Battle, Is.Null);
            Assert.That(session.Phase, Is.Not.EqualTo(CoreDemoPhase.Battle));
            Assert.That(battle.PlayerPositionMeters.X, Is.EqualTo(200f), "The current pushes once from the real arrival into lethal fire.");
            Assert.That(battle.TacticalRevision, Is.EqualTo(revision + 1));
            CoreNavalTacticalMovement movement = battle.LastTacticalMovement;
            Assert.That(movement, Is.Not.Null);
            Assert.That(movement.Revision, Is.EqualTo(battle.TacticalRevision));
            Assert.That(movement.Frames[0].PlayerPositionMeters.X, Is.Zero);
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
        public void CurseDomainBonus_IsCappedAndSharedAcrossInstances()
        {
            var session = NewBattle();
            SetPose(session, false, 200, 0, 180);
            Place(session, CoreNavalFieldKind.Curse, 2, 0);
            Assert.That(session.NavalFieldCurseBonus(false, 0), Is.Zero);
            Assert.That(session.NavalFieldCurseBonus(false, 6), Is.EqualTo(8));
            Place(session, CoreNavalFieldKind.Curse, 2, 0);
            Assert.That(session.NavalFieldCurseBonus(false, 6), Is.Zero);
        }

        [Test]
        public void EnemyPath_WeighsDetourAgainstDamageButCanStillCrossFire()
        {
            var session = NewBattle();
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Fire, false, 3, -1, false, 0, 0), Is.True);
            MethodInfo plan = typeof(CoreVoyageSession).GetMethod("BuildNavalFieldEnemyPath", BindingFlags.Instance | BindingFlags.NonPublic);
            var detour = (System.Collections.Generic.List<CoreNavalMovementNode>)plan.Invoke(session, new object[] { 300f, null });
            Assert.That(detour.Count, Is.GreaterThan(1));
            Assert.That(detour[1].PositionMeters.Y, Is.EqualTo(100f), "Three steps can route around the fire while reaching cannon range.");
            var urgent = (System.Collections.Generic.List<CoreNavalMovementNode>)plan.Invoke(session, new object[] { 100f, null });
            Assert.That(urgent.Count, Is.EqualTo(2));
            Assert.That(urgent[1].PositionMeters.X, Is.EqualTo(400f), "One affordable fire hit is worthwhile to enter attack range.");
            Assert.That(session.GetEnemyNavalFieldIntent(100f), Does.Contain("火场"));
        }
        [Test]
        public void RealTurnFlow_GrantsBloodGuardAfterDefaultResetAndExpiresBeforeNextBenefit()
        {
            var session = NewBattle();
            Place(session, CoreNavalFieldKind.Blood, 0, 0);
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(6));
            Assert.That(session.Battle.NavalFields.Player.BloodAttackBonus, Is.EqualTo(4));
            Assert.That(session.Battle.NavalFields.Cells.Count, Is.EqualTo(4));
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.NavalFields.Cells, Is.Empty);
            Assert.That(session.Battle.PlayerBlock, Is.Zero);
            Assert.That(session.Battle.NavalFields.Player.BloodAttackBonus, Is.Zero);
        }
        [Test]
        public void CommittedThunderRoute_DoesNotPreviewPreviouslyCancelledRemainder()
        {
            var session = NewBattle();
            Place(session, CoreNavalFieldKind.Thunder, 1, 0);
            Set(session.Battle, "NavalActionPoints", 3);
            session.TryAppendNavalMovementSegment(new VoyagePoint(300, 0), out _);
            session.CommitNavalMovement(out _);
            Assert.That(session.NavalMovementFieldPreview.ActiveSteps, Is.Zero);
            Assert.That(session.NavalMovementFieldPreview.FinalPositionMeters.X, Is.EqualTo(100));
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
            Assert.That(preview.HullDamage, Is.EqualTo(2));
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
        public void EnemyPreview_DiscardsFieldsDueAtUpcomingStartWithoutChangingThemEarly()
        {
            var session = NewBattle();
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Fire, false, 3, -1, false, 0, 0), Is.True);
            session.Battle.NavalFields.EnemyTurn = 1;
            Assert.That(session.GetEnemyNavalFieldPreview(100).Damage, Is.Zero);
            Assert.That(session.Battle.NavalFields.Cells.Count, Is.EqualTo(4));
            session.NavalFieldsBeginTurn(false);
            Assert.That(session.Battle.NavalFields.Cells, Is.Empty);
        }

        [Test]
        public void Snapshot_RejectsExtendedLifetimeAndReplenishedUsedImmunity()
        {
            var session = NewBattle();
            Place(session, CoreNavalFieldKind.Fire, 1, 0);
            CoreNavalFieldState snapshot = session.CaptureNavalFields();
            foreach (CoreNavalFieldCell cell in snapshot.Cells) cell.ExpiresAtOwnerTurn = 200;
            Assert.That(session.RestoreNavalFields(snapshot), Is.False);
            snapshot = session.CaptureNavalFields();
            snapshot.Player.FireImmunityCharges = 2;
            snapshot.Player.FireImmunityUsed = 2;
            Assert.That(session.RestoreNavalFields(snapshot), Is.False);
            session.Battle.Stage = NavalBattleStage.Boarding;
            Assert.That(session.RestoreNavalFields(session.CaptureNavalFields()), Is.False);
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
        private static void Place(CoreVoyageSession session, CoreNavalFieldKind kind, int x, int y, int dx = 0, int dy = 0) =>
            Assert.That(session.PlaceNavalField(kind, true, x, y, false, dx, dy), Is.True, session.CanPlaceNavalField(kind, true, x, y, false, dx, dy));
        private static void Set(object target, string name, object value) => target.GetType().GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        private static void SetPose(CoreVoyageSession session, bool player, float x, float y, float heading) =>
            typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session.Battle, new object[] { player, new VoyagePoint(x, y), heading });
    }
}




