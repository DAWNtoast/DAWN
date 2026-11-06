using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreThunderFieldRevision20261024Tests
    {
        [Test]
        public void ThunderUsesSevenBySevenSouthwestPlacementAndFiveOpposingTurns()
        {
            var s = Start();
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Thunder, true, 3, 1, false, 0, 0), Is.True);
            Assert.That(s.Battle.NavalFields.Cells.Count, Is.EqualTo(49));
            Assert.That(s.GetNavalFieldAt(3, 1), Is.Not.Null);
            Assert.That(s.GetNavalFieldAt(9, 7), Is.Not.Null);
            Assert.That(s.GetNavalFieldAt(2, 1), Is.Null);
            Assert.That(s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(3, 1)), Is.EqualTo(5));
            Assert.That(s.Battle.Schools.ThunderChain, Is.Zero, "Placement itself is not movement.");
            s.NavalFieldsEndTurn(true);
            Assert.That(s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(3, 1)), Is.EqualTo(5));
            s.NavalFieldsEndTurn(false);
            Assert.That(s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(3, 1)), Is.EqualTo(4));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ForcedMovementCountsOnlyLandingAndCanRepeatTheSameCell(bool player)
        {
            var s = Start();
            Pose(s, player, 0, 0);
            Pose(s, !player, 10, 0);
            AddCell(s, CoreNavalFieldKind.Thunder, 1, 0, 3);
            Assert.That(s.PushNavalShip(player, 1, 0), Is.True);
            Assert.That(Chain(s, player), Is.EqualTo(3));
            Assert.That(s.PushNavalShip(player, 1, 0), Is.True);
            Assert.That(Chain(s, player), Is.EqualTo(3), "Leaving a thunder field does not award chain.");
            Assert.That(s.PushNavalShip(player, -1, 0), Is.True);
            Assert.That(Chain(s, player), Is.EqualTo(6));
            Assert.That(s.Battle.NavalFields.TriggerLedger, Is.Empty);
            Assert.That(s.Battle.NavalFields.PresentationEventCount, Is.EqualTo(2));
        }

        [Test]
        public void CurrentForcedLandingAddsRemainingThunderDuration()
        {
            var s = Start();
            AddCell(s, CoreNavalFieldKind.Tide, 0, 0, 5, 1, 0);
            AddCell(s, CoreNavalFieldKind.Thunder, 1, 0, 4);
            s.NavalFieldsEndTurn(true);
            Assert.That(s.Battle.PlayerPositionMeters, Is.EqualTo(new VoyagePoint(100, 0)));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(4));
            Assert.That(s.Battle.NavalFields.TriggerLedger, Does.Not.Contain("P:K:4"));
        }

        [Test]
        public void RepeatedOverloadChargesOneThousandOncePerLandingAndKeepsChain()
        {
            var s = Start();
            s.Battle.Schools.ThunderChain = 45;
            s.Battle.PlayerBlock = 1000;
            SetHull(s, 2500);
            for (int x = 1; x <= 3; x++) AddCell(s, CoreNavalFieldKind.Thunder, x, 0, 5);
            Assert.That(s.PushNavalShip(true, 1, 0), Is.True);
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(50));
            Assert.That(s.Battle.PlayerBlock, Is.Zero); Assert.That(s.Hull, Is.EqualTo(2500));
            Assert.That(s.PushNavalShip(true, 1, 0), Is.True);
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(55)); Assert.That(s.Hull, Is.EqualTo(1500));
            Assert.That(s.PushNavalShip(true, 1, 0), Is.True);
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(60)); Assert.That(s.Hull, Is.EqualTo(500));
        }

        [Test]
        public void ThunderImmunityPreventsOverloadDamageButStillAddsEveryLanding()
        {
            var s = Start();
            s.Battle.Schools.ThunderChain = 45;
            SetHull(s, 2500);
            AddCell(s, CoreNavalFieldKind.Thunder, 1, 0, 5);
            AddCell(s, CoreNavalFieldKind.Thunder, 2, 0, 5);
            s.Battle.NavalFields.Player.FieldImmunityMask = 1 << (int)CoreNavalFieldKind.Thunder;
            s.Battle.NavalFields.Player.FieldImmunityExpires = s.Battle.NavalFields.PlayerTurn + 1;
            s.Battle.NavalActionPoints = 2;
            Assert.That(s.TryAppendNavalMovementSegment(new VoyagePoint(200, 0), out string reason), Is.True, reason);
            var preview = s.NavalMovementFieldPreview;
            Assert.That(preview.FinalThunderChain, Is.EqualTo(55));
            Assert.That(preview.Damage, Is.Zero); Assert.That(preview.Summary, Does.Contain("免疫过载伤害"));
            Assert.That(s.CommitNavalMovement(out reason), Is.True, reason);
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(55));
            Assert.That(s.Hull, Is.EqualTo(2500));
        }

        [Test]
        public void MovementPreviewMatchesRepeatedOverloadAndNeverMutatesLiveResources()
        {
            var s = Start();
            SetHull(s, 5000);
            s.Battle.Schools.ThunderChain = 45;
            s.Battle.PlayerBlock = 1001;
            for (int x = 1; x <= 3; x++) AddCell(s, CoreNavalFieldKind.Thunder, x, 0, 5);
            s.Battle.NavalActionPoints = 3;
            Assert.That(s.TryAppendNavalMovementSegment(new VoyagePoint(300, 0), out string reason), Is.True, reason);
            var preview = s.NavalMovementFieldPreview;
            Assert.That(preview.Damage, Is.EqualTo(3000)); Assert.That(preview.HullDamage, Is.EqualTo(1999));
            Assert.That(preview.FinalThunderChain, Is.EqualTo(60)); Assert.That(preview.FinalBlock, Is.Zero);
            Assert.That(preview.ActiveSteps, Is.EqualTo(3)); Assert.That(preview.StopsAtThunder, Is.False);
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(45)); Assert.That(s.Battle.PlayerBlock, Is.EqualTo(1001));
            Assert.That(s.Hull, Is.EqualTo(5000)); Assert.That(s.Battle.NavalFields.PresentationEventCount, Is.Zero);
            Assert.That(s.CommitNavalMovement(out reason), Is.True, reason);
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(preview.FinalThunderChain));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(preview.FinalBlock));
            Assert.That(s.Hull, Is.EqualTo(5000 - preview.HullDamage));
            Assert.That(s.Battle.PlayerPositionMeters, Is.EqualTo(preview.FinalPositionMeters));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TurnStartPreservesChainInsideThunderAndClearsItOutside(bool player)
        {
            var s = Start();
            Pose(s, player, 0, 0); Pose(s, !player, 10, 0);
            AddCell(s, CoreNavalFieldKind.Thunder, 0, 0, 5);
            if (player) s.Battle.Schools.ThunderChain = 27;
            else s.Battle.NavalFields.Enemy.ThunderChain = 27;
            s.NavalFieldsBeginTurn(player);
            Assert.That(Chain(s, player), Is.EqualTo(27));
            Assert.That(s.PushNavalShip(player, 1, 0), Is.True);
            s.NavalFieldsBeginTurn(player);
            Assert.That(Chain(s, player), Is.Zero);
        }

        [Test]
        public void EnemyMovementPreviewMatchesForcedLandingsWithoutChangingItsChain()
        {
            var s = Start();
            Pose(s, false, 0, 0); Pose(s, true, 10, 0);
            s.Battle.NavalFields.PlayerActing = false;
            s.Battle.NavalFields.Enemy.ThunderChain = 45;
            s.Battle.EnemyHull = 5000; s.Battle.EnemyBlock = 1001;
            var path = new List<CoreNavalMovementNode> { new CoreNavalMovementNode(new VoyagePoint(0, 0), 0) };
            for (int x = 1; x <= 3; x++)
            {
                AddCell(s, CoreNavalFieldKind.Thunder, x, 0, 5);
                path.Add(new CoreNavalMovementNode(new VoyagePoint(x * 100, 0), 0));
            }
            var preview = (CoreNavalMovementFieldPreview)typeof(CoreVoyageSession)
                .GetMethod("PreviewNavalFields", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(s, new object[] { path, false, null });
            Assert.That(preview.FinalThunderChain, Is.EqualTo(60));
            Assert.That(preview.HullDamage, Is.EqualTo(1999));
            Assert.That(s.Battle.NavalFields.Enemy.ThunderChain, Is.EqualTo(45));
            for (int x = 1; x <= 3; x++) Assert.That(s.PushNavalShip(false, 1, 0), Is.True);
            Assert.That(s.Battle.NavalFields.Enemy.ThunderChain, Is.EqualTo(preview.FinalThunderChain));
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(5000 - preview.HullDamage));
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(preview.FinalBlock));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void LethalForcedThunderEntryRecordsOnlyTheCommittedStep(bool player)
        {
            var s = Start();
            Pose(s, player, 0, 0); Pose(s, !player, 10, 0);
            if (player) { s.Battle.Schools.ThunderChain = 45; SetHull(s, 1); }
            else { s.Battle.NavalFields.Enemy.ThunderChain = 45; s.Battle.EnemyHull = 1; }
            AddCell(s, CoreNavalFieldKind.Thunder, 1, 0, 5);
            CoreBattleState battle = s.Battle;
            int revision = battle.TacticalRevision;
            Assert.That(s.PushNavalShip(player, 1, 0), Is.True);
            Assert.That(battle.TacticalRevision, Is.EqualTo(revision + 1));
            Assert.That(battle.LastTacticalMovement, Is.Not.Null);
            var frames = battle.LastTacticalMovement.Frames;
            Assert.That(player ? frames[frames.Count - 1].PlayerPositionMeters : frames[frames.Count - 1].EnemyPositionMeters,
                Is.EqualTo(new VoyagePoint(100, 0)));
            Assert.That(s.Phase, Is.Not.EqualTo(CoreDemoPhase.Battle));
        }

        private static int Chain(CoreVoyageSession s, bool player) =>
            player ? s.Battle.Schools.ThunderChain : s.Battle.NavalFields.Enemy.ThunderChain;
        private static CoreVoyageSession Start()
        {
            var s = new CoreVoyageSession();
            s.SetSeed("THUNDER-FIELDS-20261024"); s.StartVoyage();
            var encounter = new CoreEncounter("thunder_field_revision", "测试敌舰", "海战", "场地验证", true, false, 1, 70);
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(s, new object[] { encounter, false });
            s.Battle.Stage = NavalBattleStage.Naval;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = 0;
            Pose(s, true, 0, 0); Pose(s, false, 10, 0);
            return s;
        }
        private static void AddCell(CoreVoyageSession s, CoreNavalFieldKind kind, int x, int y, int turns, int dx = 0, int dy = 0)
        {
            s.Battle.NavalFields.Cells.Add(new CoreNavalFieldCell { X = x, Y = y, Kind = kind, PlayerOwned = true,
                InstanceId = s.Battle.NavalFields.NextInstanceId++, ExpiresAtOwnerTurn = s.Battle.NavalFields.CompletedEnemyTurns + turns,
                DirectionX = dx, DirectionY = dy });
        }
        private static void SetHull(CoreVoyageSession s, int hull) => typeof(CoreVoyageSession).GetProperty("Hull").SetValue(s, hull);
        private static void Pose(CoreVoyageSession s, bool player, int x, int y) =>
            typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(s.Battle, new object[] { player, new VoyagePoint(x * 100, y * 100), player ? 0f : 180f });
    }
}
