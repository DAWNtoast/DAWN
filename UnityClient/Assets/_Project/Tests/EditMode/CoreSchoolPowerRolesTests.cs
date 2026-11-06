using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreSchoolPowerRolesTests
    {
        [TestCase("card_school_tide_b04", 0, 9)]
        [TestCase("card_school_tide_b04", 3, 32)]
        [TestCase("card_school_tide_n03", 0, 8)]
        [TestCase("card_school_tide_n03", 2, 18)]
        public void TideDefenseCreatesReserveOnlyAsTheNewGuardActuallyAbsorbsDamage(string id, int rank, int guard)
        {
            var s = Start(id, rank);
            s.Battle.PlayerBlock = 4;
            Set(s.Battle.Schools, "TidePower", 7);
            s.Cargo.Wood = 1;
            Play(s, id);
            int totalGuard = guard + 4;
            Assert.That(s.Cargo.Wood, Is.Zero);
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(totalGuard));
            Assert.That(s.Battle.Schools.TidePaidBlock, Is.EqualTo(totalGuard));
            Assert.That(s.Battle.Schools.TidePower, Is.EqualTo(7), "Casting or converting guard does not invent tide.");

            int hull = s.Hull, absorbed = Math.Min(20, totalGuard);
            HitPlayer(s, 20, 0);
            Assert.That(s.Hull, Is.EqualTo(hull - (20 - absorbed)));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(totalGuard - absorbed));
            Assert.That(s.Battle.Schools.TidePower, Is.EqualTo(7 + absorbed));
            HitPlayer(s, 7, 7);
            Assert.That(s.Hull, Is.EqualTo(hull - (20 - absorbed) - 7));
            Assert.That(s.Battle.Schools.TidePower, Is.EqualTo(7 + absorbed), "Unblocked health damage never awards tide.");
            HitPlayer(s, 100, 0);
            Assert.That(s.Battle.PlayerBlock, Is.Zero);
            Assert.That(s.Battle.Schools.TidePaidBlock, Is.Zero);
            Assert.That(s.Battle.Schools.TidePower, Is.EqualTo(7 + totalGuard));
        }

        [TestCase("card_school_tide_b04", 3)]
        [TestCase("card_school_tide_n03", 2)]
        public void UpgradedTideDefenseWithoutWoodStillGivesOnlyTwoOrdinaryGuard(string id, int rank)
        {
            var s = Start(id, rank);
            s.Cargo.Wood = 0;
            s.Battle.PlayerBlock = 4;
            Play(s, id);
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(6));
            Assert.That(s.Battle.Schools.TidePaidBlock, Is.Zero);
            HitPlayer(s, 6, 0);
            Assert.That(s.Battle.PlayerBlock, Is.Zero);
            Assert.That(s.Battle.Schools.TidePower, Is.Zero);
        }

        [TestCase(0, 5)] [TestCase(2, 20)] [TestCase(3, 25)]
        public void CurseDetonationBypassesLargeGuardAndPreservesItsMarksForTheNextCast(int rank, int damage)
        {
            const string id = "card_school_curse_b05";
            var s = Start(id, rank);
            s.ApplyNavalEnemyCurseMarks(5);
            s.Battle.EnemyBlock = 1000;
            int enemy = s.Battle.EnemyHull;
            Play(s, id);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(enemy - damage));
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(1000), "Curse damage answers shields without consuming them.");
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(5));
        }

        [TestCase(11, false)]
        [TestCase(10, true)]
        public void PiercingCurseDamageExecutesAtEqualityBeforeLaterAttachedFire(int hull, bool executes)
        {
            const string id = "card_school_curse_b05";
            var s = Start(id, 0);
            s.Battle.EnemyHull = hull;
            s.Battle.EnemyBlock = 1000;
            s.ApplyNavalEnemyCurseMarks(5);
            s.Battle.PlayerFireRounds = 7;
            Play(s, id);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(executes ? 0 : 6), "Five marks survive at six remaining hull and execute when damage leaves exactly five.");
            Assert.That(s.Battle.EnemyNavalExecutedByCurse, Is.EqualTo(executes));
            if (executes) Assert.That(s.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.Victory));
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(5));
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(1000));
            Assert.That(s.Battle.LastAction, Does.Contain(executes ? "已造成 5 点实际伤害" : "衍生伤害 5"));
            Assert.That(s.Battle.PlayerFireRounds, Is.EqualTo(executes ? 7 : 0), "Only execution stops the later attached-fire effect.");
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(executes ? 0 : 7));
        }

        [TestCase(0, 14)] [TestCase(3, 90)]
        public void BloodCannonPaysFiveForItsUpgradeRewardAndOnlyThatPaymentGeneratesPoolGuard(int rank, int bonus)
        {
            const string id = "card_school_blood_b03";
            var s = Start(id, rank);
            Set(s, "Hull", 20);
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Blood, true, 0, 0, false, 0, 0), Is.True);
            CoreBattleCard card = s.GetBattleCard(Index(id));
            int expected = CoreNavalStatRules.ScaleCannonDamage(card.Damage, s.EffectiveFirepower) + bonus;
            Assert.That(s.GetBattleCardHealthCost(card), Is.EqualTo(5));
            int enemy = s.Battle.EnemyHull;
            Play(s, id);
            Assert.That(s.Hull, Is.EqualTo(15));
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(enemy - expected));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(10), "The five-point payment receives the pool's double rebate.");
            Assert.That(s.Cargo.Powder, Is.EqualTo(19));
            Assert.That(s.Cargo.Cannonballs, Is.EqualTo(19));

            HitPlayer(s, 12, 0); // Exhaust the ten-point rebate and still lose two hull, without another rebate.
            Assert.That(s.Hull, Is.EqualTo(13));
            Assert.That(s.Battle.PlayerBlock, Is.Zero, "An attack loss is not an active life payment.");
            s.Battle.PlayerBurnStacks = 4;
            Invoke(s, "ApplyNavalPlayerBurn");
            Assert.That(s.Hull, Is.EqualTo(9));
            Assert.That(s.Battle.PlayerBlock, Is.Zero, "Burn losses cannot regenerate blood-pool guard.");
        }

        [TestCase(0)] [TestCase(3)]
        public void BloodCannonAtFiveHullKeepsTheBaseAttackWithoutPartialPaymentOrPoolReward(int rank)
        {
            const string id = "card_school_blood_b03";
            var s = Start(id, rank);
            Set(s, "Hull", 5);
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Blood, false, 0, 0, false, 0, 0), Is.True);
            CoreBattleCard card = s.GetBattleCard(Index(id));
            Assert.That(s.GetBattleCardHealthCost(card), Is.Zero);
            Assert.That(s.HasBloodPaidBenefit(card), Is.False);
            int enemy = s.Battle.EnemyHull;
            Play(s, id);
            Assert.That(s.Hull, Is.EqualTo(5));
            Assert.That(s.Battle.PlayerBlock, Is.Zero);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(enemy - CoreNavalStatRules.ScaleCannonDamage(card.Damage, s.EffectiveFirepower)));
            Assert.That(s.Cargo.Powder + s.Cargo.Cannonballs, Is.EqualTo(38));
        }

        [TestCase(0, false, 0)] [TestCase(0, true, 16)]
        [TestCase(2, false, 0)] [TestCase(2, true, 75)]
        public void UpgradedDesperateCannonRewardsOnlyAffordableDesperationBeforePayment(int rank, bool desperate, int bonus)
        {
            const string id = "card_school_blood_n03";
            var s = Start(id, rank);
            int health = s.MaxHull * 40 / 100 + (desperate ? 0 : 1);
            Set(s, "Hull", health);
            CoreBattleCard card = s.GetBattleCard(Index(id));
            Assert.That(s.GetBattleCardHealthCost(card), Is.EqualTo(desperate ? 5 : 0));
            int enemy = s.Battle.EnemyHull;
            Play(s, id);
            Assert.That(s.Hull, Is.EqualTo(health - (desperate ? 5 : 0)));
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(enemy - CoreNavalStatRules.ScaleCannonDamage(card.Damage, s.EffectiveFirepower) - bonus));
        }

        private static CoreVoyageSession Start(string id, int rank)
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            s.ConfigureCardUpgrades(new Dictionary<string, int> { { id, rank } });
            s.ConfigureComponentBonuses(2000, 0, 49, 0, 0);
            s.SetSeed("SCHOOL-POWER-ROLES-20261024");
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[4]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            Pose(s, true, 0, 0); Pose(s, false, 2, 0);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 10000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = s.Battle.PlayerLoadedShot = 0;
            s.Battle.Energy = 20;
            s.Cargo.Powder = s.Cargo.Cannonballs = 20;
            s.Battle.DrawPile.Clear(); s.Battle.DiscardPile.Clear();
            Set(s, "Morale", 0);
            return s;
        }

        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
        private static void Play(CoreVoyageSession s, string id)
        {
            s.Battle.Hand.Clear(); s.Battle.Hand.Add(Index(id));
            Assert.That(s.PlayCard(0), Is.True, id + ": " + s.CardUnavailableReason(0));
        }
        private static void HitPlayer(CoreVoyageSession s, int damage, int piercing) =>
            Invoke(s, "NavalExpansionDirectDamage", true, damage, piercing);
        private static object Invoke(object target, string method, params object[] args) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        private static void Set(object target, string name, object value) => target.GetType()
            .GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        private static void Pose(CoreVoyageSession s, bool player, int x, int y) => typeof(CoreBattleState)
            .GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s.Battle,
                new object[] { player, new VoyagePoint(x * 100, y * 100), player ? 0f : 180f });
    }
}
