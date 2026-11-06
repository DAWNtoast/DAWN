using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreTideFieldRevision20261024Tests
    {
        [TestCase(CoreNavalFieldKind.Fire, 3)]
        [TestCase(CoreNavalFieldKind.Arcane, 2)]
        [TestCase(CoreNavalFieldKind.Curse, 2)]
        [TestCase(CoreNavalFieldKind.Tide, 4)]
        [TestCase(CoreNavalFieldKind.Thunder, 7)]
        [TestCase(CoreNavalFieldKind.Blood, 2)]
        public void EveryFieldUsesLowerLeftAnchorAndOnlyAnchorMustBeInRange(CoreNavalFieldKind kind, int size)
        {
            CoreVoyageSession s = NewBattle();
            Assert.That(s.CanPlaceNavalFieldAnchor(kind, true, 4, 0), Is.Empty);
            Assert.That(s.PlaceNavalField(kind, true, 4, 0, true, 1, 0), Is.True);
            Assert.That(s.Battle.NavalFields.Cells.Count, Is.EqualTo(size * size));
            for (int x = 4; x < 4 + size; x++)
                for (int y = 0; y < size; y++) Assert.That(s.GetNavalFieldAt(x, y), Is.Not.Null);
            Assert.That(s.GetNavalFieldAt(3, 0), Is.Null);
            Assert.That(s.GetNavalFieldAt(4, -1), Is.Null);
            Assert.That(s.CanPlaceNavalFieldAnchor(kind, true, 4, 1), Is.Not.Empty);
            Assert.That(s.CanPlaceNavalFieldAnchor(kind, false, 6, 0), Is.Empty);
        }

        [Test]
        public void WaterAnchorCanBePreviewedBeforeDirectionIsSelectedAndLastsFiveTurns()
        {
            CoreVoyageSession s = NewBattle();
            Assert.That(s.CanPlaceNavalFieldAnchor(CoreNavalFieldKind.Tide, true, 0, 0), Is.Empty);
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Tide, true, 0, 0, false, 0, 0), Is.False);
            Assert.That(s.Battle.NavalFields.Cells, Is.Empty);
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Tide, true, 0, 0, false, 0, 1), Is.True);
            Assert.That(s.Battle.NavalFields.Cells.Count, Is.EqualTo(16));
            Assert.That(s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(0, 0)), Is.EqualTo(5));
        }

        [Test]
        public void AreaCannotCrossTheLegalSeaBoundaryEvenWhenAnchorIsValid()
        {
            CoreVoyageSession s = NewBattle();
            Pose(s, true, 9999900, 0, 0);
            Assert.That(s.CanPlaceNavalFieldAnchor(CoreNavalFieldKind.Tide, true, 99999, 0), Is.Not.Empty);
            Assert.That(s.Battle.NavalFields.Cells, Is.Empty);
        }

        [TestCase(true, true)] [TestCase(true, false)]
        [TestCase(false, true)] [TestCase(false, false)]
        public void ReverseCurrentBlocksForcedMovementForEitherShipAndEitherOwner(bool player, bool owner)
        {
            CoreVoyageSession s = NewBattle();
            int x = player ? 0 : 10;
            Field(s, CoreNavalFieldKind.Tide, x, 0, owner, 1, 0);
            Assert.That(s.PushNavalShip(player, -1, 0), Is.False);
            Assert.That(s.PushNavalShip(player, 0, 1), Is.True, "Perpendicular motion is allowed.");
        }

        [Test]
        public void CurrentBlocksPlannedEdgesReachAndCommitAfterNewFieldAppears()
        {
            CoreVoyageSession s = NewBattle();
            Set(s.Battle, "NavalActionPoints", 4);
            Assert.That(s.TryAppendNavalMovementSegment(new VoyagePoint(200, 0), out _), Is.True);
            Field(s, CoreNavalFieldKind.Tide, 100 / 100, 0, false, -1, 0);
            Assert.That(s.CommitNavalMovement(out string reason), Is.False);
            Assert.That(reason, Does.Contain("水流"));
            Assert.That(s.Battle.PlayerPositionMeters, Is.EqualTo(new VoyagePoint(0, 0)));
            s.ClearNavalMovementPath();
            Assert.That(s.TryAppendNavalMovementStep(new VoyagePoint(100, 0), out reason), Is.True, reason);
            Assert.That(s.TryAppendNavalMovementStep(new VoyagePoint(200, 0), out reason), Is.False);
            CollectionAssert.DoesNotContain(s.GetNavalLegalNextSteps(), new VoyagePoint(200, 0));
        }

        [Test]
        public void CurrentDoesNotPushOnMoveButPushesOnlyOnceAtTurnEndWithoutChaining()
        {
            CoreVoyageSession s = NewBattle();
            Field(s, CoreNavalFieldKind.Tide, 1, 0, true, 1, 0);
            Field(s, CoreNavalFieldKind.Tide, 2, 0, false, 0, 1);
            Assert.That(s.TryAppendNavalMovementStep(new VoyagePoint(100, 0), out _), Is.True);
            Assert.That(s.NavalMovementFieldPreview.FinalPositionMeters, Is.EqualTo(new VoyagePoint(100, 0)));
            Assert.That(s.CommitNavalMovement(out _), Is.True);
            Assert.That(s.ConfirmNavalArrivalHeading(0, out _), Is.True);
            Assert.That(s.Battle.PlayerPositionMeters, Is.EqualTo(new VoyagePoint(100, 0)));
            Assert.That(s.Battle.NavalFields.TriggerLedger, Is.Empty);
            Assert.That(s.NavalFieldsEndTurn(true), Is.False);
            Assert.That(s.Battle.PlayerPositionMeters, Is.EqualTo(new VoyagePoint(200, 0)));
            Assert.That(s.NavalFieldsEndTurn(true), Is.False);
            Assert.That(s.Battle.PlayerPositionMeters, Is.EqualTo(new VoyagePoint(200, 0)));
            Assert.That(s.Battle.NavalArrivalActionPoints, Is.Zero);
        }

        [Test]
        public void LastDurationCurrentPushesBeforeItExpires()
        {
            CoreVoyageSession s = NewBattle();
            CoreNavalFieldCell field = Field(s, CoreNavalFieldKind.Tide, 0, 0, false, 0, 1);
            field.ExpiresAtOwnerTurn = 1;
            Assert.That(s.NavalFieldsEndTurn(true), Is.False);
            Assert.That(s.Battle.PlayerPositionMeters, Is.EqualTo(new VoyagePoint(0, 100)));
            Assert.That(s.Battle.NavalFields.Cells, Is.Empty);
        }

        [Test]
        public void CurrentImmunityCoversReverseMovementAndPushThroughNextPlayerTurnEnd()
        {
            CoreVoyageSession s = NewBattle();
            Field(s, CoreNavalFieldKind.Tide, 0, 0, false, 1, 0);
            s.GrantNavalTideImmunityThroughNextPlayerTurnEnd(true);
            Assert.That(s.PushNavalShip(true, -1, 0), Is.True);
            Pose(s, true, 0, 0, 180);
            s.NavalFieldsEndTurn(true);
            Assert.That(s.Battle.PlayerPositionMeters, Is.EqualTo(new VoyagePoint(0, 0)));
            s.NavalFieldsBeginTurn(false);
            s.NavalFieldsEndTurn(false);
            s.NavalFieldsBeginTurn(true);
            Assert.That(s.Battle.NavalFields.Player.TideImmunityExpiresAfterPlayerTurnEnd, Is.GreaterThan(0));
            s.NavalFieldsEndTurn(true);
            Assert.That(s.Battle.PlayerPositionMeters, Is.EqualTo(new VoyagePoint(0, 0)));
            Assert.That(s.Battle.NavalFields.Player.TideImmunityExpiresAfterPlayerTurnEnd, Is.Zero);
            Assert.That(s.PushNavalShip(true, -1, 0), Is.False);
        }

        [Test]
        public void MovementCapIsAbsoluteAndConsumedOnlyByAnActualActiveMove()
        {
            CoreVoyageSession s = NewBattle();
            Set(s.Battle, "NavalActionPoints", 5);
            s.ApplyNavalMovementCap(true, 1);
            Assert.That(s.CommitNavalMovement(out _), Is.False);
            Assert.That(s.Battle.NavalFields.Player.NextMovementMaxCells, Is.EqualTo(1));
            Assert.That(s.PushNavalShip(true, 0, 1), Is.True);
            Assert.That(s.Battle.NavalFields.Player.NextMovementMaxCells, Is.EqualTo(1));
            Assert.That(s.TryAppendNavalMovementSegment(new VoyagePoint(200, 100), out _), Is.False);
            Assert.That(s.TryAppendNavalMovementStep(new VoyagePoint(100, 100), out _), Is.True);
            Assert.That(s.CommitNavalMovement(out _), Is.True);
            Assert.That(s.Battle.NavalFields.Player.NextMovementMaxCells, Is.Zero);
        }

        [Test]
        public void RecedingWaveQuotesAndExecutesOnlyOneEnemyMovementCellAfterPushingOutOfRange()
        {
            CoreVoyageSession s = NewBattle(unlockTide: true);
            Pose(s, false, 400, 0, 180);
            Set(s.Battle.Schools, "TidePower", 3);
            s.Battle.Energy = 3;
            int card = -1;
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
                if (CoreVoyageSession.CardLibrary[index].Id == "card_school_tide_n04") { card = index; break; }
            Assert.That(card, Is.GreaterThanOrEqualTo(0));
            s.Battle.Hand.Clear(); s.Battle.Hand.Add(card);
            Assert.That(s.Battle.Profile?.TrainingTarget, Is.Null);
            Assert.That(s.PlayCard(0), Is.True, s.CardUnavailableReason(0));
            Assert.That(s.Battle.EnemyPositionMeters, Is.EqualTo(new VoyagePoint(500, 0)));
            Assert.That(s.Battle.NavalFields.Enemy.NextMovementMaxCells, Is.EqualTo(1));
            string intent = s.GetEnemyIntent();
            Assert.That(intent, Does.Contain("最多 100 米").And.Not.Contain("200 米"));
            Assert.That(s.GetEnemyNavalFieldPreview(200).ActiveSteps, Is.EqualTo(1));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyPositionMeters, Is.EqualTo(new VoyagePoint(400, 0)));
            Assert.That(s.Battle.NavalFields.Enemy.NextMovementMaxCells, Is.Zero);
        }

        [Test]
        public void EnemyPathCannotSailUpstreamAndPredictsTurnEndPushSeparately()
        {
            CoreVoyageSession s = NewBattle();
            Field(s, CoreNavalFieldKind.Tide, 10, 0, true, 1, 0);
            var path = (List<CoreNavalMovementNode>)typeof(CoreVoyageSession).GetMethod("BuildNavalFieldEnemyPath", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(s, new object[] { 100f, null });
            if (path.Count > 1) Assert.That(path[1].PositionMeters.X, Is.GreaterThanOrEqualTo(1000f));
            CoreNavalMovementFieldPreview preview = s.GetEnemyNavalFieldPreview(0);
            Assert.That(preview.FinalPositionMeters, Is.EqualTo(new VoyagePoint(1000, 0)), "The enemy acts before its current pushes it.");
            Assert.That(preview.EndTurnPositionMeters, Is.EqualTo(new VoyagePoint(1100, 0)));
            Assert.That(preview.Summary, Does.Contain("回合末顺流"));
            Assert.That(s.Battle.EnemyPositionMeters, Is.EqualTo(new VoyagePoint(1000, 0)));
            s.NavalFieldsBeginTurn(false);
            s.NavalFieldsEndTurn(false);
            Assert.That(s.Battle.EnemyPositionMeters, Is.EqualTo(preview.EndTurnPositionMeters));
        }

        [TestCase(400, -1, false)]
        [TestCase(500, 1, true)]
        public void EnemyIntentUsesPlayersPendingCurrentPositionWithoutMovingAnything(int enemyX, int direction, bool willShoot)
        {
            var s = new CoreVoyageSession();
            s.SetSeed("PLAYER-CURRENT-INTENT");
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            Pose(s, true, 0, 0, 0); Pose(s, false, enemyX, 0, 180);
            Field(s, CoreNavalFieldKind.Tide, 0, 0, true, direction, 0);
            MethodInfo intent = typeof(CoreVoyageSession).GetMethod("GetEnemyBaseIntent", BindingFlags.Instance | BindingFlags.NonPublic);
            string before = (string)intent.Invoke(s, null);
            Assert.That(before.Contains("炮击伤害"), Is.EqualTo(willShoot));
            Assert.That(s.Battle.PlayerPositionMeters, Is.EqualTo(new VoyagePoint(0, 0)));
            Assert.That(s.Battle.NavalFields.TriggerLedger, Is.Empty);
            Assert.That(s.NavalFieldsEndTurn(true), Is.False);
            s.Battle.EnemyBlock = 0;
            s.NavalFieldsBeginTurn(false);
            Assert.That((string)intent.Invoke(s, null), Is.EqualTo(before));
        }

        [Test]
        public void EnemyRoutePreviewTargetsPlayersPendingCurrentPosition()
        {
            CoreVoyageSession s = NewBattle();
            Pose(s, false, 500, 0, 180);
            Field(s, CoreNavalFieldKind.Tide, 0, 0, true, 0, 1);
            CoreNavalMovementFieldPreview before = s.GetEnemyNavalFieldPreview(200);
            Assert.That(s.NavalFieldsEndTurn(true), Is.False);
            s.NavalFieldsBeginTurn(false);
            CoreNavalMovementFieldPreview after = s.GetEnemyNavalFieldPreview(200);
            Assert.That(after.FinalPositionMeters, Is.EqualTo(before.FinalPositionMeters));
            Assert.That(after.ActiveSteps, Is.EqualTo(before.ActiveSteps));
        }

        [Test]
        public void CurrentAndMovementCapRoundTripAndRejectNegativeValues()
        {
            CoreVoyageSession s = NewBattle();
            s.ApplyNavalMovementCap(false);
            s.GrantNavalTideImmunityThroughNextPlayerTurnEnd(true);
            CoreNavalFieldState saved = s.CaptureNavalFields();
            Assert.That(s.RestoreNavalFields(saved), Is.True);
            Assert.That(s.Battle.NavalFields.Enemy.NextMovementMaxCells, Is.EqualTo(1));
            Assert.That(s.Battle.NavalFields.Player.TideImmunityExpiresAfterPlayerTurnEnd, Is.EqualTo(2));
            saved.Enemy.NextMovementMaxCells = -1;
            Assert.That(s.RestoreNavalFields(saved), Is.False);
        }

        [Test]
        public void ExpandedCurrentInheritsLongestDurationAndItsDirectionWithoutGrowingTwice()
        {
            CoreVoyageSession s = NewBattle();
            CoreNavalFieldCell shortSource = Field(s, CoreNavalFieldKind.Tide, -1, 0, true, 0, 1);
            CoreNavalFieldCell longSource = Field(s, CoreNavalFieldKind.Tide, 1, 0, true, 0, -1);
            shortSource.ExpiresAtOwnerTurn = 2;
            longSource.ExpiresAtOwnerTurn = 5;
            Expand(s);
            CoreNavalFieldCell shared = s.GetNavalFieldAt(0, 0);
            Assert.That(shared.ExpiresAtOwnerTurn, Is.EqualTo(5));
            Assert.That(shared.DirectionY, Is.EqualTo(-1));
            Assert.That(shared.InstanceId, Is.EqualTo(longSource.InstanceId));
            Assert.That(s.GetNavalFieldAt(3, 0), Is.Null);
        }

        [Test]
        public void EquallyLongCurrentSourcesChooseRandomSourceAndRepeatWithTheSameSeed()
        {
            var directions = new HashSet<int>();
            for (int seed = 0; seed < 32; seed++)
            {
                CoreVoyageSession s = NewBattle("CURRENT-TIE-" + seed);
                Field(s, CoreNavalFieldKind.Tide, -1, 0, true, 0, 1);
                Field(s, CoreNavalFieldKind.Tide, 1, 0, true, 0, -1);
                Expand(s);
                int direction = s.GetNavalFieldAt(0, 0).DirectionY;
                directions.Add(direction);
                CoreVoyageSession same = NewBattle("CURRENT-TIE-" + seed);
                Field(same, CoreNavalFieldKind.Tide, -1, 0, true, 0, 1);
                Field(same, CoreNavalFieldKind.Tide, 1, 0, true, 0, -1);
                Expand(same);
                Assert.That(same.GetNavalFieldAt(0, 0).DirectionY, Is.EqualTo(direction));
            }
            CollectionAssert.AreEquivalent(new[] { -1, 1 }, directions);
        }

        private static void Expand(CoreVoyageSession s) => typeof(CoreVoyageSession)
            .GetMethod("ExpandAllPlayerFields", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(s, new object[] { CoreNavalFieldKind.Tide });

        private static CoreVoyageSession NewBattle(string seed = "TIDE-FIELD-REVISION", bool unlockTide = false)
        {
            var s = new CoreVoyageSession();
            if (unlockTide)
            {
                s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
                Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            }
            s.SetSeed(seed); s.StartVoyage();
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(s, new object[] { new CoreEncounter("tide_fields", "测试敌舰", "海战", "水流测试", true, false, 1, 70), false });
            s.Battle.Stage = NavalBattleStage.Naval;
            Pose(s, true, 0, 0, 0); Pose(s, false, 1000, 0, 180);
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = 0;
            return s;
        }
        private static CoreNavalFieldCell Field(CoreVoyageSession s, CoreNavalFieldKind kind, int x, int y, bool owner, int dx, int dy)
        {
            var cell = new CoreNavalFieldCell { X = x, Y = y, Kind = kind, PlayerOwned = owner, DirectionX = dx, DirectionY = dy,
                InstanceId = s.Battle.NavalFields.NextInstanceId++, ExpiresAtOwnerTurn = 5 };
            s.Battle.NavalFields.Cells.Add(cell); return cell;
        }
        private static void Set(object target, string name, object value) => target.GetType().GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        private static void Pose(CoreVoyageSession s, bool player, float x, float y, float heading) =>
            typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(s.Battle, new object[] { player, new VoyagePoint(x, y), heading });
    }
}
