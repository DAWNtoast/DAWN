using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Departure-only paper and action treatment; existing controls own all interaction state.</summary>
    public static class DepartureSkin
    {
        public const string PaperResourcePath = "CardWorkshopV2/deck_builder_collection_parchment_v3";
        public static readonly Color Ink = new Color(.20f, .13f, .08f, 1f);
        public static readonly Color SelectedInk = new Color(.10f, .28f, .25f, 1f);
        private static readonly Color Bronze = new Color(.62f, .39f, .16f, 1f);
        private static Sprite paper;

        static DepartureSkin()
        {
            Application.quitting += Release;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Release;
#endif
        }

        public static Sprite Paper
        {
            get
            {
                if (paper != null) return paper;
                Texture2D texture = Resources.Load<Texture2D>(PaperResourcePath);
                if (texture == null) return null;
                // Preserve the faint compass corners while the quiet centre resizes.
                Vector4 border = new Vector4(texture.width * .22f, texture.height * .22f,
                    texture.width * .22f, texture.height * .22f);
                paper = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                    Vector2.one * .5f, 100f, 0, SpriteMeshType.FullRect, border);
                paper.name = PaperResourcePath;
                paper.hideFlags = HideFlags.HideAndDontSave;
                return paper;
            }
        }

        public static void ApplyPanel(Image image)
        {
            if (image == null || Paper == null) return;
            image.sprite = Paper;
            image.overrideSprite = null;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.preserveAspect = false;
            image.pixelsPerUnitMultiplier = Paper.rect.width / 800f;
            image.color = Color.white;
            Transform rim = image.transform.Find("Material Timber Rim");
            if (rim != null) rim.gameObject.SetActive(false);
        }

        public static void ReleaseGameplayPaperReference()
        {
            if (paper != null) paper.hideFlags &= ~HideFlags.DontUnloadUnusedAsset;
            paper = null;
        }

        public static void Apply(RectTransform route, RectTransform setup)
        {
            if (route != null)
            {
                ApplyPanel(route.GetComponent<Image>());
                for (int index = 0; index < 3; index++)
                {
                    Button button = route.Find("Route " + index)?.GetComponent<Button>();
                    if (button == null) continue;
                    WoodButtonRoleState state = button.GetComponent<WoodButtonRoleState>();
                    Text label = button.transform.Find("Label")?.GetComponent<Text>();
                    bool selected = state != null ? state.IsSelected : label != null && label.text.StartsWith("◆");
                    SetRouteSelected(button, selected);
                }
                ApplyPaperText(route);
                foreach (string endpoint in new[] { "Departure Chart Preview Origin", "Departure Chart Preview Destination" })
                {
                    Text markerLabel = route.Find("Departure Chart Selection/Departure Chart Preview/" + endpoint + "/Label")?.GetComponent<Text>();
                    // These labels sit over blue water, not the surrounding paper.
                    if (markerLabel != null) markerLabel.color = WoodButtonSkin.Ink;
                }
            }
            ApplySetup(setup);
        }

        public static void ApplySetup(RectTransform setup)
        {
            if (setup == null) return;
            ApplyPanel(setup.GetComponent<Image>());
            // Buying provisions is one of several preparation actions, not the departure action.
            Button provisions = setup.Find("Departure Buy Provisions")?.GetComponent<Button>();
            if (provisions != null) ApplySecondary(provisions);
            Button start = setup.Find("Start Voyage")?.GetComponent<Button>();
            if (start != null) ApplyPrimary(start);
            ApplyPaperText(setup);
        }

        public static void SetRouteSelected(Button button, bool selected)
        {
            if (button == null || NauticalSurfaceSkin.Paper == null) return;
            Image face = button.GetComponent<Image>();
            if (face == null) return;
            face.sprite = NauticalSurfaceSkin.Paper;
            face.overrideSprite = null;
            face.type = Image.Type.Tiled;
            face.fillCenter = true;
            face.preserveAspect = false;
            face.pixelsPerUnitMultiplier = face.sprite.rect.width / 420f;
            face.color = Color.white;
            button.targetGraphic = face;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = selected ? new Color(1f, .97f, .87f, 1f) : new Color(.96f, .94f, .88f, 1f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = new Color(1f, .98f, .91f, 1f);
            colors.pressedColor = new Color(.85f, .85f, .76f, 1f);
            colors.disabledColor = new Color(.80f, .80f, .76f, 1f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = .1f;
            button.colors = colors;
            WoodButtonRoleState state = button.GetComponent<WoodButtonRoleState>();
            if (state != null)
            {
                state.AssignRole(WoodButtonRole.Secondary, true);
                state.Select(selected);
                state.effectColor = Bronze;
                state.effectDistance = new Vector2(2f, -2f);
                state.useGraphicAlpha = true;
                state.enabled = selected;
            }
            Text label = button.transform.Find("Label")?.GetComponent<Text>();
            if (label != null)
            {
                label.color = selected ? SelectedInk : Ink;
                RemoveLabelBacking(label);
            }
        }

        private static void ApplyPrimary(Button button)
        {
            Image face = button.GetComponent<Image>();
            if (face == null || NauticalSurfaceSkin.Paper == null) return;
            face.sprite = NauticalSurfaceSkin.Paper;
            face.overrideSprite = null;
            face.type = Image.Type.Tiled;
            face.fillCenter = true;
            face.preserveAspect = false;
            face.pixelsPerUnitMultiplier = face.sprite.rect.width / 420f;
            // Keep the pigment in the mesh, so the existing Outline retains its copper colour.
            face.color = new Color(.10f, .28f, .26f, 1f);
            button.targetGraphic = face;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.20f, 1.20f, 1.12f, 1f);
            colors.selectedColor = new Color(1.13f, 1.13f, 1.08f, 1f);
            colors.pressedColor = new Color(.72f, .76f, .72f, 1f);
            colors.disabledColor = new Color(.65f, .65f, .62f, 1f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = .1f;
            button.colors = colors;
            WoodButtonRoleState state = button.GetComponent<WoodButtonRoleState>();
            if (state != null)
            {
                state.AssignRole(WoodButtonRole.Primary, true);
                state.effectColor = new Color(.80f, .57f, .26f, 1f);
                state.effectDistance = new Vector2(2f, -2f);
                state.useGraphicAlpha = true;
                state.enabled = true;
            }
            Text label = button.transform.Find("Label")?.GetComponent<Text>();
            if (label != null)
            {
                label.color = WoodButtonSkin.Ink;
                RemoveLabelBacking(label);
            }
        }

        private static void ApplySecondary(Button button)
        {
            // Older saved previews can have the wooden sprite without the newer role component.
            // Set the same secondary palette directly without adding serialized components.
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(.84f, .80f, .73f, 1f);
            colors.highlightedColor = new Color(1.05f, .96f, .8176f, 1f);
            colors.selectedColor = new Color(.9576f, .896f, .73f, 1f);
            colors.pressedColor = new Color(.5712f, .52f, .4526f, 1f);
            colors.disabledColor = new Color(.52f, .53f, .54f, .82f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = .1f;
            button.colors = colors;
            WoodButtonRoleState state = button.GetComponent<WoodButtonRoleState>();
            if (state != null)
            {
                state.AssignRole(WoodButtonRole.Secondary, true);
                state.Select(false);
                state.enabled = false;
            }
            Text label = button.transform.Find("Label")?.GetComponent<Text>();
            if (label != null) label.color = WoodButtonSkin.Ink;
        }

        private static void ApplyPaperText(RectTransform panel)
        {
            foreach (Text label in panel.GetComponentsInChildren<Text>(true))
            {
                // Buttons and inputs keep their own selected, disabled and validation treatment.
                if (!IsPaperLabel(label, panel)) continue;
                label.color = NauticalSurfaceSkin.ReadableTextColor(label.transform.parent, label.color);
                RemoveLabelBacking(label);
            }
        }

        private static bool IsPaperLabel(Text label, RectTransform panel)
        {
            if (label == null) return false;
            // Only labels physically inside this sheet use its dark ink.
            Vector3 local = panel.InverseTransformPoint(label.rectTransform.position);
            if (!panel.rect.Contains(new Vector2(local.x, local.y))) return false;
            for (Transform current = label.transform.parent; current != null; current = current.parent)
            {
                if (current.GetComponent<Selectable>() != null && current.name != "Use Chart Seed") return false;
                if (current.name == "Departure Chart Preview") return false;
                Image surface = current.GetComponent<Image>();
                if (surface != null && surface.enabled && surface.color.a >= .65f)
                    return NauticalSurfaceSkin.IsPaperSurface(surface.sprite);
                // Labels on map illustrations retain their authored marker contrast.
                RawImage art = current.GetComponent<RawImage>();
                if (art != null && art.enabled && art.color.a >= .65f) return false;
                if (current == panel) break;
            }
            return false;
        }

        private static void RemoveLabelBacking(Text label)
        {
            foreach (Shadow effect in label.GetComponents<Shadow>()) effect.enabled = false;
            Transform backing = label.transform.parent?.Find(label.name + " Backing");
            Image plate = backing == null ? null : backing.GetComponent<Image>();
            if (plate == null) return;
            plate.enabled = false;
            plate.raycastTarget = false;
        }

        private static void Release()
        {
            if (paper == null) return;
            if (Application.isPlaying) Object.Destroy(paper);
            else Object.DestroyImmediate(paper);
            paper = null;
        }
    }
}
