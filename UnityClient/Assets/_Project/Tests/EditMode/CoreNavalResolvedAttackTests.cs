using System;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreNavalResolvedAttackTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase(20, 0, 100)]
        [TestCase(20, 7, 100)]
        [TestCase(20, 100, 100)]
        [TestCase(0, 0, 100)]
        [TestCase(1000, 0, 5)]
        public void ActualCannonContact_RecordsExactDamageGuardZeroAndLethalResults(int rawAttack, int guard, int health)
        {
            CoreVoyageSession session = NewBattle();
            CoreBattleState battle = session.Battle;
            SetHull(session, health);
            battle.PlayerBlock = guard;
            int armored = CoreNavalStatRules.ReduceIncomingCannonDamage(rawAttack, session.EffectiveArmor);
            ExecutePlan(session, "Cannon", rawAttack);

            CoreNavalResolvedAttack result = battle.LastNavalAttack;
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Executed, Is.True, "An executed zero-damage cannon still has a contact fact.");
            Assert.That(result.HealthBefore, Is.EqualTo(health));
            Assert.That(result.GuardBefore, Is.EqualTo(guard));
            Assert.That(result.GuardAbsorbed, Is.EqualTo(Math.Min(guard, armored)));
            Assert.That(result.HealthLost, Is.EqualTo(Math.Min(health, Math.Max(0, armored - guard))));
            Assert.That(result.HealthAfter, Is.EqualTo(session.Hull));
            Assert.That(result.GuardAfter, Is.EqualTo(battle.PlayerBlock));
        }

        [Test]
        public void EndTurnBurn_IsExcludedFromCannonContact()
        {
            CoreVoyageSession session = NewBattle();
            SetHull(session, 1000);
            session.Battle.PlayerBurnStacks = 5;
            Assert.That(session.EndBattleTurn(), Is.True);
            CoreNavalResolvedAttack result = session.Battle.LastNavalAttack;
            Assert.That(result, Is.Not.Null);
            Assert.That(result.HealthBefore, Is.EqualTo(995), "Burn resolves before the enemy gets its cannon action.");
            Assert.That(result.HealthAfter, Is.EqualTo(session.Hull));
            Assert.That(1000 - session.Hull, Is.EqualTo(result.HealthLost + 5));
        }

        [Test]
        public void GuardFact_ExcludesBurnAbsorptionAndNewTurnExpiry()
        {
            CoreVoyageSession session = NewBattle();
            session.Battle.PlayerBlock = 100;
            session.Battle.PlayerBurnStacks = 4;
            Assert.That(session.EndBattleTurn(), Is.True);
            CoreNavalResolvedAttack result = session.Battle.LastNavalAttack;
            Assert.That(result, Is.Not.Null);
            Assert.That(result.GuardBefore, Is.EqualTo(96));
            Assert.That(result.GuardAfter, Is.GreaterThan(0), "The actual contact preserves guard left over after that cannon.");
            Assert.That(result.HealthLost, Is.Zero);
            Assert.That(session.Battle.PlayerBlock, Is.Zero, "Subsequent turn reset is not included in the contact fact.");
        }

        [Test]
        public void LethalPlayerEndBurn_ClearsPreviousContactAndDoesNotInventEnemyFire()
        {
            CoreVoyageSession session = NewBattle();
            CoreBattleState battle = session.Battle;
            ExecutePlan(session, "Cannon", 1);
            Assert.That(battle.LastNavalAttack, Is.Not.Null);
            SetHull(session, 3);
            battle.PlayerBurnStacks = 5;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(battle.LastNavalAttack, Is.Null);
            Assert.That(session.Hull, Is.Zero);
        }

        [Test]
        public void OutOfRangeCannon_ClearsPreviousContactEvenWhenBurnStillDamagesPlayer()
        {
            CoreVoyageSession session = NewBattle();
            CoreBattleState battle = session.Battle;
            ExecutePlan(session, "Cannon", 1);
            Assert.That(battle.LastNavalAttack, Is.Not.Null);
            SetPose(battle, false, 500f);
            SetHull(session, 1000);
            battle.PlayerBurnStacks = 3;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(battle.LastNavalAttack, Is.Null, "An announced or attempted out-of-range cannon never fired.");
            Assert.That(session.Hull, Is.EqualTo(997));
        }

        [Test]
        public void GuardActionAndDeadEnemy_DoNotCreateCannonFacts()
        {
            CoreVoyageSession session = NewBattle();
            ExecutePlan(session, "Guard", 20);
            Assert.That(session.Battle.LastNavalAttack, Is.Null);
            CoreBattleState battle = session.Battle;
            battle.EnemyHull = 0;
            ExecutePlan(session, "Cannon", 20);
            Assert.That(battle.LastNavalAttack, Is.Null);
        }

        [Test]
        public void ReadOnlyIntentPreview_DoesNotCreateOrRewriteAContact()
        {
            CoreVoyageSession session = NewBattle();
            for (int i = 0; i < 4; i++) session.GetEnemyIntent();
            Assert.That(session.Battle.LastNavalAttack, Is.Null);
            ExecutePlan(session, "Cannon", 20);
            CoreNavalResolvedAttack recorded = session.Battle.LastNavalAttack;
            for (int i = 0; i < 4; i++) session.GetEnemyIntent();
            Assert.That(session.Battle.LastNavalAttack, Is.SameAs(recorded));
        }

        private static CoreVoyageSession NewBattle()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[4]), Is.True);
            CoreBattleState battle = session.Battle;
            battle.Stage = NavalBattleStage.Naval;
            battle.EnemyHull = battle.EnemyMaxHull = 10000;
            battle.PlayerBlock = battle.EnemyBlock = 0;
            SetHull(session, 1000);
            SetPose(battle, true, 0f);
            SetPose(battle, false, 100f);
            return session;
        }

        private static void SetHull(CoreVoyageSession session, int value) =>
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, value);

        private static void SetPose(CoreBattleState battle, bool player, float x) =>
            typeof(CoreBattleState).GetMethod("SetTacticalPose", PrivateInstance)
                .Invoke(battle, new object[] { player, new VoyagePoint(x, 0f), 90f });

        private static void ExecutePlan(CoreVoyageSession session, string action, int attack)
        {
            Type planType = typeof(CoreVoyageSession).GetNestedType("EnemyBattlePlan", BindingFlags.NonPublic);
            Type actionType = typeof(CoreVoyageSession).GetNestedType("EnemyBattleAction", BindingFlags.NonPublic);
            object plan = Activator.CreateInstance(planType);
            planType.GetField("Action").SetValue(plan, Enum.Parse(actionType, action));
            planType.GetField("Attack").SetValue(plan, attack);
            // Use the real execution branch, with no movement or boss modifiers. The
            // arbitrary text proves recording does not depend on localized intent text.
            typeof(CoreVoyageSession).GetMethod("ExecuteEnemyPlan", PrivateInstance)
                .Invoke(session, new[] { plan, (object)"Arbitrary translated intent" });
        }
    }
}
