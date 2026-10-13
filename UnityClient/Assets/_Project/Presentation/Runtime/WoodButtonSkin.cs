using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Shared physical wood backing for UI controls, not scene-object hit targets.
    /// Nine-slicing preserves fasteners on both 30px +/- controls and long decision rows.</summary>
    public static class WoodButtonSkin
    {
        public const string ResourcePath = "UIWood/wood_button_face_v1";
        public static readonly Color Ink = new Color(1f, 0.92f, 0.76f, 1f);
        public static readonly Color MutedInk = new Color(0.78f, 0.73f, 0.64f, 1f);
        private static Sprite face;

        static WoodButtonSkin()
        {
            Application.quitting += Release;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Release;
#endif
        }

        public static Sprite Face
        {
            get
            {
                if (face != null) return face;
                var texture = Resources.Load<Texture2D>(ResourcePath);
                if (texture == null) return null;
                texture.filterMode = FilterMode.Trilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                // Measured corner artwork ends within 112 source pixels.
                face = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect, Vector4.one * 112f);
                face.name = "Carved Walnut Button Face";
                face.hideFlags = HideFlags.HideAndDontSave;
                return face;
            }
        }

        public static bool IsControl(string name, Color originalFace)
        {
            if (originalFace.a <= 0.01f) return false;
            // These Selectables are cards/map markers, not flat action buttons.
            foreach (string prefix in new[] { "Battle Card ", "Add Deck Card ", "Event Node " })
                if (name.StartsWith(prefix, StringComparison.Ordinal)) return false;
            return true;
        }

        public static bool Apply(Button button, Color originalFace)
        {
            if (button == null || !IsControl(button.name, originalFace) || Face == null) return false;
            Image image = button.GetComponent<Image>();
            if (image == null) return false;
            image.sprite = Face;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.preserveAspect = false;
            image.color = Color.white; // ColorTint must not multiply the old blue/green fill twice.
            Vector2 size = image.rectTransform.sizeDelta;
            float corner = Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.12f, 3f, 9f);
            image.pixelsPerUnitMultiplier = 112f / corner;
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            WoodButtonRoleState state = State(button);
            if (!state.HasExplicitRole) state.AssignRole(DefaultRoleFor(button.name), false);
            foreach (Transform child in button.transform)
            {
                Text label = child.GetComponent<Text>();
                if (label == null) continue;
                label.color = child.name == "Detail" ? MutedInk : Ink;
                label.raycastTarget = false;
                if (child.name != "Label") continue; // Preserve authored two-line menu layouts.
                float insetX = Mathf.Clamp(size.x * 0.06f, 4f, 12f);
                float insetY = Mathf.Clamp(size.y * 0.07f, 2f, 5f);
                label.rectTransform.sizeDelta = size - new Vector2(insetX * 2f, insetY * 2f);
                label.resizeTextForBestFit = true;
                label.resizeTextMaxSize = label.fontSize;
                label.resizeTextMinSize = Mathf.Min(label.fontSize, Mathf.Max(10, label.fontSize - 3));
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
            }
            ApplyRoleAppearance(button, state);
            return true;
        }

        public static void SetSelected(Button button, bool selected)
        {
            if (button == null || !(button.targetGraphic is Image image) || image.sprite == null || image.sprite != Face) return;
            WoodButtonRoleState state = State(button);
            if (!state.HasExplicitRole) state.AssignRole(DefaultRoleFor(button.name), false);
            state.Select(selected);
            ApplyRoleAppearance(button, state);
        }

        /// <summary>Override semantic emphasis without changing callbacks, hit bounds or selected-tab state.</summary>
        public static void SetRole(Button button, WoodButtonRole role)
        {
            if (button == null || !(button.targetGraphic is Image image) || image.sprite == null || image.sprite != Face) return;
            WoodButtonRoleState state = State(button);
            state.AssignRole(role, true);
            ApplyRoleAppearance(button, state);
        }

        public static WoodButtonRole DefaultRoleFor(string name)
        {
            if (string.IsNullOrEmpty(name)) return WoodButtonRole.Secondary;
            if (name == "Confirm New Game" || name == "Reset" || name == "Reset Tutorial" ||
                name == "Retreat Battle" || name == "Emergency Battle Return")
                return WoodButtonRole.Danger;
            // Navigation controls stay quiet even if their caller historically
            // supplied a gold color. The settlement's finish action is an exception.
            if (name.StartsWith("Back ", StringComparison.Ordinal) || name.StartsWith("Cancel ", StringComparison.Ordinal) ||
                name.StartsWith("Previous ", StringComparison.Ordinal) || name.StartsWith("Next ", StringComparison.Ordinal) ||
                (name.Contains("Settings") && !name.StartsWith("Apply ", StringComparison.Ordinal)))
                return WoodButtonRole.Secondary;
            if (name.StartsWith("Confirm", StringComparison.Ordinal) || name.StartsWith("Purchase", StringComparison.Ordinal) ||
                name.StartsWith("Apply", StringComparison.Ordinal) || name.StartsWith("Start", StringComparison.Ordinal) ||
                name.StartsWith("Claim", StringComparison.Ordinal) || name.StartsWith("Recruit", StringComparison.Ordinal) ||
                name == "Execute Sail" || name == "Return To Port" || name == "End Battle Turn" || name == "New Game" ||
                name == "Tutorial Primary" || name == "Primary Action" || name == "Recover Profile Save" ||
                name == "Treasure Craft Map" || name == "Treasure Equip And Depart" || name == "Shipyard Equip Component" ||
                name == "Departure Buy Provisions" || name == "Warehouse Display Selected")
                return WoodButtonRole.Primary;
            return WoodButtonRole.Secondary;
        }

        private static WoodButtonRoleState State(Button button)
        {
            WoodButtonRoleState state = button.GetComponent<WoodButtonRoleState>();
            return state != null ? state : button.gameObject.AddComponent<WoodButtonRoleState>();
        }

        private static void ApplyRoleAppearance(Button button, WoodButtonRoleState state)
        {
            Color normal = state.Role == WoodButtonRole.Primary ? new Color(1.80f, 1.32f, .72f, 1f) :
                state.Role == WoodButtonRole.Danger ? new Color(1.35f, .65f, .48f, 1f) : new Color(.84f, .80f, .73f, 1f);
            if (state.IsSelected) normal = Color.Lerp(normal, new Color(1.45f, 1.21f, .78f, 1f), .7f);
            SetColors(button, normal);
            // A mesh edge shares the Image's ColorTint, so a disabled primary
            // cannot retain a detached bright border. No added hit target or Update.
            state.effectColor = state.Role == WoodButtonRole.Danger ? new Color(.67f, .28f, .16f, .95f) :
                new Color(.83f, .58f, .24f, state.IsSelected ? .95f : .85f);
            state.effectDistance = new Vector2(1f, -1f);
            state.useGraphicAlpha = true;
            state.enabled = state.Role != WoodButtonRole.Secondary || state.IsSelected;
            // Builders may add cost/status labels with their own meaning after
            // Apply. Updating a role or selection must not recolor those labels.
            Text label = button.GetComponentInChildren<Text>();
            if (label != null) label.color = state.IsSelected ? new Color(1f, .85f, .46f, 1f) : Ink;
        }

        private static void SetColors(Button button, Color normal)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = new Color(normal.r * 1.25f, normal.g * 1.2f, normal.b * 1.12f, 1f);
            colors.selectedColor = new Color(normal.r * 1.14f, normal.g * 1.12f, normal.b, 1f);
            colors.pressedColor = new Color(normal.r * 0.68f, normal.g * 0.65f, normal.b * 0.62f, 1f);
            colors.disabledColor = new Color(0.52f, 0.53f, 0.54f, 0.82f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            button.colors = colors;
        }

        private static void Release()
        {
            if (face == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(face);
            else UnityEngine.Object.DestroyImmediate(face);
            face = null;
        }
    }
}
