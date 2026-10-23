using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBoardingResolvedAttackTests
    {
        [TestCase("arcane_11", "arcane", 36)]
        [TestCase("thunder_05", "thunder", 14)]
        [TestCase("blood_13", "blood", 12)]
        public void NextPlayerTurn_RecordsActualQueuedSourceAndSettlementOnce(string key, string school, int damage)
        {
            CoreVoyageSession session = Battle(key);
            CoreBattleState battle = session.Battle;
            int health = battle.EnemyBoardingHealth;
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(health));
            Assert.That(battle.Boarding.ResolvedAttacks, Is.Empty, "Queueing is not an attack contact.");
            Assert.That(battle.Boarding.DelayedDamage.Single().SourceSchoolId, Is.EqualTo(school));
            Assert.That(battle.Boarding.DelayedDamage.Single().SourceCardId, Is.EqualTo("card_boarding_" + key));
            Assert.That(session.EndBattleTurn(), Is.True);
            CoreBoardingResolvedAttack result = battle.Boarding.ResolvedAttacks.Single();
            Assert.That(result.Sequence, Is.EqualTo(1));
            Assert.That(result.SourceSchoolId, Is.EqualTo(school));
            Assert.That(result.SourceCardId, Is.EqualTo("card_boarding_" + key));
            Assert.That(result.HealthLost + result.GuardAbsorbed, Is.EqualTo(damage));
            Assert.That(result.HealthAfter, Is.EqualTo(battle.EnemyBoardingHealth));
            Assert.That(result.GuardAfter, Is.EqualTo(battle.EnemyBlock));
            Assert.That(battle.Boarding.DelayedDamage, Is.Empty);
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(battle.Boarding.ResolvedAttackSequence, Is.EqualTo(1), "An expired queue cannot settle twice.");
        }

        [TestCase("thunder_05", "thunder", 14)]
        [TestCase("blood_13", "blood", 12)]
        [TestCase("basic_13", "", 12)]
        public void EarlyResolution_UsesOriginalSchoolAndRealGuardLoss_NotResolverSchool(string key, string school, int damage)
        {
            CoreVoyageSession session = Battle(key, "arcane_03", "arcane_15");
            CoreBattleState battle = session.Battle;
            battle.EnemyBlock = 100;
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.PlayCard(0), Is.True);
            CoreBattleCard resolver = session.GetBattleCard(battle.Hand[0]);
            Assert.That(session.PlayCard(0), Is.True);
            CoreBoardingResolvedAttack result = battle.Boarding.ResolvedAttacks.Single();
            Assert.That(result.SourceCardId, Is.EqualTo("card_boarding_" + key));
            Assert.That(result.SourceSchoolId, Is.EqualTo(school));
            Assert.That(result.HealthLost, Is.Zero);
            Assert.That(result.GuardBefore, Is.EqualTo(100));
            Assert.That(result.GuardAfter, Is.EqualTo(100 - damage));
            Assert.That(result.GuardAbsorbed, Is.EqualTo(damage));
            Assert.That(BattleSchoolAttackPresentation.TryPlayer(resolver, true, 0, damage, out _, out _), Is.False);
            bool isSchool = BattleSchoolAttackPresentation.TryResolved(result, out var kind, out var outcome);
            Assert.That(isSchool, Is.EqualTo(school.Length > 0));
            Assert.That(outcome, Is.EqualTo(BattleSchoolAttackOutcome.Guarded));
            if (school == "thunder") Assert.That(kind, Is.EqualTo(BattleSchoolAttackKind.Thunder));
            if (school == "blood") Assert.That(kind, Is.EqualTo(BattleSchoolAttackKind.Blood));
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(battle.Boarding.ResolvedAttackSequence, Is.EqualTo(1));
        }

        [Test]
        public void MultipleDueAttacks_RetainExecutionOrderAndIndividualBeforeAfterValues()
        {
            CoreVoyageSession session = Battle("arcane_11", "thunder_05", "blood_13");
            CoreBattleState battle = session.Battle;
            battle.Energy = 10;
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.EndBattleTurn(), Is.True);
            var facts = battle.Boarding.ResolvedAttacks;
            Assert.That(facts.Select(a => a.SourceSchoolId), Is.EqualTo(new[] { "arcane", "thunder", "blood" }));
            Assert.That(facts.Select(a => a.Sequence), Is.EqualTo(new long[] { 1, 2, 3 }));
            Assert.That(facts.Select(a => a.HealthLost + a.GuardAbsorbed), Is.EqualTo(new[] { 36, 14, 12 }));
            for (int i = 1; i < facts.Count; i++)
            {
                Assert.That(facts[i].HealthBefore, Is.EqualTo(facts[i - 1].HealthAfter));
                Assert.That(facts[i].GuardBefore, Is.EqualTo(facts[i - 1].GuardAfter));
            }
        }

        [Test]
        public void PreviewAndRejectedResolver_DoNotCreateOrConsumeSettlementFacts()
        {
            CoreVoyageSession session = Battle("arcane_15");
            CoreBattleState battle = session.Battle;
            SetMana(session, 3);
            int energy = battle.Energy;
            Assert.That(session.PlayCard(0), Is.False);
            Assert.That(battle.Energy, Is.EqualTo(energy));
            Assert.That(battle.Schools.AvailableMana, Is.EqualTo(3));
            Assert.That(battle.Boarding.ResolvedAttackSequence, Is.Zero);
            battle.Hand.Clear(); battle.Hand.Add(Index("thunder_05"));
            Assert.That(session.PlayCard(0), Is.True);
            battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
                1000, 0, 20, 0, 0, 28, 1, 1, healthMultiplier: 1);
            typeof(CoreBoardingEnemyState).GetProperty("Deck").SetValue(battle.BoardingEnemy, CoreBoardingEnemyCatalog.ForSchool("thunder"));
            for (int i = 0; i < 3; i++) Assert.That(session.GetBoardingEnemyPreview(), Is.Not.Null);
            Assert.That(battle.Boarding.DelayedDamage.Count, Is.EqualTo(1));
            Assert.That(battle.Boarding.ResolvedAttackSequence, Is.Zero);
            Assert.That(session.EndBattleTurn(), Is.True);
            var fact = battle.Boarding.ResolvedAttacks.Single();
            for (int i = 0; i < 3; i++) session.GetBoardingEnemyPreview();
            Assert.That(battle.Boarding.ResolvedAttackSequence, Is.EqualTo(1));
            Assert.That(battle.Boarding.ResolvedAttacks.Single().HealthAfter, Is.EqualTo(fact.HealthAfter));
            string serialized = JsonUtility.ToJson(battle.Boarding);
            Assert.That(serialized, Does.Not.Contain("resolvedAttacks").And.Not.Contain("resolvedAttackSequence"));
            Assert.That(new CoreBoardingBattleState().ResolvedAttacks, Is.Empty);
        }

        [Test]
        public void RepeatedRealTurns_KeepBoundedHistoryWithMonotonicSequences()
        {
            CoreVoyageSession session = Battle("thunder_05");
            CoreBattleState battle = session.Battle;
            battle.PlayerBoardingHealth = battle.PlayerMaxBoardingHealth = 1000000;
            battle.EnemyBoardingHealth = battle.EnemyMaxBoardingHealth = 1000000;
            int turns = CoreBoardingBattleState.ResolvedAttackHistoryLimit + 3;
            for (int i = 0; i < turns; i++)
            {
                battle.Hand.Clear(); battle.DrawPile.Clear(); battle.DiscardPile.Clear();
                battle.Hand.Add(Index("thunder_05"));
                Assert.That(session.PlayCard(0), Is.True);
                Assert.That(session.EndBattleTurn(), Is.True);
            }
            Assert.That(battle.Boarding.ResolvedAttackSequence, Is.EqualTo(turns));
            Assert.That(battle.Boarding.ResolvedAttacks.Count, Is.EqualTo(CoreBoardingBattleState.ResolvedAttackHistoryLimit));
            Assert.That(battle.Boarding.ResolvedAttacks.First().Sequence, Is.EqualTo(4));
            Assert.That(battle.Boarding.ResolvedAttacks.Last().Sequence, Is.EqualTo(turns));
        }

        private static CoreVoyageSession Battle(params string[] keys)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            session.SetSeed("BOARDING-RESOLVED-ATTACKS");
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, 1000);
            Assert.That(session.EndBattleTurn(), Is.True);
            session.Battle.DistanceMeters = 100f;
            typeof(CoreBattleState).GetProperty("EnemyHeadingDegrees").SetValue(session.Battle, 90f);
            typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(session.Battle, session.Battle.EnemyHeadingDegrees);
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(session.TryBoard(), Is.True);
            typeof(CoreVoyageSession).GetProperty("Morale").SetValue(session, 0);
            CoreBattleState battle = session.Battle;
            battle.PlayerBoardingHealth = battle.PlayerMaxBoardingHealth = 1000;
            battle.EnemyBoardingHealth = battle.EnemyMaxBoardingHealth = 10000;
            battle.EnemyHull = battle.EnemyMaxHull = 10000;
            battle.PlayerBlock = battle.EnemyBlock = 0;
            battle.Hand.Clear(); battle.DrawPile.Clear(); battle.DiscardPile.Clear();
            battle.Hand.AddRange(keys.Select(Index));
            battle.Energy = 3;
            SetMana(session, 0);
            return session;
        }
        private static int Index(string key) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(i => CoreVoyageSession.CardLibrary[i].Id == "card_boarding_" + key);
        private static void SetMana(CoreVoyageSession session, int value)
        {
            typeof(CoreSchoolBattleState).GetProperty("Mana").SetValue(session.Battle.Schools, value);
            typeof(CoreSchoolBattleState).GetProperty("TemporaryMana").SetValue(session.Battle.Schools, 0);
        }
    }
}
