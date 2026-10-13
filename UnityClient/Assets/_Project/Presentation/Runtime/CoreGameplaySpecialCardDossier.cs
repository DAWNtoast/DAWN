using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        // A read-only card sheet shared by crew and component dossiers. The
        // source is inspected here; reading a dossier never acquires a card.
        private void BuildDossierSpecialCard(RectTransform page, string prefix,
            CoreSpecialCardDefinition definition, float width, string emptyMessage, Color ink, Color muted)
        {
            if (definition == null)
            {
                DossierCardText(prefix + " Special Card Name", page, width, 40f, new Vector2(0f, 72f), 28, ink, "没有专属卡牌");
                DossierCardText(prefix + " Special Card Empty", page, width - 40f, 108f, Vector2.zero, 26, muted, emptyMessage);
                return;
            }

            int cardIndex = -1;
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
                if (CoreVoyageSession.CardLibrary[index].Id == definition.Card.Id) { cardIndex = index; break; }
            CoreBattleCard card = cardIndex < 0 ? definition.Card : session.GetBattleCard(cardIndex);
            // Keep the battle face intact; fit the whole card within the dossier
            // bay, leaving the shared page controls and contract buttons clear.
            float pageHeight = page.rect.height;
            float cardScale = Mathf.Min(0.86f, (pageHeight - 12f) / CardFaceLayout.FrameSize.y);
            RectTransform face = CreateCollectionCardFace(page, card, prefix + " Special Card",
                prefix + " Special Card Art", prefix + " Special Card Name", cardScale,
                effectDescription: SpecialCardPreviewEffectDescription(card),
                effectName: prefix + " Special Card Effect");
            face.anchoredPosition = new Vector2(-width * 0.5f + 112f, 0f) -
                CardSchoolArtCatalog.FrameBounds(card).center * cardScale;
            float textWidth = width - 250f;
            const float textCenter = 125f;
            List<string> stages = new List<string>();
            if (card.IsAllowedInStage(NavalBattleStage.Preparation)) stages.Add("策略");
            if (card.IsAllowedInStage(NavalBattleStage.Naval)) stages.Add("远程攻击");
            if (card.IsAllowedInStage(NavalBattleStage.Boarding)) stages.Add("近战");
            DossierCardText(prefix + " Special Card Stage", page, textWidth, 32f,
                new Vector2(textCenter, pageHeight * 0.5f - 16f), 22, muted, string.Join(" / ", stages));
            // Crew-dependent calculations remain beside the card, while the
            // face uses the same description and line layout as other previews.
            bool boarding = card.IsAllowedInStage(NavalBattleStage.Boarding);
            CreateCardEffectArea(prefix + " Special Card Details", page, new Vector2(textWidth, pageHeight - 126f),
                new Vector2(textCenter, 20f), boarding ? 20 : 22, boarding ? ink : muted,
                boarding ? session.BattleCardPortDescription(card) : definition.Lore,
                loreName: prefix + " Special Card Lore", lore: boarding ? definition.Lore : null, loreColor: muted,
                textName: prefix + (boarding ? " Special Card Calculation" : " Special Card Lore"));
            bool acquired = false;
            foreach (string id in session.AcquiredSpecialCardIds)
                if (id == card.Id) { acquired = true; break; }
            int experience = session.GetSpecialCardExperience(card.Id);
            int threshold = session.GetSpecialCardNextThreshold(card.Id);
            string progress = !acquired ? "尚未获得" : threshold == 0 ? "航海经验 " + experience + " · 已达三阶" :
                "航海经验 " + experience + " / " + threshold;
            DossierCardText(prefix + " Special Card Growth", page, textWidth, 48f,
                new Vector2(textCenter, -pageHeight * 0.5f + 44f), 20, muted,
                progress + "\n仅随该来源出海成长，不可金币强化。", TextAnchor.MiddleLeft);
        }

        private string SpecialCardPreviewEffectDescription(CoreBattleCard card)
        {
            NavalBattleStage stage = card.IsAllowedInStage(NavalBattleStage.Boarding)
                ? NavalBattleStage.Boarding : card.IsAllowedInStage(NavalBattleStage.Naval)
                    ? NavalBattleStage.Naval : NavalBattleStage.Preparation;
            return session.BattleCardFaceDescription(card, stage);
        }

        private Text CreateCardEffectArea(string name, Transform parent, Vector2 size, Vector2 position,
            int fontSize, Color color, string value, TextAnchor alignment = TextAnchor.UpperLeft,
            string loreName = null, string lore = null, int loreSize = 20, Color? loreColor = null, string textName = null)
        {
            RectTransform viewport = CreateRect(name + " Viewport", parent, size, position);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hitArea = viewport.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            hitArea.raycastTarget = false;
            RectTransform content = CreateRect(name + " Content", viewport, size, Vector2.zero);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            float textWidth = size.x - 16f;
            Text effect = DossierCardText(textName ?? name, content, textWidth, size.y, Vector2.zero,
                fontSize, color, value, alignment);
            // The viewport owns clipping. Legacy Text must not silently discard
            // its last row when capture or CanvasScaler changes pixelsPerUnit.
            effect.verticalOverflow = VerticalWrapMode.Overflow;
            float effectHeight = MeasureCardTextHeight(effect);
            // At small canvas scales CJK glyph bearings can rise several
            // logical pixels above the first line's typographic top edge.
            const float topPadding = 12f;
            PlaceCardEffectParagraph(effect, textWidth, effectHeight, topPadding);
            float height = topPadding + effectHeight;
            if (!string.IsNullOrEmpty(lore))
            {
                Text story = DossierCardText(loreName ?? name + " Lore", content, textWidth, size.y,
                    Vector2.zero, loreSize, loreColor ?? color, lore);
                story.verticalOverflow = VerticalWrapMode.Overflow;
                float storyHeight = MeasureCardTextHeight(story);
                PlaceCardEffectParagraph(story, textWidth, storyHeight, height + 12f);
                height += 12f + storyHeight;
            }
            content.sizeDelta = new Vector2(size.x, Mathf.Max(size.y, height));
            if (height > size.y)
            {
                hitArea.raycastTarget = true;
                ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
                scroll.viewport = viewport;
                scroll.content = content;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.scrollSensitivity = 28f;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.verticalNormalizedPosition = 1f;
                Image track = CreateImage(name + " Scrollbar", viewport, new Vector2(5f, size.y),
                    new Vector2(size.x * 0.5f - 3f, 0f), new Color(color.r, color.g, color.b, 0.15f));
                RectTransform handleArea = CreateRect("Sliding Area", track.transform, new Vector2(5f, size.y), Vector2.zero);
                Image handle = CreateImage("Handle", handleArea, Vector2.zero, Vector2.zero,
                    new Color(color.r, color.g, color.b, 0.65f));
                Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
                scrollbar.handleRect = handle.rectTransform;
                scrollbar.targetGraphic = handle;
                scrollbar.direction = Scrollbar.Direction.BottomToTop;
                scroll.verticalScrollbar = scrollbar;
            }
            return effect;
        }

        private static float MeasureCardTextHeight(Text label)
        {
            // The embedded CJK font's final glyph row can extend past its line
            // metrics. Measure both the live canvas and the unit-scale capture
            // canvas, then reserve one font line instead of a four-pixel guess.
            TextGenerationSettings settings = label.GetGenerationSettings(label.rectTransform.rect.size);
            settings.resizeTextForBestFit = false;
            settings.fontSize = label.fontSize;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            settings.generateOutOfBounds = true;
            float height = label.preferredHeight;
            using (TextGenerator generator = new TextGenerator())
            {
                float liveScale = Mathf.Max(0.01f, settings.scaleFactor);
                height = Mathf.Max(height, generator.GetPreferredHeight(label.text, settings) / liveScale);
                settings.scaleFactor = 1f;
                height = Mathf.Max(height, generator.GetPreferredHeight(label.text, settings));
            }
            float finalLineSafety = Mathf.Ceil(label.fontSize * Mathf.Max(1f, label.lineSpacing));
            return Mathf.Ceil(height) + finalLineSafety + 4f;
        }

        private static void PlaceCardEffectParagraph(Text label, float width, float height, float top)
        {
            RectTransform rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(-4f, -top);
        }

        private Text DossierCardText(string name, Transform parent, float width, float height, Vector2 position,
            int size, Color color, string value, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            Text label = CreateText(name, parent, new Vector2(width, height), position, size, FontStyle.Normal, alignment, color);
            label.text = value;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = false;
            label.raycastTarget = false;
            return label;
        }
    }
}
