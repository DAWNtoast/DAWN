using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class BoardingBattleArtCatalogTests
    {
        private static readonly SeaTerrainType[] Environments =
        {
            SeaTerrainType.OpenWater, SeaTerrainType.Beach, SeaTerrainType.Strait,
            SeaTerrainType.Reef, SeaTerrainType.FogBank, SeaTerrainType.StormFront,
        };
        private static readonly string[] EnvironmentNames = { "open", "coast", "strait", "reef", "fog", "storm" };
        private static readonly NavigationTimeOfDay[] Times = { NavigationTimeOfDay.Day, NavigationTimeOfDay.Dusk, NavigationTimeOfDay.Night };
        private static readonly string[] TimeNames = { "day", "dusk", "night" };

        [Test]
        public void BoardingArt_AllEighteenEnvironmentsResolveDistinctInstalledPaintingsAndReuseTheirTexture()
        {
            var paths = new HashSet<string>();
            var textures = new HashSet<Texture2D>();
            for (int environment = 0; environment < Environments.Length; environment++)
                for (int time = 0; time < Times.Length; time++)
                {
                    string path = "BoardingBattle/Backgrounds/boarding_" + EnvironmentNames[environment] + "_" + TimeNames[time] + "_v1";
                    Assert.That(BoardingBattleArtCatalog.EnvironmentResourcePath(Environments[environment], Times[time]), Is.EqualTo(path));
                    Texture2D expected = Resources.Load<Texture2D>(path);
                    Assert.That(expected, Is.Not.Null, path);
                    Texture2D actual = BoardingBattleArtCatalog.ForEnvironment(Environments[environment], Times[time]);
                    Assert.That(actual, Is.SameAs(expected), path);
                    Assert.That(BoardingBattleArtCatalog.ForEnvironment(Environments[environment], Times[time]), Is.SameAs(actual));
                    Assert.That(actual.width, Is.GreaterThan(actual.height), path + " must be a landscape deck painting.");
                    paths.Add(path);
                    textures.Add(actual);
                }
            Assert.That(paths.Count, Is.EqualTo(18));
            Assert.That(textures.Count, Is.EqualTo(18), "Different environments/times must not silently borrow one painting.");
        }

        [TestCase(SeaTerrainType.Shoal)]
        [TestCase(SeaTerrainType.KelpField)]
        [TestCase(SeaTerrainType.Island)]
        [TestCase(SeaTerrainType.Beach)]
        public void BoardingArt_CoastalTerrainAliasesPreserveTheSelectedTime(SeaTerrainType terrain)
        {
            for (int i = 0; i < Times.Length; i++)
                Assert.That(BoardingBattleArtCatalog.EnvironmentResourcePath(terrain, Times[i]),
                    Is.EqualTo("BoardingBattle/Backgrounds/boarding_coast_" + TimeNames[i] + "_v1"));
        }

        [Test]
        public void BoardingArt_UnknownEnumsHaveADeterministicOpenDayFallback()
        {
            Assert.That(BoardingBattleArtCatalog.EnvironmentResourcePath((SeaTerrainType)(-1), (NavigationTimeOfDay)999),
                Is.EqualTo("BoardingBattle/Backgrounds/boarding_open_day_v1"));
        }

        [Test]
        public void BoardingArt_OrdinaryAndHostilePartiesHaveSeparateFullBodySources()
        {
            Texture2D player = Resources.Load<Texture2D>("BoardingBattle/Characters/boarding_player_party_v1");
            Texture2D enemy = Resources.Load<Texture2D>("BoardingBattle/Characters/boarding_enemy_party_v1");
            Assert.That(player, Is.Not.Null);
            Assert.That(enemy, Is.Not.Null);
            Assert.That(enemy, Is.Not.SameAs(player));
            Assert.That(BoardingBattleArtCatalog.ForParty(false), Is.SameAs(player));
            Assert.That(BoardingBattleArtCatalog.ForParty(true), Is.SameAs(enemy));
        }

        [TestCase("ordinary_raider", "party")]
        [TestCase("gold_customs", "imperial")]
        [TestCase("moon_ghost", "ghost")]
        [TestCase("boss_route_shattered_sail_reef", "route_reef")]
        [TestCase("boss_route_golden_tide_trade", "route_gold")]
        [TestCase("boss_route_moon_tide_ruins", "route_moon")]
        [TestCase("boss_route_shattered_sail_reef_tide", "ghost")]
        [TestCase("boss_route_golden_tide_trade_thunder", "imperial")]
        [TestCase("boss_route_moon_tide_ruins_arcane", "treasure")]
        [TestCase("treasure_boss_moon_vault", "treasure")]
        [TestCase("legendary_pirate_iron_oath", "iron_oath")]
        [TestCase("legendary_pirate_thunder_crown", "thunder_crown")]
        [TestCase("legendary_pirate_red_widow", "red_widow")]
        [TestCase("legendary_pirate_ember_ark", "ember_ark")]
        [TestCase("legendary_pirate_tide_requiem", "tide_requiem")]
        [TestCase("legendary_pirate_last_horizon", "treasure")]
        public void BoardingArt_EnemyIdentitySelectsItsOwnInstalledParty(string encounterId, string suffix)
        {
            CoreEncounter encounter = Encounter(encounterId);
            var battle = new CoreBattleState { Profile = CoreBattleBalance.Resolve(encounter, 2), EnemyName = "A display title is not an identity" };
            string path = "BoardingBattle/Characters/boarding_enemy_" + suffix + "_v1";
            Assert.That(BoardingBattleArtCatalog.EnemyResourcePath(battle, encounter, 2), Is.EqualTo(path));
            Texture2D expected = Resources.Load<Texture2D>(path);
            Assert.That(expected, Is.Not.Null, path);
            Assert.That(BoardingBattleArtCatalog.ForEnemy(battle, encounter, 2), Is.SameAs(expected));
            Assert.That(BoardingBattleArtCatalog.ForEnemy(battle, encounter, 2), Is.SameAs(expected));
        }

        [Test]
        public void BoardingArt_OnlyAnExactLegendaryProfileRecoversMissingIdentityAndTutorialStaysOrdinary()
        {
            const string generic = "BoardingBattle/Characters/boarding_enemy_party_v1";
            var unknown = new CoreBattleState
            {
                EnemyName = "铁誓不沉号",
                Profile = new CoreBattleProfile(CoreBossKind.Legendary, CoreBattleArchetype.Ironclad, 200, 20, 20, 10, 0, 20, 3f, 3f),
            };
            Assert.That(BoardingBattleArtCatalog.EnemyResourcePath(unknown, Encounter("legendary_pirate_unknown"), 2), Is.EqualTo(generic));
            Assert.That(BoardingBattleArtCatalog.EnemyResourcePath(null, null, 0), Is.EqualTo(generic));
            var known = new CoreBattleState { Profile = CoreLegendaryWarships.Find("legendary_pirate_iron_oath").Profile };
            Assert.That(BoardingBattleArtCatalog.EnemyResourcePath(known, null, 0),
                Is.EqualTo("BoardingBattle/Characters/boarding_enemy_iron_oath_v1"));
            var tutorial = new CoreBattleState { Profile = CoreGuidedTutorialContent.BossProfile };
            Assert.That(BoardingBattleArtCatalog.EnemyResourcePath(tutorial, Encounter(CoreGuidedTutorialContent.BossEventId), 2), Is.EqualTo(generic));
            Assert.That(BoardingBattleArtCatalog.EnemyResourcePath(tutorial, null, 2), Is.EqualTo(generic));
        }

        [TestCase(-1, "route_reef")]
        [TestCase(1, "route_gold")]
        [TestCase(99, "route_moon")]
        public void BoardingArt_MissingRouteEncounterFallsBackToTheActualRouteTier(int route, string suffix)
        {
            var battle = new CoreBattleState { Profile = CoreBattleBalance.FirstRouteBoss };
            Assert.That(BoardingBattleArtCatalog.EnemyResourcePath(battle, null, route),
                Is.EqualTo("BoardingBattle/Characters/boarding_enemy_" + suffix + "_v1"));
        }

        [TestCase("断锚港", "broken_anchor")]
        [TestCase("白贝港", "white_shell")]
        [TestCase("铜钟港", "bronze_bell")]
        [TestCase("盐冠港", "salt_crown")]
        [TestCase("暮潮港", "twilight_tide")]
        [TestCase("灰鸥港", "gray_gull")]
        public void BoardingArt_AllThirtySixIdentitiesUseTheirOwnFullBodyCellInSixSharedAtlases(string port, string slug)
        {
            string path = "BoardingBattle/Characters/boarding_crew_" + slug + "_v2";
            Texture2D expected = Resources.Load<Texture2D>(path);
            Assert.That(expected, Is.Not.Null, path);
            var cells = new List<Rect>();
            var ids = new HashSet<string>();
            for (int profession = 0; profession < 6; profession++)
            {
                CrewVariantDefinition crew = CrewNarrativeCatalog.ForPortAndProfession(port, profession);
                Assert.That(crew, Is.Not.Null);
                Assert.That(ids.Add(crew.Id), Is.True);
                Assert.That(BoardingBattleArtCatalog.ForCrew(crew), Is.SameAs(expected), crew.Id);
                Rect uv = BoardingBattleArtCatalog.CrewUv(crew);
                Assert.That(Mathf.FloorToInt(uv.center.x * 3f), Is.EqualTo(profession % 3),
                    "Navigator/carpenter/doctor and gunner/cook/diver retain their source identities.");
                Assert.That(Mathf.FloorToInt(uv.center.y * 2f), Is.EqualTo(1 - profession / 3));
                Assert.That(uv.width, Is.InRange(.15f, .34f));
                Assert.That(uv.height, Is.InRange(.35f, .5f));
                Assert.That(uv.xMin, Is.GreaterThan(0f));
                Assert.That(uv.yMin, Is.GreaterThan(0f));
                Assert.That(uv.xMax, Is.LessThan(1f));
                Assert.That(uv.yMax, Is.LessThan(1f));
                foreach (Rect previous in cells) Assert.That(uv.Overlaps(previous), Is.False, crew.Id);
                cells.Add(uv);
            }
            // Source-alpha coverage detects boots and props clipped by a grid
            // even when the renderer and its placement metadata agree.
            var readable = new Texture2D(2, 2);
            try
            {
                Assert.That(readable.LoadImage(System.IO.File.ReadAllBytes(UnityEditor.AssetDatabase.GetAssetPath(expected))), Is.True);
                Color32[] pixels = readable.GetPixels32();
                int[] coverage = new int[pixels.Length];
                foreach (Rect uv in cells)
                {
                    int left = Mathf.RoundToInt(uv.xMin * readable.width);
                    int right = Mathf.RoundToInt(uv.xMax * readable.width);
                    int bottom = Mathf.RoundToInt(uv.yMin * readable.height);
                    int top = Mathf.RoundToInt(uv.yMax * readable.height);
                    int visible = 0;
                    for (int y = bottom; y < top; y++)
                        for (int x = left; x < right; x++)
                        {
                            int pixel = y * readable.width + x;
                            coverage[pixel]++;
                            if (pixels[pixel].a >= 64) visible++;
                        }
                    Assert.That(visible, Is.GreaterThan(5000), "Each character needs its own complete visible body.");
                }
                for (int pixel = 0; pixel < pixels.Length; pixel++)
                    if (pixels[pixel].a >= 64 && coverage[pixel] != 1)
                        Assert.Fail(slug + " has a clipped or duplicated silhouette pixel at " + pixel % readable.width + "," + pixel / readable.width);
            }
            finally { Object.DestroyImmediate(readable); }
        }

        [Test]
        public void BoardingArt_MissingCastResourceFallsBackWithoutCroppingTheLegacyWholeBody()
        {
            var cache = (Dictionary<string, Texture2D>)typeof(BoardingBattleArtCatalog)
                .GetField("Cast", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            const string atlasPath = "BoardingBattle/Characters/boarding_crew_broken_anchor_v2";
            const string enemyPath = "BoardingBattle/Characters/boarding_enemy_imperial_v1";
            bool hadAtlas = cache.TryGetValue(atlasPath, out Texture2D oldAtlas);
            bool hadEnemy = cache.TryGetValue(enemyPath, out Texture2D oldEnemy);
            try
            {
                // Reproduce the catalog's normal negative-cache state without
                // deleting imported assets or changing the production loader.
                cache[atlasPath] = null;
                cache[enemyPath] = null;
                CrewVariantDefinition crew = CrewNarrativeCatalog.ForPortAndProfession("断锚港", 0);
                Texture2D fallback = Resources.Load<Texture2D>("PortArt/CrewFullBody/crew_navigator_evelyn_fullbody");
                Assert.That(fallback, Is.Not.Null);
                Assert.That(BoardingBattleArtCatalog.ForCrew(crew), Is.SameAs(fallback));
                Assert.That(BoardingBattleArtCatalog.CrewUv(crew), Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
                Assert.That(BoardingBattleArtCatalog.ForEnemy(null, Encounter("gold_customs"), 1), Is.SameAs(BoardingBattleArtCatalog.ForParty(true)));
            }
            finally
            {
                if (hadAtlas) cache[atlasPath] = oldAtlas; else cache.Remove(atlasPath);
                if (hadEnemy) cache[enemyPath] = oldEnemy; else cache.Remove(enemyPath);
            }
        }

        private static CoreEncounter Encounter(string id) => new CoreEncounter(id, "Identity fixture", "海战", "", true, false, 1, 100);

        [TestCase(1920, 1080)]
        [TestCase(1024, 768)]
        [TestCase(3440, 1440)]
        [TestCase(720, 1280)]
        public void BoardingArt_CoverCropFillsTheViewportWithoutStretchingOrReadingOutsideTexture(int width, int height)
        {
            var texture = new Texture2D(1600, 900);
            try
            {
                Rect uv = BoardingBattleLayout.CoverUv(texture, new Vector2(width, height));
                Assert.That(uv.xMin, Is.GreaterThanOrEqualTo(0f));
                Assert.That(uv.yMin, Is.GreaterThanOrEqualTo(0f));
                Assert.That(uv.xMax, Is.LessThanOrEqualTo(1f));
                Assert.That(uv.yMax, Is.LessThanOrEqualTo(1f));
                Assert.That(uv.center.x, Is.EqualTo(.5f));
                Assert.That(uv.yMin, Is.Zero, "The deck foreground must stay in frame when the painting is cropped.");
                Assert.That(texture.width * uv.width / (texture.height * uv.height), Is.EqualTo(width / (float)height).Within(.0001f));
            }
            finally { Object.DestroyImmediate(texture); }
        }
    }
}
