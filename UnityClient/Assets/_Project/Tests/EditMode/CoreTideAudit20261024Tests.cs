using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreTideAudit20261024Tests
    {
        [TestCase(19, 100, 119)]
        [TestCase(int.MaxValue - 2, 10, int.MaxValue)]
        public void NavalAbsorptionAccumulatesAllTideIgnoringLegacyCap(int before, int absorbed, int expected)
        {
            var s = Start(false);
            Set(s.Battle.Schools, "TideLimit", 20);
            Set(s.Battle.Schools, "TidePower", before);
            Set(s.Battle.Schools, "TidePaidBlock", absorbed);
            s.Battle.PlayerBlock = 0;
            Invoke(s, "RecordPaidTideGuard", absorbed);
            Assert.That(s.Battle.Schools.TidePower, Is.EqualTo(expected));
            Assert.That(s.Battle.Schools.TideLimit, Is.EqualTo(int.MaxValue));
            Assert.That(s.Battle.Schools.TidePaidBlock, Is.Zero);
        }

        [Test]
        public void BoardingUsesAggregateStorageAndCanSpendAHugeReserve()
        {
            var s = Start(true);
            Invoke(s, "CTBGainTide", int.MaxValue - 4);
            Invoke(s, "CTBGainTide", 100);
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(int.MaxValue));
            var points = (ICollection)typeof(CoreBoardingBattleState).GetField("TidePoints", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(s.Battle.Boarding);
            Assert.That(points.Count, Is.Zero, "New resource amounts do not allocate one object per point.");
            s.Battle.EnemyBlock = int.MaxValue;
            Play(s, "card_boarding_tide_08");
            Assert.That(s.Battle.Boarding.TidePower, Is.Zero);
            Assert.That(s.Battle.Schools.TidePower, Is.Zero);
            Assert.That(s.Battle.EnemyBlock, Is.Zero);
        }

        [TestCase(0, 50, 50)]
        [TestCase(int.MaxValue - 10, 50, int.MaxValue)]
        public void EnemyAbsorptionAlsoAccumulatesWithoutTwelvePointCap(int before, int absorbed, int expected)
        {
            var s = Start(true);
            Set(s.Battle.BoardingEnemy, "TidePower", before);
            Set(s.Battle.BoardingEnemy, "TideGuard", absorbed);
            s.Battle.EnemyBlock = 0;
            Invoke(s, "OnBoardingEnemyDamaged", absorbed, 0);
            Assert.That(s.Battle.BoardingEnemy.TidePower, Is.EqualTo(expected));
        }

        [TestCase("card_boarding_tide_11", 11, 1, 2, 11, 1)]
        [TestCase("card_boarding_tide_11", 12, 1, 20, 0, 0)]
        [TestCase("card_boarding_tide_11", 12, 0, 2, 12, 0)]
        [TestCase("card_boarding_tide_12", 15, 1, 2, 15, 1)]
        [TestCase("card_boarding_tide_12", 16, 1, 11, 0, 0)]
        [TestCase("card_boarding_tide_12", 16, 0, 2, 16, 0)]
        [TestCase("card_special_crew_white_shell_doctor_mirei", 15, 1, 2, 15, 1)]
        [TestCase("card_special_crew_white_shell_doctor_mirei", 16, 1, 11, 0, 0)]
        public void TideHealingUsesBoostOnlyWhenBothSuppliesAreAvailable(string id, int tide, int medicine,
            int recovery, int remainingTide, int remainingMedicine)
        {
            var s = Start(true);
            s.Battle.PlayerBoardingHealth = 500;
            s.Cargo.Medicine = medicine;
            Invoke(s, "CTBGainTide", tide);
            CoreBattleCard card = s.GetBattleCard(Index(id));
            CoreBattleCardEffects quote = s.GetBattleCardEffects(card, NavalBattleStage.Boarding);
            Assert.That(quote.Repair, Is.EqualTo(recovery));
            Assert.That(quote.MedicineCost, Is.EqualTo(medicine - remainingMedicine));
            Play(s, id);
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(remainingTide));
            Assert.That(s.Cargo.Medicine, Is.EqualTo(remainingMedicine));
            if (card.BoardingSpec.Key == "tide_11")
                Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(500 + recovery));
            else
            {
                Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(500));
                Invoke(s, "BPlayerEnd");
                Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(500 + recovery));
            }
        }

        [TestCase(0, 12, 6, 2, 5, 7)] [TestCase(1, 14, 12, 4, 0, 1)]
        [TestCase(2, 16, 15, 6, 0, 1)] [TestCase(3, 19, 18, 7, 0, 1)]
        public void WaveCannonHasLowTideDamageAdvantageWithoutAddingDraw(int rank, int damage, int burstDamage,
            int burstCoefficient, int lowTide, int highTide)
        {
            var s = Start(false, rank);
            int index = Index("card_school_tide_b03");
            CoreBattleCard card = s.GetBattleCard(index);
            CoreBattleCard burst = s.GetBattleCardAtLevel(Index("card_school_tide_n02"), rank);
            Assert.That(card.Damage, Is.EqualTo(damage));
            Assert.That(burst.Damage, Is.EqualTo(burstDamage));
            Assert.That(card.Damage, Is.GreaterThan(burst.Damage));
            Assert.That(card.Draw, Is.Zero);
            Assert.That(burst.Draw, Is.Zero);
            Assert.That(card.Description, Does.Not.Contain("抽"));
            Assert.That(burst.Description, Does.Not.Contain("抽"));
            Assert.That(card.Energy, Is.EqualTo(1));
            Assert.That(card.PowderCost, Is.EqualTo(1));
            Assert.That(card.CannonballCost, Is.EqualTo(1));
            Assert.That(card.SchoolEffect.Power, Is.EqualTo(100));
            Assert.That(CoreNavalExpansionBalance.TideStrikeDamageCoefficient(rank), Is.EqualTo(burstCoefficient));
            int WaveAt(int tide) => MeasureTideAttackDamage("card_school_tide_b03", rank, tide, damage + tide);
            int BurstAt(int tide) => MeasureTideAttackDamage("card_school_tide_n02", rank, tide,
                burstDamage + burstCoefficient * tide);
            Assert.That(WaveAt(lowTide), Is.GreaterThan(BurstAt(lowTide)));
            Assert.That(WaveAt(highTide), Is.LessThan(BurstAt(highTide)));
            if (rank == 0)
                Assert.That(WaveAt(6), Is.EqualTo(BurstAt(6)));
            Set(s.Battle.Schools, "TidePower", 30);
            s.Battle.PlayerLoadedShot = 0;
            s.Battle.DrawPile.Clear(); s.Battle.DiscardPile.Clear();
            s.Battle.DrawPile.Add(Index("card_brace"));
            int expected = CoreNavalStatRules.ScaleCannonDamage(damage, s.EffectiveFirepower) + 30;
            int hullBefore = s.Battle.EnemyHull;
            Assert.That(s.GetBattleCardDamage(card, NavalBattleStage.Naval), Is.EqualTo(expected));
            Play(s, "card_school_tide_b03");
            Assert.That(hullBefore - s.Battle.EnemyHull, Is.EqualTo(expected));
            Assert.That(s.Battle.Schools.TidePower, Is.Zero);
            Assert.That(s.Battle.Hand, Is.Empty);
            Assert.That(s.Battle.DrawPile.Count, Is.EqualTo(1));
            Assert.That(s.GetBattleCard(s.Battle.DrawPile[0]).Id, Is.EqualTo("card_brace"));
        }

        [TestCase(false)] [TestCase(true)]
        public void TideStatusDisplaysUncappedBalanceOutsideResourceSummary(bool boarding)
        {
            var s = Start(boarding);
            if (boarding) Invoke(s, "CTBGainTide", 31);
            else Set(s.Battle.Schools, "TidePower", 31);
            string summary = CoreBattleStatusPresentation.BuildResourceSummary(s);
            Assert.That(summary, Does.Not.Contain("潮势"));
            CoreBattleStatus tide = CoreBattleStatusPresentation.Build(s, true).Single(status => status.Id == "tide_power");
            Assert.That(tide.Value, Is.EqualTo("31"));
            Assert.That(tide.Tooltip, Does.Contain("当前 31").And.Not.Contain("31/"));
            if (boarding) Assert.That(s.GetBoardingStateSummary(), Does.Contain("潮势 31（").And.Not.Contain("/12"));
        }

        private static int MeasureTideAttackDamage(string id, int rank, int tide, int expected)
        {
            var s = Start(false, rank);
            Assert.That(s.EffectiveFirepower, Is.EqualTo(6), "The crossover uses the unmodified cannon multiplier.");
            Set(s.Battle.Schools, "TidePower", tide);
            s.Battle.PlayerLoadedShot = 0;
            s.Battle.DrawPile.Clear(); s.Battle.DiscardPile.Clear();
            s.Battle.DrawPile.Add(Index("card_brace"));
            int hullBefore = s.Battle.EnemyHull;
            Play(s, id);
            int actual = hullBefore - s.Battle.EnemyHull;
            Assert.That(actual, Is.EqualTo(expected), id + " +" + rank + " with " + tide + " tide");
            Assert.That(s.Battle.Schools.TidePower, Is.Zero);
            Assert.That(s.Battle.Hand, Is.Empty);
            Assert.That(s.Battle.DrawPile.Count, Is.EqualTo(1));
            return actual;
        }

        private static CoreVoyageSession Start(bool boarding, int tideRank = 0)
        {
            var s = new CoreVoyageSession();
            s.ConfigureCardUpgrades(new Dictionary<string, int>
                { { "card_school_tide_b03", tideRank }, { "card_school_tide_n02", tideRank } });
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            s.ConfigureSpecialCardSources(new[] { "crew_white_shell_doctor_mirei" }, Array.Empty<string>());
            s.SetSeed("SIX-SCHOOL-TIDE-AUDIT");
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 10000;
            s.Battle.DistanceMeters = boarding ? 100 : 200;
            Set(s.Battle, "EnemyHeadingDegrees", 90f); Set(s.Battle, "PlayerHeadingDegrees", 90f);
            s.Battle.Alignment = NavalBattleAlignment.Parallel;
            if (boarding)
            {
                Assert.That(s.TryBoard(), Is.True);
                s.Battle.PlayerBoardingHealth = s.Battle.PlayerMaxBoardingHealth = 1000;
                s.Battle.EnemyBoardingHealth = s.Battle.EnemyMaxBoardingHealth = 10000;
            }
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = 0;
            s.Cargo.Powder = s.Cargo.Cannonballs = s.Cargo.Wood = 100;
            Set(s, "Morale", 0);
            return s;
        }
        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
        private static void Play(CoreVoyageSession s, string id)
        {
            s.Battle.Hand.Clear(); s.Battle.Hand.Add(Index(id)); s.Battle.Energy = 100;
            Assert.That(s.PlayCard(0), Is.True, id + ": " + s.CardUnavailableReason(0));
        }
        private static void Set(object target, string name, object value) => target.GetType().GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        private static object Invoke(CoreVoyageSession s, string name, params object[] args) => typeof(CoreVoyageSession)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s, args);
    }
}
