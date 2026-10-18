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
            if (card.Action == NavalBattleCardAction.Sabotage && Battle.DistanceMeters > 150f) return "水下破坏需要距离不超过 150 米";
            if (card.SchoolId.Length > 0 && Battle.Stage == NavalBattleStage.Naval && card.Damage > 0 &&
                card.Action == NavalBattleCardAction.Standard && Battle.DistanceMeters > 350f) return "攻击需要距离不超过 350 米";
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
            if (!hasImmediateBenefit && card.Action == NavalBattleCardAction.FireRounds && Battle.PlayerFireRounds >= card.StrategyLimit) return "烈焰强化已达 " + card.StrategyLimit + " 发";
            if (!hasImmediateBenefit && card.SchoolEffect != null)
            {
                CoreSchoolCardEffect school = card.SchoolEffect;
                if (school.Mechanic == CoreSchoolMechanic.FireLoad && Battle.PlayerFireRounds >= SchoolDefault(school.Limit, 4)) return "附火储备已达上限";
                if (school.Mechanic == CoreSchoolMechanic.ArcaneCharge && Battle.Schools.Mana >= Battle.Schools.ManaLimit) return "法力已满";
            }
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
                if (Battle != null && Battle.PlayerFireRounds > 0) description += " 命中点燃敌舰。";
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
            if (Battle != null && handIndex >= 0 && handIndex < Battle.Hand.Count &&
                CoreNavalSchoolExpansionCatalog.FindSpec(GetBattleCard(Battle.Hand[handIndex])?.Id) != null)
                return PlayNavalCard(handIndex, null);
            if (BActive && handIndex >= 0 && handIndex < Battle.Hand.Count && GetBattleCard(Battle.Hand[handIndex])?.BoardingSpec != null)
                return BPlayCard(handIndex, null);
            if (!CanPlayCard(handIndex)) return false;
            int cardIndex = Battle.Hand[handIndex];
            CoreBattleCard card = GetBattleCard(cardIndex);
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
            Battle.Hand.RemoveAt(handIndex);
            Battle.Energy -= energyCost;
            Battle.RecordSuccessfulCardUse(BattleCardUseIdentity(card));
            if (Battle.CardsPlayedThisTurn < int.MaxValue) Battle.CardsPlayedThisTurn += 1;
            Cargo.Powder -= powderCost;
            Cargo.Cannonballs -= card.CannonballCost;
            Cargo.Wood -= woodCost;

            PaySchoolCardCost(card);
            bool cannon = card.Action == NavalBattleCardAction.Cannon;
            bool critical = false;
            bool fireRound = cannon && Battle.PlayerFireRounds > 0;
            if (cannon)
            {
                Battle.PlayerLoadedShot = 0;
                Battle.Schools.OverflowContribution = 0;
                if (fireRound) Battle.PlayerFireRounds -= 1;
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
            damage -= blocked;
            if (damage <= 0) critical = false;
            if (Battle.Stage == NavalBattleStage.Boarding)
                Battle.EnemyBoardingHealth = Math.Max(0, Battle.EnemyBoardingHealth - damage);
            else
                Battle.EnemyHull = Math.Max(0, Battle.EnemyHull - damage);
            int effectiveDirectDamage = Math.Min(previousEnemyHealth, damage);
            NavalExpansionOnDirectHit(card, effectiveDirectDamage, targetOnThunder);
            if (Battle.Stage == NavalBattleStage.Boarding)
                OnBoardingEnemyDamaged(blocked, effectiveDirectDamage);
            if (fireRound && damage > 0)
            {
                ApplySchoolBurn(Battle.PlayerFireRoundDamage, 2);
            }
            int moraleDelta = 0;
            if (cannon && damage > 0)
            {
                Morale = ClampMorale((long)Morale + 2);
                moraleDelta = 2;
            }

            int block = effects.Block + (effects.Block > 0 && Battle.Stage != NavalBattleStage.Boarding ? Battle.PlayerDeckBuff : 0);
            int previousBlock = Battle.PlayerBlock;
            Battle.PlayerBlock = (int)Math.Min(int.MaxValue, (long)previousBlock + block);
            block = Battle.PlayerBlock - previousBlock;
            if (tideWoodCost > 0) Battle.Schools.TidePaidBlock = SchoolSum(Battle.Schools.TidePaidBlock, block);
            if (card.SchoolEffect?.Mechanic == CoreSchoolMechanic.CurseWard)
                Battle.Schools.CurseGuard = SchoolSum(Battle.Schools.CurseGuard, block);
            int previousHealth = Battle.Stage == NavalBattleStage.Boarding ? Battle.PlayerBoardingHealth : Hull;
            if (Battle.Stage == NavalBattleStage.Boarding)
            {
                Battle.PlayerBoardingHealth = (int)Math.Min(Battle.PlayerMaxBoardingHealth, (long)Battle.PlayerBoardingHealth + effects.ActualRepair);
                if (effects.ActualRepair > 0) Cargo.Medicine -= effects.MedicineCost;
            }
            else
                Hull = (int)Math.Min(MaxHull, (long)Hull + NavalExpansionHealing(true, effects.Repair));
            int repaired = (Battle.Stage == NavalBattleStage.Boarding ? Battle.PlayerBoardingHealth : Hull) - previousHealth;
            ApplyBattleCardStrategy(card);
            ApplySchoolCardEffect(card, preBurn, effectiveDirectDamage, fireFallback);
            if (!fireFallback) ApplySchoolScorch(preBurn, effectiveDirectDamage);
            CompleteSchoolCard(card);
            NavalExpansionCompleteAttack(card);
            Battle.Energy = SchoolSum(Battle.Energy, energyRecovery);
            int derivedDamage = Math.Max(0, previousEnemyHealth - SchoolEnemyHealth - effectiveDirectDamage);
            block = Math.Max(0, Battle.PlayerBlock - previousBlock);
            repaired = (Battle.Stage == NavalBattleStage.Boarding ? Battle.PlayerBoardingHealth : Hull) - previousHealth;
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
                (fireRound && damage > 0 ? "；敌舰燃烧 2 回合" : string.Empty) +
                (moraleDelta > 0 ? "；士气 +" + moraleDelta : string.Empty) + "。";
            // A resolved card may be redrawn, but its next play uses the incremented
            // ID-based cost. Failed attempts never enter the discard pile or count.
            Battle.DiscardPile.Add(cardIndex);
            DrawCards(card.Draw);
            AdvanceGuidedBossCard();
            ResolveBattleOutcome();
            return true;
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
                    // The reserve shares the strongest currently loaded flame.
                    // Emptying it ends that bonus; a new reserve uses its source card.
                    Battle.PlayerFireRoundDamage = Battle.PlayerFireRounds > 0
                        ? Math.Max(Battle.PlayerFireRoundDamage, card.BurnDamage)
                        : card.BurnDamage;
                    Battle.PlayerFireRounds = Math.Max(Battle.PlayerFireRounds, Math.Min(card.StrategyLimit, Battle.PlayerFireRounds + card.StrategyAmount));
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
            NavalBattleAlignment impactAlignment = NavalBattleAlignment.EnemyBroadside;
            Battle.PlayerMovingTowardEnemy = false;
            float targetQuality = impactAlignment == NavalBattleAlignment.HeadOn ? Battle.EnemyBowQuality : Battle.EnemyHullQuality;
            RamDamage(Battle.PlayerBowQuality, targetQuality, out int enemyDamage, out int selfDamage);
            enemyDamage = CoreBattleBalance.ScaleBattleValue(enemyDamage, CoreBattleBalance.PlayerRamDamageMultiplier);
            selfDamage = ApplyCurseBattleDamage(selfDamage);
            Battle.EnemyHull = Math.Max(0, Battle.EnemyHull - enemyDamage);
            Hull = Math.Max(0, Hull - selfDamage);
            Morale = ClampMorale((long)Morale + 5);
            Battle.DistanceMeters = 30f;
            Battle.LastAction = "冲撞" + (impactAlignment == NavalBattleAlignment.HeadOn ? "敌舰舰艏" : "敌舰侧舷") +
                "：敌舰船体 -" + enemyDamage + "，己舰船体 -" + selfDamage + "（舰艏与防护结算，穿透格挡）；士气 +5。";
            if (ResolveBattleOutcome()) return true;

            // A successful non-lethal ram is itself the boarding connection.
            // Keep the old planned-approach mapping so a bow-to-bow impact
            // settles alongside while a bow-to-broadside impact stays offset.
            Battle.BoardingApproach = impactAlignment == NavalBattleAlignment.EnemyBroadside
                ? NavalBoardingApproach.Quarter : NavalBoardingApproach.Alongside;
            Battle.BoardingConnection = impactAlignment == NavalBattleAlignment.HeadOn
                ? NavalBoardingConnection.RamBowToBow : NavalBoardingConnection.RamBowToBroadside;
            Battle.BoardingInitiatedByEnemy = false;
            EnterBattleStage(NavalBattleStage.Boarding);
            if (Phase != CoreDemoPhase.Battle) return true;
            Battle.PlayerBlock = 0;
            Battle.EnemyBlock = 0;
            Battle.EnemyBurnTurns = 0;
            Battle.EnemyBurnDamage = 0;
            ApplySchoolBoardingEntry();
            BInitialize();
            Battle.PlayerMovingTowardEnemy = false;
            Battle.EnemyMovingTowardPlayer = false;
            Battle.LastAction = (impactAlignment == NavalBattleAlignment.HeadOn
                ? "角对角冲撞成功，两舰舰艏锁定。"
                : "角对侧舷冲撞成功，己舰舰艏卡住敌舰侧舷。") +
                "船只已连接，进入接舷近战；己舰船体 -" + selfDamage + "，敌舰船体 -" + enemyDamage + "。";
            AddLog("冲撞完成，" + (impactAlignment == NavalBattleAlignment.HeadOn ? "角对角" : "角对侧舷") + "连接，进入接舷白刃战。");
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
            Battle.BoardingApproach = Battle.PlannedBoardingApproach;
            Battle.BoardingConnection = NavalBoardingConnection.Planned;
            Battle.BoardingInitiatedByEnemy = false;
            EnterBattleStage(NavalBattleStage.Boarding);
            if (Phase != CoreDemoPhase.Battle) return true;
            Battle.PlayerBlock = 0;
            Battle.EnemyBlock = 0;
            Battle.EnemyBurnTurns = 0;
            Battle.EnemyBurnDamage = 0;
            ApplySchoolBoardingEntry();
            BInitialize();
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
            }
            else
            {
                if (!preparing && NavalFieldsEndTurn(true)) return true;
                NavalExpansionEndPlayerTurn();
                EndSchoolPlayerWindow();
                if (!preparing)
                {
                    Battle.EnemyBlock = 0;
                    NavalFieldsBeginTurn(false);
                }
                if (ResolveBattleOutcome()) return true;
            }
            // Keep the preview for this turn. Recomputing it after a burn tick can
            // mistake next turn's lethal fire for the enemy action being executed now.
            if (enemyIntent == null) enemyIntent = GetEnemyIntent();
            if (!boardingRules && ApplySchoolNaturalBurn()) return true;
            ExecuteEnemyPlan(BuildEnemyPlan(), enemyIntent);
            if (ResolveBattleOutcome()) return true;
            if (!preparing && Battle.Stage == previousStage)
            {
                if (boardingRules)
                {
                    if (BEnemyEnd()) return true;
                }
                else
                {
                    if (NavalFieldsEndTurn(false)) return true;
                    if (ApplySchoolCurseTick()) return true;
                    CompleteSchoolEnemyTurn();
                    NavalExpansionEndEnemyTurn();
                }
            }
            Battle.Turn += 1;
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
            public int BoardingBurn;
            public int BoardingDrain;
            public int BoardingSelfDamage;
            public int BoardingPiercing;
        }

        public bool EnemyWillRam => Phase == CoreDemoPhase.Battle && Battle != null &&
            !(Battle.Stage == NavalBattleStage.Naval && Battle.EnemyBurnTurns > 0 && Battle.EnemyHull <= Battle.EnemyBurnDamage) &&
            BuildEnemyPlan().Action == EnemyBattleAction.Ram;
        public float EnemyRamRangeMeters => NavalGridCellMeters;
        public int EnemyRamFirstStrikeDamage => EnemyWillRam ? ApplyCurseBattleDamage(Battle.Profile?.MeleeDamage ?? 8) : 0;

        private bool EnemyChoosesRam()
        {
            if (Battle.Stage != NavalBattleStage.Naval || !NavalRamGeometryIsLegal(false)) return false;
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
            CoreTrainingTarget target = Battle.Profile?.TrainingTarget;
            if (target != null)
            {
                bool preparing = Battle.Stage == NavalBattleStage.Preparation;
                bool boarding = Battle.Stage == NavalBattleStage.Boarding;
                return new EnemyBattlePlan
                {
                    Action = preparing ? EnemyBattleAction.Prepare : boarding ? EnemyBattleAction.Melee :
                        NavalCannonTargetInRange ? EnemyBattleAction.Cannon : EnemyBattleAction.Guard,
                    Attack = preparing ? 0 : boarding ? target.MeleeDamage : NavalCannonTargetInRange ? target.CannonDamage : 0,
                    Guard = target.Guard,
                    Repair = preparing ? 0 : target.Repair,
                };
            }
            if (IsGuidedBossBattle && GuidedBossLesson != GuidedBossLesson.FinalBattle)
            {
                if (Battle.Stage == NavalBattleStage.Boarding) return BuildBoardingEnemyPlan();
                if (Battle.Stage == NavalBattleStage.Naval && GuidedBossLesson == GuidedBossLesson.Gunnery && NavalCannonTargetInRange)
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
            if (!NavalCannonTargetInRange)
                return new EnemyBattlePlan { Action = EnemyBattleAction.Advance, Move = NavalApproachMovement(Battle.DistanceMeters, archetype == CoreBattleArchetype.Tempest ? 240f : 180f), Guard = 2, Load = Math.Max(0, Math.Min(2, 12 - Battle.EnemyLoadedShot)) };
            if (EnemyChoosesRam())
                return new EnemyBattlePlan { Action = EnemyBattleAction.Ram, Attack = meleeDamage };
            if (guardTurn)
                return new EnemyBattlePlan { Action = EnemyBattleAction.Guard, Move = NavalApproachMovement(Battle.DistanceMeters, 90f), Guard = guard, Load = Math.Max(0, Math.Min(4, 12 - Battle.EnemyLoadedShot)) };
            return new EnemyBattlePlan
            {
                Action = EnemyBattleAction.Cannon,
                Move = Battle.DistanceMeters > 60f ? NavalApproachMovement(Battle.DistanceMeters, archetype == CoreBattleArchetype.Artillery ? 60f : archetype == CoreBattleArchetype.Raider ? 150f : 100f) : 0f,
                Attack = cannonDamage + Battle.PhaseTurn / 4 + Battle.EnemyLoadedShot,
            };
        }

        public string GetEnemyIntent()
        {
            string intent = GetEnemyBaseIntent();
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
            int quotedGuard = BActive ? CTBEnemyBlockGain(plan.Guard, false) : fieldPreview == null
                ? NavalExpansionEnemyBlockGain(plan.Guard)
                : SchoolSum(fieldPreview.FinalBlock, fieldPreview.RuneClampConsumed
                    ? Math.Max(0, plan.Guard) : NavalExpansionEnemyBlockGain(plan.Guard));
            string burn = Battle.Stage != NavalBattleStage.Preparation && HasSchoolFire
                ? (BActive ? "回合结束灼烧 " : "先承受灼烧 ") + Battle.EnemyBurnDamage + "；" : string.Empty;
            if (!BActive && Battle.Stage != NavalBattleStage.Preparation && HasSchoolFire && SchoolEnemyHealth <= Battle.EnemyBurnDamage)
                return "灼烧 " + Battle.EnemyBurnDamage + " 将击败敌方，敌方无法行动";
            if (plan.BoardingTurn != null) return BoardingEnemyIntent(plan, burn);
            if (Battle.Profile?.TrainingTarget != null)
            {
                if (Battle.Stage == NavalBattleStage.Preparation)
                    return "木桩准备：静止；格挡设为 " + plan.Guard + "（本轮不攻击、不回复）";
                int damage = Battle.Stage == NavalBattleStage.Boarding
                    ? ApplyCurseBattleDamage(SchoolEnemyRawAttack(plan.Attack)) : EnemyCannonDamage(plan.Attack, false, fieldPreview?.AttackBonus);
                if (BActive) damage = BEnemyAttack(damage, false);
                return burn + "木桩：格挡设为 " + quotedGuard + "；回复 " + plan.Repair + "；" +
                    (Battle.Stage == NavalBattleStage.Naval && !NavalCannonTargetInRange ? "目标超出4格炮击范围，本轮不攻击" :
                    (Battle.Stage == NavalBattleStage.Boarding ? "白刃伤害 " : "炮击伤害 ") + damage + "（格挡前）");
            }
            string move = plan.Move > 0f ? (IsGuidedBossBattle ? "靠近 " : "整格机动，最多 ") + QuantizeNavalEnemyTravel(plan.Move).ToString("0") + " 米；" : string.Empty;
            switch (plan.Action)
            {
                case EnemyBattleAction.Prepare:
                    return "准备：" + move + "装填 +" + NavalExpansionEnemyLoadGain(plan.Load) + "；格挡设为 " + quotedGuard + "（本轮不攻击）";
                case EnemyBattleAction.Advance:
                    return burn + "追击：" + move + "装填 +" + NavalExpansionEnemyLoadGain(plan.Load) + "；格挡设为 " + quotedGuard;
                case EnemyBattleAction.Guard:
                    return burn + move + "防御：格挡设为 " + quotedGuard + (plan.Load > 0 ? "；装填 +" + NavalExpansionEnemyLoadGain(plan.Load) : string.Empty);
                case EnemyBattleAction.Ram:
                    RamDamage(Battle.EnemyBowQuality, Battle.PlayerHullQuality,
                        out int damage, out int recoil);
                    damage = ApplyCurseBattleDamage(SchoolEnemyRawAttack(damage));
                    damage = NavalExpansionEnemyAttackDamage(damage);
                    return burn + "冲撞→接舷：己舰 -" + damage + " / 敌舰 -" + recoil + "（穿透格挡）\n" +
                        "敌方先手：船员伤害 " + ApplyCurseBattleDamage(plan.Attack) + "\n避让：航行离开相邻格，或改变到达朝向";
                case EnemyBattleAction.Melee:
                    int melee = ApplyCurseBattleDamage(SchoolEnemyRawAttack(plan.Attack));
                    return burn + "白刃突击：船员伤害 " + (BActive ? BEnemyAttack(melee, false) : melee) + "（格挡前）";
                default:
                    return burn + move + "炮击：伤害 " + EnemyCannonDamage(plan.Attack, false, fieldPreview?.AttackBonus) + "（装甲后、格挡前）" +
                        (plan.Move > 0f ? "；机动后仍在4格内时开炮" : string.Empty);
            }
        }

        private void ExecuteEnemyPlan(EnemyBattlePlan plan, string intent)
        {
            if (plan.BoardingTurn != null)
            {
                ExecuteBoardingEnemyPlan(plan, intent);
                return;
            }
            float movement = NavalApproachMovement(Battle.DistanceMeters, plan.Move);
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
                    Battle.EnemyBoardingHealth = (int)Math.Min(Battle.EnemyMaxBoardingHealth, (long)Battle.EnemyBoardingHealth + plan.Repair);
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
            if (plan.Action == EnemyBattleAction.Cannon || plan.Action == EnemyBattleAction.Melee)
            {
                int attack = plan.Action == EnemyBattleAction.Cannon ? EnemyCannonDamage(plan.Attack, true) : ApplyCurseBattleDamage(SchoolEnemyRawAttack(plan.Attack));
                if (BActive) attack = BEnemyAttack(attack, true);
                int absorbed = Math.Min(Battle.PlayerBlock, attack);
                Battle.LastEnemyGuardAbsorbed = absorbed;
                int damage = Math.Max(0, attack - absorbed);
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
                    if (damage > 0)
                        Morale = ClampMorale((long)Morale - 1);
                }
                Battle.LastAction = Battle.EnemyName + "：" + intent + "；格挡吸收 " + absorbed + "，实际损失 " + damage + "。";
                if (Hull > 0 && (Battle.Stage != NavalBattleStage.Boarding || Battle.PlayerBoardingHealth > 0))
                {
                    if (BActive) BAfterEnemyAttack(absorbed, damage);
                    else ApplySchoolGuardReaction(absorbed);
                }
            }
            else if (plan.Action == EnemyBattleAction.Ram)
            {
                bool headOn = false; // Rams only connect a bow with a perpendicular broadside.
                RamDamage(Battle.EnemyBowQuality, headOn ? Battle.PlayerBowQuality : Battle.PlayerHullQuality, out int damage, out int recoil);
                damage = ApplyCurseBattleDamage(SchoolEnemyRawAttack(damage));
                damage = NavalExpansionEnemyAttackDamage(damage, true);
                Hull = Math.Max(0, Hull - damage);
                Battle.EnemyHull = Math.Max(0, Battle.EnemyHull - recoil);
                Morale = ClampMorale((long)Morale - 5);
                Battle.EnemyMovingTowardPlayer = false;
                Battle.DistanceMeters = 30f;
                Battle.LastAction = Battle.EnemyName + "冲撞：己舰船体 -" + damage + "，敌舰反伤 " + recoil + "；士气 -5。";
                if (ResolveBattleOutcome()) return;
                Battle.BoardingApproach = headOn ? NavalBoardingApproach.Alongside : NavalBoardingApproach.Quarter;
                Battle.BoardingConnection = headOn ? NavalBoardingConnection.RamBowToBow : NavalBoardingConnection.RamBowToBroadside;
                Battle.BoardingInitiatedByEnemy = true;
                EnterBattleStage(NavalBattleStage.Boarding);
                if (Phase != CoreDemoPhase.Battle) return;
                // Ship guard does not become crew guard when either side boards.
                Battle.PlayerBlock = 0;
                Battle.EnemyBlock = 0;
                Battle.EnemyBurnTurns = 0;
                Battle.EnemyBurnDamage = 0;
                ApplySchoolBoardingEntry();
                BInitialize();
                Battle.PlayerMovingTowardEnemy = false;
                Battle.EnemyMovingTowardPlayer = false;
                int rawFirstStrike = ApplyCurseBattleDamage(plan.Attack);
                if (BActive) rawFirstStrike = BEnemyAttack(rawFirstStrike, true);
                int firstStrike = AbsorbSchoolBoardingFirstStrike(rawFirstStrike);
                Battle.PlayerBoardingHealth = Math.Max(0, Battle.PlayerBoardingHealth - firstStrike);
                if (BActive && Battle.PlayerBoardingHealth > 0) BAfterEnemyAttack(rawFirstStrike - firstStrike, firstStrike);
                Battle.LastAction = Battle.EnemyName + "冲撞连接成功：己舰船体 -" + damage + "，敌舰反伤 " + recoil +
                    "；士气 -5；进入接舷，敌方先手造成船员伤害 " + firstStrike + "。";
                AddLog(Battle.LastAction);
            }
            else Battle.LastAction = Battle.EnemyName + "：" + intent + "。";
        }

        private int EnemyCannonDamage(int rawAttack, bool consume = false, int? fieldBonus = null)
        {
            int bonus = Battle.Stage == NavalBattleStage.Naval ? fieldBonus ?? Battle.NavalFields.Enemy.BloodAttackBonus : 0;
            if (consume) Battle.NavalFields.Enemy.BloodAttackBonus = 0;
            return NavalExpansionEnemyAttackDamage(ApplyCurseBattleDamage(Math.Max(1,
                SchoolEnemyRawAttack(SchoolSum(rawAttack, bonus)) - Math.Max(0, (EffectiveArmor - 4) / 2))), consume);
        }

        private bool ResolveBattleOutcome()
        {
            if (Battle == null) return true;
            if (InterceptGuidedBossExerciseOutcome()) return true;
            if (Battle.Profile?.TrainingTarget != null)
            {
                if (TrainingOutcome == CoreTrainingBattleOutcome.Victory || TrainingOutcome == CoreTrainingBattleOutcome.Defeat)
                    return true;
                bool defeated = Hull <= 0 || (Battle.Stage == NavalBattleStage.Boarding && Battle.PlayerBoardingHealth <= 0);
                bool victorious = Battle.EnemyHull <= 0 || (Battle.Stage == NavalBattleStage.Boarding && Battle.EnemyBoardingHealth <= 0);
                if (!defeated && !victorious) return false;
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
                ClearNavalFields();
                NavalExpansionClear();
                AddLog(Hull <= 0 ? "己方旗舰沉没，航行失败。" : "己方接舷队失去战斗能力，航行失败。");
                FinishVoyage(VoyageOutcome.Failure);
                return true;
            }
            if (Battle.EnemyHull <= 0 || (Battle.Stage == NavalBattleStage.Boarding && Battle.EnemyBoardingHealth <= 0))
            {
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

        private void EnterBattleStage(NavalBattleStage stage)
        {
            NavalBattleStage previousStage = Battle.Stage;
            if (stage == NavalBattleStage.Boarding)
            {
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
