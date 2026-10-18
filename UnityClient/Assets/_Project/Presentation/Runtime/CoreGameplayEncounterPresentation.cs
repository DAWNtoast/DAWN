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
            public string ObjectName, Label, Detail, LockedReason, Requirement;
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
            Color ink = MarketShopSkin.Ink;
            Color muted = MarketShopSkin.MutedInk;
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
            Image paper = CreateImage(prefix + " Parchment", card, new Vector2(1610f, 884f), Vector2.zero, Color.white);
            NauticalSurfaceSkin.ApplyReadingPanel(paper);
            paper.raycastTarget = false;

            RectTransform content = CreateRect(prefix + " Paper Content", card, new Vector2(1420f, 748f), Vector2.zero);
            Text chapter = EncounterText(prefix + " Journal Heading", content, new Vector2(510f, 30f), new Vector2(-445f, 336f), 19, muted, TextAnchor.MiddleCenter);
            chapter.text = teaching ? "首 航 手 记" : "船 长 航 海 日 志";
            RectTransform artFrame = CreateFixedPanel(prefix + " Illustration Mount", content, new Vector2(524f, 524f), new Vector2(-445f, 40f), Html("#715237"));
            artFrame.GetComponent<Image>().raycastTarget = false;
            Image artMat = CreateImage(prefix + " Illustration Mat", artFrame, new Vector2(518f, 518f), Vector2.zero, Html("#E9DFC9"));
            artMat.raycastTarget = false;
            // Repainted atlas cells have no baked frame. Preserve their full source
            // region before applying the square tutorial window's cover crop.
            RawImage picture = CreateRawImage(teaching ? "Tutorial Event Art" : "Demo Event Art", artFrame,
                new Vector2(506f, 506f), Vector2.zero, illustration, EncounterCoverUv(illustration, illustrationUv, 1f));
            picture.raycastTarget = false;
            Text place = EncounterText(prefix + " Location", content, new Vector2(510f, 40f), new Vector2(-445f, -257f), 22, accent, TextAnchor.MiddleCenter);
            place.text = location;
            Text setting = EncounterText(prefix + " Terrain", content, new Vector2(510f, 28f), new Vector2(-445f, -295f), 16, muted, TextAnchor.MiddleCenter);
            setting.text = teaching ? "首航训练 · 练习航海抉择" : BattleEnvironmentArtCatalog.LabelFor(terrain) + " · 航海抉择";
            CreateImage(prefix + " Column Rule", content, new Vector2(2f, 645f), new Vector2(-150f, 0f), new Color(0.47f, 0.32f, 0.15f, 0.35f)).raycastTarget = false;

            Text badge = EncounterText(prefix + " Badge", content, new Vector2(500f, 26f), new Vector2(435f, 138f), 17,
                mandatory ? Html("#8C3426") : accent, TextAnchor.MiddleLeft);
            badge.text = category + " · " + (mandatory ? "必须处理" : "可选择略过") + (teaching ? " · 教学" : " · 风险 " + risk);
            Text title = EncounterText(prefix + " Title", content, new Vector2(780f, 52f), new Vector2(310f, 278f), 35, ink, TextAnchor.MiddleLeft);
            title.text = eventName;
            Text body = EncounterText(teaching ? "Tutorial Encounter Body" : "Encounter Description", content,
                new Vector2(780f, 88f), new Vector2(310f, 205f), 22, ink, TextAnchor.UpperLeft, FontStyle.Normal);
            body.text = description;
            Text prompt = EncounterText(prefix + " Decision Heading", content, new Vector2(250f, 28f), new Vector2(45f, 138f), 16, accent, TextAnchor.MiddleLeft);
            prompt.text = "船长，请下达决定";
            CreateImage(prefix + " Decision Rule", content, new Vector2(740f, 2f), new Vector2(290f, 118f), new Color(0.47f, 0.32f, 0.15f, 0.4f)).raycastTarget = false;

            RectTransform choiceRoot = CreateRect(teaching ? "Tutorial Choices" : "Encounter Choices", content,
                new Vector2(740f, 404f), new Vector2(290f, -88f));
            float cursor = choiceRoot.rect.height * 0.5f;
            for (int index = 0; index < choices.Count; index++)
            {
                EncounterDossierChoice choice = choices[index];
                float height = choice.IsSkip ? 64f : 94f;
                BuildEncounterDecision(choiceRoot, choice, index + 1, new Vector2(0f, cursor - height * 0.5f), height, accent);
                cursor -= height + 11f;
            }
            Text footnote = EncounterText(prefix + " Decision Footer", content, new Vector2(430f, 26f), new Vector2(415f, -299f), 14, muted, TextAnchor.MiddleLeft);
            footnote.text = mandatory ? "处理此事件后方可继续航行。" : "略过不耗时；选择后结算效果。";
        }

        private void BuildEncounterDecision(RectTransform parent, EncounterDossierChoice choice, int number, Vector2 position, float height, Color accent)
        {
            Button button = CreateButton(choice.ObjectName, parent, new Vector2(740f, height), position,
                Color.white, MarketShopSkin.Ink, choice.IsSkip ? 19 : 22);
            MarketShopSkin.ApplyLightButton(button);
            // The entire row remains the hit target; decoration never intercepts clicks.
            button.interactable = choice.Available;

            Color light = choice.Available ? MarketShopSkin.Ink : MarketShopSkin.MutedInk;
            Text label = button.GetComponentInChildren<Text>();
            label.rectTransform.sizeDelta = new Vector2(514f, choice.IsSkip ? 25f : 32f);
            label.rectTransform.anchoredPosition = new Vector2(-16f, choice.IsSkip ? 12f : 20f);
            label.alignment = TextAnchor.MiddleLeft;
            label.color = light;
            label.text = choice.Label;
            FitEncounterText(label, choice.IsSkip ? 19 : 22);
            Text detail = EncounterText("Detail", button.transform, new Vector2(514f, choice.IsSkip ? 21f : 34f),
                new Vector2(-16f, choice.IsSkip ? -13f : -18f), choice.IsSkip ? 14 : 16, light, TextAnchor.MiddleLeft, FontStyle.Normal);
            detail.text = choice.Detail;
            Text hours = EncounterText("Hours", button.transform, new Vector2(104f, height - 12f), new Vector2(307f, 0f),
                15, choice.Available ? Html("#8F6C3E") : Html("#95523B"), TextAnchor.MiddleCenter);
            hours.text = !choice.Available ? "不可选 · " + choice.Hours.ToString("0.#") + " 小时\n" + choice.LockedReason : choice.IsSkip ? "继续航行" :
                (choice.StartsBattle ? "进入海战\n" : "耗时\n") + choice.Hours.ToString("0.#") + " 小时";
            Image marker = CreateImage("Decision Seal", button.transform, new Vector2(38f, 38f), new Vector2(-332f, 0f),
                choice.StartsBattle ? Html("#913E2E") : accent, circleSprite);
            marker.raycastTarget = false;
            Text digit = EncounterText("Decision Number", marker.transform, new Vector2(36f, 32f), Vector2.zero, 18,
                Html("#F9E1AD"), TextAnchor.MiddleCenter);
            digit.text = choice.IsSkip ? "→" : number.ToString("00");
            button.onClick.AddListener(() => choice.Resolve?.Invoke());
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
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(13, fontSize - 3);
            text.resizeTextMaxSize = fontSize;
            text.raycastTarget = false;
        }

        private static Rect EncounterCoverUv(Texture texture, Rect source, float targetAspect)
        {
            if (texture == null) return source;
            // Mipmaps prevent fine rigging and waves aliasing in small windows.
            // Keep their bilinear footprint inside the cell through mip level 4.
            float guardPixels = texture is Texture2D atlas && atlas.mipmapCount > 1 ? 8f : 0.5f;
            float edgeX = guardPixels / texture.width, edgeY = guardPixels / texture.height;
            source = new Rect(source.x + edgeX, source.y + edgeY,
                source.width - edgeX * 2f, source.height - edgeY * 2f);
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
