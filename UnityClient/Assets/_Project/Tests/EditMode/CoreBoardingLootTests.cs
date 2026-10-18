using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Random;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBoardingLootTests
    {
        private const string RouteBoss = "boss_route_shattered_sail_reef";

        [Test]
        public void OrdinaryChanceAndConditionalRarityWeightsMatchTheBalance()
        {
            Assert.That(CoreVoyageSession.BoardingComponentDropChance, Is.EqualTo(.5d));
            Assert.That(CoreBoardingLootBalance.BossEquipmentDropChance, Is.EqualTo(.5d));
            var counts = Enumerable.Range(0, 100).Select(i => CoreBoardingLootBalance.RarityForRoll((i + .5d) / 100d))
                .GroupBy(r => r).ToDictionary(group => group.Key, group => group.Count());
            Assert.That(counts[CoreBoardingComponentRarity.Rare], Is.EqualTo(50));
            Assert.That(counts[CoreBoardingComponentRarity.Epic], Is.EqualTo(40));
            Assert.That(counts[CoreBoardingComponentRarity.Legendary], Is.EqualTo(10));
        }

        [Test]
        public void OrdinaryAndBossEquipmentCanBothDropOrOnlyBossEquipmentCanDrop()
        {
            var both = new CoreVoyageSession(); Queue(both, SeedFor(true, true), RouteBoss);
            CoreBoardingComponentDrop[] drops = Drain(both);
            Assert.That(drops.Length, Is.EqualTo(2));
            Assert.That(drops[0].IsBossEquipment, Is.False);
            Assert.That(drops[0].ComponentId, Is.Empty);
            Assert.That(drops[1].IsBossEquipment, Is.True);
            var bossOnly = new CoreVoyageSession(); Queue(bossOnly, SeedFor(false, true), RouteBoss);
            CoreBoardingComponentDrop[] single = Drain(bossOnly);
            Assert.That(single.Length, Is.EqualTo(1));
            Assert.That(single[0].IsBossEquipment, Is.True);
        }

        [Test]
        public void AllRealBossesDropOnlyTheirAuthoredEquipmentAndRarity()
        {
            foreach (string id in CoreRouteBossCatalog.All.Select(b => b.Id).Concat(CoreLegendaryWarships.All.Select(b => b.Id)))
            {
                var session = new CoreVoyageSession(); Queue(session, SeedFor(false, true), id);
                CoreBoardingComponentDrop drop = Drain(session).Single();
                var authored = CoreBoardingLootBalance.BossEquipment(id, out CoreBoardingComponentRarity rarity);
                Assert.That(authored, Does.Contain(drop.ComponentId), id);
                Assert.That(drop.Slot, Is.EqualTo(CoreBoardingLootBalance.ComponentSlot(drop.ComponentId)), id);
                Assert.That(drop.Rarity, Is.EqualTo(CoreRouteBossCatalog.Find(id) != null
                    ? CoreBoardingComponentRarity.Epic : CoreBoardingComponentRarity.Legendary), id);
                Assert.That(drop.Rarity, Is.EqualTo(rarity), id);
            }
        }

        [TestCase("tutorial_boss_event")]
        [TestCase("treasure_boss_moon_vault")]
        [TestCase("random_ambush")]
        public void EncountersWithoutEquipmentNeverInventABossItem(string id)
        {
            var session = new CoreVoyageSession(); Queue(session, 0u, id);
            CoreBoardingComponentDrop[] drops = Drain(session);
            Assert.That(drops.Length, Is.EqualTo(1));
            Assert.That(drops[0].IsBossEquipment, Is.False);
            Assert.That(CoreBoardingLootBalance.BossEquipment(id, out _), Is.Empty);
        }

        [TestCase(false, 1d)] [TestCase(true, 0d)]
        public void SinkingOrExhaustedChartProducesNoItems(bool captured, double multiplier)
        {
            var session = new CoreVoyageSession(); Queue(session, SeedFor(true, true), RouteBoss, captured, multiplier);
            Assert.That(session.TryPeekBoardingComponentDrop(out _), Is.False);
        }

        [Test]
        public void ChartMultiplierReducesBothOpportunities()
        {
            uint seed = Enumerable.Range(0, 1000).Select(i => (uint)i).First(value =>
            {
                var random = new Mulberry32Random(value);
                double ordinary = random.NextDouble(), boss = random.NextDouble();
                return ordinary >= .25d && ordinary < .5d && boss >= .25d && boss < .5d;
            });
            var full = new CoreVoyageSession(); Queue(full, seed, RouteBoss);
            var half = new CoreVoyageSession(); Queue(half, seed, RouteBoss, multiplier: .5d);
            Assert.That(Drain(full).Length, Is.EqualTo(2));
            Assert.That(Drain(half), Is.Empty);
        }

        [Test]
        public void PeekAndWrongAcknowledgementPreserveTheRewardAndRandomStream()
        {
            var session = new CoreVoyageSession(); Queue(session, SeedFor(true, true), RouteBoss);
            uint state = Random(session).State;
            Assert.That(session.TryPeekBoardingComponentDrop(out CoreBoardingComponentDrop first), Is.True);
            Assert.That(session.TryPeekBoardingComponentDrop(out CoreBoardingComponentDrop repeated), Is.True);
            Assert.That(repeated, Is.SameAs(first));
            Assert.That(session.AcknowledgeBoardingComponentDrop(first.Sequence + 1), Is.False);
            Assert.That(Random(session).State, Is.EqualTo(state));
            Assert.That(session.AcknowledgeBoardingComponentDrop(first.Sequence), Is.True);
            Assert.That(session.AcknowledgeBoardingComponentDrop(first.Sequence), Is.False);
            Assert.That(Drain(session).Length, Is.EqualTo(1));
        }

        [Test]
        public void QueueSurvivesCheckpointWithoutCaptureOrPeekRerollingIt()
        {
            var source = Sailing(); Queue(source, SeedFor(true, true), RouteBoss);
            Assert.That(source.TryPeekBoardingComponentDrop(out CoreBoardingComponentDrop first), Is.True);
            uint state = Random(source).State;
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            Assert.That(Random(source).State, Is.EqualTo(state));
            Assert.That(source.TryPeekBoardingComponentDrop(out CoreBoardingComponentDrop after), Is.True);
            Assert.That(after, Is.SameAs(first));
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
            Assert.That(Drain(restored).Select(Key), Is.EqualTo(Drain(source).Select(Key)));
            Queue(source, state, RouteBoss); Queue(restored, state, RouteBoss);
            Assert.That(Drain(restored).Select(Key), Is.EqualTo(Drain(source).Select(Key)));
        }

        [Test]
        public void LegacySlotImportsAsRareAndOldConsumeApiRemainsOneShot()
        {
            CoreVoyageCheckpoint old = Sailing().CaptureVoyageCheckpoint();
            old.boardingLootVersion = 0; old.nextBoardingComponentSequence = 0; old.pendingBoardingComponents = null;
            old.pendingBoardingComponentDrop = true; old.pendingBoardingComponentSlot = 3;
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(old, out string reason), Is.True, reason);
            Assert.That(Random(restored).State, Is.EqualTo(old.voyageRandomState));
            Assert.That(restored.TryPeekBoardingComponentDrop(out CoreBoardingComponentDrop drop), Is.True);
            Assert.That(drop.Rarity, Is.EqualTo(CoreBoardingComponentRarity.Rare));
            Assert.That(restored.TryConsumeBoardingComponentDrop(out int slot), Is.True);
            Assert.That(slot, Is.EqualTo(3));
            Assert.That(restored.TryConsumeBoardingComponentDrop(out _), Is.False);
            Assert.That(restored.CaptureVoyageCheckpoint().pendingBoardingComponents, Is.Empty);
        }

        [TestCase("rarity")] [TestCase("slot")] [TestCase("sequence")]
        [TestCase("bossId")] [TestCase("mirror")] [TestCase("version")]
        public void CorruptQueueIsRejectedWithoutChangingTheLoadedSession(string corruption)
        {
            var source = Sailing(); Queue(source, SeedFor(true, true), RouteBoss);
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            switch (corruption)
            {
                case "rarity": checkpoint.pendingBoardingComponents[0].rarity = 0; break;
                case "slot": checkpoint.pendingBoardingComponents[0].slot = 5; break;
                case "sequence": checkpoint.pendingBoardingComponents[1].sequence = checkpoint.pendingBoardingComponents[0].sequence; break;
                case "bossId": checkpoint.pendingBoardingComponents[1].componentId = "bow_oak_ram"; break;
                case "mirror": checkpoint.pendingBoardingComponentSlot = (checkpoint.pendingBoardingComponentSlot + 1) % 5; break;
                case "version": checkpoint.boardingLootVersion = 2; break;
            }
            var target = new CoreVoyageSession(); target.RestoreProfile(876m, target.CurrentPort);
            Assert.That(target.TryRestoreVoyageCheckpoint(checkpoint, out _), Is.False);
            Assert.That(target.ProfileGold, Is.EqualTo(876m));
            Assert.That(target.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(target.TryPeekBoardingComponentDrop(out _), Is.False);
        }

        [Test]
        public void StartingAnotherVoyagePreservesUnclaimedItems()
        {
            var session = new CoreVoyageSession(); Queue(session, SeedFor(true, true), RouteBoss);
            Assert.That(session.TryPeekBoardingComponentDrop(out CoreBoardingComponentDrop pending), Is.True);
            session.StartVoyage();
            Assert.That(session.TryPeekBoardingComponentDrop(out CoreBoardingComponentDrop retained), Is.True);
            Assert.That(retained, Is.SameAs(pending));
            Assert.That(Drain(session).Length, Is.EqualTo(2));
        }

        [Test]
        public void StartingAnotherBattlePreservesUnclaimedItems()
        {
            var session = Sailing(); Queue(session, SeedFor(true, true), RouteBoss);
            Assert.That(session.TryPeekBoardingComponentDrop(out CoreBoardingComponentDrop pending), Is.True);
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { CoreRouteBossCatalog.Find(RouteBoss).CreateEncounter(), true });
            Assert.That(session.Battle, Is.Not.Null);
            Assert.That(session.TryPeekBoardingComponentDrop(out CoreBoardingComponentDrop retained), Is.True);
            Assert.That(retained, Is.SameAs(pending));
            Assert.That(Drain(session).Length, Is.EqualTo(2));
        }

        [Test]
        public void CapturingAnUnconvertedLegacySlotDoesNotMutateTheSession()
        {
            var session = Sailing();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(CoreVoyageSession).GetField("pendingBoardingComponentDrop", flags).SetValue(session, true);
            typeof(CoreVoyageSession).GetField("pendingBoardingComponentSlot", flags).SetValue(session, 2);
            uint state = Random(session).State;
            CoreVoyageCheckpoint captured = session.CaptureVoyageCheckpoint();
            Assert.That(Random(session).State, Is.EqualTo(state));
            Assert.That(typeof(CoreVoyageSession).GetField("nextBoardingComponentSequence", flags).GetValue(session), Is.EqualTo(1));
            Assert.That(captured.pendingBoardingComponents.Length, Is.EqualTo(1));
            Assert.That(captured.pendingBoardingComponents[0].slot, Is.EqualTo(2));
            Assert.That(captured.nextBoardingComponentSequence, Is.EqualTo(2));
        }

        private static string Key(CoreBoardingComponentDrop d) => d.Sequence + ":" + d.Slot + ":" + d.Rarity + ":" +
            d.SelectionSeed + ":" + d.ComponentId + ":" + d.SourceEncounterId + ":" + d.IsBossEquipment;
        private static CoreVoyageSession Sailing() { var session = new CoreVoyageSession(); session.StartVoyage(); return session; }
        private static Mulberry32Random Random(CoreVoyageSession s) => (Mulberry32Random)typeof(CoreVoyageSession)
            .GetField("voyageRandom", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(s);
        private static void Queue(CoreVoyageSession s, uint seed, string id, bool captured = true, double multiplier = 1d)
        {
            typeof(CoreVoyageSession).GetField("voyageRandom", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(s, new Mulberry32Random(seed));
            typeof(CoreVoyageSession).GetMethod("QueueBoardingVictoryLoot", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(s, new object[] { captured, id, multiplier });
        }
        private static CoreBoardingComponentDrop[] Drain(CoreVoyageSession s)
        {
            var drops = new List<CoreBoardingComponentDrop>();
            while (s.TryPeekBoardingComponentDrop(out CoreBoardingComponentDrop drop))
            { drops.Add(drop); Assert.That(s.AcknowledgeBoardingComponentDrop(drop.Sequence), Is.True); }
            return drops.ToArray();
        }
        private static uint SeedFor(bool ordinary, bool boss)
        {
            for (uint seed = 0; seed < 1000; seed++)
            {
                var random = new Mulberry32Random(seed);
                bool ordinaryDrop = random.NextDouble() < .5d, bossDrop = random.NextDouble() < .5d;
                if (ordinaryDrop == ordinary && bossDrop == boss) return seed;
            }
            throw new InvalidOperationException("Expected deterministic seed not found.");
        }
    }
}
