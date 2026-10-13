using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreCollectionRewardCatalogTests
    {
        private const string Beacon = "seventh_harbor_beacon_lantern";

        [Test]
        public void EveryCatalogEntry_HasExactlyOneRuleAndMatchingPlayerVisibleSource()
        {
            Assert.That(CoreCollectionRewardCatalog.All.Count, Is.EqualTo(40));
            CollectionAssert.AreEquivalent(CoreWarehouseCollection.Catalog.Select(t => t.Id),
                CoreCollectionRewardCatalog.All.Select(r => r.Id));
            Assert.That(CoreCollectionRewardCatalog.All.Select(r => r.Id).Distinct().Count(), Is.EqualTo(40));
            foreach (CoreWarehouseTreasureDefinition treasure in CoreWarehouseCollection.Catalog)
            {
                CoreCollectionRewardRule rule = CoreCollectionRewardCatalog.Find(treasure.Id);
                Assert.That(rule, Is.Not.Null, treasure.Id);
                Assert.That(rule.Source, Is.Not.Empty, treasure.Id);
                Assert.That(treasure.Source, Is.EqualTo(rule.Source), treasure.Id);
                Assert.That(treasure.Story, Is.Not.Empty, treasure.Id);
            }
            Assert.Throws<NotSupportedException>(() =>
                ((IList<CoreCollectionRewardRule>)CoreCollectionRewardCatalog.All).Clear());
        }

        [Test]
        public void OneSave_CanEarnAllFortyFromFiniteBossChartTreasureAndRecruitmentMilestones()
        {
            var progress = new CoreCollectionProgress();
            var facts = new CoreCollectionFacts();
            var collection = new CoreWarehouseCollection();
            Assert.That(Reconcile(progress, facts, collection), Is.Zero);

            progress.defeatedBossIds = KnownBosses();
            Assert.That(Reconcile(progress, facts, collection), Is.EqualTo(12));
            Assert.That(collection.OwnedIds.Count, Is.EqualTo(12));

            progress.completedChartSeeds = DistinctSeeds("chart", 5);
            progress.completedRouteIds = CoreVoyageSession.Routes.Select(r => r.Id).ToArray();
            progress.visitedPorts = CoreVoyageSession.DestinationPorts.ToArray();
            Assert.That(Reconcile(progress, facts, collection), Is.EqualTo(7));
            Assert.That(collection.OwnedIds.Count, Is.EqualTo(19));

            progress.completedTreasureSeeds = DistinctSeeds("treasure", 8);
            progress.completedTreasureRanks = new[] { "weathered", "gilded", "moonlit" };
            // Eight treasure rewards cross the twenty-keepsake threshold and unlock its extra reward.
            Assert.That(Reconcile(progress, facts, collection), Is.EqualTo(9));
            Assert.That(collection.OwnedIds.Count, Is.EqualTo(28));

            facts.recruitedCrewIds = RequiredCrewIds();
            facts.crewCardLevels = RequiredCrewCardLevels();
            Assert.That(facts.recruitedCrewIds.All(id => CrewNarrativeCatalog.FindById(id) != null), Is.True);
            Assert.That(facts.recruitedCrewIds.All(id => CoreSpecialCardCatalog.ForCrew(id) != null), Is.True,
                "Every recruitment keepsake must have an obtainable character card.");
            // Ten fully trained crew gifts, the thirty-keepsake reward, and the final beacon finish the collection.
            Assert.That(Reconcile(progress, facts, collection), Is.EqualTo(12));
            Assert.That(collection.OwnedIds.Count, Is.EqualTo(40));
            Assert.That(collection.Owns(Beacon), Is.True);
            Assert.That(Reconcile(progress, facts, collection), Is.Zero, "Replaying milestones must not duplicate ownership.");
        }

        [Test]
        public void FinalBeacon_RequiresEveryOtherEntryRatherThanAnyThirtyNineIds()
        {
            CoreCollectionRewardRule final = CoreCollectionRewardCatalog.Find(Beacon);
            string[] missingCompass = CoreCollectionRewardCatalog.All.Skip(1).Select(r => r.Id).ToArray();
            Assert.That(missingCompass.Length, Is.EqualTo(39));
            Assert.That(final.IsSatisfied(null, null, missingCompass), Is.False);
            string[] allOthers = CoreCollectionRewardCatalog.All.Where(r => r.Id != Beacon).Select(r => r.Id).ToArray();
            Assert.That(final.IsSatisfied(null, null, allOthers), Is.True);
            Assert.That(final.IsSatisfied(null, null, allOthers.Append("unknown").ToArray()), Is.True);
        }

        [Test]
        public void RepeatedMilestonesAndUnknownIds_DoNotMeetDistinctProgressRequirements()
        {
            var progress = new CoreCollectionProgress
            {
                defeatedBossIds = new[] { CoreGuidedTutorialContent.BossEventId, CoreGuidedTutorialContent.BossEventId,
                    "legendary_pirate_invented", "boss_route_invented", "ambush" },
                completedChartSeeds = new[] { "chart-1", "chart-1", null, " " },
                completedRouteIds = new[] { "route_shattered_sail_reef", "route_shattered_sail_reef", "invalid" },
                visitedPorts = new[] { "灰鸥港", "灰鸥港", "第七港口", "unknown" },
                completedTreasureSeeds = new[] { "treasure-1", "treasure-1", null, " " },
                completedTreasureRanks = new[] { "weathered", "weathered", "invalid" }
            };
            AssertSatisfied("first_victory_compass", progress, true);
            AssertSatisfied("starboard_copper_scope", progress, false);
            AssertSatisfied("saltworn_quartermaster_key", progress, true);
            AssertSatisfied("sealed_chart", progress, false);
            AssertSatisfied("old_captain_free_flag", progress, false);
            AssertSatisfied("first_berth_drydock_nail", progress, false);
            AssertSatisfied("six_port_anchor_seal", progress, false);
            AssertSatisfied("nameless_sailor_nameplate", progress, true);
            AssertSatisfied("empty_berth_registry", progress, false);
            AssertSatisfied("unfinished_seventh_berth_key", progress, false);
            var fakeOwned = Enumerable.Repeat("first_victory_compass", 30)
                .Concat(Enumerable.Range(0, 30).Select(i => "unknown-" + i)).ToArray();
            Assert.That(CoreCollectionRewardCatalog.Find("free_waters_mooring_ring").IsSatisfied(null, null, fakeOwned), Is.False);
            Assert.That(CoreCollectionRewardCatalog.Find("inheritance_oath_inkstone").IsSatisfied(null, null, fakeOwned), Is.False);
        }

        [TestCase("boatswain_bell", "boss_route_golden_tide_trade")]
        [TestCase("first_stormglass", "boss_route_shattered_sail_reef")]
        [TestCase("godbreaker_oar", "boss_route_moon_tide_ruins")]
        [TestCase("iron_oath_shackle", "legendary_pirate_iron_oath")]
        [TestCase("thunder_crown_fuse", "legendary_pirate_thunder_crown")]
        [TestCase("red_widow_grappling_hook", "legendary_pirate_red_widow")]
        [TestCase("ember_ark_fireseed", "legendary_pirate_ember_ark")]
        [TestCase("tide_requiem_gauge", "legendary_pirate_tide_requiem")]
        public void NamedBossReward_RequiresTheExactVictory(string treasure, string boss)
        {
            AssertSatisfied(treasure, new CoreCollectionProgress { defeatedBossIds = new[] { CoreGuidedTutorialContent.BossEventId } }, false);
            Assert.That(CoreCollectionRewardCatalog.IsKnownBoss(boss), Is.True);
            AssertSatisfied(treasure, new CoreCollectionProgress { defeatedBossIds = new[] { boss } }, true);
        }

        [TestCase("leviathan_bone_whistle", "weathered")]
        [TestCase("twilight_tide_moon_shard", "gilded")]
        [TestCase("storm_altar_fragment", "moonlit")]
        public void TreasureRankReward_RequiresCompletionOfItsOwnRank(string treasure, string rank)
        {
            AssertSatisfied(treasure, new CoreCollectionProgress { completedTreasureRanks = new[] { "unknown" } }, false);
            AssertSatisfied(treasure, new CoreCollectionProgress { completedTreasureRanks = new[] { rank } }, true);
        }

        [TestCase("evelyn_six_port_ruler", "crew_mad_navigator")]
        [TestCase("barlow_living_hull_plane", "crew_old_carpenter")]
        [TestCase("moer_storm_suture_kit", "crew_cold_doctor")]
        [TestCase("leisa_free_flag_badge", "crew_one_eye_gunner")]
        [TestCase("pepe_shared_pot_lid", "crew_salt_pot_cook")]
        [TestCase("noya_drowned_diver_lens", "crew_treasure_diver")]
        public void RecruitmentReward_UsesMigratedCanonicalIdentityNotAnotherPersonWithTheSameProfession(string treasure, string legacyId)
        {
            string canonicalId = CrewNarrativeCatalog.FindById(legacyId).Id;
            var facts = new CoreCollectionFacts
            {
                recruitedCrewIds = new[] { "crew_broken_anchor_navigator_lorne" },
                crewCardLevels = new Dictionary<string, int> { [canonicalId] = 3, ["crew_broken_anchor_navigator_lorne"] = 3 },
            };
            Assert.That(CoreCollectionRewardCatalog.Find(treasure).IsSatisfied(null, facts, null), Is.False);
            facts.recruitedCrewIds = new[] { canonicalId };
            Assert.That(CoreCollectionRewardCatalog.Find(treasure).IsSatisfied(null, facts, null), Is.True);
        }

        [TestCase("evelyn_six_port_ruler", "crew_gray_gull_navigator_evelyn")]
        [TestCase("barlow_living_hull_plane", "crew_gray_gull_carpenter_barlow")]
        [TestCase("moer_storm_suture_kit", "crew_gray_gull_doctor_moer")]
        [TestCase("leisa_free_flag_badge", "crew_gray_gull_gunner_leisa")]
        [TestCase("pepe_shared_pot_lid", "crew_gray_gull_cook_pepe")]
        [TestCase("lorne_first_berth_token", "crew_broken_anchor_navigator_lorne")]
        [TestCase("atang_white_shell_button", "crew_white_shell_carpenter_atang")]
        [TestCase("ada_bronze_bell_bandage", "crew_bronze_bell_doctor_ada")]
        [TestCase("helen_thirteenth_cannon_coin", "crew_bronze_bell_gunner_helen")]
        [TestCase("noya_drowned_diver_lens", "crew_gray_gull_diver_noya")]
        public void RecruitmentReward_RequiresTheRecruitedCharactersOwnCardAtLevelThree(string treasure, string crewId)
        {
            CoreCollectionRewardRule rule = CoreCollectionRewardCatalog.Find(treasure);
            var facts = new CoreCollectionFacts { recruitedCrewIds = new[] { crewId } };
            Assert.That(rule.Source, Does.Contain("角色卡牌升至 3 级"));
            Assert.That(rule.IsSatisfied(null, facts, null), Is.False, "Recruitment alone is insufficient.");
            facts.crewCardLevels = null;
            Assert.That(rule.IsSatisfied(null, facts, null), Is.False, "Missing card progress cannot prove the requirement.");
            facts.crewCardLevels = new Dictionary<string, int> { ["another_crew"] = 3 };
            Assert.That(rule.IsSatisfied(null, facts, null), Is.False, "Another character's trained card cannot qualify.");
            for (int level = 0; level <= 4; level++)
            {
                facts.crewCardLevels = new Dictionary<string, int> { [crewId] = level };
                Assert.That(rule.IsSatisfied(null, facts, null), Is.EqualTo(level >= 3), "Card level " + level);
            }
            facts.recruitedCrewIds = Array.Empty<string>();
            Assert.That(rule.IsSatisfied(null, facts, null), Is.False, "Card progress cannot replace recruitment.");
        }

        [Test]
        public void PreviouslyOwnedRecruitmentReward_RemainsOwnedWithoutTheNewCardRequirement()
        {
            var collection = new CoreWarehouseCollection();
            collection.Import("evelyn_six_port_ruler", string.Empty);
            Assert.That(Reconcile(new CoreCollectionProgress(), new CoreCollectionFacts(), collection), Is.Zero);
            Assert.That(collection.Owns("evelyn_six_port_ruler"), Is.True);
        }

        [Test]
        public void RuleEvaluation_IsNullSafeAndDoesNotMutateSuppliedProgressOrOwnership()
        {
            var progress = new CoreCollectionProgress
            {
                defeatedBossIds = KnownBosses(),
                completedChartSeeds = DistinctSeeds("chart", 5),
                completedRouteIds = CoreVoyageSession.Routes.Select(r => r.Id).ToArray(),
                visitedPorts = CoreVoyageSession.DestinationPorts.ToArray(),
                completedTreasureSeeds = DistinctSeeds("treasure", 8),
                completedTreasureRanks = new[] { "weathered", "gilded", "moonlit" }
            };
            var facts = new CoreCollectionFacts
            {
                recruitedCrewIds = RequiredCrewIds(),
                crewCardLevels = RequiredCrewCardLevels(),
            };
            var owned = new[] { CoreWarehouseCollection.TutorialTreasureId };
            string before = Snapshot(progress, facts, owned);
            foreach (CoreCollectionRewardRule rule in CoreCollectionRewardCatalog.All)
            {
                Assert.That(rule.IsSatisfied(null, null, null), Is.False, rule.Id);
                rule.IsSatisfied(progress, facts, owned);
            }
            Assert.That(Snapshot(progress, facts, owned), Is.EqualTo(before));
        }

        private static string Snapshot(CoreCollectionProgress p, CoreCollectionFacts f, string[] owned) =>
            string.Join("|", p.defeatedBossIds.Concat(p.completedChartSeeds).Concat(p.completedRouteIds)
                .Concat(p.visitedPorts).Concat(p.completedTreasureSeeds).Concat(p.completedTreasureRanks)
                .Concat(f.recruitedCrewIds).Concat(f.crewCardLevels.OrderBy(pair => pair.Key).Select(pair => pair.Key + "=" + pair.Value))
                .Concat(owned));

        private static void AssertSatisfied(string id, CoreCollectionProgress progress, bool expected) =>
            Assert.That(CoreCollectionRewardCatalog.Find(id).IsSatisfied(progress, null, null), Is.EqualTo(expected), id);

        private static int Reconcile(CoreCollectionProgress progress, CoreCollectionFacts facts, CoreWarehouseCollection collection)
        {
            int initialCount = collection.OwnedIds.Count;
            bool changed;
            do
            {
                changed = false;
                foreach (CoreCollectionRewardRule rule in CoreCollectionRewardCatalog.All)
                    if (rule.IsSatisfied(progress, facts, collection.OwnedIds) && collection.Grant(rule.Id)) changed = true;
            } while (changed);
            return collection.OwnedIds.Count - initialCount;
        }

        private static string[] DistinctSeeds(string prefix, int count) =>
            Enumerable.Range(1, count).Select(i => prefix + "-" + i).ToArray();

        private static string[] KnownBosses() => CoreVoyageSession.Routes.Select(r => "boss_" + r.Id)
            .Concat(CoreLegendaryWarships.All.Select(b => b.Id))
            .Concat(new[] { CoreGuidedTutorialContent.BossEventId, CoreBattleBalance.TreasureBossId }).ToArray();

        private static string[] RequiredCrewIds() => new[]
        {
            "crew_gray_gull_navigator_evelyn", "crew_gray_gull_carpenter_barlow", "crew_gray_gull_doctor_moer",
            "crew_gray_gull_gunner_leisa", "crew_gray_gull_cook_pepe", "crew_broken_anchor_navigator_lorne",
            "crew_white_shell_carpenter_atang", "crew_bronze_bell_doctor_ada", "crew_bronze_bell_gunner_helen",
            "crew_gray_gull_diver_noya"
        };

        private static IReadOnlyDictionary<string, int> RequiredCrewCardLevels() => RequiredCrewIds()
            .Where(id => CoreSpecialCardCatalog.ForCrew(id) != null).ToDictionary(id => id, id => 3);
    }
}
