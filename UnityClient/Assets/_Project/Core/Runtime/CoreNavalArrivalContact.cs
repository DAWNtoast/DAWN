using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        /// <summary>Checks an arrival contact without selecting a heading or spending its reserved budget.</summary>
        public bool CanNavalArrivalContact(bool boarding, out float heading, out string reason)
        {
            heading = 0f;
            if (Phase != CoreDemoPhase.Battle || Battle == null || Battle.Stage != NavalBattleStage.Naval)
                reason = "仅炮战阶段可以到达接触";
            else if (!Battle.NavalAwaitingHeadingSelection)
                reason = "当前没有等待确认的到达接触";
            else if (IsGuidedBossBattle && !(boarding ? GuidedBossAllowsBoard : GuidedBossAllowsRam))
                reason = "请先完成当前教学动作";
            else if (Hull <= 0 || Battle.EnemyHull <= 0)
                reason = "战斗已结束，不能到达接触";
            else if (Battle.NavalArrivalActionPoints < 1)
                reason = "到达接触需要本次移动剩余 1 行动点";
            else
            {
                heading = boarding ? Battle.EnemyHeadingDegrees :
                    NavalBearing(Battle.PlayerPositionMeters, Battle.EnemyPositionMeters);
                if (boarding && !IsNavalArrivalHeadingAllowed(heading))
                    heading = (heading + 180f) % 360f;
                reason = NavalPlayerContactGeometryReason(boarding, heading);
                if (reason.Length == 0 && !IsNavalArrivalHeadingAllowed(heading))
                    reason = "到达接触只能保持直行、左转或右转朝向，不能反向";
            }
            return reason.Length == 0;
        }

        /// <summary>Atomically selects the legal contact heading and settles one reserved action point.</summary>
        public bool TryNavalArrivalContact(bool boarding, out string reason)
        {
            if (!CanNavalArrivalContact(boarding, out float heading, out reason)) return false;

            // No fallible checks remain after this point. Do not refund the reserved
            // budget into ordinary AP or expose an intermediate free-action state.
            var frames = new List<CoreNavalTacticalFrame> { Battle.TacticalFrame };
            TurnNavalShip(true, heading, frames);
            FinishNavalMovement(frames, true, Battle.PlayerMovingTowardEnemy);
            Battle.NavalArrivalActionPoints -= 1;
            Battle.NavalActionPoints = 0;
            ClearNavalArrivalContactState();
            ClearNavalMovementPath();
            CoreBattleState arrivalBattle = Battle;
            bool completed = boarding ? ExecutePlayerBoarding() : ExecutePlayerRam();
            // Ram settlement closes the physical connection distance. Preserve the
            // entire final turn and connection in one snapshot for the presentation.
            frames.Add(arrivalBattle.TacticalFrame);
            arrivalBattle.CommitTacticalMovement(frames);
            return completed;
        }

        private bool IsNavalArrivalHeadingAllowed(float heading)
        {
            if (float.IsNaN(heading) || float.IsInfinity(heading)) return false;
            float turn = Math.Abs(NavalAngleDelta(navalArrivalHeading, heading));
            return turn < NavalContactTolerance || Math.Abs(turn - 90f) < NavalContactTolerance;
        }

        private string NavalPlayerContactGeometryReason(bool boarding, float playerHeading)
        {
            string action = boarding ? "接舷" : "冲撞";
            if (!NavalShipsAreOrthogonallyAdjacent)
                return action + "需要与敌舰上下左右相邻（100 米）";
            if (boarding)
            {
                if (!NavalHeadingsAreParallel(playerHeading, Battle.EnemyHeadingDegrees))
                    return "接舷需要两舰平行（同向或反向均可）";
                return NavalBoardingGeometryIsLegal(playerHeading) ? string.Empty :
                    "接舷需要两舰舷侧相邻，横向船身位于相邻两行，纵向船身位于相邻两列；首尾相邻不可接舷";
            }
            if (Math.Abs(NavalAngleDelta(playerHeading,
                NavalBearing(Battle.PlayerPositionMeters, Battle.EnemyPositionMeters))) >= NavalContactTolerance)
                return "冲撞需要船头指向相邻敌舰";
            return string.Empty;
        }

        private void ClearNavalArrivalContactState()
        {
            if (Battle == null) return;
            Battle.NavalArrivalActionPoints = 0;
            Battle.NavalAwaitingHeadingSelection = false;
        }
    }
}
