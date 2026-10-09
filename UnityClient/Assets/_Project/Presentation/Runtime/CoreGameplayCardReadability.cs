using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        // The complete paragraph remains the Text source. Only its viewport is
        // bounded, so choosing a larger font never discards the final effects.
        private static ScrollRect ReadableParagraph(Text text, Transform owner, Vector2 size,
            Vector2 position, int standardSize, bool interactive)
        {
            string name = text.name + " Viewport";
            RectTransform viewport = owner.Find(name) as RectTransform;
            if (viewport == null)
            {
                viewport = new GameObject(name, typeof(RectTransform), typeof(RectMask2D), typeof(Image), typeof(ScrollRect)).GetComponent<RectTransform>();
                viewport.SetParent(owner, false);
                Image hit = viewport.GetComponent<Image>();
                hit.color = Color.clear;
                hit.raycastTarget = interactive;
                RectTransform content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
                content.SetParent(viewport, false);
                content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, 1f);
                text.transform.SetParent(content, false);
                ScrollRect created = viewport.GetComponent<ScrollRect>();
                created.viewport = viewport;
                created.content = content;
                created.horizontal = false;
                created.vertical = true;
                created.movementType = ScrollRect.MovementType.Clamped;
                created.inertia = false;
                created.scrollSensitivity = 44f;
                if (interactive)
                {
                    RectTransform track = new GameObject(name + " Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar)).GetComponent<RectTransform>();
                    track.SetParent(viewport, false);
                    track.anchorMin = new Vector2(1f, 0f); track.anchorMax = Vector2.one;
                    track.sizeDelta = new Vector2(8f, 0f); track.anchoredPosition = new Vector2(-4f, 0f);
                    track.GetComponent<Image>().color = new Color(.2f, .13f, .08f, .15f);
                    RectTransform handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                    handle.SetParent(track, false);
                    handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one; handle.sizeDelta = Vector2.zero;
                    handle.GetComponent<Image>().color = new Color(.2f, .13f, .08f, .65f);
                    Scrollbar bar = track.GetComponent<Scrollbar>();
                    bar.handleRect = handle; bar.targetGraphic = handle.GetComponent<Image>();
                    bar.direction = Scrollbar.Direction.BottomToTop;
                    bar.SetValueWithoutNotify(1f);
                    created.verticalScrollbar = bar;
                }
            }
            viewport.sizeDelta = size;
            viewport.anchoredPosition = position;
            ScrollRect scroll = viewport.GetComponent<ScrollRect>();
            GameReadability.Body(text, standardSize);
            text.raycastTarget = false;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = new Vector2(.5f, 1f);
            text.rectTransform.sizeDelta = new Vector2(size.x - 16f, size.y);
            float height = MeasureCardTextHeight(text);
            text.rectTransform.sizeDelta = new Vector2(size.x - 16f, height);
            text.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            scroll.content.sizeDelta = new Vector2(size.x, Mathf.Max(size.y, height + 16f));
            scroll.content.anchoredPosition = Vector2.zero;
            if (scroll.verticalScrollbar != null)
                scroll.verticalScrollbar.gameObject.SetActive(height + 16f > size.y);
            return scroll;
        }

        private static void RefreshReadableParagraph(Text text)
        {
            if (text == null) return;
            ScrollRect scroll = text.GetComponentInParent<ScrollRect>();
            if (scroll == null || scroll.content == null) return;
            float height = MeasureCardTextHeight(text);
            scroll.content.sizeDelta = new Vector2(scroll.content.sizeDelta.x,
                Mathf.Max(scroll.viewport.rect.height, height + 16f));
            text.rectTransform.sizeDelta = new Vector2(text.rectTransform.sizeDelta.x, height);
            if (scroll.verticalScrollbar != null)
                scroll.verticalScrollbar.gameObject.SetActive(height + 16f > scroll.viewport.rect.height);
        }

        private static void ConfigureReadableCardBubble(RectTransform bubble, Text explanation, Text title, bool right)
        {
            if (bubble == null || explanation == null || title == null) return;
            // The card's hover canvas remains non-blocking. Its own wheel relay
            // scrolls this paragraph without creating a second click/drag target.
            bubble.sizeDelta = new Vector2(590f, 620f);
            bubble.anchoredPosition = new Vector2(right ? 390f : -390f, 185f);
            GameReadability.Body(title, 32);
            title.text = title.text.Replace("<size=18>", "<size=" + GameReadability.FontSize(18) + ">");
            float titleHeight = 76f * GameReadability.Scale;
            title.rectTransform.sizeDelta = new Vector2(558f, titleHeight);
            title.rectTransform.anchoredPosition = new Vector2(0f, 294f - titleHeight * .5f);
            title.alignment = TextAnchor.UpperLeft;
            float detailsTop = 282f - titleHeight;
            float detailsHeight = detailsTop + 294f;
            ScrollRect details = ReadableParagraph(explanation, bubble, new Vector2(558f, detailsHeight),
                new Vector2(0f, detailsTop - detailsHeight * .5f), 22, false);
            Transform pointer = bubble.Find("Keyword Bubble Pointer");
            if (pointer != null) pointer.gameObject.SetActive(false);
            Button card = bubble.GetComponentInParent<Button>();
            if (card != null)
            {
                BattleCardReadabilityScroll relay = card.GetComponent<BattleCardReadabilityScroll>();
                if (relay == null) relay = card.gameObject.AddComponent<BattleCardReadabilityScroll>();
                relay.Details = details;
                relay.Face = card.transform.Find("Label Viewport")?.GetComponent<ScrollRect>();
            }
        }

        private static Text ReadableCardLabel(Transform root, string name) =>
            root.GetComponentsInChildren<Text>(true).Single(text => text.name == name);

        private static int ReadableInspectionPageSize => GameReadability.Level == 2 ? 2 : 4;

        private static void LayoutReadableInspectionCard(Button card, int slot)
        {
            int columns = GameReadability.Level == 2 ? 1 : 2;
            float width = columns == 1 ? 730f : 356f;
            RectTransform box = card.GetComponent<RectTransform>();
            box.sizeDelta = new Vector2(width, 146f);
            box.anchoredPosition = new Vector2(-777f + width * .5f + slot % columns * (width + 18f), 25f - slot / columns * 158f);
            foreach (RawImage icon in card.GetComponentsInChildren<RawImage>())
            {
                icon.rectTransform.sizeDelta = new Vector2(56f, 56f);
                icon.rectTransform.anchoredPosition = new Vector2(-width * .5f + 38f, 0f);
            }
            foreach (Text text in card.GetComponentsInChildren<Text>())
            {
                if (string.IsNullOrEmpty(text.text)) continue;
                bool title = text.name.Contains("Name");
                GameReadability.Body(text, title ? 32 : 18);
                text.rectTransform.sizeDelta = new Vector2(width - 96f, title ? 88f : 42f);
                text.rectTransform.anchoredPosition = new Vector2(38f, title ? 18f : -49f);
            }
        }

        private static void StackReadableInspection(Transform owner, string name, Vector2 size, Vector2 position, string[] fields)
        {
            Text[] labels = fields.Select(field => owner.GetComponentsInChildren<Text>(true).FirstOrDefault(text => text.name == field))
                .Where(text => text != null).ToArray();
            if (labels.Length == 0) return;
            RectTransform viewport = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect)).GetComponent<RectTransform>();
            viewport.SetParent(owner, false); viewport.sizeDelta = size; viewport.anchoredPosition = position;
            Image hit = viewport.GetComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
            RectTransform content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false); content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, 1f);
            float top = 12f;
            foreach (Text label in labels)
            {
                ScrollRect oldScroll = label.GetComponentInParent<ScrollRect>();
                if (oldScroll != null) oldScroll.gameObject.SetActive(false);
                else if (label.transform.parent != owner) label.transform.parent.parent.gameObject.SetActive(false);
                label.transform.SetParent(content, false); label.gameObject.SetActive(true);
                int standard = label.name.Contains("Name") ? 32 : label.name.Contains("Source") || label.name.Contains("Stages") || label.name.Contains("Active") ? 20 : 22;
                GameReadability.Body(label, standard);
                label.alignment = TextAnchor.UpperLeft; label.verticalOverflow = VerticalWrapMode.Overflow;
                label.rectTransform.sizeDelta = new Vector2(size.x - 24f, size.y);
                float height = MeasureCardTextHeight(label);
                PlaceCardEffectParagraph(label, size.x - 24f, height, top); top += height + 12f;
            }
            content.sizeDelta = new Vector2(size.x, Mathf.Max(size.y, top)); content.anchoredPosition = Vector2.zero;
            ScrollRect scroll = viewport.GetComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 48f;
        }

        private static void StyleReadableInspectionControls(Transform panel)
        {
            foreach (Text label in panel.GetComponentsInChildren<Text>(true))
            {
                if (label.GetComponentInParent<ScrollRect>() != null || label.GetComponentInParent<Button>() != null) continue;
                if (label.name.Contains("Heading")) { GameReadability.Body(label, 32); label.rectTransform.sizeDelta = new Vector2(1550f, 44f * GameReadability.Scale); label.rectTransform.anchoredPosition = new Vector2(0f, 290f); }
                else if (label.name.Contains("Rule")) { GameReadability.Body(label, 18); label.rectTransform.sizeDelta = new Vector2(1550f, 38f * GameReadability.Scale); label.rectTransform.anchoredPosition = new Vector2(0f, 220f); }
                else { GameReadability.Body(label, label.name.Contains("Empty") ? 22 : 18); label.rectTransform.sizeDelta = new Vector2(label.rectTransform.sizeDelta.x, Mathf.Max(label.rectTransform.sizeDelta.y, label.preferredHeight + 8f)); }
            }
            foreach (Button button in panel.GetComponentsInChildren<Button>(true))
            {
                Text label = button.transform.Find("Label")?.GetComponent<Text>();
                if (label == null || string.IsNullOrEmpty(label.text)) continue;
                GameReadability.Body(label, 22);
                label.text = label.text.Replace("← ", "").Replace(" →", "");
                RectTransform box = button.GetComponent<RectTransform>();
                box.sizeDelta = new Vector2(Mathf.Max(box.sizeDelta.x, 136f), 44f * GameReadability.Scale);
                if (button.name.StartsWith("Special Card Filter") || button.name.StartsWith("Upgrade Stage")) box.anchoredPosition = new Vector2(box.anchoredPosition.x, 147f);
                label.rectTransform.sizeDelta = box.sizeDelta - new Vector2(16f, 8f);
            }
        }
    }
}
