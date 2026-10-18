using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        public IEnumerator DeckBuilderSpecial_CrewConfigurationReturnsToSameStagePageAndUpdatesMembership()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            CrewVariantDefinition navigator = CrewNarrativeCatalog.FindById("crew_white_shell_navigator_saina");
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, navigator.PortName);
            CoreSpecialCardDefinition source = CoreSpecialCardCatalog.ForCrew(navigator.Id);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(navigator.Id), 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewRecruitmentKey(0), 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.ActiveCrewVariantKey(0), navigator.Id);
            PlayerPrefs.SetString(CoreGameplayDemoController.CrewLocationKeyForReset(navigator.Id), navigator.PortName);
            PlayerPrefs.SetString(CoreGameplayDemoController.SpecialCardExperienceKey, source.Card.Id + "=120");
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return ClickShipyardControl("Port Hotspot Voyage");
            yield return ClickShipyardControl("Configure Voyage Crew");
            Assert.That(DepartureCrewChoiceState(0), Is.EqualTo("已登船"));
            yield return ClickShipyardControl("Departure Special Crew Toggle 0");
            yield return ClickShipyardControl("Confirm Departure Crew");
            int route = controller.Session.SelectedRouteIndex;
            string destination = controller.Session.SelectedDestinationPort;
            Button departureBack = ActiveButton("Return To Port Hub");
            int index = DeckBuilderIndex(source.Card.Id);
            AcquireSpecialCardSecondPageFixture(controller, index, NavalBattleStage.Naval);
            yield return ClickShipyardControl("Configure Voyage Battle Decks");
            yield return ClickShipyardControl("Edit Stage Deck Naval");
            yield return ClickShipyardControl("Open Special Card Collection");
            yield return RevealWorkshopSpecialCard(index);
            yield return ClickShipyardControl("Select Special Card " + index);
            AssertSpecialCardMembership(index, false);
            Assert.That(ActiveButton("Configure Special Card Source " + index).GetComponentInChildren<Text>().text, Is.EqualTo("配置船员"));
            string page = GameObject.Find("Upgrade Collection Page").GetComponent<Text>().text;
            Assert.That(ActiveButton("Previous Upgrade Cards").interactable, Is.True, "The return must preserve a noninitial collection page.");
            Dictionary<string, string> saved = SpecialSourceNavigationProgress();
            Dictionary<NavalBattleStage, int[]> decks = SpecialSourceNavigationDecks(controller);
            decimal gold = controller.Session.ProfileGold;
            yield return new WaitForSecondsRealtime(.7f);
            NavalBattleVisualCapture.Capture(controller, "DeckBuilderSpecial_Crew_01_Standby.png",
                "炮击阶段已收藏的待命船员卡显示配置船员，且只展示本阶段卡牌", "Card Workshop Preview Panel");

            yield return ClickShipyardControl("Configure Special Card Source " + index);
            Assert.That(GameObject.Find("Departure Crew Configuration Title"), Is.Not.Null);
            Assert.That(DepartureCrewChoiceState(0), Is.EqualTo("待命"), "Opening configuration does not board the crew automatically.");
            Assert.That(controller.Session.GetBonusBattleCards(NavalBattleStage.Naval), Has.No.Member(index));
            yield return ClickShipyardControl("Back To Voyage Setup");
            AssertReturnedWorkshopSpecialCard(controller, index, NavalBattleStage.Naval, page, false);
            yield return ClickShipyardControl("Configure Special Card Source " + index);
            yield return ClickShipyardControl("Departure Special Crew Toggle 0");
            Assert.That(DepartureCrewChoiceState(0), Is.EqualTo("已登船"));
            yield return ClickShipyardControl("Confirm Departure Crew");
            AssertReturnedWorkshopSpecialCard(controller, index, NavalBattleStage.Naval, page, true);
            Assert.That(controller.Session.GetBonusBattleCards(NavalBattleStage.Naval), Has.Member(index));
            AssertSpecialSourceNavigationProgress(controller, decks, saved, gold);
            Assert.That(controller.Session.GetSpecialCardExperience(source.Card.Id), Is.EqualTo(120));
            yield return new WaitForSecondsRealtime(.7f);
            NavalBattleVisualCapture.Capture(controller, "DeckBuilderSpecial_Crew_02_Joined.png",
                "船员登船后返回原阶段原页，卡牌下方由配置船员更新为已加入", "Card Workshop Preview Panel");
            AssertDepartureReturnCaption(departureBack, "返回出航界面");
            yield return ClickShipyardControl("Return To Port Hub");
            AssertDepartureReturnSelection(controller, route, destination);
            Assert.That(GameObject.Find("Card Workshop Preview Panel"), Is.Null);
            Assert.That(controller.Session.GetBonusBattleCards(NavalBattleStage.Naval), Has.Member(index));
            AssertSpecialSourceNavigationProgress(controller, decks, saved, gold);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DeckBuilderSpecial_ComponentConfigurationReturnsToSameStagePageAndUpdatesMembership()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            ComponentArtEntry component = new ComponentArtCatalog().Entries.First(item => item.Slot == ComponentSlot.Bow &&
                item.Rarity == ComponentRarity.Rare && CoreSpecialCardCatalog.ForComponent(item.Id).Card.IsAllowedInStage(NavalBattleStage.Naval));
            OwnedShipComponent owned = controller.GrantOwnedComponent(component);
            CoreSpecialCardDefinition source = CoreSpecialCardCatalog.ForComponent(component.Id);
            int index = DeckBuilderIndex(source.Card.Id);
            AcquireSpecialCardSecondPageFixture(controller, index, NavalBattleStage.Naval);
            yield return ClickShipyardControl("Port Hotspot CardWorkshop");
            yield return ClickShipyardControl("Edit Stage Deck Naval");
            yield return ClickShipyardControl("Open Special Card Collection");
            yield return RevealWorkshopSpecialCard(index);
            yield return ClickShipyardControl("Select Special Card " + index);
            AssertSpecialCardMembership(index, false);
            Assert.That(ActiveButton("Configure Special Card Source " + index).GetComponentInChildren<Text>().text, Is.EqualTo("替换部件"));
            string page = GameObject.Find("Upgrade Collection Page").GetComponent<Text>().text;
            Assert.That(ActiveButton("Previous Upgrade Cards").interactable, Is.True);
            // Establish the same durable profile shape used by real equipment saves.
            // This keeps strict value comparisons meaningful for initially absent legacy keys.
            typeof(CoreGameplayDemoController).GetMethod("SaveProgress", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
            Dictionary<string, string> saved = SpecialSourceNavigationProgress();
            Dictionary<NavalBattleStage, int[]> decks = SpecialSourceNavigationDecks(controller);
            decimal gold = controller.Session.ProfileGold;
            int ship = controller.Session.SelectedShipIndex;
            int inventoryCount = controller.OwnedComponents.Count;
            yield return new WaitForSecondsRealtime(.7f);
            NavalBattleVisualCapture.Capture(controller, "DeckBuilderSpecial_Component_01_Idle.png",
                "未安装的已拥有部件对应卡显示替换部件入口", "Card Workshop Preview Panel");

            yield return ClickShipyardControl("Configure Special Card Source " + index);
            Assert.That(GameObject.Find("Shipyard Equipment Page"), Is.Not.Null);
            Assert.That(owned.ShipIndex, Is.EqualTo(-1), "Opening configuration does not equip a part automatically.");
            yield return ClickShipyardControl("Return To Port Hub");
            AssertReturnedWorkshopSpecialCard(controller, index, NavalBattleStage.Naval, page, false);
            yield return ClickShipyardControl("Configure Special Card Source " + index);
            yield return ClickShipyardControl("Shipyard Equipment Owned " + owned.InstanceId);
            yield return ClickShipyardControl("Shipyard Equip Component");
            Assert.That(controller.InstalledShipComponent(ship, component.Slot).InstanceId, Is.EqualTo(owned.InstanceId));
            yield return ClickShipyardControl("Return To Port Hub");
            AssertReturnedWorkshopSpecialCard(controller, index, NavalBattleStage.Naval, page, true);
            Assert.That(controller.Session.GetBonusBattleCards(NavalBattleStage.Naval), Has.Member(index));
            Assert.That(controller.OwnedComponents.Count, Is.EqualTo(inventoryCount));
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(ship));
            AssertSpecialSourceNavigationProgress(controller, decks, saved, gold);
            Assert.That(controller.Session.GetSpecialCardExperience(source.Card.Id), Is.Zero);
            yield return new WaitForSecondsRealtime(.7f);
            NavalBattleVisualCapture.Capture(controller, "DeckBuilderSpecial_Component_02_Joined.png",
                "真实装备部件后返回原阶段原页，对应专属牌显示已加入", "Card Workshop Preview Panel");
            LogAssert.NoUnexpectedReceived();
        }

        private static void AcquireSpecialCardSecondPageFixture(CoreGameplayDemoController controller, int target, NavalBattleStage stage)
        {
            int[] earlier = CoreSpecialCardCatalog.All.Select(source => DeckBuilderIndex(source.Card.Id))
                .Where(index => CoreBattleCardNumbering.CompareLibraryIndices(index, target) < 0 &&
                    controller.Session.GetBattleCard(index, stage).IsAllowedInStage(stage))
                .OrderBy(index => CoreBattleCardNumbering.Number(CoreVoyageSession.CardLibrary[index])).Take(8).ToArray();
            Assert.That(earlier, Has.Length.EqualTo(8));
            controller.Session.AcquireSpecialCards(earlier.Concat(new[] { target }).Select(index => controller.Session.GetBattleCard(index).Id).ToArray());
        }

        private static IEnumerator RevealWorkshopSpecialCard(int index)
        {
            while (ActiveButton("Select Special Card " + index) == null)
            {
                Assert.That(ActiveButton("Next Upgrade Cards").interactable, Is.True, "The acquired special card must be reachable.");
                yield return ClickShipyardControl("Next Upgrade Cards");
            }
        }

        private static Dictionary<NavalBattleStage, int[]> SpecialSourceNavigationDecks(CoreGameplayDemoController controller) =>
            new[] { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding }
                .ToDictionary(stage => stage, stage => controller.Session.GetBattleDeck(stage).ToArray());

        private static Dictionary<string, string> SpecialSourceNavigationProgress() => DeckBuilderSavedProgress()
            .Where(pair => pair.Key != CoreGameplayDemoController.AcquiredSpecialCardsKey).ToDictionary(pair => pair.Key, pair => pair.Value);

        private static void AssertSpecialSourceNavigationProgress(CoreGameplayDemoController controller,
            Dictionary<NavalBattleStage, int[]> decks, Dictionary<string, string> saved, decimal gold)
        {
            foreach (var deck in decks) CollectionAssert.AreEqual(deck.Value, controller.Session.GetBattleDeck(deck.Key));
            CollectionAssert.AreEquivalent(saved, SpecialSourceNavigationProgress(), "Source configuration preserves base decks, gold and training progress.");
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
        }

        private static void AssertReturnedWorkshopSpecialCard(CoreGameplayDemoController controller, int index,
            NavalBattleStage stage, string page, bool joined)
        {
            Assert.That(GameObject.Find("Card Workshop Preview Panel"), Is.Not.Null);
            Assert.That(GameObject.Find("Deck Builder Panel"), Is.Null);
            Assert.That(GameObject.Find("Upgrade Collection Page").GetComponent<Text>().text, Is.EqualTo(page));
            Assert.That((NavalBattleStage)typeof(CoreGameplayDemoController).GetField("selectedBattleDeckStage", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(controller), Is.EqualTo(stage));
            Assert.That((bool)typeof(CoreGameplayDemoController).GetField("cardWorkshopDeckBuilderOpen", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(controller), Is.True);
            int[] visible = GameObject.Find("Workshop Preview Collection").GetComponentsInChildren<Button>()
                .Where(button => button.name.StartsWith("Select Special Card "))
                .Select(button => int.Parse(button.name.Substring("Select Special Card ".Length))).ToArray();
            Assert.That(visible.All(card => controller.Session.GetBattleCard(card).IsAllowedInStage(stage)), Is.True);
            AssertSpecialCollectionPage(controller, visible, index, page.Replace(" ", string.Empty));
            AssertSpecialCardMembership(index, joined);
        }

        private static void AssertSpecialCardMembership(int index, bool joined)
        {
            Assert.That(GameObject.Find("Special Card Source " + index), Is.Null);
            Assert.That(GameObject.Find("Special Card Progress " + index), Is.Null);
            Assert.That(ActiveButton("Select Special Card " + index).GetComponent<DeckBuilderCardDrag>(), Is.Null);
            GameObject status = GameObject.Find("Special Card Deck Status " + index);
            Button configure = ActiveButton("Configure Special Card Source " + index);
            if (joined)
            {
                Assert.That(status, Is.Not.Null);
                Assert.That(status.GetComponent<Text>().text, Is.EqualTo("已加入"));
                Assert.That(configure, Is.Null);
            }
            else
            {
                Assert.That(status, Is.Null);
                Assert.That(configure, Is.Not.Null);
                CoreSpecialCardDefinition source = CoreSpecialCardCatalog.FindByCardId(CoreVoyageSession.CardLibrary[index].Id);
                Assert.That(configure.GetComponentInChildren<Text>().text,
                    Is.EqualTo(source.SourceKind == CoreSpecialCardSourceKind.Crew ? "配置船员" : "替换部件"));
            }
        }

        private static IEnumerator ReturnFromWorkshopForgeToSelectedDeck()
        {
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            NavalBattleStage stage = (NavalBattleStage)typeof(CoreGameplayDemoController)
                .GetField("selectedBattleDeckStage", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            ActiveButton("Back From Card Upgrades").onClick.Invoke();
            yield return null;
            ActiveButton("Edit Stage Deck " + stage).onClick.Invoke();
            yield return null;
        }

        [UnityTest]
        public IEnumerator WorkshopPreview_SeparatesPaidBaseCardsFromVoyageSpecialCollection()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            CrewVariantDefinition navigator = CrewNarrativeCatalog.ForPortAndProfession("灰鸥港", 0);
            CrewVariantDefinition doctor = CrewNarrativeCatalog.ForPortAndProfession("灰鸥港", 2);
            CrewVariantDefinition cook = CrewNarrativeCatalog.ForPortAndProfession("灰鸥港", 4);
            CoreSpecialCardDefinition cookCard = CoreSpecialCardCatalog.ForCrew(cook.Id);
            PlayerPrefs.SetString(CoreGameplayDemoController.SpecialCardExperienceKey, cookCard.Card.Id + "=839");
            foreach (CrewVariantDefinition crew in new[] { navigator, doctor, cook })
            {
                PlayerPrefs.SetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(crew.Id), 1);
                PlayerPrefs.SetString(CoreGameplayDemoController.ActiveCrewVariantKey(crew.ProfessionIndex), crew.Id);
            }
            ComponentArtCatalog catalog = new ComponentArtCatalog();
            ComponentArtEntry rare = catalog.Entries.First(item => item.Slot == ComponentSlot.Bow && item.Rarity == ComponentRarity.Rare);
            ComponentArtEntry green = catalog.Entries.First(item => item.Slot == ComponentSlot.Sail && item.Rarity == ComponentRarity.Uncommon);
            PlayerPrefs.SetString(CoreGameplayDemoController.ComponentLoadoutKey(ComponentSlot.Bow), rare.Id);
            PlayerPrefs.SetString(CoreGameplayDemoController.ComponentLoadoutKey(ComponentSlot.Sail), green.Id);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreSpecialCardDefinition crewCard = CoreSpecialCardCatalog.ForCrew(navigator.Id);
            CoreSpecialCardDefinition componentCard = CoreSpecialCardCatalog.ForComponent(rare.Id);
            CoreSpecialCardDefinition doctorCard = CoreSpecialCardCatalog.ForCrew(doctor.Id);
            Assert.That(doctorCard, Is.Not.Null);
            Assert.That(controller.Session.AcquiredSpecialCardIds, Has.Member(doctorCard.Card.Id),
                "Existing recruited doctors receive their new card without purchasing or recruiting again.");
            int doctorIndex = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .First(i => CoreVoyageSession.CardLibrary[i].Id == doctorCard.Card.Id);
            Assert.That(controller.Session.GetBonusBattleCards(NavalBattleStage.Boarding), Has.Member(doctorIndex));
            Assert.That(controller.Session.GetBonusBattleCards(NavalBattleStage.Preparation), Has.No.Member(doctorIndex));
            Assert.That(controller.Session.GetBonusBattleCards(NavalBattleStage.Naval), Has.No.Member(doctorIndex));
            int cookIndex = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .First(i => CoreVoyageSession.CardLibrary[i].Id == cookCard.Card.Id);
            Assert.That(controller.Session.AcquiredSpecialCardIds, Has.Member(cookCard.Card.Id),
                "Previously recruited Pepe receives his support card without another recruitment fee.");
            foreach (NavalBattleStage stage in System.Enum.GetValues(typeof(NavalBattleStage)))
                Assert.That(controller.Session.GetBonusBattleCards(stage), Has.Member(cookIndex));
            Assert.That(controller.Session.GetCardUpgradeLevel(cookCard.Card.Id),
                Is.EqualTo(Mathf.Min(2, controller.Session.GetCardMaximumUpgradeLevel(cookCard.Card.Id))));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.AcquiredSpecialCardsKey), Does.Contain(cookCard.Card.Id));
            Assert.That(CoreSpecialCardCatalog.ForComponent(green.Id), Is.Null);
            Assert.That(controller.Session.AcquiredSpecialCardIds, Has.Member(crewCard.Card.Id));
            Assert.That(controller.Session.AcquiredSpecialCardIds, Has.Member(componentCard.Card.Id));
            decimal gold = controller.Session.ProfileGold;
            ActiveButton("Port Hotspot CardWorkshop").onClick.Invoke();
            yield return null;
            NavalBattleVisualCapture.Capture(controller, "01_Card_Workshop_Preview.png", "直接进入两行四列卡牌预览与三阶段编辑入口", "Card Workshop Preview Panel");
            NavalBattleStage[] stages = { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding };
            for (int chest = 0; chest < 3; chest++)
            {
                ActiveButton("Workshop Preview Stage " + stages[chest]).onClick.Invoke();
                yield return null;
                Assert.That(GameObject.Find("Deck Builder Panel"), Is.Null, "Filtering only changes the preview catalog.");
                ActiveButton("Open Battle Card Upgrades").onClick.Invoke();
                yield return null;
                Assert.That(GameObject.Find("Battle Card Upgrade Panel"), Is.Not.Null);
                foreach (Button button in GameObject.Find("Workshop Preview Collection").GetComponentsInChildren<Button>())
                    if (button.name.StartsWith("Select Upgrade Card "))
                    {
                        int index = int.Parse(button.name.Substring("Select Upgrade Card ".Length));
                        CoreBattleCard card = controller.Session.GetBattleCard(index);
                        Assert.That(card.IsAllowedInStage(stages[chest]), Is.True);
                        Assert.That(card.RequiredCrew, Is.Empty);
                        Assert.That(card.RequiredComponent, Is.Empty);
                    }
                ActiveButton("Back From Card Upgrades").onClick.Invoke();
                yield return null;
            }
            ActiveButton("Open Special Card Collection").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Special Card Collection Panel"), Is.Null);
            Assert.That(GameObject.Find("Card Workshop Preview Panel"), Is.Not.Null);
            Assert.That(GameObject.Find("Confirm Battle Card Upgrade"), Is.Null);
            Assert.That(ActiveButton("Open Battle Card Upgrades").interactable, Is.False);
            ActiveButton("Open Battle Card Upgrades").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Battle Card Upgrade Panel"), Is.Null);
            int specialIndex = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).First(i => CoreVoyageSession.CardLibrary[i].Id == crewCard.Card.Id);
            int componentIndex = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).First(i => CoreVoyageSession.CardLibrary[i].Id == componentCard.Card.Id);
            ActiveButton("Select Special Card " + specialIndex).onClick.Invoke();
            yield return null;
            AssertSpecialCardMembership(specialIndex, true);
            Assert.That(GameObject.Find("Special Card Source " + specialIndex), Is.Null);
            Assert.That(GameObject.Find("Special Card Progress " + specialIndex), Is.Null);
            Assert.That(controller.Session.TryUpgradeBattleCard(specialIndex, out _), Is.False);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            NavalBattleVisualCapture.Capture(controller, "02_Special_Card_Collection.png", "工坊内查看特殊卡与已加入状态，强化入口覆盖禁用标记", "Card Workshop Preview Panel");
            ActiveButton("Select Special Card " + doctorIndex).onClick.Invoke();
            yield return null;
            AssertSpecialCardMembership(doctorIndex, true);
            NavalBattleVisualCapture.Capture(controller, "03_Doctor_Boarding_Card.png", "船医莫尔的接舷治疗专属牌在同一工坊内选中", "Card Workshop Preview Panel");
            int[] firstPage = new[] { specialIndex, componentIndex, doctorIndex, cookIndex }.OrderBy(index => index).ToArray();
            AssertSpecialCollectionPage(controller, firstPage, doctorIndex, "1/1");
            ActiveButton("Select Special Card " + cookIndex).onClick.Invoke();
            yield return null;
            AssertSpecialCardMembership(cookIndex, true);
            Assert.That(GameObject.Find("Special Card Progress " + cookIndex), Is.Null);
            AssertSpecialCollectionPage(controller, firstPage, cookIndex, "1/1");
            Assert.That(ActiveButton("Previous Upgrade Cards").interactable, Is.False);
            Assert.That(ActiveButton("Next Upgrade Cards").interactable, Is.False);
            RectTransform paper = GameObject.Find("Workshop Preview Parchment").GetComponent<RectTransform>();
            foreach (Button button in paper.GetComponentsInChildren<Button>())
            {
                Vector3[] corners = new Vector3[4];
                button.GetComponent<RectTransform>().GetWorldCorners(corners);
                foreach (Vector3 corner in corners) Assert.That(paper.rect.Contains(paper.InverseTransformPoint(corner)), Is.True, button.name);
            }
            ActiveButton("Return To Port Hub").onClick.Invoke();
            yield return null;
            ActiveButton("Port Hotspot Voyage").onClick.Invoke();
            yield return null;
            ActiveButton("Start Voyage").onClick.Invoke();
            yield return null;
            Assert.That(controller.Session.GetSpecialCardExperience(crewCard.Card.Id), Is.Zero, "Departure alone cannot grant XP.");
            string xpBeforeVoyage = PlayerPrefs.GetString(CoreGameplayDemoController.SpecialCardExperienceKey, string.Empty);
            int waterBeforeVoyage = controller.Session.Cargo.Water;
            int foodBeforeVoyage = controller.Session.Cargo.Food;
            PrepareShortExperienceVoyage(controller.Session);
            ActiveButton("Execute Sail").onClick.Invoke();
            yield return null;
            int xp = controller.Session.GetSpecialCardExperience(crewCard.Card.Id);
            Assert.That(xp, Is.GreaterThan(0));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.SpecialCardExperienceKey, string.Empty), Is.EqualTo(xpBeforeVoyage),
                "An unsettled voyage must not persist XP separately from its resource consumption.");
            typeof(CoreGameplayDemoController).GetMethod("SaveSpecialCardProgress", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, new object[] { true });
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.SpecialCardExperienceKey, string.Empty), Is.EqualTo(xpBeforeVoyage),
                "The save helper must also guard future accidental calls at sea.");
            Assert.That(controller.Session.GetSpecialCardExperience(componentCard.Card.Id), Is.EqualTo(xp));
            Assert.That(controller.Session.GetSpecialCardExperience(doctorCard.Card.Id), Is.EqualTo(xp));
            Assert.That(controller.Session.GetSpecialCardExperience(cookCard.Card.Id), Is.EqualTo(839 + xp));
            Assert.That(controller.Session.GetCardUpgradeLevel(cookCard.Card.Id), Is.EqualTo(controller.Session.GetCardMaximumUpgradeLevel(cookCard.Card.Id)));
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session.GetSpecialCardExperience(crewCard.Card.Id), Is.Zero,
                "Reloading an unfinished voyage rolls back its XP together with its supplies.");
            Assert.That(controller.Session.Cargo.Water, Is.EqualTo(waterBeforeVoyage));
            Assert.That(controller.Session.Cargo.Food, Is.EqualTo(foodBeforeVoyage));
            Assert.That(controller.Session.AcquiredSpecialCardIds, Has.Member(componentCard.Card.Id));
            Assert.That(controller.Session.GetSpecialCardExperience(cookCard.Card.Id), Is.EqualTo(839),
                "Reloading an unsettled voyage restores Pepe's committed training too.");

            ActiveButton("Port Hotspot Voyage").onClick.Invoke();
            yield return null;
            ActiveButton("Start Voyage").onClick.Invoke();
            yield return null;
            PrepareShortExperienceVoyage(controller.Session);
            ActiveButton("Execute Sail").onClick.Invoke();
            yield return null;
            int earnedThisVoyage = controller.Session.GetSpecialCardExperience(crewCard.Card.Id);
            Assert.That(earnedThisVoyage, Is.GreaterThan(0));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.SpecialCardExperienceKey, string.Empty), Is.EqualTo(xpBeforeVoyage));
            yield return WaitForNavigationMotionCompletion(controller);
            ActiveButton("Safe Return").onClick.Invoke();
            yield return null;
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            typeof(CoreGameplayDemoController).GetMethod("SaveSpecialCardProgress", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, new object[] { true });
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.SpecialCardExperienceKey, string.Empty), Is.EqualTo(xpBeforeVoyage),
                "The animated settlement is not yet a committed return to port.");
            yield return SettlementChestTestActions.ShowOverview();
            Button settle = ActiveButton("Return To Port");
            settle.onClick.Invoke();
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            int settledWater = controller.Session.Cargo.Water;
            int settledFood = controller.Session.Cargo.Food;
            Assert.That(settledWater, Is.LessThan(waterBeforeVoyage));
            Assert.That(settledFood, Is.LessThan(foodBeforeVoyage));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.SpecialCardExperienceKey), Does.Contain(crewCard.Card.Id + "=" + earnedThisVoyage));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.CargoKey("Water")), Is.EqualTo(settledWater));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.CargoKey("Food")), Is.EqualTo(settledFood));
            settle.onClick.Invoke();
            Assert.That(controller.Session.GetSpecialCardExperience(crewCard.Card.Id), Is.EqualTo(earnedThisVoyage), "A stale confirm cannot award XP twice.");
            Assert.That(controller.Session.Cargo.Water, Is.EqualTo(settledWater));
            Assert.That(controller.Session.Cargo.Food, Is.EqualTo(settledFood));
            yield return null;
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session.GetSpecialCardExperience(crewCard.Card.Id), Is.EqualTo(earnedThisVoyage));
            Assert.That(controller.Session.GetSpecialCardExperience(componentCard.Card.Id), Is.EqualTo(earnedThisVoyage));
            Assert.That(controller.Session.GetSpecialCardExperience(doctorCard.Card.Id), Is.EqualTo(earnedThisVoyage));
            Assert.That(controller.Session.GetSpecialCardExperience(cookCard.Card.Id), Is.EqualTo(839 + earnedThisVoyage));
            Assert.That(controller.Session.GetCardUpgradeLevel(cookCard.Card.Id), Is.EqualTo(controller.Session.GetCardMaximumUpgradeLevel(cookCard.Card.Id)));
            Assert.That(controller.Session.AcquiredSpecialCardIds, Has.Member(cookCard.Card.Id));
            Assert.That(controller.Session.Cargo.Water, Is.EqualTo(settledWater));
            Assert.That(controller.Session.Cargo.Food, Is.EqualTo(settledFood));
            typeof(CoreGameplayDemoController).GetMethod("RestoreSpecialCardProgress", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
            Assert.That(controller.Session.GetSpecialCardExperience(crewCard.Card.Id), Is.EqualTo(earnedThisVoyage), "Restoring the same committed snapshot is idempotent.");
        }

        private static void AssertSpecialCollectionPage(CoreGameplayDemoController controller, int[] visibleIndices, int selectedIndex, string page)
        {
            GameObject panel = GameObject.Find("Workshop Preview Collection");
            Button[] choices = panel.GetComponentsInChildren<Button>()
                .Where(button => button.name.StartsWith("Select Special Card ")).ToArray();
            Assert.That(choices.Length, Is.LessThanOrEqualTo(8), "Each collection page fits two rows of four cards.");
            CollectionAssert.AreEquivalent(visibleIndices.Select(index => "Select Special Card " + index).ToArray(),
                choices.Select(button => button.name).ToArray());
            Outline[] selection = panel.GetComponentsInChildren<CardSchoolFrameGraphic>()
                .Select(frame => frame.GetComponent<Outline>()).Where(outline => outline != null).ToArray();
            Assert.That(selection, Has.Length.EqualTo(1));
            Assert.That(selection[0].GetComponentInParent<Button>().name, Is.EqualTo("Select Special Card " + selectedIndex));
            Assert.That(GameObject.Find("Special Card Name " + selectedIndex).GetComponent<Text>().text,
                Is.EqualTo(CardFaceLayout.DisplayName(controller.Session.GetBattleCard(selectedIndex))));
            Assert.That(GameObject.Find("Upgrade Collection Page").GetComponent<Text>().text.Replace(" ", string.Empty), Does.Contain(page));
        }

        private static void PrepareShortExperienceVoyage(CoreVoyageSession session)
        {
            // This fixture audits resource/XP commits, not random encounters or boss outcomes.
            foreach (CoreMapNode node in session.Map.Nodes) node.Visited = true;
            typeof(CoreVoyageSession).GetField("randomAmbushOccurred", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, true);
            session.SetDistance(30);
            foreach (SailingDirection heading in System.Enum.GetValues(typeof(SailingDirection)))
            {
                session.SetHeading(heading);
                SailingForecast forecast = session.GetForecast();
                if (forecast != null && !forecast.Blocked && forecast.ActualDistanceNm >= 1f) return;
            }
            Assert.Fail("Expected a legal short test voyage from the departure port.");
        }
    }
}
