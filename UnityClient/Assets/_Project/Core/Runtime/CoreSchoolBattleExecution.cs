using System;
using System.Globalization;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        private static int SchoolSum(int a, int b) => (int)Math.Min(int.MaxValue, Math.Max(0L, (long)a) + Math.Max(0L, (long)b));
        private static int SchoolProduct(int a, int b) => (int)Math.Min(int.MaxValue, Math.Max(0L, (long)a) * Math.Max(0L, (long)b));
        private static int SchoolDefault(int value, int fallback) => value > 0 ? value : fallback;
        private bool SchoolBattleActive(NavalBattleStage stage) => Phase == CoreDemoPhase.Battle && Battle != null && Battle.Stage == stage;
        private int SchoolPlayerHealth => Battle.Stage == NavalBattleStage.Boarding ? Battle.PlayerBoardingHealth : Hull;
        private int SchoolPlayerMaximum => Battle.Stage == NavalBattleStage.Boarding ? Battle.PlayerMaxBoardingHealth : MaxHull;
        private int SchoolEnemyHealth => Battle.Stage == NavalBattleStage.Boarding ? Battle.EnemyBoardingHealth : Battle.EnemyHull;
        private int SchoolEnemyMaximum => Battle.Stage == NavalBattleStage.Boarding ? Battle.EnemyMaxBoardingHealth : Battle.EnemyMaxHull;
        private bool HasSchoolFire => Battle != null && Battle.EnemyBurnTurns > 0;

        private string SchoolCardUnavailableReason(CoreBattleCard card)
        {
            CoreSchoolCardEffect e = card.SchoolEffect;
            if (e == null || Battle == null) return string.Empty;
            switch (e.Mechanic)
            {
                case CoreSchoolMechanic.ArcaneRelease:
                case CoreSchoolMechanic.ArcaneWard:
                case CoreSchoolMechanic.ArcaneAttackReserve:
                case CoreSchoolMechanic.ArcaneGuardReserve:
                case CoreSchoolMechanic.ArcaneMend:
                case CoreSchoolMechanic.ArcaneEcho:
                    if (Battle.Schools.AvailableMana < e.Threshold) return "法力不足";
                    break;
                case CoreSchoolMechanic.CurseDrain:
                case CoreSchoolMechanic.CurseWard:
                    if (Battle.Schools.CurseMarks < e.Threshold) return "咒印层数不足";
                    break;
                case CoreSchoolMechanic.CurseDetonate:
                    if (card.Id != "card_school_curse_b05" && Battle.Schools.CurseMarks == 0) return "先施加咒印才能强化结算";
                    break;
                case CoreSchoolMechanic.TideRiposte:
                    if (card.Id == "card_school_tide_b03" || card.Id == "card_school_tide_b06") break;
                    if (Battle.Schools.TidePower < Math.Max(1, e.Threshold)) return "潮势不足；先用木板布防并实际挡伤";
                    break;
                case CoreSchoolMechanic.TideRenew:
                case CoreSchoolMechanic.TideCleanse:
                    if (Battle.Schools.TidePower < Math.Max(1, e.Threshold)) return "潮势不足；先用木板布防并实际挡伤";
                    break;
                case CoreSchoolMechanic.BloodRisk:
                case CoreSchoolMechanic.BloodWard:
                    if (SchoolPlayerHealth <= GetBattleCardHealthCost(card)) return "支付生命后须至少剩余 1 点";
                    break;
            }
            return string.Empty;
        }

        private void GetSchoolCardBonuses(CoreBattleCard card, NavalBattleStage stage, out int damage, out int block, out int repair)
        {
            damage = block = repair = 0;
            if (card?.SchoolEffect == null || !SchoolBattleActive(stage) || UsesFireFallback(card)) return;
            CoreSchoolCardEffect e = card.SchoolEffect;
            CoreSchoolBattleState s = Battle.Schools;
            int threshold = SchoolDefault(e.Threshold, 1);
            switch (e.Mechanic)
            {
                case CoreSchoolMechanic.ComponentFieldStrike: damage = ComponentFieldDamageBonus(card); break;
                case CoreSchoolMechanic.ComponentFieldShelter:
                    block = ComponentFieldBlockBonus(card); repair = ComponentFieldRepairBonus(card); break;
                case CoreSchoolMechanic.ComponentFieldPulse: repair = ComponentFieldRepairBonus(card); break;
                case CoreSchoolMechanic.FireExploit: if (card.Id != "card_school_fire_b04" && HasSchoolFire) damage = e.Power; break;
                case CoreSchoolMechanic.FireGuard: if (HasSchoolFire) block = e.Power; break;
                case CoreSchoolMechanic.FireHeal: if (HasSchoolFire) repair = e.Power; break;
                case CoreSchoolMechanic.ArcaneRelease: if (s.AvailableMana >= e.Threshold) damage = e.Power; break;
                case CoreSchoolMechanic.ArcaneWard: if (s.AvailableMana >= e.Threshold) block = e.Power; break;
                case CoreSchoolMechanic.ArcaneMend: if (s.AvailableMana >= e.Threshold) repair = e.Power; break;
                case CoreSchoolMechanic.CurseWard: if (s.CurseMarks >= e.Threshold) block = SchoolProduct(e.Power, s.CurseMarks); break;
                case CoreSchoolMechanic.CurseDrain: if (s.CurseMarks >= e.Threshold) repair = e.Power; break;
                case CoreSchoolMechanic.TideCleanse: block = e.Power; break;
                case CoreSchoolMechanic.TideRenew: if (s.TidePower >= threshold) repair = e.Power; break;
                case CoreSchoolMechanic.ThunderPrime:
                case CoreSchoolMechanic.ThunderChain:
                    damage = e.Threshold > 0 ? (s.ThunderChain >= e.Threshold ? e.Power : 0) : SchoolProduct(e.Power, s.ThunderChain);
                    break;
                case CoreSchoolMechanic.ThunderFinish: damage = SchoolProduct(e.Power, s.ThunderChain); break;
                case CoreSchoolMechanic.BloodRisk: if (HasBloodPaidBenefit(card)) damage = e.Power; break;
                case CoreSchoolMechanic.BloodWard: if (HasBloodPaidBenefit(card)) block = e.Power; break;
                case CoreSchoolMechanic.BloodExecute:
                    if ((long)SchoolEnemyHealth * 100 <= (long)SchoolEnemyMaximum * Math.Min(100, e.Threshold)) damage = e.Power;
                    break;
            }
            if (IsThunderAttack(card)) damage = SchoolSum(damage, s.ThunderReadyDamage);
        }

        private int SchoolAttackStanceQuote(CoreBattleCard card, NavalBattleStage stage)
        {
            // Tide converts stored actual absorbed damage directly into its quoted bonus.
            return card?.SchoolEffect?.Mechanic == CoreSchoolMechanic.TideRiposte && SchoolBattleActive(stage)
                ? (int)Math.Min(int.MaxValue, (long)Battle.Schools.TidePower * card.SchoolEffect.Power / 100) : 0;
        }

        private void PaySchoolCardCost(CoreBattleCard card)
        {
            CoreSchoolCardEffect e = card.SchoolEffect;
            if (e == null) return;
            switch (e.Mechanic)
            {
                case CoreSchoolMechanic.ArcaneRelease:
                case CoreSchoolMechanic.ArcaneWard:
                case CoreSchoolMechanic.ArcaneAttackReserve:
                case CoreSchoolMechanic.ArcaneGuardReserve:
                case CoreSchoolMechanic.ArcaneMend:
                case CoreSchoolMechanic.ArcaneEcho: PayMana(e.Threshold); break;
                case CoreSchoolMechanic.CurseDrain:
                case CoreSchoolMechanic.CurseWard: NavalExpansionConsumeCurse(e.Threshold); break;
                case CoreSchoolMechanic.ThunderFinish:
                    if (BActive) ConsumeThunderChain(true, Battle.Schools.ThunderChain);
                    break;
                case CoreSchoolMechanic.TideRiposte:
                    if (BActive) CTBConsumeTide(BState.TidePower);
                    else Battle.Schools.TidePower = 0;
                    break;
                case CoreSchoolMechanic.TideRenew:
                case CoreSchoolMechanic.TideCleanse:
                    if (BActive) CTBConsumeTide(Math.Max(1, e.Threshold));
                    else Battle.Schools.TidePower -= Math.Max(1, e.Threshold);
                    break;
                case CoreSchoolMechanic.BloodRisk:
                case CoreSchoolMechanic.BloodWard:
                    PayBattleHealthCost(true, GetBattleCardHealthCost(card));
                    break;
            }
        }

        private void AddSchoolGuard(int amount)
        {
            if (BActive) { BGuard(amount); return; }
            Battle.PlayerBlock = SchoolSum(Battle.PlayerBlock, NavalArcaneBlockGain(amount, true));
        }

        public int NavalArcaneBlockGain(int amount, bool consume = false)
        {
            if (amount <= 0 || Battle == null || Battle.Stage == NavalBattleStage.Boarding) return Math.Max(0, amount);
            int bonus = Battle.Schools.ArcaneNextBlockBonus;
            if (consume) Battle.Schools.ArcaneNextBlockBonus = 0;
            return SchoolSum(amount, bonus);
        }
        private void HealSchoolPlayer(int amount)
        {
            if (IsBattleHealingBlocked(true)) return;
            if (Battle.Stage == NavalBattleStage.Boarding)
                Battle.PlayerBoardingHealth = Math.Min(Battle.PlayerMaxBoardingHealth, SchoolSum(Battle.PlayerBoardingHealth, amount));
            else Hull = Math.Min(MaxHull, SchoolSum(Hull, NavalExpansionHealing(true, amount)));
        }
        private int DamageSchoolEnemy(int amount) => DamageSchoolEnemyWithSettlement(amount, null, 0);
        private int DamageSchoolEnemyWithSettlement(int amount, CoreBattleSettlementKind? settlement, int guardLost)
        {
            int damage = Math.Min(SchoolEnemyHealth, Math.Max(0, amount));
            if (Battle.Stage == NavalBattleStage.Boarding)
            {
                Battle.EnemyBoardingHealth -= damage;
                if (settlement.HasValue) Battle.RecordSettlement(settlement.Value, false, damage, guardLost, healthAfter: Battle.EnemyBoardingHealth);
                BResolveCurseExecutions();
            }
            else
            {
                Battle.EnemyHull -= damage;
                if (settlement.HasValue) Battle.RecordSettlement(settlement.Value, false, damage, guardLost, healthAfter: Battle.EnemyHull);
                CheckNavalCurseExecution();
            }
            return damage;
        }

        private void ApplySchoolBurn(int layers, int legacyDuration)
        {
            ApplyFireBurn(false, layers);
        }
        private void ExtendSchoolBurn(int amount)
        {
            if (!HasSchoolFire || amount <= 0) return;
            ApplyFireBurn(false, amount);
        }

        private int SchoolFinalLoadedBonus()
        {
            if (Battle == null) return 0;
            return SchoolSum(Battle.PlayerLoadedShot, Battle.Schools.OverflowContribution);
        }

        /// <summary>Actual damage added to the next cannon hit, including school overflow.</summary>
        public int PlayerLoadedShotDamageBonus => SchoolFinalLoadedBonus();

        /// <summary>The loading card's authored ceiling, in the same units as cannon damage.</summary>
        public int GetBattleLoadedShotLimit(CoreBattleCard card) => card != null &&
            (card.Action == NavalBattleCardAction.Load || card.Action == NavalBattleCardAction.PrecisionLoad)
                ? card.StrategyLimit : 0;

        /// <summary>Actual remaining final damage gain; at a partial cap the card adds only the available room.</summary>
        public int GetBattleLoadedShotIncrease(CoreBattleCard card)
        {
            int limit = GetBattleLoadedShotLimit(card);
            if (limit <= 0) return 0;
            int room = Math.Max(0, limit - PlayerLoadedShotDamageBonus);
            return Math.Min(card.StrategyAmount, room);
        }

        private void ApplyLegacyLoadedShot(CoreBattleCard card)
        {
            Battle.PlayerLoadedShot = SchoolSum(Battle.PlayerLoadedShot, GetBattleLoadedShotIncrease(card));
        }

        private int ApplySchoolCardEffect(CoreBattleCard card, bool preBurn, int effectiveDirectDamage, bool fireFallback = false)
        {
            if (ApplyComponentFieldCardEffect(card)) return 0;
            CoreSchoolCardEffect e = card.SchoolEffect;
            if (fireFallback || e == null || SchoolEnemyHealth <= 0 && e.Mechanic != CoreSchoolMechanic.BloodDrain) return 0;
            CoreSchoolBattleState s = Battle.Schools;
            int power = e.Power;
            int duration = SchoolDefault(e.Duration, 2);
            switch (e.Mechanic)
            {
                case CoreSchoolMechanic.FireLoad:
                    Battle.PlayerFireRounds = SchoolSum(Battle.PlayerFireRounds, e.Secondary);
                    break;
                case CoreSchoolMechanic.FireIgnite:
                    ApplySchoolBurn(power, duration);
                    break;
                case CoreSchoolMechanic.FireIgniteOrExtend:
                    if (card.Id == "card_school_fire_b05")
                    {
                        ApplySchoolBurn(power, duration);
                        if (e.Secondary > 0) ExtendSchoolBurn(e.Secondary);
                    }
                    else if (effectiveDirectDamage > 0)
                    {
                        if (preBurn) ExtendSchoolBurn(SchoolDefault(e.Secondary, 1));
                        else ApplySchoolBurn(power, duration);
                    }
                    break;
                case CoreSchoolMechanic.FireExploit:
                    if (card.Id == "card_school_fire_b04" && preBurn) return NavalActiveCardDamage(false, power, 0);
                    break;
                case CoreSchoolMechanic.FireExtend: if (card.Damage == 0 || effectiveDirectDamage > 0) ExtendSchoolBurn(e.Power); break;
                case CoreSchoolMechanic.FirePrepareGuard: s.PrepareFireGuard = Math.Max(s.PrepareFireGuard, power); break;
                case CoreSchoolMechanic.FireNaturalGuard:
                    s.NaturalFireGuard = Math.Max(s.NaturalFireGuard, power); s.NaturalFireGuardTurns = duration; break;
                case CoreSchoolMechanic.FireBoardingSeed:
                    if (effectiveDirectDamage > 0)
                    {
                        ApplySchoolBurn(power, duration);
                        s.BoardingBurn = Math.Max(s.BoardingBurn, e.Secondary); s.BoardingBurnTurns = Math.Max(s.BoardingBurnTurns, duration);
                    }
                    break;
                case CoreSchoolMechanic.FireBoardingCover: if (preBurn) s.BoardingCover = Math.Max(s.BoardingCover, e.Power); break;
                case CoreSchoolMechanic.FireExtraTick:
                    if (effectiveDirectDamage > 0)
                    {
                        ApplySchoolBurn(power, duration);
                        if (preBurn && HasSchoolFire && !s.ExtraFireTickUsed)
                        {
                            s.ExtraFireTickUsed = true; return DamageSchoolEnemy(Battle.EnemyBurnDamage);
                        }
                    }
                    break;
                case CoreSchoolMechanic.FireScorch: s.ScorchDamage = Math.Max(s.ScorchDamage, power); s.ScorchCharges = SchoolDefault(e.Secondary, 2); break;
                case CoreSchoolMechanic.FireOverflow:
                    s.OverflowAmount = Math.Max(s.OverflowAmount, e.Power); s.OverflowOwnLimit = Math.Max(s.OverflowOwnLimit, e.Limit);
                    s.OverflowTotalLimit = Math.Max(s.OverflowTotalLimit, e.Threshold); break;
                case CoreSchoolMechanic.ArcaneCharge:
                    GainBattleMana(e.Power); break;
                case CoreSchoolMechanic.ArcaneAttackReserve:
                    s.ArcaneNextAttackDamage = SchoolSum(s.ArcaneNextAttackDamage, power); break;
                case CoreSchoolMechanic.ArcaneGuardReserve:
                    s.ArcaneNextBlockBonus = SchoolSum(s.ArcaneNextBlockBonus, power); break;
                case CoreSchoolMechanic.ArcaneCapacity: s.ManaLimit = SchoolSum(s.ManaLimit, e.Power); break;
                case CoreSchoolMechanic.ArcaneTemporary: GainBattleMana(e.Power); break;
                case CoreSchoolMechanic.ArcaneEcho: DrawCards(e.Power); break;
                case CoreSchoolMechanic.CurseMark:
                    ApplyNavalEnemyCurseMarks(e.Power); break;
                case CoreSchoolMechanic.CurseDetonate:
                    int curseDamage = SchoolProduct(power, s.CurseMarks);
                    return NavalActiveCardDamage(false, curseDamage,
                        card.Id == "card_school_curse_b05" ? curseDamage : 0);
                case CoreSchoolMechanic.TideCleanse: Battle.EnemyLoadedShot = 0; break;
                case CoreSchoolMechanic.ThunderReady:
                    s.ThunderReadyDamage = card.Id == "card_school_thunder_b01" || card.Id == "card_school_thunder_b02"
                        ? SchoolSum(s.ThunderReadyDamage, e.Power) : Math.Max(s.ThunderReadyDamage, e.Power);
                    break;
                case CoreSchoolMechanic.ThunderEcho:
                    if (s.ThunderChain >= SchoolDefault(e.Threshold, 1) && !s.ThunderEchoUsed) { s.ThunderEchoUsed = true; DrawCards(e.Power); }
                    break;
                // Lifesteal resolves with the direct hit, before curse execution can finish the card.
                // BloodMend's deterministic missing-health heal is already paid from the common card quote.
            }
            return 0;
        }

        private int SchoolBloodMendQuote(CoreBattleCard card, NavalBattleStage stage)
        {
            CoreSchoolCardEffect e = card?.SchoolEffect;
            if (e == null || e.Mechanic != CoreSchoolMechanic.BloodMend || !SchoolBattleActive(stage)) return 0;
            return (int)Math.Min(SchoolDefault(e.Limit, int.MaxValue),
                (long)Math.Max(0, SchoolPlayerMaximum - SchoolPlayerHealth) * Math.Min(100, e.Power) / 100);
        }

        private int ApplySchoolScorch(bool preBurn, int directDamage)
        {
            CoreSchoolBattleState s = Battle.Schools;
            if (!preBurn || directDamage <= 0 || SchoolEnemyHealth <= 0 || s.ScorchCharges <= 0) return 0;
            s.ScorchCharges -= 1;
            return DamageSchoolEnemy(s.ScorchDamage);
        }

        private void CompleteSchoolCard(CoreBattleCard card)
        {
            if (IsThunderAttack(card))
            {
                Battle.Schools.ThunderReadyDamage = 0;
            }
            CompleteThunderCard(card, IsThunderAttack(card));
            if (Phase != CoreDemoPhase.Battle) return;
            ApplySpecialCardDoctrine(card);
        }

        private bool ApplySchoolNaturalBurn()
        {
            if (Battle.Stage == NavalBattleStage.Preparation || !HasSchoolFire) return false;
            int absorbed = Math.Min(Battle.EnemyBlock, Battle.EnemyBurnTurns);
            Battle.EnemyBlock -= absorbed;
            RecordEnemyTidalAbsorption(absorbed);
            int damage = DamageSchoolEnemyWithSettlement(Battle.EnemyBurnTurns - absorbed, CoreBattleSettlementKind.Burn, absorbed);
            Battle.EnemyBurnTurns -= 1;
            Battle.EnemyBurnDamage = Battle.EnemyBurnTurns;
            Battle.LastAction += " 燃烧：格挡 -" + absorbed + "，敌方" + (Battle.Stage == NavalBattleStage.Boarding ? "船员" : "船体") + " -" + damage + "。";
            if (ResolveBattleOutcome()) return true;
            if (damage > 0 && Battle.Schools.NaturalFireGuardTurns > 0) AddSchoolGuard(Battle.Schools.NaturalFireGuard);
            return false;
        }

        private int SchoolEnemyRawAttack(int attack)
        {
            if (Battle == null) return attack;
            return Math.Max(0, attack);
        }

        private void ApplySchoolGuardReaction(int absorbed)
        {
            CoreSchoolBattleState s = Battle.Schools;
            s.LastGuardAbsorbed = absorbed;
            if (SchoolEnemyHealth <= 0 || absorbed <= 0) return;
            RecordPaidTideGuard(absorbed);
        }

        private void CompleteSchoolEnemyTurn()
        {
            CoreSchoolBattleState s = Battle.Schools;
            if (s.NaturalFireGuardTurns > 0) s.NaturalFireGuardTurns -= 1;
            if (s.TideCounterTurns > 0) s.TideCounterTurns -= 1;
            if (s.TideStanceTurns > 0) s.TideStanceTurns -= 1;
        }

        private void ApplySchoolBoardingEntry()
        {
            CoreSchoolBattleState s = Battle.Schools;
            int burn = s.BoardingBurn;
            int turns = s.BoardingBurnTurns;
            int cover = s.BoardingCover;
            s.ClearTarget();
            BInitialize(); // Import the persistent tide reserve before any boarding effect can synchronize it.
            Battle.SpecialCards.Clear();
            s.BoardingBurn = 0; s.BoardingBurnTurns = 0;
            // The cover is the only naval defense transferred into this new target phase.
            s.BoardingCover = cover;
            if (burn > 0) ApplySchoolBurn(burn, turns);
            if (!Battle.BoardingInitiatedByEnemy)
            {
                AddSchoolGuard(s.BoardingCover); s.BoardingCover = 0;
            }
        }
        private int AbsorbSchoolBoardingFirstStrike(int attack)
        {
            int absorbed = Math.Min(Math.Max(0, attack), Battle.Schools.BoardingCover);
            AddSchoolGuard(Battle.Schools.BoardingCover - absorbed);
            Battle.Schools.BoardingCover = 0;
            return Math.Max(0, attack - absorbed);
        }

        private string SchoolCardEffectDescription(CoreBattleCard card, NavalBattleStage stage, CoreBattleCardEffects current)
        {
            CoreSchoolCardEffect e = card.SchoolEffect;
            string template = card.DescriptionTemplate
                .Replace("{schoolThreshold}", (SchoolBattleActive(stage) &&
                    (e.Mechanic == CoreSchoolMechanic.BloodRisk || e.Mechanic == CoreSchoolMechanic.BloodWard)
                    ? CoreSpecialCardRules.HealthCost(card, Battle) : e.Threshold).ToString(CultureInfo.InvariantCulture))
                .Replace("{schoolPower}", e.Power.ToString(CultureInfo.InvariantCulture))
                .Replace("{schoolSecondary}", e.Secondary.ToString(CultureInfo.InvariantCulture));
            CoreBattleCardEffects basic = new CoreBattleCardEffects(card, stage, current.OrdinaryCrew, 0, null);
            CoreBattleCard display = new CoreBattleCard(card.Id, card.Name, card.Type, card.Energy, basic.Damage, basic.Block,
                card.Repair, card.Draw, card.PowderCost, template, card.AllowedStages, card.Action, card.RequiredCrew,
                card.ArtId, card.CannonballCost, card.RequiredComponent, card.UpgradeLevel, card.SchoolId, e);
            string text = CoreBattleCardText.RenderTemplate(display.DescriptionTemplate, display, stage);
            if (SchoolBattleActive(stage))
            {
                // Conditions keep their authored base numbers; a separate total avoids double-counting their bonus.
                if (current.Damage != basic.Damage) text += "\n本次伤害 " + GetBattleCardDamage(card, stage) + "。";
                if (current.Block != basic.Block) text += "\n本次格挡 " + current.Block + "。";
                if (current.FullRepair != basic.FullRepair) text += "\n本次治疗 " + current.ActualRepair + "。";
            }
            return text;
        }
    }
}
