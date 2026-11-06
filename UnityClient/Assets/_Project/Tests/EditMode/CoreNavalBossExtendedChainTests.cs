using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreNavalBossExtendedChainTests
    {
        private static IEnumerable<CoreNavalFieldKind> Kinds => Enum.GetValues(typeof(CoreNavalFieldKind)).Cast<CoreNavalFieldKind>();
        private static IEnumerable<string> BossIds => CoreRouteBossCatalog.All.Select(x => x.Id)
            .Concat(CoreLegendaryWarships.All.Select(x => x.Id)).Concat(new[] { CoreBattleBalance.TreasureBossId });

        [TestCaseSource(nameof(BossIds))]
        public void EveryChainTelegraphsBothFieldActionsAndItsBurstBeforeRecovery(string id)
        {
            CoreVoyageSession session = Start(id);
            int length = Move(session).CycleLength;
            Assert.That(length, Is.InRange(5, 7));
            var moves = new List<CoreNavalBossMove>();
            for (int step = 0; step < length; step++)
            {
                session.Battle.EnemyNavalBossActionsCompleted = step;
                CoreNavalBossMove move = Move(session); moves.Add(move);
                Assert.That(move.CycleLength, Is.EqualTo(length));
                Assert.That(move.CycleStep, Is.EqualTo(step + 1));
                Assert.That(move.AllowRam, Is.EqualTo(step == length - 1));
                Assert.That(session.GetEnemyIntent(), Does.Contain("循环 " + (step + 1) + "/" + length)
                    .And.Contain("下一步：" + move.NextName));
            }
            Assert.That(moves[0].FieldAction, Is.EqualTo(CoreNavalBossFieldAction.Place));
            Assert.That(moves.Any(m => m.FieldAction == CoreNavalBossFieldAction.Extend), Is.True);
            Assert.That(moves.Any(m => m.FieldAction == CoreNavalBossFieldAction.Expand), Is.True);
            Assert.That(moves[length - 2].StageName, Is.EqualTo("爆发"));
            Assert.That(moves[length - 1].StageName, Is.EqualTo("回整"));
            for (int i = 0; i < length; i++)
            {
                Assert.That(moves[i].NextName, Is.EqualTo(moves[(i + 1) % length].Name));
                Assert.That(moves[i].NextStageName, Is.EqualTo(moves[(i + 1) % length].StageName));
            }
        }

        [TestCaseSource(nameof(Kinds))]
        public void ExtensionUsesOnlyNearbyOwnedInstancesAndCapsTheirRemainingLifetime(CoreNavalFieldKind kind)
        {
            var state = new CoreNavalFieldState { CompletedPlayerTurns = 4 };
            CoreNavalFieldCell near = Cell(state, kind, false, 3, 0, 5);
            CoreNavalFieldCell fragment = Cell(state, kind, false, 12, 0, 5);
            fragment.InstanceId = near.InstanceId;
            CoreNavalFieldCell player = Cell(state, kind, true, 2, 1, 3);
            CoreNavalFieldCell other = Cell(state, Other(kind), false, 3, 1, 3);
            CoreNavalFieldCell far = Cell(state, kind, false, 30, 0, 3);
            string untouched = Cells(new[] { player, other, far });
            var move = Action(kind, CoreNavalBossFieldAction.Extend);
            for (int repeat = 0; repeat < 10; repeat++) Mutate(state, move, new VoyagePoint(400, 0), new VoyagePoint(0, 0), true);
            Assert.That(near.ExpiresAtOwnerTurn, Is.EqualTo(10));
            Assert.That(fragment.ExpiresAtOwnerTurn, Is.EqualTo(10), "An instance retains one expiry even where its footprint is split.");
            Assert.That(Cells(new[] { player, other, far }), Is.EqualTo(untouched));
            Assert.That(state.PresentationEventCount, Is.EqualTo(2), "Capped no-op extensions must not emit more events.");
            Assert.That(state.PresentationEventAt(0).Change, Is.EqualTo(CoreNavalFieldChangeKind.Extended));
        }

        [TestCaseSource(nameof(Kinds))]
        public void ExpansionUsesTheOriginalBoundaryPreservesOtherFieldsAndCannotGrowWithoutLimit(CoreNavalFieldKind kind)
        {
            var state = new CoreNavalFieldState();
            CoreNavalFieldCell source = Cell(state, kind, false, 1, 0, 4);
            CoreNavalFieldCell player = Cell(state, kind, true, 2, 0, 4);
            CoreNavalFieldCell other = Cell(state, Other(kind), false, 1, 1, 4);
            string untouched = Cells(new[] { player, other });
            var move = Action(kind, CoreNavalBossFieldAction.Expand);
            Mutate(state, move, new VoyagePoint(400, 0), new VoyagePoint(0, 0), true);
            CoreNavalFieldCell[] added = state.Cells.Where(c => c.InstanceId == source.InstanceId && !ReferenceEquals(c, source)).ToArray();
            Assert.That(added.Length, Is.InRange(1, 8));
            Assert.That(added.All(c => Math.Abs(c.X - source.X) + Math.Abs(c.Y - source.Y) == 1), Is.True,
                "A newly added cell must not become another source within the same action.");
            Assert.That(added.All(c => c.ExpiresAtOwnerTurn == source.ExpiresAtOwnerTurn && c.DirectionX == source.DirectionX), Is.True);
            if (kind == CoreNavalFieldKind.Fire || kind == CoreNavalFieldKind.Curse || kind == CoreNavalFieldKind.Tide)
                Assert.That(added.Any(c => c.X == 0 && c.Y == 0), Is.True, "Offensive expansion must prioritize the player's adjacent cell.");
            for (int repeat = 0; repeat < 30; repeat++) Mutate(state, move, new VoyagePoint(400, 0), new VoyagePoint(0, 0), false);
            Assert.That(state.Cells.Count(c => !c.PlayerOwned && c.Kind == kind), Is.LessThanOrEqualTo(CoreVoyageSession.GetNavalBossFieldCellLimit(kind)));
            Assert.That(state.Cells.Where(c => !c.PlayerOwned && c.Kind == kind).All(c =>
                Math.Abs(c.X - 4) + Math.Abs(c.Y) <= 4 + CoreVoyageSession.GetNavalFieldSize(kind) - 1), Is.True);
            Assert.That(Cells(new[] { player, other }), Is.EqualTo(untouched));
            Assert.That(state.Cells.Select(c => c.X + ":" + c.Y).Distinct().Count(), Is.EqualTo(state.Cells.Count));
        }

        [TestCaseSource(nameof(Kinds))]
        public void PreviewAndExecutionMutateIdenticalCellsButOnlyExecutionEmitsEvents(CoreNavalFieldKind kind)
        {
            foreach (CoreNavalBossFieldAction action in new[] { CoreNavalBossFieldAction.Place, CoreNavalBossFieldAction.Extend, CoreNavalBossFieldAction.Expand })
            {
                var live = new CoreNavalFieldState();
                Cell(live, kind, false, 3, 0, 4);
                Cell(live, Other(kind), true, 0, 0, 3);
                CoreNavalFieldState preview = live.Copy();
                var move = Action(kind, action);
                Mutate(preview, move, new VoyagePoint(400, 0), new VoyagePoint(0, 0), false);
                Assert.That(live.PresentationSequence, Is.Zero);
                Mutate(live, move, new VoyagePoint(400, 0), new VoyagePoint(0, 0), true);
                Assert.That(Cells(preview.Cells), Is.EqualTo(Cells(live.Cells)), kind + " " + action);
                Assert.That(preview.NextInstanceId, Is.EqualTo(live.NextInstanceId));
                Assert.That(preview.PresentationSequence, Is.Zero);
                Assert.That(live.PresentationSequence, Is.GreaterThan(0));
            }
        }

        [TestCase(CoreNavalBossFieldAction.Extend)]
        [TestCase(CoreNavalBossFieldAction.Expand)]
        public void MissingOrExpiredOwnedFieldsFallBackToOneBoundedFreshPlacement(CoreNavalBossFieldAction action)
        {
            var state = new CoreNavalFieldState { CompletedPlayerTurns = 2 };
            Cell(state, CoreNavalFieldKind.Fire, false, 2, 0, 0);
            CoreNavalFieldCell player = Cell(state, CoreNavalFieldKind.Fire, true, 20, 0, 4);
            var move = Action(CoreNavalFieldKind.Fire, action);
            Mutate(state, move, new VoyagePoint(400, 0), new VoyagePoint(0, 0), true);
            int size = CoreVoyageSession.GetNavalFieldSize(CoreNavalFieldKind.Fire);
            var current = state.Cells.Where(c => !c.PlayerOwned && c.Kind == CoreNavalFieldKind.Fire).ToArray();
            Assert.That(current.Length, Is.EqualTo(size * size));
            Assert.That(current.All(c => c.ExpiresAtOwnerTurn == 5), Is.True);
            Assert.That(state.Cells.Contains(player), Is.True);
            int previousInstance = current[0].InstanceId;
            Mutate(state, Action(CoreNavalFieldKind.Fire, CoreNavalBossFieldAction.Place), new VoyagePoint(800, 0), new VoyagePoint(400, 0), true);
            Assert.That(state.Cells.Any(c => c.InstanceId == previousInstance), Is.False,
                "A new cycle replaces old same-school islands instead of accumulating map-wide fields.");
            Assert.That(state.Cells.Count(c => !c.PlayerOwned && c.Kind == CoreNavalFieldKind.Fire), Is.EqualTo(size * size));
        }

        [TestCaseSource(nameof(BossIds))]
        public void AFieldExpiringAtPlayerEndIsRecastByBothTheAdvertisedAndExecutedExtension(string id)
        {
            CoreVoyageSession session = Start(id);
            int length = Move(session).CycleLength;
            for (int step = 0; step < length; step++)
            {
                session.Battle.EnemyNavalBossActionsCompleted = step;
                if (Move(session).FieldAction == CoreNavalBossFieldAction.Extend) break;
            }
            CoreNavalBossMove move = Move(session);
            Assert.That(move.FieldAction, Is.EqualTo(CoreNavalBossFieldAction.Extend));
            CoreNavalFieldCell old = Cell(session.Battle.NavalFields, move.FieldKind.Value, false, 3, 0, 1);
            long sequence = session.Battle.NavalFields.PresentationSequence;
            int instance = session.Battle.NavalFields.NextInstanceId;
            string cells = Cells(session.Battle.NavalFields.Cells);
            Assert.That(session.GetEnemyIntent(), Does.Contain("没有可用自有场地").And.Contain("补场"));
            session.GetEnemyNavalFieldPreview(0);
            Assert.That(Cells(session.Battle.NavalFields.Cells), Is.EqualTo(cells));
            Assert.That(session.Battle.NavalFields.PresentationSequence, Is.EqualTo(sequence));
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.NavalFields.Cells.Any(c => c.InstanceId == old.InstanceId), Is.False);
            Assert.That(session.Battle.NavalFields.Cells.Any(c => c.InstanceId == instance), Is.True);
            Assert.That(session.Battle.NavalFields.Cells.Where(c => c.InstanceId == instance).All(c =>
                session.GetNavalFieldRemainingTurns(c) == move.FieldTurns), Is.True);
        }

        private static CoreNavalBossMove Action(CoreNavalFieldKind kind, CoreNavalBossFieldAction action) =>
            new CoreNavalBossMove { FieldKind = kind, FieldAction = action, FieldTurns = kind == CoreNavalFieldKind.Thunder ? 5 : 3,
                FieldExtendTurns = 3, FieldExpansionCells = 8 };
        private static CoreNavalFieldKind Other(CoreNavalFieldKind kind) => kind == CoreNavalFieldKind.Fire ? CoreNavalFieldKind.Arcane : CoreNavalFieldKind.Fire;
        private static CoreNavalFieldCell Cell(CoreNavalFieldState state, CoreNavalFieldKind kind, bool player, int x, int y, int remaining)
        {
            var cell = new CoreNavalFieldCell { Kind = kind, PlayerOwned = player, X = x, Y = y, InstanceId = state.NextInstanceId++,
                ExpiresAtOwnerTurn = (player ? state.CompletedEnemyTurns : state.CompletedPlayerTurns) + remaining,
                DirectionX = kind == CoreNavalFieldKind.Tide ? -1 : 0 };
            state.Cells.Add(cell); return cell;
        }
        private static void Mutate(CoreNavalFieldState state, CoreNavalBossMove move, VoyagePoint enemy, VoyagePoint player, bool events) =>
            typeof(CoreVoyageSession).GetMethod("ApplyNavalBossFieldAction", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { state, move, enemy, player, events });
        private static string Cells(IEnumerable<CoreNavalFieldCell> cells) => string.Join(";", cells.OrderBy(c => c.X).ThenBy(c => c.Y)
            .Select(c => c.X + ":" + c.Y + ":" + c.Kind + ":" + c.PlayerOwned + ":" + c.InstanceId + ":" + c.ExpiresAtOwnerTurn + ":" + c.DirectionX + ":" + c.DirectionY));
        private static CoreVoyageSession Start(string id)
        {
            var session = new CoreVoyageSession(); session.ConfigureComponentBonuses(10000, -1000, 0, 0, 0); session.StartVoyage();
            var encounter = CoreRouteBossCatalog.Find(id)?.CreateEncounter() ?? CoreLegendaryWarships.Find(id)?.CreateEncounter() ?? CoreLegendaryWarships.CreateTreasureEncounter();
            Call(session, "StartBattle", encounter, true);
            session.Battle.Stage = NavalBattleStage.Naval; session.Battle.PhaseTurn = 2;
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 10000;
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, 10000);
            Call(session.Battle, "SetTacticalPose", true, new VoyagePoint(0, 0), 90f);
            Call(session.Battle, "SetTacticalPose", false, new VoyagePoint(400, 0), 90f);
            return session;
        }
        private static CoreNavalBossMove Move(CoreVoyageSession session)
        {
            object plan = Call(session, "BuildEnemyPlan");
            return (CoreNavalBossMove)plan.GetType().GetField("BossMove", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(plan);
        }
        private static object Call(object target, string name, params object[] args) => target.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
}
