using System.Collections.Generic;
using System.Text;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private static string TavernSentenceParagraphs(string value)
        {
            string text = (value ?? string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
            StringBuilder paragraphs = new StringBuilder(text.Length + 16);
            for (int index = 0; index < text.Length; index++)
            {
                char letter = text[index];
                paragraphs.Append(letter);
                if ("。！？".IndexOf(letter) < 0) continue;
                // Closing quotation marks belong to the sentence that precedes them.
                while (index + 1 < text.Length && "”’」』".IndexOf(text[index + 1]) >= 0)
                    paragraphs.Append(text[++index]);
                if (index + 1 < text.Length) paragraphs.Append("\n\n");
            }
            return paragraphs.ToString();
        }

        private static string TavernSkillParagraphs(string value) => (value ?? string.Empty)
            .Replace("\r", string.Empty).Replace("\n", string.Empty)
            .Replace("；", "；\n\n").Replace("，", "，\n");

        private Text CreateTavernReadingText(string name, RectTransform parent, Vector2 inset,
            Vector2 size, int fontSize, float lineSpacing, Color ink, string value)
        {
            Text label = DossierCardText(name, parent, size.x, size.y, Vector2.zero, fontSize, ink, value);
            label.lineSpacing = lineSpacing;
            PlaceTavernText(label, inset, size);
            float measured = MeasureTavernParagraph(label);
            if (measured > size.y && label.text.Contains("\n\n"))
            {
                // Preserve the wording and authored font size. Long accounts
                // first use a single paragraph break before needing a scroll.
                label.text = label.text.Replace("\n\n", "\n");
                measured = MeasureTavernParagraph(label);
            }
            if (measured <= size.y) return label;

            RectTransform viewport = CreateRect(name + " Viewport", parent, size, Vector2.zero);
            PlaceTavernRect(viewport, inset, size);
            RectTransform content = CreateRect(name + " Content", viewport, size, Vector2.zero);
            PlaceTavernRect(content, Vector2.zero, new Vector2(size.x, measured + 12f));
            label.transform.SetParent(content, false);
            PlaceTavernText(label, new Vector2(0f, 8f), new Vector2(size.x - 12f, measured));
            // Reserving the scrollbar gutter can add a wrapped line.
            measured = MeasureTavernParagraph(label);
            label.rectTransform.sizeDelta = new Vector2(size.x - 12f, measured);
            content.sizeDelta = new Vector2(size.x, measured + 12f);
            EnableTavernReadingScroll(name, viewport, content, ink);
            return label;
        }

        private static void PlaceTavernRect(RectTransform rect, Vector2 inset, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(inset.x, -inset.y);
            rect.sizeDelta = size;
        }

        private static void PlaceTavernText(Text label, Vector2 inset, Vector2 size)
        {
            PlaceTavernRect(label.rectTransform, inset, size);
            label.alignment = TextAnchor.UpperLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            // Paragraph bounds are measured from the actual glyph mesh. Only a
            // viewport may clip; Text must never silently drop the final line.
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.resizeTextForBestFit = false;
        }

        private static float MeasureTavernParagraph(Text label)
        {
            KeepComponentPunctuationWithSentence(label);
            TextGenerationSettings settings = label.GetGenerationSettings(label.rectTransform.rect.size);
            settings.fontSize = label.fontSize;
            settings.resizeTextForBestFit = false;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            settings.generateOutOfBounds = true;
            float height = label.preferredHeight;
            using (TextGenerator generator = new TextGenerator())
            {
                float liveScale = Mathf.Max(.01f, settings.scaleFactor);
                // CanvasScaler can apply its final size after this hierarchy is
                // built. Reserve the measured glyph bounds at the common small
                // preview sizes as well as the live and 1920-unit canvases.
                foreach (float scale in new[] { liveScale, 1f / 3f, .5f, 2f / 3f, 1f })
                {
                    settings.scaleFactor = scale;
                    height = Mathf.Max(height, generator.GetPreferredHeight(label.text, settings) / scale);
                    // GetPreferredHeight repopulates this generator with its
                    // own measurement settings. Restore actual rendering
                    // settings before inspecting the final row's glyph mesh.
                    generator.Populate(label.text, settings);
                    foreach (UIVertex vertex in generator.verts)
                        height = Mathf.Max(height, label.rectTransform.rect.yMax - vertex.position.y / scale);
                }
            }
            return Mathf.Ceil(height) + 4f;
        }

        private void EnableTavernReadingScroll(string name, RectTransform viewport, RectTransform content, Color ink)
        {
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hitArea = viewport.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 28f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.verticalNormalizedPosition = 1f;
            Vector2 size = viewport.rect.size;
            Image track = CreateImage(name + " Scrollbar", viewport, new Vector2(5f, size.y),
                new Vector2(size.x - 3f, -size.y * .5f), new Color(ink.r, ink.g, ink.b, .15f));
            // Both top-left and centered viewports are used by this layout.
            track.rectTransform.anchorMin = track.rectTransform.anchorMax = new Vector2(0f, 1f);
            RectTransform handleArea = CreateRect("Sliding Area", track.transform, new Vector2(5f, size.y), Vector2.zero);
            Image handle = CreateImage("Handle", handleArea, Vector2.zero, Vector2.zero,
                new Color(ink.r, ink.g, ink.b, .65f));
            Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
        }

        // Crew and component sheets share the reviewed reading column and footer clearance.
        private void BuildTavernDossierSpecialCard(RectTransform page, CoreSpecialCardDefinition definition,
            string emptyMessage, Color ink, Color muted)
        {
            BuildReadingDossierSpecialCard(page, "Tavern", definition, emptyMessage, ink, muted);
        }

        private void BuildReadingDossierSpecialCard(RectTransform page, string prefix, CoreSpecialCardDefinition definition,
            string emptyMessage, Color ink, Color muted)
        {
            if (definition == null)
            {
                CreateTavernReadingText(prefix + " Special Card Name", page, new Vector2(20f, 44f),
                    new Vector2(850f, 42f), 28, 1f, ink, "没有专属卡牌");
                if (!string.IsNullOrWhiteSpace(emptyMessage))
                    CreateTavernReadingText(prefix + " Special Card Empty", page, new Vector2(20f, 110f),
                        new Vector2(850f, page.rect.height - 166f), 26, 1.22f, muted, TavernSentenceParagraphs(emptyMessage));
                return;
            }

            int cardIndex = -1;
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
                if (CoreVoyageSession.CardLibrary[index].Id == definition.Card.Id) { cardIndex = index; break; }
            CoreBattleCard card = cardIndex < 0 ? definition.Card : session.GetBattleCard(cardIndex);
            // Reserve room for the whole ornate frame after raising the footer.
            float cardScale = Mathf.Min(.86f, (page.rect.height - 36f) / CardFaceLayout.FrameSize.y);
            RectTransform face = CreateCollectionCardFace(page, card, prefix + " Special Card",
                prefix + " Special Card Art", prefix + " Special Card Name", cardScale,
                effectDescription: SpecialCardPreviewEffectDescription(card), effectName: prefix + " Special Card Effect");
            face.anchoredPosition = new Vector2(-page.rect.width * .5f + 112f, 12f) -
                CardSchoolArtCatalog.FrameBounds(card).center * cardScale;

            List<string> stages = new List<string>();
            if (card.IsAllowedInStage(NavalBattleStage.Preparation)) stages.Add("策略");
            if (card.IsAllowedInStage(NavalBattleStage.Naval)) stages.Add("远程攻击");
            if (card.IsAllowedInStage(NavalBattleStage.Boarding)) stages.Add("近战");
            DossierCardText(prefix + " Special Card Stage", page, 580f, 32f,
                new Vector2(75f, page.rect.height * .5f - 16f), 22, muted, string.Join(" / ", stages));

            // Reuse the component growth-hint space for effects while keeping the top edge fixed.
            bool component = prefix == "Component";
            RectTransform viewport = CreateRect(prefix + " Special Card Details Viewport", page,
                new Vector2(580f, page.rect.height - (component ? 80f : 144f)), new Vector2(75f, component ? -3f : 29f));
            RectTransform content = CreateRect(prefix + " Special Card Details Content", viewport,
                viewport.rect.size, Vector2.zero);
            PlaceTavernRect(content, Vector2.zero, viewport.rect.size);
            bool boarding = card.IsAllowedInStage(NavalBattleStage.Boarding);
            float top = 12f;
            if (boarding)
                top = AddTavernCardParagraph(content, prefix + " Special Card Calculation",
                    session.BattleCardPortDescription(card), top, 22, 1.18f, ink);
            top = AddTavernCardParagraph(content, prefix + " Special Card Lore",
                TavernSentenceParagraphs(definition.Lore), top, 24, 1.22f, ink);
            content.sizeDelta = new Vector2(580f, Mathf.Max(viewport.rect.height, top));
            if (top > viewport.rect.height)
                EnableTavernReadingScroll(prefix + " Special Card Details", viewport, content, ink);
        }

        private float AddTavernCardParagraph(RectTransform content, string name, string value, float top,
            int size, float spacing, Color ink)
        {
            Text label = DossierCardText(name, content, 564f, 190f, Vector2.zero, size, ink, value);
            label.lineSpacing = spacing;
            PlaceTavernText(label, new Vector2(8f, top), new Vector2(564f, 190f));
            float height = MeasureTavernParagraph(label);
            label.rectTransform.sizeDelta = new Vector2(564f, height);
            return top + height + 12f;
        }
    }
}
