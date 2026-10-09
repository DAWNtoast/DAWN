using System;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreEconomySafetyTests
    {
        [Test]
        public void OversizedMixedSupplyQuoteNeverWrapsOrMutatesFunds()
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(int.MaxValue, session.CurrentPort);
            var order = new VoyageSupplyOrder { Medicine = int.MaxValue, Cannonballs = int.MaxValue };
            var quote = session.QuoteSupplyPurchase(order);
            Assert.That(quote.Cost, Is.EqualTo((session.GetSupplyUnitPrice(VoyageSupplyType.Medicine) +
                session.GetSupplyUnitPrice(VoyageSupplyType.Cannonballs)) * int.MaxValue));
            Assert.That(order.TotalUnits, Is.EqualTo(int.MaxValue));
            Assert.That(quote.CanAfford, Is.False);
            Assert.That(session.TryPurchaseSupplies(order), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void NegativeCargoCannotCreateSpaceForAPurchase()
        {
            var session = new CoreVoyageSession();
            session.Cargo.Iron = -100;
            decimal gold = session.ProfileGold;
            Assert.That(session.TryPurchaseSupplies(new VoyageSupplyOrder { Wood = 100 }), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
        }

        [Test]
        public void ProfileRewardAtLimitRetainsTheWholeExcessAboard()
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(CoreEconomyBalance.MaximumProfileGold - 5, session.CurrentPort);
            int aboard = session.Cargo.Gold;
            session.GrantProfileGold(45, "test");
            Assert.That(session.ProfileGold, Is.EqualTo(CoreEconomyBalance.MaximumProfileGold));
            Assert.That(session.Cargo.Gold, Is.EqualTo(aboard + 40));
        }

        [Test]
        public void LegacyLargeAccountDoesNotWrapOrLoseItsSavedBalance()
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(int.MaxValue, session.CurrentPort);
            int aboard = session.Cargo.Gold;
            session.GrantProfileGold(35, "test");
            Assert.That(session.ProfileGold, Is.EqualTo(int.MaxValue));
            Assert.That(session.Cargo.Gold, Is.EqualTo(aboard + 35));
        }

        [Test]
        public void CashDepositOnlyMovesAvailableAccountHeadroom()
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(CoreEconomyBalance.MaximumProfileGold - 3, session.CurrentPort);
            session.Cargo.Gold = 40;
            Assert.That(session.TryDepositTravelCash(), Is.True);
            Assert.That(session.ProfileGold, Is.EqualTo(CoreEconomyBalance.MaximumProfileGold));
            Assert.That(session.Cargo.Gold, Is.EqualTo(37));
            Assert.That(session.TryDepositTravelCash(), Is.False);
            Assert.That(session.TrySpendProfileGold(20, "test"), Is.True);
            Assert.That(session.TryDepositTravelCash(), Is.True);
            Assert.That(session.Cargo.Gold, Is.EqualTo(17));
        }

        [Test]
        public void SettlementCreditsExactlyOnceAndPreservesAccountOverflowAboard()
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(CoreEconomyBalance.MaximumProfileGold - 5, session.CurrentPort);
            long initialTotal = (long)session.ProfileGold + session.Cargo.Gold;
            session.StartVoyage();
            session.Cargo.Gold += 100;
            Assert.That(session.SafeReturn(), Is.True);
            int protectedGold = session.Settlement.BankedGold;
            session.ReturnToPortAfterSettlement();
            Assert.That(session.ProfileGold, Is.EqualTo(CoreEconomyBalance.MaximumProfileGold));
            Assert.That((long)session.ProfileGold + session.Cargo.Gold, Is.EqualTo(initialTotal + protectedGold));
            session.ReturnToPortAfterSettlement();
            Assert.That((long)session.ProfileGold + session.Cargo.Gold, Is.EqualTo(initialTotal + protectedGold));
        }

        [Test]
        public void SaleAtAccountLimitDoesNotConsumeTheMaterial()
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(CoreEconomyBalance.MaximumProfileGold, session.CurrentPort);
            int wood = session.Cargo.Wood;
            Assert.That(session.TrySellCargoMaterial("wood", 1), Is.False);
            Assert.That(session.Cargo.Wood, Is.EqualTo(wood));
        }

        [Test]
        public void RewardWithNoCargoSpaceIsAtomicAndCanBeRetriedAfterSale()
        {
            var session = new CoreVoyageSession();
            session.Cargo.Iron = 45;
            decimal gold = session.ProfileGold;
            int wood = session.Cargo.Wood;
            int fame = session.Cargo.Fame;
            Assert.That(session.TryReceivePortRewards(35, 3, 0, 1, "dispatch", out string failure), Is.False);
            Assert.That(failure, Does.Contain("货舱"));
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(session.Cargo.Wood, Is.EqualTo(wood));
            Assert.That(session.Cargo.Fame, Is.EqualTo(fame));
            Assert.That(session.TrySellCargoMaterial("iron", 45), Is.True);
            Assert.That(session.TryReceivePortRewards(35, 3, 0, 1, "dispatch", out failure), Is.True);
            Assert.That(session.ProfileGold, Is.EqualTo(gold + 45 * 6 + 35));
            Assert.That(session.Cargo.Wood, Is.EqualTo(wood + 3));
        }

        [Test]
        public void RewardAtAccountLimitRejectsAllResourcesTogether()
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(CoreEconomyBalance.MaximumProfileGold - 1, session.CurrentPort);
            int wood = session.Cargo.Wood;
            Assert.That(session.TryReceivePortRewards(35, 3, 0, 0, "dispatch", out _), Is.False);
            Assert.That(session.Cargo.Wood, Is.EqualTo(wood));
            Assert.That(session.ProfileGold, Is.EqualTo(CoreEconomyBalance.MaximumProfileGold - 1));
        }

        [Test]
        public void RewardWithFameOverflowIsRejectedWithoutPayingGold()
        {
            var session = new CoreVoyageSession();
            session.Cargo.Fame = int.MaxValue;
            decimal gold = session.ProfileGold;
            Assert.That(session.TryReceivePortRewards(35, 0, 0, 1, "dispatch", out _), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(session.Cargo.Fame, Is.EqualTo(int.MaxValue));
        }

        [TestCase(-1, 0, 0, 0)]
        [TestCase(1, -1, 0, 0)]
        [TestCase(1, 0, -1, 0)]
        [TestCase(1, 0, 0, -1)]
        public void RewardRejectsNegativeInputs(int gold, int wood, int iron, int fame)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.TryReceivePortRewards(gold, wood, iron, fame, "dispatch", out _), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(500));
        }

        [Test]
        public void DispatchCannotPayDuringAVoyage()
        {
            var session = new CoreVoyageSession();
            session.StartVoyage();
            decimal gold = session.ProfileGold;
            Assert.That(session.TryReceivePortRewards(35, 3, 0, 0, "dispatch", out _), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
        }

        [Test]
        public void SwitchingToASmallerShipCannotOverloadItsHold()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.SelectShip(1), Is.True);
            session.Cargo.Iron = 20;
            Assert.That(session.Cargo.UsedVolume, Is.GreaterThan(CoreVoyageSession.Ships[0].CargoCapacity));
            Assert.That(session.CanSelectShip(0), Is.False);
            Assert.That(session.SelectShip(0), Is.False);
            Assert.That(session.SelectedShipIndex, Is.EqualTo(1));
            Assert.That(session.Cargo.Iron, Is.EqualTo(20));
        }

        [Test]
        public void DispatchDeadlineIsReevaluatedAtTheExactCompletionBoundary()
        {
            long end = CoreVoyageSession.GameClockOrigin.AddHours(2).Ticks;
            Assert.That(CoreEconomyBalance.IsDispatchReady(0, end.ToString(), end - 1), Is.False);
            Assert.That(CoreEconomyBalance.IsDispatchReady(0, end.ToString(), end), Is.True);
            Assert.That(CoreEconomyBalance.IsDispatchReady(0, end.ToString(), end + 1), Is.True);
        }

        [Test]
        public void EventCannotWrapAccumulatedFameToZero()
        {
            var session = new CoreVoyageSession();
            session.StartVoyage();
            session.Cargo.Fame = int.MaxValue;
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, CoreDemoPhase.Encounter);
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session,
                new CoreEncounter("fame-limit", "声望奖励", "事件", "", false, false, 0, 0,
                    new CoreEncounterChoice("领取", "声望 +5", 0, new VoyageEffect { Fame = 5 })));
            Assert.That(session.IsChoiceAvailable(0), Is.False);
            Assert.That(session.ResolveChoice(0), Is.False);
            Assert.That(session.Cargo.Fame, Is.EqualTo(int.MaxValue));
        }

        [TestCase(-1, "1")]
        [TestCase(99, "1")]
        [TestCase(0, "0")]
        [TestCase(0, "-1")]
        [TestCase(0, "invalid")]
        [TestCase(0, "9223372036854775807")]
        public void MalformedDispatchRecordCannotBecomeACompletedReward(int mission, string end)
        {
            Assert.That(CoreEconomyBalance.IsValidDispatchRecord(mission, end, out _), Is.False);
            Assert.That(CoreEconomyBalance.IsDispatchReady(mission, end, DateTime.MaxValue.Ticks), Is.False);
        }
    }
}
