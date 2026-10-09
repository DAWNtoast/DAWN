using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Shared design tokens. Keep each existing surface's palette while its layout migrates.</summary>
    public static class UiTheme
    {
        public static readonly Palette Gameplay = new Palette(
            "#E8E1C7", "#9DB0AE", "#071820", "#0F2932", "#173A43",
            "#287E79", "#D8A541", "#CE6750", "#5EAE78", "#061218");
        public static readonly Palette Menu = new Palette(
            "#F1E7CC", "#A9B8B5", "#06151C", "#0B222B", "#123640",
            "#2B8880", "#D9A83E", "#D16B52", "#5EAE78", "#061218");
        public static readonly Palette Tutorial = new Palette(
            "#E8E1C7", "#A9B3B0", "#081A24", "#102B34", "#173943",
            "#2A7C78", "#D9A441", "#CF694F", "#5FAE78", "#061218");

        public static readonly Color PaperInk = Parse("#2B1B11");
        public static readonly Color PaperMutedInk = Parse("#46321F");
        public static readonly Color PaperAccent = Parse("#203D2D");
        public static readonly Color DisabledControl = new Color(0.2f, 0.26f, 0.27f, 0.75f);
        public static readonly Vector2 ButtonLabelInset = new Vector2(24f, 10f);
        public const float CompactSpacing = 8f;
        public const float Spacing = 16f;
        public const float Gutter = 24f;
        public const float BorderWidth = 1f;
        public const float HoverBlend = 0.16f;
        public const float PressBlend = 0.18f;

        public readonly struct Palette
        {
            public readonly Color Ink, MutedInk, DeepSea, Panel, PanelRaised, Teal, Gold, Coral, Green, Fog;

            internal Palette(string ink, string mutedInk, string deepSea, string panel, string panelRaised,
                string teal, string gold, string coral, string green, string fog)
            {
                Ink = Parse(ink); MutedInk = Parse(mutedInk); DeepSea = Parse(deepSea);
                Panel = Parse(panel); PanelRaised = Parse(panelRaised); Teal = Parse(teal);
                Gold = Parse(gold); Coral = Parse(coral); Green = Parse(green); Fog = Parse(fog);
            }
        }

        private static Color Parse(string html)
        {
            return ColorUtility.TryParseHtmlString(html, out Color color) ? color : Color.magenta;
        }
    }
}
