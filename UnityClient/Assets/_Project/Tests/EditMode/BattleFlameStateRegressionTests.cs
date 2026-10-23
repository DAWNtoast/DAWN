using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class BattleFlameStateRegressionTests
    {
        private const string StrongFlame = "card_special_bow_gilded_mermaid";
        private const string WeakFlame = "card_special_crew_salt_crown_gunner_rokan";

        [Test]
        public void EmptyFlameReserve_UsesTheNewSourceStrengthInsteadOfSpentUpgrade()
        {
            CoreVoyageSession session = CreateMixedFlameBattle();
            ExhaustStrongReserve(session);
            Assert.That(session.Battle.PlayerFireRounds, Is.Zero);
            int weakDamage = session.GetBattleCard(Index(WeakFlame)).BurnDamage;
            Assert.That(weakDamage, Is.LessThan(session.GetBattleCard(Index(StrongFlame)).BurnDamage));

            Play(session, WeakFlame);

            Assert.That(session.Battle.PlayerFireRounds, Is.EqualTo(2));
            Assert.That(session.Battle.PlayerFireRoundDamage, Is.EqualTo(weakDamage),
                "An exhausted upgraded reserve must not permanently upgrade a different source.");
        }

        [Test]
        public void ExpiredStrongFire_AFreshWeakShotBurnsForItsAuthoredDamage()
        {
            CoreVoyageSession session = CreateMixedFlameBattle();
            ExhaustStrongReserve(session);
            int layers = session.Battle.EnemyBurnTurns;
            while (layers > 0)
            {
                int before = session.Battle.EnemyHull;
                typeof(CoreVoyageSession).GetMethod("ApplySchoolNaturalBurn", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(session, null);
                Assert.That(session.Battle.EnemyHull, Is.EqualTo(before - layers));
                Assert.That(session.Battle.EnemyBurnDamage, Is.EqualTo(--layers));
                Assert.That(session.Battle.EnemyBurnTurns, Is.EqualTo(layers));
            }
            Assert.That(session.EndBattleTurn(), Is.True, "Begin a fresh action window after the previous ammunition was spent.");
            Play(session, WeakFlame);
            Play(session, "card_chain_shot");

            int weakDamage = session.GetBattleCard(Index(WeakFlame)).BurnDamage;
            Assert.That(session.Battle.EnemyBurnDamage, Is.EqualTo(weakDamage));
            Assert.That(session.GetEnemyIntent(), Does.Contain("灼烧 " + weakDamage));
            int hull = session.Battle.EnemyHull;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(hull - weakDamage));
        }

        [Test]
        public void LiveStrongFire_AWeakShotAddsItsLayersToTheRemainingStrongFire()
        {
            CoreVoyageSession session = CreateMixedFlameBattle();
            ExhaustStrongReserve(session);
            Assert.That(session.EndBattleTurn(), Is.True);
            int remaining = session.GetBattleCard(Index(StrongFlame)).BurnDamage * 2 - 2;
            Assert.That(session.Battle.EnemyBurnTurns, Is.EqualTo(remaining), "Each completed enemy turn removes exactly one layer.");
            Play(session, WeakFlame);
            Play(session, "card_chain_shot");

            Assert.That(session.Battle.PlayerFireRoundDamage, Is.EqualTo(session.GetBattleCard(Index(WeakFlame)).BurnDamage));
            int total = remaining + session.GetBattleCard(Index(WeakFlame)).BurnDamage;
            Assert.That(session.Battle.EnemyBurnTurns, Is.EqualTo(total));
            Assert.That(session.Battle.EnemyBurnDamage, Is.EqualTo(total),
                "New applications add layers without replacing or reviving an earlier source.");
        }

        [Test]
        public void EnemyActionLog_UsesThisTurnsIntentAfterANonlethalBurn()
        {
            CoreVoyageSession session = CreateMixedFlameBattle();
            session.Battle.DistanceMeters = 60f;
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            session.Battle.EnemyHull = 15;
            session.Battle.EnemyBurnTurns = 10;
            session.Battle.EnemyBurnDamage = 10;
            session.Battle.PlayerBlock = 0;
            int playerHull = session.Hull;
            Assert.That(session.GetEnemyIntent(), Does.Contain("炮击：伤害"));

            Assert.That(session.EndBattleTurn(), Is.True);

            Assert.That(session.Battle.EnemyHull, Is.EqualTo(5));
            Assert.That(session.Hull, Is.LessThan(playerHull));
            Assert.That(session.Battle.LastAction, Does.Contain("炮击：伤害"));
            Assert.That(session.Battle.LastAction, Does.Not.Contain("敌方无法行动"),
                "Next turn's lethal burn must not replace the attack that just occurred.");
            Assert.That(session.GetEnemyIntent(), Does.Not.Contain("敌方无法行动"));
            Assert.That(session.GetEnemyIntent(), Does.Contain("回合结束灼烧 9"),
                "Even a lethal next burn resolves after the enemy action.");
        }

        private static CoreVoyageSession CreateMixedFlameBattle()
        {
            var session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            session.ConfigureSpecialCardSources(new[] { "crew_salt_crown_gunner_rokan" }, new[] { "bow_gilded_mermaid" });
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { "card_chain_shot", 2 } });
            session.RestoreSpecialCardExperience(new Dictionary<string, int> { { StrongFlame, 120 } });
            session.SetSeed("MIXED-FLAME-REGRESSION");
            session.StartVoyage();
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session,
                new object[] { new CoreEncounter("flame_test", "火焰测试敌舰", "战斗", "混合来源测试", true, false, 1, 70), false });
            Assert.That(session.EndBattleTurn(), Is.True);
            session.Battle.DistanceMeters = 200f;
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 500;
            session.Battle.EnemyBlock = 0;
            return session;
        }

        private static void ExhaustStrongReserve(CoreVoyageSession session)
        {
            Play(session, StrongFlame);
            Assert.That(session.Battle.PlayerFireRounds, Is.EqualTo(2), "Upgrade damage does not multiply ammunition reserves.");
            Assert.That(session.Battle.PlayerFireRoundDamage, Is.EqualTo(session.GetBattleCard(Index(StrongFlame)).BurnDamage));
            for (int shot = 0; shot < 2; shot++)
            {
                // Exhaust ammunition through normal turns. Each shot adds its own
                // layers; the intervening enemy turn settles and removes one layer.
                if (shot > 0) Assert.That(session.EndBattleTurn(), Is.True);
                Play(session, "card_chain_shot");
            }
            int layers = session.GetBattleCard(Index(StrongFlame)).BurnDamage * 2 - 1;
            Assert.That(session.Battle.EnemyBurnTurns, Is.EqualTo(layers));
            Assert.That(session.Battle.EnemyBurnDamage, Is.EqualTo(layers));
        }

        private static void Play(CoreVoyageSession session, string id)
        {
            session.Battle.Hand.Clear();
            session.Battle.Hand.Add(Index(id));
            Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
        }

        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == id);
    }
}
