using System.Collections;
using System.Linq;
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
        public IEnumerator EveryTreasureRank_EntersDirectlyIntoNavigationWithoutAComic()
        {
            foreach (string rank in new[] { "weathered", "gilded", "moonlit" })
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CoreVoyageSession voyage = controller.Session;
                voyage.RestoreTreasureProgress(new CoreTreasureProgress {
                    pirateExperience = CoreVoyageSession.PirateExperienceForLevel(5),
                    identifiedWeatheredFragments = rank == "weathered" ? 4 : 0,
                    identifiedGildedFragments = rank == "gilded" ? 4 : 0,
                    identifiedMoonlitFragments = rank == "moonlit" ? 4 : 0
                });
                string seed = "TREASURE-DIRECT-NAVIGATION-20261103-" + rank;
                Assert.That(voyage.TryCraftTreasureMapForRank(rank, seed, out string reason), Is.True, reason);
                Assert.That(voyage.TrySelectTreasureSeed(seed, out reason), Is.True, reason);
                Assert.That(voyage.SelectedTreasureMap.ContentVersion, Is.EqualTo(1));
                voyage.Cargo.Water = 200; voyage.Cargo.Food = 200;
                ActiveButton("Port Hotspot Voyage").onClick.Invoke();
                yield return null;
                ActiveButton("Start Voyage").onClick.Invoke();
                yield return null;
                Assert.That(voyage.ActiveTreasureMap.ContentVersion, Is.EqualTo(1));
                Assert.That(GameObject.Find("Treasure Story Overlay"), Is.Null);
                Assert.That(GameObject.Find("Treasure Story Comic Page"), Is.Null);
                Assert.That(voyage.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
                Assert.That(GameObject.Find("Navigation Chart"), Is.Not.Null);
                Assert.That(ActiveButton("Heading North").IsInteractable(), Is.True);
                double hours = voyage.TotalHours;
                ClickAcceptanceButton("Heading North");
                yield return null;
                Assert.That(voyage.Heading, Is.EqualTo(SailingDirection.North));
                Assert.That(voyage.TotalHours, Is.EqualTo(hours), "Selecting a heading does not spend travel time.");
                Assert.That(GameObject.Find("Treasure Story Overlay"), Is.Null,
                    "A navigation redraw cannot recreate the removed introduction.");
            }
            LogAssert.NoUnexpectedReceived();
        }

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
            yield return DismissTreasureStoryIfPresent(controller);
            CoreMapNode core = voyage.Map.Nodes.Single(n => n.Encounter.Id == CoreTreasureHuntCatalog.CoreEncounterId);
            voyage.Map.Nodes.Clear(); voyage.Map.Nodes.Add(core);
            System.Array.Clear(voyage.Map.TerrainCells, 0, voyage.Map.CellCount);
            core.Position = new VoyagePoint(voyage.Map.WorldWidth * .5f, voyage.Map.WorldHeight * .5f);
            voyage.Map.Destination = core.Position;
            SetDestinationTestPosition(voyage, core.Position);
            SetDestinationTestField(voyage, "randomAmbushOccurred", true);
            // This fixture replaced node indices and positions after the map was built.
            // Heading changes preserve the chart, so rebuild the fixture explicitly
            // before exercising the real marker callbacks and pointer navigation.
            typeof(CoreGameplayDemoController).GetMethod("Render",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(controller, null);
            yield return null;
            ActiveButton("Heading East").onClick.Invoke();
            yield return null;
            Canvas canvas = GameObject.Find("Navigation Chart").GetComponentInParent<Canvas>().rootCanvas;
            Assert.That(canvas.GetComponent<CanvasScaler>().screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand));
            RectTransform surface = GameObject.Find("Map Course Input").GetComponent<RectTransform>();
            RectTransform frame = GameObject.Find("Procedural Sea Chart Frame").GetComponent<RectTransform>();
            RectMask2D clip = frame.GetComponent<RectMask2D>();
            Assert.That(clip, Is.Not.Null, "The square-cell chart clips its panned logical map at the visible frame.");
            Assert.That(surface.IsChildOf(frame), Is.True, "Map input must remain inside the chart's clipping hierarchy.");
            Assert.That(surface.GetComponent<Image>().maskable, Is.True);
            RectTransform viewport = FixedGameViewport.Bounds(canvas);
            var corners = new Vector3[4];
            frame.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector2 local = viewport.InverseTransformPoint(corner);
                Assert.That(local.x, Is.InRange(viewport.rect.xMin - .01f, viewport.rect.xMax + .01f));
                Assert.That(local.y, Is.InRange(viewport.rect.yMin - .01f, viewport.rect.yMax + .01f));
            }
            ClickAcceptanceButton("Event Node " + core.Id);
            yield return null;
            Assert.That(voyage.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            Assert.That(voyage.IsChoiceAvailable(0), Is.False);
            int continueChoice = core.Encounter.Choices.ToList().FindIndex(choice => choice.Effect.PostponeTreasureCore);
            Assert.That(continueChoice, Is.GreaterThanOrEqualTo(0));
            yield return WaitForEncounterDossierPresentationCompletion();
            ScrollRect choices = GameObject.Find("Encounter Decision Viewport").GetComponent<ScrollRect>();
            Assert.That(choices.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            if (choices.vertical)
            {
                choices.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0f, -20f) });
                yield return null;
                Canvas.ForceUpdateCanvases();
            }
            ActiveButton("Encounter Choice " + continueChoice).GetComponent<RectTransform>().GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector2 local = choices.viewport.InverseTransformPoint(corner);
                Assert.That(local.x, Is.InRange(choices.viewport.rect.xMin - .5f, choices.viewport.rect.xMax + .5f));
                Assert.That(local.y, Is.InRange(choices.viewport.rect.yMin - .5f, choices.viewport.rect.yMax + .5f),
                    "The complete continue-exploring choice must be revealed before its real pointer click.");
            }
            ClickAcceptanceButton("Encounter Choice " + continueChoice);
            yield return null;
            Assert.That(voyage.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(core.Skipped, Is.True);
            // Outside the marker but inside the 58px automatic snap range.
            yield return ClickDestinationTestGraphic(GameObject.Find("Map Course Input"),
                DestinationTestMapPoint(voyage, core.Position) + new Vector2(40f, 0f));
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

    }
}
