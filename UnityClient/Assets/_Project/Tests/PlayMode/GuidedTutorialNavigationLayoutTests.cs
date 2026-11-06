using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        [Timeout(60000)]
        public IEnumerator GuidedTutorialLayout_CompassGoesStraightToSailAcrossViewports()
        {
            yield return LoadGuidedTutorialScene(enterDeparture: true);
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            ClickGuidedButton("Start Voyage");
            yield return null;
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));

            using (var viewport = new GuidedLayoutViewport(controller))
            {
                yield return AssertGuidedNavigationLayoutSizes(viewport, controller, "Heading East", "罗盘");
                Button east = RequireGuidedButton("Heading East");
                GameObject retainedChart = GameObject.Find("Navigation Chart");
                Button.ButtonClickedEvent priorHeadingClick = east.onClick;
                var eastFocus = RequireSingleGuidedLayoutFocus();
                Assert.That(eastFocus.VisualTarget, Is.SameAs(east.transform.Find("Label")),
                    "A radial heading uses its direction label, never the whole compass square.");
                RectTransform eastRect = (RectTransform)east.transform;
                ClickNavigationInterfaceRefreshGraphic(east.gameObject,
                    Vector2.right * eastRect.rect.width * .335f);
                yield return null;

                Assert.That(GameObject.Find("Navigation Chart"), Is.SameAs(retainedChart));
                Assert.That(RequireGuidedButton("Heading East"), Is.SameAs(east));
                Assert.That(east.IsInteractable(), Is.False, "The same heading button must become locked at the Sail step.");
                priorHeadingClick.Invoke();
                Assert.That(controller.CurrentGuidedTutorialNavigationInputStep,
                    Is.EqualTo(GuidedTutorialNavigationInputStep.Sail), "Retired tutorial wrappers cannot reopen the heading step.");

                Assert.That(controller.CurrentGuidedTutorialNavigationInputStep,
                    Is.EqualTo(GuidedTutorialNavigationInputStep.Sail),
                    "Selecting the compass heading goes directly to execution, without a slider lesson.");
                Slider slider = GameObject.Find("Distance Slider").GetComponent<Slider>();
                Assert.That(slider.IsInteractable(), Is.False);
                Assert.That(slider.GetComponent<GuidedTutorialDistanceInput>(), Is.Null,
                    "The removed lesson must not attach pointer-release or submit handlers.");
                slider.onValueChanged.Invoke(90f);
                ExecuteEvents.Execute(slider.gameObject, new PointerEventData(EventSystem.current)
                    { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(slider.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                yield return null;
                Assert.That(controller.Session.PlannedDistanceNm, Is.EqualTo(CoreGuidedTutorialContent.PlannedDistanceNm),
                    "Locked or cached slider callbacks cannot change the fixed teaching route.");
                Assert.That(controller.CurrentGuidedTutorialNavigationInputStep,
                    Is.EqualTo(GuidedTutorialNavigationInputStep.Sail));
                yield return AssertGuidedNavigationLayoutSizes(viewport, controller, "Execute Sail", "执行航行");
                ClickNavigationInterfaceRefreshGraphic(RequireGuidedButton("Execute Sail").gameObject, Vector2.zero);
                yield return null;
                yield return WaitForGuidedNavigationPresentation();
                Assert.That(controller.Session.ActiveEncounter.Id, Is.EqualTo("tutorial_first_event"));
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator GuidedTutorialLayout_RecruitedNavigatorTeachesEventTargetLockAcrossViewports()
        {
            yield return LoadGuidedTutorialScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return DriveGuidedTutorial(controller, "SecondSail", new System.Collections.Generic.HashSet<string>(), false);
            Assert.That(controller.Session.HasNavigator, Is.True);
            Assert.That(controller.Session.GuidedTutorialLeg, Is.EqualTo(CoreGuidedTutorialContent.NavigatorVoyageLeg));
            CoreMapNode targetNode = controller.Session.Map.Nodes.Single(node =>
                node.Encounter.Id == CoreGuidedTutorialContent.NavigatorEventId);
            string targetName = "Event Node " + targetNode.Id;
            Assert.That(controller.Session.HasDirectCourseTarget, Is.False,
                "Recruitment must teach an actual map click, not preselect the event for the player.");
            float distanceBefore = controller.Session.TotalDistanceNm;
            int waterBefore = controller.Session.Cargo.Water;
            Button east = RequireGuidedButton("Heading East");
            Button sail = RequireGuidedButton("Execute Sail");
            Button.ButtonClickedEvent priorEastClick = east.onClick;
            Assert.That(east.IsInteractable(), Is.False);
            Assert.That(sail.IsInteractable(), Is.False);
            east.onClick.Invoke();
            sail.onClick.Invoke();
            foreach (CoreMapNode other in controller.Session.Map.Nodes.Where(node => node != targetNode && node.IsChartVisible))
            {
                Button otherMarker = RequireGuidedButton("Event Node " + other.Id);
                Assert.That(otherMarker.IsInteractable(), Is.False);
                otherMarker.onClick.Invoke();
            }
            Assert.That(controller.CurrentGuidedTutorialNavigationInputStep, Is.EqualTo(GuidedTutorialNavigationInputStep.Heading));
            Assert.That(controller.Session.HasDirectCourseTarget, Is.False);
            Assert.That(controller.Session.TotalDistanceNm, Is.EqualTo(distanceBefore));

            using (var viewport = new GuidedLayoutViewport(controller))
            {
                yield return AssertGuidedNavigationLayoutSizes(viewport, controller, targetName, "八个方向", true);
                Assert.That(GameObject.Find("Guided Tutorial Instruction").GetComponent<Text>().text, Does.Contain("锁定"));
                viewport.Capture("78_Guided_Event_Target_Lock.png");
                Button marker = RequireGuidedButton(targetName);
                Button.ButtonClickedEvent priorMarkerClick = marker.onClick;
                ClickNavigationInterfaceRefreshGraphic(marker.gameObject, Vector2.zero);
                yield return null;
                Assert.That(controller.CurrentGuidedTutorialNavigationInputStep, Is.EqualTo(GuidedTutorialNavigationInputStep.Sail));
                Assert.That(controller.Session.HasDirectCourseTarget, Is.True);
                Assert.That(controller.Session.DirectCourseTarget, Is.EqualTo(targetNode.Position));
                Assert.That(controller.Session.TotalDistanceNm, Is.EqualTo(distanceBefore), "Selecting an event must not sail automatically.");
                Assert.That(controller.Session.Cargo.Water, Is.EqualTo(waterBefore));
                Assert.That(RequireGuidedButton(targetName).IsInteractable(), Is.False);
                Assert.That(RequireGuidedButton("Heading East").IsInteractable(), Is.False);
                priorMarkerClick.Invoke();
                priorEastClick.Invoke();
                Assert.That(controller.Session.DirectCourseTarget, Is.EqualTo(targetNode.Position), "Cached callbacks cannot clear the event lock.");
                Assert.That(controller.CurrentGuidedTutorialNavigationInputStep, Is.EqualTo(GuidedTutorialNavigationInputStep.Sail));
                yield return AssertGuidedNavigationLayoutSizes(viewport, controller, "Execute Sail", "锁定");
                Assert.That(GameObject.Find("Guided Tutorial Instruction").GetComponent<Text>().text, Does.Contain("执行航行"));
                viewport.Capture("79_Guided_Event_Target_Execute.png");
                ClickNavigationInterfaceRefreshGraphic(RequireGuidedButton("Execute Sail").gameObject, Vector2.zero);
                yield return null;
                yield return WaitForGuidedNavigationPresentation();
                Assert.That(controller.Session.ActiveEncounter.Id, Is.EqualTo(CoreGuidedTutorialContent.NavigatorEventId));
                ClickGuidedButton("Encounter Choice 0");
                yield return null;
                Assert.That(controller.Session.GuidedTutorialCompletedEncounterIds, Does.Contain(CoreGuidedTutorialContent.NavigatorEventId));
                AssertSingleGuidedLayoutFocus("Heading East");
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator AssertGuidedNavigationLayoutSizes(GuidedLayoutViewport viewport,
            CoreGameplayDemoController controller, string target, string instructionTerm, bool verifyPointerTarget = false)
        {
            foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720),
                new Vector2Int(1280, 800) })
            {
                viewport.ResizeAndRender(size.x, size.y);
                yield return null;
                viewport.Render();
                AssertSingleGuidedLayoutFocus(target);
                if (verifyPointerTarget)
                {
                    Button button = RequireGuidedButton(target);
                    RectTransform rect = (RectTransform)button.transform;
                    Canvas canvas = button.GetComponentInParent<Canvas>();
                    var pointer = new PointerEventData(EventSystem.current)
                    {
                        button = PointerEventData.InputButton.Left,
                        position = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rect.TransformPoint(rect.rect.center)),
                    };
                    var hits = new System.Collections.Generic.List<RaycastResult>();
                    EventSystem.current.RaycastAll(pointer, hits);
                    Assert.That(hits, Is.Not.Empty, target + " at " + size);
                    Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.SameAs(button.gameObject),
                        "The event target must remain clickable without the instruction covering it at " + size);
                }
                AssertGuidedInstructionsRemainVisible(controller);
                Text instruction = GameObject.Find("Guided Tutorial Instruction").GetComponent<Text>();
                TextGenerationSettings textSettings = instruction.GetGenerationSettings(instruction.rectTransform.rect.size);
                instruction.cachedTextGenerator.Populate(instruction.text, textSettings);
                textSettings.verticalOverflow = VerticalWrapMode.Overflow;
                textSettings.generateOutOfBounds = true;
                using (var complete = new TextGenerator())
                {
                    complete.Populate(instruction.text, textSettings);
                    Assert.That(instruction.cachedTextGenerator.characterCountVisible, Is.EqualTo(complete.characterCountVisible),
                        "The instruction must retain its last line at " + size);
                    Assert.That(instruction.cachedTextGenerator.vertexCount, Is.EqualTo(complete.vertexCount),
                        "The instruction must render its complete glyph mesh at " + size);
                }
                Assert.That(GameObject.Find("Guided Tutorial Instruction").GetComponent<Text>().text,
                    Does.Contain(instructionTerm), target + " at " + size);
                Assert.That(GameObject.Find("Guided Tutorial Instruction").GetComponent<Text>().text,
                    Does.Not.Contain("滑杆").And.Not.Contain("滑块"), "No manual-distance lesson at " + size);
                Assert.That(GameObject.Find("Distance Slider").GetComponent<Slider>().IsInteractable(), Is.False);
                RectTransform paper = GameObject.Find("Guided Tutorial Banner").GetComponent<RectTransform>();
                foreach (string readout in new[] { "Navigation Map Title Strip", "Distance Label", "Forecast" })
                    Assert.That(GuidedWorldRect(paper).Overlaps(GuidedWorldRect(
                        GameObject.Find(readout).GetComponent<RectTransform>())), Is.False,
                        "The instruction must leave " + readout + " readable at " + size);
            }
        }

        private static GuidedTutorialFocusGraphic RequireSingleGuidedLayoutFocus()
        {
            GuidedTutorialFocusGraphic[] focuses = Object.FindObjectsByType<GuidedTutorialFocusGraphic>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(item => item.isActiveAndEnabled && item.HasVisibleTarget).ToArray();
            Assert.That(focuses.Length, Is.EqualTo(1),
                "A sequential lesson must present one next action, not competing red outlines.");
            return focuses[0];
        }

        private static void AssertSingleGuidedLayoutFocus(string target)
        {
            Assert.That(RequireSingleGuidedLayoutFocus().Target.name, Is.EqualTo(target));
            AssertGuidedFocusTarget(target);
        }

        // A real camera/render texture changes CanvasScaler's rendering display
        // size. This exercises layout and GraphicRaycaster together without
        // changing the editor Game view or desktop display resolution.
        private sealed class GuidedLayoutViewport : IDisposable
        {
            private readonly Canvas canvas;
            private readonly RenderMode originalMode;
            private readonly Camera originalCamera;
            private readonly float originalPlaneDistance;
            private readonly Camera camera;
            private RenderTexture target;

            public GuidedLayoutViewport(CoreGameplayDemoController controller)
            {
                canvas = controller.GetComponentsInChildren<Canvas>().Single(item => item.isRootCanvas);
                originalMode = canvas.renderMode;
                originalCamera = canvas.worldCamera;
                originalPlaneDistance = canvas.planeDistance;
                var cameraHost = new GameObject("Guided Layout Test Camera", typeof(Camera));
                camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0f, 0f, -1000f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 100f;
            }

            public void ResizeAndRender(int width, int height)
            {
                camera.targetTexture = null;
                if (target != null) { target.Release(); Object.Destroy(target); }
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                camera.targetTexture = target;
                camera.aspect = width / (float)height;
                camera.rect = new Rect(0f, 0f, 1f, 1f);
                Render();
                float scale = Mathf.Min(width / 1920f, height / 1080f);
                Assert.That(canvas.renderingDisplaySize.x, Is.EqualTo(width).Within(.5f));
                Assert.That(canvas.renderingDisplaySize.y, Is.EqualTo(height).Within(.5f));
                Assert.That(canvas.scaleFactor, Is.EqualTo(scale).Within(.001f));
                Assert.That(canvas.GetComponent<RectTransform>().rect.width, Is.EqualTo(width / scale).Within(.1f));
                Assert.That(canvas.GetComponent<RectTransform>().rect.height, Is.EqualTo(height / scale).Within(.1f));
            }

            public void Render()
            {
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
                for (int pass = 0; pass < 3; pass++)
                {
                    Canvas.ForceUpdateCanvases();
                    handle.Invoke(scaler, null);
                    foreach (Text label in canvas.GetComponentsInChildren<Text>()) label.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
            }

            public void Capture(string fileName)
            {
                string[] args = Environment.GetCommandLineArgs();
                int index = Array.IndexOf(args, "-navalVisualCapturePath");
                if (index < 0) return;
                if (fileName != "78_Guided_Event_Target_Lock.png" && fileName != "79_Guided_Event_Target_Execute.png")
                    throw new ArgumentException("Expected one of the event-target lesson screenshots.", nameof(fileName));
                // Reserve the same opt-in directory as other visual tests without
                // switching this measured camera canvas into world-space capture.
                MethodInfo resolve = typeof(NavalBattleVisualCapture).GetMethod("ResolveDirectory",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(resolve, Is.Not.Null);
                resolve.Invoke(null, null);
                string path = Path.Combine(Path.GetFullPath(args[index + 1]), fileName);
                Assert.That(target, Is.Not.Null, "Capture requires the measured viewport render texture.");
                RenderTexture originalActive = RenderTexture.active;
                Texture2D pixels = null;
                try
                {
                    Render();
                    RenderTexture.active = target;
                    pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false, false);
                    pixels.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
                    pixels.Apply(false, false);
                    using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        byte[] png = pixels.EncodeToPNG();
                        stream.Write(png, 0, png.Length);
                    }
                }
                finally
                {
                    RenderTexture.active = originalActive;
                    if (pixels != null) Object.DestroyImmediate(pixels);
                }
            }

            public void Dispose()
            {
                canvas.renderMode = originalMode;
                canvas.worldCamera = originalCamera;
                canvas.planeDistance = originalPlaneDistance;
                camera.targetTexture = null;
                Object.Destroy(camera.gameObject);
                if (target != null) { target.Release(); Object.Destroy(target); }
                Canvas.ForceUpdateCanvases();
            }
        }
    }
}
