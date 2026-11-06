using System;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        public const int CurrentSplitSupportCardVersion = 1;
        private bool splitSupportCardUpgradesRestored;

        // These cards formerly borrowed the ship card's identity in boarding.
        // Keep both historical library slots, but use the boarding slot in old decks.
        private static int RestoredBattleCardIndex(int index, NavalBattleStage stage)
        {
            index = CanonicalBattleCardIndex(index);
            if (stage != NavalBattleStage.Boarding || index < 0 || index >= cardLibrary.Count) return index;
            string id = cardLibrary[index].Id == "card_energy_order" ? "card_boarding_basic_09" :
                cardLibrary[index].Id == "card_tactical_draw" ? "card_boarding_basic_08" : null;
            if (id == null) return index;
            for (int i = 0; i < cardLibrary.Count; i++) if (cardLibrary[i].Id == id) return i;
            throw new InvalidOperationException("Missing split boarding card: " + id);
        }

        /// <summary>Preserve previously shared investment once, before either card can be upgraded independently.</summary>
        public void RestoreSplitSupportCardUpgrades(int savedVersion)
        {
            if (Phase != CoreDemoPhase.Port || splitSupportCardUpgradesRestored) return;
            splitSupportCardUpgradesRestored = true;
            if (savedVersion >= CurrentSplitSupportCardVersion) return;
            CopyUpgrade("card_energy_order", "card_boarding_basic_09");
            CopyUpgrade("card_tactical_draw", "card_boarding_basic_08");
        }

        private void CopyUpgrade(string oldId, string boardingId)
        {
            if (!cardUpgradeLevels.TryGetValue(oldId, out int oldLevel) || oldLevel <= 0) return;
            cardUpgradeLevels.TryGetValue(boardingId, out int boardingLevel);
            cardUpgradeLevels[boardingId] = Math.Max(boardingLevel, oldLevel);
        }
    }
}
