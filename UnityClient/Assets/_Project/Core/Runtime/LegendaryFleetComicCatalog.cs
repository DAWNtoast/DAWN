using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>A single wordless page; all four visual panels are part of its image.</summary>
    public sealed class LegendaryFleetComicPage
    {
        internal LegendaryFleetComicPage(string imageResource)
        {
            ImageResource = imageResource;
        }

        public string ImageResource { get; }
    }

    /// <summary>Historical memories shown before a legendary encounter and after victory.</summary>
    public sealed class LegendaryFleetComicStory
    {
        internal LegendaryFleetComicStory(string key, string shipName)
        {
            EncounterId = "legendary_pirate_" + key;
            ShipName = shipName;
            Before = new LegendaryFleetComicPage("LegendaryFleetComics/" + key + "_before");
            After = new LegendaryFleetComicPage("LegendaryFleetComics/" + key + "_after");
        }

        public string EncounterId { get; }
        public string ShipName { get; }
        public LegendaryFleetComicPage Before { get; }
        public LegendaryFleetComicPage After { get; }
    }

    /// <summary>
    /// Stable six-ship identity and resource mapping for the wordless fleet comics.
    /// It contains no captions, narration or Unity dependency. Treasure map rank and
    /// map content version do not select a legendary ship's personal memories.
    /// </summary>
    public static class LegendaryFleetComicCatalog
    {
        public const int ContentVersion = 1;

        private static readonly IReadOnlyList<LegendaryFleetComicStory> stories =
            Array.AsReadOnly(new[]
            {
                new LegendaryFleetComicStory("iron_oath", "铁誓不沉号"),
                new LegendaryFleetComicStory("red_widow", "绯刃寡妇号"),
                new LegendaryFleetComicStory("thunder_crown", "雷冠审判号"),
                new LegendaryFleetComicStory("ember_ark", "余烬方舟号"),
                new LegendaryFleetComicStory("tide_requiem", "潮汐镇魂号"),
                new LegendaryFleetComicStory("last_horizon", "终界观星号")
            });

        public static IReadOnlyList<LegendaryFleetComicStory> All => stories;

        public static LegendaryFleetComicStory Find(string encounterId)
        {
            for (int index = 0; index < stories.Count; index++)
                if (string.Equals(stories[index].EncounterId, encounterId, StringComparison.Ordinal))
                    return stories[index];
            return null;
        }
    }
}
