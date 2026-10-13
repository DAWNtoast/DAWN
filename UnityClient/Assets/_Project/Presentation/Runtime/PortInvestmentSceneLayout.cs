using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Top-origin source pixels registered to the uncropped, full-screen investment painting.</summary>
    public static class PortInvestmentSceneLayout
    {
        public static readonly Vector2 SourceSize = new Vector2(1672f, 941f);
        public static readonly Rect Scene = new Rect(0f, 0f, SourceSize.x, SourceSize.y);
        public static readonly Rect ProjectPlaque = new Rect(135f, 123f, 285f, 67f);
        public static readonly Rect Ledger = new Rect(1150f, 180f, 410f, 570f);
        public static readonly Rect Benefits = new Rect(180f, 680f, 800f, 145f);

        // PhaseSurfaceLayout.SourceUvToContent applies the legacy cropped stage.
        // This scene is displayed with fullSurface and an identity UV rectangle.
        public static Vector2 Position(Vector2 sourcePixel) => new Vector2(
            (sourcePixel.x / SourceSize.x - .5f) * PhaseSurfaceLayout.ContentSize.x,
            (.5f - sourcePixel.y / SourceSize.y) * PhaseSurfaceLayout.ContentSize.y);

        public static Vector2 Size(Vector2 sourcePixelSize) => new Vector2(
            sourcePixelSize.x / SourceSize.x * PhaseSurfaceLayout.ContentSize.x,
            sourcePixelSize.y / SourceSize.y * PhaseSurfaceLayout.ContentSize.y);

        public static Vector2 RelativePosition(Rect sourceBounds, Rect parentBounds) =>
            Position(sourceBounds.center) - Position(parentBounds.center);
    }
}
