using System.Collections;
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
        public IEnumerator WorkshopFourChests_SeparatePaidBaseCardsFromVoyageSpecialCollection()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            CrewVariantDefinition navigator = CrewNarrativeCatalog.ForPortAndProfession("灰鸥港", 0);
            CrewVariantDefinition doctor = CrewNarrativeCatalog.ForPortAndProfession("灰鸥港", 2);
            foreach (CrewVariantDefinition crew in new[] { navigator, doctor })
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
            Assert.That(CoreSpecialCardCatalog.ForComponent(green.Id), Is.Null);
            Assert.That(controller.Session.AcquiredSpecialCardIds, Has.Member(crewCard.Card.Id));
            Assert.That(controller.Session.AcquiredSpecialCardIds, Has.Member(componentCard.Card.Id));
            decimal gold = controller.Session.ProfileGold;
            ActiveButton("Port Hotspot CardWorkshop").onClick.Invoke();
            yield return null;
            NavalBattleVisualCapture.Capture(controller, "01_Four_Card_Chests.png", "三类基础卡强化与特殊卡收藏入口", "Card Workshop Chest Stage");
            NavalBattleStage[] stages = { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding };
            for (int chest = 0; chest < 3; chest++)
            {
                ActiveButton("Open Deck Chest " + chest).onClick.Invoke();
                yield return null;
                Assert.That(GameObject.Find("Battle Card Upgrade Panel"), Is.Not.Null);
                foreach (Button button in GameObject.Find("Battle Card Upgrade Panel").GetComponentsInChildren<Button>())
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
            ActiveButton("Open Deck Chest 3").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Special Card Collection Panel"), Is.Not.Null);
            Assert.That(GameObject.Find("Confirm Battle Card Upgrade"), Is.Null);
            int specialIndex = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).First(i => CoreVoyageSession.CardLibrary[i].Id == crewCard.Card.Id);
            ActiveButton("Select Special Card " + specialIndex).onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Special Card Detail Lore").GetComponent<Text>().text, Is.EqualTo(crewCard.Lore));
            Assert.That(GameObject.Find("Special Card Detail Experience").GetComponent<Text>().text, Does.Contain("0 / 120"));
            Assert.That(controller.Session.TryUpgradeBattleCard(specialIndex, out _), Is.False);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            NavalBattleVisualCapture.Capture(controller, "02_Special_Card_Collection.png", "具体人物的专属战术、故事与航程经验", "Special Card Collection Panel");
            ActiveButton("Select Special Card " + doctorIndex).onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Special Card Detail Lore").GetComponent<Text>().text, Is.EqualTo(doctorCard.Lore));
            NavalBattleVisualCapture.Capture(controller, "03_Doctor_Boarding_Card.png", "船医莫尔的接舷治疗专属牌", "Special Card Collection Panel");
            RectTransform paper = GameObject.Find("Special Card Collection Parchment").GetComponent<RectTransform>();
            foreach (Button button in GameObject.Find("Special Card Collection Panel").GetComponentsInChildren<Button>())
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
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session.GetSpecialCardExperience(crewCard.Card.Id), Is.Zero,
                "Reloading an unfinished voyage rolls back its XP together with its supplies.");
            Assert.That(controller.Session.Cargo.Water, Is.EqualTo(waterBeforeVoyage));
            Assert.That(controller.Session.Cargo.Food, Is.EqualTo(foodBeforeVoyage));
            Assert.That(controller.Session.AcquiredSpecialCardIds, Has.Member(componentCard.Card.Id));

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
            Assert.That(controller.Session.Cargo.Water, Is.EqualTo(settledWater));
            Assert.That(controller.Session.Cargo.Food, Is.EqualTo(settledFood));
            typeof(CoreGameplayDemoController).GetMethod("RestoreSpecialCardProgress", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
            Assert.That(controller.Session.GetSpecialCardExperience(crewCard.Card.Id), Is.EqualTo(earnedThisVoyage), "Restoring the same committed snapshot is idempotent.");
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
