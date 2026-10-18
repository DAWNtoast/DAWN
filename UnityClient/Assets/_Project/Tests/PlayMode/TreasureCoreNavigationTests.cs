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
            core.Position = new VoyagePoint(voyage.Map.WorldWidth * .5f, voyage.Map.WorldHeight * .5f);
            voyage.Map.Destination = core.Position;
            SetDestinationTestPosition(voyage, core.Position);
            SetDestinationTestField(voyage, "randomAmbushOccurred", true);
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

        private IEnumerator ExerciseTreasureStoryPageRetirement(CoreGameplayDemoController controller)
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            System.Reflection.MethodInfo render = typeof(CoreGameplayDemoController).GetMethod("Render", flags);
            System.Reflection.FieldInfo page = typeof(CoreGameplayDemoController).GetField("treasureStoryPage", flags);
            render.Invoke(controller, null);
            RawImage first = AssertTreasureStoryFullscreen();
            Button.ButtonClickedEvent retiredAdvance = ActiveButton("Treasure Story Advance").onClick;
            Assert.That(page.GetValue(controller), Is.EqualTo(0));
            ComicAutoAdvance firstTimer = GameObject.Find("Treasure Story Overlay").GetComponent<ComicAutoAdvance>();
            Assert.That(firstTimer, Is.Not.Null);
            Assert.That(firstTimer.RemainingSeconds, Is.InRange(11f, 12f));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(page.GetValue(controller), Is.EqualTo(0), "A page stays open before its twelve-second reading interval expires.");
            Assert.That(GameObject.Find("Treasure Story Comic Page").GetComponent<RawImage>(), Is.SameAs(first));

            firstTimer.RestoreRemainingSeconds(.6f);
            float beforeRender = firstTimer.RemainingSeconds;
            render.Invoke(controller, null);

            RawImage replacement = AssertTreasureStoryFullscreen();
            ComicAutoAdvance replacementTimer = GameObject.Find("Treasure Story Overlay").GetComponent<ComicAutoAdvance>();
            Assert.That(replacementTimer.RemainingSeconds, Is.EqualTo(beforeRender).Within(.01f),
                "Rebuilding the same page must retain its remaining reading time.");
            Assert.That(replacement, Is.Not.SameAs(first));
            Assert.That(first.gameObject.activeInHierarchy, Is.False);
            retiredAdvance.Invoke();
            Assert.That(page.GetValue(controller), Is.EqualTo(0), "Retired callbacks cannot advance a replacement page.");
            Assert.That(GameObject.Find("Treasure Story Comic Page").GetComponent<RawImage>(), Is.SameAs(replacement));
            yield return null;

            Button.ButtonClickedEvent firstPageAdvance = ActiveButton("Treasure Story Advance").onClick;
            ClickAcceptanceButton("Treasure Story Advance");
            RawImage final = AssertTreasureStoryFullscreen();
            Assert.That(page.GetValue(controller), Is.EqualTo(1), "One click advances exactly one image.");
            ComicAutoAdvance finalTimer = GameObject.Find("Treasure Story Overlay").GetComponent<ComicAutoAdvance>();
            Assert.That(finalTimer.RemainingSeconds, Is.EqualTo(12f).Within(.01f), "A real page change starts a full reading interval.");
            Assert.That(final.texture, Is.Not.SameAs(replacement.texture));
            firstPageAdvance.Invoke(); firstPageAdvance.Invoke(); retiredAdvance.Invoke();
            Assert.That(page.GetValue(controller), Is.EqualTo(1), "An old page callback cannot finish the current page.");
            Assert.That(GameObject.Find("Treasure Story Comic Page").GetComponent<RawImage>(), Is.SameAs(final));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(page.GetValue(controller), Is.EqualTo(1), "The final page also remains open before its timeout.");

            Button.ButtonClickedEvent finalPageAdvance = ActiveButton("Treasure Story Advance").onClick;
            finalTimer.RestoreRemainingSeconds(.08f);
            float deadline = Time.realtimeSinceStartup + 2f;
            while (GameObject.Find("Treasure Story Overlay") != null && Time.realtimeSinceStartup < deadline)
            {
                // Even frequent unrelated UI renders must not restart the clock
                // and prevent the last page from completing automatically.
                render.Invoke(controller, null);
                yield return null;
            }
            Assert.That(GameObject.Find("Treasure Story Overlay"), Is.Null);
            finalPageAdvance.Invoke(); firstPageAdvance.Invoke(); retiredAdvance.Invoke();
            Assert.That(GameObject.Find("Treasure Story Overlay"), Is.Null, "Retired callbacks must not reopen a completed story.");
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        private static RawImage AssertTreasureStoryFullscreen()
        {
            GameObject overlay = GameObject.Find("Treasure Story Overlay");
            Assert.That(overlay, Is.Not.Null);
            Text[] dialogue = overlay.GetComponentsInChildren<Text>(true);
            Assert.That(dialogue, Is.Not.Empty, "The authored comic dialogue boxes must contain their story text.");
            foreach (Text label in dialogue)
            {
                Assert.That(label.name, Does.StartWith("Treasure Comic Dialogue "), "Full-screen comics only add text inside the authored dialogue boxes.");
                Assert.That(string.IsNullOrWhiteSpace(label.text), Is.False, label.name);
                Assert.That(label.font, Is.SameAs(GameTypography.InterfaceFont), label.name + " uses the embedded artistic face.");
                Assert.That(label.raycastTarget, Is.False, label.name + " must not intercept click-to-advance.");
            }
            Button advance = overlay.GetComponentsInChildren<Button>(false).Single();
            Assert.That(advance.name, Is.EqualTo("Treasure Story Advance"));
            Assert.That(advance.targetGraphic.color.a, Is.EqualTo(0f));
            RawImage image = overlay.GetComponentsInChildren<RawImage>(false).Single();
            Assert.That(image.name, Is.EqualTo("Treasure Story Comic Page"));
            Assert.That(image.texture, Is.Not.Null);
            Canvas canvas = image.GetComponentInParent<Canvas>().rootCanvas;
            Assert.That(overlay.transform.parent, Is.SameAs(FixedGameViewport.Bounds(canvas)),
                "Comics cover the fixed game viewport, preserving the letterbox bars at other window ratios.");
            Canvas.ForceUpdateCanvases();
            foreach (RectTransform rect in new[] { overlay.GetComponent<RectTransform>(), image.rectTransform, (RectTransform)advance.transform })
            {
                AssertFullscreenComicRect(rect);
            }
            Assert.That(image.uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)),
                "Treasure pages retain the complete artwork so dialogue near an outer edge remains visible at every window ratio.");
            return image;
        }
    }
}
