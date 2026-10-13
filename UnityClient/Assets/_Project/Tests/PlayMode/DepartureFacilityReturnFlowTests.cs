using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator DepartureFacilityReturn_MarketAlwaysReturnsToPortAndRetainsSelectedVoyage()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return SelectDepartureReturnFixture(controller);
            string destination = controller.Session.SelectedDestinationPort;
            int route = controller.Session.SelectedRouteIndex;
            GameObject header = GameObject.Find("Port Facility Header");
            Button back = ActiveButton("Return To Port Hub");
            AssertDepartureReturnCaption(back, "返回港口");

            // Repeating the same detour exercises the retained header's label and
            // callback rebinding, including consuming the return context once.
            for (int visit = 0; visit < 2; visit++)
            {
                ClickAcceptanceButton("Adjust Voyage Supplies"); yield return null;
                Assert.That(GameObject.Find("Market Panel"), Is.Not.Null);
                Assert.That(GameObject.Find("Port Facility Header"), Is.SameAs(header));
                AssertDepartureReturnCaption(back, "返回港口");
                if (visit == 0)
                    NavalBattleVisualCapture.Capture(controller, "Departure_Market_Return.png", "从出航采购补给，市场页头固定返回港口", "Market Panel");
                ClickAcceptanceButton("Return To Port Hub"); yield return null;
                Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);
                Assert.That(GameObject.Find("Voyage Setup"), Is.Null);
                ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
                AssertDepartureReturnSelection(controller, route, destination);
                AssertDepartureReturnCaption(back, "返回港口");
            }

            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);
            ClickAcceptanceButton("Port Hotspot Market"); yield return null;
            Assert.That(GameObject.Find("Port Facility Header"), Is.SameAs(header));
            AssertDepartureReturnCaption(back, "返回港口");
            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);
            Assert.That(GameObject.Find("Voyage Setup"), Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator DepartureFacilityReturn_DeckOverviewAndNestedWorkshopKeepReturnOriginAndDeckEdits()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return SelectDepartureReturnFixture(controller);
            string destination = controller.Session.SelectedDestinationPort;
            int route = controller.Session.SelectedRouteIndex;
            Button back = ActiveButton("Return To Port Hub");

            ClickAcceptanceButton("Configure Voyage Battle Decks"); yield return null;
            Assert.That(GameObject.Find("Card Workshop Preview Panel"), Is.Not.Null);
            AssertDepartureReturnCaption(back, "返回出航界面");
            NavalBattleVisualCapture.Capture(controller, "Departure_Deck_Return.png", "从出航配置三阶段牌组，页头直接返回出航界面", "Card Workshop Preview Panel");
            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            AssertDepartureReturnSelection(controller, route, destination);

            ClickAcceptanceButton("Configure Voyage Battle Decks"); yield return null;
            ClickAcceptanceButton("Edit Stage Deck " + NavalBattleStage.Preparation); yield return null;
            Assert.That(GameObject.Find("Deck Builder Panel"), Is.Not.Null);
            ClickAcceptanceButton("Battle Deck Stage Tab " + NavalBattleStage.Naval); yield return null;
            int initialDeckCount = controller.Session.GetBattleDeck(NavalBattleStage.Naval).Count;
            Button addition = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .First(button => button.name.StartsWith("Add Deck Card ") &&
                    controller.Session.GetBattleDeck(NavalBattleStage.Naval).Count(index => index == int.Parse(button.name.Substring("Add Deck Card ".Length))) < 2);
            ClickAcceptanceButton(addition.name); yield return null;
            int[] editedDeck = controller.Session.GetBattleDeck(NavalBattleStage.Naval).ToArray();
            Assert.That(editedDeck.Length, Is.EqualTo(initialDeckCount + 1));
            AssertDepartureReturnCaption(back, "返回出航界面");
            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            AssertDepartureReturnSelection(controller, route, destination);
            CollectionAssert.AreEqual(editedDeck, controller.Session.GetBattleDeck(NavalBattleStage.Naval));

            ClickAcceptanceButton("Configure Voyage Battle Decks"); yield return null;
            ClickAcceptanceButton("Edit Stage Deck " + NavalBattleStage.Naval); yield return null;
            ClickAcceptanceButton("Open Special Card Collection"); yield return null;
            Assert.That(GameObject.Find("Card Workshop Preview Panel"), Is.Not.Null);
            Assert.That(GameObject.Find("Battle Card Upgrade Panel"), Is.Null);
            AssertDepartureReturnCaption(back, "返回出航界面");
            ClickAcceptanceButton("Workshop Preview Stage Naval"); yield return null;
            Assert.That(GameObject.Find("Deck Builder Panel"), Is.Not.Null);
            AssertDepartureReturnCaption(back, "返回出航界面");
            ClickAcceptanceButton("Back To Card Workshop Chests"); yield return null;
            Assert.That(GameObject.Find("Card Workshop Preview Panel"), Is.Not.Null);
            AssertDepartureReturnCaption(back, "返回出航界面");
            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            AssertDepartureReturnSelection(controller, route, destination);
            CollectionAssert.AreEqual(editedDeck, controller.Session.GetBattleDeck(NavalBattleStage.Naval));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.StageBattleDeckKey(NavalBattleStage.Naval)),
                Is.EqualTo(string.Join(",", editedDeck)));

            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            ClickAcceptanceButton("Port Hotspot CardWorkshop"); yield return null;
            AssertDepartureReturnCaption(back, "返回港口");
            ClickAcceptanceButton("Workshop Preview Stage Naval"); yield return null;
            ClickAcceptanceButton("Open Battle Card Upgrades"); yield return null;
            yield return ReturnFromWorkshopForgeToSelectedDeck();
            AssertDepartureReturnCaption(back, "返回港口");
            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator DepartureFacilityReturn_FacilityArrowsClearDepartureOriginBeforeRevisitingFacility()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return SelectDepartureReturnFixture(controller);
            Button back = ActiveButton("Return To Port Hub");
            string[] departureActions = { "Adjust Voyage Supplies", "Configure Voyage Battle Decks" };
            string[] panels = { "Market Panel", "Card Workshop Preview Panel" };
            for (int index = 0; index < departureActions.Length; index++)
            {
                if (index > 0) { ClickAcceptanceButton("Port Hotspot Voyage"); yield return null; }
                ClickAcceptanceButton(departureActions[index]); yield return null;
                Assert.That(GameObject.Find(panels[index]), Is.Not.Null);
                AssertDepartureReturnCaption(back, index == 0 ? "返回港口" : "返回出航界面");
                string section = controller.ActivePortSection;
                ClickAcceptanceButton("Next Port Facility"); yield return null;
                AssertDepartureReturnCaption(back, "返回港口");
                ClickAcceptanceButton("Previous Port Facility"); yield return null;
                Assert.That(controller.ActivePortSection, Is.EqualTo(section));
                AssertDepartureReturnCaption(back, "返回港口");
                ClickAcceptanceButton("Return To Port Hub"); yield return null;
                Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);
                Assert.That(GameObject.Find("Voyage Setup"), Is.Null);
            }
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator SelectDepartureReturnFixture(CoreGameplayDemoController controller)
        {
            // Arrange access to route 1; choose the actual route and destination
            // through pointer clicks so a reset to the default cannot pass.
            var progress = controller.Session.CaptureTreasureProgress();
            progress.pirateExperience = 1500;
            controller.Session.RestoreTreasureProgress(progress);
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            ClickAcceptanceButton("Route 1"); yield return null;
            int destination = Enumerable.Range(0, CoreVoyageSession.DestinationPorts.Count).Single(index =>
                CoreVoyageSession.DestinationPorts[index] == "铜钟港");
            ClickAcceptanceButton("Departure Destination " + destination); yield return null;
            Assert.That(controller.Session.SelectedRouteIndex, Is.EqualTo(1));
            Assert.That(controller.Session.SelectedDestinationPort, Is.EqualTo("铜钟港"));
        }

        private static void AssertDepartureReturnCaption(Button retainedBack, string caption)
        {
            Assert.That(ActiveButton("Return To Port Hub"), Is.SameAs(retainedBack));
            Assert.That(retainedBack.GetComponentInChildren<Text>().text, Is.EqualTo(caption));
        }

        private static void AssertDepartureReturnSelection(CoreGameplayDemoController controller, int route, string destination)
        {
            Assert.That(GameObject.Find("Voyage Setup"), Is.Not.Null);
            Assert.That(GameObject.Find("Port Hub"), Is.Null);
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(controller.Session.SelectedRouteIndex, Is.EqualTo(route));
            Assert.That(controller.Session.SelectedDestinationPort, Is.EqualTo(destination));
            string origin = controller.Session.CurrentPort;
            string expectedSummary = origin == "断锚港"
                ? "当前航线：" + origin + " → " + destination
                : "选择目标港口，当前航线：" + origin + "——" + destination;
            Assert.That(GameObject.Find("Departure Route Summary").GetComponent<Text>().text,
                Is.EqualTo(expectedSummary));
        }
    }
}
