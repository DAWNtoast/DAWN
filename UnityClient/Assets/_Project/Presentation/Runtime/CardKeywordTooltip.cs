using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Short glossary entries beside the inspected card, independent of its fan pose.</summary>
    [DisallowMultipleComponent]
    public sealed class CardKeywordTooltip : MonoBehaviour
    {
        public const float Width = 320f;
        public const float CardGap = 16f;
        private const float Padding = 18f;
        private const float EntryGap = 10f;
        private readonly List<Entry> rows = new List<Entry>();
        private IReadOnlyList<RectTransform> companionCards;
        private RectTransform card, rect, viewport;
        private float contentHeight;
        private bool allowRightFallback;

        private sealed class Entry
        {
            public RectTransform Panel;
            public Text Title, Body;
        }

        public static RectTransform Create(RectTransform card, string name,
            IReadOnlyList<CoreBattleCardKeyword> entries, Font font, bool allowRightFallback = false)
        {
            if (card == null || entries == null || entries.Count == 0) return null;
            var host = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(CardKeywordTooltip));
            var rect = (RectTransform)host.transform;
            rect.SetParent(card, false);
            rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
            rect.pivot = new Vector2(0f, 1f);
            CanvasGroup group = host.GetComponent<CanvasGroup>();
            group.interactable = group.blocksRaycasts = false;
            var tooltip = host.GetComponent<CardKeywordTooltip>();
            tooltip.card = card;
            tooltip.rect = rect;
            tooltip.allowRightFallback = allowRightFallback;
            tooltip.SetEntries(entries, font);
            host.SetActive(false);
            return rect;
        }

        public void SetEntries(IReadOnlyList<CoreBattleCardKeyword> entries, Font font)
        {
            if (rect == null) rect = (RectTransform)transform;
            if (card == null) card = rect.parent as RectTransform;
            int count = entries == null ? 0 : entries.Count;
            float top = 0f;
            for (int i = 0; i < count; i++)
            {
                if (i == rows.Count) rows.Add(CreateEntry());
                Entry row = rows[i];
                row.Panel.gameObject.SetActive(true);
                row.Body.font = font != null ? font : GameTypography.BodyFont;
                row.Title.text = entries[i].Keyword;
                row.Body.text = entries[i].Meaning;
                float titleHeight = Mathf.Max(28f, Mathf.Ceil(row.Title.preferredHeight) + 4f);
                float bodyHeight = Mathf.Max(28f, Mathf.Ceil(row.Body.preferredHeight) + 4f);
                float height = Padding * 2f + titleHeight + 5f + bodyHeight;
                row.Panel.anchoredPosition = new Vector2(0f, -top);
                row.Panel.sizeDelta = new Vector2(Width, height);
                row.Title.rectTransform.anchoredPosition = new Vector2(Padding, -Padding);
                row.Title.rectTransform.sizeDelta = new Vector2(Width - Padding * 2f, titleHeight);
                row.Body.rectTransform.anchoredPosition = new Vector2(Padding, -Padding - titleHeight - 5f);
                row.Body.rectTransform.sizeDelta = new Vector2(Width - Padding * 2f, bodyHeight);
                top += height + EntryGap;
            }
            for (int i = count; i < rows.Count; i++) rows[i].Panel.gameObject.SetActive(false);
            contentHeight = Mathf.Max(0f, top - EntryGap);
            rect.sizeDelta = new Vector2(Width, contentHeight);
            if (count == 0) gameObject.SetActive(false);
            RefreshPosition();
        }

        private Entry CreateEntry()
        {
            // Construct the dark inset directly: reading-panel skins would replace it with parchment.
            var host = new GameObject("Keyword Entry", typeof(RectTransform), typeof(Image), typeof(Outline));
            var panel = (RectTransform)host.transform;
            panel.SetParent(rect, false);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 1f);
            Image surface = host.GetComponent<Image>();
            surface.color = new Color(.055f, .075f, .075f, .98f);
            surface.raycastTarget = false;
            Outline rim = host.GetComponent<Outline>();
            rim.effectColor = new Color(.63f, .61f, .49f, 1f);
            rim.effectDistance = new Vector2(2f, -2f);
            return new Entry
            {
                Panel = panel,
                Title = CreateLabel(panel, "Keyword Bubble Title", 23, FontStyle.Bold, new Color(.96f, .84f, .59f)),
                Body = CreateLabel(panel, "Keyword Bubble Text", 21, FontStyle.Normal, new Color(.95f, .94f, .87f))
            };
        }

        private static Text CreateLabel(RectTransform panel, string name, int size, FontStyle style, Color color)
        {
            var host = new GameObject(name, typeof(RectTransform), typeof(Text));
            Text label = host.GetComponent<Text>();
            label.rectTransform.SetParent(panel, false);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(0f, 1f);
            label.rectTransform.sizeDelta = new Vector2(Width - Padding * 2f, 28f);
            GameTypography.Apply(label, size, style);
            label.color = color;
            label.alignment = TextAnchor.UpperLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            // Heights are measured above. Preserve the final glyph row when a nested canvas
            // rounds its scaled line height differently during collection hover or capture.
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = false;
            label.resizeTextForBestFit = false;
            label.raycastTarget = false;
            return label;
        }

        private void OnEnable() => RefreshPosition();
        private void LateUpdate() => RefreshPosition();

        public void SetCompanionCards(IReadOnlyList<RectTransform> cards) => companionCards = cards;

        public void RefreshPosition()
        {
            if (rect == null || card == null) return;
            if (viewport == null) viewport = FixedGameViewport.Bounds(card.GetComponentInParent<Canvas>());
            if (viewport == null) return;
            float fit = Mathf.Min(1f, (viewport.rect.height - 32f) / Mathf.Max(1f, contentHeight));
            // Cancel card enlargement/rotation so glossary text stays upright and readable.
            rect.rotation = viewport.rotation;
            Vector3 parentScale = card.lossyScale;
            Vector3 screenScale = viewport.lossyScale;
            rect.localScale = new Vector3(screenScale.x / Mathf.Max(.001f, parentScale.x) * fit,
                screenScale.y / Mathf.Max(.001f, parentScale.y) * fit, 1f);
            Rect frame = VisualBoundsInViewport(card);
            float left = frame.xMin, right = frame.xMax, top = frame.yMax;
            // Comparison cards must remain readable together. Place the glossary
            // beside their combined footprint instead of over the earlier rank.
            if (companionCards != null)
                foreach (RectTransform companion in companionCards)
                {
                    if (companion == null || !companion.gameObject.activeInHierarchy) continue;
                    Rect other = VisualBoundsInViewport(companion);
                    left = Mathf.Min(left, other.xMin);
                    right = Mathf.Max(right, other.xMax);
                }
            float x = left - CardGap - Width * fit;
            if (allowRightFallback && x < viewport.rect.xMin + 8f)
                x = right + CardGap;
            x = Mathf.Clamp(x, viewport.rect.xMin + 8f, viewport.rect.xMax - Width * fit - 8f);
            float y = Mathf.Clamp(top, viewport.rect.yMin + contentHeight * fit + 8f, viewport.rect.yMax - 8f);
            rect.position = viewport.TransformPoint(new Vector3(x, y, 0f));
        }

        private Rect VisualBoundsInViewport(RectTransform face)
        {
            Rect frame = CardFaceLayout.VisualBounds;
            Vector2 minimum = Vector2.positiveInfinity, maximum = Vector2.negativeInfinity;
            for (int i = 0; i < 4; i++)
            {
                Vector3 corner = new Vector3((i & 1) == 0 ? frame.xMin : frame.xMax,
                    (i & 2) == 0 ? frame.yMin : frame.yMax, 0f);
                Vector2 point = viewport.InverseTransformPoint(face.TransformPoint(corner));
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }
            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }
    }
}
