using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Readable UI materials that accompany each independently painted collection
    /// room. This catalog never loads textures; PortFacilityArtCatalog owns the
    /// lazy room-background cache and its existing release lifecycle.
    /// </summary>
    public static class WarehouseRoomArtCatalog
    {
        private static readonly WarehouseRoomTheme BrokenAnchor = Theme("#102D30", "#F2E5CB", "#ACAFA0", "#D4AC62");
        private static readonly WarehouseRoomTheme WhiteShell = Theme("#153B49", "#F4EDDB", "#ACCBCA", "#DFD2A3");
        private static readonly WarehouseRoomTheme BronzeBell = Theme("#302820", "#F5E7D4", "#C5AE92", "#DDAD79");
        private static readonly WarehouseRoomTheme SaltCrown = Theme("#38212A", "#F8E8CE", "#C5ABA7", "#E7C478");
        private static readonly WarehouseRoomTheme TwilightTide = Theme("#25283F", "#EDE6F7", "#B3B1CE", "#C8C7E2");
        private static readonly WarehouseRoomTheme GrayGull = Theme("#26343B", "#E5E6DC", "#AABAC0", "#B8C5C7");

        public static WarehouseRoomTheme ForPort(string portName)
        {
            switch (portName)
            {
                case "断锚港": return BrokenAnchor;
                case "白贝港": return WhiteShell;
                case "铜钟港": return BronzeBell;
                case "盐冠港": return SaltCrown;
                case "暮潮港": return TwilightTide;
                default: return GrayGull;
            }
        }

        private static WarehouseRoomTheme Theme(string lining, string ink, string muted, string brass)
        {
            ColorUtility.TryParseHtmlString(lining, out Color liningColor);
            ColorUtility.TryParseHtmlString(ink, out Color inkColor);
            ColorUtility.TryParseHtmlString(muted, out Color mutedColor);
            ColorUtility.TryParseHtmlString(brass, out Color brassColor);
            return new WarehouseRoomTheme(liningColor, inkColor, mutedColor, brassColor);
        }
    }

    public sealed class WarehouseRoomTheme
    {
        public WarehouseRoomTheme(Color lining, Color ink, Color muted, Color brass)
        {
            Lining = lining;
            Ink = ink;
            Muted = muted;
            Brass = brass;
        }

        public Color Lining { get; }
        public Color Ink { get; }
        public Color Muted { get; }
        public Color Brass { get; }
    }
}
