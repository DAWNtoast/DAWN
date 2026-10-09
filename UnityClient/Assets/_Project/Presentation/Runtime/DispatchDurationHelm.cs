using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Dispatch-only slider skin. The rectangular hit target does not rotate.</summary>
    public static class DispatchDurationHelm
    {
        public const string ResourcePath = "DispatchV2/dispatch_duration_helm_v1";

        public static void Apply(Slider slider)
        {
            Texture2D texture = Resources.Load<Texture2D>(ResourcePath);
            if (texture == null || slider == null || slider.handleRect == null) return;
            RectTransform handle = slider.handleRect;
            if (handle.Find("Dispatch Duration Helm") != null) return;
            // Slider stretches its handle vertically. A zero height delta keeps
            // the hit box square instead of stretching the wheel into an oval.
            var area = (RectTransform)handle.parent;
            area.sizeDelta = new Vector2(((RectTransform)slider.transform).sizeDelta.x - 44f, 44f);
            handle.sizeDelta = new Vector2(44f, 0f);
            Image hitTarget = handle.GetComponent<Image>();
            hitTarget.color = Color.clear;
            hitTarget.raycastTarget = true;

            var wheelObject = new GameObject("Dispatch Duration Helm", typeof(RectTransform), typeof(RawImage));
            var wheelRect = (RectTransform)wheelObject.transform;
            wheelRect.SetParent(handle, false);
            wheelRect.anchorMin = wheelRect.anchorMax = Vector2.one * 0.5f;
            wheelRect.sizeDelta = Vector2.one * 40f;
            RawImage wheel = wheelObject.GetComponent<RawImage>();
            wheel.texture = texture;
            wheel.color = Color.white;
            wheel.raycastTarget = false;
            slider.targetGraphic = wheel;
            slider.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = slider.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.14f, 0.88f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.82f, 0.69f, 0.48f);
            colors.disabledColor = new Color(0.55f, 0.52f, 0.46f);
            colors.fadeDuration = 0.1f;
            slider.colors = colors;
            // Turn only when the selected duration changes, never spin while idle.
            slider.onValueChanged.AddListener(_ => SetRotation(slider, wheelRect));
            SetRotation(slider, wheelRect);
        }

        private static void SetRotation(Slider slider, RectTransform wheel)
        {
            wheel.localRotation = Quaternion.Euler(0f, 0f, -135f * slider.normalizedValue);
        }
    }
}
