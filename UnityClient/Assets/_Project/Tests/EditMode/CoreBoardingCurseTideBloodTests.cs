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
        public void CurseMarks_AddWithoutTheOldCapAndPersistWithoutPeriodicDamage()
        {
            CoreVoyageSession s = Battle();
            Play(s, "curse_02");
            End(s);
            Play(s, "curse_02");
            Play(s, "curse_02");
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(18));
            int hp = s.Battle.EnemyBoardingHealth;
            for (int turn = 0; turn < 4; turn++)
            {
                End(s);
                Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(18));
                Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp));
            }
            Assert.That(s.Battle.Schools.CurseDamagePerMark, Is.Zero);
        }

        [Test]
        public void CurseConsumption_RemovesOnlyPaidLayersAndEntanglementChangesThreshold()
        {
            CoreVoyageSession s = Battle();
            Play(s, "curse_02"); End(s); Play(s, "curse_02");
            Play(s, "curse_13");
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(10));
            int enemy = s.Battle.EnemyBoardingHealth;
            Play(s, "curse_10");
            Put(s, "curse_10");
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy));
            Assert.That(s.Battle.Schools.EnemyCurseExecutionThresholdPercent, Is.EqualTo(90));
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(10));
            End(s);
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(10));
            End(s); End(s); End(s);
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(10));
        }

        [Test]
        public void CurseGuardAndLink_ApplyMarksEvenWhenAttackIsFullyBlocked()
        {
            CoreVoyageSession s = Battle();
            Play(s, "curse_03");
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(6));
            Play(s, "curse_05");
            s.Battle.EnemyBlock = 100;
            int hp = s.Battle.EnemyBoardingHealth;
            Play(s, "curse_01");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp));
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(6));
            Play(s, "blood_07");
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(8), "A two-hit card consumes only one link charge.");
        }

        [Test]
        public void CurseContract_SeedsAfterDamageWhileCounterPrecedesAttack()
        {
            CoreVoyageSession s = Battle();
            Play(s, "curse_04"); Play(s, "curse_09");
            int hp = s.Battle.EnemyBoardingHealth;
            End(s);
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(4));
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp));
            s.Battle.PlayerBlock = 100;
            End(s);
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(4));
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - 8), "Only the pre-attack counter causes damage; marks persist.");
        }

        [Test]
        public void CurseWeakness_UsesCurrentMarksOnEveryAttackInItsWindow()
        {
            CoreVoyageSession s = Battle();
            Play(s, "curse_02"); Play(s, "curse_02"); Play(s, "curse_07");
            int hp = s.Battle.PlayerBoardingHealth;
            string firstIntent = s.GetEnemyIntent();
            Assert.That(s.GetEnemyIntent(), Is.EqualTo(firstIntent), "Preview must not consume the debuff.");
            End(s);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(hp));
            End(s);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(hp), "The second turn still reads twelve persistent curse layers.");
        }

        [Test]
        public void CurseArmor_RemovesCurrentGuardAndReducesEveryGrantInItsWindow()
        {
            CoreVoyageSession s = Battle();
            Play(s, "curse_02"); s.Battle.EnemyBlock = 20;
            Play(s, "curse_08");
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(0));
            // Normal guard turns provide 10 guard. The delayed reduction reads current marks (6).
            s.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
                1000, 0, 10, 10, 0, 28, 1, 1, healthMultiplier: 1);
            s.Battle.PhaseTurn = 2;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(0));
            s.Battle.PhaseTurn = 2;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(0));
        }

        [Test]
        public void CurseFinale_ConsumesAllMarksAndIgnoresGuardWithoutSeedingLinks()
        {
            CoreVoyageSession s = Battle();
            Play(s, "curse_02"); Play(s, "curse_02"); Play(s, "curse_05");
            s.Battle.EnemyBlock = 200;
            int hp = s.Battle.EnemyBoardingHealth;
            Play(s, "curse_11");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - 136));
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(200));
            Assert.That(s.Battle.Boarding.CurseMarks, Is.Zero);
        }

        [Test]
        public void CurseSupports_RequireMarksWithoutConsumingThemAndAllowRepeatedEnergyConversion()
        {
            CoreVoyageSession s = Battle();
            Play(s, "curse_02"); Play(s, "curse_02");
            s.Battle.PlayerBoardingHealth = 500; s.Cargo.Medicine = 1;
            Play(s, "curse_12", 2);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(526));
            Assert.That(s.Cargo.Medicine, Is.Zero);
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(12));
            Play(s, "curse_15", 2);
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(20));
            Play(s, "curse_14");
            Assert.That(s.Battle.Energy, Is.EqualTo(102));
            Play(s, "curse_02"); Put(s, "curse_14");
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(18));
        }

        [Test]
        public void CurseUnprovisionedHeal_DoesNotSpendMarks()
        {
            CoreVoyageSession s = Battle();
            Play(s, "curse_02"); s.Battle.PlayerBoardingHealth = 500;
            Play(s, "curse_12", 0);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(502));
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(6));
        }

        [Test]
        public void TideMedicine_InsufficientTideKeepsMedicineAndUsesBaseEffect()
        {
            CoreVoyageSession s = Battle();
            s.Battle.PlayerBoardingHealth = 500; s.Cargo.Medicine = 2;
            foreach (string key in new[] { "tide_11", "tide_12" })
            {
                Put(s, key);
                Assert.That(s.PlayCard(0), Is.True, key);
                Assert.That(s.Battle.Energy, Is.EqualTo(key == "tide_11" ? 99 : 98), key);
                Assert.That(s.Cargo.Medicine, Is.EqualTo(2), key);
                Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(502), key);
            }
        }

        [Test]
        public void CursePainAndBloodPursuit_ReadMarksAndActualPenetration()
        {
            CoreVoyageSession s = Battle();
            Play(s, "curse_02");
            int hp = s.Battle.EnemyBoardingHealth;
            Play(s, "curse_06");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - 18));
            Assert.That(s.Battle.Boarding.CurseMarks, Is.EqualTo(6));
            s.Battle.EnemyBlock = 12; Play(s, "blood_10");
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
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(6), "Unused power remains for the rest of the battle.");
        }

        [TestCase(17, 3, 0)]
        [TestCase(3, 0, 11)]
        public void MixedArcaneAndTideGuard_ConsumesOrdinaryThenRetainedThenTide(int attack, int expectedTide, int expectedRetained)
        {
            CoreVoyageSession s = Battle(attack);
            Play(s, "arcane_03"); Play(s, "arcane_08"); Play(s, "tide_02");
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(44));
            End(s);
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(expectedTide));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(expectedRetained));
            Assert.That(s.Battle.Boarding.ArcaneRetainedGuard, Is.EqualTo(expectedRetained));
            End(s);
            Assert.That(s.Battle.PlayerBlock, Is.Zero, "Retained guard cannot survive a second reset.");
        }

        [Test]
        public void EnteringBoarding_PreservesManaAndLimitAndClearsOldTargetState()
        {
            var s = new CoreVoyageSession();
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            typeof(CoreSchoolBattleState).GetProperty("ManaLimit").SetValue(s.Battle.Schools, 10);
            typeof(CoreSchoolBattleState).GetProperty("Mana").SetValue(s.Battle.Schools, 8);
            typeof(CoreSchoolBattleState).GetProperty("CurseMarks").SetValue(s.Battle.Schools, 5);
            typeof(CoreSchoolBattleState).GetProperty("CurseDamagePerMark").SetValue(s.Battle.Schools, 4);
            s.Battle.DistanceMeters = 100f;
            typeof(CoreBattleState).GetProperty("EnemyHeadingDegrees").SetValue(s.Battle, 90f);
            typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(s.Battle, s.Battle.EnemyHeadingDegrees);
            s.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(s.TryBoard(), Is.True);
            Assert.That(s.Battle.Schools.ManaLimit, Is.EqualTo(10));
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(8));
            Assert.That(s.Battle.Schools.CurseMarks, Is.Zero);
            Assert.That(s.Battle.Schools.CurseDamagePerMark, Is.Zero);
            Assert.That(s.Battle.Boarding.CurseMarks, Is.Zero);
            Assert.That(s.Battle.Boarding.TidePower, Is.Zero);
        }

        [Test]
        public void LeaveTideConvertsCashoutGuardAndPersistsWithoutGeneratingFreePower()
        {
            CoreVoyageSession s = Battle(20);
            Play(s, "tide_02"); Play(s, "tide_01"); End(s);
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(20));
            Play(s, "tide_05", 6);
            Play(s, "tide_15");
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(16));
            End(s);
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(32), "The converted cashout guard absorbs damage and replenishes tide.");
            Put(s, "tide_05");
            Assert.That(s.PlayCard(0, new CoreBoardingPlayOptions { ResourceAmount = 1 }), Is.True);
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(32), "Reapplying conversion does not grant free power.");
            End(s);
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(32));
        }

        [Test]
        public void TideCashoutGuard_CannotGenerateNewPower()
        {
            CoreVoyageSession s = Battle();
            Play(s, "tide_02"); End(s); Play(s, "tide_15");
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(16));
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(6));
            End(s);
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(6));
        }

        [Test]
        public void TideSmallCounterAndHeal_UseActualStoredPower()
        {
            CoreVoyageSession s = Battle(12);
            Play(s, "tide_01"); End(s);
            int enemy = s.Battle.EnemyBoardingHealth;
            Play(s, "tide_06");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 22));
            Assert.That(s.Battle.Boarding.TidePower, Is.Zero);
            s.Battle.EnemyBlock = 7; Play(s, "tide_04");
            Assert.That(s.Battle.EnemyBlock, Is.Zero);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 34));
            Play(s, "tide_02"); End(s);
            s.Battle.PlayerBoardingHealth = 500; s.Cargo.Medicine = 1;
            Play(s, "tide_11");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(520));
            Assert.That(s.Cargo.Medicine, Is.Zero);
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(0));
        }

        [Test]
        public void TideAttacks_ConsumeAllPowerAndIgnoreClientResourceAmounts()
        {
            CoreVoyageSession s = Battle(20);
            Play(s, "tide_02"); Play(s, "tide_01"); End(s);
            int hp = s.Battle.EnemyBoardingHealth;
            Play(s, "tide_07", 4);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - 52));
            Assert.That(s.Battle.Boarding.TidePower, Is.Zero);
            Play(s, "tide_02"); Play(s, "tide_01"); End(s);
            Play(s, "tide_08");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - 142));
            Assert.That(s.Battle.Boarding.TidePower, Is.Zero);
        }

        [Test]
        public void TideAmbushAndWeakness_ResolveAfterOneReducedEnemyAttack()
        {
            CoreVoyageSession s = Battle(20);
            Play(s, "tide_02"); End(s); Play(s, "tide_09"); Play(s, "tide_10");
            int player = s.Battle.PlayerBoardingHealth, enemy = s.Battle.EnemyBoardingHealth;
            End(s);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(player - 14));
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 32));
            End(s);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 32));
        }

        [Test]
        public void TideMedicineBath_PaysPerPlayAndAddsToDecayingRecovery()
        {
            CoreVoyageSession s = Battle(20);
            Play(s, "tide_02"); End(s);
            s.Battle.PlayerBoardingHealth = 500; s.Cargo.Medicine = 1;
            Play(s, "tide_12", 8);
            Assert.That(s.Cargo.Medicine, Is.Zero);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(500));
            Put(s, "tide_12"); Assert.That(s.PlayCard(0), Is.True);
            s.Battle.PlayerBlock = 20; End(s);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(513));
            s.Battle.PlayerBlock = 20; End(s);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(525));
            s.Battle.PlayerBlock = 20; End(s);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(536));
        }

        [Test]
        public void TideSupportCards_ConsumePowerAndDrawWithinHandLimit()
        {
            CoreVoyageSession s = Battle(20);
            Play(s, "tide_03"); Assert.That(s.Battle.Hand.Count, Is.EqualTo(1));
            Play(s, "tide_02"); End(s); Play(s, "tide_13");
            Assert.That(s.Battle.Hand.Count, Is.EqualTo(3));
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(4));
            Play(s, "tide_02"); End(s); Play(s, "tide_14");
            Assert.That(s.Battle.Hand.Count, Is.EqualTo(1));
            Assert.That(s.Battle.Energy, Is.EqualTo(101));
        }

        [Test]
        public void BloodCosts_ArePaidBeforeDesperateBonusAndCannotBeShielded()
        {
            CoreVoyageSession s = Battle();
            s.Battle.PlayerBoardingHealth = 400;
            s.Battle.PlayerBlock = 100;
            Play(s, "blood_15");
            s.Battle.PlayerBoardingHealth = 402;
            int enemy = s.Battle.EnemyBoardingHealth;
            Play(s, "blood_01");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(396));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(100));
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 48));
            s.Battle.PlayerBoardingHealth = 3; Put(s, "blood_03");
            int energy = s.Battle.Energy;
            Assert.That(s.PlayCard(0), Is.False);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(3));
            Assert.That(s.Battle.Energy, Is.EqualTo(energy));
        }

        [Test]
        public void BloodFixedLifePayment_CannotBeReducedByClientAndAllowsRepeatedEnergyCards()
        {
            CoreVoyageSession s = Battle();
            int enemy = s.Battle.EnemyBoardingHealth;
            Play(s, "blood_02", 1);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(992));
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 68));
            Play(s, "blood_03"); Assert.That(s.Battle.PlayerBlock, Is.EqualTo(22));
            Play(s, "blood_04"); Assert.That(s.Battle.Hand.Count, Is.EqualTo(3));
            Play(s, "blood_05");
            Assert.That(s.Battle.Energy, Is.EqualTo(102));
            Put(s, "blood_05"); Assert.That(s.PlayCard(0), Is.True);
        }

        [Test]
        public void BloodDrain_UsesAllActualHealthDamageOnEveryPlay()
        {
            CoreVoyageSession s = Battle();
            Play(s, "blood_06");
            s.Battle.PlayerBoardingHealth = 500; s.Battle.EnemyBlock = 8;
            Play(s, "blood_06");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(510));
            Play(s, "blood_06");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(528));
            End(s); s.Battle.PlayerBlock = 0;
            int hp = s.Battle.PlayerBoardingHealth;
            Play(s, "blood_06");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(hp + 18));
        }

        [Test]
        public void BloodTwoHitAttack_ChecksFirstHitPenetrationAndAppliesOathOnlyOnce()
        {
            CoreVoyageSession s = Battle();
            s.Battle.PlayerBoardingHealth = 400; Play(s, "blood_15");
            s.Battle.EnemyBlock = 40;
            int hp = s.Battle.EnemyBoardingHealth;
            Play(s, "blood_07");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - 24), "First 24+16 is entirely blocked: second hit remains24.");
            Play(s, "blood_07");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(hp - 96));
        }

        [Test]
        public void BloodDebt_TriggersOnEnemyAttackIncludingBlockedDamageButNotLifeCosts()
        {
            CoreVoyageSession s = Battle();
            Play(s, "blood_08");
            int enemy = s.Battle.EnemyBoardingHealth;
            Play(s, "blood_05");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy));
            s.Battle.PlayerBlock = 100; End(s);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 16));
            End(s);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 32));
            End(s);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 32));
        }

        [Test]
        public void BloodDesperateHealingAndDelayedDamage_LockInAtPlay()
        {
            CoreVoyageSession s = Battle();
            s.Battle.PlayerBoardingHealth = 400;
            Play(s, "blood_13");
            Play(s, "blood_09");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(405));
            int enemy = s.Battle.EnemyBoardingHealth;
            s.Battle.PlayerBlock = 100; End(s);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 44));
            s.Battle.PlayerBoardingHealth = 400; s.Cargo.Medicine = 1;
            Play(s, "blood_09");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(426));
            Assert.That(s.Cargo.Medicine, Is.Zero);
        }

        [Test]
        public void BloodDesperateCards_HaveRevisedBonusesAndLastResortExhaustsEachCopy()
        {
            CoreVoyageSession s = Battle();
            s.Battle.PlayerBoardingHealth = 400;
            int enemy = s.Battle.EnemyBoardingHealth;
            Play(s, "blood_11"); Play(s, "blood_12");
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 34));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(24));
            Play(s, "blood_14");
            Assert.That(s.Battle.Hand.Count, Is.EqualTo(1));
            End(s); Put(s, "blood_14");
            Assert.That(s.PlayCard(0), Is.True);
        }

        private static CoreVoyageSession Battle(int attack = 10)
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            s.SetSeed("BOARDING-CTB-TEST");
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 1000;
            s.Battle.DistanceMeters = 100f;
            typeof(CoreBattleState).GetProperty("EnemyHeadingDegrees").SetValue(s.Battle, 90f);
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
