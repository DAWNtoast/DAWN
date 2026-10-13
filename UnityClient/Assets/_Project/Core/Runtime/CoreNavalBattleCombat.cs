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
            int uses = Phase == CoreDemoPhase.Battle && Battle != null ? Battle.SuccessfulCardUses(card.Id) : 0;
            return (int)Math.Min(int.MaxValue, (long)Math.Max(0, card.Energy) + uses);
        }

        public string CardUnavailableReason(int handIndex)
        {
            if (Phase != CoreDemoPhase.Battle || Battle == null) return "当前没有战斗";
            if (handIndex < 0 || handIndex >= Battle.Hand.Count) return "手牌不存在";
            int cardIndex = Battle.Hand[handIndex];
            if (cardIndex < 0 || cardIndex >= cardLibrary.Count) return "卡牌不存在";
            CoreBattleCard card = GetBattleCard(cardIndex);
            if (card == null) return "卡牌不存在";
            string unlockReason = GetBattleCardUnlockReason(card);
            if (unlockReason.Length > 0) return unlockReason;
            string tutorialRestriction = GuidedBossCardRestriction(card);
            if (tutorialRestriction.Length > 0) return tutorialRestriction;
            if (!card.IsAllowedInStage(Battle.Stage)) return "该牌不适用于当前阶段";
            if (!HasBattleCrew(card.RequiredCrew)) return "所需船员不在队伍中";
            if (!HasBattleComponent(card.RequiredComponent)) return "所需部件未装备";
            if (GetBattleCardEnergyCost(cardIndex) > Battle.Energy) return "行动点不足";
            if (GetBattleCardPowderCost(cardIndex) > Cargo.Powder) return "火药不足";
            if (card.CannonballCost > Cargo.Cannonballs) return "炮弹不足，可机动接舷";
            if (card.Action == NavalBattleCardAction.Cannon && Battle.DistanceMeters > 350f) return "炮击需要距离不超过 350 米";
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
            if (!CanPlayCard(handIndex)) return false;
            int cardIndex = Battle.Hand[handIndex];
            CoreBattleCard card = GetBattleCard(cardIndex);
            CoreBattleCardEffects effects = GetBattleCardEffects(card, Battle.Stage);
            bool preBurn = HasSchoolFire;
            int previousEnemyHealth = SchoolEnemyHealth;
            int damage = GetBattleCardDamage(card, Battle.Stage);
            int energyCost = GetBattleCardEnergyCost(cardIndex);
            int powderCost = GetBattleCardPowderCost(cardIndex);
            int tideWoodCost = TideWoodCost(card);
            int woodCost = effects.WoodCost + tideWoodCost;
            int energyRecovery = GetBattleCardEnergyRecovery(cardIndex);
            bool fireFallback = UsesFireFallback(card);
            Battle.Hand.RemoveAt(handIndex);
            Battle.Energy -= energyCost;
            Battle.RecordSuccessfulCardUse(card.Id);
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
            int blocked = Math.Min(Battle.EnemyBlock, damage);
            Battle.EnemyBlock -= blocked;
            damage -= blocked;
            if (damage <= 0) critical = false;
            if (Battle.Stage == NavalBattleStage.Boarding)
                Battle.EnemyBoardingHealth = Math.Max(0, Battle.EnemyBoardingHealth - damage);
            else
                Battle.EnemyHull = Math.Max(0, Battle.EnemyHull - damage);
            int effectiveDirectDamage = Math.Min(previousEnemyHealth, damage);
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
                Hull = (int)Math.Min(MaxHull, (long)Hull + effects.Repair);
            int repaired = (Battle.Stage == NavalBattleStage.Boarding ? Battle.PlayerBoardingHealth : Hull) - previousHealth;
            ApplyBattleCardStrategy(card);
            ApplySchoolCardEffect(card, preBurn, effectiveDirectDamage, fireFallback);
            if (!fireFallback) ApplySchoolScorch(preBurn, effectiveDirectDamage);
            CompleteSchoolCard(card);
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
                (energyRecovery > 0 ? "；行动 +" + energyRecovery : string.Empty) +
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
            if (card.SchoolEffect != null)
            {
                ApplySchoolCardMovement(card);
                return;
            }
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
                case NavalBattleCardAction.Approach:
                    Battle.DistanceMeters -= NavalApproachMovement(Battle.DistanceMeters, card.MovementMeters);
                    Battle.PlayerMovingTowardEnemy = true;
                    break;
                case NavalBattleCardAction.Retreat:
                    Battle.DistanceMeters = Math.Min(900f, Battle.DistanceMeters + card.MovementMeters);
                    Battle.PlayerMovingTowardEnemy = false;
                    break;
                case NavalBattleCardAction.Circle:
                case NavalBattleCardAction.Turn:
                    Battle.DistanceMeters -= NavalApproachMovement(Battle.DistanceMeters, card.MovementMeters);
                    Battle.Alignment = NavalBattleAlignment.EnemyBroadside;
                    Battle.PlayerMovingTowardEnemy = action == NavalBattleCardAction.Turn;
                    break;
                case NavalBattleCardAction.Parallel:
                case NavalBattleCardAction.Navigator:
                    // Presentation metadata only: coming off an enemy broadside
                    // produces an offset quarter-to-quarter grapple. Repeating a
                    // parallel maneuver keeps the established approach intact.
                    if (action == NavalBattleCardAction.Navigator || Battle.Alignment != NavalBattleAlignment.Parallel)
                        Battle.PlannedBoardingApproach = Battle.Alignment == NavalBattleAlignment.EnemyBroadside || action == NavalBattleCardAction.Navigator
                            ? NavalBoardingApproach.Quarter : NavalBoardingApproach.Alongside;
                    Battle.DistanceMeters -= NavalApproachMovement(Battle.DistanceMeters, card.MovementMeters);
                    Battle.Alignment = NavalBattleAlignment.Parallel;
                    Battle.PlayerMovingTowardEnemy = false;
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
            if (IsGuidedBossBattle && GuidedBossExpectedAction != GuidedBossActionKind.Ram)
                reason = "请先完成当前教学动作";
            else if (Phase != CoreDemoPhase.Battle || Battle == null || Battle.Stage != NavalBattleStage.Naval)
                reason = "仅炮战阶段可以冲撞";
            else if (Battle.DistanceMeters <= 0f) reason = "距离过近，先拉开距离再冲撞";
            else if (Battle.DistanceMeters > 100f) reason = "冲撞需要距离不超过 100 米";
            else if (!Battle.PlayerMovingTowardEnemy) reason = "先打出推进或满帆转向，朝敌舰航行";
            else if (Battle.Alignment != NavalBattleAlignment.HeadOn && Battle.Alignment != NavalBattleAlignment.EnemyBroadside)
                reason = "舰艏须对准敌舰舰艏或侧舷，平行时不能冲撞";
            else if (Battle.Energy < 1) reason = "冲撞需要 1 行动点";
            else reason = string.Empty;
            return reason.Length == 0;
        }

        public bool TryRam()
        {
            if (!CanRam(out string reason)) return false;
            NavalBattleAlignment impactAlignment = Battle.Alignment;
            Battle.Energy -= 1;
            Battle.PlayerMovingTowardEnemy = false;
            float targetQuality = impactAlignment == NavalBattleAlignment.HeadOn ? Battle.EnemyBowQuality : Battle.EnemyHullQuality;
            RamDamage(Battle.PlayerBowQuality, targetQuality, out int enemyDamage, out int selfDamage);
            enemyDamage = CoreBattleBalance.ScaleBattleValue(enemyDamage,
                IsGuidedBossBattle ? 1 : CoreBattleBalance.PlayerRamDamageMultiplier);
            selfDamage = IsGuidedBossBattle ? selfDamage : ApplyCurseBattleDamage(selfDamage);
            Battle.EnemyHull = Math.Max(0, Battle.EnemyHull - enemyDamage);
            Hull = Math.Max(0, Hull - selfDamage);
            Morale = ClampMorale((long)Morale + 5);
            Battle.DistanceMeters = 30f;
            Battle.LastAction = "冲撞" + (impactAlignment == NavalBattleAlignment.HeadOn ? "敌舰舰艏" : "敌舰侧舷") +
                "：敌舰船体 -" + enemyDamage + "，己舰船体 -" + selfDamage + "（部件品质结算，穿透格挡）；士气 +5。";
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
            if (IsGuidedBossBattle) reason = "本次教学请以舰艏冲撞完成接舷";
            else if (Phase != CoreDemoPhase.Battle || Battle == null || Battle.Stage != NavalBattleStage.Naval)
                reason = "仅炮战阶段可以接舷";
            else if (Battle.DistanceMeters > 60f) reason = "接舷需要距离不超过 60 米";
            else if (Battle.Alignment != NavalBattleAlignment.Parallel) reason = "接舷前须打出并排行船，令两舰平行";
            else reason = string.Empty;
            return reason.Length == 0;
        }

        public bool TryBoard()
        {
            if (!CanBoard(out string reason)) return false;
            if (ResolveBattleOutcome()) return false;
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
            Battle.PlayerMovingTowardEnemy = false;
            Battle.EnemyMovingTowardPlayer = false;
            Battle.LastAction = "两舰接舷！更换为白刃战牌组；击败敌方船员即可夺船。";
            AddLog("接舷开始，船员进入白刃战。");
            return true;
        }

        public bool EndBattleTurn()
        {
            if (Phase != CoreDemoPhase.Battle || Battle == null) return false;
            if (IsGuidedBossBattle && GuidedBossExpectedAction != GuidedBossActionKind.EndTurn) return ResolveBattleOutcome();
            Battle.LastEnemyGuardAbsorbed = 0;
            Battle.Schools.LastGuardAbsorbed = 0;
            if (ResolveBattleOutcome()) return true;
            NavalBattleStage previousStage = Battle.Stage;
            bool preparing = previousStage == NavalBattleStage.Preparation;
            EndSchoolPlayerWindow();
            // Keep the preview for this turn. Recomputing it after a burn tick can
            // mistake next turn's lethal fire for the enemy action being executed now.
            string enemyIntent = GetEnemyIntent();
            if (ApplySchoolNaturalBurn()) return true;
            ExecuteEnemyPlan(BuildEnemyPlan(), enemyIntent);
            if (ResolveBattleOutcome()) return true;
            if (!preparing && Battle.Stage == previousStage)
            {
                if (ApplySchoolCurseTick()) return true;
                CompleteSchoolEnemyTurn();
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
            }
            else
            {
                Battle.PlayerBlock = 0;
                Battle.Schools.TidePaidBlock = 0;
                Battle.Schools.CurseGuard = 0;
                Battle.DiscardPile.AddRange(Battle.Hand);
                Battle.Hand.Clear();
                Battle.PhaseTurn += 1;
                Battle.Energy = Battle.MaxEnergy;
                Battle.CardsPlayedThisTurn = 0;
                DrawCards(5);
            }
            AdvanceGuidedBossTurn();
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
        }

        public bool EnemyWillRam => Phase == CoreDemoPhase.Battle && Battle != null &&
            !(Battle.Stage == NavalBattleStage.Naval && Battle.EnemyBurnTurns > 0 && Battle.EnemyHull <= Battle.EnemyBurnDamage) &&
            BuildEnemyPlan().Action == EnemyBattleAction.Ram;
        public float EnemyRamRangeMeters => Battle?.Profile?.RamRangeMeters ?? 100f;
        public int EnemyRamFirstStrikeDamage => EnemyWillRam ? ApplyCurseBattleDamage(Battle.Profile?.MeleeDamage ?? 8) : 0;

        private bool EnemyChoosesRam()
        {
            if (Battle.Stage != NavalBattleStage.Naval || Battle.DistanceMeters <= 0f || Battle.DistanceMeters > EnemyRamRangeMeters) return false;
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
                    Action = preparing ? EnemyBattleAction.Prepare : boarding ? EnemyBattleAction.Melee : EnemyBattleAction.Cannon,
                    Attack = preparing ? 0 : boarding ? target.MeleeDamage : target.CannonDamage,
                    Guard = target.Guard,
                    Repair = preparing ? 0 : target.Repair,
                };
            }
            if (IsGuidedBossBattle)
            {
                if (Battle.Stage == NavalBattleStage.Naval)
                    return Battle.PhaseTurn == 1
                        ? new EnemyBattlePlan { Action = EnemyBattleAction.Guard, Move = NavalApproachMovement(Battle.DistanceMeters, 100f), Guard = 3 }
                        : new EnemyBattlePlan { Action = EnemyBattleAction.Cannon, Move = NavalApproachMovement(Battle.DistanceMeters, 180f), Attack = 3 };
                return new EnemyBattlePlan { Action = EnemyBattleAction.Melee, Attack = Battle.PhaseTurn == 1 ? 7 : 3 };
            }
            CoreBattleProfile profile = Battle.Profile;
            int guard = profile?.Guard ?? (Battle.IsBoss ? 9 : 6);
            int cannonDamage = profile?.CannonDamage ?? (Battle.IsBoss ? 12 : 8);
            int meleeDamage = profile?.MeleeDamage ?? (Battle.IsBoss ? 12 : 8);
            CoreBattleArchetype archetype = profile?.Archetype ?? CoreBattleArchetype.Balanced;
            bool guardTurn = archetype == CoreBattleArchetype.Ironclad ? Battle.PhaseTurn % 2 == 0 : Battle.PhaseTurn % 3 == 2;
            if (Battle.Stage == NavalBattleStage.Preparation)
                return new EnemyBattlePlan { Action = EnemyBattleAction.Prepare, Move = NavalApproachMovement(Battle.DistanceMeters, 150f), Guard = Math.Max(4, profile?.InitialGuard ?? 0), Load = Math.Max(0, Math.Min(4, 12 - Battle.EnemyLoadedShot)) };
            if (Battle.Stage == NavalBattleStage.Boarding)
                return guardTurn
                    ? new EnemyBattlePlan { Action = EnemyBattleAction.Guard, Guard = guard }
                    : new EnemyBattlePlan { Action = EnemyBattleAction.Melee, Attack = meleeDamage + Battle.PhaseTurn / 4 };
            if (Battle.DistanceMeters > 350f)
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
            if (Battle == null || Phase != CoreDemoPhase.Battle) return string.Empty;
            EnemyBattlePlan plan = BuildEnemyPlan();
            string burn = Battle.Stage != NavalBattleStage.Preparation && HasSchoolFire ? "先承受灼烧 " + Battle.EnemyBurnDamage + "；" : string.Empty;
            if (Battle.Stage != NavalBattleStage.Preparation && HasSchoolFire && SchoolEnemyHealth <= Battle.EnemyBurnDamage)
                return "灼烧 " + Battle.EnemyBurnDamage + " 将击败敌方，敌方无法行动";
            if (Battle.Profile?.TrainingTarget != null)
            {
                if (Battle.Stage == NavalBattleStage.Preparation)
                    return "木桩准备：静止；格挡设为 " + plan.Guard + "（本轮不攻击、不回复）";
                int damage = Battle.Stage == NavalBattleStage.Boarding
                    ? ApplyCurseBattleDamage(SchoolEnemyRawAttack(plan.Attack)) : EnemyCannonDamage(plan.Attack);
                return burn + "木桩：格挡设为 " + plan.Guard + "；回复 " + plan.Repair + "；" +
                    (Battle.Stage == NavalBattleStage.Boarding ? "白刃伤害 " : "炮击伤害 ") + damage + "（格挡前）";
            }
            string move = plan.Move > 0f ? "靠近 " + plan.Move.ToString("0") + " 米；" : string.Empty;
            switch (plan.Action)
            {
                case EnemyBattleAction.Prepare:
                    return "准备：" + move + "装填 +" + plan.Load + "；格挡设为 " + plan.Guard + "（本轮不攻击）";
                case EnemyBattleAction.Advance:
                    return burn + "追击：" + move + "装填 +" + plan.Load + "；格挡设为 2";
                case EnemyBattleAction.Guard:
                    return burn + move + "防御：格挡设为 " + plan.Guard + (plan.Load > 0 ? "；装填 +" + plan.Load : string.Empty);
                case EnemyBattleAction.Ram:
                    RamDamage(Battle.EnemyBowQuality, Battle.Alignment == NavalBattleAlignment.HeadOn ? Battle.PlayerBowQuality : Battle.PlayerHullQuality,
                        out int damage, out int recoil);
                    damage = ApplyCurseBattleDamage(SchoolEnemyRawAttack(damage));
                    return burn + "冲撞→接舷：己舰 -" + damage + " / 敌舰 -" + recoil + "（穿透格挡）\n" +
                        "敌方先手：船员伤害 " + ApplyCurseBattleDamage(plan.Attack) + "\n避让：拉开至 " + EnemyRamRangeMeters.ToString("0") + " 米外，或先撞";
                case EnemyBattleAction.Melee:
                    return burn + "白刃突击：船员伤害 " + (IsGuidedBossBattle ? plan.Attack : ApplyCurseBattleDamage(SchoolEnemyRawAttack(plan.Attack))) + "（格挡前）";
                default:
                    return burn + move + "炮击：伤害 " + EnemyCannonDamage(plan.Attack) + "（装甲后、格挡前）";
            }
        }

        private void ExecuteEnemyPlan(EnemyBattlePlan plan, string intent)
        {
            float movement = NavalApproachMovement(Battle.DistanceMeters, plan.Move);
            if (movement > 0f)
            {
                Battle.DistanceMeters -= movement;
                Battle.EnemyMovingTowardPlayer = true;
            }
            // Enemy guard lasts until their next action; it does not accumulate forever.
            Battle.EnemyBlock = plan.Guard;
            Battle.EnemyLoadedShot = Math.Min(12, Battle.EnemyLoadedShot + plan.Load);
            if (Battle.Profile?.TrainingTarget != null)
            {
                Battle.EnemyLoadedShot = 0;
                Battle.EnemyMovingTowardPlayer = false;
                // Burn resolves before the action; healing cannot revive a defeated target.
                // Heal only the active pool before attack/guard reactions, capped at its maximum.
                if (Battle.Stage == NavalBattleStage.Boarding)
                    Battle.EnemyBoardingHealth = (int)Math.Min(Battle.EnemyMaxBoardingHealth, (long)Battle.EnemyBoardingHealth + plan.Repair);
                else if (Battle.Stage == NavalBattleStage.Naval)
                    Battle.EnemyHull = (int)Math.Min(Battle.EnemyMaxHull, (long)Battle.EnemyHull + plan.Repair);
            }
            if (plan.Action == EnemyBattleAction.Cannon || plan.Action == EnemyBattleAction.Melee)
            {
                int attack = IsGuidedBossBattle ? plan.Attack : plan.Action == EnemyBattleAction.Cannon ? EnemyCannonDamage(plan.Attack) : ApplyCurseBattleDamage(SchoolEnemyRawAttack(plan.Attack));
                int absorbed = Math.Min(Battle.PlayerBlock, attack);
                Battle.LastEnemyGuardAbsorbed = absorbed;
                int damage = Math.Max(0, attack - absorbed);
                Battle.PlayerBlock -= absorbed;
                if (plan.Action == EnemyBattleAction.Melee)
                    Battle.PlayerBoardingHealth = Math.Max(0, Battle.PlayerBoardingHealth - damage);
                else
                {
                    Battle.EnemyLoadedShot = 0;
                    Hull = Math.Max(0, Hull - damage);
                    if (damage > 0)
                        Morale = ClampMorale((long)Morale - 1);
                }
                Battle.LastAction = Battle.EnemyName + "：" + intent + "；格挡吸收 " + absorbed + "，实际损失 " + damage + "。";
                if (Hull > 0 && (Battle.Stage != NavalBattleStage.Boarding || Battle.PlayerBoardingHealth > 0)) ApplySchoolGuardReaction(absorbed);
            }
            else if (plan.Action == EnemyBattleAction.Ram)
            {
                bool headOn = Battle.Alignment == NavalBattleAlignment.HeadOn;
                RamDamage(Battle.EnemyBowQuality, headOn ? Battle.PlayerBowQuality : Battle.PlayerHullQuality, out int damage, out int recoil);
                damage = ApplyCurseBattleDamage(SchoolEnemyRawAttack(damage));
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
                Battle.PlayerMovingTowardEnemy = false;
                Battle.EnemyMovingTowardPlayer = false;
                int firstStrike = AbsorbSchoolBoardingFirstStrike(ApplyCurseBattleDamage(plan.Attack));
                Battle.PlayerBoardingHealth = Math.Max(0, Battle.PlayerBoardingHealth - firstStrike);
                Battle.LastAction = Battle.EnemyName + "冲撞连接成功：己舰船体 -" + damage + "，敌舰反伤 " + recoil +
                    "；士气 -5；进入接舷，敌方先手造成船员伤害 " + firstStrike + "。";
                AddLog(Battle.LastAction);
            }
            else Battle.LastAction = Battle.EnemyName + "：" + intent + "。";
        }

        private int EnemyCannonDamage(int rawAttack) => IsGuidedBossBattle ? rawAttack : ApplyCurseBattleDamage(Math.Max(1, SchoolEnemyRawAttack(rawAttack) - Math.Max(0, (EffectiveArmor - 4) / 2)));

        private bool ResolveBattleOutcome()
        {
            if (Battle == null) return true;
            if (Battle.Profile?.TrainingTarget != null)
            {
                if (TrainingOutcome == CoreTrainingBattleOutcome.Victory || TrainingOutcome == CoreTrainingBattleOutcome.Defeat)
                    return true;
                bool defeated = Hull <= 0 || (Battle.Stage == NavalBattleStage.Boarding && Battle.PlayerBoardingHealth <= 0);
                bool victorious = Battle.EnemyHull <= 0 || (Battle.Stage == NavalBattleStage.Boarding && Battle.EnemyBoardingHealth <= 0);
                if (!defeated && !victorious) return false;
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
                AddLog(Hull <= 0 ? "己方旗舰沉没，航行失败。" : "己方接舷队失去战斗能力，航行失败。");
                FinishVoyage(VoyageOutcome.Failure);
                return true;
            }
            if (Battle.EnemyHull <= 0 || (Battle.Stage == NavalBattleStage.Boarding && Battle.EnemyBoardingHealth <= 0))
            {
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
            pendingBoardingComponentDrop = false;
            pendingBoardingComponentSlot = -1;
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
                DistanceMeters = trainingTarget == null ? 600f : 300f,
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
                LastAction = "相距 " + (trainingTarget == null ? "600" : "300") + " 米。首轮仅能部署策略，双方准备后进入炮战。",
            };
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
            Battle.DiscardPile.AddRange(Battle.Hand);
            Battle.Hand.Clear();
            Battle.Stage = stage;
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
                }
                int last = Battle.DrawPile.Count - 1;
                Battle.Hand.Add(Battle.DrawPile[last]);
                Battle.DrawPile.RemoveAt(last);
            }
        }
    }
}
