using System;
using System.Collections.Generic;

namespace ShatteredSail.Presentation
{
    /// <summary>Historical memories unlocked by reaching a port; the port is not the memory's location.</summary>
    public sealed class ArrivalComicStory
    {
        internal ArrivalComicStory(string id, string portName)
        {
            Id = id;
            PortName = portName;
            Pages = Array.AsReadOnly(new[] {
                "ArrivalComics/" + id + "_page01",
                "ArrivalComics/" + id + "_page02"
            });
        }

        public string Id { get; }
        public string PortName { get; }
        public IReadOnlyList<string> Pages { get; }
    }

    /// <summary>Stable five-port mapping. Page art contains four wordless panels and no runtime lettering.</summary>
    public static class ArrivalComicCatalog
    {
        private static readonly IReadOnlyList<ArrivalComicStory> stories = Array.AsReadOnly(new[] {
            new ArrivalComicStory("white_shell", "白贝港"),
            new ArrivalComicStory("bronze_bell", "铜钟港"),
            new ArrivalComicStory("salt_crown", "盐冠港"),
            new ArrivalComicStory("twilight_tide", "暮潮港"),
            new ArrivalComicStory("gray_gull", "灰鸥港")
        });

        public static IReadOnlyList<ArrivalComicStory> All => stories;

        public static ArrivalComicStory Find(string portName)
        {
            for (int index = 0; index < stories.Count; index++)
                if (string.Equals(stories[index].PortName, portName, StringComparison.Ordinal)) return stories[index];
            return null;
        }

        public static ArrivalComicStory FindById(string id)
        {
            for (int index = 0; index < stories.Count; index++)
                if (string.Equals(stories[index].Id, id, StringComparison.Ordinal)) return stories[index];
            return null;
        }
    }
}
