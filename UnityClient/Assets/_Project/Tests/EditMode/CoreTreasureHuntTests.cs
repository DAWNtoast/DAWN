using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Random;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreTreasureHuntTests
    {
        [TestCase("weathered", 2, 90, 2, 9, 420, 14f)]
        [TestCase("gilded", 3, 130, 2, 10, 570, 27f)]
        [TestCase("moonlit", 5, 175, 3, 11, 735, 30f)]
        public void Catalog_HasConfiguredRanksAndAllEighteenTemplates(string rankId, int level, int power, int clues, int sides, int gold, float volume)
        {
            CoreTreasureMapRank rank = CoreTreasureHuntCatalog.FindRank(rankId);
            Assert.That(rank.UnlockLevel, Is.EqualTo(level));
            Assert.That(rank.RecommendedPower, Is.EqualTo(power));
            Assert.That(rank.RequiredClues, Is.EqualTo(clues));
            Assert.That(rank.SidePointCount, Is.EqualTo(sides));
            var events = CoreTreasureHuntCatalog.CreateEncounters(rank);
            Assert.That(events.Count, Is.EqualTo(18));
            Assert.That(events.Select(x => x.Id).Distinct().Count(), Is.EqualTo(18));
            Assert.That(events.Count(x => x.Category == "线索"), Is.EqualTo(6));
            foreach (CoreEncounter encounter in events)
                Assert.That(CoreEncounterDiscoveryRules.TryClassify(encounter.Id, out _), Is.True, encounter.Id);
            CoreEncounter core = events.Single(x => x.Id == "treasure_core");
            Assert.That(core.Mandatory && core.Hidden, Is.True);
            Assert.That(core.Choices[0].RequiredTreasureClues, Is.EqualTo(clues));
            Assert.That(core.Choices[0].Effect.Gold, Is.EqualTo(gold));
            var reward = core.Choices[0].Effect;
            Assert.That(new VoyageCargo { Gem = reward.Gem, AncientMaterial = reward.AncientMaterial, Relic = reward.Relic }.UsedVolume, Is.EqualTo(volume));
            Assert.That(core.Choices[1].Effect.Powder, Is.EqualTo(-3));
            Assert.That(core.Choices[1].Effect.Hull, Is.EqualTo(-12));
            Assert.That(core.Choices[2].RequiredCrew, Is.EqualTo("diver"));
            Assert.That(core.Choices[2].Effect.BlackPearl, Is.EqualTo(1));
            Assert.That(core.Choices[3].Effect.PostponeTreasureCore, Is.True);
        }

        [Test]
        public void Crafting_EnforcesLevelFragmentsLimitAndUniqueSeedsWithoutSpendingOnFailure()
        {
            var session = Profile(99, 4);
            Assert.That(session.TryCraftTreasureMap("ONE", out string reason), Is.False);
            Assert.That(reason, Does.Contain("等级"));
            Assert.That(session.TreasureFragments, Is.EqualTo(4));
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 100, fragments = 3 });
            Assert.That(session.TryCraftTreasureMap("ONE", out reason), Is.False);
            Assert.That(reason, Does.Contain("还缺 1"));
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 100, fragments = 28 });
            Assert.That(session.TryCraftTreasureMap("ONE", out reason), Is.True, reason);
            Assert.That(session.SelectedTreasureMap.RankId, Is.EqualTo("weathered"));
            Assert.That(session.TryCraftTreasureMap(" ONE ", out _), Is.False);
            Assert.That(session.TreasureFragments, Is.EqualTo(24));
            for (int i = 1; i < 6; i++) Assert.That(session.TryCraftTreasureMap("HELD-" + i, out reason), Is.True, reason);
            Assert.That(session.TryCraftTreasureMap("SEVENTH", out reason), Is.False);
            Assert.That(reason, Does.Contain("6"));
            Assert.That(session.TreasureMaps.Count, Is.EqualTo(6));
            Assert.That(session.TreasureFragments, Is.EqualTo(4));
        }

        [Test]
        public void MoonlitPity_IncreasesAfterEligibleNonMoonlitCraftsAndResetsOnMoonlit()
        {
            var session = Profile(1400, 4);
            Assert.That(session.MoonlitPityCount, Is.Zero);
            Assert.That(session.MoonlitCraftChance, Is.EqualTo(.15d).Within(.000001d));

            for (int attempt = 0; attempt < CoreTreasureHuntCatalog.MoonlitPityCap; attempt++)
            {
                ResetCraftInventory(session);
                string seed = FindSeedWithRankRollBelow(.01d, "PITY-" + attempt + "-");
                Assert.That(session.TryCraftTreasureMap(seed, out string reason), Is.True, reason);
                Assert.That(session.SelectedTreasureMap.RankId, Is.Not.EqualTo("moonlit"));
                Assert.That(session.MoonlitPityCount, Is.EqualTo(attempt + 1));
                Assert.That(session.MoonlitCraftChance, Is.EqualTo(System.Math.Min(1d, .15d * (attempt + 2))).Within(.000001d));
            }

            CoreTreasureProgress saved = session.CaptureTreasureProgress();
            var restored = new CoreVoyageSession();
            restored.RestoreTreasureProgress(saved);
            Assert.That(restored.MoonlitPityCount, Is.EqualTo(CoreTreasureHuntCatalog.MoonlitPityCap));
            Assert.That(restored.MoonlitCraftChance, Is.EqualTo(1d).Within(.000001d));

            ResetCraftInventory(restored);
            Assert.That(restored.TryCraftTreasureMap("PITY-GUARANTEED", out string guaranteedReason), Is.True, guaranteedReason);
            Assert.That(restored.SelectedTreasureMap.RankId, Is.EqualTo("moonlit"));
            Assert.That(restored.MoonlitPityCount, Is.Zero);
            Assert.That(restored.MoonlitCraftChance, Is.EqualTo(.15d).Within(.000001d));
        }

        [Test]
        public void MoonlitPity_DoesNotAdvanceBeforeMoonlitUnlock()
        {
            var session = Profile(260, 4);
            Assert.That(session.TryCraftTreasureMap("PRE-MOONLIT", out string reason), Is.True, reason);
            Assert.That(session.MoonlitPityCount, Is.Zero);
            Assert.That(session.MoonlitCraftChance, Is.Zero);
        }

        [TestCase(100, "weathered")]
        [TestCase(260, "weathered,gilded")]
        [TestCase(900, "weathered,gilded,moonlit")]
        public void CraftRank_IsSeedBoundAndNeverExceedsUnlockedLevel(int xp, string allowed)
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < 80; i++)
            {
                var left = Profile(xp, 4); var right = Profile(xp, 4);
                Assert.That(left.TryCraftTreasureMap("RANK-" + i, out _), Is.True);
                Assert.That(right.TryCraftTreasureMap("RANK-" + i, out _), Is.True);
                Assert.That(left.SelectedTreasureMap.RankId, Is.EqualTo(right.SelectedTreasureMap.RankId));
                seen.Add(left.SelectedTreasureMap.RankId);
            }
            Assert.That(seen, Is.EquivalentTo(allowed.Split(',')));
        }

        [Test]
        public void Persistence_RoundTripsOwnershipSelectionResourcesAndPermanentCompletion()
        {
            var session = Profile(100, 8);
            Assert.That(session.TryCraftTreasureMap("SAVE-ONE", out _), Is.True);
            Assert.That(session.TryCraftTreasureMap("SAVE-TWO", out _), Is.True);
            session.Cargo.Gem = 3; session.Cargo.AncientMaterial = 7; session.Cargo.BlackPearl = 1;
            CoreTreasureProgress saved = session.CaptureTreasureProgress();
            saved.completedSeeds = new[] { "OLD-COMPLETE", "OLD-COMPLETE" };
            var restored = new CoreVoyageSession(); restored.RestoreTreasureProgress(saved);
            Assert.That(restored.TreasureMaps.Select(x => x.Seed), Is.EqualTo(session.TreasureMaps.Select(x => x.Seed)));
            Assert.That(restored.SelectedTreasureMap.Id, Is.EqualTo(session.SelectedTreasureMap.Id));
            Assert.That(restored.Cargo.Gem, Is.EqualTo(3));
            Assert.That(restored.Cargo.AncientMaterial, Is.EqualTo(7));
            Assert.That(restored.Cargo.BlackPearl, Is.EqualTo(1));
            Assert.That(restored.CompletedTreasureSeeds, Is.EqualTo(new[] { "OLD-COMPLETE" }));
            saved.fragments = 4; restored.RestoreTreasureProgress(saved);
            Assert.That(restored.TryCraftTreasureMap("OLD-COMPLETE", out _), Is.False);
            Assert.That(restored.TreasureFragments, Is.EqualTo(4));
        }

        [TestCase("water")]
        [TestCase("food")]
        [TestCase("overload")]
        [TestCase("negative")]
        public void FailedDeparture_DoesNotConsumeMapOrTransferMoney(string failure)
        {
            var session = Ready("weathered", "DEPART-" + failure);
            if (failure == "water") session.Cargo.Water = 0;
            if (failure == "food") session.Cargo.Food = 0;
            if (failure == "overload") session.Cargo.Wood = 100;
            if (failure == "negative") session.Cargo.Gem = -1;
            decimal gold = session.ProfileGold;
            int travelGold = session.Cargo.Gold;
            string id = session.SelectedTreasureMap.Id;
            Assert.That(session.TryStartTreasureVoyage(out string reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(session.SelectedTreasureMap.Id, Is.EqualTo(id));
            Assert.That(session.TreasureMaps.Count, Is.EqualTo(1));
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(session.Cargo.Gold, Is.EqualTo(travelGold));
            Assert.Throws<InvalidOperationException>(() => session.StartVoyage());
            Assert.That(session.TreasureMaps.Count, Is.EqualTo(1));
        }

        [TestCase("weathered")]
        [TestCase("gilded")]
        [TestCase("moonlit")]
        public void HiddenMaps_AreSeedBoundReachableSpacedAndGuaranteeClues(string rankId)
        {
            CoreTreasureMapRank rank = CoreTreasureHuntCatalog.FindRank(rankId);
            for (int seed = 0; seed < 8; seed++)
            {
                var a = Ready(rankId, "GEOGRAPHY-" + seed); var b = Ready(rankId, "GEOGRAPHY-" + seed);
                b.RestoreProfile(500, "灰鸥港");
                Assert.That(a.TryStartTreasureVoyage(out string reason), Is.True, reason);
                Assert.That(b.TryStartTreasureVoyage(out reason), Is.True, reason);
                Assert.That(a.TreasureMaps, Is.Empty);
                Assert.That(a.IsTreasureVoyage, Is.True);
                Assert.That(a.CanDrawSeaChart, Is.False);
                Assert.That(a.Map.Hash, Is.EqualTo(b.Map.Hash), "Treasure geography depends on seed/rank, not departure port.");
                var legendaryIds = CoreLegendaryWarships.CreateTreasureEncounters(rankId).Select(encounter => encounter.Id).ToArray();
                Assert.That(a.Map.Nodes.Count, Is.EqualTo(rank.SidePointCount + 1 + legendaryIds.Length));
                Assert.That(a.Map.Nodes.Count(x => x.Encounter.Category == "线索"), Is.GreaterThanOrEqualTo(rank.RequiredClues + 1));
                Assert.That(a.Map.Nodes.Any(x => x.IsBoss), Is.False, "Optional legendary encounters never block the treasure core.");
                CollectionAssert.AreEquivalent(legendaryIds, a.Map.Nodes.Where(x => x.Encounter.BossKind != CoreBossKind.None)
                    .Select(x => x.Encounter.Id));
                CoreMapNode core = a.Map.Nodes.Single(x => x.Encounter.Id == "treasure_core");
                Assert.That(core.IsChartVisible && core.IntelRevealed, Is.True);
                Assert.That(a.Map.Destination, Is.EqualTo(core.Position));
                HashSet<int> reachable = Reachable(a.Map);
                foreach (CoreMapNode node in a.Map.Nodes)
                {
                    Assert.That(node.Position, Is.EqualTo(b.Map.Nodes.Single(x => x.Id == node.Id).Position));
                    Assert.That(node.Encounter.Id, Is.EqualTo(b.Map.Nodes.Single(x => x.Id == node.Id).Encounter.Id));
                    Assert.That(reachable.Contains(a.Map.CellIndex(node.Position)), Is.True, node.Encounter.Id);
                    if (node.Encounter.Hidden && node != core) Assert.That(node.IsChartVisible, Is.False, "Hidden side locations cannot leak through fog.");
                    foreach (CoreMapNode other in a.Map.Nodes.Where(x => x.Id != node.Id))
                        Assert.That(Distance(node.Position, other.Position), Is.GreaterThanOrEqualTo(rank.MinimumEventSpacing - .001));
                }
            }
        }

        [Test]
        public void RealOrdinaryEncounterChoices_EarnFragmentsAndExperienceOnlyOnceAtReturn()
        {
            var session = new CoreVoyageSession();
            EmptyHold(session); session.Cargo.Water = 240; session.Cargo.Food = 240;
            for (int trip = 0; trip < 2; trip++)
            {
                session.StartVoyage();
                foreach (string id in new[] { "event_reef_sharp_current", "event_reef_hidden_cache" })
                {
                    CoreMapNode node = session.Map.Nodes.Single(x => x.Encounter.Id == id);
                    Enter(session, node);
                    Assert.That(session.ResolveChoice(0), Is.True);
                    Assert.That(node.Visited, Is.True);
                }
                Assert.That(session.VoyageTreasureFragments, Is.EqualTo(2));
                Assert.That(session.TreasureFragments, Is.EqualTo(trip * 2));
                Assert.That(session.SafeReturn(), Is.True);
                Assert.That(session.Settlement.BankedFragments, Is.EqualTo(2), "Fragments are intangible and not rounded down by cargo protection.");
                Assert.That(session.Settlement.BankedPirateExperience, Is.GreaterThan(0));
                session.ReturnToPortAfterSettlement();
                int xp = session.PirateExperience;
                session.ReturnToPortAfterSettlement();
                Assert.That(session.TreasureFragments, Is.EqualTo((trip + 1) * 2));
                Assert.That(session.PirateExperience, Is.EqualTo(xp));
            }
            Assert.That(session.TreasureFragments, Is.EqualTo(4));
        }

        [TestCase("weathered", 0)]
        [TestCase("gilded", 0)]
        [TestCase("moonlit", 0)]
        [TestCase("weathered", 1)]
        [TestCase("weathered", 2)]
        public void CoreOpening_RequiresCluesAndCostsThenSettlesOnceAtNinetyFivePercent(string rankId, int choice)
        {
            var session = Ready(rankId, "CORE-" + rankId + choice);
            Assert.That(session.TryStartTreasureVoyage(out _), Is.True);
            CoreMapNode core = session.Map.Nodes.Single(x => x.Encounter.Id == "treasure_core");
            Enter(session, core);
            Assert.That(session.IsChoiceAvailable(choice), Is.False);
            Assert.That(session.ChoiceUnavailableReason(choice), Does.Contain("线索"));
            Set(session, "TreasureClues", core.Encounter.Choices[choice].RequiredTreasureClues);
            int gold = session.Cargo.Gold, powder = session.Cargo.Powder, hull = session.Hull;
            Assert.That(session.ResolveChoice(choice), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.TreasureRecovered));
            Assert.That(session.Settlement.ProtectionRatio, Is.EqualTo(.95f));
            Assert.That(session.Settlement.ChartSaved, Is.False);
            Assert.That(session.TreasureAdventureCompleted && session.Settlement.TreasureCompleted, Is.True);
            Assert.That(session.Settlement.BankedGold, Is.EqualTo((int)Math.Floor(core.Encounter.Choices[choice].Effect.Gold * .95f)));
            Assert.That(session.Settlement.BankedGem, Is.EqualTo(core.Encounter.Choices[choice].Effect.Gem), "A 5% cargo loss must not erase the sole gem.");
            Assert.That(session.Settlement.BankedRelic, Is.EqualTo(core.Encounter.Choices[choice].Effect.Relic));
            Assert.That(session.Settlement.BankedBlackPearl, Is.EqualTo(core.Encounter.Choices[choice].Effect.BlackPearl));
            Assert.That(session.Settlement.BankedAncientMaterial, Is.EqualTo((int)Math.Floor(core.Encounter.Choices[choice].Effect.AncientMaterial * .95f)), "Common material rounding is unchanged.");
            if (choice == 1) { Assert.That(session.Cargo.Powder, Is.EqualTo(powder - 3)); Assert.That(session.Hull, Is.EqualTo(hull - 12)); }
            Assert.That(session.ResolveChoice(choice), Is.False);
            Assert.That(session.BossDefeated, Is.False);
            string departure = session.CurrentPort;
            session.ReturnToPortAfterSettlement();
            decimal banked = session.ProfileGold;
            session.ReturnToPortAfterSettlement();
            Assert.That(session.ProfileGold, Is.EqualTo(banked));
            Assert.That(session.CurrentPort, Is.EqualTo(departure));
            Assert.That(session.CompletedTreasureSeeds, Is.EqualTo(new[] { "CORE-" + rankId + choice }));
            Assert.That(session.TreasureMaps, Is.Empty);
            var restored = new CoreVoyageSession(); restored.RestoreTreasureProgress(session.CaptureTreasureProgress());
            Assert.That(restored.CompletedTreasureSeeds, Is.EqualTo(session.CompletedTreasureSeeds));
        }

        [Test]
        public void CoreOpening_RejectsCargoOverflowLethalBreachAndAbsentDiverWithoutMutations()
        {
            var session = Ready("weathered", "CORE-GATES"); session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            Assert.That(session.TryStartTreasureVoyage(out _), Is.True);
            CoreMapNode core = session.Map.Nodes.Single(x => x.Encounter.Id == "treasure_core");
            Enter(session, core); Set(session, "TreasureClues", 2);
            session.Cargo.Wood = 75;
            Assert.That(session.IsChoiceAvailable(0), Is.False);
            Assert.That(session.ChoiceUnavailableReason(0), Does.Contain("货舱"));
            int gold = session.Cargo.Gold;
            Assert.That(session.ResolveChoice(0), Is.False);
            Assert.That(session.Cargo.Gold, Is.EqualTo(gold));
            session.Cargo.Wood = 0;
            Set(session, "Hull", 12);
            Assert.That(session.IsChoiceAvailable(1), Is.False);
            Assert.That(session.ChoiceUnavailableReason(1), Does.Contain("船体"));
            Assert.That(session.IsChoiceAvailable(2), Is.False);
            Assert.That(session.ChoiceUnavailableReason(2), Is.EqualTo("缺少所需船员：潜水员。"));
            Assert.That(session.IsChoiceAvailable(3), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WithdrawalOrFailure_ConsumesChartWithoutCompletionOrCoreRewards(bool failure)
        {
            var session = Ready("weathered", "EXIT-" + failure);
            Assert.That(session.TryStartTreasureVoyage(out _), Is.True);
            if (failure) session.Cargo.Water = 0;
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.Outcome, Is.EqualTo(failure ? VoyageOutcome.Failure : VoyageOutcome.SafeReturn));
            Assert.That(session.Settlement.TreasureCompleted, Is.False);
            Assert.That(session.Settlement.BankedGold, Is.Zero);
            session.ReturnToPortAfterSettlement();
            Assert.That(session.TreasureMaps, Is.Empty);
            Assert.That(session.CompletedTreasureSeeds, Is.Empty);
            var saved = session.CaptureTreasureProgress(); saved.fragments = 4;
            session.RestoreTreasureProgress(saved);
            Assert.That(session.TryCraftTreasureMap("EXIT-" + failure, out _), Is.False, "The consumed seed must not be reissued after an abandoned voyage.");
        }

        [Test]
        public void ReturningFromTreasure_RestoresNormalChartingAndAllowsAnotherTreasureVoyage()
        {
            var session = Ready("weathered", "FIRST");
            Assert.That(session.TryStartTreasureVoyage(out _), Is.True);
            Assert.That(session.CanDrawSeaChart, Is.False);
            Assert.That(session.SafeReturn(), Is.True);
            session.ReturnToPortAfterSettlement();
            Assert.That(session.Map, Is.Null);
            Assert.That(session.IsTreasureVoyage, Is.False);
            Assert.That(session.CanDrawSeaChart, Is.True);
            session.SelectRoute(0); session.StartVoyage();
            Assert.That(session.IsTreasureVoyage, Is.False);
            Assert.That(session.ActiveTreasureMap, Is.Null);
            Assert.That(session.CanDrawSeaChart, Is.True);
            Assert.That(session.SafeReturn(), Is.True); session.ReturnToPortAfterSettlement();
            var progress = session.CaptureTreasureProgress(); progress.fragments = 4;
            session.RestoreTreasureProgress(progress);
            Assert.That(session.TryCraftTreasureMap("SECOND", out string reason), Is.True, reason);
            Assert.That(session.TryStartTreasureVoyage(out reason), Is.True, reason);
            Assert.That(session.IsTreasureVoyage, Is.True);
            Assert.That(session.ActiveTreasureMap.Seed, Is.EqualTo("SECOND"));
        }

        [Test]
        public void EarlyCoreVisit_CanLeaveCollectRealCluesAndRevisitWithoutLosingTheAdventure()
        {
            var session = Ready("weathered", "EARLY-CORE");
            Assert.That(session.TryStartTreasureVoyage(out _), Is.True);
            CoreMapNode core = session.Map.Nodes.Single(x => x.Encounter.Id == "treasure_core");
            Enter(session, core);
            Assert.That(session.IsChoiceAvailable(0), Is.False);
            Assert.That(session.ResolveChoice(3), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(core.Visited, Is.False);
            Assert.That(core.Skipped, Is.True);
            Assert.That(session.Settlement, Is.Null);
            foreach (CoreMapNode clue in session.Map.Nodes.Where(x => x.Encounter.Category == "线索").Take(2))
            {
                Enter(session, clue);
                int option = Enumerable.Range(0, clue.Encounter.Choices.Count)
                    .First(index => clue.Encounter.Choices[index].Effect.TreasureClues > 0 && session.IsChoiceAvailable(index));
                Assert.That(session.ResolveChoice(option), Is.True);
            }
            Assert.That(session.TreasureClues, Is.EqualTo(2));
            Set(session, "Position", core.Position);
            session.SetCourseTo(core.Position);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            Assert.That(session.ActiveEncounter.Id, Is.EqualTo("treasure_core"));
            Assert.That(core.Skipped, Is.False);
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(core.Visited, Is.True);
            Assert.That(session.Settlement.TreasureCompleted, Is.True);
        }

        [TestCase(0, false, .24)]
        [TestCase(1, false, .27)]
        [TestCase(8, false, .36)]
        [TestCase(8, true, .74)]
        public void TreasureLanterns_AddCappedDetectionAlongsideNavigator(int lanterns, bool navigator, double chance)
        {
            var session = Ready("weathered", "LANTERN");
            session.ConfigureCrewCapabilities(navigator, false, false, false, false, false);
            session.Cargo.Lantern = lanterns;
            Assert.That(session.TryStartTreasureVoyage(out _), Is.True);
            Assert.That(session.AmbushDetectionChance, Is.EqualTo(chance).Within(.00001));
            Assert.That(session.SafeReturn(), Is.True); session.ReturnToPortAfterSettlement();
            session.SelectRoute(0); session.StartVoyage();
            Assert.That(session.AmbushDetectionChance,
                Is.EqualTo(CoreVoyageSession.Routes[0].AmbushDetectionChance + (navigator ? .38 : 0)).Within(.00001),
                "Treasure lantern bonuses must not carry over to an ordinary route.");
        }

        [Test]
        public void CrowdedFragmentRoute_RetriesPackingWithoutDroppingEventsOrSafetyBands()
        {
            foreach (string seed in new[] { "COMPLETE-EVENT-CATALOG-AUDIT", "PACK-0", "PACK-1", "PACK-2", "PACK-3", "PACK-4" })
            {
                var first = new CoreVoyageSession(); first.SetSeed(seed); first.StartVoyage();
                var same = new CoreVoyageSession(); same.SetSeed(seed); same.StartVoyage();
                Assert.That(first.Map.Nodes.Count, Is.EqualTo(15));
                Assert.That(first.Map.Nodes.Count(x => x.Encounter.Choices.Any(c => c.Effect.MapFragments > 0)), Is.EqualTo(2));
                Assert.That(first.Map.EventFootprints.Count, Is.EqualTo(first.Map.Nodes.Count), "Failed attempts must not leave duplicate footprints.");
                var reserved = new HashSet<int>();
                foreach (CoreMapNode node in first.Map.Nodes)
                {
                    Assert.That(node.Position, Is.EqualTo(same.Map.Nodes.Single(x => x.Id == node.Id).Position));
                    foreach (int cell in node.Footprint.ReservedCellIndices) Assert.That(reserved.Add(cell), Is.True, "Safety bands must never overlap.");
                    foreach (CoreMapNode other in first.Map.Nodes.Where(x => x.Id != node.Id))
                        Assert.That(Distance(node.Position, other.Position), Is.GreaterThanOrEqualTo(first.Map.Route.MinimumEventSpacing - .001));
                }
            }
        }

        [TestCase(0f, false, false, 0)]
        [TestCase(19.99f, false, false, 0)]
        [TestCase(20f, false, false, 20)]
        [TestCase(0f, true, false, 25)]
        [TestCase(0f, true, true, 0)]
        public void VoyageExperience_UsesPhysicalEligibilityAndExcludesSkippedEvents(float distanceNm, bool visited, bool skipped, int expected)
        {
            var session = new CoreVoyageSession(); session.StartVoyage();
            Set(session, "TotalDistanceNm", distanceNm);
            session.Map.Nodes[0].Visited = visited; session.Map.Nodes[0].Skipped = skipped;
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.BankedPirateExperience, Is.EqualTo(expected));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void EqualChartDistance_AwardsEqualExperienceAcrossOrdinaryRouteScales(int routeIndex)
        {
            var session = new CoreVoyageSession(); session.SelectRoute(routeIndex); session.StartVoyage();
            float distanceNm = 100f * session.Map.Route.NauticalMilesPerMapUnit;
            Set(session, "TotalDistanceNm", distanceNm); // Exactly 100 chart units at this route's physical scale.
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.BankedPirateExperience, Is.EqualTo(165));
            Assert.That(session.Settlement.DistanceNm, Is.EqualTo(distanceNm), "The voyage report retains actual nautical miles.");
        }

        [TestCase("weathered", 2400f)]
        [TestCase("gilded", 2700f)]
        [TestCase("moonlit", 3000f)]
        public void EqualChartDistance_AwardsEqualExperienceAcrossTreasureRankScales(string rankId, float distanceNm)
        {
            var session = Ready(rankId, "XP-SCALE"); Assert.That(session.TryStartTreasureVoyage(out _), Is.True);
            Set(session, "TotalDistanceNm", distanceNm);
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.BankedPirateExperience, Is.EqualTo(165));
        }

        [Test]
        public void ObservedLongFirstVoyage_DoesNotJumpToMaximumLevelOrAwardExperienceTwice()
        {
            var session = Profile(120, 0); session.StartVoyage();
            // Regression snapshot from the real first free voyage: 6,503 nautical
            // miles and five completed events. This fixture tests settlement math.
            Set(session, "TotalDistanceNm", 6503f);
            foreach (CoreMapNode node in session.Map.Nodes.Take(5)) node.Visited = true;
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.BankedPirateExperience, Is.EqualTo(426), "The first route now covers 27 nautical miles per chart unit.");
            Assert.That(session.PirateExperience, Is.EqualTo(120), "The pending settlement has not been confirmed.");
            session.ReturnToPortAfterSettlement();
            Assert.That(session.PirateExperience, Is.EqualTo(546));
            Assert.That(session.PirateLevel, Is.EqualTo(4));
            session.ReturnToPortAfterSettlement();
            Assert.That(session.PirateExperience, Is.EqualTo(546));
        }

        [TestCase(VoyageOutcome.ArrivedAtPort)]
        [TestCase(VoyageOutcome.TreasureRecovered)]
        public void ExperienceNormalization_PreservesTheSixtyPointCompletionBonus(VoyageOutcome outcome)
        {
            var session = new CoreVoyageSession(); session.StartVoyage();
            Set(session, "TotalDistanceNm", 100f * session.Map.Route.NauticalMilesPerMapUnit);
            typeof(CoreVoyageSession).GetMethod("FinishVoyage", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { outcome });
            Assert.That(session.Settlement.BankedPirateExperience, Is.EqualTo(225));
        }

        private static CoreVoyageSession Profile(int xp, int fragments)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = xp, fragments = fragments });
            return session;
        }

        private static void ResetCraftInventory(CoreVoyageSession session)
        {
            CoreTreasureProgress progress = session.CaptureTreasureProgress();
            progress.fragments = CoreTreasureHuntCatalog.FragmentCost;
            progress.maps = new CoreTreasureMapSave[0];
            progress.selectedMapId = string.Empty;
            session.RestoreTreasureProgress(progress);
        }

        private static string FindSeedWithRankRollBelow(double upperBound, string prefix)
        {
            for (int index = 0; index < 100000; index++)
            {
                string seed = prefix + index;
                double roll = new Mulberry32Random(SeedHash.Compute("TREASURE-RANK::" + seed)).NextDouble();
                if (roll < upperBound) return seed;
            }
            Assert.Fail("Could not find a deterministic seed below the requested rank roll.");
            return string.Empty;
        }
        private static CoreVoyageSession Ready(string rankId, string seed)
        {
            var session = Profile(1400, 0);
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1400,
                maps = new[] { new CoreTreasureMapSave { id = "test:" + seed, seed = seed, rankId = rankId } }, selectedMapId = "test:" + seed });
            EmptyHold(session); session.Cargo.Water = 100; session.Cargo.Food = 100; session.Cargo.Powder = 8;
            return session;
        }
        private static void EmptyHold(CoreVoyageSession session)
        {
            session.Cargo.Water = session.Cargo.Food = session.Cargo.Wood = session.Cargo.Iron = session.Cargo.Cloth = 0;
            session.Cargo.Powder = session.Cargo.Cannonballs = session.Cargo.Beer = session.Cargo.Medicine = 0;
        }
        // Isolated rule tests enter authored encounters directly. PlayMode acceptance owns navigation and UI agency.
        private static void Enter(CoreVoyageSession session, CoreMapNode node)
        {
            typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, node);
            Set(session, "ActiveEncounter", node.Encounter); Set(session, "Phase", CoreDemoPhase.Encounter);
        }
        private static void Set(CoreVoyageSession session, string property, object value) => typeof(CoreVoyageSession).GetProperty(property).SetValue(session, value);
        private static double Distance(VoyagePoint a, VoyagePoint b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
        private static HashSet<int> Reachable(CoreVoyageMap map)
        {
            var visited = new HashSet<int>(); var queue = new Queue<int>();
            int start = map.CellIndex(map.Departure); visited.Add(start); queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int next = queue.Dequeue(), x = next % CoreVoyageMap.GridColumns, y = next / CoreVoyageMap.GridColumns;
                foreach (var delta in new[] { new[] { -1, 0 }, new[] { 1, 0 }, new[] { 0, -1 }, new[] { 0, 1 } })
                {
                    int xx = x + delta[0], yy = y + delta[1];
                    if (map.IsPassable(xx, yy)) { int cell = map.CellIndex(xx, yy); if (visited.Add(cell)) queue.Enqueue(cell); }
                }
            }
            return visited;
        }
    }
}
