using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Presentation
{
    /// <summary>Resolves a saved Core reward against stable equipment identities.</summary>
    public static class BoardingComponentDropSelection
    {
        public static bool TryGrantNext(CoreVoyageSession session, IReadOnlyList<ComponentArtEntry> entries,
            Func<ComponentArtEntry, bool> grant, out ComponentArtEntry granted)
        {
            granted = null;
            if (session == null || grant == null || !session.TryPeekBoardingComponentDrop(out CoreBoardingComponentDrop drop)) return false;
            ComponentArtEntry selected = Select(drop, entries);
            if (selected == null || !grant(selected)) return false;
            if (!session.AcknowledgeBoardingComponentDrop(drop.Sequence)) return false;
            granted = selected;
            return true;
        }

        public static ComponentArtEntry Select(CoreBoardingComponentDrop drop, IReadOnlyList<ComponentArtEntry> entries)
        {
            if (drop == null || entries == null || drop.Slot < 0 || drop.Slot >= ComponentArtCatalog.SlotCount ||
                !Enum.IsDefined(typeof(CoreBoardingComponentRarity), drop.Rarity)) return null;
            var candidates = new List<ComponentArtEntry>();
            foreach (ComponentArtEntry entry in entries)
            {
                if (entry == null || (int)entry.Slot != drop.Slot || (int)entry.Rarity != (int)drop.Rarity) continue;
                if (drop.IsBossEquipment)
                {
                    if (entry.Id == drop.ComponentId) return entry;
                }
                else if (drop.ComponentId.Length == 0) candidates.Add(entry);
            }
            // Never substitute a different rarity or an unrelated fitting if a catalog
            // entry is unavailable. The controller retains the reward for a later retry.
            if (drop.IsBossEquipment || candidates.Count == 0) return null;
            candidates.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
            return candidates[(int)(drop.SelectionSeed % (uint)candidates.Count)];
        }
    }
}
