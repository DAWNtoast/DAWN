using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreShipMarketCatalogTests
    {
        [TestCase(0, "sloop")]
        [TestCase(1, "merchant")]
        [TestCase(2, "patrol")]
        [TestCase(3, "relic")]
        public void ExistingHullSaveIndicesAndArtworkRemainCompatibleWhenStatsAreRebalanced(int index, string id)
        {
            CoreShipDefinition ship = CoreVoyageSession.Ships[index];
            Assert.That(ship.Id, Is.EqualTo(id));
            Assert.That(CoreShipMarketCatalog.ArtworkIndex(index), Is.EqualTo(index));
        }

        [Test]
        public void StarterConstantsMatchThePlayableHullAndItsUnmodifiedNewGameCargoFits()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.SelectedShipIndex, Is.Zero);
            Assert.That(session.MaxHull, Is.EqualTo(CoreVoyageSession.ShipMaxHull));
            Assert.That(session.Armor, Is.EqualTo(CoreVoyageSession.ShipArmor));
            Assert.That(session.Firepower, Is.EqualTo(CoreVoyageSession.ShipFirepower));
            Assert.That(session.SpeedKnots, Is.EqualTo(CoreVoyageSession.ShipSpeedKnots));
            Assert.That(session.CargoCapacityValue, Is.EqualTo(CoreVoyageSession.CargoCapacity));
            Assert.That(session.CrewCountValue, Is.EqualTo(CoreVoyageSession.CrewCount));
            Assert.That(CoreShipMarketCatalog.PurchaseCost(0), Is.Zero);
            Assert.That(session.Cargo.UsedVolume, Is.GreaterThan(0f));
            Assert.That(session.CargoCapacityValue - session.Cargo.UsedVolume, Is.GreaterThanOrEqualTo(10f),
                "The new hull must fit real starting rations, timber and ammunition without deleting cargo.");

            session.ConfigureCrewCapabilities(true, false, false, false, false, false);
            session.ConfigureDepartureCrew(session.CrewCountValue - 1, 1);
            Assert.That(session.IsShortStaffed, Is.False, "The tutorial navigator must leave enough ordinary sailors.");
            Assert.That(CoreBattleBalance.BoardingHealthForOrdinaryCrew(session.SelectedOrdinaryCrew),
                Is.GreaterThan(CoreGuidedTutorialContent.BossProfile.BoardingHealth));
        }

        [TestCase(0, new[] { 0, 1, 4 })]
        [TestCase(1, new[] { 0, 5, 6 })]
        [TestCase(2, new[] { 1, 2, 7 })]
        [TestCase(3, new[] { 1, 8, 9 })]
        [TestCase(4, new[] { 3, 10 })]
        [TestCase(5, new[] { 0, 11 })]
        public void EachPortHasItsOwnStockAndAtLeastOneExclusiveHull(int port, int[] expected)
        {
            IReadOnlyList<int> stock = CoreShipMarketCatalog.ShipsForPort(port);
            CollectionAssert.AreEqual(expected, stock);
            Assert.That(stock.Distinct().Count(), Is.EqualTo(stock.Count));
            Assert.That(stock.Any(ship => Enumerable.Range(0, 6).Count(other => CoreShipMarketCatalog.IsSoldAtPort(other, ship)) == 1), Is.True);
            for (int ship = 0; ship < CoreVoyageSession.Ships.Count; ship++)
                Assert.That(CoreShipMarketCatalog.IsSoldAtPort(port, ship), Is.EqualTo(expected.Contains(ship)));
            Assert.Throws<NotSupportedException>(() => ((IList<int>)stock)[0] = 11);
        }

        [Test]
        public void AllTwelveHullRecordsHaveReachableMarketsArtworkAndIndividualLore()
        {
            string[] ids = { "sloop", "merchant", "patrol", "relic", "salvage", "courier", "provision", "gunship", "barge", "privateer", "occult", "clipper" };
            CollectionAssert.AreEqual(ids, CoreVoyageSession.Ships.Select(ship => ship.Id));
            Assert.That(CoreVoyageSession.Ships.Select(ship => ship.Name).Distinct().Count(), Is.EqualTo(12));
            for (int index = 0; index < ids.Length; index++)
            {
                CoreShipDefinition ship = CoreVoyageSession.Ships[index];
                int home = CoreShipMarketCatalog.HomePortIndex(index);
                Assert.That(home, Is.InRange(0, CoreVoyageSession.DestinationPorts.Count - 1));
                Assert.That(CoreShipMarketCatalog.IsSoldAtPort(home, index), Is.True, ship.Id);
                Assert.That(CoreShipMarketCatalog.ArtworkIndex(index), Is.InRange(0, 3));
                Assert.That(CoreShipMarketCatalog.Role(index), Is.Not.Empty);
                Assert.That(CoreShipMarketCatalog.Specialty(index), Is.Not.Empty);
                Assert.That(CoreShipMarketCatalog.Specialty(index).Length, Is.LessThanOrEqualTo(40), ship.Id);
                if (index > 0)
                    Assert.That(CoreShipMarketCatalog.PurchaseCost(index), Is.InRange(1, CoreEconomyBalance.MaximumProfileGold));
                Assert.That(ship.Lore, Is.Not.Empty);
                Assert.That(SeventhPortLoreCatalog.ShipLoreFor(ship.Id).Description, Is.EqualTo(ship.Lore));
                Assert.That(SeventhPortLoreCatalog.StoryLinks.Any(link => link.ShipIds.Contains(ship.Id)), Is.True, ship.Id);
                if (index >= 4)
                {
                    Assert.That(Enumerable.Range(0, 6).Count(port => CoreShipMarketCatalog.IsSoldAtPort(port, index)), Is.EqualTo(1));
                }
            }
            Assert.That(CoreVoyageSession.Ships.Select(ship => ship.Lore).Distinct().Count(), Is.EqualTo(12));
        }

        [TestCase(-1)] [TestCase(int.MinValue)] [TestCase(12)] [TestCase(int.MaxValue)]
        public void InvalidShipIndicesCannotBeOfferedOrBecomeFreePurchases(int index)
        {
            Assert.That(CoreShipMarketCatalog.PurchaseCost(index), Is.GreaterThan(CoreEconomyBalance.MaximumProfileGold));
            Assert.That(CoreShipMarketCatalog.HomePortIndex(index), Is.EqualTo(-1));
            Assert.That(CoreShipMarketCatalog.Role(index), Is.Empty);
            Assert.That(CoreShipMarketCatalog.Specialty(index), Is.Empty);
            Assert.That(CoreShipMarketCatalog.ArtworkIndex(index), Is.InRange(0, 3));
            foreach (int port in Enumerable.Range(0, 6)) Assert.That(CoreShipMarketCatalog.IsSoldAtPort(port, index), Is.False);
        }

        [TestCase(-1)] [TestCase(int.MinValue)] [TestCase(6)] [TestCase(int.MaxValue)]
        public void InvalidPortsHaveNoStock(int port)
        {
            Assert.That(CoreShipMarketCatalog.ShipsForPort(port), Is.Empty);
            foreach (int ship in Enumerable.Range(0, 12)) Assert.That(CoreShipMarketCatalog.IsSoldAtPort(port, ship), Is.False);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)]
        public void AnyHullCanBeSelectedDirectlyAtLevelOneAndCarryEightDaysPlusCombatReserves(int index)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.TryDepositAllPortCargo(), Is.True);
            Assert.That(session.SelectShip(index), Is.True);
            CoreShipDefinition ship = session.SelectedShip;
            Assert.That(session.PirateLevel, Is.EqualTo(1), "Hull selection adds no progression gate.");
            Assert.That(session.SelectedOrdinaryCrew, Is.EqualTo(ship.CrewCount));
            Assert.That(session.EffectiveCrewCount, Is.EqualTo(ship.CrewCount));
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            Assert.That(session.EstimateDailyRationConsumption(ship.CrewCount, false), Is.EqualTo(ship.CrewCount));
            double cookedRations = session.EstimateDailyRationConsumption(ship.CrewCount, true);
            Assert.That(cookedRations, Is.LessThan(ship.CrewCount));
            int eightDays = (int)Math.Ceiling(session.EstimateDailyRationConsumption(ship.CrewCount, false) * 8d);
            session.Cargo.Water = session.Cargo.Food = eightDays;
            Assert.That(session.Cargo.UsedVolume, Is.LessThan(ship.CargoCapacity * .4f), "Full crew provisions must leave room for the ship's purpose.");
            session.Cargo.Cannonballs = 6; session.Cargo.Powder = 3; session.Cargo.Wood = 8;
            Assert.That(ship.CargoCapacity - session.Cargo.UsedVolume, Is.GreaterThanOrEqualTo(20f));
            session.StartVoyage();
            Assert.That(session.Hull, Is.EqualTo(ship.MaxHull));
            Assert.That(session.EffectiveFirepower, Is.EqualTo(ship.Firepower));
            Assert.That(session.EffectiveArmor, Is.EqualTo(ship.Armor));
            Assert.That(session.EffectiveSpeedKnots, Is.EqualTo(ship.SpeedKnots));
            Assert.That(session.WaterSupplyHours, Is.GreaterThanOrEqualTo(8d * 24d));
            Assert.That(session.GetForecast().WaterCost, Is.GreaterThan(0));
        }

        [Test]
        public void HeavyGunshipCarriesTwentySixDaysAndCombatReservesWithoutRequiringACook()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.TryDepositAllPortCargo(), Is.True);
            Assert.That(session.SelectShip(7), Is.True, "Saving for a top-tier hull requires no intermediate ship.");
            Assert.That(session.PirateLevel, Is.EqualTo(1));
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            int days = Math.Max(26, CoreTreasureHuntCatalog.FindRank("moonlit").RecommendedVoyageDays);
            int rations = (int)Math.Ceiling(session.EstimateDailyRationConsumption(session.CrewCountValue, false) * days);
            session.Cargo.Water = session.Cargo.Food = rations;
            session.Cargo.Cannonballs = 24;
            session.Cargo.Powder = 12;
            session.Cargo.Wood = 8;
            session.Cargo.Medicine = 4;
            Assert.That(session.CargoCapacityValue - session.Cargo.UsedVolume, Is.GreaterThanOrEqualTo(25f),
                "Full crew rations and a practical ammunition/repair load must leave room for expedition loot.");
            session.StartVoyage();
            Assert.That(session.EffectiveCrewCount, Is.EqualTo(session.CrewCountValue));
            Assert.That(session.WaterSupplyHours, Is.GreaterThanOrEqualTo(days * 24d));
            Assert.That(session.FoodSupplyHours, Is.GreaterThanOrEqualTo(days * 24d));
        }

        [Test]
        public void SpecialistCapacityRequiresATradeoffOnLightHullsButNotLargerShips()
        {
            foreach (int index in Enumerable.Range(0, CoreVoyageSession.Ships.Count))
            {
                var session = new CoreVoyageSession();
                Assert.That(session.TryDepositAllPortCargo(), Is.True);
                Assert.That(session.SelectShip(index), Is.True);
                session.ConfigureDepartureCrew(session.CrewCountValue - 6, 6);
                bool lightHull = index == 0 || index == 5;
                Assert.That(session.IsShortStaffed, Is.EqualTo(lightHull), session.SelectedShip.Id);
                Assert.That(session.EffectiveCrewCount, Is.EqualTo(session.CrewCountValue));
                if (lightHull)
                {
                    Assert.That(session.EffectiveSpeedKnots, Is.LessThan(session.SpeedKnots));
                    Assert.That(session.EffectiveFirepower, Is.LessThan(session.Firepower));
                    session.ConfigureDepartureCrew(session.CrewCountValue - 5, 5);
                    Assert.That(session.IsShortStaffed, Is.False, "One specialist can stand by to retain an effective sailing crew.");
                }
            }
        }

        [Test]
        public void CombatHullGrowthImprovesDurabilityAndFirepowerAtAnIncreasingPurchaseCost()
        {
            var ships = CoreVoyageSession.Ships;
            int[] combatProgression = { 0, 2, 7 };
            for (int stage = 1; stage < combatProgression.Length; stage++)
            {
                int earlier = combatProgression[stage - 1], later = combatProgression[stage];
                Assert.That(ships[later].MaxHull, Is.GreaterThan(ships[earlier].MaxHull));
                Assert.That(ships[later].Armor, Is.GreaterThan(ships[earlier].Armor));
                Assert.That(ships[later].Firepower, Is.GreaterThan(ships[earlier].Firepower));
                Assert.That(ships[later].CrewCount, Is.GreaterThan(ships[earlier].CrewCount));
                Assert.That(CoreShipMarketCatalog.PurchaseCost(later), Is.GreaterThan(CoreShipMarketCatalog.PurchaseCost(earlier)));
            }
            Assert.That(ships[7].MaxHull, Is.EqualTo(ships.Max(ship => ship.MaxHull)));
            Assert.That(ships[7].Armor, Is.EqualTo(ships.Max(ship => ship.Armor)));
            Assert.That(ships[7].Firepower, Is.EqualTo(ships.Max(ship => ship.Firepower)));
        }

        [Test]
        public void SpecializedHullAdvantagesKeepConcreteTransportExplorationAndCombatTradeoffs()
        {
            var ships = CoreVoyageSession.Ships;
            Assert.That(ships[1].CargoCapacity, Is.GreaterThan(ships[0].CargoCapacity));
            Assert.That(Enumerable.Range(2, ships.Count - 2).All(index =>
                CoreShipMarketCatalog.PurchaseCost(index) > CoreShipMarketCatalog.PurchaseCost(1)), Is.True,
                "The first merchant remains the least expensive paid cargo upgrade.");
            Assert.That(ships[4].CargoCapacity, Is.GreaterThan(ships[1].CargoCapacity));
            Assert.That(ships[4].MaxHull, Is.GreaterThan(ships[1].MaxHull));
            Assert.That(ships[4].SpeedKnots, Is.LessThan(ships[1].SpeedKnots));
            Assert.That(ships[5].SpeedKnots, Is.EqualTo(ships.Max(ship => ship.SpeedKnots)));
            Assert.That(ships[5].CargoCapacity, Is.EqualTo(ships.Min(ship => ship.CargoCapacity)));
            Assert.That(ships[5].CrewCount, Is.EqualTo(ships.Min(ship => ship.CrewCount)));
            Assert.That(CoreShipMarketCatalog.PurchaseCost(5), Is.LessThan(CoreShipMarketCatalog.PurchaseCost(11)));
            Assert.That(ships[6].CargoCapacity, Is.GreaterThan(ships[1].CargoCapacity));
            Assert.That(ships[6].Firepower, Is.LessThan(ships[1].Firepower));
            Assert.That(ships[6].CrewCount, Is.LessThan(ships[8].CrewCount));
            Assert.That(ships[7].CrewCount, Is.GreaterThan(ships[2].CrewCount));
            Assert.That(ships[7].SpeedKnots, Is.LessThan(ships[2].SpeedKnots));
            Assert.That(ships[8].CargoCapacity, Is.EqualTo(ships.Max(ship => ship.CargoCapacity)));
            Assert.That(ships[8].SpeedKnots, Is.EqualTo(ships.Min(ship => ship.SpeedKnots)));
            Assert.That(CoreShipMarketCatalog.PurchaseCost(8), Is.LessThan(CoreShipMarketCatalog.PurchaseCost(11)));
            Assert.That(ships[9].CargoCapacity, Is.GreaterThan(ships[2].CargoCapacity));
            Assert.That(ships[9].Firepower, Is.LessThan(ships[7].Firepower));
            Assert.That(ships[9].Armor, Is.LessThan(ships[7].Armor));
            Assert.That(ships[10].SpeedKnots, Is.GreaterThan(ships[3].SpeedKnots));
            Assert.That(ships[10].Firepower, Is.GreaterThan(ships[3].Firepower));
            Assert.That(ships[10].CrewCount, Is.EqualTo(ships[3].CrewCount));
            Assert.That(ships[10].Armor, Is.LessThan(ships[2].Armor));
            Assert.That(ships[11].SpeedKnots, Is.GreaterThan(ships[1].SpeedKnots));
            Assert.That(ships[11].CargoCapacity, Is.GreaterThan(ships[1].CargoCapacity));
            Assert.That(ships[11].CargoCapacity, Is.LessThan(ships[8].CargoCapacity));
            Assert.That(ships[11].Armor, Is.LessThan(ships[7].Armor));
            Assert.That(ships[11].Firepower, Is.LessThan(ships[7].Firepower));
        }

        [Test]
        public void ALoadedLargeHullCannotSwitchIntoTheCourierAndLoseItsCargo()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.TryDepositAllPortCargo(), Is.True);
            Assert.That(session.SelectShip(8), Is.True);
            session.Cargo.Wood = 76;
            Assert.That(session.SelectShip(5), Is.False);
            Assert.That(session.SelectedShipIndex, Is.EqualTo(8));
            Assert.That(session.Cargo.Wood, Is.EqualTo(76));
            Assert.That(session.TryDepositPortCargo("wood", 1), Is.True);
            Assert.That(session.SelectShip(5), Is.True);
            Assert.That(session.Cargo.UsedVolume, Is.EqualTo(session.CargoCapacityValue));
        }
    }
}
