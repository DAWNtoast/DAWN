using System;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreRecommendedSupplyTests
    {
        [TestCase(0, 20, 16, 2, 8)]
        [TestCase(1, 30, 24, 4, 12)]
        [TestCase(2, 40, 32, 8, 12)]
        public void EmptyCombatStockIsPaidForOnceAtTheSelectedRouteReserve(int route, int powder, int balls, int medicine, int wood)
        {
            var session = Port(8, route);
            Assert.That(session.TryDepositAllPortCargo(), Is.True);
            VoyageSupplyOrder order = session.CreateRecommendedSupplyOrder();
            Assert.That(order.Powder, Is.EqualTo(powder));
            Assert.That(order.Cannonballs, Is.EqualTo(balls));
            Assert.That(order.Medicine, Is.EqualTo(medicine));
            Assert.That(order.Wood, Is.EqualTo(wood));
            decimal gold = session.ProfileGold;
            decimal cost = session.QuoteSupplyPurchase(order).Cost;
            Assert.That(session.TryPurchaseSupplies(order), Is.True);
            Assert.That(session.ProfileGold, Is.EqualTo(gold - cost));
            Assert.That(session.CreateRecommendedSupplyOrder().TotalUnits, Is.Zero);
            Assert.That(session.RestockAtPort(), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(gold - cost));
        }

        [TestCase(0, false)] [TestCase(1, false)] [TestCase(2, false)]
        [TestCase(0, true)] [TestCase(1, true)] [TestCase(2, true)]
        public void EveryHullGetsAPurchasableOrderWithoutRemovingExistingCargo(int route, bool cook)
        {
            for (int ship = 0; ship < CoreVoyageSession.Ships.Count; ship++)
            {
                var session = Port(ship, route, cook);
                int water = session.Cargo.Water, food = session.Cargo.Food, powder = session.Cargo.Powder;
                float volume = session.Cargo.UsedVolume;
                decimal gold = session.ProfileGold;
                VoyageSupplyOrder order = session.CreateRecommendedSupplyOrder();
                Assert.That(session.Cargo.UsedVolume, Is.EqualTo(volume), "Recommendation must not mutate cargo.");
                Assert.That(session.ProfileGold, Is.EqualTo(gold));
                Assert.That(session.QuoteSupplyPurchase(order).FitsCargo, Is.True, session.SelectedShip.Id);
                Assert.That(session.TryPurchaseSupplies(order), Is.True, session.SelectedShip.Id);
                Assert.That(session.Cargo.Water, Is.GreaterThanOrEqualTo(water));
                Assert.That(session.Cargo.Food, Is.GreaterThanOrEqualTo(food));
                Assert.That(session.Cargo.Powder, Is.GreaterThanOrEqualTo(powder));
                Assert.That(session.Cargo.UsedVolume, Is.LessThanOrEqualTo(session.CargoCapacityValue + .001f));
                Assert.That(session.CreateRecommendedSupplyOrder().TotalUnits, Is.Zero, "A second click must not add a different hidden reserve.");
            }
        }

        [Test]
        public void FreshStarterCanAffordTheWholeFirstRouteRecommendationAndKeepsItsCombatStock()
        {
            var session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            VoyageSupplyOrder order = session.CreateRecommendedSupplyOrder();
            Assert.That(order.Powder, Is.Zero);
            Assert.That(order.Cannonballs, Is.Zero);
            Assert.That(120 + order.Water, Is.EqualTo(260));
            Assert.That(120 + order.Food, Is.EqualTo(260));
            Assert.That(session.QuoteSupplyPurchase(order).CanPurchase, Is.True);
        }

        [Test]
        public void DraftCrewCookAndRationComponentUseTheSameActualDailyConsumptionAsSailing()
        {
            var session = Port(8, 0);
            session.TryDepositAllPortCargo();
            session.ConfigureComponentContextEffect(0, 0, 0, 0f, 0f, 1.2f);
            int crewBefore = session.EffectiveCrewCount;
            VoyageSupplyOrder draft = session.CreateRecommendedSupplyOrder(6, true);
            Assert.That(draft.Water, Is.EqualTo(169)); // ceil(6 * .9 * 1.2 * 26)
            Assert.That(draft.Food, Is.EqualTo(169));
            Assert.That(session.EffectiveCrewCount, Is.EqualTo(crewBefore), "Previewing a draft must not commit crew changes.");
            session.ConfigureDepartureCrew(6, 0);
            session.ConfigureCrewCapabilities(false, false, false, false, true, false);
            VoyageSupplyOrder committed = session.CreateRecommendedSupplyOrder();
            Assert.That(committed.Water, Is.EqualTo(draft.Water));
            Assert.That(committed.Food, Is.EqualTo(draft.Food));
        }

        [TestCase("weathered", 20)] [TestCase("gilded", 24)] [TestCase("moonlit", 28)]
        public void TreasureUsesItsOwnVoyageDurationAndFullLegendaryGuardReserves(string rank, int days)
        {
            var session = Port(8, 0);
            session.TryDepositAllPortCargo();
            session.ConfigureDepartureCrew(10, 0);
            CoreTreasureProgress progress = session.CaptureTreasureProgress();
            progress.pirateExperience = 900;
            progress.maps = new[] { new CoreTreasureMapSave { id = "supply-map", seed = "supply-" + rank, rankId = rank, mapGeometryVersion = 1, contentVersion = 1 } };
            session.RestoreTreasureProgress(progress);
            Assert.That(session.TrySelectTreasureMap("supply-map", out string reason), Is.True, reason);
            VoyageSupplyOrder order = session.CreateRecommendedSupplyOrder();
            Assert.That(order.Water, Is.EqualTo(10 * days));
            Assert.That(order.Food, Is.EqualTo(10 * days));
            Assert.That(order.Powder, Is.EqualTo(40));
            Assert.That(order.Cannonballs, Is.EqualTo(32));
            Assert.That(order.Medicine, Is.EqualTo(8));
        }

        [Test]
        public void PatrolKeepsTwoLegendaryBattleBudgetsBeforeAddingOptionalRationMargin()
        {
            var session = Port(2, 2);
            session.TryDepositAllPortCargo();
            VoyageSupplyOrder order = session.CreateRecommendedSupplyOrder();
            Assert.That(order.Powder, Is.GreaterThanOrEqualTo(30), "Two observed worst single fights each consume 15 powder.");
            Assert.That(order.Cannonballs, Is.GreaterThanOrEqualTo(28), "Two observed worst single fights each consume 14 balls.");
            Assert.That(order.Water / 22d, Is.GreaterThanOrEqualTo(18d));
            Assert.That(order.Food / 22d, Is.GreaterThanOrEqualTo(18d));
            Assert.That(order.Water, Is.LessThan(session.RecommendedSupplyTarget(VoyageSupplyType.Water)), "The UI must expose this capacity shortfall.");
            Assert.That(session.QuoteSupplyPurchase(order).FitsCargo, Is.True);
        }

        [Test]
        public void SmallLateGameHoldBalancesPowderAndBallsInsteadOfFillingOnlyOne()
        {
            var session = Port(0, 2);
            session.TryDepositAllPortCargo();
            VoyageSupplyOrder order = session.CreateRecommendedSupplyOrder();
            Assert.That(order.Powder, Is.GreaterThan(0));
            Assert.That(order.Cannonballs, Is.GreaterThan(0));
            Assert.That(Math.Abs(order.Powder / 40d - order.Cannonballs / 32d), Is.LessThan(.08d));
            Assert.That(order.Water, Is.GreaterThanOrEqualTo(180));
            Assert.That(order.Food, Is.GreaterThanOrEqualTo(180));
            Assert.That(session.QuoteSupplyPurchase(order).FitsCargo, Is.True);
        }

        [Test]
        public void FullHoldCannotMintSuppliesOrDiscardValuables()
        {
            var session = Port(0, 2);
            session.Cargo.Iron = 100;
            float volume = session.Cargo.UsedVolume;
            decimal gold = session.ProfileGold;
            Assert.That(session.CreateRecommendedSupplyOrder().TotalUnits, Is.Zero);
            Assert.That(session.RestockAtPort(), Is.False);
            Assert.That(session.Cargo.UsedVolume, Is.EqualTo(volume));
            Assert.That(session.Cargo.Iron, Is.EqualTo(100));
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
        }

        private static CoreVoyageSession Port(int ship, int route, bool cook = false)
        {
            var session = new CoreVoyageSession();
            session.SelectShip(ship);
            session.RestoreProfile(10000m, session.CurrentPort);
            session.ConfigureDepartureCrew(session.CrewCountValue, 0);
            session.ConfigureCrewCapabilities(false, false, false, false, cook, false);
            CoreTreasureProgress progress = session.CaptureTreasureProgress();
            progress.pirateExperience = 900;
            session.RestoreTreasureProgress(progress);
            session.SelectRoute(route);
            return session;
        }
    }
}
