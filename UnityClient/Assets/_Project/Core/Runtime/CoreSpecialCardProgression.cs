using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        private readonly Dictionary<string, int> specialCardExperience = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> acquiredSpecialCards = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> activeSpecialCrewIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> activeSpecialComponentIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> voyageSpecialCards = new HashSet<string>(StringComparer.Ordinal);
        private bool explicitSpecialCardSources;
        private double specialCardDistanceRemainder;

        // All locked source cards earn full distance XP; there is no separate per-voyage rank cap.
        // Four nautical miles of real sailing contribute roughly one progression point.
        public const double SpecialCardExperiencePerNauticalMile = 0.25d;

        public IReadOnlyDictionary<string, int> SpecialCardExperience => new ReadOnlyDictionary<string, int>(specialCardExperience);
        public IReadOnlyList<string> AcquiredSpecialCardIds
        {
            get
            {
                List<string> ordered = new List<string>(acquiredSpecialCards);
                ordered.Sort(StringComparer.Ordinal);
                return ordered.AsReadOnly();
            }
        }

        public int GetSpecialCardExperience(string cardId) => cardId != null && specialCardExperience.TryGetValue(cardId, out int xp) ? xp : 0;

        // Thresholds are cumulative, not the remaining amount. Zero means fully trained (or not a special card).
        public int GetSpecialCardNextThreshold(string cardId)
        {
            if (!IsSpecialBattleCard(cardId)) return 0;
            int level = SpecialCardLevelForExperience(GetSpecialCardExperience(cardId));
            return level >= 3 ? 0 : SpecialCardExperienceForLevel(level + 1);
        }

        public static bool IsSpecialBattleCard(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return false;
            foreach (CoreBattleCard card in cardLibrary)
                if (card.Id == cardId) return card.RequiredCrew.Length > 0 || card.RequiredComponent.Length > 0;
            return false;
        }

        private static int SpecialCardExperienceForLevel(int level) => level == 1 ? 120 : level == 2 ? 360 : level >= 3 ? 840 : 0;
        private static int SpecialCardLevelForExperience(int xp) => xp >= 840 ? 3 : xp >= 360 ? 2 : xp >= 120 ? 1 : 0;

        // Restore only raises known progress. This also makes migration from old paid upgrades idempotent,
        // regardless of whether the XP or old upgrade record is restored first.
        public void RestoreSpecialCardExperience(IReadOnlyDictionary<string, int> experience)
        {
            if (Phase != CoreDemoPhase.Port || experience == null) return;
            List<KeyValuePair<string, int>> copy = new List<KeyValuePair<string, int>>(experience);
            foreach (KeyValuePair<string, int> pair in copy)
                if (pair.Value > 0) RestoreSpecialCardMinimumExperience(pair.Key, pair.Value);
            MigrateAcquiredSpecialCardExperience();
        }

        private void RestoreSpecialCardMinimumExperience(string cardId, int minimumExperience)
        {
            if (!IsSpecialBattleCard(cardId)) return;
            acquiredSpecialCards.Add(cardId);
            specialCardExperience[cardId] = Math.Max(GetSpecialCardExperience(cardId), Math.Max(0, minimumExperience));
        }

        public void RestoreAcquiredSpecialCards(IReadOnlyList<string> cardIds) => AcquireSpecialCards(cardIds);

        public void AcquireSpecialCards(IReadOnlyList<string> cardIds)
        {
            if (Phase != CoreDemoPhase.Port || cardIds == null) return;
            foreach (string cardId in cardIds)
                if (IsSpecialBattleCard(cardId))
                {
                    acquiredSpecialCards.Add(cardId);
                    MigrateLegacySpecialCardExperience(CoreSpecialCardCatalog.FindByCardId(cardId));
                }
        }

        /// <summary>
        /// Runtime calls this with actual crew identities and equipped part identities. It supersedes the
        /// legacy profession/type capability APIs, which remain available for old offline simulations.
        /// The catalog enforces which crew and rare-or-better components really provide cards.
        /// </summary>
        public void ConfigureSpecialCardSources(IReadOnlyList<string> crewIds, IReadOnlyList<string> componentIds)
        {
            if (Phase != CoreDemoPhase.Port) return;
            activeSpecialCrewIds.Clear();
            activeSpecialComponentIds.Clear();
            explicitSpecialCardSources = true;
            if (crewIds != null)
                foreach (string crewId in crewIds)
                {
                    CoreSpecialCardDefinition definition = CoreSpecialCardCatalog.ForCrew(crewId);
                    if (definition == null || definition.SourceKind != CoreSpecialCardSourceKind.Crew) continue;
                    activeSpecialCrewIds.Add(definition.SourceId);
                    acquiredSpecialCards.Add(definition.Card.Id);
                    MigrateLegacySpecialCardExperience(definition);
                }
            if (componentIds != null)
                foreach (string componentId in componentIds)
                {
                    CoreSpecialCardDefinition definition = CoreSpecialCardCatalog.ForComponent(componentId);
                    if (definition == null || definition.SourceKind != CoreSpecialCardSourceKind.Component) continue;
                    activeSpecialComponentIds.Add(definition.SourceId);
                    acquiredSpecialCards.Add(definition.Card.Id);
                    MigrateLegacySpecialCardExperience(definition);
                }
        }

        private void MigrateAcquiredSpecialCardExperience()
        {
            // Snapshot: applying a historical lower bound also marks that card acquired.
            foreach (string cardId in new List<string>(acquiredSpecialCards))
                MigrateLegacySpecialCardExperience(CoreSpecialCardCatalog.FindByCardId(cardId));
        }

        private void MigrateLegacySpecialCardExperience(CoreSpecialCardDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.LegacySourceKey)) return;
            int legacyExperience = 0;
            // Only the original profession/type cards are transferable historical progress. Never copy
            // experience from another newly recruited identity, even if they share a profession.
            for (int index = 0; index < Math.Min(LegacyBattleCardSlotCount, cardLibrary.Count); index++)
            {
                CoreBattleCard old = cardLibrary[index];
                string source = definition.SourceKind == CoreSpecialCardSourceKind.Crew ? old.RequiredCrew : old.RequiredComponent;
                if (source == definition.LegacySourceKey) legacyExperience = Math.Max(legacyExperience, GetSpecialCardExperience(old.Id));
            }
            if (legacyExperience > 0) RestoreSpecialCardMinimumExperience(definition.Card.Id, legacyExperience);
        }

        private void LockSpecialCardsForVoyage()
        {
            voyageSpecialCards.Clear();
            specialCardDistanceRemainder = 0d;
            for (int phase = 0; phase < 3; phase++)
                foreach (int index in GetBonusBattleCards((NavalBattleStage)phase))
                {
                    string cardId = cardLibrary[index].Id;
                    voyageSpecialCards.Add(cardId);
                    acquiredSpecialCards.Add(cardId);
                }
        }

        private void AwardSpecialCardVoyageDistance(float nauticalMiles)
        {
            if (Phase != CoreDemoPhase.Sailing || voyageSpecialCards.Count == 0 || nauticalMiles <= 0f ||
                float.IsNaN(nauticalMiles) || float.IsInfinity(nauticalMiles)) return;
            // Accumulate fractional progression points once per sailing leg, shared by all locked source cards.
            // Never use wall-clock time, requested distance, log count, or event time as an experience source.
            double progress = specialCardDistanceRemainder + nauticalMiles * SpecialCardExperiencePerNauticalMile;
            double wholeExperience = Math.Floor(progress);
            specialCardDistanceRemainder = progress - wholeExperience;
            if (wholeExperience < 1d) return;
            int delta = wholeExperience >= int.MaxValue ? int.MaxValue : (int)wholeExperience;
            foreach (string cardId in voyageSpecialCards)
            {
                int previous = GetSpecialCardExperience(cardId);
                int next = (int)Math.Min(int.MaxValue, (long)previous + delta);
                specialCardExperience[cardId] = next;
                int previousLevel = SpecialCardLevelForExperience(previous);
                int nextLevel = SpecialCardLevelForExperience(next);
                if (nextLevel > previousLevel)
                {
                    string name = cardId;
                    foreach (CoreBattleCard card in cardLibrary)
                        if (card.Id == cardId) { name = card.Name; break; }
                    AddLog("航行历练：「" + name + "」达到 " + nextLevel + " 阶（" + next + " 经验）。");
                }
            }
        }
    }
}
