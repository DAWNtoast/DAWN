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
        public IEnumerator WorkshopPreview_DefaultEightCardsNavigationAndInsufficientGoldStayReadOnly()
        {
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

            ActiveButton("Port Hotspot CardWorkshop").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Card Workshop Preview Panel"), Is.Not.Null);
            Assert.That(GameObject.Find("Card Workshop Chest Stage"), Is.Null);
            Assert.That(GameObject.Find("CardWorkshop Background").GetComponent<RawImage>().texture, Is.Null);
            Assert.That(GameObject.Find("Battle Card Upgrade Panel"), Is.Null);
            Assert.That(GameObject.Find("Deck Builder Panel"), Is.Null);
            Assert.That(GameObject.Find("Workshop Preview Deck Name"), Is.Null);
            Assert.That(GameObject.Find("Workshop Preview Deck Count"), Is.Null);
            foreach (NavalBattleStage stage in stages)
                Assert.That(ActiveButton("Edit Stage Deck " + stage), Is.Not.Null);
            ActiveButton("Workshop Preview Stage Naval").onClick.Invoke();
            yield return null;
            Button[] firstPage = GameObject.Find("Workshop Preview Collection").GetComponentsInChildren<Button>()
                .Where(button => button.name.StartsWith("Select Upgrade Card ")).ToArray();
            Assert.That(firstPage, Has.Length.EqualTo(8));
            Assert.That(ActiveButton("Previous Upgrade Cards").interactable, Is.False);
            Assert.That(ActiveButton("Next Upgrade Cards").interactable, Is.True);
            yield return new WaitForSecondsRealtime(.7f);
            NavalBattleVisualCapture.Capture(controller, "WorkshopPreview_01_Overview.png",
                "默认进入真实卡牌两行四列预览，右侧强化入口与三个阶段编辑入口", "Card Workshop Preview Panel");

            ActiveButton("Open Battle Card Upgrades").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Workshop Actions"), Is.Null);
            Assert.That(GameObject.Find("Card Upgrade Heading").GetComponent<Text>().text, Is.EqualTo("卡牌强化"));
            Button[] numberedFirstPage = AssertForgePage(controller, 0);
            AssertForgeComparison(controller, ForgeCardIndex(numberedFirstPage[0]));
            yield return new WaitForSecondsRealtime(.7f);
            NavalBattleVisualCapture.Capture(controller, "WorkshopPreview_02_Upgrade.png",
                "右侧整块羊皮纸显示卡名、等比例强化前后卡面与底部强化按钮", "Card Workshop Preview Panel");
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
            ActiveButton("Back From Card Upgrades").onClick.Invoke();
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
                ActiveButton("Edit Stage Deck " + stage).onClick.Invoke();
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
            ActiveButton("Edit Stage Deck Naval").onClick.Invoke();
            yield return null;
            AssertWorkshopLeftLayoutUnchanged(navalSecondPageLayout, true, "second page entering editor");
            Assert.That(ActiveButton("Previous Battle Card Collection Page").interactable, Is.True);
            Assert.That(GameObject.Find("Battle Card Collection Page").GetComponent<Text>().text.Replace(" ", string.Empty),
                Does.Contain("第2/"));
            yield return new WaitForSecondsRealtime(.7f);
            NavalBattleVisualCapture.Capture(controller, "WorkshopPreview_06_SecondPageEditor.png",
                "编辑页保留总览第二页的相同卡牌和图标位置", "Deck Builder Panel");
            ActiveButton("Back To Card Workshop Chests").onClick.Invoke();
            yield return null;
            AssertWorkshopLeftLayoutUnchanged(navalSecondPageLayout, false, "second page returning from editor");
            Assert.That(GameObject.Find("Upgrade Collection Page").GetComponent<Text>().text.Replace(" ", string.Empty),
                Does.Contain("第2/"));
            ActiveButton("Edit Stage Deck Naval").onClick.Invoke();
            yield return null;
            ActiveButton("Previous Battle Card Collection Page").onClick.Invoke();
            yield return null;
            AssertWorkshopLeftLayoutUnchanged(navalFirstPageLayout, true, "editor changes back to first page");
            ActiveButton("Back To Card Workshop Chests").onClick.Invoke();
            yield return null;
            AssertWorkshopLeftLayoutUnchanged(navalFirstPageLayout, false, "return preserves editor's changed page");
            Assert.That(ActiveButton("Previous Upgrade Cards").interactable, Is.False);
            controller.Session.AcquireSpecialCards(CoreSpecialCardCatalog.All.Take(12).Select(item => item.Card.Id).ToArray());
            string[] acquired = controller.Session.AcquiredSpecialCardIds.ToArray();
            Dictionary<string, Vector3[]> shell = CaptureWorkshopLeftLayout(false)
                .Where(item => item.Key.StartsWith("tab/") || item.Key.StartsWith("page/")).ToDictionary(item => item.Key, item => item.Value);
            ActiveButton("Open Special Card Collection").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Special Card Collection Panel"), Is.Null);
            Assert.That(GameObject.Find("Card Workshop Preview Panel"), Is.Not.Null);
            Assert.That(GameObject.Find("Workshop Actions"), Is.Not.Null);
            Assert.That(GameObject.Find("Workshop Upgrade Disabled Mark").GetComponent<ProhibitionMarkGraphic>().raycastTarget, Is.False);
            Assert.That(ActiveButton("Open Battle Card Upgrades").interactable, Is.False);
            ActiveButton("Open Battle Card Upgrades").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Battle Card Upgrade Panel"), Is.Null, "Even a stale callback cannot enter paid upgrades for special cards.");
            Assert.That(GameObject.Find("Confirm Battle Card Upgrade"), Is.Null);
            foreach (NavalBattleStage stage in stages)
            {
                Vector3[] corners = WorkshopWorldCorners(ActiveButton("Workshop Preview Stage " + stage).GetComponent<RectTransform>());
                for (int corner = 0; corner < 4; corner++)
                    Assert.That(Vector3.Distance(corners[corner], shell["tab/" + stage][corner]), Is.LessThan(.01f));
            }
            HashSet<int> visibleSpecials = new HashSet<int>();
            int specialPages = Mathf.CeilToInt(acquired.Length / 8f);
            for (int page = 0; page < specialPages; page++)
            {
                Button[] specials = GameObject.Find("Workshop Preview Collection").GetComponentsInChildren<Button>()
                    .Where(button => button.name.StartsWith("Select Special Card ")).ToArray();
                Assert.That(specials.Length, Is.InRange(1, 8));
                RectTransform paper = GameObject.Find("Workshop Preview Parchment").GetComponent<RectTransform>();
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
                    Assert.That(bounds.center.y, Is.EqualTo(0f).Within(.1f), "Paging arrows stay vertically centered on the parchment.");
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
                            "Paging arrows leave special card membership and source actions clear.");
                }
                Assert.That(pagers[0].center.x, Is.LessThan(0f));
                Assert.That(pagers[1].center.x, Is.EqualTo(-pagers[0].center.x).Within(.1f), "Paging arrows flank the card collection symmetrically.");
                Assert.That(pagers[1].width, Is.EqualTo(pagers[0].width).Within(.1f));
                Assert.That(pagers[1].height, Is.EqualTo(pagers[0].height).Within(.1f));
                foreach (int index in specials.Select(card => int.Parse(card.name.Substring("Select Special Card ".Length))).ToArray())
                {
                    Assert.That(visibleSpecials.Add(index), Is.True);
                    Assert.That(acquired, Has.Member(controller.Session.GetBattleCard(index).Id));
                    ActiveButton("Select Special Card " + index).onClick.Invoke();
                    yield return null;
                    Assert.That(GameObject.Find("Card Workshop Preview Panel"), Is.Not.Null);
                    Assert.That(ActiveButton("Open Battle Card Upgrades").interactable, Is.False);
                }
                if (page + 1 < specialPages) { ActiveButton("Next Upgrade Cards").onClick.Invoke(); yield return null; }
            }
            CollectionAssert.AreEquivalent(acquired, visibleSpecials.Select(index => controller.Session.GetBattleCard(index).Id));
            ActiveButton("Previous Upgrade Cards").onClick.Invoke();
            yield return null;
            yield return new WaitForSecondsRealtime(.7f);
            NavalBattleVisualCapture.Capture(controller, "WorkshopPreview_04_Special.png",
                "特殊牌保留同一工坊两行四列布局，锤子覆盖红色禁用圈，强化按钮不可用", "Card Workshop Preview Panel");
            ActiveButton("Edit Stage Deck Naval").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Deck Builder Panel"), Is.Not.Null);
            ActiveButton("Back To Card Workshop Chests").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Card Workshop Preview Panel"), Is.Not.Null);
            Assert.That(GameObject.Find("Workshop Upgrade Disabled Mark"), Is.Null);
            Assert.That(ActiveButton("Open Battle Card Upgrades").interactable, Is.True);
            ActiveButton("Open Special Card Collection").onClick.Invoke();
            yield return null;
            ActiveButton("Workshop Preview Stage Naval").onClick.Invoke();
            yield return null;
            Assert.That(ActiveButton("Open Battle Card Upgrades").interactable, Is.True);
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
            ActiveButton("Open Battle Card Upgrades").onClick.Invoke();
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
