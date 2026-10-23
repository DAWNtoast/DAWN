using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Preserves each component's painted rarity board through selection and button feedback.</summary>
    [DisallowMultipleComponent]
    public sealed class ComponentRarityWoodSkin : Outline
    {
        private static readonly string[] ResourcePaths =
        {
            "UIWood/ComponentRarity/wood_common", "UIWood/ComponentRarity/wood_uncommon",
            "UIWood/ComponentRarity/wood_rare", "UIWood/ComponentRarity/wood_epic",
            "UIWood/ComponentRarity/wood_legendary",
        };
        private static readonly Sprite[] Faces = new Sprite[ResourcePaths.Length];

        [SerializeField] private ComponentRarity rarity;
        [SerializeField] private bool selected;

        public ComponentRarity Rarity => rarity;
        public bool IsSelected => selected;

        static ComponentRarityWoodSkin()
        {
            Application.quitting += Release;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Release;
#endif
        }

        /// <summary>Returns only an authored rarity board; missing resources remain detectable by validation.</summary>
        public static Sprite FaceFor(ComponentRarity value)
        {
            int index = (int)value;
            if (index < 0 || index >= Faces.Length) return null;
            if (Faces[index] != null) return Faces[index];
            Texture2D texture = Resources.Load<Texture2D>(ResourcePaths[index]);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Trilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            float border = Mathf.Round(Mathf.Min(texture.width, texture.height) * .14f);
            Sprite face = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                Vector2.one * .5f, 100f, 0, SpriteMeshType.FullRect, Vector4.one * border);
            face.name = "Component Rarity Wood " + value;
            face.hideFlags = HideFlags.HideAndDontSave;
            Faces[index] = face;
            return face;
        }

        /// <summary>Returns false when falling back to the shared board, without hiding the component or its input.</summary>
        public static bool Apply(Button button, ComponentRarity value)
        {
            if (button == null) return false;
            Image image = button.GetComponent<Image>();
            if (image == null) return false;
            Sprite authored = FaceFor(value);
            Sprite face = authored != null ? authored : WoodButtonSkin.Face;
            if (face == null) return false;

            image.sprite = face;
            image.overrideSprite = null;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.preserveAspect = false;
            image.color = Color.white;
            float corner = Mathf.Clamp(Mathf.Min(image.rectTransform.rect.width, image.rectTransform.rect.height) * .12f, 6f, 16f);
            image.pixelsPerUnitMultiplier = face.border.x / corner;
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;

            ComponentRarityWoodSkin state = button.GetComponent<ComponentRarityWoodSkin>();
            if (state == null) state = button.gameObject.AddComponent<ComponentRarityWoodSkin>();
            state.rarity = value;
            ApplyState(button, state);
            return authored != null;
        }

        public static void SetSelected(Button button, bool value)
        {
            if (button == null) return;
            ComponentRarityWoodSkin state = button.GetComponent<ComponentRarityWoodSkin>();
            if (state == null) return;
            state.selected = value;
            ApplyState(button, state);
        }

        private static void ApplyState(Button button, ComponentRarityWoodSkin state)
        {
            WoodButtonRoleState legacy = button.GetComponent<WoodButtonRoleState>();
            if (legacy != null) legacy.enabled = false;

            // Only neutral brightness changes: the artwork supplies the rarity color.
            ColorBlock colors = button.colors;
            colors.normalColor = Gray(state.selected ? 1f : .9f);
            colors.highlightedColor = Gray(1.12f);
            colors.selectedColor = Gray(1.06f);
            colors.pressedColor = Gray(.72f);
            colors.disabledColor = new Color(.5f, .5f, .5f, .86f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = .1f;
            button.colors = colors;

            // This mesh edge shares the button's tint, including its disabled state.
            state.effectColor = new Color(1f, .95f, .83f, .9f);
            state.effectDistance = new Vector2(1f, -1f);
            state.useGraphicAlpha = true;
            state.enabled = state.selected;
        }

        private static Color Gray(float brightness) => new Color(brightness, brightness, brightness, 1f);

        private static void Release()
        {
            for (int index = 0; index < Faces.Length; index++)
            {
                if (Faces[index] == null) continue;
                if (Application.isPlaying) Object.Destroy(Faces[index]);
                else Object.DestroyImmediate(Faces[index]);
                Faces[index] = null;
            }
        }
    }
}
