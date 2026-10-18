using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBoardingBaselineTests
    {
        [TestCase("basic_01", 1, 8)]
        [TestCase("basic_02", 2, 20)]
        [TestCase("basic_03", 3, 35)]
        public void BasicAttack_PaysPrintedEnergyAndDealsAuthoredDamage(string key, int cost, int damage)
        {
            CoreVoyageSession s = Battle();
            Put(s, key);
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.Energy, Is.EqualTo(3 - cost));
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(1000 - damage));
            Assert.That(s.Battle.Hand, Is.Empty);
            Assert.That(s.Battle.DiscardPile.Last(), Is.EqualTo(Index(key)));
        }

        [TestCase("basic_04", 1, 4)]
        [TestCase("basic_05", 2, 10)]
        public void BasicGuard_ExpiresAtTheNextPlayerTurn(string key, int cost, int block)
        {
            CoreVoyageSession s = Battle();
            Put(s, key);
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.Energy, Is.EqualTo(3 - cost));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(block));
            End(s);
            Assert.That(s.Battle.PlayerBlock, Is.Zero);
            Assert.That(s.Battle.Boarding.TidePower, Is.Zero, "Baseline guard cannot generate tide power.");
        }

        [TestCase(2, 6, 1)]
        [TestCase(0, 2, 0)]
        public void BasicHeal_UsesMedicineForSixOtherwiseTwo(int medicine, int healing, int medicineSpent)
        {
            CoreVoyageSession s = Battle();
            s.Cargo.Medicine = medicine;
            Put(s, "basic_06");
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(500 + healing));
            Assert.That(s.Battle.Energy, Is.EqualTo(2));
            Assert.That(s.Cargo.Medicine, Is.EqualTo(medicine - medicineSpent));
            End(s);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(500 + healing), "Immediate healing must not create delayed ticks.");
        }

        [TestCase(2, 5, 1)]
        [TestCase(0, 2, 0)]
        public void BasicSustainedHeal_PaysOnceAndTicksOnExactlyTwoPlayerEnds(int medicine, int tick, int medicineSpent)
        {
            CoreVoyageSession s = Battle();
            s.Cargo.Medicine = medicine;
            Put(s, "basic_07");
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.Energy, Is.EqualTo(1));
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(500));
            Assert.That(s.Cargo.Medicine, Is.EqualTo(medicine - medicineSpent));
            End(s);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(500 + tick));
            End(s);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(500 + 2 * tick));
            End(s);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(500 + 2 * tick));
            Assert.That(s.Cargo.Medicine, Is.EqualTo(medicine - medicineSpent), "Later ticks never charge a second medicine.");
            Assert.That(s.Battle.Boarding.HealingTicks, Is.Empty);
        }

        [Test]
        public void BasicDraw_PaysTwoAndDrawsThreeAfterRemovingThePlayedCard()
        {
            CoreVoyageSession s = Battle();
            Put(s, "basic_08");
            s.Battle.DrawPile.Clear(); s.Battle.DiscardPile.Clear();
            s.Battle.DrawPile.AddRange(new[] { Index("basic_01"), Index("basic_02"), Index("basic_03") });
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.Energy, Is.EqualTo(1));
            Assert.That(s.Battle.Hand, Is.EqualTo(new[] { Index("basic_03"), Index("basic_02"), Index("basic_01") }));
            Assert.That(s.Battle.DiscardPile, Is.EqualTo(new[] { Index("basic_08") }));
        }

        [TestCase("basic_09", 0, 1)]
        [TestCase("basic_10", 3, 4)]
        public void BasicEnergy_GainsNetOneWithoutCappingAtTurnMaximum(string key, int startEnergy, int endEnergy)
        {
            CoreVoyageSession s = Battle();
            s.Battle.Energy = startEnergy; Put(s, key);
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.Energy, Is.EqualTo(endEnergy));
        }

        [Test]
        public void BasicEnergyRecovery_CannotPayItsOwnUpfrontCostOrBypassRepeatCosts()
        {
            CoreVoyageSession s = Battle();
            s.Battle.Energy = 1; Put(s, "basic_10");
            Assert.That(s.PlayCard(0), Is.False);
            Assert.That(s.Battle.Energy, Is.EqualTo(1));
            Assert.That(s.Battle.Hand, Is.EqualTo(new[] { Index("basic_10") }));
            s.Battle.Energy = 0; Put(s, "basic_09");
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.Energy, Is.EqualTo(1));
            Put(s, "basic_09");
            Assert.That(s.GetBattleCardEnergyCost(Index("basic_09")), Is.EqualTo(1));
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.Energy, Is.EqualTo(1));
            Put(s, "basic_09");
            Assert.That(s.GetBattleCardEnergyCost(Index("basic_09")), Is.EqualTo(2));
            Assert.That(s.PlayCard(0), Is.False);
            Assert.That(s.Battle.Energy, Is.EqualTo(1));
        }

        [TestCase("basic_11", 4, 0)]
        [TestCase("basic_12", 0, 2)]
        public void BasicMixedCards_PayOneApplyHalfEffectAndDrawOne(string key, int damage, int guard)
        {
            CoreVoyageSession s = Battle();
            Put(s, key);
            s.Battle.DrawPile.Clear(); s.Battle.DrawPile.Add(Index("basic_01"));
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.Energy, Is.EqualTo(2));
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(1000 - damage));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(guard));
            Assert.That(s.Battle.Hand, Is.EqualTo(new[] { Index("basic_01") }));
        }

        [Test]
        public void BasicDelayedStrike_ResolvesOnceAtNextPlayerStartAgainstCurrentGuardWithoutCritical()
        {
            CoreVoyageSession s = Battle();
            typeof(CoreVoyageSession).GetProperty("Morale").SetValue(s, 100);
            Put(s, "basic_13");
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.Energy, Is.EqualTo(2));
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(1000));
            Assert.That(s.Battle.Boarding.DelayedDamage.Count, Is.EqualTo(1));
            s.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
                1000, 0, 0, 5, 0, 28, 1, 1, healthMultiplier: 1);
            s.Battle.PhaseTurn = 2; // Enemy grants five guard before the next player start.
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(993), "Twelve delayed damage minus the enemy's current five guard.");
            Assert.That(s.Battle.EnemyBlock, Is.Zero);
            Assert.That(s.Battle.Boarding.DelayedDamage, Is.Empty);
            End(s);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(993));
        }

        private static CoreVoyageSession Battle()
        {
            var s = new CoreVoyageSession();
            s.SetSeed("BOARDING-BASELINE-TEST");
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 1000;
            s.Battle.DistanceMeters = 100f;
            typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(s.Battle, s.Battle.EnemyHeadingDegrees);
            s.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(s.TryBoard(), Is.True);
            s.Battle.PlayerBoardingHealth = 500; s.Battle.PlayerMaxBoardingHealth = 1000;
            s.Battle.EnemyBoardingHealth = s.Battle.EnemyMaxBoardingHealth = 1000;
            s.Battle.EnemyBlock = 0;
            s.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
                1000, 0, 0, 0, 0, 28, 1, 1, healthMultiplier: 1);
            typeof(CoreVoyageSession).GetProperty("Morale").SetValue(s, 0);
            s.Cargo.Medicine = 0;
            return s;
        }

        private static int Index(string key) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == "card_boarding_" + key);
        private static void Put(CoreVoyageSession s, string key)
        { s.Battle.Hand.Clear(); s.Battle.Hand.Add(Index(key)); }
        private static void End(CoreVoyageSession s)
        { s.Battle.PhaseTurn = 1; Assert.That(s.EndBattleTurn(), Is.True); }
    }
}
