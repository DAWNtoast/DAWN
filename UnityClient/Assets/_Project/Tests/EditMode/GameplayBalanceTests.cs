using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;

namespace ShatteredSail.Core.Tests
{
    public sealed class GameplayBalanceTests
    {
        [Test]
        public void ForcedEventWithNoAffordableChoiceOffersNoRewardEmergencyExit()
        {
            CoreVoyageSession session = Voyage();
            session.Cargo.Water = 0;
            SetEncounter(session, new CoreEncounter("stranded", "搁浅", "事件", "", true, false, 0, 0,
                new CoreEncounterChoice("等待", "一天", 24, new VoyageEffect { Gold = 100 })));
            Assert.That(session.CanRequestEmergencyTow(), Is.True);
            Assert.That(session.RequestEmergencyTow(), Is.True);
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            Assert.That(session.Settlement.BankedGold, Is.Zero);
            Assert.That(session.RequestEmergencyTow(), Is.False);
        }

        [Test]
        public void BattleReward_IsDeferredAndFailureDoesNotPayThePromise()
        {
            CoreVoyageSession session = Voyage();
            int gold = session.Cargo.Gold;
            SetEncounter(session, new CoreEncounter("test-fight", "测试敌舰", "战斗", "", true, false, 2, 95,
                new CoreEncounterChoice("战斗", "胜利金币 +110", 0, new VoyageEffect { Gold = 110 }, startsBattle: true)));
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.Cargo.Gold, Is.EqualTo(gold));
            for (int turn = 0; turn < 100 && session.Phase == CoreDemoPhase.Battle; turn++) session.EndBattleTurn();
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            Assert.That(session.Settlement.BankedGold, Is.Zero);
        }

