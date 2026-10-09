using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
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
        [UnityTest]
        public IEnumerator RareResourceEvents_FourthChoicesScrollAndSpendOnlyCarriedStock()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            // Isolated UI fixtures: enter each authored event, then operate its real button.
            // This does not claim to earn these supplies through a campaign.
            session.RestoreTreasureProgress(new CoreTreasureProgress {
                pirateExperience = 1400, selectedMapId = "rare-event-ui",
                maps = new[] { new CoreTreasureMapSave { id = "rare-event-ui", seed = "RARE-EVENT-UI", rankId = "weathered" } },
                gems = 1, ancientMaterials = 3, relics = 1, blackPearls = 1, ancientCompasses = 1, lanterns = 1
            });
            session.Cargo.Water = session.Cargo.Food = 150;
            session.Cargo.Wood = session.Cargo.Iron = session.Cargo.Cloth = session.Cargo.Beer = 0;
            session.Cargo.Powder = session.Cargo.Cannonballs = session.Cargo.Medicine = 0;
            Assert.That(session.TryStartTreasureVoyage(out string reason), Is.True, reason);
            string[] ids = { "treasure_rival_sloop", "treasure_reef_snare", "treasure_drowned_shrine",
                "treasure_ghost_fleet", "treasure_coral_maze", "treasure_clue_fogbell" };
            string[] keys = { "gem", "ancientmaterial", "relic", "blackpearl", "ancientcompass", "lantern" };
            var events = CoreTreasureHuntCatalog.CreateEncounters(CoreTreasureHuntCatalog.FindRank("weathered"));
            for (int i = 0; i < ids.Length; i++)
            {
                Assert.That(session.PortWarehouse.Restore("v2;" + session.CurrentPort + "," + keys[i] + ",20"), Is.True);
                // This isolated fixture enters authored events without map
                // placement. The chain-reef option still requires a real reef
                // context; leaving Any would test it at the departure water.
                var node = new CoreMapNode { Id = 990 + i, Position = session.Position,
                    TerrainConstraint = ids[i] == "treasure_reef_snare" ? EventTerrainConstraint.Reef : EventTerrainConstraint.Any,
                    Encounter = events.Single(e => e.Id == ids[i]) };
                session.Map.Nodes.Add(node);
                typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, node);
                typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session, node.Encounter);
                typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, CoreDemoPhase.Encounter);
                RenderRareEventFixture(controller);
                yield return null;
                if (i == 0) yield return DismissTreasureStoryIfPresent(controller);
                yield return WaitForEncounterDossierPresentationCompletion();
                ScrollRect scroll = GameObject.Find("Encounter Decision Viewport").GetComponent<ScrollRect>();
                Assert.That(scroll.vertical, Is.True);
                scroll.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0, -20) });
                yield return null;
                Button spend = ActiveButton("Encounter Choice 3");
                Assert.That(spend.interactable, Is.True, ids[i] + ": " + session.ChoiceUnavailableReason(3));
                Vector3 center = spend.GetComponent<RectTransform>().TransformPoint(Vector3.zero);
                Assert.That(RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport,
                    RectTransformUtility.WorldToScreenPoint(null, center)), Is.True, "Fourth option must be reachable in the viewport.");
                NavalBattleVisualCapture.Capture(controller, "RareEvent_" + keys[i] + ".png",
                    "珍稀物品可选事件：" + node.Encounter.Choices[3].Detail, "Encounter Card");
                ClickAcceptanceButton("Encounter Choice 3");
                yield return null;
                Assert.That(session.GetWarehouseCargoAmount(keys[i]), Is.Zero);
                Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, keys[i]), Is.EqualTo(20));
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
                // Reopen a distinct copy with only shore stock: display the shortage and reject the actual callback.
                node.Visited = false;
                typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, node);
                typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session, node.Encounter);
                typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, CoreDemoPhase.Encounter);
                RenderRareEventFixture(controller);
                yield return null;
                spend = ActiveButton("Encounter Choice 3");
                Assert.That(spend.interactable, Is.False);
                Assert.That(string.Join(" ", spend.GetComponentsInChildren<Text>().Select(t => t.text)), Does.Contain("随船"));
                double hours = session.TotalHours;
                spend.onClick.Invoke();
                yield return null;
                Assert.That(session.TotalHours, Is.EqualTo(hours));
                Assert.That(session.GetWarehouseCargoAmount(keys[i]), Is.Zero);
                Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, keys[i]), Is.EqualTo(20));
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static void RenderRareEventFixture(CoreGameplayDemoController controller) =>
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
    }
}
