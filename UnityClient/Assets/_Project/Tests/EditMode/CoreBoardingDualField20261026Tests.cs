using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBoardingDualField20261026Tests
    {
        [TestCase(CoreNavalFieldKind.Fire, 20)]
        [TestCase(CoreNavalFieldKind.Arcane, 20)]
        [TestCase(CoreNavalFieldKind.Curse, 12)]
        public void ActivePlayerAttackQuoteAndGuardSettlementUseFieldModifier(CoreNavalFieldKind kind, int damage)
        {
            var s = Battle(); Field(s, kind, 5); s.Battle.EnemyBlock = 7;
            var card = Card("basic_01");
            Assert.That(s.GetBoardingCardDamageQuote(card), Is.EqualTo(damage));
            Play(s, "basic_01");
            Assert.That(s.Battle.EnemyBlock, Is.Zero);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(1000 - damage + 7));
        }

        [TestCase(CoreNavalFieldKind.Fire, CoreNavalFieldKind.Arcane, 25)]
        [TestCase(CoreNavalFieldKind.Fire, CoreNavalFieldKind.Curse, 15)]
        [TestCase(CoreNavalFieldKind.Arcane, CoreNavalFieldKind.Curse, 15)]
        public void SharedInheritedArenaComposesFieldsBeforeRounding(CoreNavalFieldKind a, CoreNavalFieldKind b, int damage)
        {
            var s = Battle(); Field(s, a, 5); Field(s, b, 5);
            Assert.That(s.GetBoardingCardDamageQuote(Card("basic_01")), Is.EqualTo(damage));
            Play(s, "basic_01");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(1000 - damage));
        }

        [TestCase("arcane_06")]
        [TestCase("thunder_11")]
        [TestCase("blood_07")]
        [TestCase("curse_11")]
        public void MultiHitAndPiercingQuotesMatchActualFieldDamage(string key)
        {
            var s = Battle(); Field(s, CoreNavalFieldKind.Arcane, 5);
            s.Battle.Schools.Mana = 3; s.Battle.Schools.ThunderChain = 1;
            if (key == "curse_11") Invoke(s, "CTBAddMarks", 4);
            int damage = s.GetBoardingCardDamageQuote(Card(key));
            Play(s, key);
            Assert.That(1000 - s.Battle.EnemyBoardingHealth, Is.EqualTo(damage), key);
        }

        [Test]
        public void ScheduledAttackUsesFieldAtResolutionExactlyOnce()
        {
            var s = Battle(); Field(s, CoreNavalFieldKind.Fire, 5);
            int quote = s.GetBoardingCardDamageQuote(Card("basic_13"));
            Play(s, "basic_13");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(1000));
            Invoke(s, "BPlayerStart");
            Assert.That(1000 - s.Battle.EnemyBoardingHealth, Is.EqualTo(quote));
        }

        [Test]
        public void ExpiredFieldDoesNotRemainAttachedToScheduledAttack()
        {
            var s = Battle(); int baseline = s.GetBoardingCardDamageQuote(Card("basic_13"));
            Field(s, CoreNavalFieldKind.Fire, 5); Play(s, "basic_13");
            s.Battle.BoardingFields.Fields.Clear(); Invoke(s, "BPlayerStart");
            Assert.That(1000 - s.Battle.EnemyBoardingHealth, Is.EqualTo(baseline));
        }

        [TestCase(CoreNavalFieldKind.Fire, 25)]
        [TestCase(CoreNavalFieldKind.Arcane, 25)]
        [TestCase(CoreNavalFieldKind.Curse, 15)]
        public void EnemyPreviewAndExecutionApplyTheSameSharedField(CoreNavalFieldKind kind, int damage)
        {
            var s = Battle(); EnemyScript(s); Field(s, kind, 5);
            var preview = s.GetBoardingEnemyPreview();
            Assert.That(preview.AttackDamage, Is.EqualTo(damage));
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(1000));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.BoardingEnemy.LastActions.Single().AttackDamage, Is.EqualTo(damage));
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(1000 - damage));
        }

        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void BloodLossUsesPreDecayDurationAndBypassesGuard(bool player, bool owner)
        {
            var s = Battle(); Field(s, CoreNavalFieldKind.Blood, 5, owner);
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = 77;
            Invoke(s, player ? "BPlayerEnd" : "BEnemyEnd");
            Assert.That(player ? s.Battle.PlayerBoardingHealth : s.Battle.EnemyBoardingHealth, Is.EqualTo(995));
            Assert.That(player ? s.Battle.PlayerBlock : s.Battle.EnemyBlock, Is.EqualTo(77));
            Assert.That(s.Battle.BoardingFields.Remaining(CoreNavalFieldKind.Blood), Is.EqualTo(player == owner ? 5 : 4));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void BloodLossCanKillWithoutAnyGuardRefund(bool player)
        {
            var s = Battle(); Field(s, CoreNavalFieldKind.Blood, 5);
            CoreBattleState state = s.Battle;
            state.PlayerBlock = state.EnemyBlock = 77;
            if (player) state.PlayerBoardingHealth = 4; else state.EnemyBoardingHealth = 4;
            Assert.That((bool)Invoke(s, player ? "BPlayerEnd" : "BEnemyEnd"), Is.True);
            Assert.That(player ? state.PlayerBoardingHealth : state.EnemyBoardingHealth, Is.Zero);
            Assert.That(player ? state.PlayerBlock : state.EnemyBlock, Is.EqualTo(77));
        }

        [Test]
        public void ActiveLifePaymentReturnsDoubleGuardButTerrainLossReturnsNone()
        {
            var s = Battle(); Field(s, CoreNavalFieldKind.Blood, 5);
            Play(s, "blood_01");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(994));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(12));
            Invoke(s, "BPlayerEnd");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(989));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(12));
        }

        [Test]
        public void EnemyBloodPreviewIncludesBothEndLossesAndDoublePaymentGuard()
        {
            var s = Battle(); EnemyScript(s, 50); Field(s, CoreNavalFieldKind.Blood, 5);
            var preview = s.GetBoardingEnemyPreview();
            Assert.That(preview.PlayerBloodPoolLoss, Is.EqualTo(5));
            Assert.That(preview.EnemyBloodPoolLoss, Is.EqualTo(5));
            Assert.That(preview.Actions.Single().SelfDamage, Is.EqualTo(10));
            Assert.That(preview.Actions.Single().GuardGained, Is.EqualTo(20));
            Assert.That(s.GetEnemyIntent(), Does.Contain("血池流失生命 5"));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(975));
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(985));
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(20));
        }

        [Test]
        public void BloodLossInPreviewCanPreventAllEnemyActionsWithoutMutatingState()
        {
            var s = Battle(); EnemyScript(s); Field(s, CoreNavalFieldKind.Blood, 5);
            s.Battle.PlayerBoardingHealth = 4; s.Battle.PlayerBlock = 100;
            var preview = s.GetBoardingEnemyPreview();
            Assert.That(preview.Actions, Is.Empty);
            Assert.That(preview.PlayerBloodPoolLoss, Is.EqualTo(4));
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(4));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(100));
            Assert.That(s.Battle.BoardingFields.Remaining(CoreNavalFieldKind.Blood), Is.EqualTo(5));
        }

        [Test]
        public void BloodAndCurseExecuteImmediatelyAfterThirdPreDecayLifeLoss()
        {
            var s = Battle(true); EnemyScript(s);
            s.Battle.BoardingEnemy.Deck = new CoreBoardingEnemyDeck("neutral", new CoreBoardingEnemyTurn("等待",
                new CoreBoardingEnemyCard("idle", "等待")));
            Field(s, CoreNavalFieldKind.Blood, 10); Field(s, CoreNavalFieldKind.Curse, 10);
            CoreBattleState state = s.Battle;
            state.PlayerBoardingHealth = 54; state.PlayerBlock = 999;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(state.PlayerBoardingHealth, Is.EqualTo(44));
            Assert.That(state.BoardingEnemy.CurseMarks, Is.EqualTo(10));
            Assert.That(state.BoardingFields.Remaining(CoreNavalFieldKind.Blood), Is.EqualTo(9));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(state.PlayerBoardingHealth, Is.EqualTo(35));
            Assert.That(state.BoardingEnemy.CurseMarks, Is.EqualTo(19));
            Assert.That(state.BoardingFields.Remaining(CoreNavalFieldKind.Blood), Is.EqualTo(8));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(state.BoardingEnemy.CurseMarks, Is.EqualTo(27));
            Assert.That(state.PlayerBoardingHealth, Is.Zero,
                "27 marks do not execute at 35 HP; the R=8 loss first lowers HP to exactly 27, then immediately executes.");
            Assert.That(state.Boarding.PlayerExecutedByCurse, Is.True);
        }

        [Test]
        public void CurseReductionIsPreservedByEveryPublicDamageQuote()
        {
            var s = Battle(); Field(s, CoreNavalFieldKind.Curse, 5);
            CoreBattleCard card = Card("basic_01");
            Assert.That(s.GetBattleCardEffects(card, NavalBattleStage.Boarding).Damage, Is.EqualTo(12));
            Assert.That(s.GetBattleCardDamage(card, NavalBattleStage.Boarding), Is.EqualTo(12));
            Play(s, "basic_01");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(988));
        }

        [TestCase(CoreNavalFieldKind.Fire)]
        [TestCase(CoreNavalFieldKind.Arcane)]
        [TestCase(CoreNavalFieldKind.Curse)]
        public void PassiveDamageAndOverloadDoNotReceiveAttackModifiers(CoreNavalFieldKind kind)
        {
            var s = Battle(); Field(s, kind, 5);
            Invoke(s, "BDeal", 20, false, false);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(980));
            Invoke(s, "BBoardingFieldDamage", true, 30);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(970));
        }

        private static void Field(CoreVoyageSession s, CoreNavalFieldKind kind, int turns, bool owner = true)
            => s.Battle.BoardingFields.Fields.Add(new CoreBoardingField { Kind = kind, RemainingTurns = turns, PlayerOwned = owner });

        private static CoreBattleCard Card(string key) => CoreVoyageSession.CardLibrary.Single(c => c.Id == "card_boarding_" + key);

        private static void Play(CoreVoyageSession s, string key)
        {
            s.Battle.Hand.Clear(); s.Battle.Hand.Add(CoreVoyageSession.CardLibrary.ToList().IndexOf(Card(key)));
            s.Battle.Energy = 100;
            Assert.That(s.PlayCard(0), Is.True, key);
        }

        private static void EnemyScript(CoreVoyageSession s, int selfDamage = 0)
        {
            s.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
                1000, 20, 20, 0, 0, 28, 1, 1, healthMultiplier: 1);
            s.Battle.BoardingEnemy.Deck = new CoreBoardingEnemyDeck("neutral", new CoreBoardingEnemyTurn("测试",
                new CoreBoardingEnemyCard("test", "test", attackPercent: 100, selfDamagePercent: selfDamage)));
        }

        private static CoreVoyageSession Battle(bool voyage = false)
        {
            var s = new CoreVoyageSession(); s.SetSeed("BOARDING-DUAL-FIELD-20261026");
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            if (voyage) s.StartVoyage();
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 1000; s.Battle.DistanceMeters = 100f;
            Set(s.Battle, "EnemyHeadingDegrees", 90f); Set(s.Battle, "PlayerHeadingDegrees", 90f);
            s.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(s.TryBoard(), Is.True);
            s.Battle.PlayerBoardingHealth = s.Battle.PlayerMaxBoardingHealth = 1000;
            s.Battle.EnemyBoardingHealth = s.Battle.EnemyMaxBoardingHealth = 1000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = 0; s.Cargo.Powder = 100; s.Cargo.Curse = 0;
            Set(s, "Morale", 0);
            return s;
        }

        private static object Invoke(CoreVoyageSession s, string name, params object[] args)
            => typeof(CoreVoyageSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s, args);

        private static void Set(object target, string name, object value)
            => target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }
}
