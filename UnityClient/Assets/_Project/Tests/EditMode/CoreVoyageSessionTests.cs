using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreVoyageSessionTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void CollectionIncomeCreditsTheAccountWithoutChangingVoyageCargo(bool sailing)
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(127.5m, "断锚港");
            if (sailing) session.StartVoyage();
            CoreDemoPhase phase = session.Phase;
            int cargoGold = session.Cargo.Gold;
            float cargoVolume = session.Cargo.UsedVolume;

            session.GrantCollectionIncome(300);
            session.GrantCollectionIncome(0);
            session.GrantCollectionIncome(-10);

            Assert.That(session.ProfileGold, Is.EqualTo(427.5m));
            Assert.That(session.Phase, Is.EqualTo(phase));
            if (sailing) Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.Cargo.Gold, Is.EqualTo(cargoGold));
            Assert.That(session.Cargo.UsedVolume, Is.EqualTo(cargoVolume));
        }

        [Test]
        public void CollectionIncomeAtTheAccountLimitDoesNotOverflowIntoCargo()
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(CoreEconomyBalance.MaximumProfileGold - 5m, "断锚港");
            int cargoGold = session.Cargo.Gold;

            session.GrantCollectionIncome(300);

            Assert.That(session.ProfileGold, Is.EqualTo(CoreEconomyBalance.MaximumProfileGold));
            Assert.That(session.Cargo.Gold, Is.EqualTo(cargoGold));
        }

        [Test]
        public void CollectionIncomeDoesNotReduceALegacyAccountAboveTheCurrentLimit()
        {
            var session = new CoreVoyageSession();
            decimal legacyBalance = CoreEconomyBalance.MaximumProfileGold + 15m;
            session.RestoreProfile(legacyBalance, "断锚港");

            session.GrantCollectionIncome(300);

            Assert.That(session.ProfileGold, Is.EqualTo(legacyBalance));
        }

        [Test]
        public void NewCampaignProvisionsLeaveSpaceForCargoWithoutChangingItsBudget()
        {
            var session = new CoreVoyageSession();

            Assert.That(session.Cargo.Water, Is.EqualTo(120));
            Assert.That(session.Cargo.Food, Is.EqualTo(120));
            Assert.That(session.Cargo.Cannonballs, Is.EqualTo(16));
            Assert.That(session.Cargo.Powder, Is.EqualTo(20));
            Assert.That(session.Cargo.UsedVolume, Is.EqualTo(53.60f).Within(0.001f));
            Assert.That(session.CargoCapacityValue - session.Cargo.UsedVolume,
                Is.EqualTo(26.40f).Within(0.001f));
            Assert.That(session.ProfileGold, Is.EqualTo(500));
            Assert.That(session.Cargo.Gold, Is.EqualTo(60));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void DeparturesAndTutorialSetupDoNotReissueStartingProvisions(int tutorialLeg)
        {
            var session = new CoreVoyageSession();
            session.Cargo.Water = 35;
            session.Cargo.Food = 41;
            session.Cargo.Cannonballs = 3;
            session.Cargo.Powder = 5;
            session.RestoreProfile(432, "白贝港");

            Assert.That(session.SelectShip(0), Is.True);
            Assert.That(session.ConfigureGuidedTutorialVoyage(tutorialLeg), Is.True);
            session.StartVoyage();

            Assert.That(session.Cargo.Water, Is.EqualTo(35));
            Assert.That(session.Cargo.Food, Is.EqualTo(41));
            Assert.That(session.Cargo.Cannonballs, Is.EqualTo(3));
            Assert.That(session.Cargo.Powder, Is.EqualTo(5));
            Assert.That(session.ProfileGold, Is.EqualTo(432));
        }

        [TestCase(286, 286, 10, 0)]
        [TestCase(42, 31, 0, 2)]
        [TestCase(300, 275, 21, 0)]
        public void RestoringExistingVoyageCargoDoesNotApplyNewCampaignProvisions(
            int water, int food, int cannonballs, int tutorialLeg)
        {
            var previous = new CoreVoyageSession();
            previous.Cargo.Water = water;
            previous.Cargo.Food = food;
            previous.Cargo.Cannonballs = cannonballs;
            previous.RestoreProfile(432, "白贝港");
            previous.ConfigureGuidedTutorialVoyage(tutorialLeg);
            previous.StartVoyage();
            CoreVoyageCheckpoint checkpoint = previous.CaptureVoyageCheckpoint();
            var restored = new CoreVoyageSession();

            Assert.That(restored.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
            Assert.That(restored.Cargo.Water, Is.EqualTo(water));
            Assert.That(restored.Cargo.Food, Is.EqualTo(food));
            Assert.That(restored.Cargo.Cannonballs, Is.EqualTo(cannonballs));
            Assert.That(restored.Cargo.Powder, Is.EqualTo(previous.Cargo.Powder));
            Assert.That(restored.Cargo.UsedVolume, Is.EqualTo(previous.Cargo.UsedVolume).Within(0.001f));
            Assert.That(restored.ProfileGold, Is.EqualTo(previous.ProfileGold));
            Assert.That(restored.Cargo.Gold, Is.EqualTo(previous.Cargo.Gold));
        }

        [Test]
        public void SameRouteSeedAndPort_GeneratesIdenticalMap()
        {
            CoreVoyageSession first = CreateVoyage(0, "MAP-ALPHA");
            CoreVoyageSession second = CreateVoyage(0, "MAP-ALPHA");

            Assert.That(first.Map.Hash, Is.EqualTo(second.Map.Hash));
            Assert.That(first.Map.DestinationName, Is.EqualTo(second.Map.DestinationName));
            Assert.That(first.Map.Nodes.Count, Is.GreaterThanOrEqualTo(10));
            Assert.That(first.Map.Nodes.Count(node => node.IsBoss), Is.EqualTo(1));
            Assert.That(first.Map.Nodes.Select(node => node.Encounter.Id).Distinct().Count(), Is.EqualTo(first.Map.Nodes.Count), "Seeded map events should be drawn without replacement.");
            Assert.That(first.Map.ExploredCells.Length, Is.EqualTo(first.Map.Columns * first.Map.Rows));
            Assert.That(first.Map.TerrainCells, Is.EqualTo(second.Map.TerrainCells));
            Assert.That(first.Map.TerrainFeatureIds, Is.EqualTo(second.Map.TerrainFeatureIds));
            Assert.That(first.Map.AnchorNodeIds, Is.EqualTo(second.Map.AnchorNodeIds));
            Assert.That(first.Map.EventReservedCells, Is.EqualTo(second.Map.EventReservedCells));
            Assert.That(first.Map.TerrainVariants, Is.EqualTo(second.Map.TerrainVariants));
            Assert.That(first.Map.CurrentVectors, Is.EqualTo(second.Map.CurrentVectors));
            Assert.That(first.Map.CurrentStrengths, Is.EqualTo(second.Map.CurrentStrengths));
            Assert.That(first.Map.GlobalWindDirection, Is.EqualTo(second.Map.GlobalWindDirection));
            Assert.That(first.Map.GlobalWindVector.X, Is.EqualTo(second.Map.GlobalWindVector.X).Within(0.0001f));
            Assert.That(first.Map.GlobalWindVector.Y, Is.EqualTo(second.Map.GlobalWindVector.Y).Within(0.0001f));
            Assert.That(first.Map.GlobalWindStrength, Is.EqualTo(second.Map.GlobalWindStrength).Within(0.0001f));
            Assert.That(first.Map.EventFootprints.Count, Is.EqualTo(second.Map.EventFootprints.Count));
            for (int index = 0; index < first.Map.Nodes.Count; index += 1)
            {
                Assert.That(first.Map.Nodes[index].Position.X, Is.EqualTo(second.Map.Nodes[index].Position.X));
                Assert.That(first.Map.Nodes[index].Position.Y, Is.EqualTo(second.Map.Nodes[index].Position.Y));
                if (!first.Map.Nodes[index].IsBoss)
                    Assert.That(first.Map.Nodes[index].Encounter.Id, Is.EqualTo(second.Map.Nodes[index].Encounter.Id));
                else
                {
                    // The current guardian is selected independently on departure;
                    // its seeded geography and reserved footprint must still match.
                    Assert.That(CoreRouteBossCatalog.Find(first.Map.Nodes[index].Encounter.Id).RouteId, Is.EqualTo(first.Map.Route.Id));
                    Assert.That(CoreRouteBossCatalog.Find(second.Map.Nodes[index].Encounter.Id).RouteId, Is.EqualTo(second.Map.Route.Id));
                }
                Assert.That(first.Map.Nodes[index].Footprint.CellIndices,
                    Is.EqualTo(second.Map.Nodes[index].Footprint.CellIndices));
                Assert.That(first.Map.Nodes[index].Footprint.ReservedCellIndices,
                    Is.EqualTo(second.Map.Nodes[index].Footprint.ReservedCellIndices));
            }
        }

        [Test]
        public void PortSafetyDiscs_RemainOpenPassableAndOutsideEveryEventReservation()
        {
            const int portReserveRadiusCells = 3;
            for (int route = 0; route < CoreVoyageSession.Routes.Count; route += 1)
            {
                CoreVoyageMap map = CreateVoyage(route, "PORT-SAFETY-" + route).Map;
                VoyageGridCoordinate[] ports =
                {
                    map.PointToCell(map.Departure),
                    map.PointToCell(map.Destination),
                };

                foreach (VoyageGridCoordinate port in ports)
                {
                    for (int y = -portReserveRadiusCells; y <= portReserveRadiusCells; y += 1)
                    {
                        for (int x = -portReserveRadiusCells; x <= portReserveRadiusCells; x += 1)
                        {
                            if (x * x + y * y > portReserveRadiusCells * portReserveRadiusCells + 1)
                            {
                                continue;
                            }

                            int column = port.Column + x;
                            int row = port.Row + y;
                            if (!map.IsInBounds(column, row)) continue;
                            int cellIndex = map.CellIndex(column, row);
                            Assert.That(map.TerrainCells[cellIndex], Is.EqualTo(SeaTerrainType.OpenWater),
                                "Port approaches must be cleared to open water.");
                            Assert.That(map.IsPassable(column, row), Is.True);
                            Assert.That(map.EventReservedCells[cellIndex], Is.False,
                                "Fixed event footprints may not reserve a port approach cell.");
                            Assert.That(map.AnchorNodeIds[cellIndex], Is.EqualTo(-1));
                        }
                    }
                }
            }
        }

        [Test]
        public void EventFirstGeneration_ReservesDisjointFootprintsAndFillsEveryChartCell()
        {
            for (int route = 0; route < CoreVoyageSession.Routes.Count; route += 1)
            {
                CoreVoyageSession voyage = CreateVoyage(route, "EVENT-FIRST-" + route);
                CoreVoyageMap map = voyage.Map;
                HashSet<int> reservedCells = new HashSet<int>();

                Assert.That(map.EventFootprints.Count, Is.EqualTo(map.Nodes.Count),
                    "Every fixed event, including the boss, must own a terrain footprint before free sea is filled.");
                foreach (CoreMapNode node in map.Nodes)
                {
                    CoreEventFootprint footprint = map.FootprintForNode(node.Id);
                    Assert.That(footprint, Is.Not.Null);
                    Assert.That(node.Footprint, Is.SameAs(footprint));
                    Assert.That(footprint.CellIndices.Count, Is.GreaterThanOrEqualTo(5));
                    Assert.That(footprint.ReservedCellIndices.Count, Is.GreaterThan(footprint.CellIndices.Count));

                    VoyageGridCoordinate anchor = map.PointToCell(node.Position);
                    Assert.That(anchor, Is.EqualTo(footprint.Anchor));
                    Assert.That(map.EventAnchorCells[map.CellIndex(anchor.Column, anchor.Row)], Is.True);

                    foreach (int cellIndex in footprint.CellIndices)
                    {
                        Assert.That(cellIndex, Is.InRange(0, map.CellCount - 1));
                        Assert.That(map.AnchorNodeIds[cellIndex], Is.EqualTo(node.Id));
                        Assert.That(map.TerrainFeatureIds[cellIndex], Is.EqualTo(node.Id));
                    }

                    foreach (int cellIndex in footprint.ReservedCellIndices)
                    {
                        Assert.That(reservedCells.Add(cellIndex), Is.True,
                            "Event footprints and their one-cell safety bands must never overlap.");
                        Assert.That(map.EventReservedCells[cellIndex], Is.True);
                    }
                }

                Assert.That(map.TerrainFeatureIds.All(featureId => featureId >= 0), Is.True,
                    "All cells must belong to a deterministic visual feature after ocean filling.");
                Assert.That(map.TerrainVariants.All(variant => variant <= 15), Is.True);
                Assert.That(map.TerrainCells.Count(terrain => terrain == SeaTerrainType.OpenWater),
                    Is.GreaterThan(map.CellCount / 3), "The remaining chart should be filled by broad blue-water regions.");
                Assert.That(map.TerrainCells.Count(terrain => terrain != SeaTerrainType.OpenWater),
                    Is.GreaterThan(80), "Event geography and generated islands should produce a detailed chart.");

                VoyageGridCoordinate departure = map.PointToCell(map.Departure);
                VoyageGridCoordinate destination = map.PointToCell(map.Destination);
                Assert.That(map.TerrainAt(departure.Column, departure.Row), Is.EqualTo(SeaTerrainType.OpenWater));
                Assert.That(map.TerrainAt(destination.Column, destination.Row), Is.EqualTo(SeaTerrainType.OpenWater));
            }
        }

        [Test]
        public void BossPlacement_IsNearDestinationAfterTenDayVoyageAndInsideSafeBorder()
        {
            HashSet<string> locations = new HashSet<string>();

            for (int route = 0; route < CoreVoyageSession.Routes.Count; route += 1)
            {
                for (int seedIndex = 0; seedIndex < 24; seedIndex += 1)
                {
                    CoreVoyageSession voyage = CreateVoyage(route, "BOSS-POSITION-" + seedIndex);
                    CoreMapNode boss = voyage.Map.Nodes.Single(node => node.IsBoss);
                    float departureDistance = Distance(voyage.Map.Departure, boss.Position);
                    float destinationDistance = Distance(voyage.Map.Destination, boss.Position);

                    Assert.That(voyage.Map.BossExclusionRadius,
                        Is.EqualTo(CoreVoyageSession.BossTenDayRadiusMapUnits(voyage.Map.Route)).Within(0.001f));
                    Assert.That(departureDistance, Is.GreaterThanOrEqualTo(voyage.Map.BossExclusionRadius));
                    Assert.That(destinationDistance, Is.InRange(CoreVoyageSession.BossPortClearanceMapUnits, CoreVoyageSession.BossPortApproachRadiusMapUnits));
                    Assert.That(boss.Position.X, Is.InRange(
                        CoreVoyageSession.BossBorderMarginMapUnits,
                        voyage.Map.WorldWidth - CoreVoyageSession.BossBorderMarginMapUnits));
                    Assert.That(boss.Position.Y, Is.InRange(
                        CoreVoyageSession.BossBorderMarginMapUnits,
                        voyage.Map.WorldHeight - CoreVoyageSession.BossBorderMarginMapUnits));

                    locations.Add(route + ":" + boss.Position.X.ToString("F2") + ":" + boss.Position.Y.ToString("F2"));
                }
            }

            Assert.That(locations.Count, Is.GreaterThan(40), "Boss positions should vary around the destination approach instead of using one fixed port marker.");
        }

        [Test]
        public void DenseChartTerrain_CreatesEventLinkedFeaturesAndIrregularVisibility()
        {
            CoreVoyageSession voyage = CreateVoyage(0, "TERRAIN-VISION-01");

            Assert.That(voyage.Map.Columns, Is.EqualTo(48));
            Assert.That(voyage.Map.Rows, Is.EqualTo(36));
            Assert.That(voyage.Map.TerrainCells.Count(value => value != SeaTerrainType.OpenWater), Is.GreaterThan(80));

            int eventLinkedTerrain = 0;
            float farthestVisible = 0f;
            float nearestHidden = float.MaxValue;
            float visionRadius = 180f / voyage.Map.Route.NauticalMilesPerMapUnit;
            for (int row = 0; row < voyage.Map.Rows; row += 1)
            {
                for (int column = 0; column < voyage.Map.Columns; column += 1)
                {
                    int cellIndex = row * voyage.Map.Columns + column;
                    VoyagePoint center = voyage.Map.CellCenter(column, row);
                    float dx = center.X - voyage.Position.X;
                    float dy = center.Y - voyage.Position.Y;
                    float observerDistance = (float)System.Math.Sqrt(dx * dx + dy * dy);
                    if (voyage.Map.ExploredCells[cellIndex])
                    {
                        farthestVisible = System.Math.Max(farthestVisible, observerDistance);
                    }
                    else if (observerDistance <= visionRadius)
                    {
                        nearestHidden = System.Math.Min(nearestHidden, observerDistance);
                    }

                    if (voyage.Map.TerrainCells[cellIndex] == SeaTerrainType.OpenWater)
                    {
                        continue;
                    }
                    if (voyage.Map.Nodes.Any(node =>
                    {
                        float nodeDx = center.X - node.Position.X;
                        float nodeDy = center.Y - node.Position.Y;
                        return nodeDx * nodeDx + nodeDy * nodeDy <= 36f;
                    }))
                    {
                        eventLinkedTerrain += 1;
                    }
                }
            }

            Assert.That(eventLinkedTerrain, Is.GreaterThan(40));
            Assert.That(nearestHidden, Is.LessThan(farthestVisible), "Terrain and edge noise should break the visibility boundary into an irregular silhouette.");
        }

        [Test]
        public void RouteSelection_ChangesRulesAndMapHash()
        {
            CoreVoyageSession reef = CreateVoyage(0, "SAME-SEED");
            CoreVoyageSession golden = CreateVoyage(1, "SAME-SEED");

            Assert.That(reef.Map.Hash, Is.Not.EqualTo(golden.Map.Hash));
            Assert.That(reef.Map.Route.NauticalMilesPerMapUnit, Is.EqualTo(27f));
            Assert.That(golden.Map.Route.NauticalMilesPerMapUnit, Is.EqualTo(15f));
            Assert.That(reef.Map.Islands.Count, Is.GreaterThan(golden.Map.Islands.Count));
            Assert.That(reef.Map.Islands.Count, Is.GreaterThanOrEqualTo(10));
            Assert.That(golden.Map.Islands.Count, Is.GreaterThanOrEqualTo(7));
        }

        [Test]
        public void EventConstraints_AnchorBeachesAndNavigableStraits()
        {
            CoreVoyageSession voyage = CreateVoyage(0, "EVENT-TERRAIN-01");
            CoreMapNode beach = voyage.Map.Nodes.First(node => node.TerrainConstraint == EventTerrainConstraint.Beach);
            CoreMapNode strait = voyage.Map.Nodes.First(node => node.TerrainConstraint == EventTerrainConstraint.Strait);

            Assert.That(TerrainAt(voyage.Map, beach.Position), Is.EqualTo(SeaTerrainType.Beach));
            Assert.That(TerrainAt(voyage.Map, strait.Position), Is.EqualTo(SeaTerrainType.Strait));
            Assert.That(IsPassable(voyage.Map, beach.Position), Is.True);
            Assert.That(IsPassable(voyage.Map, strait.Position), Is.True, "A strait is a water corridor and may be crossed directly.");
            Assert.That(voyage.Map.TerrainCells.Count(value => value == SeaTerrainType.Island), Is.GreaterThan(50));

            foreach (CoreMapNode node in voyage.Map.Nodes)
            {
                SeaTerrainType requiredTerrain;
                switch (node.TerrainConstraint)
                {
                    case EventTerrainConstraint.Beach: requiredTerrain = SeaTerrainType.Beach; break;
                    case EventTerrainConstraint.Strait: requiredTerrain = SeaTerrainType.Strait; break;
                    case EventTerrainConstraint.Reef: requiredTerrain = SeaTerrainType.Reef; break;
                    case EventTerrainConstraint.Fog: requiredTerrain = SeaTerrainType.FogBank; break;
                    case EventTerrainConstraint.Storm: requiredTerrain = SeaTerrainType.StormFront; break;
                    default: requiredTerrain = SeaTerrainType.OpenWater; break;
                }

                Assert.That(node.Footprint.CellIndices.Any(cell => voyage.Map.TerrainCells[cell] == requiredTerrain), Is.True,
                    "Every fixed event footprint must contain geography matching its terrain constraint.");
                Assert.That(IsPassable(voyage.Map, node.Position), Is.True,
                    "The event anchor itself must remain reachable even when its footprint contains islands.");
            }

            Assert.That(beach.Footprint.CellIndices.Count(cell => voyage.Map.TerrainCells[cell] == SeaTerrainType.Island), Is.GreaterThanOrEqualTo(4),
                "A beach event should be attached to a multi-cell island rather than a single marker cell.");
            Assert.That(strait.Footprint.CellIndices.Count(cell => voyage.Map.TerrainCells[cell] == SeaTerrainType.Island), Is.GreaterThanOrEqualTo(4),
                "A strait event should include solid banks while preserving its passable water corridor.");
        }

        [Test]
        public void Navigation_SnapsIslandTargetsAndNeverCrossesBlockedTerrain()
        {
            CoreVoyageSession voyage = CreateVoyage(0, "ISLAND-ROUTING-01");
            int islandIndex = System.Array.FindIndex(voyage.Map.TerrainCells, terrain => terrain == SeaTerrainType.Island);
            Assert.That(islandIndex, Is.GreaterThanOrEqualTo(0));
            VoyagePoint blockedTarget = voyage.Map.CellCenter(islandIndex % voyage.Map.Columns, islandIndex / voyage.Map.Columns);

            voyage.SetCourseTo(blockedTarget);
            SailingForecast forecast = voyage.GetForecast();

            Assert.That(forecast.Blocked, Is.False);
            Assert.That(IsPassable(voyage.Map, forecast.Target), Is.True);
            Assert.That(forecast.PathPoints.Count, Is.GreaterThanOrEqualTo(2));
            for (int segment = 1; segment < forecast.PathPoints.Count; segment += 1)
            {
                AssertSegmentPassable(voyage.Map, forecast.PathPoints[segment - 1], forecast.PathPoints[segment]);
            }
        }

        [Test]
        public void WindIsGlobalWhileCurrentsRemainSeedBoundToTerrain()
        {
            CoreVoyageSession first = CreateVoyage(2, "GLOBAL-WIND-01");
            CoreVoyageSession second = CreateVoyage(2, "GLOBAL-WIND-01");
            SeaCondition near = first.GetSeaConditionAt(first.Map.Departure);
            SeaCondition far = first.GetSeaConditionAt(first.Map.Destination);

            Assert.That(near.WindDirection, Is.EqualTo(far.WindDirection));
            Assert.That(near.WindStrength, Is.EqualTo(far.WindStrength).Within(0.0001f));
            Assert.That(near.WindVector.X, Is.EqualTo(far.WindVector.X).Within(0.0001f));
            Assert.That(first.Map.CurrentVectors, Is.EqualTo(second.Map.CurrentVectors));
            Assert.That(first.Map.CurrentStrengths, Is.EqualTo(second.Map.CurrentStrengths));
        }

        [Test]
        public void ForecastAndSail_UseAccumulatedCrewRationsAndRevealChart()
        {
            CoreVoyageSession voyage = CreateVoyage(0, "RATIONS-01");
            int waterBefore = voyage.Cargo.Water;
            float coverageBefore = voyage.ExplorationCoverage();
            voyage.SetDistance(180);

            SailingForecast forecast = voyage.GetForecast();
            Assert.That(forecast, Is.Not.Null);
            Assert.That(forecast.Hours, Is.GreaterThan(0d));
            Assert.That(forecast.WaterCost, Is.GreaterThan(0));
            Assert.That(voyage.Sail(), Is.True);

            Assert.That(voyage.TotalDistanceNm, Is.GreaterThan(0f));
            Assert.That(voyage.Cargo.Water, Is.LessThan(waterBefore));
            Assert.That(voyage.ExplorationCoverage(), Is.GreaterThanOrEqualTo(coverageBefore));
        }

        [Test]
        public void SafeReturn_CreatesProtectedSettlementAndRestoresPortLoop()
        {
            CoreVoyageSession voyage = CreateVoyage(2, "RETURN-HOME");

            Assert.That(voyage.SafeReturn(), Is.True);
            Assert.That(voyage.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(voyage.Settlement.Outcome, Is.EqualTo(VoyageOutcome.SafeReturn));
            Assert.That(voyage.Settlement.ProtectionRatio, Is.EqualTo(0.85f));
            Assert.That(voyage.Settlement.ChartSaved, Is.True);

            voyage.ReturnToPortAfterSettlement();
            Assert.That(voyage.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(voyage.Map, Is.Null);
        }

        [Test]
        public void CourseToPoint_ChoosesNearestEightWayHeadingAndValidDistance()
        {
            CoreVoyageSession voyage = CreateVoyage(0, "COURSE-01");
            voyage.SetCourseTo(new VoyagePoint(voyage.Position.X + 8f, voyage.Position.Y + 8f));

            Assert.That(voyage.Heading, Is.EqualTo(SailingDirection.NorthEast));
            Assert.That(voyage.HasDirectCourseTarget, Is.True);
            Assert.That(voyage.PlannedDistanceNm, Is.InRange(CoreVoyageSession.MinimumDistanceNm, voyage.MaximumDistanceNm));
            Assert.That(voyage.PlannedDistanceNm, Is.EqualTo(voyage.DefaultDistanceNm));
        }

        [Test]
        public void DirectCourse_PreservesPreciseClickedPointUntilManualHeadingIsChosen()
        {
            CoreVoyageSession voyage = CreateVoyage(0, "DIRECT-COURSE");
            VoyagePoint target = new VoyagePoint(voyage.Position.X + 4f, voyage.Position.Y + 1f);

            voyage.SetCourseTo(target);
            SailingForecast forecast = voyage.GetForecast();

            Assert.That(voyage.HasDirectCourseTarget, Is.True);
            Assert.That(forecast.Target.X, Is.EqualTo(target.X).Within(0.001f));
            Assert.That(forecast.Target.Y, Is.EqualTo(target.Y).Within(0.001f));
            Assert.That(voyage.Heading, Is.EqualTo(SailingDirection.East));

            voyage.SetHeading(SailingDirection.North);
            Assert.That(voyage.HasDirectCourseTarget, Is.False);
        }

        [Test]
        public void WindAndCurrent_ExposeDirectionsAndApplyTailOrHeadSpeedModifiers()
        {
            CoreVoyageSession voyage = CreateVoyage(1, "WIND-CURRENT-01");
            SeaCondition condition = voyage.GetSeaConditionAt(voyage.Position);

            Assert.That(condition, Is.Not.Null);
            Assert.That(condition.CurrentStrength, Is.GreaterThan(0f));
            Assert.That(condition.WindStrength, Is.GreaterThan(0f));

            voyage.SetHeading(condition.CurrentDirection);
            SailingForecast withCurrent = voyage.GetForecast();
            SailingDirection againstCurrentDirection = (SailingDirection)(((int)condition.CurrentDirection + 4) % 8);
            voyage.SetHeading(againstCurrentDirection);
            SailingForecast againstCurrent = voyage.GetForecast();

            Assert.That(withCurrent.CurrentRelation, Is.EqualTo("顺流"));
            Assert.That(withCurrent.CurrentSpeedModifier, Is.GreaterThan(0f));
            Assert.That(againstCurrent.CurrentRelation, Is.EqualTo("逆流"));
            Assert.That(againstCurrent.CurrentSpeedModifier, Is.LessThan(0f));

            voyage.SetHeading(condition.WindDirection);
            SailingForecast withWind = voyage.GetForecast();
            SailingDirection againstWindDirection = (SailingDirection)(((int)condition.WindDirection + 4) % 8);
            voyage.SetHeading(againstWindDirection);
            SailingForecast againstWind = voyage.GetForecast();

            Assert.That(withWind.WindRelation, Is.EqualTo("顺风"));
            Assert.That(withWind.WindSpeedModifier, Is.GreaterThan(0f));
            Assert.That(againstWind.WindRelation, Is.EqualTo("逆风"));
            Assert.That(againstWind.WindSpeedModifier, Is.LessThan(0f));
        }

        [Test]
        public void PortProfile_CanRestoreProgressAndReceiveTutorialReward()
        {
            CoreVoyageSession session = new CoreVoyageSession();
            session.RestoreProfile(640, "灰鸥港");
            session.GrantProfileGold(45, "教学奖励");

            Assert.That(session.CurrentPort, Is.EqualTo("灰鸥港"));
            Assert.That(session.ProfileGold, Is.EqualTo(685));
            Assert.That(session.LogEntries.Last(), Does.Contain("教学奖励"));
        }

        [Test]
        public void PortProfile_SpendsGoldOnlyWhenBalanceAndPhaseAllowIt()
        {
            CoreVoyageSession session = new CoreVoyageSession();
            session.RestoreProfile(640, "断锚港");

            Assert.That(session.TrySpendProfileGold(250, "部件采购"), Is.True);
            Assert.That(session.ProfileGold, Is.EqualTo(390));
            Assert.That(session.LogEntries.Last(), Does.Contain("金币 -250"));
            Assert.That(session.TrySpendProfileGold(500, "余额不足"), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(390));

            session.StartVoyage();
            Assert.That(session.TrySpendProfileGold(10, "航行中消费"), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(390));
        }

        [Test]
        public void VoyageStartingMoraleUsesPreviousExplorationCoverage()
        {
            CoreVoyageSession first = new CoreVoyageSession();
            first.StartVoyage();
            Assert.That(first.Morale, Is.EqualTo(CoreVoyageSession.DefaultStartingMorale));

            CoreVoyageSession high = new CoreVoyageSession();
            high.RestoreLastVoyageExplorationCoverage(80.01f);
            high.StartVoyage();
            Assert.That(high.Morale, Is.EqualTo(CoreVoyageSession.HighExplorationStartingMorale));

            CoreVoyageSession low = new CoreVoyageSession();
            low.RestoreLastVoyageExplorationCoverage(29.99f);
            low.StartVoyage();
            Assert.That(low.Morale, Is.EqualTo(CoreVoyageSession.LowExplorationStartingMorale));

            CoreVoyageSession boundary = new CoreVoyageSession();
            boundary.RestoreLastVoyageExplorationCoverage(80f);
            boundary.StartVoyage();
            Assert.That(boundary.Morale, Is.EqualTo(CoreVoyageSession.DefaultStartingMorale));
        }

        [Test]
        public void MoraleClampsToTwentyAndDeterminesBoardingCriticalRate()
        {
            CoreVoyageSession session = new CoreVoyageSession();
            session.StartVoyage();
            SetEncounter(session, new CoreEncounterChoice("极值士气", "", 0,
                new VoyageEffect { Morale = int.MaxValue }));

            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.Morale, Is.EqualTo(CoreVoyageSession.MaxMorale));
            Assert.That(session.BoardingCriticalChancePercent, Is.EqualTo(60));
        }

        private static void SetEncounter(CoreVoyageSession session, CoreEncounterChoice choice)
        {
            SetProperty(session, "Phase", CoreDemoPhase.Encounter);
            SetProperty(session, "ActiveEncounter",
                new CoreEncounter("morale-test", "士气测试", "事件", "", true, false, 1, 0, choice));
        }

        private static void SetProperty(CoreVoyageSession session, string name, object value) =>
            typeof(CoreVoyageSession).GetProperty(name).SetValue(session, value);

        [Test]
        public void PortSupplies_QuotePurchaseAndCarryThePurchasedLoadoutIntoVoyage()
        {
            CoreVoyageSession session = new CoreVoyageSession();
            VoyageSupplyOrder order = new VoyageSupplyOrder
            {
                Water = 10,
                Food = 10,
                Medicine = 1,
                Wood = 1,
                Powder = 1,
                Cannonballs = 1,
            };

            VoyageSupplyQuote quote = session.QuoteSupplyPurchase(order);
            // Broken Anchor: retain the exact quantity-based prices; medicine
            // is an import here, while timber is a local specialty.
            Assert.That(quote.Cost, Is.EqualTo(.32m + .96m + 9.92m + 3.25m + 5m + 8m));
            Assert.That(quote.AddedVolume, Is.EqualTo(4.14f).Within(0.001f));
            Assert.That(quote.CanPurchase, Is.True);

            int waterBefore = session.Cargo.Water;
            Assert.That(session.TryPurchaseSupplies(order), Is.True);
            Assert.That(session.ProfileGold, Is.EqualTo(500 - quote.Cost));
            Assert.That(session.Cargo.Water, Is.EqualTo(waterBefore + 10));

            session.StartVoyage();
            Assert.That(session.Cargo.Water, Is.EqualTo(waterBefore + 10), "Starting a voyage must not replace the player's purchased loadout.");
        }

        [Test]
        public void PortSupplies_RejectOrdersThatExceedCargoOrAvailableGold()
        {
            CoreVoyageSession fullCargo = new CoreVoyageSession();
            int excessCannonballs = (int)(fullCargo.CargoCapacityValue - fullCargo.Cargo.UsedVolume) + 1;
            VoyageSupplyOrder oversized = new VoyageSupplyOrder { Cannonballs = excessCannonballs };
            Assert.That(fullCargo.QuoteSupplyPurchase(oversized).FitsCargo, Is.False);
            Assert.That(fullCargo.TryPurchaseSupplies(oversized), Is.False);
            Assert.That(fullCargo.Cargo.Cannonballs, Is.EqualTo(16));

            CoreVoyageSession poorCaptain = new CoreVoyageSession();
            poorCaptain.RestoreProfile(1, "断锚港");
            VoyageSupplyOrder unaffordable = new VoyageSupplyOrder { Wood = 1 };
            Assert.That(poorCaptain.QuoteSupplyPurchase(unaffordable).CanAfford, Is.False);
            Assert.That(poorCaptain.TryPurchaseSupplies(unaffordable), Is.False);
            Assert.That(poorCaptain.ProfileGold, Is.EqualTo(1));
            Assert.That(poorCaptain.Cargo.Wood, Is.EqualTo(8));
        }

        [Test]
        public void PortProgression_SelectedShipChangesRealVoyageStats()
        {
            CoreVoyageSession session = new CoreVoyageSession();
            CoreShipDefinition starter = session.SelectedShip;
            CoreShipDefinition patrol = CoreVoyageSession.Ships[2];
            Assert.That(session.SelectShip(2), Is.True);
            Assert.That(session.SelectedShip.Id, Is.EqualTo("patrol"));
            Assert.That(session.MaxHull, Is.EqualTo(patrol.MaxHull).And.GreaterThan(starter.MaxHull));
            Assert.That(session.Armor, Is.EqualTo(patrol.Armor).And.GreaterThan(starter.Armor));
            Assert.That(session.Firepower, Is.EqualTo(patrol.Firepower).And.GreaterThan(starter.Firepower));
            Assert.That(session.SpeedKnots, Is.EqualTo(patrol.SpeedKnots));
            Assert.That(session.CargoCapacityValue, Is.EqualTo(patrol.CargoCapacity));
            Assert.That(session.CrewCountValue, Is.EqualTo(patrol.CrewCount));

            session.StartVoyage();
            Assert.That(session.Hull, Is.EqualTo(patrol.MaxHull));
            Assert.That(session.EffectiveFirepower, Is.EqualTo(patrol.Firepower));
            Assert.That(session.EffectiveArmor, Is.EqualTo(patrol.Armor));
            Assert.That(session.SelectedShipIndex, Is.EqualTo(2));
        }

        [Test]
        public void PortProgression_CustomDeckValidatesCopiesAndPersistsIntoSession()
        {
            CoreVoyageSession session = new CoreVoyageSession();
            List<int> validDeck = new List<int> { 0, 0, 1, 2, 5, 6, 7, 8, 9, 10 };
            Assert.That(session.SetBattleDeck(NavalBattleStage.Naval, validDeck), Is.True);
            Assert.That(session.GetBattleDeck(NavalBattleStage.Naval), Is.EqualTo(validDeck));

            Assert.That(session.SetBattleDeck(NavalBattleStage.Naval, new List<int> { 0, 0, 0, 1, 2, 5, 6, 7, 8, 9 }), Is.False, "A deck cannot contain more than two owned copies of one card.");
            Assert.That(session.SetBattleDeck(NavalBattleStage.Naval, validDeck.Take(9).ToList()), Is.False, "A deck needs at least ten cards.");
            Assert.That(session.GetBattleDeck(NavalBattleStage.Naval), Is.EqualTo(validDeck), "Rejected edits must leave the valid deck unchanged.");
        }

        [Test]
        public void PortProgression_RecruitedCapabilitiesControlCareerOptionsAndRationEfficiency()
        {
            CoreVoyageSession withoutCook = new CoreVoyageSession();
            withoutCook.ConfigureCrewCapabilities(false, false, false, false, false, false);
            withoutCook.StartVoyage();
            int plainRations = withoutCook.GetForecast().WaterCost;

            CoreVoyageSession withCook = new CoreVoyageSession();
            withCook.ConfigureCrewCapabilities(true, true, true, true, true, true);
            withCook.StartVoyage();
            int cookedRations = withCook.GetForecast().WaterCost;

            Assert.That(withoutCook.HasNavigator, Is.False);
            Assert.That(withCook.HasNavigator, Is.True);
            Assert.That(withCook.HasCarpenter, Is.True);
            Assert.That(withCook.HasDoctor, Is.True);
            Assert.That(withCook.HasGunner, Is.True);
            Assert.That(withCook.HasCook, Is.True);
            Assert.That(withCook.HasDiver, Is.True);
            Assert.That(cookedRations, Is.LessThanOrEqualTo(plainRations));
        }

        [Test]
        public void DepartureCrewConfiguration_ClampsFormationToShipCapacity()
        {
            var session = new CoreVoyageSession();
            session.ConfigureDepartureCrew(999, 999);

            Assert.That(session.SelectedSpecialCrewCount, Is.EqualTo(session.CrewCountValue));
            Assert.That(session.SelectedOrdinaryCrew, Is.Zero);
            Assert.That(session.EffectiveCrewCount, Is.EqualTo(session.CrewCountValue));

            session.ConfigureDepartureCrew(7, 2);
            Assert.That(session.SelectedSpecialCrewCount, Is.EqualTo(2));
            Assert.That(session.SelectedOrdinaryCrew, Is.EqualTo(7));
            Assert.That(session.EffectiveCrewCount, Is.EqualTo(9));
        }
        private static CoreVoyageSession CreateVoyage(int route, string seed)
        {
            CoreVoyageSession session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1500 });
            session.SelectRoute(route);
            Assert.That(session.SelectedRouteIndex, Is.EqualTo(route), "Terrain fixtures must unlock their requested route.");
            session.SetSeed(seed);
            session.StartVoyage();
            return session;
        }

        private static SeaTerrainType TerrainAt(CoreVoyageMap map, VoyagePoint point)
        {
            int column = System.Math.Max(0, System.Math.Min(map.Columns - 1, (int)(point.X / map.WorldWidth * map.Columns)));
            int row = System.Math.Max(0, System.Math.Min(map.Rows - 1, (int)(point.Y / map.WorldHeight * map.Rows)));
            return map.TerrainAt(column, row);
        }

        private static bool IsPassable(CoreVoyageMap map, VoyagePoint point)
        {
            return TerrainAt(map, point) != SeaTerrainType.Island;
        }

        private static void AssertSegmentPassable(CoreVoyageMap map, VoyagePoint start, VoyagePoint end)
        {
            float distance = Distance(start, end);
            int samples = System.Math.Max(1, (int)System.Math.Ceiling(distance / 0.35f));
            for (int sample = 0; sample <= samples; sample += 1)
            {
                float t = sample / (float)samples;
                VoyagePoint point = new VoyagePoint(start.X + (end.X - start.X) * t, start.Y + (end.Y - start.Y) * t);
                Assert.That(IsPassable(map, point), Is.True, "Plotted path crossed an island cell.");
            }
        }

        private static float Distance(VoyagePoint first, VoyagePoint second)
        {
            float x = first.X - second.X;
            float y = first.Y - second.Y;
            return (float)System.Math.Sqrt(x * x + y * y);
        }
    }
}
