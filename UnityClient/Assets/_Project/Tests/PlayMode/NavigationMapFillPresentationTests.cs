using System.Collections;
using System.Reflection;
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
        public IEnumerator NavigationMapFill_EventHoverSelectionAndRarePulseKeepTheirVisibleShape()
        {
            yield return LoadNavigationInterfaceRefreshFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            session.Map.Nodes.Add(new CoreMapNode
            {
                Id = 990, Position = new VoyagePoint(43f, 39f), Discovered = true,
                Encounter = new CoreEncounter("map-fill-event", "海图标记", "事件", "普通海图标记。", false, false, 0, 0)
            });
            session.Map.Nodes.Add(new CoreMapNode
            {
                Id = 991, Position = new VoyagePoint(55f, 43f), Discovered = true,
                Encounter = new CoreEncounter("legendary_pirate_map_fill", "稀有标记", "海战", "持续脉动的海图标记。", true, false, 1, 70)
            });
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
            yield return null;
            yield return null;

            RectTransform map = GameObject.Find("Map Layer").GetComponent<RectTransform>();
            AssertNavigationMapFillSquareCells(map, session.Map);
            Button ordinary = ActiveButton("Event Node 990");
            Button rare = ActiveButton("Event Node 991");
            Assert.That(ordinary.GetComponent<NauticalButtonMotion>(), Is.Not.Null);
            Assert.That(rare.GetComponent<VoyageEventMarkerPulse>(), Is.Not.Null);
            var ordinaryRect = ordinary.GetComponent<RectTransform>();
            var rareRect = rare.GetComponent<RectTransform>();
            float ordinaryRestingWidth = NavigationMapFillWorldWidth(ordinaryRect);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(ordinary.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            EventSystem.current.SetSelectedGameObject(ordinary.gameObject);
            float minimumPulseWidth = float.PositiveInfinity, maximumPulseWidth = 0f;
            float maximumHoverWidth = ordinaryRestingWidth;
            float deadline = Time.realtimeSinceStartup + .4f;
            int frames = 0;
            do
            {
                // Let both animation components run. Reapplying layout here
                // would conceal a tween that overwrites the compensated scale.
                yield return null;
                AssertNavigationMapFillRoundMarker(ordinaryRect, "hover and selection");
                AssertNavigationMapFillRoundMarker(rareRect, "rare event pulse");
                maximumHoverWidth = Mathf.Max(maximumHoverWidth, NavigationMapFillWorldWidth(ordinaryRect));
                float pulseWidth = NavigationMapFillWorldWidth(rareRect);
                minimumPulseWidth = Mathf.Min(minimumPulseWidth, pulseWidth);
                maximumPulseWidth = Mathf.Max(maximumPulseWidth, pulseWidth);
                frames++;
            } while (frames < 12 || Time.realtimeSinceStartup < deadline);
            Assert.That(maximumHoverWidth, Is.GreaterThan(ordinaryRestingWidth * 1.005f),
                "Hover/selection feedback must remain animated after aspect compensation.");
            Assert.That(maximumPulseWidth - minimumPulseWidth, Is.GreaterThan(minimumPulseWidth * .001f),
                "The rare marker must continue pulsing while retaining its proportions.");

            ExecuteEvents.Execute(ordinary.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            EventSystem.current.SetSelectedGameObject(null);
            for (int frame = 0; frame < 8; frame++)
            {
                yield return null;
                AssertNavigationMapFillRoundMarker(ordinaryRect, "return from hover and selection");
                AssertNavigationMapFillRoundMarker(rareRect, "continuing rare event pulse");
            }
            Assert.That(GameObject.Find("Map Layer").GetComponent<RectTransform>(), Is.SameAs(map),
                "Marker feedback must keep the existing chart.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavigationMapFill_DistanceSliderRebuildKeepsCourseTargetRound()
        {
            yield return LoadNavigationInterfaceRefreshFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var map = GameObject.Find("Map Layer").GetComponent<RectTransform>();
            var slider = GameObject.Find("Distance Slider").GetComponent<Slider>();
            AssertNavigationMapFillSquareCells(map, controller.Session.Map);
            foreach (int distance in new[] { 20, 170, 60 })
            {
                RectTransform previous = GameObject.Find("Course Target").GetComponent<RectTransform>();
                slider.value = distance;
                RectTransform target = GameObject.Find("Course Target").GetComponent<RectTransform>();
                Assert.That(target, Is.Not.SameAs(previous), "The real slider callback must rebuild the course preview.");
                AssertNavigationMapFillRoundMarker(target, "first frame after changing distance");
                for (int frame = 0; frame < 8; frame++)
                {
                    yield return null;
                    AssertNavigationMapFillRoundMarker(target, "settled course target");
                }
                Assert.That(controller.Session.PlannedDistanceNm, Is.EqualTo(distance));
                Assert.That(GameObject.Find("Map Layer").GetComponent<RectTransform>(), Is.SameAs(map));
                Assert.That(GameObject.Find("Distance Slider").GetComponent<Slider>(), Is.SameAs(slider));
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavigationMapFill_DragPansToBothBoundsWithoutSelectingACourse()
        {
            yield return LoadNavigationInterfaceRefreshFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            CoreVoyageMap world = session.Map;
            var map = GameObject.Find("Map Layer").GetComponent<RectTransform>();
            var frame = GameObject.Find("Procedural Sea Chart Frame").GetComponent<RectTransform>();
            var sea = GameObject.Find("Ocean Tile Layer").GetComponent<RectTransform>();
            var input = GameObject.Find("Map Course Input").GetComponent<MapCourseInputSurface>();
            Assert.That(map.GetComponent<NavigationMapViewport>(), Is.Not.Null);
            GameObject dragHandler = ExecuteEvents.GetEventHandler<IBeginDragHandler>(input.gameObject);
            Assert.That(dragHandler, Is.Not.Null, "The visible map surface must route dragging to its viewport.");
            Canvas canvas = map.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            Vector2 start = RectTransformUtility.WorldToScreenPoint(camera, frame.TransformPoint(frame.rect.center));
            Vector3 originalOffset = map.localPosition;
            Vector3 firstOffset = originalOffset;
            SailingDirection heading = session.Heading;
            int distance = session.PlannedDistanceNm;
            VoyagePoint origin = session.Position;
            bool directTarget = session.HasDirectCourseTarget;
            for (int pass = 0; pass < 2; pass++)
            {
                float sign = pass == 0 ? 1f : -1f;
                Vector2 end = RectTransformUtility.WorldToScreenPoint(camera,
                    frame.TransformPoint(frame.rect.center + frame.rect.size * (3f * sign)));
                var pointer = new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                    position = start, pressPosition = start, eligibleForClick = true,
                    pointerPressRaycast = new RaycastResult { module = raycaster, gameObject = input.gameObject }
                };
                ExecuteEvents.Execute(dragHandler, pointer, ExecuteEvents.beginDragHandler);
                pointer.position = end;
                pointer.delta = end - start;
                pointer.dragging = true;
                ExecuteEvents.Execute(dragHandler, pointer, ExecuteEvents.dragHandler);
                ExecuteEvents.Execute(dragHandler, pointer, ExecuteEvents.endDragHandler);
                Assert.That(pointer.eligibleForClick, Is.False, "A drag must not become an accidental sail destination.");
                pointer.dragging = false;
                input.OnPointerClick(pointer);
                yield return null;
                Assert.That(GameObject.Find("Map Layer").GetComponent<RectTransform>(), Is.SameAs(map));
                Assert.That(session.Map, Is.SameAs(world), "Panning must never regenerate geography.");
                Assert.That(session.Heading, Is.EqualTo(heading));
                Assert.That(session.PlannedDistanceNm, Is.EqualTo(distance));
                Assert.That(session.HasDirectCourseTarget, Is.EqualTo(directTarget));
                Assert.That(session.Position.X, Is.EqualTo(origin.X));
                Assert.That(session.Position.Y, Is.EqualTo(origin.Y));
                AssertNavigationMapFillSquareCells(map, world);
                var corners = new Vector3[4];
                sea.GetWorldCorners(corners);
                Vector3 bottomLeft = frame.InverseTransformPoint(corners[0]);
                Vector3 topRight = frame.InverseTransformPoint(corners[2]);
                Assert.That(bottomLeft.x, Is.LessThanOrEqualTo(frame.rect.xMin + .02f));
                Assert.That(bottomLeft.y, Is.LessThanOrEqualTo(frame.rect.yMin + .02f));
                Assert.That(topRight.x, Is.GreaterThanOrEqualTo(frame.rect.xMax - .02f));
                Assert.That(topRight.y, Is.GreaterThanOrEqualTo(frame.rect.yMax - .02f));
                if (pass == 0) firstOffset = map.localPosition;
                else Assert.That(Vector3.Distance(firstOffset, map.localPosition), Is.GreaterThan(.1f),
                    "Dragging opposite directions must reveal different portions of the world.");
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertNavigationMapFillSquareCells(RectTransform map, CoreVoyageMap world)
        {
            Assert.That(map.lossyScale.x / map.lossyScale.y, Is.EqualTo(1f).Within(.001f),
                "The expanded world uses uniform map scaling.");
            RectTransform sea = map.Find("Ocean Tile Layer") as RectTransform;
            var corners = new Vector3[4];
            sea.GetWorldCorners(corners);
            float width = Vector3.Distance(corners[0], corners[3]);
            float height = Vector3.Distance(corners[0], corners[1]);
            Assert.That(width / world.Columns, Is.EqualTo(height / world.Rows).Within(.001f), "Terrain cells remain square.");
            Assert.That(width / world.FineColumns, Is.EqualTo(height / world.FineRows).Within(.001f), "Exploration cells remain square.");
        }

        private static void AssertNavigationMapFillRoundMarker(RectTransform marker, string context)
        {
            float horizontal = marker.TransformVector(Vector3.right).magnitude;
            float vertical = marker.TransformVector(Vector3.up).magnitude;
            Assert.That(vertical, Is.GreaterThan(0f), context + " " + marker.name);
            Assert.That(horizontal / vertical, Is.EqualTo(1f).Within(.001f),
                context + " " + marker.name + " must retain equal displayed horizontal and vertical scale");
            var corners = new Vector3[4];
            marker.GetWorldCorners(corners);
            Assert.That(Vector3.Distance(corners[0], corners[3]) / Vector3.Distance(corners[0], corners[1]),
                Is.EqualTo(1f).Within(.001f), context + " " + marker.name + " must remain round or square");
        }

        private static float NavigationMapFillWorldWidth(RectTransform marker)
        {
            var corners = new Vector3[4];
            marker.GetWorldCorners(corners);
            return Vector3.Distance(corners[0], corners[3]);
        }
    }
}
