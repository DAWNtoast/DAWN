using System;
using System.Collections.Generic;
using ShatteredSail.Core.Random;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        public const float MinimumNavalDistanceMeters = 30f;

        private CoreDemoPhase trainingReturnPhase;
        private Mulberry32Random trainingPreviousVoyageRandom;
        public CoreTrainingBattleOutcome TrainingOutcome { get; private set; }

        /// <summary>
        /// Starts a fixed target with the configured real decks, equipment and supplies.
        /// Training preserves combat losses but grants no encounter rewards or progress.
        /// Boarding maxima are fixed; normal naval damage still carries into boarding.
        /// </summary>
        public bool StartTrainingBattle(CoreTrainingTarget target) => StartTrainingBattleCore(target, null, null);

        /// <summary>
        /// Starts a consecutive training bout with the preceding bout's remaining crew HP.
        /// Hull and cargo remain untouched. Crew maximum, decks and battle state initialize normally;
        /// boarding deducts only this bout's additional naval carryover from the retained crew HP.
        /// An optional sequence key isolates this bout's reproducible shuffle and decision streams.
        /// </summary>
        public bool StartTrainingBattle(CoreTrainingTarget target, int startingCrewHealth, string sequenceKey = null) =>
            StartTrainingBattleCore(target, startingCrewHealth, sequenceKey);

        private bool StartTrainingBattleCore(CoreTrainingTarget target, int? startingCrewHealth, string sequenceKey)
        {
            if (target == null || (Phase != CoreDemoPhase.Port && Phase != CoreDemoPhase.Sailing) || Hull <= 0)
                return false;
            if (startingCrewHealth.HasValue && (startingCrewHealth.Value <= 0 ||
                startingCrewHealth.Value > CoreBattleBalance.BoardingHealthForOrdinaryCrew(SelectedOrdinaryCrew)))
                return false;
            trainingReturnPhase = Phase;
            trainingPreviousVoyageRandom = voyageRandom;
            voyageRandom = new Mulberry32Random(SeedHash.Compute((Map == null ? SelectedSeed : Map.Seed) +
                "::TRAINING::" + target.Id + TrainingSequenceSuffix(sequenceKey)));
            StartBattleCore(new CoreEncounter(target.Id, target.Name, "训练", "固定木桩", false, false, 0, 0),
                false, target, startingCrewHealth, sequenceKey);
            return true;
        }

        private static string TrainingSequenceSuffix(string sequenceKey) =>
            string.IsNullOrEmpty(sequenceKey) ? string.Empty : "::SEQUENCE::" + sequenceKey;

        public bool CanPlayCard(int handIndex) => CardUnavailableReason(handIndex).Length == 0;

        // Card-library index, not hand slot. Copies share their stable ID, so
        // drawing another copy cannot bypass the current turn's escalating cost.
        public int GetBattleCardEnergyCost(int cardIndex)
        {
            CoreBattleCard card = GetBattleCard(cardIndex);
            if (card == null) return 0;
            int uses = Phase == CoreDemoPhase.Battle && Battle != null ? Battle.SuccessfulCardUses(BattleCardUseIdentity(card)) : 0;
            return (int)Math.Min(int.MaxValue, (long)Math.Max(0, card.Energy) + uses);
        }

        public string CardUnavailableReason(int handIndex)
        {
            if (Phase != CoreDemoPhase.Battle || Battle == null) return "当前没有战斗";
            if (NavalActionMovementActive && Battle.NavalAwaitingHeadingSelection) return "请先确定到达朝向";
            if (handIndex < 0 || handIndex >= Battle.Hand.Count) return "手牌不存在";
            int cardIndex = Battle.Hand[handIndex];
            if (cardIndex < 0 || cardIndex >= cardLibrary.Count) return "卡牌不存在";
            CoreBattleCard card = GetBattleCard(cardIndex);
            if (card == null) return "卡牌不存在";
            if (CoreNavalSchoolExpansionCatalog.FindSpec(card.Id) != null)
                return NavalExpansionCardUnavailableReason(handIndex);
            if (BActive && card.BoardingSpec != null) return GetBoardingChoiceUnavailableReason(handIndex, BDefaultOptions(handIndex));
            string unlockReason = GetBattleCardUnlockReason(card);
            if (unlockReason.Length > 0) return unlockReason;
            string tutorialRestriction = GuidedBossCardRestriction(card);
            if (tutorialRestriction.Length > 0) return tutorialRestriction;
            if (!card.IsAllowedInStage(Battle.Stage)) return "该牌不适用于当前阶段";
            if (!HasBattleCrew(card.RequiredCrew)) return "所需船员不在队伍中";
            if (!HasBattleComponent(card.RequiredComponent)) return "所需部件未装备";
            if (GetBattleCardEnergyCost(cardIndex) > Battle.Energy) return "能量不足";
            if (GetBattleCardPowderCost(cardIndex) > Cargo.Powder) return "火药不足";
            if (card.CannonballCost > Cargo.Cannonballs) return "炮弹不足，可机动接舷";
            if (card.Action == NavalBattleCardAction.Cannon && !NavalCannonTargetInRange) return NavalCannonOutOfRangeReason;
            if (Battle.Stage == NavalBattleStage.Naval &&
                !CoreNavalCardRangeRules.Contains(card, Battle.PlayerPositionMeters, Battle.EnemyPositionMeters))
                return "目标超出 " + CoreNavalCardRangeRules.MaximumCells(card) + " 格范围";
            string schoolReason = SchoolCardUnavailableReason(card);
            if (schoolReason.Length > 0) return schoolReason;
            // A capped strategy does not cancel a mixed card's remaining effects.
            // Draw remains a benefit, but repeated copies pay their actual turn cost.
            CoreBattleCardEffects effects = GetBattleCardEffects(card, Battle.Stage);
            bool hasImmediateBenefit = card.Draw > 0 || effects.Damage > 0 || effects.Block > 0 ||
                (effects.Repair > 0 && (Battle.Stage == NavalBattleStage.Boarding
                    ? Battle.PlayerBoardingHealth < Battle.PlayerMaxBoardingHealth : Hull < MaxHull));
            if (!hasImmediateBenefit && card.Action == NavalBattleCardAction.Polish && Battle.PlayerDeckBuff >= card.StrategyLimit) return "甲板强化已达 +" + card.StrategyLimit;
            if (!hasImmediateBenefit && (card.Action == NavalBattleCardAction.Load || card.Action == NavalBattleCardAction.PrecisionLoad) && GetBattleLoadedShotIncrease(card) <= 0) return "装填增益已达 +" + GetBattleLoadedShotLimit(card);
            return string.Empty;
        }

        public string BattleCardDescription(CoreBattleCard card) => BattleCardDescription(card,
            Phase == CoreDemoPhase.Battle && Battle != null ? Battle.Stage : NavalBattleStage.Naval);

        public string BattleCardDescription(CoreBattleCard card, NavalBattleStage stage)
        {
            if (card == null) return string.Empty;
            string description = BattleCardEffectDescription(card, stage);
            if (stage == NavalBattleStage.Boarding)
            {
                return description +
                    (card.Repair > 0 ? "\n" + BattleCardMedicineDescription(card, stage) : string.Empty);
            }
            if (card.Action == NavalBattleCardAction.Cannon)
            {
                description += " 当前炮击 " + GetBattleCardDamage(card, stage) + "。";
                if (Battle != null && Battle.PlayerFireRounds > 0)
                    description += " 本次攻击向目标施加 " + Battle.PlayerFireRounds + " 层燃烧。";
            }
            if (Battle != null && stage != NavalBattleStage.Boarding &&
                (card.Action == NavalBattleCardAction.Load || card.Action == NavalBattleCardAction.PrecisionLoad))
                description += " 当前装填加伤 +" + PlayerLoadedShotDamageBonus + "；本次实际增加 +" + GetBattleLoadedShotIncrease(card) + "。";
            if (Battle != null && stage != NavalBattleStage.Boarding && card.Block > 0 && Battle.PlayerDeckBuff > 0)
                description += " 甲板额外格挡 +" + Battle.PlayerDeckBuff + "。";
            return description;
        }

        public bool PlayCard(int handIndex)
        {
            if (Phase == CoreDemoPhase.Battle && ResolveBattleOutcome()) return false;
            if (Battle != null && handIndex >= 0 && handIndex < Battle.Hand.Count &&
                CoreNavalSchoolExpansionCatalog.FindSpec(GetBattleCard(Battle.Hand[handIndex])?.Id) != null)
                return PlayNavalCard(handIndex, null);
            if (BActive && handIndex >= 0 && handIndex < Battle.Hand.Count && GetBattleCard(Battle.Hand[handIndex])?.BoardingSpec != null)
                return BPlayCard(handIndex, null);
            if (!CanPlayCard(handIndex)) return false;
            int cardIndex = Battle.Hand[handIndex];
            CoreBattleCard card = GetBattleCard(cardIndex);
            bool retainedThunderAtPlay = CoreThunderRules.IsInThunderField(Battle, true);
            try
            {
            CoreBattleCardEffects effects = GetBattleCardEffects(card, Battle.Stage);
            bool preBurn = HasSchoolFire;
            int previousEnemyHealth = SchoolEnemyHealth;
            int damage = GetBattleCardDamage(card, Battle.Stage);
            bool targetOnThunder = NavalFieldAt(Battle.EnemyPositionMeters)?.Kind == CoreNavalFieldKind.Thunder;
            int energyCost = GetBattleCardEnergyCost(cardIndex);
            int powderCost = GetBattleCardPowderCost(cardIndex);
            int tideWoodCost = TideWoodCost(card);
            int woodCost = effects.WoodCost + tideWoodCost;
            int energyRecovery = GetBattleCardEnergyRecovery(cardIndex);
            bool fireFallback = UsesFireFallback(card);
            int cardsToDraw = card.Id == "card_school_blood_b02" && !HasBloodPaidBenefit(card) ? 0 : card.Draw;
            Battle.Hand.RemoveAt(handIndex);
            Battle.Energy -= energyCost;
            Battle.RecordSuccessfulCardUse(BattleCardUseIdentity(card));
            if (Battle.CardsPlayedThisTurn < int.MaxValue) Battle.CardsPlayedThisTurn += 1;
            Cargo.Powder -= powderCost;
            Cargo.Cannonballs -= card.CannonballCost;
            Cargo.Wood -= woodCost;

            PaySchoolCardCost(card);
            if (CheckCardCurseExecution()) return FinishCardCurseExecution(card, cardIndex, false);
            bool cannon = card.Action == NavalBattleCardAction.Cannon;
            bool critical = false;
            if (cannon)
            {
                Battle.PlayerLoadedShot = 0;
                Battle.Schools.OverflowContribution = 0;
            }
            if (Battle.Stage == NavalBattleStage.Boarding && damage > 0 && RollBoardingCritical())
            {
                damage = DoubleBattleDamage(damage);
                critical = true;
            }
            NavalExpansionFirstDamageBonus(card, true); // Already included in the live damage quote.
            int piercing = Math.Min(damage, NavalExpansionFirstPiercing(card, consume: true));
            int blocked = Math.Min(Battle.EnemyBlock, damage - piercing);
            Battle.EnemyBlock -= blocked;
            RecordEnemyTidalAbsorption(blocked);
            damage -= blocked;
            if (damage <= 0) critical = false;
            if (Battle.Stage == NavalBattleStage.Boarding)
                Battle.EnemyBoardingHealth = Math.Max(0, Battle.EnemyBoardingHealth - damage);
            else
                Battle.EnemyHull = Math.Max(0, Battle.EnemyHull - damage);
            int effectiveDirectDamage = Math.Min(previousEnemyHealth, damage);
            int lifeStealRestored = 0;
            if (card.SchoolEffect?.Mechanic == CoreSchoolMechanic.BloodDrain)
            {
                int beforeHealing = SchoolPlayerHealth;
                CoreSchoolCardEffect drain = card.SchoolEffect;
                HealSchoolPlayer((int)Math.Min(SchoolDefault(drain.Limit, int.MaxValue),
                    (long)effectiveDirectDamage * Math.Min(100, drain.Power) / 100));
                lifeStealRestored = SchoolPlayerHealth - beforeHealing;
            }
            if (CheckCardCurseExecution()) return FinishCardCurseExecution(card, cardIndex, false, effectiveDirectDamage);
            NavalExpansionOnDirectHit(card, SchoolSum(effectiveDirectDamage, blocked), targetOnThunder);
            if (Battle.Stage == NavalBattleStage.Boarding)
                OnBoardingEnemyDamaged(blocked, effectiveDirectDamage);
            int moraleDelta = 0;
            if (cannon && damage > 0)
            {
                Morale = ClampMorale((long)Morale + 2);
                moraleDelta = 2;
            }

            int block = effects.Block + (effects.Block > 0 && Battle.Stage != NavalBattleStage.Boarding ? Battle.PlayerDeckBuff : 0);
            if (Battle.Stage != NavalBattleStage.Boarding) block = NavalArcaneBlockGain(block, true);
            int previousBlock = Battle.PlayerBlock;
            Battle.PlayerBlock = (int)Math.Min(int.MaxValue, (long)previousBlock + block);
            block = Battle.PlayerBlock - previousBlock;
            if (tideWoodCost > 0) Battle.Schools.TidePaidBlock = Battle.PlayerBlock;
            if (BActive && (tideWoodCost > 0 || BState.TideGuardConversionActive))
            {
                BState.TideGuard = Battle.PlayerBlock;
                CTBSyncResources();
            }
            if (card.SchoolEffect?.Mechanic == CoreSchoolMechanic.CurseWard && card.Id != "card_school_curse_b06")
                Battle.Schools.CurseGuard = SchoolSum(Battle.Schools.CurseGuard, block);
            int previousHealth = Battle.Stage == NavalBattleStage.Boarding ? Battle.PlayerBoardingHealth : Hull;
            if (Battle.Stage == NavalBattleStage.Boarding)
            {
                if (!IsBattleHealingBlocked(true))
                {
                    Battle.PlayerBoardingHealth = (int)Math.Min(Battle.PlayerMaxBoardingHealth, (long)Battle.PlayerBoardingHealth + effects.ActualRepair);
                    if (effects.ActualRepair > 0) Cargo.Medicine -= effects.MedicineCost;
                }
            }
            else
                Hull = (int)Math.Min(MaxHull, (long)Hull + NavalExpansionHealing(true, effects.Repair));
            int repaired = (Battle.Stage == NavalBattleStage.Boarding ? Battle.PlayerBoardingHealth : Hull) - previousHealth;
            ApplyBattleCardStrategy(card);
            int schoolEffectDamage = ApplySchoolCardEffect(card, preBurn, effectiveDirectDamage, fireFallback);
            if (Phase != CoreDemoPhase.Battle) return true;
            int actualDamageSoFar = SchoolSum(effectiveDirectDamage, schoolEffectDamage);
            if (CheckCardCurseExecution()) return FinishCardCurseExecution(card, cardIndex, false, actualDamageSoFar);
            int attachedFire = ApplyAttachedFireAfterAttack(card);
            int scorchDamage = fireFallback ? 0 : ApplySchoolScorch(preBurn, effectiveDirectDamage);
            if (CheckCardCurseExecution()) return FinishCardCurseExecution(card, cardIndex, false, SchoolSum(actualDamageSoFar, scorchDamage));
            CompleteSchoolCard(card);
            if (Phase != CoreDemoPhase.Battle) return true;
            if (CheckCardCurseExecution()) return FinishCardCurseExecution(card, cardIndex, false, SchoolSum(actualDamageSoFar, scorchDamage));
            NavalExpansionCompleteAttack(card);
            if (Phase != CoreDemoPhase.Battle) return true;
            Battle.Energy = SchoolSum(Battle.Energy, energyRecovery);
            int derivedDamage = Math.Max(0, previousEnemyHealth - SchoolEnemyHealth - effectiveDirectDamage);
            block = Math.Max(0, Battle.PlayerBlock - previousBlock);
            repaired = SchoolSum(lifeStealRestored, (Battle.Stage == NavalBattleStage.Boarding ? Battle.PlayerBoardingHealth : Hull) - previousHealth);
            Battle.LastAction = "打出「" + card.Name + "」" +
                (damage > 0 ? "：" + (Battle.Stage == NavalBattleStage.Boarding ? "船员" : "船体") + "伤害 " + damage : string.Empty) +
                (critical ? "（暴击）" : string.Empty) +
                (derivedDamage > 0 ? "；衍生伤害 " + derivedDamage : string.Empty) +
                (block > 0 ? "；格挡 +" + block : string.Empty) +
                (repaired > 0 ? "；恢复 " + repaired : string.Empty) +
                (energyRecovery > 0 ? "；能量 +" + energyRecovery : string.Empty) +
                (NavalActionMovementActive && card.NavalActionPointGain > 0 ? "；行动点 +" + card.NavalActionPointGain : string.Empty) +
                (powderCost > 0 ? "；火药 -" + powderCost : string.Empty) +
                (card.CannonballCost > 0 ? "；炮弹 -" + card.CannonballCost : string.Empty) +
                (woodCost > 0 ? "；木板 -" + woodCost : string.Empty) +
                (effects.MedicineCost > 0 && repaired > 0 ? "；药品 -1" : string.Empty) +
                (attachedFire > 0 ? "；附火施加 " + attachedFire + " 层燃烧" : string.Empty) +
                (moraleDelta > 0 ? "；士气 +" + moraleDelta : string.Empty) + "。";
            // A resolved card may be redrawn, but its next play uses the incremented
            // ID-based cost. Failed attempts never enter the discard pile or count.
            Battle.DiscardPile.Add(cardIndex);
            DrawCards(cardsToDraw);
            AdvanceGuidedBossCard();
            ResolveBattleOutcome();
            return true;
            }
            finally { CompleteNavalThunderFinisher(card, retainedThunderAtPlay); }
        }

        private void ApplyBattleCardStrategy(CoreBattleCard card)
        {
            if (ApplyNavalActionPointCard(card)) return;
            if (card.SchoolEffect != null) return;
            NavalBattleCardAction action = card.Action;
            switch (action)
            {
                case NavalBattleCardAction.Polish:
                    Battle.PlayerDeckBuff = Math.Max(Battle.PlayerDeckBuff, Math.Min(card.StrategyLimit, Battle.PlayerDeckBuff + card.StrategyAmount));
                    break;
                case NavalBattleCardAction.Load:
                case NavalBattleCardAction.PrecisionLoad:
                    ApplyLegacyLoadedShot(card);
                    break;
                case NavalBattleCardAction.FireRounds:
                    Battle.PlayerFireRounds = SchoolSum(Battle.PlayerFireRounds, card.StrategyAmount);
                    break;
            }
        }

        private static float NavalApproachMovement(float distanceMeters, float requestedMovement)
        {
            // Approaching cannot make disconnected ships touch. An existing
            // zero/negative range is not pushed apart by an approach either.
            return Math.Min(Math.Max(0f, requestedMovement), Math.Max(0f, distanceMeters - MinimumNavalDistanceMeters));
        }

        public bool CanRam(out string reason)
        {
            if (IsGuidedBossBattle && !GuidedBossAllowsRam)
                reason = "请先完成当前教学动作";
            else if (Phase != CoreDemoPhase.Battle || Battle == null || Battle.Stage != NavalBattleStage.Naval)
                reason = "仅炮战阶段可以冲撞";
            else if (NavalActionMovementActive && Battle.NavalAwaitingHeadingSelection) reason = "请先确定到达朝向";
            else
            {
                reason = NavalPlayerContactGeometryReason(false, Battle.PlayerHeadingDegrees);
                if (reason.Length == 0 && Battle.NavalActionPoints < 1) reason = "冲撞需要 1 行动点";
            }
            return reason.Length == 0;
        }

        public bool TryRam()
        {
            if (!CanRam(out string reason)) return false;
            Battle.NavalActionPoints -= 1;
            return ExecutePlayerRam();
        }

        // Called only after the direct or arrival entry point has validated and paid.
        private bool ExecutePlayerRam()
        {
            CoreBoardingFieldState inheritedFields = CaptureBoardingFields(false, true);
            NavalBoardingConnection connection = NavalRamConnection(true);
            string target = connection == NavalBoardingConnection.RamBowToBow ? "敌舰舰艏" :
                connection == NavalBoardingConnection.RamBowToStern ? "敌舰舰尾" : "敌舰侧舷";
            Battle.PlayerMovingTowardEnemy = false;
            float targetQuality = NavalRamTargetQuality(true);
            RamDamage(Battle.PlayerBowQuality, targetQuality, out int enemyDamage, out int selfDamage);
            enemyDamage = CoreBattleBalance.ScaleBattleValue(enemyDamage, CoreBattleBalance.PlayerRamDamageMultiplier);
            enemyDamage = NavalFieldAttackDamage(enemyDamage, true, false);
            selfDamage = ApplyCurseBattleDamage(selfDamage);
            Battle.EnemyHull = Math.Max(0, Battle.EnemyHull - enemyDamage);
            Hull = Math.Max(0, Hull - selfDamage);
            Morale = ClampMorale((long)Morale + 5);
            Battle.DistanceMeters = 30f;
            Battle.LastAction = "冲撞" + target +
                "：敌舰船体 -" + enemyDamage + "，己舰船体 -" + selfDamage + "（舰艏与防护结算，穿透格挡）；士气 +5。";
            if (ResolveBattleOutcome()) return true;

            // A successful non-lethal ram is itself the boarding connection.
            // End-on impacts settle alongside; a broadside impact stays offset.
            Battle.BoardingApproach = connection == NavalBoardingConnection.RamBowToBroadside
                ? NavalBoardingApproach.Quarter : NavalBoardingApproach.Alongside;
            Battle.BoardingConnection = connection;
            Battle.BoardingInitiatedByEnemy = false;
            EnterBattleStageWithFields(NavalBattleStage.Boarding, inheritedFields);
            if (Phase != CoreDemoPhase.Battle) return true;
            Battle.PlayerBlock = 0;
            Battle.EnemyBlock = 0;
            Battle.PlayerBurnStacks = 0;
            Battle.EnemyBurnTurns = 0;
            Battle.EnemyBurnDamage = 0;
            ApplySchoolBoardingEntry();
            BInitialize();
            BBoardingFieldsBeginTurn(true);
            Battle.PlayerMovingTowardEnemy = false;
            Battle.EnemyMovingTowardPlayer = false;
            Battle.LastAction = (connection == NavalBoardingConnection.RamBowToBow
                ? "角对角冲撞成功，两舰舰艏锁定。"
                : connection == NavalBoardingConnection.RamBowToStern ? "舰艏撞击舰尾成功，己舰舰艏卡住敌舰舰尾。"
                : "角对侧舷冲撞成功，己舰舰艏卡住敌舰侧舷。") +
                "船只已连接，进入接舷近战；己舰船体 -" + selfDamage + "，敌舰船体 -" + enemyDamage + "。";
            AddLog("冲撞" + target + "完成，船只连接，进入接舷白刃战。");
            BeginGuidedBossBoarding();
            return true;
        }

        private static void RamDamage(float bowQuality, float targetQuality, out int damage, out int selfDamage)
        {
            float bow = SafeBattleQuality(bowQuality);
            float target = SafeBattleQuality(targetQuality);
            damage = (int)Math.Round(Clamp(20f * bow / (target + 0.5f), 6f, 42f));
            selfDamage = (int)Math.Round(Clamp(14f * target / (bow + 0.5f), 3f, 28f));
        }

        public bool CanBoard(out string reason)
        {
            if (IsGuidedBossBattle && !GuidedBossAllowsBoard) reason = "请先完成当前教学动作";
            else if (Phase != CoreDemoPhase.Battle || Battle == null || Battle.Stage != NavalBattleStage.Naval)
                reason = "仅炮战阶段可以接舷";
            else if (NavalActionMovementActive && Battle.NavalAwaitingHeadingSelection) reason = "请先确定到达朝向";
            else
            {
                reason = NavalPlayerContactGeometryReason(true, Battle.PlayerHeadingDegrees);
                if (reason.Length == 0 && Battle.NavalActionPoints < 1) reason = "接舷需要 1 行动点";
            }
            return reason.Length == 0;
        }

        public bool TryBoard()
        {
            if (!CanBoard(out string reason)) return false;
            if (ResolveBattleOutcome()) return false;
            Battle.NavalActionPoints -= 1;
            return ExecutePlayerBoarding();
        }

        private bool ExecutePlayerBoarding()
        {
            CoreBoardingFieldState inheritedFields = CaptureBoardingFields(true, true);
            Battle.BoardingApproach = Battle.PlannedBoardingApproach;
            Battle.BoardingConnection = NavalBoardingConnection.Planned;
            Battle.BoardingInitiatedByEnemy = false;
            EnterBattleStageWithFields(NavalBattleStage.Boarding, inheritedFields);
            if (Phase != CoreDemoPhase.Battle) return true;
            Battle.PlayerBlock = 0;
            Battle.EnemyBlock = 0;
            Battle.PlayerBurnStacks = 0;
            Battle.EnemyBurnTurns = 0;
            Battle.EnemyBurnDamage = 0;
            ApplySchoolBoardingEntry();
            BInitialize();
            BBoardingFieldsBeginTurn(true);
            Battle.PlayerMovingTowardEnemy = false;
            Battle.EnemyMovingTowardPlayer = false;
            Battle.LastAction = "两舰接舷！更换为白刃战牌组；击败敌方船员即可夺船。";
            AddLog("接舷开始，船员进入白刃战。");
            BeginGuidedBossBoarding();
            return true;
        }

        public bool EndBattleTurn()
        {
            if (Phase != CoreDemoPhase.Battle || Battle == null) return false;
            if (NavalActionMovementActive && Battle.NavalAwaitingHeadingSelection) return false;
            if (IsGuidedBossBattle && !GuidedBossAllowsEndTurn) return ResolveBattleOutcome();
            Battle.LastNavalAttack = null;
            Battle.SettlementWindow = CoreBattleSettlementWindow.PlayerEnd;
            Battle.LastEnemyGuardAbsorbed = 0;
            Battle.BoardingEnemy.ResetTurnResults();
            Battle.Schools.LastGuardAbsorbed = 0;
            if (ResolveBattleOutcome()) return true;
            NavalBattleStage previousStage = Battle.Stage;
            bool preparing = previousStage == NavalBattleStage.Preparation;
            bool boardingRules = BActive;
            // Predict before player-end effects; execution below sees their committed state.
            string enemyIntent = boardingRules ? GetEnemyIntent() : null;
            if (boardingRules)
            {
                if (BPlayerEnd()) return true;
                if (!HasBoardingEnemyScript)
                {
                    ResetNavalThunderChain(false);
                    Battle.BoardingEnemy.Mana = CoreBoardingEnemyState.ManaLimit;
                    BBoardingFieldsBeginTurn(false);
                }
            }
            else
            {
                if (!preparing && ApplyNavalPlayerBurn()) return true;
                if (!preparing && NavalFieldsEndTurn(true)) return true;
                NavalExpansionEndPlayerTurn();
                EndSchoolPlayerWindow();
                if (!preparing)
                {
                    Battle.EnemyBlock = 0;
                    Battle.EnemyTidalGuard = 0;
                    NavalFieldsBeginTurn(false);
                }
                if (ResolveBattleOutcome()) return true;
            }
            // Damage-over-time resolves at the affected side's turn end, after its actions.
            if (enemyIntent == null) enemyIntent = GetEnemyIntent();
            Battle.SettlementWindow = CoreBattleSettlementWindow.EnemyAction;
            ExecuteEnemyPlan(BuildEnemyPlan(), enemyIntent);
            if (ResolveBattleOutcome()) return true;
            Battle.SettlementWindow = CoreBattleSettlementWindow.EnemyEnd;
            if (!preparing && Battle.Stage == previousStage)
            {
                if (boardingRules)
                {
                    if (BEnemyEnd()) return true;
                }
                else
                {
                    if (ApplySchoolNaturalBurn()) return true;
                    if (ApplySchoolCurseTick()) return true;
                    if (NavalFieldsEndTurn(false)) return true;
                    CompleteSchoolEnemyTurn();
                    NavalExpansionEndEnemyTurn();
                }
            }
            Battle.Turn += 1;
            // Preparing and entering a new stage carry the current pool. Only a
            // subsequent player turn refills mana, discarding any excess first.
            if (!preparing) Battle.Schools.Mana = Math.Max(0, Battle.Schools.ManaLimit);
            Battle.BeginNewCardTurn();
            if (preparing)
            {
                string preparationAction = Battle.LastAction;
                Battle.PlayerBlock = Math.Max(0, Battle.PlayerBlock - Battle.Schools.CurseGuard);
                Battle.Schools.CurseGuard = 0;
                EnterBattleStage(NavalBattleStage.Naval);
                Battle.LastAction = preparationAction + " 准备结束，双方进入炮战；装填与甲板强化保留。";
            }
            else if (previousStage != NavalBattleStage.Boarding && Battle.Stage == NavalBattleStage.Boarding)
            {
                // Enemy ram already connected the ships, dealt one opening melee
                // strike and drew the boarding hand. Do not discard/redraw that
                // hand, advance its phase again, or execute a second enemy attack.
                Battle.Energy = Battle.MaxEnergy;
                Battle.CardsPlayedThisTurn = 0;
                if (BActive && BPlayerStart()) return true;
            }
            else
            {
                Battle.PlayerBlock = boardingRules ? BRemainingGuardAtNewTurn() : 0;
                Battle.Schools.TidePaidBlock = 0;
                Battle.Schools.CurseGuard = 0;
                if (boardingRules) BDiscardHandAtTurnEnd();
                else
                {
                    Battle.DiscardPile.AddRange(Battle.Hand);
                    Battle.Hand.Clear();
                }
                Battle.PhaseTurn += 1;
                Battle.Energy = Battle.MaxEnergy;
                Battle.CardsPlayedThisTurn = 0;
                if (boardingRules && BPlayerStart()) return true;
                DrawCards(5);
            }
            AdvanceGuidedBossTurn();
            BeginNavalActionPointTurn();
            if (Battle.Stage == NavalBattleStage.Naval)
            {
                NavalExpansionBeginPlayerTurn();
                NavalFieldsBeginTurn(true);
                ResolveBattleOutcome();
            }
            return true;
        }

        private enum EnemyBattleAction { Prepare, Advance, Guard, Cannon, Ram, Melee }
        private struct EnemyBattlePlan
        {
            public EnemyBattleAction Action;
            public float Move;
            public int Attack;
            public int Guard;
            public int Load;
            public int Repair;
            public CoreBoardingEnemyTurn BoardingTurn;
            public CoreNavalBossMove BossMove;
            public bool BossRush;
        }

        public bool EnemyWillRam => Phase == CoreDemoPhase.Battle && Battle != null &&
            BuildEnemyPlan().Action == EnemyBattleAction.Ram;
        public float EnemyRamRangeMeters => NavalGridCellMeters;
        public int EnemyRamFirstStrikeDamage => EnemyWillRam ? ApplyCurseBattleDamage(Battle.Profile?.MeleeDamage ?? 8) : 0;

        private bool EnemyChoosesRam()
        {
            if (Battle.Stage != NavalBattleStage.Naval || !EnemyPlannedRamGeometryIsLegal) return false;
            double chance = Battle.Profile?.RamChance ?? 0d;
            if (chance <= 0d) return false;
            // Pure per-battle/per-turn roll, independent of deck/loot RNG. Reading
            // intent, spending cards or toggling range never consumes randomness.
            uint value;
            unchecked
            {
                value = Battle.DecisionSeed ^ ((uint)Battle.Turn * 0x9E3779B9u);
                value ^= value >> 16; value *= 0x7FEB352Du;
                value ^= value >> 15; value *= 0x846CA68Bu;
                value ^= value >> 16;
            }
            return value / 4294967296d < chance;
        }

        private bool RollBoardingCritical()
        {
            int chance = BoardingCriticalChancePercent;
            if (chance <= 0 || Battle == null || Battle.Stage != NavalBattleStage.Boarding) return false;

            uint value;
            unchecked
            {
                battleCriticalRollCount += 1;
                value = Battle.DecisionSeed ^ ((uint)battleCriticalRollCount * 0x9E3779B9u);
                value ^= value >> 16; value *= 0x7FEB352Du;
                value ^= value >> 15; value *= 0x846CA68Bu;
                value ^= value >> 16;
            }
            return value / 4294967296d < chance / 100d;
        }

        private static int DoubleBattleDamage(int damage)
        {
            return (int)Math.Min((long)int.MaxValue, Math.Max(0L, (long)damage * 2L));
        }

        // This pure decision function is shared by the preview and execution. Player maneuvers
        // update the public intent immediately, before the player chooses to end their turn.
        private EnemyBattlePlan BuildEnemyPlan()
        {
            EnemyBattlePlan plan = BuildEnemyBasePlan();
            if (EnemyNavalBossRushThisTurn)
            {
                plan.BossRush = true;
                plan.Move = NavalBossRushMaxCells * NavalGridCellMeters;
            }
            return plan;
        }

        private EnemyBattlePlan BuildEnemyBasePlan()
        {
            VoyagePoint enemyTarget = NavalPlayerPositionForEnemyPlan;
            bool cannonInRange = NavalCannonInRange(enemyTarget, Battle.EnemyPositionMeters);
            float distance = NavalPointDistance(enemyTarget, Battle.EnemyPositionMeters);
            CoreTrainingTarget target = Battle.Profile?.TrainingTarget;
            if (target != null)
            {
                bool preparing = Battle.Stage == NavalBattleStage.Preparation;
                bool boarding = Battle.Stage == NavalBattleStage.Boarding;
                return new EnemyBattlePlan
                {
                    Action = preparing ? EnemyBattleAction.Prepare : boarding ? EnemyBattleAction.Melee :
                        cannonInRange ? EnemyBattleAction.Cannon : EnemyBattleAction.Guard,
                    Attack = preparing ? 0 : boarding ? target.MeleeDamage : cannonInRange ? target.CannonDamage : 0,
                    Guard = target.Guard,
                    Repair = preparing ? 0 : target.Repair,
                };
            }
            if (IsGuidedBossBattle && GuidedBossLesson != GuidedBossLesson.FinalBattle)
            {
                if (Battle.Stage == NavalBattleStage.Boarding) return BuildBoardingEnemyPlan();
                if (Battle.Stage == NavalBattleStage.Naval && GuidedBossLesson == GuidedBossLesson.Gunnery && cannonInRange)
                    return new EnemyBattlePlan { Action = EnemyBattleAction.Cannon, Attack = 12 };
                return new EnemyBattlePlan { Action = EnemyBattleAction.Prepare };
            }
            CoreBattleProfile profile = Battle.Profile;
            int guard = profile?.Guard ?? (Battle.IsBoss ? 9 : 6);
            int cannonDamage = profile?.CannonDamage ?? (Battle.IsBoss ? 12 : 8);
            int meleeDamage = profile?.MeleeDamage ?? (Battle.IsBoss ? 12 : 8);
            CoreBattleArchetype archetype = profile?.Archetype ?? CoreBattleArchetype.Balanced;
            bool guardTurn = archetype == CoreBattleArchetype.Ironclad ? Battle.PhaseTurn % 2 == 0 : Battle.PhaseTurn % 3 == 2;
            if (Battle.Stage == NavalBattleStage.Preparation)
                return new EnemyBattlePlan { Action = EnemyBattleAction.Prepare, Move = 0f, Guard = Math.Max(4, profile?.InitialGuard ?? 0), Load = Math.Max(0, Math.Min(4, 12 - Battle.EnemyLoadedShot)) };
            if (Battle.Stage == NavalBattleStage.Boarding)
                return BuildBoardingEnemyPlan();
            if (!cannonInRange)
                return new EnemyBattlePlan { Action = EnemyBattleAction.Advance, Move = NavalApproachMovement(distance, archetype == CoreBattleArchetype.Tempest ? 240f : 180f), Guard = 2, Load = Math.Max(0, Math.Min(2, 12 - Battle.EnemyLoadedShot)) };
            if (TryBuildNavalBossPlan(distance, out EnemyBattlePlan bossPlan))
            {
                // Let the setup and telegraphed burst finish. Only the recovery
                // action may choose a legal ram; movement never forces boarding.
                if (bossPlan.BossMove.AllowRam && !EnemyNavalBossRushThisTurn && EnemyChoosesRam())
                    return new EnemyBattlePlan { Action = EnemyBattleAction.Ram, Attack = meleeDamage };
                return bossPlan;
            }
            if (!EnemyNavalBossRushThisTurn && EnemyChoosesRam())
                return new EnemyBattlePlan { Action = EnemyBattleAction.Ram, Attack = meleeDamage };
            if (guardTurn)
                return new EnemyBattlePlan { Action = EnemyBattleAction.Guard, Move = NavalApproachMovement(distance, 90f), Guard = guard, Load = Math.Max(0, Math.Min(4, 12 - Battle.EnemyLoadedShot)) };
            return new EnemyBattlePlan
            {
                Action = EnemyBattleAction.Cannon,
                Move = distance > 60f ? NavalApproachMovement(distance, archetype == CoreBattleArchetype.Artillery ? 60f : archetype == CoreBattleArchetype.Raider ? 150f : 100f) : 0f,
                Attack = cannonDamage + Battle.PhaseTurn / 4 + Battle.EnemyLoadedShot,
            };
        }

        public string GetEnemyIntent()
        {
            string intent = GetEnemyBaseIntent();
            if (EnemyNavalBossRushThisTurn) intent = NavalBossRushDescription + "\n" + intent;
            else if (EnemyNavalBossFieldImmunityMaskThisTurn != 0)
                intent = EnemyNavalBossFieldImmunityDescription + "\n" + intent;
            if (Phase != CoreDemoPhase.Battle || Battle == null || Battle.Stage != NavalBattleStage.Naval)
                return intent;
            string fields = GetEnemyNavalFieldIntent();
            return fields.Length == 0 ? intent : intent + "\n" + fields;
        }

        private string GetEnemyBaseIntent()
        {
            if (Battle == null || Phase != CoreDemoPhase.Battle) return string.Empty;
            EnemyBattlePlan plan = BuildEnemyPlan();
            CoreNavalMovementFieldPreview fieldPreview = NavalFieldsActive ? GetEnemyNavalFieldPreview(plan.Move) : null;
            int quotedGuard = BActive ? CTBEnemyBlockGain(plan.Guard, false) : NavalEnemyActionGuard(plan, fieldPreview);
            string burn = Battle.Stage != NavalBattleStage.Preparation && HasSchoolFire && fieldPreview?.EntersBoarding != true
                ? "回合结束燃烧 " + Battle.EnemyBurnDamage + "；" : string.Empty;
            if (plan.BossMove != null) burn += NavalBossMoveDescription(plan, fieldPreview) + "\n";
            if (NavalBossPaymentExecutesActor(plan, fieldPreview)) return burn;
            if (plan.BoardingTurn != null) return BoardingEnemyIntent(plan, burn);
            if (Battle.Profile?.TrainingTarget != null)
            {
                if (Battle.Stage == NavalBattleStage.Preparation)
                    return "木桩准备：静止；格挡设为 " + plan.Guard + "（本轮不攻击、不回复）";
                int damage = Battle.Stage == NavalBattleStage.Boarding
                    ? ApplyCurseBattleDamage(SchoolEnemyRawAttack(plan.Attack)) : EnemyCannonDamage(plan.Attack, false, fieldPreview);
                if (BActive) damage = BEnemyAttack(damage, false);
                int healing = IsBattleHealingBlocked(false) ? 0 : Math.Min(plan.Repair,
                    Battle.Stage == NavalBattleStage.Boarding ? Battle.EnemyMaxBoardingHealth - Battle.EnemyBoardingHealth : Battle.EnemyMaxHull - Battle.EnemyHull);
                return burn + "木桩：格挡设为 " + quotedGuard + "；回复 " + Math.Max(0, healing) + "；" +
                    (Battle.Stage == NavalBattleStage.Naval && !NavalCannonInRange(NavalPlayerPositionForEnemyPlan, Battle.EnemyPositionMeters) ? "目标超出4格炮击范围，本轮不攻击" :
                    (Battle.Stage == NavalBattleStage.Boarding ? "白刃伤害 " : "炮击伤害 ") + damage + "（格挡前）");
            }
            float travel = QuantizeNavalEnemyTravel(plan.Move);
            if (NavalFieldsActive && !plan.BossRush) travel = NavalActiveMovementLimit(false, (int)Math.Round(travel / NavalGridCellMeters)) * NavalGridCellMeters;
            string move = plan.Move > 0f ? (IsGuidedBossBattle ? "靠近 " : "整格机动，最多 ") + travel.ToString("0") + " 米；" : string.Empty;
            switch (plan.Action)
            {
                case EnemyBattleAction.Prepare:
                    return "准备：" + move + "装填 +" + NavalExpansionEnemyLoadGain(plan.Load) + "；格挡设为 " + quotedGuard + "（本轮不攻击）";
                case EnemyBattleAction.Advance:
                    return burn + "追击：" + move + "装填 +" + NavalExpansionEnemyLoadGain(plan.Load) + "；格挡设为 " + quotedGuard;
                case EnemyBattleAction.Guard:
                    return burn + move + "防御：格挡设为 " + quotedGuard + (plan.Load > 0 ? "；装填 +" + NavalExpansionEnemyLoadGain(plan.Load) : string.Empty);
                case EnemyBattleAction.Ram:
                    RamDamage(Battle.EnemyBowQuality, EnemyPlannedRamTargetQuality,
                        out int damage, out int recoil);
                    damage = EnemyNavalFieldAttackDamage(ApplyCurseBattleDamage(SchoolEnemyRawAttack(damage)), false, fieldPreview);
                    damage = NavalExpansionEnemyAttackDamage(damage);
                    return burn + "冲撞→接舷：己舰 -" + damage + " / 敌舰 -" + recoil + "（穿透格挡）\n" +
                        "敌方先手：船员伤害 " + ApplyCurseBattleDamage(plan.Attack) + "\n避让：航行离开敌舰船头正前方的相邻格";
                case EnemyBattleAction.Melee:
                    int melee = ApplyCurseBattleDamage(SchoolEnemyRawAttack(plan.Attack));
                    return burn + "白刃突击：船员伤害 " + (BActive ? BEnemyAttack(melee, false) : melee) + "（格挡前）";
                default:
                    return burn + move + "炮击：伤害 " + EnemyCannonDamage(NavalBossAttack(plan, fieldPreview), false, fieldPreview) + "（装甲后、格挡前）" +
                        (plan.Move > 0f ? "；机动后仍在4格内时开炮" : string.Empty) +
                        (quotedGuard > 0 ? "；行动后格挡 " + quotedGuard : string.Empty);
            }
        }

        private void ExecuteEnemyPlan(EnemyBattlePlan plan, string intent)
        {
            if (plan.BoardingTurn != null)
            {
                ExecuteBoardingEnemyPlan(plan, intent);
                return;
            }
            // Authored boss movement can sidestep a dangerous field even at adjacent range.
            // Its shared path planner already rejects occupied/invalid destinations.
            float movement = plan.BossRush || plan.BossMove != null
                ? plan.Move : NavalApproachMovement(Battle.DistanceMeters, plan.Move);
            if (movement > 0f) AdvanceTacticalEnemy(movement);
            if (ResolveBattleOutcome()) return;
            // Enemy guard lasts until their next action; it does not accumulate forever.
            Battle.EnemyBlock = BActive ? CTBEnemyBlockGain(plan.Guard, true) :
                SchoolSum(Battle.Stage == NavalBattleStage.Naval ? Battle.EnemyBlock : 0, NavalExpansionEnemyBlockGain(plan.Guard, true));
            Battle.EnemyLoadedShot = Math.Min(12, Battle.EnemyLoadedShot + NavalExpansionEnemyLoadGain(plan.Load, true));
            if (Battle.Profile?.TrainingTarget != null)
            {
                Battle.EnemyLoadedShot = 0;
                Battle.EnemyMovingTowardPlayer = false;
                // Only a living target reaches its action. Heal the active pool
                // before attack/guard reactions, capped at its maximum.
                if (Battle.Stage == NavalBattleStage.Boarding)
                    Battle.EnemyBoardingHealth = (int)Math.Min(Battle.EnemyMaxBoardingHealth, (long)Battle.EnemyBoardingHealth + (IsBattleHealingBlocked(false) ? 0 : plan.Repair));
                else if (Battle.Stage == NavalBattleStage.Naval)
                    Battle.EnemyHull = (int)Math.Min(Battle.EnemyMaxHull, (long)Battle.EnemyHull + NavalExpansionHealing(false, plan.Repair));
            }
            // A turn or movement can change the occupied cells after the intent was quoted.
            if (plan.Action == EnemyBattleAction.Cannon && !NavalCannonTargetInRange)
            {
                Battle.LastEnemyGuardAbsorbed = 0;
                Battle.LastAction = Battle.EnemyName + "：目标超出4格炮击范围，本次未开炮。";
                return;
            }
            int bossRawAttack = NavalBossAttack(plan);
            if (BeginNavalBossMove(plan)) return;
            if (plan.Action == EnemyBattleAction.Cannon || plan.Action == EnemyBattleAction.Melee)
            {
                int attack = plan.Action == EnemyBattleAction.Cannon ? EnemyCannonDamage(bossRawAttack, true) : ApplyCurseBattleDamage(SchoolEnemyRawAttack(plan.Attack));
                if (BActive) attack = BEnemyAttack(attack, true);
                int absorbed = Math.Min(Battle.PlayerBlock, attack - NavalBossPiercing(plan, attack));
                Battle.LastEnemyGuardAbsorbed = absorbed;
                int damage = Math.Max(0, attack - absorbed);
                int actualHealthLost = Math.Min(Math.Max(0, Hull), damage);
                int navalHealthBefore = Hull, navalGuardBefore = Battle.PlayerBlock;
                Battle.PlayerBlock -= absorbed;
                if (plan.Action == EnemyBattleAction.Melee)
                {
                    Battle.BoardingEnemy.RecordAttack(Battle.PlayerBoardingHealth, damage);
                    Battle.PlayerBoardingHealth = Math.Max(0, Battle.PlayerBoardingHealth - damage);
                }
                else
                {
                    Battle.EnemyLoadedShot = 0;
                    Hull = Math.Max(0, Hull - damage);
                    Battle.LastNavalAttack = new CoreNavalResolvedAttack(navalHealthBefore, Hull, navalGuardBefore, Battle.PlayerBlock);
                    if (damage > 0)
                        Morale = ClampMorale((long)Morale - 1);
                }
                Battle.LastAction = Battle.EnemyName + "：" + intent + "；格挡吸收 " + absorbed + "，实际损失 " + damage + "。";
                HealNavalBossAttack(plan, actualHealthLost);
                if (Battle.Stage == NavalBattleStage.Boarding ? BResolveCurseExecutions() : CheckNavalCurseExecution())
                { ResolveBattleOutcome(); return; }
                if (Hull > 0 && (Battle.Stage != NavalBattleStage.Boarding || Battle.PlayerBoardingHealth > 0))
                {
                    if (BActive) BAfterEnemyAttack(absorbed, damage);
                    else ApplySchoolGuardReaction(absorbed);
                }
                CompleteNavalBossMove(plan, actualHealthLost);
            }
            else if (plan.Action == EnemyBattleAction.Ram)
            {
                CoreBoardingFieldState inheritedFields = CaptureBoardingFields(true, false);
                NavalBoardingConnection connection = NavalRamConnection(false);
                RamDamage(Battle.EnemyBowQuality, NavalRamTargetQuality(false), out int damage, out int recoil);
                damage = EnemyNavalFieldAttackDamage(ApplyCurseBattleDamage(SchoolEnemyRawAttack(damage)), true);
                damage = NavalExpansionEnemyAttackDamage(damage, true);
                Hull = Math.Max(0, Hull - damage);
                Battle.EnemyHull = Math.Max(0, Battle.EnemyHull - recoil);
                // The hull collision has committed even when it is lethal and boarding never starts.
                Battle.RecordEnemyRam(connection);
                Morale = ClampMorale((long)Morale - 5);
                Battle.EnemyMovingTowardPlayer = false;
                Battle.DistanceMeters = 30f;
                Battle.LastAction = Battle.EnemyName + "冲撞：己舰船体 -" + damage + "，敌舰反伤 " + recoil + "；士气 -5。";
                if (ResolveBattleOutcome()) return;
                Battle.BoardingApproach = connection == NavalBoardingConnection.RamBowToBroadside
                    ? NavalBoardingApproach.Quarter : NavalBoardingApproach.Alongside;
                Battle.BoardingConnection = connection;
                Battle.BoardingInitiatedByEnemy = true;
                EnterBattleStageWithFields(NavalBattleStage.Boarding, inheritedFields);
                if (Phase != CoreDemoPhase.Battle) return;
                // Ship guard does not become crew guard when either side boards.
                Battle.PlayerBlock = 0;
                Battle.EnemyBlock = 0;
                Battle.PlayerBurnStacks = 0;
            Battle.EnemyBurnTurns = 0;
                Battle.EnemyBurnDamage = 0;
                ApplySchoolBoardingEntry();
                BInitialize();
                BBoardingFieldsBeginTurn(false);
                Battle.PlayerMovingTowardEnemy = false;
                Battle.EnemyMovingTowardPlayer = false;
                int rawFirstStrike = ApplyCurseBattleDamage(plan.Attack);
                if (BActive) rawFirstStrike = BEnemyAttack(rawFirstStrike, true);
                int firstStrike = AbsorbSchoolBoardingFirstStrike(rawFirstStrike);
                Battle.PlayerBoardingHealth = Math.Max(0, Battle.PlayerBoardingHealth - firstStrike);
                if (BResolveCurseExecutions()) { ResolveBattleOutcome(); return; }
                if (BActive && Battle.PlayerBoardingHealth > 0) BAfterEnemyAttack(rawFirstStrike - firstStrike, firstStrike);
                Battle.LastAction = Battle.EnemyName + "冲撞连接成功：己舰船体 -" + damage + "，敌舰反伤 " + recoil +
                    "；士气 -5；进入接舷，敌方先手造成船员伤害 " + firstStrike + "。";
                AddLog(Battle.LastAction);
                // This transition skips the usual enemy-end branch in EndBattleTurn.
                // Settle the inherited arena once, before the first player start.
                if (Battle.BoardingFields.Fields.Count > 0) BEnemyEnd();
            }
            else
            {
                Battle.LastAction = Battle.EnemyName + "：" + intent + "。";
                CompleteNavalBossMove(plan, 0);
            }
        }

        private int EnemyCannonDamage(int rawAttack, bool consume = false, CoreNavalMovementFieldPreview fieldPreview = null)
        {
            int bonus = 0;
            if (consume) Battle.NavalFields.Enemy.BloodAttackBonus = 0;
            int armored = CoreNavalStatRules.ReduceIncomingCannonDamage(
                EnemyNavalFieldAttackDamage(SchoolEnemyRawAttack(SchoolSum(rawAttack, bonus)), consume, fieldPreview), EffectiveArmor);
            return NavalCurseCannonReduction(NavalExpansionEnemyAttackDamage(ApplyCurseBattleDamage(armored), consume), consume);
        }

        private bool ResolveBattleOutcome()
        {
            if (Battle == null) return true;
            if (Battle.Stage == NavalBattleStage.Boarding) BResolveCurseExecutions();
            else CheckNavalCurseExecution();
            if (InterceptGuidedBossExerciseOutcome()) return true;
            if (Battle.Profile?.TrainingTarget != null)
            {
                if (TrainingOutcome == CoreTrainingBattleOutcome.Victory || TrainingOutcome == CoreTrainingBattleOutcome.Defeat)
                    return true;
                bool defeated = Hull <= 0 || (Battle.Stage == NavalBattleStage.Boarding && Battle.PlayerBoardingHealth <= 0);
                bool victorious = Battle.EnemyHull <= 0 || (Battle.Stage == NavalBattleStage.Boarding && Battle.EnemyBoardingHealth <= 0);
                if (!defeated && !victorious) return false;
                Battle.BoardingFields = new CoreBoardingFieldState();
                ClearNavalFields();
                NavalExpansionClear();
                ClearNavalArrivalContactState();
                TrainingOutcome = defeated ? CoreTrainingBattleOutcome.Defeat : CoreTrainingBattleOutcome.Victory;
                Phase = trainingReturnPhase;
                voyageRandom = trainingPreviousVoyageRandom;
                trainingPreviousVoyageRandom = null;
                AddLog(defeated ? "木桩训练失败；保留战斗与物资消耗记录，不结算航行。" : "木桩训练胜利；保留战斗与物资消耗记录，不发放奖励。");
                return true;
            }
            // Resolve the player's loss first so mutual sinking cannot award a victory payout.
            if (Hull <= 0 || (Battle.Stage == NavalBattleStage.Boarding && Battle.PlayerBoardingHealth <= 0))
            {
                Battle.BoardingFields = new CoreBoardingFieldState();
                ClearNavalFields();
                NavalExpansionClear();
                AddLog(CoreLegendaryWarships.IsSparringEncounter(ActiveEncounter?.Id)
                    ? "己方在切磋中认输，双方停止交手，本次航行按失败保护结算。"
                    : Hull <= 0 ? "己方旗舰沉没，航行失败。" : "己方接舷队失去战斗能力，航行失败。");
                FinishVoyage(VoyageOutcome.Failure);
                return true;
            }
            if (Battle.EnemyHull <= 0 || (Battle.Stage == NavalBattleStage.Boarding && Battle.EnemyBoardingHealth <= 0))
            {
                Battle.BoardingFields = new CoreBoardingFieldState();
                ClearNavalFields();
                NavalExpansionClear();
                WinBattle();
                return true;
            }
            return false;
        }

        private void StartBattle(CoreEncounter encounter, bool isBoss)
        {
            StartBattleCore(encounter, isBoss, null);
        }

        private void StartBattleCore(CoreEncounter encounter, bool isBoss, CoreTrainingTarget trainingTarget,
            int? startingCrewHealth = null, string sequenceKey = null)
        {
            TrainingOutcome = trainingTarget == null ? CoreTrainingBattleOutcome.None : CoreTrainingBattleOutcome.InProgress;
            GuidedBossStep = GuidedBossBattleStep.None;
            battleCriticalRollCount = 0;
            LastBattleWasCaptured = false;
            if (trainingTarget != null) BattleTerrain = SeaTerrainType.OpenWater;
            else if (activeNode != null)
            {
                switch (activeNode.TerrainConstraint)
                {
                    case EventTerrainConstraint.Beach: BattleTerrain = SeaTerrainType.Beach; break;
                    case EventTerrainConstraint.Strait: BattleTerrain = SeaTerrainType.Strait; break;
                    case EventTerrainConstraint.Reef: BattleTerrain = SeaTerrainType.Reef; break;
                    case EventTerrainConstraint.Fog: BattleTerrain = SeaTerrainType.FogBank; break;
                    case EventTerrainConstraint.Storm: BattleTerrain = SeaTerrainType.StormFront; break;
                    default: BattleTerrain = SeaTerrainType.OpenWater; break;
                }
            }
            else if (Map != null)
            {
                SeaTerrainType terrain = Map.TerrainAt(Position);
                BattleTerrain = terrain == SeaTerrainType.Island ? SeaTerrainType.OpenWater : terrain;
            }
            else BattleTerrain = SeaTerrainType.OpenWater;

            if (voyageRandom == null) voyageRandom = new Mulberry32Random(1);
            bool guidedBoss = GuidedTutorialLeg >= CoreGuidedTutorialContent.NavigatorVoyageLeg &&
                encounter.Id == CoreGuidedTutorialContent.BossEventId;
            CoreBattleProfile profile = trainingTarget == null ? CoreBattleBalance.Resolve(encounter,
                IsTreasureVoyage ? TreasureBattleTier : SelectedRouteIndex, isBoss, guidedBoss) : new CoreBattleProfile(trainingTarget);
            int enemyHull = profile.EnemyHull;
            int boardingHealth = profile.BoardingHealth;
            int playerBoardingHealth = CoreBattleBalance.BoardingHealthForOrdinaryCrew(SelectedOrdinaryCrew);
            Battle = new CoreBattleState
            {
                EnemyName = encounter.Name,
                EnemyHull = enemyHull,
                EnemyMaxHull = enemyHull,
                Energy = 3,
                MaxEnergy = 3,
                Turn = 1,
                PhaseTurn = 1,
                Stage = NavalBattleStage.Preparation,
                IsBoss = profile.IsBoss,
                Profile = profile,
                EnemyBlock = profile.InitialGuard,
                DistanceMeters = 600f,
                Alignment = NavalBattleAlignment.HeadOn,
                PlannedBoardingApproach = NavalBoardingApproach.Alongside,
                BoardingApproach = NavalBoardingApproach.Alongside,
                PlayerBoardingHealth = trainingTarget != null && startingCrewHealth.HasValue ? startingCrewHealth.Value : playerBoardingHealth,
                PlayerMaxBoardingHealth = playerBoardingHealth,
                TrainingStartingCrewHealth = trainingTarget == null ? null : startingCrewHealth,
                PlayerOrdinaryCrew = SelectedOrdinaryCrew,
                EnemyOrdinaryCrew = profile.OrdinaryCrewCount,
                DecisionSeed = SeedHash.Compute((Map == null ? SelectedSeed : Map.Seed) + "::BATTLE::" + encounter.Id +
                    (trainingTarget == null ? string.Empty : TrainingSequenceSuffix(sequenceKey))),
                EnemyBoardingHealth = boardingHealth,
                EnemyMaxBoardingHealth = boardingHealth,
                PlayerBowQuality = battleBowQuality,
                PlayerHullQuality = battleHullQuality,
                EnemyBowQuality = profile.BowQuality,
                EnemyHullQuality = profile.HullQuality,
                LastAction = "相距 600 米。准备阶段可部署卡牌并规划航行，结束回合后进入炮战。",
            };
            Battle.BoardingEnemy.Deck = CoreBoardingEnemyCatalog.ForEncounter(encounter.Id);
            // Each effective deck is a fresh battle-only copy. Crew bonuses never enter saves.
            // The guided first boss teaches combat with the built-in decks. Older
            // saves may already have custom decks: use the defaults only in these
            // draw piles, without rewriting their loadouts or granting upgrades.
            for (int index = 0; index < 3; index += 1)
            {
                NavalBattleStage stage = (NavalBattleStage)index;
                List<int> drawPile = Battle.DrawPileFor(stage);
                drawPile.AddRange(guidedBoss ? (IEnumerable<int>)defaultBattleDecks[index] : GetBattleDeck(stage));
                drawPile.AddRange(GetBonusBattleCards(stage));
                Shuffle(drawPile);
            }
            Phase = CoreDemoPhase.Battle;
            DrawCards(5);
            InitializeGuidedBossScript(encounter);
            Battle.NavalStartPlayerHull = Hull;
            Battle.NavalStartEnemyHull = Battle.EnemyHull;
            AddLog("与「" + encounter.Name + "」进入三阶段卡牌海战：策略准备 → 舰船炮战 → 接舷白刃战。");
        }

        private void EnterBattleStage(NavalBattleStage stage) => EnterBattleStageWithFields(stage, null);

        private void EnterBattleStageWithFields(NavalBattleStage stage, CoreBoardingFieldState inheritedFields)
        {
            NavalBattleStage previousStage = Battle.Stage;
            if (stage == NavalBattleStage.Boarding)
            {
                Battle.BoardingFields = inheritedFields ?? new CoreBoardingFieldState();
                if (Battle.BoardingFields.Contains(CoreNavalFieldKind.Thunder))
                    Battle.BoardingEnemy.ThunderChain = Battle.NavalFields.Enemy.ThunderChain;
                ClearNavalFields();
                NavalExpansionClear();
            }
            Battle.DiscardPile.AddRange(Battle.Hand);
            Battle.Hand.Clear();
            Battle.Stage = stage;
            BeginNavalActionPointTurn();
            Battle.PhaseTurn = 1;
            Battle.Energy = Battle.MaxEnergy;
            Battle.CardsPlayedThisTurn = 0;
            // A new phase is a fresh card-cost window, even when boarding was
            // initiated during the player's current turn rather than EndTurn.
            Battle.BeginNewCardTurn();
            DrawCards(5);
            if (stage == NavalBattleStage.Boarding && previousStage != NavalBattleStage.Boarding)
            {
                ApplyNavalDamageToBoardingHealth();
                if (Battle.TrainingStartingCrewHealth.HasValue && Battle.PlayerBoardingHealth <= 0)
                    ResolveBattleOutcome();
            }
        }

        private void ApplyNavalDamageToBoardingHealth()
        {
            if (Battle == null) return;
            int fullCrewAfterNavalDamage = BoardingHealthAfterNavalDamage(
                Battle.PlayerMaxBoardingHealth, Battle.NavalStartPlayerHull, Hull, MaxHull);
            if (Battle.Profile?.TrainingTarget != null && Battle.TrainingStartingCrewHealth.HasValue)
            {
                int additionalLoss = Battle.PlayerMaxBoardingHealth - fullCrewAfterNavalDamage;
                Battle.TrainingBoardingCarryover = Math.Min(Battle.PlayerBoardingHealth, additionalLoss);
                Battle.PlayerBoardingHealth -= Battle.TrainingBoardingCarryover;
            }
            else Battle.PlayerBoardingHealth = fullCrewAfterNavalDamage;
            Battle.EnemyBoardingHealth = BoardingHealthAfterNavalDamage(
                Battle.EnemyMaxBoardingHealth, Battle.NavalStartEnemyHull, Battle.EnemyHull, Battle.EnemyMaxHull);
        }

        private static int BoardingHealthAfterNavalDamage(int maxBoardingHealth, int startHull, int currentHull, int maxHull)
        {
            if (maxBoardingHealth <= 0) return 0;
            if (maxHull <= 0) return maxBoardingHealth;

            // Half of the naval-stage hull damage percentage carries into
            // boarding health. Keep the calculation rational so the required
            // upward rounding is exact even when the hull maximum is odd.
            int damage = Math.Max(0, startHull - Math.Max(0, currentHull));
            damage = Math.Min(maxHull, damage);
            long denominator = 2L * maxHull;
            long numerator = (long)maxBoardingHealth * Math.Max(0L, denominator - damage);
            long health = numerator / denominator + (numerator % denominator == 0L ? 0L : 1L);
            return (int)Math.Max(1L, Math.Min(maxBoardingHealth, health));
        }

        private void DrawCards(int count)
        {
            if (Battle == null) return;
            for (int drawn = 0; drawn < count && Battle.Hand.Count < 8; drawn += 1)
            {
                if (Battle.DrawPile.Count == 0)
                {
                    if (Battle.DiscardPile.Count == 0) return;
                    Battle.DrawPile.AddRange(Battle.DiscardPile);
                    Battle.DiscardPile.Clear();
                    Shuffle(Battle.DrawPile);
                    if (IsGuidedBossBattle) GuidedBossDeckShuffles++;
                }
                int last = Battle.DrawPile.Count - 1;
                Battle.Hand.Add(Battle.DrawPile[last]);
                Battle.DrawPile.RemoveAt(last);
            }
        }
    }
}
