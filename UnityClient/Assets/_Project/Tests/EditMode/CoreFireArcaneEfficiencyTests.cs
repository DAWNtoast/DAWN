using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    /// <summary>Given-hand efficiency bounds, not shuffled-deck win rates.</summary>
    public sealed class CoreFireArcaneEfficiencyTests
    {
        [TestCase(0, 6)]
        [TestCase(1, 8)]
        [TestCase(2, 10)]
        [TestCase(3, 12)]
        public void MainFireStarterProvidesUsefulBurnWithoutTheOldDoublePowderTax(int rank, int burn)
        {
            var s = new CoreVoyageSession();
            CoreBattleCard card = s.GetBattleCardAtLevel(Index("card_school_fire_b03"), rank);
            Assert.That(card.Energy, Is.EqualTo(1));
            Assert.That(card.PowderCost, Is.EqualTo(1));
            Assert.That(card.CannonballCost, Is.EqualTo(1));
            Assert.That(card.SchoolEffect.Power, Is.EqualTo(burn));
        }

        [TestCase(6, 1, new[] { 23, 46, 69 }, new[] { 34, 78, 127 }, 15)]
        [TestCase(22, 1, new[] { 62, 124, 186 }, new[] { 73, 156, 220 }, 16)]
        [TestCase(38, 2, new[] { 94, 188, 282 }, new[] { 104, 216, 336 }, 15)]
        public void FireHasAVisibleTwoToThreeTurnPayoffWithLiveGuardAndRepair(int power, int target, int[] neutralExpected, int[] fireExpected, int remainingBurn)
        {
            Pressure neutral = Run(power, target, "card_chain_shot", "card_cannon_barrage");
            Pressure fire = Run(power, target, "card_school_fire_b03", "card_school_fire_b04");
            Assert.That(neutral.Loss, Is.EqualTo(neutralExpected));
            Assert.That(fire.Loss, Is.EqualTo(fireExpected));
            Assert.That(neutral.Powder, Is.EqualTo(6));
            Assert.That(fire.Powder, Is.EqualTo(9), "The exploit finisher still costs two powder, so burn is not a free upgrade to ordinary guns.");
            Assert.That(fire.Balls, Is.EqualTo(neutral.Balls));
            Assert.That(fire.Burn, Is.EqualTo(remainingBurn));
        }

        [Test]
        public void NavalManaGeneratorNowConvertsItsManaIntoDamageWithinThreeEnergy()
        {
            Pressure neutral = Run(6, 1, "card_chain_shot", "card_cannon_barrage");
            Pressure original = Run(6, 1, "card_school_arcane_b06", "card_school_arcane_b04");
            Pressure burst = Run(6, 1, "card_school_arcane_b06", "card_school_arcane_n04");
            Assert.That(neutral.Loss, Is.EqualTo(new[] { 23, 46, 69 }));
            Assert.That(original.Loss, Is.EqualTo(new[] { 18, 36, 54 }));
            Assert.That(burst.Loss, Is.EqualTo(new[] { 28, 56, 84 }));
            Assert.That(burst.Powder + burst.Balls, Is.Zero);
        }

        private sealed class Pressure
        {
            public readonly int[] Loss = new int[3];
            public int Powder, Balls, Burn;
        }

        private static Pressure Run(int power, int target, params string[] sequence)
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            // Extra hull only keeps the isolated damage-pressure experiment alive;
            // normal armor and the requested real ship firepower remain unchanged.
            s.ConfigureComponentBonuses(1000, 0, power - 6, 0, 0);
            s.SetSeed("FIRE-ARCANE-EFFICIENCY"); s.StartVoyage();
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[target]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            Pose(s, true, 0); Pose(s, false, 2);
            s.Cargo.Powder = s.Cargo.Cannonballs = 100;
            int initial = s.Battle.EnemyHull;
            var result = new Pressure();
            for (int turn = 0; turn < 3; turn++)
            {
                Assert.That(s.Battle.Energy, Is.EqualTo(3));
                s.Battle.Hand.Clear(); s.Battle.DrawPile.Clear(); s.Battle.DiscardPile.Clear();
                foreach (string id in sequence) s.Battle.Hand.Add(Index(id));
                foreach (string filler in new[] { "card_retreat", "card_brace", "card_tactical_draw" })
                    if (s.Battle.Hand.Count < 5) s.Battle.Hand.Add(Index(filler));
                Assert.That(s.Battle.Hand.Count, Is.EqualTo(5));
                Assert.That(s.Battle.Hand.GroupBy(i => i).All(g => g.Count() <= 2), Is.True);
                foreach (string id in sequence)
                {
                    int slot = s.Battle.Hand.FindIndex(i => CoreVoyageSession.CardLibrary[i].Id == id);
                    Assert.That(s.PlayCard(slot), Is.True, s.CardUnavailableReason(slot));
                }
                Assert.That(s.Battle.Energy, Is.Zero);
                Assert.That(s.Battle.Hand.Count, Is.LessThanOrEqualTo(8));
                if (s.TrainingOutcome == CoreTrainingBattleOutcome.Victory)
                {
                    for (int remaining = turn; remaining < 3; remaining++) result.Loss[remaining] = initial;
                    break;
                }
                Assert.That(s.EndBattleTurn(), Is.True);
                result.Loss[turn] = initial - s.Battle.EnemyHull;
            }
            result.Powder = 100 - s.Cargo.Powder; result.Balls = 100 - s.Cargo.Cannonballs;
            result.Burn = s.Battle.EnemyBurnTurns;
            return result;
        }

        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
        private static void Pose(CoreVoyageSession s, bool player, int x) => typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(s.Battle, new object[] { player, new VoyagePoint(x * 100, 0), player ? 0f : 180f });
    }
}
