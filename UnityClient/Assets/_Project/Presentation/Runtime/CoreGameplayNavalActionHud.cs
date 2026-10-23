using System;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private void RefreshNavalActionHud(RectTransform root, CoreBattleState battle)
        {
            if (root == null || battle == null || battle.Stage == NavalBattleStage.Boarding) return;
            RectTransform resources = root.Find("Player Resource HUD") as RectTransform;
            if (resources == null) return;
            Text actionPoints = resources.Find("Naval Action Points")?.GetComponent<Text>();
            if (actionPoints == null)
                actionPoints = CreateText("Naval Action Points", resources, new Vector2(212f, 24f),
                    new Vector2(0f, -38f), 17, FontStyle.Bold, TextAnchor.MiddleCenter, Teal);
            Text hint = resources.Find("Naval Immediate Card Hint")?.GetComponent<Text>();
            if (hint == null)
                hint = CreateText("Naval Immediate Card Hint", resources, new Vector2(212f, 18f),
                    new Vector2(0f, -61f), 13, FontStyle.Normal, TextAnchor.MiddleCenter, Ink);
            SetNavalHudText(resources, "Naval Action Points", new Vector2(212f, 24f), new Vector2(0f, -38f), 17);
            SetNavalHudText(resources, "Naval Immediate Card Hint", new Vector2(212f, 18f), new Vector2(0f, -61f), 13);
            actionPoints.raycastTarget = hint.raycastTarget = false;

            NavalTacticalBoardView board = root.GetComponentInChildren<NavalTacticalBoardView>();
            NavalActionHudObserver observer = root.GetComponent<NavalActionHudObserver>();
            if (observer == null) observer = root.gameObject.AddComponent<NavalActionHudObserver>();
            observer.Configure(() => ReadNavalActionHudState(root, battle, board), state =>
            {
                if (!state.Active || actionPoints == null || hint == null) return;
                actionPoints.text = state.Phase == NavalActionHudPhase.ChoosingHeading
                    ? "到达剩余 " + state.ArrivalActionPoints + " 点" : "行动点 " + state.ActionPoints;
                hint.text = state.Phase == NavalActionHudPhase.Moving ? "航行中 · 暂不可出牌" :
                    state.Phase == NavalActionHudPhase.ChoosingHeading ?
                        (state.ArrivalActionPoints > 0 ? "可接舷/冲撞 · 定向后清空" : "先选择到达后的船头朝向") :
                    state.Phase == NavalActionHudPhase.ResolvingAction ? "行动结算中" :
                    state.Phase == NavalActionHudPhase.PlanningRoute ? "拖动我舰选路，再确认航行" :
                    battle.Stage == NavalBattleStage.Preparation ? "点击卡牌完成备战" : "场地牌先选区，再确认出牌";
                hint.color = state.Phase == NavalActionHudPhase.ChoosingHeading ? Gold : Ink;
                RefreshCompactBattleResources(root, battle);
                RefreshNavalHandInputAvailability(root, battle, state.InputLocked);
                // Heading selection locks cards, but arrival contact can still
                // spend the unused movement budget after the animation ends.
                RefreshNavalSpecialActionControls(root);
            });
        }

        private NavalActionHudState ReadNavalActionHudState(RectTransform root, CoreBattleState battle,
            NavalTacticalBoardView board)
        {
            bool active = isActiveAndEnabled && session != null && session.Phase == CoreDemoPhase.Battle &&
                ReferenceEquals(session.Battle, battle) && root != null && root.gameObject.activeInHierarchy;
            if (!active) return new NavalActionHudState(false, 0, NavalActionHudPhase.Ready, true);
            bool awaitingHeading = battle.Stage != NavalBattleStage.Boarding && battle.NavalAwaitingHeadingSelection;
            bool boardAnimating = board != null && board.IsAnimating;
            bool guidedRoute = HasGuidedBossPresentation && session.GuidedBossExpectedAction == GuidedBossActionKind.Movement;
            NavalActionHudPhase phase = awaitingHeading && boardAnimating ? NavalActionHudPhase.Moving :
                battleAnimationPlaying || boardAnimating ? NavalActionHudPhase.ResolvingAction :
                awaitingHeading ? NavalActionHudPhase.ChoosingHeading :
                guidedRoute ? NavalActionHudPhase.PlanningRoute : NavalActionHudPhase.Ready;
            bool contactInputLocked = battleAnimationPlaying || boardAnimating || BattleInspectionBlocksInput ||
                IsBoardingCardChoiceOpen || IsNavalCardChoiceOpen;
            return new NavalActionHudState(true, battle.NavalActionPoints, phase,
                awaitingHeading || contactInputLocked, battle.NavalArrivalActionPoints, contactInputLocked);
        }

        private void RefreshNavalHandInputAvailability(RectTransform root, CoreBattleState battle, bool inputLocked)
        {
            if (root != retainedBattleRoot || !ReferenceEquals(battle, retainedBattleState)) return;
            // Change only availability, preserving retained card identity, fan
            // position and hover while a route executes or awaits its heading.
            foreach (RetainedBattleHandCard view in retainedBattleHandViews)
            {
                if (view.Button == null || !view.Button.gameObject.activeInHierarchy ||
                    view.Button.transform.parent != retainedBattleHand || view.Index < 0 ||
                    view.Index >= battle.Hand.Count || battle.Hand[view.Index] != view.CardId) continue;
                bool available = !inputLocked && session.CanPlayCard(view.Index);
                // Authored failed-attempt lessons deliberately permit a click
                // on one unavailable card so its real failure can be observed.
                bool canObserveFailure = !inputLocked && IsGuidedBossBlockedCardControl(view.Button.name);
                view.Button.interactable = (available || canObserveFailure) &&
                    (!tutorialActive || GuidedTutorialAllowsControl(view.Button.name));
                if (view.Available == available) continue;
                CoreBattleCard data = session.GetBattleCard(view.CardId);
                view.Surface.Configure(data.Type, available, data.SchoolId);
                view.SchoolFrame.Configure(data.SchoolId, available, data.PowderCost, data.CannonballCost,
                    CardSpecialCostPresentation.ResourceType(data),
                    supplyType: CardSupplyCostPresentation.ResourceType(data, battle.Stage));
                view.Available = available;
            }
        }
    }

    internal enum NavalActionHudPhase { Ready, Moving, ChoosingHeading, ResolvingAction, PlanningRoute }

    internal readonly struct NavalActionHudState
    {
        public readonly bool Active, InputLocked, ContactInputLocked;
        public readonly int ActionPoints, ArrivalActionPoints;
        public readonly NavalActionHudPhase Phase;

        public NavalActionHudState(bool active, int actionPoints, NavalActionHudPhase phase, bool inputLocked,
            int arrivalActionPoints = 0, bool contactInputLocked = false)
        {
            Active = active;
            ActionPoints = actionPoints;
            Phase = phase;
            InputLocked = inputLocked;
            ArrivalActionPoints = arrivalActionPoints;
            ContactInputLocked = contactInputLocked;
        }

        public bool SameAs(NavalActionHudState other) => Active == other.Active && ActionPoints == other.ActionPoints &&
            Phase == other.Phase && InputLocked == other.InputLocked &&
            ArrivalActionPoints == other.ArrivalActionPoints && ContactInputLocked == other.ContactInputLocked;
    }

    internal sealed class NavalActionHudObserver : MonoBehaviour
    {
        private Func<NavalActionHudState> read;
        private Action<NavalActionHudState> apply;
        private NavalActionHudState previous;
        private bool hasPrevious;

        public void Configure(Func<NavalActionHudState> readState, Action<NavalActionHudState> applyState)
        {
            read = readState;
            apply = applyState;
            hasPrevious = false;
            Refresh();
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (read == null || apply == null) return;
            NavalActionHudState state = read();
            if (hasPrevious && previous.SameAs(state)) return;
            previous = state;
            hasPrevious = true;
            apply(state);
        }
    }
}
