using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Retired duplicate identities remain readable without occupying the playable collection.</summary>
    public static class CoreBattleCardAliases
    {
        public static string CanonicalId(string id)
        {
            switch (id)
            {
                case "card_parallel_course":
                case "card_turn_port":
                case "card_turn_starboard":
                case "card_turn_about":
                case "card_sail_forward": return "card_approach";
                case "card_circle_enemy": return "card_retreat";
                default: return id;
            }
        }

        public static bool IsAlias(string id) => !string.Equals(id, CanonicalId(id), StringComparison.Ordinal);
    }

    public sealed partial class CoreVoyageSession
    {
        private static class CanonicalCardIndexes
        {
            internal static readonly int[] Values = Build();

            private static int[] Build()
            {
                var byId = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int i = 0; i < cardLibrary.Count; i++) byId.Add(cardLibrary[i].Id, i);
                var result = new int[cardLibrary.Count];
                for (int i = 0; i < result.Length; i++)
                {
                    string id = CoreBattleCardAliases.CanonicalId(cardLibrary[i].Id);
                    if (!byId.TryGetValue(id, out result[i]))
                        throw new InvalidOperationException("Missing canonical battle card: " + id);
                }
                return result;
            }
        }

        public static int CanonicalBattleCardIndex(int index) => index < 0 || index >= cardLibrary.Count
            ? index : CanonicalCardIndexes.Values[index];

        // Saved decks use numeric indices. Empty compatibility slots preserve those
        // indices after deleting the duplicate designs; they cannot enter any deck.
        private static CoreBattleCard RetiredBattleCardSlot(string id) =>
            new CoreBattleCard(id, string.Empty, string.Empty, 0,
                0, 0, 0, 0, 0, string.Empty, NavalBattleStages.None);

        private static List<int> CanonicalBattleDeck(IReadOnlyList<int> source)
        {
            var result = new List<int>(source.Count);
            foreach (int index in source) result.Add(CanonicalBattleCardIndex(index));
            return result;
        }

        private static int[] CanonicalStarterBattleDeck(int[] source, NavalBattleStage stage)
        {
            var result = new List<int>();
            var copies = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (int savedIndex in source)
            {
                int index = CanonicalBattleCardIndex(savedIndex);
                string identity = BattleDeckCopyIdentity(index, stage);
                copies.TryGetValue(identity, out int count);
                if (count >= BaseBattleCardCopies) continue;
                result.Add(index);
                copies[identity] = count + 1;
            }
            return result.ToArray();
        }
    }
}
