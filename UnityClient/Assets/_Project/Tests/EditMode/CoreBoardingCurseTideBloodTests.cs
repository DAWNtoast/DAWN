using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBoardingCurseTideBloodTests
    {
        [Test]
        public void CurseMarks_AreCappedAndExpireInIndependentBatchesWithoutDamage()
        {
            CoreVoyageSession s = Battle();
            Play(s, "curse_02");
            End(s);
            Play(s, "curse_02");
            Play(s, "curse_02"); // Overflow cannot refresh either batch.
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(6));
            int hp = s.Battle.EnemyBoardingHealth;
            End(s);
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(6));
            End(s);
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(3));
            End(s);
            Assert.That(s.Battle.Boarding.CurseMarks, Is.Zero);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp), "Marks must never tick damage.");
            Assert.That(s.Battle.Schools.CurseDamagePerMark, Is.Zero);
        }

        [Test]
        public void CurseConsumption_UsesTheOldestLayersAndExtensionIsOncePerLayer()
        {
            CoreVoyageSession s = Battle();
            Play(s, "curse_02"); End(s); Play(s, "curse_02");
            Play(s, "curse_13");
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(5));
            End(s); End(s);
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(3), "The consumed layer belongs to the earlier batch.");
            Play(s, "curse_10");
            Put(s, "curse_10");
            int energy = s.Battle.Energy;
            Assert.That(s.PlayCard(0), Is.False);
            Assert.That(s.Battle.Energy, Is.EqualTo(energy));
            End(s);
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(3));
            End(s);
            Assert.That(s.Battle.Boarding.CurseMarks, Is.Zero);
        }

        [Test]
        public void CurseGuardAndLink_ApplyMarksEvenWhenAttackIsFullyBlocked()
        {
            CoreVoyageSession s = Battle();
            Play(s, "curse_03");
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(3));
            Play(s, "curse_05");
            s.Battle.EnemyBlock = 100;
            int hp = s.Battle.EnemyBoardingHealth;
            Play(s, "curse_01");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp));
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(3));
            Play(s, "blood_07");
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(4), "A two-hit card consumes only one link charge.");
        }

        [Test]
        public void CurseContract_SeedsBeforeCounterAndIgnoresFullyBlockedAttack()
        {
            CoreVoyageSession s = Battle();
            Play(s, "curse_04"); Play(s, "curse_09");
            int hp = s.Battle.EnemyBoardingHealth;
            End(s);
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(2));
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - 4));
            s.Battle.PlayerBlock = 100;
            End(s);
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(2));
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - 8), "Counter triggers on blocked attacks, contract does not.");
        }

        [Test]
        public void CurseWeakness_UsesCurrentMarksAndCannotReduceMoreThanHalf()
        {
            CoreVoyageSession s = Battle();
            Play(s, "curse_02"); Play(s, "curse_02"); Play(s, "curse_07");
            int hp = s.Battle.PlayerBoardingHealth;
            string firstIntent = s.GetEnemyIntent();
            Assert.That(s.GetEnemyIntent(), Is.EqualTo(firstIntent), "Preview must not consume the debuff.");
            End(s);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(hp - 5));
            End(s);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(hp - 15), "The next full attack consumes it once.");
        }

        [Test]
        public void CurseArmor_RemovesCurrentGuardAndReducesNextPositiveGrantOnly()
        {
            CoreVoyageSession s = Battle();
            Play(s, "curse_02"); s.Battle.EnemyBlock = 20;
            Play(s, "curse_08");
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(10));
            // Normal guard turns provide 10 guard. The delayed reduction reads current marks (3).
            s.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
                1000, 0, 10, 10, 0, 28, 1, 1, healthMultiplier: 1);
            s.Battle.PhaseTurn = 2;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(5));
            s.Battle.PhaseTurn = 2;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(10));
        }

        [Test]
        public void CurseFinale_ConsumesAllMarksAndIgnoresGuardWithoutSeedingLinks()
        {
            CoreVoyageSession s = Battle();
            Play(s, "curse_02"); Play(s, "curse_02"); Play(s, "curse_05");
            s.Battle.EnemyBlock = 200;
            int hp = s.Battle.EnemyBoardingHealth;
            Play(s, "curse_11");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - 68));
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(200));
            Assert.That(s.Battle.Boarding.CurseMarks, Is.Zero);
        }

        [Test]
        public void CurseSupports_PayFixedMarksAndRespectSharedEnergyLimit()
        {
            CoreVoyageSession s = Battle();
            Play(s, "curse_02"); Play(s, "curse_02");
            s.Battle.PlayerBoardingHealth = 500; s.Cargo.Medicine = 1;
            Play(s, "curse_12", 2);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(513));
            Assert.That(s.Cargo.Medicine, Is.Zero);
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(4));
            Play(s, "curse_15", 2);
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(10));
            Play(s, "curse_14");
            Assert.That(s.Battle.Energy, Is.EqualTo(102));
            Play(s, "curse_02"); Put(s, "curse_14");
            Assert.That(s.PlayCard(0), Is.False);
        }

        [Test]
        public void CurseUnprovisionedHeal_DoesNotSpendMarks()
        {
            CoreVoyageSession s = Battle();
            Play(s, "curse_02"); s.Battle.PlayerBoardingHealth = 500;
            Play(s, "curse_12", 0);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(502));
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(3));
        }

        [Test]
        public void FixedResourceMedicine_InsufficientResourceFailsBeforeAnyPayment()
        {
            CoreVoyageSession s = Battle();
            s.Battle.PlayerBoardingHealth = 500; s.Cargo.Medicine = 2;
            foreach (string key in new[] { "curse_12", "tide_11", "tide_12" })
            {
                Put(s, key);
                Assert.That(s.PlayCard(0), Is.False, key);
                Assert.That(s.Battle.Energy, Is.EqualTo(100), key);
                Assert.That(s.Cargo.Medicine, Is.EqualTo(2), key);
                Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(500), key);
            }
        }

        [Test]
        public void CursePainAndBloodPursuit_ReadMarksAndActualPenetration()
        {
            CoreVoyageSession s = Battle();
            Play(s, "curse_02");
            int hp = s.Battle.EnemyBoardingHealth;
            Play(s, "curse_06");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - 11));
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(3));
            s.Battle.EnemyBlock = 6; Play(s, "blood_10");
            Assert.That(s.Battle.Hand.Count, Is.Zero);
            Play(s, "blood_10");
            Assert.That(s.Battle.Hand.Count, Is.EqualTo(1));
        }

        [Test]
        public void TideGuard_UsesOrdinaryGuardFirstAndDoesNotChargeWood()
        {
            CoreVoyageSession s = Battle();
            s.Cargo.Wood = 0;
            Play(s, "tide_02");
            s.Battle.PlayerBlock += 4;
            End(s);
            Assert.That(s.Battle.LastEnemyGuardAbsorbed, Is.EqualTo(10));
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(6));
            Assert.That(s.Battle.PlayerBlock, Is.Zero);
            Assert.That(s.Cargo.Wood, Is.Zero);
            End(s);
            Assert.That(s.Battle.Boarding.TidePower, Is.Zero, "Unused power expires at the next player end.");
        }

        [TestCase(8, 3, 0)]
        [TestCase(3, 0, 2)]
        public void MixedArcaneAndTideGuard_ConsumesOrdinaryThenRetainedThenTide(int attack, int expectedTide, int expectedRetained)
        {
            CoreVoyageSession s = Battle(attack);
            Play(s, "arcane_03"); Play(s, "arcane_08"); Play(s, "tide_02");
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(15));
            End(s);
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(expectedTide));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(expectedRetained));
            Assert.That(s.Battle.Boarding.ArcaneRetainedGuard, Is.EqualTo(expectedRetained));
            End(s);
            Assert.That(s.Battle.PlayerBlock, Is.Zero, "Retained guard cannot survive a second reset.");
        }

        [Test]
        public void EnteringBoarding_CapsCarriedManaWithoutRefillingAndClearsOldTargetState()
        {
            var s = new CoreVoyageSession();
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            typeof(CoreSchoolBattleState).GetProperty("ManaLimit").SetValue(s.Battle.Schools, 10);
            typeof(CoreSchoolBattleState).GetProperty("Mana").SetValue(s.Battle.Schools, 8);
            typeof(CoreSchoolBattleState).GetProperty("CurseMarks").SetValue(s.Battle.Schools, 5);
            typeof(CoreSchoolBattleState).GetProperty("CurseDamagePerMark").SetValue(s.Battle.Schools, 4);
            s.Battle.DistanceMeters = 100f;
            typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(s.Battle, s.Battle.EnemyHeadingDegrees);
            s.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(s.TryBoard(), Is.True);
            Assert.That(s.Battle.Schools.ManaLimit, Is.EqualTo(3));
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(3));
            Assert.That(s.Battle.Schools.CurseMarks, Is.Zero);
            Assert.That(s.Battle.Schools.CurseDamagePerMark, Is.Zero);
            Assert.That(s.Battle.Boarding.CurseMarks, Is.Zero);
            Assert.That(s.Battle.Boarding.TidePower, Is.Zero);
        }

        [Test]
        public void TideRetention_ExtendsEachPointOnceAndSpendingUsesEarliestExpiration()
        {
            CoreVoyageSession s = Battle(20);
            Play(s, "tide_02"); Play(s, "tide_01"); End(s);
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(12));
            Play(s, "tide_05", 6);
            Play(s, "tide_15");
            Assert.That(s.Battle.Boarding.ExtendableTidePower, Is.Zero, "Spending removes the unextended points first.");
            End(s);
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(6));
            Put(s, "tide_05");
            Assert.That(s.PlayCard(0, new CoreBoardingPlayOptions { ResourceAmount = 1 }), Is.False);
            End(s);
            Assert.That(s.Battle.Boarding.TidePower, Is.Zero);
        }

        [Test]
        public void TideCashoutGuard_CannotGenerateNewPower()
        {
            CoreVoyageSession s = Battle();
            Play(s, "tide_02"); End(s); Play(s, "tide_15");
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(8));
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(4));
            End(s);
            Assert.That(s.Battle.Boarding.TidePower, Is.Zero);
        }

        [Test]
        public void TideSmallCounterAndHeal_UseActualStoredPower()
        {
            CoreVoyageSession s = Battle();
            Play(s, "tide_01"); End(s);
            int enemy = s.Battle.EnemyBoardingHealth;
            Play(s, "tide_06");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 10));
            Assert.That(s.Battle.Boarding.TidePower, Is.Zero);
            s.Battle.EnemyBlock = 7; Play(s, "tide_04");
            Assert.That(s.Battle.EnemyBlock, Is.Zero);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 13));
            Play(s, "tide_02"); End(s);
            s.Battle.PlayerBoardingHealth = 500; s.Cargo.Medicine = 1;
            Play(s, "tide_11");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(510));
            Assert.That(s.Cargo.Medicine, Is.Zero);
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(4));
        }

        [Test]
        public void TideAttacks_ConsumeAllPowerAndIgnoreClientResourceAmounts()
        {
            CoreVoyageSession s = Battle(20);
            Play(s, "tide_02"); Play(s, "tide_01"); End(s);
            int hp = s.Battle.EnemyBoardingHealth;
            Play(s, "tide_07", 4);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - 28));
            Assert.That(s.Battle.Boarding.TidePower, Is.Zero);
            Play(s, "tide_02"); Play(s, "tide_01"); End(s);
            Play(s, "tide_08");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - 76));
            Assert.That(s.Battle.Boarding.TidePower, Is.Zero);
        }

        [Test]
        public void TideAmbushAndWeakness_ResolveAfterOneReducedEnemyAttack()
        {
            CoreVoyageSession s = Battle();
            Play(s, "tide_02"); End(s); Play(s, "tide_09"); Play(s, "tide_10");
            int player = s.Battle.PlayerBoardingHealth, enemy = s.Battle.EnemyBoardingHealth;
            End(s);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(player - 7));
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 16));
            End(s);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 16));
        }

        [Test]
        public void TideMedicineBath_PaysOnceAndHealsOnTwoPlayerEnds()
        {
            CoreVoyageSession s = Battle();
            Play(s, "tide_02"); End(s);
            s.Battle.PlayerBoardingHealth = 500; s.Cargo.Medicine = 1;
            Play(s, "tide_12", 8);
            Assert.That(s.Cargo.Medicine, Is.Zero);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(500));
            Put(s, "tide_12"); Assert.That(s.PlayCard(0), Is.False);
            s.Battle.PlayerBlock = 20; End(s);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(508));
            s.Battle.PlayerBlock = 20; End(s);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(516));
            s.Battle.PlayerBlock = 20; End(s);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(516));
        }

        [Test]
        public void TideSupportCards_ConsumePowerAndDrawWithinHandLimit()
        {
            CoreVoyageSession s = Battle();
            Play(s, "tide_03"); Assert.That(s.Battle.Hand.Count, Is.EqualTo(1));
            Play(s, "tide_02"); End(s); Play(s, "tide_13");
            Assert.That(s.Battle.Hand.Count, Is.EqualTo(3));
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(2));
            Play(s, "tide_02"); End(s); Play(s, "tide_14");
            Assert.That(s.Battle.Hand.Count, Is.EqualTo(1));
            Assert.That(s.Battle.Energy, Is.EqualTo(101));
        }

        [Test]
        public void BloodCosts_ArePaidBeforeDesperateBonusAndCannotBeShielded()
        {
            CoreVoyageSession s = Battle();
            s.Battle.PlayerBoardingHealth = 402;
            s.Battle.PlayerBlock = 100;
            Play(s, "blood_15");
            int enemy = s.Battle.EnemyBoardingHealth;
            Play(s, "blood_01");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(399));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(100));
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 24));
            s.Battle.PlayerBoardingHealth = 3; Put(s, "blood_03");
            int energy = s.Battle.Energy;
            Assert.That(s.PlayCard(0), Is.False);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(3));
            Assert.That(s.Battle.Energy, Is.EqualTo(energy));
        }

        [Test]
        public void BloodFixedLifePayment_CannotBeReducedByClientAndSharesEnergyLimit()
        {
            CoreVoyageSession s = Battle();
            int enemy = s.Battle.EnemyBoardingHealth;
            Play(s, "blood_02", 1);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(996));
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 34));
            Play(s, "blood_03"); Assert.That(s.Battle.PlayerBlock, Is.EqualTo(11));
            Play(s, "blood_04"); Assert.That(s.Battle.Hand.Count, Is.EqualTo(3));
            Play(s, "blood_05");
            Assert.That(s.Battle.Energy, Is.EqualTo(102));
            Put(s, "blood_05"); Assert.That(s.PlayCard(0), Is.False);
        }

        [Test]
        public void BloodDrain_UsesActualDamageAndOnlySuccessfulHealingConsumesTheOpportunity()
        {
            CoreVoyageSession s = Battle();
            Play(s, "blood_06"); // Full health: no successful healing yet.
            s.Battle.PlayerBoardingHealth = 500; s.Battle.EnemyBlock = 8;
            Play(s, "blood_06");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(501));
            Play(s, "blood_06");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(501), "Copies share the successful-heal cap.");
            End(s); s.Battle.PlayerBlock = 0;
            int hp = s.Battle.PlayerBoardingHealth;
            Play(s, "blood_06");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(hp + 2));
        }

        [Test]
        public void BloodTwoHitAttack_ChecksFirstHitPenetrationAndAppliesOathOnlyOnce()
        {
            CoreVoyageSession s = Battle();
            s.Battle.PlayerBoardingHealth = 400; Play(s, "blood_15");
            s.Battle.EnemyBlock = 20;
            int hp = s.Battle.EnemyBoardingHealth;
            Play(s, "blood_07");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - 12), "First 12+8 is entirely blocked: second hit remains 12.");
            Play(s, "blood_07");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - 40));
        }

        [Test]
        public void BloodDebt_TriggersOnEnemyLifeLossButNotLifeCostsOrBlockedAttacks()
        {
            CoreVoyageSession s = Battle();
            Play(s, "blood_08");
            int enemy = s.Battle.EnemyBoardingHealth;
            Play(s, "blood_05");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy));
            s.Battle.PlayerBlock = 100; End(s);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy));
            End(s);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 8));
            End(s);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 8));
        }

        [Test]
        public void BloodDesperateHealingAndDelayedDamage_LockInAtPlay()
        {
            CoreVoyageSession s = Battle();
            s.Battle.PlayerBoardingHealth = 400;
            Play(s, "blood_13");
            Play(s, "blood_09");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(403));
            int enemy = s.Battle.EnemyBoardingHealth;
            s.Battle.PlayerBlock = 100; End(s);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 18));
            s.Battle.PlayerBoardingHealth = 400; s.Cargo.Medicine = 1;
            Play(s, "blood_09");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(409));
            Assert.That(s.Cargo.Medicine, Is.Zero);
        }

        [Test]
        public void BloodDesperateCards_HaveFixedBonusesAndLastResortIsOncePerBattle()
        {
            CoreVoyageSession s = Battle();
            s.Battle.PlayerBoardingHealth = 400;
            int enemy = s.Battle.EnemyBoardingHealth;
            Play(s, "blood_11"); Play(s, "blood_12");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 14));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(8));
            Play(s, "blood_14");
            Assert.That(s.Battle.Hand.Count, Is.EqualTo(1));
            End(s); Put(s, "blood_14");
            Assert.That(s.PlayCard(0), Is.False);
        }

        private static CoreVoyageSession Battle(int attack = 10)
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            s.SetSeed("BOARDING-CTB-TEST");
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 1000;
            s.Battle.DistanceMeters = 100f;
            typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(s.Battle, s.Battle.EnemyHeadingDegrees);
            s.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(s.TryBoard(), Is.True);
            s.Battle.PlayerBoardingHealth = s.Battle.PlayerMaxBoardingHealth = 1000;
            s.Battle.EnemyBoardingHealth = s.Battle.EnemyMaxBoardingHealth = 1000;
            s.Battle.EnemyBlock = 0;
            s.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
                1000, 0, attack, 0, 0, 28, 1, 1, healthMultiplier: 1);
            typeof(CoreVoyageSession).GetProperty("Morale").SetValue(s, 0);
            s.Cargo.Medicine = 0; s.Cargo.Wood = 0;
            return s;
        }

        private static int Index(string key) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == "card_boarding_" + key);

        private static void Put(CoreVoyageSession s, string key)
        {
            s.Battle.Hand.Clear(); s.Battle.Hand.Add(Index(key)); s.Battle.Energy = 100;
        }

        private static void Play(CoreVoyageSession s, string key, int amount = 0)
        {
            Put(s, key);
            Assert.That(s.PlayCard(0, new CoreBoardingPlayOptions { ResourceAmount = amount }), Is.True, key);
        }

        private static void End(CoreVoyageSession s)
        {
            s.Battle.PhaseTurn = 1; // Keep this fixture on the enemy's attack action.
            Assert.That(s.EndBattleTurn(), Is.True);
        }
    }
}
