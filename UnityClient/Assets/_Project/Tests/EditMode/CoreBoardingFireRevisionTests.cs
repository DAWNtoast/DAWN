using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBoardingFireRevisionTests
    {
        [Test]
        public void BoardingEntry_ImportsAppliedSeedOnlyOnce()
        {
            var s = Battle(beforeBoarding: session => {
                Set(session.Battle.Schools, "BoardingBurn", 4);
                Set(session.Battle.Schools, "BoardingBurnTurns", 2);
            });
            Assert.That(s.Battle.Boarding.EnemyBurnStacks, Is.EqualTo(4));
            Assert.That(s.Battle.BoardingEnemy.SelfBurnDamage, Is.EqualTo(4));
            Assert.That(s.Battle.EnemyBurnDamage, Is.EqualTo(4));
            Invoke(s, "BInitialize");
            Assert.That(s.Battle.Boarding.EnemyBurnStacks, Is.EqualTo(4));
        }

        [Test]
        public void PlayerBurn_AllSourcesShareOnePoolAndSpendGuardBeforeHealth()
        {
            var s = Battle();
            Invoke(s, "BAddSelfBurn", 2);
            Set(s.Battle.BoardingEnemy, "PlayerBurnDamage", 5);
            Assert.That(s.Battle.Boarding.SelfBurnStacks, Is.EqualTo(5));
            s.Battle.PlayerBlock = 3;
            int health = s.Battle.PlayerBoardingHealth;
            Invoke(s, "BPlayerEnd");
            Assert.That(s.Battle.PlayerBlock, Is.Zero);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(health - 2));
            Assert.That(s.Battle.Boarding.SelfBurnStacks, Is.EqualTo(4));
            Assert.That(s.Battle.BoardingEnemy.PlayerBurnDamage, Is.EqualTo(4));
            Assert.That(s.Battle.BoardingEnemy.LastBurnHealthLost, Is.EqualTo(2));
        }

        [Test]
        public void EnemyBurn_AllSourcesSettleAndDecayOnlyOnce()
        {
            var s = Battle();
            Set(s.Battle.BoardingEnemy, "SelfBurnDamage", 3);
            Invoke(s, "BAddEnemyBurn", 4, 2);
            Assert.That(s.Battle.Boarding.EnemyBurnStacks, Is.EqualTo(7));
            s.Battle.EnemyBlock = 5;
            int health = s.Battle.EnemyBoardingHealth;
            Invoke(s, "BEnemyEnd");
            Assert.That(s.Battle.EnemyBlock, Is.Zero);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(health - 2));
            Assert.That(s.Battle.Boarding.EnemyBurnStacks, Is.EqualTo(6));
            Assert.That(s.Battle.EnemyBurnDamage, Is.EqualTo(6));
        }

        [Test]
        public void Transfer_MovesAllBurnIncludingEnemyAppliedLayers()
        {
            var s = Battle();
            Invoke(s, "BAddSelfBurn", 3);
            Invoke(s, "BApplyPlayerBurn", 8, false);
            Invoke(s, "BAddEnemyBurn", 2, 2);
            Hand(s, "fire_15");
            Assert.That(s.GetBoardingPlayChoice(0).MaxResource, Is.EqualTo(11));
            Play(s, "fire_15");
            Assert.That(s.Battle.Boarding.SelfBurnStacks, Is.Zero);
            Assert.That(s.Battle.BoardingEnemy.PlayerBurnDamage, Is.Zero);
            Assert.That(s.Battle.Boarding.EnemyBurnStacks, Is.EqualTo(13));
        }

        [Test]
        public void BathingFlame_QuotesAndDealsDamageAfterAddingThreeBurn()
        {
            var s = Battle();
            Invoke(s, "BAddSelfBurn", 8);
            int health = s.Battle.EnemyBoardingHealth;
            Assert.That(s.GetBoardingCardDamageQuote(s.GetBattleCard(Index("fire_11"))), Is.EqualTo(82));
            Assert.That(s.Battle.Boarding.SelfBurnStacks, Is.EqualTo(8), "A quote must be read-only.");
            Play(s, "fire_11");
            Assert.That(s.Battle.Boarding.SelfBurnStacks, Is.EqualTo(11));
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(health - 82));
        }

        [Test]
        public void FirePot_ReappliesOnlyAtTheNextPlayerStart()
        {
            var s = Battle();
            Play(s, "fire_04");
            Assert.That(s.Battle.Boarding.EnemyBurnStacks, Is.EqualTo(4));
            Invoke(s, "BPlayerStart");
            Assert.That(s.Battle.Boarding.EnemyBurnStacks, Is.EqualTo(8));
            Assert.That(s.Battle.Boarding.FirePots, Is.Empty);
            Invoke(s, "BPlayerStart");
            Assert.That(s.Battle.Boarding.EnemyBurnStacks, Is.EqualTo(8));
        }

        [Test]
        public void FirePact_OnlyCopiesSelfAppliedBurnAndExpiresAfterNextPlayerEnd()
        {
            var s = Battle();
            Play(s, "fire_12");
            Play(s, "fire_12");
            Invoke(s, "BApplyPlayerBurn", 3, false);
            Assert.That(s.Battle.Boarding.EnemyBurnStacks, Is.Zero);
            Invoke(s, "BAddSelfBurn", 2);
            Assert.That(s.Battle.Boarding.EnemyBurnStacks, Is.EqualTo(4));
            Invoke(s, "BPlayerEnd");
            Invoke(s, "BAddSelfBurn", 2);
            Assert.That(s.Battle.Boarding.EnemyBurnStacks, Is.EqualTo(8));
            Invoke(s, "BPlayerEnd");
            Invoke(s, "BAddSelfBurn", 2);
            Assert.That(s.Battle.Boarding.EnemyBurnStacks, Is.EqualTo(8));
        }

        [Test]
        public void BurningThroughGuard_DoesNotTriggerRetaliation()
        {
            var s = Battle();
            Play(s, "fire_13");
            int enemy = s.Battle.EnemyBoardingHealth;
            Invoke(s, "BPlayerEnd");
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(14));
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy));
            s.Battle.PlayerBlock -= 1;
            Invoke(s, "BAfterEnemyAttack", 1, 0);
            s.Battle.PlayerBlock -= 1;
            Invoke(s, "BAfterEnemyAttack", 1, 0);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 2));
        }

        [Test]
        public void AttachedFire_FullyBlockedAttackSpendsAllLayersOnce()
        {
            var s = Battle();
            s.Battle.PlayerFireRounds = 5;
            s.Battle.EnemyBlock = 100;
            int enemy = s.Battle.EnemyBoardingHealth;
            Play(s, "basic_01");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy));
            Assert.That(s.Battle.PlayerFireRounds, Is.Zero);
            Assert.That(s.Battle.Boarding.EnemyBurnStacks, Is.EqualTo(5));
            Play(s, "basic_01");
            Assert.That(s.Battle.Boarding.EnemyBurnStacks, Is.EqualTo(5));
        }

        [Test]
        public void AttachedFire_DelayedActiveAttackSpendsAtResolution()
        {
            var s = Battle();
            s.Battle.PlayerFireRounds = 4;
            Play(s, "basic_13");
            Assert.That(s.Battle.PlayerFireRounds, Is.EqualTo(4));
            Invoke(s, "BPlayerStart");
            Assert.That(s.Battle.PlayerFireRounds, Is.Zero);
            Assert.That(s.Battle.Boarding.EnemyBurnStacks, Is.EqualTo(4));
        }

        [Test]
        public void AttachedFire_PassiveEchoDoesNotConsumeReserve()
        {
            var s = Battle();
            s.Battle.PlayerFireRounds = 4;
            s.Battle.Boarding.DelayedDamage.Add(new CoreBoardingDelayedDamage { Amount = 5, SourceCardId = "card_boarding_basic_13" });
            Invoke(s, "BPlayerStart");
            Assert.That(s.Battle.PlayerFireRounds, Is.EqualTo(4));
            Assert.That(s.Battle.Boarding.EnemyBurnStacks, Is.Zero);
        }

        [Test]
        public void EnemyPreview_UsesUnifiedBurnAndConsumesOilOnlyInItsOwnCopy()
        {
            var s = Battle();
            s.Battle.NavalExpansion.OilPlayerEndsRemaining = 1;
            Set(s.Battle.BoardingEnemy, "Deck", new CoreBoardingEnemyDeck("fire", new[] {
                new CoreBoardingEnemyTurn("连续引火", new[] {
                    new CoreBoardingEnemyCard("fire-a", "引火", mechanic: CoreBoardingEnemyMechanic.FireSelfBurn, resourceAmount: 2),
                    new CoreBoardingEnemyCard("fire-b", "引火", mechanic: CoreBoardingEnemyMechanic.FireSelfBurn, resourceAmount: 2)
                })
            }));
            var preview = (CoreBoardingEnemyPreview)Invoke(s, "RunBoardingEnemyTurn", false, true);
            Assert.That(preview.Actions[0].ResourcesAfter.SelfBurnDamage, Is.EqualTo(4));
            Assert.That(preview.Actions[1].ResourcesAfter.SelfBurnDamage, Is.EqualTo(6));
            Assert.That(preview.ResourcesAfter.SelfBurnDamage, Is.EqualTo(5));
            Assert.That(s.Battle.NavalExpansion.OilPlayerEndsRemaining, Is.EqualTo(1));
            Assert.That(s.Battle.Boarding.EnemyBurnStacks, Is.Zero);
            Invoke(s, "BPlayerEnd");
            Invoke(s, "RunBoardingEnemyTurn", true, false);
            Invoke(s, "BEnemyEnd");
            Assert.That(s.Battle.Boarding.EnemyBurnStacks, Is.EqualTo(5));
            Assert.That(s.Battle.NavalExpansion.OilPlayerEndsRemaining, Is.Zero);
        }

        [Test]
        public void EnemyBurn_PreviewAndCommitAgreeWhenBurnConsumesRetainedGuard()
        {
            var s = Battle();
            Invoke(s, "BAddEnemyBurn", 3, 2);
            s.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
                1000, 0, 20, 0, 0, 28, 1, 1, healthMultiplier: 1, boardingGuard: 10);
            Set(s.Battle.BoardingEnemy, "Deck", new CoreBoardingEnemyDeck("tide", new[] {
                new CoreBoardingEnemyTurn("留盾", new[] {
                    new CoreBoardingEnemyCard("tide-guard", "留盾", guardPercent: 100, mechanic: CoreBoardingEnemyMechanic.TideGuard)
                })
            }));
            var preview = (CoreBoardingEnemyPreview)Invoke(s, "RunBoardingEnemyTurn", false, true);
            Assert.That(preview.ResourcesAfter.RetainedGuard, Is.EqualTo(7));
            Invoke(s, "BPlayerEnd");
            Invoke(s, "RunBoardingEnemyTurn", true, false);
            Invoke(s, "BEnemyEnd");
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(7));
            Assert.That(s.Battle.BoardingEnemy.RetainedGuard, Is.EqualTo(preview.ResourcesAfter.RetainedGuard));
            Assert.That(s.Battle.Boarding.EnemyBurnStacks, Is.EqualTo(2));
        }

        [Test]
        public void CopyResources_DoesNotRetainLiveBurnPoolBinding()
        {
            var s = Battle();
            Invoke(s, "BAddSelfBurn", 4);
            var copy = (CoreBoardingEnemyState)typeof(CoreBoardingEnemyState).GetMethod("CopyResources", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(s.Battle.BoardingEnemy, null);
            Set(copy, "PlayerBurnDamage", 1);
            Assert.That(s.Battle.Boarding.SelfBurnStacks, Is.EqualTo(4));
            Assert.That(copy.PlayerBurnDamage, Is.EqualTo(1));
        }

        private static CoreVoyageSession Battle(Action<CoreVoyageSession> beforeBoarding = null)
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            s.SetSeed("BOARDING-FIRE-REVISION-20261024");
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 1000;
            s.Battle.DistanceMeters = 100f;
            Set(s.Battle, "EnemyHeadingDegrees", 90f); Set(s.Battle, "PlayerHeadingDegrees", 90f);
            s.Battle.Alignment = NavalBattleAlignment.Parallel;
            beforeBoarding?.Invoke(s);
            Assert.That(s.TryBoard(), Is.True);
            s.Battle.PlayerBoardingHealth = s.Battle.PlayerMaxBoardingHealth = 1000;
            s.Battle.EnemyBoardingHealth = s.Battle.EnemyMaxBoardingHealth = 1000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = 0;
            s.Battle.Hand.Clear(); s.Battle.DrawPile.Clear(); s.Battle.DiscardPile.Clear();
            Set(s, "Morale", 0);
            s.Cargo.Powder = 20; s.Cargo.Medicine = 0;
            return s;
        }

        private static int Index(string key) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == "card_boarding_" + key);
        private static void Hand(CoreVoyageSession s, string key)
        { s.Battle.Hand.Clear(); s.Battle.Hand.Add(Index(key)); s.Battle.Energy = 100; }
        private static void Play(CoreVoyageSession s, string key)
        { Hand(s, key); Assert.That(s.PlayCard(0), Is.True, key + ": " + s.CardUnavailableReason(0)); }
        private static object Invoke(CoreVoyageSession s, string name, params object[] args) =>
            typeof(CoreVoyageSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s, args);
        private static void Set(object target, string property, object value) =>
            target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
    }
}
