using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        private bool hadTutorialCompleted;
        private bool hadTutorialReward;
        private bool hadProfileGold;
        private bool hadCurrentPort;
        private int savedTutorialCompleted;
        private int savedTutorialReward;
        private int savedProfileGold;
        private string savedCurrentPort;
        private readonly bool[] hadComponentLoadout = new bool[ComponentArtCatalog.SlotCount];
        private readonly string[] savedComponentLoadout = new string[ComponentArtCatalog.SlotCount];
        private readonly HashSet<string> hadComponentShopPurchase = new HashSet<string>();
        private readonly Dictionary<string, int> savedComponentShopPurchase = new Dictionary<string, int>();
        private readonly HashSet<string> hadMetaIntPrefs = new HashSet<string>();
        private readonly Dictionary<string, int> savedMetaIntPrefs = new Dictionary<string, int>();
        private readonly HashSet<string> hadMetaStringPrefs = new HashSet<string>();
        private readonly Dictionary<string, string> savedMetaStringPrefs = new Dictionary<string, string>();

        [SetUp]
        public void PreserveAndResetSavedProgress()
        {
            hadTutorialCompleted = PlayerPrefs.HasKey(CoreGameplayDemoController.TutorialCompletedKey);
            hadTutorialReward = PlayerPrefs.HasKey(CoreGameplayDemoController.TutorialRewardGrantedKey);
            hadProfileGold = PlayerPrefs.HasKey(CoreGameplayDemoController.ProfileGoldKey);
            hadCurrentPort = PlayerPrefs.HasKey(CoreGameplayDemoController.CurrentPortKey);
            savedTutorialCompleted = PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey, 0);
            savedTutorialReward = PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialRewardGrantedKey, 0);
            savedProfileGold = PlayerPrefs.GetInt(CoreGameplayDemoController.ProfileGoldKey, 500);
            savedCurrentPort = PlayerPrefs.GetString(CoreGameplayDemoController.CurrentPortKey, "断锚港");
            PreserveMetaProgressionPrefs();
            foreach (ComponentSlot slot in System.Enum.GetValues(typeof(ComponentSlot)))
            {
                string key = CoreGameplayDemoController.ComponentLoadoutKey(slot);
                hadComponentLoadout[(int)slot] = PlayerPrefs.HasKey(key);
                savedComponentLoadout[(int)slot] = PlayerPrefs.GetString(key, string.Empty);
            }
            hadComponentShopPurchase.Clear();
            savedComponentShopPurchase.Clear();
            ComponentArtCatalog componentArt = new ComponentArtCatalog();
            string[] ports = { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" };
            foreach (string port in ports)
            {
                foreach (PortComponentShopStockItem offer in PortComponentShopCatalog.BuildStock(componentArt, port, CoreVoyageSession.GameClockOrigin)
                    .Concat(PortComponentShopCatalog.BuildExpandedStock(componentArt, port, CoreVoyageSession.GameClockOrigin)))
                {
                    string key = CoreGameplayDemoController.ComponentShopPurchaseKey(offer.Id);
                    if (PlayerPrefs.HasKey(key))
                    {
                        hadComponentShopPurchase.Add(key);
                        savedComponentShopPurchase[key] = PlayerPrefs.GetInt(key, 0);
                    }
                }
            }
            ResetSavedProgress();
            PlayerPrefs.SetString(CoreGameplayDemoController.WorldEconomyScopeKey, System.Guid.NewGuid().ToString("N"));
            DeleteCurrentComponentShopPurchases();
        }

        [TearDown]
        public void RestoreSavedProgress()
        {
            DeleteCurrentComponentShopPurchases();
            ResetSavedProgress();
            if (hadTutorialCompleted) PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, savedTutorialCompleted);
            if (hadTutorialReward) PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialRewardGrantedKey, savedTutorialReward);
            if (hadProfileGold) PlayerPrefs.SetInt(CoreGameplayDemoController.ProfileGoldKey, savedProfileGold);
            if (hadCurrentPort) PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, savedCurrentPort);
            foreach (ComponentSlot slot in System.Enum.GetValues(typeof(ComponentSlot)))
            {
                if (hadComponentLoadout[(int)slot])
                {
                    PlayerPrefs.SetString(CoreGameplayDemoController.ComponentLoadoutKey(slot), savedComponentLoadout[(int)slot]);
                }
            }
            foreach (string key in hadComponentShopPurchase)
            {
                PlayerPrefs.SetInt(key, savedComponentShopPurchase[key]);
            }
            foreach (string key in hadMetaIntPrefs) PlayerPrefs.SetInt(key, savedMetaIntPrefs[key]);
            foreach (string key in hadMetaStringPrefs) PlayerPrefs.SetString(key, savedMetaStringPrefs[key]);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator CardForge_UpgradesBaseCardsThreeTimesAndExcludesSpecialSources()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            controller.Session.GrantProfileGold(10000, "强化界面隔离测试");
            controller.Session.ConfigureCrewCapabilities(true, true, false, true, false, false);
            ActiveButton("Port Hotspot CardWorkshop").onClick.Invoke();
            yield return null;
            ActiveButton("Workshop Preview Stage Naval").onClick.Invoke();
            yield return null;
            ActiveButton("Open Battle Card Upgrades").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Battle Card Upgrade Panel"), Is.Not.Null);
            Assert.That(GameObject.Find("Battle Card Upgrade Panel Texture").GetComponent<RawImage>().texture, Is.Not.Null);
            ActiveButton("Select Upgrade Card 0").onClick.Invoke();
            yield return null;
            AssertForgePage(controller, 0);
            AssertForgeComparison(controller, 0);
            yield return new WaitForSecondsRealtime(.7f);
            NavalBattleVisualCapture.Capture(controller, "18_Card_Forge_Rank0_To1.png",
                "完整卡面对比零阶到一阶：相同卡图、名称与费用，伤害升级", "Battle Card Upgrade Panel");
            decimal initialGold = controller.Session.ProfileGold;
            int paid = 0;
            for (int level = 1; level <= 3; level++)
            {
                paid += controller.Session.GetCardUpgradeCost(0);
                Button upgrade = ActiveButton("Confirm Battle Card Upgrade");
                Assert.That(upgrade.interactable, Is.True);
                upgrade.onClick.Invoke();
                yield return null;
                Assert.That(controller.Session.GetCardUpgradeLevel("card_chain_shot"), Is.EqualTo(level));
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(initialGold - paid));
                Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.BattleCardUpgradeKey), Does.Contain("card_chain_shot=" + level));
                Assert.That(GameObject.Find("Upgrade Selected Name").GetComponent<Text>().text, Does.Contain("+" + level));
                AssertForgeComparison(controller, 0);
                if (level == 2)
                {
                    yield return new WaitForSecondsRealtime(.7f);
                    NavalBattleVisualCapture.Capture(controller, "19_Card_Forge_Rank2_To3.png",
                        "完整卡面对比二阶到三阶：原始费用不变，伤害由1.5倍提升至2倍", "Battle Card Upgrade Panel");
                }
            }
            Assert.That(ActiveButton("Confirm Battle Card Upgrade").interactable, Is.False);
            ActiveButton("Confirm Battle Card Upgrade").onClick.Invoke();
            yield return null;
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(initialGold - paid));
            CoreBattleCard card = controller.Session.GetBattleCard(0);
            Assert.That(card.Energy, Is.EqualTo(CoreVoyageSession.CardLibrary[0].Energy));
            Assert.That(card.Draw, Is.Zero, "Chain shot never gains an unauthored draw effect.");
            Assert.That(GameObject.Find("Upgrade Rules"), Is.Null,
                "The forge compares the actual card faces without the removed explanatory paragraph.");
            RectTransform bounds = GameObject.Find("Battle Card Upgrade Panel").GetComponent<RectTransform>();
            foreach (Button button in GameObject.Find("Battle Card Upgrade Panel").GetComponentsInChildren<Button>())
            {
                Vector3[] corners = new Vector3[4];
                button.GetComponent<RectTransform>().GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                    Assert.That(bounds.rect.Contains(bounds.InverseTransformPoint(corner)), Is.True, button.name + " must remain on parchment");
            }
            yield return new WaitForSecondsRealtime(.7f);
            NavalBattleVisualCapture.Capture(controller, "20_Card_Forge_Rank3.png", "完整三阶卡面与满阶提示，重复点击不再扣费", "Battle Card Upgrade Panel");
            HashSet<int> seen = new HashSet<int>();
            int pageCount = Mathf.Max(1, (controller.Session.GetAvailableBattleCards(NavalBattleStage.Naval).Count + 7) / 8);
            for (int page = 0; page < pageCount; page++)
            {
                Button[] choices = AssertForgePage(controller, page);
                foreach (Button choice in choices) seen.Add(ForgeCardIndex(choice));
                Button selected = choices.FirstOrDefault(item => controller.Session.GetBattleCard(ForgeCardIndex(item)).PowderCost > 0) ?? choices.Last();
                int selectedIndex = ForgeCardIndex(selected);
                selected.onClick.Invoke();
                yield return null;
                AssertForgeComparison(controller, selectedIndex);
                Button next = ActiveButton("Next Upgrade Cards");
                Assert.That(next.interactable, Is.EqualTo(page + 1 < pageCount));
                if (page + 1 == pageCount) break;
                next.onClick.Invoke();
                yield return null;
            }
            CollectionAssert.AreEquivalent(controller.Session.GetAvailableBattleCards(NavalBattleStage.Naval), seen,
                "Eight-card paging must preserve every available base card.");
            if (pageCount > 1)
            {
                Assert.That(ActiveButton("Previous Upgrade Cards").interactable, Is.True);
                ActiveButton("Previous Upgrade Cards").onClick.Invoke();
                yield return null;
                AssertForgePage(controller, pageCount - 2);
            }
            foreach (int index in seen)
            {
                Assert.That(CoreVoyageSession.CardLibrary[index].RequiredCrew, Is.Empty);
                Assert.That(CoreVoyageSession.CardLibrary[index].RequiredComponent, Is.Empty);
            }
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreVoyageSession restored = Object.FindFirstObjectByType<CoreGameplayDemoController>().Session;
            Assert.That(restored.GetCardUpgradeLevel("card_chain_shot"), Is.EqualTo(3));
            Assert.That(restored.ProfileGold, Is.EqualTo(initialGold - paid));
            Assert.That(restored.GetBattleCard(0).Energy, Is.EqualTo(CoreVoyageSession.CardLibrary[0].Energy), "Restoring rank 3 preserves the authored action cost.");
        }

        private static int ForgeCardIndex(Button button) => int.Parse(button.name.Substring("Select Upgrade Card ".Length));

        private static Button[] AssertForgePage(CoreGameplayDemoController controller, int page)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform panel = GameObject.Find("Workshop Preview Collection").GetComponent<RectTransform>();
            Button[] choices = panel.GetComponentsInChildren<Button>()
                .Where(button => button.name.StartsWith("Select Upgrade Card ")).OrderBy(ForgeCardIndex).ToArray();
            int[] expected = controller.Session.GetAvailableBattleCards(NavalBattleStage.Naval).Skip(page * 8).Take(8).ToArray();
            CollectionAssert.AreEqual(expected, choices.Select(ForgeCardIndex).ToArray());
            Assert.That(choices.Length, Is.InRange(1, 8));
            int pages = Mathf.Max(1, (controller.Session.GetAvailableBattleCards(NavalBattleStage.Naval).Count + 7) / 8);
            Assert.That(GameObject.Find("Upgrade Collection Page").GetComponent<Text>().text.Replace(" ", string.Empty), Does.Contain((page + 1) + "/" + pages));
            Assert.That(ActiveButton("Previous Upgrade Cards").interactable, Is.EqualTo(page > 0));
            Assert.That(GameObject.Find("Upgrade Comparison Name Current").GetComponent<Text>().text,
                Is.EqualTo(CardFaceLayout.DisplayName(controller.Session.GetBattleCard(expected[0]))),
                "Changing catalog page also selects its first visible card.");
            List<Rect> placed = new List<Rect>();
            foreach (Button choice in choices)
            {
                int index = ForgeCardIndex(choice);
                RectTransform face = GameObject.Find("Upgrade Collection " + index + " Face").GetComponent<RectTransform>();
                Assert.That(face.sizeDelta, Is.EqualTo(CardFaceLayout.CardSize));
                Assert.That(face.localScale.x, Is.GreaterThan(0f));
                Assert.That(face.localScale.y, Is.EqualTo(face.localScale.x));
                CardSchoolFrameGraphic frame = face.GetComponentInChildren<CardSchoolFrameGraphic>();
                Rect authored = CardSchoolArtCatalog.FrameBounds(controller.Session.GetBattleCard(index));
                Assert.That(frame.rectTransform.sizeDelta, Is.EqualTo(authored.size));
                Rect bounds = ForgeBoundsIn(panel, frame.rectTransform);
                Assert.That(bounds.width / bounds.height, Is.EqualTo(authored.width / authored.height).Within(.001f));
                Assert.That(placed.All(other => !other.Overlaps(bounds)), Is.True, "Full authored frames cannot overlap.");
                placed.Add(bounds);
            }
            if (choices.Length == 8)
            {
                Assert.That(placed.Select(bounds => Mathf.Round(bounds.center.x)).Distinct().Count(), Is.EqualTo(4));
                Assert.That(placed.Select(bounds => Mathf.Round(bounds.center.y)).Distinct().Count(), Is.EqualTo(2));
            }
            return choices;
        }

        private static void AssertForgeComparison(CoreGameplayDemoController controller, int index)
        {
            Canvas.ForceUpdateCanvases();
            CoreBattleCard current = controller.Session.GetBattleCard(index);
            foreach (string side in new[] { "Current", "Next" })
            {
                CoreBattleCard expected = side == "Current" ? current : controller.Session.GetBattleCardAtLevel(index, Mathf.Min(3, current.UpgradeLevel + 1));
                RectTransform face = GameObject.Find("Upgrade Comparison " + side + " Face").GetComponent<RectTransform>();
                Assert.That(face.sizeDelta, Is.EqualTo(CardFaceLayout.CardSize), side);
                Assert.That(face.localScale, Is.EqualTo(Vector3.one), side);
                RawImage picture = face.GetComponentInChildren<RawImage>();
                Assert.That(picture, Is.Not.Null, side + " is a complete card face with its illustration");
                Assert.That(picture.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(CardFaceLayout.IllustrationLayout(picture.GetComponent<RawImage>()).size), side);
                Text name = GameObject.Find("Upgrade Comparison Name " + side).GetComponent<Text>();
                Text body = GameObject.Find("Upgrade Comparison Body " + side).GetComponent<Text>();
                Assert.That(name.transform.IsChildOf(face), Is.True, side);
                Assert.That(body.transform.IsChildOf(face), Is.True, side);
                Assert.That(name.text, Is.EqualTo(CardFaceLayout.DisplayName(expected)), side);
                Assert.That(ForgeVisibleText(body.text),
                    Is.EqualTo(ForgeVisibleText(CardDescriptionText.Compact(
                        controller.Session.BattleCardFaceDescription(expected, NavalBattleStage.Naval)))), side);
                Assert.That(body.rectTransform.sizeDelta, Is.EqualTo(CardFaceLayout.EffectSize), side);
                Assert.That(body.preferredHeight, Is.LessThanOrEqualTo(body.rectTransform.rect.height + .5f), side);
                CardOrnamentGraphic[] ornaments = face.GetComponentsInChildren<CardOrnamentGraphic>();
                CardOrnamentGraphic energy = ornaments.Single(item => item.Kind == CardOrnamentKind.Energy);
                Assert.That(energy.GetComponentInChildren<Text>().text, Is.EqualTo(expected.Energy.ToString()), side);
                CardOrnamentGraphic[] powder = ornaments.Where(item => item.Kind == CardOrnamentKind.Gunpowder).ToArray();
                Assert.That(powder.Length, Is.EqualTo(expected.PowderCost > 0 ? 1 : 0), side);
                if (powder.Length > 0) Assert.That(powder[0].GetComponentInChildren<Text>().text, Is.EqualTo(expected.PowderCost.ToString()), side);
            }
            Assert.That(ActiveButton("Confirm Battle Card Upgrade").interactable,
                Is.EqualTo(current.UpgradeLevel < 3 && controller.Session.ProfileGold >= controller.Session.GetCardUpgradeCost(index)));
            Assert.That(GameObject.Find("Upgrade Comparison Heading Current").GetComponent<Text>().text, Does.Contain("强化前"));
            Assert.That(GameObject.Find("Upgrade Comparison Heading Next").GetComponent<Text>().text,
                Does.Contain(current.UpgradeLevel == 3 ? "已达三阶" : "强化后"));
        }

        private static string ForgeVisibleText(string text) => System.Text.RegularExpressions.Regex.Replace(text, @"</?(?:b|i|size|color)(?:=[^>]*)?>", string.Empty)
            .Replace('\u00a0', ' ').Replace("\n", string.Empty).Replace("\r", string.Empty);

        private static string DossierTextWithoutWhitespace(string text) =>
            new string(text.Where(character => !char.IsWhiteSpace(character)).ToArray());

        private static Rect ForgeBoundsIn(Transform parent, RectTransform child)
        {
            Vector3[] corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector3[] local = corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(local.Min(point => point.x), local.Min(point => point.y),
                local.Max(point => point.x), local.Max(point => point.y));
        }

        private static void AssertDossierCardFace(CoreGameplayDemoController controller, string prefix, CoreSpecialCardDefinition definition)
        {
            Assert.That(definition, Is.Not.Null);
            int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Single(candidate => CoreVoyageSession.CardLibrary[candidate].Id == definition.Card.Id);
            CoreBattleCard card = controller.Session.GetBattleCard(index);
            RectTransform face = GameObject.Find(prefix + " Special Card Face").GetComponent<RectTransform>();
            RawImage picture = GameObject.Find(prefix + " Special Card Art").GetComponent<RawImage>();
            Text title = GameObject.Find(prefix + " Special Card Name").GetComponent<Text>();
            Text effect = GameObject.Find(prefix + " Special Card Effect").GetComponent<Text>();
            Assert.That(face.sizeDelta, Is.EqualTo(CardFaceLayout.CardSize));
            RectTransform page = face.parent.GetComponent<RectTransform>();
            bool reviewedSheet = prefix == "Tavern" || prefix == "Component";
            float frameVerticalReserve = reviewedSheet ? 36f : 12f;
            Assert.That(face.localScale.x, Is.EqualTo(Mathf.Min(.86f, (page.rect.height - frameVerticalReserve) / CardFaceLayout.FrameSize.y)).Within(.001f));
            CardSchoolFrameGraphic fullFrame = face.GetComponentInChildren<CardSchoolFrameGraphic>();
            Assert.That(fullFrame, Is.Not.Null);
            Assert.That(fullFrame.rectTransform.sizeDelta, Is.EqualTo(CardSchoolArtCatalog.FrameBounds(card).size));
            Assert.That(fullFrame.rectTransform.anchoredPosition, Is.EqualTo(CardSchoolArtCatalog.FrameBounds(card).center));
            Rect frameBounds = ForgeBoundsIn(page, fullFrame.rectTransform);
            Assert.That(frameBounds.xMin, Is.GreaterThanOrEqualTo(page.rect.xMin - .1f));
            Assert.That(frameBounds.xMax, Is.LessThanOrEqualTo(page.rect.xMax + .1f));
            Assert.That(frameBounds.yMin, Is.GreaterThanOrEqualTo(page.rect.yMin + 5.9f));
            Assert.That(frameBounds.yMax, Is.LessThanOrEqualTo(page.rect.yMax - 5.9f));
            Assert.That(frameBounds.center.y, Is.EqualTo(page.rect.center.y + (reviewedSheet ? 12f : 0f)).Within(.1f));
            Assert.That(picture.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(CardFaceLayout.IllustrationLayout(picture.GetComponent<RawImage>()).size));
            Assert.That(title.text, Is.EqualTo(CardFaceLayout.DisplayName(card)));
            NavalBattleStage previewStage = card.IsAllowedInStage(NavalBattleStage.Boarding) ? NavalBattleStage.Boarding :
                card.IsAllowedInStage(NavalBattleStage.Naval) ? NavalBattleStage.Naval : NavalBattleStage.Preparation;
            Assert.That(ForgeVisibleText(effect.text), Is.EqualTo(ForgeVisibleText(CardDescriptionText.Compact(
                controller.Session.BattleCardFaceDescription(card, previewStage)))));
            Assert.That(effect.rectTransform.sizeDelta, Is.EqualTo(CardFaceLayout.EffectSize));
            Assert.That(effect.preferredHeight, Is.LessThanOrEqualTo(effect.rectTransform.rect.height + .5f));
            Assert.That(face.GetComponentsInChildren<Button>(), Is.Empty, "Reading the card cannot buy, equip or recruit its source.");
            Assert.That(face.GetComponentsInChildren<ScrollRect>(), Is.Empty, "The complete card face uses its normal fixed description window.");
            CardOrnamentGraphic energy = face.GetComponentsInChildren<CardOrnamentGraphic>()
                .Single(item => item.Kind == CardOrnamentKind.Energy);
            Assert.That(energy.GetComponentInChildren<Text>().text, Is.EqualTo(card.Energy.ToString()));
            RectTransform details = GameObject.Find(prefix + " Special Card Details Viewport").GetComponent<RectTransform>();
            Assert.That(details.IsChildOf(face), Is.False, "Lore and crew calculations remain in their separate details column.");
            Assert.That(ForgeBoundsIn(face.parent, details).xMin, Is.GreaterThan(ForgeBoundsIn(face.parent, face).xMax));
        }

        [UnityTest]
        public IEnumerator TreasureBoardClicksInspectFragmentsWithoutChangingOrdinaryRoutes()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo");
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Button skip = ActiveButton("Skip Tutorial");
            if (skip != null) { skip.onClick.Invoke(); yield return null; }
            ActiveButton("Port Hotspot Treasure").onClick.Invoke();
            yield return null;
            string seed = controller.Session.SelectedSeed;
            int ordinaryRoute = controller.Session.SelectedRouteIndex;
            for (int index = 0; index < 3; index++)
            {
                Button route = ActiveButton("Treasure Route " + index);
                Assert.That(route, Is.Not.Null);
                RawImage chart = route.GetComponentsInChildren<RawImage>().Single(item => item.name == "Treasure Route Art " + index);
                Vector3 position = chart.transform.position;
                Vector3 scale = chart.transform.localScale;
                EventTrigger trigger = route.GetComponent<EventTrigger>();
                trigger.triggers.Single(item => item.eventID == EventTriggerType.PointerEnter).callback.Invoke(new BaseEventData(null));
                Assert.That(chart.transform.position, Is.EqualTo(position));
                Assert.That(chart.transform.localScale, Is.EqualTo(scale));
                trigger.triggers.Single(item => item.eventID == EventTriggerType.PointerExit).callback.Invoke(new BaseEventData(null));
                route.onClick.Invoke();
                yield return null;
                Assert.That(controller.Session.SelectedRouteIndex, Is.EqualTo(ordinaryRoute));
                Assert.That(controller.Session.SelectedSeed, Is.EqualTo(seed));
                Text[] labels = Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Where(item => item.name.StartsWith("Treasure Route Name ")).ToArray();
                Assert.That(labels.Length, Is.EqualTo(3));
                Assert.That(labels.All(item => item.text.Contains("藏宝图")), Is.True);
                Assert.That(labels.Single(item => item.name == "Treasure Route Name " + index).text,
                    Is.EqualTo(CoreTreasureHuntCatalog.Ranks[index].Name + "藏宝图"));
                Assert.That(GameObject.Find("Treasure Route Progress " + index).GetComponent<Text>().text, Does.Contain("/4"));
                Assert.That(GameObject.Find("Treasure Route Reading Desk"), Is.Null);
                Assert.That(GameObject.Find("Treasure Selected Route"), Is.Null);
                Assert.That(GameObject.Find("Treasure Route Detail"), Is.Null);
            }
        }

        [UnityTest]
        public IEnumerator ShipyardBrowserWrapsAndUnaffordableDetailsNeverSpendGold()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo");
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Button skip = ActiveButton("Skip Tutorial");
            if (skip != null) { skip.onClick.Invoke(); yield return null; }
            controller.Session.RestoreProfile(10, "灰鸥港");
            ActiveButton("Port Hotspot Shipyard").onClick.Invoke();
            yield return null;
            ActiveButton("Shipyard Previous Vessel").onClick.Invoke();
            yield return null;
            const int grayGullLastShip = 11;
            Assert.That(ActiveButton("Shipyard Ship " + grayGullLastShip), Is.Not.Null, "Previous wraps to the last vessel sold at this port.");
            Canvas.ForceUpdateCanvases();
            Button inspected = ActiveButton("Shipyard Ship " + grayGullLastShip);
            Canvas shipCanvas = inspected.GetComponentInParent<Canvas>();
            Camera uiCamera = shipCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : shipCanvas.worldCamera;
            PointerEventData shipPointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(uiCamera, inspected.targetGraphic.transform.position),
            };
            List<RaycastResult> shipHits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(shipPointer, shipHits);
            Assert.That(shipHits.Any(hit => ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject) == inspected.gameObject), Is.True,
                "The ship's visible image must route real pointer hits to the inspection button through the waterline mask.");
            ActiveButton("Shipyard Ship " + grayGullLastShip).onClick.Invoke();
            yield return null;
            Assert.That(ActiveButton("Confirm Shipyard Vessel").interactable, Is.False);
            ActiveButton("Confirm Shipyard Vessel").onClick.Invoke();
            yield return null;
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(10));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ShipOwnedKey(grayGullLastShip), 0), Is.Zero);
            Assert.That(controller.Session.SelectedShipIndex, Is.Zero);
            ActiveButton("Back To Shipyard Gallery").onClick.Invoke();
            yield return null;
            Assert.That(ActiveButton("Shipyard Ship " + grayGullLastShip), Is.Not.Null);
            ActiveButton("Shipyard Next Vessel").onClick.Invoke();
            yield return null;
            Assert.That(ActiveButton("Shipyard Ship 0"), Is.Not.Null, "Next wraps from the last to the first ship.");
            ActiveButton("Return To Port Hub").onClick.Invoke();
            yield return null;
            ActiveButton("Port Hotspot Shipyard").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Shipyard Detail"), Is.Null, "Re-entering opens the gallery, not a stale purchase sheet.");
        }

        [Test]
        public void RegionalComponentShopStock_IsDeterministicAndUsesOnlyConfiguredPortSlots()
        {
            ComponentArtCatalog art = new ComponentArtCatalog();
            Dictionary<string, ComponentSlot[]> expected = new Dictionary<string, ComponentSlot[]>
            {
                { "断锚港", new[] { ComponentSlot.Bow, ComponentSlot.Plating, ComponentSlot.Cannon } },
                { "白贝港", new[] { ComponentSlot.Sail, ComponentSlot.Instrument, ComponentSlot.Bow } },
                { "铜钟港", new[] { ComponentSlot.Plating, ComponentSlot.Cannon, ComponentSlot.Bow } },
                { "盐冠港", new[] { ComponentSlot.Cannon, ComponentSlot.Bow, ComponentSlot.Sail } },
                { "暮潮港", new[] { ComponentSlot.Instrument, ComponentSlot.Sail, ComponentSlot.Bow } },
                { "灰鸥港", new[] { ComponentSlot.Sail, ComponentSlot.Instrument, ComponentSlot.Bow } },
            };

            foreach (KeyValuePair<string, ComponentSlot[]> port in expected)
            {
                CollectionAssert.AreEqual(port.Value, PortComponentShopCatalog.PreferredSlots(port.Key));
                IReadOnlyList<PortComponentShopStockItem> first = PortComponentShopCatalog.BuildStock(art, port.Key, 2026, 9);
                IReadOnlyList<PortComponentShopStockItem> second = PortComponentShopCatalog.BuildStock(art, port.Key, 2026, 9);

                Assert.That(first.Count, Is.EqualTo(PortComponentShopCatalog.StockCount(0)));
                Assert.That(first.All(offer => port.Value.Contains(offer.Entry.Slot)), Is.True, port.Key + " exposed an unconfigured component slot.");
                Assert.That(first.All(offer => offer.Quality >= 0.9f && offer.Quality <= 1.1f), Is.True);
                Assert.That(first.All(offer => offer.Price >= 10 && offer.Price % 10 == 0), Is.True);
                CollectionAssert.AreEqual(
                    first.Select(offer => offer.Id + ":" + offer.Entry.Id + ":" + offer.Quality + ":" + offer.Price).ToArray(),
                    second.Select(offer => offer.Id + ":" + offer.Entry.Id + ":" + offer.Quality + ":" + offer.Price).ToArray());
            }

            ComponentArtEntry sail = art.GetById("sail_patched_lateen");
            Assert.That(PortComponentShopCatalog.CalculatePrice(sail, "白贝港", 1f), Is.EqualTo(200));

            HashSet<string> firstTemplateIds = new HashSet<string>(art.Entries
                .GroupBy(entry => new { entry.Slot, entry.Rarity })
                .Select(group => group.First().Id));
            HashSet<string> sampledIds = new HashSet<string>();
            for (int year = 2026; year <= 2030; year += 1)
            {
                for (int month = 1; month <= 12; month += 1)
                {
                    foreach (string portName in expected.Keys)
                    {
                        foreach (PortComponentShopStockItem offer in PortComponentShopCatalog.BuildStock(art, portName, year, month))
                        {
                            sampledIds.Add(offer.Entry.Id);
                        }
                    }
                }
            }
            Assert.That(sampledIds.Any(id => !firstTemplateIds.Contains(id)), Is.True,
                "Regional stock should sample among all five variants instead of always taking a rarity's first template.");
        }

        [Test]
        public void RegionalCrewNarrative_HasSixDistinctPortRostersAndConnectedStories()
        {
            Assert.That(CrewNarrativeCatalog.Ports.Count, Is.EqualTo(6));
            Assert.That(CrewNarrativeCatalog.ProfessionIds.Count, Is.EqualTo(6));
            Assert.That(CrewNarrativeCatalog.AllCrew.Count, Is.EqualTo(36));
            Assert.That(CrewNarrativeCatalog.Stories.Count, Is.EqualTo(15));
            Assert.That(CrewNarrativeCatalog.AllCrew.Select(crew => crew.Id).Distinct().Count(), Is.EqualTo(36));
            Assert.That(CrewNarrativeCatalog.AllCrew.Select(crew => crew.PortraitAssetName).Distinct().Count(), Is.EqualTo(36));
            Assert.That(CrewNarrativeCatalog.AllCrew.Select(crew => crew.StoryId).Distinct().Count(), Is.EqualTo(15),
                "Every chapter in the connected port anthology should be discoverable through at least one crew dossier.");

            foreach (string port in CrewNarrativeCatalog.Ports)
            {
                IReadOnlyList<CrewVariantDefinition> roster = CrewNarrativeCatalog.ForPort(port);
                Assert.That(roster.Count, Is.EqualTo(6), port);
                CollectionAssert.AreEqual(Enumerable.Range(0, 6), roster.Select(crew => crew.ProfessionIndex), port);
                Assert.That(roster.All(crew => crew.PortName == port), Is.True, port);
                Assert.That(roster.All(crew => !string.IsNullOrWhiteSpace(crew.AppearanceTags)), Is.True, port);
                Assert.That(roster.All(crew => !string.IsNullOrWhiteSpace(crew.Biography)), Is.True, port);
                Assert.That(roster.All(crew => CrewNarrativeCatalog.StoryFor(crew) != null), Is.True, port);
            }

            foreach (int profession in Enumerable.Range(0, 6))
            {
                CrewVariantDefinition[] variants = CrewNarrativeCatalog.Ports
                    .Select(port => CrewNarrativeCatalog.ForPortAndProfession(port, profession))
                    .ToArray();
                Assert.That(variants.Select(crew => crew.DisplayName).Distinct().Count(), Is.EqualTo(6));
                Assert.That(variants.Select(crew => crew.AppearanceTags).Distinct().Count(), Is.EqualTo(6));
            }

            for (int index = 0; index < CrewNarrativeCatalog.Stories.Count; index += 1)
            {
                CrewStoryDefinition story = CrewNarrativeCatalog.Stories[index];
                Assert.That(story.Chapter, Is.EqualTo(index + 1));
                Assert.That(story.Body.Length, Is.GreaterThan(55));
                Assert.That(index == 0 ? string.IsNullOrEmpty(story.PreviousId) : story.PreviousId == CrewNarrativeCatalog.Stories[index - 1].Id, Is.True);
                Assert.That(index == CrewNarrativeCatalog.Stories.Count - 1 ? string.IsNullOrEmpty(story.NextId) : story.NextId == CrewNarrativeCatalog.Stories[index + 1].Id, Is.True);
            }

            Assert.That(CrewNarrativeCatalog.FindById("crew_mad_navigator").Id, Is.EqualTo("crew_gray_gull_navigator_evelyn"));
            Assert.That(CrewNarrativeCatalog.FindById("crew_old_carpenter").Id, Is.EqualTo("crew_gray_gull_carpenter_barlow"));
            Assert.That(CrewNarrativeCatalog.FindById("crew_cold_doctor").Id, Is.EqualTo("crew_gray_gull_doctor_moer"));
            Assert.That(CrewNarrativeCatalog.FindById("crew_one_eye_gunner").Id, Is.EqualTo("crew_gray_gull_gunner_leisa"));
            Assert.That(CrewNarrativeCatalog.FindById("crew_salt_pot_cook").Id, Is.EqualTo("crew_gray_gull_cook_pepe"));
            Assert.That(CrewNarrativeCatalog.FindById("crew_treasure_diver").Id, Is.EqualTo("crew_gray_gull_diver_noya"));
        }

        [UnityTest]
        public IEnumerator Scene_CompletesPortVoyageReturnLoop()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;

            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller, Is.Not.Null);
            LegacyDemoArtCatalog art = new LegacyDemoArtCatalog();
            Assert.That(art.IsAvailable, Is.True, "The original browser demo art sheets should load from Resources.");
            PortArtCatalog portArt = new PortArtCatalog();
            Assert.That(portArt.IsAvailable, Is.True, "The generated port, ship, crew, upgrade, and battle art should load from Resources.");
            Assert.That(portArt.PortBackground.name, Is.EqualTo("port_hub_broken_anchor_v3"));
            Assert.That(portArt.PortBackgroundCount, Is.EqualTo(6));
            string[] themedPorts = { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" };
            Assert.That(themedPorts.Select(port => portArt.PortBackgroundFor(port).name).Distinct().Count(), Is.EqualTo(themedPorts.Length));
            Assert.That(themedPorts.All(port => portArt.PortBackgroundFor(port).name.EndsWith("_v3")), Is.True,
                "All ports should use the landmark-redesigned backgrounds rather than palette-only variants.");
            Assert.That(CoreGameplayDemoController.SoundEffectsEnabled, Is.True);
            Assert.That(GameObject.Find("Shattered Sail Audio"), Is.Not.Null);
            Assert.That(controller.ActiveMusicKey, Is.EqualTo(CoreGameplayDemoController.PortMusicKey(controller.Session.CurrentPort)));
            string[] battleSoundEffectKeys = {
                "battle_start", "battle_card_play", "battle_enemy_intent", "battle_maneuver",
                "battle_cannon_fire", "battle_impact", "battle_melee", "battle_melee_impact",
                "battle_ram_charge", "battle_ram_impact", "battle_guard", "battle_repair",
                "battle_victory", "battle_sunk" };
            Assert.That(battleSoundEffectKeys.All(key => Resources.Load<AudioClip>("Audio/BattleSfx/" + key) != null), Is.True,
                "Every battle animation cue should be packaged under the dedicated battle SFX folder.");
            Assert.That(CoreGameplayDemoController.WorldSoundEffectKeys.All(key => Resources.Load<AudioClip>("Audio/WorldSfx/" + key) != null), Is.True,
                "Every world transition cue should be packaged under the dedicated world SFX folder.");
            Assert.That(CoreGameplayDemoController.InterfaceSoundEffectKeys.All(key => Resources.Load<AudioClip>("Audio/InterfaceSfx/" + key) != null), Is.True,
                "Every interface action cue should be packaged under the dedicated interface SFX folder.");
            Assert.That(CoreGameplayDemoController.AmbienceSoundEffectKeys.All(key => Resources.Load<AudioClip>("Audio/Ambience/" + key) != null), Is.True,
                "Every non-battle ambience cue should be packaged under the dedicated ambience folder.");
            string[] musicKeys = {
                "port_broken_anchor", "port_white_shell", "port_bronze_bell", "port_salt_crown", "port_twilight_tide", "port_gray_gull",
                "route_shattered_sail_reef", "route_golden_tide_trade", "route_moon_tide_ruins",
                "treasure_weathered", "treasure_gilded", "treasure_moonlit",
                "boss_route_shattered_sail_reef", "boss_route_golden_tide_trade", "boss_route_moon_tide_ruins",
                "battle_nonboss", "battle_legendary_iron_oath", "battle_legendary_thunder_crown",
                "battle_legendary_red_widow", "battle_legendary_ember_ark", "battle_legendary_tide_requiem" };
            Assert.That(musicKeys.All(key => Resources.Load<AudioClip>("Audio/Music/" + key) != null), Is.True,
                "Every port, route, treasure, boss, and legendary battle track should be packaged.");
            Assert.That(portArt.PortBackgroundFor("未知港口"), Is.SameAs(portArt.PortBackground));
            Assert.That(portArt.CrewFullBodyCount, Is.EqualTo(6));
            Assert.That(Enumerable.Range(0, portArt.CrewFullBodyCount).All(index => portArt.CrewFullBody(index) != null), Is.True);
            ComponentArtCatalog componentArt = new ComponentArtCatalog();
            Assert.That(componentArt.Entries.Count, Is.EqualTo(ComponentArtCatalog.ExpectedEntryCount));
            Assert.That(componentArt.IsAvailable, Is.True, "All 125 component records, their mapped art, and the modular base ship should load from Resources.");
            Assert.That(componentArt.BaseShipTexture, Is.Not.Null);
            Assert.That(componentArt.Entries.Select(entry => entry.Id).Distinct().Count(), Is.EqualTo(ComponentArtCatalog.ExpectedEntryCount));
            Assert.That(componentArt.Entries.All(entry => entry.ShipLayer != null), Is.True);
            Assert.That(componentArt.Entries.GroupBy(entry => entry.Slot).All(group => group.Count() == ComponentArtCatalog.EntriesPerSlot), Is.True);
            Assert.That(componentArt.Entries.GroupBy(entry => entry.Rarity).All(group =>
                group.Count() == ComponentArtCatalog.SlotCount * ComponentArtCatalog.EntriesPerRarityPerSlot), Is.True);
            Assert.That(componentArt.Entries.GroupBy(entry => new { entry.Slot, entry.Rarity }).All(group =>
                group.Count() == ComponentArtCatalog.EntriesPerRarityPerSlot), Is.True);
            Assert.That(componentArt.Entries.Select(entry => entry.Effect).Distinct().Count(), Is.EqualTo(ComponentArtCatalog.ExpectedEntryCount));
            Assert.That(componentArt.Entries.Select(entry => entry.Specialization).Distinct().Count(), Is.EqualTo(ComponentArtCatalog.ExpectedEntryCount));
            Rect chainShotUv = art.CardUv("card_chain_shot");
            Rect cannonBarrageUv = art.CardUv("card_cannon_barrage");
            Assert.That(chainShotUv.xMin, Is.GreaterThan(0.25f), "Chain shot should use the second card-art column.");
            Assert.That(cannonBarrageUv.xMin, Is.LessThan(0.05f), "Cannon barrage should use the first card-art column.");
            Assert.That(chainShotUv.width, Is.LessThan(0.24f), "Card UVs should exclude horizontal atlas gutters.");
            Assert.That(chainShotUv.height, Is.LessThan(0.24f), "Card UVs should exclude vertical atlas gutters.");
            PortFacilityArtCatalog facilityArt = new PortFacilityArtCatalog();
            Assert.That(facilityArt.IsAvailable, Is.True, "Every dedicated port facility background should load from Resources.");
            Assert.That(GameObject.Find("Core Gameplay Canvas"), Is.Not.Null);
            Assert.That(controller.IsTutorialActive, Is.False);

            Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);
            Assert.That(GameObject.Find("Port Hub Background"), Is.Not.Null);
            Assert.That(GameObject.Find("Port Hub Background").GetComponent<RawImage>().texture,
                Is.SameAs(portArt.PortBackgroundFor(controller.Session.CurrentPort)));
            Button warehouseHotspot = ActiveButton("Port Hotspot Warehouse");
            Assert.That(warehouseHotspot, Is.Not.Null);
            Assert.That(GameObject.Find("Port Icon Warehouse"), Is.Null, "Port landmarks should be embedded in the background rather than separate icon objects.");
            Assert.That(warehouseHotspot.colors.normalColor.a, Is.Zero, "A port landmark must not have a visible detached button panel until hovered.");
            Assert.That(warehouseHotspot.colors.highlightedColor, Is.EqualTo(warehouseHotspot.colors.normalColor), "Port hotspots must keep pointer feedback transparent.");
            Image warehouseGlow = warehouseHotspot.targetGraphic as Image;
            Text warehouseLabel = GameObject.Find("Port Label Warehouse").GetComponent<Text>();
            Assert.That(warehouseGlow, Is.Not.Null);
            Assert.That(warehouseGlow.color.a, Is.Zero);
            int restingLabelSize = warehouseLabel.fontSize;
            Color restingLabelColor = warehouseLabel.color;
            ExecuteEvents.Execute(warehouseHotspot.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            yield return null;
            Assert.That(warehouseGlow.color.a, Is.Zero, "Hovering a port landmark must not show a warm glow.");
            Assert.That(warehouseLabel.color, Is.Not.EqualTo(restingLabelColor), "The brass nameplate emphasizes the facility name on hover.");
            Assert.That(warehouseLabel.fontSize, Is.EqualTo(restingLabelSize), "Hover feedback preserves the compact nameplate layout.");
            ExecuteEvents.Execute(warehouseHotspot.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
            yield return null;
            Assert.That(warehouseGlow.color.a, Is.Zero, "The hover glow should clear after the pointer leaves.");
            Assert.That(warehouseLabel.fontSize, Is.EqualTo(restingLabelSize));
            Assert.That(warehouseLabel.color, Is.EqualTo(restingLabelColor));
            warehouseHotspot.onClick.Invoke();
            yield return null;

            Assert.That(GameObject.Find("Port Facility Header"), Is.Not.Null);
            Assert.That(GameObject.Find("Warehouse Background"), Is.Not.Null);
            Assert.That(GameObject.Find("Warehouse Panel"), Is.Not.Null);
            ActiveButton("Next Port Facility").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Shipyard Panel"), Is.Not.Null);
            ActiveButton("Return To Port Hub").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);
            decimal regionalPreviewGold = controller.Session.ProfileGold;
            controller.Session.RestoreProfile(regionalPreviewGold, "白贝港");
            ActiveButton("Port Hotspot Warehouse").onClick.Invoke();
            yield return null;
            ActiveButton("Return To Port Hub").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Port Hub Background").GetComponent<RawImage>().texture.name,
                Is.EqualTo("port_hub_white_shell_v3"), "Returning to a different port should rebuild the hub with that port's own background.");

            Button tavernHotspot = ActiveButton("Port Hotspot Tavern");
            Assert.That(tavernHotspot, Is.Not.Null);
            tavernHotspot.onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Tavern Crew Stage"), Is.Not.Null);
            Assert.That(GameObject.Find("Tavern Background").GetComponent<RawImage>().texture,
                Is.SameAs(facilityArt.Background(PortFacilityArtCatalog.TavernFacilityIndex, controller.Session.CurrentPort)),
                "The tavern should use this port's dedicated composition, or the integrated seated-crew fallback while that art is absent.");
            Assert.That(GameObject.Find("Tavern Crew Portrait 0"), Is.Null, "The old bust-card layout should not be created.");
            Assert.That(GameObject.Find("Tavern Crew Full Body 0"), Is.Null, "Standing cutouts should not be layered over the furnished tavern scene.");
            CrewVariantDefinition regionalNavigator = CrewNarrativeCatalog.ForPortAndProfession(controller.Session.CurrentPort, 0);
            Assert.That(GameObject.Find("Tavern Crew Name 0").GetComponent<Text>().text, Is.EqualTo(regionalNavigator.DisplayName));
            Button firstCrew = ActiveButton("Tavern Crew 0");
            Assert.That(firstCrew.targetGraphic, Is.TypeOf<Image>());
            EventTrigger crewHover = firstCrew.GetComponent<EventTrigger>();
            Assert.That(crewHover, Is.Not.Null);
            Assert.That(crewHover.triggers.Any(entry => entry.eventID == EventTriggerType.PointerEnter), Is.True);
            Assert.That(crewHover.triggers.Any(entry => entry.eventID == EventTriggerType.PointerExit), Is.True);
            Button tavernReturn = ActiveButton("Return To Port Hub");
            Assert.That(tavernReturn.GetComponentInChildren<Text>().text, Is.EqualTo("返回港口"));
            firstCrew.onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Tavern Crew Detail"), Is.Not.Null);
            Assert.That(ActiveButton("Return To Port Hub"), Is.SameAs(tavernReturn), "Opening a dossier reuses the facility header return button.");
            Assert.That(tavernReturn.GetComponentInChildren<Text>().text, Is.EqualTo("返回酒馆"));
            Assert.That(GameObject.Find("Back To Tavern Crew Lineup"), Is.Null, "The dossier must not duplicate its header return button.");
            Assert.That(GameObject.Find("Tavern Dossier Parchment").GetComponent<RawImage>().texture.name,
                Is.EqualTo(regionalNavigator.Id + "_dossier_v1"));
            Assert.That(GameObject.Find("Tavern Crew Detail Full Body"), Is.Null,
                "Crew portraits are painted directly into their detail parchment.");
            Assert.That(GameObject.Find("Tavern Crew Detail Name").GetComponent<Text>().text, Is.EqualTo(regionalNavigator.DisplayName));
            Text crewBiography = GameObject.Find("Tavern Crew Biography").GetComponent<Text>();
            Assert.That(crewBiography.text.Replace("\r", string.Empty).Replace("\n", string.Empty),
                Is.EqualTo(regionalNavigator.Biography.Replace("\r", string.Empty).Replace("\n", string.Empty)));
            Assert.That(crewBiography.fontSize, Is.GreaterThanOrEqualTo(26));
            Assert.That(crewBiography.resizeTextForBestFit, Is.False);
            Transform[] dossierFields = GameObject.Find("Tavern Crew Detail").GetComponentsInChildren<Transform>(true);
            Assert.That(dossierFields.Any(item => item.name == "Tavern Crew Appearance"), Is.False);
            Assert.That(dossierFields.Any(item => item.name == "Tavern Crew Profile Heading"), Is.False);
            Assert.That(ActiveButton("DossierPageBackground").GetComponentInChildren<Text>().text, Is.EqualTo("人物背景"));
            Assert.That(GameObject.Find("Tavern Crew Story"), Is.Null, "The story must not compete with background text on the first page.");
            decimal goldBeforePaging = controller.Session.ProfileGold;
            ActiveButton("DossierPageStory").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Tavern Crew Story Heading").GetComponent<Text>().text, Does.Contain("航海异闻"));
            Assert.That(GameObject.Find("Tavern Crew Story").GetComponent<Text>().text.Replace("\r", string.Empty).Replace("\n", string.Empty),
                Is.EqualTo(CrewNarrativeCatalog.StoryFor(regionalNavigator).Body.Replace("\r", string.Empty).Replace("\n", string.Empty)));
            ActiveButton("DossierPageSkills").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Tavern Crew Skill").GetComponent<Text>().text.Replace("\r", string.Empty).Replace("\n", string.Empty),
                Is.EqualTo(regionalNavigator.SkillDescription.Replace("\r", string.Empty).Replace("\n", string.Empty)));
            ActiveButton("DossierPageSpecialCards").onClick.Invoke();
            yield return null;
            CoreSpecialCardDefinition personalTactic = CoreSpecialCardCatalog.ForCrew(regionalNavigator.Id);
            AssertDossierCardFace(controller, "Tavern", personalTactic);
            Assert.That(GameObject.Find("Tavern Special Card Lore").GetComponent<Text>().text.Replace("\r", string.Empty).Replace("\n", string.Empty),
                Is.EqualTo(personalTactic.Lore.Replace("\r", string.Empty).Replace("\n", string.Empty)));
            Assert.That(GameObject.Find("Tavern Special Card Growth").GetComponent<Text>().text, Does.Contain("尚未获得"));
            Assert.That(GameObject.Find("Tavern Dossier Page Counter").GetComponent<Text>().text, Is.EqualTo("第 4 / 4 页"));
            NavalBattleVisualCapture.Capture(controller, "03_Crew_Special_Card_Dossier.png",
                "船员第四页：个人来历衍生的特殊卡牌，阅读不招募也不扣费", "Tavern Crew Detail");
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(goldBeforePaging));
            Assert.That(controller.Session.HasNavigator, Is.False);
            decimal goldBeforeRecruitment = controller.Session.ProfileGold;
            ActiveButton("Recruit Tavern Crew 0").onClick.Invoke();
            yield return null;
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.CrewRecruitmentKey(0), 0), Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(regionalNavigator.Id), 0), Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.ActiveCrewVariantKey(0)), Is.EqualTo(regionalNavigator.Id));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(goldBeforeRecruitment - regionalNavigator.ContractCost));
            Assert.That(controller.Session.HasNavigator, Is.True);
            Assert.That(ActiveButton("Recruit Tavern Crew 0").interactable, Is.False);
            tavernReturn.onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Tavern Crew Stage"), Is.Not.Null);
            Assert.That(GameObject.Find("Tavern Crew Detail"), Is.Null);
            Assert.That(ActiveButton("Return To Port Hub"), Is.SameAs(tavernReturn));
            Assert.That(tavernReturn.GetComponentInChildren<Text>().text, Is.EqualTo("返回港口"));
            tavernReturn.onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);

            Button componentHotspot = ActiveButton("Port Hotspot ComponentShop");
            Assert.That(componentHotspot, Is.Not.Null);
            string[] marketPorts = { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港" };
            IReadOnlyList<PortComponentShopStockItem> regionalStock = null;
            PortComponentShopStockItem purchasedOffer = null;
            foreach (string marketPort in marketPorts)
            {
                // PlayMode uses the expanded market-v2 pool and purchase IDs;
                // BuildStock is the separate deterministic editor-preview batch.
                IReadOnlyList<PortComponentShopStockItem> candidateStock = PortComponentShopCatalog.BuildExpandedStock(componentArt, marketPort, controller.Session.GameDateTime);
                PortComponentShopStockItem candidate = candidateStock.FirstOrDefault(offer =>
                    offer.Entry.Rarity >= ComponentRarity.Rare && controller.EquippedComponent(offer.Entry.Slot)?.Id != offer.Entry.Id);
                if (candidate != null)
                {
                    regionalStock = candidateStock;
                    purchasedOffer = candidate;
                    controller.Session.RestoreProfile(10000, marketPort);
                    break;
                }
            }
            Assert.That(purchasedOffer, Is.Not.Null, "At least one regional market should contain a rare-or-better unequipped component with a special card this month.");
            componentHotspot.onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Component Shop Panel"), Is.Not.Null);
            Assert.That(GameObject.Find("Component Shop Panel").GetComponent<Image>().color.a, Is.LessThan(0.2f),
                "The component stock should sit in the illustrated shop rather than on an opaque blue overlay.");
            Assert.That(GameObject.Find("Component Shop Ship Preview"), Is.Null,
                "The retired lower-right ship preview must not return to the component showroom.");
            Assert.That(GameObject.Find("Component Shop Merchant"), Is.Null,
                "The component showroom must no longer create a merchant or workstation.");
            Assert.That(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(item => item.name.StartsWith("Component Shop Modular Ship")), Is.False,
                "No modular-ship preview layers should be created inside the component shop.");
            Assert.That(GameObject.Find("Component Shop Stock Header"), Is.Null,
                "The authored component shop preview removes the separate stock header.");
            Text regionalRule = GameObject.Find("Component Shop Regional Rule").GetComponent<Text>();
            Assert.That(regionalRule.text, Does.Contain(PortComponentShopCatalog.PreferredSlotLabel(controller.Session.CurrentPort)));
            Assert.That(Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Count(button => button.name.StartsWith("Component Shop Category ")), Is.EqualTo(5));
            foreach (ComponentSlot slot in System.Enum.GetValues(typeof(ComponentSlot)))
            {
                ActiveButton("Component Shop Category " + slot).onClick.Invoke();
                yield return null;
                var category = regionalStock.Where(item => item.Entry.Slot == slot).ToArray();
                for (int page = 0; page < ComponentShopShowroomLayout.PageCount(category.Length); page++)
                {
                    var visible = category.Skip(page * 5).Take(5).ToArray();
                    CollectionAssert.AreEquivalent(visible.Select(item => "Component Shop Offer " + item.Id),
                        Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                            .Where(item => item.name.StartsWith("Component Shop Offer ")).Select(item => item.name));
                    foreach (var offer in visible)
                    {
                        ActiveButton("Component Shop Offer " + offer.Id).onClick.Invoke();
                        yield return null;
                        RawImage model = GameObject.Find("Component Detail Model").GetComponent<RawImage>();
                        Assert.That(model.texture, Is.SameAs(offer.Entry.ShipLayer));
                        Assert.That(GameObject.Find("Component Detail Name").GetComponent<Text>().text, Is.EqualTo(offer.Entry.Name));
                        Assert.That(GameObject.Find("Component Dossier Overview Page"), Is.Not.Null);
                        ActiveButton("Return To Port Hub").onClick.Invoke();
                        yield return null;
                    }
                    if (page + 1 < ComponentShopShowroomLayout.PageCount(category.Length))
                    {
                        ActiveButton("Component Shop Next Display").onClick.Invoke();
                        yield return null;
                    }
                }
            }
            yield return FindComponentPurchaseOffer(purchasedOffer);
            decimal goldBeforePurchase = controller.Session.ProfileGold;
            string equippedBeforePurchase = controller.EquippedComponent(purchasedOffer.Entry.Slot)?.Id;
            int hullBeforeComponent = controller.Session.MaxHull;
            int armorBeforeComponent = controller.Session.Armor;
            int firepowerBeforeComponent = controller.Session.Firepower;
            float speedBeforeComponent = controller.Session.SpeedKnots;
            float visionBeforeComponent = controller.Session.VisionBonusNm;
            string purchaseKey = CoreGameplayDemoController.ComponentShopPurchaseKey(purchasedOffer.Id);
            Button purchasedSceneOffer = ActiveButton("Component Shop Offer " + purchasedOffer.Id);
            Button componentReturn = ActiveButton("Return To Port Hub");
            Assert.That(componentReturn.GetComponentInChildren<Text>().text, Is.EqualTo("返回港口"));
            purchasedSceneOffer.onClick.Invoke();
            yield return null;
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(goldBeforePurchase),
                "Opening component details must not spend currency.");
            Assert.That(controller.EquippedComponent(purchasedOffer.Entry.Slot)?.Id, Is.EqualTo(equippedBeforePurchase),
                "Opening component details must not equip the inspected item.");
            Assert.That(PlayerPrefs.GetInt(purchaseKey, 0), Is.Zero,
                "Opening component details must not mark the offer as purchased.");
            Assert.That(GameObject.Find("Component Shop Detail"), Is.Not.Null);
            ActiveButton("ComponentDossierPageOverview").onClick.Invoke();
            Assert.That(ActiveButton("Return To Port Hub"), Is.SameAs(componentReturn));
            Assert.That(componentReturn.GetComponentInChildren<Text>().text, Is.EqualTo("返回部件陈列"));
            Assert.That(GameObject.Find("Back To Component Shop"), Is.Null, "The component dossier must not duplicate its header return button.");
            RawImage detailParchment = GameObject.Find("Component Detail Parchment").GetComponent<RawImage>();
            Assert.That(detailParchment.texture, Is.Not.Null);
            Assert.That(detailParchment.texture.name.ToLowerInvariant(), Does.Contain("parchment"));
            Assert.That(GameObject.Find("Component Detail Name").GetComponent<Text>().text, Is.EqualTo(purchasedOffer.Entry.Name));
            Text detailMeta = GameObject.Find("Component Detail Meta").GetComponent<Text>();
            Assert.That(detailMeta.text, Does.Contain(ComponentArtCatalog.RarityName(purchasedOffer.Entry.Rarity)));
            Assert.That(detailMeta.text, Does.Contain(ComponentArtCatalog.SlotName(purchasedOffer.Entry.Slot)));
            Assert.That(GameObject.Find("Component Detail Quality").GetComponent<Text>().text,
                Does.Contain(Mathf.RoundToInt(purchasedOffer.Quality * 100f).ToString()));
            Assert.That(GameObject.Find("Component Detail Specialization").GetComponent<Text>().text,
                Does.Contain(purchasedOffer.Entry.Specialization));
            Assert.That(DossierTextWithoutWhitespace(GameObject.Find("Component Detail Description").GetComponent<Text>().text),
                Is.EqualTo(DossierTextWithoutWhitespace(purchasedOffer.Entry.Description)));
            ActiveButton("ComponentDossierPageEffects").onClick.Invoke();
            Assert.That(DossierTextWithoutWhitespace(GameObject.Find("Component Detail Effect").GetComponent<Text>().text),
                Does.Contain(DossierTextWithoutWhitespace(purchasedOffer.Entry.Effect)));
            ActiveButton("ComponentDossierPageStory").onClick.Invoke();
            Assert.That(GameObject.Find("Component Detail Story Chapter").GetComponent<Text>().text,
                Is.EqualTo(purchasedOffer.Entry.StoryTitle));
            Assert.That(DossierTextWithoutWhitespace(GameObject.Find("Component Detail Story Body").GetComponent<Text>().text),
                Is.EqualTo(DossierTextWithoutWhitespace(purchasedOffer.Entry.StoryBody)));
            NavalBattleVisualCapture.Capture(controller, "04_Component_Story_Tab.png",
                "部件档案独立故事标签，购买操作始终留在页底", "Component Shop Detail");
            ActiveButton("ComponentDossierPageSpecialCards").onClick.Invoke();
            yield return null;
            CoreSpecialCardDefinition partTactic = CoreSpecialCardCatalog.ForComponent(purchasedOffer.Entry.Id);
            if (purchasedOffer.Entry.Rarity >= ComponentRarity.Rare)
            {
                Assert.That(DossierTextWithoutWhitespace(GameObject.Find("Component Special Card Lore").GetComponent<Text>().text),
                    Is.EqualTo(DossierTextWithoutWhitespace(partTactic.Lore)));
                AssertDossierCardFace(controller, "Component", partTactic);
            }
            else
                Assert.That(GameObject.Find("Component Dossier SpecialCards Page")
                    .GetComponentsInChildren<Text>(true).Select(text => text.text),
                    Is.EqualTo(new[] { "没有专属卡牌" }));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(goldBeforePurchase));
            NavalBattleVisualCapture.Capture(controller, "05_Component_Special_Card_Tab.png",
                "部件第四页：蓝色及以上具有专属战术，白绿部件明确说明无卡", "Component Shop Detail");
            ActiveButton("ComponentDossierPageOverview").onClick.Invoke();
            Assert.That(DossierTextWithoutWhitespace(GameObject.Find("Component Detail Description").GetComponent<Text>().text),
                Is.EqualTo(DossierTextWithoutWhitespace(purchasedOffer.Entry.Description)));
            Assert.That(GameObject.Find("Component Detail Price"), Is.Null,
                "The authored component dossier preview removes the separate price text lane; purchase remains in the fixed button.");
            Assert.That(GameObject.Find("Component Shop Ship Preview"), Is.Null);

            Button purchase = ActiveButton("Purchase And Equip Component " + purchasedOffer.Id);
            Assert.That(purchase, Is.Not.Null);
            Assert.That(purchase.interactable, Is.True);
            purchase.onClick.Invoke();
            yield return null;
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(goldBeforePurchase - purchasedOffer.Price));
            Assert.That(controller.EquippedComponent(purchasedOffer.Entry.Slot).Id, Is.EqualTo(purchasedOffer.Entry.Id));
            Assert.That(PlayerPrefs.GetInt(purchaseKey, 0), Is.EqualTo(1));
            Assert.That(GameObject.Find("Component Shop Detail"), Is.Not.Null,
                "The purchased detail sheet should remain visible until the player returns.");
            Assert.That(ActiveButton("Purchase And Equip Component " + purchasedOffer.Id).interactable, Is.False);
            Assert.That(
                controller.Session.MaxHull != hullBeforeComponent ||
                controller.Session.Armor != armorBeforeComponent ||
                controller.Session.Firepower != firepowerBeforeComponent ||
                !Mathf.Approximately(controller.Session.SpeedKnots, speedBeforeComponent) ||
                !Mathf.Approximately(controller.Session.VisionBonusNm, visionBeforeComponent),
                Is.True,
                "Equipping a purchased component must change at least one real ship statistic.");

            componentReturn.onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Component Shop Panel"), Is.Not.Null);
            Assert.That(GameObject.Find("Component Shop Detail"), Is.Null);
            Assert.That(ActiveButton("Return To Port Hub"), Is.SameAs(componentReturn));
            Assert.That(componentReturn.GetComponentInChildren<Text>().text, Is.EqualTo("返回港口"));
            Assert.That(ActiveButton("Component Shop Offer " + purchasedOffer.Id).interactable, Is.True);
            Assert.That(ActiveButton("Purchase Component " + purchasedOffer.Id), Is.Null);
            var purchasedCategory = regionalStock.Where(offer => offer.Entry.Slot == purchasedOffer.Entry.Slot).ToList();
            int purchasedPageStart = purchasedCategory.FindIndex(offer => offer.Id == purchasedOffer.Id) /
                ComponentShopShowroomLayout.PageSize * ComponentShopShowroomLayout.PageSize;
            CollectionAssert.AreEquivalent(
                purchasedCategory.Skip(purchasedPageStart).Take(ComponentShopShowroomLayout.PageSize)
                    .Select(offer => "Component Shop Offer " + offer.Id).ToArray(),
                Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Where(button => button.name.StartsWith("Component Shop Offer "))
                    .Select(button => button.name)
                    .ToArray(),
                "Returning from details must preserve the chosen stock group, including its sold offer.");
            Assert.That(GameObject.Find("Component Shop Ship Preview"), Is.Null);
            componentReturn.onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);

            Button cardWorkshopHotspot = ActiveButton("Port Hotspot CardWorkshop");
            Assert.That(cardWorkshopHotspot, Is.Not.Null);
            int[] deckBeforeChestEntry = controller.Session.BattleDeck.ToArray();
            int[] preparationBeforeChestEntry = controller.Session.GetBattleDeck(NavalBattleStage.Preparation).ToArray();
            int[] boardingBeforeChestEntry = controller.Session.GetBattleDeck(NavalBattleStage.Boarding).ToArray();
            string savedDeckBeforeChestEntry = PlayerPrefs.GetString(CoreGameplayDemoController.BattleDeckKey, string.Empty);
            cardWorkshopHotspot.onClick.Invoke();
            yield return null;

            Assert.That(GameObject.Find("Card Workshop Preview Panel"), Is.Not.Null);
            Assert.That(GameObject.Find("Card Workshop Chest Stage"), Is.Null);
            Assert.That(GameObject.Find("Deck Builder Panel"), Is.Null, "Opening the preview does not edit a deck.");
            foreach (NavalBattleStage stage in new[] { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding })
                Assert.That(ActiveButton("Edit Stage Deck " + stage), Is.Not.Null);
            Assert.That(ActiveButton("Open Battle Card Upgrades"), Is.Not.Null);
            Assert.That(ActiveButton("Open Special Card Collection"), Is.Not.Null);
            CollectionAssert.AreEqual(deckBeforeChestEntry, controller.Session.BattleDeck.ToArray(),
                "Opening the workshop entrance must not change the configured deck.");
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.BattleDeckKey, string.Empty),
                Is.EqualTo(savedDeckBeforeChestEntry), "Showing the card preview must not write a new deck save.");

            ActiveButton("Workshop Preview Stage Preparation").onClick.Invoke();
            yield return null;
            ActiveButton("Open Battle Card Upgrades").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Card Workshop Chest Stage"), Is.Null);
            Assert.That(GameObject.Find("Battle Card Upgrade Panel"), Is.Not.Null);
            yield return ReturnFromWorkshopForgeToSelectedDeck();
            Assert.That(GameObject.Find("Deck Builder Panel"), Is.Not.Null);
            RectTransform parchment = GameObject.Find("Deck Builder Parchment")?.GetComponent<RectTransform>();
            RectTransform deckRail = GameObject.Find("Deck Builder Deck Rail")?.GetComponent<RectTransform>();
            RawImage collectionPaper = GameObject.Find("Deck Builder Parchment Texture")?.GetComponent<RawImage>();
            RawImage deckPaper = GameObject.Find("Deck Builder Deck Rail Texture")?.GetComponent<RawImage>();
            Assert.That(parchment, Is.Not.Null, "The available-card collection should sit on a parchment surface.");
            Assert.That(collectionPaper, Is.Not.Null);
            Assert.That(collectionPaper.texture, Is.Not.Null);
            Assert.That(parchment.anchoredPosition.x, Is.LessThan(0f));
            Assert.That(deckRail, Is.Not.Null, "The selected cards should occupy a full-height parchment rail.");
            Assert.That(deckPaper, Is.Not.Null);
            Assert.That(deckPaper.texture, Is.SameAs(collectionPaper.texture));
            Assert.That(deckRail.anchoredPosition.x, Is.GreaterThan(0f));
            Assert.That(parchment.sizeDelta.x, Is.GreaterThan(deckRail.sizeDelta.x));
            Assert.That(deckRail.sizeDelta.y, Is.EqualTo(parchment.sizeDelta.y));
            Assert.That(GameObject.Find("Deck Builder Pirate Frame"), Is.Null);
            Assert.That(ActiveButton("Back To Card Workshop Chests"), Is.Not.Null);

            int workshopIndex = controller.Session.GetAvailableBattleCards(NavalBattleStage.Preparation)[0];
            CoreBattleCard workshopCard = controller.Session.GetBattleCard(workshopIndex);
            RawImage workshopArt = GameObject.Find("Workshop Card Art " + workshopIndex).GetComponent<RawImage>();
            Assert.That(workshopArt, Is.Not.Null);
            Assert.That(CardSchoolArtCatalog.HasDedicatedArt(workshopCard), Is.True);
            Assert.That(workshopArt.texture, Is.SameAs(Resources.Load<Texture2D>(CardSchoolArtCatalog.DedicatedIllustrationPath(workshopCard))));
            Assert.That(workshopArt.uvRect, Is.EqualTo(CardSchoolArtCatalog.IllustrationUv(workshopCard, null)),
                "The collection resolves the complete individual card image with full UVs.");
            Assert.That(workshopArt.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(CardFaceLayout.IllustrationLayout(workshopArt.GetComponent<RawImage>()).size),
                "Collection and dossier cards share the battle card's fixed illustration window.");
            foreach (NavalBattleStage stage in System.Enum.GetValues(typeof(NavalBattleStage)))
            {
                ActiveButton("Battle Deck Stage Tab " + stage).onClick.Invoke();
                yield return null;
                HashSet<int> seenCards = new HashSet<int>();
                for (int page = 0; page < 5; page += 1)
                {
                    RawImage[] cardArt = Object.FindObjectsByType<RawImage>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                        .Where(image => image.name.StartsWith("Workshop Card Art ")).ToArray();
                    Assert.That(cardArt.Length, Is.InRange(1, 8), "Stage collections must paginate without crowding the eight-card display.");
                    foreach (RawImage item in cardArt)
                    {
                        int cardIndex = int.Parse(item.name.Substring("Workshop Card Art ".Length));
                        Assert.That(seenCards.Add(cardIndex), Is.True, "Collection pages must not repeat cards.");
                        Assert.That(CoreVoyageSession.CardLibrary[cardIndex].IsAllowedInStage(stage), Is.True);
                        Assert.That(CoreVoyageSession.CardLibrary[cardIndex].RequiredCrew, Is.Empty);
                    }
                    Button nextPage = ActiveButton("Next Battle Card Collection Page");
                    if (!nextPage.interactable) break;
                    nextPage.onClick.Invoke();
                    yield return null;
                }
                CollectionAssert.AreEquivalent(controller.Session.GetAvailableBattleCards(stage), seenCards,
                    "Every legal base card must remain reachable across the stage collection pages.");
            }
            ActiveButton("Battle Deck Stage Tab " + NavalBattleStage.Preparation).onClick.Invoke();
            yield return null;
            ActiveDeckCardCopyButton(8).onClick.Invoke();
            yield return null;
            Assert.That(controller.Session.GetBattleDeck(NavalBattleStage.Preparation).Count, Is.EqualTo(preparationBeforeChestEntry.Length - 1),
                "Removing the selected copy changes only that stage's current deck by one card.");
            ActiveButton("Add Deck Card 2").onClick.Invoke();
            yield return null;
            Assert.That(controller.Session.GetBattleDeck(NavalBattleStage.Preparation).Count, Is.EqualTo(preparationBeforeChestEntry.Length));
            Assert.That(controller.Session.GetBattleDeck(NavalBattleStage.Preparation).Count(cardIndex => cardIndex == 2),
                Is.EqualTo(preparationBeforeChestEntry.Count(cardIndex => cardIndex == 2) + 1));
            string expectedSavedDeck = string.Join(",", controller.Session.GetBattleDeck(NavalBattleStage.Preparation));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.StageBattleDeckKey(NavalBattleStage.Preparation), string.Empty), Is.EqualTo(expectedSavedDeck));
            CollectionAssert.AreEqual(deckBeforeChestEntry, controller.Session.GetBattleDeck(NavalBattleStage.Naval));
            CollectionAssert.AreEqual(boardingBeforeChestEntry, controller.Session.GetBattleDeck(NavalBattleStage.Boarding));

            int[] editedDeck = controller.Session.GetBattleDeck(NavalBattleStage.Preparation).ToArray();
            ActiveButton("Back To Card Workshop Chests").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Card Workshop Preview Panel"), Is.Not.Null);
            Assert.That(GameObject.Find("Deck Builder Panel"), Is.Null);
            CollectionAssert.AreEqual(editedDeck, controller.Session.GetBattleDeck(NavalBattleStage.Preparation).ToArray(),
                "Returning to the card preview must retain deck edits.");
            ActiveButton("Workshop Preview Stage Naval").onClick.Invoke();
            yield return null;
            ActiveButton("Open Battle Card Upgrades").onClick.Invoke();
            yield return null;
            yield return ReturnFromWorkshopForgeToSelectedDeck();
            Assert.That(GameObject.Find("Deck Builder Panel"), Is.Not.Null);
            CollectionAssert.AreEqual(editedDeck, controller.Session.GetBattleDeck(NavalBattleStage.Preparation).ToArray(),
                "Selecting another stage must not reset the configured deck.");
            CollectionAssert.AreEqual(deckBeforeChestEntry, controller.Session.GetBattleDeck(NavalBattleStage.Naval));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.StageBattleDeckKey(NavalBattleStage.Preparation), string.Empty), Is.EqualTo(expectedSavedDeck));
            ActiveButton("Return To Port Hub").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);

            // The component market fixture can finish in any port. This part
            // tests the original 800-gold merchant, sold at Broken Anchor;
            // White Shell instead stocks hulls 0, 5 and 6.
            controller.Session.RestoreProfile(controller.Session.ProfileGold, "断锚港");
            Assert.That(CoreShipMarketCatalog.IsSoldAtPort(0, 1), Is.True);
            Button shipyardHotspot = ActiveButton("Port Hotspot Shipyard");
            Assert.That(shipyardHotspot, Is.Not.Null);
            shipyardHotspot.onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Shipyard Panel").GetComponent<Image>().color.a, Is.LessThan(0.2f),
                "The vessel floats in the water berth rather than on opaque cards.");
            Assert.That(GameObject.Find("Shipyard Background").GetComponent<RawImage>().texture.name,
                Is.EqualTo("shipyard_water_gallery_v3"));
            decimal goldBeforeShipInspection = controller.Session.ProfileGold;
            ActiveButton("Shipyard Next Vessel").onClick.Invoke();
            yield return null;
            Button shipBerth = ActiveButton("Shipyard Ship 1");
            Assert.That(shipBerth, Is.Not.Null, "The fixture must browse the merchant in its actual regional stock.");
            GameObject berthGlow = GameObject.Find("Shipyard Berth Glow 1");
            Assert.That(berthGlow, Is.Not.Null, "The currently browsed merchant retains its authored water contact.");
            Image shipBerthGlow = berthGlow.GetComponent<Image>();
            Vector3 restingBerthScale = shipBerth.transform.localScale;
            ExecuteEvents.Execute(shipBerth.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            Assert.That(shipBerthGlow.color.a, Is.Zero);
            Assert.That(shipBerth.transform.localScale, Is.EqualTo(restingBerthScale));
            ExecuteEvents.Execute(shipBerth.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
            Assert.That(shipBerthGlow.color.a, Is.Zero);
            int retainedHullModifier = controller.Session.MaxHull - controller.Session.SelectedShip.MaxHull;
            int retainedArmorModifier = controller.Session.Armor - controller.Session.SelectedShip.Armor;
            int retainedFirepowerModifier = controller.Session.Firepower - controller.Session.SelectedShip.Firepower;
            float retainedSpeedModifier = controller.Session.SpeedKnots - controller.Session.SelectedShip.SpeedKnots;
            float retainedVisionModifier = controller.Session.VisionBonusNm;
            ActiveButton("Shipyard Ship 1").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Shipyard Detail"), Is.Not.Null);
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(0), "Inspection must not switch the flagship.");
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(goldBeforeShipInspection), "Inspection must not charge the player.");
            Button purchaseShip = ActiveButton("Confirm Shipyard Vessel");
            purchaseShip.onClick.Invoke();
            purchaseShip.onClick.Invoke();
            yield return null;
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(goldBeforeShipInspection - 800), "A double click must only charge once.");
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(1));
            Assert.That(controller.Session.SelectedShip.MaxHull, Is.EqualTo(95), "The new ship's base hull should remain unchanged.");
            Assert.That(controller.Session.MaxHull, Is.EqualTo(95),
                "A newly purchased ship has its own original fittings; the previous flagship's upgrades must not be cloned.");
            Assert.That(controller.Session.Armor, Is.EqualTo(controller.Session.SelectedShip.Armor));
            Assert.That(controller.Session.Firepower, Is.EqualTo(controller.Session.SelectedShip.Firepower));
            Assert.That(controller.Session.SpeedKnots, Is.EqualTo(controller.Session.SelectedShip.SpeedKnots).Within(0.0001f));
            Assert.That(controller.Session.VisionBonusNm, Is.Zero);
            foreach (ComponentSlot slot in System.Enum.GetValues(typeof(ComponentSlot)))
                Assert.That(controller.EquippedComponent(slot).Id, Is.EqualTo(componentArt.Get((int)slot * ComponentArtCatalog.EntriesPerSlot).Id));
            Assert.That(controller.ShipComponent(0, purchasedOffer.Entry.Slot).Id, Is.EqualTo(purchasedOffer.Entry.Id),
                "The bought component remains installed on the original ship.");
            Assert.That(controller.Session.CargoCapacityValue, Is.EqualTo(150f));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ShipOwnedKey(1), 0), Is.EqualTo(1));
            ActiveButton("Back To Shipyard Gallery").onClick.Invoke();
            yield return null;
            Assert.That(ActiveButton("Shipyard Ship 1"), Is.Not.Null, "Returning preserves the browsed vessel.");
            ActiveButton("Shipyard Previous Vessel").onClick.Invoke();
            yield return null;
            ActiveButton("Shipyard Ship 0").onClick.Invoke();
            yield return null;
            ActiveButton("Confirm Shipyard Vessel").onClick.Invoke();
            yield return null;
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(0));
            Assert.That(controller.Session.MaxHull, Is.EqualTo(controller.Session.SelectedShip.MaxHull + retainedHullModifier));
            Assert.That(controller.Session.Armor, Is.EqualTo(controller.Session.SelectedShip.Armor + retainedArmorModifier));
            Assert.That(controller.Session.Firepower, Is.EqualTo(controller.Session.SelectedShip.Firepower + retainedFirepowerModifier));
            Assert.That(controller.Session.SpeedKnots,
                Is.EqualTo(controller.Session.SelectedShip.SpeedKnots + retainedSpeedModifier).Within(0.0001f));
            Assert.That(controller.Session.VisionBonusNm, Is.EqualTo(retainedVisionModifier));
            Assert.That(controller.EquippedComponent(purchasedOffer.Entry.Slot).Id, Is.EqualTo(purchasedOffer.Entry.Id));
            ActiveButton("Return To Port Hub").onClick.Invoke();
            yield return null;

            // The showroom fixture switches ports directly above; explicitly
            // move its recruited navigator with it, as a real voyage would.
            PlayerPrefs.SetString(CoreGameplayDemoController.CrewLocationKeyForReset(regionalNavigator.Id), controller.Session.CurrentPort);
            Assert.That(ActiveButton("Port Hotspot Dispatch"), Is.Null);

            Button investmentHotspot = ActiveButton("Port Hotspot Maintenance");
            Assert.That(investmentHotspot, Is.Not.Null);
            investmentHotspot.onClick.Invoke();
            yield return null;

            GameObject investmentPanel = GameObject.Find("Port Investment Panel");
            Assert.That(investmentPanel, Is.Not.Null,
                "The maintenance hotspot now opens the current port investment facility.");
            Assert.That(GameObject.Find("Port Investment Summary"), Is.Not.Null);
            Assert.That(GameObject.Find("Port Investment Ledger"), Is.Not.Null);
            Assert.That(ActiveButton("Confirm Port Investment"), Is.Not.Null);
            Assert.That(GameObject.Find("Maintenance Panel"), Is.Null,
                "The retired maintenance composition must not shadow the investment facility.");

            int waterBeforePurchase = controller.Session.Cargo.Water;
            ActiveButton("Return To Port Hub").onClick.Invoke();
            yield return null;
            Button voyageHotspot = ActiveButton("Port Hotspot Voyage");
            Assert.That(voyageHotspot, Is.Not.Null);
            voyageHotspot.onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Port Briefing"), Is.Not.Null);
            Assert.That(GameObject.Find("Route Preview 0"), Is.Not.Null);
            RawImage[] departureShipImages = GameObject.Find("Starter Modular Ship").GetComponentsInChildren<RawImage>(true);
            Assert.That(departureShipImages.Any(image => image.name.Contains(" Layer ")), Is.False,
                "Purchased equipment must not add overlays to the starting ship.");
            Assert.That(departureShipImages.Single().texture,
                Is.SameAs(Resources.Load<Texture2D>("ModularShip/light_sloop_base")),
                "The starting ship retains its base hull after a component purchase and refit.");
            // The retired Cargo summary is replaced by the actual nine-item
            // provisioning panel. Opening it displays inventory without buying.
            decimal goldBeforeSupplyInspection = controller.Session.ProfileGold;
            Button inspectSupplies = ActiveButton("Departure Buy Provisions");
            Assert.That(inspectSupplies, Is.Not.Null);
            Assert.That(inspectSupplies.interactable, Is.True);
            inspectSupplies.onClick.Invoke();
            yield return null;
            GameObject waterName = GameObject.Find("Departure Basic Supply Name water");
            GameObject waterHeld = GameObject.Find("Departure Basic Supply Held water");
            Assert.That(waterName, Is.Not.Null, "The real departure supply panel must show fresh water.");
            Assert.That(waterHeld, Is.Not.Null);
            Assert.That(waterName.GetComponent<Text>().text, Is.EqualTo("淡水"));
            Assert.That(waterHeld.GetComponent<Text>().text, Is.EqualTo("船舱中 " + waterBeforePurchase));
            Assert.That(GameObject.Find("Departure Basic Supply Quantity water").GetComponent<Text>().text, Is.EqualTo("0"));
            Assert.That(controller.Session.Cargo.Water, Is.EqualTo(waterBeforePurchase));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(goldBeforeSupplyInspection), "Inspecting supplies cannot charge the player.");
            ActiveButton("Departure Basic Supplies Close").onClick.Invoke();
            yield return null;
            Assert.That(ActiveButton("Adjust Voyage Supplies"), Is.Not.Null);

            Button start = ActiveButton("Start Voyage");
            Assert.That(start, Is.Not.Null);
            start.onClick.Invoke();
            yield return null;
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing),
                (string)typeof(CoreGameplayDemoController).GetField("portNotice", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(controller));
            float musicDeadline = Time.realtimeSinceStartup + 3f;
            while (controller.ActiveMusicKey != CoreGameplayDemoController.RouteMusicKey(controller.Session.SelectedRouteIndex) &&
                Time.realtimeSinceStartup < musicDeadline) yield return null;
            Assert.That(controller.Session.Cargo.Water, Is.EqualTo(waterBeforePurchase));
            Assert.That(controller.ActiveMusicKey, Is.EqualTo(CoreGameplayDemoController.RouteMusicKey(controller.Session.SelectedRouteIndex)),
                "Leaving port should crossfade into the selected route music.");

            Assert.That(GameObject.Find("Navigation Chart"), Is.Not.Null);
            Assert.That(GameObject.Find("Map Layer"), Is.Not.Null);
            Assert.That(GameObject.Find("Procedural Sea Chart"), Is.Not.Null);
            Assert.That(controller.VoyageMapArt.IsAvailable, Is.True,
                "The detailed voyage chart must load its chart base, all three water textures, and seven terrain overlays.");
            Assert.That(controller.VoyageMapArt.LoadedTextureCount, Is.EqualTo(12));
            CoreVoyageMap voyageMap = controller.Session.Map;
            Transform oceanLayer = GameObject.Find("Ocean Tile Layer").transform;
            Vector2 oceanSize = ((RectTransform)oceanLayer).rect.size;
            Assert.That(oceanSize, Is.EqualTo(new Vector2(voyageMap.Columns, voyageMap.Rows) * (1160f / 48f)),
                "The voyage chart must preserve square cells for the current map geometry.");
            RawImage oceanBase = GameObject.Find("Ocean Chart Base").GetComponent<RawImage>();
            Assert.That(oceanBase.texture.name, Is.EqualTo("water_chart_base_v2"));
            RawImage[] oceanPatches = oceanLayer.GetComponentsInChildren<RawImage>()
                .Where(image => image.name.StartsWith("Ocean Patch "))
                .ToArray();
            Assert.That(oceanPatches.Length, Is.EqualTo(Mathf.CeilToInt(voyageMap.Columns / 8f) * Mathf.CeilToInt(voyageMap.Rows / 8f)),
                "Eight-cell water patches must cover the complete current chart, including partial edge patches.");
            Assert.That(oceanPatches.Select(patch => patch.texture.name)
                .Where(name => name.StartsWith("water_open_"))
                .Distinct()
                .Count(), Is.EqualTo(3),
                "A seed-bound chart should mix the three detailed blue-water variants.");
            Assert.That(oceanPatches.All(patch => patch.color.a > 0f && patch.color.a < 0.25f), Is.True,
                "Macro water variation should stay subtle over the seamless chart base.");
            Assert.That(oceanPatches.All(patch => patch is VoyageMapBlendGraphic), Is.True,
                "Macro water patches must feather into their neighbours rather than stop at rectangular borders.");
            Assert.That(oceanLayer.GetComponentsInChildren<VoyageMapTileLayerGraphic>(), Is.Empty,
                "The visible water layer must use standard RawImages rather than the unreliable custom mesh batch.");
            Assert.That(GameObject.Find("Demo Route Map"), Is.Null, "Navigation must be rendered from the voyage seed instead of a fixed backdrop.");
            Assert.That(GameObject.Find("Player Ship"), Is.Not.Null);
            RawImage mapShipBase = GameObject.Find("Player Ship Base").GetComponent<RawImage>();
            Assert.That(mapShipBase.texture.name, Does.Contain("light_sloop_base"), "The chart ship should use the transparent modular hull rather than the black-backed legacy atlas.");
            Assert.That(controller.Session.Map.Nodes.Count, Is.GreaterThanOrEqualTo(10));
            Assert.That(controller.Session.Map.Nodes.Select(node => node.Encounter.Id).Distinct().Count(), Is.EqualTo(controller.Session.Map.Nodes.Count));
            Assert.That(voyageMap.ExploredCells.Length, Is.EqualTo(voyageMap.Columns * voyageMap.Rows));
            Assert.That(GameObject.Find("Chart Fog Tiles").GetComponent<VoyageMapTileLayerGraphic>().TileCount,
                Is.EqualTo(voyageMap.FineColumns * voyageMap.FineRows),
                "The visual fog renders the complete chart grid while logical exploration retains its separate grid.");
            Assert.That(GameObject.Find("Current Field Arrow 0"), Is.Not.Null);
            Assert.That(GameObject.Find("Current Direction Arrow"), Is.Null, "Oversized ship-local current arrows were removed.");
            Assert.That(GameObject.Find("Wind Direction Arrow"), Is.Null, "Wind is now one global voyage direction.");
            Assert.That(GameObject.Find("Global Wind Direction Arrow"), Is.Null);
            Assert.That(GameObject.Find("Sea Conditions Legend"), Is.Null);
            Assert.That(GameObject.Find("Departure Port").GetComponent<RawImage>().texture, Is.Not.Null);
            Assert.That(GameObject.Find("Destination Port").GetComponent<RawImage>().texture, Is.Not.Null);
            Text forecastText = GameObject.Find("Forecast").GetComponent<Text>();
            Assert.That(forecastText.text, Does.Contain("预计").And.Contain("航速"));
            Assert.That(forecastText.text, Does.Contain("淡水").And.Contain("食物"));
            Assert.That(GameObject.Find("Forecast Details Panel"), Is.Null, "Wind/current detail starts collapsed.");
            double hoursBeforeDisclosure = controller.Session.ElapsedGameHours;
            ActiveButton("Forecast Details").onClick.Invoke();
            yield return null;
            Text detailText = GameObject.Find("Forecast Detail Text").GetComponent<Text>();
            Assert.That(detailText.text, Does.Contain("洋流").And.Contain("海风"));
            ActiveButton("Forecast Details").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Forecast Details Panel"), Is.Null);
            Assert.That(controller.Session.ElapsedGameHours, Is.EqualTo(hoursBeforeDisclosure));

            MapCourseInputSurface courseInput = Object.FindFirstObjectByType<MapCourseInputSurface>();
            Assert.That(courseInput, Is.Not.Null, "The chart should accept clicks in unexplored water.");
            for (int index = 0; index < controller.Session.Map.Nodes.Count; index += 1)
            {
                controller.Session.Map.Nodes[index].Discovered = false;
                controller.Session.Map.Nodes[index].IntelRevealed = false;
            }
            for (int index = 0; index < controller.Session.Map.ExploredCells.Length; index += 1)
            {
                controller.Session.Map.ExploredCells[index] = true;
            }
            int revealedTerrainCell = System.Array.FindIndex(controller.Session.Map.ExploredCells, explored => explored);
            Assert.That(revealedTerrainCell, Is.GreaterThanOrEqualTo(0));
            controller.Session.Map.TerrainCells[revealedTerrainCell] = SeaTerrainType.FogBank;
            courseInput.SelectLocalPoint(new Vector2(250f, -100f));
            yield return null;
            Assert.That(controller.Session.HasDirectCourseTarget, Is.True);
            Assert.That(GameObject.Find("Direct Course Target Halo"), Is.Not.Null);
            Transform terrainArtLayer = GameObject.Find("Terrain Art Layer").transform;
            RawImage[] terrainFeatures = terrainArtLayer.GetComponentsInChildren<RawImage>()
                .Where(image => image.name.StartsWith("Terrain Feature "))
                .ToArray();
            HashSet<string> expectedNaturalFeatures = new HashSet<string>();
            for (int cellIndex = 0; cellIndex < controller.Session.Map.CellCount; cellIndex += 1)
            {
                SeaTerrainType terrain = controller.Session.Map.TerrainCells[cellIndex];
                int featureId = controller.Session.Map.TerrainFeatureIds[cellIndex];
                if (controller.Session.Map.ExploredCells[cellIndex] &&
                    terrain != SeaTerrainType.OpenWater &&
                    terrain != SeaTerrainType.Strait &&
                    terrain != SeaTerrainType.Island && terrain != SeaTerrainType.Beach && terrain != SeaTerrainType.Shoal &&
                    featureId >= 0 &&
                    controller.Session.Map.AnchorNodeIds[cellIndex] < 0)
                {
                    expectedNaturalFeatures.Add(featureId + " " + terrain);
                }
            }
            Assert.That(expectedNaturalFeatures.Count, Is.GreaterThan(0));
            Assert.That(terrainFeatures.Length, Is.EqualTo(expectedNaturalFeatures.Count),
                "Non-solid natural details keep one feathered illustration; solid land uses the continuous coast field.");
            Assert.That(terrainFeatures.All(feature => feature.texture != null && feature.texture.name.StartsWith("terrain_")), Is.True);
            Assert.That(terrainArtLayer.GetComponentsInChildren<VoyageMapTileLayerGraphic>(), Is.Empty,
                "Natural terrain should no longer depend on the custom tile-batch graphic.");
            CoreMapNode straitNode = controller.Session.Map.Nodes.First(node => node.TerrainConstraint == EventTerrainConstraint.Strait);
            Assert.That(GameObject.Find("Strait Feature " + straitNode.Id), Is.Null,
                "The cropped double-bank rectangle must not return; strait banks need closed coastlines.");
            VoyageChartSurfaceGraphic coastSurface = GameObject.Find("Continuous Coast Surface").GetComponent<VoyageChartSurfaceGraphic>();
            Assert.That(coastSurface.Field, Is.Not.Null);
            Assert.That(coastSurface.material.shader.name, Is.EqualTo("Shattered Sail/UI/Continuous Voyage Coast"));
            VoyageGridCoordinate straitAnchor = controller.Session.Map.PointToCell(straitNode.Position);
            Assert.That(coastSurface.Field.SampleSignedDistance(straitAnchor.Column + .5f, straitAnchor.Row + .5f), Is.LessThan(0f),
                "The visible strait anchor remains water, consistent with navigation.");
            int terrainColumn = revealedTerrainCell % voyageMap.Columns;
            int terrainRow = revealedTerrainCell / voyageMap.Columns;
            Image terrainCell = GameObject.Find("Sea Terrain " + terrainColumn + " " + terrainRow).GetComponent<Image>();
            Assert.That(terrainCell, Is.Not.Null);
            Assert.That(terrainCell.sprite, Is.Null, "Terrain cells must no longer use the old monochrome circle sprite.");
            Assert.That(terrainCell.color.a, Is.LessThan(0.3f), "Terrain-cell tint should reveal the illustrated feature beneath it.");

            CoreMapNode snappedNode = controller.Session.Map.Nodes.First(node => node.Encounter != null && node.Encounter.HasChartLocation && !node.Visited);
            snappedNode.Position = new VoyagePoint(controller.Session.Position.X + 3f, controller.Session.Position.Y + 1f);
            snappedNode.Discovered = true;
            snappedNode.IntelRevealed = true;
            courseInput = Object.FindFirstObjectByType<MapCourseInputSurface>();
            Vector2 snappedLocal = new Vector2(
                (snappedNode.Position.X / voyageMap.WorldWidth - .5f) * oceanSize.x + 40f,
                (snappedNode.Position.Y / voyageMap.WorldHeight - .5f) * oceanSize.y);
            courseInput.SelectLocalPoint(snappedLocal);
            yield return null;
            Assert.That(controller.Session.DirectCourseTarget.X, Is.EqualTo(snappedNode.Position.X).Within(0.001f));
            Assert.That(controller.Session.DirectCourseTarget.Y, Is.EqualTo(snappedNode.Position.Y).Within(0.001f));
            Assert.That(GameObject.Find("Course Target Label"), Is.Null);

            Button returnButton = ActiveButton("Safe Return");
            Assert.That(returnButton, Is.Not.Null);
            returnButton.onClick.Invoke();
            yield return null;

            Assert.That(GameObject.Find("Settlement Card"), Is.Not.Null);
            Button finish = ActiveButton("Return To Port");
            Assert.That(finish, Is.Not.Null);
            finish.onClick.Invoke();
            yield return null;

            Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator FirstLaunch_StartsAtPortHubAndReloadRequiresDockEntryBeforeDeparture()
        {
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;

            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.IsTutorialActive, Is.True);

            Assert.That(controller.CurrentGuidedTutorialStep, Is.EqualTo(GuidedTutorialStep.FirstDeparture));
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);
            Assert.That(GameObject.Find("Voyage Setup"), Is.Null);
            Assert.That(ActiveButton("Start Voyage"), Is.Null);
            Assert.That(typeof(CoreGameplayDemoController).GetMethod("PrepareGuidedTutorialDeparture",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(controller, null), Is.EqualTo(false), "A direct departure callback cannot bypass dock entry.");
            Assert.That(controller.Session.GuidedTutorialLeg, Is.Zero);
            Assert.That(RequireGuidedButton("Port Hotspot Voyage").IsInteractable(), Is.True);
            AssertGuidedFocusTarget("Port Hotspot Voyage");
            foreach (string facility in new[] { "Warehouse", "Shipyard", "Tavern", "ComponentShop", "Maintenance", "CardWorkshop" })
                Assert.That(RequireGuidedButton("Port Hotspot " + facility).IsInteractable(), Is.False,
                    "The first lesson must guide entry to the dock before unlocking other facilities.");
            Assert.That(ActiveButton("Tutorial Primary"), Is.Null);
            Assert.That(ActiveButton("Skip Tutorial"), Is.Null);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey, 0), Is.Zero);

            NavalBattleVisualCapture.Capture(controller, "01_New_Save_Port_Hub.png",
                "新存档从港口总览开始，红框引导点击出航码头。", "Port Hub");

            ClickGuidedButton("Port Hotspot Voyage");
            yield return null;
            Assert.That(GameObject.Find("Voyage Setup"), Is.Not.Null);
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(RequireGuidedButton("Start Voyage").IsInteractable(), Is.True);
            AssertGuidedFocusTarget("Start Voyage");
            NavalBattleVisualCapture.Capture(controller, "02_New_Save_Departure.png",
                "点击出航码头后，继续目的地、海图与扬帆教学。", "Voyage Setup");

            // An unfinished initial save must reopen the harbor, including if
            // the player had entered the dock but had not started the voyage.
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.CurrentGuidedTutorialStep, Is.EqualTo(GuidedTutorialStep.FirstDeparture));
            Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);
            Assert.That(GameObject.Find("Voyage Setup"), Is.Null);
            Assert.That(RequireGuidedButton("Port Hotspot Voyage").IsInteractable(), Is.True);
            AssertGuidedFocusTarget("Port Hotspot Voyage");
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialCheckpointKey, 0), Is.Zero);

            ClickGuidedButton("Port Hotspot Voyage");
            yield return null;
            ClickGuidedButton("Start Voyage");
            yield return null;
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(controller.CurrentGuidedTutorialNavigationInputStep,
                Is.EqualTo(GuidedTutorialNavigationInputStep.Heading));
            AssertGuidedFocusTarget("Heading East");
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey, 0), Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        private static Button ActiveButton(string name)
        {
            return Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(button => button.name == name);
        }

        private static Button ActiveDeckCardCopyButton(int cardIndex)
        {
            string prefix = "Remove Deck Card " + cardIndex + " Copy ";
            Button button = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(candidate => candidate.name.StartsWith(prefix, System.StringComparison.Ordinal))
                .OrderBy(candidate => candidate.transform.GetSiblingIndex())
                .FirstOrDefault();
            Assert.That(button, Is.Not.Null, "Every selected base-card copy must have an individual remove control: " + cardIndex);
            return button;
        }

        private static IEnumerator RevealDeckCardCopy(Button button)
        {
            ScrollRect scroll = button.GetComponentInParent<ScrollRect>();
            Assert.That(scroll, Is.Not.Null, "Selected deck cards must live in the scrollable rail.");
            Canvas.ForceUpdateCanvases();
            RectTransform viewport = scroll.viewport;
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, button.transform);
            float range = scroll.content.rect.height - viewport.rect.height;
            if (range > 0f)
            {
                float delta = bounds.max.y > viewport.rect.yMax ? bounds.max.y - viewport.rect.yMax
                    : bounds.min.y < viewport.rect.yMin ? bounds.min.y - viewport.rect.yMin : 0f;
                if (Mathf.Abs(delta) > .1f)
                {
                    scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + delta / range);
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                }
            }
        }

        [UnityTest]
        public IEnumerator StageDecks_MigrateLegacyCardsAndRetainIndependentEditsAfterReload()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            int[] legacy = { 0, 0, 0, 1, 2, 3, 4, 5 };
            int[] boarding = { 3, 4, 14, 14, 15, 16, 17, 18 };
            PlayerPrefs.SetString(CoreGameplayDemoController.BattleDeckKey, string.Join(",", legacy));
            PlayerPrefs.SetString(CoreGameplayDemoController.StageBattleDeckKey(NavalBattleStage.Boarding), string.Join(",", boarding));
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;

            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(AchievementUnlocked("tutorial_deck"), Is.False,
                "Restoring default or legacy decks must not count as a manual edit.");
            NavalBattleStage[] stages = { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding };
            Dictionary<NavalBattleStage, int[]> expected = new Dictionary<NavalBattleStage, int[]>();
            foreach (NavalBattleStage stage in stages)
            {
                int[] deck = controller.Session.GetBattleDeck(stage).ToArray();
                Assert.That(deck.Length, Is.EqualTo(10));
                Assert.That(deck.All(index => CoreVoyageSession.CardLibrary[index].IsAllowedInStage(stage)), Is.True);
                Assert.That(deck.GroupBy(index => index).All(group => group.Count() <= 2), Is.True);
                if (stage == NavalBattleStage.Boarding) CollectionAssert.AreEqual(boarding, deck.Take(boarding.Length));
                else
                    foreach (IGrouping<int, int> retained in legacy.Where(index => CoreVoyageSession.CardLibrary[index].IsAllowedInStage(stage)).GroupBy(index => index))
                        Assert.That(deck.Count(index => index == retained.Key), Is.GreaterThanOrEqualTo(System.Math.Min(2, retained.Count())),
                            "Migration preserves the two owned copies before filling the stage deck.");
                expected[stage] = deck;
                Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.StageBattleDeckKey(stage)), Is.EqualTo(string.Join(",", deck)));
            }

            ActiveButton("Port Hotspot CardWorkshop").onClick.Invoke();
            yield return null;
            ActiveButton("Workshop Preview Stage Preparation").onClick.Invoke();
            yield return null;
            ActiveButton("Open Battle Card Upgrades").onClick.Invoke();
            yield return null;
            yield return ReturnFromWorkshopForgeToSelectedDeck();
            foreach (NavalBattleStage stage in stages)
            {
                ActiveButton("Battle Deck Stage Tab " + stage).onClick.Invoke();
                yield return null;
                Button add = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .First(button => button.name.StartsWith("Add Deck Card ") && button.interactable &&
                        controller.Session.GetBattleDeck(stage).Count(index => index == int.Parse(button.name.Substring("Add Deck Card ".Length))) < 2);
                int cardIndex = int.Parse(add.name.Substring("Add Deck Card ".Length));
                int countBefore = expected[stage].Count(index => index == cardIndex);
                add.onClick.Invoke();
                yield return null;
                Assert.That(AchievementUnlocked("tutorial_deck"), Is.True);
                Assert.That(controller.Session.GetBattleDeck(stage).Count, Is.EqualTo(expected[stage].Length + 1));
                Assert.That(controller.Session.GetBattleDeck(stage).Count(index => index == cardIndex), Is.EqualTo(countBefore + 1));
                expected[stage] = controller.Session.GetBattleDeck(stage).ToArray();
                foreach (NavalBattleStage untouched in stages)
                {
                    CollectionAssert.AreEqual(expected[untouched], controller.Session.GetBattleDeck(untouched));
                    Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.StageBattleDeckKey(untouched)),
                        Is.EqualTo(string.Join(",", expected[untouched])));
                }
            }

            PlayerPrefs.SetString(CoreGameplayDemoController.BattleDeckKey, "7,7,7,8,8,8,9,9");
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(AchievementUnlocked("tutorial_deck"), Is.True,
                "A completed manual-edit achievement survives reloading the same save.");
            foreach (NavalBattleStage stage in stages)
                CollectionAssert.AreEqual(expected[stage], controller.Session.GetBattleDeck(stage),
                    "Once migrated, each v2 stage must reload its own saved edits independently of the old combined deck.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Achievements_ConfirmedVoyageReturnRecordsDamageAndSuccessfulNavigatorVoyage()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            for (int voyage = 0; voyage < 3; voyage++)
            {
                // First return is undamaged and has no navigator. The second is
                // damaged but fails; only the third earns a navigator completion.
                var session = controller.Session;
                session.ConfigureCrewCapabilities(voyage > 0, false, false, false, false, false);
                session.Cargo.Water = 10;
                session.Cargo.Food = 10;
                session.StartVoyage();
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
                if (voyage == 1)
                {
                    typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, session.MaxHull - 1);
                    session.Cargo.Water = 0;
                }
                Assert.That(session.SafeReturn(), Is.True);
                Assert.That(session.Settlement.Outcome,
                    Is.EqualTo(voyage == 1 ? VoyageOutcome.Failure : VoyageOutcome.SafeReturn));
                RenderResponsiveBattle(controller); yield return null;
                Assert.That(AchievementUnlocked("tutorial_recovery"), Is.EqualTo(voyage == 2));
                Assert.That(AchievementUnlocked("tutorial_navigator"), Is.False,
                    "An unconfirmed settlement must not record a completed voyage.");
                Button skip = ActiveButton("Skip Settlement Reveal");
                if (skip != null && skip.IsInteractable()) { skip.onClick.Invoke(); yield return null; }
                ActiveButton("Return To Port").onClick.Invoke(); yield return null;
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
                Assert.That(AchievementUnlocked("tutorial_recovery"), Is.EqualTo(voyage >= 1));
                Assert.That(AchievementUnlocked("tutorial_navigator"), Is.EqualTo(voyage == 2),
                    "A failed voyage with a navigator must not count as a successful navigator voyage.");
            }
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            Assert.That(AchievementUnlocked("tutorial_recovery"), Is.True);
            Assert.That(AchievementUnlocked("tutorial_navigator"), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        private static bool AchievementUnlocked(string id) =>
            AchievementCatalog.IsUnlocked(AchievementCatalog.All.Single(definition => definition.Id == id));

        [UnityTest]
        public IEnumerator RetiredPlaceholderCrew_AllPortsHaveOnlySixIllustratedCandidates_AndLegacySaveIsCleaned()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            ProfileGoldPersistence.Save(1000);
            PlayerPrefs.SetInt(CoreGameplayDemoController.LegacyWitchRecruitedKey, 1);
            CrewVariantDefinition yilan = CrewNarrativeCatalog.ForPortAndProfession("暮潮港", 3);
            CoreSpecialCardDefinition yilanCard = CoreSpecialCardCatalog.ForCrew(yilan.Id);
            CoreSpecialCardDefinition navigatorCard = CoreSpecialCardCatalog.ForCrew(CrewNarrativeCatalog.LegacyForProfession(0).Id);
            string retiredIdentityCard = "card_special_crew_witch_jinchao";
            PlayerPrefs.SetString(CoreGameplayDemoController.SpecialCardExperienceKey,
                "card_witch_fire_rounds=840;card_witch_hex=360;" + retiredIdentityCard + "=840;" + yilanCard.Card.Id + "=177;" + navigatorCard.Card.Id + "=213");
            PlayerPrefs.SetString(CoreGameplayDemoController.AcquiredSpecialCardsKey,
                "card_witch_fire_rounds;card_witch_hex;" + retiredIdentityCard + ";" + yilanCard.Card.Id + ";" + navigatorCard.Card.Id);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.LegacyWitchRecruitedKey), Is.False);
            AssertRetiredPlaceholderProgressAbsent(controller);
            Assert.That(controller.Session.GetSpecialCardExperience(yilanCard.Card.Id), Is.EqualTo(177), "The removed placeholder cannot transfer its XP to the real Yilan.");
            Assert.That(controller.Session.GetSpecialCardExperience(navigatorCard.Card.Id), Is.EqualTo(213));
            string[] ports = { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" };
            for (int port = 0; port < ports.Length; port++)
            {
                controller.Session.RestoreProfile(1000, ports[port]);
                ActiveButton("Port Hotspot Tavern").onClick.Invoke();
                yield return null;
                Assert.That(ActiveButton("Witch Recruitment Contract"), Is.Null);
                for (int profession = 0; profession < 6; profession++)
                {
                    Assert.That(ActiveButton("Tavern Crew " + profession), Is.Not.Null);
                    Assert.That(GameObject.Find("Tavern Crew Name " + profession).GetComponent<Text>().text,
                        Is.EqualTo(CrewNarrativeCatalog.ForPortAndProfession(ports[port], profession).DisplayName));
                }
                Assert.That(Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Any(label => label.text.Contains("烬潮")), Is.False);
                NavalBattleVisualCapture.Capture(controller, "90_Tavern_Without_Placeholder_" + port + ".png",
                    ports[port] + "：只有六名有角色画面的候选人，无女巫占位木牌", "Tavern Crew Stage");
                ActiveButton("Return To Port Hub").onClick.Invoke();
                yield return null;
            }

            // Ordinary recruitment, card acquisition and repeated-click guards survive
            // removal of the unauthored seventh candidate.
            ActiveButton("Port Hotspot Tavern").onClick.Invoke();
            yield return null;
            ActiveButton("Tavern Crew 0").onClick.Invoke();
            yield return null;
            Button.ButtonClickedEvent recruit = ActiveButton("Recruit Tavern Crew 0").onClick;
            decimal goldBefore = controller.Session.ProfileGold;
            recruit.Invoke();
            yield return null;
            decimal goldAfter = controller.Session.ProfileGold;
            Assert.That(goldAfter, Is.EqualTo(goldBefore - CrewNarrativeCatalog.ForPortAndProfession("灰鸥港", 0).ContractCost));
            Assert.That(controller.Session.HasNavigator, Is.True);
            Assert.That(ActiveButton("Recruit Tavern Crew 0").interactable, Is.False);
            recruit.Invoke();
            yield return null;
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(goldAfter));
            foreach (NavalBattleStage stage in System.Enum.GetValues(typeof(NavalBattleStage)))
            {
                Assert.That(controller.Session.GetCrewBattleCards(stage).Count(index => CoreVoyageSession.CardLibrary[index].Id == navigatorCard.Card.Id),
                    Is.EqualTo(navigatorCard.Card.IsAllowedInStage(stage) ? 1 : 0));
                Assert.That(controller.Session.GetBattleDeck(stage).Any(index => CoreVoyageSession.CardLibrary[index].RequiredCrew.Length > 0), Is.False);
            }
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session.HasNavigator, Is.True);
            AssertRetiredPlaceholderProgressAbsent(controller);
            Assert.That(controller.Session.GetSpecialCardExperience(yilanCard.Card.Id), Is.EqualTo(177));
            Assert.That(controller.Session.GetSpecialCardExperience(navigatorCard.Card.Id), Is.EqualTo(213));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(goldAfter));
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertRetiredPlaceholderProgressAbsent(CoreGameplayDemoController controller)
        {
            Assert.That(CoreVoyageSession.CardLibrary.Any(card => card.Id.Contains("witch") || card.RequiredCrew.Contains("witch")), Is.False);
            Assert.That(controller.Session.AcquiredSpecialCardIds.Any(id => id.Contains("witch")), Is.False);
            Assert.That(controller.Session.SpecialCardExperience.Keys.Any(id => id.Contains("witch")), Is.False);
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.SpecialCardExperienceKey), Does.Not.Contain("witch"));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.AcquiredSpecialCardsKey), Does.Not.Contain("witch"));
        }

        [UnityTest]
        public IEnumerator LegacyDispatchData_DoesNotBlockLocalCrewCardsAndStillRespectsTheirLocation()
        {
            CrewVariantDefinition away = CrewNarrativeCatalog.LegacyForProfession(0);
            CrewVariantDefinition aboard = CrewNarrativeCatalog.ForPortAndProfession("白贝港", 0);
            Assert.That(aboard.Id, Is.Not.EqualTo(away.Id));
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewRecruitmentKey(0), 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(away.Id), 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(aboard.Id), 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, "断锚港");
            PlayerPrefs.SetString(CoreGameplayDemoController.CrewLocationKeyForReset(away.Id), "断锚港");
            PlayerPrefs.SetString(CoreGameplayDemoController.CrewLocationKeyForReset(aboard.Id), "白贝港");
            PlayerPrefs.SetString(CoreGameplayDemoController.ActiveCrewVariantKey(0), away.Id);
            PlayerPrefs.SetInt("ShatteredSail.Dispatch.Mission.v1", 0);
            PlayerPrefs.SetInt("ShatteredSail.Dispatch.BalanceVersion", 1);
            PlayerPrefs.SetString("ShatteredSail.Dispatch.CrewCsv.v1", "0");
            PlayerPrefs.SetString("ShatteredSail.Dispatch.CrewIdentities.v2", away.Id);
            PlayerPrefs.SetInt("ShatteredSail.Dispatch.RealClock.v1", 1);
            PlayerPrefs.SetString("ShatteredSail.Dispatch.EndsAt.v1", System.DateTime.UtcNow.AddDays(1).Ticks.ToString());
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session.HasNavigator, Is.True, "Retired assignment data must not reserve a local crew member.");
            CoreSpecialCardDefinition localCard = CoreSpecialCardCatalog.ForCrew(away.Id);
            foreach (NavalBattleStage stage in System.Enum.GetValues(typeof(NavalBattleStage)))
                Assert.That(controller.Session.GetCrewBattleCards(stage).Count(index => CoreVoyageSession.CardLibrary[index].RequiredCrew == away.Id),
                    Is.EqualTo(localCard.Card.IsAllowedInStage(stage) ? 1 : 0));

            PlayerPrefs.SetString(CoreGameplayDemoController.CrewLocationKeyForReset(away.Id), "白贝港");
            PlayerPrefs.SetString(CoreGameplayDemoController.ActiveCrewVariantKey(0), aboard.Id);
            PlayerPrefs.SetString(CoreGameplayDemoController.CrewLocationKeyForReset(aboard.Id), "断锚港");
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session.HasNavigator, Is.True, "A separately recruited navigator still aboard must retain their cards.");
            CoreSpecialCardDefinition aboardCard = CoreSpecialCardCatalog.ForCrew(aboard.Id);
            foreach (NavalBattleStage stage in System.Enum.GetValues(typeof(NavalBattleStage)))
            {
                Assert.That(controller.Session.GetCrewBattleCards(stage).Count(index => CoreVoyageSession.CardLibrary[index].RequiredCrew == aboard.Id),
                    Is.EqualTo(aboardCard.Card.IsAllowedInStage(stage) ? 1 : 0));
                Assert.That(controller.Session.GetCrewBattleCards(stage).Any(index => CoreVoyageSession.CardLibrary[index].RequiredCrew == away.Id), Is.False);
            }
            Assert.That(PlayerPrefs.GetString("ShatteredSail.Dispatch.CrewIdentities.v2"), Is.EqualTo(away.Id));
            LogAssert.NoUnexpectedReceived();
        }

        private static void ResetSavedProgress()
        {
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.TutorialCompletedKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.TutorialRewardGrantedKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.ProfileGoldKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.CurrentPortKey);
            foreach (ComponentSlot slot in System.Enum.GetValues(typeof(ComponentSlot)))
            {
                PlayerPrefs.DeleteKey(CoreGameplayDemoController.ComponentLoadoutKey(slot));
            }
            foreach (string key in MetaIntKeys()) PlayerPrefs.DeleteKey(key);
            foreach (string key in MetaStringKeys()) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        private void PreserveMetaProgressionPrefs()
        {
            hadMetaIntPrefs.Clear();
            savedMetaIntPrefs.Clear();
            hadMetaStringPrefs.Clear();
            savedMetaStringPrefs.Clear();
            foreach (string key in MetaIntKeys())
            {
                if (!PlayerPrefs.HasKey(key)) continue;
                hadMetaIntPrefs.Add(key);
                savedMetaIntPrefs[key] = PlayerPrefs.GetInt(key, 0);
            }
            foreach (string key in MetaStringKeys())
            {
                if (!PlayerPrefs.HasKey(key)) continue;
                hadMetaStringPrefs.Add(key);
                savedMetaStringPrefs[key] = PlayerPrefs.GetString(key, string.Empty);
            }
        }

        private static IEnumerable<string> MetaIntKeys()
        {
            foreach (string key in AchievementCatalog.PersistenceKeys) yield return key;
            yield return CoreGameplayDemoController.ComponentPurchaseAchievementKey;
            yield return CoreGameplayDemoController.MarketPurchaseStorageModeKey;
            yield return CoreGameplayDemoController.WarehousePortMigrationKey;
            yield return CoreGameplayDemoController.WarehouseDisplayRecordKey;
            yield return CoreGameplayDemoController.GuidedTutorialCheckpointKey;
            yield return CoreGameplayDemoController.GuidedTutorialDeckEditedKey;
            yield return CoreGameplayDemoController.GuidedTutorialComponentRewardKey;
            yield return CoreGameplayDemoController.GuidedTutorialRecoveryKey;
            yield return CoreGameplayDemoController.GuidedTutorialNavigatorEventKey;
            yield return CoreGameplayDemoController.GuidedTutorialWarehousePendingKey;
            yield return CoreGameplayDemoController.GuidedTutorialEquipmentPendingKey;
            yield return CoreGameplayDemoController.SelectedShipKey;
            yield return "ShatteredSail.Dispatch.Mission.v1";
            yield return "ShatteredSail.Dispatch.DurationHours.v1";
            yield return "ShatteredSail.Dispatch.BalanceVersion";
            yield return "ShatteredSail.Dispatch.GameClock.v1";
            yield return "ShatteredSail.Dispatch.RealClock.v1";
            yield return CoreGameplayDemoController.BattleDeckEditedKey;
            yield return CoreGameplayDemoController.LegacyWitchRecruitedKey;
            foreach (string port in new[] { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" })
            {
                yield return CoreGameplayDemoController.PortInvestmentKey(port);
                yield return CoreGameplayDemoController.GameMasterShopRefreshKey(port);
                foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                    yield return CoreGameplayDemoController.MarketPurchaseKey(port, CoreVoyageSession.GameClockOrigin, item);
            }
            foreach (ComponentSlot slot in System.Enum.GetValues(typeof(ComponentSlot)))
                yield return CoreGameplayDemoController.ComponentConditionKey(slot);
            for (int index = 0; index < 6; index += 1) yield return CoreGameplayDemoController.CrewRecruitmentKey(index);
            foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew) yield return CoreGameplayDemoController.CrewVariantRecruitmentKey(crew.Id);
            for (int index = 0; index < CoreVoyageSession.Ships.Count; index += 1) yield return CoreGameplayDemoController.ShipOwnedKey(index);
            string[] cargo = { "Water", "Food", "Beer", "Medicine", "Wood", "Iron", "Cloth", "Powder", "Cannonballs", "Gold", "Fame" };
            for (int index = 0; index < cargo.Length; index += 1) yield return CoreGameplayDemoController.CargoKey(cargo[index]);
        }

        private static IEnumerable<string> MetaStringKeys()
        {
            yield return CoreGameplayDemoController.GameClockKey;
            yield return CoreGameplayDemoController.WorldEconomyScopeKey;
            yield return ProfileGoldPersistence.DecimalKey;
            yield return CoreGameplayDemoController.PortWarehouseCargoKey;
            foreach (string port in CoreVoyageSession.DestinationPorts)
            {
                yield return CoreGameplayDemoController.WarehouseOwnedPortKey(port);
                yield return CoreGameplayDemoController.WarehouseDisplayPortKey(port);
            }
            yield return CoreGameplayDemoController.TreasureProgressKey;
            yield return CoreGameplayDemoController.DrawnSeaChartKey;
            yield return CoreGameplayDemoController.SeaChartProgressKey;
            yield return CoreGameplayDemoController.CollectionProgressKey;
            yield return CoreGameplayDemoController.RecordedSeaChartsKey;
            yield return CoreGameplayDemoController.GameMasterShopScopeKey;
            yield return CoreGameplayDemoController.WarehouseOwnedKey;
            yield return CoreGameplayDemoController.WarehouseDisplayKey;
            yield return CoreGameplayDemoController.ComponentInventoryKey;
            yield return CoreGameplayDemoController.BattleDeckKey;
            yield return CoreGameplayDemoController.BattleCardUpgradeKey;
            yield return CoreGameplayDemoController.SpecialCardExperienceKey;
            yield return CoreGameplayDemoController.AcquiredSpecialCardsKey;
            foreach (NavalBattleStage stage in System.Enum.GetValues(typeof(NavalBattleStage)))
                yield return CoreGameplayDemoController.StageBattleDeckKey(stage);
            yield return "ShatteredSail.Dispatch.EndsAt.v1";
            yield return "ShatteredSail.Dispatch.CrewCsv.v1";
            yield return "ShatteredSail.Dispatch.CrewIdentities.v2";
            yield return CoreGameplayDemoController.CrewAtSeaKey;
            for (int index = 0; index < 6; index += 1) yield return CoreGameplayDemoController.ActiveCrewVariantKey(index);
            foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew)
                yield return CoreGameplayDemoController.CrewLocationKeyForReset(crew.Id);
        }

        private static void DeleteCurrentComponentShopPurchases()
        {
            ComponentArtCatalog art = new ComponentArtCatalog();
            string[] ports = { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" };
            foreach (string port in ports)
            {
                foreach (PortComponentShopStockItem offer in PortComponentShopCatalog.BuildStock(art, port, CoreVoyageSession.GameClockOrigin)
                    .Concat(PortComponentShopCatalog.BuildExpandedStock(art, port, CoreVoyageSession.GameClockOrigin)))
                {
                    PlayerPrefs.DeleteKey(CoreGameplayDemoController.ComponentShopPurchaseKey(offer.Id));
                }
            }
        }
    }
}
