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
    }

    public enum GuidedBossActionKind { None, Card, BlockedCard, EndTurn, Ram }

    public sealed partial class CoreVoyageSession
    {
        public GuidedBossBattleStep GuidedBossStep { get; private set; }
        public bool IsGuidedBossBattle => Phase == CoreDemoPhase.Battle && Battle != null &&
            GuidedTutorialLeg >= CoreGuidedTutorialContent.NavigatorVoyageLeg &&
            Battle.Profile == CoreGuidedTutorialContent.BossProfile && GuidedBossStep != GuidedBossBattleStep.None;

        private bool guidedBossSuppliesHeld;
        private int guidedBossPowderReserve, guidedBossBallReserve, guidedBossMedicineReserve;
        private int guidedBossOwnedPowder, guidedBossOwnedBall, guidedBossOwnedMedicine;
        private static CoreBattleCard guidedBossLoadCard;

        public GuidedBossActionKind GuidedBossExpectedAction
        {
            get
            {
                if (!IsGuidedBossBattle) return GuidedBossActionKind.None;
                switch (GuidedBossStep)
                {
                    case GuidedBossBattleStep.NavalRangeAttempt:
                    case GuidedBossBattleStep.NavalLoadAttempt: return GuidedBossActionKind.BlockedCard;
                    case GuidedBossBattleStep.NavalEndTurn1:
                    case GuidedBossBattleStep.NavalEndTurn2:
                    case GuidedBossBattleStep.BoardingEndTurn1:
                    case GuidedBossBattleStep.BoardingEndTurn2: return GuidedBossActionKind.EndTurn;
                    case GuidedBossBattleStep.NavalRam: return GuidedBossActionKind.Ram;
                    case GuidedBossBattleStep.None:
                    case GuidedBossBattleStep.Complete: return GuidedBossActionKind.None;
                    default: return GuidedBossActionKind.Card;
                }
            }
        }

        public string GuidedBossExpectedCardId
        {
            get
            {
                if (!IsGuidedBossBattle) return string.Empty;
                switch (GuidedBossStep)
                {
                    case GuidedBossBattleStep.NavalRetreat: return "card_retreat";
                    case GuidedBossBattleStep.NavalLoad:
                    case GuidedBossBattleStep.NavalLoadAttempt: return "card_load_shot";
                    case GuidedBossBattleStep.NavalGuard:
                    case GuidedBossBattleStep.NavalGuard2: return "card_brace";
                    case GuidedBossBattleStep.NavalRangeAttempt:
                    case GuidedBossBattleStep.NavalFire: return "card_chain_shot";
                    case GuidedBossBattleStep.NavalApproach2:
                    case GuidedBossBattleStep.NavalApproach3: return "card_approach";
                    case GuidedBossBattleStep.NavalCircle: return "card_circle_enemy";
                    case GuidedBossBattleStep.BoardingAttack1:
                    case GuidedBossBattleStep.BoardingAttack2:
                    case GuidedBossBattleStep.BoardingAttack3: return "card_crew_thrust";
                    case GuidedBossBattleStep.BoardingGuard1:
                    case GuidedBossBattleStep.BoardingGuard2: return "card_crew_guard";
                    case GuidedBossBattleStep.BoardingHeal: return "card_crew_bandage";
                    default: return string.Empty;
                }
            }
        }

        /// <summary>Confirms only an authored failed attempt. The card stays in hand;
        /// ordinary PlayCard failures never advance this lesson or spend anything.</summary>
        public bool TryObserveGuidedBossBlockedCard(int handIndex, out string reason)
        {
            reason = string.Empty;
            if (!IsGuidedBossBattle || GuidedBossExpectedAction != GuidedBossActionKind.BlockedCard ||
                handIndex < 0 || handIndex >= Battle.Hand.Count ||
                GetBattleCard(Battle.Hand[handIndex])?.Id != GuidedBossExpectedCardId) return false;
            reason = CardUnavailableReason(handIndex);
            bool expected = GuidedBossStep == GuidedBossBattleStep.NavalRangeAttempt
                ? Battle.DistanceMeters > 350f && reason == "炮击需要距离不超过 350 米"
                : Cargo.Powder == 0 && reason == "火药不足";
            if (!expected) return false;
            GuidedBossStep += 1;
            Battle.LastAction = reason + "；卡牌、行动点和补给均未消耗。";
            AddLog(Battle.LastAction);
            return true;
        }

        private string GuidedBossCardRestriction(CoreBattleCard card)
        {
            if (!IsGuidedBossBattle) return string.Empty;
            return (GuidedBossExpectedAction == GuidedBossActionKind.Card || GuidedBossExpectedAction == GuidedBossActionKind.BlockedCard) &&
                card.Id == GuidedBossExpectedCardId ? string.Empty : "请先完成当前教学动作";
        }

        private CoreBattleCard GuidedBossCardDefinition(int index)
        {
            CoreBattleCard original = cardLibrary[index];
            if (original.AllowedStages == NavalBattleStages.None) return null;
            if (original.Id != "card_load_shot") return original;
            // This lesson explicitly handles a single powder charge. The normal
            // card, player upgrade levels and saved loadouts remain untouched.
            if (guidedBossLoadCard == null)
                guidedBossLoadCard = new CoreBattleCard(original.Id, original.Name, original.Type,
                    original.Energy, 0, 0, 0, 0, 1, "教学装填：消耗 1 火药，下次炮击伤害 +4。",
                    original.AllowedStages, original.Action, artId: original.ArtId);
            return guidedBossLoadCard;
        }

        private void InitializeGuidedBossScript(CoreEncounter encounter)
        {
            GuidedBossStep = GuidedBossBattleStep.None;
            if (GuidedTutorialLeg < CoreGuidedTutorialContent.NavigatorVoyageLeg || encounter.Id != CoreGuidedTutorialContent.BossEventId) return;
            GuidedBossStep = GuidedBossBattleStep.NavalRetreat;
            guidedBossSuppliesHeld = true;
            guidedBossOwnedPowder = Math.Min(1, Cargo.Powder); guidedBossPowderReserve = Cargo.Powder - guidedBossOwnedPowder;
            guidedBossOwnedBall = Math.Min(1, Cargo.Cannonballs); guidedBossBallReserve = Cargo.Cannonballs - guidedBossOwnedBall;
            guidedBossOwnedMedicine = Math.Min(1, Cargo.Medicine); guidedBossMedicineReserve = Cargo.Medicine - guidedBossOwnedMedicine;
            Cargo.Powder = Cargo.Cannonballs = Cargo.Medicine = 1;
            Hull = MaxHull;
            Battle.Stage = NavalBattleStage.Naval;
            Battle.Turn = Battle.PhaseTurn = 1;
            Battle.DistanceMeters = 400f;
            Battle.EnemyBlock = Battle.PlayerBlock = 0;
            Battle.PlayerBowQuality = Battle.PlayerHullQuality = Battle.EnemyBowQuality = Battle.EnemyHullQuality = 1f;
            // These are battle-only authored exercise participants, never a
            // change to the recruited crew, departure roster or player upgrades.
            Battle.PlayerOrdinaryCrew = 10;
            Battle.PlayerBoardingHealth = Battle.PlayerMaxBoardingHealth = 60;
            Battle.EnemyOrdinaryCrew = 3;
            Battle.EnemyBoardingHealth = Battle.EnemyMaxBoardingHealth = 9;
            SetGuidedBossHand("card_retreat", "card_load_shot", "card_brace");
            Battle.LastAction = "首领相距 400 米。先拉开距离，再装填并稳固船身。";
            AddLog("近港首领教学：船体整备至全满，本场使用固定演练手牌与一份弹药、药品；额外随船储备暂存，结束后归还。");
        }

        private void SetGuidedBossHand(params string[] ids)
        {
            Battle.Hand.Clear();
            Battle.DrawPile.Clear(); Battle.DiscardPile.Clear();
            Battle.Energy = Battle.MaxEnergy = 3;
            Battle.CardsPlayedThisTurn = 0;
            Battle.BeginNewCardTurn();
            foreach (string id in ids)
                for (int i = 0; i < cardLibrary.Count; i++)
                    if (cardLibrary[i].Id == id) { Battle.Hand.Add(i); break; }
        }

        private void AdvanceGuidedBossCard()
        {
            if (IsGuidedBossBattle && GuidedBossExpectedAction == GuidedBossActionKind.Card) GuidedBossStep += 1;
        }

        private void AdvanceGuidedBossTurn()
        {
            if (!IsGuidedBossBattle) return;
            switch (GuidedBossStep)
            {
                case GuidedBossBattleStep.NavalEndTurn1:
                    GuidedBossStep = GuidedBossBattleStep.NavalRangeAttempt;
                    SetGuidedBossHand("card_chain_shot", "card_approach", "card_brace"); break;
                case GuidedBossBattleStep.NavalEndTurn2:
                    GuidedBossStep = GuidedBossBattleStep.NavalLoadAttempt;
                    SetGuidedBossHand("card_load_shot", "card_circle_enemy", "card_approach"); break;
                case GuidedBossBattleStep.BoardingEndTurn1:
                    GuidedBossStep = GuidedBossBattleStep.BoardingHeal;
                    SetGuidedBossHand("card_crew_bandage", "card_crew_thrust", "card_crew_guard"); break;
                case GuidedBossBattleStep.BoardingEndTurn2:
                    GuidedBossStep = GuidedBossBattleStep.BoardingAttack3;
                    SetGuidedBossHand("card_crew_thrust"); break;
            }
        }

        private void BeginGuidedBossBoarding()
        {
            if (!IsGuidedBossBattle) return;
            GuidedBossStep = GuidedBossBattleStep.BoardingAttack1;
            SetGuidedBossHand("card_crew_thrust", "card_crew_guard");
        }

        private void RestoreGuidedBossSupplies()
        {
            if (!guidedBossSuppliesHeld) return;
            guidedBossSuppliesHeld = false;
            Cargo.Powder = guidedBossPowderReserve + Math.Min(guidedBossOwnedPowder, Cargo.Powder);
            Cargo.Cannonballs = guidedBossBallReserve + Math.Min(guidedBossOwnedBall, Cargo.Cannonballs);
            Cargo.Medicine = guidedBossMedicineReserve + Math.Min(guidedBossOwnedMedicine, Cargo.Medicine);
        }
    }
}
