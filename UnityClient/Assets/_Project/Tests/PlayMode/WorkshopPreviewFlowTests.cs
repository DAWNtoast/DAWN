using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator WorkshopPreview_DefaultSixCardsNavigationAndInsufficientGoldStayReadOnly()
        {
            using var storage = new VoyageCargoStorageScope();
            // Seed a legal owned collection before the production controller loads it.
            // Read-only UI must never be tested by mutating its live save-backed inventory.
            var fixture = new CoreVoyageSession();
            fixture.RestoreTreasureProgress(new CoreTreasureProgress
                { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            CoreCardPackProgress fixtureCards = fixture.CaptureCardPackProgress();
            fixtureCards.randomState = 1234567u;
            fixtureCards.cards = fixture.GetCardPackPool("fire")
                .Where(index => CoreVoyageSession.CardLibrary[index].SchoolId == "fire")
                .Select(index => new CoreCardCopySave { id = CoreVoyageSession.CardLibrary[index].Id, count = 1 }).ToArray();
            Assert.That(fixture.RestoreCardPackProgress(fixtureCards), Is.True, "The fixture must own legal authored fire cards.");
            PlayerPrefs.SetString(CoreGameplayDemoController.TreasureProgressKey, JsonUtility.ToJson(fixture.CaptureTreasureProgress()));
            PlayerPrefs.SetString(CoreGameplayDemoController.CardPackProgressKey, JsonUtility.ToJson(fixture.CaptureCardPackProgress()));
            PlayerPrefs.Save();
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            CrewVariantDefinition navigator = CrewNarrativeCatalog.ForPortAndProfession("灰鸥港", 0);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(navigator.Id), 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.ActiveCrewVariantKey(navigator.ProfessionIndex), navigator.Id);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            NavalBattleStage[] stages = { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding };
            Dictionary<NavalBattleStage, int[]> decks = stages.ToDictionary(stage => stage, stage => controller.Session.GetBattleDeck(stage).ToArray());
            Dictionary<NavalBattleStage, string> saves = stages.ToDictionary(stage => stage,
                stage => PlayerPrefs.GetString(CoreGameplayDemoController.StageBattleDeckKey(stage), string.Empty));
            decimal gold = controller.Session.ProfileGold;
            string upgrades = PlayerPrefs.GetString(CoreGameplayDemoController.BattleCardUpgradeKey, string.Empty);
            bool hadCardPackSave = PlayerPrefs.HasKey(CoreGameplayDemoController.CardPackProgressKey);
            string cardPackSave = PlayerPrefs.GetString(CoreGameplayDemoController.CardPackProgressKey, string.Empty);
            CoreCardPackProgress originalCardPacks = controller.Session.CaptureCardPackProgress();
            string originalCardPacksJson = JsonUtility.ToJson(originalCardPacks);
            string treasureSave = PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey, string.Empty);
            string originalTreasureJson = JsonUtility.ToJson(controller.Session.CaptureTreasureProgress());
            Assert.That(controller.Session.PirateLevel, Is.EqualTo(10));
            Assert.That(hadCardPackSave, Is.True);
            Assert.That(originalCardPacks.randomState, Is.EqualTo(1234567u));

            ActiveButton("Port Hotspot CardWorkshop").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Workshop Home Panel"), Is.Not.Null);
            Assert.That(GameObject.Find("Workshop Preview Collection"), Is.Null);
            ActiveButton("Workshop Entrance Collection").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Card Workshop Preview Panel"), Is.Not.Null);
            Assert.That(GameObject.Find("Card Workshop Chest Stage"), Is.Null);
            RawImage workshopBackground = GameObject.Find("CardWorkshop Background").GetComponent<RawImage>();
            Assert.That(workshopBackground.texture, Is.SameAs(Resources.Load<Texture2D>("CardWorkshopV2/workshop_home_v1")));
            Assert.That(workshopBackground.color.r, Is.LessThan(.5f), "The room stays dim behind the reading surfaces.");
            Assert.That(GameObject.Find("Battle Card Upgrade Panel"), Is.Null);
            Assert.That(GameObject.Find("Deck Builder Panel"), Is.Null);
            Assert.That(GameObject.Find("Workshop Preview Deck Name"), Is.Null);
            Assert.That(GameObject.Find("Workshop Preview Deck Count"), Is.Null);
            foreach (string page in new[] { "Collection", "DeckBuilder", "Packs", "Upgrade" })
                Assert.That(ActiveButton("Workshop Page " + page), Is.Not.Null);
            ActiveButton("Workshop Preview Stage Naval").onClick.Invoke();
            yield return null;
            Button[] firstPage = GameObject.Find("Workshop Preview Collection").GetComponentsInChildren<Button>()
                .Where(button => button.name.StartsWith("Select Upgrade Card ")).ToArray();
            Assert.That(firstPage, Has.Length.EqualTo(6));
            Assert.That(ActiveButton("Previous Upgrade Cards").interactable, Is.False);
            Assert.That(ActiveButton("Next Upgrade Cards").interactable, Is.True);
            yield return new WaitForSecondsRealtime(.7f);
            NavalBattleVisualCapture.Capture(controller, "WorkshopPreview_01_Overview.png",
                "四个子页导航下默认显示已有卡牌与选中卡牌详情", "Card Workshop Preview Panel");

            ActiveButton("Workshop School Filter fire").onClick.Invoke();
            yield return null;
            Button[] fireCards = GameObject.Find("Workshop Preview Collection").GetComponentsInChildren<Button>()
                .Where(button => button.name.StartsWith("Select Upgrade Card ")).ToArray();
            Assert.That(fireCards, Is.Not.Empty);
            Assert.That(fireCards.All(card => controller.Session.GetBattleCard(ForgeCardIndex(card), NavalBattleStage.Naval).SchoolId == "fire"), Is.True);
            Assert.That(fireCards.All(card => controller.Session.GetBaseBattleCardOwnedCopies(ForgeCardIndex(card)) > 0), Is.True);
            yield return new WaitForSecondsRealtime(.7f);
            NavalBattleVisualCapture.Capture(controller, "WorkshopPreview_01b_FireFilter.png",
                "左侧排列无体系与六体系筛选，火焰筛选只展示火焰牌，底部显示页数与卡牌统计", "Card Workshop Preview Panel");
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.CardPackProgressKey), Is.EqualTo(hadCardPackSave));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.CardPackProgressKey, string.Empty), Is.EqualTo(cardPackSave),
                "Filtering the owned collection must never write card or pack progress.");
            Assert.That(JsonUtility.ToJson(controller.Session.CaptureCardPackProgress()), Is.EqualTo(originalCardPacksJson),
                "Filtering preserves every owned copy, unopened pack and future random draw.");
            Assert.That(JsonUtility.ToJson(controller.Session.CaptureTreasureProgress()), Is.EqualTo(originalTreasureJson));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey, string.Empty), Is.EqualTo(treasureSave));
            ActiveButton("Workshop School Filter neutral").onClick.Invoke();
            yield return null;

            ActiveButton("Workshop Page Upgrade").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Workshop Actions"), Is.Null);
            Assert.That(GameObject.Find("Card Upgrade Heading"), Is.Null);
            Button[] numberedFirstPage = AssertForgePage(controller, 0);
            AssertForgeComparison(controller, ForgeCardIndex(numberedFirstPage[0]));
            yield return new WaitForSecondsRealtime(.7f);
            NavalBattleVisualCapture.Capture(controller, "WorkshopPreview_02_Upgrade.png",
                "独立卡牌列表旁展示放大的强化前后卡面与金币操作", "Card Workshop Upgrade Workspace");
            ActiveButton("Next Upgrade Cards").onClick.Invoke();
            yield return null;
            Button[] secondPage = AssertForgePage(controller, 1);
            Assert.That(secondPage, Is.Not.Empty);
            int chosen = ForgeCardIndex(secondPage.Last());
            secondPage.Last().onClick.Invoke();
            yield return null;
            AssertForgeComparison(controller, chosen);
            ActiveButton("Previous Upgrade Cards").onClick.Invoke();
            yield return null;
            AssertForgePage(controller, 0);
            ActiveButton("Workshop Page Collection").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Battle Card Upgrade Panel"), Is.Null);

            Dictionary<string, Vector3[]> navalFirstPageLayout = null;
            foreach (NavalBattleStage stage in stages)
            {
                ActiveButton("Workshop Preview Stage " + stage).onClick.Invoke();
                yield return null;
                Assert.That(GameObject.Find("Deck Builder Panel"), Is.Null, "The preview tab only filters cards.");
                Button[] choices = GameObject.Find("Workshop Preview Collection").GetComponentsInChildren<Button>()
                    .Where(button => button.name.StartsWith("Select Upgrade Card ")).ToArray();
                Assert.That(choices, Is.Not.Empty);
                foreach (Button choice in choices)
                    Assert.That(controller.Session.GetBattleCard(ForgeCardIndex(choice)).IsAllowedInStage(stage), Is.True);
                Dictionary<string, Vector3[]> previewLayout = CaptureWorkshopLeftLayout(false);
                if (stage == NavalBattleStage.Naval)
                {
                    navalFirstPageLayout = previewLayout;
                    yield return new WaitForSecondsRealtime(.7f);
                    NavalBattleVisualCapture.Capture(controller, "WorkshopPreview_03a_OverviewBeforeEditor.png",
                        "点击同阶段编辑前的左侧卡牌、费用图标、页签及翻页按钮位置", "Card Workshop Preview Panel");
                }
                ActiveButton("Workshop Page DeckBuilder").onClick.Invoke();
                yield return null;
                Assert.That(GameObject.Find("Deck Builder Panel"), Is.Not.Null);
                AssertWorkshopLeftLayoutUnchanged(previewLayout, true, stage + " entering editor");
                foreach (string column in new[] { "Base", "Special" })
                {
                    Assert.That(GameObject.Find("Deck Builder " + column + " Column"), Is.Not.Null,
                        "Editing keeps both deck categories visible beside the unchanged collection.");
                    Assert.That(GameObject.Find("Deck Builder " + column + " Scroll Viewport").GetComponent<ScrollRect>(), Is.Not.Null);
                }
                if (stage == NavalBattleStage.Naval)
                {
                    yield return new WaitForSecondsRealtime(.7f);
                    NavalBattleVisualCapture.Capture(controller, "WorkshopPreview_03_Editor.png",
                        "从预览直接打开阶段编辑，左侧真实卡牌位置不变，右侧基础与专属紧凑横条并列", "Deck Builder Panel");
                }
                ActiveButton("Back To Card Workshop Chests").onClick.Invoke();
                yield return null;
                Assert.That(GameObject.Find("Card Workshop Preview Panel"), Is.Not.Null);
                AssertWorkshopLeftLayoutUnchanged(previewLayout, false, stage + " returning from editor");
            }

            ActiveButton("Workshop Preview Stage Naval").onClick.Invoke();
            yield return null;
            ActiveButton("Next Upgrade Cards").onClick.Invoke();
            yield return null;
            Dictionary<string, Vector3[]> navalSecondPageLayout = CaptureWorkshopLeftLayout(false);
            Assert.That(ActiveButton("Previous Upgrade Cards").interactable, Is.True);
            yield return new WaitForSecondsRealtime(.7f);
            NavalBattleVisualCapture.Capture(controller, "WorkshopPreview_05_SecondPageBeforeEditor.png",
                "总览第二页进入编辑前，切换应保留当前页和全部卡面位置", "Card Workshop Preview Panel");
            ActiveButton("Workshop Page DeckBuilder").onClick.Invoke();
            yield return null;
            AssertWorkshopLeftLayoutUnchanged(navalSecondPageLayout, true, "second page entering editor");
            Assert.That(ActiveButton("Previous Battle Card Collection Page").interactable, Is.True);
            Assert.That(GameObject.Find("Battle Card Collection Page"), Is.Null);
            yield return new WaitForSecondsRealtime(.7f);
            NavalBattleVisualCapture.Capture(controller, "WorkshopPreview_06_SecondPageEditor.png",
                "编辑页保留总览第二页的相同卡牌和图标位置", "Deck Builder Panel");
            ActiveButton("Back To Card Workshop Chests").onClick.Invoke();
            yield return null;
            AssertWorkshopLeftLayoutUnchanged(navalSecondPageLayout, false, "second page returning from editor");
            Assert.That(GameObject.Find("Upgrade Collection Page"), Is.Null);
            Assert.That(ActiveButton("Previous Upgrade Cards").interactable, Is.True);
            ActiveButton("Workshop Page DeckBuilder").onClick.Invoke();
            yield return null;
            ActiveButton("Previous Battle Card Collection Page").onClick.Invoke();
            yield return null;
            AssertWorkshopLeftLayoutUnchanged(navalFirstPageLayout, true, "editor changes back to first page");
            ActiveButton("Back To Card Workshop Chests").onClick.Invoke();
            yield return null;
            AssertWorkshopLeftLayoutUnchanged(navalFirstPageLayout, false, "return preserves editor's changed page");
            Assert.That(ActiveButton("Previous Upgrade Cards").interactable, Is.False);
            controller.Session.AcquireSpecialCards(CoreSpecialCardCatalog.All.Take(12).Select(item => item.Card.Id).ToArray());
            string[] acquired = controller.Session.AcquiredSpecialCardIds.Where(id =>
                CardSchoolArtCatalog.NormalizeSchool(CoreVoyageSession.CardLibrary.First(card => card.Id == id).SchoolId) == "neutral").ToArray();
            Dictionary<string, Vector3[]> shell = CaptureWorkshopLeftLayout(false)
                .Where(item => item.Key.StartsWith("tab/") || item.Key.StartsWith("page/")).ToDictionary(item => item.Key, item => item.Value);
            ActiveButton("Open Special Card Collection").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Special Card Collection Panel"), Is.Null);
            Assert.That(GameObject.Find("Card Workshop Preview Panel"), Is.Not.Null);
            Assert.That(GameObject.Find("Workshop Collection Detail"), Is.Not.Null);
            Assert.That(ActiveButton("Confirm Battle Card Upgrade"), Is.Null);
            Assert.That(GameObject.Find("Battle Card Upgrade Panel"), Is.Null, "Even a stale callback cannot enter paid upgrades for special cards.");
            Assert.That(GameObject.Find("Confirm Battle Card Upgrade"), Is.Null);
            foreach (NavalBattleStage stage in stages)
            {
                Vector3[] corners = WorkshopWorldCorners(ActiveButton("Workshop Preview Stage " + stage).GetComponent<RectTransform>());
                for (int corner = 0; corner < 4; corner++)
                    Assert.That(Vector3.Distance(corners[corner], shell["tab/" + stage][corner]), Is.LessThan(.01f));
            }
            HashSet<int> visibleSpecials = new HashSet<int>();
            int specialPages = Mathf.CeilToInt(acquired.Length / 6f);
            for (int page = 0; page < specialPages; page++)
            {
                Button[] specials = GameObject.Find("Workshop Preview Collection").GetComponentsInChildren<Button>()
                    .Where(button => button.name.StartsWith("Select Special Card ")).ToArray();
                Assert.That(specials.Length, Is.InRange(1, 6));
                RectTransform paper = GameObject.Find("Workshop Preview Parchment").GetComponent<RectTransform>();
                Rect collectionBounds = ForgeBoundsIn(paper, GameObject.Find("Workshop Preview Collection").GetComponent<RectTransform>());
                Assert.That(paper.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Workshop Collection Page Count"), Is.False);
                string[] pagerNames = { "Previous Upgrade Cards", "Next Upgrade Cards" };
                Rect[] pagers = new Rect[pagerNames.Length];
                for (int side = 0; side < pagerNames.Length; side++)
                {
                    Button arrow = ActiveButton(pagerNames[side]);
                    Rect bounds = pagers[side] = ForgeBoundsIn(paper, arrow.GetComponent<RectTransform>());
                    Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(paper.rect.xMin - .5f));
                    Assert.That(bounds.xMax, Is.LessThanOrEqualTo(paper.rect.xMax + .5f));
                    Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(paper.rect.yMin - .5f));
                    Assert.That(bounds.yMax, Is.LessThanOrEqualTo(paper.rect.yMax + .5f));
                    Assert.That(bounds.center.y, Is.EqualTo(collectionBounds.center.y).Within(.1f), "Paging arrows are vertically centered on the card display.");
                    WoodenPageArrowGraphic wood = arrow.GetComponent<WoodenPageArrowGraphic>();
                    Assert.That(wood, Is.Not.Null, "Paging controls are solid wooden arrow silhouettes.");
                    Assert.That(wood.Forward, Is.EqualTo(side == 1));
                    Assert.That(arrow.targetGraphic, Is.SameAs(wood));
                    Assert.That(wood.mainTexture, Is.SameAs(WoodButtonSkin.Face.texture));
                    Assert.That(arrow.GetComponentsInChildren<Image>(true), Is.Empty, "No rectangular backing remains behind the wooden arrow.");
                    Assert.That(arrow.GetComponentsInChildren<Text>(true), Is.Empty, "The arrow is a wood silhouette rather than a text glyph.");
                    Assert.That(wood.color.a, Is.EqualTo(1f));
                    Assert.That(arrow.colors.normalColor.a, Is.EqualTo(1f));
                    Assert.That(arrow.colors.highlightedColor.a, Is.EqualTo(1f));
                    Assert.That(arrow.colors.pressedColor.a, Is.EqualTo(1f));
                    Assert.That(arrow.colors.selectedColor.a, Is.EqualTo(1f));
                    Assert.That(arrow.colors.disabledColor.a, Is.EqualTo(1f), "Disabled arrows dim without becoming transparent.");
                    foreach (Button card in specials)
                    {
                        Assert.That(bounds.Overlaps(ForgeBoundsIn(paper, card.GetComponent<RectTransform>())), Is.False, arrow.name + " leaves card interaction clear.");
                        Assert.That(bounds.Overlaps(ForgeBoundsIn(paper, card.GetComponentInChildren<CardSchoolFrameGraphic>().rectTransform)), Is.False, arrow.name + " leaves the full card face clear.");
                    }
                    foreach (RectTransform status in paper.GetComponentsInChildren<RectTransform>().Where(item =>
                        item.name.StartsWith("Special Card Deck Status ") || item.name.StartsWith("Configure Special Card Source ")))
                        Assert.That(bounds.Overlaps(ForgeBoundsIn(paper, status)), Is.False,
                            "Paging arrows leave special card membership and source actions clear: " + arrow.name + " / " + status.name);
                }
                Rect[] specialCardBounds = specials.Select(card => ForgeBoundsIn(paper,
                    card.GetComponentInChildren<CardSchoolFrameGraphic>().rectTransform)).ToArray();
                Assert.That(pagers[0].xMax, Is.LessThan(specialCardBounds.Min(bounds => bounds.xMin)),
                    "Previous page stays outside the left card column.");
                Assert.That(pagers[1].xMin, Is.GreaterThan(specialCardBounds.Max(bounds => bounds.xMax)),
                    "Next page stays outside the visible card columns.");
                Assert.That(pagers[0].center.x, Is.LessThan(0f));
                Assert.That((pagers[0].center.x + pagers[1].center.x) * .5f, Is.EqualTo(collectionBounds.center.x).Within(.1f), "Paging arrows flank the card display symmetrically.");
                Assert.That(pagers[1].width, Is.EqualTo(pagers[0].width).Within(.1f));
                Assert.That(pagers[1].height, Is.EqualTo(pagers[0].height).Within(.1f));
                foreach (int index in specials.Select(card => int.Parse(card.name.Substring("Select Special Card ".Length))).ToArray())
                {
                    Assert.That(visibleSpecials.Add(index), Is.True);
                    Assert.That(acquired, Has.Member(controller.Session.GetBattleCard(index).Id));
                    ActiveButton("Select Special Card " + index).onClick.Invoke();
                    yield return null;
                    Assert.That(GameObject.Find("Card Workshop Preview Panel"), Is.Not.Null);
                    Assert.That(ActiveButton("Confirm Battle Card Upgrade"), Is.Null);
                }
                if (page + 1 < specialPages) { ActiveButton("Next Upgrade Cards").onClick.Invoke(); yield return null; }
            }
            CollectionAssert.AreEquivalent(acquired, visibleSpecials.Select(index => controller.Session.GetBattleCard(index).Id));
            ActiveButton("Previous Upgrade Cards").onClick.Invoke();
            yield return null;
            yield return new WaitForSecondsRealtime(.7f);
            NavalBattleVisualCapture.Capture(controller, "WorkshopPreview_04_Special.png",
                "已有卡牌页显示特殊牌与完整详情，不提供特殊牌金币强化动作", "Card Workshop Preview Panel");
            ActiveButton("Workshop Page DeckBuilder").onClick.Invoke();
            yield return null;
            ActiveButton("Workshop Preview Stage Naval").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Deck Builder Panel"), Is.Not.Null);
            ActiveButton("Back To Card Workshop Chests").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Card Workshop Preview Panel"), Is.Not.Null);
            Assert.That(GameObject.Find("Workshop Upgrade Disabled Mark"), Is.Null);
            Assert.That(ActiveButton("Workshop Page Upgrade").interactable, Is.True);
            ActiveButton("Open Special Card Collection").onClick.Invoke();
            yield return null;
            ActiveButton("Workshop Preview Stage Naval").onClick.Invoke();
            yield return null;
            Assert.That(ActiveButton("Workshop Page Upgrade").interactable, Is.True);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.BattleCardUpgradeKey, string.Empty), Is.EqualTo(upgrades));
            foreach (NavalBattleStage stage in stages)
            {
                CollectionAssert.AreEqual(decks[stage], controller.Session.GetBattleDeck(stage));
                Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.StageBattleDeckKey(stage), string.Empty), Is.EqualTo(saves[stage]));
            }

            Assert.That(controller.Session.TrySpendProfileGold(gold - 119m, "强化不足金币测试"), Is.True);
            ActiveButton("Workshop Preview Stage Naval").onClick.Invoke();
            yield return null;
            ActiveButton("Workshop Page Upgrade").onClick.Invoke();
            yield return null;
            while (ActiveButton("Select Upgrade Card 0") == null)
            {
                Assert.That(ActiveButton("Next Upgrade Cards").interactable, Is.True,
                    "Chain shot must remain reachable in numbered catalog order.");
                ActiveButton("Next Upgrade Cards").onClick.Invoke();
                yield return null;
            }
            ActiveButton("Select Upgrade Card 0").onClick.Invoke();
            yield return null;
            Button upgrade = ActiveButton("Confirm Battle Card Upgrade");
            Assert.That(upgrade.interactable, Is.False);
            upgrade.onClick.Invoke();
            yield return null;
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(119m));
            Assert.That(JsonUtility.ToJson(controller.Session.CaptureCardPackProgress()), Is.EqualTo(originalCardPacksJson),
                "Read-only navigation and the rejected upgrade preserve ownership and future pack draws.");
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.CardPackProgressKey), Is.EqualTo(hadCardPackSave));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.CardPackProgressKey, string.Empty), Is.EqualTo(cardPackSave));
            Assert.That(JsonUtility.ToJson(controller.Session.CaptureTreasureProgress()), Is.EqualTo(originalTreasureJson),
                "Read-only navigation and rejected upgrades cannot change XP or treasure progress.");
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey, string.Empty), Is.EqualTo(treasureSave));
            Assert.That(controller.Session.GetCardUpgradeLevel("card_chain_shot"), Is.Zero);
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.BattleCardUpgradeKey, string.Empty), Is.EqualTo(upgrades));
            LogAssert.NoUnexpectedReceived();
        }

        private static Dictionary<string, Vector3[]> CaptureWorkshopLeftLayout(bool editing)
        {
            Canvas.ForceUpdateCanvases();
            var layout = new Dictionary<string, Vector3[]>();
            string stagePrefix = editing ? "Battle Deck Stage Tab " : "Workshop Preview Stage ";
            foreach (NavalBattleStage stage in new[] { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding })
                layout.Add("tab/" + stage, WorkshopWorldCorners(ActiveButton(stagePrefix + stage).GetComponent<RectTransform>()));
            layout.Add("tab/fourth", WorkshopWorldCorners(ActiveButton("Open Special Card Collection")
                .GetComponent<RectTransform>()));
            foreach (string school in new[] { "neutral" }.Concat(CardSchoolArtCatalog.SchoolIds))
                layout.Add("school/" + school, WorkshopWorldCorners(ActiveButton("Workshop School Filter " + school).GetComponent<RectTransform>()));
            layout.Add("page/previous", WorkshopWorldCorners(ActiveButton(editing ? "Previous Battle Card Collection Page" : "Previous Upgrade Cards")
                .GetComponent<RectTransform>()));
            layout.Add("page/next", WorkshopWorldCorners(ActiveButton(editing ? "Next Battle Card Collection Page" : "Next Upgrade Cards")
                .GetComponent<RectTransform>()));

            Transform collection = GameObject.Find(editing ? "Deck Builder Collection" : "Workshop Preview Collection").transform;
            string cardPrefix = editing ? "Add Deck Card " : "Select Upgrade Card ";
            Button[] cards = collection.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith(cardPrefix)).ToArray();
            Assert.That(cards, Is.Not.Empty);
            foreach (Button card in cards)
            {
                string index = card.name.Substring(cardPrefix.Length);
                layout.Add("card/" + index, WorkshopWorldCorners(card.GetComponent<RectTransform>()));
                Transform face = card.transform.Find((editing ? "Workshop Card " : "Upgrade Collection ") + index + " Face");
                Assert.That(face, Is.Not.Null, "The actual complete card face must exist for " + index);
                Graphic[] graphics = face.GetComponentsInChildren<Graphic>(true);
                Assert.That(graphics.Any(graphic => graphic is CardSchoolFrameGraphic), Is.True);
                Assert.That(graphics.Any(graphic => graphic is CardOrnamentGraphic), Is.True, "Card cost icons must be included.");
                for (int graphic = 0; graphic < graphics.Length; graphic++)
                    layout.Add("card/" + index + "/" + graphic + "/" + graphics[graphic].GetType().Name,
                        WorkshopWorldCorners(graphics[graphic].rectTransform));
            }
            return layout;
        }

        private static Vector3[] WorkshopWorldCorners(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners;
        }

        private static void AssertWorkshopLeftLayoutUnchanged(Dictionary<string, Vector3[]> expected, bool editing, string transition)
        {
            Dictionary<string, Vector3[]> actual = CaptureWorkshopLeftLayout(editing);
            CollectionAssert.AreEquivalent(expected.Keys, actual.Keys, transition + " must preserve the visible cards and all their graphics.");
            foreach (KeyValuePair<string, Vector3[]> item in expected)
                for (int corner = 0; corner < item.Value.Length; corner++)
                    Assert.That(Vector3.Distance(item.Value[corner], actual[item.Key][corner]), Is.LessThanOrEqualTo(.01f),
                        transition + ": " + item.Key + " corner " + corner + " must not move or resize.");
        }
    }
}
