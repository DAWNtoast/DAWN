using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Departure-only paper and action treatment; existing controls own all interaction state.</summary>
    public static class DepartureSkin
    {
        public const string PaperResourcePath = "CardWorkshopV2/deck_builder_collection_parchment_v3";
        public static readonly Color Ink = new Color(.20f, .13f, .08f, 1f);
        public static Sprite Paper => MarketShopSkin.Paper;

        public static void ApplyPanel(Image image)
        {
            if (image == null || Paper == null) return;
            MarketShopSkin.ApplyPanel(image);
            Transform rim = image.transform.Find("Material Timber Rim");
            if (rim != null) rim.gameObject.SetActive(false);
        }

        public static void ReleaseGameplayPaperReference()
        {
            // MarketShopSkin owns this shared sprite and its release lifecycle.
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
            WoodButtonSkin.ApplyAction(provisions, WoodButtonRole.Secondary);
            Button start = setup.Find("Start Voyage")?.GetComponent<Button>();
            WoodButtonSkin.ApplyAction(start, WoodButtonRole.Primary);
            ApplyPaperText(setup);
        }

        public static void SetRouteSelected(Button button, bool selected)
        {
            WoodButtonSkin.ApplyAction(button, WoodButtonRole.Secondary, selected);
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

    }
}
