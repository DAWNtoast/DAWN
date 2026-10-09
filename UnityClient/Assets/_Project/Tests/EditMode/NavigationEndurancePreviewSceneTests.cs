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
        public void SavedNavigation_RationDurationIsVisibleBelowScrollableJournal(string relative)
        {
            AssertSavedCanvasScale(Root + relative);
            Scene scene = EditorSceneManager.OpenScene(Root + relative, OpenSceneMode.Additive);
            try
            {
                CoreGameplayDemoController controller = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
                Assert.That(controller.EditorPreviewPersistsGeneratedUi, Is.True);
                RectTransform journal = controller.GetComponentsInChildren<RectTransform>(true)
                    .Single(item => item.name == "Navigation Journal");
                RectTransform endurance = journal.Find("Voyage Supply Endurance") as RectTransform;
                Assert.That(endurance, Is.Not.Null, relative);
                Assert.That(endurance.parent, Is.SameAs(journal), "The duration must remain visible when the journal scrolls.");
                AssertInside(endurance, journal, relative);
                RectTransform scroll = journal.Find("Voyage Journal Scroll") as RectTransform;
                Assert.That(scroll, Is.Not.Null);
                Assert.That(LocalBounds(endurance).Overlaps(LocalBounds(scroll)), Is.False,
                    relative + " must keep rations clear of the log viewport.");
                Assert.That(LocalBounds(endurance).yMax, Is.LessThan(LocalBounds(scroll).yMin));
                foreach (string name in new[] { "Log Title", "Cargo Status", "Voyage Cargo Summary" })
                    Assert.That(LocalBounds(endurance).Overlaps(LocalBounds((RectTransform)journal.Find(name))), Is.False,
                        relative + " must not cover " + name);

                ScrollRect logScroll = scroll.GetComponent<ScrollRect>();
                Assert.That(logScroll.vertical, Is.True);
                Assert.That(logScroll.content.name, Is.EqualTo("Voyage Log"));
                Assert.That(logScroll.viewport, Is.SameAs(scroll));
                Assert.That(scroll.GetComponent<RectMask2D>(), Is.Not.Null);
                Assert.That(logScroll.content.GetComponent<Text>().text, Is.Not.Empty);

                Text waterFood = endurance.Find("Water Food Endurance")?.GetComponent<Text>();
                Text summary = endurance.Find("Supply Endurance Summary")?.GetComponent<Text>();
                Assert.That(waterFood, Is.Not.Null);
                Assert.That(summary, Is.Not.Null);
                Assert.That(waterFood.text, Does.Contain("水"));
                Assert.That(waterFood.text, Does.Contain("食物"));
                Assert.That(summary.text, Is.Not.Empty);
                foreach (Text label in new[] { waterFood, summary })
                {
                    AssertInside(label.rectTransform, endurance, relative + "/" + label.name);
                    Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1f),
                        relative + "/" + label.name + " must display its full duration or estimate.");
                    Assert.That(label.raycastTarget, Is.False, "The duration readout must not intercept navigation input.");
                }
                Assert.That(LocalBounds(waterFood.rectTransform).Overlaps(LocalBounds(summary.rectTransform)), Is.False);
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
            Rect bounds = LocalBounds(child);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(parent.rect.xMin - 0.01f), reason);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(parent.rect.xMax + 0.01f), reason);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(parent.rect.yMin - 0.01f), reason);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(parent.rect.yMax + 0.01f), reason);
        }

        private static Rect LocalBounds(RectTransform item) => new Rect(
            item.anchoredPosition - Vector2.Scale(item.sizeDelta, item.pivot), item.sizeDelta);
    }
}
