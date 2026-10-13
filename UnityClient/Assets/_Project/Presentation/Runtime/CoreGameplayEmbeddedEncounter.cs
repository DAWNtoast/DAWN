using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private void BuildEmbeddedEncounter(string eventName, string category, string description, bool mandatory,
            int risk, int routeIndex, string location, SeaTerrainType terrain, Texture2D illustration,
            Rect illustrationUv, List<EncounterDossierChoice> choices)
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
            CreateRawImage("Encounter Parchment", card, new Vector2(956, 626), Vector2.zero, paper, new Rect(0, 0, 1, 1)).raycastTarget = false;
            RectTransform content = CreateRect("Encounter Paper Content", card, new Vector2(824, 502), Vector2.zero);
            RectTransform artFrame = CreateFixedPanel("Encounter Illustration Mount", content, new Vector2(246, 288), new Vector2(-282, 24), muted);
            artFrame.GetComponent<Image>().raycastTarget = false;
            // Repainted event cells are borderless; only crop for the final layout aspect.
            CreateRawImage("Demo Event Art", artFrame, new Vector2(238, 280), Vector2.zero,
                illustration, EncounterCoverUv(illustration, illustrationUv, 4f / 3f)).raycastTarget = false;
            EncounterText("Encounter Location", content, new Vector2(244, 38), new Vector2(-282, -151), 21, accent, TextAnchor.MiddleCenter).text = location;
            EncounterText("Encounter Title", content, new Vector2(520, 42), new Vector2(146, 199), 28, ink, TextAnchor.MiddleLeft).text = eventName;
            EncounterText("Encounter Description", content, new Vector2(520, 64), new Vector2(146, 142), 20, ink, TextAnchor.UpperLeft, FontStyle.Normal).text = description;
            EncounterText("Encounter Badge", content, new Vector2(350, 24), new Vector2(61, 95), 16,
                mandatory ? Html("#8C3426") : accent, TextAnchor.MiddleLeft).text = category + " · " + (mandatory ? "必须处理" : "可选择略过") + " · 风险 " + risk;
            RectTransform viewport = CreateRect("Encounter Decision Viewport", content, new Vector2(520, 296), new Vector2(146, -67));
            viewport.gameObject.AddComponent<RectMask2D>();
            float totalHeight = 0;
            foreach (var choice in choices) totalHeight += (choice.IsSkip ? 80 : 96) + 10;
            totalHeight = Mathf.Max(296, totalHeight - 10);
            RectTransform actions = CreateRect("Encounter Choices", viewport, new Vector2(520, totalHeight), Vector2.zero);
            actions.anchorMin = actions.anchorMax = new Vector2(0.5f, 1);
            actions.pivot = new Vector2(0.5f, 1);
            actions.anchoredPosition = Vector2.zero;
            float cursor = 0;
            for (int i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];
                float height = choice.IsSkip ? 80 : 96;
                Button button = CreateButton(choice.ObjectName, actions, new Vector2(520, height), new Vector2(0, -cursor - height * 0.5f), PanelRaised, Ink, 20);
                button.GetComponent<RectTransform>().anchorMin = button.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 1);
                button.interactable = choice.Available;
                Text label = button.GetComponentInChildren<Text>();
                label.rectTransform.sizeDelta = new Vector2(340, 25);
                label.rectTransform.anchoredPosition = new Vector2(-36, choice.IsSkip ? 12 : 18);
                label.text = choice.Label;
                label.alignment = TextAnchor.MiddleLeft;
                FitEncounterText(label, choice.IsSkip ? 18 : 20);
                Text detail = EncounterText("Detail", button.transform, new Vector2(340, choice.IsSkip ? 20 : 29),
                    new Vector2(-36, choice.IsSkip ? -13 : -14), 15, Ink, TextAnchor.MiddleLeft, FontStyle.Normal);
                detail.text = choice.Detail;
                Text hours = EncounterText("Hours", button.transform, new Vector2(90, height - 8), new Vector2(204, 0), 14, Gold, TextAnchor.MiddleCenter);
                hours.text = !choice.Available ? "不可选 · " + choice.Hours.ToString("0.#") + " 小时\n" + choice.LockedReason :
                    choice.IsSkip ? "继续航行" : (choice.StartsBattle ? "进入海战\n" : "耗时\n") + choice.Hours.ToString("0.#") + " 小时";
                // Longer resource/crew shortages must remain inside this narrow
                // column instead of being clipped by the decision viewport.
                hours.resizeTextForBestFit = true;
                hours.resizeTextMinSize = 10;
                hours.resizeTextMaxSize = 14;
                EncounterText("Decision Number", button.transform, new Vector2(34, 32), new Vector2(-235, 0), 18, Gold, TextAnchor.MiddleCenter).text = choice.IsSkip ? "→" : (i + 1).ToString("00");
                button.onClick.AddListener(() => choice.Resolve?.Invoke());
                cursor += height + 10;
            }
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = actions; scroll.viewport = viewport; scroll.horizontal = false;
            scroll.vertical = totalHeight > 296; scroll.scrollSensitivity = 24; scroll.movementType = ScrollRect.MovementType.Clamped;
            // The responsive layout places the reminder above the full-width choices.
            EncounterText("Encounter Decision Footer", content, new Vector2(156, 24), new Vector2(328, 95), 14, muted, TextAnchor.MiddleRight).text =
                scroll.vertical ? "上下滚动查看更多" : mandatory ? "选择可行方案继续" : "略过不耗时";
            NavigationDashboardLayout.Attach(contentRoot);
        }
    }
}
