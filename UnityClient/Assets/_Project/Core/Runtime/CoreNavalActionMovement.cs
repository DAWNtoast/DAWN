using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ShatteredSail.Core.Gameplay
{
    public readonly struct CoreNavalMovementNode
    {
        internal CoreNavalMovementNode(VoyagePoint position, float heading)
        { PositionMeters = position; HeadingDegrees = heading; }
        public VoyagePoint PositionMeters { get; }
        public float HeadingDegrees { get; }
    }

    internal static class CoreNavalActionPointRules
    {
        internal static bool IsMovementCard(CoreBattleCard card) => card != null &&
            (card.MovementMeters > 0 || card.Action == NavalBattleCardAction.TurnPort ||
             card.Action == NavalBattleCardAction.TurnStarboard || card.Action == NavalBattleCardAction.TurnAbout);

        internal static string ConvertDescription(string source, CoreBattleCard card)
        {
            if (!IsMovementCard(card)) return source;
            if (card.MovementMeters == 0 || card.Action == NavalBattleCardAction.SailForward)
                return "获得 {navalActionPoints} 点行动点。拖动规划航线；到达余量可用于接舷或冲撞，普通停船后清空。";
            string result = Regex.Replace(source, @"[^；。：:\r\n]*\{movement\}[^；。\r\n]*", "获得 {navalActionPoints} 点行动点");
            result = result.Replace("再转舵与推进以占据侧翼；", string.Empty)
                .Replace("朝敌舰航行时准备冲撞；", string.Empty)
                .Replace("朝敌舰航行时准备冲撞。", string.Empty)
                .Replace("平行且距离不超过 60 米可接舷。", "两舰平行且舷侧相邻（100 米）时，消耗 1 行动点接舷；同向或反向均可，首尾相邻不可接舷。");
            return result;
        }
    }

    public sealed partial class CoreBattleState
    {
        public int NavalActionPoints { get; internal set; } = 1;
        public int NavalArrivalActionPoints { get; internal set; }
        public bool NavalAwaitingHeadingSelection { get; internal set; }
    }

    public sealed partial class CoreVoyageSession
    {
        private readonly List<CoreNavalMovementNode> navalMovementPath = new List<CoreNavalMovementNode>();
        private CoreBattleState navalPathBattle;
        private int navalPathTurn = -1;
        private float navalArrivalHeading;
        public bool NavalActionMovementActive => Phase == CoreDemoPhase.Battle && Battle != null &&
            Battle.Stage != NavalBattleStage.Boarding;
        public IReadOnlyList<CoreNavalMovementNode> NavalMovementPath
        {
            get { EnsureNavalMovementPath(); return navalMovementPath.AsReadOnly(); }
        }
        public int NavalMovementPlannedSteps { get { EnsureNavalMovementPath(); return Math.Max(0, navalMovementPath.Count - 1); } }

        public bool NavalMovementEndsInRam
        {
            get
            {
                if (!NavalActionMovementActive) return false;
                EnsureNavalMovementPath();
                return navalMovementPath.Count > 1 &&
                    NavalMovementTargetsEnemy(navalMovementPath[navalMovementPath.Count - 1].PositionMeters);
            }
        }

        private bool NavalMovementTargetsEnemy(VoyagePoint point) => Battle != null &&
            NavalPointDistance(point, Battle.EnemyPositionMeters) < NavalContactTolerance;

        private void EnsureNavalMovementPath()
        {
            if (!NavalActionMovementActive)
            {
                navalMovementPath.Clear();
                navalPathBattle = null;
                navalPathTurn = -1;
                return;
            }
            bool changed = navalPathBattle != Battle || navalPathTurn != Battle.Turn;
            if (!changed && navalMovementPath.Count > 0 && !Battle.NavalAwaitingHeadingSelection)
                changed = NavalPointDistance(navalMovementPath[0].PositionMeters, Battle.PlayerPositionMeters) > .01f ||
                    Math.Abs(NavalAngleDelta(navalMovementPath[0].HeadingDegrees, Battle.PlayerHeadingDegrees)) > .01f;
            if (!changed && navalMovementPath.Count > 0) return;
            navalPathBattle = Battle;
            navalPathTurn = Battle.Turn;
            navalMovementPath.Clear();
            navalMovementPath.Add(new CoreNavalMovementNode(Battle.PlayerPositionMeters, Battle.PlayerHeadingDegrees));
        }

        public string NavalMovementStepUnavailableReason(VoyagePoint targetMeters)
        {
            if (!NavalActionMovementActive) return "当前不是行动点海战";
            if (Battle.NavalAwaitingHeadingSelection) return "请先确定到达朝向";
            EnsureNavalMovementPath();
            if (navalMovementPath.Count - 1 >= NavalActiveMovementLimit(true, Battle.NavalActionPoints)) return "行动点不足，每格消耗 1 点";
            return NavalMovementEdgeUnavailableReason(navalMovementPath[navalMovementPath.Count - 1],
                targetMeters, navalMovementPath.Count - 1);
        }

        private string NavalMovementEdgeUnavailableReason(CoreNavalMovementNode previous, VoyagePoint targetMeters, int precedingSteps)
        {
            string tutorialRestriction = GuidedBossMovementRestriction(targetMeters, precedingSteps);
            if (tutorialRestriction.Length > 0) return tutorialRestriction;
            if (float.IsNaN(targetMeters.X) || float.IsNaN(targetMeters.Y) ||
                float.IsInfinity(targetMeters.X) || float.IsInfinity(targetMeters.Y) ||
                !FieldCellValid(FieldGrid(targetMeters.X), FieldGrid(targetMeters.Y))) return "落点无效";
            float dx = targetMeters.X - previous.PositionMeters.X;
            float dy = targetMeters.Y - previous.PositionMeters.Y;
            bool horizontal = Math.Abs(dy) < .01f && Math.Abs(Math.Abs(dx) - NavalGridCellMeters) < .01f;
            bool vertical = Math.Abs(dx) < .01f && Math.Abs(Math.Abs(dy) - NavalGridCellMeters) < .01f;
            if (!horizontal && !vertical) return "请沿相邻的上、下、左、右海域逐格拖出航线";
            float heading = NavalBearing(previous.PositionMeters, targetMeters);
            float turn = Math.Abs(NavalAngleDelta(previous.HeadingDegrees, heading));
            if (precedingSteps == 0 && turn > .01f) return "第一格必须沿当前船头正前方航行";
            if (precedingSteps > 0 && turn > 90.01f) return "航行中只能直行或左右转，不能直接反向";
            if (NavalMovementTargetsEnemy(previous.PositionMeters))
                return "驶入敌舰海域将主动冲撞，航线必须在敌舰格结束";
            if (NavalMovementTargetsEnemy(targetMeters))
            {
                if (Battle.Stage != NavalBattleStage.Naval) return "仅炮战阶段可以驶入敌舰海域冲撞";
                if (IsGuidedBossBattle && !GuidedBossAllowsRam) return "请先完成当前教学动作";
                return string.Empty;
            }
            if (NavalSegmentDistance(previous.PositionMeters, targetMeters, Battle.EnemyPositionMeters) < MinimumNavalDistanceMeters - .01f)
                return "该格航线被敌舰占据";
            return string.Empty;
        }

        public bool TryAppendNavalMovementStep(VoyagePoint targetMeters, out string reason)
        {
            reason = NavalMovementStepUnavailableReason(targetMeters);
            if (reason.Length > 0) return false;
            VoyagePoint previous = navalMovementPath[navalMovementPath.Count - 1].PositionMeters;
            navalMovementPath.Add(new CoreNavalMovementNode(targetMeters, NavalBearing(previous, targetMeters)));
            return true;
        }

        public IReadOnlyList<VoyagePoint> GetNavalLegalNextSteps()
        {
            var result = new List<VoyagePoint>();
            if (!NavalActionMovementActive || Battle.NavalAwaitingHeadingSelection) return result.AsReadOnly();
            EnsureNavalMovementPath();
            CoreNavalMovementNode previous = navalMovementPath[navalMovementPath.Count - 1];
            foreach (float turn in new[] { 0f, 90f, -90f })
            {
                float heading = NormalizeNavalHeading(previous.HeadingDegrees + turn) * NavalDegreesToRadians;
                var point = new VoyagePoint(previous.PositionMeters.X + (float)Math.Round(Math.Cos(heading)) * NavalGridCellMeters,
                    previous.PositionMeters.Y + (float)Math.Round(Math.Sin(heading)) * NavalGridCellMeters);
                if (NavalMovementStepUnavailableReason(point).Length == 0) result.Add(point);
            }
            return result.AsReadOnly();
        }

        public bool UndoNavalMovementStep()
        {
            if (!NavalActionMovementActive || Battle.NavalAwaitingHeadingSelection) return false;
            EnsureNavalMovementPath();
            if (navalMovementPath.Count <= 1) return false;
            navalMovementPath.RemoveAt(navalMovementPath.Count - 1);
            return true;
        }

        public void ClearNavalMovementPath()
        {
            if (Battle != null && Battle.NavalAwaitingHeadingSelection) return;
            navalMovementPath.Clear();
            navalPathBattle = null;
            EnsureNavalMovementPath();
        }

        public bool CommitNavalMovement(out string reason) => CommitNavalMovement(out reason, out _);

        /// <summary>The final step into an enemy cell pays for a ram instead of ordinary sailing.</summary>
        public bool CommitNavalMovement(out string reason, out NavalBoardingConnection ramConnection)
        {
            ramConnection = NavalBoardingConnection.Planned;
            if (!NavalActionMovementActive) { reason = "当前不是行动点海战"; return false; }
            if (Battle.NavalAwaitingHeadingSelection) { reason = "请先确定到达朝向"; return false; }
            EnsureNavalMovementPath();
            if (navalMovementPath.Count <= 1) { reason = "请先点击己方船只，再拖动绘出航线"; return false; }
            if (navalMovementPath.Count - 1 > NavalActiveMovementLimit(true, Battle.NavalActionPoints)) { reason = "行动点不足"; return false; }
            reason = GuidedBossMovementCommitRestriction();
            if (reason.Length > 0) return false;
            // Cards may change the tactical situation while a route is selected. Validate
            // every edge against the current enemy pose before mutating any battle state.
            for (int index = 1; index < navalMovementPath.Count; index++)
            {
                reason = NavalMovementEdgeUnavailableReason(navalMovementPath[index - 1],
                    navalMovementPath[index].PositionMeters, index - 1);
                if (reason.Length > 0) return false;
            }
            CoreBattleState committedBattle = Battle;
            var frames = new List<CoreNavalTacticalFrame> { committedBattle.TacticalFrame };
            float initialRange = Battle.DistanceMeters;
            int steps = 0;
            bool ramAtDestination = false;
            float ramHeading = 0f;
            for (int index = 1; index < navalMovementPath.Count; index++)
            {
                CoreNavalMovementNode node = navalMovementPath[index];
                if (NavalMovementTargetsEnemy(node.PositionMeters))
                {
                    // Keep the adjacent contact pose for normal ram geometry, armor and
                    // impact presentation. Do not sail through or overlap the target.
                    ramAtDestination = true;
                    ramHeading = node.HeadingDegrees;
                    break;
                }
                TurnNavalShip(true, node.HeadingDegrees, frames);
                AdvanceNavalShip(true, NavalGridCellMeters, frames);
                steps++;
                FieldShip(true).MovementSlow = 0;
                bool interrupted = NavalFieldsEntered(true, true);
                if (!NavalActionMovementActive)
                {
                    // A lethal field clears Session.Battle. Retain only the route already
                    // executed on the captured result for its final movement presentation.
                    committedBattle.CommitTacticalMovement(frames);
                    reason = string.Empty;
                    return true;
                }
                if (interrupted) break;
            }
            if (steps > 0) NavalFieldsMovementEnded(true, frames);
            if (!NavalActionMovementActive)
            {
                committedBattle.CommitTacticalMovement(frames);
                reason = string.Empty;
                return true;
            }
            if (ramAtDestination && NavalPlayerContactGeometryReason(false, ramHeading).Length == 0)
            {
                // The route budget already includes its final one-point collision step.
                // Resolve all preceding movement/terrain first and discard unused points.
                Battle.NavalActionPoints = 0;
                ClearNavalArrivalContactState();
                TurnNavalShip(true, ramHeading, frames);
                FinishNavalMovement(frames, true, Battle.DistanceMeters < initialRange - .01f);
                ClearNavalMovementPath();
                ramConnection = NavalRamConnection(true);
                bool completed = ExecutePlayerRam();
                frames.Add(committedBattle.TacticalFrame);
                committedBattle.CommitTacticalMovement(frames);
                reason = string.Empty;
                return completed;
            }
            navalArrivalHeading = Battle.PlayerHeadingDegrees;
            Battle.NavalArrivalActionPoints = Battle.NavalActionPoints - steps;
            Battle.NavalActionPoints = 0;
            Battle.NavalAwaitingHeadingSelection = true;
            FinishNavalMovement(frames, true, Battle.DistanceMeters < initialRange - .01f);
            Battle.LastAction = "航行 " + steps + " 格（" + (steps * 100) + " 米）；请确定到达朝向。" +
                (Battle.NavalArrivalActionPoints > 0
                    ? "剩余 " + Battle.NavalArrivalActionPoints + " 点仅可用于本次到达接舷或冲撞，普通停船后清空。"
                    : "本次移动没有剩余行动点可用于接触。");
            reason = string.Empty;
            return true;
        }

        public IReadOnlyList<float> GetNavalArrivalHeadings()
        {
            if (!NavalActionMovementActive) return Array.Empty<float>();
            EnsureNavalMovementPath();
            float heading = Battle.NavalAwaitingHeadingSelection ? navalArrivalHeading :
                navalMovementPath[navalMovementPath.Count - 1].HeadingDegrees;
            return Array.AsReadOnly(new[] { NormalizeNavalHeading(heading), NormalizeNavalHeading(heading + 90f), NormalizeNavalHeading(heading - 90f) });
        }

        public bool ConfirmNavalArrivalHeading(float headingDegrees, out string reason)
        {
            if (!NavalActionMovementActive || !Battle.NavalAwaitingHeadingSelection)
            { reason = "当前没有等待确定的到达朝向"; return false; }
            if (float.IsNaN(headingDegrees) || float.IsInfinity(headingDegrees)) { reason = "朝向无效"; return false; }
            float heading = NormalizeNavalHeading(headingDegrees);
            if (!IsNavalArrivalHeadingAllowed(heading)) { reason = "到达后只能保持直行、左转或右转朝向，不能反向"; return false; }
            var frames = new List<CoreNavalTacticalFrame> { Battle.TacticalFrame };
            bool wasClosing = Battle.PlayerMovingTowardEnemy;
            TurnNavalShip(true, heading, frames);
            FinishNavalMovement(frames, true, wasClosing);
            ClearNavalArrivalContactState();
            ClearNavalMovementPath();
            Battle.LastAction = "到达朝向已确定；可继续出牌，获得行动点后可再次移动。";
            AdvanceGuidedBossMovement();
            reason = string.Empty;
            return true;
        }

        private bool ApplyNavalActionPointCard(CoreBattleCard card)
        {
            if (!NavalActionMovementActive || card.NavalActionPointGain <= 0) return false;
            Battle.NavalActionPoints = (int)Math.Min(int.MaxValue, (long)Battle.NavalActionPoints + card.NavalActionPointGain);
            return true;
        }

        private void BeginNavalActionPointTurn()
        {
            if (Battle == null) return;
            Battle.NavalActionPoints = Battle.Stage == NavalBattleStage.Boarding ? 0 : 1;
            ClearNavalArrivalContactState();
            navalMovementPath.Clear();
            navalPathBattle = null;
            if (NavalActionMovementActive)
            {
                Battle.MaxEnergy = 3;
                Battle.Energy = 3;
            }
        }

        private static float NavalPointDistance(VoyagePoint a, VoyagePoint b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private static float NavalSegmentDistance(VoyagePoint from, VoyagePoint to, VoyagePoint point)
        {
            float dx = to.X - from.X, dy = to.Y - from.Y;
            float squared = dx * dx + dy * dy;
            float t = squared <= .001f ? 0f : Math.Max(0f, Math.Min(1f,
                ((point.X - from.X) * dx + (point.Y - from.Y) * dy) / squared));
            return NavalPointDistance(point, new VoyagePoint(from.X + dx * t, from.Y + dy * t));
        }
    }
}