        [Test]
        public void Settlement_OnlyBanksUnspentNetLootAndCannotDoubleClaim()
        {
            CoreVoyageSession session = Voyage();
            decimal profile = session.ProfileGold;
            Apply(session, new VoyageEffect { Gold = -30 });
            Apply(session, new VoyageEffect { Gold = 110, Wood = 12, Iron = 4 });
            Apply(session, new VoyageEffect { Wood = -5, Iron = -1 });
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.BankedGold, Is.EqualTo(68));
            Assert.That(session.Settlement.BankedWood, Is.EqualTo(5));
            Assert.That(session.Settlement.BankedIron, Is.EqualTo(2));
            Assert.That(session.Cargo.Wood, Is.EqualTo(13));
            Assert.That(session.Cargo.Iron, Is.EqualTo(2));
            Assert.That(session.Cargo.Gold, Is.EqualTo(60), "Cash transferred to the account must leave the aboard purse.");
            session.ReturnToPortAfterSettlement(); session.ReturnToPortAfterSettlement();
            Assert.That(session.ProfileGold, Is.EqualTo(profile + 68 + 60), "Return the remaining travel cash as principal, not as new loot.");
            Assert.That(session.Cargo.Gold, Is.Zero);
            session.StartVoyage(); session.SafeReturn(); session.ReturnToPortAfterSettlement();
            Assert.That(session.ProfileGold, Is.EqualTo(profile + 68 + 60), "An empty repeat voyage cannot bank old loot again.");
            Assert.That(session.Cargo.Gold, Is.Zero);
        }

        [Test]
        public void VictoryPaysTheExistingRewardOnceAndNeverBeforeVictory()
        {
            CoreVoyageSession session = Voyage();
            SetEncounter(session, new CoreEncounter("reward", "测试敌舰", "战斗", "", true, false, 1, 70,
                new CoreEncounterChoice("迎战", "金币 +110", 0, new VoyageEffect { Gold = 110 }, startsBattle: true)));
            int initial = session.Cargo.Gold;
            session.ResolveChoice(0);
            Assert.That(session.Cargo.Gold, Is.EqualTo(initial));
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(session.Cargo.Gold, Is.EqualTo(initial), "Finishing preparation must not claim the promised victory reward.");
            session.Battle.DistanceMeters = 350f;
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            session.Battle.EnemyHull = 1;
            session.Battle.EnemyBlock = 0;
            session.Battle.Hand.Clear(); session.Battle.Hand.Add(0);
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            // Preserve the existing combat economy: choice reward plus the original 45-gold victory award.
            Assert.That(session.Cargo.Gold, Is.EqualTo(initial + 110 + 45));
            Assert.That(session.PlayCard(0), Is.False);
            Assert.That(session.ResolveChoice(0), Is.False);
            Assert.That(session.Cargo.Gold, Is.EqualTo(initial + 110 + 45));
        }

        [Test]
        public void EventQuote_UsesNetCargoAndIncludesElapsedRations()
        {
            CoreVoyageSession session = Voyage();
            session.Cargo.Wood += (int)(session.CargoCapacityValue - session.Cargo.UsedVolume);
            SetEncounter(session, new CoreEncounter("exchange", "交换", "贸易", "", false, false, 0, 0,
                new CoreEncounterChoice("交换", "木材 -6、铁矿 +3", 1, new VoyageEffect { Wood = -6, Iron = 3 })));
            Assert.That(session.IsChoiceAvailable(0), Is.True, "Equal-volume trades must work in a full hold.");
            Assert.That(session.ResolveChoice(0), Is.True);
            session.Cargo.Water = 0;
            SetEncounter(session, new CoreEncounter("time", "耗时", "事件", "", false, false, 0, 0,
                new CoreEncounterChoice("等待", "等待一天", 24, new VoyageEffect())));
            Assert.That(session.IsChoiceAvailable(0), Is.False);
        }

        [Test]
        public void DoctorCannotTurnOneMedicineTradeIntoFreeGold()
        {
            var session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(false, false, true, false, false, false); session.StartVoyage();
            session.Cargo.Medicine = 0;
            SetEncounter(session, new CoreEncounter("medicine", "药材交易", "贸易", "", false, false, 0, 0,
                new CoreEncounterChoice("交换", "药品 -1", 0, new VoyageEffect { Medicine = -1, Gold = 28 })));
            Assert.That(session.IsChoiceAvailable(0), Is.False);
        }

        [Test]
        public void DoctorMedicineDiscountRoundsUpAndEventRepairBonusMatchesItsQuote()
        {
            foreach (int medicineCost in new[] { 1, 2, 3, 4, 7, 8 })
            {
                var session = new CoreVoyageSession();
                session.ConfigureCrewCapabilities(false, false, true, false, false, false); session.StartVoyage();
                session.Cargo.Medicine = 10;
                typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, session.MaxHull - 20);
                SetEncounter(session, new CoreEncounter("treatment", "照料伤员", "事件", "", false, false, 0, 0,
                    new CoreEncounterChoice("治疗", "消耗药品，修复船体", 0,
                        new VoyageEffect { Medicine = -medicineCost, Hull = 5 })));
                Assert.That(session.IsChoiceAvailable(0), Is.True);
                Assert.That(session.ResolveChoice(0), Is.True);
                Assert.That(10 - session.Cargo.Medicine, Is.EqualTo((int)Math.Ceiling(medicineCost * 0.75d)));
                Assert.That(session.Hull, Is.EqualTo(session.MaxHull - 13));
            }
        }

        [Test]
        public void ExhaustedRationsStillAllowRescueButNotSafeReturnProtection()
        {
            CoreVoyageSession session = Voyage();
            session.Cargo.Water = session.Cargo.Food = 0;
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            Assert.That(session.Settlement.ProtectionRatio, Is.EqualTo(0.35f));
        }

        [Test]
        public void RecommendedSupplyTracksCrewAndRouteAndRestockingChargesGold()
        {
            foreach (int ship in Enumerable.Range(0, 4))
            foreach (int route in Enumerable.Range(0, 3))
            {
                var session = new CoreVoyageSession(); session.SelectShip(ship); SelectUnlockedRoute(session, route);
                session.Cargo.Water = session.Cargo.Food = 0;
                var order = session.CreateRecommendedSupplyOrder();
                Assert.That(order.Water, Is.EqualTo(order.Food));
                Assert.That(order.Water / (double)session.CrewCountValue, Is.GreaterThanOrEqualTo(13.5d));
                Assert.That(session.QuoteSupplyPurchase(order).FitsCargo, Is.True);
                decimal cost = session.QuoteSupplyPurchase(order).Cost;
                decimal gold = session.ProfileGold;
                Assert.That(session.RestockAtPort(), Is.True);
                Assert.That(session.ProfileGold, Is.EqualTo(gold - cost));
                Assert.That(session.RestockAtPort(), Is.False, "A full stock request must not reset the cargo or mint supplies.");
            }
        }

        [TestCase(0, 160)] [TestCase(1, 140)] [TestCase(2, 180)]
        public void StarterRecommendedLongVoyageSupplyStillFillsRouteEndurance(
            int route, int targetRations)
        {
            var session = new CoreVoyageSession();
            SelectUnlockedRoute(session, route);
            VoyageSupplyOrder order = session.CreateRecommendedSupplyOrder();

            Assert.That(order.Water, Is.EqualTo(targetRations - 120));
            Assert.That(order.Food, Is.EqualTo(targetRations - 120));
            Assert.That(order.Cannonballs, Is.Zero,
                "Existing starter ammunition must not be reduced to the lower restock reserve.");
            Assert.That(order.Powder, Is.Zero, "A fresh campaign already exceeds the twelve-charge reserve.");
            VoyageSupplyQuote quote = session.QuoteSupplyPurchase(order);
            decimal gold = session.ProfileGold;
            Assert.That(quote.CanPurchase, Is.True);
            Assert.That(session.RestockAtPort(), Is.True);
            Assert.That(session.Cargo.Water, Is.EqualTo(targetRations));
            Assert.That(session.Cargo.Food, Is.EqualTo(targetRations));
            Assert.That(session.Cargo.Cannonballs, Is.EqualTo(16));
            Assert.That(session.Cargo.Powder, Is.EqualTo(20));
            Assert.That(session.ProfileGold, Is.EqualTo(gold - quote.Cost));
            Assert.That(session.RestockAtPort(), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(gold - quote.Cost));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void RecommendedAmmunition_ReplenishesTwelvePowderAndTenBallsOnceWithoutGrantingFreeStock(int route)
        {
            var session = new CoreVoyageSession();
            SelectUnlockedRoute(session, route);
            session.Cargo.Powder = session.Cargo.Cannonballs = 0;
            VoyageSupplyOrder order = session.CreateRecommendedSupplyOrder();
            Assert.That(order.Powder, Is.EqualTo(12));
            Assert.That(order.Cannonballs, Is.EqualTo(10));
            VoyageSupplyQuote quote = session.QuoteSupplyPurchase(order);
            Assert.That(quote.CanPurchase, Is.True);
            Assert.That(quote.FitsCargo, Is.True);
            decimal before = session.ProfileGold;
            Assert.That(session.RestockAtPort(), Is.True);
            Assert.That(session.Cargo.Powder, Is.EqualTo(12));
            Assert.That(session.Cargo.Cannonballs, Is.EqualTo(10));
            Assert.That(session.ProfileGold, Is.EqualTo(before - quote.Cost));
            Assert.That(session.CreateRecommendedSupplyOrder().TotalUnits, Is.Zero);
            Assert.That(session.RestockAtPort(), Is.False);
            Assert.That(session.Cargo.Powder, Is.EqualTo(12));
            Assert.That(session.Cargo.Cannonballs, Is.EqualTo(10));
            Assert.That(session.ProfileGold, Is.EqualTo(before - quote.Cost));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void LegacyProvisionsAboveRecommendedReserveAreNotRemovedOrToppedUp(int route)
        {
            var session = new CoreVoyageSession();
            session.Cargo.Water = session.Cargo.Food = 286;
            session.Cargo.Cannonballs = 10;
            SelectUnlockedRoute(session, route);
            float volume = session.Cargo.UsedVolume;
            decimal gold = session.ProfileGold;

            Assert.That(session.CreateRecommendedSupplyOrder().TotalUnits, Is.Zero);
            Assert.That(session.RestockAtPort(), Is.False);
            Assert.That(session.Cargo.Water, Is.EqualTo(286));
            Assert.That(session.Cargo.Food, Is.EqualTo(286));
            Assert.That(session.Cargo.Cannonballs, Is.EqualTo(10));
            Assert.That(session.Cargo.UsedVolume, Is.EqualTo(volume));
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
        }

        [Test]
        public void WhiteShellBulkWaterCostDoesNotRoundUpAnExtraGoldFromFloatNoise()
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(500, "白贝港");
            Assert.That(session.CalculateSupplyPurchaseCost(VoyageSupplyType.Water, 500), Is.EqualTo(10.4m));
            Assert.That(session.QuoteSupplyPurchase(new VoyageSupplyOrder { Water = 500 }).Cost, Is.EqualTo(10.4m));
        }

        [Test]
        public void RecommendedSuppliesRespectExistingIronAndNeverDiscardCargo()
        {
            var ordinary = new CoreVoyageSession(); var laden = new CoreVoyageSession();
            ordinary.Cargo.Water = ordinary.Cargo.Food = 0;
            laden.Cargo.Water = laden.Cargo.Food = 0;
            laden.Cargo.Iron = 20;
            int wood = laden.Cargo.Wood, cloth = laden.Cargo.Cloth;
            var order = laden.CreateRecommendedSupplyOrder();
            Assert.That(order.Water, Is.GreaterThan(0));
            Assert.That(order.Food, Is.GreaterThan(0));
            Assert.That(order.Water + order.Food,
                Is.LessThan(ordinary.CreateRecommendedSupplyOrder().Water + ordinary.CreateRecommendedSupplyOrder().Food));
            Assert.That(laden.QuoteSupplyPurchase(order).FitsCargo, Is.True);
            Assert.That(laden.Cargo.Iron, Is.EqualTo(20), "Computing a recommendation must never discard valuable cargo.");
            Assert.That(laden.TryPurchaseSupplies(order), Is.True);
            Assert.That(laden.Cargo.Iron, Is.EqualTo(20));
            Assert.That(laden.Cargo.Wood, Is.EqualTo(wood));
            Assert.That(laden.Cargo.Cloth, Is.EqualTo(cloth));
            Assert.That(laden.Cargo.UsedVolume, Is.LessThanOrEqualTo(laden.CargoCapacityValue + 0.001f));
        }

        [TestCase("wood", 2)] [TestCase("iron", 6)] [TestCase("cloth", 4)]
        public void SellingCargoMaterialsTransfersExactlyTheQuotedValue(string material, int unitPrice)
        {
            var session = new CoreVoyageSession();
            session.Cargo.Wood = session.Cargo.Iron = session.Cargo.Cloth = 9;
            decimal gold = session.ProfileGold;
            Assert.That(session.TrySellCargoMaterial(material, 3), Is.True);
            Assert.That(session.ProfileGold, Is.EqualTo(gold + 3 * unitPrice));
            Assert.That(session.Cargo.Wood, Is.EqualTo(material == "wood" ? 6 : 9));
            Assert.That(session.Cargo.Iron, Is.EqualTo(material == "iron" ? 6 : 9));
            Assert.That(session.Cargo.Cloth, Is.EqualTo(material == "cloth" ? 6 : 9));
        }

        [Test]
        public void CargoSaleRejectsInvalidQuantitiesMaterialsAndNonPortPhasesAtomically()
        {
            var session = new CoreVoyageSession();
            session.Cargo.Wood = session.Cargo.Iron = session.Cargo.Cloth = 9;
            decimal gold = session.ProfileGold;
            foreach (string material in new[] { "wood", "iron", "cloth" })
            foreach (int quantity in new[] { 0, -1, 10, int.MaxValue })
                Assert.That(session.TrySellCargoMaterial(material, quantity), Is.False);
            foreach (string material in new[] { "gold", "unknown", "", null })
                Assert.That(session.TrySellCargoMaterial(material, 1), Is.False);
            foreach (CoreDemoPhase phase in new[] { CoreDemoPhase.Sailing, CoreDemoPhase.Encounter, CoreDemoPhase.Battle, CoreDemoPhase.Settlement })
            {
                typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, phase);
                Assert.That(session.TrySellCargoMaterial("iron", 1), Is.False);
            }
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(session.Cargo.Wood, Is.EqualTo(9));
            Assert.That(session.Cargo.Iron, Is.EqualTo(9));
            Assert.That(session.Cargo.Cloth, Is.EqualTo(9));
        }

        [Test]
        public void SellingExcessIronCanFundSuppliesAndRecoverAnOverloadedHold()
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(0, session.CurrentPort);
            session.Cargo.Water = session.Cargo.Food = 0;
            session.Cargo.Iron = 60;
            Assert.That(session.Cargo.UsedVolume, Is.GreaterThan(session.CargoCapacityValue));
            Assert.That(session.TrySellCargoMaterial("iron", 40), Is.True);
            Assert.That(session.Cargo.Iron, Is.EqualTo(20));
            Assert.That(session.ProfileGold, Is.EqualTo(240));
            var order = session.CreateRecommendedSupplyOrder();
            Assert.That(order.Water, Is.GreaterThan(0));
            Assert.That(order.Food, Is.GreaterThan(0));
            Assert.That(session.QuoteSupplyPurchase(order).CanPurchase, Is.True);
            Assert.That(session.TryPurchaseSupplies(order), Is.True);
            Assert.That(session.Cargo.Iron, Is.EqualTo(20));
            Assert.That(session.Cargo.UsedVolume, Is.LessThanOrEqualTo(session.CargoCapacityValue + 0.001f));
        }

        [Test]
        public void WoodResaleIsLowerThanEveryPortsSupplyPurchasePrice()
        {
            foreach (string port in new[] { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" })
            {
                var session = new CoreVoyageSession();
                session.RestoreProfile(500, port);
                Assert.That(session.CalculateSupplyPurchaseCost(VoyageSupplyType.Wood, 1), Is.GreaterThan(2), port);
                Assert.That(session.TryPurchaseSupplies(new VoyageSupplyOrder { Wood = 1 }), Is.True);
                Assert.That(session.TrySellCargoMaterial("wood", 1), Is.True);
                Assert.That(session.ProfileGold, Is.LessThan(500), "Buying and reselling must not print money at " + port);
            }
        }

        [Test]
        public void TravelCashDepositIsPortOnlyAndCannotBeClaimedTwice()
        {
            var session = new CoreVoyageSession();
            session.Cargo.Gold = 73;
            decimal gold = session.ProfileGold;
            Assert.That(session.TryDepositTravelCash(), Is.True);
            Assert.That(session.ProfileGold, Is.EqualTo(gold + 73));
            Assert.That(session.Cargo.Gold, Is.Zero);
            Assert.That(session.TryDepositTravelCash(), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(gold + 73));
            session.Cargo.Gold = 41;
            foreach (CoreDemoPhase phase in new[] { CoreDemoPhase.Sailing, CoreDemoPhase.Encounter, CoreDemoPhase.Battle, CoreDemoPhase.Settlement })
            {
                typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, phase);
                Assert.That(session.TryDepositTravelCash(), Is.False);
                Assert.That(session.Cargo.Gold, Is.EqualTo(41));
                Assert.That(session.ProfileGold, Is.EqualTo(gold + 73));
            }
        }

        [Test]
        public void ComponentPriceCurvePreservesRarityGrowthWithoutTwentyFoldPrices()
        {
            var catalog = new ComponentArtCatalog();
            foreach (var slot in catalog.Entries.GroupBy(entry => entry.Slot))
            {
                double first = 0, previous = 0;
                foreach (var rarity in slot.GroupBy(entry => entry.Rarity).OrderBy(group => group.Key))
                {
                    double average = rarity.Average(entry => PortComponentShopCatalog.CalculatePrice(entry, "断锚港", 1f));
                    Assert.That(average, Is.GreaterThan(previous));
                    if (first == 0) first = average;
                    previous = average;
                }
                Assert.That(previous / first, Is.InRange(8d, 12d));
            }
        }

        [TestCase(0, false)] [TestCase(1, false)] [TestCase(2, false)]
        [TestCase(0, true)] [TestCase(1, true)] [TestCase(2, true)]
        public void SeededSailingForecastMatchesActualTimeAndRations(int route, bool cook)
        {
            foreach (int ship in Enumerable.Range(0, CoreVoyageSession.Ships.Count))
            {
                var session = new CoreVoyageSession();
                session.SelectShip(ship); SelectUnlockedRoute(session, route);
                session.ConfigureCrewCapabilities(false, false, false, false, cook, false);
                session.SetSeed("NONCOMBAT-SAIL-" + route); session.StartVoyage();
                // Isolate navigation economics: retain generated terrain/currents, but do not resolve encounters.
                foreach (CoreMapNode node in session.Map.Nodes) node.Visited = true;
                session.SetCourseTo(session.Map.Destination);
                SailingForecast forecast = session.GetForecast();
                Assert.That(forecast.Blocked, Is.False);
                Assert.That(forecast.ActualDistanceNm, Is.GreaterThan(1f));
                int water = session.Cargo.Water, food = session.Cargo.Food;
                Assert.That(session.Sail(), Is.True);
                Assert.That(session.Battle, Is.Null, "The economics fixture must not execute combat.");
                Assert.That(session.TotalHours, Is.EqualTo(forecast.Hours).Within(0.000001d));
                Assert.That(session.TotalDistanceNm, Is.EqualTo(forecast.ActualDistanceNm).Within(0.001f));
                Assert.That(water - session.Cargo.Water, Is.EqualTo(forecast.WaterCost));
                Assert.That(food - session.Cargo.Food, Is.EqualTo(forecast.FoodCost));
                int expected = (int)Math.Ceiling(session.CrewCountValue * forecast.Hours / 24d *
                    (cook ? GameplayBalance.CookRationMultiplier : 1d));
                Assert.That(forecast.WaterCost, Is.EqualTo(expected));
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void SplittingEventTimeIntoHoursDoesNotAvoidRationCosts(bool cook)
        {
            var whole = new CoreVoyageSession(); var split = new CoreVoyageSession();
            foreach (var session in new[] { whole, split })
            {
                session.ConfigureCrewCapabilities(false, false, false, false, cook, false);
                session.StartVoyage();
            }
            int startingWater = whole.Cargo.Water;
            ResolveWaitingEvent(whole, 24d);
            for (int hour = 0; hour < 24; hour++) ResolveWaitingEvent(split, 1d);
            Assert.That(split.TotalHours, Is.EqualTo(whole.TotalHours));
            Assert.That(split.Cargo.Water, Is.EqualTo(whole.Cargo.Water));
            Assert.That(split.Cargo.Food, Is.EqualTo(whole.Cargo.Food));
            Assert.That(startingWater - whole.Cargo.Water, Is.EqualTo((int)Math.Ceiling(whole.CrewCountValue *
                (cook ? GameplayBalance.CookRationMultiplier : 1d))));
        }

        [Test]
        public void NavigatorImprovesOnlyPreparedVoyagesByOneKnot()
        {
            var ordinary = new CoreVoyageSession(); var navigator = new CoreVoyageSession();
            ordinary.ConfigureCrewCapabilities(false, false, false, false, false, false);
            navigator.ConfigureCrewCapabilities(true, false, false, false, false, false);
            foreach (var session in new[] { ordinary, navigator })
            {
                session.SetSeed("NONCOMBAT-NAVIGATOR"); session.StartVoyage();
                session.SetCourseTo(session.Map.Destination);
            }
            var normal = ordinary.GetForecast(); var improved = navigator.GetForecast();
            Assert.That(normal.Blocked, Is.False);
            Assert.That(improved.EffectiveSpeedKnots - normal.EffectiveSpeedKnots, Is.EqualTo(1f).Within(0.00001f));
            Assert.That(improved.Hours, Is.LessThan(normal.Hours));
            Assert.That(improved.WaterCost, Is.LessThanOrEqualTo(normal.WaterCost));
        }

        [Test]
        public void InvalidOrUnaffordableSupplyOrdersCannotMutateCargoOrFunds()
        {
            var session = new CoreVoyageSession();
            decimal gold = session.ProfileGold;
            int water = session.Cargo.Water;
            int wood = session.Cargo.Wood;
            Assert.That(session.TryPurchaseSupplies(new VoyageSupplyOrder { Water = -100, Wood = -2 }), Is.False);
            Assert.That(session.TryPurchaseSupplies(new VoyageSupplyOrder { Water = 100000 }), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(session.Cargo.Water, Is.EqualTo(water));
            Assert.That(session.Cargo.Wood, Is.EqualTo(wood));
            session.RestoreProfile(0, session.CurrentPort);
            Assert.That(session.TryPurchaseSupplies(new VoyageSupplyOrder { Wood = 1 }), Is.False);
            Assert.That(session.ProfileGold, Is.Zero);
            Assert.That(session.Cargo.Wood, Is.EqualTo(wood));
        }

        private static void SelectUnlockedRoute(CoreVoyageSession session, int route)
        {
            CoreTreasureProgress progress = session.CaptureTreasureProgress();
            progress.pirateExperience = Math.Max(progress.pirateExperience,
                CoreVoyageSession.PirateExperienceForLevel(CoreVoyageSession.Routes[route].UnlockLevel));
            session.RestoreTreasureProgress(progress);
            session.SelectRoute(route);
            Assert.That(session.SelectedRouteIndex, Is.EqualTo(route));
        }

        private static void ResolveWaitingEvent(CoreVoyageSession session, double hours)
        {
            SetEncounter(session, new CoreEncounter("wait", "候潮", "事件", "等待适航潮汐", false, false, 0, 0,
                new CoreEncounterChoice("等待", "消耗时间与补给", hours, new VoyageEffect())));
            Assert.That(session.ResolveChoice(0), Is.True);
        }

        private static CoreVoyageSession Voyage()
        {
            var session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(false, false, false, false, false, false); session.StartVoyage();
            return session;
        }

        private static void SetEncounter(CoreVoyageSession session, CoreEncounter encounter)
        {
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, CoreDemoPhase.Encounter);
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session, encounter);
        }
        private static void Apply(CoreVoyageSession session, VoyageEffect effect) => Invoke(session, "ApplyEffect", effect);
        private static void Invoke(CoreVoyageSession session, string name, params object[] args) =>
            typeof(CoreVoyageSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, args);
    }
}
