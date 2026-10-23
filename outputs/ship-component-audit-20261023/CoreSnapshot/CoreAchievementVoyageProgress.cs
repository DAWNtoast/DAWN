using System;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        private const int AchievementDeckStageMask = 7;
        private int achievementEditedDeckStages;
        private bool achievementVoyageSuppliesActive;
        public int AchievementVoyageBattleWins { get; private set; }
        public int AchievementWonEditedDeckStages { get; private set; }
        public bool AchievementVoyageRanOutOfSupplies { get; private set; }
        public bool AchievementVoyageSupplyEvidenceAvailable { get; private set; }

        public void ConfigureAchievementEditedDeckStages(int mask)
        {
            if (Phase != CoreDemoPhase.Port) return;
            if (mask < 0 || (mask & ~AchievementDeckStageMask) != 0) throw new ArgumentOutOfRangeException(nameof(mask));
            achievementEditedDeckStages = mask;
        }

        private void BeginAchievementVoyageProgress()
        {
            AchievementVoyageBattleWins = 0;
            AchievementWonEditedDeckStages = 0;
            AchievementVoyageSupplyEvidenceAvailable = true;
            AchievementVoyageRanOutOfSupplies = Cargo.Water <= 0 || Cargo.Food <= 0;
            achievementVoyageSuppliesActive = true;
        }

        private void ObserveAchievementVoyageSupplies()
        {
            if (!achievementVoyageSuppliesActive || !AchievementVoyageSupplyEvidenceAvailable || Map == null ||
                Phase == CoreDemoPhase.Port || Phase == CoreDemoPhase.Settlement ||
                Phase == CoreDemoPhase.Battle && Battle?.Profile?.TrainingTarget != null) return;
            if (Cargo.Water <= 0 || Cargo.Food <= 0) AchievementVoyageRanOutOfSupplies = true;
        }

        private void EndAchievementVoyageSupplyObservation()
        {
            ObserveAchievementVoyageSupplies();
            achievementVoyageSuppliesActive = false;
        }

        private void RecordAchievementBattleVictory()
        {
            if (Phase != CoreDemoPhase.Battle || Battle == null || Map == null || Battle.Profile?.TrainingTarget != null ||
                GuidedTutorialLeg != 0 || activeSeaChart?.TutorialWaters == true || IsTreasureVoyage) return;
            if (AchievementVoyageBattleWins < int.MaxValue) AchievementVoyageBattleWins++;
            AchievementWonEditedDeckStages |= Battle.AchievementPlayedDeckStages & achievementEditedDeckStages;
        }

        private static bool ValidAchievementVoyageCheckpoint(CoreVoyageCheckpoint checkpoint)
        {
            if (checkpoint.achievementProgressVersion == 0)
                return checkpoint.achievementEditedDeckStages == 0 && checkpoint.achievementVoyageBattleWins == 0 &&
                    checkpoint.achievementWonEditedDeckStages == 0 && !checkpoint.achievementVoyageRanOutOfSupplies &&
                    !checkpoint.achievementVoyageSupplyEvidenceAvailable;
            return checkpoint.achievementProgressVersion == 1 && checkpoint.achievementEditedDeckStages >= 0 &&
                (checkpoint.achievementEditedDeckStages & ~AchievementDeckStageMask) == 0 &&
                checkpoint.achievementVoyageBattleWins >= 0 && checkpoint.achievementWonEditedDeckStages >= 0 &&
                (checkpoint.achievementWonEditedDeckStages & ~checkpoint.achievementEditedDeckStages) == 0 &&
                (checkpoint.achievementVoyageBattleWins > 0 || checkpoint.achievementWonEditedDeckStages == 0) &&
                (!checkpoint.achievementVoyageRanOutOfSupplies || checkpoint.achievementVoyageSupplyEvidenceAvailable);
        }

        private void RestoreAchievementVoyageProgress(CoreVoyageCheckpoint checkpoint)
        {
            achievementEditedDeckStages = checkpoint.achievementEditedDeckStages;
            AchievementVoyageBattleWins = checkpoint.achievementVoyageBattleWins;
            AchievementWonEditedDeckStages = checkpoint.achievementWonEditedDeckStages;
            AchievementVoyageSupplyEvidenceAvailable = checkpoint.achievementVoyageSupplyEvidenceAvailable;
            AchievementVoyageRanOutOfSupplies = checkpoint.achievementVoyageRanOutOfSupplies ||
                AchievementVoyageSupplyEvidenceAvailable && (Cargo.Water <= 0 || Cargo.Food <= 0 || departureCargo.Water <= 0 || departureCargo.Food <= 0);
            achievementVoyageSuppliesActive = true;
        }

        private void CopyAchievementVoyageProgress(CoreVoyageSession source)
        {
            achievementEditedDeckStages = source.achievementEditedDeckStages;
            AchievementVoyageBattleWins = source.AchievementVoyageBattleWins;
            AchievementWonEditedDeckStages = source.AchievementWonEditedDeckStages;
            AchievementVoyageSupplyEvidenceAvailable = source.AchievementVoyageSupplyEvidenceAvailable;
            AchievementVoyageRanOutOfSupplies = source.AchievementVoyageRanOutOfSupplies;
            achievementVoyageSuppliesActive = source.achievementVoyageSuppliesActive;
        }
    }

    public sealed partial class CoreBattleState
    {
        internal int AchievementPlayedDeckStages { get; private set; }
        private void RecordAchievementCardUse()
        {
            int stage = (int)Stage;
            if (stage >= 0 && stage < 3) AchievementPlayedDeckStages |= 1 << stage;
        }
    }
}
