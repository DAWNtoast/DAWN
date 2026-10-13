using System;
using System.Text;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Player card values after upgrades and current effects, independent of crew count.</summary>
    public readonly struct CoreBattleCardEffects
    {
        // Retained for callers compiled against the former crew-scaling API.
        public const int ReferenceOrdinaryCrew = 28;
        public const decimal MinimumBoardingScale = 1m;
        public const decimal MaximumBoardingScale = 1m;
        public const int UnprovisionedRepair = 2;

        internal CoreBattleCardEffects(CoreBattleCard card, NavalBattleStage stage, int ordinaryCrew,
            int medicine, int? missingHealth, int extraDamage = 0, int extraBlock = 0, int extraRepair = 0,
            int flatRepair = 0, bool medicineBoostAllowed = true, int flatDamage = 0, int wood = 0)
        {
            OrdinaryCrew = Math.Max(0, ordinaryCrew);
            BoardingScale = 1m;
            Damage = Sum(Sum(card == null ? 0 : card.Damage, extraDamage), flatDamage);
            int baseBlock = card == null ? 0 : stage == NavalBattleStage.Boarding
                ? card.BoardingBlock ?? card.Block : card.Block;
            Block = Sum(baseBlock, extraBlock);
            FullRepair = Sum(Sum(card == null ? 0 : card.Repair, extraRepair), flatRepair);
            bool needsSupply = medicineBoostAllowed && card != null && card.IsAllowedInStage(stage) && FullRepair > 0;
            // The two-point fallback is final health and does not grow with
            // upgrades. Blood healing opts out of the supply gate.
            FallbackRepair = needsSupply ? Math.Min(UnprovisionedRepair, FullRepair) : FullRepair;
            BaseRepair = FallbackRepair;
            HasMedicineBoost = needsSupply && stage == NavalBattleStage.Boarding && medicine > 0 && FullRepair > FallbackRepair;
            HasWoodBoost = needsSupply && stage != NavalBattleStage.Boarding && wood > 0 && FullRepair > FallbackRepair;
            Repair = !needsSupply || HasMedicineBoost || HasWoodBoost ? FullRepair : FallbackRepair;
            IsRepairPreview = !missingHealth.HasValue;
            ActualRepair = missingHealth.HasValue ? Math.Min(Math.Max(0, missingHealth.Value), Repair) : 0;
            // A supply is useful only if the fixed fallback cannot fill the
            // wound. Quoting and overhealing never consume inventory.
            MedicineCost = HasMedicineBoost && missingHealth.HasValue &&
                missingHealth.Value > FallbackRepair ? 1 : 0;
            WoodCost = HasWoodBoost && missingHealth.HasValue &&
                missingHealth.Value > FallbackRepair ? 1 : 0;
        }

        public int Damage { get; }
        public int Block { get; }
        public int Repair { get; }
        public int FullRepair { get; }
        public int FallbackRepair { get; }
        // Compatibility for existing tooltips: the base is the no-supply fallback.
        public int BaseRepair { get; }
        public int ActualRepair { get; }
        public int MedicineCost { get; }
        public int WoodCost { get; }
        public bool HasMedicineBoost { get; }
        public bool HasWoodBoost { get; }
        public bool IsRepairPreview { get; }
        public int OrdinaryCrew { get; }
        public decimal BoardingScale { get; }

        public static decimal BoardingScaleFor(int ordinaryCrew) => 1m;
        private static int Sum(int first, int second) => (int)Math.Min(int.MaxValue, (long)Math.Max(0, first) + Math.Max(0, second));
    }

    public sealed partial class CoreVoyageSession
    {
        /// <summary>Damage before critical hits and enemy guard, including the current cannon bonuses.</summary>
        public int GetBattleCardDamage(CoreBattleCard card, NavalBattleStage stage)
        {
            if (card == null) return 0;
            int damage = GetBattleCardEffects(card, stage).Damage;
            if (card.Action != NavalBattleCardAction.Cannon) return damage;
            int firepower = IsGuidedBossBattle ? 0 : Math.Max(0, (EffectiveFirepower - 8) / 2);
            return SchoolSum(damage, SchoolSum(firepower, SchoolFinalLoadedBonus()));
        }

        public CoreBattleCardEffects GetBattleCardEffects(int cardIndex, NavalBattleStage stage) =>
            GetBattleCardEffects(GetBattleCard(cardIndex), stage);

        public CoreBattleCardEffects GetBattleCardEffects(CoreBattleCard card, NavalBattleStage stage)
        {
            card = GetSupplyAdjustedBattleCard(card);
            int ordinaryCrew = Phase == CoreDemoPhase.Battle && Battle != null
                ? Battle.PlayerOrdinaryCrew : SelectedOrdinaryCrew;
            int? missing = null;
            if (Phase == CoreDemoPhase.Battle && Battle != null && Battle.Stage == stage)
                missing = stage == NavalBattleStage.Boarding
                    ? Math.Max(0, Battle.PlayerMaxBoardingHealth - Battle.PlayerBoardingHealth)
                    : Math.Max(0, MaxHull - Hull);
            GetSchoolCardBonuses(card, stage, out int extraDamage, out int extraBlock, out int extraRepair);
            return new CoreBattleCardEffects(card, stage, ordinaryCrew, Cargo.Medicine, missing,
                extraDamage, extraBlock, extraRepair,
                SchoolBloodMendQuote(card, stage), card?.SchoolEffect?.Mechanic != CoreSchoolMechanic.BloodMend &&
                    card?.SchoolEffect?.Mechanic != CoreSchoolMechanic.BloodDrain,
                SchoolAttackStanceQuote(card, stage), Cargo.Wood);
        }

        /// <summary>Compact, stage-specific effects for the card face. The definition itself stays unchanged.</summary>
        public string BattleCardEffectDescription(CoreBattleCard card, NavalBattleStage stage)
        {
            if (card == null) return string.Empty;
            card = GetSupplyAdjustedBattleCard(card);
            CoreBattleCardEffects effect = GetBattleCardEffects(card, stage);
            if (card.SchoolEffect != null) return SchoolCardEffectDescription(card, stage, effect);
            if (stage != NavalBattleStage.Boarding)
            {
                // Preserve every authored condition and line break, replacing
                // only effect placeholders with the same quote combat uses.
                return new CoreBattleCard(card.Id, card.Name, card.Type, card.Energy,
                    effect.Damage, effect.Block, effect.Repair, card.Draw, card.PowderCost, card.DescriptionTemplate,
                    card.AllowedStages, card.Action, card.RequiredCrew, card.ArtId,
                    card.CannonballCost, card.RequiredComponent, card.UpgradeLevel, card.SchoolId, card.SchoolEffect, card.BoardingBlock).Description;
            }
            // Use the existing description builder so strategies, draws and resource
            // costs remain in sync. Boarding also gives all-stage healing its proper label.
            var effectiveCard = new CoreBattleCard(card.Id, card.Name, card.Type, card.Energy,
                effect.Damage, effect.Block, effect.Repair, card.Draw, card.PowderCost, string.Empty,
                NavalBattleStages.Boarding, card.Action, card.RequiredCrew, card.ArtId,
                card.CannonballCost, card.RequiredComponent, card.UpgradeLevel, card.SchoolId, card.SchoolEffect);
            return DescribeUpgradedBattleCard(effectiveCard, card.BurnDamage);
        }

        /// <summary>Port comparison: authored effects and current supply-dependent effects are labelled separately.</summary>
        public string BattleCardPortDescription(CoreBattleCard card)
        {
            if (card == null) return string.Empty;
            if (!card.IsAllowedInStage(NavalBattleStage.Boarding) || card.Damage == 0 && card.Block == 0 && card.Repair == 0)
                return BattleCardEffectDescription(card, NavalBattleStage.Naval);
            CoreBattleCardEffects effect = GetBattleCardEffects(card, NavalBattleStage.Boarding);
            StringBuilder current = new StringBuilder();
            if (card.Damage > 0) current.Append("伤害 ").Append(effect.Damage).Append('\n');
            if (card.Block > 0) current.Append("格挡 ").Append(effect.Block).Append('\n');
            if (card.Repair > 0) current.Append("恢复 ").Append(effect.Repair).Append("（上限）\n");
            return "卡牌效果：\n" + card.Description +
                "\n\n当前接舷效果：\n" +
                current.ToString().TrimEnd('\n') +
                (card.Repair > 0 ? "\n" + BattleCardMedicineDescription(card, NavalBattleStage.Boarding, true) : string.Empty);
        }

        /// <summary>Current healing and its fixed unprovisioned fallback share the combat quote.</summary>
        public string BattleCardMedicineDescription(CoreBattleCard card, NavalBattleStage stage, bool portPreview = false)
        {
            if (card == null || stage != NavalBattleStage.Boarding || !card.IsAllowedInStage(stage))
                return string.Empty;
            CoreBattleCardEffects effect = GetBattleCardEffects(card, stage);
            if (effect.FullRepair <= 0 || effect.FullRepair == effect.FallbackRepair) return string.Empty;
            string supply = effect.HasMedicineBoost ? "有药完整 " + effect.FullRepair : "无随船药品";
            if (portPreview || effect.IsRepairPreview)
                return CoreBattleCardText.FormatEffects("治疗预览：" + supply + "，无药保底 " + effect.FallbackRepair + "；有效治疗消耗 1 药品，预览不消耗。");
            return CoreBattleCardText.FormatEffects("本次实际恢复 " + effect.ActualRepair + "，" +
                (effect.MedicineCost > 0 ? "药品 -1" : "不耗药") + "；" + supply + "，无药保底 " + effect.FallbackRepair + "。");
        }

        public string BattleCardWoodDescription(CoreBattleCard card, NavalBattleStage stage, bool portPreview = false)
        {
            if (card == null || stage == NavalBattleStage.Boarding || !card.IsAllowedInStage(stage)) return string.Empty;
            CoreBattleCardEffects effect = GetBattleCardEffects(card, stage);
            if (effect.FullRepair <= 0 || effect.FullRepair == effect.FallbackRepair) return string.Empty;
            string supply = effect.HasWoodBoost ? "有木板完整 " + effect.FullRepair : "无随船木板";
            if (portPreview || effect.IsRepairPreview)
                return CoreBattleCardText.FormatEffects("维修预览：" + supply + "，无木板保底 " + effect.FallbackRepair + "；有效维修消耗 1 木板，预览不消耗。");
            return CoreBattleCardText.FormatEffects("本次实际修复 " + effect.ActualRepair + "，" +
                (effect.WoodCost > 0 ? "木板 -1" : "不耗木板") + "；" + supply + "，无木板保底 " + effect.FallbackRepair + "。");
        }
    }
}
