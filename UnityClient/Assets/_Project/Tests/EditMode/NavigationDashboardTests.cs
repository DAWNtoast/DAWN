using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Core.Tests
{
    public sealed class NavigationDashboardTests
    {
        private GameObject host;
        private CoreGameplayDemoController controller;
        private int? previousReadingLevel;
        [SetUp] public void PrepareReadingSize()
        {
            previousReadingLevel = GameReadability.OverrideLevel;
            GameReadability.OverrideLevel = 0;
        }
        [TearDown] public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
            GameReadability.OverrideLevel = previousReadingLevel;
        }
        private void Create(CoreGameplayPreviewScreen screen = CoreGameplayPreviewScreen.Navigation)
        {
            host = new GameObject("Navigation Dashboard Test"); host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(screen, CoreGameplayPreviewPort.GrayGull, 0); host.SetActive(true);
            Canvas.ForceUpdateCanvases();
        }
        private T Find<T>(string name) where T : Component => host.GetComponentsInChildren<T>(true).Single(x => x.name == name);
        private void Render() => typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
        private static void Set(object target, string name, object value) => target.GetType().GetProperty(name).GetSetMethod(true).Invoke(target, new[] { value });

        [Test]
        public void ForecastDisclosure_PreservesCourseAndRefreshesDetailsWhileDragging()
        {
            Create();
            Button toggle = Find<Button>("Forecast Details");
            RectTransform details = Find<RectTransform>("Forecast Details Panel");
            Assert.That(details.gameObject.activeSelf, Is.False);
            Assert.That(Find<Text>("Forecast").text, Does.Contain("预计").And.Contain("航速").And.Contain("淡水").And.Contain("食物"));
            Assert.That(Find<Text>("Forecast").text, Does.Not.Contain("洋流").And.Not.Contain("海风"));
            var position = controller.Session.Position;
            double hours = controller.Session.ElapsedGameHours;
            int planned = controller.Session.PlannedDistanceNm;
            toggle.onClick.Invoke();
            Assert.That(details.gameObject.activeSelf, Is.True);
            Assert.That(Find<Text>("Forecast Detail Text").text, Does.Contain("洋流").And.Contain("海风"));
            Assert.That(controller.Session.PlannedDistanceNm, Is.EqualTo(planned));
            Find<Slider>("Distance Slider").value = 170;
            Assert.That(Find<RectTransform>("Forecast Details Panel"), Is.SameAs(details));
            Assert.That(Find<Text>("Forecast").text, Does.Contain(controller.Session.GetForecast().Hours.ToString("0.0")));
            toggle.onClick.Invoke();
            Assert.That(details.gameObject.activeSelf, Is.False);
            Assert.That(controller.Session.Position.X, Is.EqualTo(position.X));
            Assert.That(controller.Session.Position.Y, Is.EqualTo(position.Y));
            Assert.That(controller.Session.ElapsedGameHours, Is.EqualTo(hours));
        }

        [TestCase(1920,1080,0)]
        [TestCase(1920,1080,1)]
        [TestCase(1920,1080,2)]
        [TestCase(1280,720,0)]
        [TestCase(1280,720,1)]
        [TestCase(1280,720,2)]
        [TestCase(1280,1024,0)]
        [TestCase(1280,1024,1)]
        [TestCase(1280,1024,2)]
        public void CompactNavigation_KeepsControlsLegibleAndGivesMostSpaceToScenery(int width, int height, int level)
        {
            GameReadability.OverrideLevel = level;
            Create();
            using (var viewport = new DashboardViewport(host))
            {
                viewport.ResizeAndRender(width,height);
                RectTransform root = viewport.Canvas.GetComponent<RectTransform>();
                Rect chart = RectIn(root,Find<RectTransform>("Navigation Chart"));
                Rect controls = RectIn(root,Find<RectTransform>("Navigation Controls"));
                Rect lookout = RectIn(root,Find<RectTransform>("Forward View Panel"));
                Rect journal = RectIn(root,Find<RectTransform>("Navigation Journal"));
                Assert.That(chart.height, Is.GreaterThan(controls.height), "The sea chart keeps the majority of its column.");
                Assert.That(lookout.height, Is.GreaterThan(journal.height), "The forward view keeps the majority of its column.");
                string[] controlsToRead = { "Distance Label", "Forecast", "Forecast Details", "Execute Sail", "Safe Return" };
                Rect[] boxes = controlsToRead.Select(name => RectIn(root,Find<RectTransform>(name))).ToArray();
                for (int i=0;i<boxes.Length;i++)
                {
                    Assert.That(boxes[i].xMin,Is.GreaterThanOrEqualTo(controls.xMin),controlsToRead[i]);
                    Assert.That(boxes[i].xMax,Is.LessThanOrEqualTo(controls.xMax),controlsToRead[i]);
                    Assert.That(boxes[i].yMin,Is.GreaterThanOrEqualTo(controls.yMin),controlsToRead[i]);
                    Assert.That(boxes[i].yMax,Is.LessThanOrEqualTo(controls.yMax),controlsToRead[i]);
                    for(int j=i+1;j<boxes.Length;j++) Assert.That(boxes[i].Overlaps(boxes[j]),Is.False,controlsToRead[i]+" / "+controlsToRead[j]);
                }
                foreach(string name in new[]{"Forecast","Lookout Report","Water Food Endurance","Supply Endurance Summary"})
                {
                    Text text = Find<Text>(name);
                    Assert.That(text.preferredHeight,Is.LessThanOrEqualTo(text.rectTransform.rect.height+1f),name);
                    Assert.That(text.resizeTextForBestFit, Is.False, name + " must keep the selected reading size.");
                }
                foreach (string name in new[] { "Map Title", "Navigation Title", "Log Title", "Lookout Heading" })
                {
                    Text title = Find<Text>(name);
                    Assert.That(title.fontSize, Is.EqualTo(GameReadability.FontSize(32)), name);
                    Assert.That(title.font, Is.SameAs(GameTypography.InterfaceFont), name);
                    Assert.That(title.preferredHeight, Is.LessThanOrEqualTo(title.rectTransform.rect.height + 1f), name);
                }
                ScrollRect logScroll = Find<ScrollRect>("Voyage Journal Scroll");
                Text log = Find<Text>("Voyage Log");
                Assert.That(log.fontSize, Is.EqualTo(GameReadability.FontSize(22)));
                Assert.That(logScroll.viewport.rect.height,
                    Is.GreaterThanOrEqualTo(log.fontSize * log.lineSpacing * 3 + 8),
                    "The journal must expose at least three complete lines at every reading size.");
                Rect logBounds = RectIn(Find<RectTransform>("Navigation Journal"), logScroll.viewport);
                foreach (string name in new[] { "Log Title", "Voyage Cargo Summary", "Cargo Status", "Voyage Supply Endurance" })
                    Assert.That(logBounds.Overlaps(RectIn(Find<RectTransform>("Navigation Journal"), Find<RectTransform>(name))),
                        Is.False, "Journal text must not overlap " + name);
                Find<Button>("Forecast Details").onClick.Invoke();
                viewport.ResizeAndRender(width,height);
                Rect disclosure = RectIn(root,Find<RectTransform>("Forecast Details Panel"));
                Assert.That(disclosure.yMin,Is.GreaterThanOrEqualTo(controls.yMax-.1f));
                Assert.That(disclosure.yMax,Is.LessThan(chart.yMax));
            }
        }

        [Test]
        public void PortNameplates_KeepLandmarkTargetsAndEmphasizeOnlyTheHoveredSign()
        {
            Create(CoreGameplayPreviewScreen.PortHub);
            using(var viewport = new DashboardViewport(host))
            {
                viewport.ResizeAndRender(1920,1080);
                foreach(string key in new[]{"Warehouse","Shipyard","Tavern","ComponentShop","CardWorkshop","Dispatch","Treasure","Maintenance","Voyage","Market"})
                {
                    Button button = Find<Button>("Port Hotspot "+key);
                    Image sign = Find<Image>("Port Sign "+key);
                    Text label = Find<Text>("Port Label "+key);
                    Assert.That(sign.sprite,Is.SameAs(WoodButtonSkin.Face));
                    Assert.That(sign.raycastTarget,Is.True);
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(viewport.Camera,sign.transform.position);
                    var data = new PointerEventData(viewport.Events){position=point};
                    var hits = new System.Collections.Generic.List<RaycastResult>();
                    viewport.Canvas.GetComponent<GraphicRaycaster>().Raycast(data,hits);
                    Assert.That(hits.Count,Is.GreaterThan(0),key);
                    Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),Is.SameAs(button.gameObject),key);
                    Color before = sign.color;
                    ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerEnterHandler);
                    Assert.That(sign.color,Is.Not.EqualTo(before),key);
                    ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerExitHandler);
                    Assert.That(sign.color,Is.EqualTo(before),key);
                    Assert.That(label.preferredHeight,Is.LessThanOrEqualTo(label.rectTransform.rect.height+1f),key);
                }
            }
        }

        [TestCase(0d, "已耗尽")]
        [TestCase(0.5d, "不足 1 小时")]
        [TestCase(1d, "约 1 小时")]
        [TestCase(23.9d, "约 23 小时")]
        [TestCase(24d, "约 1 天 0 小时")]
        [TestCase(49.9d, "约 2 天 1 小时")]
        [TestCase(double.PositiveInfinity, "无需消耗")]
        public void SupplyDuration_DoesNotOverstateTimeOrShowInvalidNumbers(double hours, string expected)
        {
            var formatter = typeof(CoreGameplayDemoController).GetMethod("FormatSupplyEnduranceDuration",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(formatter.Invoke(null, new object[] { hours }), Is.EqualTo(expected));
        }

        [TestCase(CoreGameplayPreviewScreen.Navigation)]
        [TestCase(CoreGameplayPreviewScreen.Encounter)]
        public void Dashboard_HasFourNonOverlappingRegions_WithChartAspectAndInputPreserved(CoreGameplayPreviewScreen screen)
        {
            Create(screen);
            string[] names = { "Navigation Chart", "Navigation Controls", "Forward View Panel", "Navigation Journal" };
            Assert.That(Find<ScrollRect>("Voyage Journal Scroll").vertical, Is.True);
            Assert.That(Find<Text>("Voyage Log").fontSize, Is.GreaterThanOrEqualTo(19));
            foreach (string label in new[] { "Map Title", "Map Seed", "Forecast", "Distance Label", "Cargo Status", "Voyage Log" })
                Assert.That(Find<Text>(label).color.grayscale, Is.LessThan(0.4f), label + " must use dark ink on parchment.");
            foreach (string key in new[] { "sea", "island", "reef", "storm", "fog", "pirate" })
                Assert.That(Resources.Load<Texture2D>("NavigationLookout/lookout_" + key + "_v1"), Is.Not.Null, key);
            using (var viewport = new DashboardViewport(host))
            {
                // Moving an ExecuteAlways host into its preview scene can rebuild
                // its preview. Record identities after that setup, before any resize.
                var panels = names.Select(Find<RectTransform>).ToArray();
                var map = Find<RectTransform>("Map Layer");
                MapCourseInputSurface input = Find<MapCourseInputSurface>("Map Course Input");
                CoreVoyageSession session = controller.Session;
                Slider distance = Find<Slider>("Distance Slider");
                // Reuse the same live hierarchy, including a final return to 1080p.
                // No controller Render/Apply call may conceal a stale resize layout.
                foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720),
                    new Vector2Int(1280, 1024), new Vector2Int(3440, 1440), new Vector2Int(3840, 1080),
                    new Vector2Int(1920, 1080) })
                {
                    viewport.ResizeAndRender(size.x, size.y);
                    string context = size.x + "x" + size.y;
                    Assert.That(controller.Session, Is.SameAs(session), context);
                    Assert.That(Find<Slider>("Distance Slider"), Is.SameAs(distance), context);
                    for (int index = 0; index < panels.Length; index++)
                        Assert.That(Find<RectTransform>(names[index]), Is.SameAs(panels[index]), context);
                    Assert.That(Find<RectTransform>("Map Layer"), Is.SameAs(map), context);
                    Assert.That(Find<MapCourseInputSurface>("Map Course Input"), Is.SameAs(input), context);
                    AssertDashboardCoverage(viewport.Canvas, panels, Find<RectTransform>("Voyage Header"), context);
                    if (screen == CoreGameplayPreviewScreen.Navigation)
                    {
                        Assert.That(Find<RawImage>("Lookout Ocean Transition").rectTransform.sizeDelta,
                            Is.EqualTo(Find<RectTransform>("Forward Viewport").sizeDelta), context + " panorama blend must fill the resized lookout");
                        AssertPanoramaPixelAspectAndHorizon(context);
                    }
                    else
                        Assert.That(Find<RectTransform>("Encounter Card").sizeDelta,
                            Is.EqualTo(Find<RectTransform>("Forward Viewport").sizeDelta), context + " encounter card replaces the lookout and must fill its resized viewport");
                    AssertVoyageHeaderControlsAndTime(context);
                    AssertMapGeometryAndPointerCoordinates(viewport, map, input, context);
                }
            }
        }

        private void AssertPanoramaPixelAspectAndHorizon(string context)
        {
            var ocean = Find<RawImage>("Lookout Ocean");
            var transition = Find<RawImage>("Lookout Ocean Transition");
            Assert.That(transition.uvRect, Is.EqualTo(ocean.uvRect), context + " both blend layers must crop together");
            foreach (RawImage image in new[] { ocean, transition })
            {
                Rect uv = image.uvRect;
                Assert.That(uv.xMin, Is.EqualTo(0), context + " left sky remains visible");
                Assert.That(uv.yMin, Is.GreaterThanOrEqualTo(0), context);
                Assert.That(uv.xMax, Is.LessThanOrEqualTo(1), context);
                Assert.That(uv.yMax, Is.LessThanOrEqualTo(1), context);
                var corners = new Vector3[4];
                image.rectTransform.GetWorldCorners(corners);
                float pixelWidth = Vector3.Distance(corners[0], corners[3]) / (image.texture.width * uv.width);
                float pixelHeight = Vector3.Distance(corners[0], corners[1]) / (image.texture.height * uv.height);
                Assert.That(pixelWidth / pixelHeight, Is.EqualTo(1f).Within(.0001f), context + " panorama pixels must stay square after actual canvas resize");
                Assert.That((.612f - uv.yMin) / uv.height, Is.EqualTo(.612f).Within(.0001f), context + " painted horizon stays aligned with scenery");
            }
        }

        private void AssertVoyageHeaderControlsAndTime(string context)
        {
            RectTransform header = Find<RectTransform>("Voyage Header");
            string[] names = { "Voyage Header Title", "Voyage Header Status", "Lookout Time Ribbon", "Voyage Cargo", "Voyage Settings" };
            Rect[] controls = names.Select(name => RectIn(header, Find<RectTransform>(name))).ToArray();
            for (int index = 0; index < names.Length; index++)
            {
                string name = names[index];
                RectTransform control = Find<RectTransform>(name);
                Assert.That(control.parent, Is.SameAs(header), context + " " + name);
                Rect bounds = controls[index];
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(header.rect.xMin), context + " " + name);
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(header.rect.xMax), context + " " + name);
                Assert.That(bounds.yMin, Is.GreaterThan(-header.rect.height * .5f), context + " header bottom");
                Assert.That(bounds.yMax, Is.LessThan(header.rect.height * .5f), context + " header top");
                for (int other = index + 1; other < controls.Length; other++)
                    Assert.That(bounds.Overlaps(controls[other]), Is.False, context + " " + name + " / " + names[other]);
            }
            Rect title = controls[0], status = controls[1], clock = controls[2], cargo = controls[3], settings = controls[4];
            Assert.That(title.xMin - header.rect.xMin, Is.EqualTo(48f).Within(.01f), context + " left information margin");
            Assert.That(title.xMin, Is.EqualTo(status.xMin).Within(.01f), context + " title/status left alignment");
            Assert.That(title.xMax, Is.EqualTo(status.xMax).Within(.01f), context + " title/status width alignment");
            Assert.That(title.yMin - status.yMax, Is.GreaterThanOrEqualTo(3.99f), context + " title above status with readable separation");
            Assert.That(clock.xMin - title.xMax, Is.GreaterThanOrEqualTo(11.99f), context + " information/clock separation");
            Assert.That(cargo.xMin - clock.xMax, Is.GreaterThanOrEqualTo(11.99f), context + " clock/action separation");
            Assert.That(clock.center.x, Is.EqualTo((title.xMax + cargo.xMin) * .5f).Within(.01f),
                context + " time centers in the space between information and actions");
            Assert.That(settings.xMin - cargo.xMax, Is.EqualTo(16f).Within(.01f), context + " neighboring cargo/settings actions");
            Assert.That(cargo.yMin, Is.EqualTo(settings.yMin).Within(.01f), context + " action bottom alignment");
            Assert.That(cargo.yMax, Is.EqualTo(settings.yMax).Within(.01f), context + " action top alignment");
            Assert.That(header.rect.xMax - settings.xMax, Is.EqualTo(30f).Within(.01f), context + " right settings margin");
            RectTransform ribbon = Find<RectTransform>("Lookout Time Ribbon");
            Assert.That(Find<RectTransform>("Forward Viewport").GetComponentsInChildren<VoyageTimeRibbonGraphic>(), Is.Empty,
                context + " time belongs only in the header, including during encounters");
            foreach (Graphic graphic in ribbon.GetComponentsInChildren<Graphic>())
                Assert.That(graphic.raycastTarget, Is.False, context + " time must not intercept header input");
            foreach (string name in new[] { "Lookout Time Readout", "Lookout Time Legend" })
            {
                Text label = Find<Text>(name);
                Rect bounds = RectIn(ribbon, label.rectTransform);
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(ribbon.rect.xMin - .01f), context + name);
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(ribbon.rect.xMax + .01f), context + name);
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(ribbon.rect.yMin - .01f), context + name);
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(ribbon.rect.yMax + .01f), context + name);
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .1f), context + name);
                Assert.That(label.cachedTextGenerator.characterCountVisible, Is.GreaterThanOrEqualTo(label.text.Length), context + name);
            }
            Assert.That(Find<Text>("Lookout Time Readout").text, Does.Contain(controller.Session.GameDateTime.ToString("yyyy-MM-dd HH:mm")), context);
            Assert.That(Find<VoyageTimeRibbonGraphic>("Lookout Time Track").LocalHour,
                Is.EqualTo(NavigationTimeOfDayUtility.LocalHour(controller.Session.ElapsedGameHours)).Within(.001d), context);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Dashboard_TutorialKeepsTallHeaderAndSeparatesBothLessonBandsFromLiveControls(int level)
        {
            GameReadability.OverrideLevel = level;
            Create();
            typeof(CoreGameplayDemoController).GetField("tutorialActive", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, true);
            Render();
            using (var viewport = new DashboardViewport(host))
            {
                // Preview-scene relocation can rebuild the controller, so set
                // the lesson flag after relocation as well as before rendering.
                typeof(CoreGameplayDemoController).GetField("tutorialActive", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, true);
                Render();
                foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720),
                    new Vector2Int(1280, 1024), new Vector2Int(3840, 1080) })
                {
                    viewport.ResizeAndRender(size.x, size.y);
                    string context = "Guided " + size;
                    RectTransform header = Find<RectTransform>("Voyage Header");
                    var panels = new[] { "Navigation Chart", "Navigation Controls", "Forward View Panel", "Navigation Journal" }
                        .Select(Find<RectTransform>).ToArray();
                    AssertDashboardCoverage(viewport.Canvas, panels, header, context, PhaseSurfaceLayout.GetHeaderHeight(true));
                    AssertVoyageHeaderControlsAndTime(context);
                    foreach (string lesson in new[] { "Guided Tutorial Objective", "Guided Tutorial Instruction" })
                    {
                        Rect label = RectIn(header, Find<RectTransform>(lesson));
                        Assert.That(label.yMin, Is.GreaterThanOrEqualTo(header.rect.yMin), context);
                        Assert.That(label.yMax, Is.LessThanOrEqualTo(header.rect.yMax), context);
                        foreach (string control in new[] { "Voyage Header Title", "Voyage Header Status", "Lookout Time Ribbon", "Voyage Cargo", "Voyage Settings" })
                            Assert.That(label.Overlaps(RectIn(header, Find<RectTransform>(control))), Is.False,
                                context + " " + lesson + " / " + control);
                    }
                }
            }
        }

        private static void AssertDashboardCoverage(Canvas canvas, RectTransform[] panels, RectTransform header, string context,
            float expectedHeaderHeight = PhaseSurfaceLayout.HeaderHeight)
        {
            RectTransform root = canvas.GetComponent<RectTransform>();
            Rect headerBounds = RectIn(root, header);
            Rect available = Rect.MinMaxRect(root.rect.xMin, root.rect.yMin, root.rect.xMax, headerBounds.yMin);
            const float tolerance = .01f;
            Assert.That(headerBounds.xMin, Is.EqualTo(root.rect.xMin).Within(tolerance), context + " header left");
            Assert.That(headerBounds.xMax, Is.EqualTo(root.rect.xMax).Within(tolerance), context + " header right");
            Assert.That(headerBounds.yMax, Is.EqualTo(root.rect.yMax).Within(tolerance), context + " header top");
            Assert.That(headerBounds.height, Is.EqualTo(expectedHeaderHeight).Within(tolerance), context);
            var bounds = panels.Select(panel => RectIn(root, panel)).ToArray();
            double area = 0d;
            for (int index = 0; index < panels.Length; index++)
            {
                Rect item = bounds[index];
                Assert.That(item.width, Is.GreaterThan(0f), context + panels[index].name);
                Assert.That(item.height, Is.GreaterThan(0f), context + panels[index].name);
                Assert.That(item.xMin, Is.GreaterThanOrEqualTo(available.xMin - tolerance), context);
                Assert.That(item.xMax, Is.LessThanOrEqualTo(available.xMax + tolerance), context);
                Assert.That(item.yMin, Is.GreaterThanOrEqualTo(available.yMin - tolerance), context);
                Assert.That(item.yMax, Is.LessThanOrEqualTo(available.yMax + tolerance), context);
                Assert.That(panels[index].localScale.x, Is.GreaterThan(0f), context);
                Assert.That(panels[index].localScale.y, Is.EqualTo(panels[index].localScale.x).Within(.00001f),
                    context + " " + panels[index].name + " must not stretch text or illustrations.");
                area += (double)item.width * item.height;
                for (int other = index + 1; other < bounds.Length; other++)
                {
                    float overlapWidth = Mathf.Min(item.xMax, bounds[other].xMax) - Mathf.Max(item.xMin, bounds[other].xMin);
                    float overlapHeight = Mathf.Min(item.yMax, bounds[other].yMax) - Mathf.Max(item.yMin, bounds[other].yMin);
                    Assert.That(overlapWidth > tolerance && overlapHeight > tolerance, Is.False,
                        context + " overlapping " + panels[index].name + " / " + panels[other].name);
                }
            }
            // Explicit seam checks reject gutters; area alone could conceal gaps paired with overlap.
            foreach (int index in new[] { 0, 1 })
            {
                Assert.That(bounds[index].xMin, Is.EqualTo(available.xMin).Within(tolerance), context + " left edge");
                Assert.That(bounds[index].xMax, Is.EqualTo(bounds[2].xMin).Within(tolerance), context + " vertical seam");
            }
            foreach (int index in new[] { 2, 3 })
            {
                Assert.That(bounds[index].xMax, Is.EqualTo(available.xMax).Within(tolerance), context + " right edge");
                Assert.That(bounds[index].xMin, Is.EqualTo(bounds[0].xMax).Within(tolerance), context + " vertical seam");
            }
            foreach (int index in new[] { 0, 2 })
                Assert.That(bounds[index].yMax, Is.EqualTo(available.yMax).Within(tolerance), context + " header seam");
            foreach (int index in new[] { 1, 3 })
                Assert.That(bounds[index].yMin, Is.EqualTo(available.yMin).Within(tolerance), context + " bottom edge");
            Assert.That(bounds[0].yMin, Is.EqualTo(bounds[1].yMax).Within(tolerance), context + " left horizontal seam");
            Assert.That(bounds[2].yMin, Is.EqualTo(bounds[3].yMax).Within(tolerance), context + " right horizontal seam");
            Assert.That(area, Is.EqualTo((double)available.width * available.height)
                .Within((available.width + available.height) * tolerance), context + " panels must cover all available area");
        }

        private static Rect RectIn(RectTransform root, RectTransform item)
        {
            var corners = new Vector3[4];
            item.GetWorldCorners(corners);
            Vector3[] points = corners.Select(root.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y),
                points.Max(point => point.x), points.Max(point => point.y));
        }

        private static void AssertMapGeometryAndPointerCoordinates(DashboardViewport viewport, RectTransform map,
            MapCourseInputSurface input, string context)
        {
            Assert.That(map.sizeDelta, Is.EqualTo(new Vector2(1200, 675)), context);
            Assert.That(map.localScale.x, Is.EqualTo(map.localScale.y).Within(.00001f), context);
            var corners = new Vector3[4];
            map.GetWorldCorners(corners);
            float width = Vector3.Distance(corners[0], corners[3]);
            float height = Vector3.Distance(corners[0], corners[1]);
            Assert.That(height, Is.GreaterThan(0f), context);
            Assert.That(width / height, Is.EqualTo(1200f / 675f).Within(.0001f),
                context + " map aspect must include every ancestor's scale");
            foreach (Vector3 corner in corners)
            {
                Vector3 pixel = viewport.Camera.WorldToScreenPoint(corner);
                Assert.That(pixel.x, Is.InRange(-.01f, viewport.Width + .01f), context + " map x");
                Assert.That(pixel.y, Is.InRange(-.01f, viewport.Height + .01f), context + " map y");
            }

            // Exercise the production screen-to-map conversion with its real event camera.
            // Temporarily observe the callback so resizing never changes the voyage or rebuilds UI.
            FieldInfo callback = typeof(MapCourseInputSurface).GetField("selectLocalPoint", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo target = typeof(MapCourseInputSurface).GetField("mapRect", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(target.GetValue(input), Is.SameAs(map), context + " bound map");
            var original = (System.Action<Vector2>)callback.GetValue(input);
            var raycaster = viewport.Canvas.GetComponent<GraphicRaycaster>();
            Assert.That(raycaster.eventCamera, Is.SameAs(viewport.Camera), context);
            try
            {
                foreach (Vector2 local in new[] { Vector2.zero, new Vector2(-430f, -210f), new Vector2(430f, 210f) })
                {
                    Vector2 observed = new Vector2(float.NaN, float.NaN);
                    input.Initialize(map, value => observed = value);
                    Vector2 pixel = viewport.Camera.WorldToScreenPoint(map.TransformPoint(local));
                    var pointer = new PointerEventData(viewport.Events) { position = pixel,
                        pointerPressRaycast = new RaycastResult { module = raycaster, gameObject = input.gameObject } };
                    input.OnPointerClick(pointer);
                    Assert.That(Vector2.Distance(observed, local), Is.LessThan(.02f), context + " pointer " + local);
                }
            }
            finally { input.Initialize(map, original); }
        }

        internal sealed class DashboardViewport : System.IDisposable
        {
            private readonly Scene scene;
            private RenderTexture target;
            public readonly Canvas Canvas;
            public readonly Camera Camera;
            public readonly EventSystem Events;
            public int Width { get; private set; }
            public int Height { get; private set; }

            public DashboardViewport(GameObject host)
            {
                scene = EditorSceneManager.NewPreviewScene();
                SceneManager.MoveGameObjectToScene(host, scene);
                Canvas = host.GetComponentsInChildren<Canvas>().Single(item => item.isRootCanvas);
                var cameraHost = new GameObject("Navigation Layout Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera = cameraHost.GetComponent<Camera>();
                Camera.enabled = false;
                Camera.scene = scene;
                Camera.orthographic = true;
                Camera.orthographicSize = 5f;
                Camera.nearClipPlane = .1f;
                Camera.farClipPlane = 2000f;
                Camera.transform.position = new Vector3(0f, 0f, -1000f);
                Camera.cullingMask = 1 << 31;
                Camera.clearFlags = CameraClearFlags.SolidColor;
                Camera.backgroundColor = Color.black;
                Camera.allowHDR = false;
                Camera.allowMSAA = false;
                var eventHost = new GameObject("Navigation Layout Events");
                eventHost.SetActive(false);
                SceneManager.MoveGameObjectToScene(eventHost, scene);
                Events = eventHost.AddComponent<EventSystem>();
                Canvas.renderMode = RenderMode.ScreenSpaceCamera;
                Canvas.worldCamera = Camera;
                Canvas.planeDistance = 100f;
                foreach (Transform child in Canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            }

            public void ResizeAndRender(int width, int height)
            {
                Camera.targetTexture = null;
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                Width = width; Height = height;
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                Camera.targetTexture = target;
                Camera.aspect = width / (float)height;
                Camera.rect = new Rect(0f, 0f, 1f, 1f);
                CanvasScaler scaler = Canvas.GetComponent<CanvasScaler>();
                Assert.That(scaler.enabled, Is.True);
                Assert.That(scaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand));
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
                for (int pass = 0; pass < 4; pass++)
                {
                    UnityEngine.Canvas.ForceUpdateCanvases();
                    handle.Invoke(scaler, null);
                    foreach (Text label in Canvas.GetComponentsInChildren<Text>()) label.SetAllDirty();
                    UnityEngine.Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(Camera, new RenderPipeline.StandardRequest { destination = target });
                    else Camera.Render();
                }
                float scale = Mathf.Min(width / 1920f, height / 1080f);
                Assert.That(Canvas.renderingDisplaySize.x, Is.EqualTo(width).Within(.5f));
                Assert.That(Canvas.renderingDisplaySize.y, Is.EqualTo(height).Within(.5f));
                Assert.That(Canvas.pixelRect.width, Is.EqualTo(width).Within(.5f));
                Assert.That(Canvas.pixelRect.height, Is.EqualTo(height).Within(.5f));
                Assert.That(Canvas.scaleFactor, Is.EqualTo(scale).Within(.001f));
                Assert.That(Canvas.GetComponent<RectTransform>().rect.width, Is.EqualTo(width / scale).Within(.1f));
                Assert.That(Canvas.GetComponent<RectTransform>().rect.height, Is.EqualTo(height / scale).Within(.1f));
            }

            public void Dispose()
            {
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }

        [Test]
        public void TutorialBossRewardKeepsTheJournalRationDurationAndLookoutReadoutsVisible()
        {
            Create();
            typeof(CoreGameplayDemoController).GetField("tutorialActive", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, true);
            Set(controller.Session, "GuidedTutorialLeg", CoreGuidedTutorialContent.BossVoyageLeg);
            Set(controller.Session, "GuidedTutorialBossDefeated", true);
            Render();
            Canvas.ForceUpdateCanvases();
            RectTransform reward = Find<RectTransform>("Guided Tutorial Component Reward");
            Assert.That(reward.parent.name, Is.EqualTo("Forward View Panel"));
            foreach (string name in new[] { "Voyage Supply Endurance", "Voyage Journal Scroll", "Lookout Report Plaque",
                "Lookout Heading Plaque", "Lookout Time Ribbon" })
            {
                RectTransform other = Find<RectTransform>(name);
                Bounds rewardBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(controller.transform, reward);
                Bounds otherBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(controller.transform, other);
                Assert.That(rewardBounds.Intersects(otherBounds), Is.False, "Tutorial reward obscures " + name);
            }
            Text label = Find<Text>("Guided Tutorial Reward Text");
            Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1f));
            Assert.That(Find<Text>("Water Food Endurance").text, Does.Contain("淡水"));
        }

        [Test]
        public void HeadingAndDistance_UpdateTheRealCourseAndLookout_WithoutReplacingSliderWhileDragging()
        {
            Create();
            Find<Button>("Heading South").onClick.Invoke();
            Assert.That(controller.Session.Heading, Is.EqualTo(SailingDirection.South));
            Assert.That(Find<Text>("Lookout Heading").text, Does.Contain("南"));
            var slider = Find<Slider>("Distance Slider");
            slider.value = 170;
            Assert.That(controller.Session.PlannedDistanceNm, Is.EqualTo(170));
            Assert.That(Find<Slider>("Distance Slider"), Is.SameAs(slider));
            Assert.That(Find<Text>("Distance Label").text, Does.Contain("170"));
            Assert.That(Find<RectTransform>("Map Layer").GetComponentsInChildren<MapCourseInputSurface>().Length, Is.EqualTo(1));
        }

        [Test]
        public void DistanceChanges_PreserveWorldVisualsAndReplaceDirectTargetWithAccurateCourse()
        {
            Create();
            controller.Session.SetCourseTo(controller.Session.Map.Destination);
            Render();
            Assert.That(controller.Session.HasDirectCourseTarget, Is.True);
            var slider = Find<Slider>("Distance Slider");
            string[] retainedNames = { "Ocean Tile Layer", "Terrain Art Layer", "Visibility Mask Layer",
                "Map Course Input", "Player Ship", "Forward Viewport", "Lookout Ocean", "Lookout Ocean Transition", "Lookout Time Track" };
            var retained = retainedNames.Select(Find<RectTransform>).ToArray();
            VoyagePoint position = controller.Session.Position;
            double elapsed = controller.Session.ElapsedGameHours;
            int water = controller.Session.Cargo.Water;
            for (int index = 0; index < 10; index++)
            {
                slider.value = index % 2 == 0 ? 20 : 170;
                Canvas.ForceUpdateCanvases();
                Assert.That(controller.Session.HasDirectCourseTarget, Is.False);
                SailingForecast forecast = controller.Session.GetForecast();
                Vector2 expected = new Vector2(Mathf.Lerp(-580f, 580f, forecast.Target.X / 100f),
                    Mathf.Lerp(-315f, 315f, forecast.Target.Y / 64f));
                Assert.That(Vector2.Distance(Find<RectTransform>("Course Target").anchoredPosition, expected), Is.LessThan(.001f));
                Assert.That(Find<Slider>("Distance Slider"), Is.SameAs(slider));
                for (int item = 0; item < retainedNames.Length; item++)
                    Assert.That(Find<RectTransform>(retainedNames[item]), Is.SameAs(retained[item]), retainedNames[item]);
            }
            Assert.That(host.GetComponentsInChildren<Text>().Any(text => text.name == "Course Target Label"), Is.False);
            Assert.That(controller.Session.Position.X, Is.EqualTo(position.X));
            Assert.That(controller.Session.Position.Y, Is.EqualTo(position.Y));
            Assert.That(controller.Session.ElapsedGameHours, Is.EqualTo(elapsed));
            Assert.That(controller.Session.Cargo.Water, Is.EqualTo(water));
        }

        [Test]
        public void Lookout_UsesPersistentGameTimeForDayDuskNight()
        {
            Create();
            Assert.That(Find<Text>("Lookout Time Readout").text, Does.Contain("白天"));
            Assert.That(Find<Text>("Lookout Heading").text, Does.Not.Contain("白天"));
            Assert.That(Find<RectTransform>("Lookout Time Ribbon").parent.name, Is.EqualTo("Voyage Header"));
            AssertLookoutPanoramas(NavigationTimeOfDay.Day, NavigationTimeOfDay.Day, 0f);
            Assert.That(Find<VoyageTimeRibbonGraphic>("Lookout Time Track").LocalHour, Is.EqualTo(8d).Within(0.001d));
            var track = Find<VoyageTimeRibbonGraphic>("Lookout Time Track");
            Assert.That(track.GetComponent<CanvasRenderer>(), Is.Not.Null,
                "The day/night timeline needs a renderer, not just its overlaid text.");
            Assert.That(track, Is.InstanceOf<MaskableGraphic>());
            Assert.That(Find<Text>("Lookout Time Readout").text, Does.Contain("08:00"));

            Set(controller.Session, "ElapsedGameHours", 9d);
            Render();
            Assert.That(Find<Text>("Lookout Time Readout").text, Does.Contain("黄昏"));
            Assert.That(Find<Text>("Lookout Heading").text, Does.Not.Contain("黄昏"));
            AssertLookoutPanoramas(NavigationTimeOfDay.Day, NavigationTimeOfDay.Dusk, 1f / 3f);
            Assert.That(Find<VoyageTimeRibbonGraphic>("Lookout Time Track").LocalHour, Is.EqualTo(17d).Within(0.001d));
            Assert.That(Find<Text>("Lookout Time Readout").text, Does.Contain("17:00"));

            Set(controller.Session, "ElapsedGameHours", 12d);
            Render();
            Assert.That(Find<Text>("Lookout Time Readout").text, Does.Contain("夜晚"));
            Assert.That(Find<Text>("Lookout Heading").text, Does.Not.Contain("夜晚"));
            AssertLookoutPanoramas(NavigationTimeOfDay.Dusk, NavigationTimeOfDay.Night, 2f / 3f);
            Assert.That(Find<VoyageTimeRibbonGraphic>("Lookout Time Track").LocalHour, Is.EqualTo(20d).Within(0.001d));
            Assert.That(Find<Text>("Lookout Time Readout").text, Does.Contain("20:00"));
            Assert.That(host.GetComponentsInChildren<Image>(true).Any(image => image.name.StartsWith("Lookout Night Star ")), Is.False);
        }

        private void AssertLookoutPanoramas(NavigationTimeOfDay from, NavigationTimeOfDay to, float amount)
        {
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Lookout Time Overlay"), Is.False);
            var ocean = Find<RawImage>("Lookout Ocean");
            var transition = Find<RawImage>("Lookout Ocean Transition");
            SeaTerrainType terrain = controller.Session.Map.TerrainAt(controller.Session.Position);
            Assert.That(ocean.texture, Is.SameAs(NavigationLookoutArtCatalog.ForEnvironment(terrain, from)));
            Assert.That(transition.texture, Is.SameAs(NavigationLookoutArtCatalog.ForEnvironment(terrain, to)));
            Assert.That(ocean.color, Is.EqualTo(Color.white));
            Assert.That(transition.color.r, Is.EqualTo(1f));
            Assert.That(transition.color.g, Is.EqualTo(1f));
            Assert.That(transition.color.b, Is.EqualTo(1f));
            Assert.That(transition.color.a, Is.EqualTo(amount).Within(.001f));
            Assert.That(ocean.raycastTarget || transition.raycastTarget, Is.False);
            Assert.That(ocean.rectTransform.sizeDelta, Is.EqualTo(Find<RectTransform>("Forward Viewport").sizeDelta));
            Assert.That(transition.rectTransform.sizeDelta, Is.EqualTo(ocean.rectTransform.sizeDelta));
        }

        [TestCase(SeaTerrainType.FogBank, LookoutSceneryKind.Fog)]
        [TestCase(SeaTerrainType.StormFront, LookoutSceneryKind.Storm)]
        public void Lookout_LocalWeatherUsesPanoramaWhileDistantWeatherAndDiscoveryRemainProjected(SeaTerrainType weather, LookoutSceneryKind kind)
        {
            Create();
            var map = new CoreVoyageMap { Route = CoreVoyageSession.Routes[0], Seed = "LOCAL-LOOKOUT", Hash = 23,
                DepartureName = "起港", DestinationName = "终港", Departure = new VoyagePoint(10, 32), Destination = new VoyagePoint(90, 32) };
            VoyagePoint origin = map.CellCenter(24, 16);
            map.TerrainCells[16 * CoreVoyageMap.GridColumns + 28] = weather;
            var hidden = new CoreMapNode { Id = 73, Position = map.CellCenter(26, 16), Discovered = false,
                Encounter = new CoreEncounter("pirate_hidden", "未发现敌船", "海战", "隐藏", true, false, 1, 70) };
            map.Nodes.Add(hidden);
            Set(controller.Session, "Map", map);
            Set(controller.Session, "Position", origin);
            controller.Session.SetHeading(SailingDirection.East);
            Render();
            AssertLookoutPanoramas(NavigationTimeOfDay.Day, NavigationTimeOfDay.Day, 0f);
            Assert.That(Find<RawImage>("Lookout Ocean").texture,
                Is.SameAs(NavigationLookoutArtCatalog.ForEnvironment(SeaTerrainType.OpenWater, NavigationTimeOfDay.Day)),
                "A distant weather feature must not replace the local clear panorama.");
            var distant = VoyageLookoutProjector.Project(map, origin, SailingDirection.East).Single(item => item.Kind == kind);
            Assert.That(Find<RawImage>("Lookout " + distant.Key).texture, Is.Not.Null);

            map.TerrainCells[map.CellIndex(origin)] = weather;
            Render();
            AssertLookoutPanoramas(NavigationTimeOfDay.Day, NavigationTimeOfDay.Day, 0f);
            var observations = VoyageLookoutProjector.Project(map, origin, SailingDirection.East);
            var local = observations.Single(item => item.Kind == kind && item.DistanceNm == 0f);
            Assert.That(Find<RectTransform>("Forward Viewport").Find("Lookout " + local.Key), Is.Null,
                "The zero-distance weather must not cover the painted weather a second time.");
            Assert.That(Find<RawImage>("Lookout " + distant.Key).texture, Is.Not.Null);
            Assert.That(Find<Text>("Lookout Report").text, Does.Not.Contain("未发现敌船"));
            Assert.That(Find<RectTransform>("Forward Viewport").GetComponentsInChildren<Graphic>().Any(graphic => graphic.raycastTarget), Is.False);
            Color daylight = Find<RawImage>("Lookout " + distant.Key).color;
            Set(controller.Session, "ElapsedGameHours", 14d);
            Render();
            Assert.That(Find<RawImage>("Lookout " + distant.Key).color.grayscale, Is.LessThan(daylight.grayscale),
                "Dynamic scenery must share the painted panorama's night lighting without a full-view overlay.");
            Assert.That(Find<RawImage>("Lookout " + distant.Key).color.a, Is.EqualTo(daylight.a));
            Assert.That(hidden.Discovered || hidden.Visited || hidden.Skipped, Is.False);
            Assert.That(map.ExploredCells.All(value => !value), Is.True);
        }

        [Test]
        public void Encounter_KeepsHeaderClockAndBothActionsVisibleAcrossDateRollover()
        {
            Create(CoreGameplayPreviewScreen.Encounter);
            foreach (double hours in new[] { 0d, 9d, 36d })
            {
                Set(controller.Session, "ElapsedGameHours", hours);
                Render();
                Canvas.ForceUpdateCanvases();
                Assert.That(Find<RectTransform>("Encounter Card"), Is.Not.Null);
                Text time = Find<Text>("Lookout Time Readout");
                Assert.That(time.text, Does.Contain(controller.Session.GameDateTime.ToString("yyyy-MM-dd HH:mm")));
                Assert.That(time.text, Does.Contain(NavigationTimeOfDayUtility.Label(NavigationTimeOfDayUtility.FromVoyageHours(hours))));
                Assert.That(Find<VoyageTimeRibbonGraphic>("Lookout Time Track").LocalHour,
                    Is.EqualTo(NavigationTimeOfDayUtility.LocalHour(hours)).Within(.001d));
                Assert.That(time.transform.parent.parent.name, Is.EqualTo("Voyage Header"));
                Assert.That(Find<Button>("Voyage Cargo").interactable, Is.True);
                Assert.That(Find<Button>("Voyage Settings").interactable, Is.True);
                Assert.That(Find<RectTransform>("Forward Viewport").GetComponentsInChildren<VoyageTimeRibbonGraphic>(), Is.Empty);
            }
        }

        [Test]
        public void OrdinaryEvent_IsInsideLookout_PreservesChartAndJournal_AndLocksSailing()
        {
            Create(CoreGameplayPreviewScreen.Encounter);
            var viewport = Find<RectTransform>("Forward Viewport");
            Assert.That(Find<RectTransform>("Encounter Card").parent, Is.SameAs(viewport));
            Assert.That(Find<RectTransform>("Encounter Card").sizeDelta, Is.EqualTo(viewport.sizeDelta));
            Assert.That(Find<Button>("Execute Sail").interactable, Is.False);
            Assert.That(Find<Slider>("Distance Slider").interactable, Is.False);
            Assert.That(Find<Button>("Safe Return").interactable, Is.False);
            Assert.That(Find<Image>("Map Course Input").raycastTarget, Is.False);
            Assert.That(Find<Text>("Voyage Log").text, Is.Not.Empty);
            Assert.That(Find<RectTransform>("Map Layer"), Is.Not.Null);
        }

        [Test]
        public void ChoosingCombat_EntersFullBattleWithoutNavigationDashboard()
        {
            Create();
            var encounter = new CoreEncounter("test_pirate", "海盗拦截", "海战", "敌船逼近", true, false, 1, 65,
                new CoreEncounterChoice("迎战", "进入海战", 0, new VoyageEffect(), "", true));
            Set(controller.Session, "ActiveEncounter", encounter); Set(controller.Session, "Phase", CoreDemoPhase.Encounter);
            Render(); Find<Button>("Encounter Choice 0").onClick.Invoke();
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            Assert.That(Find<RectTransform>("Battle Card").sizeDelta.x, Is.GreaterThan(1700));
            Assert.That(host.GetComponentsInChildren<RectTransform>().Any(x => x.name == "Forward Viewport" || x.name == "Map Layer"), Is.False);
        }
    }
}
