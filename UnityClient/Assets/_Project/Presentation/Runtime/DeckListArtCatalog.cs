using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Authored transparent deck-list art; only UVs are trimmed, the source PNGs remain intact.</summary>
    public static class DeckListArtCatalog
    {
        public const string ResourceRoot = "CardWorkshopV2/DeckList/";
        [Serializable] public sealed class Layout
        {
            public string id;
            public float x, y, width, height, left, right, top, bottom, energyX, energyY;
            public Rect Uv => new Rect(x, y, width, height);
        }
        [Serializable] private sealed class LayoutFile { public Layout[] entries; }
        private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Layout> layouts = new Dictionary<string, Layout>();
        private static Sprite thumb;
        private static bool loaded;

        public static void ReleaseLoadedTextures()
        {
            // Live UI retains borrowed textures and the generated sprite during the unused-asset scan.
            textures.Clear();
            thumb = null;
        }

        public static Texture2D Texture(string id)
        {
            if (!textures.TryGetValue(id, out Texture2D value) || value == null)
            {
                value = Resources.Load<Texture2D>(ResourceRoot + id);
                textures[id] = value;
            }
            return value;
        }

        public static Layout Bounds(string id)
        {
            if (!loaded)
            {
                loaded = true;
                TextAsset json = Resources.Load<TextAsset>(ResourceRoot + "layouts");
                LayoutFile file = json == null ? null : JsonUtility.FromJson<LayoutFile>(json.text);
                if (file?.entries != null) foreach (Layout entry in file.entries) layouts[entry.id] = entry;
            }
            return layouts.TryGetValue(id, out Layout value) ? value : null;
        }

        public static Sprite Thumb
        {
            get
            {
                if (thumb != null) return thumb;
                Texture2D texture = Texture("thumb");
                Layout bounds = Bounds("thumb");
                if (texture == null || bounds == null) return null;
                Rect uv = bounds.Uv;
                thumb = Sprite.Create(texture, new Rect(uv.x * texture.width, uv.y * texture.height,
                    uv.width * texture.width, uv.height * texture.height), new Vector2(.5f, .5f), 100f,
                    0, SpriteMeshType.FullRect);
                thumb.name = "Deck List Diamond Scroll Thumb";
                return thumb;
            }
        }

        public static float MapRowX(float source, Layout layout, float width)
        {
            const float left = 54f, right = 42f;
            if (source <= layout.left) return source / Mathf.Max(.001f, layout.left) * left;
            if (source >= 1f - layout.right)
                return width - right + (source - 1f + layout.right) / Mathf.Max(.001f, layout.right) * right;
            return left + (source - layout.left) / (1f - layout.left - layout.right) * (width - left - right);
        }
    }
}
