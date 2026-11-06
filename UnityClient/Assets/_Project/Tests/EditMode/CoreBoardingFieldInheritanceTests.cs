using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBoardingFieldInheritanceTests
    {
        [TestCase(CoreNavalFieldKind.Fire)]
        [TestCase(CoreNavalFieldKind.Arcane)]
        [TestCase(CoreNavalFieldKind.Curse)]
        [TestCase(CoreNavalFieldKind.Thunder)]
        [TestCase(CoreNavalFieldKind.Blood)]
        [TestCase(CoreNavalFieldKind.Tide)]
        public void PlayerRamInheritsOnlyTheStruckCellAndDropsCurrent(CoreNavalFieldKind targetKind)
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -100, 0, 0, 0, 0, 90);
            AddCell(session, targetKind == CoreNavalFieldKind.Blood ? CoreNavalFieldKind.Fire : CoreNavalFieldKind.Blood,
                -1, 0, false, 9);
            CoreNavalFieldCell source = AddCell(session, targetKind, 0, 0, true, 3);
            Assert.That(session.TryRam(), Is.True);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(session.Battle.NavalFields.Cells, Is.Empty);
            Assert.That(session.Battle.BoardingFields.Fields.Count, Is.EqualTo(targetKind == CoreNavalFieldKind.Tide ? 0 : 1));
            Assert.That(session.Battle.BoardingFields.Remaining(targetKind), Is.EqualTo(targetKind == CoreNavalFieldKind.Tide ? 0 : 3));
            source.ExpiresAtOwnerTurn = 99;
            Assert.That(session.Battle.BoardingFields.Remaining(targetKind), Is.EqualTo(targetKind == CoreNavalFieldKind.Tide ? 0 : 3));
        }

        [TestCase(CoreNavalFieldKind.Fire, CoreNavalFieldKind.Arcane)]
        [TestCase(CoreNavalFieldKind.Curse, CoreNavalFieldKind.Blood)]
        [TestCase(CoreNavalFieldKind.Thunder, CoreNavalFieldKind.Tide)]
        public void PlannedBoardingCombinesBothCellsByKind(CoreNavalFieldKind playerKind, CoreNavalFieldKind enemyKind)
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -100, 0, 90, 0, 0, 90);
            AddCell(session, playerKind, -1, 0, false, 4);
            AddCell(session, enemyKind, 0, 0, true, 6);
            Assert.That(session.TryBoard(), Is.True);
            Assert.That(session.Battle.BoardingFields.Remaining(playerKind), Is.EqualTo(4));
            Assert.That(session.Battle.BoardingFields.Remaining(enemyKind), Is.EqualTo(enemyKind == CoreNavalFieldKind.Tide ? 0 : 6));
            Assert.That(session.Battle.BoardingFields.Fields.Count, Is.EqualTo(enemyKind == CoreNavalFieldKind.Tide ? 1 : 2));
        }

        [TestCase(true)] [TestCase(false)]
        public void SameKindUsesLongestRemainingDurationAndItsOwnerInsteadOfAbsoluteExpiry(bool longerPlayerOwned)
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -100, 0, 90, 0, 0, 90);
            session.Battle.NavalFields.CompletedPlayerTurns = 20;
            session.Battle.NavalFields.CompletedEnemyTurns = 2;
            AddCell(session, CoreNavalFieldKind.Fire, -1, 0, !longerPlayerOwned, 3);
            AddCell(session, CoreNavalFieldKind.Fire, 0, 0, longerPlayerOwned, 6);
            Assert.That(session.TryBoard(), Is.True);
            CoreBoardingField inherited = session.Battle.BoardingFields.Fields.Single();
            Assert.That(inherited.Kind, Is.EqualTo(CoreNavalFieldKind.Fire));
            Assert.That(inherited.RemainingTurns, Is.EqualTo(6));
            Assert.That(inherited.PlayerOwned, Is.EqualTo(longerPlayerOwned));
        }

        [Test]
        public void ExpiredCellsDoNotBecomeBoardingFields()
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -100, 0, 90, 0, 0, 90);
            session.Battle.NavalFields.CompletedPlayerTurns = 3;
            session.Battle.NavalFields.CompletedEnemyTurns = 8;
            AddCell(session, CoreNavalFieldKind.Fire, -1, 0, true, 0);
            AddCell(session, CoreNavalFieldKind.Blood, 0, 0, false, -1);
            Assert.That(session.TryBoard(), Is.True);
            Assert.That(session.Battle.BoardingFields.Fields, Is.Empty);
        }

        [TestCase(true)] [TestCase(false)]
        public void EnemyRamCapturesThePlayersOriginalCellBeforeDistanceCloses(bool playerOwned)
        {
            CoreVoyageSession session = EnemyRamBattle();
            AddCell(session, CoreNavalFieldKind.Blood, -1, 0, playerOwned, 4);
            AddCell(session, CoreNavalFieldKind.Fire, 0, 0, !playerOwned, 9);
            Assert.That(session.EnemyWillRam, Is.True);
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(session.Battle.BoardingInitiatedByEnemy, Is.True);
            Assert.That(session.Battle.PlayerPositionMeters.X, Is.EqualTo(-30f).Within(.01f));
            CoreBoardingField inherited = session.Battle.BoardingFields.Fields.Single();
            Assert.That(inherited.Kind, Is.EqualTo(CoreNavalFieldKind.Blood));
            Assert.That(inherited.PlayerOwned, Is.EqualTo(playerOwned));
            Assert.That(inherited.RemainingTurns, Is.EqualTo(3));
        }

        [Test]
        public void MovementRamInheritsTargetCellWithoutEnteringItOrTakingTheAdjacentField()
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -200, 0, 0, 0, 0, 90);
            Set(session.Battle, "NavalActionPoints", 2);
            AddCell(session, CoreNavalFieldKind.Blood, -1, 0, false, 9);
            AddCell(session, CoreNavalFieldKind.Curse, 0, 0, true, 3);
            Append(session, -100, 0);
            Append(session, 0, 0);
            Assert.That(session.CommitNavalMovement(out string reason), Is.True, reason);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(session.Battle.BoardingFields.Fields.Single().Kind, Is.EqualTo(CoreNavalFieldKind.Curse));
            Assert.That(session.Battle.BoardingFields.Remaining(CoreNavalFieldKind.Curse), Is.EqualTo(3));
        }

        [TestCase(true)] [TestCase(false)]
        public void ArrivalContactUsesTheSameOneCellOrTwoCellRule(bool boarding)
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -200, 0, 0, 0, 0, 90);
            Set(session.Battle, "NavalActionPoints", 2);
            AddCell(session, CoreNavalFieldKind.Blood, -1, 0, false, 4);
            AddCell(session, CoreNavalFieldKind.Arcane, 0, 0, true, 3);
            Append(session, -100, 0);
            Assert.That(session.CommitNavalMovement(out string reason), Is.True, reason);
            Assert.That(session.TryNavalArrivalContact(boarding, out reason), Is.True, reason);
            Assert.That(session.Battle.BoardingFields.Remaining(CoreNavalFieldKind.Blood), Is.EqualTo(boarding ? 4 : 0));
            Assert.That(session.Battle.BoardingFields.Remaining(CoreNavalFieldKind.Arcane), Is.EqualTo(3));
        }

        [TestCase(true)] [TestCase(false)]
        public void ActiveContactGrantsFirstRefractionManaExactlyOnce(bool ram)
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -100, 0, ram ? 0 : 90, 0, 0, 90);
            Set(session.Battle.Schools, "Mana", 7);
            AddCell(session, CoreNavalFieldKind.Arcane, 0, 0, true, 4);
            Assert.That(ram ? session.TryRam() : session.TryBoard(), Is.True);
            Assert.That(session.Battle.Schools.Mana, Is.EqualTo(11));
            Assert.That(ram ? session.TryRam() : session.TryBoard(), Is.False);
            session.GetEnemyIntent();
            CoreBattleStatusPresentation.Build(session, true);
            Assert.That(session.Battle.Schools.Mana, Is.EqualTo(11));
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.BoardingFields.Remaining(CoreNavalFieldKind.Arcane), Is.EqualTo(3));
            Assert.That(session.Battle.Schools.Mana, Is.EqualTo(session.Battle.Schools.ManaLimit + 3));
            Assert.That(session.Battle.BoardingEnemy.Mana, Is.EqualTo(CoreBoardingEnemyState.ManaLimit + 4),
                "Training targets also receive the shared field at their turn start.");
        }

        [Test]
        public void EnemyRamGrantsEnemyManaThenExpiresBeforeFirstPlayerManaGrant()
        {
            CoreVoyageSession session = EnemyRamBattle();
            AddCell(session, CoreNavalFieldKind.Arcane, -1, 0, true, 4);
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.BoardingEnemy.Mana, Is.EqualTo(CoreBoardingEnemyState.ManaLimit + 4));
            Assert.That(session.Battle.BoardingFields.Remaining(CoreNavalFieldKind.Arcane), Is.EqualTo(3));
            Assert.That(session.Battle.Schools.Mana, Is.EqualTo(session.Battle.Schools.ManaLimit + 3));
        }

        [Test]
        public void EnemyRamAppliesSharedFireAtThatEnemyTurnEndBeforeExpiry()
        {
            CoreVoyageSession session = EnemyRamBattle();
            AddCell(session, CoreNavalFieldKind.Fire, -1, 0, true, 4);
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.Boarding.EnemyBurnStacks, Is.EqualTo(4));
            Assert.That(session.Battle.BoardingFields.Remaining(CoreNavalFieldKind.Fire), Is.EqualTo(3));
            Assert.That(session.Battle.Boarding.SelfBurnStacks, Is.Zero,
                "Ship burn earned before the collision does not become crew burn.");
        }

        [Test]
        public void FieldCopyOwnsItsEntriesAndExpiryOnlyUsesTheOpposingTurn()
        {
            var fields = new CoreBoardingFieldState();
            fields.Fields.Add(new CoreBoardingField { Kind = CoreNavalFieldKind.Fire, PlayerOwned = true, RemainingTurns = 2 });
            fields.Fields.Add(new CoreBoardingField { Kind = CoreNavalFieldKind.Blood, PlayerOwned = false, RemainingTurns = 1 });
            CoreBoardingFieldState copy = fields.Copy();
            copy.EndTurn(true);
            Assert.That(copy.Remaining(CoreNavalFieldKind.Fire), Is.EqualTo(2));
            Assert.That(copy.Contains(CoreNavalFieldKind.Blood), Is.False);
            Assert.That(fields.Remaining(CoreNavalFieldKind.Blood), Is.EqualTo(1));
            copy.EndTurn(false);
            Assert.That(copy.Remaining(CoreNavalFieldKind.Fire), Is.EqualTo(1));
            Assert.That(fields.Remaining(CoreNavalFieldKind.Fire), Is.EqualTo(2));
            copy.EndTurn(false);
            Assert.That(copy.Fields, Is.Empty);
            Assert.That(fields.Fields.Count, Is.EqualTo(2));
        }

        [TestCase(CoreNavalFieldKind.Fire, "火场")]
        [TestCase(CoreNavalFieldKind.Arcane, "折射阵")]
        [TestCase(CoreNavalFieldKind.Curse, "禁域")]
        [TestCase(CoreNavalFieldKind.Thunder, "雷栅")]
        [TestCase(CoreNavalFieldKind.Blood, "血池")]
        public void SharedFieldStatusShowsTheSameRemainingDurationForBothCombatants(CoreNavalFieldKind kind, string name)
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -100, 0, 90, 0, 0, 90);
            AddCell(session, kind, 0, 0, true, 4);
            Assert.That(session.TryBoard(), Is.True);
            string id = "boarding_field_" + kind.ToString().ToLowerInvariant();
            foreach (bool player in new[] { true, false })
            {
                CoreBattleStatus status = CoreBattleStatusPresentation.Build(session, player).Single(item => item.Id == id);
                Assert.That(status.Name, Is.EqualTo(name));
                Assert.That(status.Value, Is.EqualTo("4"));
                Assert.That(status.Tooltip, Does.Contain(name));
                if (kind == CoreNavalFieldKind.Thunder)
                    Assert.That(status.Tooltip, Does.Contain("1000").And.Contain("50").And.Not.Contain("每移动"));
            }
            Assert.That(session.Battle.BoardingFields.Remaining(kind), Is.EqualTo(4));
        }

        [Test]
        public void EqualLifetimeSourcesChooseOneOwnerAndRepeatedPreviewsKeepTheChosenField()
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -100, 0, 90, 0, 0, 90);
            AddCell(session, CoreNavalFieldKind.Blood, -1, 0, true, 4);
            AddCell(session, CoreNavalFieldKind.Blood, 0, 0, false, 4);
            Assert.That(session.TryBoard(), Is.True);
            CoreBoardingField selected = session.Battle.BoardingFields.Fields.Single();
            Assert.That(selected.Kind, Is.EqualTo(CoreNavalFieldKind.Blood));
            Assert.That(selected.RemainingTurns, Is.EqualTo(4));
            bool owner = selected.PlayerOwned;
            for (int i = 0; i < 3; i++)
            {
                session.GetEnemyIntent();
                session.GetBoardingEnemyPreview();
                CoreBattleStatusPresentation.Build(session, true);
                Assert.That(session.Battle.BoardingFields.Fields.Single(), Is.SameAs(selected));
                Assert.That(selected.PlayerOwned, Is.EqualTo(owner));
                Assert.That(selected.RemainingTurns, Is.EqualTo(4));
            }
        }

        [TestCase(true)] [TestCase(false)]
        public void TrainingOutcomeClearsInheritedFieldsAndANewBattleStartsEmpty(bool playerLoses)
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -100, 0, 90, 0, 0, 90);
            AddCell(session, CoreNavalFieldKind.Blood, 0, 0, true, 4);
            Assert.That(session.TryBoard(), Is.True);
            CoreBattleState finished = session.Battle;
            if (playerLoses) finished.PlayerBoardingHealth = 0;
            else finished.EnemyBoardingHealth = 0;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.TrainingOutcome, Is.EqualTo(playerLoses ? CoreTrainingBattleOutcome.Defeat : CoreTrainingBattleOutcome.Victory));
            Assert.That(finished.BoardingFields.Fields, Is.Empty);
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(session.Battle.BoardingFields.Fields, Is.Empty);
        }

        [Test]
        public void LethalRamNeverLeavesAPlannedInheritanceOnTheBattleResult()
        {
            CoreVoyageSession session = NewBattle();
            Pose(session, -100, 0, 0, 0, 0, 90);
            AddCell(session, CoreNavalFieldKind.Fire, 0, 0, true, 4);
            CoreBattleState battle = session.Battle;
            battle.EnemyHull = 1;
            Assert.That(session.TryRam(), Is.True);
            Assert.That(session.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.Victory));
            Assert.That(battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(battle.BoardingFields.Fields, Is.Empty);
        }

        private static CoreVoyageSession NewBattle()
        {
            var session = new CoreVoyageSession();
            session.SetSeed("BOARDING-FIELD-INHERITANCE");
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(session.EndBattleTurn(), Is.True);
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 500;
            session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth = 1000;
            session.Battle.EnemyBoardingHealth = session.Battle.EnemyMaxBoardingHealth = 1000;
            session.Battle.NavalFields.Cells.Clear();
            Set(session.Battle, "NavalActionPoints", 1);
            return session;
        }

        private static CoreVoyageSession EnemyRamBattle()
        {
            CoreVoyageSession session = NewBattle();
            session.Battle.Profile = new CoreBattleProfile(CoreBossKind.Legendary, CoreBattleArchetype.Raider,
                500, 1, 7, 0, 0, 30, 1f, 1f, 1d, 120f);
            Pose(session, -100, 0, 90, 0, 0, 180);
            return session;
        }

        private static CoreNavalFieldCell AddCell(CoreVoyageSession session, CoreNavalFieldKind kind,
            int x, int y, bool playerOwned, int remaining)
        {
            CoreNavalFieldState state = session.Battle.NavalFields;
            var cell = new CoreNavalFieldCell {
                X = x, Y = y, Kind = kind, PlayerOwned = playerOwned, InstanceId = state.NextInstanceId++,
                ExpiresAtOwnerTurn = (playerOwned ? state.CompletedEnemyTurns : state.CompletedPlayerTurns) + remaining,
                DirectionX = kind == CoreNavalFieldKind.Tide ? 1 : 0
            };
            state.Cells.Add(cell);
            return cell;
        }

        private static void Append(CoreVoyageSession session, float x, float y) =>
            Assert.That(session.TryAppendNavalMovementStep(new VoyagePoint(x, y), out string reason), Is.True, reason);

        private static void Pose(CoreVoyageSession session, float px, float py, float ph, float ex, float ey, float eh)
        {
            MethodInfo method = typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(session.Battle, new object[] { true, new VoyagePoint(px, py), ph });
            method.Invoke(session.Battle, new object[] { false, new VoyagePoint(ex, ey), eh });
        }

        private static void Set(object target, string name, object value) =>
            target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
    }
}
