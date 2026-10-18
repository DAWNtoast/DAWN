using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class SettlementChestSequencePlayModeTests
    {
        private GameObject host;
        private CanvasGroup intro, overview;
        private Text[] amounts;
        private Button skip;
        private int completions, sounds;
        private SettlementChestSequenceController sequence;
        private float previousScale;

        [SetUp] public void Setup() { previousScale = Time.timeScale; completions = 0; sounds = 0; }
        [TearDown] public void Cleanup() { Time.timeScale = previousScale; if (host != null) Object.DestroyImmediate(host); }

        private T Child<T>(string name, Transform parent = null) where T : Component
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent == null ? host.transform : parent, false);
            return child.AddComponent<T>();
        }

        private void Create(VoyageSettlement result)
        {
            host = new GameObject("Isolated Settlement Chest Test", typeof(RectTransform));
            intro = Child<CanvasGroup>("Intro"); overview = Child<CanvasGroup>("Overview");
            var chest = new GameObject("Chest", typeof(RectTransform)).GetComponent<RectTransform>(); chest.SetParent(intro.transform, false);
            var plan = SettlementChestRewardPlan.Create(result);
            var rewards = new RectTransform[plan.Items.Count]; var groups = new CanvasGroup[plan.Items.Count];
            amounts = new Text[plan.Items.Count]; var positions = new Vector2[plan.Items.Count];
            for (int i = 0; i < plan.Items.Count; i++)
            {
                groups[i] = Child<CanvasGroup>("Reward " + i, intro.transform); rewards[i] = groups[i].GetComponent<RectTransform>();
                amounts[i] = Child<Text>("Amount " + i, rewards[i]); amounts[i].font = GameTypography.InterfaceFont;
                positions[i] = new Vector2(-300 + i * 150, -200);
            }
            var title = Child<Text>("Title", intro.transform); title.font = GameTypography.InterfaceFont;
            var status = Child<Text>("Status", intro.transform); status.font = GameTypography.InterfaceFont;
            var fireworks = Child<SettlementFireworksGraphic>("Fireworks", intro.transform);
            skip = Child<Button>("Skip", intro.transform);
            sequence = host.AddComponent<SettlementChestSequenceController>();
            sequence.Configure(plan, intro, overview, chest, rewards, groups, amounts, positions, title, status,
                fireworks, skip, (_, __) => sounds++, () => completions++, true);
        }

        [UnityTest]
        public IEnumerator ChestAdvancesOnUnscaledTime_ThenAutomaticallyShowsOverviewWithoutConfirmation()
        {
            Create(new VoyageSettlement { Outcome = VoyageOutcome.SafeReturn, BankedGold = 10, BankedWood = 2 });
            Time.timeScale = 0;
            float before = sequence.Elapsed;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(sequence.Elapsed, Is.GreaterThan(before));
            Assert.That(overview.interactable, Is.False);
            Assert.That(overview.blocksRaycasts, Is.False);
            sequence.SetElapsed(sequence.Duration - .15f);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(sequence.IsComplete, Is.True);
            Assert.That(overview.alpha, Is.EqualTo(1f).Within(.001f));
            Assert.That(overview.interactable, Is.True);
            Assert.That(overview.blocksRaycasts, Is.True);
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(intro.gameObject.activeSelf && intro.alpha > 0f, Is.False);
            int completedSoundCount = sounds;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(sounds, Is.EqualTo(completedSoundCount), "The finite sequence must stop emitting sound cues.");
            Assert.That(completions, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SkipIsIdempotent_AndDoesNotPlayAllMissedSounds()
        {
            Create(new VoyageSettlement { Outcome = VoyageOutcome.SafeReturn, BankedGold = int.MaxValue, BankedWood = 42 });
            int before = sounds;
            sequence.CompleteImmediately(); sequence.CompleteImmediately(); skip.onClick.Invoke();
            yield return null;
            Assert.That(sequence.IsComplete, Is.True);
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(sounds, Is.EqualTo(before));
            Assert.That(amounts.Any(text => text.text.Contains("2,147,483,647")), Is.True);
            Assert.That(overview.interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator NoRewardsImmediatelyShowsOverviewWithoutPlayingAnEmptyChestSequence()
        {
            Create(new VoyageSettlement { Outcome = VoyageOutcome.SafeReturn });
            yield return null;
            Assert.That(sequence.Duration, Is.Zero);
            Assert.That(sequence.IsComplete, Is.True);
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(overview.alpha, Is.EqualTo(1f).Within(.001f));
            Assert.That(sounds, Is.Zero);
        }

        [UnityTest]
        public IEnumerator JackpotHasAnticipationBeforeFireworks_AndMoreTimeThanOrdinaryRewards()
        {
            Create(new VoyageSettlement { Outcome = VoyageOutcome.SafeReturn, BankedGold = 20000, BankedWood = 7 });
            Assert.That(sequence.JackpotStart, Is.GreaterThan(0f));
            Assert.That(sequence.FireworksStart, Is.GreaterThanOrEqualTo(sequence.JackpotStart));
            Assert.That(sequence.Duration, Is.GreaterThan(sequence.FireworksStart + .5f));
            sequence.SetElapsed(sequence.JackpotStart - .1f);
            Assert.That(sequence.IsComplete, Is.False);
            Assert.That(overview.interactable, Is.False);
            sequence.CompleteImmediately();
            yield return null;
            Assert.That(completions, Is.EqualTo(1));
        }
    }
}
