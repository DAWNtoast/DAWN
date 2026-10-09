using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class ShipBoardingGrowthTests
    {
        [TestCase(-1, 30)] [TestCase(0, 30)] [TestCase(7, 30)]
        [TestCase(14, 50)] [TestCase(28, 100)] [TestCase(56, 160)]
        public void BoardingScaleHasBoundedOrdinarySailorGrowth(int ordinary, int percent)
        {
            Assert.That(CoreBattleCardEffects.BoardingScaleFor(ordinary), Is.EqualTo(percent / 100m));
        }

        [Test]
        public void HalfManningRoundsAfterUpgradeAndDoesNotChangeNavalOrPreparationCards()
        {
            CoreVoyageSession session = Port(14, 0);
            var card = new CoreBattleCard("rounding", "取整", "测试", 1, 7, 9, 11, 2, 0, "测试");
            CoreBattleCardEffects effects = session.GetBattleCardEffects(card, NavalBattleStage.Boarding);
            Assert.That(effects.Damage, Is.EqualTo(4));
            Assert.That(effects.Block, Is.EqualTo(5));
            Assert.That(effects.Repair, Is.EqualTo(6));
            foreach (NavalBattleStage stage in new[] { NavalBattleStage.Preparation, NavalBattleStage.Naval })
            {
                CoreBattleCardEffects unchanged = session.GetBattleCardEffects(card, stage);
                Assert.That(unchanged.Damage, Is.EqualTo(7));
                Assert.That(unchanged.Block, Is.EqualTo(9));
                Assert.That(unchanged.Repair, Is.EqualTo(11));
            }
            Assert.That(session.GetBattleCard(14).Damage, Is.EqualTo(18));
            Assert.That(session.GetBattleCardEffects(14, NavalBattleStage.Boarding).Damage, Is.EqualTo(9));
            Assert.That(session.GetBattleCard(14).Energy, Is.Zero, "Ship scaling never undoes purchased fee reductions.");
        }

        [Test]
        public void ExpertsDoNotIncreaseEffectsAndLiveCombatUsesTheDepartureSnapshot()
        {
            CoreVoyageSession sailors = Port(14, 0), experts = Port(14, 6);
            Assert.That(experts.GetBattleCardEffects(14, NavalBattleStage.Boarding).Damage,
                Is.EqualTo(sailors.GetBattleCardEffects(14, NavalBattleStage.Boarding).Damage));
            StartBattle(experts);
            typeof(CoreBattleState).GetProperty(nameof(CoreBattleState.PlayerOrdinaryCrew)).SetValue(experts.Battle, 28);
            Assert.That(experts.SelectedOrdinaryCrew, Is.EqualTo(14));
            Assert.That(experts.GetBattleCardEffects(14, NavalBattleStage.Boarding).Damage, Is.EqualTo(18));
        }

        [Test]
        public void RealBoardingResolutionDescriptionAndEscalatingFeesUseTheSameEffects()
        {
            CoreVoyageSession session = Port(14, 0);
            StartBattle(session);
            session.Battle.Stage = NavalBattleStage.Boarding;
            session.Battle.EnemyBoardingHealth = 200;
            session.Battle.PlayerBoardingHealth = 10;
            session.Battle.Energy = 10;
            session.Battle.Hand.Clear(); session.Battle.Hand.AddRange(new[] { 14, 15, 17 });
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(191));
            Assert.That(session.GetBattleCardEnergyCost(14), Is.EqualTo(1));
            Assert.That(session.Battle.LastAction, Does.Contain("伤害 9"));
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(9));
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(18));
            Assert.That(session.Battle.LastAction, Does.Contain("恢复 8"));
            Assert.That(session.BattleCardEffectDescription(session.GetBattleCard(14), NavalBattleStage.Boarding), Does.Contain("伤害 9"));
            Assert.That(session.BattleCardDescription(session.GetBattleCard(14)), Does.Contain("14 名普通水手").And.Contain("50%"));
            Assert.That(session.BattleCardPortDescription(session.GetBattleCard(14)), Does.Contain("基础（28 名普通水手）").And.Contain("当前 14 人"));
        }

        [Test]
        public void EnemyMeleeKeepsItsAuthoredDamageRegardlessOfPlayerManning()
        {
            foreach (int ordinary in new[] { 7, 28 })
            {
                CoreVoyageSession session = Port(ordinary, 0);
                StartBattle(session);
                session.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced, 500, 1, 7, 0, 0, 20, 1f, 1f);
                session.Battle.Stage = NavalBattleStage.Boarding;
                session.Battle.PlayerBlock = 0;
                int before = session.Battle.PlayerBoardingHealth;
                Assert.That(session.EndBattleTurn(), Is.True);
                Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(before - 7));
            }
        }

        private static CoreVoyageSession Port(int ordinary, int specialists)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.TryDepositAllPortCargo(), Is.True);
            Assert.That(session.SelectShip(7), Is.True);
            session.ConfigureDepartureCrew(ordinary, specialists);
            session.ConfigureCardUpgrades(new Dictionary<string, int>
            { { "card_crew_thrust", 2 }, { "card_crew_guard", 2 }, { "card_crew_bandage", 2 } });
            return session;
        }

        private static void StartBattle(CoreVoyageSession session)
        {
            session.SetSeed("SHIP-BOARDING-GROWTH"); session.StartVoyage();
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { new CoreEncounter("ship_growth_fixture", "成长夹具", "海战", "隔离战斗", true, false, 1, 500), false });
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
        }
    }
}
