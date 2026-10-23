using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreAchievementCatalogTests
    {
        [Test]
        public void Catalog_ContainsFiftyUniqueCompleteEntries()
        {
            Assert.That(CoreAchievementCatalog.Catalog.Count, Is.EqualTo(CoreAchievementCatalog.CatalogCount));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (CoreAchievementDefinition achievement in CoreAchievementCatalog.Catalog)
            {
                Assert.That(achievement.Id, Is.Not.Null.And.Not.Empty);
                Assert.That(ids.Add(achievement.Id), Is.True, "Duplicate achievement id: " + achievement.Id);
                Assert.That(achievement.Name, Is.Not.Null.And.Not.Empty);
                Assert.That(achievement.Description, Is.Not.Null.And.Not.Empty);
                Assert.That(achievement.RequirementKey, Is.Not.Null.And.Not.Empty);
                Assert.That(achievement.Target, Is.GreaterThan(0));
            }
            Assert.That(ids.Count, Is.EqualTo(50));
            Assert.That(CoreAchievementCatalog.All, Is.SameAs(CoreAchievementCatalog.Catalog));
        }

        [Test]
        public void RetainedCategoriesKeepTheirSerializedValues()
        {
            Assert.That((int)CoreAchievementCategory.Fleet, Is.EqualTo(9));
            Assert.That((int)CoreAchievementCategory.Exploration, Is.EqualTo(11));
            Assert.That((int)CoreAchievementCategory.Supplies, Is.EqualTo(12));
            Assert.That(Enum.IsDefined(typeof(CoreAchievementCategory), 10), Is.False);
        }

        [Test]
        public void OriginalMainMenuAliases_ResolveCanonicalDefinitions()
        {
            CoreAchievementDefinition first = CoreAchievementCatalog.Find("first_sail");
            CoreAchievementDefinition port = CoreAchievementCatalog.Find("stranger_port");
            CoreAchievementDefinition trade = CoreAchievementCatalog.Find("sea_trade");
            CoreAchievementDefinition gold = CoreAchievementCatalog.Find("golden_captain");

            Assert.That(first.Name, Is.EqualTo("初次扬帆"));
            Assert.That(first.RequirementKind, Is.EqualTo(CoreAchievementRequirementKind.Flag));
            Assert.That(first.RequirementKey, Is.EqualTo("tutorial_completed"));
            Assert.That(port.Name, Is.EqualTo("陌生港口"));
            Assert.That(trade.Name, Is.EqualTo("海上生意"));
            Assert.That(trade.Target, Is.EqualTo(1000));
            Assert.That(gold.Name, Is.EqualTo("黄金船长"));
            Assert.That(gold.Target, Is.EqualTo(2500));
            Assert.That(CoreAchievementCatalog.Find("tutorial_departure"), Is.SameAs(first));
            Assert.That(CoreAchievementCatalog.Find("port_gray_gull"), Is.SameAs(port));
            Assert.That(CoreAchievementCatalog.Find("gold_600"), Is.SameAs(trade));
            Assert.That(CoreAchievementCatalog.Find("gold_900"), Is.SameAs(gold));
            Assert.That(first.Id, Is.EqualTo("tutorial_departure"));
            Assert.That(first.LegacyId, Is.EqualTo("first_sail"));
            Assert.That(port.Id, Is.EqualTo("port_gray_gull"));
            Assert.That(port.LegacyId, Is.EqualTo("stranger_port"));
            Assert.That(trade.Id, Is.EqualTo("gold_600"));
            Assert.That(trade.LegacyId, Is.EqualTo("sea_trade"));
            Assert.That(gold.Id, Is.EqualTo("gold_900"));
            Assert.That(gold.LegacyId, Is.EqualTo("golden_captain"));
        }

        [Test]
        public void Progress_EvaluatesFlagsCountersAndClampsInvalidValues()
        {
            CoreAchievementProgress progress = new CoreAchievementProgress();
            CoreAchievementDefinition first = CoreAchievementCatalog.Find("first_sail");
            CoreAchievementDefinition gold = CoreAchievementCatalog.Find("golden_captain");

            Assert.That(progress.IsUnlocked(first), Is.False);
            progress.SetFlag(first.RequirementKey);
            Assert.That(progress.IsUnlocked(first), Is.True);
            progress.SetCounter(gold.RequirementKey, -20);
            Assert.That(progress.GetCounter(gold.RequirementKey), Is.Zero);
            progress.Increment(gold.RequirementKey, 2500);
            Assert.That(progress.IsUnlocked(gold), Is.True);
            Assert.That(progress.CountUnlocked(new[] { first, gold }), Is.EqualTo(2));
        }

        [Test]
        public void Progress_UnknownKeysAndNullDefinitionsAreSafe()
        {
            CoreAchievementProgress progress = new CoreAchievementProgress();
            Assert.That(progress.GetCounter("unknown"), Is.Zero);
            Assert.That(progress.IsFlagSet("unknown"), Is.False);
            Assert.That(progress.IsUnlocked(null), Is.False);
            Assert.That(progress.CountUnlocked(null), Is.Zero);
            Assert.That(CoreAchievementCatalog.Find("missing"), Is.Null);
            Assert.That(CoreAchievementCatalog.IsKnown("missing"), Is.False);
        }

        [Test]
        public void Progress_PredicateRequirementsCannotBeUnlockedByFlagsOrCounters()
        {
            var progress = new CoreAchievementProgress();
            var predicate = new CoreAchievementDefinition("predicate_fixture", "复合条件", "必须由适配层检查", 
                CoreAchievementCategory.Voyage, CoreAchievementRequirementKind.Predicate, "predicate_fixture", 1);
            progress.SetFlag(predicate.RequirementKey);
            progress.SetCounter(predicate.RequirementKey, int.MaxValue);
            Assert.That(progress.IsUnlocked(predicate), Is.False);
            Assert.That(progress.CountUnlocked(new[] { predicate }), Is.Zero);
        }

        [Test]
        public void StarterInventoryAndConfiguredDecksCannotAwardActionChallenges()
        {
            var progress = new CoreAchievementProgress();
            progress.SetCounter("warehouse_treasures_owned", 1);
            progress.SetCounter("components_equipped", 5);
            progress.SetCounter("configured_cards", 12);
            progress.SetCounter("configured_deck_stages", 3);
            progress.SetFlag("all_base_cargo_present");
            progress.SetFlag("tutorial_component_reward");

            foreach (string id in new[] { "warehouse_one", "component_full", "battle_deck", "battle_stages", "cargo_all", "tutorial_component" })
                Assert.That(progress.IsUnlocked(CoreAchievementCatalog.Find(id)), Is.False, id);
            Assert.That(progress.IsUnlocked(CoreAchievementCatalog.Find("tutorial_warehouse")), Is.True,
                "The first collection remains a separate introductory milestone.");
        }

        [Test]
        public void ThreeBattleStylesRequiresVictoriousAuthoredStagesInsteadOfSavedDefaults()
        {
            var progress = new CoreAchievementProgress();
            CoreAchievementDefinition achievement = CoreAchievementCatalog.Find("battle_stages");
            progress.SetCounter("configured_deck_stages", 3);
            progress.SetCounter("authored_deck_victory_stages", 2);
            Assert.That(progress.IsUnlocked(achievement), Is.False);
            progress.Increment("authored_deck_victory_stages");
            Assert.That(progress.IsUnlocked(achievement), Is.True);
        }

        [Test]
        public void Progress_IncrementAtIntegerLimitRetainsUnlockedAchievements()
        {
            var progress = new CoreAchievementProgress();
            CoreAchievementDefinition gold = CoreAchievementCatalog.Find("golden_captain");
            progress.SetCounter(gold.RequirementKey, int.MaxValue - 1);
            Assert.That(progress.Increment(gold.RequirementKey, 10), Is.EqualTo(int.MaxValue));
            Assert.That(progress.IsUnlocked(gold), Is.True);
            Assert.That(progress.Increment(gold.RequirementKey, int.MaxValue), Is.EqualTo(int.MaxValue));
            Assert.That(progress.Increment(gold.RequirementKey, int.MinValue), Is.Zero);
        }
    }
}
