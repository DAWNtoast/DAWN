using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public enum GuidedBossBattleStep
    {
        None, NavalRetreat, NavalLoad, NavalGuard, NavalEndTurn1,
        NavalRangeAttempt, NavalApproach2, NavalFire, NavalGuard2, NavalEndTurn2,
        NavalLoadAttempt, NavalCircle, NavalApproach3, NavalRam,
        BoardingAttack1, BoardingGuard1, BoardingEndTurn1,
        BoardingHeal, BoardingAttack2, BoardingGuard2, BoardingEndTurn2,
        BoardingAttack3, Complete,
        NavalMove1, NavalMove2, NavalMove3,
        CourseResources = 100, CourseFreeMove, CourseActionCard, CourseSecondMove, CourseBasicsDone,
        CourseManeuver, CourseManeuverDone,
        CourseLoad, CoursePrepareGuard, CoursePrepareEnd, CourseRangeAttempt,
        CourseApproach1, CourseApproach2, CourseEnergy, CourseGunneryMove, CourseFire, CourseNavalGuard,
        CourseEnemyInspect, CourseEnemyTurn, CourseRepair, CourseDeckInspect, CourseDraw, CourseGunneryDone,
        CourseBoardMove, CourseBoardAction, CourseBoardContact, CourseBoardDone,
        CourseRamMove, CourseRamAction, CourseRamContact, CourseRamDone,
        CourseBoardingAttack, CourseBoardingGuard, CourseBoardingEnd1, CourseBandage, CourseBoardingDraw,
        CourseBoardingEnd2, CourseAid, CourseAmbush, CourseBoardingEnd3, CourseEffectsInspect,
        CourseBoardingEnd4, CourseBoardingDone, CourseFinal, CourseRetry, CourseCardInspect,
    }

    public enum GuidedBossActionKind { None, Card, BlockedCard, EndTurn, Ram, Movement, Inspect, Continue, FreePlay, Board }

    public sealed partial class CoreVoyageSession
    {
        public GuidedBossBattleStep GuidedBossStep { get; private set; }
        public bool IsGuidedBossBattle => Phase == CoreDemoPhase.Battle && Battle != null &&
            GuidedTutorialLeg >= CoreGuidedTutorialContent.NavigatorVoyageLeg &&
            Battle.Profile == CoreGuidedTutorialContent.BossProfile && GuidedBossStep != GuidedBossBattleStep.None;

        private bool guidedBossSuppliesHeld;
        private int guidedBossOwnedPowder, guidedBossOwnedBall, guidedBossOwnedMedicine, guidedBossOwnedWood;
        private int guidedBossEntryMorale;
        public GuidedBossActionKind GuidedBossExpectedAction => IsGuidedBossBattle
            ? CurrentGuidedLessonStep?.Action ?? GuidedBossActionKind.None : GuidedBossActionKind.None;
        public string GuidedBossExpectedCardId => IsGuidedBossBattle ? CurrentGuidedLessonStep?.Card ?? string.Empty : string.Empty;
        // Compatibility guidance only; the normal three arrival choices remain available.
        public float GuidedBossMovementHeadingDegrees => Battle == null ? 0f : Battle.NavalAwaitingHeadingSelection
            ? navalArrivalHeading : Battle.PlayerHeadingDegrees;
        public VoyagePoint GuidedBossMovementTargetMeters
        {
            get
            {
                if (Battle == null) return new VoyagePoint(0f, 0f);
                if (GuidedBossStep == GuidedBossBattleStep.CourseGunneryMove) return new VoyagePoint(-300f, 0f);
                if (GuidedBossStep == GuidedBossBattleStep.CourseBoardMove || GuidedBossStep == GuidedBossBattleStep.CourseRamMove)
                    return new VoyagePoint(-100f, 0f);
                float radians = Battle.PlayerHeadingDegrees * NavalDegreesToRadians;
                return new VoyagePoint(Battle.PlayerPositionMeters.X + (float)Math.Round(Math.Cos(radians)) * NavalGridCellMeters,
                    Battle.PlayerPositionMeters.Y + (float)Math.Round(Math.Sin(radians)) * NavalGridCellMeters);
            }
        }

        private string GuidedBossMovementRestriction(VoyagePoint targetMeters, int precedingSteps) =>
            IsGuidedBossBattle && !GuidedBossAllowsMovement ? "请先完成当前教学动作" : string.Empty;

        private string GuidedBossMovementCommitRestriction()
        {
            if (!IsGuidedBossBattle) return string.Empty;
            if (GuidedBossStep == GuidedBossBattleStep.CourseManeuver)
            {
                bool turned = false;
                for (int index = 2; index < navalMovementPath.Count; index++)
                    turned |= Math.Abs(NavalAngleDelta(navalMovementPath[index - 1].HeadingDegrees,
                        navalMovementPath[index].HeadingDegrees)) > .01f;
                if (navalMovementPath.Count < 3 || !turned) return "本段请规划至少2格、包含一次转弯的路线";
            }
            if (GuidedBossStep == GuidedBossBattleStep.CourseGunneryMove &&
                !NavalCannonInRange(navalMovementPath[navalMovementPath.Count - 1].PositionMeters, Battle.EnemyPositionMeters))
                return "本段请先规划进入4格炮击范围的路线（横纵格数相加）";
            return string.Empty;
        }

        public bool TryObserveGuidedBossBlockedCard(int handIndex, out string reason)
        {
            reason = string.Empty;
            if (!IsGuidedBossBattle || GuidedBossExpectedAction != GuidedBossActionKind.BlockedCard ||
                handIndex < 0 || handIndex >= Battle.Hand.Count || GetBattleCard(Battle.Hand[handIndex])?.Id != GuidedBossExpectedCardId)
                return false;
            reason = CardUnavailableReason(handIndex);
            if (NavalCannonTargetInRange || reason != NavalCannonOutOfRangeReason) return false;
            GuidedBossStep = CurrentGuidedLessonStep.Next;
            Battle.LastAction = reason + "；手牌、能量、行动点与补给均未消耗。";
            return true;
        }

        private string GuidedBossCardRestriction(CoreBattleCard card)
        {
            if (!IsGuidedBossBattle || GuidedBossExpectedAction == GuidedBossActionKind.FreePlay) return string.Empty;
            return (GuidedBossExpectedAction == GuidedBossActionKind.Card || GuidedBossExpectedAction == GuidedBossActionKind.BlockedCard) &&
                card.Id == GuidedBossExpectedCardId ? string.Empty : "请先完成当前教学动作";
        }

        private CoreBattleCard GuidedBossCardDefinition(int index) => cardLibrary[index].AllowedStages == NavalBattleStages.None
            ? null : cardLibrary[index];

        private void InitializeGuidedBossScript(CoreEncounter encounter)
        {
            GuidedBossStep = GuidedBossBattleStep.None;
            GuidedBossLesson = GuidedBossLesson.None;
            if (GuidedTutorialLeg < CoreGuidedTutorialContent.NavigatorVoyageLeg || encounter.Id != CoreGuidedTutorialContent.BossEventId) return;
            guidedBossSuppliesHeld = true;
            guidedBossOwnedPowder = Cargo.Powder; guidedBossOwnedBall = Cargo.Cannonballs;
            guidedBossOwnedMedicine = Cargo.Medicine; guidedBossOwnedWood = Cargo.Wood;
            guidedBossEntryMorale = Morale;
            StartGuidedBossLesson(GuidedBossLesson.NavalBasics);
        }

        private void StartGuidedBossLesson(GuidedBossLesson lesson)
        {
            string name = Battle?.EnemyName ?? "近港首领";
            GuidedBossLesson = lesson;
            GuidedBossExerciseFailed = false;
            GuidedBossDeckShuffles = 0;
            // Exercise gains and losses must not accumulate through chapter retries
            // or grant an advantage when the final, ordinary battle begins.
            Morale = guidedBossEntryMorale;
            bool final = lesson == GuidedBossLesson.FinalBattle;
            NavalBattleStage stage = lesson == GuidedBossLesson.Gunnery || final ? NavalBattleStage.Preparation :
                lesson == GuidedBossLesson.BoardingCards ? NavalBattleStage.Boarding : NavalBattleStage.Naval;
            int crewHealth = CoreBattleBalance.BoardingHealthForOrdinaryCrew(SelectedOrdinaryCrew);
            Battle = new CoreBattleState
            {
                EnemyName = name, Profile = CoreGuidedTutorialContent.BossProfile, IsBoss = true,
                EnemyHull = final ? 80 : 1000, EnemyMaxHull = final ? 80 : 1000,
                EnemyBoardingHealth = final ? 40 : 1000, EnemyMaxBoardingHealth = final ? 40 : 1000,
                PlayerOrdinaryCrew = SelectedOrdinaryCrew, EnemyOrdinaryCrew = final ? 7 : 30,
                PlayerBoardingHealth = crewHealth, PlayerMaxBoardingHealth = crewHealth,
                Energy = 3, MaxEnergy = 3, Turn = 1, PhaseTurn = 1, Stage = stage,
                PlayerBowQuality = battleBowQuality, PlayerHullQuality = battleHullQuality,
                EnemyBowQuality = 1f, EnemyHullQuality = 1f,
                Alignment = NavalBattleAlignment.HeadOn, PlannedBoardingApproach = NavalBoardingApproach.Alongside,
                BoardingApproach = NavalBoardingApproach.Alongside,
            };
            Battle.BoardingEnemy.Deck = CoreBoardingEnemyCatalog.ForSchool("neutral");
            Battle.SetTacticalPose(false, new VoyagePoint(0f, 0f), 180f);
            Battle.SetTacticalPose(true, new VoyagePoint(-600f, 0f), 0f);
            Hull = MaxHull;
            Cargo.Powder = Cargo.Cannonballs = Cargo.Medicine = Cargo.Wood = 20;
            Battle.NavalStartPlayerHull = Hull;
            Battle.NavalStartEnemyHull = Battle.EnemyHull;
            BeginNavalActionPointTurn();
            SetGuidedLessonDeck(NavalBattleStage.Preparation, "card_load_shot", "card_brace", "card_approach", "card_energy_order", "card_deck_polish", "card_brace");
            SetGuidedLessonDeck(NavalBattleStage.Naval, "card_approach", "card_chain_shot", "card_approach", "card_brace", "card_energy_order",
                "card_chain_shot", "card_emergency_repair", "card_approach", "card_cannon_barrage", "card_full_sail_turn");
            SetGuidedLessonDeck(NavalBattleStage.Boarding, "card_crew_thrust", "card_crew_guard", "card_crew_bandage", "card_cutlass_raid", "card_energy_order",
                "card_boarding_basic_07", "card_boarding_basic_13", "card_tactical_draw", "card_crew_thrust", "card_crew_guard");
            switch (lesson)
            {
                case GuidedBossLesson.NavalBasics:
                    GuidedBossStep = GuidedBossBattleStep.CourseResources; break;
                case GuidedBossLesson.Maneuver:
                    GuidedBossStep = GuidedBossBattleStep.CourseManeuver;
                    Battle.NavalActionPoints = 3; break;
                case GuidedBossLesson.Gunnery:
                    GuidedBossStep = GuidedBossBattleStep.CourseLoad;
                    Hull = Math.Max(1, MaxHull - 8); Battle.NavalStartPlayerHull = Hull;
                    SetGuidedLessonDeck(NavalBattleStage.Naval, "card_chain_shot", "card_approach", "card_approach", "card_energy_order", "card_brace",
                        "card_emergency_repair", "card_tactical_draw", "card_energy_order", "card_brace", "card_chain_shot");
                    break;
                case GuidedBossLesson.BoardingContact:
                case GuidedBossLesson.RamContact:
                    GuidedBossStep = lesson == GuidedBossLesson.BoardingContact ? GuidedBossBattleStep.CourseBoardContact : GuidedBossBattleStep.CourseRamMove;
                    Battle.SetTacticalPose(true, new VoyagePoint(lesson == GuidedBossLesson.BoardingContact ? -100f : -200f, 0f), 0f);
                    Battle.SetTacticalPose(false, new VoyagePoint(0f, 0f), lesson == GuidedBossLesson.BoardingContact ? 0f : 90f);
                    break;
                case GuidedBossLesson.BoardingCards:
                    GuidedBossStep = GuidedBossBattleStep.CourseBoardingAttack;
                    Battle.PlayerBoardingHealth = Math.Max(1, crewHealth - 15);
                    SetGuidedLessonDeck(NavalBattleStage.Boarding,
                        "card_crew_thrust", "card_crew_guard", "card_crew_rally", "card_cutlass_raid", "card_crew_pistol",
                        "card_crew_bandage", "card_tactical_draw", "card_crew_guard", "card_crew_thrust", "card_crew_rally",
                        "card_crew_guard", "card_crew_thrust", "card_crew_rally",
                        "card_boarding_basic_07", "card_boarding_basic_13", "card_crew_thrust", "card_crew_guard", "card_energy_order");
                    break;
                default: GuidedBossStep = GuidedBossBattleStep.CourseFinal; break;
            }
            Battle.CommitTacticalMovement(new List<CoreNavalTacticalFrame> { Battle.TacticalFrame });
            DrawCards(5);
            if (stage == NavalBattleStage.Boarding) BInitialize();
            Battle.LastAction = "开始本段演习：" + GuidedBossLessonTitle + "。重新布置船位、牌序、士气与训练配给；本课程结束后恢复入场个人补给。";
            AddLog(Battle.LastAction);
        }

        private void SetGuidedLessonDeck(NavalBattleStage stage, params string[] firstToLast)
        {
            List<int> pile = Battle.DrawPileFor(stage);
            pile.Clear();
            for (int at = firstToLast.Length - 1; at >= 0; at--)
                for (int index = 0; index < cardLibrary.Count; index++)
                    if (cardLibrary[index].Id == firstToLast[at]) { pile.Add(index); break; }
        }

        private void AdvanceGuidedBossCard()
        {
            if (IsGuidedBossBattle && GuidedBossExpectedAction == GuidedBossActionKind.Card)
                GuidedBossStep = CurrentGuidedLessonStep.Next;
        }
        private void AdvanceGuidedBossMovement()
        {
            if (IsGuidedBossBattle && !Battle.NavalAwaitingHeadingSelection && GuidedBossExpectedAction == GuidedBossActionKind.Movement)
                GuidedBossStep = CurrentGuidedLessonStep.Next;
        }
        private void AdvanceGuidedBossTurn()
        {
            if (IsGuidedBossBattle && GuidedBossExpectedAction == GuidedBossActionKind.EndTurn)
                GuidedBossStep = CurrentGuidedLessonStep.Next;
        }
        private void BeginGuidedBossBoarding()
        {
            if (!IsGuidedBossBattle) return;
            if (GuidedBossLesson == GuidedBossLesson.BoardingContact) GuidedBossStep = GuidedBossBattleStep.CourseBoardDone;
            if (GuidedBossLesson == GuidedBossLesson.RamContact) GuidedBossStep = GuidedBossBattleStep.CourseRamDone;
        }
        private bool InterceptGuidedBossExerciseOutcome()
        {
            if (!IsGuidedBossBattle || GuidedBossLesson == GuidedBossLesson.FinalBattle) return false;
            bool lost = Hull <= 0 || Battle.Stage == NavalBattleStage.Boarding && Battle.PlayerBoardingHealth <= 0;
            bool won = Battle.EnemyHull <= 0 || Battle.Stage == NavalBattleStage.Boarding && Battle.EnemyBoardingHealth <= 0;
            if (!lost && (!won || GuidedBossLesson == GuidedBossLesson.FinalBattle)) return false;
            GuidedBossExerciseFailed = true;
            GuidedBossStep = GuidedBossBattleStep.CourseRetry;
            Battle.LastAction = lost ? "演习失败，请重新开始本段。" : "演习目标未按步骤完成，请重新开始本段。";
            return true;
        }
        private void RestoreGuidedBossSupplies()
        {
            if (!guidedBossSuppliesHeld) return;
            guidedBossSuppliesHeld = false;
            Cargo.Powder = guidedBossOwnedPowder; Cargo.Cannonballs = guidedBossOwnedBall;
            Cargo.Medicine = guidedBossOwnedMedicine; Cargo.Wood = guidedBossOwnedWood;
            // An early exit discards exercise-only morale changes. The final
            // battle keeps its normal morale outcome, including on defeat.
            if (GuidedBossLesson != GuidedBossLesson.FinalBattle) Morale = guidedBossEntryMorale;
        }
    }
}
