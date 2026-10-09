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
                DossierCardText(prefix + " Special Card Growth", page, width, 32f, new Vector2(0f, -105f), 20, muted,
                    "特殊卡牌不接受金币强化，仅随对应来源出海获得经验。", TextAnchor.MiddleLeft);
                return;
            }

            int cardIndex = -1;
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
                if (CoreVoyageSession.CardLibrary[index].Id == definition.Card.Id) { cardIndex = index; break; }
            CoreBattleCard card = cardIndex < 0 ? definition.Card : session.GetBattleCard(cardIndex);
            float textWidth = width - 204f;
            float textCenter = 102f;
            RawImage picture = CreateRawImage(prefix + " Special Card Art", page, new Vector2(176f, 176f),
                new Vector2(-width / 2f + 92f, 42f), art.CardSheet, art.CardUv(card.ArtId));
            picture.raycastTarget = false;
            DossierCardText(prefix + " Special Card Name", page, textWidth, 44f, new Vector2(textCenter, 118f), 32, ink,
                card.Name + "　+" + card.UpgradeLevel);
            List<string> stages = new List<string>();
            if (card.IsAllowedInStage(NavalBattleStage.Preparation)) stages.Add("策略");
            if (card.IsAllowedInStage(NavalBattleStage.Naval)) stages.Add("远程攻击");
            if (card.IsAllowedInStage(NavalBattleStage.Boarding)) stages.Add("近战");
            DossierCardText(prefix + " Special Card Stage", page, textWidth, 32f, new Vector2(textCenter, 76f), 20, muted,
                string.Join(" / ", stages) + "　·　" + card.Energy + " 能量");
            // Effects retain their semantic line breaks and full font size. The
            // shared scroll area keeps long rules and lore clear of the footer.
            int effectSize = 22;
            CreateCardEffectArea(prefix + " Special Card Effect", page, new Vector2(textWidth, 136f),
                new Vector2(textCenter, -12f), effectSize, ink, session.BattleCardPortDescription(card),
                loreName: prefix + " Special Card Lore", lore: definition.Lore, loreColor: muted);
            bool acquired = false;
            foreach (string id in session.AcquiredSpecialCardIds)
                if (id == card.Id) { acquired = true; break; }
            int experience = session.GetSpecialCardExperience(card.Id);
            int threshold = session.GetSpecialCardNextThreshold(card.Id);
            string progress = !acquired ? "尚未获得" : threshold == 0 ? "航海经验 " + experience + " · 已达三阶" :
                "航海经验 " + experience + " / " + threshold;
            DossierCardText(prefix + " Special Card Growth", page, width, 32f, new Vector2(0f, -101f), 20, muted,
                progress + "　·　仅随该来源出海成长，不可金币强化。", TextAnchor.MiddleLeft);
        }

        private Text CreateCardEffectArea(string name, Transform parent, Vector2 size, Vector2 position,
            int fontSize, Color color, string value, TextAnchor alignment = TextAnchor.UpperLeft,
            string loreName = null, string lore = null, int loreSize = 20, Color? loreColor = null)
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
            Text effect = DossierCardText(name, content, textWidth, size.y, Vector2.zero,
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
                handle.rectTransform.anchorMin = Vector2.zero; handle.rectTransform.anchorMax = Vector2.one;
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
