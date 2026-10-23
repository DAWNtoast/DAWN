using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>A texture-free rarity glow drawn behind a component's artwork.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ComponentRarityHaloGraphic : MaskableGraphic
    {
        private const int Segments = 96;
        private static readonly float[] Radii =
            { .28f, .50f, .66f, .74f, .80f, .845f, .865f, .89f, .92f, .955f, 1f };
        private static readonly float[] Opacities =
            { .075f, .095f, .15f, .26f, .38f, .64f, .66f, .38f, .22f, .09f, 0f };

        [SerializeField] private ComponentRarity rarity;
        [SerializeField] private bool subdued;

        public ComponentRarity Rarity => rarity;

        public void Configure(ComponentRarity rarity, bool subdued = false)
        {
            this.rarity = rarity;
            this.subdued = subdued;
            color = ComponentArtCatalog.RarityColor(rarity);
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Configure(rarity, subdued);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            Configure(rarity, subdued);
        }
#endif

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = GetPixelAdjustedRect();
            Vector2 halfSize = bounds.size * .5f;
            if (halfSize.x <= 0f || halfSize.y <= 0f) return;

            // Each alpha band is continuous around the ellipse. The narrow
            // luminous ring remains readable at icon size; the wide outer
            // falloff and very faint center leave the painted part in focus.
            float strength = subdued ? .52f : 1f;
            if (rarity == ComponentRarity.Common) strength *= .82f;
            Color tint = color;
            Color centerTint = tint;
            centerTint.a *= .06f * strength;
            mesh.AddVert(bounds.center, centerTint, Vector2.zero);

            for (int band = 0; band < Radii.Length; band++)
            {
                Color bandTint = band == 5 || band == 6
                    ? Color.Lerp(tint, new Color(1f, 1f, 1f, tint.a), .14f)
                    : tint;
                bandTint.a *= Opacities[band] * strength;
                int start = mesh.currentVertCount;
                for (int segment = 0; segment < Segments; segment++)
                {
                    float angle = segment * (Mathf.PI * 2f / Segments);
                    Vector2 point = bounds.center + new Vector2(
                        Mathf.Cos(angle) * halfSize.x * Radii[band],
                        Mathf.Sin(angle) * halfSize.y * Radii[band]);
                    mesh.AddVert(point, bandTint, Vector2.zero);
                }

                for (int segment = 0; segment < Segments; segment++)
                {
                    int next = (segment + 1) % Segments;
                    if (band == 0)
                    {
                        mesh.AddTriangle(0, start + segment, start + next);
                        continue;
                    }
                    int previous = start - Segments;
                    mesh.AddTriangle(previous + segment, start + segment, start + next);
                    mesh.AddTriangle(previous + segment, start + next, previous + next);
                }
            }

            // A restrained pair/four points gives the highest tiers a second
            // visual cue without animated particles or another texture.
            float minimumRadius = Mathf.Min(halfSize.x, halfSize.y);
            int glintCount = rarity == ComponentRarity.Legendary ? 4
                : rarity == ComponentRarity.Epic ? 2 : 0;
            if (minimumRadius < 12f || glintCount == 0) return;
            float glintRadius = Mathf.Clamp(minimumRadius * .07f, 1.5f, 6f);
            Color glintTint = Color.Lerp(tint, new Color(1f, 1f, 1f, tint.a), .42f);
            glintTint.a *= .80f * strength;
            for (int glint = 0; glint < glintCount; glint++)
            {
                float angle = Mathf.PI * .25f + glint * (Mathf.PI * 2f / glintCount);
                Vector2 position = bounds.center + new Vector2(
                    Mathf.Cos(angle) * halfSize.x * .855f,
                    Mathf.Sin(angle) * halfSize.y * .855f);
                AddGlint(mesh, position, glintRadius, glintTint);
            }
        }

        private static void AddGlint(VertexHelper mesh, Vector2 center, float radius, Color tint)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(center, tint, Vector2.zero);
            Color edgeTint = tint;
            edgeTint.a = 0f;
            for (int point = 0; point < 8; point++)
            {
                float angle = point * (Mathf.PI * .25f);
                float reach = point % 2 == 0 ? radius : radius * .22f;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach,
                    edgeTint, Vector2.zero);
            }
            for (int point = 0; point < 8; point++)
                mesh.AddTriangle(start, start + 1 + point, start + 1 + (point + 1) % 8);
        }
    }
}
