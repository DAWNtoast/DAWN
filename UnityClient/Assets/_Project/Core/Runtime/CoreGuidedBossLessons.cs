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
        public string GuidedBossGoalText => CurrentGuidedLessonStep?.Goal ?? string.Empty;
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
        public bool GuidedBossHasMovementTarget => IsGuidedBossBattle &&
            (GuidedBossStep == GuidedBossBattleStep.CourseGunneryMove || GuidedBossStep == GuidedBossBattleStep.CourseBoardMove ||
             GuidedBossStep == GuidedBossBattleStep.CourseRamMove);
        public string GuidedBossLessonTitle
        {
            get
            {
                switch (GuidedBossLesson)
                {
                    case GuidedBossLesson.NavalBasics: return "1 / 7 · 行动点与能量";
                    case GuidedBossLesson.Maneuver: return "2 / 7 · 拖动航线与到达朝向";
                    case GuidedBossLesson.Gunnery: return "3 / 7 · 准备、炮击与牌堆";
                    case GuidedBossLesson.BoardingContact: return "4 / 7 · 平行接舷演习";
                    case GuidedBossLesson.RamContact: return "5 / 7 · 船头冲撞演习";
                    case GuidedBossLesson.BoardingCards: return "6 / 7 · 正式接舷基础牌";
                    case GuidedBossLesson.FinalBattle: return "7 / 7 · 自由实战验收";
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
            if (GuidedBossLesson >= GuidedBossLesson.FinalBattle) { reason = "请在实战中取得胜利"; return false; }
            StartGuidedBossLesson(GuidedBossLesson + 1);
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
                string goal, string instruction, string card = "", string topic = "", GuidedBossBattleStep? next = null)
            { result.Add(id, new GuidedLessonStep { Lesson = lesson, Action = action, Goal = goal, Instruction = instruction,
                Card = card, Topic = topic, Next = next ?? id + 1 }); }
            void Done(GuidedBossBattleStep id, GuidedBossLesson lesson) => Add(id, lesson, GuidedBossActionKind.Continue,
                "本段演习完成", "点击开始下一段演习。此时才重新布置船位、牌序与临时补给；演习途中按真实回合结算。");
            var basics = GuidedBossLesson.NavalBasics;
            Add(GuidedBossBattleStep.CourseResources, basics, GuidedBossActionKind.Inspect, "查看行动点、能量与补给", "每回合开始有1行动点与3能量。行动点用于航行或接触，能量用于出牌。查看资源后，先用初始行动点航行。", topic: "resources", next: GuidedBossBattleStep.CourseCardInspect);
            Add(GuidedBossBattleStep.CourseCardInspect, basics, GuidedBossActionKind.Inspect, "悬停阅读迎敌推进", "查看左上角0能量费用和牌面效果；迎敌推进获得1行动点，出牌不会直接航行。", "card_approach", "card", GuidedBossBattleStep.CourseFreeMove);
            Add(GuidedBossBattleStep.CourseFreeMove, basics, GuidedBossActionKind.Movement, "用初始行动点航行1格", "不用出牌，先点击己方船只查看可达区域，再拖到船头正前方1格并确认航线；到达后选合法终向，普通停船会清空剩余行动点。");
            Add(GuidedBossBattleStep.CourseActionCard, basics, GuidedBossActionKind.Card, "打出迎敌推进，获得1行动点", "迎敌推进基础费用为0，获得1行动点，不会直接移动或旋转船只。", "card_approach");
            Add(GuidedBossBattleStep.CourseSecondMove, basics, GuidedBossActionKind.Movement, "再次航行1格并确认朝向", "点击己方船只，沿现在的船头拖出1格航线并确认。玩家可在同一回合交替出牌与移动；到达定向前不能出牌或结束回合。");
            Done(GuidedBossBattleStep.CourseBasicsDone, basics);
            Add(GuidedBossBattleStep.CourseManeuver, GuidedBossLesson.Maneuver, GuidedBossActionKind.Movement,
                "拖出至少2格、包含转弯的航线", "本段演习准备3行动点。点击己方船只查看可达区域，按住拖出至少2格且含一次转弯的路线；第一格沿船头，随后只可直行或左右转，不可反向。拖回已选格可撤回，确认航线后抵达并任选合法终向。");
            Done(GuidedBossBattleStep.CourseManeuverDone, GuidedBossLesson.Maneuver);
            var gun = GuidedBossLesson.Gunnery;
            Add(GuidedBossBattleStep.CourseLoad, gun, GuidedBossActionKind.Card, "准备阶段装填弹药", "本段船体预置8点损伤供修理练习。装填消耗1能量、1火药，增加下一次炮击伤害并抽1张牌。", "card_load_shot");
            Add(GuidedBossBattleStep.CoursePrepareGuard, gun, GuidedBossActionKind.Card, "打出稳固船身", "这张牌基础费用为0，增加2格挡；准备结束后装填与格挡保留到首轮炮战。", "card_brace");
            Add(GuidedBossBattleStep.CoursePrepareEnd, gun, GuidedBossActionKind.EndTurn, "结束准备阶段", "准备阶段不受攻击，结束后进入炮战，能量、行动点与同名牌费用按新回合恢复。");
            Add(GuidedBossBattleStep.CourseRangeAttempt, gun, GuidedBossActionKind.BlockedCard, "尝试打出射程外的链弹", "当前相距6格，链弹射程4格，按横向与纵向格数相加计算。尝试失败不会扣能量、火药、炮弹或手牌。", "card_chain_shot");
            Add(GuidedBossBattleStep.CourseApproach1, gun, GuidedBossActionKind.Card, "打出第一张迎敌推进", "第一张消耗0能量，行动点从1增至2。", "card_approach");
            Add(GuidedBossBattleStep.CourseApproach2, gun, GuidedBossActionKind.Card, "再打出同名迎敌推进", "同名牌本回合每成功使用一次，后续费用加1；第二张需要1能量，行动点增至3。", "card_approach");
            Add(GuidedBossBattleStep.CourseEnergy, gun, GuidedBossActionKind.Card, "用整顿号令恢复能量", "整顿号令基础费用0，恢复1能量；它不增加航行行动点。", "card_energy_order");
            Add(GuidedBossBattleStep.CourseGunneryMove, gun, GuidedBossActionKind.Movement, "航行进入4格射程", "点击己方船只，用3行动点拖出航线，让横向与纵向距离合计不超过4格。到达后选择合法船头方向，普通停船会清空剩余行动点，仍可用剩余能量开炮。");
            Add(GuidedBossBattleStep.CourseFire, gun, GuidedBossActionKind.Card, "打出装填后的链弹", "炮击同时消耗能量、火药和炮弹，并消耗装填增益。查看敌舰船体与自己的补给变化。", "card_chain_shot");
            Add(GuidedBossBattleStep.CourseNavalGuard, gun, GuidedBossActionKind.Card, "为敌方回合补充格挡", "格挡吸收装甲减伤后的伤害。未吸收的格挡会在正常回合边界清除。", "card_brace");
            Add(GuidedBossBattleStep.CourseEnemyInspect, gun, GuidedBossActionKind.Inspect, "查看敌方意图", "敌意图显示装甲减伤后、格挡吸收前的伤害。查看后结束回合，比较实际承伤。", topic: "enemy");
            Add(GuidedBossBattleStep.CourseEnemyTurn, gun, GuidedBossActionKind.EndTurn, "让敌方执行当前意图", "这是正常敌方回合：实际伤害、弃牌、抽牌、资源恢复与费用重置均按正式规则处理。");
            Add(GuidedBossBattleStep.CourseRepair, gun, GuidedBossActionKind.Card, "用木板修理船体", "紧急修理消耗1能量；有木板时修复5船体并消耗1木板，无木板时只修复2。", "card_emergency_repair");
            Add(GuidedBossBattleStep.CourseDeckInspect, gun, GuidedBossActionKind.Inspect, "打开抽牌堆与弃牌堆", "手牌上限8张。手牌打出后进入弃牌堆，回合结束会弃掉剩余手牌。抽牌堆为空时会将弃牌堆洗回。", topic: "deck");
            Add(GuidedBossBattleStep.CourseDraw, gun, GuidedBossActionKind.Card, "使用战术筹划触发真实洗牌", "当前抽牌堆已空。战术筹划进入弃牌堆后抽2张，系统会洗回弃牌堆；同名费用记录不会因洗牌消失。", "card_tactical_draw");
            Done(GuidedBossBattleStep.CourseGunneryDone, gun);
            var board = GuidedBossLesson.BoardingContact;
            Add(GuidedBossBattleStep.CourseBoardMove, board, GuidedBossActionKind.Movement, "航行到敌舰左侧相邻格", "敌舰朝北时，航行到它左侧的相邻格，到达后朝北或朝南可让两舰平行、舷侧相邻。请按当前船头规划合法航线；普通停船后清空剩余行动点。");
            Add(GuidedBossBattleStep.CourseBoardAction, board, GuidedBossActionKind.Card, "重新获得接舷所需行动点", "接舷需要1行动点，不耗能量。先出迎敌推进重新获得行动点。", "card_approach");
            Add(GuidedBossBattleStep.CourseBoardContact, board, GuidedBossActionKind.FreePlay, "已有1行动点，直接平行接舷", "本段两舰朝北，位于左右相邻的两列，相距100米，已有1行动点可直接接舷。接舷要求两舰平行且舷侧相邻，同向或反向均可；横向船身需位于相邻两行，纵向船身需位于相邻两列，首尾相邻不可接舷。若自行驶离，请重新调整位置与朝向。");
            Done(GuidedBossBattleStep.CourseBoardDone, board);
            var ram = GuidedBossLesson.RamContact;
            Add(GuidedBossBattleStep.CourseRamMove, ram, GuidedBossActionKind.Movement, "航行到敌舰左侧相邻格", "本段敌舰朝北，我舰朝东。点击己方船只，向前拖出1格航线并确认；到达后朝东可指向敌舰侧舷，两船船头互相垂直。");
            Add(GuidedBossBattleStep.CourseRamAction, ram, GuidedBossActionKind.Card, "重新获得冲撞所需行动点", "本段已用1行动点航行1格并普通停船，没有接触余量。先出迎敌推进，冲撞本身只扣1行动点。", "card_approach");
            Add(GuidedBossBattleStep.CourseRamContact, ram, GuidedBossActionKind.FreePlay, "船头指向相邻敌舰，发动冲撞", "冲撞需要我舰船头指向上下左右相邻的敌舰，可撞击敌舰舰艏、舰尾或左右侧舷。本段练习从左侧撞击敌舰侧舷；冲撞造成双方船体伤害，非致命撞击会连接船只并进入接舷。");
            Done(GuidedBossBattleStep.CourseRamDone, ram);
            var crew = GuidedBossLesson.BoardingCards;
            Add(GuidedBossBattleStep.CourseBoardingAttack, crew, GuidedBossActionKind.Card, "用水手刺击进攻", "船员预置15点伤势供治疗练习。水手刺击消耗1能量，基础伤害8；士气每点提供3%暴击率，暴击造成双倍伤害。", "card_crew_thrust");
            Add(GuidedBossBattleStep.CourseBoardingGuard, crew, GuidedBossActionKind.Card, "用盾阵掩护防御", "盾阵掩护消耗1能量，获得4格挡；接舷格挡与船体格挡独立。", "card_crew_guard");
            Add(GuidedBossBattleStep.CourseBoardingEnd1, crew, GuidedBossActionKind.EndTurn, "观察敌方攻击与换手牌", "结束回合，观察格挡吸收和船员生命，再使用下一手中的治疗与抽牌。");
            Add(GuidedBossBattleStep.CourseBandage, crew, GuidedBossActionKind.Card, "使用战地包扎", "正式战地包扎消耗1能量与1药品，恢复6生命；无药品时恢复2。", "card_crew_bandage");
            Add(GuidedBossBattleStep.CourseBoardingDraw, crew, GuidedBossActionKind.Card, "使用接舷战术筹划", "接舷版本消耗2能量、抽3张；它与海战版本的1能量抽2张不同。", "card_tactical_draw");
            Add(GuidedBossBattleStep.CourseBoardingEnd2, crew, GuidedBossActionKind.EndTurn, "换到持续效果手牌", "本回合能量已用完。正常结束回合并抽下一手，不会重置牌堆。");
            Add(GuidedBossBattleStep.CourseAid, crew, GuidedBossActionKind.Card, "布置持续救护", "消耗2能量，施放时用1药品，获得5层持续恢复；每次己方回合结束恢复当前层数的生命，然后减少1层。无药品时获得2层。", "card_boarding_basic_07");
            Add(GuidedBossBattleStep.CourseAmbush, crew, GuidedBossActionKind.Card, "布置伏击预令", "消耗1能量，本次不直接伤害；下一己方回合开始造成12伤害，受格挡、不暴击。", "card_boarding_basic_13");
            Add(GuidedBossBattleStep.CourseBoardingEnd3, crew, GuidedBossActionKind.EndTurn, "结算第一次救护与延迟伏击", "结束时先结算本回合救护；敌方行动后，下一己方回合开始触发伏击。");
            Add(GuidedBossBattleStep.CourseEffectsInspect, crew, GuidedBossActionKind.Inspect, "查看生命、敌方受伤与持续效果", "伏击已经结算，持续恢复由5层降为4层；下次己方回合结束会恢复4生命。查看资源与战斗记录后继续。", topic: "resources");
            Add(GuidedBossBattleStep.CourseBoardingEnd4, crew, GuidedBossActionKind.EndTurn, "观察持续恢复逐回合递减", "再结束一回合，恢复4生命后剩3层；之后仍会逐回合恢复并减层，药品不会再次扣除。");
            Done(GuidedBossBattleStep.CourseBoardingDone, crew);
            Add(GuidedBossBattleStep.CourseFinal, GuidedBossLesson.FinalBattle, GuidedBossActionKind.FreePlay,
                "自由实战：击败首领船体或完成接舷胜利", "本段解除教学操作锁，按正式伤害、装甲、牌组循环与接舷规则战斗。到达时若本次移动还剩至少1行动点，可在合法终向下直接接舷或冲撞；普通停船后余量清空。只有这段真正胜利才发放首领奖励；失败按正常流程返港整备，不可中途重置。最终胜负均恢复入场个人补给。");
            Add(GuidedBossBattleStep.CourseRetry, GuidedBossLesson.None, GuidedBossActionKind.Continue,
                "本段演习失败，请重新开始", "重试不会发放奖励或消耗个人库存。");
            return result;
        }
    }
}
