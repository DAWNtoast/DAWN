using System;

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

        public VoyageEffect EffectiveChoiceEffect(int choiceIndex)
        {
            if (ActiveEncounter == null || choiceIndex < 0 || choiceIndex >= ActiveEncounter.Choices.Count) return null;
            CoreEncounterChoice choice = ActiveEncounter.Choices[choiceIndex];
            VoyageEffect result = CopyVoyageEffect(choice.Effect);
            // These new expedition choices remove a burden; curse is not a
            // currency paid for their reward. Cleanse the amount actually held.
            // Keep old curse-cost events and other voyages on their original rules.
            if (IsTreasureExpedition && result.Curse < 0 &&
                (ActiveEncounter.Id == "moonlit_cleanse" || ActiveEncounter.Id == "moonlit_choir"))
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
