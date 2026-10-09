using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private sealed class EncounterDossierChoice
        {
            public string ObjectName, Label, Detail, LockedReason;
            public double Hours;
            public bool Available, StartsBattle, IsSkip;
            public Action Resolve;
        }

        // Regular encounters and tutorial encounters deliberately share this
        // paper-safe layout. Decorative ropes are not a usable text rectangle.
        private void BuildEncounterDossier(string prefix, string eventName, string category, string description,
            bool mandatory, int risk, int routeIndex, string location, SeaTerrainType terrain,
            Texture2D illustration, Rect illustrationUv, List<EncounterDossierChoice> choices)
        {
            if (prefix == "Encounter")
            {
                BuildEmbeddedEncounter(eventName, category, description, mandatory, risk, routeIndex,
                    location, terrain, illustration, illustrationUv, choices);
                return;
            }
            Color ink = Html("#352415");
            Color muted = Html("#735334");
            Color accent = routeIndex == 2 ? Html("#504471") : routeIndex == 1 ? Html("#8B3E26") : Html("#2D6860");
            bool teaching = prefix == "Tutorial Encounter";

            RectTransform overlay = CreateStretchPanel(prefix + " Overlay", contentRoot, Html("#151C1C"));
            Texture2D environment = battleEnvironmentArt == null ? null : battleEnvironmentArt.ForTerrain(terrain);
            if (environment != null)
            {
                RawImage sea = CreateRawImage(prefix + " Local Sea", overlay,
                    PhaseSurfaceLayout.BackgroundSize, PhaseSurfaceLayout.BackgroundPosition,
                    environment, PhaseSurfaceLayout.BackgroundUv(environment));
                sea.raycastTarget = false;
            }
            CreateStretchPanel(prefix + " Backdrop Shade", overlay, new Color(0.055f, 0.037f, 0.025f, 0.69f)).GetComponent<Image>().raycastTarget = false;
            RectTransform card = CreateRect(prefix + " Card", overlay, new Vector2(1660f, 930f), Vector2.zero);
            EncounterDossierRevealMotion reveal = card.gameObject.AddComponent<EncounterDossierRevealMotion>();
            reveal.Play(card, 0.34f);
            Image shadow = CreateImage(prefix + " Paper Shadow", card, new Vector2(1600f, 868f), new Vector2(8f, -14f), new Color(0f, 0f, 0f, 0.4f));
            shadow.raycastTarget = false;
            Texture2D paperTexture = Resources.Load<Texture2D>("CardWorkshopV2/deck_builder_parchment_v2");
            RawImage paper = CreateRawImage(prefix + " Parchment", card, new Vector2(1610f, 884f), Vector2.zero,
                paperTexture, new Rect(0f, 0f, 1f, 1f));
            paper.raycastTarget = false;
            string[] frames = { "salvage", "escort", "survey" };
            Texture2D borderTexture = Resources.Load<Texture2D>("DispatchV2/dispatch_frame_" + frames[Mathf.Clamp(routeIndex, 0, 2)] + "_v1");
            RawImage frame = CreateRawImage(prefix + " Themed Frame", card, new Vector2(1660f, 930f), Vector2.zero,
                borderTexture, new Rect(0f, 0f, 1f, 1f));
            frame.raycastTarget = false;

            RectTransform content = CreateRect(prefix + " Paper Content", card, new Vector2(1420f, 748f), Vector2.zero);
            Text chapter = EncounterText(prefix + " Journal Heading", content, new Vector2(510f, 46f), new Vector2(-445f, 336f), 18, muted, TextAnchor.MiddleCenter);
            chapter.text = teaching ? "首 航 手 记" : "船 长 航 海 日 志";
            RectTransform artFrame = CreateFixedPanel(prefix + " Illustration Mount", content, new Vector2(524f, 524f), new Vector2(-445f, 40f), Html("#715237"));
            artFrame.GetComponent<Image>().raycastTarget = false;
            Image artMat = CreateImage(prefix + " Illustration Mat", artFrame, new Vector2(518f, 518f), Vector2.zero, Html("#D3AB70"));
            artMat.raycastTarget = false;
            // Crop the atlas's baked gold frame before framing the illustration.
            // Insets are relative to the cell, so import downscaling cannot leave
            // a black/gold strip or neighbouring corner inside the new art mat.
            Rect artUv = illustrationUv;
            if (illustration != null)
            {
                float insetX = illustrationUv.width * 0.05f;
                float insetY = illustrationUv.height * 0.065f;
                artUv = new Rect(artUv.x + insetX, artUv.y + insetY, artUv.width - insetX * 2f, artUv.height - insetY * 2f);
            }
            RawImage picture = CreateRawImage(teaching ? "Tutorial Event Art" : "Demo Event Art", artFrame,
                new Vector2(506f, 506f), Vector2.zero, illustration, EncounterCoverUv(illustration, artUv, 1f));
            picture.raycastTarget = false;
            Text place = EncounterText(prefix + " Location", content, new Vector2(510f, 48f), new Vector2(-445f, -257f), 20, accent, TextAnchor.MiddleCenter);
            place.text = location;
            Text setting = EncounterText(prefix + " Terrain", content, new Vector2(510f, 44f), new Vector2(-445f, -311f), 18, muted, TextAnchor.MiddleCenter);
            setting.text = teaching ? "首航训练 · 练习航海抉择" : BattleEnvironmentArtCatalog.LabelFor(terrain) + " · 航海抉择";
            CreateImage(prefix + " Column Rule", content, new Vector2(2f, 645f), new Vector2(-150f, 0f), new Color(0.47f, 0.32f, 0.15f, 0.35f)).raycastTarget = false;

            float scale = GameReadability.Scale;
            float titleHeight = 54 * scale, badgeHeight = 38 * scale;
            float titleTop = 336, badgeTop = titleTop - titleHeight - 10;
            Text badge = EncounterText(prefix + " Badge", content, new Vector2(780f, badgeHeight), new Vector2(310f, badgeTop - badgeHeight * .5f), 18,
                mandatory ? Html("#8C3426") : accent, TextAnchor.MiddleLeft);
            badge.text = category + " · " + (mandatory ? "必须处理" : "可选择略过") + (teaching ? " · 教学" : " · 风险 " + risk);
            Text title = EncounterText(prefix + " Title", content, new Vector2(780f, titleHeight), new Vector2(310f, titleTop - titleHeight * .5f), 32, ink, TextAnchor.MiddleLeft);
            title.text = eventName;
            float viewportTop = badgeTop - badgeHeight - 12, viewportBottom = -280;
            ScrollRect scroll = CreateEncounterReadingScroll(prefix + " Decision Viewport", content,
                new Vector2(780, viewportTop - viewportBottom), new Vector2(310, (viewportTop + viewportBottom) * .5f),
                teaching ? "Tutorial Choices" : "Encounter Choices");
            RectTransform choiceRoot = scroll.content;
            float cursor = FlowEncounterNarrative(choiceRoot, teaching ? "Tutorial Encounter Body" : "Encounter Description", description, ink);
            for (int index = 0; index < choices.Count; index++)
            {
                EncounterDossierChoice choice = choices[index];
                float height = BuildEncounterDecision(choiceRoot, choice, index + 1, new Vector2(0f, -cursor - 40), 80, accent, choiceRoot.rect.width);
                cursor += height + 11f;
            }
            FinishEncounterReadingScroll(scroll, cursor + 4);
            Text footnote = EncounterText(prefix + " Decision Footer", content, new Vector2(780f, 54f), new Vector2(310f, -319f), 18, muted, TextAnchor.MiddleLeft);
            footnote.text = "上下滚动阅读全部选项 · " + (mandatory ? "处理后继续航行" : "略过不耗时");
        }

        private float BuildEncounterDecision(RectTransform parent, EncounterDossierChoice choice, int number, Vector2 position, float height, Color accent, float width = 740f)
        {
            Button button = CreateButton(choice.ObjectName, parent, new Vector2(width, height), position,
                Html("#5F452D"), Html("#FFF1CB"), 22);
            // Pure decoration never intercepts a click; the whole wood row is
            // the hit target. Highlight changes colour only, never layout/feet.
            // The shared nine-slice preserves the same edge thickness on all choices.
            button.interactable = choice.Available;

            Color light = choice.Available ? Html("#FFF1CB") : Html("#D4C8B6");
            Text label = button.GetComponentInChildren<Text>();
            label.rectTransform.sizeDelta = new Vector2(width - 220, 40);
            label.alignment = TextAnchor.MiddleLeft;
            label.color = light;
            label.text = choice.Label;
            FitEncounterText(label, 22);
            Text detail = EncounterText("Detail", button.transform, new Vector2(width - 220, 40),
                Vector2.zero, 22, light, TextAnchor.MiddleLeft, FontStyle.Normal);
            detail.text = choice.Detail;
            Text hours = EncounterText("Hours", button.transform, new Vector2(132f, height - 12f), new Vector2(width * .5f - 78, 0f),
                18, choice.Available ? Html("#EFCA86") : Html("#D4C8B6"), TextAnchor.MiddleCenter);
            hours.text = !choice.Available ? "不可选 · " + choice.Hours.ToString("0.#") + " 小时\n" + choice.LockedReason : choice.IsSkip ? "继续航行" :
                (choice.StartsBattle ? "进入海战\n" : "耗时\n") + choice.Hours.ToString("0.#") + " 小时";
            float actualHeight = LayoutEncounterDecision(button, height);
            RectTransform buttonRect = button.GetComponent<RectTransform>();
            buttonRect.anchoredPosition = position - new Vector2(0, (actualHeight - height) * .5f);
            if (parent.pivot.y == 1f) buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(.5f, 1f);
            Image marker = CreateImage("Decision Seal", button.transform, new Vector2(38f, 38f), new Vector2(-width * .5f + 28, 0f),
                choice.StartsBattle ? Html("#913E2E") : accent, circleSprite);
            marker.raycastTarget = false;
            Text digit = EncounterText("Decision Number", marker.transform, new Vector2(40f, 42f), Vector2.zero, 18,
                Html("#F9E1AD"), TextAnchor.MiddleCenter);
            digit.text = choice.IsSkip ? "→" : number.ToString("00");
            button.onClick.AddListener(() => choice.Resolve?.Invoke());
            return actualHeight;
        }

        private static float LayoutEncounterDecision(Button button, float minimumHeight)
        {
            RectTransform rect = button.GetComponent<RectTransform>();
            Text label = button.transform.Find("Label").GetComponent<Text>();
            Text detail = button.transform.Find("Detail").GetComponent<Text>();
            Text hours = button.transform.Find("Hours").GetComponent<Text>();
            float labelHeight = MeasureEncounterTextHeight(label);
            float detailHeight = MeasureEncounterTextHeight(detail);
            float height = Mathf.Max(minimumHeight, labelHeight + detailHeight + 30, MeasureEncounterTextHeight(hours) + 20);
            rect.sizeDelta = new Vector2(rect.rect.width, height);
            label.rectTransform.sizeDelta = new Vector2(label.rectTransform.rect.width, labelHeight);
            label.rectTransform.anchoredPosition = new Vector2(-44, height * .5f - 12 - labelHeight * .5f);
            detail.rectTransform.sizeDelta = new Vector2(detail.rectTransform.rect.width, detailHeight);
            detail.rectTransform.anchoredPosition = new Vector2(-44, height * .5f - 18 - labelHeight - detailHeight * .5f);
            hours.rectTransform.sizeDelta = new Vector2(132, height - 20);
            return height;
        }

        private static float MeasureEncounterTextHeight(Text text)
        {
            // Canvas pixel rounding can move the last glyph onto another line
            // after the dashboard gets its final scale. Measure with the live
            // generator settings and a small width reserve, without shrinking.
            float width = Mathf.Max(1, text.rectTransform.rect.width - 16);
            TextGenerationSettings settings = text.GetGenerationSettings(new Vector2(width, 0));
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            using (var generator = new TextGenerator())
                return Mathf.Ceil(Mathf.Max(text.preferredHeight,
                    generator.GetPreferredHeight(text.text, settings) / text.pixelsPerUnit)) + 8;
        }

        private Text EncounterText(string name, Transform parent, Vector2 size, Vector2 position, int fontSize,
            Color color, TextAnchor anchor, FontStyle style = FontStyle.Bold)
        {
            Text text = CreateText(name, parent, size, position, fontSize, style, anchor, color);
            FitEncounterText(text, fontSize);
            return text;
        }

        private static void FitEncounterText(Text text, int fontSize)
        {
            bool heading = text.name.EndsWith("Title");
            GameReadability.Body(text, heading ? 32 : Mathf.Max(18, fontSize));
            if (heading) text.font = GameTypography.InterfaceFont;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.lineSpacing = 1f;
            text.raycastTarget = false;
        }

        private ScrollRect CreateEncounterReadingScroll(string name, RectTransform parent, Vector2 size, Vector2 position, string contentName)
        {
            RectTransform viewport = CreateRect(name, parent, size, position);
            Image surface = viewport.gameObject.AddComponent<Image>();
            surface.color = Color.clear;
            surface.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = CreateRect(contentName, viewport, new Vector2(size.x - 20, size.y), Vector2.zero);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, 1f);
            content.anchoredPosition = new Vector2(-10, 0);
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true;
            scroll.scrollSensitivity = 40; scroll.movementType = ScrollRect.MovementType.Clamped;
            RectTransform bar = CreateRect(name + " Scrollbar", viewport, new Vector2(10, -4), new Vector2(-1, 0));
            bar.anchorMin = new Vector2(1, 0); bar.anchorMax = Vector2.one; bar.pivot = new Vector2(1, .5f);
            Image track = bar.gameObject.AddComponent<Image>(); track.color = new Color(.2f, .12f, .04f, .25f);
            Scrollbar scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            RectTransform handle = CreateRect("Handle", bar, Vector2.zero, Vector2.zero);
            handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one;
            Image thumb = handle.gameObject.AddComponent<Image>(); thumb.color = Html("#A1793D");
            scrollbar.handleRect = handle; scrollbar.targetGraphic = thumb; scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.SetValueWithoutNotify(1f);
            scroll.verticalScrollbar = scrollbar;
            return scroll;
        }

        private static void FinishEncounterReadingScroll(ScrollRect scroll, float contentHeight)
        {
            scroll.content.sizeDelta = new Vector2(scroll.content.rect.width, Mathf.Max(scroll.viewport.rect.height, contentHeight));
            // Scrollbar's default is zero. A fresh encounter must start with its
            // narrative at the top, even when the final rows make it scrollable.
            scroll.verticalNormalizedPosition = 1f;
            scroll.content.anchoredPosition = new Vector2(-10, 0);
            scroll.StopMovement();
        }

        private float FlowEncounterNarrative(RectTransform content, string name, string value, Color ink)
        {
            Text text = EncounterText(name, content, new Vector2(content.rect.width, 80), Vector2.zero, 22, ink, TextAnchor.UpperLeft, FontStyle.Normal);
            text.text = value;
            float height = MeasureEncounterTextHeight(text);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f, 1f);
            text.rectTransform.sizeDelta = new Vector2(content.rect.width, height);
            text.rectTransform.anchoredPosition = new Vector2(0, -height * .5f);
            return height + 14;
        }

        private static Rect EncounterCoverUv(Texture texture, Rect source, float targetAspect)
        {
            if (texture == null) return source;
            float sourceAspect = texture.width * source.width / (texture.height * source.height);
            if (sourceAspect > targetAspect)
            {
                float width = source.width * targetAspect / sourceAspect;
                return new Rect(source.center.x - width * 0.5f, source.y, width, source.height);
            }
            float height = source.height * sourceAspect / targetAspect;
            return new Rect(source.x, source.center.y - height * 0.5f, source.width, height);
        }
    }
}
