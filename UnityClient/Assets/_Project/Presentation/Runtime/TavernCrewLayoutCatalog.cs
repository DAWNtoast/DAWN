using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Hand-checked hit rectangles over the six integrated regional paintings.
    /// Coordinates are measured on the 1672 x 941 composition, from top-left.
    /// Each rectangle follows a person's head/torso, excluding the shared table
    /// and a neighbour's limbs. Keeping them separate from the UI generator
    /// makes future art iterations easy to recalibrate without changing logic.
    /// </summary>
    public static class TavernCrewLayoutCatalog
    {
        private static readonly Rect[][] Bounds =
        {
            new[] { R(70,254,216,476), R(302,278,195,433), R(552,385,169,368), R(736,318,229,239), R(979,315,261,237), R(1264,367,281,383) },
            new[] { R(85,223,233,511), R(336,239,156,425), R(548,292,182,457), R(750,303,157,190), R(969,249,235,236), R(1220,314,206,441) },
            new[] { R(78,239,213,467), R(296,235,164,493), R(491,362,144,427), R(654,315,181,201), R(909,296,260,225), R(1180,325,255,485) },
            new[] { R(86,185,212,552), R(357,196,163,484), R(557,345,160,371), R(728,260,254,273), R(1020,282,244,253), R(1267,381,203,367) },
            new[] { R(90,225,197,506), R(288,248,184,455), R(536,335,139,398), R(685,274,198,210), R(972,291,206,202), R(1188,309,227,475) },
            new[] { R(69,222,217,463), R(287,222,250,484), R(552,321,183,434), R(741,256,233,250), R(1029,285,225,226), R(1262,350,198,409) },
        };

        public static Rect NormalizedBounds(string portName, int profession)
        {
            int port;
            switch (portName)
            {
                case "断锚港": port = 0; break;
                case "白贝港": port = 1; break;
                case "铜钟港": port = 2; break;
                case "盐冠港": port = 3; break;
                case "暮潮港": port = 4; break;
                default: port = 5; break;
            }
            return Bounds[port][Mathf.Clamp(profession, 0, 5)];
        }

        public static Vector2 StageCenter(string portName, int profession)
        {
            Rect rect = NormalizedBounds(portName, profession);
            return PhaseSurfaceLayout.SourceUvToContent(rect.center, new Vector2(1672f, 941f)) -
                new Vector2(0f, PhaseSurfaceLayout.StagePositionY);
        }

        public static Vector2 StageSize(string portName, int profession)
        {
            Rect rect = NormalizedBounds(portName, profession);
            return PhaseSurfaceLayout.SourcePixelSizeToContent(
                new Vector2(rect.width * 1672f, rect.height * 941f), new Vector2(1672f, 941f));
        }

        private static Rect R(float x, float y, float width, float height)
        {
            return new Rect(x / 1672f, y / 941f, width / 1672f, height / 941f);
        }
    }
}
