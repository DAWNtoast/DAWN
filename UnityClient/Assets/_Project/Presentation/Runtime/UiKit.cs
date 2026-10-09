using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Common primitives for menus, the legacy tutorial and gameplay. Factories retain
    /// their authored geometry and skins; identifiers remain stable if objects move or are renamed.
    /// </summary>
    public static partial class UiKit
    {
        public static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.one * 0.5f;
            rect.anchorMax = Vector2.one * 0.5f;
            rect.pivot = Vector2.one * 0.5f;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            UiId.Bind(item, name);
            return rect;
        }

        public static Image CreateImage(string name, Transform parent, Vector2 size, Vector2 position,
            Color color, Sprite sprite = null)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            NauticalSurfaceSkin.ApplyControlPart(image);
            return image;
        }

        public static Text CreateText(string name, Transform parent, Vector2 size, Vector2 position,
            int fontSize, FontStyle style, TextAnchor alignment, Color color)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            Text text = rect.gameObject.AddComponent<Text>();
            GameTypography.Apply(text, fontSize, style);
            text.alignment = alignment;
            text.color = NauticalSurfaceSkin.ReadableTextColor(parent, color);
            text.raycastTarget = false;
            text.supportRichText = true;
            return text;
        }

        public static Button CreateButton(string name, Transform parent, Vector2 size, Vector2 position,
            Color normalColor, Color textColor, int fontSize, bool animate = false, bool specialSkinFallback = false)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = normalColor;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (animate) rect.gameObject.AddComponent<NauticalButtonMotion>();
            ColorBlock colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = Color.Lerp(normalColor, Color.white, UiTheme.HoverBlend);
            colors.pressedColor = Color.Lerp(normalColor, Color.black, UiTheme.PressBlend);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = UiTheme.DisabledControl;
            colors.colorMultiplier = 1f;
            button.colors = colors;
            Text label = CreateText("Label", rect, size - UiTheme.ButtonLabelInset, Vector2.zero,
                fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, textColor);
            label.text = name;
            if (!WoodButtonSkin.Apply(button, normalColor) && specialSkinFallback)
                NauticalSurfaceSkin.ApplySpecialButton(button, normalColor);
            return button;
        }

        public static RectTransform CreatePanel(string name, Transform parent, Vector2 size, Vector2 position, Color color)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            NauticalSurfaceSkin.ApplyPanel(image, color);
            return rect;
        }

        public static RectTransform CreateStretchPanel(string name, Transform parent, Color color)
        {
            RectTransform rect = CreateRect(name, parent, Vector2.zero, Vector2.zero);
            UiLayout.Stretch(rect);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            if (name == "Fill") NauticalSurfaceSkin.ApplyControlPart(image);
            else NauticalSurfaceSkin.ApplyPanel(image, color);
            return rect;
        }
    }
}
