using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>
    /// Deleted profession/type cards keep only save-migration metadata. Their library
    /// positions are inert slots, and their published numbers are never reassigned.
    /// </summary>
    public static class CoreRetiredSourceCards
    {
        private sealed class Source
        {
            internal readonly CoreSpecialCardSourceKind Kind;
            internal readonly string Key;
            internal readonly int Number;

            internal Source(CoreSpecialCardSourceKind kind, string key, int number)
            {
                Kind = kind;
                Key = key;
                Number = number;
            }
        }

        private static readonly Dictionary<string, Source> sources = new Dictionary<string, Source>(StringComparer.Ordinal)
        {
            { "card_navigator_course", new Source(CoreSpecialCardSourceKind.Crew, "navigator", 8002) },
            { "card_carpenter_shoring", new Source(CoreSpecialCardSourceKind.Crew, "carpenter", 8011) },
            { "card_doctor_sutures", new Source(CoreSpecialCardSourceKind.Crew, "doctor", 8024) },
            { "card_gunner_precision", new Source(CoreSpecialCardSourceKind.Crew, "gunner", 8001) },
            { "card_cook_rations", new Source(CoreSpecialCardSourceKind.Crew, "cook", 8007) },
            { "card_diver_sabotage", new Source(CoreSpecialCardSourceKind.Crew, "diver", 8017) },
            { "card_component_bow", new Source(CoreSpecialCardSourceKind.Component, "bow", 9014) },
            { "card_component_sail", new Source(CoreSpecialCardSourceKind.Component, "sail", 9002) },
            { "card_component_cannon", new Source(CoreSpecialCardSourceKind.Component, "cannon", 9015) },
            { "card_component_plating", new Source(CoreSpecialCardSourceKind.Component, "plating", 9022) },
            { "card_component_instrument", new Source(CoreSpecialCardSourceKind.Component, "instrument", 9001) }
        };

        public static bool IsRetired(string cardId) => cardId != null && sources.ContainsKey(cardId);

        internal static bool MatchesLegacySource(string cardId, CoreSpecialCardDefinition definition) =>
            definition != null && cardId != null && sources.TryGetValue(cardId, out Source source) &&
            source.Kind == definition.SourceKind && source.Key == definition.LegacySourceKey;

        internal static bool ReservesNumber(int number)
        {
            foreach (Source source in sources.Values)
                if (source.Number == number) return true;
            return false;
        }
    }
}
