using System;
using System.Linq;

namespace ShatteredSail.Core.Gameplay
{
    public enum CoreEncounterRepeatPolicy { Diminishing, Renewable, OncePerChart, RandomVoyage }

    public sealed partial class CoreVoyageSession
    {
        private double pendingSeaChartRewardMultiplier = 1d;
        private bool pendingSeaChartClaim;
        public int ActiveEncounterClaimCount => activeNode == null || ActiveEncounter == null ? 0 :
            SeaChartClaimCount(ActiveSeaChartState, ActiveEncounter.Id);
        public double ActiveEncounterRewardMultiplier => SeaChartRewardMultiplier(null);

        private string SeaChartChoiceRewardConditionReason(CoreEncounterChoice choice)
        {
            if (choice.StartsBattle || SeaChartRewardMultiplier(choice) >= 1d) return string.Empty;
            VoyageEffect source = choice.Effect;
            // Pure costs and exits can be authored ways through mandatory incidents.
            // Only protect choices whose original reward has been diminished.
            if (!HasOtherExplorationChoiceBenefit(source) && source.SailingBoostHours <= 0d &&
                source.SurveyBoostHours <= 0d && string.IsNullOrWhiteSpace(source.GrantDiscovery) &&
                string.IsNullOrWhiteSpace(source.RevealEncounterId)) return string.Empty;
            int index = -1;
            for (int i = 0; i < ActiveEncounter.Choices.Count; i++)
                if (ReferenceEquals(ActiveEncounter.Choices[i], choice)) { index = i; break; }
            if (index < 0) return string.Empty;

            // EffectiveChoiceEffect returns a detached quote. Remove capped benefits
            // from this local check without changing the authored or displayed effect.
            VoyageEffect effect = EffectiveChoiceEffect(index);
            if (Hull >= MaxHull) effect.Hull = 0;
            if (Morale >= MaxMorale) effect.Morale = 0;
            if (Cargo.Curse <= 0) effect.Curse = Math.Max(0, effect.Curse);
            if (effect.Reveal > 0 && (Map == null || !Map.Nodes.Any(node => node.Encounter != null &&
                node.Encounter.CanRevealByIntel && !node.Visited && !node.IntelRevealed))) effect.Reveal = 0;
            if (HasOtherExplorationChoiceBenefit(effect)) return string.Empty;

            bool discovery = IsKnownExplorationDiscovery(effect.GrantDiscovery) && !HasDiscovery(effect.GrantDiscovery);
            bool target = Map != null && Map.Nodes.Any(node => node.Encounter != null &&
                node.Encounter.Id == effect.RevealEncounterId && node.Encounter.HasChartLocation && !node.Visited && !node.IsChartVisible);
            bool sailing = effect.SailingBoostHours > 0d && Math.Min(ExplorationBoostLimitHours, effect.SailingBoostHours) >
                Math.Max(0d, sailingBoostHoursRemaining - choice.Hours);
            bool survey = effect.SurveyBoostHours > 0d && Math.Min(ExplorationBoostLimitHours, effect.SurveyBoostHours) >
                Math.Max(0d, surveyBoostHoursRemaining - choice.Hours);
            return discovery || target || sailing || survey ? string.Empty : "此处奖励已耗尽，本次没有可获得的收益。";
        }

        public VoyageEffect EffectiveChoiceEffect(int choiceIndex)
        {
            if (ActiveEncounter == null || choiceIndex < 0 || choiceIndex >= ActiveEncounter.Choices.Count) return null;
            CoreEncounterChoice choice = ActiveEncounter.Choices[choiceIndex];
            VoyageEffect result = CopyVoyageEffect(choice.Effect);
            // Authored cleansing removes up to the amount held. The expanded
            // moon voyage services require one curse, not enough curse to pay
            // the entire reduction. Preserve legacy curse-cost event rules.
            bool expeditionCleanse = IsTreasureExpedition &&
                (ActiveEncounter.Id == "moonlit_cleanse" || ActiveEncounter.Id == "moonlit_choir");
            bool voyageCleanse = !IsTreasureVoyage && !choice.StartsBattle &&
                ActiveEncounter.Id != null && ActiveEncounter.Id.StartsWith("event_moon_v2_", StringComparison.Ordinal);
            if (result.Curse < 0 && (expeditionCleanse || voyageCleanse))
                result.Curse = Math.Max(result.Curse, -Cargo.Curse);
            double multiplier = SeaChartRewardMultiplier(choice);
            if (multiplier >= 1d) return result;
            int Scale(long value) => value > 0 ? (int)Math.Min(int.MaxValue, Math.Floor(value * multiplier)) : (int)value;
            // Apply crew benefits once before reducing the earned positive amount.
            // Paid costs, elapsed time, damage and curse are never discounted.
            result.Hull = Scale((long)result.Hull + (result.Hull > 0 && HasDoctor ? 2 : 0));
            result.Medicine = Scale(AdjustedMedicineEffect(result.Medicine));
            result.Wood = Scale(AdjustedSalvageEffect(result.Wood));
            result.Iron = Scale(AdjustedSalvageEffect(result.Iron));
            result.Water = Scale(result.Water); result.Food = Scale(result.Food); result.Beer = Scale(result.Beer);
            result.Cloth = Scale(result.Cloth); result.Powder = Scale(result.Powder); result.Cannonballs = Scale(result.Cannonballs);
            result.Gold = Scale(result.Gold); result.Fame = Scale(result.Fame); result.Morale = Scale(result.Morale);
            result.Reveal = Scale(result.Reveal); result.MapFragments = Scale(result.MapFragments);
            result.Gem = Scale(result.Gem); result.AncientMaterial = Scale(result.AncientMaterial); result.Relic = Scale(result.Relic);
            result.BlackPearl = Scale(result.BlackPearl); result.AncientCompass = Scale(result.AncientCompass); result.Lantern = Scale(result.Lantern);
            result.SailingBoostHours *= multiplier;
            result.SurveyBoostHours *= multiplier;
            if (multiplier <= 0d) { result.GrantDiscovery = null; result.RevealEncounterId = null; }
            result.CrewBonusesApplied = true;
            return result;
        }

