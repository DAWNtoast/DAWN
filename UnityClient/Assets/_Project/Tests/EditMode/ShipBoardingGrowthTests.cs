using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class ShipBoardingGrowthTests
    {
        [TestCase(-1, 100)] [TestCase(0, 100)] [TestCase(7, 100)]
        [TestCase(14, 100)] [TestCase(28, 100)] [TestCase(56, 100)]
        public void BoardingCompatibilityScaleIsAlwaysOne(int ordinary, int percent)
        {
            Assert.That(CoreBattleCardEffects.BoardingScaleFor(ordinary), Is.EqualTo(percent / 100m));
        }

        [Test]
        public void EveryStageUsesAuthoredValuesWithoutACrewMultiplier()
        {
            CoreVoyageSession session = Port(14, 0);
            session.Cargo.Medicine = session.Cargo.Wood = 1;
            var card = new CoreBattleCard("rounding", "取整", "测试", 1, 7, 9, 11, 2, 0, "测试");
            CoreBattleCardEffects effects = session.GetBattleCardEffects(card, NavalBattleStage.Boarding);
            Assert.That(effects.Damage, Is.EqualTo(7));
            Assert.That(effects.Block, Is.EqualTo(9));
            Assert.That(effects.Repair, Is.EqualTo(11));
            foreach (NavalBattleStage stage in new[] { NavalBattleStage.Preparation, NavalBattleStage.Naval })
            {
                CoreBattleCardEffects unchanged = session.GetBattleCardEffects(card, stage);
                Assert.That(unchanged.Damage, Is.EqualTo(7));
                Assert.That(unchanged.Block, Is.EqualTo(9));
                Assert.That(unchanged.Repair, Is.EqualTo(11));
            }
            Assert.That(session.GetBattleCard(14).Damage, Is.EqualTo(12), "Rank two scales the authored eight damage times 1.5 once.");
            Assert.That(session.GetBattleCardEffects(14, NavalBattleStage.Boarding).Damage, Is.EqualTo(12), "Boarding uses the upgraded value without another multiplier.");
            Assert.That(session.GetBattleCard(14).Energy, Is.EqualTo(1), "The original fee is preserved.");
        }

        [Test]
        public void NeitherExpertsNorTheLiveOrdinaryCrewSnapshotChangeCardValues()
        {
            CoreVoyageSession sailors = Port(14, 0), experts = Port(14, 6);
            Assert.That(experts.GetBattleCardEffects(14, NavalBattleStage.Boarding).Damage,
                Is.EqualTo(sailors.GetBattleCardEffects(14, NavalBattleStage.Boarding).Damage));
            StartBattle(experts);
            typeof(CoreBattleState).GetProperty(nameof(CoreBattleState.PlayerOrdinaryCrew)).SetValue(experts.Battle, 28);
            Assert.That(experts.SelectedOrdinaryCrew, Is.EqualTo(14));
            Assert.That(experts.GetBattleCardEffects(14, NavalBattleStage.Boarding).Damage, Is.EqualTo(experts.GetBattleCard(14).Damage));
        }

        [Test]
        public void RealBoardingResolutionDescriptionAndEscalatingFeesUseTheSameEffects()
        {
            CoreVoyageSession session = Port(14, 0);
            session.Cargo.Medicine = 1;
            StartBattle(session);
            session.Battle.Stage = NavalBattleStage.Boarding;
            session.Battle.EnemyBoardingHealth = 200;
            session.Battle.PlayerBoardingHealth = 10;
            session.Battle.Energy = 10;
            int damage = 12; // Base 8 × rank-two 1.5, irrespective of 14 sailors.
            int guard = 6; // Base 4 × rank-two 1.5.
            int repair = 9; // Base 6 × rank-two 1.5; medicine restores the full value.
            session.Battle.Hand.Clear(); session.Battle.Hand.AddRange(new[] { 14, 15, 17 });
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(200 - damage));
            Assert.That(session.GetBattleCardEnergyCost(14), Is.EqualTo(2));
            Assert.That(session.Battle.LastAction, Does.Contain("伤害 " + damage));
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(guard));
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(10 + repair));
            Assert.That(session.Battle.LastAction, Does.Contain("恢复 " + repair));
            Assert.That(session.BattleCardEffectDescription(session.GetBattleCard(14), NavalBattleStage.Boarding), Does.Contain("伤害 " + damage));
            Assert.That(session.BattleCardDescription(session.GetBattleCard(14)), Does.Contain("伤害 " + damage).And.Not.Contain("50%"));
            Assert.That(session.BattleCardPortDescription(session.GetBattleCard(14)), Does.Contain("伤害 " + damage).And.Not.Contain("28 名普通水手"));
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

        [Test]
        public void EncounterHealthScalesOnceWithoutChangingPlayerHealthOrCrewCounts()
        {
            CoreVoyageSession session = Port(14, 0);
            StartBattle(session);
            Assert.That(session.Battle.EnemyMaxHull, Is.EqualTo(345));
            Assert.That(session.Battle.EnemyMaxBoardingHealth, Is.EqualTo(162));
            Assert.That(session.Battle.EnemyOrdinaryCrew, Is.EqualTo(22));
            Assert.That(session.Battle.PlayerMaxBoardingHealth, Is.EqualTo(76));
            Assert.That(session.Battle.EnemyMaxHull, Is.EqualTo(session.Battle.Profile.EnemyHull));
            Assert.That(session.Battle.EnemyMaxBoardingHealth, Is.EqualTo(session.Battle.Profile.BoardingHealth));
        }

        [Test]
        public void NavalPreviewAndResolutionUseAuthoredShotFirepowerAndLoadBeforeGuard()
        {
            var session = new CoreVoyageSession();
            session.ConfigureDepartureCrew(7, 0);
            session.ConfigureComponentBonuses(0, 0, 10, 0, 0);
            StartBattle(session);
            Assert.That(session.EffectiveFirepower, Is.EqualTo(16));
            session.Battle.Stage = NavalBattleStage.Naval;
            session.Battle.DistanceMeters = 200;
            session.Battle.PlayerLoadedShot = 4;
            session.Battle.EnemyHull = 200;
            session.Battle.EnemyBlock = 5;
            session.Battle.Hand.Clear(); session.Battle.Hand.Add(0);
            session.Cargo.Powder = session.Cargo.Cannonballs = 1;
            Assert.That(session.BattleCardDescription(session.GetBattleCard(0)), Does.Contain("当前炮击 18"));
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(187), "10 card + 4 firepower + 4 loading - 5 guard.");
            Assert.That(session.Battle.PlayerLoadedShot, Is.Zero);
            Assert.That(session.Cargo.Powder, Is.Zero);
            Assert.That(session.Cargo.Cannonballs, Is.Zero);
        }

        [Test]
        public void DamagePreviewSaturatesAndNonScriptTutorialReplayUsesNormalScaling()
        {
            var session = new CoreVoyageSession();
            var large = new CoreBattleCard("large", "大值", "测试", 1, int.MaxValue, 0, 0, 0, 0, "{damage}");
            Assert.That(session.GetBattleCardEffects(large, NavalBattleStage.Naval).Damage, Is.EqualTo(int.MaxValue));
            Assert.That(session.BattleCardEffectDescription(large, NavalBattleStage.Naval), Does.Contain(int.MaxValue.ToString()));
            session.StartVoyage();
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { new CoreEncounter(CoreGuidedTutorialContent.BossEventId,
                    "普通重访", "海战", "非教学", true, false, 1, 70), true });
            Assert.That(session.IsGuidedBossBattle, Is.False);
            Assert.That(session.Battle.EnemyMaxHull, Is.EqualTo(78));
            Assert.That(session.Battle.EnemyMaxBoardingHealth, Is.EqualTo(42));
            Assert.That(session.GetBattleCardEffects(session.GetBattleCard(0), NavalBattleStage.Naval).Damage,
                Is.EqualTo(session.GetBattleCard(0).Damage));
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
