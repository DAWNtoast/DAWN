using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Shared reference geometry for the full-screen phase surface. The header
    /// owns the top 108 pixels; guided lessons keep their separate 148px band.
    /// The background reveals additional sky above the established room layout
    /// without stretching the source art or moving its authored hotspots.
    /// </summary>
    public static class PhaseSurfaceLayout
    {
        public static readonly Vector2 ContentSize = new Vector2(1920f, 1080f);
        public const float HeaderHeight = 108f;
        public const float TutorialHeaderHeight = 148f;
        private const float AuthoredBackgroundHeight = 932f;
        public static readonly Vector2 BackgroundSize = new Vector2(1920f, 1080f - HeaderHeight);
        public static readonly Vector2 BackgroundPosition = new Vector2(0f, -HeaderHeight * .5f);
        public const float StagePositionY = -55f;

        public static float GetHeaderHeight(bool tutorial) => tutorial
            ? TutorialHeaderHeight + (GameReadability.Scale - 1f) * 128f
            : HeaderHeight + (GameReadability.Scale - 1f) * 40f;

        public static Rect BackgroundUv(Texture texture)
        {
            if (texture == null || texture.width <= 0 || texture.height <= 0)
                return new Rect(0f, 0f, 1f, 1f);

            return BackgroundUv(new Vector2(texture.width, texture.height));
        }

        public static Rect BackgroundUv(Vector2 sourceSize)
        {
            if (sourceSize.x <= 0f || sourceSize.y <= 0f)
                return new Rect(0f, 0f, 1f, 1f);

            float scale = Mathf.Max(BackgroundSize.x / sourceSize.x, BackgroundSize.y / sourceSize.y);
            float visibleWidth = BackgroundSize.x / (sourceSize.x * scale);
            float visibleHeight = BackgroundSize.y / (sourceSize.y * scale);
            float authoredScale = Mathf.Max(BackgroundSize.x / sourceSize.x, AuthoredBackgroundHeight / sourceSize.y);
            float authoredBottom = (1f - AuthoredBackgroundHeight / (sourceSize.y * authoredScale)) * .5f;
            // Preserve the old bottom edge and reveal 40px above it. Near-wide
            // sources have less spare sky; clamp their crop instead of repeating
            // edge pixels or stretching the room. Source-point helpers use this
            // same crop, including that limited-source fallback.
            float bottom = Mathf.Clamp(authoredBottom, 0f, 1f - visibleHeight);
            return new Rect((1f - visibleWidth) * .5f, bottom, visibleWidth, visibleHeight);
        }

        /// <summary>Maps a top-origin source-image UV into Phase Content coordinates.</summary>
        public static Vector2 SourceUvToContent(Vector2 sourceUv, Vector2 sourceSize)
        {
            Rect crop = BackgroundUv(sourceSize);
            float croppedY = (1f - sourceUv.y - crop.y) / crop.height;
            return new Vector2(
                ((sourceUv.x - crop.x) / crop.width - .5f) * BackgroundSize.x,
                (croppedY - .5f) * BackgroundSize.y + BackgroundPosition.y);
        }

        public static Vector2 SourcePixelToContent(Vector2 sourcePixel, Vector2 sourceSize)
        {
            return SourceUvToContent(new Vector2(sourcePixel.x / sourceSize.x, sourcePixel.y / sourceSize.y), sourceSize);
        }

        public static Vector2 SourcePixelSizeToContent(Vector2 sourcePixelSize, Vector2 sourceSize)
        {
            Rect crop = BackgroundUv(sourceSize);
            return new Vector2(
                sourcePixelSize.x / (sourceSize.x * crop.width) * BackgroundSize.x,
                sourcePixelSize.y / (sourceSize.y * crop.height) * BackgroundSize.y);
        }

        public static Vector2 ContentToSourceUv(Vector2 contentPosition, Vector2 sourceSize)
        {
            Rect crop = BackgroundUv(sourceSize);
            Vector2 relative = contentPosition - BackgroundPosition;
            float croppedY = .5f + relative.y / BackgroundSize.y;
            return new Vector2(
                crop.x + (relative.x / BackgroundSize.x + .5f) * crop.width,
                1f - (crop.y + croppedY * crop.height));
        }
    }
}
