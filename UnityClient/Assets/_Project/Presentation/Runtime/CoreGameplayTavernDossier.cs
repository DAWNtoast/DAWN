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

        private bool IsTavernDossierOpen => activePortFacility == PortFacilityArtCatalog.TavernFacilityIndex &&
            selectedTavernCandidate >= 0;

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
            RectTransform detail = CreateRect("Tavern Crew Detail", contentRoot, new Vector2(1820f, 650f), new Vector2(0f, -55f));
            BuildTavernDossierArt(detail, crew);

            // Both art modes share the approved writing bay and its controls.
            // Keep every detail control clear of the wax seals.
            RectTransform profile = CreateFixedPanel("Tavern Crew Detail Profile", detail, new Vector2(1060f, 615f), new Vector2(212f, -10f), Color.clear);
            profile.GetComponent<Image>().raycastTarget = false;
            // Keep the name's original left edge and baseline. Its narrower
            // rectangle makes room for the red role summary on the same row.
            Text name = CreateText("Tavern Crew Detail Name", profile, new Vector2(390f, 52f), new Vector2(-182f, 220f), 36, FontStyle.Bold, TextAnchor.MiddleLeft, parchmentInk);
            name.text = crew.DisplayName;
            Text role = CreateText("Tavern Crew Detail Role", profile, new Vector2(450f, 52f), new Vector2(255f, 220f), 22, FontStyle.Bold, TextAnchor.MiddleLeft, parchmentAccent);
            role.text = crew.PortName + "　·　" + crew.RoleSummary;

            string[] pageNames = { "Background", "Story", "Skills", "SpecialCards" };
            string[] pageTitles = { "人物背景", "航海异闻", "职业技能", "专属卡牌" };
            RectTransform[] pages = new RectTransform[pageNames.Length];
            Button[] tabs = new Button[pageNames.Length];
            for (int page = 0; page < pages.Length; page++)
            {
                // The tabs and top of every page rise one text line. Extend
                // each page upward so its lower edge stays above the footer.
                pages[page] = CreateRect("Tavern Dossier " + pageNames[page] + " Page", profile, new Vector2(930f, page == 3 ? 368f : 326f), new Vector2(0f, page == 3 ? -52f : -42f));
                tabs[page] = CreateButton("DossierPage" + pageNames[page], profile, new Vector2(216f, 42f), new Vector2((page - 1.5f) * 238f, 160f), new Color(0.42f, 0.27f, 0.14f, 0.12f), parchmentMuted, 24);
                tabs[page].GetComponentInChildren<Text>().text = pageTitles[page];
            }

            CreateTavernReadingText("Tavern Crew Biography", pages[0], new Vector2(20f, 8f),
                new Vector2(880f, 304f), 27, 1.25f, parchmentInk, TavernSentenceParagraphs(crew.Biography));
            CreateTavernReadingText("Tavern Crew Story Heading", pages[1], new Vector2(0f, 8f),
                new Vector2(860f, 38f), 25, 1.1f, parchmentAccent, story.ChapterLabel);
            CreateTavernReadingText("Tavern Crew Story", pages[1], new Vector2(20f, 56f),
                new Vector2(880f, 270f), 26, 1.22f, parchmentInk, TavernSentenceParagraphs(story.Body));
            CreateTavernReadingText("Tavern Crew Skill Heading", pages[2], new Vector2(0f, 8f),
                new Vector2(850f, 40f), 28, 1.1f, parchmentInk, "专长 · " + crew.Specialty + "　｜　职业能力");
            CreateTavernReadingText("Tavern Crew Skill", pages[2], new Vector2(20f, 64f),
                new Vector2(850f, 232f), 26, 1.3f, parchmentAccent, TavernSkillParagraphs(crew.SkillDescription));
            bool recruited = IsCrewRecruitedForView(crew);
            bool affordable = session.ProfileGold >= crew.ContractCost;
            bool active = recruited && (IsRecruitmentFlowPreview || ActiveCrewForProfession(safe).Id == crew.Id);

            BuildTavernDossierSpecialCard(pages[3], CoreSpecialCardCatalog.ForCrew(crew.Id),
                "这位船员不提供战斗专属卡牌。职业专长仍然生效。", parchmentInk, parchmentMuted);

            // The four named tabs provide navigation; the footer holds only the crew action.
            Button recruit = CreateButton("Recruit Tavern Crew " + safe, detail, new Vector2(330f, 44f), new Vector2(427f, -242f), recruited ? new Color(0.38f, 0.28f, 0.18f, 0.7f) : parchmentAccent, recruited ? new Color(0.78f, 0.67f, 0.49f, 1f) : buttonPaper, 22);
            recruit.GetComponentInChildren<Text>().text = recruited ? active ? "当前职业代表" : "设为职业代表" : "签署契约 · " + crew.ContractCost + " 金币";
            recruit.interactable = recruited ? !active : affordable;
            recruit.onClick.AddListener(() =>
            {
                if (!Application.isPlaying || editorPreviewBuilding) return;
                if (!recruited) RecruitTavernCrew(safe, crew);
                else
                {
                    PlayerPrefs.SetString(ActiveCrewVariantKey(safe), crew.Id);
                    SaveProgress();
                    portNotice = "「" + crew.DisplayName + "」已设为职业代表。";
                    Render();
                }
            });

            for (int page = 0; page < pages.Length; page++)
            {
                int capturedPage = page;
                tabs[page].onClick.AddListener(() => ShowTavernDossierPage(capturedPage, pages, tabs));
            }
            ShowTavernDossierPage(tavernDossierPage, pages, tabs);
        }

        // Page navigation changes presentation only. In particular it must not
        // rebuild the game session, save progress, or invoke recruitment.
        private void ShowTavernDossierPage(int page, RectTransform[] pages, Button[] tabs)
        {
            tavernDossierPage = Mathf.Clamp(page, 0, pages.Length - 1);
            for (int index = 0; index < pages.Length; index++)
            {
                bool selected = index == tavernDossierPage;
                pages[index].gameObject.SetActive(selected);
                WoodButtonSkin.SetSelected(tabs[index], selected);
            }
        }
    }
}
