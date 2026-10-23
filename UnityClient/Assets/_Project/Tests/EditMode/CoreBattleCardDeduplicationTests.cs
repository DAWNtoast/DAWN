using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBattleCardDeduplicationTests
    {
        private static readonly string[] RetiredMovementIds =
        {
            "card_parallel_course", "card_turn_port", "card_turn_starboard",
            "card_turn_about", "card_sail_forward", "card_circle_enemy",
        };

        [TestCase("card_parallel_course", "card_approach")]
        [TestCase("card_turn_port", "card_approach")]
        [TestCase("card_turn_starboard", "card_approach")]
        [TestCase("card_turn_about", "card_approach")]
        [TestCase("card_sail_forward", "card_approach")]
        [TestCase("card_circle_enemy", "card_retreat")]
        public void HistoricSlotsRemainReadableButOnlyCanonicalDefinitionsCanEnterCollections(string oldId, string retainedId)
        {
            var session = new CoreVoyageSession();
            int oldIndex = Index(oldId), retainedIndex = Index(retainedId);
            Assert.That(CoreVoyageSession.CardLibrary[oldIndex].Id, Is.EqualTo(oldId));
            Assert.That(CoreVoyageSession.CardLibrary[oldIndex].AllowedStages, Is.EqualTo(NavalBattleStages.None));
            Assert.That(session.GetBaseBattleCardOwnedCopies(oldIndex), Is.Zero);
            Assert.That(session.GetBaseBattleCardOwnedCopies(retainedIndex), Is.EqualTo(2));
            foreach (NavalBattleStage stage in new[] { NavalBattleStage.Preparation, NavalBattleStage.Naval })
            {
                CoreBattleCard actual = session.GetBattleCard(oldIndex, stage);
                CoreBattleCard expected = session.GetBattleCard(retainedIndex, stage);
                Assert.That(actual.Id, Is.EqualTo(retainedId));
                Assert.That(actual.Description, Is.EqualTo(expected.Description));
                Assert.That(actual.Energy, Is.EqualTo(expected.Energy));
                Assert.That(actual.NavalActionPointGain, Is.EqualTo(1));
                Assert.That(actual.Block, Is.EqualTo(expected.Block));
                CollectionAssert.DoesNotContain(session.GetAvailableBattleCards(stage), oldIndex);
            }
            for (int level = 0; level <= 3; level++)
            {
                CoreBattleCard actual = session.GetBattleCardAtLevel(oldIndex, level);
                CoreBattleCard expected = session.GetBattleCardAtLevel(retainedIndex, level);
                Assert.That(actual.Id, Is.EqualTo(retainedId));
                Assert.That(actual.UpgradeLevel, Is.EqualTo(expected.UpgradeLevel));
                Assert.That(actual.Block, Is.EqualTo(expected.Block));
                Assert.That(actual.NavalActionPointGain, Is.EqualTo(expected.NavalActionPointGain));
                if (retainedId == "card_approach")
                {
                    Assert.That(actual.UpgradeLevel, Is.EqualTo(level == 0 ? 0 : 1));
                    Assert.That(actual.NavalActionPointGain, Is.EqualTo(level == 0 ? 1 : 2));
                }
            }
            Assert.That(CoreVoyageSession.CardLibrary[0].Id, Is.EqualTo("card_chain_shot"));
            Assert.That(CoreVoyageSession.CardLibrary[10].Id, Is.EqualTo("card_approach"));
            Assert.That(CoreVoyageSession.CardLibrary[12].Id, Is.EqualTo("card_circle_enemy"),
                "Saved integer positions must not shift when a card is retired.");
        }

        [TestCase(NavalBattleStage.Preparation)]
        [TestCase(NavalBattleStage.Naval)]
        public void StarterDeckAndCollectionExposeOnlyRetainedMovementIdentities(NavalBattleStage stage)
        {
            var session = new CoreVoyageSession();
            int[] retired = RetiredMovementIds.Select(Index).ToArray();
            var deck = session.GetBattleDeck(stage);
            Assert.That(deck.Count, Is.InRange(CoreVoyageSession.MinimumBaseBattleDeckSize, CoreVoyageSession.MaximumBaseBattleDeckSize));
            Assert.That(deck.Intersect(retired), Is.Empty);
            Assert.That(session.GetAvailableBattleCards(stage).Intersect(retired), Is.Empty);
            Assert.That(deck, Does.Contain(Index("card_approach")));
            Assert.That(deck.GroupBy(index => index).All(group => group.Count() <= 2), Is.True);
            Assert.That(session.SetBattleDeck(stage, deck), Is.True, "The starter preset must pass the same rules as an edited deck.");
        }

        [TestCase(NavalBattleStage.Preparation)]
        [TestCase(NavalBattleStage.Naval)]
        public void EditingMixedOldAndNewIdsSharesInventoryAndRejectsThirdCopyAtomically(NavalBattleStage stage)
        {
            var session = new CoreVoyageSession();
            int canonical = Index("card_approach");
            int[] others = OtherCards(session, stage).Take(4).SelectMany(index => new[] { index, index }).ToArray();
            int[] legal = new[] { Index("card_turn_port"), canonical }.Concat(others).ToArray();
            int[] input = (int[])legal.Clone();
            Assert.That(session.SetBattleDeck(stage, legal), Is.True);
            int[] accepted = session.GetBattleDeck(stage).ToArray();
            CollectionAssert.AreEqual(new[] { canonical, canonical }.Concat(others), accepted);
            CollectionAssert.AreEqual(input, legal);

            int[] thirdCopy = new[] { canonical, Index("card_turn_starboard"), Index("card_sail_forward") }
                .Concat(others.Take(7)).ToArray();
            Assert.That(session.SetBattleDeck(stage, thirdCopy), Is.False);
            CollectionAssert.AreEqual(accepted, session.GetBattleDeck(stage));
        }

        [TestCase(NavalBattleStage.Preparation)]
        [TestCase(NavalBattleStage.Naval)]
        public void RestoringSeveralFormerNamesCollapsesTheirInventoryInOrderAndFillsMinimum(NavalBattleStage stage)
        {
            var session = new CoreVoyageSession();
            int[] legacy = LegacyDeck(session, stage);
            int[] input = (int[])legacy.Clone();
            int[] otherStage = session.GetBattleDeck(NavalBattleStage.Boarding).ToArray();
            Assert.That(session.RestoreBattleDeck(stage, legacy), Is.True);
            int[] restored = session.GetBattleDeck(stage).ToArray();
            Assert.That(restored.Length, Is.EqualTo(CoreVoyageSession.MinimumBaseBattleDeckSize));
            CollectionAssert.AreEqual(new[] { Index("card_approach"), Index("card_approach"),
                Index("card_retreat"), Index("card_retreat") }, restored.Take(4));
            Assert.That(restored.GroupBy(index => index).All(group => group.Count() <= 2), Is.True);
            Assert.That(restored.Select(index => CoreVoyageSession.CardLibrary[index].Id).Intersect(RetiredMovementIds), Is.Empty);
            CollectionAssert.AreEqual(input, legacy);
            CollectionAssert.AreEqual(otherStage, session.GetBattleDeck(NavalBattleStage.Boarding));
            Assert.That(session.RestoreBattleDeck(stage, restored), Is.True);
            CollectionAssert.AreEqual(restored, session.GetBattleDeck(stage));
        }

        [TestCase(3, 1)]
        [TestCase(1, 3)]
        public void ExistingUpgradeInvestmentUsesHighestRankAndLaterReconfigurationStaysUnified(int oldRank, int newRank)
        {
            var session = new CoreVoyageSession();
            var saved = new Dictionary<string, int> { ["card_circle_enemy"] = oldRank, ["card_retreat"] = newRank };
            session.ConfigureCardUpgrades(saved);
            Assert.That(session.GetCardUpgradeLevel("card_circle_enemy"), Is.EqualTo(2));
            Assert.That(session.GetCardUpgradeLevel("card_retreat"), Is.EqualTo(2));
            Assert.That(session.CardUpgradeLevels["card_retreat"], Is.EqualTo(3), "The old investment survives the effective +2 cap.");
            Assert.That(session.GetBattleCard(Index("card_circle_enemy")).Block,
                Is.EqualTo(session.GetBattleCard(Index("card_retreat")).Block));
            Assert.That(saved["card_circle_enemy"], Is.EqualTo(oldRank));
            Assert.That(saved["card_retreat"], Is.EqualTo(newRank));

            session.ConfigureCardUpgrades(new Dictionary<string, int> { ["card_retreat"] = 1 });
            Assert.That(session.GetCardUpgradeLevel("card_circle_enemy"), Is.EqualTo(1),
                "A replaced configuration must not leave a stale hidden higher alias rank.");
            Assert.That(session.GetCardUpgradeLevel("card_retreat"), Is.EqualTo(1));
            session.RestoreProfile(1000m, "断锚港");
            Assert.That(session.TryUpgradeBattleCard(Index("card_circle_enemy"), out string reason), Is.True, reason);
            Assert.That(session.GetCardUpgradeLevel("card_retreat"), Is.EqualTo(2));
            Assert.That(session.GetCardUpgradeLevel("card_circle_enemy"), Is.EqualTo(2));
            Assert.That(session.ProfileGold, Is.EqualTo(760m), "One shared upgrade must be paid exactly once.");
        }

        [TestCase(NavalBattleStage.Preparation)]
        [TestCase(NavalBattleStage.Naval)]
        public void PlayingDifferentHistoricNamesCannotBypassSameTurnEnergySurcharge(NavalBattleStage stage)
        {
            CoreVoyageSession session = NewBattle(stage);
            session.Battle.Energy = 0;
            session.Battle.Hand.Clear();
            session.Battle.Hand.AddRange(new[] { Index("card_turn_port"), Index("card_turn_starboard"), Index("card_approach") });
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(2));
            Assert.That(session.GetBattleCardEnergyCost(Index("card_turn_starboard")), Is.EqualTo(1));
            Assert.That(session.GetBattleCardEnergyCost(Index("card_approach")), Is.EqualTo(1));
            Assert.That(session.PlayCard(0), Is.False);
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(2));
            session.Battle.Energy = 1;
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.Energy, Is.Zero);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(3));
            Assert.That(session.GetBattleCardEnergyCost(Index("card_approach")), Is.EqualTo(2));
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.GetBattleCardEnergyCost(Index("card_turn_port")), Is.Zero);
            Assert.That(session.GetBattleCardEnergyCost(Index("card_approach")), Is.Zero);
        }

        [Test]
        public void RetiredGuardMovementStillGrantsGuardAndSharesRetainedCardsRepeatCost()
        {
            CoreVoyageSession session = NewBattle(NavalBattleStage.Preparation);
            session.Battle.Hand.Clear();
            session.Battle.Hand.AddRange(new[] { Index("card_circle_enemy"), Index("card_retreat") });
            session.Battle.Energy = 3;
            int guard = session.Battle.PlayerBlock;
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.PlayerBlock - guard, Is.EqualTo(3));
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(2));
            Assert.That(session.GetBattleCardEnergyCost(Index("card_retreat")), Is.EqualTo(2));
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.PlayerBlock - guard, Is.EqualTo(6));
            Assert.That(session.Battle.Energy, Is.Zero);
        }

        [TestCase(NavalBattleStage.Preparation)]
        [TestCase(NavalBattleStage.Naval)]
        public void VoyageCheckpointMigratesOldDeckAndRanksWithoutMutatingInputAndCanBeSavedAgain(NavalBattleStage stage)
        {
            var source = new CoreVoyageSession();
            int[] legacy = LegacyDeck(source, stage);
            source.StartVoyage();
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            checkpoint.battleDecks[(int)stage].cards = legacy;
            checkpoint.cardUpgradeLevels = new[]
            {
                new CoreVoyageValueCheckpoint { id = "card_circle_enemy", value = 3 },
                new CoreVoyageValueCheckpoint { id = "card_retreat", value = 1 },
            };
            int[] inputDeck = (int[])legacy.Clone();
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
            Assert.That(restored.GetBattleDeck(stage).Count, Is.EqualTo(10));
            Assert.That(restored.GetBattleDeck(stage).Select(index => CoreVoyageSession.CardLibrary[index].Id)
                .Intersect(RetiredMovementIds), Is.Empty);
            Assert.That(restored.GetBattleDeck(stage).Count(index => index == Index("card_approach")), Is.EqualTo(2));
            Assert.That(restored.GetCardUpgradeLevel("card_retreat"), Is.EqualTo(2));
            Assert.That(restored.GetCardUpgradeLevel("card_circle_enemy"), Is.EqualTo(2));
            Assert.That(restored.CardUpgradeLevels["card_retreat"], Is.EqualTo(3));
            CollectionAssert.AreEqual(inputDeck, checkpoint.battleDecks[(int)stage].cards);
            Assert.That(checkpoint.cardUpgradeLevels[0].id, Is.EqualTo("card_circle_enemy"));
            Assert.That(checkpoint.cardUpgradeLevels[0].value, Is.EqualTo(3));
            Assert.That(checkpoint.cardUpgradeLevels[1].value, Is.EqualTo(1));
            CoreVoyageCheckpoint migrated = restored.CaptureVoyageCheckpoint();
            var again = new CoreVoyageSession();
            Assert.That(again.TryRestoreVoyageCheckpoint(migrated, out reason), Is.True, reason);
            CollectionAssert.AreEqual(restored.GetBattleDeck(stage), again.GetBattleDeck(stage));
            Assert.That(again.GetCardUpgradeLevel("card_retreat"), Is.EqualTo(2));
            Assert.That(again.CardUpgradeLevels["card_retreat"], Is.EqualTo(3));
        }

        [TestCase("fire")]
        [TestCase("arcane")]
        [TestCase("curse")]
        [TestCase("tide")]
        [TestCase("thunder")]
        [TestCase("blood")]
        public void SchoolRulesRemainSeparateEvenWhenPrintedDamageDrawOrGuardMatchesNeutralCards(string school)
        {
            var session = new CoreVoyageSession();
            CoreTreasureProgress progress = session.CaptureTreasureProgress();
            progress.pirateExperience = 5400;
            session.RestoreTreasureProgress(progress);
            int[] boarding = session.GetAvailableBattleCards(NavalBattleStage.Boarding)
                .Where(index => session.GetBattleCard(index, NavalBattleStage.Boarding).SchoolId == school).ToArray();
            Assert.That(boarding.Length, Is.EqualTo(15));
            Assert.That(boarding.Select(index => session.GetBattleCard(index, NavalBattleStage.Boarding).BoardingSpec.Key)
                .Distinct().Count(), Is.EqualTo(15));
            foreach (int index in boarding)
                Assert.That(session.GetBattleCard(index, NavalBattleStage.Boarding).Id, Is.EqualTo(CoreVoyageSession.CardLibrary[index].Id));
            Assert.That(session.GetAvailableBattleCards(NavalBattleStage.Naval)
                .Any(index => session.GetBattleCard(index, NavalBattleStage.Naval).SchoolId == school), Is.True);
        }

        [Test]
        public void NavalSourceCardsKeepTheirOriginalDesignAndIndependentGrowth()
        {
            var session = new CoreVoyageSession();
            const string firstId = "card_special_cannon_redwake_repeater";
            const string secondId = "card_special_cannon_stormnail_array";
            int first = Index(firstId), second = Index(secondId);
            session.ConfigureSpecialCardSources(null, new[] { "cannon_redwake_repeater", "cannon_stormnail_array" });
            Assert.That(session.GetBattleCard(first).Name, Is.EqualTo(CoreVoyageSession.CardLibrary[first].Name));
            Assert.That(session.GetBattleCard(second).Name, Is.EqualTo(CoreVoyageSession.CardLibrary[second].Name));
            Assert.That(session.GetBattleCard(first).Name, Is.Not.EqualTo(session.GetBattleCard(second).Name));
            Assert.That(session.GetBattleCard(first).ArtId, Is.EqualTo(CoreVoyageSession.CardLibrary[first].ArtId));
            Assert.That(session.GetBattleCard(second).ArtId, Is.EqualTo(CoreVoyageSession.CardLibrary[second].ArtId));
            session.RestoreSpecialCardExperience(new Dictionary<string, int> { [firstId] = 840, [secondId] = 120 });
            Assert.That(session.GetBattleCard(first).UpgradeLevel, Is.EqualTo(session.GetCardMaximumUpgradeLevel(first)));
            Assert.That(session.GetBattleCard(second).UpgradeLevel, Is.EqualTo(1));
            Assert.That(session.GetSpecialCardExperience(firstId), Is.EqualTo(840));
            Assert.That(session.GetSpecialCardExperience(secondId), Is.EqualTo(120));
            CollectionAssert.AreEquivalent(new[] { first, second }, session.GetBonusBattleCards(NavalBattleStage.Naval));
            Assert.That(session.GetBattleCard(Index("card_school_curse_b04")).Energy, Is.EqualTo(2),
                "A generic design must not inherit the cost of a similar source-specific card.");
        }

        [Test]
        public void RedesignedNavalComponentsKeepOwnershipAndIndependentExperience()
        {
            var session = new CoreVoyageSession();
            const string moonId = "card_special_plating_moonsilver_skin";
            const string treeId = "card_special_plating_worldtree_bark";
            session.ConfigureSpecialCardSources(null, new[] { "plating_moonsilver_skin", "plating_worldtree_bark" });
            int moon = Index(moonId), tree = Index(treeId);
            CoreBattleCard first = session.GetBattleCard(moon, NavalBattleStage.Naval);
            CoreBattleCard second = session.GetBattleCard(tree, NavalBattleStage.Naval);
            Assert.That(first.SchoolEffect.Mechanic, Is.EqualTo(CoreSchoolMechanic.ComponentFieldShelter));
            Assert.That(second.SchoolEffect.Mechanic, Is.EqualTo(CoreSchoolMechanic.ComponentFieldShelter));
            Assert.That(first.Id, Is.Not.EqualTo(second.Id));
            Assert.That(first.RequiredComponent, Is.Not.EqualTo(second.RequiredComponent));
            CollectionAssert.Contains(session.GetBonusBattleCards(NavalBattleStage.Naval), moon);
            CollectionAssert.Contains(session.GetBonusBattleCards(NavalBattleStage.Naval), tree);
            CollectionAssert.DoesNotContain(session.GetBonusBattleCards(NavalBattleStage.Boarding), moon);
            CollectionAssert.DoesNotContain(session.GetBonusBattleCards(NavalBattleStage.Boarding), tree);
            session.RestoreSpecialCardExperience(new Dictionary<string, int> { [moonId] = 840, [treeId] = 120 });
            Assert.That(session.GetCardUpgradeLevel(moonId), Is.EqualTo(session.GetCardMaximumUpgradeLevel(moonId)));
            Assert.That(session.GetCardUpgradeLevel(treeId), Is.EqualTo(1));
            session.ConfigureSpecialCardSources(null, new[] { "plating_worldtree_bark" });
            CollectionAssert.DoesNotContain(session.GetBonusBattleCards(NavalBattleStage.Naval), moon);
            CollectionAssert.Contains(session.GetBonusBattleCards(NavalBattleStage.Naval), tree);
            Assert.That(session.GetSpecialCardExperience(moonId), Is.EqualTo(840));
        }

        private static int[] LegacyDeck(CoreVoyageSession session, NavalBattleStage stage) =>
            new[] { "card_turn_port", "card_turn_starboard", "card_turn_about", "card_sail_forward",
                "card_parallel_course", "card_approach", "card_circle_enemy", "card_retreat" }
            .Select(Index).Concat(OtherCards(session, stage).Take(2)).ToArray();

        private static IEnumerable<int> OtherCards(CoreVoyageSession session, NavalBattleStage stage) =>
            session.GetAvailableBattleCards(stage).Where(index =>
                CoreVoyageSession.CardLibrary[index].Id != "card_approach" &&
                CoreVoyageSession.CardLibrary[index].Id != "card_retreat" &&
                !RetiredMovementIds.Contains(CoreVoyageSession.CardLibrary[index].Id));

        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == id);

        private static CoreVoyageSession NewBattle(NavalBattleStage stage)
        {
            var session = new CoreVoyageSession();
            session.SetSeed("CARD-DEDUPLICATION-TEST");
            session.StartVoyage();
            var encounter = new CoreEncounter("card_deduplication_test", "测试敌舰", "海战", "卡牌去重测试", true, false, 1, 70);
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { encounter, false });
            session.Battle.Stage = stage;
            return session;
        }
    }
}
