using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Soft deck contact beneath a complete cast or an individual crew member.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BoardingContactShadowGraphic : MaskableGraphic
    {
        [SerializeField] private bool formation;

        public void Configure(bool isFormation)
        {
            formation = isFormation;
            raycastTarget = false;
            color = new Color(.055f, .035f, .018f, .32f);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            int count = formation ? 3 : 1;
            float width = formation ? rect.width * .25f : rect.width;
            for (int i = 0; i < count; i++)
            {
                float x = count == 1 ? rect.center.x : Mathf.Lerp(rect.xMin, rect.xMax, .17f + i * .33f);
                AddContact(vh, new Vector2(x, rect.center.y), new Vector2(width * .5f, rect.height * .5f));
            }
        }

        private void AddContact(VertexHelper vh, Vector2 center, Vector2 radius)
        {
            const int segments = 32;
            int start = vh.currentVertCount;
            Color32 inner = color;
            Color32 outer = new Color(color.r, color.g, color.b, 0f);
            vh.AddVert(center, inner, Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector2 point = new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y);
                vh.AddVert(center + point * .4f, inner, Vector2.zero);
                vh.AddVert(center + point, outer, Vector2.zero);
                if (i == 0) continue;
                int current = start + 1 + i * 2;
                vh.AddTriangle(start, current - 2, current);
                vh.AddTriangle(current - 2, current - 1, current + 1);
                vh.AddTriangle(current - 2, current + 1, current);
            }
        }
    }
}
