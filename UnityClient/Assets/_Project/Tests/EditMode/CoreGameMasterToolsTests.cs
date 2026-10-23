using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreGameMasterToolsTests
    {
        [TestCase("0", 0)]
        [TestCase("1", 1)]
        [TestCase("00042", 42)]
        [TestCase("2147483647", int.MaxValue)]
        public void ParseTreasureFragments_AcceptsNonnegativeAsciiIntegers(string input, int expected)
        {
            Assert.That(CoreGameMasterTools.TryParseTreasureFragments(input, out int fragments), Is.True);
            Assert.That(fragments, Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("-1")]
        [TestCase("+1")]
        [TestCase(" 1")]
        [TestCase("1 ")]
        [TestCase("1\n")]
        [TestCase("1.0")]
        [TestCase("1,000")]
        [TestCase("1e3")]
        [TestCase("１２３")]
        [TestCase("١٢٣")]
        [TestCase("2147483648")]
        [TestCase("9999999999999999999999")]
        public void ParseTreasureFragments_RejectsMalformedOrOverflowingInput(string input)
        {
            Assert.That(CoreGameMasterTools.TryParseTreasureFragments(input, out int fragments), Is.False);
            Assert.That(fragments, Is.Zero);
        }

        [Test]
        public void ParseTreasureFragments_RejectsOverlongInputWithoutTruncatingItsValue()
        {
            Assert.That(CoreGameMasterTools.TryParseTreasureFragments(new string('0', 63) + "1", out int allowed), Is.True);
            Assert.That(allowed, Is.EqualTo(1));
            Assert.That(CoreGameMasterTools.TryParseTreasureFragments(new string('0', 64) + "1", out int rejected), Is.False);
            Assert.That(rejected, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(42)]
        [TestCase(int.MaxValue)]
        public void SetTreasureFragments_InPortChangesOnlyUnidentifiedInventory(int fragments)
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(1234, "灰鸥港");
            session.RestoreTreasureProgress(new CoreTreasureProgress {
                pirateExperience = 900, fragments = 12,
                identifiedWeatheredFragments = 3, identifiedGildedFragments = 5, identifiedMoonlitFragments = 2,
                identificationSeed = "GM-KEEPS-IDENTIFICATION", identificationSequence = 17, moonlitPityCount = 2,
                selectedMapId = "gilded:HELD", maps = new[] {
                    new CoreTreasureMapSave { id = "gilded:HELD", seed = "HELD", rankId = "gilded" } },
                usedMaps = new[] { new CoreUsedTreasureMapSave { id = "weathered:USED", seed = "USED",
                    rankId = "weathered", departurePort = "白贝港", hasExplorationCoverage = true, explorationCoverage = .5f } },
                completedSeeds = new[] { "COMPLETED" }, issuedSeeds = new[] { "HELD", "USED", "COMPLETED" },
                gems = 2, ancientMaterials = 3, relics = 4, blackPearls = 5, ancientCompasses = 6, lanterns = 7, curses = 8,
            });
            CoreTreasureProgress expected = session.CaptureTreasureProgress();
            expected.fragments = fragments;
            var cargo = session.Cargo;
            int cargoGold = cargo.Gold;
            int water = cargo.Water;
            int ship = session.SelectedShipIndex;
            int voyageFragments = session.VoyageTreasureFragments;

            Assert.That(session.TryGameMasterSetTreasureFragments(fragments), Is.True);

            Assert.That(session.TreasureFragments, Is.EqualTo(fragments));
            Assert.That(UnityEngine.JsonUtility.ToJson(session.CaptureTreasureProgress()),
                Is.EqualTo(UnityEngine.JsonUtility.ToJson(expected)), "GM must preserve identified fragments, maps and identification progress.");
            Assert.That(session.ProfileGold, Is.EqualTo(1234));
            Assert.That(session.Cargo, Is.SameAs(cargo));
            Assert.That(cargo.Gold, Is.EqualTo(cargoGold));
            Assert.That(cargo.Water, Is.EqualTo(water));
            Assert.That(session.SelectedShipIndex, Is.EqualTo(ship));
            Assert.That(session.VoyageTreasureFragments, Is.EqualTo(voyageFragments));
            Assert.That(session.CurrentPort, Is.EqualTo("灰鸥港"));
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
        }

        [TestCase(CoreDemoPhase.Sailing)]
        [TestCase(CoreDemoPhase.Encounter)]
        [TestCase(CoreDemoPhase.Battle)]
        [TestCase(CoreDemoPhase.Settlement)]
        public void SetTreasureFragments_RejectsEveryNonPortPhaseWithoutChangingProgress(CoreDemoPhase phase)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { fragments = 12, identifiedWeatheredFragments = 3 });
            typeof(CoreVoyageSession).GetProperty("Phase", BindingFlags.Public | BindingFlags.Instance).SetValue(session, phase);
            string before = UnityEngine.JsonUtility.ToJson(session.CaptureTreasureProgress());
            int logCount = session.LogEntries.Count;
            Assert.That(session.TryGameMasterSetTreasureFragments(0), Is.False);
            Assert.That(UnityEngine.JsonUtility.ToJson(session.CaptureTreasureProgress()), Is.EqualTo(before));
            Assert.That(session.LogEntries.Count, Is.EqualTo(logCount));
        }

        [TestCase(-1)]
        [TestCase(int.MinValue)]
        public void SetTreasureFragments_RejectsNegativeCountsWithoutClamping(int fragments)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { fragments = 12, identifiedWeatheredFragments = 3 });
            string before = UnityEngine.JsonUtility.ToJson(session.CaptureTreasureProgress());
            int logCount = session.LogEntries.Count;
            Assert.That(session.TryGameMasterSetTreasureFragments(fragments), Is.False);
            Assert.That(UnityEngine.JsonUtility.ToJson(session.CaptureTreasureProgress()), Is.EqualTo(before));
            Assert.That(session.LogEntries.Count, Is.EqualTo(logCount));
        }

        [Test]
        public void SetTreasureFragments_UsesNormalIdentificationAndCraftingRules()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.TryGameMasterSetTreasureFragments(4), Is.True);
            Assert.That(session.TryIdentifyTreasureFragment(out _), Is.False, "Setting inventory must not unlock identification early.");
            Assert.That(session.TreasureFragments, Is.EqualTo(4));

            CoreTreasureProgress unlocked = session.CaptureTreasureProgress();
            unlocked.pirateExperience = 100;
            session.RestoreTreasureProgress(unlocked);
            for (int index = 0; index < 4; index++)
                Assert.That(session.TryIdentifyTreasureFragment(out string reason), Is.True, reason);
            Assert.That(session.TreasureFragments, Is.Zero);
            Assert.That(session.GetIdentifiedTreasureFragments("weathered"), Is.EqualTo(4));
            Assert.That(session.TryCraftTreasureMapForRank("weathered", "GM-INTERACTION", out string craftReason), Is.True, craftReason);
            Assert.That(session.GetIdentifiedTreasureFragments("weathered"), Is.Zero);
            Assert.That(session.TreasureMaps.Single().Seed, Is.EqualTo("GM-INTERACTION"));
        }

        [TestCase("0", 0)]
        [TestCase("1", 1)]
        [TestCase("00042", 42)]
        [TestCase("1000000000", CoreGameMasterTools.MaximumProfileGold)]
        public void ParseGold_AcceptsOnlyExactSupportedAmounts(string input, int expected)
        {
            Assert.That(CoreGameMasterTools.TryParseProfileGold(input, out decimal gold), Is.True);
            Assert.That(gold, Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("-1")]
        [TestCase("+1")]
        [TestCase(" 100")]
        [TestCase("100 ")]
        [TestCase("1\n")]
        [TestCase("1.00001")]
        [TestCase("1,000")]
        [TestCase("1e3")]
        [TestCase("１２３")]
        [TestCase("1000000001")]
        [TestCase("2147483647")]
        [TestCase("9999999999999999999999")]
        public void ParseGold_RejectsMalformedOrOutOfRangeAmounts(string input)
        {
            Assert.That(CoreGameMasterTools.TryParseProfileGold(input, out _), Is.False);
        }

        [TestCase(0)]
        [TestCase(42)]
        [TestCase(CoreGameMasterTools.MaximumProfileGold)]
        public void SetGold_InPortChangesOnlyProfileGold(int gold)
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(1234, "灰鸥港");
            var cargo = session.Cargo;
            int cargoGold = cargo.Gold;
            int water = cargo.Water;
            int ship = session.SelectedShipIndex;
            Assert.That(session.TryGameMasterSetProfileGold(gold), Is.True);
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(session.Cargo, Is.SameAs(cargo));
            Assert.That(cargo.Gold, Is.EqualTo(cargoGold));
            Assert.That(cargo.Water, Is.EqualTo(water));
            Assert.That(session.SelectedShipIndex, Is.EqualTo(ship));
            Assert.That(session.CurrentPort, Is.EqualTo("灰鸥港"));
        }

        [TestCase(CoreDemoPhase.Sailing)]
        [TestCase(CoreDemoPhase.Encounter)]
        [TestCase(CoreDemoPhase.Battle)]
        [TestCase(CoreDemoPhase.Settlement)]
        public void SetGold_RejectsEveryNonPortPhase(CoreDemoPhase phase)
        {
            var session = new CoreVoyageSession();
            decimal gold = session.ProfileGold;
            typeof(CoreVoyageSession).GetProperty("Phase", BindingFlags.Public | BindingFlags.Instance).SetValue(session, phase);
            Assert.That(session.TryGameMasterSetProfileGold(0), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
        }

        [TestCase(-1)]
        [TestCase(1000000001)]
        [TestCase(int.MaxValue)]
        public void SetGold_RejectsOutOfRangeWithoutClamping(int requested)
        {
            var session = new CoreVoyageSession();
            decimal before = session.ProfileGold;
            Assert.That(session.TryGameMasterSetProfileGold(requested), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(before));
        }

        private static string[] KnownPorts => CoreVoyageSession.DestinationPorts.ToArray();

        [TestCaseSource(nameof(KnownPorts))]
        public void MoveToPort_MovesOnlyCaptainAndFlagshipWithoutSpendingOrAwardingProgress(string destination)
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(1234.5678m, "断锚港");
            Assert.That(session.RegisterShipAtCurrentPort(1), Is.True);
            session.RestoreTreasureProgress(new CoreTreasureProgress {
                pirateExperience = 900, fragments = 12, identifiedWeatheredFragments = 3,
                maps = new[] { new CoreTreasureMapSave { id = "weathered:KEPT", seed = "KEPT", rankId = "weathered" } },
                selectedMapId = "weathered:KEPT", gems = 2, ancientMaterials = 3,
            });
            string treasure = UnityEngine.JsonUtility.ToJson(session.CaptureTreasureProgress());
            string collection = UnityEngine.JsonUtility.ToJson(session.CaptureCollectionProgress());
            string charts = UnityEngine.JsonUtility.ToJson(session.CaptureSeaChartProgress());
            var cargo = session.Cargo;
            int cash = cargo.Gold, water = cargo.Water, food = cargo.Food;
            int hull = session.Hull, morale = session.Morale;

            Assert.That(session.TryGameMasterMoveToPort(" " + destination + " "), Is.True);

            Assert.That(session.CurrentPort, Is.EqualTo(destination));
            Assert.That(session.ShipPort(session.SelectedShipIndex), Is.EqualTo(destination));
            Assert.That(session.ShipPort(1), Is.EqualTo("断锚港"), "Inactive vessels must stay at their existing berth.");
            Assert.That(session.CaptureShipPortProgress().ships.Length, Is.EqualTo(2));
            Assert.That(session.SelectedDestinationPort, Is.Not.EqualTo(destination));
            Assert.That(CoreVoyageSession.DestinationPorts, Does.Contain(session.SelectedDestinationPort));
            Assert.That(session.SelectedShipIndex, Is.Zero);
            Assert.That(session.ProfileGold, Is.EqualTo(1234.5678m));
            Assert.That(session.Cargo, Is.SameAs(cargo));
            Assert.That(cargo.Gold, Is.EqualTo(cash));
            Assert.That(cargo.Water, Is.EqualTo(water));
            Assert.That(cargo.Food, Is.EqualTo(food));
            Assert.That(session.Hull, Is.EqualTo(hull));
            Assert.That(session.Morale, Is.EqualTo(morale));
            Assert.That(UnityEngine.JsonUtility.ToJson(session.CaptureTreasureProgress()), Is.EqualTo(treasure));
            Assert.That(UnityEngine.JsonUtility.ToJson(session.CaptureCollectionProgress()), Is.EqualTo(collection));
            Assert.That(UnityEngine.JsonUtility.ToJson(session.CaptureSeaChartProgress()), Is.EqualTo(charts));
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(session.Map, Is.Null);
            Assert.That(session.Settlement, Is.Null);
        }

        [Test]
        public void MoveToPort_PreservesChartAtItsOtherEndpointAndCanDepart()
        {
            var session = new CoreVoyageSession();
            string seed = CoreVoyageSession.CreateChartSeed(0, "断锚港", "白贝港");
            Assert.That(session.TrySelectChartSeed(seed, out string reason), Is.True, reason);
            CoreSeaChartDefinition chart = session.SelectedChartDefinition;

            Assert.That(session.TryGameMasterMoveToPort("白贝港"), Is.True);

            Assert.That(session.SelectedChartDefinition, Is.SameAs(chart));
            Assert.That(session.SelectedSeed, Is.EqualTo(seed));
            Assert.That(session.SelectedDestinationPort, Is.EqualTo("断锚港"));
            Assert.That(session.CanStartSelectedChart(out reason), Is.True, reason);
            session.StartVoyage();
            Assert.That(session.Map.DepartureName, Is.EqualTo("白贝港"));
            Assert.That(session.Map.DestinationName, Is.EqualTo("断锚港"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MoveToPort_ClearsUnrelatedChartAndItsStructuredSeedBeforeDeparture(bool clearedSelection)
        {
            var session = new CoreVoyageSession();
            string seed = CoreVoyageSession.CreateChartSeed(0, "断锚港", "白贝港");
            Assert.That(session.TrySelectChartSeed(seed, out string reason), Is.True, reason);
            if (clearedSelection) session.ClearChartSeedSelection();

            Assert.That(session.TryGameMasterMoveToPort("铜钟港"), Is.True);

            Assert.That(session.SelectedChartDefinition, Is.Null);
            Assert.That(session.SelectedSeed, Is.Not.EqualTo(seed));
            Assert.That(session.CaptureSeaChartProgress().charts, Is.Empty);
            session.StartVoyage();
            Assert.That(session.Map.DepartureName, Is.EqualTo("铜钟港"));
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("不存在的港口")]
        public void MoveToPort_RejectsUnknownPortsWithoutChangingState(string destination)
        {
            var session = new CoreVoyageSession();
            string seed = CoreVoyageSession.CreateChartSeed(0, "断锚港", "白贝港");
            Assert.That(session.TrySelectChartSeed(seed, out _), Is.True);
            string shipPorts = UnityEngine.JsonUtility.ToJson(session.CaptureShipPortProgress());
            int logCount = session.LogEntries.Count;

            Assert.That(session.TryGameMasterMoveToPort(destination), Is.False);

            Assert.That(session.CurrentPort, Is.EqualTo("断锚港"));
            Assert.That(session.SelectedDestinationPort, Is.EqualTo("白贝港"));
            Assert.That(session.SelectedSeed, Is.EqualTo(seed));
            Assert.That(session.IsChartSeedSelected, Is.True);
            Assert.That(UnityEngine.JsonUtility.ToJson(session.CaptureShipPortProgress()), Is.EqualTo(shipPorts));
            Assert.That(session.LogEntries.Count, Is.EqualTo(logCount));
        }

        [TestCase(0)]
        [TestCase(900)]
        [TestCase(5400)]
        [TestCase(6400)]
        [TestCase(int.MaxValue)]
        public void RaisePirateLevelToTen_ChangesOnlyExperienceAndPreservesExcess(int experience)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress {
                pirateExperience = experience, fragments = 12,
                identifiedWeatheredFragments = 3, identifiedGildedFragments = 5, identifiedMoonlitFragments = 2,
                identificationSeed = "GM-LEVEL-KEEPS-IDENTIFICATION", identificationSequence = 17, moonlitPityCount = 2,
                selectedMapId = "weathered:HELD", maps = new[] {
                    new CoreTreasureMapSave { id = "weathered:HELD", seed = "HELD", rankId = "weathered" } },
                completedSeeds = new[] { "COMPLETED" }, issuedSeeds = new[] { "HELD", "COMPLETED" },
                gems = 2, ancientMaterials = 3, relics = 4, blackPearls = 5, ancientCompasses = 6, lanterns = 7, curses = 8,
            });
            CoreTreasureProgress expected = session.CaptureTreasureProgress();
            expected.pirateExperience = Math.Max(experience, CoreVoyageSession.PirateExperienceForLevel(10));
            decimal gold = session.ProfileGold;
            var cargo = session.Cargo;
            string collection = UnityEngine.JsonUtility.ToJson(session.CaptureCollectionProgress());

            Assert.That(session.TryGameMasterRaisePirateLevelToTen(), Is.True);

            Assert.That(session.PirateLevel, Is.EqualTo(10));
            Assert.That(UnityEngine.JsonUtility.ToJson(session.CaptureTreasureProgress()),
                Is.EqualTo(UnityEngine.JsonUtility.ToJson(expected)));
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(session.Cargo, Is.SameAs(cargo));
            Assert.That(session.CurrentPort, Is.EqualTo("断锚港"));
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(UnityEngine.JsonUtility.ToJson(session.CaptureCollectionProgress()), Is.EqualTo(collection));
        }

        [Test]
        public void RaisePirateLevelToTen_UsesNormalRouteAndBloodSchoolUnlocksAndSurvivesRestore()
        {
            var session = new CoreVoyageSession();
            var blood = CoreBoardingCardCatalog.All.First(card => card.SchoolId == "blood");
            Assert.That(session.IsBattleCardUnlocked(blood), Is.False);
            Assert.That(session.TryGameMasterRaisePirateLevelToTen(), Is.True);

            var restored = new CoreVoyageSession();
            restored.RestoreTreasureProgress(session.CaptureTreasureProgress());

            Assert.That(restored.PirateLevel, Is.EqualTo(10));
            Assert.That(restored.IsBattleCardUnlocked(blood), Is.True);
            for (int route = 0; route < CoreVoyageSession.Routes.Count; route++)
                Assert.That(restored.CanSelectRoute(route, out string reason), Is.True, reason);
        }

        [TestCase(CoreDemoPhase.Sailing)]
        [TestCase(CoreDemoPhase.Encounter)]
        [TestCase(CoreDemoPhase.Battle)]
        [TestCase(CoreDemoPhase.Settlement)]
        public void MoveAndRaiseLevel_RejectNonPortPhasesWithoutPartialMutation(CoreDemoPhase phase)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 900, fragments = 12 });
            string treasure = UnityEngine.JsonUtility.ToJson(session.CaptureTreasureProgress());
            string shipPorts = UnityEngine.JsonUtility.ToJson(session.CaptureShipPortProgress());
            string destination = session.SelectedDestinationPort, seed = session.SelectedSeed;
            int logCount = session.LogEntries.Count;
            typeof(CoreVoyageSession).GetProperty("Phase", BindingFlags.Public | BindingFlags.Instance).SetValue(session, phase);

            Assert.That(session.TryGameMasterMoveToPort("铜钟港"), Is.False);
            Assert.That(session.TryGameMasterRaisePirateLevelToTen(), Is.False);

            Assert.That(session.CurrentPort, Is.EqualTo("断锚港"));
            Assert.That(session.SelectedDestinationPort, Is.EqualTo(destination));
            Assert.That(session.SelectedSeed, Is.EqualTo(seed));
            Assert.That(UnityEngine.JsonUtility.ToJson(session.CaptureTreasureProgress()), Is.EqualTo(treasure));
            Assert.That(UnityEngine.JsonUtility.ToJson(session.CaptureShipPortProgress()), Is.EqualTo(shipPorts));
            Assert.That(session.LogEntries.Count, Is.EqualTo(logCount));
        }

        [Test]
        public void RevisionZero_PreservesLegacyStockAndPurchaseIds()
        {
            var art = new ComponentArtCatalog();
            var date = new DateTime(2026, 9, 13);
            var legacy = PortComponentShopCatalog.BuildStock(art, "灰鸥港", date);
            var explicitZero = PortComponentShopCatalog.BuildStock(art, "灰鸥港", date, 0, "ignored-until-refresh");
            CollectionAssert.AreEqual(legacy.Select(Describe), explicitZero.Select(Describe));
            Assert.That(explicitZero.All(item => !item.Id.Contains("gm-") && item.RefreshRevision == 0), Is.True);
        }

        [Test]
        public void Refresh_ChangesBatchIdsWithoutChangingClockOrOtherPorts()
        {
            var art = new ComponentArtCatalog();
            var date = new DateTime(2026, 9, 13);
            var original = PortComponentShopCatalog.BuildStock(art, "灰鸥港", date);
            var otherPort = PortComponentShopCatalog.BuildStock(art, "白贝港", date);
            var refreshed = PortComponentShopCatalog.BuildStock(art, "灰鸥港", date, 1, "save-a");
            var restored = PortComponentShopCatalog.BuildStock(art, "灰鸥港", date, 1, "save-a");
            CollectionAssert.AreEqual(refreshed.Select(Describe), restored.Select(Describe), "Reopening/restoring must not reroll a batch.");
            Assert.That(refreshed.Count, Is.EqualTo(PortComponentShopCatalog.StockCount(0)));
            Assert.That(original.Select(item => item.Id).Intersect(refreshed.Select(item => item.Id)), Is.Empty);
            Assert.That(refreshed.All(item => item.RefreshKey == PortComponentShopCatalog.MonthKey(date) && item.RefreshRevision == 1), Is.True);
            CollectionAssert.AreEqual(otherPort.Select(Describe), PortComponentShopCatalog.BuildStock(art, "白贝港", date).Select(Describe));
        }

        [Test]
        public void Refresh_InvalidatesOldCallbacksAndNewGamesHaveIndependentReceipts()
        {
            var art = new ComponentArtCatalog();
            var date = new DateTime(2026, 9, 13);
            var original = PortComponentShopCatalog.BuildStock(art, "灰鸥港", date, 0)[0];
            var first = PortComponentShopCatalog.BuildStock(art, "灰鸥港", date, 1, "old-save")[0];
            var nextSave = PortComponentShopCatalog.BuildStock(art, "灰鸥港", date, 1, "new-save")[0];
            Assert.That(PortComponentShopCatalog.IsCurrentOffer(original, "灰鸥港", date, 1, "old-save"), Is.False);
            Assert.That(PortComponentShopCatalog.IsCurrentOffer(first, "灰鸥港", date, 1, "old-save"), Is.True);
            Assert.That(PortComponentShopCatalog.IsCurrentOffer(first, "灰鸥港", date, 2, "old-save"), Is.False);
            Assert.That(PortComponentShopCatalog.IsCurrentOffer(first, "灰鸥港", date, 1, "new-save"), Is.False);
            Assert.That(PortComponentShopCatalog.IsCurrentOffer(first, "白贝港", date, 1, "old-save"), Is.False);
            Assert.That(PortComponentShopCatalog.IsCurrentOffer(first, "灰鸥港", date.AddMonths(1), 1, "old-save"), Is.False);
            Assert.That(first.Id, Is.Not.EqualTo(nextSave.Id));
            Assert.That(CoreGameplayDemoController.ComponentShopPurchaseKey(first.Id),
                Is.Not.EqualTo(CoreGameplayDemoController.ComponentShopPurchaseKey(nextSave.Id)));
        }

        [Test]
        public void NegativeRevision_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PortComponentShopCatalog.BuildStock(new ComponentArtCatalog(), "灰鸥港", new DateTime(2026, 9, 1), -1));
        }

        private static string Describe(PortComponentShopStockItem item)
        {
            return item.Id + ":" + item.Entry.Id + ":" + item.Price;
        }
    }
}
