using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public enum GuidedBossLesson { None, NavalBasics, Maneuver, Gunnery, BoardingContact, RamContact, BoardingCards, FinalBattle }

    public sealed partial class CoreVoyageSession
    {
        private sealed class GuidedLessonStep
        {
            internal GuidedBossLesson Lesson;
            internal GuidedBossActionKind Action;
            internal string Card, Goal, Instruction, Topic;
            internal GuidedBossBattleStep Next;
        }

        private static readonly Dictionary<GuidedBossBattleStep, GuidedLessonStep> guidedLessonSteps = CreateGuidedLessonSteps();
        private GuidedLessonStep CurrentGuidedLessonStep => guidedLessonSteps.TryGetValue(GuidedBossStep, out GuidedLessonStep step) ? step : null;
        public GuidedBossLesson GuidedBossLesson { get; private set; }
        public bool GuidedBossExerciseFailed { get; private set; }
        public int GuidedBossDeckShuffles { get; private set; }
        public bool GuidedBossCanContinue => IsGuidedBossBattle && GuidedBossExpectedAction == GuidedBossActionKind.Continue;
        public bool GuidedBossCanRestart => IsGuidedBossBattle && GuidedBossLesson != GuidedBossLesson.FinalBattle;
        public string GuidedBossGoalText => GuidedBossRequiresEastArrivalHeading && Battle.NavalAwaitingHeadingSelection
            ? "选择朝东，让船头指向敌舰" : CurrentGuidedLessonStep?.Goal ?? string.Empty;
        public string GuidedBossInstruction => GuidedBossExerciseFailed
            ? "本段演习未完成。开始重试会重新布置本段船位、牌序和临时补给，不发放战利品。"
            : CurrentGuidedLessonStep?.Instruction ?? string.Empty;
        public string GuidedBossInspectionTopic => CurrentGuidedLessonStep?.Topic ?? string.Empty;
        public bool GuidedBossAllowsMovement => IsGuidedBossBattle &&
            (GuidedBossExpectedAction == GuidedBossActionKind.Movement || GuidedBossExpectedAction == GuidedBossActionKind.FreePlay);
        public bool GuidedBossAllowsEndTurn => IsGuidedBossBattle &&
            (GuidedBossExpectedAction == GuidedBossActionKind.EndTurn || GuidedBossExpectedAction == GuidedBossActionKind.FreePlay);
        public bool GuidedBossAllowsBoard => IsGuidedBossBattle && GuidedBossExpectedAction == GuidedBossActionKind.FreePlay &&
            (GuidedBossLesson == GuidedBossLesson.BoardingContact || GuidedBossLesson == GuidedBossLesson.FinalBattle);
        public bool GuidedBossAllowsRam => IsGuidedBossBattle && GuidedBossExpectedAction == GuidedBossActionKind.FreePlay &&
            (GuidedBossLesson == GuidedBossLesson.RamContact || GuidedBossLesson == GuidedBossLesson.FinalBattle);
        public bool GuidedBossHasMovementTarget => IsGuidedBossBattle && GuidedBossStep == GuidedBossBattleStep.CourseRamMove;
        public bool GuidedBossRequiresEastArrivalHeading => IsGuidedBossBattle && GuidedBossStep == GuidedBossBattleStep.CourseRamMove;
        public string GuidedBossLessonTitle
        {
            get
            {
                switch (GuidedBossLesson)
                {
                    case GuidedBossLesson.NavalBasics: return "1 / 3 · 行动点与同名卡费用";
                    case GuidedBossLesson.BoardingContact: return "2 / 3 · 接舷";
                    case GuidedBossLesson.RamContact: return "3 / 3 · 冲撞";
                    default: return string.Empty;
                }
            }
        }

        public bool TryInspectGuidedBoss(string topic, out string reason)
        {
            reason = string.Empty;
            if (!IsGuidedBossBattle || GuidedBossExpectedAction != GuidedBossActionKind.Inspect ||
                !string.Equals(topic, GuidedBossInspectionTopic, StringComparison.Ordinal))
            { reason = "当前没有等待这项检查"; return false; }
            GuidedBossStep = CurrentGuidedLessonStep.Next;
            return true;
        }

        public bool TryContinueGuidedBossLesson(out string reason)
        {
            reason = string.Empty;
            if (!GuidedBossCanContinue) { reason = "请先完成本段演习目标"; return false; }
            if (GuidedBossExerciseFailed) return TryRestartGuidedBossLesson(out reason);
            // Keep the legacy enum values stable; retired lessons must never be
            // reached by incrementing the enum when a short lesson finishes.
            GuidedBossLesson next;
            switch (GuidedBossLesson)
            {
                case GuidedBossLesson.NavalBasics: next = GuidedBossLesson.BoardingContact; break;
                case GuidedBossLesson.BoardingContact: next = GuidedBossLesson.RamContact; break;
                case GuidedBossLesson.RamContact: next = GuidedBossLesson.FinalBattle; break;
                default: reason = "请在实战中取得胜利"; return false;
            }
            StartGuidedBossLesson(next);
            return true;
        }

        public bool TryRestartGuidedBossLesson(out string reason)
        {
            reason = string.Empty;
            if (!GuidedBossCanRestart) { reason = "正式实战不能重置演习，请按正常战斗完成或失败后整备"; return false; }
            StartGuidedBossLesson(GuidedBossLesson);
            return true;
        }

        private static Dictionary<GuidedBossBattleStep, GuidedLessonStep> CreateGuidedLessonSteps()
        {
            var result = new Dictionary<GuidedBossBattleStep, GuidedLessonStep>();
            void Add(GuidedBossBattleStep id, GuidedBossLesson lesson, GuidedBossActionKind action,
                string goal, string instruction, GuidedBossBattleStep next, string card = "")
            {
                result.Add(id, new GuidedLessonStep { Lesson = lesson, Action = action, Goal = goal,
                    Instruction = instruction, Card = card, Topic = string.Empty, Next = next });
            }
            void Done(GuidedBossBattleStep id, GuidedBossLesson lesson, string instruction) =>
                Add(id, lesson, GuidedBossActionKind.Continue, "本段演习完成", instruction, id);

            var basics = GuidedBossLesson.NavalBasics;
            Add(GuidedBossBattleStep.CourseFreeMove, basics, GuidedBossActionKind.Movement,
                "航行后，行动点会清空",
                "本段有3行动点。点击己舰，向船头前方拖出1格航线，确认航行并选择朝向；普通停船后，剩余行动点也会清空。",
                GuidedBossBattleStep.CourseApproach1);
            Add(GuidedBossBattleStep.CourseApproach1, basics, GuidedBossActionKind.Card,
                "打出第一张迎敌推进",
                "行动点已清空。打出迎敌推进，获得1行动点；首次使用费用为0。",
                GuidedBossBattleStep.CourseApproach2, "card_approach");
            Add(GuidedBossBattleStep.CourseApproach2, basics, GuidedBossActionKind.Card,
                "再次使用同名卡，费用增加",
                "同名卡本回合每成功使用一次，后续费用加1，所有副本共享。再打出迎敌推进，费用为1；下回合或切换阶段时重置。",
                GuidedBossBattleStep.CourseSecondMove, "card_approach");
            Add(GuidedBossBattleStep.CourseSecondMove, basics, GuidedBossActionKind.Movement,
                "用获得的2行动点再航行一次",
                "已获得2行动点。点击己舰，拖出1至2格航线，确认航行并选择停船朝向。",
                GuidedBossBattleStep.CourseBasicsDone);
            Done(GuidedBossBattleStep.CourseBasicsDone, basics, "航行停船后行动点清空；同名卡再次使用费用增加。接下来练习接舷。");

            var board = GuidedBossLesson.BoardingContact;
            Add(GuidedBossBattleStep.CourseBoardContact, board, GuidedBossActionKind.FreePlay,
                "两舰平行、舷侧相邻时，点击接舷",
                "本段两舰朝北、舷侧相邻，已有1行动点，点击「接舷」。接舷要求两舰平行且舷侧相邻，同向或反向均可；消耗1行动点，不消耗能量。",
                GuidedBossBattleStep.CourseBoardDone);
            Done(GuidedBossBattleStep.CourseBoardDone, board, "已完成接舷。接下来练习冲撞。");

            var ram = GuidedBossLesson.RamContact;
            Add(GuidedBossBattleStep.CourseRamMove, ram, GuidedBossActionKind.Movement,
                "靠近敌舰，到达后选择朝东",
                "向前拖出1格航线并确认，到达后选择朝东，让我舰船头指向相邻敌舰。",
                GuidedBossBattleStep.CourseRamAction);
            Add(GuidedBossBattleStep.CourseRamAction, ram, GuidedBossActionKind.Card,
                "获得冲撞所需的1行动点",
                "普通停船后行动点已清空。打出迎敌推进，获得冲撞所需的1行动点。",
                GuidedBossBattleStep.CourseRamContact, "card_approach");
            Add(GuidedBossBattleStep.CourseRamContact, ram, GuidedBossActionKind.FreePlay,
                "船头指向相邻敌舰，点击冲撞",
                "点击「冲撞」，消耗1行动点，不消耗能量。船头须指向上下左右相邻的敌舰，可撞舰艏、舰尾或侧舷；双方受到撞击伤害，存活则进入接舷。也可将航线终点拖入敌舰所在格并确认冲撞。",
                GuidedBossBattleStep.CourseRamDone);
            Done(GuidedBossBattleStep.CourseRamDone, ram, "已完成冲撞。点击「开始战斗」自由作战。");

            // Final combat retains ordinary outcome and supply handling, with no
            // further authored teaching steps or instructional overlay.
            Add(GuidedBossBattleStep.CourseFinal, GuidedBossLesson.FinalBattle, GuidedBossActionKind.FreePlay,
                string.Empty, string.Empty, GuidedBossBattleStep.CourseFinal);
            Add(GuidedBossBattleStep.CourseRetry, GuidedBossLesson.None, GuidedBossActionKind.Continue,
                "本段演习失败，请重新开始", "重试本段演习。", GuidedBossBattleStep.CourseRetry);
            return result;
        }
    }
}
