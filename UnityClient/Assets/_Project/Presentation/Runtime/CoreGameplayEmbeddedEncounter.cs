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
            if (lookoutViewport == null) BuildSailing();
            for (int i = lookoutViewport.childCount - 1; i >= 0; i--) RetireNavigationChild(lookoutViewport.GetChild(i));
            RectTransform card = CreateRect("Encounter Card", lookoutViewport, new Vector2(1096, 906), Vector2.zero);
            Color ink = MarketShopSkin.Ink;
            Color accent = mandatory ? Html("#95523B") : Html("#8F6C3E");
            Image paper = CreateRect("Encounter Parchment", card, card.sizeDelta, Vector2.zero).gameObject.AddComponent<Image>();
            MarketShopSkin.ApplyPanel(paper);
            paper.raycastTarget = false;
            RectTransform content = CreateRect("Encounter Paper Content", card, new Vector2(1024, 834), Vector2.zero);
            JournalText("Encounter Title", content, eventName, 32, ink);
            JournalSurface("Encounter Header Divider", content, new Vector2(1024, 1), new Color(.49f, .37f, .21f, .35f));
            RectTransform mount = CreateRect("Encounter Illustration Mount", content, new Vector2(328, 248), Vector2.zero);
            JournalSurface("Illustration Border", mount, mount.sizeDelta, Html("#826849"));
            JournalSurface("Illustration Mat", mount, mount.sizeDelta - new Vector2(4, 4), Html("#E9DFC9"));
            bool specialShip = SpecialWarshipArtCatalog.IsSpecialTexture(illustration);
            CreateRawImage("Demo Event Art", mount,
                specialShip ? UiImageAspect.FitSize(illustration, illustrationUv, new Vector2(320, 240)) : new Vector2(320, 240),
                Vector2.zero, illustration, specialShip ? illustrationUv : EncounterCoverUv(illustration, illustrationUv, 4f / 3f)).raycastTarget = false;
            RectTransform storyViewport = CreateRect("Encounter Description Viewport", content, new Vector2(620, 226), Vector2.zero);
            Image storyHit = storyViewport.gameObject.AddComponent<Image>(); storyHit.color = Color.clear;
            storyViewport.gameObject.AddComponent<RectMask2D>();
            RectTransform story = CreateRect("Encounter Description Content", storyViewport, new Vector2(620, 226), Vector2.zero);
            story.anchorMin = story.anchorMax = story.pivot = new Vector2(.5f, 1f);
            JournalText("Encounter Description", story, description, 24, ink, FontStyle.Normal);
            ScrollRect storyScroll = storyViewport.gameObject.AddComponent<ScrollRect>();
            storyScroll.content = story; storyScroll.viewport = storyViewport; storyScroll.horizontal = false;
            storyScroll.scrollSensitivity = 28f; storyScroll.movementType = ScrollRect.MovementType.Clamped;
            JournalSurface("Encounter Decision Rule", content, new Vector2(1024, 2), new Color(.49f, .37f, .21f, .4f));
            RectTransform viewport = CreateRect("Encounter Decision Viewport", content, new Vector2(1000, 360), Vector2.zero);
            Image hit = viewport.gameObject.AddComponent<Image>(); hit.color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform actions = CreateRect("Encounter Choices", viewport, new Vector2(1000, 360), Vector2.zero);
            actions.anchorMin = actions.anchorMax = actions.pivot = new Vector2(.5f, 1f);
            RectTransform secondary = CreateRect("Encounter Secondary Actions", content, new Vector2(1024, 88), Vector2.zero);
            int number = 0;
            foreach (EncounterDossierChoice choice in choices)
                BuildJournalChoice(choice.IsSkip ? secondary : actions, choice, choice.IsSkip ? 0 : ++number);
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = actions; scroll.viewport = viewport; scroll.horizontal = false;
            scroll.scrollSensitivity = 32f; scroll.movementType = ScrollRect.MovementType.Clamped;
            RectTransform barRoot = CreateRect("Encounter Decision Scrollbar", content, new Vector2(10, 360), Vector2.zero);
            Image track = barRoot.gameObject.AddComponent<Image>(); track.color = new Color(.38f, .29f, .18f, .15f);
            RectTransform handle = CreateRect("Handle", barRoot, Vector2.zero, Vector2.zero);
            Image thumb = handle.gameObject.AddComponent<Image>(); thumb.color = accent;
            Scrollbar bar = barRoot.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle; bar.targetGraphic = thumb; bar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            NavigationDashboardLayout.Attach(contentRoot);
            card.gameObject.AddComponent<EncounterDossierRevealMotion>().Play(card, .24f, !editorPreviewBuilding);
        }

        private void BuildJournalChoice(RectTransform parent, EncounterDossierChoice choice, int number)
        {
            Button button = CreateButton(choice.ObjectName, parent, new Vector2(560, 112), Vector2.zero,
                Color.white, MarketShopSkin.Ink, 24);
            RectTransform row = button.GetComponent<RectTransform>();
            MarketShopSkin.ApplyLightButton(button);
            button.interactable = choice.Available;
            Color rowAccent = choice.Available
                ? choice.StartsBattle || choice.ObjectName == "Emergency Encounter Return" ? Html("#95523B") : Html("#8F6C3E")
                : Html("#988C77");
            Color ink = choice.Available ? MarketShopSkin.Ink : Html("#786D5B");
            JournalSurface("Choice Accent", row, new Vector2(4, 112), rowAccent);
            JournalSurface("Time Rule", row, new Vector2(1, 64), new Color(.49f, .37f, .21f, .25f));
            JournalText("Decision Number", row, choice.IsSkip ? "→" : number.ToString("00"), 24, rowAccent);
            Text label = button.GetComponentInChildren<Text>();
            label.text = choice.Label; label.alignment = TextAnchor.MiddleLeft; label.color = ink;
            JournalText("Detail", row, choice.Detail, 20, MarketShopSkin.MutedInk, FontStyle.Normal);
            Text hours = JournalText("Hours", row, choice.IsSkip ?
                choice.ObjectName == "Emergency Encounter Return" ? "返回港口" : "继续航行" :
                (choice.StartsBattle ? "进入海战\n" : "耗时\n") + choice.Hours.ToString("0.#") + " 小时", 20, rowAccent);
            hours.alignment = TextAnchor.MiddleCenter;
            if (!choice.IsSkip)
                JournalText("Requirement", row, !choice.Available ? "暂不可选 · " + choice.LockedReason :
                    string.IsNullOrEmpty(choice.Requirement) ? "" : "条件 · " + choice.Requirement, 20,
                    choice.Available ? MarketShopSkin.MutedInk : Html("#95523B"), FontStyle.Normal);
            button.onClick.AddListener(() => choice.Resolve?.Invoke());
        }

        private Text JournalText(string name, Transform parent, string value, int size, Color color, FontStyle style = FontStyle.Bold)
        {
            Text text = EncounterText(name, parent, new Vector2(400, 40), Vector2.zero, size, color, TextAnchor.MiddleLeft, style);
            text.color = color; text.text = value;
            if (name == "Encounter Description") text.alignment = TextAnchor.UpperLeft;
            if (name == "Decision Number") text.alignment = TextAnchor.MiddleCenter;
            return text;
        }

        private Image JournalSurface(string name, Transform parent, Vector2 size, Color color)
        {
            Image image = CreateRect(name, parent, size, Vector2.zero).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false; return image;
        }
    }
}
