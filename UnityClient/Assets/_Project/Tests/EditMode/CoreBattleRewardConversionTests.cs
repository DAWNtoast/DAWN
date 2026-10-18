using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBattleRewardConversionTests
    {
        private static readonly string[] Materials = {
            "Water", "Food", "Beer", "Medicine", "Wood", "Iron", "Cloth", "Powder", "Cannonballs",
            "Gem", "AncientMaterial", "Relic", "BlackPearl", "AncientCompass", "Lantern"
        };

        private static IEnumerable<TestCaseData> MaterialPrices()
        {
            decimal[] prices = { .032m, .096m, 3m, 6.4m, 5m, 10m, 7m, 5m, 8m, 60m, 8m, 100m, 120m, 75m, 20m };
            for (int i = 0; i < Materials.Length; i++)
                yield return new TestCaseData(Materials[i], prices[i]).SetName("MaterialConversion_" + Materials[i]);
        }

        [TestCaseSource(nameof(MaterialPrices))]
        public void EveryPhysicalMaterialUsesItsFixedPriceAndIgnoresNegativeCosts(string material, decimal unitPrice)
        {
            var reward = new VoyageEffect();
            PropertyInfo property = typeof(VoyageEffect).GetProperty(material);
            property.SetValue(reward, 3);
            Assert.That(CoreBattleRewardBalance.MaterialGoldValue(reward), Is.EqualTo(3m * unitPrice));
            Assert.That(CoreBattleRewardBalance.MaterialGoldValue(reward, doctor: true), Is.EqualTo(3m * unitPrice));
            Assert.That(property.GetValue(reward), Is.EqualTo(3), "Pricing must not consume or mutate the promise.");
            property.SetValue(reward, -3);
            Assert.That(CoreBattleRewardBalance.MaterialGoldValue(reward, doctor: true, diver: true), Is.Zero);
        }

        [Test]
        public void NonCargoEffectsHaveNoMaterialPrice()
        {
            Assert.That(CoreBattleRewardBalance.MaterialGoldValue(null), Is.Zero);
            Assert.That(CoreBattleRewardBalance.MaterialGoldValue(new VoyageEffect {
                Gold = 500, Fame = 50, Hull = 20, Morale = 30, Reveal = 4,
                MapFragments = 5, TreasureClues = 6, Curse = 7, TreasureComplete = true
            }), Is.Zero);
        }

        [TestCase(1, 30)]
        [TestCase(3, 60)]
        [TestCase(8, 150)]
        [TestCase(11, 195)]
        public void DiverSalvageAddsAtLeastOneAndThenOneQuarterBeforePricing(int quantity, int goldValue)
        {
            var reward = new VoyageEffect { Wood = quantity, Iron = quantity };
            Assert.That(CoreBattleRewardBalance.MaterialGoldValue(reward, diver: true), Is.EqualTo((decimal)goldValue));
            Assert.That(reward.Wood, Is.EqualTo(quantity));
            Assert.That(reward.Iron, Is.EqualTo(quantity));
        }

        [Test]
        public void AlreadyAdjustedCrewRewardIsNotBoostedTwice()
        {
            var reward = new VoyageEffect { Wood = 10, Iron = 4, Medicine = 3 };
            typeof(VoyageEffect).GetProperty("CrewBonusesApplied", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(reward, true);
            Assert.That(CoreBattleRewardBalance.MaterialGoldValue(reward, doctor: true, diver: true), Is.EqualTo(109.2m));
        }

        [TestCase(false, 0, 100)]
        [TestCase(true, 0, 100)]
        [TestCase(false, 40, 100)]
        [TestCase(true, 40, 200)]
        [TestCase(false, 104, 100)]
        [TestCase(false, 105, 200)]
        [TestCase(false, 205, 300)]
        public void BaseAndPromisedGoldRoundOnceWithPositiveMinimumAndMidpointsAwayFromZero(bool boss, int promised, long expected)
        {
            var reward = new VoyageEffect { Gold = promised };
            Assert.That(CoreBattleRewardBalance.GoldReward(boss, reward, false), Is.EqualTo(expected));
            Assert.That(CoreBattleRewardBalance.GoldReward(boss, reward, true), Is.EqualTo(expected * 2));
        }

        [TestCase(0d, 0L)]
        [TestCase(.25d, 100L)]
        [TestCase(.5d, 200L)]
        [TestCase(1d, 400L)]
        public void ChartReductionPrecedesRoundingAndCaptureDoublesTheSameQuote(double multiplier, long expected)
        {
            var reward = new VoyageEffect { Gold = 300, Iron = 1 };
            Assert.That(CoreBattleRewardBalance.GoldReward(false, reward, false, multiplier), Is.EqualTo(expected));
            Assert.That(CoreBattleRewardBalance.GoldReward(false, reward, true, multiplier), Is.EqualTo(expected * 2));
        }

        [Test]
        public void ExtremeDiverMaterialsAndGoldStayWideUntilCurrencyIsCredited()
        {
            var reward = new VoyageEffect { Gold = int.MaxValue, Wood = int.MaxValue };
            Assert.That(CoreBattleRewardBalance.MaterialGoldValue(reward, diver: true), Is.EqualTo(13421772790m));
            Assert.That(CoreBattleRewardBalance.GoldReward(false, reward, false, diver: true), Is.EqualTo(15569256500L));
            Assert.That(CoreBattleRewardBalance.GoldReward(false, reward, true, diver: true), Is.EqualTo(31138513000L));
        }

        [TestCase(false, false, false, 1300)]
        [TestCase(true, false, false, 2600)]
        [TestCase(false, true, true, 1400)]
        [TestCase(true, true, true, 2800)]
        public void VictoryConvertsAllFifteenMaterialsOnceAndPreservesOtherProgress(bool captured, bool doctor, bool diver, int expectedGold)
        {
            CoreVoyageSession session = Voyage(doctor, diver);
            var reward = MaterialsReward(3);
            reward.Gold = 20; reward.Fame = 4; reward.MapFragments = 2; reward.TreasureClues = 2; reward.Curse = 2;
            int[] cargo = Snapshot(session);
            int gold = session.Cargo.Gold, fame = session.Cargo.Fame, curse = session.Cargo.Curse;
            Enter(session, reward);
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.Cargo.Gold, Is.EqualTo(gold));
            CollectionAssert.AreEqual(cargo, Snapshot(session), "Accepting battle must not prepay materials.");
            Win(session, captured);
            Assert.That(session.Cargo.Gold - gold, Is.EqualTo(expectedGold));
            Assert.That(session.Cargo.Fame - fame, Is.EqualTo(captured ? 20 : 10));
            CollectionAssert.AreEqual(cargo, Snapshot(session), "All fifteen physical rewards must be converted, never loaded.");
            Assert.That(session.VoyageTreasureFragments, Is.EqualTo(captured ? 3 : 2), "Authored fragment rewards retain their existing capture rule.");
            Assert.That(session.TreasureClues, Is.EqualTo(2));
            Assert.That(session.Cargo.Curse, Is.EqualTo(curse + 2));
            Assert.That(session.EndBattleTurn(), Is.False);
            Assert.That(session.ResolveChoice(0), Is.False);
            Assert.That(session.Cargo.Gold - gold, Is.EqualTo(expectedGold));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExactlyFullHoldCanEnterBattleAndReceiveConvertedLoot(bool captured)
        {
            CoreVoyageSession session = Voyage();
            foreach (string material in Materials) typeof(VoyageCargo).GetProperty(material).SetValue(session.Cargo, 0);
            session.Cargo.Wood = (int)session.CargoCapacityValue;
            Assert.That(session.Cargo.UsedVolume, Is.EqualTo(session.CargoCapacityValue).Within(.001f));
            int[] cargo = Snapshot(session);
            int gold = session.Cargo.Gold;
            Enter(session, MaterialsReward(3));
            Assert.That(session.IsChoiceAvailable(0), Is.True);
            Assert.That(session.ResolveChoice(0), Is.True);
            Win(session, captured);
            Assert.That(session.Cargo.Gold - gold, Is.EqualTo(captured ? 2600 : 1300));
            CollectionAssert.AreEqual(cargo, Snapshot(session));
            Assert.That(session.Cargo.UsedVolume, Is.EqualTo(session.CargoCapacityValue).Within(.001f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NegativeMaterialCostsArePaidBeforeBattleAndDoctorDiscountsOnlyMedicine(bool doctor)
        {
            CoreVoyageSession session = Voyage(doctor);
            foreach (string material in Materials) typeof(VoyageCargo).GetProperty(material).SetValue(session.Cargo, 2);
            session.Cargo.Medicine = 6;
            int[] before = Snapshot(session);
            var effect = MaterialsReward(-1);
            effect.Medicine = -5; effect.Gold = 110;
            int gold = session.Cargo.Gold;
            Enter(session, effect);
            Assert.That(session.ResolveChoice(0), Is.True);
            int[] paid = Snapshot(session);
            for (int i = 0; i < Materials.Length; i++)
                Assert.That(paid[i], Is.EqualTo(before[i] - (Materials[i] == "Medicine" ? (doctor ? 4 : 5) : 1)), Materials[i]);
            Assert.That(session.Cargo.Gold, Is.EqualTo(gold));
            Win(session, false);
            Assert.That(session.Cargo.Gold - gold, Is.EqualTo(200));
            CollectionAssert.AreEqual(paid, Snapshot(session), "Victory must not refund or recharge entry costs.");
        }

        [Test]
        public void FutureGoldCannotPayAnUnaffordableMaterialEntryCost()
        {
            CoreVoyageSession session = Voyage();
            session.Cargo.Wood = 2;
            Enter(session, new VoyageEffect { Wood = -3, Gold = 10000 });
            int gold = session.Cargo.Gold;
            Assert.That(session.IsChoiceAvailable(0), Is.False);
            Assert.That(session.ResolveChoice(0), Is.False);
            Assert.That(session.Battle, Is.Null);
            Assert.That(session.Cargo.Wood, Is.EqualTo(2));
            Assert.That(session.Cargo.Gold, Is.EqualTo(gold));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DefeatDoesNotCreditPromisedGoldMaterialsFameOrFragments(bool boarding)
        {
            CoreVoyageSession session = Voyage();
            var reward = MaterialsReward(3);
            reward.Gold = 1000; reward.Fame = 50; reward.MapFragments = 4;
            int gold = session.Cargo.Gold, fame = session.Cargo.Fame;
            int[] cargo = Snapshot(session);
            Enter(session, reward);
            Assert.That(session.ResolveChoice(0), Is.True);
            if (boarding) { session.Battle.Stage = NavalBattleStage.Boarding; session.Battle.PlayerBoardingHealth = 0; }
            else typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, 0);
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            Assert.That(session.Settlement.BankedGold, Is.Zero);
            Assert.That(session.Settlement.BankedFame, Is.Zero);
            Assert.That(session.Settlement.BankedFragments, Is.Zero);
            Assert.That(session.Cargo.Gold, Is.EqualTo(gold));
            Assert.That(session.Cargo.Fame, Is.EqualTo(fame));
            CollectionAssert.AreEqual(cargo, Snapshot(session));
        }

        [TestCase(0d, false, 0, 0)]
        [TestCase(0d, true, 0, 0)]
        [TestCase(.25d, false, 100, 3)]
        [TestCase(.25d, true, 200, 6)]
        [TestCase(.5d, false, 200, 6)]
        [TestCase(.5d, true, 400, 12)]
        public void SettlementUsesTheCombinedGoldQuoteUnderChartReduction(double multiplier, bool captured, int expectedGold, int expectedFame)
        {
            CoreVoyageSession session = Voyage(diver: true);
            Enter(session, new VoyageEffect { Gold = 280, Fame = 6, Iron = 1, Wood = 2 });
            Assert.That(session.ResolveChoice(0), Is.True);
            SetField(session, "pendingSeaChartRewardMultiplier", multiplier);
            int gold = session.Cargo.Gold, fame = session.Cargo.Fame;
            int[] cargo = Snapshot(session);
            Win(session, captured);
            // Base 45 + promised 280 + salvaged iron 20 + salvaged wood 15 = 360.
            Assert.That(session.Cargo.Gold - gold, Is.EqualTo(expectedGold));
            Assert.That(session.Cargo.Fame - fame, Is.EqualTo(expectedFame));
            CollectionAssert.AreEqual(cargo, Snapshot(session));
        }

        private static CoreVoyageSession Voyage(bool doctor = false, bool diver = false)
        {
            var session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(false, false, doctor, false, false, diver);
            session.StartVoyage();
            return session;
        }

        private static VoyageEffect MaterialsReward(int quantity)
        {
            var reward = new VoyageEffect();
            foreach (string material in Materials) typeof(VoyageEffect).GetProperty(material).SetValue(reward, quantity);
            return reward;
        }

        private static int[] Snapshot(CoreVoyageSession session) => Materials
            .Select(material => (int)typeof(VoyageCargo).GetProperty(material).GetValue(session.Cargo)).ToArray();

        private static void Enter(CoreVoyageSession session, VoyageEffect reward)
        {
            var encounter = new CoreEncounter("conversion-audit", "折价测试敌舰", "海战", "测试", true, false, 1, 70,
                new CoreEncounterChoice("迎战", "胜利后结算", 0, reward, startsBattle: true));
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session, encounter);
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, CoreDemoPhase.Encounter);
        }

        private static void Win(CoreVoyageSession session, bool captured)
        {
            if (captured) { session.Battle.Stage = NavalBattleStage.Boarding; session.Battle.EnemyBoardingHealth = 0; }
            else session.Battle.EnemyHull = 0;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.Battle, Is.Null);
        }

        private static void SetField(CoreVoyageSession session, string name, object value) =>
            typeof(CoreVoyageSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, value);
    }
}
