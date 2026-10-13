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
        private readonly Func<string, Texture2D> resourceLoader;

        public PortHeaderArtCatalog() : this(Resources.Load<Texture2D>) { }

        internal PortHeaderArtCatalog(Func<string, Texture2D> resourceLoader)
        {
            this.resourceLoader = resourceLoader ?? throw new ArgumentNullException(nameof(resourceLoader));
        }

        public PortHeaderTheme ForPort(string portName, Sprite suppliedFrame = null)
        {
            // Authored sheets have different blank margins. Keep the exact crop
            // and nine-slice metadata, but create only the requested frame.
            switch (portName)
            {
                case "断锚港": return Get("断锚港", "broken_anchor", "#E7B17C", new Rect(0f, 836f / 1214f, 1f, 378f / 1214f), suppliedFrame);
                case "白贝港": return Get("白贝港", "white_shell", "#F2DEB5", new Rect(0f, 0f, 1f, 1f), suppliedFrame);
                case "铜钟港": return Get("铜钟港", "bronze_bell", "#EFCA80", new Rect(0f, 335f / 859f, 1f, 524f / 859f), suppliedFrame);
                case "盐冠港": return Get("盐冠港", "salt_crown", "#F6D586", new Rect(0f, 144f / 859f, 1f, 617f / 859f), suppliedFrame);
                case "暮潮港": return Get("暮潮港", "twilight_tide", "#D7C7F4", new Rect(0f, 191f / 859f, 1f, 496f / 859f), suppliedFrame);
                default: return Get("灰鸥港", "gray_gull", "#EBD0A0", new Rect(0f, 826f / 1214f, 1f, 388f / 1214f), suppliedFrame);
            }
        }

        private PortHeaderTheme Get(string port, string slug, string accentHex, Rect normalizedRect, Sprite suppliedFrame)
        {
            ColorUtility.TryParseHtmlString(accentHex, out Color accent);
            // Navigation already has its own painted frame. Reuse that actual
            // sprite and the regional accent without loading an unused atlas.
            // A missing supplied frame follows the original regional fallback.
            if (suppliedFrame != null) return new PortHeaderTheme(suppliedFrame, suppliedFrame.rect.height, accent);
            if (themes.TryGetValue(port, out PortHeaderTheme cached)) return cached;
            Texture2D texture = resourceLoader("PortHeaders/" + slug + "_header_v1");
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
            var theme = new PortHeaderTheme(frame, height, accent);
            themes.Add(port, theme);
            return theme;
        }

        /// <summary>Retains only the current port's frame without destroying any live UI's borrowed sprite.</summary>
        public void ReleaseUnusedThemes(string retainedPort)
        {
            if (themes.Count == 0) return;
            var released = new List<string>();
            foreach (var pair in themes)
            {
                if (pair.Key == retainedPort) continue;
                // HideAndDontSave otherwise pins the generated sprite and its
                // large source texture even after this dictionary forgets it.
                // Live Images remain authoritative owners during the next scan.
                if (pair.Value.Frame != null)
                    pair.Value.Frame.hideFlags &= ~HideFlags.DontUnloadUnusedAsset;
                released.Add(pair.Key);
            }
            foreach (string port in released) themes.Remove(port);
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
