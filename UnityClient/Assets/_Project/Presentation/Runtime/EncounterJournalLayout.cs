using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Readable journal geometry for the full-height voyage event panel.</summary>
    public static class EncounterJournalLayout
    {
        private const float RowGap = 10f;
        private const float ScrollbarGutter = 20f;

        public static void Apply(RectTransform card)
        {
            if (card == null) return;
            Box(Child(card, "Encounter Parchment"), card.sizeDelta, Vector2.zero);
            RectTransform content = Child(card, "Encounter Paper Content");
            if (content == null) return;
            // Match the market drawer's paper and framed control spacing.
            Vector2 size = card.sizeDelta - new Vector2(72f, 72f);
            Box(content, size, Vector2.zero);
            float top = size.y * .5f, bottom = -top;

            RectTransform title = Child(content, "Encounter Title");
            float titleTop = top;
            float titleHeight = Measure(title, 32, size.x, 40f);
            TextBox(title, new Vector2(size.x, titleHeight), new Vector2(0f, titleTop - titleHeight * .5f), 32);

            float dividerY = titleTop - titleHeight - 10f;
            Box(Child(content, "Encounter Header Divider"), new Vector2(size.x, 1f), new Vector2(0f, dividerY));
            float heroTop = dividerY - 14f;
            // Give the prose at least half of the narrow column, while keeping
            // the painting's inner 4:3 composition and room for the decisions.
            const float artWidth = 256f, artHeight = 194f;
            float artCenter = -size.x * .5f + artWidth * .5f;
            RectTransform mount = Child(content, "Encounter Illustration Mount");
            Box(mount, new Vector2(artWidth, artHeight), new Vector2(artCenter, heroTop - artHeight * .5f));
            Box(Child(mount, "Illustration Border"), new Vector2(artWidth, artHeight), Vector2.zero);
            Box(Child(mount, "Illustration Mat"), new Vector2(artWidth - 4f, artHeight - 4f), Vector2.zero);
            RectTransform artRect = Child(mount, "Demo Event Art");
            RawImage art = artRect == null ? null : artRect.GetComponent<RawImage>();
            Vector2 artSize = new Vector2(artWidth - 8f, artHeight - 8f);
            if (art != null && SpecialWarshipArtCatalog.IsSpecialTexture(art.texture))
                artSize = UiImageAspect.FitSize(art.texture, art.uvRect, artSize);
            Box(artRect, artSize, Vector2.zero);

            float narrativeWidth = size.x - artWidth - 20f;
            float narrativeCenter = size.x * .5f - narrativeWidth * .5f;
            float narrativeBottom = heroTop - artHeight;
            LayoutNarrative(content, narrativeWidth, narrativeCenter, heroTop, narrativeBottom);

            float decisionTop = narrativeBottom - 16f;
            Box(Child(content, "Encounter Decision Rule"), new Vector2(size.x, 1f), new Vector2(0f, decisionTop));

            float secondaryTop = LayoutSecondaryActions(content, size.x, bottom);
            LayoutDecisions(content, size.x, decisionTop - 12f, secondaryTop + 12f);
        }

        private static void LayoutNarrative(RectTransform content, float width, float center, float top, float bottom)
        {
            RectTransform viewport = Child(content, "Encounter Description Viewport");
            RectTransform body = Child(viewport, "Encounter Description Content");
            RectTransform description = Child(body, "Encounter Description");
            if (viewport == null || body == null || description == null) return;
            ScrollRect scroll = viewport.GetComponent<ScrollRect>();
            float normalized = ScrollPosition(scroll, body);
            // The measured paragraph box remains top-anchored. Center its
            // glyphs inside that box so CJK ascenders get clearance at the mask.
            description.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;
            float textHeight = Measure(description, 22, width, 26f);
            float availableHeight = Mathf.Max(40f, top - bottom);
            bool overflow = textHeight > availableHeight + .5f;
            float viewportHeight = availableHeight;
            Box(viewport, new Vector2(width, viewportHeight), new Vector2(center, top - viewportHeight * .5f));
            TopContent(body, new Vector2(width, Mathf.Max(viewportHeight, textHeight)));
            description.anchorMin = description.anchorMax = new Vector2(.5f, 1f);
            description.pivot = new Vector2(.5f, 1f);
            TextBox(description, new Vector2(width, textHeight), Vector2.zero, 22);
            if (scroll == null) return;
            scroll.horizontal = false;
            scroll.vertical = overflow;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.verticalNormalizedPosition = overflow ? Mathf.Clamp01(normalized) : 1f;
        }

        private static float LayoutSecondaryActions(RectTransform content, float width, float bottom)
        {
            RectTransform root = Child(content, "Encounter Secondary Actions");
            var buttons = new List<RectTransform>();
            if (root != null)
                foreach (Transform child in root)
                    if (child.gameObject.activeSelf && child.GetComponent<Button>() != null) buttons.Add((RectTransform)child);
            if (buttons.Count == 0)
            {
                Box(root, new Vector2(width, 0f), new Vector2(0f, bottom));
                return bottom;
            }
            // A rescue action is uncommon but must remain readable alongside
            // skip. Stack both in the narrow column instead of halving labels.
            float height = 0f;
            foreach (RectTransform row in buttons) height += MeasureRow(row, width, true) + 8f;
            height -= 8f;
            float rowBottom = bottom;
            Box(root, new Vector2(width, height), new Vector2(0f, rowBottom + height * .5f));
            float cursor = height * .5f;
            for (int index = 0; index < buttons.Count; index++)
            {
                RectTransform row = buttons[index];
                float rowHeight = MeasureRow(row, width, true);
                row.anchorMin = row.anchorMax = row.pivot = Vector2.one * .5f;
                Box(row, new Vector2(width, rowHeight), new Vector2(0f, cursor - rowHeight * .5f));
                LayoutRow(row, width, rowHeight, true);
                cursor -= rowHeight + 8f;
            }
            return rowBottom + height;
        }

        private static void LayoutDecisions(RectTransform content, float width, float top, float bottom)
        {
            RectTransform viewport = Child(content, "Encounter Decision Viewport");
            RectTransform actions = Child(viewport, "Encounter Choices");
            if (viewport == null || actions == null) return;
            ScrollRect scroll = viewport.GetComponent<ScrollRect>();
            float normalized = ScrollPosition(scroll, actions);
            float viewportHeight = Mathf.Max(100f, top - bottom);
            // Anchor the minimum readable viewport above the fixed actions.
            // An unusually tall footer must never push it into those controls.
            top = bottom + viewportHeight;
            // Measure at full width first so a previously visible scrollbar
            // cannot leave an empty gutter after shorter choices are shown.
            bool overflow = MeasureChoices(actions, width) > viewportHeight + .5f;
            float gutter = overflow ? ScrollbarGutter : 0f;
            float rowWidth = width - gutter;
            Box(viewport, new Vector2(rowWidth, viewportHeight), new Vector2(-gutter * .5f, top - viewportHeight * .5f));
            float cursor = 0f;
            foreach (Transform child in actions)
            {
                if (!child.gameObject.activeSelf || child.GetComponent<Button>() == null) continue;
                RectTransform row = (RectTransform)child;
                float height = MeasureRow(row, rowWidth, false);
                row.anchorMin = row.anchorMax = new Vector2(.5f, 1f);
                row.pivot = Vector2.one * .5f;
                Box(row, new Vector2(rowWidth, height), new Vector2(0f, -cursor - height * .5f));
                LayoutRow(row, rowWidth, height, false);
                cursor += height + RowGap;
            }
            float needed = Mathf.Max(0f, cursor - RowGap);
            TopContent(actions, new Vector2(rowWidth, Mathf.Max(viewportHeight, needed)));
            overflow = needed > viewportHeight + .5f;
            RectTransform barRect = Child(content, "Encounter Decision Scrollbar");
            Box(barRect, new Vector2(12f, viewportHeight), new Vector2(width * .5f - 6f, top - viewportHeight * .5f));
            Scrollbar bar = barRect == null ? null : barRect.GetComponent<Scrollbar>();
            if (bar != null)
            {
                bar.direction = Scrollbar.Direction.BottomToTop;
                bar.size = needed <= 0f ? 1f : Mathf.Clamp01(viewportHeight / needed);
                bar.gameObject.SetActive(overflow);
            }
            if (scroll != null)
            {
                scroll.horizontal = false;
                scroll.vertical = overflow;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.verticalNormalizedPosition = overflow ? Mathf.Clamp01(normalized) : 1f;
                if (bar != null) bar.SetValueWithoutNotify(scroll.verticalNormalizedPosition);
            }
        }

        private static float MeasureChoices(RectTransform actions, float width)
        {
            float height = 0f;
            foreach (Transform child in actions)
                if (child.gameObject.activeSelf && child.GetComponent<Button>() != null)
                    height += MeasureRow((RectTransform)child, width, false) + RowGap;
            return Mathf.Max(0f, height - RowGap);
        }

        private static float MeasureRow(RectTransform row, float width, bool secondary)
        {
            float labelHeight = Measure(Child(row, "Label"), 24, width - 186f, 30f);
            float hoursHeight = Measure(Child(row, "Hours"), 20, 100f, secondary ? 26f : 42f);
            float detailHeight = Measure(Child(row, "Detail"), 20, width - 36f, 0f);
            float requirementHeight = Measure(Child(row, "Requirement"), 20, width - 36f, 0f);
            float height = Mathf.Max(labelHeight, hoursHeight) + (detailHeight > 0f ? detailHeight + 6f : 0f) +
                (requirementHeight > 0f ? requirementHeight + 6f : 0f) + (secondary ? 24f : 28f);
            return Mathf.Max(secondary ? 56f : 104f, height);
        }

        private static void LayoutRow(RectTransform row, float width, float height, bool secondary)
        {
            float labelWidth = width - 186f;
            float labelCenter = -width * .5f + 54f + labelWidth * .5f;
            float bodyWidth = width - 36f;
            RectTransform label = Child(row, "Label"), detail = Child(row, "Detail"), requirement = Child(row, "Requirement");
            float labelHeight = Measure(label, 24, labelWidth, 30f);
            float hoursHeight = Measure(Child(row, "Hours"), 20, 100f, secondary ? 26f : 42f);
            float headingHeight = Mathf.Max(labelHeight, hoursHeight);
            float detailHeight = Measure(detail, 20, bodyWidth, 0f);
            float requirementHeight = Measure(requirement, 20, bodyWidth, 0f);
            float contentHeight = headingHeight + (detailHeight > 0f ? detailHeight + 6f : 0f) +
                (requirementHeight > 0f ? requirementHeight + 6f : 0f);
            float cursor = secondary ? contentHeight * .5f : height * .5f - 14f;
            TextBox(label, new Vector2(labelWidth, labelHeight), new Vector2(labelCenter, cursor - labelHeight * .5f), 24);
            float timeCenter = width * .5f - 66f;
            TextBox(Child(row, "Hours"), new Vector2(100f, hoursHeight), new Vector2(timeCenter, cursor - hoursHeight * .5f), 20);
            Box(Child(row, "Time Rule"), new Vector2(1f, headingHeight), new Vector2(width * .5f - 124f, cursor - headingHeight * .5f));
            TextBox(Child(row, "Decision Number"), new Vector2(30f, 30f),
                new Vector2(-width * .5f + 26f, cursor - 15f), 20);
            cursor -= headingHeight;
            // Details and requirements use the complete row width beneath the
            // title/time band, keeping long resource and crew text readable.
            if (detailHeight > 0f) cursor -= 6f;
            TextBox(detail, new Vector2(bodyWidth, detailHeight), new Vector2(0f, cursor - detailHeight * .5f), 20);
            cursor -= detailHeight;
            if (requirementHeight > 0f) cursor -= 6f;
            TextBox(requirement, new Vector2(bodyWidth, requirementHeight), new Vector2(0f, cursor - requirementHeight * .5f), 20);
            Box(Child(row, "Choice Accent"), new Vector2(4f, height - 12f), new Vector2(-width * .5f + 3f, 0f));
        }

        private static float Measure(RectTransform rect, int size, float width, float minimum)
        {
            Text text = rect == null ? null : rect.GetComponent<Text>();
            if (text == null || string.IsNullOrEmpty(text.text)) return minimum;
            Font(text, size);
            rect.sizeDelta = new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, minimum));
            float height = Mathf.Max(minimum, Mathf.Ceil(text.preferredHeight) + 2f);
            // The CJK font's painted glyphs extend beyond preferredHeight.
            // Measure the actual alignment at the live and reference scales;
            // centered text needs clearance on both sides of its baseline.
            TextGenerationSettings settings = text.GetGenerationSettings(rect.rect.size);
            settings.generateOutOfBounds = true;
            using (var generator = new TextGenerator())
                foreach (float scale in new[] { Mathf.Max(.01f, settings.scaleFactor), 2f / 3f, 1f })
                {
                    settings.scaleFactor = scale;
                    for (int pass = 0; pass < 3; pass++)
                    {
                        settings.generationExtents = new Vector2(Mathf.Max(1f, width), height);
                        generator.Populate(text.text, settings);
                        float low = -settings.pivot.y * height, high = low + height;
                        float overflow = 0f;
                        foreach (UIVertex vertex in generator.verts)
                        {
                            float y = vertex.position.y / scale;
                            overflow = Mathf.Max(overflow, low - y, y - high);
                        }
                        if (overflow <= .01f) break;
                        height += Mathf.Ceil(overflow * 2f) + 2f;
                    }
                }
            return Mathf.Ceil(height);
        }

        private static void TextBox(RectTransform rect, Vector2 size, Vector2 position, int fontSize)
        {
            Box(rect, size, position);
            Text text = rect == null ? null : rect.GetComponent<Text>();
            if (text != null) Font(text, fontSize);
        }

        private static void Font(Text text, int size)
        {
            text.fontSize = size;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            // Allocated bounds include the glyph mesh; only the explicit
            // narrative and decision viewports should clip scrolling content.
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
        }

        private static void TopContent(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1f);
            Box(rect, size, rect.anchoredPosition);
        }

        private static float ScrollPosition(ScrollRect scroll, RectTransform content)
        {
            // A new content rect can temporarily fit its authored viewport,
            // whose normalized getter then reports zero. Its top-anchored
            // position is authoritative until the player has scrolled it.
            if (scroll == null || content == null || content.anchoredPosition.y <= .5f) return 1f;
            return Mathf.Clamp01(scroll.verticalNormalizedPosition);
        }

        private static void Box(RectTransform rect, Vector2 size, Vector2 position)
        {
            if (rect == null) return;
            if (rect.sizeDelta != size) rect.sizeDelta = size;
            if (rect.anchoredPosition != position) rect.anchoredPosition = position;
        }

        private static RectTransform Child(Transform parent, string name)
        {
            if (parent == null) return null;
            // The scrollbar can be inactive when every choice fits. Keep it
            // addressable so a later resize can show it again.
            for (int index = parent.childCount - 1; index >= 0; index--)
                if (parent.GetChild(index).name == name) return parent.GetChild(index) as RectTransform;
            return null;
        }
    }
}
