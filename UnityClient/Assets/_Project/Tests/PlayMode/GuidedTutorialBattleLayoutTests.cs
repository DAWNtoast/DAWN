using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator GuidedTutorialLayout_BattleFocusFollowsCompleteCardAndHoverAcrossViewports()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return LoadGuidedTutorialScene();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                yield return DriveGuidedTutorial(controller, "TutorialBoss", new HashSet<string>(), false);
                ClickGuidedButton("Encounter Choice 0");
                yield return null;
                yield return WaitForGuidedCaptureScreenReveal(controller);
                Assert.That(controller.Session.IsGuidedBossBattle, Is.True);
                yield return DriveGuidedCardCourseTo(controller, GuidedBossBattleStep.CourseActionCard);

                // Initialize the existing opt-in capture directory before adding
                // the actual viewport images beneath it. Ordinary runs write none.
                NavalBattleVisualCapture.Capture(controller, "TutorialLayout_Battle_Reference.png",
                    "新手实战教学：完整卡框与引导标注，后续截图使用真实视口尺寸");
                string unchanged = GuidedBossFingerprint(controller.Session);
                using (var viewport = new GuidedLayoutViewport(controller))
                {
                    foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080),
                        new Vector2Int(1280, 720), new Vector2Int(1280, 800) })
                    {
                        viewport.ResizeAndRender(size.x, size.y);
                        yield return null;
                        viewport.Render();
                        GuidedTutorialFocusGraphic focus = RequireSingleGuidedLayoutFocus();
                        Button card = focus.Target.GetComponent<Button>();
                        Assert.That(card.name, Does.StartWith("Battle Card "));
                        CardSchoolFrameGraphic frame = card.GetComponentInChildren<CardSchoolFrameGraphic>();
                        Assert.That(frame, Is.Not.Null);
                        Assert.That(frame.rectTransform.rect.width, Is.GreaterThan(focus.Target.rect.width),
                            "The regression needs the current frame, which extends beyond the selectable body.");
                        Rect restingBounds = AssertGuidedBattleCardFocus(controller, focus, card, frame, size + " rest");
                        Vector2 restingEdges = GuidedBattleCardWorldEdgeLengths(frame.rectTransform);
                        CaptureGuidedBattleViewport(controller, viewport,
                            "TutorialLayout_Battle_" + size.x + "x" + size.y + "_Rest.png");

                        Canvas rootCanvas = card.GetComponentInParent<Canvas>().rootCanvas;
                        var pointer = new PointerEventData(EventSystem.current)
                        {
                            position = RectTransformUtility.WorldToScreenPoint(rootCanvas.worldCamera,
                                frame.rectTransform.TransformPoint(frame.rectTransform.rect.center))
                        };
                        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                        yield return new WaitForSecondsRealtime(.4f);
                        viewport.Render();
                        Rect hoveringBounds = AssertGuidedBattleCardFocus(controller, focus, card, frame, size + " hover", true);
                        Vector2 hoveringEdges = GuidedBattleCardWorldEdgeLengths(frame.rectTransform);
                        // Uprighting a rotated fan card can narrow its AABB even
                        // while the actual card grows. Compare its own edges.
                        Assert.That(hoveringEdges.x, Is.GreaterThan(restingEdges.x * 1.01f),
                            "Inspection enlarges the actual card width, independent of fan rotation.");
                        Assert.That(hoveringEdges.y, Is.GreaterThan(restingEdges.y * 1.01f),
                            "Inspection enlarges the actual card height, independent of fan rotation.");
                        Assert.That(Vector2.Distance(hoveringBounds.center, restingBounds.center), Is.GreaterThan(5f),
                            "The teaching annotation must follow the card's inspection movement.");
                        Canvas cardCanvas = card.GetComponent<Canvas>();
                        Canvas annotationCanvas = focus.GetComponent<Canvas>();
                        Assert.That(cardCanvas.overrideSorting, Is.True);
                        Assert.That(annotationCanvas, Is.Not.Null);
                        Assert.That(annotationCanvas.overrideSorting, Is.True);
                        Assert.That(annotationCanvas.sortingOrder, Is.GreaterThan(cardCanvas.sortingOrder),
                            "The hovered card cannot paint over its teaching annotation.");
                        AssertGuidedBattleTopRaycastButton(pointer.position, card, size + " original pointer after hover lift");
                        CaptureGuidedBattleViewport(controller, viewport,
                            "TutorialLayout_Battle_" + size.x + "x" + size.y + "_Hover.png");

                        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                        yield return new WaitForSecondsRealtime(.45f);
                        viewport.Render();
                        AssertGuidedBattleCardFocus(controller, focus, card, frame, size + " returned to rest");
                        Assert.That(annotationCanvas.overrideSorting, Is.False,
                            "Leaving the card restores the annotation's ordinary layer.");
                        Assert.That(GuidedBossFingerprint(controller.Session), Is.EqualTo(unchanged),
                            "Resizing, pointing and inspecting must never submit a tutorial action.");
                    }
                }
                // The taught action still passes through normal UI input after
                // all the resizes and inspection transitions.
                yield return null;
                yield return PlayGuidedScriptAction(controller, false, null);
                Assert.That(controller.Session.GuidedBossStep, Is.EqualTo(GuidedBossBattleStep.CourseSecondMove));
                LogAssert.NoUnexpectedReceived();
            }
        }

        private static Vector2 GuidedBattleCardWorldEdgeLengths(RectTransform frame)
        {
            var corners = new Vector3[4];
            frame.GetWorldCorners(corners);
            return new Vector2(Vector3.Distance(corners[0], corners[3]),
                Vector3.Distance(corners[0], corners[1]));
        }

        private static Rect AssertGuidedBattleCardFocus(CoreGameplayDemoController controller,
            GuidedTutorialFocusGraphic focus, Button card, CardSchoolFrameGraphic frame, string context, bool hovering = false)
        {
            Canvas.ForceUpdateCanvases();
            Assert.That(focus.VisualTarget, Is.SameAs(frame.rectTransform), context);
            Assert.That(focus.raycastTarget, Is.False, context);
            Assert.That(focus.canvasRenderer.cull, Is.False, context);
            RectTransform surface = controller.GetComponentsInChildren<RectTransform>()
                .Single(rect => rect.name == "Phase Content");
            Assert.That(focus.TryGetProtectedRect(surface, out Rect protectedBounds), Is.True, context);
            var corners = new Vector3[4];
            frame.rectTransform.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in corners)
            {
                Vector2 point = surface.InverseTransformPoint(corner);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
                Assert.That(point.x, Is.InRange(protectedBounds.xMin - .5f, protectedBounds.xMax + .5f), context);
                Assert.That(point.x, Is.InRange(surface.rect.xMin - .5f, surface.rect.xMax + .5f), context + " horizontal visibility");
                if (hovering)
                {
                    Assert.That(point.y, Is.InRange(surface.rect.yMin - .5f, surface.rect.yMax + .5f), context + " complete inspected card");
                    Assert.That(point.y, Is.InRange(protectedBounds.yMin - .5f, protectedBounds.yMax + .5f), context);
                }
            }
            Assert.That(max.y, Is.LessThanOrEqualTo(surface.rect.yMax + .5f), context + " visible card top");
            Assert.That(max.y, Is.LessThanOrEqualTo(protectedBounds.yMax + .5f), context + " outlined card top");
            if (!hovering)
            {
                // The fan rests partly below the screen. Its upper half and
                // energy emblem remain visible/selectable; hover reveals it all.
                float visibleBottom = Mathf.Max(min.y, surface.rect.yMin);
                float visibleHeight = max.y - visibleBottom;
                Assert.That(visibleHeight, Is.GreaterThanOrEqualTo((max.y - min.y) * .5f), context + " selectable resting fan");
                Assert.That(protectedBounds.Contains(new Vector2((min.x + max.x) * .5f,
                    visibleBottom + visibleHeight * .5f)), Is.True, context + " outline follows visible card");
            }
            AssertGuidedInstructionsRemainVisible(controller);
            AssertSingleGuidedLayoutFocus(card.name);

            // The visible resource emblem protrudes beyond the smaller button
            // body. It must remain a real clickable part of the taught card.
            CardOrnamentGraphic energy = card.GetComponentsInChildren<CardOrnamentGraphic>()
                .Single(marker => marker.Kind == CardOrnamentKind.Energy);
            Vector3 local = energy.rectTransform.anchoredPosition +
                Vector2.Scale(new Vector2(-1f, 1f), energy.rectTransform.sizeDelta) * .25f;
            Assert.That(focus.Target.rect.Contains(local), Is.False, context + " expanded hit area");
            Canvas root = card.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, focus.Target.TransformPoint(local));
            AssertGuidedBattleTopRaycastButton(screen, card, context + " full-frame energy emblem");
            return protectedBounds;
        }

        private static void AssertGuidedBattleTopRaycastButton(Vector2 position, Button expected, string context)
        {
            Assert.That(EventSystem.current, Is.Not.Null, context);
            var pointer = new PointerEventData(EventSystem.current) { position = position };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, context + " at " + position + " must receive normal pointer input.");
            string details = string.Join(" | ", hits.Take(3).Select(hit =>
            {
                Button button = hit.gameObject.GetComponentInParent<Button>();
                return hit.gameObject.name + " [button=" + (button == null ? "none" : button.name) + "]";
            }));
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(expected),
                context + " at " + position + "; first three hits: " + details);
        }

        private static void CaptureGuidedBattleViewport(CoreGameplayDemoController controller,
            GuidedLayoutViewport viewport, string fileName)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-navalVisualCapturePath");
            if (index < 0) return;
            string directory = Path.Combine(Path.GetFullPath(args[index + 1]), "GuidedBattleLayout");
            Directory.CreateDirectory(directory);
            viewport.Render();
            Canvas root = controller.GetComponentsInChildren<Canvas>().Single(canvas => canvas.isRootCanvas);
            RenderTexture target = root.worldCamera.targetTexture;
            Assert.That(target, Is.Not.Null);
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                RenderTexture.active = target;
                pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
                pixels.Apply(false, false);
                Color32[] sample = pixels.GetPixels32();
                int darkest = 255, lightest = 0;
                for (int pixel = 0; pixel < sample.Length; pixel += 997)
                {
                    int light = (sample[pixel].r + sample[pixel].g + sample[pixel].b) / 3;
                    darkest = Mathf.Min(darkest, light);
                    lightest = Mathf.Max(lightest, light);
                }
                Assert.That(lightest - darkest, Is.GreaterThan(20), "The actual viewport capture cannot be blank.");
                using (var stream = new FileStream(Path.Combine(directory, fileName), FileMode.CreateNew,
                    FileAccess.Write, FileShare.None))
                {
                    byte[] png = pixels.EncodeToPNG();
                    stream.Write(png, 0, png.Length);
                }
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) Object.Destroy(pixels);
            }
        }
    }
}
