using System.Linq;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class BossTreasureDropPreviewSceneTests
    {
        private const string Root = "Assets/_Project/Scenes/UIPreviewsForMe/";

        [TestCase("UI_TreasureMap.unity")]
        [TestCase("UITreasureMap/UITreasureMap_MapUnopened.unity")]
        public void SavedWorkbenchExplainsChanceAndRetainsLayout(string reference)
        {
            CheckScene(reference, controller =>
            {
                Text notice = Find(controller, "Treasure Workbench Notice");
                Assert.That(notice.text, Does.Contain("首领有 50% 概率掉落 1 个"));
                Assert.That(notice.preferredHeight, Is.LessThanOrEqualTo(notice.rectTransform.rect.height + 1f));
            });
        }

        [TestCase("TutorialVoyage/27_ThirdSettlement.unity")]
        [TestCase("TutorialVoyage/28_DrawChart.unity")]
        public void SavedTutorialSettlementShowsOneFragmentWithoutChangingItsActions(string reference)
        {
            CheckScene(reference, controller =>
            {
                Text rewards = Find(controller, "Settlement Treasure Rewards");
                Assert.That(rewards.text, Is.EqualTo("藏宝图碎片 +1"));
                Assert.That(rewards.preferredHeight, Is.LessThanOrEqualTo(rewards.rectTransform.rect.height + 1f));
                Assert.That(Find(controller, "Settlement Badge").text, Does.Contain("演示数据"));
                AssertRewardDoesNotOverlap(controller, "Settlement Treasure Manifest");
                Assert.That(controller.GetComponentsInChildren<Button>(true).Any(item => item.name == "Return To Port"), Is.True);
                if (reference.Contains("28_DrawChart"))
                    Assert.That(controller.GetComponentsInChildren<Button>(true).Any(item => item.name == "Draw Sea Chart"), Is.True);
            });
        }

        [Test]
        public void SavedTutorialReturnLogsFragmentAndPreservesRationDuration()
        {
            CheckScene("TutorialVoyage/26_ThirdReturn.unity", controller =>
            {
                Text log = Find(controller, "Voyage Log");
                Assert.That(log.text, Does.Contain("新手首领必定掉落：藏宝图碎片 +1（返航后入账）"));
                Assert.That(log.preferredHeight, Is.LessThanOrEqualTo(log.rectTransform.rect.height + 1f));
                Assert.That(Find(controller, "Water Food Endurance"), Is.Not.Null);
                foreach (string name in new[] { "Voyage Supply Endurance", "Voyage Journal Scroll", "Lookout Report Plaque", "Lookout Heading Plaque" })
                    AssertRewardDoesNotOverlap(controller, name);
                Assert.That(controller.GetComponentsInChildren<ScrollRect>(true).Single(item => item.name == "Voyage Journal Scroll").vertical, Is.True);
            });
        }

        private static Text Find(CoreGameplayDemoController controller, string name) =>
            controller.GetComponentsInChildren<Text>(true).Single(item => item.name == name);

        private static void AssertRewardDoesNotOverlap(CoreGameplayDemoController controller, string otherName)
        {
            RectTransform reward = controller.GetComponentsInChildren<RectTransform>(true)
                .Single(item => item.name == "Guided Tutorial Component Reward");
            RectTransform other = controller.GetComponentsInChildren<RectTransform>(true)
                .Single(item => item.name == otherName);
            Bounds rewardBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(controller.transform, reward);
            Bounds otherBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(controller.transform, other);
            Assert.That(rewardBounds.Intersects(otherBounds), Is.False, "Saved tutorial reward obscures " + otherName);
            Text label = Find(controller, "Guided Tutorial Reward Text");
            Assert.That(label.text, Does.Contain("破浪者的罗盘"));
            Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1f));
        }

        private static void CheckScene(string reference, System.Action<CoreGameplayDemoController> check)
        {
            Scene scene = EditorSceneManager.OpenScene(Root + reference, OpenSceneMode.Additive);
            try
            {
                CoreGameplayDemoController controller = scene.GetRootGameObjects()
                    .SelectMany(item => item.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
                Assert.That(controller.EditorPreviewPersistsGeneratedUi, Is.True);
                check(controller);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
