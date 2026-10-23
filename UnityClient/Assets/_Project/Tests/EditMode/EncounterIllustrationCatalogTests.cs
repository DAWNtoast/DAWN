using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class EncounterIllustrationCatalogTests
    {
        private static readonly string[] Routes = {
            "route_shattered_sail_reef", "route_golden_tide_trade", "route_moon_tide_ruins"
        };

        private static IEnumerable<string> EventIds()
        {
            foreach (string route in Routes)
            {
                foreach (string id in CoreVoyageEventCatalog.RandomEventIds(route)) yield return id;
                foreach (CoreEncounter encounter in CoreVoyageEventCatalog.CreateExpandedFixedEncounters(route)) yield return encounter.Id;
            }
            foreach (CoreTreasureMapRank rank in CoreTreasureHuntCatalog.Ranks)
            {
                foreach (CoreEncounter encounter in CoreTreasureHuntCatalog.CreateEncounters(rank)) yield return encounter.Id;
                foreach (CoreEncounter encounter in CoreTreasureExpeditionCatalog.CreateEncounters(rank)) yield return encounter.Id;
            }
            string[] otherIds = {
                "event_reef_sharp_current", "event_reef_hidden_cache", "event_reef_broken_mast", "event_reef_shortcut",
                "event_reef_sunken_boat", "event_reef_saltwater_leak", "event_reef_fisher_cache", "event_reef_coral_ambush",
                "gold_convoy", "gold_customs", "gold_wreck", "moon_obelisk", "moon_whisper", "moon_ghost",
                "common_beach_castaway", "common_strait_beacon", "common_floating_market", "common_whale_song",
                "common_storm_debris", "common_reef_divers", "random_tailwind", "random_floating_supplies",
                "random_food_spoilage", "random_food_spoil", "random_ambush", "random_ambush_warning",
                "tutorial_first_event", "tutorial_optional_event", "tutorial_crew_requirement_event", "tutorial_navigator_event",
                "tutorial_hidden_event", "tutorial_ambush_event", "tutorial_boss_event", "guided_tutorial_random_event",
                "boss_route_shattered_sail_reef", "boss_route_golden_tide_trade", "boss_route_moon_tide_ruins"
            };
            foreach (string id in otherIds) yield return id;
            yield return CoreBattleBalance.TreasureBossId;
            foreach (CoreRouteBoss boss in CoreRouteBossCatalog.All) yield return boss.Id;
            foreach (CoreLegendaryWarship ship in CoreLegendaryWarships.All) yield return ship.Id;
        }

        private sealed class AuditEntry
        {
            public string Group;
            public string Context;
            public CoreEncounter Event;
        }

        private static IEnumerable<CoreEncounter> CurrentEvents() => CurrentEntries().Select(entry => entry.Event);

        private static List<AuditEntry> CurrentEntries()
        {
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            var entries = new List<AuditEntry>();
            void Add(string group, string context, CoreEncounter encounter) =>
                entries.Add(new AuditEntry { Group = group, Context = context, Event = encounter });
            var session = new CoreVoyageSession();
            session.StartVoyage();
            foreach (CoreRouteDefinition route in CoreVoyageSession.Routes)
            {
                var pool = (IReadOnlyList<CoreEncounter>)typeof(CoreVoyageSession).GetMethod("CreateEncounterPool", hidden)
                    .Invoke(session, new object[] { route });
                foreach (CoreEncounter encounter in pool)
                    Add("base", route.Id, CoreVoyageEventCatalog.EnrichExploration(encounter, route.Id));
                foreach (CoreEncounter encounter in CoreVoyageEventCatalog.CreateExpandedFixedEncounters(route.Id))
                    Add("fixed", route.Id, CoreVoyageEventCatalog.EnrichExploration(encounter, route.Id));
                foreach (string id in CoreVoyageEventCatalog.RandomEventIds(route.Id))
                    Add("random", route.Id, CoreVoyageEventCatalog.CreateRandomEncounter(id, route.RecommendedPower));
            }
            foreach (CoreRouteBoss boss in CoreRouteBossCatalog.All) Add("routeBoss", boss.RouteId, boss.CreateEncounter());
            foreach (CoreLegendaryWarship ship in CoreLegendaryWarships.All) Add("legendary", ship.SchoolId, ship.CreateEncounter());
            Add("treasureBoss", "moon", CoreLegendaryWarships.CreateTreasureEncounter());
            foreach (CoreTreasureMapRank rank in CoreTreasureHuntCatalog.Ranks)
                foreach (CoreEncounter encounter in CoreTreasureExpeditionCatalog.CreateEncounters(rank)) Add("treasure", rank.Id, encounter);
            foreach (string method in new[] { "FirstEvent", "OptionalEvent", "CrewRequirementEvent", "NavigatorEvent", "AmbushEvent", "BossEvent" })
                Add("tutorial", method, Guided(method));
            Add("tutorial", "RandomEvent:False", Guided("RandomEvent", false));
            Add("tutorial", "RandomEvent:True", Guided("RandomEvent", true));
            foreach (string id in new[] { "random_tailwind", "random_floating_supplies" })
                Add("common", "all routes", (CoreEncounter)typeof(CoreVoyageSession).GetMethod("CreateSeaChartRandomEncounter", hidden).Invoke(session, new object[] { id }));
            foreach (bool detected in new[] { false, true })
                Add("common", "treasure/unmapped voyage", (CoreEncounter)typeof(CoreVoyageSession).GetMethod("CreateRandomAmbush", hidden).Invoke(session, new object[] { detected }));
            return entries;
        }

        private static CoreEncounter Guided(string method, params object[] arguments) =>
            (CoreEncounter)typeof(CoreGuidedTutorialContent).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, arguments);

        private static HashSet<string> ReferencedPaths()
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in EventIds().Distinct())
            {
                Assert.That(EncounterIllustrationCatalog.TryGetLocation(id, out var location), Is.True, id);
                paths.Add(location.ResourcePath);
            }
            foreach (CoreEncounter encounter in CurrentEvents())
            {
                Assert.That(EncounterIllustrationCatalog.TryGetLocation(encounter, out var location), Is.True, encounter.Name);
                paths.Add(location.ResourcePath);
            }
            return paths;
        }

        [Test]
        public void EveryRuntimeStoryVariantHasAnAuthoredIllustration()
        {
            List<AuditEntry> entries = CurrentEntries();
            CoreEncounter[] events = entries.Select(entry => entry.Event).ToArray();
            Assert.That(events.Length, Is.EqualTo(210));
            Assert.That(events.Select(e => e.Id).Distinct().Count(), Is.EqualTo(195));
            var csv = new StringBuilder("group,context,eventId,eventName,description,resourcePath,uvX,uvY,uvWidth,uvHeight,mappingKind\n");
            var currentPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (AuditEntry entry in entries)
            {
                CoreEncounter encounter = entry.Event;
                Assert.That(EncounterIllustrationCatalog.TryGetLocation(encounter, out var location), Is.True, encounter.Id + ": " + encounter.Name);
                Assert.That(location.ResourcePath, Does.StartWith("EncounterIllustrations/"), encounter.Name);
                currentPaths.Add(location.ResourcePath);
                string kind = location.ResourcePath.StartsWith(EncounterIllustrationCatalog.ReviewedResourceRoot, StringComparison.Ordinal) ? "reviewed" :
                    location.ResourcePath.StartsWith(EncounterIllustrationCatalog.BossSceneResourceRoot, StringComparison.Ordinal) ? "bossScene" :
                    location.Uv == new Rect(0, 0, 1, 1) ? "standalone" : "atlasCell";
                csv.AppendLine(string.Join(",", new[] { entry.Group, entry.Context, encounter.Id, encounter.Name, encounter.Description,
                    location.ResourcePath, location.Uv.x.ToString("R", CultureInfo.InvariantCulture), location.Uv.y.ToString("R", CultureInfo.InvariantCulture),
                    location.Uv.width.ToString("R", CultureInfo.InvariantCulture), location.Uv.height.ToString("R", CultureInfo.InvariantCulture), kind }.Select(Csv)));
            }
            HashSet<string> allPaths = ReferencedPaths();
            string output = TestContext.CurrentContext.WorkDirectory;
            // Unity's EditMode runner may omit NUnit's optional work directory.
            if (string.IsNullOrWhiteSpace(output))
                output = Path.Combine(Application.temporaryCachePath, "EncounterIllustrationAudit");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "illustration-audit.csv"), csv.ToString(), new UTF8Encoding(false));
            File.WriteAllLines(Path.Combine(output, "illustration-resource-paths.txt"), allPaths.OrderBy(path => path, StringComparer.Ordinal));
            File.WriteAllLines(Path.Combine(output, "illustration-coverage.txt"), entries.GroupBy(entry => entry.Group).Select(group =>
                group.Key + ": records=" + group.Count()).Concat(new[] {
                "CURRENT: records=" + entries.Count + ", ids=" + events.Select(e => e.Id).Distinct().Count() + ", resourcePaths=" + currentPaths.Count,
                "CURRENT_AND_COMPATIBILITY: resourcePaths=" + allPaths.Count,
                "REVIEWED: resourcePaths=" + allPaths.Count(path => path.StartsWith(EncounterIllustrationCatalog.ReviewedResourceRoot, StringComparison.Ordinal)),
                "The CSV records runtime bindings, not visual approval or installed-resource validation." }));
            Console.WriteLine("Current illustration resource paths: " + currentPaths.Count + "; current and compatibility: " + allPaths.Count);
        }

        private static string Csv(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

        [Test]
        public void ReviewedSeaScenesDoNotReplaceUnrelatedTreasureMirrorOrGuidedBuoy()
        {
            string[] ids = { "event_trade_debt_collector", "event_trade_merchant_distress", "event_moon_mirror_pool",
                "event_moon_spectral_debris", "event_moon_lunar_observatory", "moon_ghost", "event_moon_v2_borrowed_shadow",
                "event_moon_drowned_library", "event_moon_healing_spring" };
            CoreEncounter[] events = CurrentEvents().ToArray();
            foreach (string id in ids)
            {
                CoreEncounter encounter = events.Single(e => e.Id == id);
                Assert.That(EncounterIllustrationCatalog.TryGetLocation(encounter, out var location), Is.True, id);
                Assert.That(location.ResourcePath, Is.EqualTo(EncounterIllustrationCatalog.ReviewedResourceRoot + id), id);
                Assert.That(location.Uv, Is.EqualTo(new Rect(0, 0, 1, 1)), id);
            }
            EncounterIllustrationCatalog.TryGetLocation("moonlit_mirror_pool", out var treasureMirror);
            Assert.That(treasureMirror.ResourcePath, Is.EqualTo("EncounterIllustrations/legacy_moon_v3"));
            Assert.That(treasureMirror.Uv, Is.EqualTo(EncounterIllustrationCatalog.AtlasCellUv(0, 2)));
            EncounterIllustrationCatalog.TryGetLocation("tutorial_navigator_event", out var legacyAstrolabe, legacyTutorial: true);
            Assert.That(legacyAstrolabe.ResourcePath, Is.EqualTo(EncounterIllustrationCatalog.ReviewedResourceRoot + "event_moon_lunar_observatory"));
            EncounterIllustrationCatalog.TryGetLocation(Guided("NavigatorEvent"), out var guidedBuoy);
            Assert.That(guidedBuoy.ResourcePath, Is.EqualTo(EncounterIllustrationCatalog.ReviewedResourceRoot + "tutorial_tide_buoy"));
            AssertSameLocation("moonlit_cleanse", "event_moon_healing_spring");
            EncounterIllustrationCatalog.TryGetLocation("moonlit_echo_library", out var echoLibrary);
            Assert.That(echoLibrary.ResourcePath, Is.EqualTo("EncounterIllustrations/legacy_moon_v3"));
            Assert.That(echoLibrary.Uv, Is.EqualTo(EncounterIllustrationCatalog.AtlasCellUv(1, 2)));
        }

        [Test]
        public void ReviewedPaintingsMatchAllTwentyNineActualStoryScenes()
        {
            var expected = new Dictionary<string, string>(StringComparer.Ordinal) {
                { "沉船工坊主机舱", "core_weathered" }, { "商盟封存总仓", "core_gilded" }, { "三相月蚀圣所", "core_moonlit" },
                { "无法辨认的潮汐笔记", "tutorial_note" }, { "顺风捎来的讯息", "tutorial_message_ship" },
                { "水手发现漂流信筒", "tutorial_message_tube" }, { "潮汐暗标", "tutorial_tide_buoy" },
                { "潮痕秘藏", "reef_tide_cache" }, { "断桅求援", "reef_broken_mast" }, { "暗流推船", "reef_chart_current" },
                { "风暴残骸", "storm_debris" }, { "珊瑚采珠人", "pearl_divers" }, { "横海沉链", "weathered_chain" },
                { "龙骨封舱", "weathered_keel" }, { "锈锚拆船场", "weathered_rustyard" }, { "沉没测深浮标", "weathered_buoy" },
                { "裂缝沉箱", "weathered_caisson" }, { "失踪押运员", "gilded_witness" }, { "赈济交割船", "gilded_relief" },
                { "封缄拍卖艇", "gilded_auction" }, { "逆风商队", "gilded_convoy" }, { "海底账册浮标", "gilded_ledger_buoy" },
                { "昼辉祭坛", "moonlit_dawn" }, { "暮潮祭坛", "moonlit_dusk" }, { "夜幕祭坛", "moonlit_midnight" },
                { "无声唱诗礁", "moonlit_choir" }, { "沉睡守月像", "moonlit_guardian" }, { "不落月井", "moonlit_well" },
                { "沉月王庭 · 守藏舰", "treasure_guard_ship" }
            };
            var seen = new HashSet<string>();
            foreach (CoreEncounter encounter in CurrentEvents())
            {
                if (!expected.TryGetValue(encounter.Name, out string key)) continue;
                Assert.That(EncounterIllustrationCatalog.TryGetLocation(encounter, out var location), Is.True, encounter.Name);
                Assert.That(location.ResourcePath, Is.EqualTo(EncounterIllustrationCatalog.ReviewedResourceRoot + key), encounter.Name);
                Assert.That(location.Uv, Is.EqualTo(new Rect(0, 0, 1, 1)), encounter.Name);
                seen.Add(key);
            }
            CollectionAssert.AreEquivalent(expected.Values, seen, "Missing reviewed scenes: " + string.Join(", ", expected.Values.Except(seen)));
        }

        [Test]
        public void TreasureCoreVariantsKeepOldMapsOnTheOriginalStoneDoorScene()
        {
            var currentPaths = new HashSet<string>();
            foreach (CoreTreasureMapRank rank in CoreTreasureHuntCatalog.Ranks)
            {
                CoreEncounter current = CoreTreasureExpeditionCatalog.CreateEncounters(rank).Single(e => e.Id == CoreTreasureHuntCatalog.CoreEncounterId);
                CoreEncounter legacy = CoreTreasureHuntCatalog.CreateEncounters(rank).Single(e => e.Id == current.Id);
                Assert.That(EncounterIllustrationCatalog.TryGetLocation(current, out var currentLocation), Is.True);
                Assert.That(EncounterIllustrationCatalog.TryGetLocation(legacy, out var legacyLocation), Is.True);
                Assert.That(currentLocation.ResourcePath, Does.StartWith(EncounterIllustrationCatalog.ReviewedResourceRoot));
                currentPaths.Add(currentLocation.ResourcePath);
                Assert.That(legacy.Name, Is.EqualTo("秘藏核心"));
                Assert.That(legacyLocation.ResourcePath, Is.EqualTo(EncounterIllustrationCatalog.TreasureResourcePath));
                Assert.That(legacyLocation.Uv, Is.EqualTo(EncounterIllustrationCatalog.AtlasCellUv(2, 3)));
            }
            Assert.That(currentPaths.Count, Is.EqualTo(3));
        }

        [Test]
        public void ActualEncounterLoadingKeepsSharedIdVariantsDistinctEvenWhenArtIsMissing()
        {
            var requests = new List<string>();
            var constructor = typeof(LegacyDemoArtCatalog).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(Func<string, Texture2D>) }, null);
            var art = (LegacyDemoArtCatalog)constructor.Invoke(new object[] { new Func<string, Texture2D>(path => { requests.Add(path); return null; }) });
            CoreEncounter[] variants = CoreTreasureHuntCatalog.Ranks.Select(rank => CoreTreasureExpeditionCatalog.CreateEncounters(rank)
                .Single(e => e.Id == CoreTreasureHuntCatalog.CoreEncounterId)).Concat(new[] { Guided("RandomEvent", true), Guided("RandomEvent", false) }).ToArray();
            foreach (CoreEncounter encounter in variants)
            {
                // Use an unrelated selected route: the actual story controls its illustration.
                art.EncounterArt(2, encounter, out Texture2D texture, out Rect uv);
                Assert.That(texture, Is.Null);
                Assert.That(uv, Is.EqualTo(new Rect(0, 0, 1, 1)));
                art.EncounterArt(0, encounter, out _, out _);
            }
            CollectionAssert.AreEquivalent(new[] { "core_weathered", "core_gilded", "core_moonlit", "tutorial_message_ship", "tutorial_message_tube" }
                .Select(key => EncounterIllustrationCatalog.ReviewedResourceRoot + key), requests);
        }

        [Test]
        public void EveryExistingVoyageAndTreasureEventHasAnAuthoredNewIllustration()
        {
            foreach (string id in EventIds().Distinct())
            {
                Assert.That(EncounterIllustrationCatalog.TryGetLocation(id, out var location), Is.True, id);
                Assert.That(location.ResourcePath, Does.StartWith("EncounterIllustrations/"), id);
                Assert.That(location.Uv.width, Is.GreaterThan(0), id);
                Assert.That(location.Uv.height, Is.GreaterThan(0), id);
                Assert.That(location.Uv.xMin, Is.GreaterThanOrEqualTo(0), id);
                Assert.That(location.Uv.yMin, Is.GreaterThanOrEqualTo(0), id);
                Assert.That(location.Uv.xMax, Is.LessThanOrEqualTo(1.00001f), id);
                Assert.That(location.Uv.yMax, Is.LessThanOrEqualTo(1.00001f), id);
                if (id.Contains("_v2_"))
                {
                    string expected = id == "event_moon_v2_borrowed_shadow" ? EncounterIllustrationCatalog.ReviewedResourceRoot + id : "EncounterIllustrations/" + id + "_v1";
                    Assert.That(location.ResourcePath, Is.EqualTo(expected), id);
                    Assert.That(location.Uv, Is.EqualTo(new Rect(0, 0, 1, 1)), id);
                }
            }
        }

        [Test]
        public void EveryReferencedIllustrationIsInstalledWithStableUnityMetadata()
        {
            var paths = ReferencedPaths();
            var guids = new HashSet<string>(StringComparer.Ordinal);
            Assert.That(paths, Is.Not.Empty);
            Console.WriteLine("Checking installed metadata for " + paths.Count + " referenced illustration resources.");
            foreach (string path in paths)
            {
                string asset = Path.Combine(Application.dataPath, "_Project/Resources", path + ".png");
                Assert.That(File.Exists(asset), Is.True, path);
                Assert.That(File.Exists(asset + ".meta"), Is.True, path + " metadata");
                string guid = File.ReadLines(asset + ".meta").FirstOrDefault(line => line.StartsWith("guid: ", StringComparison.Ordinal));
                Assert.That(guid, Is.Not.Null, path);
                Assert.That(guids.Add(guid), Is.True, "Duplicate asset GUID: " + path);
            }
        }

        [Test]
        public void EveryIllustrationImportsAndLoadsAsTexture()
        {
            var paths = ReferencedPaths();
            foreach (string path in paths)
            {
                Texture2D texture = Resources.Load<Texture2D>(path);
                try
                {
                    Assert.That(texture, Is.Not.Null, path);
                    Assert.That(texture.width, Is.GreaterThanOrEqualTo(1024), path);
                    Assert.That(texture.height, Is.GreaterThanOrEqualTo(768), path);
                    Assert.That(texture.mipmapCount, Is.GreaterThan(1), path + " must remain filtered in the compact event panel");
                }
                finally { if (texture != null) Resources.UnloadAsset(texture); }
            }
        }

        [TestCase("event_reef_rotten_rations")]
        [TestCase("event_trade_food_spoilage")]
        [TestCase("event_moon_food_spoilage")]
        [TestCase("random_food_spoilage")]
        public void SpoiledProvisionsShareTheAuthoredHoldScene(string id)
        {
            Assert.That(EncounterIllustrationCatalog.TryGetLocation(id, out var location), Is.True);
            Assert.That(location.ResourcePath, Is.EqualTo("EncounterIllustrations/provisions_spoilage_v1"));
        }

        [Test]
        public void TreasureAliasesDepictTheActivityInsteadOfUnrelatedOldCells()
        {
            Assert.That(EncounterIllustrationCatalog.TryGetLocation("gilded_auction", out var auction), Is.True);
            Assert.That(auction.ResourcePath, Is.EqualTo(EncounterIllustrationCatalog.ReviewedResourceRoot + "gilded_auction"));
            AssertSameLocation("gilded_insurance", "event_reef_v2_shell_lime_barge");
            AssertSameLocation("weathered_winch", "event_reef_v2_backspin_winch");
            AssertSameLocation("moonlit_cleanse", "event_moon_healing_spring");
        }

        [Test]
        public void GuidedAndStandaloneTutorialsUseTheirOwnSharedIdMeanings()
        {
            EncounterIllustrationCatalog.TryGetLocation("tutorial_first_event", out var guided);
            EncounterIllustrationCatalog.TryGetLocation("tutorial_first_event", out var standalone, legacyTutorial: true);
            Assert.That(guided.ResourcePath, Is.EqualTo("EncounterIllustrations/legacy_reef_v3"));
            Assert.That(standalone.ResourcePath, Is.EqualTo("EncounterIllustrations/event_reef_drifting_crate_v1"));
            EncounterIllustrationCatalog.TryGetLocation("tutorial_optional_event", out var barrels, legacyTutorial: true);
            Assert.That(barrels.Uv, Is.EqualTo(EncounterIllustrationCatalog.AtlasCellUv(2, 1)));
            EncounterIllustrationCatalog.TryGetLocation(Guided("CrewRequirementEvent"), out var note);
            EncounterIllustrationCatalog.TryGetLocation("tutorial_crew_requirement_event", out var oldChart, legacyTutorial: true);
            Assert.That(note.ResourcePath, Is.EqualTo(EncounterIllustrationCatalog.ReviewedResourceRoot + "tutorial_note"));
            Assert.That(oldChart.ResourcePath, Is.EqualTo("EncounterIllustrations/event_trade_v2_ledger_forgery_v1"));
            EncounterIllustrationCatalog.TryGetLocation(Guided("NavigatorEvent"), out var buoy);
            EncounterIllustrationCatalog.TryGetLocation("tutorial_navigator_event", out var oldAstrolabe, legacyTutorial: true);
            Assert.That(buoy.ResourcePath, Is.EqualTo(EncounterIllustrationCatalog.ReviewedResourceRoot + "tutorial_tide_buoy"));
            Assert.That(oldAstrolabe.ResourcePath, Is.EqualTo(EncounterIllustrationCatalog.ReviewedResourceRoot + "event_moon_lunar_observatory"));
            Assert.That(oldAstrolabe.Uv, Is.EqualTo(new Rect(0, 0, 1, 1)));
        }

        [Test]
        public void SharedImagesLoadOnceAndReleaseOnlyBorrowedReferences()
        {
            var requests = new List<string>();
            var texture = new Texture2D(8, 8);
            var catalog = Create(path => { requests.Add(path); return texture; });
            try
            {
                Assert.That(requests, Is.Empty);
                catalog.TryLoad("event_reef_rotten_rations", out var first, out var uv);
                catalog.TryLoad("event_trade_food_spoilage", out var second, out _);
                Assert.That(second, Is.SameAs(first));
                Assert.That(uv, Is.EqualTo(new Rect(0, 0, 1, 1)));
                Assert.That(requests.Count, Is.EqualTo(1));
                Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Trilinear));
                Assert.That(texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                catalog.ReleaseLoadedTextures();
                Assert.That(texture != null, Is.True);
                catalog.TryLoad("event_moon_food_spoilage", out _, out _);
                Assert.That(requests.Count, Is.EqualTo(2));
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Test]
        public void MissingAndUnknownImagesNeverLoadAnUnrelatedHashedScene()
        {
            var requests = new List<string>();
            var catalog = Create(path => { requests.Add(path); return null; });
            Assert.That(catalog.TryLoad("event_reef_v2_message_gull", out var missing, out _), Is.True);
            Assert.That(missing, Is.Null);
            Assert.That(catalog.TryLoad("event_reef_v2_message_gull", out _, out _), Is.True);
            Assert.That(requests.Count, Is.EqualTo(1), "A missing resource is cached until explicit release.");
            Assert.That(catalog.TryLoad("unregistered_future_event", out _, out _), Is.False);
            Assert.That(catalog.TryLoad((string)null, out _, out _), Is.False);
            Assert.That(catalog.TryLoad((CoreEncounter)null, out _, out _), Is.False);
            Assert.That(requests.Count, Is.EqualTo(1));
        }

        private static void AssertSameLocation(string firstId, string secondId)
        {
            Assert.That(EncounterIllustrationCatalog.TryGetLocation(firstId, out var first), Is.True);
            Assert.That(EncounterIllustrationCatalog.TryGetLocation(secondId, out var second), Is.True);
            Assert.That(first.ResourcePath, Is.EqualTo(second.ResourcePath));
            Assert.That(first.Uv, Is.EqualTo(second.Uv));
        }

        private static EncounterIllustrationCatalog Create(Func<string, Texture2D> loader)
        {
            var constructor = typeof(EncounterIllustrationCatalog).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(Func<string, Texture2D>) }, null);
            return (EncounterIllustrationCatalog)constructor.Invoke(new object[] { loader });
        }
    }
}
