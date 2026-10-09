using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Six independently painted materials. Nine-slicing retains the
    /// proportions of shell/chain/crown end-caps at every canvas resolution.</summary>
    public sealed class PortHeaderArtCatalog : IDisposable
    {
        private readonly Dictionary<string, PortHeaderTheme> themes = new Dictionary<string, PortHeaderTheme>(StringComparer.Ordinal);

        public PortHeaderArtCatalog()
        {
            // Authored sheets have different blank margins. Select each frame
            // in sprite coordinates without modifying or recolouring source pixels.
            Add("断锚港", "broken_anchor", "#E7B17C", new Rect(0f, 836f / 1214f, 1f, 378f / 1214f));
            Add("白贝港", "white_shell", "#F2DEB5", new Rect(0f, 0f, 1f, 1f));
            Add("铜钟港", "bronze_bell", "#EFCA80", new Rect(0f, 335f / 859f, 1f, 524f / 859f));
            Add("盐冠港", "salt_crown", "#F6D586", new Rect(0f, 144f / 859f, 1f, 617f / 859f));
            Add("暮潮港", "twilight_tide", "#D7C7F4", new Rect(0f, 191f / 859f, 1f, 496f / 859f));
            Add("灰鸥港", "gray_gull", "#EBD0A0", new Rect(0f, 826f / 1214f, 1f, 388f / 1214f));
        }

        public PortHeaderTheme ForPort(string portName)
        {
            return portName != null && themes.TryGetValue(portName, out PortHeaderTheme theme) ? theme : themes["灰鸥港"];
        }

        private void Add(string port, string slug, string accentHex, Rect normalizedRect)
        {
            Texture2D texture = Resources.Load<Texture2D>("PortHeaders/" + slug + "_header_v1");
            ColorUtility.TryParseHtmlString(accentHex, out Color accent);
            Sprite frame = null;
            float height = 148f;
            if (texture != null)
            {
                Rect rect = new Rect(normalizedRect.x * texture.width, normalizedRect.y * texture.height,
                    normalizedRect.width * texture.width, normalizedRect.height * texture.height);
                height = rect.height;
                Vector4 border = new Vector4(rect.width * 0.13f, height * 0.29f, rect.width * 0.13f, height * 0.29f);
                frame = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
                frame.name = slug + " Header Frame";
                frame.hideFlags = HideFlags.HideAndDontSave;
            }
            themes.Add(port, new PortHeaderTheme(frame, height, accent));
        }

        public void Dispose()
        {
            foreach (PortHeaderTheme theme in themes.Values)
            {
                if (theme.Frame == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(theme.Frame);
                else UnityEngine.Object.DestroyImmediate(theme.Frame);
            }
            themes.Clear();
        }
    }

    public sealed class PortHeaderTheme
    {
        public PortHeaderTheme(Sprite frame, float frameHeight, Color accent)
        {
            Frame = frame;
            FrameHeight = frameHeight;
            Accent = accent;
        }
        public Sprite Frame { get; }
        public float FrameHeight { get; }
        public Color Accent { get; }
    }
}
