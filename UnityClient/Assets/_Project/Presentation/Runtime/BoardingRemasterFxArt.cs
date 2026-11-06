using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Rows of eight painted deck-space frames shared by the boarding graphics.</summary>
    public static class BoardingRemasterFxArt
    {
        public const string TextureResource = "BattleVfx/BoardingRemaster/boarding_fx_v3";
        public const int Frames = 8, Rows = 12, CellPixels = 256;
        public const int Slash = 0, Dash = 1, Guard = 2, GuardBreak = 3, Heal = 4, Dust = 5;
        public const int Fire = 6, Arcane = 7, Curse = 8, Tide = 9, Thunder = 10, Blood = 11;
        private static Texture2D atlas;

        public static Texture2D Load()
        {
            if (atlas == null) atlas = Resources.Load<Texture2D>(TextureResource);
            return atlas;
        }

        // Live RawImages retain this texture until their owning effect leaves the stage.
        public static void ReleaseLoadedTextures() => atlas = null;

        public static int SchoolRow(BattleSchoolAttackKind school)
        {
            switch (school)
            {
                case BattleSchoolAttackKind.Fire: return Fire;
                case BattleSchoolAttackKind.Arcane: return Arcane;
                case BattleSchoolAttackKind.Curse: return Curse;
                case BattleSchoolAttackKind.Tide: return Tide;
                case BattleSchoolAttackKind.Thunder: return Thunder;
                default: return Blood;
            }
        }

        public static Rect FrameUv(int row, float progress, bool mirror = false)
        {
            if (float.IsNaN(progress) || float.IsInfinity(progress)) progress = 0f;
            int frame = Mathf.Clamp(Mathf.FloorToInt(progress * Frames), 0, Frames - 1);
            row = Mathf.Clamp(row, 0, Rows - 1);
            const float inset = 2f / CellPixels;
            float left = (frame + inset) / Frames, right = (frame + 1f - inset) / Frames;
            return new Rect(mirror ? right : left, (Rows - row - 1f + inset) / Rows,
                mirror ? left - right : right - left, (1f - 2f * inset) / Rows);
        }

        internal static RawImage CreateLayer(RectTransform parent, string name, float pivotY = .5f)
        {
            Texture2D texture = Load();
            if (texture == null) return null;
            var layer = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            var image = layer.GetComponent<RawImage>();
            var rect = image.rectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, pivotY);
            image.texture = texture;
            image.color = Color.clear;
            image.raycastTarget = false;
            return image;
        }

        // Crop both screen and UV rectangles at the edges so effects above the
        // deck cannot paint over controls when the battle viewport is resized.
        internal static void Pose(RawImage image, int row, float p, Vector2 anchor, Vector2 size,
            float alpha, bool mirror = false)
        {
            if (image == null) return;
            RectTransform rect = image.rectTransform;
            RectTransform parent = rect.parent as RectTransform;
            if (parent == null) return;
            Rect bounds = parent.rect;
            Rect uv = FrameUv(row, p, mirror);
            Vector2 min = anchor - Vector2.Scale(rect.pivot, size);
            Vector2 max = min + size;
            Vector2 low = Vector2.Max(bounds.min, min), high = Vector2.Min(bounds.max, max);
            if (size.x <= 0f || size.y <= 0f || low.x >= high.x || low.y >= high.y)
            {
                image.color = Color.clear;
                return;
            }
            Vector2 visible = high - low;
            image.uvRect = new Rect(uv.x + (low.x - min.x) / size.x * uv.width,
                uv.y + (low.y - min.y) / size.y * uv.height,
                visible.x / size.x * uv.width, visible.y / size.y * uv.height);
            rect.sizeDelta = visible;
            rect.anchoredPosition = low + Vector2.Scale(rect.pivot, visible) - bounds.center;
            image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        }
    }
}
