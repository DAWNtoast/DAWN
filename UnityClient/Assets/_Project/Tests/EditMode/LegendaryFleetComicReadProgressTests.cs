using System;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class LegendaryFleetComicReadProgressTests
    {
        private const string Ship = "legendary_pirate_last_horizon";

        [Test]
        public void Completion_IsImmediateIdempotentAndIndependentForBeforeAndAfter()
        {
            string json = string.Empty;
            int writes = 0;
            var progress = new LegendaryFleetComicReadProgress("campaign", () => json, value => { json = value; writes++; });
            Assert.That(progress.MarkReadStoryKey("legendary:v1:before:" + Ship), Is.True);
            Assert.That(writes, Is.EqualTo(1));
            var reloaded = new LegendaryFleetComicReadProgress("campaign", () => json, value => json = value);
            Assert.That(reloaded.HasReadStoryKey("legendary:v1:before:" + Ship), Is.True);
            Assert.That(reloaded.HasReadStoryKey("legendary:v1:after:" + Ship), Is.False);
            Assert.That(progress.MarkReadStoryKey("legendary:v1:before:" + Ship), Is.False);
            Assert.That(writes, Is.EqualTo(1));
            Assert.That(progress.MarkReadStoryKey("legendary:v1:after:" + Ship), Is.True);
            Assert.That(reloaded.HasReadStoryKey("after:" + Ship), Is.True);
        }

        [Test]
        public void ImageRevisions_DoNotReplayAnAcknowledgedPage()
        {
            string json = string.Empty;
            var progress = new LegendaryFleetComicReadProgress("campaign", () => json, value => json = value);
            progress.MarkReadStoryKey("legendary:v1:before:" + Ship);
            Assert.That(progress.HasReadStoryKey("legendary:v2:before:" + Ship), Is.True);
            Assert.That(progress.HasReadStoryKey("legendary:v99:before:" + Ship), Is.True);
            Assert.That(json, Does.Not.Contain("legendary:v1"));
        }

        [Test]
        public void MultipleControllersAndOldSnapshots_MergeRatherThanOverwriteReadPages()
        {
            string json = string.Empty;
            var first = new LegendaryFleetComicReadProgress("campaign", () => json, value => json = value);
            var second = new LegendaryFleetComicReadProgress("campaign", () => json, value => json = value);
            first.MarkReadStoryKey("before:" + Ship);
            second.MarkReadStoryKey("after:" + Ship);
            first.ImportReadStoryKeys(new[] { "legendary:v1:before:legendary_pirate_iron_oath" });
            Assert.That(second.HasReadStoryKey("before:" + Ship), Is.True);
            Assert.That(first.HasReadStoryKey("after:" + Ship), Is.True);
            Assert.That(second.HasReadStoryKey("before:legendary_pirate_iron_oath"), Is.True);
        }

        [Test]
        public void NonComicAndUnknownKeys_CannotConsumeAReading()
        {
            string json = string.Empty;
            int writes = 0;
            var progress = new LegendaryFleetComicReadProgress("campaign", () => json, value => { json = value; writes++; });
            Assert.That(progress.ImportReadStoryKeys(new[] {
                "intro:map", "battle:map:" + Ship, "legendary:v1:before:unknown", "legendary:v1:pending:" + Ship,
                "legendary:v0:before:" + Ship, "legendary:future:before:" + Ship, null
            }), Is.False);
            Assert.That(writes, Is.Zero);
            Assert.That(json, Is.Empty);
        }

        [Test]
        public void DifferentCampaignScope_StartsUnread()
        {
            string json = string.Empty;
            var previous = new LegendaryFleetComicReadProgress("old-campaign", () => json, value => json = value);
            previous.MarkReadStoryKey("before:" + Ship);
            previous.MarkReadStoryKey("after:" + Ship);
            var next = new LegendaryFleetComicReadProgress("new-campaign", () => json, value => json = value);
            Assert.That(next.HasReadStoryKey("before:" + Ship), Is.False);
            Assert.That(next.HasReadStoryKey("after:" + Ship), Is.False);
            next.MarkReadStoryKey("before:" + Ship);
            Assert.That(next.HasReadStoryKey("after:" + Ship), Is.False);
            Assert.That(json, Does.Contain("new-campaign"));
        }

        [Test]
        public void PlayerPrefsBackend_PersistsAcrossInstancesAndGuardsTheNewGameBoundary()
        {
            const string scopeKey = CoreGameplayDemoController.WorldEconomyScopeKey;
            const string readKey = LegendaryFleetComicReadProgress.PlayerPrefsKey;
            bool hadScope = PlayerPrefs.HasKey(scopeKey), hadRead = PlayerPrefs.HasKey(readKey);
            string scopeBefore = PlayerPrefs.GetString(scopeKey, string.Empty), readBefore = PlayerPrefs.GetString(readKey, string.Empty);
            try
            {
                PlayerPrefs.SetString(scopeKey, Guid.NewGuid().ToString("N"));
                PlayerPrefs.DeleteKey(readKey);
                LegendaryFleetComicReadProgress old = LegendaryFleetComicReadProgress.ForCurrentProfile();
                Assert.That(old.MarkReadStoryKey("before:" + Ship), Is.True);
                Assert.That(PlayerPrefs.GetString(readKey), Is.Not.Empty);
                Assert.That(LegendaryFleetComicReadProgress.ForCurrentProfile().HasReadStoryKey("before:" + Ship), Is.True);
                // StartNewGame changes this same GUID after clearing the old voyage.
                PlayerPrefs.SetString(scopeKey, Guid.NewGuid().ToString("N"));
                LegendaryFleetComicReadProgress current = LegendaryFleetComicReadProgress.ForCurrentProfile();
                Assert.That(current.HasReadStoryKey("before:" + Ship), Is.False);
                current.MarkReadStoryKey("before:legendary_pirate_iron_oath");
                string newCampaign = PlayerPrefs.GetString(readKey);
                old.MarkReadStoryKey("after:" + Ship);
                Assert.That(PlayerPrefs.GetString(readKey), Is.EqualTo(newCampaign), "A stale controller cannot overwrite the new campaign.");
                PlayerPrefs.DeleteKey(scopeKey);
                Assert.That(LegendaryFleetComicReadProgress.ForCurrentProfile(), Is.Null);
            }
            finally
            {
                if (hadScope) PlayerPrefs.SetString(scopeKey, scopeBefore); else PlayerPrefs.DeleteKey(scopeKey);
                if (hadRead) PlayerPrefs.SetString(readKey, readBefore); else PlayerPrefs.DeleteKey(readKey);
                PlayerPrefs.Save();
            }
        }
    }
}
