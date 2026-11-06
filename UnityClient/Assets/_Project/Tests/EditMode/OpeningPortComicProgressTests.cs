using System;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class OpeningPortComicProgressTests
    {
        [Test]
        public void Opening_RemembersEachAcknowledgedPageAndCompletesOnlyAtTheEnd()
        {
            string json = string.Empty;
            Func<OpeningPortComicProgress> reload = () => new OpeningPortComicProgress("captain", () => json, value => json = value);
            Assert.That(reload().PendingStoryKey, Is.Empty, "Old campaigns are never forced to replay an opening.");
            Assert.That(reload().TryQueue("opening"), Is.True);
            for (int page = 0; page < 4; page++)
            {
                Assert.That(reload().PendingPage, Is.EqualTo(page));
                Assert.That(reload().HasRead("opening"), Is.False);
                Assert.That(reload().Advance("opening", page - 1), Is.False);
                Assert.That(reload().Advance("opening", page), Is.True);
            }
            Assert.That(reload().HasRead("opening"), Is.True);
            Assert.That(reload().PendingStoryKey, Is.Empty);
            Assert.That(reload().TryQueue("opening"), Is.False);
        }

        [TestCase("opening")]
        [TestCase("arrival:white_shell")]
        [TestCase("arrival:bronze_bell")]
        [TestCase("arrival:salt_crown")]
        [TestCase("arrival:twilight_tide")]
        [TestCase("arrival:gray_gull")]
        public void EveryPanel_ResumesExactlyAndOnlyTheLastCompletesTheStory(string key)
        {
            string json = string.Empty;
            Func<OpeningPortComicProgress> reload = () => new OpeningPortComicProgress("captain", () => json, value => json = value);
            Assert.That(reload().TryQueue(key), Is.True);
            for (int page = 0; page < OpeningPortComicProgress.PageCount(key); page++)
                for (int panel = 0; panel < ComicPanelCatalog.QuadPanelCount; panel++)
                {
                    Assert.That(reload().PendingPage, Is.EqualTo(page));
                    Assert.That(reload().PendingPanel, Is.EqualTo(panel));
                    Assert.That(reload().HasRead(key), Is.False);
                    Assert.That(reload().AdvancePanel(key, page, panel - 1), Is.False);
                    Assert.That(reload().AdvancePanel(key, page - 1, panel), Is.False);
                    Assert.That(reload().AdvancePanel(key, page, panel), Is.True);
                    Assert.That(reload().AdvancePanel(key, page, panel), Is.False, "A stale panel acknowledgement cannot skip the next panel.");
                }
            Assert.That(reload().HasRead(key), Is.True);
            Assert.That(reload().PendingStoryKey, Is.Empty);
            Assert.That(reload().PendingPanel, Is.Zero);
        }

        [Test]
        public void LegacyPageProgress_ResumesItsFirstPanelAndPreservesReadStories()
        {
            string json = "{\"version\":1,\"profileScope\":\"captain\",\"readKeys\":[\"arrival:gray_gull\"],\"pendingStoryKey\":\"opening\",\"pendingPage\":2}";
            var progress = new OpeningPortComicProgress("captain", () => json, value => json = value);
            Assert.That(progress.PendingPage, Is.EqualTo(2));
            Assert.That(progress.PendingPanel, Is.Zero);
            Assert.That(progress.HasRead("arrival:gray_gull"), Is.True);
            Assert.That(progress.AdvancePanel("opening", 2, 0), Is.True);
            Assert.That(progress.PendingPage, Is.EqualTo(2));
            Assert.That(progress.PendingPanel, Is.EqualTo(1));
        }

        [TestCase(-1)]
        [TestCase(4)]
        public void InvalidPanelProgress_RestartsThePendingSheetWithoutAcknowledgingIt(int panel)
        {
            string json = "{\"version\":1,\"profileScope\":\"captain\",\"pendingStoryKey\":\"opening\",\"pendingPage\":2,\"pendingPanel\":" + panel + "}";
            var progress = new OpeningPortComicProgress("captain", () => json, _ => Assert.Fail("Reading must not rewrite the save."));
            Assert.That(progress.PendingStoryKey, Is.EqualTo("opening"));
            Assert.That(progress.PendingPage, Is.EqualTo(2));
            Assert.That(progress.PendingPanel, Is.Zero);
            Assert.That(progress.HasRead("opening"), Is.False);
        }

        [Test]
        public void AllPortsAndOpening_KeepIndependentReadKeysAcrossControllers()
        {
            string json = string.Empty;
            var first = new OpeningPortComicProgress("captain", () => json, value => json = value);
            foreach (ArrivalComicStory story in ArrivalComicCatalog.All)
            {
                string key = OpeningPortComicProgress.ArrivalKey(story.Id);
                Assert.That(first.TryQueue(key), Is.True);
                Assert.That(first.Advance(key, 0), Is.True);
                var resumed = new OpeningPortComicProgress("captain", () => json, value => json = value);
                Assert.That(resumed.PendingStoryKey, Is.EqualTo(key));
                Assert.That(resumed.PendingPage, Is.EqualTo(1));
                Assert.That(resumed.Advance(key, 1), Is.True);
            }
            foreach (ArrivalComicStory story in ArrivalComicCatalog.All)
                Assert.That(first.HasRead(OpeningPortComicProgress.ArrivalKey(story.Id)), Is.True);
            Assert.That(first.HasRead("opening"), Is.False);
            Assert.That(first.TryQueue("opening"), Is.True);
        }

        [Test]
        public void PendingStory_CannotBeOverwrittenOrAdvancedByAnotherStory()
        {
            string json = string.Empty;
            var progress = new OpeningPortComicProgress("captain", () => json, value => json = value);
            progress.TryQueue("arrival:white_shell");
            Assert.That(progress.TryQueue("arrival:bronze_bell"), Is.False);
            Assert.That(progress.Advance("arrival:bronze_bell", 0), Is.False);
            Assert.That(progress.PendingStoryKey, Is.EqualTo("arrival:white_shell"));
            Assert.That(progress.PendingPage, Is.Zero);
        }

        [Test]
        public void NewCampaign_IsUnreadAndDoesNotResumeThePriorCaptain()
        {
            string json = string.Empty;
            var old = new OpeningPortComicProgress("old", () => json, value => json = value);
            old.TryQueue("opening"); old.Advance("opening", 0);
            var next = new OpeningPortComicProgress("new", () => json, value => json = value);
            Assert.That(next.PendingStoryKey, Is.Empty);
            Assert.That(next.TryQueue("opening"), Is.True);
            Assert.That(next.PendingPage, Is.Zero);
        }

        [TestCase("not-json")]
        [TestCase("{\"version\":1,\"profileScope\":\"captain\",\"pendingStoryKey\":\"arrival:white_shell\",\"pendingPage\":2}")]
        [TestCase("{\"version\":1,\"profileScope\":\"captain\",\"pendingStoryKey\":\"opening\",\"pendingPage\":-1}")]
        [TestCase("{\"version\":2,\"profileScope\":\"captain\",\"pendingStoryKey\":\"opening\"}")]
        public void InvalidOrFutureState_DoesNotStartAnInventedStory(string json)
        {
            var progress = new OpeningPortComicProgress("captain", () => json, _ => Assert.Fail("A read must not rewrite a save."));
            Assert.That(progress.PendingStoryKey, Is.Empty);
            Assert.That(progress.TryQueue("arrival:broken_anchor"), Is.False);
            Assert.That(progress.TryQueue("legendary:v1:before:legendary_pirate_last_horizon"), Is.False);
        }

        [Test]
        public void PlayerPrefsBackend_ResumesAndKeepsLegendaryReadStateAndNewCampaignSafe()
        {
            string[] keys = { CoreGameplayDemoController.WorldEconomyScopeKey, OpeningPortComicProgress.PlayerPrefsKey,
                LegendaryFleetComicReadProgress.PlayerPrefsKey };
            bool[] had = Array.ConvertAll(keys, PlayerPrefs.HasKey);
            string[] saved = Array.ConvertAll(keys, key => PlayerPrefs.GetString(key, string.Empty));
            try
            {
                PlayerPrefs.SetString(keys[0], Guid.NewGuid().ToString("N"));
                PlayerPrefs.DeleteKey(keys[1]);
                PlayerPrefs.SetString(keys[2], "legendary-input-must-stay-exact");
                var old = OpeningPortComicProgress.ForCurrentProfile();
                old.TryQueue("opening"); old.Advance("opening", 0);
                Assert.That(OpeningPortComicProgress.ForCurrentProfile().PendingPage, Is.EqualTo(1));
                Assert.That(PlayerPrefs.GetString(keys[2]), Is.EqualTo("legendary-input-must-stay-exact"));
                PlayerPrefs.SetString(keys[0], Guid.NewGuid().ToString("N"));
                var next = OpeningPortComicProgress.ForCurrentProfile();
                Assert.That(next.PendingStoryKey, Is.Empty);
                next.TryQueue("arrival:gray_gull");
                string newRecord = PlayerPrefs.GetString(keys[1]);
                old.TryQueue("opening"); old.Advance("opening", 0);
                Assert.That(PlayerPrefs.GetString(keys[1]), Is.EqualTo(newRecord));
                PlayerPrefs.DeleteKey(keys[0]);
                Assert.That(OpeningPortComicProgress.ForCurrentProfile(), Is.Null);
            }
            finally
            {
                for (int index = 0; index < keys.Length; index++)
                    if (had[index]) PlayerPrefs.SetString(keys[index], saved[index]); else PlayerPrefs.DeleteKey(keys[index]);
                PlayerPrefs.Save();
            }
        }
    }
}
