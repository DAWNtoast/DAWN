using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Small UI glints and a finite radial burst. No texture edits or global random state.</summary>
    public sealed class SettlementCelebrationGraphic : MaskableGraphic
    {
        private float elapsed = 2.8f;
        public void SetTime(float seconds) { elapsed = seconds; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float reveal = Mathf.Clamp01((elapsed - 0.20f) / 0.55f);
            for (int i = 0; i < 18; i++)
            {
                float angle = i * Mathf.PI * 2 / 18 + 0.12f;
                float radius = 245f + (i % 3) * 22f;
                Color gold = new Color(1f, 0.68f, 0.23f, (0.04f + 0.12f * (1f - Mathf.Clamp01(elapsed / 2.8f))) * reveal);
                int n = vh.currentVertCount;
                vh.AddVert(new Vector3(0, -10), gold, Vector2.zero);
                vh.AddVert(new Vector3(Mathf.Cos(angle - 0.035f) * radius, Mathf.Sin(angle - 0.035f) * radius * 0.74f), new Color(gold.r, gold.g, gold.b, 0), Vector2.zero);
                vh.AddVert(new Vector3(Mathf.Cos(angle + 0.035f) * radius, Mathf.Sin(angle + 0.035f) * radius * 0.74f), new Color(gold.r, gold.g, gold.b, 0), Vector2.zero);
                vh.AddTriangle(n, n + 1, n + 2);
            }
            float burst = Mathf.Clamp01((elapsed - 0.48f) / 1.6f);
            float alpha = Mathf.Sin(burst * Mathf.PI) * 0.85f;
            for (int i = 0; i < 30; i++)
            {
                float angle = i * 2.399963f;
                float radius = 70 + burst * (115 + i % 7 * 18);
                Vector2 center = new Vector2(Mathf.Cos(angle) * radius, 20 + Mathf.Sin(angle) * radius * 0.65f - burst * burst * 55);
                float size = 2 + i % 4;
                Color tint = new Color(1f, 0.72f + (i % 3) * 0.08f, 0.27f, alpha);
                int n = vh.currentVertCount;
                vh.AddVert(center + Vector2.up * size * 1.7f, tint, Vector2.zero);
                vh.AddVert(center + Vector2.right * size, tint, Vector2.zero);
                vh.AddVert(center + Vector2.down * size * 1.7f, tint, Vector2.zero);
                vh.AddVert(center + Vector2.left * size, tint, Vector2.zero);
                vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
            }
        }
    }
}
