using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Hand-checked head/torso regions for six distinct tavern compositions.
    /// Coordinates use the 1672 x 941 source image, measured from top-left.
    /// Profession order is stable even when characters change seats or depth.
    /// Separate label anchors keep names away from faces and the page header.
    /// </summary>
    public static class TavernCrewLayoutCatalog
    {
        private static readonly Vector2 SourceSize = new Vector2(1672f, 941f);
        private static readonly Rect[][] Bounds =
        {
            // 断锚港
            new[] { R(125,60,235,170), R(1195,235,205,350), R(175,364,280,305), R(730,120,190,245), R(1405,95,180,185), R(835,390,275,270) },
            // 白贝港
            new[] { R(100,195,305,385), R(1175,100,185,172), R(1275,280,315,310), R(635,180,220,300), R(427,148,146,182), R(872,430,285,330) },
            // 铜钟港
            new[] { R(1204,150,120,220), R(95,225,335,340), R(735,220,160,280), R(1095,378,235,282), R(1345,205,250,400), R(443,168,260,355) },
            // 盐冠港
            new[] { R(655,130,220,255), R(1360,150,240,270), R(135,180,175,255), R(350,365,305,345), R(1000,190,230,270), R(1240,435,240,290) },
            // 暮潮港
            new[] { R(1210,36,225,227), R(709,222,195,265), R(150,391,350,305), R(1140,277,244,235), R(121,146,201,179), R(1430,445,228,355) },
            // 灰鸥港
            new[] { R(1330,180,160,190), R(90,330,425,305), R(820,102,150,180), R(1055,370,335,320), R(110,138,238,185), R(685,290,195,260) },
        };

        private static readonly Vector2[][] NameCenters =
        {
            new[] { new Vector2(260f, 270f), new Vector2(1220f, 205f), new Vector2(285f, 330f), new Vector2(822f, 98f), new Vector2(1505f, 342f), new Vector2(1030f, 355f) },
            new[] { new Vector2(226f, 160f), new Vector2(1450f, 185f), new Vector2(1450f, 640f), new Vector2(760f, 152f), new Vector2(485f, 122f), new Vector2(1040f, 403f) },
            new[] { new Vector2(1255f, 135f), new Vector2(200f, 190f), new Vector2(805f, 196f), new Vector2(1250f, 704f), new Vector2(1475f, 165f), new Vector2(574f, 135f) },
            new[] { new Vector2(795f, 424f), new Vector2(1460f, 120f), new Vector2(235f, 145f), new Vector2(485f, 335f), new Vector2(1130f, 164f), new Vector2(1400f, 774f) },
            new[] { new Vector2(1310f, 235f), new Vector2(810f, 188f), new Vector2(430f, 360f), new Vector2(1210f, 549f), new Vector2(205f, 125f), new Vector2(1525f, 413f) },
            new[] { new Vector2(1420f, 147f), new Vector2(445f, 310f), new Vector2(1035f, 134f), new Vector2(1180f, 340f), new Vector2(226f, 110f), new Vector2(685f, 269f) },
        };

        public static Rect NormalizedBounds(string portName, int profession)
        {
            return Bounds[PortIndex(portName)][Mathf.Clamp(profession, 0, 5)];
        }

        public static Vector2 StageCenter(string portName, int profession)
        {
            Rect rect = NormalizedBounds(portName, profession);
            return PhaseSurfaceLayout.SourceUvToContent(rect.center, SourceSize) -
                new Vector2(0f, PhaseSurfaceLayout.StagePositionY);
        }

        public static Vector2 StageSize(string portName, int profession)
        {
            Rect rect = NormalizedBounds(portName, profession);
            return PhaseSurfaceLayout.SourcePixelSizeToContent(
                new Vector2(rect.width * SourceSize.x, rect.height * SourceSize.y), SourceSize);
        }

        public static Vector2 NameOffset(string portName, int profession)
        {
            Vector2 point = NameCenters[PortIndex(portName)][Mathf.Clamp(profession, 0, 5)];
            return PhaseSurfaceLayout.SourcePixelToContent(point, SourceSize) -
                PhaseSurfaceLayout.SourceUvToContent(NormalizedBounds(portName, profession).center, SourceSize);
        }

        private static int PortIndex(string portName)
        {
            switch (portName)
            {
                case "断锚港": return 0;
                case "白贝港": return 1;
                case "铜钟港": return 2;
                case "盐冠港": return 3;
                case "暮潮港": return 4;
                default: return 5;
            }
        }

        private static Rect R(float x, float y, float width, float height)
        {
            return new Rect(x / SourceSize.x, y / SourceSize.y, width / SourceSize.x, height / SourceSize.y);
        }
    }
}
