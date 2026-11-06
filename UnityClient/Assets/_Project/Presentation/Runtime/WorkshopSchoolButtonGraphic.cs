using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>School-specific trim over a solid wooden plank, without a card's energy socket.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorkshopSchoolButtonGraphic : MaskableGraphic
    {
        [SerializeField] private string schoolId = "neutral";
        private Texture2D trimTexture;
        public override Texture mainTexture => trimTexture != null ? trimTexture : Texture2D.whiteTexture;

        public static void Apply(Button button, string school, bool selected)
        {
            school = CardSchoolArtCatalog.NormalizeSchool(school);
            Color stain = Stain(school);
            Color accent = Accent(school);
            Image hitArea = button.GetComponent<Image>();
            hitArea.color = Color.clear;
            WoodButtonRoleState woodState = button.GetComponent<WoodButtonRoleState>();
            if (woodState != null) woodState.enabled = false;

            RectTransform plankRect = Layer(button.transform, "School Wood Plank", new Vector2(6f, 4f));
            RawImage plank = plankRect.gameObject.AddComponent<RawImage>();
            plank.texture = WoodButtonSkin.Face.texture;
            // Sample only the grain, leaving the generic button's brass frame behind.
            plank.uvRect = new Rect(.14f, .18f, .72f, .64f);
            plank.raycastTarget = false;
            button.targetGraphic = plank;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = selected ? new Color(1.45f, 1.35f, 1.2f) : new Color(1.12f, 1.08f, 1f);
            colors.highlightedColor = new Color(1.65f, 1.5f, 1.3f);
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = new Color(.7f, .65f, .6f);
            colors.disabledColor = new Color(.45f, .45f, .45f, .7f);
            colors.fadeDuration = .1f;
            button.colors = colors;

            Image finish = Layer(button.transform, "School Wood Stain", new Vector2(6f, 4f)).gameObject.AddComponent<Image>();
            finish.color = stain;
            finish.raycastTarget = false;

            WorkshopSchoolButtonGraphic trim = Layer(button.transform, "School Plank Trim", Vector2.zero)
                .gameObject.AddComponent<WorkshopSchoolButtonGraphic>();
            trim.schoolId = school;
            trim.trimTexture = DeckListArtCatalog.Texture(school);
            trim.color = selected ? Color.white : new Color(.88f, .88f, .88f);
            trim.raycastTarget = false;
            if (selected)
            {
                Outline edge = trim.gameObject.AddComponent<Outline>();
                edge.effectColor = new Color(accent.r, accent.g, accent.b, .75f);
                edge.effectDistance = new Vector2(.8f, -.8f);
                edge.useGraphicAlpha = true;
                // The lower inset marker makes selection visible independently of the school color.
                RectTransform marker = Layer(button.transform, "Selected School Marker", Vector2.zero);
                marker.anchorMin = marker.anchorMax = new Vector2(.5f, 0f);
                marker.sizeDelta = new Vector2(42f, 2f);
                marker.anchoredPosition = new Vector2(0f, 6f);
                Image line = marker.gameObject.AddComponent<Image>();
                line.color = new Color(1f, .91f, .62f);
                line.raycastTarget = false;
            }
            Text label = button.GetComponentInChildren<Text>();
            label.color = selected ? new Color(1f, .94f, .73f) : new Color(.97f, .94f, .85f);
            label.rectTransform.sizeDelta = new Vector2(112f, 30f);
            label.rectTransform.anchoredPosition = new Vector2(0f, selected ? 1f : 0f);
            label.transform.SetAsLastSibling();
            Shadow shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.04f, .025f, .02f, .9f);
            shadow.effectDistance = new Vector2(1f, -1f);
            trim.SetAllDirty();
        }

        private static RectTransform Layer(Transform parent, string name, Vector2 inset)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = inset;
            rect.offsetMax = -inset;
            return rect;
        }

        private static Color Stain(string school)
        {
            switch (school)
            {
                case "fire": return new Color(.72f, .16f, .025f, .43f);
                case "arcane": return new Color(.31f, .10f, .56f, .52f);
                case "curse": return new Color(.06f, .35f, .18f, .48f);
                case "tide": return new Color(.015f, .35f, .55f, .52f);
                case "thunder": return new Color(.12f, .23f, .48f, .52f);
                case "blood": return new Color(.57f, .025f, .07f, .50f);
                default: return new Color(.49f, .30f, .11f, .20f);
            }
        }

        private static Color Accent(string school)
        {
            switch (school)
            {
                case "fire": return new Color(1f, .39f, .08f);
                case "arcane": return new Color(.73f, .46f, 1f);
                case "curse": return new Color(.29f, .8f, .43f);
                case "tide": return new Color(.25f, .8f, 1f);
                case "thunder": return new Color(1f, .87f, .32f);
                case "blood": return new Color(1f, .20f, .29f);
                default: return new Color(.9f, .69f, .35f);
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            DeckListArtCatalog.Layout layout = DeckListArtCatalog.Bounds(schoolId);
            if (trimTexture == null || layout == null) return;
            Rect rect = GetPixelAdjustedRect();
            float cap = Mathf.Min(12f, rect.width * .15f);
            float end = layout.x + layout.width;
            float innerEnd = layout.x + layout.width * .94f;
            float top = layout.y + layout.height;
            // Mirror the outermost right end. The left end of the source art contains
            // an unrelated blue energy gem, and the full right end contains a cost cell.
            Quad(mesh, Rect.MinMaxRect(rect.xMin, rect.yMin, rect.xMin + cap, rect.yMax),
                end, innerEnd, layout.y, top, color);
            Quad(mesh, Rect.MinMaxRect(rect.xMin + cap, rect.yMin, rect.xMax - cap, rect.yMax),
                layout.x + layout.width * .20f, layout.x + layout.width * .80f, layout.y, top, color);
            Quad(mesh, Rect.MinMaxRect(rect.xMax - cap, rect.yMin, rect.xMax, rect.yMax),
                innerEnd, end, layout.y, top, color);
        }

        private static void Quad(VertexHelper mesh, Rect rect, float left, float right, float bottom, float top, Color tint)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(new Vector3(rect.xMin, rect.yMin), tint, new Vector2(left, bottom));
            mesh.AddVert(new Vector3(rect.xMin, rect.yMax), tint, new Vector2(left, top));
            mesh.AddVert(new Vector3(rect.xMax, rect.yMax), tint, new Vector2(right, top));
            mesh.AddVert(new Vector3(rect.xMax, rect.yMin), tint, new Vector2(right, bottom));
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
