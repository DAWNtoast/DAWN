using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Shared brass-rimmed, enamel navigation for departure and shipyard vessel selectors.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DepartureVesselButtonGraphic : MaskableGraphic
    {
        private const int Segments = 80;

        public static void Apply(Button button, bool forward)
        {
            if (button == null) return;
            // Keep the original selectable, rectangular hit area, and listeners.
            Image hit = button.GetComponent<Image>();
            if (hit != null)
            {
                hit.enabled = true;
                hit.raycastTarget = true;
                hit.sprite = null;
                hit.overrideSprite = null;
                hit.type = Image.Type.Simple;
                hit.color = Color.clear;
            }
            WoodButtonRoleState wood = button.GetComponent<WoodButtonRoleState>();
            if (wood != null) wood.enabled = false;
            foreach (Text label in button.GetComponentsInChildren<Text>(true))
            {
                label.enabled = false;
                label.raycastTarget = false;
            }

            RectTransform faceRect = Layer(button.transform, "Vessel Brass Bezel");
            DepartureVesselButtonGraphic face = faceRect.GetComponent<DepartureVesselButtonGraphic>();
            if (face == null) face = faceRect.gameObject.AddComponent<DepartureVesselButtonGraphic>();
            face.color = Color.white;
            face.raycastTarget = false;
            button.targetGraphic = face;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.10f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.76f, .83f, .85f, 1f);
            colors.disabledColor = new Color(.60f, .65f, .66f, .82f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = .12f;
            button.colors = colors;

            RectTransform arrowRect = Layer(faceRect, "Vessel Direction Chevron");
            arrowRect.anchorMin = new Vector2(.29f, .19f);
            arrowRect.anchorMax = new Vector2(.71f, .81f);
            HeaderNavigationArrowGraphic arrow = arrowRect.GetComponent<HeaderNavigationArrowGraphic>();
            if (arrow == null) arrow = arrowRect.gameObject.AddComponent<HeaderNavigationArrowGraphic>();
            arrow.Configure(forward, button);
            arrow.raycastTarget = false;
            // Edit-mode previews do not run the chevron's LateUpdate tint sync.
            arrow.canvasRenderer.SetColor(button.IsInteractable()
                ? face.canvasRenderer.GetColor() : colors.disabledColor * colors.colorMultiplier);
            // Configure may run again on a retained dock view. Reuse both layers.
            face.enabled = arrow.enabled = true;
        }

        private static RectTransform Layer(Transform parent, string name)
        {
            RectTransform rect = parent.Find(name) as RectTransform;
            if (rect == null)
            {
                var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
                node.layer = parent.gameObject.layer;
                node.transform.SetParent(parent, false);
                rect = node.GetComponent<RectTransform>();
            }
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float radius = Mathf.Min(rect.width, rect.height) * .46f;
            if (radius <= 0f) return;
            Vector2 center = rect.center;
            // A soft contact shadow and a dark lower lip separate the metal from
            // both sunlit water and the darker regional dock paintings.
            Disc(mesh, center + Vector2.down * radius * .07f, radius * 1.06f,
                new Color(.015f, .025f, .03f, .30f), new Color(.015f, .025f, .03f, .30f), .06f);
            Disc(mesh, center, radius, new Color(.20f, .14f, .075f), new Color(.08f, .065f, .045f));
            Disc(mesh, center, radius * .97f, new Color(.97f, .79f, .43f), new Color(.43f, .26f, .10f));
            Disc(mesh, center, radius * .89f, new Color(.45f, .29f, .11f), new Color(.82f, .59f, .25f));
            Disc(mesh, center, radius * .84f, new Color(.025f, .10f, .12f), new Color(.015f, .045f, .055f));
            Disc(mesh, center, radius * .79f, new Color(.055f, .28f, .32f), new Color(.02f, .11f, .15f));
            Ring(mesh, center, radius * .74f, radius * .728f,
                new Color(.55f, .69f, .61f, .38f), new Color(.15f, .35f, .35f, .24f));

            // Two small brass pins echo the hardware elsewhere in the dock UI.
            foreach (float sign in new[] { -1f, 1f })
            {
                Vector2 pin = center + Vector2.up * radius * .925f * sign;
                Disc(mesh, pin, radius * .043f, new Color(.16f, .11f, .05f), new Color(.16f, .11f, .05f));
                Disc(mesh, pin + Vector2.up * radius * .005f, radius * .027f,
                    new Color(1f, .88f, .57f), new Color(.56f, .35f, .13f));
            }
        }

        private void Disc(VertexHelper mesh, Vector2 center, float radius, Color top, Color bottom, float feather = .018f)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(center, color * Color.Lerp(bottom, top, .5f), Vector2.zero);
            for (int i = 0; i <= Segments; i++)
            {
                Vector2 direction = Direction(i);
                mesh.AddVert(center + direction * radius, color * Color.Lerp(bottom, top, (direction.y + 1f) * .5f), Vector2.zero);
                if (i > 0) mesh.AddTriangle(start, start + i, start + i + 1);
            }
            Ring(mesh, center, radius * (1f + feather), radius,
                top, bottom, true);
        }

        private void Ring(VertexHelper mesh, Vector2 center, float outer, float inner, Color top, Color bottom, bool feather = false)
        {
            int start = mesh.currentVertCount;
            for (int i = 0; i <= Segments; i++)
            {
                Vector2 direction = Direction(i);
                Color tint = color * Color.Lerp(bottom, top, (direction.y + 1f) * .5f);
                Color edge = tint;
                if (feather) edge.a = 0f;
                mesh.AddVert(center + direction * outer, edge, Vector2.zero);
                mesh.AddVert(center + direction * inner, tint, Vector2.zero);
                if (i == 0) continue;
                int previous = start + (i - 1) * 2;
                mesh.AddTriangle(previous, previous + 2, previous + 1);
                mesh.AddTriangle(previous + 1, previous + 2, previous + 3);
            }
        }

        private static Vector2 Direction(int segment)
        {
            float angle = segment * Mathf.PI * 2f / Segments;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }
}
