using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Fits the sampled pixels of a RawImage inside an authored display box.</summary>
    public static class UiImageAspect
    {
        public static Vector2 FitSize(Texture texture, Rect uv, Vector2 bounds)
        {
            return FitSize(texture == null ? Vector2.zero : new Vector2(texture.width, texture.height), uv, bounds);
        }

        public static Vector2 FitSize(Vector2 sourcePixels, Rect uv, Vector2 bounds)
        {
            bounds = new Vector2(PositiveFinite(bounds.x), PositiveFinite(bounds.y));
            float width = PositiveFinite(Mathf.Abs(sourcePixels.x * uv.width));
            float height = PositiveFinite(Mathf.Abs(sourcePixels.y * uv.height));
            // Keep a missing/loading image's placeholder intact. Mirrored UVs
            // use their absolute extent, without changing the image's facing.
            if (width == 0f || height == 0f) return bounds;
            float scale = Mathf.Min(bounds.x / width, bounds.y / height);
            return new Vector2(width * scale, height * scale);
        }

        public static void Fit(RawImage image, Vector2 bounds)
        {
            if (image == null) return;
            Vector2 size = FitSize(image.texture, image.uvRect, bounds);
            image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        }

        public static Rect InsetUv(Texture texture, Rect uv, float pixels)
        {
            if (texture == null || pixels <= 0f || float.IsNaN(pixels) || float.IsInfinity(pixels)) return uv;
            float x = Mathf.Min(pixels / texture.width, Mathf.Abs(uv.width) * 0.49f) * Mathf.Sign(uv.width);
            float y = Mathf.Min(pixels / texture.height, Mathf.Abs(uv.height) * 0.49f) * Mathf.Sign(uv.height);
            return new Rect(uv.x + x, uv.y + y, uv.width - 2f * x, uv.height - 2f * y);
        }

        private static float PositiveFinite(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
    }
}
