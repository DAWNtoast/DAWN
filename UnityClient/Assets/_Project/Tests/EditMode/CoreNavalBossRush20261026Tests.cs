using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreNavalBossRush20261026Tests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string FireBoss = "boss_route_shattered_sail_reef";
        private static IEnumerable<string> BossIds => CoreRouteBossCatalog.All.Select(x => x.Id)
            .Concat(CoreLegendaryWarships.All.Select(x => x.Id)).Concat(new[] { CoreBattleBalance.TreasureBossId });

        [TestCaseSource(nameof(BossIds))]
        public void ProtectionAndRushUseIndependentNavalRoundCadences(string id)
        {
            var s = Start(id, 1);
            string school = CoreRouteBossCatalog.Find(id)?.SchoolId ?? CoreLegendaryWarships.Find(id)?.SchoolId ?? "tide";
            CoreNavalFieldKind kind = school == "ordinary" ? CoreNavalFieldKind.Tide :
                (CoreNavalFieldKind)Enum.Parse(typeof(CoreNavalFieldKind), school, true);
            for (int turn = 1; turn <= 21; turn++)
            {
                s.Battle.PhaseTurn = turn;
                s.Battle.EnemyNavalBossActionsCompleted = turn * 7;
                bool rush = turn % 5 == 0;
                int expected = rush ? 63 : turn >= 3 && (turn - 3) % 5 == 0 ? 1 << (int)kind : 0;
                Assert.That(s.EnemyNavalBossRushThisTurn, Is.EqualTo(rush), id + " turn " + turn);
                Assert.That(s.EnemyNavalBossFieldImmunityMaskThisTurn, Is.EqualTo(expected), id + " turn " + turn);
                Assert.That(s.Battle.NavalFields.Enemy.BossFieldImmunityMask, Is.Zero, "Plans must not activate protection.");
                Assert.That(s.Battle.EnemyNavalBossActionsCompleted, Is.EqualTo(turn * 7));
            }
        }

        [TestCase(8, 0, 0)] [TestCase(-8, 0, 180)]
        [TestCase(4, 4, 90)] [TestCase(-4, -4, 270)]
        public void RushMovesAtMostFiveCardinalCellsAndMatchesExecution(int x, int y, float heading)
        {
            var s = Start(FireBoss, 5, x, y, heading);
            var before = Snapshot(s);
            var preview = Preview(s);
            Assert.That(preview.ActiveSteps, Is.EqualTo(5));
            Assert.That(Distance(preview.FinalPositionMeters, s.Battle.PlayerPositionMeters), Is.EqualTo(3));
            Assert.That(Snapshot(s), Is.EqualTo(before));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyPositionMeters, Is.EqualTo(preview.EndTurnPositionMeters));
            Assert.That(s.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
        }

        [TestCase(2, 1, 1)] [TestCase(20, 5, 15)]
        public void RushStopsBeforeTheTargetAndDoesNotTeleport(int start, int steps, int remaining)
        {
            var s = Start(FireBoss, 5, start);
            var preview = Preview(s);
            Assert.That(preview.ActiveSteps, Is.EqualTo(steps));
            Assert.That(Distance(preview.FinalPositionMeters, s.Battle.PlayerPositionMeters), Is.EqualTo(remaining));
        }

        [Test]
        public void RushIgnoresOldMovementCapsAndPreviewDoesNotConsumeThem()
        {
            var s = Start(FireBoss, 5);
            s.Battle.NavalFields.Enemy.MovementSlow = 100;
            s.Battle.NavalFields.Enemy.NextMovementMaxCells = 1;
            Assert.That(Preview(s).ActiveSteps, Is.EqualTo(5));
            Assert.That(s.Battle.NavalFields.Enemy.MovementSlow, Is.EqualTo(100));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(Distance(s.Battle.EnemyPositionMeters, s.Battle.PlayerPositionMeters), Is.EqualTo(3));
            Assert.That(s.Battle.NavalFields.Enemy.MovementSlow, Is.Zero);
            Assert.That(s.Battle.NavalFields.Enemy.NextMovementMaxCells, Is.Zero);
        }

        [TestCase(CoreNavalFieldKind.Fire)] [TestCase(CoreNavalFieldKind.Arcane)]
        [TestCase(CoreNavalFieldKind.Curse)] [TestCase(CoreNavalFieldKind.Tide)]
        [TestCase(CoreNavalFieldKind.Thunder)] [TestCase(CoreNavalFieldKind.Blood)]
        public void RushPreviewsAndExecutesOnlyNegativeImmunity(CoreNavalFieldKind kind)
        {
            var s = Start(FireBoss, 5);
            Corridor(s, kind);
            s.Battle.NavalFields.Enemy.Mana = 0;
            var before = Snapshot(s);
            var preview = Preview(s);
            for (int read = 0; read < 2; read++)
            {
                Assert.That(s.GetEnemyIntent(), Does.Contain("极速行进"));
                Assert.That(s.GetEnemyIntent(), Does.Contain("每 5 个海战回合发动一次"));
                Assert.That(CoreBattleIntentPresentation.Build(s).Any(x => x.Id == "enemy_boss_rush"), Is.True);
                Assert.That(Preview(s).FinalPositionMeters, Is.EqualTo(preview.FinalPositionMeters));
            }
            Assert.That(Snapshot(s), Is.EqualTo(before));
            Assert.That(preview.ActiveSteps, Is.EqualTo(5));
            Assert.That(preview.Damage, Is.Zero);
            int enemyHull = s.Battle.EnemyHull;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyPositionMeters, Is.EqualTo(preview.EndTurnPositionMeters));
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(enemyHull));
            Assert.That(s.Battle.EnemyBurnTurns, Is.Zero);
            Assert.That(s.Battle.Schools.CurseMarks, Is.Zero);
            Assert.That(s.Battle.NavalFields.Enemy.ThunderChain, Is.EqualTo(preview.FinalThunderChain));
            if (kind == CoreNavalFieldKind.Thunder) Assert.That(s.Battle.NavalFields.Enemy.ThunderChain, Is.GreaterThanOrEqualTo(50));
            else Assert.That(s.Battle.NavalFields.Enemy.ThunderChain, Is.Zero);
            Assert.That(s.Battle.NavalFields.Enemy.Mana, Is.EqualTo(s.Battle.NavalFields.Enemy.ManaLimit + (kind == CoreNavalFieldKind.Arcane ? 20 : 0)));
            Assert.That(s.Battle.NavalFields.Enemy.BossFieldImmunityMask, Is.Zero);
            Assert.That(s.Battle.NavalFields.Enemy.AllFieldEffectsImmune, Is.False);
            Assert.That(s.Battle.NavalFields.Cells.Count, Is.EqualTo(8));
        }

        [Test]
        public void SchoolProtectionDoesNotBlockOtherFieldsOrCleanseExistingStatuses()
        {
            var fire = Start(FireBoss, 3);
            Corridor(fire, CoreNavalFieldKind.Fire);
            fire.Battle.EnemyBurnTurns = fire.Battle.EnemyBurnDamage = 7;
            Set(fire.Battle.Schools, "CurseMarks", 2);
            fire.NavalFieldsEndTurn(true);
            fire.NavalFieldsBeginTurn(false);
            Assert.That(fire.Battle.NavalFields.Enemy.BossFieldImmunityMask, Is.EqualTo(1 << (int)CoreNavalFieldKind.Fire));
            Assert.That(fire.Battle.NavalFields.Enemy.AllFieldEffectsImmune, Is.False);
            fire.NavalFieldsEndTurn(false);
            Assert.That(fire.Battle.EnemyBurnTurns, Is.EqualTo(7));
            Assert.That(fire.Battle.Schools.CurseMarks, Is.EqualTo(2));
            Assert.That(fire.Battle.NavalFields.Enemy.BossFieldImmunityMask, Is.Zero);

            var blood = Start(FireBoss, 3);
            Corridor(blood, CoreNavalFieldKind.Blood);
            blood.NavalFieldsEndTurn(true);
            blood.NavalFieldsBeginTurn(false);
            int hull = blood.Battle.EnemyHull;
            blood.NavalFieldsEndTurn(false);
            Assert.That(blood.Battle.EnemyHull, Is.EqualTo(hull - 20), "Fire protection does not prevent blood-pool loss.");
        }

        [Test]
        public void RushDoesNotCleanseExistingBurnOrMarksAndNormalRoundRestoresFieldEffects()
        {
            var s = Start(FireBoss, 5);
            Corridor(s, CoreNavalFieldKind.Fire);
            s.Battle.EnemyBurnTurns = s.Battle.EnemyBurnDamage = 7;
            Set(s.Battle.Schools, "CurseMarks", 2);
            int hull = s.Battle.EnemyHull;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(6));
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(2));
            Assert.That(s.Battle.EnemyHull, Is.LessThan(hull));
            Assert.That(s.Battle.PhaseTurn, Is.EqualTo(6));
            s.NavalFieldsEndTurn(true);
            s.NavalFieldsBeginTurn(false);
            Assert.That(s.Battle.NavalFields.Enemy.BossFieldImmunityMask, Is.Zero);
            s.NavalFieldsEndTurn(false);
            Assert.That(s.Battle.EnemyBurnTurns, Is.GreaterThan(6));
        }

        [Test]
        public void RushRetainsBloodRebateAndStillPaysRealHealthCost()
        {
            var s = Start(FireBoss, 5);
            Corridor(s, CoreNavalFieldKind.Blood);
            s.NavalFieldsEndTurn(true);
            s.NavalFieldsBeginTurn(false);
            s.Battle.EnemyBlock = 0;
            int hull = s.Battle.EnemyHull;
            Call(s, "PayBattleHealthCost", false, 4);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - 4));
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(8));
            s.NavalFieldsEndTurn(false);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - 4));
        }

        [Test]
        public void RushRetainsThunderChainsFreeConsumptionAndGainsWithoutOverloadDamage()
        {
            var s = Start(FireBoss, 5);
            Corridor(s, CoreNavalFieldKind.Thunder);
            s.Battle.NavalFields.Enemy.ThunderChain = 49;
            s.NavalFieldsEndTurn(true);
            s.NavalFieldsBeginTurn(false);
            Assert.That(s.Battle.NavalFields.Enemy.ThunderChain, Is.EqualTo(49));
            Call(s, "ConsumeThunderChain", false, 49);
            Assert.That(s.Battle.NavalFields.Enemy.ThunderChain, Is.EqualTo(49));
            int hull = s.Battle.EnemyHull;
            Call(s, "GainNavalThunderChain", false, 1);
            Assert.That(s.Battle.NavalFields.Enemy.ThunderChain, Is.EqualTo(50));
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull));
        }

        [Test]
        public void RushCanHoldASafeUsefulFieldInsteadOfAlwaysClosingToContact()
        {
            var s = Start(FireBoss, 5, 3);
            AddCell(s, CoreNavalFieldKind.Fire, 3, 0, 1, 0);
            var preview = Preview(s);
            Assert.That(preview.ActiveSteps, Is.Zero);
            Assert.That(preview.FinalPositionMeters, Is.EqualTo(s.Battle.EnemyPositionMeters));
            Assert.That((bool)Call(s, "NavalBossShouldHoldOccupiedField", 300f), Is.True);
            Assert.That((bool)Call(s, "NavalBossShouldAvoidOccupiedField"), Is.False);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyPositionMeters, Is.EqualTo(preview.EndTurnPositionMeters));
        }

        [Test]
        public void NormalMovementCanLeaveDangerEvenInsideFiringRange()
        {
            var s = Start(FireBoss, 4, 3);
            AddCell(s, CoreNavalFieldKind.Blood, 3, 0, 1, 0);
            var before = Snapshot(s);
            Assert.That((bool)Call(s, "NavalBossShouldAvoidOccupiedField"), Is.True);
            Assert.That((bool)Call(s, "NavalBossShouldHoldOccupiedField", 300f), Is.False);
            var preview = Preview(s);
            Assert.That(preview.ActiveSteps, Is.GreaterThan(0));
            Assert.That(preview.FinalPositionMeters, Is.Not.EqualTo(s.Battle.EnemyPositionMeters));
            Assert.That(Snapshot(s), Is.EqualTo(before));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyPositionMeters, Is.EqualTo(preview.EndTurnPositionMeters));
        }

        [TestCase(4, 0, 0)] [TestCase(4, 0, 90)]
        [TestCase(4, 0, 270)] [TestCase(2, 2, 0)]
        public void NormalBossOnEmptySeaUsesTheSamePreviewAndExecutionRoute(int x, int y, float heading)
        {
            var s = Start(FireBoss, 2, x, y, heading);
            s.Battle.EnemyNavalBossActionsCompleted = 3;
            Assert.That(s.EnemyNavalBossRushThisTurn, Is.False);
            Assert.That(s.Battle.NavalFields.Cells, Is.Empty);
            object plan = Call(s, "BuildEnemyPlan");
            float meters = (float)plan.GetType().GetField("Move", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(plan);
            var before = Snapshot(s);
            var path = (List<CoreNavalMovementNode>)Call(s, "BuildNavalFieldEnemyPath", meters, null);
            var preview = s.GetEnemyNavalFieldPreview(meters);
            CoreNavalMovementNode last = path[path.Count - 1];
            Assert.That(preview.FinalPositionMeters, Is.EqualTo(last.PositionMeters));
            Assert.That(Snapshot(s), Is.EqualTo(before));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyPositionMeters, Is.EqualTo(preview.EndTurnPositionMeters));
            Assert.That(s.Battle.EnemyHeadingDegrees, Is.EqualTo(last.HeadingDegrees));
            Assert.That(s.Battle.NavalFields.Cells, Is.Empty);
        }

        [Test]
        public void AdjacentBossCanStepSidewaysOutOfBloodLossWithMatchingPreview()
        {
            var s = Start(FireBoss, 4, 1);
            s.Battle.EnemyNavalBossActionsCompleted = 3;
            AddCell(s, CoreNavalFieldKind.Blood, 1, 0, 1, 0);
            var preview = Preview(s);
            Assert.That(preview.ActiveSteps, Is.GreaterThan(0));
            Assert.That(preview.FinalPositionMeters, Is.Not.EqualTo(s.Battle.PlayerPositionMeters));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyPositionMeters, Is.EqualTo(preview.EndTurnPositionMeters));
        }

        [Test]
        public void BloodPoolBenefitRequiresTheCurrentPayableActionAndSurvival()
        {
            string id = CoreRouteBossCatalog.All.First(x => x.SchoolId == "blood").Id;
            var s = Start(id, 2, 3);
            s.Battle.EnemyNavalBossActionsCompleted = 1;
            AddCell(s, CoreNavalFieldKind.Blood, 3, 0, 1, 0);
            s.Battle.NavalFields.Cells[0].ExpiresAtOwnerTurn = 1;
            int cost = (int)Call(s, "NavalBossExpectedBloodCost");
            Assert.That(cost, Is.GreaterThan(0));
            Assert.That((bool)Call(s, "NavalBossShouldHoldOccupiedField", 300f), Is.True);
            Assert.That((bool)Call(s, "NavalBossShouldAvoidOccupiedField"), Is.False);
            s.Battle.EnemyNavalBossActionsCompleted = 0;
            Assert.That((bool)Call(s, "NavalBossShouldHoldOccupiedField", 300f), Is.False);
            s.Battle.EnemyNavalBossActionsCompleted = 1;
            s.Battle.EnemyHull = cost;
            Assert.That((bool)Call(s, "NavalBossShouldHoldOccupiedField", 300f), Is.False);
            s.Battle.EnemyHull = cost + 2;
            Set(s.Battle.Schools, "CurseMarks", 2);
            Assert.That((bool)Call(s, "NavalBossShouldHoldOccupiedField", 300f), Is.False);
            s.Battle.EnemyHull = 10000; Set(s.Battle.Schools, "CurseMarks", 0);
            s.Battle.NavalFields.Cells[0].ExpiresAtOwnerTurn = 100;
            Assert.That((bool)Call(s, "NavalBossShouldAvoidOccupiedField"), Is.True);
        }

        [Test]
        public void ThunderBossAvoidsActionOverloadFromItsActualTwoChainGain()
        {
            string id = CoreRouteBossCatalog.All.First(x => x.SchoolId == "thunder").Id;
            var s = Start(id, 4, 3);
            s.Battle.EnemyNavalBossActionsCompleted = 3;
            AddCell(s, CoreNavalFieldKind.Thunder, 3, 0, 1, 0);
            s.Battle.NavalFields.Enemy.ThunderChain = 48;
            var before = Snapshot(s);
            Assert.That((int)Call(s, "NavalBossExpectedChainGain"), Is.EqualTo(2));
            Assert.That((bool)Call(s, "NavalBossShouldAvoidOccupiedField"), Is.True);
            var preview = Preview(s);
            Assert.That(preview.ActiveSteps, Is.GreaterThan(0));
            Assert.That(s.NavalFieldAt(preview.FinalPositionMeters), Is.Null);
            Assert.That(Snapshot(s), Is.EqualTo(before));
            int hull = s.Battle.EnemyHull;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyPositionMeters, Is.EqualTo(preview.EndTurnPositionMeters));
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull));
        }

        [Test]
        public void RushPreviewIncludesPlayersPendingCurrentPushWithoutSideEffects()
        {
            var s = Start(FireBoss, 5);
            AddCell(s, CoreNavalFieldKind.Tide, 0, 0, 0, 1);
            var before = Snapshot(s);
            var preview = Preview(s);
            Assert.That(Snapshot(s), Is.EqualTo(before));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.PlayerPositionMeters, Is.EqualTo(new VoyagePoint(0, 100)));
            Assert.That(s.Battle.EnemyPositionMeters, Is.EqualTo(preview.FinalPositionMeters));
            Assert.That(preview.ActiveSteps, Is.LessThanOrEqualTo(5));
        }

        [TestCase(3)] [TestCase(5)]
        public void ActiveProtectionRoundTripsAndRejectsInvalidSnapshotMasks(int turn)
        {
            var s = Start(FireBoss, turn);
            s.NavalFieldsEndTurn(true);
            s.NavalFieldsBeginTurn(false);
            var saved = s.CaptureNavalFields();
            int expected = s.Battle.NavalFields.Enemy.BossFieldImmunityMask;
            s.Battle.NavalFields.Enemy.BossFieldImmunityMask = 0;
            Assert.That(s.RestoreNavalFields(saved), Is.True);
            Assert.That(s.Battle.NavalFields.Enemy.BossFieldImmunityMask, Is.EqualTo(expected));
            saved.Enemy.BossFieldImmunityMask = 1 << 9;
            Assert.That(s.RestoreNavalFields(saved), Is.False);
            saved = s.CaptureNavalFields(); saved.Enemy.BossFieldImmunityMask = 1 << (int)CoreNavalFieldKind.Blood;
            Assert.That(s.RestoreNavalFields(saved), Is.False);
            saved = s.CaptureNavalFields(); saved.Player.BossFieldImmunityMask = 1;
            Assert.That(s.RestoreNavalFields(saved), Is.False);
            saved = s.CaptureNavalFields(); saved.PlayerActing = true;
            Assert.That(s.RestoreNavalFields(saved), Is.False);
        }

        [Test]
        public void PreparationBoardingOrdinaryEnemiesAndTrainingTargetsNeverReceiveProtection()
        {
            var boss = Start(FireBoss, 5);
            foreach (NavalBattleStage stage in new[] { NavalBattleStage.Preparation, NavalBattleStage.Boarding })
            {
                boss.Battle.Stage = stage;
                Assert.That(boss.EnemyNavalBossRushThisTurn, Is.False);
                Assert.That(boss.EnemyNavalBossFieldImmunityMaskThisTurn, Is.Zero);
            }
            var ordinary = new CoreVoyageSession();
            Call(ordinary, "StartBattle", new CoreEncounter("rush_ordinary", "普通敌舰", "海战", "", false, false, 1, 70), false);
            ordinary.Battle.Stage = NavalBattleStage.Naval; ordinary.Battle.PhaseTurn = 5;
            Assert.That(ordinary.EnemyNavalBossFieldImmunityMaskThisTurn, Is.Zero);
            var training = new CoreVoyageSession();
            Assert.That(training.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            training.Battle.Stage = NavalBattleStage.Naval; training.Battle.PhaseTurn = 5;
            Assert.That(training.EnemyNavalBossFieldImmunityMaskThisTurn, Is.Zero);
        }

        [Test]
        public void GuidedPracticeRemainsExcludedButItsFinalBattleCanRush()
        {
            var s = new CoreVoyageSession();
            s.ConfigureGuidedTutorialVoyage(CoreGuidedTutorialContent.BossVoyageLeg);
            Call(s, "StartBattle", new CoreEncounter(CoreGuidedTutorialContent.BossEventId, "教学", "战斗", "", true, false, 0, 0), true);
            s.Battle.Stage = NavalBattleStage.Naval; s.Battle.PhaseTurn = 5;
            Assert.That(s.EnemyNavalBossFieldImmunityMaskThisTurn, Is.Zero);
            Call(s, "StartGuidedBossLesson", GuidedBossLesson.FinalBattle);
            s.Battle.Stage = NavalBattleStage.Naval; s.Battle.PhaseTurn = 5;
            Assert.That(s.EnemyNavalBossRushThisTurn, Is.True);
            Assert.That(s.EnemyNavalBossFieldImmunityMaskThisTurn, Is.EqualTo(63));
            s.Battle.PhaseTurn = 3;
            Assert.That(s.EnemyNavalBossFieldImmunityMaskThisTurn, Is.Zero, "An unknown tutorial school must not gain implicit protection.");
        }

        private static CoreVoyageSession Start(string id, int turn, int x = 8, int y = 0, float heading = 180)
        {
            var s = new CoreVoyageSession();
            s.SetSeed("BOSS-RUSH-20261026"); s.StartVoyage();
            var encounter = CoreRouteBossCatalog.Find(id)?.CreateEncounter() ?? CoreLegendaryWarships.Find(id)?.CreateEncounter()
                ?? CoreLegendaryWarships.CreateTreasureEncounter();
            Call(s, "StartBattle", encounter, true);
            s.Battle.Stage = NavalBattleStage.Naval; s.Battle.PhaseTurn = turn;
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(s, 10000);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 10000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = s.Battle.EnemyLoadedShot = 0;
            Pose(s, true, 0, 0, 0); Pose(s, false, x, y, heading);
            return s;
        }
        private static void Corridor(CoreVoyageSession s, CoreNavalFieldKind kind)
        {
            for (int x = 1; x <= 8; x++) AddCell(s, kind, x, 0, 1, 0);
        }
        private static void AddCell(CoreVoyageSession s, CoreNavalFieldKind kind, int x, int y, int dx, int dy) =>
            s.Battle.NavalFields.Cells.Add(new CoreNavalFieldCell { X = x, Y = y, Kind = kind, PlayerOwned = true,
                ExpiresAtOwnerTurn = 20, InstanceId = s.Battle.NavalFields.NextInstanceId++, DirectionX = dx, DirectionY = dy });
        private static int Distance(VoyagePoint a, VoyagePoint b) => (int)Math.Round((Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y)) / 100f);
        private static void Pose(CoreVoyageSession s, bool player, int x, int y, float heading) =>
            Call(s.Battle, "SetTacticalPose", player, new VoyagePoint(x * 100, y * 100), heading);
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Hidden).Invoke(target, args);
        private static void Set(object target, string name, object value) => target.GetType().GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        private static CoreNavalMovementFieldPreview Preview(CoreVoyageSession s)
        {
            object plan = Call(s, "BuildEnemyPlan");
            float meters = (float)plan.GetType().GetField("Move", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(plan);
            return s.GetEnemyNavalFieldPreview(meters);
        }
        private static string Snapshot(CoreVoyageSession s)
        {
            var b = s.Battle; var fields = b.NavalFields;
            return string.Join("|", new object[] { s.Hull, b.Stage, b.PhaseTurn, b.Turn, b.EnemyHull, b.EnemyBlock,
                b.EnemyBurnTurns, b.Schools.CurseMarks, b.EnemyNavalBossActionsCompleted, b.EnemyPositionMeters.X,
                b.EnemyPositionMeters.Y, b.EnemyHeadingDegrees, b.PlayerPositionMeters.X, b.PlayerPositionMeters.Y,
                fields.PlayerTurn, fields.EnemyTurn, fields.CompletedPlayerTurns, fields.CompletedEnemyTurns,
                fields.PresentationSequence, string.Join(";", fields.TriggerLedger),
                string.Join(";", fields.Enemy.GetType().GetFields().OrderBy(x => x.Name).Select(x => x.Name + "=" + x.GetValue(fields.Enemy))),
                string.Join(";", fields.Cells.Select(x => x.X + ":" + x.Y + ":" + x.Kind + ":" + x.ExpiresAtOwnerTurn)) });
        }
    }
}
