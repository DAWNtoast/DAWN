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
            int flatRepair = 0, bool medicineBoostAllowed = true, int flatDamage = 0, int wood = 0,
            int unprovisionedRepair = UnprovisionedRepair, bool fixedMedicineCost = false)
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
            FallbackRepair = needsSupply ? Math.Min(unprovisionedRepair, FullRepair) : FullRepair;
            BaseRepair = FallbackRepair;
            HasMedicineBoost = needsSupply && stage == NavalBattleStage.Boarding && medicine > 0 && FullRepair > FallbackRepair;
            HasWoodBoost = needsSupply && stage != NavalBattleStage.Boarding && wood > 0 && FullRepair > FallbackRepair;
            Repair = !needsSupply || HasMedicineBoost || HasWoodBoost ? FullRepair : FallbackRepair;
            IsRepairPreview = !missingHealth.HasValue;
            ActualRepair = missingHealth.HasValue ? Math.Min(Math.Max(0, missingHealth.Value), Repair) : 0;
            // A supply is useful only if the fixed fallback cannot fill the
            // wound. Quoting and overhealing never consume inventory.
            MedicineCost = HasMedicineBoost && missingHealth.HasValue &&
                (fixedMedicineCost || missingHealth.Value > FallbackRepair) ? 1 : 0;
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
            if (stage == NavalBattleStage.Boarding)
            {
                card = CoreBoardingCardCatalog.ForBoarding(card);
                if (card.BoardingSpec != null) return GetBoardingCardDamageQuote(card);
            }
            int damage = GetBattleCardEffects(card, stage).Damage;
            if (stage == NavalBattleStage.Naval) damage = SchoolSum(damage, NavalExpansionFirstDamageBonus(card));
            if (stage == NavalBattleStage.Naval && NavalExpansionActive && card.Damage > 0 && card.SchoolId == "thunder" &&
                CoreNavalSchoolExpansionCatalog.FindSpec(card.Id) != null)
                damage = SchoolSum(damage, Battle.Schools.ThunderReadyDamage);
            if (card.Action != NavalBattleCardAction.Cannon) return damage;
            int firepower = Math.Max(0, (EffectiveFirepower - 8) / 2);
            return SchoolSum(damage, SchoolSum(firepower, SchoolFinalLoadedBonus()));
        }

        public CoreBattleCardEffects GetBattleCardEffects(int cardIndex, NavalBattleStage stage) =>
            GetBattleCardEffects(GetBattleCard(cardIndex), stage);

        public CoreBattleCardEffects GetBattleCardEffects(CoreBattleCard card, NavalBattleStage stage)
        {
            if (stage == NavalBattleStage.Boarding)
                card = CoreBoardingCardCatalog.ForBoarding(card);
            card = GetSupplyAdjustedBattleCard(card);
            int ordinaryCrew = Phase == CoreDemoPhase.Battle && Battle != null
                ? Battle.PlayerOrdinaryCrew : SelectedOrdinaryCrew;
            int? missing = null;
            if (Phase == CoreDemoPhase.Battle && Battle != null && Battle.Stage == stage)
                missing = stage == NavalBattleStage.Boarding
                    ? Math.Max(0, Battle.PlayerMaxBoardingHealth - Battle.PlayerBoardingHealth)
                    : Math.Max(0, MaxHull - Hull);
            // New boarding rules own their conditional effects and resource payments.
            // Never attach retired school multipliers to the authored values.
            if (stage == NavalBattleStage.Boarding && card?.BoardingSpec != null)
            {
                bool desperate = BActive && CTBDesperate;
                int bloodBlock = desperate && card.BoardingSpec.Key == "blood_12" ? BValue(card, 8) - card.Block : 0;
                int bloodRepair = desperate && card.BoardingSpec.Key == "blood_09" ? BValue(card, 9) - card.Repair : 0;
                int fallback = desperate && card.BoardingSpec.Key == "blood_09" ? 3 : CoreBattleCardEffects.UnprovisionedRepair;
                return new CoreBattleCardEffects(card, stage, ordinaryCrew, Cargo.Medicine, missing,
                    Math.Max(0, GetBoardingCardDamageQuote(card) - card.Damage), bloodBlock, bloodRepair,
                    unprovisionedRepair: fallback, fixedMedicineCost: card.BoardingSpec.Key != "basic_06" &&
                        (!missing.HasValue || missing.Value > 0 || card.BoardingSpec.Key == "basic_07" || card.BoardingSpec.Key == "tide_12"));
            }
            GetSchoolCardBonuses(card, stage, out int extraDamage, out int extraBlock, out int extraRepair);
            return new CoreBattleCardEffects(card, stage, ordinaryCrew, Cargo.Medicine, missing,
                extraDamage, extraBlock, extraRepair,
                SchoolBloodMendQuote(card, stage), card?.SchoolEffect?.Mechanic != CoreSchoolMechanic.BloodMend &&
                    card?.SchoolEffect?.Mechanic != CoreSchoolMechanic.BloodDrain && !CoreComponentFieldCards.IsFieldCard(card),
                SchoolAttackStanceQuote(card, stage), Cargo.Wood);
        }

        /// <summary>Compact, stage-specific effects for the card face. The definition itself stays unchanged.</summary>
        public string BattleCardEffectDescription(CoreBattleCard card, NavalBattleStage stage)
        {
            if (card == null) return string.Empty;
            if (stage == NavalBattleStage.Boarding)
                card = CoreBoardingCardCatalog.ForBoarding(card);
            if (card.BoardingSpec != null) return card.Description;
            if (card.SchoolEffect?.Mechanic == CoreSchoolMechanic.TideWard ||
                card.Id == "card_school_tide_n03") return BattleCardFaceDescription(card, stage);
            card = GetSupplyAdjustedBattleCard(card);
            CoreBattleCardEffects effect = GetBattleCardEffects(card, stage);
            if (card.SchoolEffect != null) return SchoolCardEffectDescription(card, stage, effect);
            if (stage != NavalBattleStage.Boarding)
            {
                // Preserve every authored condition and line break, replacing
                // only effect placeholders with the same quote combat uses.
                var display = new CoreBattleCard(card.Id, card.Name, card.Type, card.Energy,
                    effect.Damage, effect.Block, effect.FullRepair, card.Draw, card.PowderCost,
                    SchoolBattleActive(stage) ? CoreSpecialCardRules.CostDescription(card.DescriptionTemplate, card, Battle) : card.DescriptionTemplate,
                    card.AllowedStages, card.Action, card.RequiredCrew, card.ArtId,
                    card.CannonballCost, card.RequiredComponent, card.UpgradeLevel, card.SchoolId, card.SchoolEffect, card.BoardingBlock);
                return CoreBattleCardText.RenderTemplate(display.DescriptionTemplate, display, stage, effect.FullRepair);
            }
            // Use the existing description builder so strategies, draws and resource
            // costs remain in sync. Boarding also gives all-stage healing its proper label.
            var effectiveCard = new CoreBattleCard(card.Id, card.Name, card.Type, card.Energy,
                effect.Damage, effect.Block, effect.FullRepair, card.Draw, card.PowderCost, string.Empty,
                NavalBattleStages.Boarding, card.Action, card.RequiredCrew, card.ArtId,
                card.CannonballCost, card.RequiredComponent, card.UpgradeLevel, card.SchoolId, card.SchoolEffect);
            return DescribeUpgradedBattleCard(effectiveCard, card.BurnDamage);
        }

        /// <summary>Port comparison: authored effects and current supply-dependent effects are labelled separately.</summary>
        public string BattleCardPortDescription(CoreBattleCard card)
        {
            if (card == null) return string.Empty;
            if (card.BoardingSpec != null) return card.Description;
            if (!card.IsAllowedInStage(NavalBattleStage.Boarding) || card.Damage == 0 && card.Block == 0 && card.Repair == 0)
                return BattleCardEffectDescription(card, NavalBattleStage.Naval);
            CoreBattleCardEffects effect = GetBattleCardEffects(card, NavalBattleStage.Boarding);
            StringBuilder current = new StringBuilder();
            if (card.Damage > 0) current.Append("造成 ").Append(effect.Damage).Append(" 点伤害\n");
            if (card.Block > 0) current.Append("获得 ").Append(effect.Block).Append(" 点格挡\n");
            if (card.Repair > 0) current.Append("恢复 ").Append(effect.Repair).Append(" 点生命（上限）\n");
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
            if (CoreBoardingCardCatalog.ForBoarding(card)?.BoardingSpec != null)
                return string.Empty; // The authored boarding text already states medicine, fallback and timing.
            CoreBattleCardEffects effect = GetBattleCardEffects(card, stage);
            if (effect.FullRepair <= 0 || effect.FullRepair == effect.FallbackRepair) return string.Empty;
            string supply = "恢复 " + effect.FallbackRepair + " 点生命值，消耗 1 药物额外恢复 " +
                (effect.FullRepair - effect.FallbackRepair) + " 点生命值。";
            if (portPreview || effect.IsRepairPreview)
                return CoreBattleCardText.FormatEffects("治疗预览：" + supply);
            return CoreBattleCardText.FormatEffects("本次实际恢复 " + effect.ActualRepair + " 点生命值；" + supply);
        }

        public string BattleCardWoodDescription(CoreBattleCard card, NavalBattleStage stage, bool portPreview = false)
        {
            if (card == null || stage == NavalBattleStage.Boarding || !card.IsAllowedInStage(stage)) return string.Empty;
            CoreBattleCardEffects effect = GetBattleCardEffects(card, stage);
            if (effect.FullRepair <= 0 || effect.FullRepair == effect.FallbackRepair) return string.Empty;
            string supply = "修复 " + effect.FallbackRepair + " 点船体，消耗 1 木板额外修复 " +
                (effect.FullRepair - effect.FallbackRepair) + " 点船体。";
            if (portPreview || effect.IsRepairPreview)
                return CoreBattleCardText.FormatEffects("维修预览：" + supply);
            return CoreBattleCardText.FormatEffects("本次实际修复 " + effect.ActualRepair + " 点船体；" + supply);
        }
    }
}
