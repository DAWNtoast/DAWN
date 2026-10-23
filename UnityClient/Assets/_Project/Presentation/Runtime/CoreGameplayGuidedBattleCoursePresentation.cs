using System;
using System.Collections.Generic;
using System.Text;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private CoreVoyageSession guidedCourseSession;
        private GuidedBossLesson guidedCourseLesson;
        private GuidedBossBattleStep guidedCourseRenderedStep;
        private string guidedCoursePreviousGoal, guidedCourseFeedback;

        private void BuildGuidedBattleCoursePresentation()
        {
            if (retainedBattleRoot == null) return;
            if (!ReferenceEquals(guidedCourseSession, session) || guidedCourseLesson != session.GuidedBossLesson)
            {
                guidedCourseSession = session;
                guidedCourseLesson = session.GuidedBossLesson;
                guidedCourseRenderedStep = session.GuidedBossStep;
                guidedCoursePreviousGoal = session.GuidedBossGoalText;
                guidedCourseFeedback = "随时悬停读卡，或查看资源、敌舰和牌堆。";
            }
            else if (guidedCourseRenderedStep != session.GuidedBossStep)
            {
                guidedCourseFeedback = "已完成 · " + guidedCoursePreviousGoal;
                guidedCourseRenderedStep = session.GuidedBossStep;
                guidedCoursePreviousGoal = session.GuidedBossGoalText;
            }
            if (session.GuidedBossExerciseFailed) guidedCourseFeedback = "演习未完成 · 可重试本段，不发放奖励。";

            const float width = 1060f, padding = 16f;
            RectTransform banner = CreateRect("Guided Tutorial Banner", retainedBattleRoot,
                new Vector2(width, 132f), new Vector2(0f, 446f));
            banner.pivot = new Vector2(.5f, 1f);
            guidedDecorations.Add(banner.gameObject);
            Image backing = banner.gameObject.AddComponent<Image>();
            MarketShopSkin.ApplyPanel(backing);
            backing.raycastTarget = false;
            Text objective = CourseText("Guided Tutorial Objective", banner, width - padding * 2f, 23,
                session.GuidedBossLessonTitle + "  ·  " + session.GuidedBossGoalText, FontStyle.Bold);
            float top = 10f;
            PositionGuidedReadingText(objective, padding, top, Mathf.Max(28f, objective.preferredHeight));
            top += objective.rectTransform.sizeDelta.y + 5f;
            Text instruction = CourseText("Guided Tutorial Instruction", banner, width - padding * 2f, 20,
                GuidedBossInstruction, FontStyle.Normal);
            float bodyHeight = Mathf.Max(26f, Mathf.Ceil(instruction.preferredHeight));
            PositionGuidedReadingText(instruction, padding, top, bodyHeight);
            top += bodyHeight + 8f;
            Text feedback = CourseText("Guided Battle Feedback", banner, 430f, 15, guidedCourseFeedback, FontStyle.Normal);
            feedback.color = new Color(.22f, .39f, .28f);
            PositionGuidedReadingText(feedback, padding, top, Mathf.Max(34f, feedback.preferredHeight));
            float rowY = -top - 17f;
            CourseButton("Guided Battle Inspect Resources", "查看资源", banner, 96f, new Vector2(-6f, rowY),
                () => OpenGuidedBattleInformation("resources"));
            CourseButton("Guided Battle Inspect Enemy", "查看敌舰", banner, 96f, new Vector2(98f, rowY),
                () => OpenGuidedBattleInformation("enemy"));
            CourseButton("Guided Battle Inspect Deck", "牌堆", banner, 72f, new Vector2(190f, rowY), OpenBattleDeckOverview);
            if (session.GuidedBossCanRestart)
                CourseButton("Guided Battle Retry", "重试本段", banner, 120f, new Vector2(294f, rowY), () => ContinueGuidedBattleCourse(true));
            if (session.GuidedBossCanContinue && !session.GuidedBossExerciseFailed)
                CourseButton("Guided Battle Continue", "继续课程", banner, 144f, new Vector2(434f, rowY), () => ContinueGuidedBattleCourse(false));
            banner.sizeDelta = new Vector2(width, top + Mathf.Max(34f, feedback.preferredHeight) + 10f);
            if (session.GuidedBossCanContinue || session.GuidedBossCanRestart)
            {
                Text reset = CourseText("Guided Battle Exercise Reset Notice", banner, width - padding * 2f, 14,
                    "继续课程或重试本段会重新布置演习场地、牌序与临时补给；章节内按实际回合结算。", FontStyle.Normal);
                float noteTop = banner.sizeDelta.y - 2f;
                PositionGuidedReadingText(reset, padding, noteTop, Mathf.Max(20f, reset.preferredHeight));
                banner.sizeDelta += new Vector2(0f, reset.rectTransform.sizeDelta.y + 6f);
            }
            ApplyGuidedBattleCourseViewport(banner);
        }

        private Text CourseText(string name, Transform parent, float width, int size, string value, FontStyle style)
        {
            Text text = CreateText(name, parent, new Vector2(width, 30f), Vector2.zero,
                size, style, TextAnchor.UpperLeft, MarketShopSkin.Ink);
            text.text = value;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = false;
            text.raycastTarget = false;
            return text;
        }

        private void CourseButton(string name, string caption, RectTransform parent, float width, Vector2 position, Action action)
        {
            Button button = CreateButton(name, parent, new Vector2(width, 34f), position, PanelRaised, MarketShopSkin.Ink, 16);
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            button.GetComponentInChildren<Text>().text = caption;
            MarketShopSkin.ApplyTextButton(button);
            CoreBattleState battle = session.Battle;
            GuidedBossBattleStep step = session.GuidedBossStep;
            button.onClick.AddListener(() =>
            {
                if (!isActiveAndEnabled || !HasGuidedBossPresentation || battleAnimationPlaying || BattleInspectionBlocksInput ||
                    !ReferenceEquals(session.Battle, battle) || session.GuidedBossStep != step ||
                    button == null || !button.gameObject.activeInHierarchy || !button.IsInteractable()) return;
                action();
            });
        }

        private void ApplyGuidedBattleCourseViewport(RectTransform banner)
        {
            NavalTacticalBoardView board = retainedBattleRoot.GetComponentInChildren<NavalTacticalBoardView>();
            if (board == null) return;
            // The chart no longer covers the entire battle surface during a
            // lesson. Fill the reading strip with the surrounding naval backdrop
            // instead of exposing the unsprited panel's default white tint.
            Image surface = retainedBattleRoot.GetComponent<Image>();
            if (surface != null) surface.color = new Color(.015f, .035f, .045f, 1f);
            RectTransform rect = (RectTransform)board.transform;
            float top = banner.anchoredPosition.y - banner.sizeDelta.y - 12f;
            const float bottom = -450f;
            rect.sizeDelta = new Vector2(1860f, top - bottom);
            rect.anchoredPosition = new Vector2(0f, (top + bottom) * .5f);
            // Reserve real space above the chart instead of painting over its
            // cells. Ship framing still keeps three-step routes fully visible.
            board.SetVerticalFraming(0f);
            RectTransform compass = rect.Find("Tactical compass") as RectTransform;
            if (compass != null) compass.anchoredPosition = new Vector2(-195f, -92f);
        }

        private void ContinueGuidedBattleCourse(bool restart)
        {
            if (!HasGuidedBossPresentation || battleAnimationPlaying || BattleInspectionBlocksInput || IsBoardingCardChoiceOpen) return;
            string reason;
            bool accepted = restart ? session.TryRestartGuidedBossLesson(out reason) : session.TryContinueGuidedBossLesson(out reason);
            if (accepted) Render();
            else { guidedCourseFeedback = reason; RefreshGuidedBattleCourse(); }
        }

        private void RefreshGuidedBattleCourse()
        {
            if (!HasGuidedBossPresentation || battleAnimationPlaying || BattleInspectionBlocksInput || contentRoot == null) return;
            BuildGuidedTutorialOverlay();
        }

        private void ObserveGuidedBattleInspection(string topic)
        {
            if (!HasGuidedBossPresentation || battleAnimationPlaying) return;
            if (session.TryInspectGuidedBoss(topic, out _) && !BattleInspectionBlocksInput) RefreshGuidedBattleCourse();
        }

        private void AttachGuidedBattleCardInspection(Button card, RetainedBattleHandCard view)
        {
            GuidedBattleCardReadObserver observer = card.gameObject.AddComponent<GuidedBattleCardReadObserver>();
            observer.Configure(() =>
            {
                if (!HasGuidedBossPresentation || battleAnimationPlaying || BattleInspectionBlocksInput ||
                    !ReferenceEquals(session.Battle, view.Battle) || card == null || !card.gameObject.activeInHierarchy ||
                    card.transform.parent != retainedBattleHand || view.Index < 0 || view.Index >= session.Battle.Hand.Count ||
                    session.Battle.Hand[view.Index] != view.CardId) return;
                string expected = session.GuidedBossExpectedCardId;
                if (!string.IsNullOrEmpty(expected) && session.GetBattleCard(view.CardId).Id != expected) return;
                ObserveGuidedBattleInspection("card");
            });
        }

        private void BuildGuidedBattleCourseFocus()
        {
            var targets = new List<Selectable>();
            if (session.GuidedBossExpectedAction == GuidedBossActionKind.Movement && session.Battle.NavalAwaitingHeadingSelection)
            {
                foreach (GuidedControlSnapshot snapshot in guidedControlSnapshots)
                    if (snapshot.Control != null && snapshot.Control.name.StartsWith("Naval Arrival Heading ", StringComparison.Ordinal))
                        FocusGuidedControl(targets, snapshot.Control.name);
            }
            else if (session.GuidedBossExpectedAction == GuidedBossActionKind.Inspect && session.GuidedBossInspectionTopic == "card")
            {
                foreach (RetainedBattleHandCard view in retainedBattleHandViews)
                {
                    if (view.Button == null || !view.Button.gameObject.activeInHierarchy) continue;
                    if (string.IsNullOrEmpty(session.GuidedBossExpectedCardId) || session.GetBattleCard(view.CardId).Id == session.GuidedBossExpectedCardId)
                        targets.Add(view.Button);
                }
            }
            else if (session.GuidedBossExpectedAction == GuidedBossActionKind.FreePlay)
            {
                if (session.GuidedBossLesson == GuidedBossLesson.BoardingContact) FocusGuidedControl(targets, "Battle Board");
                if (session.GuidedBossLesson == GuidedBossLesson.RamContact) FocusGuidedControl(targets, "Battle Ram");
            }
            else FocusGuidedControl(targets, GuidedBossTargetControl);
            if (targets.Count == 0) return;
            RectTransform layer = CreateRect("Guided Tutorial Focus Layer", contentRoot, Vector2.zero, Vector2.zero);
            StretchGuidedFocus(layer);
            guidedDecorations.Add(layer.gameObject);
            foreach (Selectable target in targets)
            {
                RectTransform marker = CreateRect("Guide Focus " + target.name, layer, Vector2.zero, Vector2.zero);
                StretchGuidedFocus(marker);
                marker.gameObject.AddComponent<GuidedTutorialFocusGraphic>().Configure(target,
                    session.GuidedBossExpectedAction == GuidedBossActionKind.Inspect && session.GuidedBossInspectionTopic == "card");
            }
        }

        private void OpenGuidedBattleInformation(string topic)
        {
            if (topic == "enemy" && HasBoardingEnemySchool(session.Battle)) OpenBoardingEnemyOverview();
            else OpenBattleOverview(false, topic);
        }

        private void RefreshGuidedBattleInformation(string topic)
        {
            CoreBattleState battle = inspectedBattle;
            bool boarding = battle.Stage == NavalBattleStage.Boarding;
            var body = new StringBuilder();
            if (topic == "enemy")
            {
                battleDeckSummary.text = "相隔 " + CoreVoyageSession.NavalGridDistance(battle.PlayerPositionMeters, battle.EnemyPositionMeters) + " 格 · 炮击4格（横纵格数相加）";
                body.Append(battle.EnemyName).Append("\n船体 ").Append(battle.EnemyHull).Append('/').Append(battle.EnemyMaxHull)
                    .Append("  ·  船员 ").Append(battle.EnemyBoardingHealth).Append('/').Append(battle.EnemyMaxBoardingHealth)
                    .Append("  ·  格挡 ").Append(battle.EnemyBlock)
                    .Append("\n\n当前意图\n").Append(session.GetEnemyIntent())
                    .Append("\n\n接舷：两舰平行（同向或反向），舷侧位于相邻两行或两列、相距 100 米，消耗 1 行动点。")
                    .Append("\n冲撞：上下左右相邻且我舰船头朝敌，可撞敌舰前后左右，消耗 1 行动点。")
                    .Append("\n前后排队不能直接接舷；两种接触动作均不消耗能量。");
            }
            else
            {
                battleDeckSummary.text = string.Empty;
                body.Append("能量 ").Append(battle.Energy).Append('/').Append(battle.MaxEnergy)
                    .Append("  ·  行动点 ").Append(battle.NavalActionPoints)
                    .Append("\n火药 ").Append(session.Cargo.Powder).Append("  ·  炮弹 ").Append(session.Cargo.Cannonballs)
                    .Append("  ·  木板 ").Append(session.Cargo.Wood).Append("  ·  药品 ").Append(session.Cargo.Medicine)
                    .Append("\n船体 ").Append(session.Hull).Append('/').Append(session.MaxHull)
                    .Append("  ·  船员 ").Append(battle.PlayerBoardingHealth).Append('/').Append(battle.PlayerMaxBoardingHealth)
                    .Append("  ·  格挡 ").Append(battle.PlayerBlock)
                    .Append("\n士气 ").Append(session.Morale).Append("  ·  接舷暴击率 ").Append(session.BoardingCriticalChancePercent).Append('%')
                    .Append("\n\n").Append(boarding ? session.GetBoardingStateSummary() : BattleActiveBuffText(battle));
                if (boarding)
                {
                    int recovery = battle.Boarding.ContinuousRecovery;
                    body.Append("\n持续恢复：");
                    if (recovery == 0) body.Append("无");
                    else
                    {
                        body.Append(recovery).Append(" 层\n下次己方回合结束恢复 ").Append(recovery)
                            .Append(" 点船员生命，然后减少 1 层；之后按剩余层数继续恢复。");
                    }
                    body.Append("\n待结算攻击 ").Append(battle.Boarding.DelayedDamage.Count).Append(" 项");
                }
                body.Append("\n\n最近行动\n").Append(string.IsNullOrWhiteSpace(battle.LastAction) ? "暂无" : battle.LastAction)
                    .Append("\n\n手牌 ").Append(battle.Hand.Count).Append("  ·  抽牌堆 ").Append(battle.DrawPile.Count)
                    .Append("  ·  弃牌堆 ").Append(battle.DiscardPile.Count)
                    .Append("\n手牌上限 8 张；抽牌堆与弃牌堆可随时从卡组查看。")
                    .Append("\n本段已洗牌 ").Append(session.GuidedBossDeckShuffles).Append(" 次")
                    .Append("\n\n能量用于出牌；航行和接触动作使用行动点。确认航行会清空全部行动点。")
                    .Append("\n同名牌在本回合每成功使用一次，后续费用加 1；换回合或阶段重置。")
                    .Append("\n查看卡牌、资源和牌堆不会消耗任何资源。");
            }
            Text text = CourseText("Guided Battle Inspection Details", battleDeckContent, 1200f, 24, body.ToString(), FontStyle.Normal);
            PositionGuidedReadingText(text, 34f, 18f, Mathf.Max(570f, text.preferredHeight));
            battleDeckContent.sizeDelta = new Vector2(1276f, text.rectTransform.sizeDelta.y + 40f);
            battleDeckContent.anchoredPosition = Vector2.zero;
            battleDeckScroll.verticalNormalizedPosition = 1f;
        }
    }

    internal sealed class GuidedBattleCardReadObserver : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Action inspect;
        private bool hovering, observed;
        private float entered;
        public void Configure(Action onRead) => inspect = onRead;
        public void OnPointerEnter(PointerEventData data) { hovering = true; observed = false; entered = Time.unscaledTime; }
        public void OnPointerExit(PointerEventData data) { hovering = false; }
        private void OnDisable() { hovering = false; }
        private void Update()
        {
            if (!hovering || observed || Time.unscaledTime - entered < .6f) return;
            observed = true;
            inspect?.Invoke();
        }
    }
}
