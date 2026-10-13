using ShatteredSail.Core.Gameplay;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private bool HasGuidedBossPresentation => tutorialActive && session != null && session.IsGuidedBossBattle;
        private GuidedBossBattleStep GuidedBossPresentationStep => HasGuidedBossPresentation
            ? session.GuidedBossStep : GuidedBossBattleStep.None;

        private string GuidedBossTargetControl
        {
            get
            {
                if (!HasGuidedBossPresentation) return string.Empty;
                switch (session.GuidedBossExpectedAction)
                {
                    case GuidedBossActionKind.EndTurn: return "End Battle Turn";
                    case GuidedBossActionKind.Ram: return "Battle Ram";
                    case GuidedBossActionKind.Card:
                    case GuidedBossActionKind.BlockedCard:
                        for (int index = 0; index < session.Battle.Hand.Count; index++)
                            if (session.GetBattleCard(session.Battle.Hand[index]).Id == session.GuidedBossExpectedCardId)
                                return "Battle Card " + index;
                        break;
                }
                return string.Empty;
            }
        }

        private bool IsGuidedBossBlockedCardControl(string name) => HasGuidedBossPresentation &&
            session.GuidedBossExpectedAction == GuidedBossActionKind.BlockedCard && name == GuidedBossTargetControl;

        // Invoked before the ordinary CanPlayCard gate. The two deliberately
        // unavailable teaching cards are observed, never played or animated.
        // The real Core API verifies both the exact step/card and its failure.
        private bool TryHandleGuidedBossCardAttempt(Button source, int handIndex, CoreBattleCard battleCard)
        {
            if (!HasGuidedBossPresentation || session.GuidedBossExpectedAction != GuidedBossActionKind.BlockedCard)
                return false;
            if (!isActiveAndEnabled || profileSaveBlocked || battleAnimationPlaying || source == null ||
                !source.gameObject.activeInHierarchy || !source.IsInteractable() ||
                retainedBattleRoot == null || !source.transform.IsChildOf(retainedBattleRoot) ||
                handIndex < 0 || handIndex >= session.Battle.Hand.Count || battleCard == null ||
                battleCard.Id != session.GuidedBossExpectedCardId || source.name != GuidedBossTargetControl ||
                session.GetBattleCard(session.Battle.Hand[handIndex]).Id != battleCard.Id)
                return true;
            if (session.TryObserveGuidedBossBlockedCard(handIndex, out _)) Render();
            return true;
        }

        private string GuidedBossObjective
        {
            get
            {
                GuidedBossBattleStep step = session.GuidedBossStep;
                if (step >= GuidedBossBattleStep.BoardingAttack1)
                {
                    int turn = step <= GuidedBossBattleStep.BoardingEndTurn1 ? 1 :
                        step <= GuidedBossBattleStep.BoardingEndTurn2 ? 2 : 3;
                    return "首领教学 · 接舷第 " + turn + " 回合";
                }
                int navalTurn = step <= GuidedBossBattleStep.NavalEndTurn1 ? 1 :
                    step <= GuidedBossBattleStep.NavalEndTurn2 ? 2 : 3;
                return "首领教学 · 炮战第 " + navalTurn + " 回合";
            }
        }

        private string GuidedBossInstruction
        {
            get
            {
                switch (session.GuidedBossStep)
                {
                    case GuidedBossBattleStep.NavalRetreat:
                        return "悬停可放大查看卡牌。点击红框中的「拉开距离」，远离敌舰并获得格挡。";
                    case GuidedBossBattleStep.NavalLoad:
                        return "打出「装填弹药」：消耗 1 火药额外装药，使下一炮增伤；开炮另需 1 火药和 1 炮弹。";
                    case GuidedBossBattleStep.NavalGuard:
                        return "打出「稳固船身」，为敌方回合准备格挡。";
                    case GuidedBossBattleStep.NavalEndTurn1:
                        return "选择「结束回合」，观察首领获得格挡并靠近。";
                    case GuidedBossBattleStep.NavalRangeAttempt:
                        return "点击红框中的「链弹」，查看射程限制；本次尝试不会消耗卡牌或弹药。";
                    case GuidedBossBattleStep.NavalApproach2:
                        return "刚才距离过远，炮击未发出，也未消耗卡牌或弹药。打出「迎敌推进」进入射程。";
                    case GuidedBossBattleStep.NavalFire:
                        return "打出「链弹」：消耗 1 火药和 1 炮弹。观察装填增伤与敌方格挡。";
                    case GuidedBossBattleStep.NavalGuard2:
                        return "打出「稳固船身」，准备抵挡首领接下来的炮击。";
                    case GuidedBossBattleStep.NavalEndTurn2:
                        return "选择「结束回合」，观察首领靠近后开炮，以及格挡如何抵消伤害。";
                    case GuidedBossBattleStep.NavalLoadAttempt:
                        return "点击红框中的「装填弹药」，查看火药不足的提示；本次尝试不消耗行动点。";
                    case GuidedBossBattleStep.NavalCircle:
                        return "火药不足，装填未执行，也未消耗行动点。打出「绕至侧舷」，对准敌舰侧面。";
                    case GuidedBossBattleStep.NavalApproach3:
                        return "打出「迎敌推进」。\n在 30 米处准备冲撞。";
                    case GuidedBossBattleStep.NavalRam:
                        return "相距 30 米，保留冲撞空间。\n点击「冲撞并接舷」。";
                    case GuidedBossBattleStep.BoardingAttack1:
                        return "打出「水手刺击」攻击守备队。接舷阶段扣除的是船员生命。";
                    case GuidedBossBattleStep.BoardingGuard1:
                        return "打出「盾阵掩护」，用格挡减少接下来的近战伤害。";
                    case GuidedBossBattleStep.BoardingEndTurn1:
                        return "选择「结束回合」，观察敌方攻击与剩余船员生命。";
                    case GuidedBossBattleStep.BoardingHeal:
                        return "船员已经受伤。打出「战地包扎」，恢复生命。";
                    case GuidedBossBattleStep.BoardingAttack2:
                        return "打出「水手刺击」，继续削减守备队生命。";
                    case GuidedBossBattleStep.BoardingGuard2:
                        return "打出「盾阵掩护」，为本回合结束后的反击做好防御。";
                    case GuidedBossBattleStep.BoardingEndTurn2:
                        return "选择「结束回合」，准备最后一次近战攻击。";
                    case GuidedBossBattleStep.BoardingAttack3:
                        return "打出「水手刺击」击败守备队，随后返航保存战利品。";
                    default:
                        return "等待当前动作完成，继续跟随红框操作。";
                }
            }
        }
    }
}
