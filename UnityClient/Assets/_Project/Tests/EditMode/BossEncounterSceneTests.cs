using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class BossEncounterSceneTests
    {
        private static readonly object[][] Cases = {
            new object[] { "boss_route_shattered_sail_reef", "boss_fire" },
            new object[] { "boss_route_shattered_sail_reef_tide", "boss_tide" },
            new object[] { "boss_route_golden_tide_trade", "boss_blood" },
            new object[] { "boss_route_golden_tide_trade_thunder", "boss_thunder" },
            new object[] { "boss_route_moon_tide_ruins", "boss_curse" },
            new object[] { "boss_route_moon_tide_ruins_arcane", "boss_arcane" },
            new object[] { "legendary_pirate_ember_ark", "legendary_fire" },
            new object[] { "legendary_pirate_tide_requiem", "legendary_tide" },
            new object[] { "legendary_pirate_red_widow", "legendary_blood" },
            new object[] { "legendary_pirate_thunder_crown", "legendary_thunder" },
            new object[] { "legendary_pirate_iron_oath", "legendary_curse" },
            new object[] { "legendary_pirate_last_horizon", "legendary_arcane" }
        };

        [Test]
        public void AllSixGuardiansAndSixLegendsHaveDistinctAuthoredSceneBindings()
        {
            string[] activeIds = CoreRouteBossCatalog.All.Select(boss => boss.Id)
                .Concat(CoreLegendaryWarships.All.Select(ship => ship.Id)).ToArray();
            CollectionAssert.AreEquivalent(activeIds, Cases.Select(item => (string)item[0]));
            var paths = new HashSet<string>();
            foreach (object[] item in Cases)
            {
                string id = (string)item[0], key = (string)item[1];
                Assert.That(EncounterIllustrationCatalog.TryGetLocation(id, out var location), Is.True, id);
                Assert.That(location.ResourcePath, Is.EqualTo("EncounterIllustrations/BossScenes/scene_" + key), id);
                Assert.That(location.Uv, Is.EqualTo(new Rect(0, 0, 1, 1)), id);
                Assert.That(paths.Add(location.ResourcePath), Is.True, id);
                Assert.That(SpecialWarshipArtCatalog.ResourcePath(id), Is.EqualTo("ShipArtV2/Special/" + key), id);
            }
            Assert.That(paths.Count, Is.EqualTo(12));
        }

        [TestCaseSource(nameof(Cases))]
        public void EventAndMapResolverPrefersSceneOverAlreadyLoadedTransparentShip(string id, string key)
        {
            Assert.That(SpecialWarshipArtCatalog.TryLoad(id, out Texture2D ship, out _), Is.True, id);
            var scene = new Texture2D(12, 8) { name = "scene_" + key };
            var requests = new List<string>();
            GameObject host = CreateResolver(path => { requests.Add(path); return scene; }, out var controller);
            try
            {
                Assert.That(Resolve(controller, id, out Rect uv), Is.SameAs(scene));
                CoreEncounter encounter = CoreRouteBossCatalog.Find(id)?.CreateEncounter() ??
                    CoreLegendaryWarships.Find(id)?.CreateEncounter();
                Assert.That(encounter, Is.Not.Null, id);
                Assert.That(Resolve(controller, encounter, out Rect actualUv), Is.SameAs(scene));
                Assert.That(Resolve(controller, encounter, out _), Is.SameAs(scene));
                Assert.That(scene, Is.Not.SameAs(ship));
                Assert.That(SpecialWarshipArtCatalog.IsSpecialTexture(scene), Is.False,
                    "A complete sea scene must not receive the transparent hull's layout or water effects.");
                Assert.That(uv, Is.EqualTo(new Rect(0, 0, 1, 1)));
                Assert.That(actualUv, Is.EqualTo(uv));
                CollectionAssert.AreEqual(new[] { "EncounterIllustrations/BossScenes/scene_" + key }, requests);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(scene);
            }
        }

        [TestCaseSource(nameof(Cases))]
        public void MissingSceneDoesNotSilentlyBecomeATransparentShip(string id, string key)
        {
            Assert.That(SpecialWarshipArtCatalog.TryLoad(id, out _, out _), Is.True, id);
            var requests = new List<string>();
            GameObject host = CreateResolver(path => { requests.Add(path); return null; }, out var controller);
            try
            {
                Assert.That(Resolve(controller, id, out _), Is.Null);
                CoreEncounter encounter = CoreRouteBossCatalog.Find(id)?.CreateEncounter() ??
                    CoreLegendaryWarships.Find(id)?.CreateEncounter();
                Assert.That(encounter, Is.Not.Null, id);
                Assert.That(Resolve(controller, encounter, out _), Is.Null);
                Assert.That(Resolve(controller, encounter, out _), Is.Null);
                CollectionAssert.AreEqual(new[] { "EncounterIllustrations/BossScenes/scene_" + key }, requests);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void ActualEncounterResolverKeepsReviewedCoreAndMessageVariantsDistinct()
        {
            var variants = new List<KeyValuePair<CoreEncounter, string>>();
            foreach (string rank in new[] { "weathered", "gilded", "moonlit" })
            {
                CoreEncounter core = CoreTreasureExpeditionCatalog.CreateEncounters(CoreTreasureHuntCatalog.FindRank(rank))
                    .Single(encounter => encounter.Id == CoreTreasureHuntCatalog.CoreEncounterId);
                variants.Add(new KeyValuePair<CoreEncounter, string>(core, "core_" + rank));
            }
            MethodInfo randomFactory = typeof(CoreGuidedTutorialContent).GetMethod("RandomEvent",
                BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(bool) }, null);
            Assert.That(randomFactory, Is.Not.Null);
            variants.Add(new KeyValuePair<CoreEncounter, string>(
                (CoreEncounter)randomFactory.Invoke(null, new object[] { true }), "tutorial_message_ship"));
            variants.Add(new KeyValuePair<CoreEncounter, string>(
                (CoreEncounter)randomFactory.Invoke(null, new object[] { false }), "tutorial_message_tube"));

            const string root = "EncounterIllustrations/Reviewed20261023/";
            var scenes = variants.ToDictionary(item => root + item.Value,
                item => new Texture2D(12, 8) { name = item.Value });
            var requests = new List<string>();
            GameObject host = CreateResolver(path => {
                requests.Add(path);
                return scenes.TryGetValue(path, out Texture2D scene) ? scene : null;
            }, out var controller);
            try
            {
                // Resolve all five through one catalog so an ID-only cache
                // cannot collapse the three cores or the two message scenes.
                foreach (KeyValuePair<CoreEncounter, string> variant in variants)
                {
                    Texture2D expected = scenes[root + variant.Value];
                    Assert.That(Resolve(controller, variant.Key, out Rect uv), Is.SameAs(expected), variant.Value);
                    Assert.That(uv, Is.EqualTo(new Rect(0, 0, 1, 1)), variant.Value);
                    Assert.That(Resolve(controller, variant.Key, out Rect cachedUv), Is.SameAs(expected), variant.Value);
                    Assert.That(cachedUv, Is.EqualTo(uv), variant.Value);
                }
                CollectionAssert.AreEqual(variants.Select(item => root + item.Value), requests);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                foreach (Texture2D scene in scenes.Values) UnityEngine.Object.DestroyImmediate(scene);
            }
        }

        [Test, Category("BossSceneResources")]
        public void EveryBossSceneImportsAndLoadsSeparatelyFromItsTransparentShip()
        {
            var catalog = new EncounterIllustrationCatalog();
            var loaded = new HashSet<Texture2D>();
            try
            {
                foreach (object[] item in Cases)
                {
                    string id = (string)item[0], key = (string)item[1];
                    Assert.That(catalog.TryLoad(id, out Texture2D scene, out Rect uv), Is.True, id);
                    Assert.That(scene, Is.Not.Null, id);
                    loaded.Add(scene);
                    Assert.That(scene.name, Is.EqualTo("scene_" + key), id);
                    Assert.That(scene.width, Is.GreaterThanOrEqualTo(1024), id);
                    Assert.That(scene.height, Is.GreaterThanOrEqualTo(768), id);
                    Assert.That(uv, Is.EqualTo(new Rect(0, 0, 1, 1)), id);
                    Assert.That(SpecialWarshipArtCatalog.IsSpecialTexture(scene), Is.False, id);
                    Assert.That(SpecialWarshipArtCatalog.TryLoad(id, out Texture2D ship, out _), Is.True, id);
                    Assert.That(scene, Is.Not.SameAs(ship), id);
                }
                Assert.That(loaded.Count, Is.EqualTo(12));
            }
            finally
            {
                catalog.ReleaseLoadedTextures();
                foreach (Texture2D texture in loaded) if (texture != null) Resources.UnloadAsset(texture);
            }
        }

        private static GameObject CreateResolver(Func<string, Texture2D> loader, out CoreGameplayDemoController controller)
        {
            var host = new GameObject("Boss Scene Resolver Test");
            host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            var constructor = typeof(LegacyDemoArtCatalog).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(Func<string, Texture2D>) }, null);
            var art = (LegacyDemoArtCatalog)constructor.Invoke(new object[] { loader });
            typeof(CoreGameplayDemoController).GetField("art", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, art);
            typeof(CoreGameplayDemoController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, new CoreVoyageSession());
            return host;
        }

        private static Texture2D Resolve(CoreGameplayDemoController controller, string id, out Rect uv)
        {
            object[] arguments = { id, null, null };
            typeof(CoreGameplayDemoController).GetMethod("ResolveEncounterIllustration", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(string), typeof(Texture2D).MakeByRefType(), typeof(Rect).MakeByRefType() }, null)
                .Invoke(controller, arguments);
            uv = (Rect)arguments[2];
            return (Texture2D)arguments[1];
        }

        private static Texture2D Resolve(CoreGameplayDemoController controller, CoreEncounter encounter, out Rect uv)
        {
            object[] arguments = { encounter, null, null };
            typeof(CoreGameplayDemoController).GetMethod("ResolveEncounterIllustration", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(CoreEncounter), typeof(Texture2D).MakeByRefType(), typeof(Rect).MakeByRefType() }, null)
                .Invoke(controller, arguments);
            uv = (Rect)arguments[2];
            return (Texture2D)arguments[1];
        }
    }
}
