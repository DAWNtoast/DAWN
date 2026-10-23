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
            var session = Profile(99, 28);
            Assert.That(session.TryCraftTreasureMap("ONE", out string reason), Is.False);
            Assert.That(reason, Does.Contain("等级"));
            Assert.That(session.TryIdentifyTreasureFragment(out _), Is.False);
            Assert.That(session.TreasureFragments, Is.EqualTo(28));
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 100, fragments = 28 });
            Assert.That(session.TryCraftTreasureMap("ONE", out reason), Is.False);
            Assert.That(reason, Does.Contain("鉴定"));
            IdentifyAll(session);
            Assert.That(session.TryCraftTreasureMapForRank("weathered", new string('X', 65), out _), Is.False);
            Assert.That(session.GetIdentifiedTreasureFragments("weathered"), Is.EqualTo(28));
            Assert.That(session.TryCraftTreasureMap("ONE", out reason), Is.True, reason);
            Assert.That(session.TreasureMaps.Single().RankId, Is.EqualTo("weathered"));
            Assert.That(session.SelectedTreasureMap, Is.Null, "Crafting only creates a seed; departure remains an explicit choice.");
            Assert.That(session.TryCraftTreasureMap(" ONE ", out _), Is.False);
            Assert.That(session.GetIdentifiedTreasureFragments("weathered"), Is.EqualTo(24));
            for (int i = 1; i < 6; i++) Assert.That(session.TryCraftTreasureMap("HELD-" + i, out reason), Is.True, reason);
            Assert.That(session.TryCraftTreasureMap("SEVENTH", out reason), Is.False);
            Assert.That(reason, Does.Contain("6"));
            Assert.That(session.TreasureMaps.Count, Is.EqualTo(6));
            Assert.That(session.TreasureFragments, Is.Zero);
            Assert.That(session.GetIdentifiedTreasureFragments("weathered"), Is.EqualTo(4));
        }

        [Test]
        public void MoonlitPity_IncreasesAfterEligibleIdentificationsAndResetsOnMoonlit()
        {
            var session = Profile(1400, 10);
            Assert.That(session.MoonlitPityCount, Is.Zero);
            Assert.That(session.MoonlitCraftChance, Is.EqualTo(.15d).Within(.000001d));

            for (int attempt = 0; attempt < CoreTreasureHuntCatalog.MoonlitPityCap; attempt++)
            {
                CoreTreasureProgress before = session.CaptureTreasureProgress();
                before.identificationSeed = FindIdentificationSeedWithRollBelow(.01d, before.identificationSequence, "PITY-" + attempt + "-");
                session.RestoreTreasureProgress(before);
                Assert.That(session.TryIdentifyTreasureFragment(out string reason), Is.True, reason);
                Assert.That(session.GetIdentifiedTreasureFragments("moonlit"), Is.Zero);
                Assert.That(session.MoonlitPityCount, Is.EqualTo(attempt + 1));
                Assert.That(session.MoonlitCraftChance, Is.EqualTo(System.Math.Min(1d, .15d * (attempt + 2))).Within(.000001d));
            }

            CoreTreasureProgress saved = session.CaptureTreasureProgress();
            var restored = new CoreVoyageSession();
            restored.RestoreTreasureProgress(saved);
            Assert.That(restored.MoonlitPityCount, Is.EqualTo(CoreTreasureHuntCatalog.MoonlitPityCap));
            Assert.That(restored.MoonlitCraftChance, Is.EqualTo(1d).Within(.000001d));

            Assert.That(restored.TryIdentifyTreasureFragment(out string guaranteedReason), Is.True, guaranteedReason);
            Assert.That(restored.GetIdentifiedTreasureFragments("moonlit"), Is.EqualTo(1));
            Assert.That(restored.MoonlitPityCount, Is.Zero);
            Assert.That(restored.MoonlitCraftChance, Is.EqualTo(.15d).Within(.000001d));
            Assert.That(restored.TryCraftTreasureMapForRank("weathered", "CRAFT-KEEPS-PITY", out _), Is.True);
            Assert.That(restored.MoonlitPityCount, Is.Zero, "Pity changes during identification only.");
        }

        [Test]
        public void MoonlitPity_DoesNotAdvanceBeforeMoonlitUnlock()
        {
            var session = Profile(260, 4);
            Assert.That(session.TryIdentifyTreasureFragment(out string reason), Is.True, reason);
            Assert.That(session.MoonlitPityCount, Is.Zero);
            Assert.That(session.MoonlitCraftChance, Is.Zero);
        }

        [TestCase(100, "weathered")]
        [TestCase(260, "weathered,gilded")]
        [TestCase(900, "weathered,gilded,moonlit")]
        public void Identification_AssignsOneFragmentToOneUnlockedMapAndReloadDoesNotReroll(int xp, string allowed)
        {
            var seen = new HashSet<string>();
            var left = Profile(xp, 80);
            for (int i = 0; i < 80; i++)
            {
                var right = new CoreVoyageSession();
                right.RestoreTreasureProgress(UnityEngine.JsonUtility.FromJson<CoreTreasureProgress>(
                    UnityEngine.JsonUtility.ToJson(left.CaptureTreasureProgress())));
                var before = CoreTreasureHuntCatalog.Ranks.ToDictionary(rank => rank.Id, rank => left.GetIdentifiedTreasureFragments(rank.Id));
                Assert.That(left.TryIdentifyTreasureFragment(out string reason), Is.True, reason);
                Assert.That(right.TryIdentifyTreasureFragment(out string restoredReason), Is.True, restoredReason);
                Assert.That(restoredReason, Is.EqualTo(reason));
                string assigned = CoreTreasureHuntCatalog.Ranks.Single(rank => left.GetIdentifiedTreasureFragments(rank.Id) == before[rank.Id] + 1).Id;
                seen.Add(assigned);
                foreach (CoreTreasureMapRank rank in CoreTreasureHuntCatalog.Ranks)
                    Assert.That(left.GetIdentifiedTreasureFragments(rank.Id), Is.EqualTo(right.GetIdentifiedTreasureFragments(rank.Id)));
                Assert.That(CoreTreasureHuntCatalog.Ranks.Sum(rank => left.GetIdentifiedTreasureFragments(rank.Id)), Is.EqualTo(i + 1));
                Assert.That(left.TreasureFragments, Is.EqualTo(79 - i));
            }
            Assert.That(seen, Is.EquivalentTo(allowed.Split(',')));
            Assert.That(left.TryIdentifyTreasureFragment(out _), Is.False);
            Assert.That(left.TreasureMaps, Is.Empty, "Identification never creates a complete map or seed.");
        }

        [Test]
        public void Crafting_RequiresFourOfTheChosenMapAndKeepsOtherFragmentsAndExistingSelection()
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1400, fragments = 8,
                identifiedWeatheredFragments = 3, identifiedGildedFragments = 3, identifiedMoonlitFragments = 2 });
            Assert.That(session.TryCraftTreasureMap("MIXED", out _), Is.False);
            foreach (CoreTreasureMapRank rank in CoreTreasureHuntCatalog.Ranks)
                Assert.That(session.TryCraftTreasureMapForRank(rank.Id, "MIXED", out _), Is.False);
            CoreTreasureProgress progress = session.CaptureTreasureProgress();
            progress.identifiedGildedFragments = 9;
            session.RestoreTreasureProgress(progress);
            Assert.That(session.TryCraftTreasureMapForRank("gilded", "GOLD-ONE", out _), Is.True);
            Assert.That(session.TreasureMaps.Single().RankId, Is.EqualTo("gilded"));
            Assert.That(session.GetIdentifiedTreasureFragments("gilded"), Is.EqualTo(5));
            Assert.That(session.TrySelectTreasureSeed(" GOLD-ONE ", out _), Is.True);
            Assert.That(session.TryCraftTreasureMapForRank("gilded", "GOLD-TWO", out _), Is.True);
            Assert.That(session.SelectedTreasureMap.Seed, Is.EqualTo("GOLD-ONE"));
            Assert.That(session.GetIdentifiedTreasureFragments("gilded"), Is.EqualTo(1));
            Assert.That(session.GetIdentifiedTreasureFragments("weathered"), Is.EqualTo(3));
            Assert.That(session.GetIdentifiedTreasureFragments("moonlit"), Is.EqualTo(2));
            Assert.That(session.TreasureFragments, Is.EqualTo(8));
            Assert.That(session.TrySelectTreasureSeed("OTHER-CAPTAINS-SEED", out _), Is.False);
            Assert.That(session.TrySelectTreasureSeed(null, out _), Is.False);
            Assert.That(session.SelectedTreasureMap.Seed, Is.EqualTo("GOLD-ONE"));
        }

        [Test]
        public void FailedIdentificationOrRankCraft_DoesNotSpendFragmentsOrAdvanceRandomness()
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 100, fragments = 2,
                identifiedWeatheredFragments = int.MaxValue, identifiedGildedFragments = 4 });
            string before = UnityEngine.JsonUtility.ToJson(session.CaptureTreasureProgress());
            Assert.That(session.TryIdentifyTreasureFragment(out _), Is.False);
            Assert.That(session.TryCraftTreasureMapForRank("gilded", "LOCKED", out _), Is.False);
            Assert.That(session.TryCraftTreasureMapForRank("missing", "INVALID", out _), Is.False);
            Assert.That(UnityEngine.JsonUtility.ToJson(session.CaptureTreasureProgress()), Is.EqualTo(before));
            session.StartVoyage();
            before = UnityEngine.JsonUtility.ToJson(session.CaptureTreasureProgress());
            Assert.That(session.TryIdentifyTreasureFragment(out _), Is.False);
            Assert.That(session.TryCraftTreasureMapForRank("weathered", "AT-SEA", out _), Is.False);
            Assert.That(UnityEngine.JsonUtility.ToJson(session.CaptureTreasureProgress()), Is.EqualTo(before));
        }

        [Test]
        public void Persistence_RoundTripsOwnershipSelectionResourcesAndPermanentCompletion()
        {
            var session = Profile(100, 8);
            IdentifyAll(session);
            Assert.That(session.TryCraftTreasureMap("SAVE-ONE", out _), Is.True);
            Assert.That(session.TryCraftTreasureMap("SAVE-TWO", out _), Is.True);
            Assert.That(session.TrySelectTreasureSeed("SAVE-TWO", out _), Is.True);
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
            saved.fragments = 4; saved.identifiedWeatheredFragments = 4; restored.RestoreTreasureProgress(saved);
            Assert.That(restored.TryCraftTreasureMap("OLD-COMPLETE", out _), Is.False);
            Assert.That(restored.TreasureFragments, Is.EqualTo(4));
            Assert.That(restored.GetIdentifiedTreasureFragments("weathered"), Is.EqualTo(4));
        }

        [Test]
        public void LegacySave_KeepsAllUnidentifiedFragmentsAndAlreadyCraftedMaps()
        {
            const string legacy = "{\"pirateExperience\":900,\"fragments\":17,\"moonlitPityCount\":2,\"selectedMapId\":\"old-map\",\"maps\":[{\"id\":\"old-map\",\"seed\":\"OLD-SEED\",\"rankId\":\"gilded\"}]}";
            var first = new CoreVoyageSession(); var reload = new CoreVoyageSession();
            first.RestoreTreasureProgress(UnityEngine.JsonUtility.FromJson<CoreTreasureProgress>(legacy));
            reload.RestoreTreasureProgress(UnityEngine.JsonUtility.FromJson<CoreTreasureProgress>(legacy));
            Assert.That(first.TreasureFragments, Is.EqualTo(17));
            Assert.That(CoreTreasureHuntCatalog.Ranks.Sum(rank => first.GetIdentifiedTreasureFragments(rank.Id)), Is.Zero);
            Assert.That(first.SelectedTreasureMap.Seed, Is.EqualTo("OLD-SEED"));
            Assert.That(first.MoonlitPityCount, Is.EqualTo(2));
            Assert.That(first.TryCraftTreasureMap("CANNOT-BYPASS-IDENTIFICATION", out _), Is.False);
            Assert.That(first.TryIdentifyTreasureFragment(out string result), Is.True);
            Assert.That(reload.TryIdentifyTreasureFragment(out string reloadedResult), Is.True);
            Assert.That(reloadedResult, Is.EqualTo(result), "Even the first identification from an old save cannot be rerolled by reloading.");
            Assert.That(first.TreasureFragments, Is.EqualTo(16));
            Assert.That(first.TreasureMaps.Single().Seed, Is.EqualTo("OLD-SEED"));
            CoreTreasureProgress upgraded = first.CaptureTreasureProgress();
            Assert.That(upgraded.identificationSequence, Is.EqualTo(1));
            Assert.That(upgraded.identificationSeed, Is.Not.Empty);
        }

        [Test]
        public void CraftedSeed_CanBeUsedFromEveryPortWithIdenticalGeography()
        {
            var crafter = Profile(100, 4); IdentifyAll(crafter);
            Assert.That(crafter.TryCraftTreasureMapForRank("weathered", "ANY-PORT", out _), Is.True);
            CoreTreasureProgress held = crafter.CaptureTreasureProgress();
            CoreVoyageMap first = null;
            foreach (string port in CoreVoyageSession.DestinationPorts)
            {
                var session = new CoreVoyageSession(); session.RestoreTreasureProgress(held); session.RestoreProfile(500, port);
                session.SelectRoute(2);
                Assert.That(session.TrySelectTreasureSeed("ANY-PORT", out string reason), Is.True, reason);
                Assert.That(session.TryStartTreasureVoyage(out reason), Is.True, reason);
                Assert.That(session.Map.DepartureName, Is.EqualTo(port));
                Assert.That(session.Map.DestinationName, Is.EqualTo("秘藏核心"));
                if (first == null) first = session.Map;
                Assert.That(session.Map.Hash, Is.EqualTo(first.Hash));
                Assert.That(session.Map.TerrainCells, Is.EqualTo(first.TerrainCells));
                Assert.That(session.Map.Nodes.Select(node => node.Position), Is.EqualTo(first.Nodes.Select(node => node.Position)));
                Assert.That(session.Map.Nodes.Select(node => node.Encounter.Id), Is.EqualTo(first.Nodes.Select(node => node.Encounter.Id)));
                Assert.That(session.TreasureMaps, Is.Empty);
                session.SafeReturn(); session.ReturnToPortAfterSettlement();
                Assert.That(session.CurrentPort, Is.EqualTo(port));
                Assert.That(session.TrySelectTreasureSeed("ANY-PORT", out _), Is.False, "An already used seed cannot open another voyage.");
            }
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
        public void LegacyHiddenMaps_AreSeedBoundReachableSpacedAndGuaranteeClues(string rankId)
        {
            CoreTreasureMapRank rank = CoreTreasureHuntCatalog.FindRank(rankId);
            for (int seed = 0; seed < 8; seed++)
            {
                var a = Ready(rankId, "GEOGRAPHY-" + seed); var b = Ready(rankId, "GEOGRAPHY-" + seed);
                // This fixture verifies fog and geometry without departure intelligence.
                // NavigatorDepartureRevealTests separately checks the intentional two-point bonus.
                a.ConfigureCrewCapabilities(false, false, false, false, false, false);
                b.ConfigureCrewCapabilities(false, false, false, false, false, false);
                b.RestoreProfile(500, "灰鸥港");
                Assert.That(a.TryStartTreasureVoyage(out string reason), Is.True, reason);
                Assert.That(b.TryStartTreasureVoyage(out reason), Is.True, reason);
                a = ResumeLegacyVoyage(a); b = ResumeLegacyVoyage(b);
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
                Assert.That(session.CompletedEventCount(), Is.EqualTo(2));
                Assert.That(session.VoyageTreasureFragments, Is.EqualTo(2));
                Assert.That(session.TreasureFragments, Is.EqualTo(trip * 2));
                if (trip == 0)
                {
                    CoreVoyageCheckpoint checkpoint = session.CaptureVoyageCheckpoint();
                    var resumed = new CoreVoyageSession();
                    Assert.That(resumed.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
                    session = resumed;
                    Assert.That(session.CompletedEventCount(), Is.EqualTo(2));
                }
                Assert.That(session.SafeReturn(), Is.True);
                Assert.That(session.Settlement.BankedFragments, Is.EqualTo(2), "Fragments are intangible and not rounded down by cargo protection.");
                Assert.That(session.Settlement.BankedPirateExperience, Is.GreaterThan(0));
                session.ReturnToPortAfterSettlement();
                int xp = session.PirateExperience;
                session.ReturnToPortAfterSettlement();
                Assert.That(session.TreasureFragments, Is.EqualTo((trip + 1) * 2));
                Assert.That(session.PirateExperience, Is.EqualTo(xp));
                var restored = new CoreVoyageSession();
                restored.RestoreTreasureProgress(session.CaptureTreasureProgress());
                restored.ReturnToPortAfterSettlement();
                Assert.That(restored.PirateExperience, Is.EqualTo(xp), "Loading confirmed progress must not credit the voyage again.");
            }
            Assert.That(session.TreasureFragments, Is.EqualTo(4));
        }

        [TestCase("weathered", 0)]
        [TestCase("gilded", 0)]
        [TestCase("moonlit", 0)]
        [TestCase("weathered", 1)]
        [TestCase("weathered", 2)]
        public void LegacyCoreOpening_RequiresCluesAndCostsThenSettlesOnceAtNinetyFivePercent(string rankId, int choice)
        {
            var session = Ready(rankId, "CORE-" + rankId + choice);
            Assert.That(session.TryStartTreasureVoyage(out _), Is.True);
            session = ResumeLegacyVoyage(session);
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
        public void LegacyCoreOpening_RejectsCargoOverflowLethalBreachAndAbsentDiverWithoutMutations()
        {
            var session = Ready("weathered", "CORE-GATES"); session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            Assert.That(session.TryStartTreasureVoyage(out _), Is.True);
            session = ResumeLegacyVoyage(session);
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
            var saved = session.CaptureTreasureProgress(); saved.identifiedWeatheredFragments = 4;
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
            var progress = session.CaptureTreasureProgress(); progress.identifiedWeatheredFragments = 4;
            session.RestoreTreasureProgress(progress);
            Assert.That(session.TryCraftTreasureMap("SECOND", out string reason), Is.True, reason);
            Assert.That(session.TrySelectTreasureSeed("SECOND", out reason), Is.True, reason);
            Assert.That(session.TryStartTreasureVoyage(out reason), Is.True, reason);
            Assert.That(session.IsTreasureVoyage, Is.True);
            Assert.That(session.ActiveTreasureMap.Seed, Is.EqualTo("SECOND"));
        }

        [Test]
        public void LegacyEarlyCoreVisit_CanLeaveCollectRealCluesAndRevisitWithoutLosingTheAdventure()
        {
            var session = Ready("weathered", "EARLY-CORE");
            Assert.That(session.TryStartTreasureVoyage(out _), Is.True);
            session = ResumeLegacyVoyage(session);
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
                var original = ((IReadOnlyList<CoreEncounter>)typeof(CoreVoyageSession)
                    .GetMethod("CreateEncounterPool", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(first, new object[] { first.Map.Route })).Select(item => item.Id).Distinct().ToArray();
                var expansion = new HashSet<string>(CoreVoyageEventCatalog.CreateFixedEncounters(first.Map.Route.Id)
                    .Select(item => item.Id));
                int expansionCount = Math.Min(4, expansion.Count);
                // Current geometry keeps every original event, chooses four additions,
                // and places exactly one of this route's bosses at the destination.
                CollectionAssert.AreEquivalent(original, first.Map.Nodes.Where(node => original.Contains(node.Encounter.Id))
                    .Select(node => node.Encounter.Id));
                Assert.That(first.Map.Nodes.Count(node => expansion.Contains(node.Encounter.Id)), Is.EqualTo(expansionCount));
                Assert.That(first.Map.Nodes.Select(node => node.Encounter.Id).Distinct().Count(), Is.EqualTo(first.Map.Nodes.Count));
                CoreMapNode boss = first.Map.Nodes.Single(node => node.IsBoss);
                Assert.That(CoreRouteBossCatalog.ForRoute(first.Map.Route.Id).Select(item => item.Id), Does.Contain(boss.Encounter.Id));
                Assert.That(first.Map.Nodes.Count, Is.EqualTo(original.Length + expansionCount + 1));
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
        [TestCase(20f, false, false, 0)]
        [TestCase(2700f, false, false, 0)]
        [TestCase(0f, true, false, 25)]
        [TestCase(0f, true, true, 0)]
        [TestCase(20f, true, false, 26)]
        [TestCase(2700f, true, true, 0)]
        public void OrdinarySafeReturn_RequiresCompletedEventsAndExcludesSkippedEvents(float distanceNm, bool visited, bool skipped, int expected)
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
            session.Map.Nodes[0].Visited = true; // Settlement math fixture: one completed event keeps an ordinary return eligible.
            Assert.That(session.CompletedEventCount(), Is.EqualTo(1));
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.BankedPirateExperience, Is.EqualTo(175));
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

        [Test]
        public void ActualTwentyMileEmptyReturn_StaysZeroAfterVoyageResumeAndPortReload()
        {
            var session = ShortReturnProfile();
            SailEmptyTwentyMiles(session);
            Assert.That(session.TotalHours, Is.GreaterThan(0d));
            CoreVoyageCheckpoint checkpoint = session.CaptureVoyageCheckpoint();
            var resumed = new CoreVoyageSession();
            Assert.That(resumed.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
            Assert.That(resumed.TotalDistanceNm, Is.EqualTo(20f).Within(.001f));
            Assert.That(resumed.CompletedEventCount(), Is.Zero);
            Assert.That(resumed.SafeReturn(), Is.True);
            Assert.That(resumed.Settlement.Outcome, Is.EqualTo(VoyageOutcome.SafeReturn));
            Assert.That(resumed.Settlement.BankedPirateExperience, Is.Zero);
            Assert.That(resumed.PirateExperience, Is.EqualTo(100), "Pending settlement must not mutate the profile.");
            Assert.That(resumed.SafeReturn(), Is.False);
            resumed.ReturnToPortAfterSettlement();
            resumed.ReturnToPortAfterSettlement();
            Assert.That(resumed.PirateExperience, Is.EqualTo(100));
            Assert.That(resumed.PirateLevel, Is.EqualTo(2));

            var restored = ShortReturnProfile();
            restored.RestoreTreasureProgress(resumed.CaptureTreasureProgress());
            restored.ReturnToPortAfterSettlement();
            Assert.That(restored.PirateExperience, Is.EqualTo(100));
            SailEmptyTwentyMiles(restored);
            Assert.That(restored.SafeReturn(), Is.True);
            Assert.That(restored.Settlement.BankedPirateExperience, Is.Zero);
            restored.ReturnToPortAfterSettlement();
            Assert.That(restored.PirateExperience, Is.EqualTo(100));
        }

        [Test]
        public void SixtyFiveActualEmptyShortReturns_DoNotLevelUpAndRetainPaidSupplyCosts()
        {
            var session = ShortReturnProfile();
            int startingWater = session.Cargo.Water, startingFood = session.Cargo.Food;
            int purchasedWater = 0, purchasedFood = 0;
            decimal startingGold = session.ProfileGold + session.Cargo.Gold, supplyCost = 0m;
            double sailedHours = 0d;
            for (int trip = 0; trip < 65; trip++)
            {
                if (session.Cargo.Water < 6 || session.Cargo.Food < 6)
                {
                    var order = new VoyageSupplyOrder { Water = 60, Food = 60 };
                    decimal cost = session.QuoteSupplyPurchase(order).Cost;
                    Assert.That(session.TryPurchaseSupplies(order), Is.True, "Trip " + trip);
                    purchasedWater += order.Water; purchasedFood += order.Food; supplyCost += cost;
                }
                SailEmptyTwentyMiles(session);
                sailedHours += session.TotalHours;
                Assert.That(session.SafeReturn(), Is.True, "Trip " + trip);
                Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.SafeReturn));
                Assert.That(session.Settlement.CompletedEvents, Is.Zero);
                Assert.That(session.Settlement.BankedPirateExperience, Is.Zero);
                session.ReturnToPortAfterSettlement();
                session.ReturnToPortAfterSettlement();
                Assert.That(session.PirateExperience, Is.EqualTo(100), "Trip " + trip);
                Assert.That(session.PirateLevel, Is.EqualTo(2), "Trip " + trip);
            }
            Assert.That(sailedHours, Is.GreaterThan(0d));
            Assert.That(startingWater + purchasedWater - session.Cargo.Water, Is.GreaterThanOrEqualTo(130));
            Assert.That(startingFood + purchasedFood - session.Cargo.Food, Is.GreaterThanOrEqualTo(65));
            Assert.That(session.ProfileGold + session.Cargo.Gold, Is.EqualTo(startingGold - supplyCost),
                "Returning the departure purse is principal, not voyage profit.");
        }

        [TestCase(1, 20)]
        [TestCase(CoreGuidedTutorialContent.NavigatorVoyageLeg, 20)]
        [TestCase(CoreGuidedTutorialContent.BossVoyageLeg, 80)]
        public void GuidedEmptyTwentyMileReturn_PreservesTutorialExperience(int leg, int expected)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.ConfigureGuidedTutorialVoyage(leg), Is.True);
            session.StartVoyage(); session.SetHeading(SailingDirection.East); session.SetDistance(20);
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.CompletedEventCount(), Is.Zero);
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.BankedPirateExperience, Is.EqualTo(expected));
        }

        [Test]
        public void EmptyOrdinaryFailure_DoesNotAwardDistanceExperience()
        {
            var session = ShortReturnProfile();
            SailEmptyTwentyMiles(session);
            session.Cargo.Water = 0;
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            Assert.That(session.Settlement.BankedPirateExperience, Is.Zero);
        }

        private static CoreVoyageSession ShortReturnProfile()
        {
            var session = Profile(100, 0);
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            session.ConfigureSpecialCardSources(Array.Empty<string>(), Array.Empty<string>());
            // The first authored chart can be replayed as an ordinary shared sea chart.
            Assert.That(session.TrySelectChartSeed("SS2.0.0.1.0.2.72533DBC.00000000.4858B60F", out string reason), Is.True, reason);
            return session;
        }

        private static void SailEmptyTwentyMiles(CoreVoyageSession session)
        {
            session.StartVoyage();
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.GuidedTutorialLeg, Is.Zero);
            Assert.That(session.IsTreasureVoyage, Is.False);
            session.SetHeading(SailingDirection.East); session.SetDistance(20);
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.TotalDistanceNm, Is.EqualTo(20f).Within(.001f));
            Assert.That(session.CompletedEventCount(), Is.Zero);
        }

        private static CoreVoyageSession Profile(int xp, int fragments)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = xp, fragments = fragments });
            return session;
        }

        private static void IdentifyAll(CoreVoyageSession session)
        {
            while (session.TreasureFragments > 0)
                Assert.That(session.TryIdentifyTreasureFragment(out string reason), Is.True, reason);
        }

        private static string FindIdentificationSeedWithRollBelow(double upperBound, long sequence, string prefix)
        {
            for (int index = 0; index < 100000; index++)
            {
                string seed = prefix + index;
                double roll = new Mulberry32Random(SeedHash.Compute("TREASURE-FRAGMENT::" + seed + "::" +
                    sequence.ToString(System.Globalization.CultureInfo.InvariantCulture))).NextDouble();
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
        // These tests describe the old four-choice vault and clue pool. Build
        // its version-zero map, serialize a full voyage, then exercise the real
        // checkpoint restore path; fresh departures still test current content.
        private static CoreVoyageSession ResumeLegacyVoyage(CoreVoyageSession source)
        {
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            CoreTreasureMapRecord active = source.ActiveTreasureMap;
            var legacy = (CoreTreasureMapRecord)typeof(CoreTreasureMapRecord).GetConstructors(hidden).Single()
                .Invoke(new object[] { active.Id, active.Seed, active.RankId, active.MapGeometryVersion, 0 });
            var map = (CoreVoyageMap)typeof(CoreVoyageSession).GetMethod("GenerateTreasureMap", hidden)
                .Invoke(source, new object[] { legacy, source.CurrentPort });
            Set(source, "Map", map); Set(source, "ActiveTreasureMap", legacy);
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            checkpoint.treasureProgress.usedMaps.Single(m => m.seed == active.Seed).contentVersion = 0;
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(UnityEngine.JsonUtility.FromJson<CoreVoyageCheckpoint>(
                UnityEngine.JsonUtility.ToJson(checkpoint)), out string reason), Is.True, reason);
            Assert.That(restored.ActiveTreasureMap.ContentVersion, Is.Zero);
            return restored;
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
