using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Matches the independent vessel to its port without changing shared ship art.</summary>
    public static class DepartureDockShipLighting
    {
        public static void Apply(RawImage hull, string port)
        {
            if (hull != null) hull.color = TintForPort(port);
        }

        public static Color TintForPort(string port)
        {
            // Assign an absolute tint so repeated layout and vessel switches never
            // compound darkness. Only the vessel image receives this treatment.
            switch (port)
            {
                case "暮潮港": return new Color(.70f, .74f, .87f, 1f);
                case "灰鸥港": return new Color(.86f, .89f, .93f, 1f);
                case "铜钟港": return new Color(1f, .94f, .84f, 1f);
                case "盐冠港": return new Color(1f, .97f, .90f, 1f);
                case "白贝港": return new Color(1f, .99f, .96f, 1f);
                default: return new Color(1f, .97f, .92f, 1f);
            }
        }
    }
}