        private double SeaChartRewardMultiplier(CoreEncounterChoice choice)
        {
            if (activeSeaChart == null || GuidedTutorialLeg > 0 || IsTreasureVoyage || activeNode == null || ActiveEncounter == null ||
                choice != null && choice.Renewable || ActiveEncounter.RepeatPolicy == CoreEncounterRepeatPolicy.Renewable ||
                ActiveEncounter.RepeatPolicy == CoreEncounterRepeatPolicy.RandomVoyage) return 1d;
            int count = ActiveEncounterClaimCount;
            if (ActiveEncounter.RepeatPolicy == CoreEncounterRepeatPolicy.OncePerChart) return count == 0 ? 1d : 0d;
            return count == 0 ? 1d : count == 1 ? .5d : count == 2 ? .25d : 0d;
        }

        private bool ShouldRecordSeaChartClaim(CoreEncounterChoice choice) => activeSeaChart != null && GuidedTutorialLeg == 0 &&
            !IsTreasureVoyage && activeNode != null && ActiveEncounter != null && !choice.Renewable &&
            ActiveEncounter.RepeatPolicy != CoreEncounterRepeatPolicy.Renewable && ActiveEncounter.RepeatPolicy != CoreEncounterRepeatPolicy.RandomVoyage;

        private void RecordSeaChartEncounterClaim()
        {
            CoreSeaChartState state = ActiveSeaChartState;
            if (state == null || ActiveEncounter == null || activeNode == null ||
                !IsKnownChartEncounter(activeSeaChart, ActiveEncounter.Id, false)) return;
            int count = SeaChartClaimCount(state, ActiveEncounter.Id);
            SetSeaChartClaimCount(state, ActiveEncounter.Id, count == int.MaxValue ? int.MaxValue : count + 1);
            TouchSeaChartProgress();
        }

        private void PrepareSeaChartBattleReward(CoreEncounterChoice choice)
        {
            pendingSeaChartRewardMultiplier = SeaChartRewardMultiplier(choice);
            pendingSeaChartClaim = ShouldRecordSeaChartClaim(choice);
        }

        private long ScaleSeaChartBattleReward(long value) => value > 0 ? (long)Math.Floor(value * pendingSeaChartRewardMultiplier) : value;

        private void RecordSeaChartBattleVictory(bool wasBoss)
        {
            if (pendingSeaChartClaim) RecordSeaChartEncounterClaim();
            if (wasBoss && ActiveEncounter != null && ActiveSeaChartState != null &&
                IsKnownChartEncounter(activeSeaChart, ActiveEncounter.Id, true))
            {
                RecordSeaChartBoss(ActiveSeaChartState, ActiveEncounter.Id);
                TouchSeaChartProgress();
            }
            pendingSeaChartClaim = false;
        }

        private static VoyageEffect CopyVoyageEffect(VoyageEffect source) => new VoyageEffect
        {
            Hull = source.Hull, Water = source.Water, Food = source.Food, Beer = source.Beer, Medicine = source.Medicine,
            Wood = source.Wood, Iron = source.Iron, Cloth = source.Cloth, Powder = source.Powder, Cannonballs = source.Cannonballs,
            Gold = source.Gold, Fame = source.Fame, Morale = source.Morale, Reveal = source.Reveal, MapFragments = source.MapFragments,
            Gem = source.Gem, AncientMaterial = source.AncientMaterial, Relic = source.Relic, BlackPearl = source.BlackPearl,
            AncientCompass = source.AncientCompass, Lantern = source.Lantern, Curse = source.Curse, TreasureClues = source.TreasureClues,
            TreasureComplete = source.TreasureComplete, ForceReturn = source.ForceReturn, PostponeTreasureCore = source.PostponeTreasureCore,
            CrewBonusesApplied = source.CrewBonusesApplied,
            SailingBoostHours = source.SailingBoostHours, SurveyBoostHours = source.SurveyBoostHours,
            GrantDiscovery = source.GrantDiscovery, RevealEncounterId = source.RevealEncounterId,
            TreasureMark = source.TreasureMark, TreasureCredit = source.TreasureCredit,
            TreasureHeat = source.TreasureHeat, TreasureResonance = source.TreasureResonance,
        };
    }
}
