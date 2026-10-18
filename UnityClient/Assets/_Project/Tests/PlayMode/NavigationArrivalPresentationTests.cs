using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        public IEnumerator NavigationArrival_GuidedSelectionKeepsItsInitialTint()
        {
            yield return LoadGuidedTutorialScene(enterDeparture: true);
            ClickGuidedButton("Start Voyage");
            AssertNavigationGuidedSelectionTint("Heading East");
            ClickGuidedButton("Heading East");
            AssertNavigationGuidedSelectionTint("Execute Sail");
            yield return null;
            AssertNavigationGuidedSelectionTint("Execute Sail");
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertNavigationGuidedSelectionTint(string name)
        {
            Button control = RequireGuidedButton(name);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(control.gameObject));
            Assert.That(control.IsInteractable(), Is.True);
            Assert.That(control.targetGraphic.canvasRenderer.GetColor(),
                Is.EqualTo(control.colors.selectedColor * control.colors.colorMultiplier),
                "The lesson's keyboard target must keep its selected appearance from the first frame.");
        }

        [UnityTest]
        public IEnumerator NavigationArrival_OrdinaryStopKeepsDashboardOpaqueAndVisuallySettled()
        {
            yield return LoadNavigationArrivalFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            var audit = new NavigationArrivalAudit();
            NavigationArrivalFrame before = ObserveNavigationArrival(controller, "before sailing");
            ActiveButton("Execute Sail").onClick.Invoke();
            Assert.That(NavigationMotionIsPlaying(controller), Is.True, "Exercise real sailing animation.");
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            var committed = new NavigationArrivalCosts(session);
            yield return ObserveNavigationArrivalCompletion(controller, audit);
            committed.AssertUnchanged(session);
            Assert.That(ActiveButton("Execute Sail").IsInteractable(), Is.True);
            Assert.That(GameObject.Find("Navigation Motion Input Guard"), Is.Null);
            WriteNavigationArrivalAudit("ordinary-stop", audit);
            AssertNavigationArrivalContinuity(before, audit);
            NavalBattleVisualCapture.Capture(controller, "Navigation_Arrival_Ordinary_Settled.png",
                "普通航行停止后，航海壳层不闪烁；首次帧属性另见 arrival JSON", "Navigation Chart");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavigationArrival_EventAndResolutionKeepDashboardOpaqueAndCostsSingle()
        {
            yield return LoadNavigationArrivalFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            var encounter = new CoreEncounter("navigation-arrival-probe", "抵达检查", "事件",
                "确认船只已抵达航点，然后继续航行。", true, false, 0, 0,
                new CoreEncounterChoice("继续航行", "确认抵达，不消耗时间与物资", 0d, new VoyageEffect()));
            session.Map.Nodes.Add(new CoreMapNode
            {
                Id = 991, Position = session.GetForecast().Target, Encounter = encounter,
                Discovered = true, TerrainConstraint = EventTerrainConstraint.OpenSea
            });
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
            yield return null; yield return null;
            var audit = new NavigationArrivalAudit();
            NavigationArrivalFrame before = ObserveNavigationArrival(controller, "before event sailing");
            ActiveButton("Execute Sail").onClick.Invoke();
            Assert.That(NavigationMotionIsPlaying(controller), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Encounter), "The actual route must collide with the event node.");
            Assert.That(session.ActiveEncounter, Is.SameAs(encounter));
            var committed = new NavigationArrivalCosts(session);
            yield return ObserveNavigationArrivalCompletion(controller, audit);
            committed.AssertUnchanged(session);
            Assert.That(GameObject.Find("Encounter Card"), Is.Not.Null);
            Assert.That(GameObject.Find("Encounter Title").GetComponent<Text>().text, Is.EqualTo(encounter.Name));
            Assert.That(ActiveButton("Execute Sail").IsInteractable(), Is.False);
            Assert.That(ActiveButton("Heading South").IsInteractable(), Is.False);
            Assert.That(ActiveButton("Safe Return").IsInteractable(), Is.False);
            Assert.That(GameObject.Find("Distance Slider").GetComponent<Slider>().IsInteractable(), Is.False);
            ActiveButton("Execute Sail").onClick.Invoke();
            ActiveButton("Heading South").onClick.Invoke();
            ActiveButton("Safe Return").onClick.Invoke();
            committed.AssertUnchanged(session);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            WriteNavigationArrivalAudit("event-arrival", audit);
            NavalBattleVisualCapture.Capture(controller, "Navigation_Arrival_Event_Settled.png",
                "真实航行触发事件后，海图和航行壳层保持显示", "Navigation Chart");

            var resolutionAudit = new NavigationArrivalAudit();
            NavigationArrivalFrame beforeResolution = ObserveNavigationArrival(controller, "before resolution");
            ActiveButton("Encounter Choice 0").onClick.Invoke();
            resolutionAudit.frames.Add(ObserveNavigationArrival(controller, "first resolved frame"));
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            yield return ObserveNavigationArrivalSettling(controller, resolutionAudit);
            committed.AssertUnchanged(session);
            Assert.That(GameObject.Find("Encounter Card"), Is.Null);
            Assert.That(ActiveButton("Execute Sail").IsInteractable(), Is.True);
            Assert.That(ActiveButton("Heading South").IsInteractable(), Is.True);
            Assert.That(GameObject.Find("Distance Slider").GetComponent<Slider>().IsInteractable(), Is.True);
            WriteNavigationArrivalAudit("event-resolution", resolutionAudit);
            AssertNavigationArrivalContinuity(before, audit, true);
            AssertNavigationArrivalContinuity(beforeResolution, resolutionAudit, false);
            NavalBattleVisualCapture.Capture(controller, "Navigation_Arrival_Resolution_Settled.png",
                "事件处理结束后立即恢复航行，不重扣航程、水粮或时间", "Navigation Chart");
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator LoadNavigationArrivalFixture()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            session.SetSeed("NAVIGATION-ARRIVAL-PRESENTATION");
            session.Cargo.Water = session.Cargo.Food = 200;
            session.StartVoyage();
            session.Map.Nodes.Clear();
            Array.Clear(session.Map.TerrainCells, 0, session.Map.CellCount);
            typeof(CoreVoyageSession).GetProperty("Position").SetValue(session, new VoyagePoint(30f, 32f));
            typeof(CoreVoyageSession).GetField("randomAmbushOccurred", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(session, true);
            session.SetHeading(SailingDirection.East);
            session.SetDistance(20);
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
            // Initial entry from the port is allowed to reveal; arrival within
            // the already visible navigation dashboard must stay fully opaque.
            yield return new WaitForSecondsRealtime(.4f);
            yield return null;
        }

        private static IEnumerator ObserveNavigationArrivalCompletion(CoreGameplayDemoController controller, NavigationArrivalAudit audit)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (NavigationMotionIsPlaying(controller) && Time.realtimeSinceStartup < deadline)
            {
                audit.frames.Add(ObserveNavigationArrival(controller, "sailing"));
                yield return null;
            }
            Assert.That(NavigationMotionIsPlaying(controller), Is.False, "The sailing animation must finish.");
            audit.frames.Add(ObserveNavigationArrival(controller, "first stopped frame"));
            yield return ObserveNavigationArrivalSettling(controller, audit);
        }

        private static IEnumerator ObserveNavigationArrivalSettling(CoreGameplayDemoController controller, NavigationArrivalAudit audit)
        {
            // Observe without Canvas.ForceUpdateCanvases or screenshot setup:
            // either would repair the first frame and hide the reported flicker.
            float deadline = Time.realtimeSinceStartup + .4f;
            do
            {
                yield return null;
                audit.frames.Add(ObserveNavigationArrival(controller, "settling"));
            } while (Time.realtimeSinceStartup < deadline);
        }

        [Serializable]
        private sealed class NavigationArrivalAudit
        {
            public string scope = "Actual UI callback; frame samples do not force layout. Screenshots, if enabled separately, are settled captures.";
            public List<NavigationArrivalFrame> frames = new List<NavigationArrivalFrame>();
        }

        [Serializable]
        private sealed class NavigationArrivalFrame
        {
            public string stage;
            public int frame;
            public float seconds;
            public float opacity;
            public List<NavigationArrivalSurface> surfaces = new List<NavigationArrivalSurface>();
            public List<NavigationArrivalTint> buttonTints = new List<NavigationArrivalTint>();
        }

        [Serializable]
        private sealed class NavigationArrivalSurface
        {
            public string name;
            public int activeCount;
            public Rect worldRect;
            public Color color;
            public int spriteId;
            public int materialId;
            public int meshVertices;
            public bool culled;
        }

        [Serializable]
        private sealed class NavigationArrivalTint
        {
            public string name;
            public Color color;
        }

        private static NavigationArrivalFrame ObserveNavigationArrival(CoreGameplayDemoController controller, string stage)
        {
            Canvas canvas = controller.transform.Find("Core Gameplay Canvas").GetComponent<Canvas>();
            Transform content = FixedGameViewport.Bounds(canvas).Find("Phase Content");
            Assert.That(content, Is.Not.Null);
            var sample = new NavigationArrivalFrame { stage = stage, frame = Time.frameCount, seconds = Time.realtimeSinceStartup, opacity = 1f };
            foreach (CanvasGroup group in content.GetComponentsInParent<CanvasGroup>()) sample.opacity *= group.alpha;
            foreach (string name in new[] { "Navigation Chart", "Navigation Controls", "Forward View Panel" })
            {
                var surface = new NavigationArrivalSurface { name = name };
                // Inactive predecessors can coexist for one frame until Destroy;
                // they do not contribute a duplicate visible panel.
                foreach (Image candidate in content.GetComponentsInChildren<Image>())
                {
                    if (candidate.name != name || !candidate.gameObject.activeInHierarchy) continue;
                    surface.activeCount++;
                    Vector3[] corners = new Vector3[4];
                    candidate.rectTransform.GetWorldCorners(corners);
                    surface.worldRect = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
                    surface.color = candidate.color;
                    surface.spriteId = candidate.sprite == null ? 0 : candidate.sprite.GetInstanceID();
                    surface.materialId = candidate.material == null ? 0 : candidate.material.GetInstanceID();
                    Mesh mesh = candidate.canvasRenderer.GetMesh();
                    surface.meshVertices = mesh == null ? 0 : mesh.vertexCount;
                    surface.culled = candidate.canvasRenderer.cull;
                }
                sample.surfaces.Add(surface);
            }
            foreach (string name in new[] { "Execute Sail", "Forecast Details", "Safe Return", "Heading South", "Heading East", "Voyage Settings", "Voyage Cargo" })
            {
                Button button = ActiveButton(name);
                if (button != null && button.targetGraphic != null)
                    sample.buttonTints.Add(new NavigationArrivalTint { name = name, color = button.targetGraphic.canvasRenderer.GetColor() });
            }
            return sample;
        }

        private static void AssertNavigationArrivalContinuity(NavigationArrivalFrame before, NavigationArrivalAudit audit,
            bool? encounterAfterArrival = null)
        {
            Assert.That(audit.frames.Count, Is.GreaterThan(1));
            foreach (NavigationArrivalFrame frame in audit.frames)
            {
                string context = frame.stage + " / frame " + frame.frame;
                Assert.That(frame.opacity, Is.EqualTo(1f).Within(.0001f), context + ": the existing navigation surface must never fade out.");
                for (int index = 0; index < frame.surfaces.Count; index++)
                {
                    NavigationArrivalSurface actual = frame.surfaces[index], expected = before.surfaces[index];
                    Rect expectedRect = expected.worldRect;
                    if (encounterAfterArrival.HasValue && actual.name == "Forward View Panel" && frame.stage != "sailing")
                    {
                        // The journal intentionally occupies the entire right column.
                        // Only the first stopped/resolved visual frame changes its bounds;
                        // model Phase changes earlier, while the voyage is still animating.
                        Rect controls = before.surfaces.Find(surface => surface.name == "Navigation Controls").worldRect;
                        float bottom = encounterAfterArrival.Value ? controls.yMin : controls.yMax;
                        expectedRect = Rect.MinMaxRect(expectedRect.xMin, bottom, expectedRect.xMax, expectedRect.yMax);
                    }
                    Assert.That(actual.activeCount, Is.EqualTo(1), context + ": visible copies of " + actual.name);
                    Assert.That(actual.worldRect.x, Is.EqualTo(expectedRect.x).Within(.1f), context + ": panel x");
                    Assert.That(actual.worldRect.y, Is.EqualTo(expectedRect.y).Within(.1f), context + ": panel y");
                    Assert.That(actual.worldRect.width, Is.EqualTo(expectedRect.width).Within(.1f), context + ": panel width");
                    Assert.That(actual.worldRect.height, Is.EqualTo(expectedRect.height).Within(.1f), context + ": panel height");
                    Assert.That(actual.color, Is.EqualTo(expected.color), context + ": panel color");
                    Assert.That(actual.spriteId, Is.EqualTo(expected.spriteId), context + ": panel sprite");
                    Assert.That(actual.materialId, Is.EqualTo(expected.materialId), context + ": panel material");
                }
            }
            NavigationArrivalFrame first = audit.frames.Find(frame => frame.stage.StartsWith("first", StringComparison.Ordinal));
            NavigationArrivalFrame settled = audit.frames[audit.frames.Count - 1];
            Assert.That(first, Is.Not.Null);
            foreach (NavigationArrivalTint initial in first.buttonTints)
            {
                NavigationArrivalTint final = settled.buttonTints.Find(tint => tint.name == initial.name);
                Assert.That(final, Is.Not.Null);
                Assert.That(Mathf.Abs(initial.color.r - final.color.r) + Mathf.Abs(initial.color.g - final.color.g) +
                    Mathf.Abs(initial.color.b - final.color.b) + Mathf.Abs(initial.color.a - final.color.a),
                    Is.LessThan(.01f), initial.name + ": a rebuilt control must have its final tint on the first arrival frame.");
            }
        }

        private static void WriteNavigationArrivalAudit(string scenario, NavigationArrivalAudit audit)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-navArrivalAuditPath");
            if (index < 0) return;
            if (index + 1 >= args.Length) throw new ArgumentException("-navArrivalAuditPath needs an Artifacts directory.");
            string directory = Path.GetFullPath(args[index + 1]);
            bool underArtifacts = false;
            foreach (string segment in directory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                underArtifacts |= string.Equals(segment, "Artifacts", StringComparison.OrdinalIgnoreCase);
            if (!underArtifacts) throw new ArgumentException("Arrival audit output must be underneath Artifacts.");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, scenario + ".json");
            using (var writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)))
                writer.Write(JsonUtility.ToJson(audit, true));
        }

        private sealed class NavigationArrivalCosts
        {
            private readonly double hours;
            private readonly float distance;
            private readonly int water, food;
            private readonly VoyagePoint position;

            public NavigationArrivalCosts(CoreVoyageSession session)
            {
                hours = session.TotalHours; distance = session.TotalDistanceNm;
                water = session.Cargo.Water; food = session.Cargo.Food; position = session.Position;
            }

            public void AssertUnchanged(CoreVoyageSession session)
            {
                Assert.That(session.TotalHours, Is.EqualTo(hours), "Presentation must not recommit elapsed time.");
                Assert.That(session.TotalDistanceNm, Is.EqualTo(distance), "Presentation must not recommit distance.");
                Assert.That(session.Cargo.Water, Is.EqualTo(water));
                Assert.That(session.Cargo.Food, Is.EqualTo(food));
                Assert.That(session.Position.X, Is.EqualTo(position.X));
                Assert.That(session.Position.Y, Is.EqualTo(position.Y));
            }
        }
    }
}

