using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>A short, noninteractive receipt of one committed encounter. No actions or skip controls.</summary>
    public sealed class VoyageOutcomeMotion : MonoBehaviour
    {
        private readonly List<RectTransform> rows = new List<RectTransform>();
        private readonly List<CanvasGroup> groups = new List<CanvasGroup>();
        private readonly List<BattleFxGraphic> shines = new List<BattleFxGraphic>();
        private readonly List<Vector2> anchors = new List<Vector2>();
        private Behaviour owner;
        private bool paused;
        public float Elapsed { get; private set; }
        public float Duration { get; private set; }
        public bool IsPlaying { get; private set; }
        public int EntryCount => rows.Count;

        public void Play(Behaviour controller, Font font, IReadOnlyList<VoyageOutcomeEntry> entries)
        {
            if (entries == null || entries.Count == 0) { gameObject.SetActive(false); return; }
            owner = controller;
            CanvasGroup rootGroup = gameObject.AddComponent<CanvasGroup>();
            rootGroup.blocksRaycasts = false; rootGroup.interactable = false;
            int columns = Mathf.Min(4, entries.Count);
            Duration = 1.45f + Mathf.Min(entries.Count - 1, 7) * .055f;
            for (int i = 0; i < entries.Count; i++)
            {
                VoyageOutcomeEntry entry = entries[i];
                bool danger = entry.Icon == "curse" ? entry.Amount > 0 : entry.Amount < 0;
                Color tint = danger ? new Color(1f, .62f, .40f) : new Color(.66f, .94f, .80f);
                var anchor = new Vector2((i % columns - (columns - 1) * .5f) * 224f,
                    (Mathf.CeilToInt(entries.Count / (float)columns) - 1) * 36f - i / columns * 72f);
                RectTransform row = MakeRect("Outcome " + entry.Icon + " " + i, transform, new Vector2(214f, 62f), anchor);
                rows.Add(row); anchors.Add(anchor);
                CanvasGroup group = row.gameObject.AddComponent<CanvasGroup>(); groups.Add(group);
                Image backing = row.gameObject.AddComponent<Image>(); backing.color = new Color(.04f, .09f, .10f, .95f); backing.raycastTarget = false;
                Outline edge = row.gameObject.AddComponent<Outline>(); edge.effectColor = new Color(tint.r, tint.g, tint.b, .30f); edge.effectDistance = new Vector2(1f, -1f);
                RectTransform shineRect = MakeRect("Outcome Sweep", row, row.sizeDelta, Vector2.zero);
                BattleFxGraphic shine = shineRect.gameObject.AddComponent<BattleFxGraphic>();
                shine.Configure(BattleFxKind.CardShine, new Color(tint.r, tint.g, tint.b, .25f), i); shines.Add(shine);
                ItemIconArt art = entry.Icon == "gold" ? ItemIconCatalog.ForSpecial("gold") : ItemIconCatalog.ForCargo(entry.Icon);
                bool hasIcon = art.IsAvailable;
                if (hasIcon)
                {
                    RawImage icon = MakeRect("Item", row, new Vector2(44f, 44f), new Vector2(-76f, 0f)).gameObject.AddComponent<RawImage>();
                    icon.texture = art.Texture; icon.uvRect = art.Uv; icon.raycastTarget = false;
                    UiImageAspect.Fit(icon, new Vector2(44f, 44f));
                }
                Text label = MakeRect("Amount", row, new Vector2(hasIcon ? 144f : 198f, 54f),
                    new Vector2(hasIcon ? 27f : 0f, 0f)).gameObject.AddComponent<Text>();
                label.font = font; label.fontSize = 22; label.alignment = TextAnchor.MiddleCenter;
                label.color = tint; label.raycastTarget = false;
                label.resizeTextForBestFit = true; label.resizeTextMinSize = 17; label.resizeTextMaxSize = 22;
                label.text = entry.Name + (entry.Amount == 0 ? string.Empty : "\n" +
                    (entry.Amount > 0 ? "+" : "−") + System.Math.Abs((long)entry.Amount).ToString(CultureInfo.InvariantCulture));
            }
            IsPlaying = true; SetElapsed(0f);
        }

        private void Update()
        {
            if (!IsPlaying || paused) return;
            if (owner == null || !owner.isActiveAndEnabled) { Finish(); return; }
            SetElapsed(Elapsed + Mathf.Min(.05f, Time.unscaledDeltaTime));
        }

        public void SetElapsed(float elapsed)
        {
            Elapsed = Mathf.Clamp(elapsed, 0f, Duration);
            if (Elapsed >= Duration) { Finish(); return; }
            for (int i = 0; i < rows.Count; i++)
            {
                float local = Elapsed - Mathf.Min(i, 7) * .055f;
                float enter = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(local / .22f));
                float exit = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Elapsed - Duration + .30f) / .30f));
                groups[i].alpha = enter * (1f - exit);
                rows[i].anchoredPosition = anchors[i] + Vector2.down * (16f * (1f - enter)) + Vector2.up * (12f * exit);
                rows[i].localScale = Vector3.one * Mathf.Lerp(.96f, 1f, enter);
                shines[i].SetProgress(Mathf.Clamp01(local / .64f));
            }
        }

        private void Finish() { IsPlaying = false; gameObject.SetActive(false); if (Application.isPlaying) Destroy(gameObject); }
        private void OnDisable()
        {
            IsPlaying = false;
            CanvasGroup group = GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 0f;
            if (Application.isPlaying && Duration > 0f) Destroy(gameObject);
        }
        private void OnApplicationPause(bool value) => paused = value;
        private static RectTransform MakeRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            RectTransform rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
    }
}
