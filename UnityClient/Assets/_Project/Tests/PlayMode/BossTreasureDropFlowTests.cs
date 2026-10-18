using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Random;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator BossTreasureDrop_RealVictoryFeedbackShowsOnlyAwardedFragmentsAndBanksOnce()
        {
            foreach (uint seed in new[] { 0u, 1u })
            {
                yield return LoadResponsiveBattleScene();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                var session = new CoreVoyageSession();
                session.StartVoyage();
                typeof(CoreGameplayDemoController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, session);
                CoreMapNode boss = session.Map.Nodes.Single(node => node.IsBoss);
                typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, boss);
                typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session, boss.Encounter);
                typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, CoreDemoPhase.Encounter);
                Assert.That(session.ResolveChoice(0), Is.True);
                Assert.That(session.EndBattleTurn(), Is.True);
                typeof(CoreVoyageSession).GetField("bossTreasureRandom", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(session, new Mulberry32Random(seed));
                SetResponsiveHand(session, "card_chain_shot", "card_brace");
                session.Battle.DistanceMeters = 250f;
                session.Battle.EnemyHull = 1;
                session.Battle.EnemyBlock = 0;
                RenderResponsiveBattle(controller);
                yield return WaitResponsiveBattle(controller);

                int expected = seed == 0u ? 1 : 0;
                bool sawBanner = false;
                ClickAcceptanceButton("Battle Card 0");
                float deadline = Time.realtimeSinceStartup + 12f;
                while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
                {
                    GameObject banner = GameObject.Find("Battle Resolution Banner");
                    if (banner != null && !sawBanner)
                    {
                        sawBanner = true;
                        string message = string.Join("\n", banner.GetComponentsInChildren<Text>().Select(label => label.text));
                        Assert.That(message.Contains("藏宝图碎片 +1"), Is.EqualTo(expected == 1));
                        foreach (Text label in banner.GetComponentsInChildren<Text>())
                            Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1f));
                        NavalBattleVisualCapture.Capture(controller, expected == 1 ? "01_Boss_Fragment_Dropped.png" : "02_Boss_No_Fragment.png",
                            expected == 1 ? "首领胜利实际掉落一片" : "未掉落时不显示碎片奖励", "Battle Resolution Banner");
                    }
                    yield return null;
                }
                Assert.That(sawBanner, Is.True);
                Assert.That(controller.IsBattleAnimationPlaying, Is.False);
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
                Assert.That(session.VoyageTreasureFragments, Is.EqualTo(expected));
                Assert.That(session.TreasureFragments, Is.Zero);
                // Match the normal next-frame input boundary after the battle
                // coroutine has rebuilt the voyage and released its UI lock.
                yield return null;
                ClickAcceptanceButton("Safe Return"); yield return null;
                Button reveal = ActiveButton("Skip Settlement Reveal");
                if (reveal != null && reveal.IsInteractable()) { ClickAcceptanceButton("Skip Settlement Reveal"); yield return null; }
                Assert.That(session.Settlement.BankedFragments, Is.EqualTo(expected));
                string rewards = string.Join("\n", controller.GetComponentsInChildren<Text>().Select(label => label.text));
                Assert.That(rewards.Contains("藏宝图碎片 +1"), Is.EqualTo(expected == 1));
                Assert.That(rewards, Does.Not.Contain("藏宝图碎片 +0"));
                NavalBattleVisualCapture.Capture(controller, expected == 1 ? "03_Dropped_Fragment_Settlement.png" : "04_No_Fragment_Settlement.png",
                    "结算仅列出本航次实际所得", "Settlement Card");
                ClickAcceptanceButton("Return To Port"); yield return null;
                Assert.That(session.TreasureFragments, Is.EqualTo(expected));
                SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
                Assert.That(Object.FindFirstObjectByType<CoreGameplayDemoController>().Session.TreasureFragments, Is.EqualTo(expected));
                LogAssert.NoUnexpectedReceived();
            }
        }
    }
}
