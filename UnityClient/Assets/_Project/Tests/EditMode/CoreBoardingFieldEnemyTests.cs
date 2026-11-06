using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBoardingFieldEnemyTests
    {
        [TestCase(true, 6)]
        [TestCase(false, 5)]
        public void ArcaneFieldRefillsEnemyManaAfterPlayerEndDecay(bool playerOwned, int mana)
        {
            var s = Battle("arcane", Card("spend", CoreBoardingEnemyMechanic.ArcaneSpend, attack: 100, resource: 10, bonus: 100));
            Field(s, CoreNavalFieldKind.Arcane, 3, playerOwned);
            Set(s.Battle.BoardingEnemy, "Mana", 99);
            var preview = s.GetBoardingEnemyPreview();
            Assert.That(preview.Actions.Single().RawAttack, Is.EqualTo(20 * (1 + mana)));
            Assert.That(preview.Actions.Single().ResourcesAfter.Mana, Is.Zero);
            Assert.That(s.Battle.BoardingEnemy.Mana, Is.EqualTo(99));
            AssertPreview(s, preview);
            Assert.That(s.Battle.BoardingFields.Remaining(CoreNavalFieldKind.Arcane), Is.EqualTo(2));
        }

        [Test]
        public void PreviewAppliesFieldsAfterExistingBurnAndNeverMutatesLiveFieldsOrMarks()
        {
            var s = Battle("fire", Card("idle"));
            Field(s, CoreNavalFieldKind.Fire, 4);
            Field(s, CoreNavalFieldKind.Curse, 4);
            Set(s.Battle.BoardingEnemy, "PlayerBurnDamage", 3);
            Set(s.Battle.BoardingEnemy, "PlayerBurnTurns", 3);
            Set(s.Battle.BoardingEnemy, "SelfBurnDamage", 2);
            Set(s.Battle.BoardingEnemy, "SelfBurnTurns", 2);
            s.Battle.PlayerBlock = 1;
            int health = s.Battle.PlayerBoardingHealth;
            string before = Snapshot(s);
            var preview = s.GetBoardingEnemyPreview();
            for (int i = 0; i < 4; i++)
            {
                Assert.That(Actions(s.GetBoardingEnemyPreview().Actions), Is.EqualTo(Actions(preview.Actions)));
                s.GetEnemyIntent();
                Assert.That(Snapshot(s), Is.EqualTo(before));
            }
            Assert.That(preview.PlayerBurnDamage, Is.EqualTo(2));
            Assert.That(preview.Actions.Single().ResourcesAfter.PlayerBurnDamage, Is.EqualTo(6));
            Assert.That(preview.ResourcesAfter.SelfBurnDamage, Is.EqualTo(5));
            Assert.That(preview.ResourcesAfter.CurseMarks, Is.EqualTo(4));
            AssertPreview(s, preview);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(health - 2));
            Assert.That(s.Battle.BoardingEnemy.PlayerBurnDamage, Is.EqualTo(6));
            Assert.That(s.Battle.BoardingEnemy.SelfBurnDamage, Is.EqualTo(5));
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(4));
            Assert.That(s.Battle.BoardingFields.Remaining(CoreNavalFieldKind.Fire), Is.EqualTo(3));
        }

        [Test]
        public void ExpiringHostileFieldsApplyPlayerEndThenStopBeforeEnemyStarts()
        {
            var s = Battle("thunder", Card("paid", selfDamage: 50), Card("strike", CoreBoardingEnemyMechanic.ThunderStrike, attack: 5));
            foreach (var kind in new[] { CoreNavalFieldKind.Fire, CoreNavalFieldKind.Curse, CoreNavalFieldKind.Arcane,
                CoreNavalFieldKind.Thunder, CoreNavalFieldKind.Blood }) Field(s, kind, 1, false);
            Set(s.Battle.BoardingEnemy, "ThunderChain", 49);
            var preview = s.GetBoardingEnemyPreview();
            Assert.That(preview.Actions[0].GuardGained, Is.Zero);
            Assert.That(preview.Actions[0].ResourcesAfter.Mana, Is.EqualTo(3));
            Assert.That(preview.Actions[1].ResourcesAfter.ThunderChain, Is.EqualTo(1));
            Assert.That(preview.Actions[1].ResourcesAfter.PlayerBurnDamage, Is.EqualTo(1));
            Assert.That(preview.Actions[1].ResourcesAfter.CurseMarks, Is.EqualTo(1));
            AssertPreview(s, preview);
            Assert.That(s.Battle.BoardingFields.Fields, Is.Empty);
            Assert.That(s.Battle.BoardingEnemy.SelfBurnDamage, Is.Zero);
        }

        [Test]
        public void ThunderPreservesAcrossBasicCardsAndFinisherAndOverloadsEachIncrease()
        {
            var s = Battle("thunder", Card("strike", CoreBoardingEnemyMechanic.ThunderStrike, attack: 50),
                Card("guard", guard: 500), Card("finish", CoreBoardingEnemyMechanic.ThunderFinisher, attack: 50, resource: 4, bonus: 25));
            Field(s, CoreNavalFieldKind.Thunder, 5);
            Set(s.Battle.BoardingEnemy, "ThunderChain", 49);
            var preview = s.GetBoardingEnemyPreview();
            CollectionAssert.AreEqual(new[] { 50, 50, 51 }, preview.Actions.Select(a => a.ResourcesAfter.ThunderChain));
            Assert.That(preview.Actions[0].EnemyHealthAfter, Is.EqualTo(9000));
            Assert.That(preview.Actions[1].EnemyGuardAfter, Is.EqualTo(20));
            Assert.That(preview.Actions[2].RawAttack, Is.EqualTo(30));
            Assert.That(preview.Actions[2].EnemyHealthAfter, Is.EqualTo(8020));
            Assert.That(preview.Actions[2].EnemyGuardAfter, Is.Zero);
            AssertPreview(s, preview);
            Assert.That(s.Battle.BoardingEnemy.ThunderChain, Is.EqualTo(51));
        }

        [Test]
        public void ThunderOverloadUsesGuardAndStopsEnemyDeckWhenItKills()
        {
            var s = Battle("thunder", Card("first", CoreBoardingEnemyMechanic.ThunderStrike, attack: 5),
                Card("second", CoreBoardingEnemyMechanic.ThunderStrike, attack: 5), Card("unreached", attack: 100));
            Field(s, CoreNavalFieldKind.Thunder, 5);
            Set(s.Battle.BoardingEnemy, "ThunderChain", 49);
            Set(s.Battle.BoardingEnemy, "RetainedGuard", 700);
            s.Battle.EnemyBlock = 700;
            s.Battle.EnemyBoardingHealth = 600;
            var state = s.Battle.BoardingEnemy;
            var preview = s.GetBoardingEnemyPreview();
            Assert.That(preview.Actions.Count, Is.EqualTo(2));
            Assert.That(preview.Actions[0].EnemyHealthAfter, Is.EqualTo(300));
            Assert.That(preview.Actions[0].EnemyGuardAfter, Is.Zero);
            Assert.That(preview.Actions[1].EnemyHealthAfter, Is.Zero);
            string expected = Actions(preview.Actions);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(Actions(state.LastActions), Is.EqualTo(expected));
            Assert.That(s.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
        }

        [Test]
        public void BloodPoolPaysDoubleGuardWithoutOrdinaryGuardCapAndDoesNotRefundEndLosses()
        {
            var s = Battle("blood", Card("pay", selfDamage: 250), Card("guard", guard: 100));
            Field(s, CoreNavalFieldKind.Blood, 5);
            Set(s.Battle.BoardingEnemy, "SelfBurnDamage", 60);
            Set(s.Battle.BoardingEnemy, "SelfBurnTurns", 60);
            var preview = s.GetBoardingEnemyPreview();
            Assert.That(preview.Actions[0].SelfDamage, Is.EqualTo(50));
            Assert.That(preview.Actions[0].GuardGained, Is.EqualTo(100));
            Assert.That(preview.Actions[0].EnemyGuardAfter, Is.EqualTo(100));
            Assert.That(preview.Actions[1].GuardGained, Is.Zero);
            Assert.That(preview.Actions[1].EnemyGuardAfter, Is.EqualTo(100));
            Assert.That(preview.EnemySelfBurnDamage, Is.Zero);
            Assert.That(preview.EnemyBloodPoolLoss, Is.EqualTo(5));
            AssertPreview(s, preview);
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(40), "Neither burning nor terrain life loss is an active life payment.");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(9945));
        }

        [Test]
        public void CurseFieldAtPlayerEndCanPreventEveryEnemyAction()
        {
            var s = Battle("curse", Card("unreached", attack: 100));
            Field(s, CoreNavalFieldKind.Curse, 6);
            s.Battle.PlayerBoardingHealth = 6;
            var state = s.Battle.BoardingEnemy;
            string before = Snapshot(s);
            var preview = s.GetBoardingEnemyPreview();
            Assert.That(preview.PlayerExecutedByCurse, Is.True);
            Assert.That(preview.Actions, Is.Empty);
            Assert.That(Snapshot(s), Is.EqualTo(before));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(state.LastActions, Is.Empty);
            Assert.That(s.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
        }

        private static void Field(CoreVoyageSession s, CoreNavalFieldKind kind, int turns, bool playerOwned = true) =>
            s.Battle.BoardingFields.Fields.Add(new CoreBoardingField { Kind = kind, PlayerOwned = playerOwned, RemainingTurns = turns });

        private static CoreBoardingEnemyCard Card(string id, CoreBoardingEnemyMechanic mechanic = CoreBoardingEnemyMechanic.Basic,
            int attack = 0, int guard = 0, int selfDamage = 0, int resource = 0, int bonus = 0) =>
            new CoreBoardingEnemyCard(id, id, attackPercent: attack, guardPercent: guard, selfDamagePercent: selfDamage,
                mechanic: mechanic, resourceAmount: resource, bonusPercent: bonus);

        private static CoreVoyageSession Battle(string school, params CoreBoardingEnemyCard[] cards)
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            s.SetSeed("BOARDING-FIELD-ENEMY"); s.StartVoyage();
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(s, new object[] { CoreRouteBossCatalog.All.Single(b => b.SchoolId == school).CreateEncounter(), true });
            Assert.That(s.EndBattleTurn(), Is.True);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 10000;
            s.Battle.DistanceMeters = 100f;
            Set(s.Battle, "EnemyHeadingDegrees", 90f); Set(s.Battle, "PlayerHeadingDegrees", 90f);
            s.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(s.TryBoard(), Is.True);
            s.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
                10000, 20, 20, 79, 0, 28, 1, 1, healthMultiplier: 1, boardingGuard: 10);
            s.Battle.PlayerBoardingHealth = s.Battle.PlayerMaxBoardingHealth = 10000;
            s.Battle.EnemyBoardingHealth = s.Battle.EnemyMaxBoardingHealth = 10000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = 0;
            s.Cargo.Curse = 0; Set(s, "Morale", 0);
            Set(s.Battle.BoardingEnemy, "Deck", new CoreBoardingEnemyDeck(school, new CoreBoardingEnemyTurn("测试", cards)));
            return s;
        }

        private static void AssertPreview(CoreVoyageSession s, CoreBoardingEnemyPreview preview)
        {
            string expected = Actions(preview.Actions);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(Actions(s.Battle.BoardingEnemy.LastActions), Is.EqualTo(expected));
        }

        private static string Actions(IEnumerable<CoreBoardingEnemyActionResult> actions) => string.Join(";", actions.Select(a =>
            a.CardId + ":" + string.Join(",", typeof(CoreBoardingEnemyActionResult).GetProperties()
                .Where(p => p.PropertyType == typeof(int) || p.PropertyType == typeof(bool))
                .OrderBy(p => p.Name).Select(p => p.Name + "=" + p.GetValue(a))) + ":" + Resources(a.ResourcesAfter)));

        private static string Resources(CoreBoardingEnemyState state) => string.Join(",", typeof(CoreBoardingEnemyState).GetProperties()
            .Where(p => p.PropertyType == typeof(int) || p.PropertyType == typeof(bool)).OrderBy(p => p.Name)
            .Select(p => p.Name + "=" + p.GetValue(state)));

        private static string Snapshot(CoreVoyageSession s) => string.Join("|", new object[] {
            Resources(s.Battle.BoardingEnemy), s.Battle.PlayerBoardingHealth, s.Battle.EnemyBoardingHealth,
            s.Battle.PlayerBlock, s.Battle.EnemyBlock, s.Battle.Boarding.CurseMarks, s.Battle.Boarding.Revision,
            string.Join(";", s.Battle.BoardingFields.Fields.Select(f => f.Kind + ":" + f.PlayerOwned + ":" + f.RemainingTurns)),
            string.Join(";", s.LogEntries), s.Battle.LastAction
        });

        private static void Set(object target, string name, object value) => target.GetType()
            .GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
    }
}
