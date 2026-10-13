using System.Collections;
using System.Collections.Generic;
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
    // Reuse the fixture's preservation and restoration of the player's save keys.
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator DepartureDestinationButtons_PointerClicksSelectNamedPortsAndDisableOrigin()
        {
            yield return LoadProvisioningFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            yield return ClickDestinationTestGraphic(ActiveButton("Port Hotspot Voyage").gameObject, Vector2.zero);
            string origin = session.CurrentPort;
            string initial = session.SelectedDestinationPort;
            int portCount = CoreVoyageSession.DestinationPorts.Count;
            int originIndex = Enumerable.Range(0, portCount)
                .Single(index => CoreVoyageSession.DestinationPorts[index] == origin);
            int initialIndex = Enumerable.Range(0, portCount)
                .Single(index => CoreVoyageSession.DestinationPorts[index] == initial);
            Assert.That(Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Count(button => button.gameObject.activeInHierarchy && button.name.StartsWith("Departure Destination ")),
                Is.EqualTo(6));
            for (int index = 0; index < portCount; index++)
            {
                Button picker = ActiveButton("Departure Destination " + index);
                Assert.That(picker, Is.Not.Null);
                Assert.That(picker.GetComponentInChildren<Text>().text, Is.EqualTo(CoreVoyageSession.DestinationPorts[index]));
                Assert.That(picker.IsInteractable(), Is.EqualTo(index != originIndex));
            }
            for (int route = 0; route < CoreVoyageSession.Routes.Count; route++)
                Assert.That(GameObject.Find("Destination Port " + route), Is.Null,
                    "The old per-route cycling controls must be removed.");

            yield return ClickDestinationTestGraphic(ActiveButton("Departure Destination " + originIndex).gameObject, Vector2.zero);
            Assert.That(session.SelectedDestinationPort, Is.EqualTo(initial), "A pointer click on the dark origin does nothing.");
            int nextCyclingIndex = Enumerable.Range(1, portCount).Select(offset => (initialIndex + offset) % portCount)
                .First(index => index != originIndex);
            int firstTarget = Enumerable.Range(0, portCount)
                .First(index => index != originIndex && index != initialIndex && index != nextCyclingIndex);
            int secondTarget = Enumerable.Range(0, portCount)
                .Last(index => index != originIndex && index != initialIndex && index != firstTarget);
            foreach (int targetIndex in new[] { firstTarget, secondTarget })
            {
                yield return ClickDestinationTestGraphic(ActiveButton("Departure Destination " + targetIndex).gameObject, Vector2.zero);
                string target = CoreVoyageSession.DestinationPorts[targetIndex];
                Assert.That(session.SelectedDestinationPort, Is.EqualTo(target), "One click must select the named port directly.");
                Assert.That(GameObject.Find("Departure Route Summary").GetComponent<Text>().text,
                    Is.EqualTo("当前航线：" + origin + " → " + target));
                Assert.That(GameObject.Find("Destination Summary"), Is.Null);
                Assert.That(session.CurrentPort, Is.EqualTo(origin));
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
                Assert.That(ActiveButton("Departure Destination " + originIndex).IsInteractable(), Is.False);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DestinationPort_EdgeClickTargetsPierAndKeepsBossBlockade()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            controller.Session.SetSeed("PORT-EDGE-CLICK");
            ActiveButton("Port Hotspot Voyage").onClick.Invoke();
            yield return null;
            ActiveButton("Start Voyage").onClick.Invoke();
            yield return null;

            CoreVoyageSession voyage = controller.Session;
            voyage.Map.Nodes.Clear();
            System.Array.Clear(voyage.Map.TerrainCells, 0, voyage.Map.CellCount);
            SetDestinationTestPosition(voyage, new VoyagePoint(voyage.Map.Destination.X - 4f, voyage.Map.Destination.Y));
            SetDestinationTestField(voyage, "randomAmbushOccurred", true);
            ActiveButton("Heading East").onClick.Invoke();
            yield return null;

            RectTransform marker = GameObject.Find("Destination Port").GetComponent<RectTransform>();
            yield return ClickDestinationTestGraphic(marker.gameObject, new Vector2(-marker.rect.width * 0.4f, 0f));
            AssertDestinationTestPoint(voyage.DirectCourseTarget, voyage.Map.Destination);
            Assert.That(voyage.HasDirectCourseTarget, Is.True);
            Assert.That(GameObject.Find("Course Target Label"), Is.Null);
            Assert.That(GameObject.Find("Destination Label"), Is.Null);
            Assert.That(voyage.BossDefeated, Is.False);
            Assert.That(ActiveButton("Execute Sail").interactable, Is.True);
            ActiveButton("Execute Sail").onClick.Invoke();
            yield return WaitForNavigationMotionCompletion(controller);
            AssertDestinationTestPoint(voyage.Position, voyage.Map.Destination);
            Assert.That(voyage.Phase, Is.EqualTo(CoreDemoPhase.Sailing), "Selecting a port cannot bypass its route boss.");
            Assert.That(voyage.Settlement, Is.Null);

            // Once the blockade is lifted, already being at the pier must not
            // require sailing away before the normal sail action can dock.
            SetDestinationTestField(voyage, "bossDefeated", true);
            ActiveButton("Heading East").onClick.Invoke();
            yield return null;
            ActiveButton("Execute Sail").onClick.Invoke();
            yield return WaitForNavigationMotionCompletion(controller);
            Assert.That(voyage.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(voyage.Settlement.Outcome, Is.EqualTo(VoyageOutcome.ArrivedAtPort));
            Assert.That(GameObject.Find("Settlement Card"), Is.Not.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DestinationPort_HitAreaPreservesOverlappingEventAndFreeWaterSelection()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            controller.Session.SetSeed("PORT-CLICK-PRIORITY");
            ActiveButton("Port Hotspot Voyage").onClick.Invoke();
            yield return null;
            ActiveButton("Start Voyage").onClick.Invoke();
            yield return null;

            CoreVoyageSession voyage = controller.Session;
            CoreMapNode node = voyage.Map.Nodes.First(candidate => candidate.Encounter.HasChartLocation &&
                !candidate.IsBoss && candidate.Encounter.BossKind == CoreBossKind.None);
            CoreMapNode boss = voyage.Map.Nodes.First(candidate => candidate.IsBoss);
            voyage.Map.Nodes.Clear();
            voyage.Map.Nodes.Add(node);
            System.Array.Clear(voyage.Map.TerrainCells, 0, voyage.Map.CellCount);
            node.Position = new VoyagePoint(voyage.Map.Destination.X - 1f, voyage.Map.Destination.Y);
            node.Discovered = true;
            node.Visited = false;
            SetDestinationTestPosition(voyage, new VoyagePoint(voyage.Map.Destination.X - 4f, voyage.Map.Destination.Y - 3f));
            ActiveButton("Heading East").onClick.Invoke();
            yield return null;

            yield return ClickDestinationTestGraphic(GameObject.Find("Event Node " + node.Id), Vector2.zero);
            AssertDestinationTestPoint(voyage.DirectCourseTarget, node.Position);
            Assert.That(GameObject.Find("Course Target Label"), Is.Null);
            Assert.That(GameObject.Find("Event Label " + node.Id), Is.Null);

            VoyagePoint water = new VoyagePoint(voyage.Map.Destination.X - 7f, voyage.Map.Destination.Y - 6f);
            Vector2 waterLocal = DestinationTestMapPoint(voyage, water);
            yield return ClickDestinationTestGraphic(GameObject.Find("Map Course Input"), waterLocal);
            AssertDestinationTestPoint(voyage.DirectCourseTarget, water);
            Assert.That(GameObject.Find("Course Target Label"), Is.Null);

            voyage.Map.Nodes.Clear();
            voyage.Map.Nodes.Add(boss);
            boss.Position = node.Position;
            boss.Discovered = true;
            boss.Visited = false;
            ActiveButton("Heading East").onClick.Invoke();
            yield return null;
            Button bossMarker = ActiveButton("Event Node " + boss.Id);
            Assert.That(bossMarker.IsInteractable(), Is.True);
            Assert.That(bossMarker.GetComponentInChildren<Text>().text, Is.EqualTo("首"));
            Assert.That(GameObject.Find("Event Label " + boss.Id), Is.Null);
            yield return ClickDestinationTestGraphic(bossMarker.gameObject, Vector2.zero);
            AssertDestinationTestPoint(voyage.DirectCourseTarget, boss.Position);
            Assert.That(GameObject.Find("Event Label " + boss.Id), Is.Null);
            Assert.That(voyage.ActiveEncounter, Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DestinationPort_NearbyMapClickSnapsToNearestPortOrEvent()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            controller.Session.SetSeed("PORT-NEARBY-SNAP");
            ActiveButton("Port Hotspot Voyage").onClick.Invoke();
            yield return null;
            ActiveButton("Start Voyage").onClick.Invoke();
            yield return null;

            CoreVoyageSession voyage = controller.Session;
            CoreMapNode node = voyage.Map.Nodes.First(candidate => candidate.Encounter.HasChartLocation &&
                !candidate.IsBoss && candidate.Encounter.BossKind == CoreBossKind.None);
            voyage.Map.Nodes.Clear();
            System.Array.Clear(voyage.Map.TerrainCells, 0, voyage.Map.CellCount);
            SetDestinationTestPosition(voyage, new VoyagePoint(voyage.Map.Destination.X - 4f, voyage.Map.Destination.Y - 3f));
            ActiveButton("Heading East").onClick.Invoke();
            yield return null;

            Vector2 portLocal = DestinationTestMapPoint(voyage, voyage.Map.Destination);
            // Forty pixels is beyond the icon's 33-pixel half width, inside the
            // same 58-pixel map snapping radius used by event markers.
            yield return ClickDestinationTestGraphic(GameObject.Find("Map Course Input"), portLocal + new Vector2(-40f, 0f));
            AssertDestinationTestPoint(voyage.DirectCourseTarget, voyage.Map.Destination);
            Assert.That(GameObject.Find("Course Target Label"), Is.Null);
            Assert.That(voyage.BossDefeated, Is.False);

            node.Position = new VoyagePoint(voyage.Map.Destination.X - 6f, voyage.Map.Destination.Y + 2f);
            node.Discovered = true;
            node.Visited = false;
            voyage.Map.Nodes.Add(node);
            ActiveButton("Heading East").onClick.Invoke();
            yield return null;
            // Both markers are in snapping range, and neither icon is hit.
            // The nearest candidate must win in either direction.
            yield return ClickDestinationTestGraphic(GameObject.Find("Map Course Input"), portLocal + new Vector2(-43f, 15f));
            AssertDestinationTestPoint(voyage.DirectCourseTarget, node.Position);
            yield return ClickDestinationTestGraphic(GameObject.Find("Map Course Input"), portLocal + new Vector2(-40f, -20f));
            AssertDestinationTestPoint(voyage.DirectCourseTarget, voyage.Map.Destination);
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator WaitForNavigationMotionCompletion(CoreGameplayDemoController controller)
        {
            FieldInfo motion = typeof(CoreGameplayDemoController).GetField("navigationMotionPlaying",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(motion, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + 5f;
            while ((bool)motion.GetValue(controller) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That((bool)motion.GetValue(controller), Is.False,
                "Navigation must finish its animation and refresh the dashboard before the next action.");
            yield return null;
        }

        private static IEnumerator ClickDestinationTestGraphic(GameObject target, Vector2 localOffset)
        {
            Assert.That(target, Is.Not.Null);
            yield return null;
            Canvas.ForceUpdateCanvases();
            RectTransform rect = target.GetComponent<RectTransform>();
            NavigationMapViewport viewport = rect.GetComponentInParent<NavigationMapViewport>();
            if (viewport != null)
            {
                // A square-cell chart may crop distant ports or event markers.
                // Pan through the real viewport before testing their pointer hit.
                RectTransform map = (RectTransform)viewport.transform;
                Vector2 mapPoint = map.InverseTransformPoint(rect.TransformPoint(rect.rect.center + localOffset));
                viewport.EnsureVisible(mapPoint);
                yield return null;
                Canvas.ForceUpdateCanvases();
            }
            Canvas canvas = target.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            PointerEventData pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center + localOffset)),
            };
            List<RaycastResult> hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, target.name + " must accept a pointer hit.");
            GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(handler, Is.SameAs(target), target.name + " was blocked by " + hits[0].gameObject.name);
            pointer.pointerPressRaycast = hits[0];
            pointer.pointerCurrentRaycast = hits[0];
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        private static Vector2 DestinationTestMapPoint(CoreVoyageSession voyage, VoyagePoint point)
        {
            RectTransform sea = GameObject.Find("Ocean Tile Layer").GetComponent<RectTransform>();
            RectTransform input = GameObject.Find("Map Course Input").GetComponent<RectTransform>();
            Vector2 seaPoint = new Vector2(
                Mathf.Lerp(sea.rect.xMin, sea.rect.xMax, point.X / voyage.Map.WorldWidth),
                Mathf.Lerp(sea.rect.yMin, sea.rect.yMax, point.Y / voyage.Map.WorldHeight));
            return (Vector2)input.InverseTransformPoint(sea.TransformPoint(seaPoint)) - input.rect.center;
        }

        private static void SetDestinationTestPosition(CoreVoyageSession voyage, VoyagePoint point)
        {
            typeof(CoreVoyageSession).GetProperty("Position").SetValue(voyage, point);
        }

        private static void SetDestinationTestField(CoreVoyageSession voyage, string name, bool value)
        {
            FieldInfo field = typeof(CoreVoyageSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(voyage, value);
        }

        private static void AssertDestinationTestPoint(VoyagePoint actual, VoyagePoint expected)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(0.001f));
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(0.001f));
        }
    }
}
