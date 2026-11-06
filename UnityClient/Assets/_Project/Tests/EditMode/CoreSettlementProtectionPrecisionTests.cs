using System;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreSettlementProtectionPrecisionTests
    {
        // Explicit expectations exercise the published percentages, not the
        // production arithmetic or floating-point values copied into the test.
        [TestCase(VoyageOutcome.Failure, 0, 0, 0)]
        [TestCase(VoyageOutcome.Failure, 1, 0, 1)]
        [TestCase(VoyageOutcome.Failure, 20, 7, 7)]
        [TestCase(VoyageOutcome.Failure, 21, 7, 8)]
        [TestCase(VoyageOutcome.SafeReturn, 0, 0, 0)]
        [TestCase(VoyageOutcome.SafeReturn, 1, 0, 1)]
        [TestCase(VoyageOutcome.SafeReturn, 20, 17, 17)]
        [TestCase(VoyageOutcome.SafeReturn, 21, 17, 18)]
        [TestCase(VoyageOutcome.ArrivedAtPort, 0, 0, 0)]
        [TestCase(VoyageOutcome.ArrivedAtPort, 1, 0, 1)]
        [TestCase(VoyageOutcome.ArrivedAtPort, 20, 19, 19)]
        [TestCase(VoyageOutcome.ArrivedAtPort, 21, 19, 20)]
        [TestCase(VoyageOutcome.TreasureRecovered, 0, 0, 0)]
        [TestCase(VoyageOutcome.TreasureRecovered, 1, 0, 1)]
        [TestCase(VoyageOutcome.TreasureRecovered, 20, 19, 19)]
        [TestCase(VoyageOutcome.TreasureRecovered, 21, 19, 20)]
        public void NetGainUsesExactPercent_CommonRoundsDown_RareRoundsUp_AndPrincipalIsCreditedOnce(
            VoyageOutcome outcome, int gain, int expectedCommon, int expectedRare)
        {
            const int principal = 3;
            var session = StartWithPrincipal(principal, 60, 500m);
            decimal totalGoldBefore = session.ProfileGold + session.Cargo.Gold;
            session.Cargo.Gold += gain;
            SetCommon(session.Cargo, principal + gain);
            SetRare(session.Cargo, principal + gain);
            Finish(session, outcome);
            AssertRewards(session, expectedCommon, expectedRare);
            Assert.That(session.Cargo.Gold, Is.EqualTo(60), "Protected profit is quoted separately; the original purse stays aboard until confirmation.");
            Assert.That(session.ProfileGold, Is.EqualTo(500m), "Finishing a voyage must not silently bank the reveal's quoted gold.");
            AssertAmounts(Common(session.Cargo), principal + expectedCommon, "common principal plus protected net gain");
            AssertAmounts(Rare(session.Cargo), principal + expectedRare, "rare principal plus protected net gain");
            ConfirmTwice(session, totalGoldBefore + expectedCommon);
        }

        // These arithmetic boundaries deliberately exceed hold capacity; they
        // validate the settlement transaction's integer domain, not a legal loadout.
        [TestCase(VoyageOutcome.Failure, 751619276, 751619277)]
        [TestCase(VoyageOutcome.SafeReturn, 1825361099, 1825361100)]
        [TestCase(VoyageOutcome.ArrivedAtPort, 2040109464, 2040109465)]
        [TestCase(VoyageOutcome.TreasureRecovered, 2040109464, 2040109465)]
        public void MaximumIntegerNetGainUsesWideArithmetic_AccountOverflowStaysAboardAndCannotDuplicate(
            VoyageOutcome outcome, int expectedCommon, int expectedRare)
        {
            var session = StartWithPrincipal(0, 0, 0m);
            session.Cargo.Gold = int.MaxValue;
            SetCommon(session.Cargo, int.MaxValue);
            SetRare(session.Cargo, int.MaxValue);
            Finish(session, outcome);
            AssertRewards(session, expectedCommon, expectedRare);
            AssertAmounts(Common(session.Cargo), expectedCommon, "maximum common gain");
            AssertAmounts(Rare(session.Cargo), expectedRare, "maximum rare gain");
            Assert.That(session.Cargo.Gold, Is.Zero);
            ConfirmTwice(session, expectedCommon);
            decimal expectedAccount = Math.Min((decimal)CoreEconomyBalance.MaximumProfileGold, expectedCommon);
            Assert.That(session.ProfileGold, Is.EqualTo(expectedAccount));
            Assert.That(session.Cargo.Gold, Is.EqualTo(expectedCommon - (int)expectedAccount), "Gold beyond the account limit must remain aboard, not be lost or credited twice.");
        }

        [TestCase(VoyageOutcome.Failure, 7)]
        [TestCase(VoyageOutcome.SafeReturn, 17)]
        [TestCase(VoyageOutcome.ArrivedAtPort, 19)]
        [TestCase(VoyageOutcome.TreasureRecovered, 19)]
        public void PrincipalNearIntegerMaximumPlusTwentyGainNeverOverflowsOrLosesExistingProperty(
            VoyageOutcome outcome, int expectedGain)
        {
            const int principal = int.MaxValue - 20;
            var session = StartWithPrincipal(principal, principal, 500m);
            decimal totalGoldBefore = session.ProfileGold + session.Cargo.Gold;
            session.Cargo.Gold = int.MaxValue;
            SetCommon(session.Cargo, int.MaxValue);
            SetRare(session.Cargo, int.MaxValue);
            Finish(session, outcome);
            AssertRewards(session, expectedGain, expectedGain);
            Assert.That(session.Cargo.Gold, Is.EqualTo(principal));
            AssertAmounts(Common(session.Cargo), principal + expectedGain, "large common principal");
            AssertAmounts(Rare(session.Cargo), principal + expectedGain, "large rare principal");
            ConfirmTwice(session, totalGoldBefore + expectedGain);
        }

        [TestCase(VoyageOutcome.Failure)]
        [TestCase(VoyageOutcome.SafeReturn)]
        [TestCase(VoyageOutcome.ArrivedAtPort)]
        [TestCase(VoyageOutcome.TreasureRecovered)]
        public void SpentPrincipalIsNotRestoredOrMiscountedAsNewProfit(VoyageOutcome outcome)
        {
            var session = StartWithPrincipal(20, 60, 500m);
            session.Cargo.Gold -= 7;
            SetCommon(session.Cargo, 13);
            SetRare(session.Cargo, 13);
            Finish(session, outcome);
            AssertRewards(session, 0, 0);
            Assert.That(session.Cargo.Gold, Is.EqualTo(53));
            AssertAmounts(Common(session.Cargo), 13, "spent common principal");
            AssertAmounts(Rare(session.Cargo), 13, "spent rare principal");
            ConfirmTwice(session, 553m);
        }

        private static CoreVoyageSession StartWithPrincipal(int materialPrincipal, int goldPrincipal, decimal account)
        {
            var session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            session.RestoreProfile(account, session.CurrentPort);
            SetCommon(session.Cargo, materialPrincipal);
            SetRare(session.Cargo, materialPrincipal);
            session.Cargo.Gold = goldPrincipal;
            session.Cargo.Water = session.Cargo.Food = 40;
            session.StartVoyage();
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.Cargo.Gold, Is.EqualTo(goldPrincipal), "The fixture supplies either a full travel purse or an empty account, so departure cannot change the chosen gold principal.");
            return session;
        }

        private static void Finish(CoreVoyageSession session, VoyageOutcome outcome)
        {
            if (outcome == VoyageOutcome.SafeReturn || outcome == VoyageOutcome.Failure)
            {
                if (outcome == VoyageOutcome.Failure) session.Cargo.Water = 0;
                Assert.That(session.SafeReturn(), Is.True);
            }
            else
            {
                // Docking and treasure completion converge here after their own
                // route checks. Exercise that real boundary without fabricating a settlement.
                MethodInfo finish = typeof(CoreVoyageSession).GetMethod("FinishVoyage", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(finish, Is.Not.Null);
                finish.Invoke(session, new object[] { outcome });
            }
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(session.Settlement.Outcome, Is.EqualTo(outcome));
            float expectedRatio = outcome == VoyageOutcome.Failure ? .35f : outcome == VoyageOutcome.SafeReturn ? .85f : .95f;
            Assert.That(session.Settlement.ProtectionRatio, Is.EqualTo(expectedRatio));
        }

        private static void AssertRewards(CoreVoyageSession session, int common, int rare)
        {
            VoyageSettlement result = session.Settlement;
            Assert.That(result.BankedGold, Is.EqualTo(common), "gold");
            Assert.That(result.BankedWood, Is.EqualTo(common), "wood");
            Assert.That(result.BankedIron, Is.EqualTo(common), "iron");
            Assert.That(result.BankedFame, Is.EqualTo(common), "fame");
            Assert.That(result.BankedAncientMaterial, Is.EqualTo(common), "ancient material follows common floor rounding");
            Assert.That(result.BankedGem, Is.EqualTo(rare), "gem");
            Assert.That(result.BankedRelic, Is.EqualTo(rare), "relic");
            Assert.That(result.BankedBlackPearl, Is.EqualTo(rare), "black pearl");
        }

        private static void ConfirmTwice(CoreVoyageSession session, decimal expectedTotalGold)
        {
            int[] common = Common(session.Cargo), rare = Rare(session.Cargo);
            int water = session.Cargo.Water, food = session.Cargo.Food;
            string expectedPort = session.Settlement.Destination;
            session.ReturnToPortAfterSettlement();
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(session.CurrentPort, Is.EqualTo(expectedPort));
            Assert.That(session.ProfileGold + session.Cargo.Gold, Is.EqualTo(expectedTotalGold));
            CollectionAssert.AreEqual(common, Common(session.Cargo));
            CollectionAssert.AreEqual(rare, Rare(session.Cargo));
            Assert.That(session.Cargo.Water, Is.EqualTo(water));
            Assert.That(session.Cargo.Food, Is.EqualTo(food));
            decimal account = session.ProfileGold;
            int aboard = session.Cargo.Gold, experience = session.PirateExperience, fragments = session.TreasureFragments;
            session.ReturnToPortAfterSettlement();
            Assert.That(session.ProfileGold, Is.EqualTo(account));
            Assert.That(session.Cargo.Gold, Is.EqualTo(aboard));
            Assert.That(session.PirateExperience, Is.EqualTo(experience));
            Assert.That(session.TreasureFragments, Is.EqualTo(fragments));
            CollectionAssert.AreEqual(common, Common(session.Cargo));
            CollectionAssert.AreEqual(rare, Rare(session.Cargo));
            Assert.That(session.CurrentPort, Is.EqualTo(expectedPort));
        }

        private static void SetCommon(VoyageCargo cargo, int amount) =>
            cargo.Wood = cargo.Iron = cargo.Fame = cargo.Cloth = cargo.Beer = cargo.Medicine = cargo.Powder = cargo.Cannonballs = cargo.AncientMaterial = amount;

        private static void SetRare(VoyageCargo cargo, int amount) =>
            cargo.Gem = cargo.Relic = cargo.BlackPearl = cargo.AncientCompass = cargo.Lantern = amount;

        private static int[] Common(VoyageCargo cargo) => new[]
            { cargo.Wood, cargo.Iron, cargo.Fame, cargo.Cloth, cargo.Beer, cargo.Medicine, cargo.Powder, cargo.Cannonballs, cargo.AncientMaterial };

        private static int[] Rare(VoyageCargo cargo) => new[]
            { cargo.Gem, cargo.Relic, cargo.BlackPearl, cargo.AncientCompass, cargo.Lantern };

        private static void AssertAmounts(int[] amounts, int expected, string category)
        {
            for (int index = 0; index < amounts.Length; index++)
                Assert.That(amounts[index], Is.EqualTo(expected), category + " at index " + index);
        }
    }
}
