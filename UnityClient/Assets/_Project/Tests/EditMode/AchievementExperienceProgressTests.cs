using System;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class AchievementProgressTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void ExperienceProgress_AllFiftyMatchUnlockRulesWithoutWriting(bool captured)
        {
            PrepareBatchMixedProgress();
            if (captured) { AchievementCatalog.CaptureProgress(); ProfileGoldPersistence.Save(17m); }
            var before = original.ToDictionary(pair => pair.Key, pair => ReadBatchPreference(pair.Key, pair.Value.IsString));
            var values = new AchievementProgressView[AchievementCatalog.All.Count + 1];
            values[AchievementCatalog.All.Count] = new AchievementProgressView(true, 42m, 99m, true);
            int count = AchievementCatalog.FillProgress(values);
            Assert.That(count, Is.EqualTo(AchievementCatalog.CountUnlocked()));
            for (int index = 0; index < AchievementCatalog.All.Count; index++)
            {
                Assert.That(values[index].Unlocked, Is.EqualTo(AchievementCatalog.IsUnlocked(AchievementCatalog.All[index])), AchievementCatalog.All[index].Id);
                Assert.That(values[index].Fraction, Is.InRange(0f, 1f));
                if (values[index].Unlocked) Assert.That(values[index].Fraction, Is.EqualTo(1f));
            }
            Assert.That(values[AchievementCatalog.All.Count].Current, Is.EqualTo(42m), "The caller's extra slots must remain untouched.");
            var flags = new bool[AchievementCatalog.All.Count];
            AchievementCatalog.FillCapturedStates(flags);
            foreach (var pair in original)
                Assert.That(ReadBatchPreference(pair.Key, pair.Value.IsString), Is.EqualTo(before[pair.Key]), pair.Key);
        }

        [Test]
        public void ExperienceProgress_ExactMoneyThresholdAndPermanentCompletionDoNotRegress()
        {
            ProfileGoldPersistence.Save(899.9999m);
            AchievementProgressView before = ExperienceProgressFor("gold_900");
            Assert.That(before.IsQuantified, Is.True);
            Assert.That(before.Current, Is.EqualTo(899.9999m));
            Assert.That(before.Target, Is.EqualTo(900m));
            Assert.That(before.Unlocked, Is.False);
            ProfileGoldPersistence.Save(900m);
            Assert.That(ExperienceProgressFor("gold_900").Unlocked, Is.True);
            AchievementCatalog.CaptureProgress();
            ProfileGoldPersistence.Save(2m);
            AchievementProgressView after = ExperienceProgressFor("gold_900");
            Assert.That(after.Current, Is.EqualTo(2m), "The numeric snapshot remains the current source value.");
            Assert.That(after.Unlocked, Is.True);
            Assert.That(after.Fraction, Is.EqualTo(1f), "Permanent milestones must stay completed after spending.");
        }

        [Test]
        public void ExperienceProgress_CompositeRequirementsReportTheirActualParts()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Water"), 80);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Food"), 120);
            var ready = ExperienceProgressFor("cargo_ready");
            Assert.That(ready.Current, Is.EqualTo(80m));
            Assert.That(ready.Target, Is.EqualTo(100m));
            Assert.That(ready.Detail, Is.EqualTo("水 80/100 · 粮 120/100"));
            var cargo = ExperienceProgressFor("cargo_all");
            Assert.That(cargo.Current, Is.EqualTo(2m));
            Assert.That(cargo.Target, Is.EqualTo(9m));
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(1), 1);
            Assert.That(ExperienceProgressFor("seventh_harbor").Current, Is.EqualTo(2m));
            foreach (string port in Ports) PlayerPrefs.SetInt(CoreGameplayDemoController.PortInvestmentKey(port), 1);
            var final = ExperienceProgressFor("seventh_harbor");
            Assert.That(final.Current, Is.EqualTo(3m));
            Assert.That(final.Target, Is.EqualTo(3m));
            Assert.That(final.Unlocked, Is.True);
        }

        [Test]
        public void ExperienceProgress_LegacyCrewDoesNotDoubleCountAndEventsStayNonNumeric()
        {
            CrewVariantDefinition crew = CrewNarrativeCatalog.LegacyForProfession(0);
            Recruit(crew);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewRecruitmentKey(0), 1);
            Assert.That(ExperienceProgressFor("crew_three").Current, Is.EqualTo(1m));
            Assert.That(ExperienceProgressFor("dispatch_resolved").IsQuantified, Is.False);
            Assert.That(ExperienceProgressFor("tutorial_recovery").IsQuantified, Is.False);
        }

        [Test]
        public void ExperienceProgress_InvalidBuffersAreRejectedWithoutPartialWrites()
        {
            Assert.Throws<ArgumentNullException>(() => AchievementCatalog.FillProgress(null));
            Assert.Throws<ArgumentException>(() => AchievementCatalog.FillProgress(new AchievementProgressView[49]));
            Assert.Throws<ArgumentNullException>(() => AchievementCatalog.FillCapturedStates(null));
            Assert.Throws<ArgumentException>(() => AchievementCatalog.FillCapturedStates(new bool[49]));
            Assert.That(default(AchievementProgressView).Fraction, Is.Zero);
            Assert.That(AchievementCatalog.PersistenceKeys.Any(PlayerPrefs.HasKey), Is.False);
        }

        private static AchievementProgressView ExperienceProgressFor(string id)
        {
            var values = new AchievementProgressView[AchievementCatalog.All.Count];
            AchievementCatalog.FillProgress(values);
            return values[AchievementCatalog.All.ToList().FindIndex(definition => definition.Id == id)];
        }
    }
}
