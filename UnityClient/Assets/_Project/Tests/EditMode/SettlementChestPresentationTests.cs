using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Core.Tests
{
    public sealed class SettlementChestPresentationTests
    {
        [Test]
        public void ActualRareRewardRemainsVisibleOnOverview_WhenTreasureObjectiveWasNotCompleted()
        {
            var host = new GameObject("Settlement Chest Actual Rare Reward Test");
            host.SetActive(false);
            try
            {
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Settlement, CoreGameplayPreviewPort.GrayGull, 0);
                host.SetActive(true);
                var result = new VoyageSettlement { Outcome = VoyageOutcome.SafeReturn, Destination = "断锚港",
                    Title = "安全返航", TreasureCompleted = false, BankedGold = 12, BankedGem = 8 };
                typeof(CoreVoyageSession).GetProperty("Settlement").GetSetMethod(true).Invoke(controller.Session, new object[] { result });
                decimal goldBefore = controller.Session.ProfileGold;
                typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                Canvas.ForceUpdateCanvases();
                Text rewards = host.GetComponentsInChildren<Text>(true).Single(text => text.name == "Settlement Treasure Rewards");
                Assert.That(rewards.gameObject.activeInHierarchy, Is.True);
                Assert.That(rewards.text, Does.Contain("宝石 +8"));
                var sequence = host.GetComponentsInChildren<SettlementChestSequenceController>(true).Single();
                Assert.That(sequence.IsComplete, Is.True, "The editor preview should show the final overview.");
                Assert.That(sequence.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(goldBefore));
                Assert.That(result.BankedGem, Is.EqualTo(8));
                Assert.That(result.TreasureCompleted, Is.False);
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
