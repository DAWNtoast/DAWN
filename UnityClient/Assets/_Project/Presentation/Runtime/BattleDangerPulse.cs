using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>One short border pulse when health crosses into danger; repainting low health never replays it.</summary>
    public sealed class BattleDangerPulse : MonoBehaviour
    {
        public const float Threshold = .25f, Duration = .85f;
        private BattleHudChangeGraphic graphic;
        private bool initialized, below;
        private float elapsed;
        private Behaviour owner;
        public int PlayCount { get; private set; }
        public bool IsPlaying { get; private set; }
        public void Bind(Behaviour value) => owner = value;
        public void SetBaseline(float health, float maximum)
        { initialized = true; below = maximum > 0f && health / maximum <= Threshold; Cancel(); }
        public void Observe(float health, float maximum)
        {
            if (!initialized) { SetBaseline(health, maximum); return; }
            bool danger = health > 0f && maximum > 0f && health / maximum <= Threshold;
            if (danger && !below && isActiveAndEnabled && Application.isPlaying)
            { EnsureGraphic(); IsPlaying = true; elapsed = 0f; PlayCount++; graphic.enabled = true; Sample(); }
            if (!danger) Cancel();
            below = maximum > 0f && health / maximum <= Threshold;
        }
        private void EnsureGraphic()
        {
            if (graphic != null) return;
            var rect = (RectTransform)new GameObject("Low Health Border Pulse", typeof(RectTransform)).transform;
            rect.SetParent(transform, false); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-8f,-7f); rect.offsetMax = new Vector2(8f,7f);
            graphic = rect.gameObject.AddComponent<BattleHudChangeGraphic>(); graphic.raycastTarget = false;
        }
        public void AdvancePresentation(float seconds)
        {
            if (!IsPlaying) return;
            if (owner != null && !owner.isActiveAndEnabled) { Cancel(); return; }
            if (float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            elapsed += Mathf.Max(0f, seconds);
            if (elapsed >= Duration) { Cancel(); return; }
            Sample();
        }
        private void Sample()
        {
            float t = Mathf.Clamp01(elapsed / Duration);
            // Two soft breaths rather than screen flashes. Never covers the live value.
            graphic.color = new Color(1f,.3f,.22f, (.25f + .5f * Mathf.Pow(Mathf.Sin(t*Mathf.PI*2f),2f)) * (1f-t));
        }
        public void Cancel() { IsPlaying = false; if (graphic != null) graphic.enabled = false; }
        private void Update() => AdvancePresentation(Time.unscaledDeltaTime);
        private void OnDisable() => Cancel();
        private void OnApplicationPause(bool paused) { if (paused) Cancel(); }
        private void OnApplicationFocus(bool focused) { if (!focused) Cancel(); }
    }
}
