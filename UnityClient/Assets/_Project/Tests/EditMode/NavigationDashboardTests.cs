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
        [TearDown] public void Cleanup() { if (host != null) Object.DestroyImmediate(host); }
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

        [TestCase(false)]
        [TestCase(true)]
        public void ChartLabels_RasterizeForDisplayedSizeWithoutChangingLayoutAcrossScreens(bool treasureVoyage)
        {
            Create();
            using (var viewport = new DashboardViewport(host))
            {
                if (treasureVoyage)
                {
                    CoreVoyageSession session = controller.Session;
                    Set(session, "Phase", CoreDemoPhase.Port);
                    Set(session, "GuidedTutorialLeg", 0);
                    session.RestoreTreasureProgress(new CoreTreasureProgress
                    {
                        pirateExperience = 1400,
                        maps = new[] { new CoreTreasureMapSave
                            { id = "text-density", seed = "NAVIGATION-TEXT-DENSITY", rankId = "moonlit" } },
                        selectedMapId = "text-density"
                    });
                    session.Cargo.Wood = session.Cargo.Iron = session.Cargo.Cloth = session.Cargo.Powder = 0;
                    session.Cargo.Cannonballs = session.Cargo.Beer = session.Cargo.Medicine = 0;
                    session.Cargo.Water = session.Cargo.Food = 100;
                    Assert.That(session.TryStartTreasureVoyage(out string reason), Is.True, reason);
                    Render();
                    foreach (Transform child in viewport.Canvas.GetComponentsInChildren<Transform>(true))
                        child.gameObject.layer = 31;
                    Assert.That(Find<Text>("Map Title").text,
                        Is.EqualTo(session.ActiveTreasureMap.Name + " · 隐藏海域"));
                    Assert.That(Find<Text>("Map Course Hint").text, Is.Not.Empty);
                }

                string[] names = treasureVoyage
                    ? new[] { "Map Title", "Water Food Endurance", "Map Course Hint" }
                    : new[] { "Map Title", "Water Food Endurance" };
                Text[] labels = names.Select(Find<Text>).ToArray();
                Font[] fonts = labels.Select(label => label.font).ToArray();
                int[] authoredSizes = { 23, 17, 14 };
                float[] authoredHeights = { 34f, 30f, 32f };
                NavigationDashboardLayout layout = Find<NavigationDashboardLayout>("Navigation Dashboard Layout");
                RectTransform chart = Find<RectTransform>("Navigation Chart");
                viewport.ResizeAndRender(1920, 1080);
                Assert.That(chart.localScale.x, Is.GreaterThan(1f), "Exercise the original chart magnification.");
                int[] firstSizes = labels.Select(label => label.fontSize).ToArray();
                Vector3[] firstScales = labels.Select(label => label.transform.localScale).ToArray();
                Vector2[] firstBoxes = labels.Select(label => label.rectTransform.sizeDelta).ToArray();

                foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(3840, 2160),
                    new Vector2Int(3440, 1440), new Vector2Int(1280, 1024), new Vector2Int(1920, 1080) })
                {
                    viewport.ResizeAndRender(size.x, size.y);
                    string context = (treasureVoyage ? "treasure " : "navigation ") + size.x + "x" + size.y;
                    AssertSupplyHeader(context);
                    AssertMapFillsAvailableChart(context);
                    for (int index = 0; index < labels.Length; index++)
                    {
                        Text label = labels[index];
                        Assert.That(Find<Text>(names[index]), Is.SameAs(label), context);
                        Assert.That(label.font, Is.SameAs(fonts[index]), context + " retain the authored typeface");
                        AssertChartLabelDensity(label, authoredSizes[index], authoredHeights[index], viewport.Camera, context);
                        int rasterSize = label.fontSize;
                        Vector3 scale = label.transform.localScale;
                        Vector2 box = label.rectTransform.sizeDelta, position = label.rectTransform.anchoredPosition;
                        for (int pass = 0; pass < 3; pass++) layout.Apply();
                        Canvas.ForceUpdateCanvases();
                        Assert.That(label.fontSize, Is.EqualTo(rasterSize), context + " repeated Apply font size");
                        Assert.That(label.transform.localScale, Is.EqualTo(scale), context + " repeated Apply scale");
                        Assert.That(label.rectTransform.sizeDelta, Is.EqualTo(box), context + " repeated Apply box");
                        Assert.That(label.rectTransform.anchoredPosition, Is.EqualTo(position), context + " repeated Apply position");
                    }
                }
                for (int index = 0; index < labels.Length; index++)
                {
                    Assert.That(labels[index].fontSize, Is.EqualTo(firstSizes[index]), "return to 1080p font size");
                    Assert.That(labels[index].transform.localScale, Is.EqualTo(firstScales[index]), "return to 1080p scale");
                    Assert.That(labels[index].rectTransform.sizeDelta, Is.EqualTo(firstBoxes[index]), "return to 1080p box");
                }

                // Exercise a longer localized route name on the actual production
                // label, in addition to the real route / treasure strings above.
                labels[0].text = "灰鸥港远航补给码头 → 白贝港外海";
                foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(3840, 2160) })
                {
                    viewport.ResizeAndRender(size.x, size.y);
                    AssertChartLabelDensity(labels[0], 23, 34f, viewport.Camera, "long title " + size);
                }
            }
        }

        private static void AssertChartLabelDensity(Text label, int authoredSize, float authoredHeight,
            Camera camera, string context)
        {
            RectTransform rect = label.rectTransform;
            string reason = context + " " + label.name;
            Vector2 origin = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(Vector3.zero));
            float projectedX = Vector2.Distance(origin,
                RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(Vector3.right)));
            float projectedY = Vector2.Distance(origin,
                RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(Vector3.up)));
            Assert.That(Mathf.Max(projectedX, projectedY), Is.LessThanOrEqualTo(label.pixelsPerUnit + .002f),
                reason + " must not enlarge glyph atlas pixels");
            Assert.That(label.fontSize * rect.localScale.x, Is.EqualTo(authoredSize).Within(.0001f),
                reason + " displayed font size");
            Assert.That(rect.rect.height * rect.localScale.y, Is.EqualTo(authoredHeight).Within(.001f),
                reason + " displayed box height");
            Assert.That(label.resizeTextForBestFit, Is.False, reason);
            Assert.That(label.horizontalOverflow, Is.EqualTo(HorizontalWrapMode.Wrap), reason);
            Assert.That(label.verticalOverflow, Is.EqualTo(VerticalWrapMode.Truncate), reason);
            Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(rect.rect.height + .1f), reason);
            Assert.That(label.cachedTextGenerator.characterCountVisible, Is.GreaterThanOrEqualTo(label.text.Length),
                reason + " must display the complete text");
            int renderedLines = label.cachedTextGenerator.lineCount;
            using (var authored = new TextGenerator())
            {
                TextGenerationSettings settings = label.GetGenerationSettings(rect.rect.size * rect.localScale.x);
                settings.fontSize = authoredSize;
                authored.Populate(label.text, settings);
                Assert.That(renderedLines, Is.EqualTo(authored.lineCount),
                    reason + " density compensation must preserve line wrapping");
            }
        }

        [Test]
        public void ExplorationSummary_WrapsAboveChartWithoutCoveringTheSea()
        {
            Create();
            using (var viewport = new DashboardViewport(host))
            {
                // Moving the host into the preview scene invokes its editor
                // disable/enable cycle and replaces the generated controls.
                Text summary = Find<Text>("Voyage Exploration Status Text");
                Assert.That(summary.text, Does.Contain("探索收获"));
                summary.text = "探索收获 · 航速 +25% 还有 18.5 小时；瞭望范围 +35% 还有 12 小时。" +
                    "本航次线索：潮工船坞图，可调查潮痕秘藏；商队互助凭证，可与香料沉船的失主交涉；" +
                    "月相星图，可解读月痕方碑并开启遗迹深处。线索取得后会显示相应目标，继续航行时仍可查看。";
                foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080),
                    new Vector2Int(1280, 1024), new Vector2Int(3440, 1440) })
                {
                    viewport.ResizeAndRender(size.x, size.y);
                    AssertMapFillsAvailableChart("exploration summary " + size);
                    Assert.That(summary.preferredHeight, Is.LessThanOrEqualTo(summary.rectTransform.rect.height + .1f));
                    Assert.That(summary.resizeTextForBestFit, Is.False);
                    Assert.That(summary.cachedTextGenerator.characterCountVisible, Is.GreaterThanOrEqualTo(summary.text.Length));
                }
            }
        }

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
            RectTransform controls = Find<RectTransform>("Navigation Controls");
            RectTransform lookout = Find<RectTransform>("Forward View Panel");
            Assert.That(controls.parent, Is.SameAs(lookout.parent));
            Assert.That(controls.GetSiblingIndex(), Is.GreaterThan(lookout.GetSiblingIndex()),
                "Opening wind details must bring the controls subtree above the lookout covering its popup area.");
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

        [TestCase(1920,1080)]
        [TestCase(1280,720)]
        [TestCase(1280,1024)]
        public void CompactNavigation_KeepsControlsLegibleAndGivesMostSpaceToScenery(int width, int height)
        {
            Create();
            using (var viewport = new DashboardViewport(host))
            {
                viewport.ResizeAndRender(width,height);
                RectTransform root = viewport.GameRoot;
                Rect chart = RectIn(root,Find<RectTransform>("Navigation Chart"));
                Rect controls = RectIn(root,Find<RectTransform>("Navigation Controls"));
                Rect lookout = RectIn(root,Find<RectTransform>("Forward View Panel"));
                Assert.That(chart.height, Is.GreaterThan(controls.height*2f));
                Assert.That(lookout.height, Is.GreaterThan(controls.height));
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
                foreach(string name in new[]{"Forecast","Lookout Report","Water Food Endurance"})
                {
                    Text text = Find<Text>(name);
                    Assert.That(text.preferredHeight,Is.LessThanOrEqualTo(text.rectTransform.rect.height+1f),name);
                }
                Find<Button>("Forecast Details").onClick.Invoke();
                viewport.ResizeAndRender(width,height);
                Rect disclosure = RectIn(root,Find<RectTransform>("Forecast Details Panel"));
                Assert.That(disclosure.yMin,Is.GreaterThanOrEqualTo(controls.yMax-.1f));
                Assert.That(disclosure.yMax,Is.LessThan(chart.yMax));
                Assert.That(disclosure.xMin,Is.GreaterThanOrEqualTo(lookout.xMin));
                Assert.That(disclosure.xMax,Is.LessThanOrEqualTo(lookout.xMax));
                Text detailText = Find<Text>("Forecast Detail Text");
                Assert.That(detailText.preferredHeight,Is.LessThanOrEqualTo(detailText.rectTransform.rect.height+1f));
            }
        }

        [Test]
        public void PortNameplates_KeepLandmarkTargetsAndEmphasizeOnlyTheHoveredSign()
        {
            Create(CoreGameplayPreviewScreen.PortHub);
            using(var viewport = new DashboardViewport(host))
            {
                viewport.ResizeAndRender(1920,1080);
                foreach(string key in new[]{"Warehouse","Shipyard","Tavern","ComponentShop","CardWorkshop","Treasure","Maintenance","Voyage","Market"})
                {
                    Button button = Find<Button>("Port Hotspot "+key);
                    Image sign = Find<Image>("Port Sign "+key);
                    Text label = Find<Text>("Port Label "+key);
                    Assert.That(sign,Is.InstanceOf<PortNameplateGraphic>());
                    Assert.That(sign.raycastTarget,Is.False);
                    Assert.That(label.raycastTarget,Is.False);
                    Assert.That(label.gameObject.activeInHierarchy,Is.True);
                    Assert.That(label.fontSize,Is.EqualTo(22));
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(viewport.Camera,sign.transform.position);
                    var data = new PointerEventData(viewport.Events){position=point};
                    var hits = new System.Collections.Generic.List<RaycastResult>();
                    viewport.Events.RaycastAll(data,hits);
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
        public void Dashboard_TilesSailingOrEncounterRegions_WithChartFilledAndInputPreserved(CoreGameplayPreviewScreen screen)
        {
            Create(screen);
            string[] names = { "Navigation Chart", "Navigation Controls", "Forward View Panel" };
            foreach (string label in new[] { "Map Title", "Water Food Endurance", "Forecast", "Distance Label" })
                Assert.That(Find<Text>(label).color.grayscale, Is.LessThan(0.4f), label + " must use dark ink on parchment.");
            foreach (string retired in new[] { "Map Seed", "Sea Conditions Legend", "Global Wind Direction Arrow",
                "Cargo Status", "Supply Endurance Summary", "Map Course Hint", "Navigation Journal",
                "Voyage Journal Scroll", "Voyage Log", "Log Title", "Voyage Cargo Summary",
                "Encounter Terrain", "Encounter Themed Frame" })
                Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == retired), Is.False, retired);
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
                CoreVoyageMap voyageMap = session.Map;
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
                    Assert.That(session.Map, Is.SameAs(voyageMap), context + " resizing must not regenerate the world");
                    Assert.That(Find<Slider>("Distance Slider"), Is.SameAs(distance), context);
                    for (int index = 0; index < panels.Length; index++)
                        Assert.That(Find<RectTransform>(names[index]), Is.SameAs(panels[index]), context);
                    Assert.That(Find<RectTransform>("Map Layer"), Is.SameAs(map), context);
                    Assert.That(Find<MapCourseInputSurface>("Map Course Input"), Is.SameAs(input), context);
                    RectTransform[] visiblePanels = screen == CoreGameplayPreviewScreen.Encounter
                        ? new[] { panels[0], panels[2] } : panels;
                    AssertDashboardCoverage(viewport.GameRoot, visiblePanels, Find<RectTransform>("Voyage Header"), context);
                    if (screen == CoreGameplayPreviewScreen.Navigation)
                    {
                        RectTransform lookout = Find<RectTransform>("Forward View Panel");
                        foreach (string name in new[] { "Forward Viewport", "Lookout Ocean", "Lookout Ocean Transition" })
                        {
                            Rect bounds = RectIn(lookout, Find<RectTransform>(name));
                            string reason = context + " " + name + " must cover the entire panel without exposing parchment";
                            Assert.That(bounds.xMin, Is.EqualTo(lookout.rect.xMin).Within(.01f), reason);
                            Assert.That(bounds.xMax, Is.EqualTo(lookout.rect.xMax).Within(.01f), reason);
                            Assert.That(bounds.yMin, Is.EqualTo(lookout.rect.yMin).Within(.01f), reason);
                            Assert.That(bounds.yMax, Is.EqualTo(lookout.rect.yMax).Within(.01f), reason);
                        }
                        AssertPanoramaPixelAspectAndHorizon(context);
                    }
                    else
                    {
                        AssertNavigationControlsSuppressed();
                        Assert.That(Find<RectTransform>("Encounter Card").sizeDelta,
                            Is.EqualTo(Find<RectTransform>("Forward Viewport").sizeDelta), context + " encounter card replaces the lookout and must fill its resized viewport");
                        AssertEmbeddedEncounterFitsPaper(context);
                    }
                    AssertVoyageHeaderControlsAndTime(context);
                    AssertSupplyHeader(context);
                    AssertMapGeometryAndPointerCoordinates(viewport, map, input, context);
                }
            }
        }

        private void AssertEmbeddedEncounterFitsPaper(string context)
        {
            RectTransform card = Find<RectTransform>("Encounter Card");
            RectTransform paper = Find<Image>("Encounter Parchment").rectTransform;
            RectTransform content = Find<RectTransform>("Encounter Paper Content");
            AssertInsidePanel(RectIn(card, paper), card, context + " parchment");
            AssertInsidePanel(RectIn(paper, content), paper, context + " paper margins");
            string[] regions = { "Encounter Illustration Mount", "Encounter Location", "Encounter Title",
                "Encounter Description Viewport", "Encounter Badge", "Encounter Decision Footer", "Encounter Decision Viewport" };
            Rect[] boxes = regions.Select(name => RectIn(content, Find<RectTransform>(name))).ToArray();
            for (int index = 0; index < boxes.Length; index++)
            {
                AssertInsidePanel(boxes[index], content, context + " " + regions[index]);
                for (int other = index + 1; other < boxes.Length; other++)
                    Assert.That(boxes[index].Overlaps(boxes[other]), Is.False,
                        context + " " + regions[index] + " / " + regions[other]);
                Text text = Find<RectTransform>(regions[index]).GetComponent<Text>();
                if (text != null)
                    Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1f),
                        context + " " + text.name + " must remain legible after resizing");
            }
            ScrollRect scroll = Find<ScrollRect>("Encounter Decision Viewport");
            Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(scroll.horizontal, Is.False);
            Assert.That(scroll.content.rect.width, Is.EqualTo(scroll.viewport.rect.width).Within(.1f),
                context + " decisions must use the full available paper width");
            RectTransform secondary = Find<RectTransform>("Encounter Secondary Actions");
            Assert.That(secondary.parent, Is.SameAs(content));
            if (secondary.rect.height > .1f)
            {
                AssertInsidePanel(RectIn(content, secondary), content, context + " fixed secondary actions");
                Assert.That(RectIn(content, secondary).Overlaps(RectIn(content, scroll.viewport)), Is.False,
                    context + " secondary actions must not overlap the choice viewport");
            }
            Text narrative = Find<Text>("Encounter Description");
            Assert.That(narrative.preferredHeight, Is.LessThanOrEqualTo(narrative.rectTransform.rect.height + .5f));
        }

        private void AssertSupplyHeader(string context)
        {
            RectTransform chart = Find<RectTransform>("Navigation Chart");
            RectTransform endurance = Find<RectTransform>("Voyage Supply Endurance");
            Assert.That(endurance.parent, Is.SameAs(chart), context + " supplies belong beside the chart title");
            Rect supplyBounds = RectIn(chart, endurance);
            Rect titleBounds = RectIn(chart, Find<RectTransform>("Map Title"));
            Rect mapBounds = RectIn(chart, Find<RectTransform>("Procedural Sea Chart Frame"));
            Assert.That(supplyBounds.xMin, Is.GreaterThanOrEqualTo(titleBounds.xMax - .01f), context);
            Assert.That(supplyBounds.yMin, Is.GreaterThanOrEqualTo(mapBounds.yMax - .01f), context);
            AssertInsidePanel(supplyBounds, chart, context + " supply header");
            foreach (string name in new[] { "Water Food Endurance" })
            {
                Text label = Find<Text>(name);
                AssertInsidePanel(RectIn(endurance, label.rectTransform), endurance, context + " " + name);
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1f), context + " " + name);
                Assert.That(label.raycastTarget, Is.False, context + " " + name);
            }
        }

        private static void AssertInsidePanel(Rect bounds, RectTransform panel, string context)
        {
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(panel.rect.xMin - .01f), context);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(panel.rect.xMax + .01f), context);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(panel.rect.yMin - .01f), context);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(panel.rect.yMax + .01f), context);
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
                Assert.That(bounds.yMin, Is.GreaterThan(-37f), context + " reserve the tutorial instruction band");
                Assert.That(bounds.yMax, Is.LessThan(39f), context + " reserve the tutorial objective band");
                for (int other = index + 1; other < controls.Length; other++)
                    Assert.That(bounds.Overlaps(controls[other]), Is.False, context + " " + name + " / " + names[other]);
            }
            Rect title = controls[0], status = controls[1], clock = controls[2], cargo = controls[3], settings = controls[4];
            Assert.That(title.xMin - header.rect.xMin, Is.EqualTo(128f).Within(.01f), context + " left information margin reserves the compass ornament");
            Assert.That(title.xMin, Is.EqualTo(status.xMin).Within(.01f), context + " title/status left alignment");
            Assert.That(title.xMax, Is.EqualTo(status.xMax).Within(.01f), context + " title/status width alignment");
            Assert.That(title.yMin - status.yMax, Is.GreaterThanOrEqualTo(7.99f), context + " title above status with readable separation");
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
            Assert.That(Find<Text>("Lookout Time Readout").text, Does.Contain(CoreVoyageSession.FormatGameDate(controller.Session.GameDateTime, true)), context);
            Assert.That(Find<VoyageTimeRibbonGraphic>("Lookout Time Track").LocalHour,
                Is.EqualTo(NavigationTimeOfDayUtility.LocalHour(controller.Session.ElapsedGameHours)).Within(.001d), context);
        }

        [Test]
        public void Dashboard_TutorialPaperKeepsLargeTextVisibleAboveTheExistingInterface()
        {
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
                    var panels = new[] { "Navigation Chart", "Navigation Controls", "Forward View Panel" }
                        .Select(Find<RectTransform>).ToArray();
                    AssertDashboardCoverage(viewport.GameRoot, panels, header, context, 148f);
                    AssertVoyageHeaderControlsAndTime(context);
                    Image paper = Find<Image>("Guided Tutorial Banner");
                    Assert.That(paper.transform.parent, Is.SameAs(header.parent), context + " independent tutorial paper");
                    Assert.That(NauticalSurfaceSkin.IsPaperSurface(paper.sprite), Is.True, context);
                    Assert.That(paper.sprite, Is.SameAs(MarketShopSkin.Paper), context);
                    Assert.That(paper.color.a, Is.EqualTo(1f), context + " opaque writing area");
                    Assert.That(paper.raycastTarget, Is.False, context);
                    Assert.That(paper.canvasRenderer.absoluteDepth,
                        Is.GreaterThan(Find<Image>("Voyage Header Frame").canvasRenderer.absoluteDepth),
                        context + " the paper must stay above the regional header");
                    Rect paperOnSurface = RectIn(viewport.GameRoot, paper.rectTransform);
                    AssertInsidePanel(paperOnSurface, viewport.GameRoot, context + " complete paper");
                    Rect? precedingLabel = null;
                    foreach (string lesson in new[] { "Guided Tutorial Objective", "Guided Tutorial Instruction" })
                    {
                        Text text = Find<Text>(lesson);
                        Rect label = RectIn(paper.rectTransform, text.rectTransform);
                        AssertInsidePanel(label, paper.rectTransform, context + " " + lesson);
                        Assert.That(text.resizeTextForBestFit, Is.False, context + " " + lesson);
                        Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(24), context + " " + lesson);
                        Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + .5f),
                            context + " " + lesson + " must display completely");
                        Assert.That(text.raycastTarget, Is.False, context + " " + lesson);
                        if (precedingLabel.HasValue)
                            Assert.That(label.Overlaps(precedingLabel.Value), Is.False, context + " objective / instruction");
                        precedingLabel = label;
                    }
                }
            }
        }

        private static void AssertDashboardCoverage(RectTransform root, RectTransform[] panels, RectTransform header, string context,
            float expectedHeaderHeight = PhaseSurfaceLayout.HeaderHeight)
        {
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
            Assert.That(bounds.Length, Is.EqualTo(2).Or.EqualTo(3), context);
            Assert.That(bounds[0].xMin, Is.EqualTo(available.xMin).Within(tolerance), context + " chart left edge");
            Assert.That(bounds[0].width, Is.EqualTo(available.width * .6625f).Within(tolerance), context + " chart width");
            if (bounds.Length == 2)
            {
                Assert.That(bounds[1].xMin, Is.EqualTo(bounds[0].xMax).Within(tolerance), context + " event seam");
                Assert.That(bounds[1].xMax, Is.EqualTo(available.xMax).Within(tolerance), context + " event right edge");
                foreach (Rect item in bounds)
                {
                    Assert.That(item.yMin, Is.EqualTo(available.yMin).Within(tolerance), context + " bottom edge");
                    Assert.That(item.yMax, Is.EqualTo(available.yMax).Within(tolerance), context + " header seam");
                }
                Assert.That(area, Is.EqualTo((double)available.width * available.height)
                    .Within((available.width + available.height) * tolerance), context + " two panels cover the voyage area");
                return;
            }
            foreach (int index in new[] { 1, 2 })
            {
                Assert.That(bounds[index].xMax, Is.EqualTo(available.xMax).Within(tolerance), context + " right edge");
                Assert.That(bounds[index].xMin, Is.EqualTo(bounds[0].xMax).Within(tolerance), context + " vertical seam");
            }
            foreach (int index in new[] { 0, 2 })
                Assert.That(bounds[index].yMax, Is.EqualTo(available.yMax).Within(tolerance), context + " header seam");
            foreach (int index in new[] { 0, 1 })
                Assert.That(bounds[index].yMin, Is.EqualTo(available.yMin).Within(tolerance), context + " bottom edge");
            Assert.That(bounds[2].yMin, Is.EqualTo(bounds[1].yMax).Within(tolerance), context + " right horizontal seam");
            Assert.That(bounds[2].height, Is.EqualTo(available.height * .52f).Within(tolerance), context + " lookout height");
            Assert.That(bounds[1].height, Is.EqualTo(available.height * .48f).Within(tolerance), context + " controls height");
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

        private void AssertMapFillsAvailableChart(string context)
        {
            RectTransform chart = Find<RectTransform>("Navigation Chart");
            Rect strip = RectIn(chart, Find<RectTransform>("Navigation Map Title Strip"));
            bool hasCourseHint = chart.Find("Map Course Hint") != null;
            RectTransform exploration = chart.Find("Voyage Exploration Status") as RectTransform;
            bool hasBossGuidance = chart.Find("Destination Boss Guidance") != null;
            Assert.That(strip.xMin, Is.EqualTo(chart.rect.xMin + 4f).Within(.01f), context + " title meets the gold frame");
            Assert.That(strip.xMax, Is.EqualTo(chart.rect.xMax - 4f).Within(.01f), context + " title meets the gold frame");
            Assert.That(Find<RectTransform>("Procedural Sea Chart Frame").GetComponent<RectMask2D>(), Is.Not.Null,
                context + " map overflow must be clipped at the gold frame");
            Rect frame = RectIn(chart, Find<RectTransform>("Procedural Sea Chart Frame"));
            Assert.That(frame.xMin, Is.EqualTo(strip.xMin).Within(.01f), context + " frame left edge");
            Assert.That(frame.xMax, Is.EqualTo(strip.xMax).Within(.01f), context + " frame right edge");
            float frameTop = strip.yMin;
            if (exploration != null)
            {
                Rect status = RectIn(chart, exploration);
                Assert.That(status.yMax, Is.EqualTo(strip.yMin).Within(.01f), context + " exploration title seam");
                Assert.That(status.xMin, Is.EqualTo(strip.xMin).Within(.01f), context + " exploration left edge");
                Assert.That(status.xMax, Is.EqualTo(strip.xMax).Within(.01f), context + " exploration right edge");
                frameTop = status.yMin;
            }
            Assert.That(frame.yMax, Is.EqualTo(frameTop).Within(.01f), context + " frame heading seam");
            Assert.That(frame.yMin, Is.EqualTo(chart.rect.yMin + 4f + (hasBossGuidance ? 72f : hasCourseHint ? 40f : 0f)).Within(.01f),
                context + " frame bottom edge");
            if (hasCourseHint)
                Assert.That(RectIn(chart, Find<RectTransform>("Map Course Hint")).yMax,
                    Is.LessThanOrEqualTo(frame.yMin + .01f), context + " treasure hint remains below the visible sea");
            foreach (string name in new[] { "Ocean Tile Layer", "Visibility Mask Layer" })
            {
                Rect bounds = RectIn(chart, Find<RectTransform>(name));
                string reason = context + " " + name + " must cover the complete clipped viewport";
                Assert.That(bounds.xMin, Is.LessThanOrEqualTo(frame.xMin + .01f), reason + " left edge");
                Assert.That(bounds.xMax, Is.GreaterThanOrEqualTo(frame.xMax - .01f), reason + " right edge");
                Assert.That(bounds.yMin, Is.LessThanOrEqualTo(frame.yMin + .01f), reason + " bottom edge");
                Assert.That(bounds.yMax, Is.GreaterThanOrEqualTo(frame.yMax - .01f), reason + " top edge");
            }
            Rect sea = RectIn(chart, Find<RectTransform>("Ocean Tile Layer"));
            CoreVoyageMap world = controller.Session.Map;
            Assert.That(sea.width / world.Columns, Is.EqualTo(sea.height / world.Rows).Within(.001f),
                context + " real terrain cells must display as squares");
            Assert.That(sea.width / world.FineColumns, Is.EqualTo(sea.height / world.FineRows).Within(.001f),
                context + " fine exploration cells must display as squares");
        }

        private static void AssertScreenAspect(RectTransform item, float expected, Camera camera, string context)
        {
            var corners = new Vector3[4];
            item.GetWorldCorners(corners);
            Vector2[] pixels = corners.Select(point => RectTransformUtility.WorldToScreenPoint(camera, point)).ToArray();
            float width = Vector2.Distance(pixels[0], pixels[3]);
            float height = Vector2.Distance(pixels[0], pixels[1]);
            Assert.That(height, Is.GreaterThan(0f), context + " " + item.name);
            Assert.That(width / height, Is.EqualTo(expected).Within(.001f),
                context + " " + item.name + " must retain its visible proportions while the chart fills the panel");
        }

        private void AssertMapGeometryAndPointerCoordinates(DashboardViewport viewport, RectTransform map,
            MapCourseInputSurface input, string context)
        {
            AssertMapFillsAvailableChart(context);
            AssertScreenAspect(Find<RectTransform>("Player Ship"), 98f / 64f, viewport.Camera, context);
            foreach (string name in new[] { "Departure Port", "Destination Port", "Departure Port Halo", "Destination Port Halo", "Course Target" })
                AssertScreenAspect(Find<RectTransform>(name), 1f, viewport.Camera, context);
            foreach (RectTransform marker in map.GetComponentsInChildren<RectTransform>())
            {
                if (marker.name.StartsWith("Event Node "))
                    AssertScreenAspect(marker, 1f, viewport.Camera, context);
                else if (marker.name.StartsWith("Event Icon "))
                    AssertScreenAspect(marker, marker.rect.width / marker.rect.height, viewport.Camera, context);
            }
            var corners = new Vector3[4];
            RectTransform frame = Find<RectTransform>("Procedural Sea Chart Frame");
            AssertInsidePanel(RectIn(viewport.GameRoot, frame), viewport.GameRoot, context + " visible sea stays on the game surface");
            frame.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector3 pixel = viewport.Camera.WorldToScreenPoint(corner);
                Assert.That(pixel.x, Is.InRange(-.01f, viewport.Width + .01f), context + " visible sea x");
                Assert.That(pixel.y, Is.InRange(-.01f, viewport.Height + .01f), context + " visible sea y");
            }

            // Exercise the production screen-to-map conversion with its real event camera.
            // Temporarily observe the callback so resizing never changes the voyage or rebuilds UI.
            FieldInfo callback = typeof(MapCourseInputSurface).GetField("selectLocalPoint", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo target = typeof(MapCourseInputSurface).GetField("mapRect", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(target.GetValue(input), Is.SameAs(map), context + " bound map");
            var original = (System.Action<Vector2>)callback.GetValue(input);
            var raycaster = viewport.Canvas.GetComponent<GraphicRaycaster>();
            Assert.That(raycaster.eventCamera, Is.SameAs(viewport.Camera), context);
            if (input.GetComponent<Graphic>().raycastTarget)
            {
                // The input surface still uses logical map coordinates. Its
                // invisible gutter must not accept clicks beyond the sea clip.
                foreach (Vector2 direction in new[] { Vector2.left, Vector2.right, Vector2.down, Vector2.up })
                {
                    Vector2 edge = frame.rect.center + Vector2.Scale(direction, frame.rect.size * .5f);
                    foreach (bool inside in new[] { true, false })
                    {
                        Vector2 local = edge + direction * (inside ? -1f : 1f);
                        Vector2 pixel = viewport.Camera.WorldToScreenPoint(frame.TransformPoint(local));
                        var pointer = new PointerEventData(viewport.Events) { position = pixel };
                        var hits = new System.Collections.Generic.List<RaycastResult>();
                        viewport.Events.RaycastAll(pointer, hits);
                        Assert.That(hits.Any(hit => hit.gameObject == input.gameObject), Is.EqualTo(inside),
                            context + " course input " + (inside ? "inside " : "outside ") + direction + " sea edge");
                    }
                }
            }
            try
            {
                foreach (Vector2 normalized in new[] { Vector2.zero, new Vector2(-.45f, -.45f),
                    new Vector2(-.45f, .45f), new Vector2(.45f, -.45f), new Vector2(.45f, .45f) })
                {
                    Vector3 world = frame.TransformPoint(frame.rect.center + Vector2.Scale(normalized, frame.rect.size));
                    Vector2 local = map.InverseTransformPoint(world);
                    Vector2 observed = new Vector2(float.NaN, float.NaN);
                    input.Initialize(map, value => observed = value);
                    Vector2 pixel = viewport.Camera.WorldToScreenPoint(world);
                    var pointer = new PointerEventData(viewport.Events) { position = pixel,
                        pointerPressRaycast = new RaycastResult { module = raycaster, gameObject = input.gameObject } };
                    input.OnPointerClick(pointer);
                    Assert.That(Vector2.Distance(observed, local), Is.LessThan(.02f), context + " pointer " + local);
                }
            }
            finally { input.Initialize(map, original); }
        }

        private sealed class DashboardViewport : System.IDisposable
        {
            private static readonly MethodInfo AddRaycaster = typeof(RaycasterManager)
                .GetMethod("AddRaycaster", BindingFlags.Static | BindingFlags.NonPublic);
            private static readonly MethodInfo RemoveRaycaster = typeof(RaycasterManager)
                .GetMethod("RemoveRaycasters", BindingFlags.Static | BindingFlags.NonPublic);
            private readonly System.Collections.Generic.HashSet<BaseRaycaster> registeredRaycasters =
                new System.Collections.Generic.HashSet<BaseRaycaster>();
            private readonly Scene scene;
            private RenderTexture target;
            public readonly Canvas Canvas;
            public readonly Camera Camera;
            public readonly EventSystem Events;
            public RectTransform GameRoot => Canvas.GetComponentsInChildren<RectTransform>(true)
                .Single(rect => rect.name == FixedGameViewport.Name);
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
                    foreach (NavigationDashboardLayout layout in Canvas.GetComponentsInChildren<NavigationDashboardLayout>())
                        layout.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
                    foreach (Text label in Canvas.GetComponentsInChildren<Text>()) label.SetAllDirty();
                    UnityEngine.Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(Camera, new RenderPipeline.StandardRequest { destination = target });
                    else Camera.Render();
                }
                RegisterRaycasters();
                float scale = Mathf.Min(width / 1920f, height / 1080f);
                Assert.That(Canvas.renderingDisplaySize.x, Is.EqualTo(width).Within(.5f));
                Assert.That(Canvas.renderingDisplaySize.y, Is.EqualTo(height).Within(.5f));
                Assert.That(Canvas.pixelRect.width, Is.EqualTo(width).Within(.5f));
                Assert.That(Canvas.pixelRect.height, Is.EqualTo(height).Within(.5f));
                Assert.That(Canvas.scaleFactor, Is.EqualTo(scale).Within(.001f));
                Assert.That(Canvas.GetComponent<RectTransform>().rect.width, Is.EqualTo(width / scale).Within(.1f));
                Assert.That(Canvas.GetComponent<RectTransform>().rect.height, Is.EqualTo(height / scale).Within(.1f));
                AssertFixedGameSurface(width, height, scale);
            }

            private void AssertFixedGameSurface(int width, int height, float scale)
            {
                string context = width + "x" + height;
                RectTransform root = GameRoot;
                Assert.That(root.rect.width, Is.EqualTo(1920f).Within(.01f), context + " logical game width");
                Assert.That(root.rect.height, Is.EqualTo(1080f).Within(.01f), context + " logical game height");
                Assert.That(root.GetComponent<RectMask2D>(), Is.Not.Null, context + " game content stays out of the bars");
                var expected = new Rect((width - 1920f * scale) * .5f, (height - 1080f * scale) * .5f,
                    1920f * scale, 1080f * scale);
                var corners = new Vector3[4];
                root.GetWorldCorners(corners);
                Vector3 lower = Camera.WorldToScreenPoint(corners[0]);
                Vector3 upper = Camera.WorldToScreenPoint(corners[2]);
                Assert.That(lower.x, Is.EqualTo(expected.xMin).Within(.1f), context + " centered game left");
                Assert.That(lower.y, Is.EqualTo(expected.yMin).Within(.1f), context + " centered game bottom");
                Assert.That(upper.x, Is.EqualTo(expected.xMax).Within(.1f), context + " centered game right");
                Assert.That(upper.y, Is.EqualTo(expected.yMax).Within(.1f), context + " centered game top");
                Assert.That((upper.x - lower.x) / (upper.y - lower.y), Is.EqualTo(16f / 9f).Within(.0001f), context);

                Graphic bars = Canvas.GetComponentsInChildren<Graphic>(true).Single(item => item.name == "Game Letterbox");
                Assert.That(bars.isActiveAndEnabled, Is.True, context);
                Assert.That(bars.color, Is.EqualTo(Color.black), context + " opaque black bars");
                Assert.That(bars.raycastTarget, Is.False, context + " bars cannot intercept input");
                RectTransform canvasRect = Canvas.GetComponent<RectTransform>();
                Rect barBounds = RectIn(canvasRect, bars.rectTransform);
                Assert.That(barBounds.xMin, Is.EqualTo(canvasRect.rect.xMin).Within(.01f), context);
                Assert.That(barBounds.xMax, Is.EqualTo(canvasRect.rect.xMax).Within(.01f), context);
                Assert.That(barBounds.yMin, Is.EqualTo(canvasRect.rect.yMin).Within(.01f), context);
                Assert.That(barBounds.yMax, Is.EqualTo(canvasRect.rect.yMax).Within(.01f), context);
                Transform overlay = Canvas.transform.Find("Game Letterbox Overlay");
                Assert.That(overlay, Is.Not.Null, context + " black bars must cover independently sorted popups");
                var probes = new System.Collections.Generic.List<Vector2>();
                if (expected.xMin > 1f)
                    foreach (float y in new[] { .1f, .5f, .9f })
                    {
                        probes.Add(new Vector2(expected.xMin * .5f, height * y));
                        probes.Add(new Vector2((expected.xMax + width) * .5f, height * y));
                    }
                if (expected.yMin > 1f)
                    foreach (float x in new[] { .1f, .5f, .9f })
                    {
                        probes.Add(new Vector2(width * x, expected.yMin * .5f));
                        probes.Add(new Vector2(width * x, (expected.yMax + height) * .5f));
                    }
                foreach (Vector2 pixel in probes)
                {
                    var hits = new System.Collections.Generic.List<RaycastResult>();
                    var pointer = new PointerEventData(Events) { position = pixel };
                    Events.RaycastAll(pointer, hits);
                    Assert.That(hits, Is.Not.Empty, context + " black bar must block input at " + pixel);
                    Assert.That(hits[0].gameObject.transform.IsChildOf(overlay), Is.True,
                        context + " topmost hit must be the black bar at " + pixel);
                    Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.Null,
                        context + " black bar click cannot reach an underlying action");
                    Assert.That(ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler), Is.Null,
                        context + " blocked click must not invoke a control");
                }
                foreach (float x in new[] { .01f, .5f, .99f })
                    foreach (float y in new[] { .01f, .5f, .99f })
                    {
                        Vector2 pixel = new Vector2(expected.xMin + expected.width * x, expected.yMin + expected.height * y);
                        var hits = new System.Collections.Generic.List<RaycastResult>();
                        Events.RaycastAll(new PointerEventData(Events) { position = pixel }, hits);
                        Assert.That(hits.Any(hit => hit.gameObject.transform.IsChildOf(overlay)), Is.False,
                            context + " the black bars must leave game input clear at " + pixel);
                    }
                if (probes.Count == 0)
                {
                    foreach (Graphic edge in overlay.GetComponentsInChildren<Graphic>().Where(edge => edge.isActiveAndEnabled))
                        Assert.That(Mathf.Min(Mathf.Abs(edge.rectTransform.rect.width), Mathf.Abs(edge.rectTransform.rect.height)),
                            Is.LessThanOrEqualTo(.001f), context + " 16:9 must have no visible or interactive black bar");
                }
            }

            private void RegisterRaycasters()
            {
                // Preview scenes do not run BaseRaycaster's normal OnEnable.
                // Use the same manager API it calls; EventSystem still casts
                // and sorts every real root/child canvas through production code.
                foreach (BaseRaycaster raycaster in Canvas.GetComponentsInChildren<BaseRaycaster>())
                {
                    if (!raycaster.isActiveAndEnabled || RaycasterManager.GetRaycasters().Contains(raycaster)) continue;
                    AddRaycaster.Invoke(null, new object[] { raycaster });
                    registeredRaycasters.Add(raycaster);
                }
            }

            public void Dispose()
            {
                foreach (BaseRaycaster raycaster in registeredRaycasters)
                    RemoveRaycaster.Invoke(null, new object[] { raycaster });
                registeredRaycasters.Clear();
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }

        [Test]
        public void TutorialBossRewardKeepsTheChartRationDurationAndLookoutReadoutsVisible()
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
            foreach (string name in new[] { "Voyage Supply Endurance", "Navigation Controls", "Lookout Report Plaque",
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
        public void DistanceChanges_PreserveWorldVisualsAndLockedDestinationWithAccurateLeg()
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
                Assert.That(controller.Session.HasDirectCourseTarget, Is.True);
                Assert.That(controller.Session.DirectCourseTarget.X, Is.EqualTo(controller.Session.Map.Destination.X).Within(.001f));
                Assert.That(controller.Session.DirectCourseTarget.Y, Is.EqualTo(controller.Session.Map.Destination.Y).Within(.001f));
                SailingForecast forecast = controller.Session.GetForecast();
                Assert.That(forecast.ActualDistanceNm, Is.LessThanOrEqualTo(controller.Session.PlannedDistanceNm + .01f));
                Assert.That(Find<Text>("Distance Label").text, Does.Contain("锁定：" + controller.Session.Map.DestinationName));
                RectTransform sea = Find<RectTransform>("Ocean Tile Layer");
                CoreVoyageMap world = controller.Session.Map;
                Vector2 expected = Find<RectTransform>("Map Layer").InverseTransformPoint(sea.TransformPoint(new Vector2(
                    Mathf.Lerp(sea.rect.xMin, sea.rect.xMax, forecast.Target.X / world.WorldWidth),
                    Mathf.Lerp(sea.rect.yMin, sea.rect.yMax, forecast.Target.Y / world.WorldHeight))));
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
                Assert.That(time.text, Does.Contain(CoreVoyageSession.FormatGameDate(controller.Session.GameDateTime, true)));
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
        public void OrdinaryEvent_FillsRightColumn_PreservesChartAndHidesSailingControls()
        {
            Create(CoreGameplayPreviewScreen.Encounter);
            var viewport = Find<RectTransform>("Forward Viewport");
            Assert.That(Find<RectTransform>("Encounter Card").parent, Is.SameAs(viewport));
            Assert.That(Find<RectTransform>("Encounter Card").sizeDelta, Is.EqualTo(viewport.sizeDelta));
            Assert.That(Find<Button>("Execute Sail").interactable, Is.False);
            Assert.That(Find<Slider>("Distance Slider").interactable, Is.False);
            Assert.That(Find<Button>("Safe Return").interactable, Is.False);
            Assert.That(Find<Image>("Map Course Input").raycastTarget, Is.False);
            AssertNavigationControlsSuppressed();
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Navigation Journal"), Is.False);
            Assert.That(Find<RectTransform>("Map Layer"), Is.Not.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ClosingEncounter_RestoresThreePanelsWithoutChangingTheChart(bool skip)
        {
            Create();
            using (var viewport = new DashboardViewport(host))
            {
                foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 1024) })
                {
                    viewport.ResizeAndRender(size.x, size.y);
                    string context = size + (skip ? " skip" : " choice");
                    string[] names = { "Navigation Chart", "Navigation Controls", "Forward View Panel" };
                    Rect[] sailingBounds = names.Select(name => RectIn(viewport.GameRoot, Find<RectTransform>(name))).ToArray();
                    Rect mapBounds = RectIn(viewport.GameRoot, Find<RectTransform>("Procedural Sea Chart Frame"));
                    Set(controller.Session, "ActiveEncounter", new CoreEncounter("layout_transition", "海上漂流瓶", "航海事件",
                        "船员拾起一个漂流瓶。", false, false, 1, 0,
                        new CoreEncounterChoice("记录发现", "不消耗补给", 0, new VoyageEffect())));
                    Set(controller.Session, "Phase", CoreDemoPhase.Encounter);
                    Render();
                    Canvas.ForceUpdateCanvases();
                    Assert.That(RectIn(viewport.GameRoot, Find<RectTransform>("Navigation Chart")), Is.EqualTo(sailingBounds[0]),
                        context + " event must preserve the entire chart region");
                    Assert.That(RectIn(viewport.GameRoot, Find<RectTransform>("Procedural Sea Chart Frame")), Is.EqualTo(mapBounds),
                        context + " event must preserve chart contents as well as the outer panel");
                    AssertNavigationControlsSuppressed();

                    Find<Button>(skip ? "Skip Encounter" : "Encounter Choice 0").onClick.Invoke();
                    Canvas.ForceUpdateCanvases();

                    Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
                    var panels = names.Select(Find<RectTransform>).ToArray();
                    for (int index = 0; index < panels.Length; index++)
                        Assert.That(RectIn(viewport.GameRoot, panels[index]), Is.EqualTo(sailingBounds[index]), context + " " + names[index]);
                    AssertDashboardCoverage(viewport.GameRoot, panels, Find<RectTransform>("Voyage Header"), context);
                    CanvasGroup group = panels[1].GetComponent<CanvasGroup>();
                    if (group != null)
                    {
                        Assert.That(group.alpha, Is.EqualTo(1f));
                        Assert.That(group.interactable && group.blocksRaycasts, Is.True);
                    }
                    Assert.That(Find<Slider>("Distance Slider").IsInteractable(), Is.True);
                    Assert.That(Find<Image>("Map Course Input").raycastTarget, Is.True);
                    Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Encounter Card"), Is.False);
                }
            }
        }

        private void AssertNavigationControlsSuppressed()
        {
            RectTransform controls = Find<RectTransform>("Navigation Controls");
            Assert.That(controls.gameObject.activeInHierarchy, Is.True, "Keep the existing control hierarchy available for restoration.");
            CanvasGroup group = controls.GetComponent<CanvasGroup>();
            Assert.That(group, Is.Not.Null);
            Assert.That(group.alpha, Is.EqualTo(0f));
            Assert.That(group.interactable || group.blocksRaycasts, Is.False);
            foreach (Selectable control in controls.GetComponentsInChildren<Selectable>(true))
                Assert.That(control.IsInteractable(), Is.False, control.name + " must not accept input behind the event.");
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
