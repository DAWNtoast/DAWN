using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Embedded display and reading faces for menus, gameplay and previews.
    /// This is a Resources asset: callers borrow it and must never destroy it.
    /// </summary>
    public static class GameTypography
    {
        public const string FontResourcePath = "Fonts/XiangcuiKesong";
        public const string BodyFontResourcePath = "Fonts/HarborBody-Regular";
        private static Font interfaceFont;
        private static Font bodyFont;

        public static Font BodyFont
        {
            get
            {
                if (bodyFont == null) bodyFont = Resources.Load<Font>(BodyFontResourcePath);
                return bodyFont != null ? bodyFont : InterfaceFont;
            }
        }

        public static Font InterfaceFont
        {
            get
            {
                if (interfaceFont == null)
                {
                    interfaceFont = Resources.Load<Font>(FontResourcePath);
                    if (interfaceFont == null)
                    {
                        Debug.LogError("Missing embedded game font: Resources/" + FontResourcePath);
                        interfaceFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    }
                }
                return interfaceFont;
            }
        }

        public static void Apply(Text text, int fontSize, FontStyle requestedStyle)
        {
            // Keep carved lettering for prominent headings; small controls, numbers
            // and reading paragraphs use the embedded regular Chinese face.
            text.font = requestedStyle == FontStyle.Bold && fontSize >= 22 ? InterfaceFont : BodyFont;
            text.fontSize = fontSize;
            // The authored face already has sharp, irregular woodcut strokes.
            // Fake bold/italic closes counters and blunts the pointed serifs,
            // especially in small descriptions. Keep its real native weight.
            text.fontStyle = FontStyle.Normal;
            text.lineSpacing = 1f;
        }
    }
}
