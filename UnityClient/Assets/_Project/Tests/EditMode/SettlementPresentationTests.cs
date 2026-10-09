using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Core.Tests
{
    public sealed class SettlementPresentationTests
    {
        private GameObject host;
        private CoreGameplayDemoController controller;
        [TearDown] public void Cleanup() { if (host != null) Object.DestroyImmediate(host); }
        private void Create()
        {
            host = new GameObject("Settlement Presentation Test"); host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Settlement, CoreGameplayPreviewPort.GrayGull, 0);
            host.SetActive(true); Canvas.ForceUpdateCanvases();
        }
        private T Find<T>(string name) where T : Component => host.GetComponentsInChildren<T>(true).Single(x => x.name == name);
        private void Render(VoyageSettlement result)
        {
            typeof(CoreVoyageSession).GetProperty("Settlement").GetSetMethod(true).Invoke(controller.Session, new object[] { result });
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(controller, null);
            Canvas.ForceUpdateCanvases();
        }

        [Test]
        public void Settlement_LoadsNewArt_AndShowsClearlyMarkedSampleWithoutChangingRealReward()
        {
            Create();
            Assert.That(Resources.Load<Texture2D>("SettlementReveal/settlement_hold_v1"), Is.Not.Null);
            Assert.That(Resources.Load<Texture2D>("SettlementReveal/settlement_chest_v1"), Is.Not.Null);
            Assert.That(Resources.Load<Texture2D>("SettlementReveal/settlement_journal_v1"), Is.Not.Null);
            Assert.That(Find<Text>("Settlement Badge").text, Does.Contain("结算预览").And.Contain("不写入存档"));
            Assert.That(Find<Text>("Settlement Reward Gold").text, Is.EqualTo("+1,268"));
            Assert.That(controller.Session.Settlement.BankedGold, Is.Zero);
            Assert.That(Find<Text>("Settlement Title").fontSize, Is.GreaterThanOrEqualTo(64));
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Header"), Is.False,
                "No generic Header should remain above the cinematic results page.");
            Assert.That(Find<SettlementRevealController>("Settlement Card").IsComplete, Is.True);
            RectTransform backdrop = Find<RawImage>("Settlement Hold").rectTransform;
            RectTransform card = Find<RectTransform>("Settlement Card");
            Assert.That(card.anchoredPosition + backdrop.anchoredPosition, Is.EqualTo(Vector2.zero),
                "The raised results card must not shift its full-screen backdrop off the canvas.");
            Assert.That(backdrop.sizeDelta, Is.EqualTo(new Vector2(1920f, 1080f)));
        }

        [TestCase(0, "凯旋归港", true)]
        [TestCase(1, "战利归仓", true)]
        [TestCase(2, "整备，再启航", false)]
        [TestCase(3, "平安归航", false)]
        public void OutcomeHasAppropriateTitleAndCelebration(int scenario, string title, bool celebration)
        {
            Create(); Render(SettlementPreviewSamples.Create(scenario));
            Assert.That(Find<Text>("Settlement Title").text, Is.EqualTo(title));
            Assert.That(Find<SettlementCelebrationGraphic>("Settlement Gold Burst").gameObject.activeSelf, Is.EqualTo(celebration));
            Assert.That(Find<Button>("Return To Port").interactable, Is.True);
            if (!celebration) Assert.That(host.GetComponentsInChildren<RawImage>().Any(x => x.name == "Settlement Treasure Chest"), Is.False);
        }

        [Test]
        public void NonGoldRewardBecomesHeadline_AndZeroDoesNotPretendToBePositive()
        {
            Create(); Render(SettlementPreviewSamples.Create(4));
            Assert.That(Find<Text>("Settlement Featured Name").text, Is.EqualTo("木材"));
            Assert.That(Find<Text>("Settlement Reward Wood").text, Is.EqualTo("+38"));
            Assert.That(Find<Text>("Settlement Reward Gold").text, Is.EqualTo("0"));
        }

        [Test]
        public void TimelineIsExactMonotonicAndSkippable_WithoutRewardOrSaveMutation()
        {
            Create(); VoyageSettlement result = SettlementPreviewSamples.Create(0); Render(result);
            decimal before = controller.Session.ProfileGold;
            var reveal = Find<SettlementRevealController>("Settlement Card");
            reveal.SetElapsed(0); Assert.That(Find<Text>("Settlement Reward Gold").text, Is.EqualTo("0"));
            Assert.That(Find<Button>("Return To Port").interactable, Is.True, "Never force a player to wait for the reveal.");
            int previous = 0;
            for (float time = 0.6f; time < 2.8f; time += 0.11f)
            {
                reveal.SetElapsed(time);
                int current = int.Parse(Find<Text>("Settlement Reward Gold").text.Replace(",", "").TrimStart('+'));
                Assert.That(current, Is.InRange(previous, 1268)); previous = current;
            }
            reveal.CompleteImmediately(); reveal.CompleteImmediately();
            Assert.That(reveal.IsComplete, Is.True);
            Assert.That(Find<Text>("Settlement Reward Gold").text, Is.EqualTo("+1,268"));
            Assert.That(Find<Text>("Settlement Reward Fame").text, Is.EqualTo("+47"));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(before));
            Assert.That(result.BankedGold, Is.EqualTo(1268));
        }

        [Test]
        public void SafeReturnDoesNotPlaySinkingSound()
        {
            // Settlement uses its dedicated world cues, separate from battle
            // victory/discovery/sinking sounds. A failed voyage is a salvage
            // outcome here, even when the preceding battle already sank a ship.
            Assert.That(SettlementRevealAudio.OutcomeCue(SettlementPreviewSamples.Create(0)), Is.EqualTo("harbor_arrive"));
            Assert.That(SettlementRevealAudio.OutcomeCue(SettlementPreviewSamples.Create(1)), Is.EqualTo("settlement_reward"));
            Assert.That(SettlementRevealAudio.OutcomeCue(SettlementPreviewSamples.Create(2)), Is.EqualTo("settlement_failure"));
            VoyageSettlement treasure = SettlementPreviewSamples.Create(1);
            treasure.TreasureCompleted = true;
            Assert.That(SettlementRevealAudio.OutcomeCue(treasure), Is.EqualTo("settlement_treasure"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EarnedFragmentsAreVisibleInOrdinaryAndTutorialSettlement(bool tutorial)
        {
            Create();
            typeof(CoreGameplayDemoController).GetField("tutorialActive", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(controller, tutorial);
            if (tutorial)
            {
                typeof(CoreVoyageSession).GetProperty("GuidedTutorialLeg").GetSetMethod(true)
                    .Invoke(controller.Session, new object[] { CoreGuidedTutorialContent.BossVoyageLeg });
                typeof(CoreVoyageSession).GetProperty("GuidedTutorialBossDefeated").GetSetMethod(true)
                    .Invoke(controller.Session, new object[] { true });
            }
            VoyageSettlement result = SettlementPreviewSamples.Create(1);
            result.BankedFragments = 1;
            result.BankedPirateExperience = 15;
            Render(result);
            Text rewards = Find<Text>("Settlement Treasure Rewards");
            Assert.That(rewards.text, Does.Contain("藏宝图碎片 +1"));
            Assert.That(rewards.text, Does.Contain("海盗经验 +15"));
            Assert.That(rewards.preferredHeight, Is.LessThanOrEqualTo(rewards.rectTransform.rect.height + 1f));
            if (tutorial)
            {
                RectTransform component = Find<RectTransform>("Guided Tutorial Component Reward");
                RectTransform manifest = Find<RectTransform>("Settlement Treasure Manifest");
                Assert.That(component.parent, Is.SameAs(manifest.parent));
                Rect cardBounds = new Rect(component.anchoredPosition - component.sizeDelta * .5f, component.sizeDelta);
                Rect fragmentBounds = new Rect(manifest.anchoredPosition - manifest.sizeDelta * .5f, manifest.sizeDelta);
                Assert.That(cardBounds.Overlaps(fragmentBounds), Is.False,
                    "The actual tutorial component/valuable card must not obscure fragments or pirate XP.");
                Assert.That(Find<Text>("Guided Tutorial Reward Text").text, Does.Contain("破浪者的罗盘"));
            }
            Assert.That(controller.Session.TreasureFragments, Is.Zero,
                "Displaying the result must not award or bank the fragment a second time.");
        }

        [Test]
        public void ExperienceOnlySettlementDoesNotAnnounceAFragmentDrop()
        {
            Create();
            VoyageSettlement result = SettlementPreviewSamples.Create(1);
            result.BankedPirateExperience = 15;
            Render(result);
            Assert.That(Find<Text>("Settlement Treasure Rewards").text, Is.EqualTo("海盗经验 +15"));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void FinalTextAndActionsFitTheirAuthoredBounds(int scenario)
        {
            Create(); Render(SettlementPreviewSamples.Create(scenario, "盐冠港"));
            var card = Find<RectTransform>("Settlement Card");
            foreach (Text label in card.GetComponentsInChildren<Text>())
            {
                if (!label.gameObject.activeInHierarchy) continue;
                var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
                float required = label.cachedTextGeneratorForLayout.GetPreferredHeight(label.text, settings) / label.pixelsPerUnit;
                Assert.That(required, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 2), label.name + " clips vertically");
            }
            foreach (Button button in card.GetComponentsInChildren<Button>())
            {
                Vector3[] corners = new Vector3[4]; button.GetComponent<RectTransform>().GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 local = card.InverseTransformPoint(corner);
                    Assert.That(local.x, Is.InRange(-930f, 930f), button.name);
                    Assert.That(local.y, Is.InRange(-470f, 470f), button.name);
                }
            }
        }
    }
}

