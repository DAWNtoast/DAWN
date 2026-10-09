using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Low-key pulse used only for rare or goal-bearing voyage nodes.</summary>
    public sealed class VoyageEventMarkerPulse : MonoBehaviour
    {
        private RectTransform rect;
        private Image image;
        private Vector3 originScale;
        private Color originColor;
        private float phase;
        private float amplitude = 0.08f;

        public void Configure(float pulsePhase, bool treasure)
        {
            rect = GetComponent<RectTransform>();
            image = GetComponent<Image>();
            if (rect == null || image == null) return;
            originScale = rect.localScale;
            originColor = image.color;
            phase = pulsePhase;
            amplitude = treasure ? 0.11f : 0.075f;
        }

        private void Update()
        {
            if (rect == null || image == null) return;
            float wave = (Mathf.Sin(Time.unscaledTime * 2.8f + phase) + 1f) * 0.5f;
            rect.localScale = originScale * (1f + wave * amplitude);
            image.color = new Color(originColor.r, originColor.g, originColor.b,
                Mathf.Lerp(originColor.a * 0.78f, originColor.a, wave));
        }

        private void OnDisable()
        {
            if (rect != null) rect.localScale = originScale;
            if (image != null) image.color = originColor;
        }
    }
}
