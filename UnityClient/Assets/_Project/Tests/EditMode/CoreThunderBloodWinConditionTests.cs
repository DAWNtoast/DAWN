using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreThunderBloodWinConditionTests
    {
        [Test]
        public void NavalThunderBuildsAndCashesOutChainWithThreeStartingEnergyAndNoField()
        {
            var s = Start(NavalBattleStage.Naval,
                new[] { "card_school_thunder_n03", "card_school_thunder_b03", "card_school_thunder_b06", "card_school_thunder_n08", "card_school_thunder_b05" },
                new[] { "card_school_thunder_n04", "card_school_thunder_n02", "card_school_thunder_b04", "card_school_thunder_n06", "card_school_thunder_n01" });
            s.Cargo.Powder = s.Cargo.Cannonballs = 1;
            int before = s.Battle.EnemyHull;

            Play(s, "card_school_thunder_n03");
            Assert.That(s.Battle.Energy, Is.EqualTo(3));
            Play(s, "card_school_thunder_b03");
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(3));
            Assert.That(s.Battle.Energy, Is.EqualTo(2));
            Play(s, "card_school_thunder_b06");

            Assert.That(s.Battle.Energy, Is.Zero);
            Assert.That(before - s.Battle.EnemyHull,
                Is.EqualTo(CoreNavalStatRules.ScaleCannonDamage(8, s.EffectiveFirepower) + 13));
            Assert.That(s.Battle.Schools.ThunderChain, Is.Zero, "The naval finisher clears after its attack completes.");
            Assert.That(s.Cargo.Powder, Is.Zero);
            Assert.That(s.Cargo.Cannonballs, Is.Zero);
            Assert.That(s.Battle.NavalFields.Cells, Is.Empty);
        }

        [Test]
        public void NavalCircuitAddsEnergyAndReplacesItselfAfterTheHeldAttacksReachItsThreshold()
        {
            var s = Start(NavalBattleStage.Naval,
                new[] { "card_school_thunder_n03", "card_school_thunder_b03", "card_school_thunder_b06", "card_school_thunder_n08", "card_school_thunder_b05" },
                new[] { "card_school_thunder_n04", "card_school_thunder_n02", "card_school_thunder_b04", "card_school_thunder_n06", "card_school_thunder_n01" });
            s.Cargo.Powder = s.Cargo.Cannonballs = 1;
            Play(s, "card_school_thunder_n03");
            Play(s, "card_school_thunder_b03");
            Assert.That(s.Battle.Energy, Is.EqualTo(2));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(3));
            int handCount = s.Battle.Hand.Count, drawCount = s.Battle.DrawPile.Count;
            int enemy = s.Battle.EnemyHull;
            Play(s, "card_school_thunder_n08");
            Assert.That(s.Battle.Energy, Is.EqualTo(3));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(3));
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(enemy), "An energy utility cannot invent an attack.");
            Assert.That(s.Battle.Hand.Count, Is.EqualTo(handCount));
            Assert.That(s.Battle.DrawPile.Count, Is.EqualTo(drawCount - 1));
            Assert.That(s.Battle.NavalFields.Cells, Is.Empty);
        }

        [Test]
        public void BoardingThunderReachesTwoChainAndFundsFinisherWithinThreeStartingEnergy()
        {
            var s = Start(NavalBattleStage.Boarding,
                new[] { "thunder_01", "thunder_03", "thunder_09", "thunder_13", "thunder_07" },
                new[] { "thunder_02", "thunder_04", "thunder_05", "thunder_06", "thunder_08" });
            int before = s.Battle.EnemyBoardingHealth;

            Play(s, "thunder_01");
            Play(s, "thunder_03");
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(2));
            Assert.That(s.Battle.Hand.Count, Is.EqualTo(4), "The attack replaces itself without injecting a card.");
            Play(s, "thunder_09");
            Assert.That(s.Battle.Energy, Is.EqualTo(4));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(2));
            Play(s, "thunder_13");

            Assert.That(before - s.Battle.EnemyBoardingHealth, Is.EqualTo(68));
            Assert.That(s.Battle.Energy, Is.EqualTo(2));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(1));
            Assert.That(s.Battle.BoardingFields.Fields, Is.Empty);
        }

        [TestCase(false, 0, 992)]
        [TestCase(false, 9, 983)]
        [TestCase(true, 0, 974)]
        public void BoardingBloodPaysForResourceAndDamageThenRecoversOnlyActualUnblockedLife(
            bool healingBanned, int drainBlock, int expectedHealth)
        {
            var s = Start(NavalBattleStage.Boarding,
                new[] { "blood_05", "blood_04", "blood_01", "blood_02", "blood_06" },
                new[] { "blood_03", "blood_07", "blood_08", "blood_10", "blood_12" });
            int before = s.Battle.EnemyBoardingHealth;
            if (healingBanned) s.ApplyBattleHealingBan(true, 2);

            Play(s, "blood_05");
            Assert.That(s.Battle.Energy, Is.EqualTo(5));
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(994));
            Play(s, "blood_04");
            Assert.That(s.Battle.Hand.Count, Is.EqualTo(6), "The draw comes from the remaining five legal deck cards.");
            Play(s, "blood_01");
            Play(s, "blood_02");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(974));
            s.Battle.EnemyBlock = drainBlock;
            Play(s, "blood_06");

            Assert.That(s.Battle.Energy, Is.Zero);
            Assert.That(before - s.Battle.EnemyBoardingHealth, Is.EqualTo(118 - drainBlock));
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(expectedHealth));
            Assert.That(s.Battle.PlayerBlock, Is.Zero, "No field or equipment refunds the active life payments.");
        }

        [Test]
        public void NavalBloodCanRecoverItsPaidCannonCostUsingOrdinaryDrain()
        {
            var s = Start(NavalBattleStage.Naval,
                new[] { "card_school_blood_b03", "card_school_blood_b04", "card_school_blood_b05", "card_school_blood_b06", "card_school_blood_n01" },
                new[] { "card_school_blood_n02", "card_school_blood_n03", "card_school_blood_b03", "card_school_blood_b04", "card_school_blood_b05" });
            s.Cargo.Powder = s.Cargo.Cannonballs = 1;
            int hull = s.Hull, before = s.Battle.EnemyHull;

            Play(s, "card_school_blood_b03");
            Assert.That(s.Hull, Is.EqualTo(hull - 5));
            Play(s, "card_school_blood_b04");

            Assert.That(s.Hull, Is.EqualTo(hull), "Life drain restores the missing five hull, capped at the maximum.");
            Assert.That(s.Battle.Energy, Is.EqualTo(1));
            Assert.That(before - s.Battle.EnemyHull,
                Is.EqualTo(CoreNavalStatRules.ScaleCannonDamage(10, s.EffectiveFirepower) + 14 + 5));
        }

        [Test]
        public void BoardingThunderCanSpendItsRemainingBudgetForOneHundredTwentyEightDamage()
        {
            var s = Start(NavalBattleStage.Boarding,
                new[] { "thunder_01", "thunder_03", "thunder_09", "thunder_11", "thunder_02" },
                new[] { "thunder_04", "thunder_05", "thunder_07", "thunder_08", "thunder_13" });
            int before = s.Battle.EnemyBoardingHealth;
            Play(s, "thunder_01"); Play(s, "thunder_03"); Play(s, "thunder_09");
            Play(s, "thunder_11"); Play(s, "thunder_02"); Play(s, "thunder_13");
            Assert.That(before - s.Battle.EnemyBoardingHealth, Is.EqualTo(128));
            Assert.That(s.Battle.Energy, Is.Zero);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(1000));
        }

        [Test]
        public void BoardingBloodAddsLifeFundedResourcesToTheSameOrdinaryEnergySupport()
        {
            var s = Start(NavalBattleStage.Boarding,
                new[] { "basic_09", "basic_10", "blood_05", "blood_04", "blood_02" },
                new[] { "blood_03", "blood_09", "blood_07", "blood_06", "blood_01" });
            int before = s.Battle.EnemyBoardingHealth;
            Play(s, "basic_09"); Play(s, "basic_10"); Play(s, "blood_05"); Play(s, "blood_04");
            Play(s, "blood_02"); Play(s, "blood_01"); Play(s, "blood_06"); Play(s, "blood_07");
            Assert.That(before - s.Battle.EnemyBoardingHealth, Is.EqualTo(174));
            Assert.That(s.Battle.Energy, Is.Zero);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(992));
        }

        [Test]
        public void OrdinaryBoardingCanProduceOneHundredTenDamageWithoutPayingLife()
        {
            var s = Start(NavalBattleStage.Boarding,
                new[] { "basic_09", "basic_10", "basic_03", "basic_02", "basic_05" },
                new[] { "basic_04", "basic_06", "basic_08", "basic_11", "basic_12" });
            int before = s.Battle.EnemyBoardingHealth;
            Play(s, "basic_09"); Play(s, "basic_10"); Play(s, "basic_03"); Play(s, "basic_02");
            Assert.That(before - s.Battle.EnemyBoardingHealth, Is.EqualTo(110));
            Assert.That(s.Battle.Energy, Is.Zero);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(1000));
        }

        private static CoreVoyageSession Start(NavalBattleStage stage, string[] opening, string[] draw)
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            s.ConfigureSpecialCardSources(Array.Empty<string>(), Array.Empty<string>());
            s.SetSeed("SIX-SCHOOL-WIN-CONDITION-THUNDER-BLOOD");
            int[] deck = opening.Concat(draw).Select(Index).ToArray();
            Assert.That(deck.Length, Is.EqualTo(10));
            Assert.That(s.SetBattleDeck(stage, deck), Is.True, "The sequence must belong to a legal ordinary ten-card deck.");
            Assert.That(deck.All(i => CoreVoyageSession.CardLibrary[i].RequiredCrew.Length == 0 &&
                CoreVoyageSession.CardLibrary[i].RequiredComponent.Length == 0), Is.True);
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            Set(s, "Hull", s.MaxHull);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 1000;
            Pose(s, true, 0, 0, 90f);
            Pose(s, false, stage == NavalBattleStage.Boarding ? 1 : 2, 0, 90f);
            s.Battle.Alignment = NavalBattleAlignment.Parallel;
            if (stage == NavalBattleStage.Boarding)
            {
                Assert.That(s.TryBoard(), Is.True);
                s.Battle.PlayerBoardingHealth = s.Battle.PlayerMaxBoardingHealth = 1000;
                s.Battle.EnemyBoardingHealth = s.Battle.EnemyMaxBoardingHealth = 1000;
            }
            Set(s, "Morale", 0);
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = 0;
            s.Battle.Hand.Clear(); s.Battle.DrawPile.Clear(); s.Battle.DiscardPile.Clear();
            s.Battle.Hand.AddRange(opening.Select(Index));
            s.Battle.DrawPile.AddRange(draw.Select(Index));
            s.Battle.Energy = 3;
            Assert.That(s.Battle.NavalFields.Cells, Is.Empty);
            return s;
        }

        private static string Id(string key) => key.StartsWith("card_", StringComparison.Ordinal) ? key : "card_boarding_" + key;
        private static int Index(string key) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(i => CoreVoyageSession.CardLibrary[i].Id == Id(key));
        private static void Set(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
        private static void Pose(CoreVoyageSession s, bool player, int x, int y, float heading) => typeof(CoreBattleState)
            .GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s.Battle,
                new object[] { player, new VoyagePoint(x * 100, y * 100), heading });
        private static void Play(CoreVoyageSession s, string key)
        {
            int slot = s.Battle.Hand.IndexOf(Index(key));
            Assert.That(slot, Is.GreaterThanOrEqualTo(0), key + " must be held without injecting another card or energy.");
            string reason = s.CardUnavailableReason(slot);
            Assert.That(reason, Is.Empty, key + ": " + reason);
            Assert.That(s.PlayCard(slot), Is.True, key);
        }
    }
}
