using System;
using System.Globalization;
using System.Text;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Player card values after upgrades and the current stage's ordinary-sailor scaling.</summary>
    public readonly struct CoreBattleCardEffects
    {
        public const int ReferenceOrdinaryCrew = 28;
        public const decimal MinimumBoardingScale = 0.3m;
        public const decimal MaximumBoardingScale = 1.6m;

        internal CoreBattleCardEffects(CoreBattleCard card, NavalBattleStage stage, int ordinaryCrew,
            int medicine, int? missingHealth)
        {
            OrdinaryCrew = Math.Max(0, ordinaryCrew);
            BoardingScale = stage == NavalBattleStage.Boarding ? BoardingScaleFor(OrdinaryCrew) : 1m;
            Damage = Scale(card == null ? 0 : card.Damage, BoardingScale);
            Block = Scale(card == null ? 0 : card.Block, BoardingScale);
            BaseRepair = Scale(card == null ? 0 : card.Repair, BoardingScale);
            HasMedicineBoost = stage == NavalBattleStage.Boarding && card != null &&
                card.IsAllowedInStage(stage) && card.Repair > 0 && medicine > 0;
            // Upgrade, then crew scaling, then the medicine bonus. This quote is
            // pure: previews never consume inventory, and no enemy uses it.
            Repair = HasMedicineBoost ? (int)Math.Min(int.MaxValue,
                (long)BaseRepair + Math.Max(1L, ((long)BaseRepair + 1L) / 2L)) : BaseRepair;
            IsRepairPreview = !missingHealth.HasValue;
            ActualRepair = missingHealth.HasValue ? Math.Min(Math.Max(0, missingHealth.Value), Repair) : 0;
            // Do not spend medicine when the ordinary bandage already fills the
            // entire wound. Partial bonus healing is useful; overhealing is not.
            MedicineCost = HasMedicineBoost && missingHealth.HasValue &&
                missingHealth.Value > BaseRepair && Repair > BaseRepair ? 1 : 0;
        }

        public int Damage { get; }
        public int Block { get; }
        public int Repair { get; }
        public int BaseRepair { get; }
        public int ActualRepair { get; }
        public int MedicineCost { get; }
        public bool HasMedicineBoost { get; }
        public bool IsRepairPreview { get; }
        public int OrdinaryCrew { get; }
        public decimal BoardingScale { get; }

        public static decimal BoardingScaleFor(int ordinaryCrew) =>
            Math.Max(MinimumBoardingScale, Math.Min(MaximumBoardingScale,
                Math.Max(0, ordinaryCrew) / (decimal)ReferenceOrdinaryCrew));

        // The definition already contains its upgrades. Round once, after scaling;
        // enemy profiles and flat naval bonuses never pass through this operation.
        private static int Scale(int value, decimal scale) =>
            (int)Math.Min(int.MaxValue, decimal.Round(Math.Max(0, value) * scale, 0, MidpointRounding.AwayFromZero));
    }

    public sealed partial class CoreVoyageSession
    {
        public CoreBattleCardEffects GetBattleCardEffects(int cardIndex, NavalBattleStage stage) =>
            GetBattleCardEffects(GetBattleCard(cardIndex), stage);

        public CoreBattleCardEffects GetBattleCardEffects(CoreBattleCard card, NavalBattleStage stage)
        {
            int ordinaryCrew = Phase == CoreDemoPhase.Battle && Battle != null
                ? Battle.PlayerOrdinaryCrew : SelectedOrdinaryCrew;
            int? missing = null;
            if (Phase == CoreDemoPhase.Battle && Battle != null && Battle.Stage == stage)
                missing = stage == NavalBattleStage.Boarding
                    ? Math.Max(0, Battle.PlayerMaxBoardingHealth - Battle.PlayerBoardingHealth)
                    : Math.Max(0, MaxHull - Hull);
            return new CoreBattleCardEffects(card, stage, ordinaryCrew, Cargo.Medicine, missing);
        }

        /// <summary>Compact, stage-specific effects for the card face. The definition itself stays unchanged.</summary>
        public string BattleCardEffectDescription(CoreBattleCard card, NavalBattleStage stage)
        {
            if (card == null) return string.Empty;
            if (stage != NavalBattleStage.Boarding) return card.Description;
            CoreBattleCardEffects effect = GetBattleCardEffects(card, stage);
            // Use the existing description builder so strategies, draws and resource
            // costs remain in sync. Boarding also gives all-stage healing its proper label.
            var effectiveCard = new CoreBattleCard(card.Id, card.Name, card.Type, card.Energy,
                effect.Damage, effect.Block, effect.Repair, card.Draw, card.PowderCost, string.Empty,
                NavalBattleStages.Boarding, card.Action, card.RequiredCrew, card.ArtId,
                card.CannonballCost, card.RequiredComponent, card.UpgradeLevel);
            return DescribeUpgradedBattleCard(effectiveCard);
        }

        /// <summary>Port comparison: upgraded base values and this ship's current boarding values are labelled separately.</summary>
        public string BattleCardPortDescription(CoreBattleCard card)
        {
            if (card == null) return string.Empty;
            if (!card.IsAllowedInStage(NavalBattleStage.Boarding) || card.Damage == 0 && card.Block == 0 && card.Repair == 0)
                return card.Description;
            CoreBattleCardEffects effect = GetBattleCardEffects(card, NavalBattleStage.Boarding);
            StringBuilder current = new StringBuilder();
            if (card.Damage > 0) current.Append("伤害 ").Append(effect.Damage).Append('\n');
            if (card.Block > 0) current.Append("格挡 ").Append(effect.Block).Append('\n');
            if (card.Repair > 0) current.Append("恢复 ").Append(effect.Repair).Append("（上限）\n");
            return "基础（" + CoreBattleCardEffects.ReferenceOrdinaryCrew + " 名普通水手）：\n" + card.Description +
                "\n\n当前 " + effect.OrdinaryCrew + " 人接舷实际（" +
                (effect.BoardingScale * 100m).ToString("0.#", CultureInfo.InvariantCulture) + "%）：\n" +
                current.ToString().TrimEnd('\n') +
                (card.Repair > 0 ? "\n" + BattleCardMedicineDescription(card, NavalBattleStage.Boarding, true) : string.Empty);
        }

        /// <summary>Current healing, optional supply use, and the unmodified fallback share the combat quote.</summary>
        public string BattleCardMedicineDescription(CoreBattleCard card, NavalBattleStage stage, bool portPreview = false)
        {
            if (card == null || card.Repair <= 0 || stage != NavalBattleStage.Boarding || !card.IsAllowedInStage(stage))
                return string.Empty;
            CoreBattleCardEffects effect = GetBattleCardEffects(card, stage);
            string supply = effect.HasMedicineBoost ? "有药潜在 " + effect.Repair : "无随船药品";
            if (portPreview || effect.IsRepairPreview)
                return CoreBattleCardText.FormatEffects("治疗预览：" + supply + "，无药基础 " + effect.BaseRepair + "；有效增疗消耗 1 药品，预览不消耗。");
            return CoreBattleCardText.FormatEffects("本次实际恢复 " + effect.ActualRepair + "，" +
                (effect.MedicineCost > 0 ? "药品 -1" : "不耗药") + "；" + supply + "，无药基础 " + effect.BaseRepair + "。");
        }
    }
}
