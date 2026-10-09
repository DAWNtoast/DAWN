using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    // A single expanding oblique plane, inspired by the reference's zoom into
    // a small deck platform. Geometry is rebuilt only during the short wipe.
    [RequireComponent(typeof(CanvasRenderer)), DisallowMultipleComponent]
    public sealed class BoardingSceneWipeGraphic : MaskableGraphic
    {
        public BoardingSceneWipeGraphic() { useLegacyMeshGeneration = false; }

        public float Progress { get; private set; }

        public void SetProgress(float progress)
        {
            Progress = float.IsNaN(progress) ? 0f : Mathf.Clamp01(progress);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Progress <= 0f) return;
            Rect bounds = rectTransform.rect;
            // At full coverage the slanted sides must also cover a portrait
            // window's corners, not just the usual wide game viewport.
            float fullWidth = Mathf.Max(bounds.width * 1.6f, bounds.width + bounds.height * .64f + 4f);
            float width = Mathf.Lerp(36f, fullWidth, Progress);
            float height = Mathf.Lerp(3f, bounds.height * 1.65f, Progress * Progress);
            float skew = height * 0.32f;
            Vector2 center = bounds.center + Vector2.down * (bounds.height * 0.13f * (1f - Progress));
            Vector2[] corners =
            {
                center + new Vector2(-width * .5f - skew, -height * .5f),
                center + new Vector2(-width * .5f + skew, height * .5f),
                center + new Vector2(width * .5f + skew, height * .5f),
                center + new Vector2(width * .5f - skew, -height * .5f),
            };
            for (int i = 0; i < corners.Length; i++)
                vh.AddVert(corners[i], color, Vector2.zero);
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(0, 2, 3);
        }
    }
}
