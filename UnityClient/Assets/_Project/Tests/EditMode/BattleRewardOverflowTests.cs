using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class BattleRewardOverflowTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void FixedVictoryRewardAtIntegerLimitPreservesBalanceAndEndsExactlyOnce(bool boss)
        {
            CoreVoyageSession session = Battle(boss);
            session.Cargo.Gold = int.MaxValue - 3;
            session.Cargo.Fame = int.MaxValue - 2;
            session.Battle.EnemyHull = 0;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.Battle, Is.Null);
            Assert.That(session.Cargo.Gold, Is.EqualTo(int.MaxValue));
            Assert.That(session.Cargo.Fame, Is.EqualTo(int.MaxValue));
            Assert.That(session.LogEntries.Any(line => line.Contains("金币 97 未能入账")), Is.True);
            Assert.That(session.LogEntries.Any(line => line.Contains("声望 " + ((boss ? 18 : 6) - 2) + " 未能入账")), Is.True);
            Assert.That(session.LogEntries.Any(line => line.Contains("金币 +3、声望 +2")), Is.True,
                "The clamped victory receipt remains present even when a fragment-drop entry follows it.");
            Assert.That(session.EndBattleTurn(), Is.False);
            Assert.That(session.PlayCard(0), Is.False);
            Assert.That(session.Cargo.Gold, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void PendingRewardsCannotWrapCurrencyAndKeepDiverConversionInWideArithmetic()
        {
            CoreVoyageSession session = Battle(true, true);
            foreach (PropertyInfo property in ResourceProperties()) property.SetValue(session.Cargo, int.MaxValue - 2);
            var reward = new VoyageEffect();
            // Currency and status still saturate; every physical resource is sold
            // before loading, even when its existing inventory is nearly full.
            foreach (PropertyInfo property in ResourceProperties())
                typeof(VoyageEffect).GetProperty(property.Name).SetValue(reward, 5);
            reward.Wood = reward.Gold = reward.Fame = int.MaxValue;
            SetReward(session, reward);
            session.Battle.EnemyHull = 0;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            foreach (PropertyInfo property in ResourceProperties())
                Assert.That(property.GetValue(session.Cargo), Is.EqualTo(
                    property.Name == "Gold" || property.Name == "Fame" || property.Name == "Curse"
                        ? int.MaxValue : int.MaxValue - 2), property.Name);
            long uncreditedGold = CoreBattleRewardBalance.GoldReward(true, reward, false, diver: true) - 2;
            Assert.That(uncreditedGold, Is.GreaterThan(int.MaxValue));
            Assert.That(session.LogEntries.Any(line => line.Contains("金币 " + uncreditedGold + " 未能入账")), Is.True);
            Assert.That(session.LogEntries.Any(line => line.Contains("声望 2147483663 未能入账")), Is.True);
            Assert.That(session.LogEntries.Any(line => line.Contains("木材 ") && line.Contains("未能入账")), Is.False);
            Assert.That(session.LogEntries.Any(line => line.Contains("宝石 ") && line.Contains("未能入账")), Is.False);
            Assert.That(session.EndBattleTurn(), Is.False);
        }

        [Test]
        public void OrdinaryPendingRewardsConvertMaterialsAndIncludeDiverBonus()
        {
            CoreVoyageSession session = Battle(true, true);
            int gold = session.Cargo.Gold, fame = session.Cargo.Fame;
            int wood = session.Cargo.Wood, iron = session.Cargo.Iron;
            SetReward(session, new VoyageEffect { Gold = 180, Fame = 20, Wood = 8, Iron = 3 });
            session.Battle.EnemyHull = 0;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Cargo.Gold, Is.EqualTo(gold + 400)); // 120 + 180 + 10*5 + 4*10 = 390.
            Assert.That(session.Cargo.Fame, Is.EqualTo(fame + 38));
            Assert.That(session.Cargo.Wood, Is.EqualTo(wood));
            Assert.That(session.Cargo.Iron, Is.EqualTo(iron));
            Assert.That(session.LogEntries.Any(line => line.Contains("未能入账")), Is.False);
        }


        [Test]
        public void BoardingVictoryDoublesRoundedGoldAndFameWithoutLoadingPhysicalLoot()
        {
            CoreVoyageSession session = Battle(false);
            int gold = session.Cargo.Gold;
            int fame = session.Cargo.Fame;
            int water = session.Cargo.Water;
            int wood = session.Cargo.Wood;
            int iron = session.Cargo.Iron;
            SetReward(session, new VoyageEffect { Gold = 20, Fame = 4, Water = 2, Wood = 8, Iron = 2,
                Gem = 1, AncientMaterial = 3, Relic = 1, BlackPearl = 1 });

            session.Battle.Stage = NavalBattleStage.Boarding;
            session.Battle.EnemyBoardingHealth = 0;
            Assert.That(session.EndBattleTurn(), Is.True);

            Assert.That(session.LastBattleWasCaptured, Is.True);
            Assert.That(session.Cargo.Gold - gold, Is.EqualTo(800)); // round(429.064 / 100) * 100 * 2.
            Assert.That(session.Cargo.Fame - fame, Is.EqualTo(20));
            Assert.That(session.Cargo.Water, Is.EqualTo(water));
            Assert.That(session.Cargo.Wood, Is.EqualTo(wood));
            Assert.That(session.Cargo.Iron, Is.EqualTo(iron));
            Assert.That(session.Cargo.Gem, Is.Zero);
            Assert.That(session.Cargo.AncientMaterial, Is.Zero);
            Assert.That(session.Cargo.Relic, Is.Zero);
            Assert.That(session.Cargo.BlackPearl, Is.Zero);
            Assert.That(session.LogEntries.Any(line => line.Contains("夺船胜利") && line.Contains("夺船奖励×2")), Is.True);
        }
        private static PropertyInfo[] ResourceProperties() => typeof(VoyageCargo).GetProperties()

            .Where(property => property.CanWrite && property.PropertyType == typeof(int)).ToArray();

        private static CoreVoyageSession Battle(bool boss, bool diver = false)
        {
            CoreVoyageSession session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(false, false, false, false, false, diver);
            session.StartVoyage();
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session,
                new object[] { new CoreEncounter("overflow-audit", "边界测试敌舰", "海战", "测试", true, false, 1, 70), boss });
            return session;
        }

        private static void SetReward(CoreVoyageSession session, VoyageEffect reward) =>
            typeof(CoreVoyageSession).GetField("pendingBattleReward", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, reward);
    }
}
