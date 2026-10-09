using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private bool buildingEmbeddedEncounter;

        private void BuildEmbeddedEncounter(string eventName, string category, string description, bool mandatory,
            int risk, int routeIndex, string location, SeaTerrainType terrain, Texture2D illustration,
            Rect illustrationUv, List<EncounterDossierChoice> choices)
        {
            // ScrollRect initialization can flush the canvas and re-enter the
            // dashboard while this card still lacks its footer/choice controls.
            buildingEmbeddedEncounter = true;
            try
            {
                // Also supports editor acceptance fixtures that invoke this builder directly.
                if (lookoutViewport == null) BuildSailing();
                for (int i = lookoutViewport.childCount - 1; i >= 0; i--) RetireNavigationChild(lookoutViewport.GetChild(i));
                RectTransform card = CreateRect("Encounter Card", lookoutViewport, new Vector2(956, 626), Vector2.zero);
                EncounterDossierRevealMotion reveal = card.gameObject.AddComponent<EncounterDossierRevealMotion>();
                reveal.Play(card, 0.30f, !editorPreviewBuilding);
                Color ink = Html("#352415"), muted = Html("#735334");
                Color accent = routeIndex == 2 ? Html("#504471") : routeIndex == 1 ? Html("#8B3E26") : Html("#2D6860");
                Texture2D paper = Resources.Load<Texture2D>("CardWorkshopV2/deck_builder_parchment_v2");
                CreateRawImage("Encounter Parchment", card, new Vector2(940, 614), Vector2.zero, paper, new Rect(0, 0, 1, 1)).raycastTarget = false;
                string[] frames = { "salvage", "escort", "survey" };
                Texture2D frame = Resources.Load<Texture2D>("DispatchV2/dispatch_frame_" + frames[Mathf.Clamp(routeIndex, 0, 2)] + "_v1");
                CreateRawImage("Encounter Themed Frame", card, new Vector2(956, 626), Vector2.zero, frame, new Rect(0, 0, 1, 1)).raycastTarget = false;
                RectTransform content = CreateRect("Encounter Paper Content", card, new Vector2(824, 502), Vector2.zero);
                EncounterText("Encounter Journal Heading", content, new Vector2(246, 42), new Vector2(-282, 220), 18, muted, TextAnchor.MiddleCenter).text = "航 海 见 闻";
                RectTransform artFrame = CreateFixedPanel("Encounter Illustration Mount", content, new Vector2(200, 204), new Vector2(-282, 84), muted);
                artFrame.GetComponent<Image>().raycastTarget = false;
                Rect inset = new Rect(illustrationUv.x + illustrationUv.width * 0.05f, illustrationUv.y + illustrationUv.height * 0.065f,
                    illustrationUv.width * 0.9f, illustrationUv.height * 0.87f);
                CreateRawImage("Demo Event Art", artFrame, new Vector2(192, 196), Vector2.zero,
                    illustration, EncounterCoverUv(illustration, inset, 192f / 196f)).raycastTarget = false;
                EncounterText("Encounter Location", content, new Vector2(244, 46), new Vector2(-282, -53), 20, accent, TextAnchor.MiddleCenter).text = location;
                EncounterText("Encounter Terrain", content, new Vector2(244, 90), new Vector2(-282, -131), 18, muted, TextAnchor.MiddleCenter).text = BattleEnvironmentArtCatalog.LabelFor(terrain) + "\n航行已暂停，等待决定";
                float scale = GameReadability.Scale, titleHeight = 50 * scale, badgeHeight = 50 * scale, footerHeight = 30 * scale;
                float titleTop = 216, badgeTop = titleTop - titleHeight - 6;
                EncounterText("Encounter Title", content, new Vector2(520, titleHeight), new Vector2(146, titleTop - titleHeight * .5f), 32, ink, TextAnchor.MiddleLeft).text = eventName;
                EncounterText("Encounter Badge", content, new Vector2(520, badgeHeight), new Vector2(146, badgeTop - badgeHeight * .5f), 18,
                    mandatory ? Html("#8C3426") : accent, TextAnchor.MiddleLeft).text = category + " · " + (mandatory ? "必须处理" : "可选择略过") + " · 风险 " + risk;
                float viewportTop = badgeTop - badgeHeight - 8, viewportBottom = -184 + footerHeight + 8;
                ScrollRect scroll = CreateEncounterReadingScroll("Encounter Decision Viewport", content,
                    new Vector2(520, viewportTop - viewportBottom), new Vector2(146, (viewportTop + viewportBottom) * .5f), "Encounter Choices");
                RectTransform actions = scroll.content;
                float cursor = FlowEncounterNarrative(actions, "Encounter Description", description, ink);
                for (int i = 0; i < choices.Count; i++)
                {
                    var choice = choices[i];
                    float height = BuildEncounterDecision(actions, choice, i + 1, new Vector2(0, -cursor - 40), 80, accent, 500);
                    cursor += height + 7;
                }
                FinishEncounterReadingScroll(scroll, cursor + 4);
                // The bottom rope, medallion and chest rise into the paper rect.
                // Keep both fixed reminders above that decorated band.
                EncounterText("Encounter Decision Footer", content, new Vector2(520, footerHeight), new Vector2(146, -184 + footerHeight * .5f), 18, muted, TextAnchor.MiddleLeft).text =
                    "上下滚动阅读事件与全部选项";
            }
            finally { buildingEmbeddedEncounter = false; }
            NavigationDashboardLayout.Attach(contentRoot);
        }

        internal static void ReflowEmbeddedEncounter(RectTransform card)
        {
            CoreGameplayDemoController owner = card.GetComponentInParent<CoreGameplayDemoController>();
            if (owner != null && owner.buildingEmbeddedEncounter) return;
            Transform paper = card.Find("Encounter Paper Content");
            if (paper == null) return;
            ScrollRect scroll = paper.Find("Encounter Decision Viewport")?.GetComponent<ScrollRect>();
            if (scroll == null || scroll.content == null || scroll.viewport == null) return;
            Text title = paper.Find("Encounter Title")?.GetComponent<Text>();
            Text badge = paper.Find("Encounter Badge")?.GetComponent<Text>();
            Text footer = paper.Find("Encounter Decision Footer")?.GetComponent<Text>();
            Text terrain = paper.Find("Encounter Terrain")?.GetComponent<Text>();
            Text narrative = scroll.content.Find("Encounter Description")?.GetComponent<Text>();
            // Persisted editor previews can have the older fixed narrative
            // beside the choices. Validate the entire flow hierarchy before
            // changing any geometry; keep those authored previews intact.
            if (title == null || badge == null || footer == null || terrain == null || narrative == null) return;
            foreach (Transform child in scroll.content)
            {
                Button button = child.GetComponent<Button>();
                if (button != null && (child.Find("Label")?.GetComponent<Text>() == null ||
                    child.Find("Detail")?.GetComponent<Text>() == null || child.Find("Hours")?.GetComponent<Text>() == null)) return;
            }
            float position = scroll.content.rect.height <= scroll.viewport.rect.height + .5f ? 1 : scroll.verticalNormalizedPosition;
            float titleHeight = MeasureEncounterTextHeight(title), badgeHeight = MeasureEncounterTextHeight(badge);
            float footerHeight = MeasureEncounterTextHeight(footer);
            title.rectTransform.sizeDelta = new Vector2(520, titleHeight);
            title.rectTransform.anchoredPosition = new Vector2(146, 216 - titleHeight * .5f);
            float badgeTop = 216 - titleHeight - 6;
            badge.rectTransform.sizeDelta = new Vector2(520, badgeHeight);
            badge.rectTransform.anchoredPosition = new Vector2(146, badgeTop - badgeHeight * .5f);
            footer.rectTransform.sizeDelta = new Vector2(520, footerHeight);
            footer.rectTransform.anchoredPosition = new Vector2(146, -184 + footerHeight * .5f);
            footer.alignment = TextAnchor.MiddleLeft;
            float terrainHeight = MeasureEncounterTextHeight(terrain);
            terrain.rectTransform.sizeDelta = new Vector2(244, terrainHeight);
            terrain.rectTransform.anchoredPosition = new Vector2(-282, -86 - terrainHeight * .5f);
            float viewportTop = badgeTop - badgeHeight - 8, viewportBottom = -184 + footerHeight + 8;
            scroll.viewport.sizeDelta = new Vector2(520, viewportTop - viewportBottom);
            scroll.viewport.anchoredPosition = new Vector2(146, (viewportTop + viewportBottom) * .5f);

            float narrativeHeight = MeasureEncounterTextHeight(narrative);
            narrative.rectTransform.sizeDelta = new Vector2(scroll.content.rect.width, narrativeHeight);
            narrative.rectTransform.anchoredPosition = new Vector2(0, -narrativeHeight * .5f);
            float cursor = narrativeHeight + 14;
            foreach (Transform child in scroll.content)
            {
                Button button = child.GetComponent<Button>();
                if (button == null) continue;
                float height = LayoutEncounterDecision(button, 80);
                button.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -cursor - height * .5f);
                cursor += height + 7;
            }
            scroll.content.sizeDelta = new Vector2(scroll.content.rect.width, Mathf.Max(scroll.viewport.rect.height, cursor + 4));
            scroll.verticalNormalizedPosition = Mathf.Clamp01(position);
            if (position >= .999f) scroll.content.anchoredPosition = new Vector2(-10, 0);
            scroll.StopMovement();
        }
    }
}
