using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreCardUpgradeLimitTests
    {
        [TestCase(0, 1)] [TestCase(1, 1)] [TestCase(2, 2)] [TestCase(3, 3)] [TestCase(5, 3)]
        public void UnauthoredFixtureUtilityLimits_KeepLegacyFallbackByPrintedCost(int energy, int cap)
        {
            var draw = new CoreBattleCard("utility_draw", "抽牌", "策略", energy, 0, 0, 0, 3, 0, "抽 {draw} 张");
            var recovery = new CoreBattleCard("utility_energy", "回能", "策略", energy, 0, 0, 0, 0, 0, "恢复能量",
                action: NavalBattleCardAction.Energize);
            Assert.That(CoreBattleUpgradeBalance.UtilityMaximumLevel(draw), Is.EqualTo(cap));
            Assert.That(CoreBattleUpgradeBalance.MaximumLevelFor(draw), Is.EqualTo(cap));
            Assert.That(CoreBattleUpgradeBalance.MaximumLevelFor(recovery), Is.EqualTo(cap));
            for (int rank = 0; rank <= 5; rank++)
            {
                int expectedBonus = Math.Min(rank, cap);
                Assert.That(CoreBattleUpgradeBalance.UtilityBonus(draw, rank), Is.EqualTo(expectedBonus));
                Assert.That(CoreBattleUpgradeBalance.UpgradeDraw(draw, rank), Is.EqualTo(3 + expectedBonus));
            }
        }

        [TestCase(0, 4, 1)] [TestCase(1, 5, 1)] [TestCase(2, 7, 1)] [TestCase(3, 9, 2)]
        public void MixedOneCostCard_GrowsDamageFirstAndAddsDrawAtItsLastRank(int rank, int damage, int draw)
        {
            CoreVoyageSession session = Boarding("basic_11", rank);
            int index = Find("card_boarding_basic_11");
            CoreBattleCard card = session.GetBattleCard(index);
            Assert.That(session.GetCardMaximumUpgradeLevel(index), Is.EqualTo(3));
            Assert.That(card.UpgradeLevel, Is.EqualTo(rank));
            Assert.That(card.Damage, Is.EqualTo(damage));
            Assert.That(card.Draw, Is.EqualTo(draw));
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(1000 - damage));
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(draw));
        }

        [TestCase(0, 3, 0)] [TestCase(1, 4, 1)] [TestCase(2, 5, 2)] [TestCase(3, 5, 2)]
        public void PureTwoCostDraw_AddsOnePerUpgradeThenStopsAtTwo(int requestedRank, int draw, int effectiveRank)
        {
            CoreVoyageSession session = Boarding("basic_08", requestedRank);
            int index = Find("card_boarding_basic_08");
            Assert.That(session.GetCardMaximumUpgradeLevel(index), Is.EqualTo(2));
            Assert.That(session.GetBattleCard(index).UpgradeLevel, Is.EqualTo(effectiveRank));
            Assert.That(session.GetBattleCard(index).Draw, Is.EqualTo(draw));
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(draw));
            Assert.That(session.Battle.Energy, Is.EqualTo(8));
        }

        [TestCase("basic_09", 0, 1, 0)] [TestCase("basic_09", 1, 2, 1)]
        [TestCase("basic_09", 2, 2, 1)] [TestCase("basic_09", 3, 2, 1)]
        [TestCase("basic_10", 0, 3, 0)] [TestCase("basic_10", 1, 4, 1)]
        [TestCase("basic_10", 2, 5, 2)] [TestCase("basic_10", 3, 5, 2)]
        public void EnergyRecovery_UsesItsOwnCappedValueAtActualPayment(string key, int requestedRank, int recovered, int effectiveRank)
        {
            CoreVoyageSession session = Boarding(key, requestedRank);
            CoreBattleCard card = session.GetBattleCard(Find("card_boarding_" + key));
            Assert.That(card.UpgradeLevel, Is.EqualTo(effectiveRank));
            Assert.That(CoreBattleUpgradeBalance.EnergyRecovery(card), Is.EqualTo(recovered));
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.Energy, Is.EqualTo(10 - card.Energy + recovered));
        }

        [TestCase(0, 1, 8)] [TestCase(1, 1, 10)] [TestCase(2, 1, 14)] [TestCase(3, 1, 14)]
        public void ThunderConduit_KeepsDrawFixedAndGrowsItsHiddenAttackBonus(int rank, int draw, int bonus)
        {
            CoreVoyageSession session = Boarding("thunder_06", rank);
            int index = Find("card_boarding_thunder_06");
            Assert.That(session.GetCardMaximumUpgradeLevel(index), Is.EqualTo(2));
            Assert.That(session.GetBattleCard(index).Draw, Is.EqualTo(draw));
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(draw));
            Assert.That(session.Battle.Boarding.ThunderReady, Is.EqualTo(bonus));
        }

        [TestCase(0, 8)] [TestCase(1, 10)] [TestCase(2, 14)] [TestCase(3, 14)]
        public void HiddenBurnDamage_HasTwoRealEffectRanksAndClampsTheThird(int rank, int burn)
        {
            CoreVoyageSession session = Boarding("fire_03", rank);
            int index = Find("card_boarding_fire_03");
            Assert.That(CoreVoyageSession.CardLibrary[index].Damage, Is.Zero, "The scaling benefit is authored in boarding execution.");
            Assert.That(session.GetCardMaximumUpgradeLevel(index), Is.EqualTo(2));
            Assert.That(session.HasBattleCardUpgradeBenefit(index), Is.True);
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.EnemyBurnDamage, Is.EqualTo(burn));
        }

        [TestCase("card_boarding_fire_04", 3)]
        [TestCase("card_boarding_fire_07", 2)]
        [TestCase("card_boarding_tide_09", 2)]
        [TestCase("card_boarding_curse_09", 2)]
        [TestCase("card_boarding_thunder_10", 2)]
        public void HiddenScalableEffects_UseTheirAuthoredMaximum(string id, int maximum)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.GetCardMaximumUpgradeLevel(id), Is.EqualTo(maximum));
            Assert.That(session.GetBattleCardAtLevel(Find(id), 3).UpgradeLevel, Is.EqualTo(maximum));
        }

        [TestCase("card_boarding_arcane_03")]
        [TestCase("card_boarding_arcane_12")]
        [TestCase("card_boarding_fire_10")]
        public void FixedEffects_HaveNoPurchasableOrEffectiveRank(string id)
        {
            CoreVoyageSession session = RichPort();
            int index = Find(id);
            Assert.That(session.GetCardMaximumUpgradeLevel(index), Is.Zero);
            Assert.That(session.HasBattleCardUpgradeBenefit(index), Is.False);
            Assert.That(session.GetBattleCardAtLevel(index, 3).UpgradeLevel, Is.Zero);
            decimal gold = session.ProfileGold;
            Assert.That(session.GetCardUpgradeCost(index), Is.Zero);
            Assert.That(session.TryUpgradeBattleCard(index, out _), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
        }

        [TestCase("card_approach")]
        [TestCase("card_parallel_course")]
        public void PureActionPointCards_HaveOnePurchasableRankAndRejectFurtherCharges(string id)
        {
            CoreVoyageSession session = RichPort();
            int index = Find(id);
            Assert.That(session.GetCardMaximumUpgradeLevel(index), Is.EqualTo(1));
            Assert.That(session.HasBattleCardUpgradeBenefit(index), Is.True);
            Assert.That(session.GetBattleCardAtLevel(index, 0).NavalActionPointGain, Is.EqualTo(1));
            CoreBattleCard preview = session.GetBattleCardAtLevel(index, 3);
            Assert.That(preview.UpgradeLevel, Is.EqualTo(1));
            Assert.That(preview.NavalActionPointGain, Is.EqualTo(2));
            Assert.That(preview.Energy, Is.Zero);
            decimal gold = session.ProfileGold;
            int cost = session.GetCardUpgradeCost(index);
            Assert.That(cost, Is.GreaterThan(0));
            Assert.That(session.TryUpgradeBattleCard(index, out string reason), Is.True, reason);
            Assert.That(session.ProfileGold, Is.EqualTo(gold - cost));
            Assert.That(session.GetCardUpgradeLevel(id), Is.EqualTo(1));
            Assert.That(session.GetBattleCard(index).NavalActionPointGain, Is.EqualTo(2));
            Assert.That(session.GetCardUpgradeCost(index), Is.Zero);
            Assert.That(session.TryUpgradeBattleCard(index, out _), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(gold - cost));
        }

        [TestCase(0, 1, 1)] [TestCase(1, 1, 2)] [TestCase(2, 2, 2)] [TestCase(3, 2, 2)]
        public void DesperateBloodUtility_GrowsDrawThenRecoveryWithoutChangingUseLimit(int rank, int recovery, int draw)
        {
            CoreVoyageSession session = Boarding("blood_14", rank);
            int index = Find("card_boarding_blood_14");
            session.Battle.PlayerBoardingHealth = 400;
            Assert.That(session.GetCardMaximumUpgradeLevel(index), Is.EqualTo(2));
            Assert.That(CoreBattleUpgradeBalance.EnergyRecovery(session.GetBattleCard(index)), Is.EqualTo(recovery));
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.Energy, Is.EqualTo(10 + recovery));
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(draw));
            session.Battle.Hand.Clear();
            session.Battle.Hand.Add(index);
            Assert.That(session.PlayCard(0), Is.False, "The upgrade does not remove the once-per-battle condition.");
        }

        [TestCase("card_boarding_basic_09", 1)]
        [TestCase("card_boarding_basic_08", 2)]
        [TestCase("card_boarding_basic_11", 3)]
        public void Purchases_StopAtThatCardsMaximumWithoutChargingAgain(string id, int maximum)
        {
            CoreVoyageSession session = RichPort();
            int index = Find(id);
            decimal gold = session.ProfileGold;
            for (int rank = 1; rank <= maximum; rank++)
            {
                int cost = session.GetCardUpgradeCost(index);
                Assert.That(cost, Is.GreaterThan(0));
                Assert.That(session.TryUpgradeBattleCard(index, out string reason), Is.True, reason);
                gold -= cost;
                Assert.That(session.ProfileGold, Is.EqualTo(gold));
                Assert.That(session.GetCardUpgradeLevel(id), Is.EqualTo(rank));
            }
            Assert.That(session.GetCardUpgradeCost(index), Is.Zero);
            Assert.That(session.TryUpgradeBattleCard(index, out _), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(session.CardUpgradeLevels[id], Is.EqualTo(maximum));
        }

        [TestCase("card_boarding_basic_09", 1)]
        [TestCase("card_boarding_basic_08", 2)]
        [TestCase("card_boarding_arcane_03", 0)]
        [TestCase("card_approach", 1)]
        public void HistoricalPurchasedRank_IsRetainedWhileEffectiveRankIsCapped(string id, int effectiveRank)
        {
            var session = new CoreVoyageSession();
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { id, 3 } });
            Assert.That(session.CardUpgradeLevels[id], Is.EqualTo(3));
            Assert.That(session.GetCardUpgradeLevel(id), Is.EqualTo(effectiveRank));
            Assert.That(session.GetBattleCard(Find(id)).UpgradeLevel, Is.EqualTo(effectiveRank));
            session.ConfigureCardUpgrades(session.CardUpgradeLevels);
            Assert.That(session.CardUpgradeLevels[id], Is.EqualTo(3), "Round trips preserve already purchased progress.");
            Assert.That(session.GetCardUpgradeLevel(id), Is.EqualTo(effectiveRank));
        }

        [Test]
        public void SharedDrawAlias_UsesTheLargestStageMaximumAndCapsEachStagesEffect()
        {
            const string id = "card_tactical_draw";
            int index = Find(id);
            var session = new CoreVoyageSession();
            Assert.That(session.GetCardMaximumUpgradeLevel(index), Is.EqualTo(2));
            Assert.That(session.GetCardMaximumUpgradeLevel(id), Is.EqualTo(2));
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { id, 2 } });
            CoreBattleCard naval = session.GetBattleCard(index, NavalBattleStage.Naval);
            CoreBattleCard boarding = session.GetBattleCard(index, NavalBattleStage.Boarding);
            Assert.That(naval.Energy, Is.EqualTo(1));
            Assert.That(naval.Draw, Is.EqualTo(4));
            Assert.That(boarding.Energy, Is.EqualTo(2));
            Assert.That(boarding.Draw, Is.EqualTo(5));
            Assert.That(boarding.UpgradeLevel, Is.EqualTo(2));
            Assert.That(session.GetCardUpgradeLevel(id), Is.EqualTo(2));
            Assert.That(session.CardUpgradeLevels[id], Is.EqualTo(2));
        }

        [TestCase("card_energy_order", "basic_09", 1)]
        [TestCase("card_crew_rally", "basic_12", 2)]
        public void BoardingAliases_KeepStableIdentityAndUseCanonicalEffectCaps(string id, string key, int maximum)
        {
            var session = new CoreVoyageSession();
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { id, 3 } });
            CoreBattleCard card = session.GetBattleCard(Find(id), NavalBattleStage.Boarding);
            Assert.That(card.Id, Is.EqualTo(id));
            Assert.That(card.BoardingSpec.Key, Is.EqualTo(key));
            Assert.That(session.GetCardMaximumUpgradeLevel(id), Is.EqualTo(maximum));
            Assert.That(card.UpgradeLevel, Is.EqualTo(maximum));
        }

        [TestCase("card_special_instrument_lunar_astrolabe", 0, 0, 120)]
        [TestCase("card_special_instrument_lunar_astrolabe", 119, 0, 120)]
        [TestCase("card_special_instrument_lunar_astrolabe", 120, 1, 360)]
        [TestCase("card_special_instrument_lunar_astrolabe", 840, 2, 0)]
        [TestCase("card_special_instrument_last_horizon_lens", 120, 1, 360)]
        [TestCase("card_special_instrument_last_horizon_lens", 360, 2, 0)]
        [TestCase("card_special_instrument_last_horizon_lens", 840, 2, 0)]
        [TestCase("card_special_crew_twilight_tide_carpenter_orlan", 0, 0, 0)]
        [TestCase("card_special_crew_twilight_tide_carpenter_orlan", 840, 0, 0)]
        public void SourceExperience_UsesCardSpecificThresholdsWithoutDiscardingEarnedXp(string id, int experience, int level, int nextThreshold)
        {
            var session = new CoreVoyageSession();
            session.RestoreSpecialCardExperience(new Dictionary<string, int> { { id, experience } });
            Assert.That(session.GetSpecialCardExperience(id), Is.EqualTo(experience));
            Assert.That(session.GetCardUpgradeLevel(id), Is.EqualTo(level));
            Assert.That(session.GetSpecialCardNextThreshold(id), Is.EqualTo(nextThreshold));
            Assert.That(session.GetBattleCard(Find(id)).UpgradeLevel, Is.EqualTo(level));
            session.RestoreSpecialCardExperience(session.SpecialCardExperience);
            Assert.That(session.GetSpecialCardExperience(id), Is.EqualTo(experience));
            Assert.That(session.GetCardUpgradeCost(Find(id)), Is.Zero, "Source cards still progress through their own experience.");
        }

        [Test]
        public void HistoricalSpecialPaidRank_PreservesFullMigratedXpForRedesignedMixedCard()
        {
            const string id = "card_special_instrument_lunar_astrolabe";
            var session = new CoreVoyageSession();
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { id, 3 } });
            Assert.That(session.GetSpecialCardExperience(id), Is.EqualTo(840));
            Assert.That(session.GetCardUpgradeLevel(id), Is.EqualTo(2));
            Assert.That(session.GetSpecialCardNextThreshold(id), Is.Zero);
            Assert.That(session.CardUpgradeLevels.ContainsKey(id), Is.False);
        }

        private static CoreVoyageSession RichPort()
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(10000, session.CurrentPort);
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            return session;
        }

        private static CoreVoyageSession Boarding(string key, int rank)
        {
            var session = RichPort();
            string id = "card_boarding_" + key;
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { id, rank } });
            session.SetSeed("CARD-UPGRADE-LIMIT");
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(session.EndBattleTurn(), Is.True);
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 1000;
            session.Battle.DistanceMeters = 100f;
            typeof(CoreBattleState).GetProperty("EnemyHeadingDegrees").SetValue(session.Battle, 90f);
            typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(session.Battle, session.Battle.EnemyHeadingDegrees);
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(session.TryBoard(), Is.True);
            session.Battle.EnemyBoardingHealth = session.Battle.EnemyMaxBoardingHealth = 1000;
            session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth = 1000;
            session.Battle.EnemyBlock = 0;
            session.Battle.Energy = 10;
            session.Cargo.Powder = 20;
            session.Cargo.Medicine = 20;
            typeof(CoreVoyageSession).GetProperty("Morale").SetValue(session, 0);
            session.Battle.Hand.Clear();
            session.Battle.Hand.Add(Find(id));
            session.Battle.DrawPile.Clear();
            session.Battle.DrawPile.AddRange(Enumerable.Repeat(Find("card_boarding_basic_01"), 20));
            session.Battle.DiscardPile.Clear();
            return session;
        }

        private static int Find(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == id);
    }
}
