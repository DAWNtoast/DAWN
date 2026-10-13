using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    [Ignore("UIPreviewsForMe was retired and cleared on 2026-10-13; these historical saved-scene fixtures are no longer maintained.")]
    public sealed class NavigationEndurancePreviewSceneTests
    {
        private const string Root = "Assets/_Project/Scenes/UIPreviewsForMe/";

        [TestCase("UI_Navigation.unity")]
        [TestCase("UI_Encounter.unity")]
        [TestCase("UINavigation/UINavigation_Exploration.unity")]
        [TestCase("UINavigation/UINavigation_ApproachingEncounter.unity")]
        [TestCase("UINavigation/UINavigation_Logbook.unity")]
        [TestCase("UINavigation/UINavigation_TreasureExploration.unity")]
        [TestCase("UIEncounter/UIEncounter_Choice.unity")]
        [TestCase("UIEncounter/UIEncounter_RiskPreview.unity")]
        [TestCase("UIEncounter/UIEncounter_Resolved.unity")]
        [TestCase("UIEncounter/UIEncounter_TreasureClue.unity")]
        [TestCase("UIEncounter/UIEncounter_TreasureCoreLocked.unity")]
        [TestCase("UIEncounter/UIEncounter_TreasureCoreReady.unity")]
        [TestCase("TutorialVoyage/02_FirstSail.unity")]
        [TestCase("TutorialVoyage/03_FirstMandatory.unity")]
        [TestCase("TutorialVoyage/04_FirstOptional.unity")]
        [TestCase("TutorialVoyage/05_FirstCrewEvent.unity")]
        [TestCase("TutorialVoyage/06_FirstReturn.unity")]
        [TestCase("TutorialVoyage/13_SecondSail.unity")]
        [TestCase("TutorialVoyage/14_SecondCrewEvent.unity")]
        [TestCase("TutorialVoyage/24_ThirdSail.unity")]
        [TestCase("TutorialVoyage/26_ThirdReturn.unity")]
        public void SavedNavigation_ThreePanelsTileTheBodyAndKeepRationDurationWithoutJournal(string relative)
        {
            AssertSavedCanvasScale(Root + relative);
            Scene scene = EditorSceneManager.OpenScene(Root + relative, OpenSceneMode.Additive);
            try
            {
                CoreGameplayDemoController controller = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
                Assert.That(controller.EditorPreviewPersistsGeneratedUi, Is.True);
                RectTransform chart = controller.GetComponentsInChildren<RectTransform>(true)
                    .Single(item => item.name == "Navigation Chart");
                NavigationMapViewport geometry = chart.GetComponentInChildren<NavigationMapViewport>(true);
                Assert.That(geometry, Is.Not.Null, relative + " must retain its map geometry after scene reload");
                Assert.That(geometry.Columns, Is.EqualTo(48), relative);
                Assert.That(geometry.Rows, Is.EqualTo(32).Or.EqualTo(36), relative + " known legacy or expanded world");
                VoyageMapTileLayerGraphic[] batches = chart.GetComponentsInChildren<VoyageMapTileLayerGraphic>(true);
                Assert.That(batches.Single(batch => batch.name == "Chart Fog Tiles").TileCount,
                    Is.EqualTo(geometry.FineColumns * geometry.FineRows), relative + " must retain fine fog data after scene reload");
                Assert.That(batches.Single(batch => batch.name == "Chart Grid Lines").TileCount,
                    Is.EqualTo(geometry.FineColumns + geometry.FineRows + 2), relative + " must retain fine grid data after scene reload");
                foreach (VoyageMapTileLayerGraphic batch in batches)
                {
                    Assert.That(batch.TileCount * 4, Is.LessThan(65000), relative + "/" + batch.name);
                    Assert.That(batch.raycastTarget, Is.False, "Chart detail must not block course selection.");
                }
                RectTransform controls = controller.GetComponentsInChildren<RectTransform>(true)
                    .Single(item => item.name == "Navigation Controls");
                RectTransform lookout = controller.GetComponentsInChildren<RectTransform>(true)
                    .Single(item => item.name == "Forward View Panel");
                RectTransform header = controller.GetComponentsInChildren<RectTransform>(true)
                    .Single(item => item.name == "Voyage Header");
                RectTransform body = chart.parent as RectTransform;
                Assert.That(controls.parent, Is.SameAs(body));
                Assert.That(lookout.parent, Is.SameAs(body));
                Rect available = Rect.MinMaxRect(body.rect.xMin, body.rect.yMin, body.rect.xMax, BoundsIn(body, header).yMin);
                Rect chartBounds = BoundsIn(body, chart), controlBounds = BoundsIn(body, controls), lookoutBounds = BoundsIn(body, lookout);
                AssertRect(chartBounds, new Rect(available.xMin, available.yMin, available.width * .6625f, available.height), relative);
                AssertRect(controlBounds, new Rect(chartBounds.xMax, available.yMin, available.width * .3375f, available.height * .48f), relative);
                AssertRect(lookoutBounds, new Rect(chartBounds.xMax, controlBounds.yMax, available.width * .3375f, available.height * .52f), relative);
                RectTransform endurance = chart.Find("Voyage Supply Endurance") as RectTransform;
                Assert.That(endurance, Is.Not.Null, relative);
                Assert.That(endurance.parent, Is.SameAs(chart), "The duration belongs beside the chart title.");
                AssertInside(endurance, chart, relative);
                RectTransform mapFrame = chart.Find("Procedural Sea Chart Frame") as RectTransform;
                Assert.That(BoundsIn(chart, endurance).xMin,
                    Is.GreaterThanOrEqualTo(BoundsIn(chart, (RectTransform)chart.Find("Map Title")).xMax - .01f), relative);
                Assert.That(BoundsIn(chart, endurance).yMin,
                    Is.GreaterThanOrEqualTo(BoundsIn(chart, mapFrame).yMax - .01f), relative);
                foreach (string retired in new[] { "Map Seed", "Sea Conditions Legend", "Global Wind Direction Arrow",
                    "Cargo Status", "Supply Endurance Summary", "Navigation Journal", "Voyage Journal Scroll",
                    "Voyage Log", "Log Title", "Voyage Cargo Summary", "Encounter Journal Heading", "Encounter Terrain",
                    "Encounter Themed Frame" })
                    Assert.That(controller.GetComponentsInChildren<Transform>(true).Any(item => item.name == retired), Is.False,
                        relative + " should no longer contain " + retired);
                Transform hint = chart.Find("Map Course Hint");
                if (hint != null)
                    Assert.That(hint.GetComponent<Text>().text, Does.Contain("藏宝"), relative + " ordinary navigation hints are retired");
                Rect stripBounds = BoundsIn(chart, (RectTransform)chart.Find("Navigation Map Title Strip"));
                Assert.That(stripBounds.xMin, Is.EqualTo(chart.rect.xMin + 4f).Within(.02f), relative + " left gold frame");
                Assert.That(stripBounds.xMax, Is.EqualTo(chart.rect.xMax - 4f).Within(.02f), relative + " right gold frame");
                Rect availableMap = Rect.MinMaxRect(stripBounds.xMin,
                    chart.rect.yMin + 4f + (hint == null ? 0f : 40f), stripBounds.xMax, stripBounds.yMin);
                AssertRect(BoundsIn(chart, mapFrame), availableMap, relative + " saved map frame fills the chart");
                Assert.That(mapFrame.GetComponent<RectMask2D>(), Is.Not.Null, relative + " saved map overflow is clipped");
                RectTransform map = mapFrame.Find("Map Layer") as RectTransform;
                foreach (string name in new[] { "Ocean Tile Layer", "Visibility Mask Layer" })
                {
                    Rect seaBounds = BoundsIn(chart, (RectTransform)map.Find(name));
                    string reason = relative + " saved " + name + " covers the complete clipped viewport";
                    Assert.That(seaBounds.xMin, Is.LessThanOrEqualTo(availableMap.xMin + .02f), reason);
                    Assert.That(seaBounds.xMax, Is.GreaterThanOrEqualTo(availableMap.xMax - .02f), reason);
                    Assert.That(seaBounds.yMin, Is.LessThanOrEqualTo(availableMap.yMin + .02f), reason);
                    Assert.That(seaBounds.yMax, Is.GreaterThanOrEqualTo(availableMap.yMax - .02f), reason);
                    Assert.That(seaBounds.width / geometry.Columns,
                        Is.EqualTo(seaBounds.height / geometry.Rows).Within(.001f), reason + " square terrain cells");
                    Assert.That(seaBounds.width / geometry.FineColumns,
                        Is.EqualTo(seaBounds.height / geometry.FineRows).Within(.001f), reason + " square exploration cells");
                }
                if (hint != null)
                    Assert.That(BoundsIn(chart, (RectTransform)hint).yMax, Is.LessThanOrEqualTo(availableMap.yMin + .02f),
                        relative + " treasure hint remains below the sea");
                Text waterFood = endurance.Find("Water Food Endurance")?.GetComponent<Text>();
                Assert.That(waterFood, Is.Not.Null);
                Assert.That(waterFood.text, Does.Contain("水"));
                Assert.That(waterFood.text, Does.Contain("食物"));
                foreach (Text label in new[] { waterFood })
                {
                    AssertInside(label.rectTransform, endurance, relative + "/" + label.name);
                    Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1f),
                        relative + "/" + label.name + " must display its full duration or estimate.");
                    Assert.That(label.raycastTarget, Is.False, "The duration readout must not intercept navigation input.");
                }
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void AssertSavedCanvasScale(string path)
        {
            // The capture renderer normalizes Canvas scale. Inspect serialized
            // data as well so a zero-scale saved page cannot pass via capture.
            string text = File.ReadAllText(path).Replace("\r\n", "\n");
            Match canvas = Regex.Match(text, @"(?ms)^--- !u!1 &(?<id>\d+)[^\n]*\nGameObject:\n(?:(?!^---).)*?^  m_Name: ['""]?" +
                Regex.Escape(CoreGameplayDemoController.EditorPreviewCanvasName) + @"['""]?$");
            Assert.That(canvas.Success, Is.True, path);
            string owner = "m_GameObject: {fileID: " + canvas.Groups["id"].Value + "}";
            Match rect = Regex.Matches(text, @"(?ms)^--- !u!224 &\d+[^\n]*\n(?<body>.*?)(?=^---|\z)")
                .Cast<Match>().Single(item => item.Groups["body"].Value.Contains(owner));
            Match scale = Regex.Match(rect.Groups["body"].Value,
                @"m_LocalScale: \{x: (?<x>[^,]+), y: (?<y>[^,]+), z: (?<z>[^}]+)\}");
            Assert.That(scale.Success, Is.True, path);
            foreach (string axis in new[] { "x", "y", "z" })
            {
                float value = float.Parse(scale.Groups[axis].Value, CultureInfo.InvariantCulture);
                Assert.That(value, Is.GreaterThan(0f).And.LessThan(float.PositiveInfinity),
                    path + " must retain a visible saved Canvas scale.");
            }
        }

        private static void AssertInside(RectTransform child, RectTransform parent, string reason)
        {
            // Text density compensation enlarges its authored rectangle and
            // scales it back down. Compare its displayed bounds in the parent's
            // coordinates, just as we do for the scaled dashboard panels.
            Rect bounds = BoundsIn(parent, child);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(parent.rect.xMin - 0.01f), reason);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(parent.rect.xMax + 0.01f), reason);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(parent.rect.yMin - 0.01f), reason);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(parent.rect.yMax + 0.01f), reason);
        }

        private static void AssertRect(Rect actual, Rect expected, string context)
        {
            Assert.That(actual.xMin, Is.EqualTo(expected.xMin).Within(.02f), context);
            Assert.That(actual.yMin, Is.EqualTo(expected.yMin).Within(.02f), context);
            Assert.That(actual.xMax, Is.EqualTo(expected.xMax).Within(.02f), context);
            Assert.That(actual.yMax, Is.EqualTo(expected.yMax).Within(.02f), context);
        }

        private static Rect BoundsIn(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector3[] points = corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y),
                points.Max(point => point.x), points.Max(point => point.y));
        }
    }
}
