using System.Collections.Generic;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Complete silhouette regions of the six remodeled crew atlases.</summary>
    public static class BoardingCrewAtlasLayout
    {
        // Authored pixel bounds retain two transparent edge pixels, boots and
        // props beyond equal-grid cells. Normalization survives import resizing.
        private static readonly Dictionary<string, Rect[]> Regions = new Dictionary<string, Rect[]>
        {
            { "boarding_crew_broken_anchor_v2", new[] { Region(71, 30, 371, 631), Region(487, 46, 772, 632), Region(904, 47, 1188, 632), Region(78, 636, 434, 1213), Region(483, 639, 836, 1210), Region(878, 650, 1221, 1213) } },
            { "boarding_crew_white_shell_v2", new[] { Region(78, 33, 414, 605), Region(479, 23, 781, 607), Region(901, 33, 1207, 605), Region(94, 643, 397, 1204), Region(495, 658, 800, 1200), Region(894, 655, 1200, 1202) } },
            { "boarding_crew_bronze_bell_v2", new[] { Region(74, 36, 370, 626), Region(462, 41, 793, 627), Region(886, 50, 1223, 627), Region(77, 642, 394, 1201), Region(428, 658, 813, 1200), Region(856, 642, 1230, 1200) } },
            { "boarding_crew_salt_crown_v2", new[] { Region(59, 19, 415, 625), Region(538, 11, 779, 625), Region(916, 28, 1175, 624), Region(65, 635, 394, 1214), Region(507, 636, 813, 1216), Region(906, 633, 1227, 1216) } },
            { "boarding_crew_twilight_tide_v2", new[] { Region(40, 26, 408, 612), Region(496, 35, 789, 611), Region(886, 35, 1193, 612), Region(53, 645, 430, 1200), Region(505, 657, 782, 1202), Region(871, 640, 1231, 1201) } },
            { "boarding_crew_gray_gull_v2", new[] { Region(96, 73, 368, 585), Region(501, 57, 776, 585), Region(938, 77, 1171, 586), Region(87, 678, 377, 1202), Region(507, 693, 777, 1201), Region(934, 712, 1206, 1203) } },
        };

        public static Rect ForFigure(Texture texture, int profession)
        {
            int index = Mathf.Clamp(profession, 0, 5);
            if (texture != null && Regions.TryGetValue(texture.name, out Rect[] cells))
                return cells[index];
            const float inset = .01f;
            return new Rect((index % 3 + inset) / 3f, (1 - index / 3 + inset) * .5f,
                (1f - inset * 2f) / 3f, (1f - inset * 2f) * .5f);
        }

        private static Rect Region(int left, int top, int right, int bottom) =>
            new Rect(left / 1254f, (1254 - bottom) / 1254f, (right - left) / 1254f, (bottom - top) / 1254f);
    }
}
