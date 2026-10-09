using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public static partial class UiKit
    {
        public static Slider CreateSlider(string name, Transform parent, Vector2 size, Vector2 position,
            float minimum, float maximum, float value, Color trackColor, Color fillColor,
            float fillInset = 18f, float handleWidth = 28f, Sprite handleSprite = null)
        {
            RectTransform root = CreateRect(name, parent, size, position);
            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.minValue = minimum;
            slider.maxValue = maximum;
            CreateImage("Background", root, new Vector2(size.x, 8f), Vector2.zero, trackColor);
            RectTransform fillArea = CreateRect("Fill Area", root, new Vector2(size.x - fillInset, 8f), new Vector2(-fillInset * .5f, 0f));
            Image fill = CreateImage("Fill", fillArea, Vector2.zero, Vector2.zero, fillColor);
            UiLayout.Stretch(fill.rectTransform);
            RectTransform handleArea = CreateRect("Handle Slide Area", root, new Vector2(size.x - 24f, size.y), Vector2.zero);
            Image handle = CreateImage("Handle", handleArea, new Vector2(handleWidth, 0f), Vector2.zero, fillColor, handleSprite);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.SetValueWithoutNotify(value);
            return slider;
        }

        public static InputField CreateInputField(string name, Transform parent, Vector2 size, Vector2 position,
            string value, string placeholderValue, Color backgroundColor, Color ink, Color mutedInk)
        {
            RectTransform rect = CreatePanel(name, parent, size, position, backgroundColor);
            InputField input = rect.gameObject.AddComponent<InputField>();
            input.targetGraphic = rect.GetComponent<Image>();
            Text text = CreateText("Text", rect, size - new Vector2(32f, 8f), Vector2.zero,
                19, FontStyle.Normal, TextAnchor.MiddleLeft, ink);
            text.raycastTarget = true;
            Text placeholder = CreateText("Placeholder", rect, size - new Vector2(32f, 8f), Vector2.zero,
                18, FontStyle.Italic, TextAnchor.MiddleLeft, mutedInk);
            placeholder.text = placeholderValue;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = InputField.LineType.SingleLine;
            input.SetTextWithoutNotify(value);
            return input;
        }

        public static Slider CreateQuantitySlider(string name, Transform parent, Vector2 size, Vector2 position,
            int minimum, int maximum, int initial, Color trackColor, Color fillColor, Sprite handleSprite = null)
        {
            if (maximum < minimum) throw new ArgumentOutOfRangeException(nameof(maximum));
            Slider slider = CreateSlider(name, parent, size, position, minimum, maximum, initial, trackColor, fillColor,
                handleSprite: handleSprite);
            slider.wholeNumbers = true;
            slider.SetValueWithoutNotify(Math.Max(minimum, Math.Min(maximum, initial)));
            slider.interactable = maximum > minimum;
            RectTransform track = (RectTransform)slider.handleRect.parent;
            track.sizeDelta = new Vector2(track.sizeDelta.x, 0f);
            slider.handleRect.sizeDelta = new Vector2(28f, 36f);
            Image hit = slider.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            return slider;
        }

        public static int ReadQuantity(Slider slider, int minimum, int maximum)
        {
            if (maximum < minimum) throw new ArgumentOutOfRangeException(nameof(maximum));
            // Slider stores float: preserve exact integer endpoints before rounding.
            if (slider.value <= slider.minValue) return minimum;
            if (slider.value >= slider.maxValue) return maximum;
            double rounded = Math.Round((double)slider.value, MidpointRounding.AwayFromZero);
            return (int)Math.Max((double)minimum, Math.Min((double)maximum, rounded));
        }

        public static Button CreateTab(string name, Transform parent, Vector2 size, Vector2 position,
            string caption, Color face, Color ink, int fontSize, bool selected, bool interactable = true)
        {
            Button tab = CreateButton(name, parent, size, position, face, ink, fontSize, animate: true, specialSkinFallback: true);
            tab.GetComponentInChildren<Text>().text = caption;
            tab.interactable = interactable;
            WoodButtonSkin.SetSelected(tab, selected);
            return tab;
        }

        public static RectTransform CreateListRow(string name, Transform parent, Vector2 size, Vector2 position, Color face)
        {
            RectTransform row = CreateRect(name, parent, size, position);
            row.gameObject.AddComponent<Image>().color = face;
            return row;
        }

        public static Text CreateBadge(string name, Transform parent, Vector2 size, Vector2 position,
            string caption, int fontSize, Color ink)
        {
            Text badge = CreateText(name, parent, size, position, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, ink);
            badge.text = caption;
            return badge;
        }

        public static Image CreateProgressFill(string name, Transform parent, Vector2 size, Vector2 position,
            Color color, Sprite sprite, float progress)
        {
            if (sprite == null) throw new ArgumentNullException(nameof(sprite), "Filled UGUI images need a sprite.");
            Image fill = CreateImage(name, parent, size, position, color, sprite);
            fill.sprite = sprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.raycastTarget = false;
            SetProgress(fill, progress);
            return fill;
        }

        public static void SetProgress(Image fill, float progress)
        {
            fill.fillAmount = float.IsNaN(progress) ? 0f : Mathf.Clamp01(progress);
        }

        public static RectTransform CreateModalBackdrop(string name, Transform parent, Color color)
        {
            RectTransform backdrop = CreateRect(name, parent, Vector2.zero, Vector2.zero);
            UiLayout.Stretch(backdrop);
            Image image = backdrop.gameObject.AddComponent<Image>();
            image.color = color;
            NauticalSurfaceSkin.ApplyPanel(image, color);
            image.raycastTarget = true;
            return backdrop;
        }

        public static Text CreateHint(string name, Transform parent, Vector2 size, Vector2 position,
            string caption, Color face, Color ink, int fontSize = 18)
        {
            RectTransform panel = CreatePanel(name, parent, size, position, face);
            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            Text label = CreateText("Hint Text", panel, size - new Vector2(24f, 12f), Vector2.zero,
                fontSize, FontStyle.Normal, TextAnchor.MiddleLeft, ink);
            label.text = caption;
            return label;
        }

    }
}
