using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private string componentDossierOfferId;
        private int componentDossierPage;

        private bool IsComponentDossierOpen => activePortFacility == PortFacilityArtCatalog.ComponentShopFacilityIndex &&
            !componentResaleOpen && !string.IsNullOrEmpty(selectedComponentShopOfferId);

        private void BuildComponentDossier(RectTransform panel, PortComponentShopStockItem offer)
        {
            ComponentArtEntry entry = offer.Entry;
            if (componentDossierOfferId != offer.Id)
            {
                componentDossierOfferId = offer.Id;
                componentDossierPage = 0;
            }
            Color rarity = ComponentArtCatalog.RarityColor(entry.Rarity);
            Color ink = Html("#352315");
            Color muted = Html("#6C4A2C");
            Color rarityInk = Color.Lerp(rarity, ink, 0.58f);
            Color buttonPaper = new Color(1f, 0.91f, 0.7f, 1f);
            bool sold = PlayerPrefs.GetInt(ComponentShopPurchaseKey(offer.Id), 0) != 0;
            bool affordable = session.ProfileGold >= offer.Price;
            RectTransform detail = CreateRect("Component Shop Detail", panel, new Vector2(1820f, 650f), Vector2.zero);
            RawImage paper = CreateRawImage("Component Detail Parchment", detail, new Vector2(1780f, 705f), new Vector2(0f, -4f),
                portArt.TavernCrewDossierParchment, new Rect(0f, 0f, 1f, 1f));
            paper.raycastTarget = false;

            // Keep the established title position while sharing the reviewed
            // crew sheet's writing column and safe space above the footer.
            RectTransform profile = CreateRect("Component Detail Profile", detail, new Vector2(1060f, 615f), new Vector2(212f, -10f));
            Text name = DossierCardText("Component Detail Name", profile, 390f, 52f, new Vector2(-182f, 220f), 36, ink, entry.Name, TextAnchor.MiddleLeft);
            GameTypography.Apply(name, 36, FontStyle.Bold);
            Text meta = DossierCardText("Component Detail Meta", profile, 450f, 52f, new Vector2(255f, 220f), 22, rarityInk,
                ComponentArtCatalog.RarityName(entry.Rarity) + " · " + ComponentArtCatalog.SlotName(entry.Slot), TextAnchor.MiddleLeft);
            GameTypography.Apply(meta, 22, FontStyle.Bold);

            string[] pageNames = { "Overview", "Effects", "Story", "SpecialCards" };
            string[] pageTitles = { "部件来历", "装配效果", "航海异闻", "专属卡牌" };
            RectTransform[] pages = new RectTransform[pageNames.Length];
            Button[] tabs = new Button[pageNames.Length];
            for (int index = 0; index < pages.Length; index++)
            {
                pages[index] = CreateRect("Component Dossier " + pageNames[index] + " Page", profile,
                    new Vector2(930f, index == 3 ? 368f : 326f), new Vector2(0f, index == 3 ? -52f : -42f));
                tabs[index] = CreateButton("ComponentDossierPage" + pageNames[index], profile,
                    new Vector2(216f, 42f), new Vector2((index - 1.5f) * 238f, 160f), new Color(0.42f, 0.27f, 0.14f, 0.12f), muted, 24);
                tabs[index].GetComponentInChildren<Text>().text = pageTitles[index];
            }
            CreateTavernReadingText("Component Detail Specialization", pages[0], new Vector2(20f, 8f), new Vector2(880f, 42f), 24, 1.1f, muted,
                "专精方向　" + entry.Specialization);
            CreateTavernReadingText("Component Detail Description", pages[0], new Vector2(20f, 68f),
                new Vector2(880f, 240f), 26, 1.25f, ink, TavernSentenceParagraphs(entry.Description));
            CreateTavernReadingText("Component Detail Effect Heading", pages[1], new Vector2(20f, 8f),
                new Vector2(880f, 38f), 26, 1.1f, rarityInk, "装配效果");
            CreateTavernReadingText("Component Detail Effect", pages[1], new Vector2(20f, 56f),
                new Vector2(880f, 250f), 26, 1.25f, ink,
                entry.Effect + "\n\n" + ComponentArtCatalog.EnvironmentSummary(entry));
            CreateTavernReadingText("Component Detail Story Chapter", pages[2], new Vector2(20f, 8f),
                new Vector2(880f, 38f), 26, 1.1f, rarityInk, entry.StoryTitle);
            CreateTavernReadingText("Component Detail Story Body", pages[2], new Vector2(20f, 56f),
                new Vector2(880f, 250f), 26, 1.25f, ink, TavernSentenceParagraphs(entry.StoryBody));
            BuildReadingDossierSpecialCard(pages[3], "Component", CoreSpecialCardCatalog.ForComponent(entry.Id),
                string.Empty, ink, muted);

            RectTransform assembly = CreateRect("Component Detail Display Assembly", detail, new Vector2(390f, 422f), new Vector2(-580f, -22f));
            BuildComponentShopDisplay(assembly, entry, "Component Detail ", string.Empty,
                sold, true, false, false, false, out _, out _, out _);
            // Page tabs own navigation; center the two purchase actions below the reading column.
            string purchaseStatus = sold ? "已售出" : affordable ? offer.Price + " 金币" : "金币不足";
            Button purchase = CreateButton("Purchase Component " + offer.Id, detail, new Vector2(240f, 44f), new Vector2(80f, -242f), Gold, buttonPaper, 20);
            purchase.GetComponentInChildren<Text>().text = "购买 · " + purchaseStatus;
            purchase.interactable = !sold && affordable;
            purchase.onClick.AddListener(() => PurchaseComponentOffer(offer, false));
            Button purchaseAndEquip = CreateButton("Purchase And Equip Component " + offer.Id, detail, new Vector2(240f, 44f), new Vector2(344f, -242f), rarity, buttonPaper, 20);
            purchaseAndEquip.GetComponentInChildren<Text>().text = "购买后装配 · " + purchaseStatus;
            purchaseAndEquip.interactable = !sold && affordable &&
                (!Application.isPlaying || editorPreviewBuilding || CanRefitShip(session.SelectedShipIndex));
            purchaseAndEquip.onClick.AddListener(() => PurchaseComponentOffer(offer, true));
            for (int index = 0; index < pages.Length; index++)
            {
                int captured = index;
                tabs[index].onClick.AddListener(() => ShowComponentDossierPage(captured, pages, tabs));
            }
            ShowComponentDossierPage(componentDossierPage, pages, tabs);
        }

        private void ShowComponentDossierPage(int selected, RectTransform[] pages, Button[] tabs)
        {
            componentDossierPage = Mathf.Clamp(selected, 0, pages.Length - 1);
            for (int index = 0; index < pages.Length; index++)
            {
                pages[index].gameObject.SetActive(index == componentDossierPage);
                WoodButtonSkin.SetSelected(tabs[index], index == componentDossierPage);
            }
        }

        private static void KeepComponentPunctuationWithSentence(Text label)
        {
            if (label.name != "Component Detail Description" && label.name != "Component Detail Story Body" &&
                label.name != "Component Special Card Lore") return;
            // Legacy UI Text wraps Chinese punctuation as ordinary characters.
            // Move the preceding character with a stranded closing mark; only
            // add line breaks, preserving the authored narrative verbatim.
            const string closing = "，。；：！？、）】》”’」』";
            TextGenerationSettings settings = label.GetGenerationSettings(label.rectTransform.rect.size);
            settings.scaleFactor = 1f;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            settings.generateOutOfBounds = true;
            using (TextGenerator generator = new TextGenerator())
            {
                for (int pass = 0; pass < 16; pass++)
                {
                    generator.Populate(label.text, settings);
                    int insert = -1;
                    foreach (UILineInfo line in generator.lines)
                    {
                        int start = line.startCharIdx;
                        if (start <= 0 || start >= label.text.Length || closing.IndexOf(label.text[start]) < 0) continue;
                        insert = start - 1;
                        while (insert > 0 && closing.IndexOf(label.text[insert]) >= 0) insert--;
                        if (insert == 0 || char.IsWhiteSpace(label.text[insert]) || label.text[insert - 1] == '\n') { insert = -1; continue; }
                        if (char.IsLowSurrogate(label.text[insert])) insert--;
                        break;
                    }
                    if (insert < 0) break;
                    label.text = label.text.Insert(insert, "\n");
                }
            }
        }
    }
}
