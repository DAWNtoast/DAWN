using System.Linq;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private int tavernDossierPage;
        private string tavernDossierCrewId;

        private void ResetTavernDossierView()
        {
            selectedTavernCandidate = -1;
            tavernDossierPage = 0;
            tavernDossierCrewId = null;
        }

        // Keep this entry point stable for the editor preview and UI tooling.
        private void BuildTavernCrewDetail(int index)
        {
            int safe = Mathf.Clamp(index, 0, TavernCrewSlotCount - 1);
            CrewVariantDefinition crew = CurrentTavernCrew(safe);
            CrewStoryDefinition story = CrewNarrativeCatalog.StoryFor(crew);
            if (tavernDossierCrewId != crew.Id)
            {
                tavernDossierCrewId = crew.Id;
                tavernDossierPage = 0;
            }

            Color parchmentInk = Html("#352315");
            Color parchmentMuted = Html("#6C4A2C");
            Color parchmentAccent = Html("#8B2F24");
            Color buttonPaper = new Color(1f, 0.91f, 0.7f, 1f);
            RectTransform detail = CreateFixedPanel("Tavern Crew Detail", contentRoot, new Vector2(1820f, 650f), new Vector2(0f, -55f), new Color(0.03f, 0.02f, 0.015f, 0.48f));
            detail.GetComponent<Image>().raycastTarget = false;
            RawImage parchment = CreateRawImage("Tavern Dossier Parchment", detail, new Vector2(1780f, 625f), new Vector2(0f, -4f), portArt.TavernCrewDossierParchment, new Rect(0f, 0f, 1f, 1f));
            parchment.raycastTarget = false;

            // These usable bays were measured inside the parchment's painted
            // rules, curled edges and seals, rather than its rectangular PNG.
            Button back = CreateButton("Back To Tavern Crew Lineup", detail, new Vector2(230f, 50f * GameReadability.Scale), new Vector2(-660f, 326f), new Color(0.24f, 0.12f, 0.055f, 0.96f), buttonPaper, GameReadability.FontSize(22));
            back.GetComponentInChildren<Text>().text = "← 返回酒馆";
            GameReadability.Body(back.GetComponentInChildren<Text>(), 22);
            back.onClick.AddListener(() =>
            {
                ResetTavernDossierView();
                portNotice = "请选择要了解的特殊船员。";
                Render();
            });

            RectTransform portraitFrame = CreateFixedPanel("Tavern Crew Detail Portrait Frame", detail, new Vector2(390f, 422f), new Vector2(-580f, -22f), Color.clear);
            portraitFrame.GetComponent<Image>().raycastTarget = false;
            // Preserve this original object name for preview/test compatibility.
            RawImage fullBody = CreateRawImage("Tavern Crew Detail Full Body", portraitFrame, new Vector2(340f, 340f), Vector2.zero, portArt.CrewPortrait(crew), portArt.CrewPortraitUv(crew));
            fullBody.raycastTarget = false;
            FitCrewPortrait(fullBody, new Vector2(340f, 340f));
            TavernPortraitPaperBlend.Attach(fullBody);
            RectTransform profile = CreateFixedPanel("Tavern Crew Detail Profile", detail, new Vector2(1060f, 535f), new Vector2(300f, -10f), Color.clear);
            profile.GetComponent<Image>().raycastTarget = false;
            Text name = CreateText("Tavern Crew Detail Name", profile, new Vector2(930f, 60f), new Vector2(0f, 220f), GameReadability.FontSize(36), FontStyle.Bold, TextAnchor.MiddleLeft, parchmentInk);
            name.text = crew.DisplayName;
            GameReadability.Body(name, 36);
            name.rectTransform.anchoredPosition = new Vector2(0f, 217f);
            Text role = CreateText("Tavern Crew Detail Role", profile, new Vector2(930f, 40f), new Vector2(0f, 166f), GameReadability.FontSize(22), FontStyle.Bold, TextAnchor.MiddleLeft, parchmentAccent);
            role.text = crew.PortName + "　·　" + crew.RoleSummary;
            GameReadability.Body(role, 22);
            if (GameReadability.Level > 0)
            {
                name.rectTransform.sizeDelta = new Vector2(930f, 60f * GameReadability.Scale);
                name.rectTransform.anchoredPosition = new Vector2(0f, 230f);
                role.rectTransform.sizeDelta = new Vector2(930f, 58f);
                role.rectTransform.anchoredPosition = new Vector2(0f, 144f);
            }

            string[] pageNames = { "Background", "Story", "Skills", "SpecialCards" };
            string[] pageTitles = { "人物背景", "航海异闻", "职业技能", "专属卡牌" };
            RectTransform[] pages = new RectTransform[pageNames.Length];
            Button[] tabs = new Button[pageNames.Length];
            for (int page = 0; page < pages.Length; page++)
            {
                pages[page] = CreateRect("Tavern Dossier " + pageNames[page] + " Page", profile, new Vector2(930f, page == 3 ? 280f : 250f), new Vector2(0f, -48f));
                tabs[page] = CreateButton("DossierPage" + pageNames[page], profile, new Vector2(216f, 46f), new Vector2((page - 1.5f) * 238f, 120f), new Color(0.42f, 0.27f, 0.14f, 0.12f), parchmentMuted, GameReadability.FontSize(24));
                tabs[page].GetComponentInChildren<Text>().text = pageTitles[page];
                GameReadability.Body(tabs[page].GetComponentInChildren<Text>(), 24);
                if (GameReadability.Level > 0)
                {
                    tabs[page].GetComponent<RectTransform>().sizeDelta = new Vector2(216f, 46f * GameReadability.Scale);
                    tabs[page].GetComponent<RectTransform>().anchoredPosition = new Vector2((page - 1.5f) * 238f, 71f);
                    tabs[page].GetComponentInChildren<Text>().rectTransform.sizeDelta = new Vector2(200f, 46f * GameReadability.Scale - 8f);
                    pages[page].sizeDelta = new Vector2(930f, 200f);
                    pages[page].anchoredPosition = new Vector2(0f, -77f);
                }
            }

            Text biography = CreateTavernDossierText("Tavern Crew Biography", pages[0], 230f, -48f, 28,
                parchmentInk, FormatCrewBiography(crew.Biography));
            biography.rectTransform.sizeDelta = new Vector2(870f, 230f);
            biography.lineSpacing = 1.2f;

            CreateTavernDossierText("Tavern Crew Story Heading", pages[1], 38f, 40f, 28, parchmentAccent, story.ChapterLabel);
            Text storyBody = CreateTavernDossierText("Tavern Crew Story", pages[1], 176f, -80f, 26, parchmentInk, story.Body);
            storyBody.rectTransform.sizeDelta = new Vector2(870f, 176f);
            storyBody.lineSpacing = 1.1f;

            CreateTavernDossierText("Tavern Crew Skill Heading", pages[2], 38f, 40f, 28, parchmentInk, "专长 · " + crew.Specialty + "　｜　职业能力");
            CreateTavernDossierText("Tavern Crew Skill", pages[2], 74f, -21f, 26, parchmentAccent, crew.SkillDescription);
            CreateTavernDossierText("Tavern Crew Stats", pages[2], 36f, -93f, 26, parchmentInk, crew.Stats);
            bool recruited = IsCrewRecruitedForView(crew);
            bool affordable = session.ProfileGold >= crew.ContractCost;
            bool active = recruited && (IsRecruitmentFlowPreview || ActiveCrewForProfession(safe).Id == crew.Id);
            string contractNote = recruited ? "已签署个人契约；可设为职业代表参加派遣。" : affordable ? "签约后启用职业能力；同职业不叠加，个人名册独立保留。" : "金币不足，还需 " + (crew.ContractCost - session.ProfileGold) + " 金币。";
            CreateTavernDossierText("Tavern Crew Contract Note", pages[2], 42f, -144f, 22, recruited ? Html("#35653A") : parchmentMuted, contractNote);

            BuildDossierSpecialCard(pages[3], "Tavern", CoreSpecialCardCatalog.ForCrew(crew.Id), 930f,
                "这位船员不提供战斗专属卡牌。职业专长与派遣能力仍然生效。", parchmentInk, parchmentMuted);

            // Shared footer: x -200..688, y -237..-181. Keep navigation and
            // recruitment inside this lane and clear of the bottom-right seal.
            Button previous = CreateButton("Previous Tavern Dossier Page", detail, new Vector2(130f, 44f), new Vector2(-95f, -210f), new Color(0.42f, 0.27f, 0.14f, 0.18f), parchmentInk, 22);
            previous.GetComponentInChildren<Text>().text = "← 上一页";
            Text counter = CreateText("Tavern Dossier Page Counter", detail, new Vector2(160f, 44f), new Vector2(80f, -210f), 22, FontStyle.Normal, TextAnchor.MiddleCenter, parchmentMuted);
            Button next = CreateButton("Next Tavern Dossier Page", detail, new Vector2(130f, 44f), new Vector2(255f, -210f), new Color(0.42f, 0.27f, 0.14f, 0.18f), parchmentInk, 22);
            next.GetComponentInChildren<Text>().text = "下一页 →";
            Button recruit = CreateButton("Recruit Tavern Crew " + safe, detail, new Vector2(330f, 44f), new Vector2(515f, -210f), recruited ? new Color(0.38f, 0.28f, 0.18f, 0.7f) : parchmentAccent, recruited ? new Color(0.78f, 0.67f, 0.49f, 1f) : buttonPaper, 22);
            recruit.GetComponentInChildren<Text>().text = recruited ? active ? "当前职业代表" : "设为职业代表" : "签署契约 · " + crew.ContractCost + " 金币";
            foreach (Button footer in new[] { previous, next, recruit })
            {
                GameReadability.Body(footer.GetComponentInChildren<Text>(), 22);
                RectTransform box = footer.GetComponent<RectTransform>();
                box.sizeDelta = new Vector2(box.sizeDelta.x, 44f * GameReadability.Scale);
                footer.GetComponentInChildren<Text>().rectTransform.sizeDelta = box.sizeDelta - new Vector2(12f, 6f);
                if (GameReadability.Level > 0) box.anchoredPosition = new Vector2(box.anchoredPosition.x, -246f);
            }
            GameReadability.Body(counter, 18);
            counter.rectTransform.sizeDelta = new Vector2(160f, 44f * GameReadability.Scale);
            if (GameReadability.Level > 0)
            {
                counter.rectTransform.anchoredPosition = new Vector2(80f, -246f);
                previous.GetComponentInChildren<Text>().text = "上一页";
                next.GetComponentInChildren<Text>().text = "下一页";
                recruit.GetComponentInChildren<Text>().text = recruited ? active ? "当前职业代表" : "设为职业代表" : "签约 · " + crew.ContractCost + " 金币";
            }
            recruit.interactable = recruited ? !active : affordable;
            recruit.onClick.AddListener(() =>
            {
                if (!Application.isPlaying || editorPreviewBuilding) return;
                if (!recruited) RecruitTavernCrew(safe, crew);
                else
                {
                    PlayerPrefs.SetString(ActiveCrewVariantKey(safe), crew.Id);
                    SaveProgress();
                    portNotice = "「" + crew.DisplayName + "」已设为职业代表；正在执行的派遣不会变更人员。";
                    Render();
                }
            });

            for (int page = 0; page < pages.Length; page++)
            {
                int capturedPage = page;
                tabs[page].onClick.AddListener(() => ShowTavernDossierPage(capturedPage, pages, tabs, previous, next, counter));
            }
            previous.onClick.AddListener(() => ShowTavernDossierPage(tavernDossierPage - 1, pages, tabs, previous, next, counter));
            next.onClick.AddListener(() => ShowTavernDossierPage(tavernDossierPage + 1, pages, tabs, previous, next, counter));
            if (GameReadability.Level > 0) WrapTavernDossierPages(pages);
            ShowTavernDossierPage(tavernDossierPage, pages, tabs, previous, next, counter);
        }

        private static string FormatCrewBiography(string biography)
        {
            if (string.IsNullOrEmpty(biography) || biography.IndexOf('\n') >= 0) return biography ?? string.Empty;
            // Separate the introduction from its continuation at a natural clause boundary.
            // Short, single-sentence profiles fall back to a comma instead of a fixed character count.
            foreach (char punctuation in new[] { '。', '！', '？', '；', '，' })
            {
                int split = biography.IndexOf(punctuation);
                if (split <= 0 || split >= biography.Length - 1) continue;
                split++;
                while (split < biography.Length && "”’」』".IndexOf(biography[split]) >= 0) split++;
                if (split < biography.Length)
                    return biography.Substring(0, split) + "\n\n" + biography.Substring(split);
            }
            return biography;
        }

        private Text CreateTavernDossierText(string objectName, Transform parent, float height, float y, int fontSize, Color color, string value)
        {
            Text label = CreateText(objectName, parent, new Vector2(930f, height), new Vector2(0f, y + 48f), GameReadability.FontSize(fontSize), FontStyle.Normal, TextAnchor.UpperLeft, color);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = false;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.text = value;
            return label;
        }

        private static void WrapTavernDossierPages(RectTransform[] pages)
        {
            foreach (RectTransform page in pages)
            {
                Text[] text = page.GetComponentsInChildren<Text>(true);
                Image hit = page.GetComponent<Image>() ?? page.gameObject.AddComponent<Image>();
                hit.color = Color.clear; hit.raycastTarget = true;
                page.gameObject.AddComponent<RectMask2D>();
                ScrollRect scroll = page.gameObject.AddComponent<ScrollRect>();
                scroll.viewport = page; scroll.horizontal = false; scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f;
                RectTransform content = new GameObject(page.name + " Content", typeof(RectTransform)).GetComponent<RectTransform>();
                content.SetParent(page, false); content.anchorMin = content.anchorMax = new Vector2(.5f, 1f); content.pivot = new Vector2(.5f, 1f);
                float top = 12f;
                RawImage art = page.GetComponentInChildren<RawImage>(true);
                if (art != null)
                {
                    art.transform.SetParent(content, false);
                    art.rectTransform.anchorMin = art.rectTransform.anchorMax = art.rectTransform.pivot = new Vector2(.5f, 1f);
                    art.rectTransform.sizeDelta = new Vector2(112f, 112f);
                    art.rectTransform.anchoredPosition = new Vector2(0f, -top);
                    top += 124f;
                }
                foreach (Text label in text)
                {
                    if (label.name.Contains("Special Card"))
                        GameReadability.Body(label, label.name.EndsWith("Name") ? 32 :
                            label.name.EndsWith("Stage") || label.name.EndsWith("Growth") ? 20 : 22);
                    label.transform.SetParent(content, false);
                    label.alignment = TextAnchor.UpperLeft;
                    label.verticalOverflow = VerticalWrapMode.Overflow;
                    label.resizeTextForBestFit = false;
                    label.rectTransform.sizeDelta = new Vector2(900f, 200f);
                    float height = MeasureCardTextHeight(label);
                    PlaceCardEffectParagraph(label, 900f, height, top);
                    top += height + 12f;
                }
                // All prose belongs to the outer reading pane. Remove empty
                // nested effect hit surfaces, which otherwise swallow its wheel.
                foreach (Transform child in page.Cast<Transform>().ToArray())
                    if (child != content) child.gameObject.SetActive(false);
                content.sizeDelta = new Vector2(page.rect.width, Mathf.Max(page.rect.height, top));
                content.anchoredPosition = Vector2.zero;
                scroll.content = content; scroll.verticalNormalizedPosition = 1f;
            }
        }

        // Page navigation changes presentation only. In particular it must not
        // rebuild the game session, save progress, or invoke recruitment.
        private void ShowTavernDossierPage(int page, RectTransform[] pages, Button[] tabs, Button previous, Button next, Text counter)
        {
            tavernDossierPage = Mathf.Clamp(page, 0, pages.Length - 1);
            for (int index = 0; index < pages.Length; index++)
            {
                bool selected = index == tavernDossierPage;
                pages[index].gameObject.SetActive(selected);
                WoodButtonSkin.SetSelected(tabs[index], selected);
            }
            previous.interactable = tavernDossierPage > 0;
            next.interactable = tavernDossierPage < pages.Length - 1;
            counter.text = "第 " + (tavernDossierPage + 1) + " / " + pages.Length + " 页";
        }
    }
}
