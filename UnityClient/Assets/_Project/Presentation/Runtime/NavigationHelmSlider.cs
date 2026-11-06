using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Changes the voyage slider's grip without replacing its input or callbacks.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Slider))]
    public sealed class NavigationHelmSlider : MonoBehaviour
    {
        private const float Diameter = 44f;
        private const string ArtworkName = "Helm Artwork";
        private Slider slider;
        private RectTransform artwork;

        public static void Attach(Slider target)
        {
            if (target == null) return;
            var skin = target.GetComponent<NavigationHelmSlider>();
            if (skin == null) skin = target.gameObject.AddComponent<NavigationHelmSlider>();
            skin.Apply();
        }

        private void OnEnable() => Apply();

        private void Apply()
        {
            slider = GetComponent<Slider>();
            if (slider == null || slider.handleRect == null) return;
            RectTransform handle = slider.handleRect;
            RectTransform root = (RectTransform)transform;
            float travel = Mathf.Max(0f, root.rect.width - Diameter);
            RectTransform slideArea = handle.parent as RectTransform;
            if (slideArea == null) return;
            slideArea.sizeDelta = new Vector2(travel, Diameter);
            slideArea.anchoredPosition = Vector2.zero;
            // Slider stretches the grip vertically and drives its horizontal anchors.
            // Keep the hit rectangle still while only the visible wheel turns.
            handle.sizeDelta = new Vector2(Diameter, 0f);
            handle.anchoredPosition = Vector2.zero;
            handle.localRotation = Quaternion.identity;
            Image oldGrip = handle.GetComponent<Image>();
            if (oldGrip != null)
            {
                oldGrip.color = Color.clear;
                oldGrip.raycastTarget = true;
            }
            RectTransform fillArea = slider.fillRect == null ? null : slider.fillRect.parent as RectTransform;
            if (fillArea != null)
            {
                fillArea.sizeDelta = new Vector2(travel, 8f);
                fillArea.anchoredPosition = Vector2.zero;
            }
            artwork = handle.Find(ArtworkName) as RectTransform;
            if (artwork == null)
            {
                var host = new GameObject(ArtworkName, typeof(RectTransform), typeof(NavigationHelmGraphic));
                host.layer = gameObject.layer;
                artwork = (RectTransform)host.transform;
                artwork.SetParent(handle, false);
            }
            artwork.anchorMin = artwork.anchorMax = artwork.pivot = Vector2.one * .5f;
            artwork.sizeDelta = Vector2.one * Diameter;
            artwork.anchoredPosition = Vector2.zero;
            var helm = artwork.GetComponent<NavigationHelmGraphic>();
            helm.color = Color.white;
            helm.raycastTarget = false;
            if (slider.targetGraphic != helm)
            {
                slider.targetGraphic = helm;
                ColorBlock colors = slider.colors;
                Color tint = slider.IsInteractable() ? colors.normalColor : colors.disabledColor;
                helm.CrossFadeColor(tint * colors.colorMultiplier, 0f, true, true);
            }
            RefreshRotation();
        }

        private void LateUpdate() => RefreshRotation();

        private void RefreshRotation()
        {
            if (slider == null || artwork == null) return;
            artwork.localRotation = Quaternion.Euler(0f, 0f, -180f * slider.normalizedValue);
        }
    }
}
