using System;
using System.Globalization;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        /// <summary>
        /// A stable card-face template with current effect values in its original slots.
        /// Quotes do not pay costs, change state, predict critical hits or include future damage.
        /// Detailed explanations remain available through BattleCardDescription.
        /// </summary>
        public string BattleCardFaceDescription(CoreBattleCard card, NavalBattleStage stage)
        {
            if (card == null) return string.Empty;
            if (stage == NavalBattleStage.Boarding)
                card = CoreBoardingCardCatalog.ForBoarding(card);
            if (card.BoardingSpec != null) return card.Description;
            // Keep both authored supply alternatives visible, as for boarding healing.
            // GetBattleCardEffects reports the actual two-point fallback separately.
            if (CoreNavalSchoolExpansionCatalog.FindSpec(card.Id)?.Mechanic == CoreNavalExpansionMechanic.TideAnchor)
                return card.Description;
            int tideWood = card.SchoolEffect?.Mechanic == CoreSchoolMechanic.TideWard &&
                (!SchoolBattleActive(stage) || Cargo.Wood >= card.SchoolEffect.WoodCost) ? card.SchoolEffect.WoodCost : 0;
            // Supply alternatives stay visible even when the current inventory cannot pay
            // for the enhanced effect. Combat quotes still use the adjusted card below.
            bool tideWard = card.SchoolEffect?.Mechanic == CoreSchoolMechanic.TideWard;
            if (!tideWard) card = GetSupplyAdjustedBattleCard(card);
            CoreBattleCardEffects current = GetBattleCardEffects(card, stage);
            CoreBattleCardEffects effect = FaceCardEffects(card, stage, current);
            // Keep the signed firepower adjustment as well as loading bonuses when substituting
            // a port-only payable school effect. Low firepower must also lower the visible value.
            int damage = (int)Math.Min(int.MaxValue, Math.Max(0L,
                (long)GetBattleCardUnmodifiedFieldDamage(card, stage) - current.Damage + effect.Damage));
            // Mana X remains a formula on this face. Quote the fixed slot independently
            // instead of subtracting a possibly saturated X contribution from the total.
            if (CoreNavalSchoolExpansionCatalog.FindSpec(card.Id)?.Mechanic == CoreNavalExpansionMechanic.ManaBurst)
                damage = SchoolSum(effect.Damage, stage == NavalBattleStage.Naval ? NavalExpansionFirstDamageBonus(card) : 0);
            if (card.Id == "card_school_thunder_n09")
                damage = SchoolSum(effect.Damage, SchoolBattleActive(stage) ?
                    SchoolSum(NavalExpansionFirstDamageBonus(card), Battle.Schools.ThunderReadyDamage) : 0);
            // Thunder cards retain their threshold/formula clause on the face. Remove
            // that same conditional contribution from the base slot to avoid counting it twice.
            if (SchoolBattleActive(stage) && (card.SchoolEffect?.Mechanic == CoreSchoolMechanic.ThunderChain ||
                card.SchoolEffect?.Mechanic == CoreSchoolMechanic.ThunderFinish))
            {
                CoreSchoolCardEffect chainEffect = card.SchoolEffect;
                int chainBonus = chainEffect.Mechanic == CoreSchoolMechanic.ThunderChain && chainEffect.Threshold > 0
                    ? (Battle.Schools.ThunderChain >= chainEffect.Threshold ? chainEffect.Power : 0)
                    : SchoolProduct(chainEffect.Power, Battle.Schools.ThunderChain);
                damage = Math.Max(0, damage - chainBonus);
            }
            int block = tideWard ? card.Block : effect.Block;
            if (SchoolBattleActive(stage) && stage != NavalBattleStage.Boarding && block > 0)
                block = NavalArcaneBlockGain(SchoolSum(block, Battle.PlayerDeckBuff));
            // These cards state their base effect and their field-dependent bonus separately.
            // Keep the bonus out of the base slot so the face never counts it twice.
            if (SchoolBattleActive(stage) && CoreComponentFieldCards.IsFieldCard(card))
            {
                damage = Math.Max(0, damage - ComponentFieldDamageBonus(card));
                block = Math.Max(0, block - ComponentFieldBlockBonus(card));
            }
            // Blood cards state their base and conditional benefit separately.
            // The quote already combines them, so restore the base slot on the face.
            CoreSchoolMechanic? bloodMechanic = card.SchoolEffect?.Mechanic;
            if (bloodMechanic == CoreSchoolMechanic.BloodRisk || bloodMechanic == CoreSchoolMechanic.BloodWard)
            {
                int paidBenefit = !SchoolBattleActive(stage) || HasBloodPaidBenefit(card) ? card.SchoolEffect.Power : 0;
                if (bloodMechanic == CoreSchoolMechanic.BloodRisk) damage = Math.Max(0, damage - paidBenefit);
                else block = Math.Max(0, block - paidBenefit);
            }
            else if (bloodMechanic == CoreSchoolMechanic.BloodExecute && SchoolBattleActive(stage))
            {
                GetSchoolCardBonuses(card, stage, out int bloodDamage, out _, out _);
                damage = Math.Max(0, damage - bloodDamage);
            }
            bool repairSupply = FaceCardUsesRepairSupply(card, stage, effect);
            int repair = repairSupply ? effect.FullRepair : effect.IsRepairPreview ? effect.Repair : effect.ActualRepair;
            int medicine = effect.IsRepairPreview && effect.HasMedicineBoost ? 1 : effect.MedicineCost;
            int wood = (effect.IsRepairPreview && effect.HasWoodBoost ? 1 : effect.WoodCost) + tideWood;
            int strategyAmount = card.StrategyAmount;
            int strategyLimit = card.StrategyLimit;
            if (card.Action == NavalBattleCardAction.Load || card.Action == NavalBattleCardAction.PrecisionLoad)
            {
                strategyAmount = GetBattleLoadedShotIncrease(card);
                strategyLimit = GetBattleLoadedShotLimit(card);
            }
            else if (SchoolBattleActive(stage) && card.Action == NavalBattleCardAction.Polish)
                strategyAmount = FaceCapacityGain(card.StrategyAmount, Battle.PlayerDeckBuff, strategyLimit);

            CoreSchoolCardEffect school = card.SchoolEffect;
            int power = school?.Power ?? 0;
            int secondary = school?.Secondary ?? 0;

            string template = FaceCardTemplate(card, SchoolBattleActive(stage));
            template = CoreNavalFireArcanePower.RenderTemplate(template, card);
            template = CoreNavalThunderPower.RenderTemplate(template, card);
            if (SchoolBattleActive(stage)) template = CoreSpecialCardRules.CostDescription(template, card, Battle);
            return CoreBattleCardText.FormatCardEffects(template
                .Replace("{damage}", FaceNumber(damage))
                .Replace("{thunderCollapseChain}", FaceNumber(CoreNavalExpansionBalance.ThunderCollapseChainCoefficient(card.UpgradeLevel)))
                .Replace("{manaBurstDamage}", FaceNumber(CoreNavalExpansionBalance.ManaBurstDamageCoefficient(card.UpgradeLevel)))
                .Replace("{tideStrikeDamage}", FaceNumber(CoreNavalExpansionBalance.TideStrikeDamageCoefficient(card.UpgradeLevel)))
                .Replace("{desperateBonus}", FaceNumber(CoreNavalExpansionBalance.DesperateCannonBonus(card.UpgradeLevel)))
                .Replace("{block}", FaceNumber(block))
                .Replace("{boardingBlock}", FaceNumber(card.BoardingBlock ?? card.Block))
                .Replace("{repair}", FaceNumber(repair))
                .Replace("{draw}", FaceNumber(card.Draw))
                .Replace("{energyRecovery}", FaceNumber(CoreBattleUpgradeBalance.EnergyRecovery(card)))
                .Replace("{movement}", FaceNumber(card.MovementMeters))
                .Replace("{navalActionPoints}", FaceNumber(card.NavalActionPointGain))
                .Replace("{strategyAmount}", FaceNumber(strategyAmount))
                .Replace("{strategyLimit}", FaceNumber(strategyLimit))
                .Replace("{burnDuration}", FaceNumber(card.BurnDuration))
                .Replace("{burnDamage}", FaceNumber(card.BurnDamage))
                .Replace("{powder}", FaceNumber(card.PowderCost))
                .Replace("{cannonballs}", FaceNumber(card.CannonballCost))
                .Replace("{schoolPower}", FaceNumber(power))
                .Replace("{schoolSecondary}", FaceNumber(secondary))
                .Replace("{schoolThreshold}", FaceNumber(SchoolBattleActive(stage) &&
                    (school?.Mechanic == CoreSchoolMechanic.BloodRisk || school?.Mechanic == CoreSchoolMechanic.BloodWard)
                    ? CoreSpecialCardRules.HealthCost(card, Battle) : school?.Threshold ?? 0))
                .Replace("{schoolDuration}", FaceNumber(school?.Duration ?? 0))
                .Replace("{schoolLimit}", FaceNumber(school?.Limit ?? 0))
                .Replace("{medicine}", FaceNumber(medicine))
                .Replace("{wood}", FaceNumber(wood)), card, stage, repairSupply ? (int?)effect.FullRepair : null) +
                (stage == NavalBattleStage.Naval ? NavalFieldAttackCardDescription(card) : string.Empty);
        }

        public string BattleCardFaceDescription(CoreBattleCard card) =>
            BattleCardFaceDescription(card, Battle == null ? NavalBattleStage.Naval : Battle.Stage);

        private CoreBattleCardEffects FaceCardEffects(CoreBattleCard card, NavalBattleStage stage, CoreBattleCardEffects current)
        {
            if (SchoolBattleActive(stage) || card.SchoolEffect == null) return current;
            // A catalogue has no target state. Payable effects have a deterministic potential
            // result; burn, marks, chain and previous guard conditions are not invented.
            int extraDamage = 0, extraBlock = 0, extraRepair = 0, flatRepair = 0;
            CoreSchoolCardEffect school = card.SchoolEffect;
            switch (school.Mechanic)
            {
                case CoreSchoolMechanic.ArcaneRelease: case CoreSchoolMechanic.BloodRisk: extraDamage = school.Power; break;
                case CoreSchoolMechanic.ArcaneWard: case CoreSchoolMechanic.BloodWard:
                case CoreSchoolMechanic.TideCleanse: extraBlock = school.Power; break;
                case CoreSchoolMechanic.ArcaneMend: case CoreSchoolMechanic.CurseDrain: extraRepair = school.Power; break;
                case CoreSchoolMechanic.BloodMend:
                    int maximum = stage == NavalBattleStage.Boarding
                        ? CoreBattleBalance.BoardingHealthForOrdinaryCrew(current.OrdinaryCrew) : MaxHull;
                    flatRepair = (int)Math.Min(SchoolDefault(school.Limit, int.MaxValue),
                        (long)maximum * Math.Min(100, school.Power) / 100); break;
            }
            return new CoreBattleCardEffects(card, stage, current.OrdinaryCrew, Cargo.Medicine, null,
                extraDamage, extraBlock, extraRepair, flatRepair,
                school.Mechanic != CoreSchoolMechanic.BloodMend && school.Mechanic != CoreSchoolMechanic.BloodDrain,
                wood: Cargo.Wood);
        }

        private static bool FaceCardUsesRepairSupply(CoreBattleCard card, NavalBattleStage stage, CoreBattleCardEffects effect) =>
            card.IsAllowedInStage(stage) && card.SchoolEffect?.Mechanic != CoreSchoolMechanic.BloodMend &&
            card.SchoolEffect?.Mechanic != CoreSchoolMechanic.BloodDrain &&
            (card.Repair > 0 || card.SchoolEffect?.Mechanic == CoreSchoolMechanic.ArcaneMend ||
                card.SchoolEffect?.Mechanic == CoreSchoolMechanic.CurseDrain || card.SchoolEffect?.Mechanic == CoreSchoolMechanic.TideRenew ||
                card.SchoolEffect?.Mechanic == CoreSchoolMechanic.FireHeal);

        private static int FaceCapacityGain(int amount, int current, int limit) =>
            Math.Min(Math.Max(0, amount), Math.Max(0, limit - Math.Max(0, current)));

        private static string FaceNumber(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string FaceCardTemplate(CoreBattleCard card, bool liveBattle)
        {
            string template = card.DescriptionTemplate.Replace(
                "炮战修复 {repair} 船体，白刃战恢复 {repair} 船员生命", "恢复当前阶段生命 {repair}");
            CoreSchoolCardEffect school = card.SchoolEffect;
            if (school == null) return template;
            // The face carries the cost, trigger and result. Shared keyword explanations
            // are removed by FormatCardEffects after all live values have been substituted.
            template = template.Replace("本次雷霆攻击后连锁 +1，上限6，回合结束清空", "雷攻后连锁 +1，上限6")
                .Replace("消耗 {wood} 木板；付费护盾吸收伤害才蓄积潮势", "耗 {wood} 木板；此盾挡伤蓄潮势")
                .Replace("无木板时基础格挡 2，不蓄积潮势，保底不随强化增长", "无木板仅基础格挡2，不蓄势");
            // Immediate bonuses already occupy the main damage/block/repair slots. These fixed
            // condition hints cannot be mistaken for a second bonus to add to those numbers.
            switch (school.Mechanic)
            {
                case CoreSchoolMechanic.FireExploit:
                    return template.Replace("出牌前敌方燃烧：伤害额外 +{schoolPower}", "出牌前敌方燃烧时增伤");
                case CoreSchoolMechanic.FireGuard:
                    return template.Replace("敌方燃烧时额外格挡 +{schoolPower}", "敌方燃烧时强化格挡");
                case CoreSchoolMechanic.FireHeal:
                    return template.Replace("敌方燃烧时额外恢复 {schoolPower}", "敌方燃烧时强化恢复");
                case CoreSchoolMechanic.ArcaneRelease:
                    return template.Replace("伤害额外 +{schoolPower}", "强化伤害")
                        .Replace("额外伤害 +{schoolPower}", "强化伤害");
                case CoreSchoolMechanic.ArcaneWard:
                    return template.Replace("格挡额外 +{schoolPower}", "强化格挡")
                        .Replace("额外格挡 +{schoolPower}", "强化格挡");
                case CoreSchoolMechanic.ArcaneMend:
                    return template.Replace("恢复额外 +{schoolPower}", "强化恢复")
                        .Replace("额外恢复 {schoolPower} 船体", "强化恢复");
                case CoreSchoolMechanic.CurseExploit:
                    return template.Replace("伤害额外 +{schoolPower}", "强化伤害");
                case CoreSchoolMechanic.CurseWard:
                    if (template.Contains("敌方每有 1 层咒印，获得 {schoolPower} 点普通格挡"))
                        return liveBattle ? template.Replace("获得 {block} 点普通格挡；", string.Empty)
                            .Replace("敌方每有 1 层咒印，获得 {schoolPower} 点普通格挡", "合计获得 {block} 点普通格挡") : template;
                    if (template.Contains("敌方每有 1 层咒印，获得 {schoolPower} 点格挡"))
                        return liveBattle ? template.Replace("格挡 +{block}；", string.Empty)
                            .Replace("敌方每有 1 层咒印，获得 {schoolPower} 点格挡", "合计获得 {block} 格挡") : template;
                    if (template.Contains("按消耗前每层咒印获得 {schoolPower} 额外格挡"))
                    {
                        if (liveBattle) template = template.Replace("获得 {block} 格挡；", string.Empty);
                        return template.Replace("按消耗前每层咒印获得 {schoolPower} 额外格挡", liveBattle
                            ? "合计获得 {block} 格挡" : "按消耗前咒印，每层获得 {schoolPower} 基础格挡");
                    }
                    if (liveBattle) template = template.Replace("格挡 +{block}；", string.Empty);
                    return template.Replace("每层获得 {schoolPower} 格挡", liveBattle ?
                        "合计获得 {block} 格挡" : "每层获得 {schoolPower} 基础格挡");
                case CoreSchoolMechanic.CurseDetonate:
                    return template.Replace("消耗所有咒印，每层追加 {schoolPower} 伤害", "消耗所有咒印，伤害随咒印层数提高");
                case CoreSchoolMechanic.CurseDrain:
                    return template.Replace("恢复 {schoolPower} 生命", "恢复 {repair} 生命");
                case CoreSchoolMechanic.TideRiposte:
                    if (liveBattle && (card.Id == "card_school_tide_b03" || card.Id == "card_school_tide_b06"))
                        return template.Replace("{damage}+1.25X", "{damage}").Replace("{damage}+X", "{damage}");
                    return template.Replace("按 {schoolPower}% 追加伤害", "按 {schoolPower}% 折入伤害");
                case CoreSchoolMechanic.TideRenew:
                    return template.Replace("额外恢复 {schoolPower}", "强化恢复");
                case CoreSchoolMechanic.TideCleanse:
                    return template.Replace("获得 {schoolPower} 格挡", template.Contains("{block}")
                        ? "强化格挡" : "获得 {block} 格挡");
                case CoreSchoolMechanic.ThunderChain:
                    return template.Replace("每层已有连锁追加 {schoolPower} 伤害", "已有连锁强化伤害");
                case CoreSchoolMechanic.ThunderWard:
                    return template.Replace("额外格挡 +{schoolPower}", "强化格挡");
                case CoreSchoolMechanic.ThunderFinish:
                    return template.Replace("消耗全部已有连锁，每层追加 {schoolPower} 伤害", "消耗全部已有连锁强化伤害");
                case CoreSchoolMechanic.BloodRisk:
                    return template.Replace("额外伤害 +{schoolPower}", "强化伤害");
                case CoreSchoolMechanic.BloodWard:
                    return template.Replace("额外格挡 +{schoolPower}",
                        template.Contains("{block}") ? "强化格挡" : "获得 {block} 格挡");
                case CoreSchoolMechanic.BloodExecute:
                    return template.Replace("额外伤害 +{schoolPower}", "本次伤害提高");
                case CoreSchoolMechanic.BloodMend:
                    return template.Replace("恢复已失生命的 {schoolPower}%，上限 {schoolLimit}",
                        "恢复 {repair} 点生命（按已失生命的 {schoolPower}%，上限 {schoolLimit}）");
                default: return template;
            }
        }
    }
}
