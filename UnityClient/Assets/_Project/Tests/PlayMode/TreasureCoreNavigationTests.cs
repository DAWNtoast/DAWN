using System.Collections;
using System.Linq;
using NUnit.Framework;
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
        public IEnumerator TreasureCore_ContinueExploringLetsNearbyWaterEscapeAndMarkerRevisit()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession voyage = controller.Session;
            // Isolated UI regression fixture. The full acceptance playthrough
            // separately earns every fragment and crafts its map through UI.
            voyage.RestoreTreasureProgress(new CoreTreasureProgress {
                pirateExperience = 100, selectedMapId = "navigation-test",
                maps = new[] { new CoreTreasureMapSave { id = "navigation-test", seed = "CORE-WATER-CLICK", rankId = "weathered" } }
            });
            voyage.Cargo.Water = 200; voyage.Cargo.Food = 200;
            ActiveButton("Port Hotspot Voyage").onClick.Invoke();
            yield return null;
            ActiveButton("Start Voyage").onClick.Invoke();
            yield return ExerciseTreasureStoryPageRetirement(controller);
            CoreMapNode core = voyage.Map.Nodes.Single(n => n.Encounter.Id == CoreTreasureHuntCatalog.CoreEncounterId);
            voyage.Map.Nodes.Clear(); voyage.Map.Nodes.Add(core);
            System.Array.Clear(voyage.Map.TerrainCells, 0, voyage.Map.CellCount);
            core.Position = new VoyagePoint(50f, 32f);
            voyage.Map.Destination = core.Position;
            SetDestinationTestPosition(voyage, core.Position);
            SetDestinationTestField(voyage, "randomAmbushOccurred", true);
            ActiveButton("Heading East").onClick.Invoke();
            yield return null;
            Canvas canvas = GameObject.Find("Navigation Chart").GetComponentInParent<Canvas>().rootCanvas;
            Assert.That(canvas.GetComponent<CanvasScaler>().screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand));
            RectTransform surface = GameObject.Find("Map Course Input").GetComponent<RectTransform>();
            var corners = new Vector3[4];
            surface.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, corner);
                Assert.That(screen.x, Is.InRange(0f, (float)Screen.width));
                Assert.That(screen.y, Is.InRange(0f, (float)Screen.height));
            }
            ClickAcceptanceButton("Event Node " + core.Id);
            yield return null;
            Assert.That(voyage.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            Assert.That(voyage.IsChoiceAvailable(0), Is.False);
            ClickAcceptanceButton("Encounter Choice 3");
            yield return null;
            Assert.That(voyage.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(core.Skipped, Is.True);
            // Outside the marker but inside the 58px automatic snap range.
            yield return ClickDestinationTestGraphic(GameObject.Find("Map Course Input"), new Vector2(40f, 0f));
            Assert.That(voyage.Phase, Is.EqualTo(CoreDemoPhase.Sailing), "Free water must not snap back to the deferred core.");
            Assert.That(voyage.HasDirectCourseTarget, Is.True);
            Assert.That(voyage.DirectCourseTarget.X, Is.GreaterThan(core.Position.X + 2f));
            ClickAcceptanceButton("Execute Sail");
            yield return WaitForNavigationMotionCompletion(controller);
            Assert.That(voyage.Position.X, Is.GreaterThan(core.Position.X + 2f));
            Assert.That(voyage.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            ClickAcceptanceButton("Event Node " + core.Id);
            yield return null;
            Assert.That(core.Skipped, Is.False, "An explicit marker click must still allow a return visit.");
            ClickAcceptanceButton("Execute Sail");
            yield return WaitForNavigationMotionCompletion(controller);
            Assert.That(voyage.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            Assert.That(voyage.ActiveEncounter.Id, Is.EqualTo(CoreTreasureHuntCatalog.CoreEncounterId));
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator ExerciseTreasureStoryPageRetirement(CoreGameplayDemoController controller)
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            System.Reflection.MethodInfo render = typeof(CoreGameplayDemoController).GetMethod("Render", flags);
            System.Reflection.FieldInfo animation = typeof(CoreGameplayDemoController).GetField("treasureStoryPageTransition", flags);
            // Force a fresh first-page turn, then retire it synchronously before
            // its first yield can complete, regardless of the runner's frame rate.
            typeof(CoreGameplayDemoController).GetField("treasureStoryRenderedPage", flags).SetValue(controller, -1);
            render.Invoke(controller, null);
            RectTransform first = GameObject.Find("Treasure Story Comic Frame").GetComponent<RectTransform>();
            Button.ButtonClickedEvent retiredNext = ActiveButton("Treasure Story Next").onClick;
            Button.ButtonClickedEvent retiredSkip = ActiveButton("Treasure Story Skip").onClick;
            Assert.That(animation.GetValue(controller), Is.Not.Null);

            render.Invoke(controller, null);

            RectTransform replacement = GameObject.Find("Treasure Story Comic Frame").GetComponent<RectTransform>();
            Assert.That(replacement, Is.Not.SameAs(first));
            Assert.That(first.gameObject.activeInHierarchy, Is.False);
            Assert.That(animation.GetValue(controller), Is.Null, "Rebuilding the same page stops its retired animation without replaying it.");
            Assert.That(replacement.localScale, Is.EqualTo(Vector3.one));
            retiredNext.Invoke(); retiredSkip.Invoke();
            Assert.That(GameObject.Find("Treasure Story Comic Frame").GetComponent<RectTransform>(), Is.SameAs(replacement));
            Assert.That(GameObject.Find("Treasure Story Heading").GetComponent<Text>().text, Does.Contain("第 1 / 2 页"));
            yield return null;

            // Finish the final page immediately while its new turn is still
            // running. Closing the overlay must cancel that coroutine too.
            ActiveButton("Treasure Story Next").onClick.Invoke();
            Assert.That(animation.GetValue(controller), Is.Not.Null);
            ActiveButton("Treasure Story Next").onClick.Invoke();
            Assert.That(GameObject.Find("Treasure Story Overlay"), Is.Null);
            Assert.That(animation.GetValue(controller), Is.Null);
            yield return null;
            yield return new WaitForSecondsRealtime(0.5f);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
