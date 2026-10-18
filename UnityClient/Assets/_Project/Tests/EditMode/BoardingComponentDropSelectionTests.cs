using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Random;
using ShatteredSail.Presentation;

namespace ShatteredSail.Core.Tests
{
    public sealed class BoardingComponentDropSelectionTests
    {
        [Test]
        public void OrdinarySelectionUsesSavedRaritySlotAndSeedRegardlessOfCatalogOrder()
        {
            var drop = new CoreBoardingComponentDrop(1, 1, CoreBoardingComponentRarity.Epic, 7u, "", "random_ambush", false);
            ComponentArtEntry[] entries = {
                Entry("sail_epic_z", 1, ComponentRarity.Epic), Entry("sail_rare", 1, ComponentRarity.Rare),
                Entry("bow_epic", 0, ComponentRarity.Epic), Entry("sail_epic_a", 1, ComponentRarity.Epic)
            };
            ComponentArtEntry chosen = BoardingComponentDropSelection.Select(drop, entries);
            Assert.That(chosen.Id, Is.EqualTo("sail_epic_z"));
            Assert.That(BoardingComponentDropSelection.Select(drop, entries.Reverse().ToArray()), Is.SameAs(chosen));
            Assert.That(chosen.Slot, Is.EqualTo(ComponentSlot.Sail));
            Assert.That(chosen.Rarity, Is.EqualTo(ComponentRarity.Epic));
        }

        [Test]
        public void MissingExactBossItemNeverFallsBackToAnotherFitting()
        {
            var drop = new CoreBoardingComponentDrop(1, 0, CoreBoardingComponentRarity.Legendary,
                2u, "bow_expected", "legendary_pirate_iron_oath", true);
            ComponentArtEntry wrong = Entry("bow_other", 0, ComponentRarity.Legendary);
            Assert.That(BoardingComponentDropSelection.Select(drop, new[] { wrong }), Is.Null);
            ComponentArtEntry expected = Entry("bow_expected", 0, ComponentRarity.Legendary);
            Assert.That(BoardingComponentDropSelection.Select(drop, new[] { wrong, expected }), Is.SameAs(expected));
            Assert.That(BoardingComponentDropSelection.Select(drop, new[] { Entry("bow_expected", 0, ComponentRarity.Epic) }), Is.Null);
        }

        [Test]
        public void FailedGrantOrUnavailableCatalogLeavesTheExactRewardForRetry()
        {
            var session = new CoreVoyageSession();
            typeof(CoreVoyageSession).GetField("voyageRandom", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(session, new Mulberry32Random(0u));
            typeof(CoreVoyageSession).GetMethod("QueueBoardingVictoryLoot", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { true, "random_ambush", 1d });
            Assert.That(session.TryPeekBoardingComponentDrop(out CoreBoardingComponentDrop pending), Is.True);
            ComponentArtEntry item = Entry("saved_reward", pending.Slot, (ComponentRarity)pending.Rarity);
            int attempts = 0;
            Assert.That(BoardingComponentDropSelection.TryGrantNext(session, new ComponentArtEntry[0],
                entry => { attempts++; return true; }, out _), Is.False);
            Assert.That(attempts, Is.Zero);
            Assert.That(BoardingComponentDropSelection.TryGrantNext(session, new[] { item },
                entry => { attempts++; return false; }, out _), Is.False);
            Assert.That(session.TryPeekBoardingComponentDrop(out CoreBoardingComponentDrop retained), Is.True);
            Assert.That(retained, Is.SameAs(pending));
            Assert.That(BoardingComponentDropSelection.TryGrantNext(session, new[] { item },
                entry => { attempts++; return true; }, out ComponentArtEntry granted), Is.True);
            Assert.That(granted, Is.SameAs(item));
            Assert.That(attempts, Is.EqualTo(2));
            Assert.That(session.TryPeekBoardingComponentDrop(out _), Is.False);
            Assert.That(BoardingComponentDropSelection.TryGrantNext(session, new[] { item }, entry => true, out _), Is.False);
        }

        [Test]
        public void EveryBossEquipmentIdResolvesAgainstTheRealCatalogWithItsExpectedRarity()
        {
            var catalog = new ComponentArtCatalog();
            foreach (string encounterId in CoreRouteBossCatalog.All.Select(b => b.Id).Concat(CoreLegendaryWarships.All.Select(b => b.Id)))
                foreach (string id in CoreBoardingLootBalance.BossEquipment(encounterId, out CoreBoardingComponentRarity rarity))
                {
                    var drop = new CoreBoardingComponentDrop(1, CoreBoardingLootBalance.ComponentSlot(id), rarity,
                        0u, id, encounterId, true);
                    ComponentArtEntry resolved = BoardingComponentDropSelection.Select(drop, catalog.ExpandedEntries);
                    Assert.That(resolved, Is.Not.Null, encounterId + " / " + id);
                    Assert.That(resolved.Id, Is.EqualTo(id));
                    Assert.That((int)resolved.Rarity, Is.EqualTo((int)rarity));
                }
        }

        private static ComponentArtEntry Entry(string id, int slot, ComponentRarity rarity) => new ComponentArtEntry(
            id, id, (ComponentSlot)slot, rarity, 1, 100, "effect", "specialization", "description", id, "",
            0, 0, 0, 0f, 0f);
    }
}
