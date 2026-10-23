using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class NavalTacticalArtCatalogTests
    {
        [Test]
        public void EveryPlayableHull_HasItsOwnImportedTacticalSprite()
        {
            var sprites = new HashSet<Sprite>();
            foreach (CoreShipDefinition ship in CoreVoyageSession.Ships)
            {
                string path = NavalTacticalArtCatalog.PlayerShipResourcePath(ship.Id);
                Assert.That(path, Is.EqualTo("NavalTactics/Ships/" + ship.Id + "_v2"));
                Sprite sprite = Resources.Load<Sprite>(path);
                Assert.That(sprite, Is.Not.Null, path);
                Assert.That(sprite.texture.mipmapCount, Is.GreaterThan(1), "Tactical hulls need filtered minification: " + path);
                Assert.That(sprites.Add(sprite), Is.True, "Different hulls must not silently share the old marker: " + ship.Id);
            }
            Assert.That(sprites.Count, Is.EqualTo(12));
        }

        [Test]
        public void NamedBossesAndLegends_KeepSeparateSchoolSprites_WithOrWithoutEncounterPointer()
        {
            var sprites = new HashSet<Sprite>();
            foreach (CoreRouteBoss boss in CoreRouteBossCatalog.All)
                AssertIdentity(boss.CreateEncounter(), boss.Profile, "boss_" + boss.SchoolId, sprites);
            foreach (CoreLegendaryWarship ship in CoreLegendaryWarships.All)
                AssertIdentity(ship.CreateEncounter(), ship.Profile, "legendary_" + ship.SchoolId, sprites);
            Assert.That(sprites.Count, Is.EqualTo(12));
            Assert.That(NavalTacticalArtCatalog.EnemyShipResourcePath(null,
                CoreLegendaryWarships.Find("legendary_pirate_iron_oath").CreateEncounter(), 0),
                Does.EndWith("legendary_curse_v2"), "Iron Oath's school is curse, independent of its Ironclad combat archetype.");
        }

        [TestCase("gold_customs", "patrol")]
        [TestCase("moon_ghost", "occult")]
        [TestCase("event_moon_open_water_ambush", "occult")]
        [TestCase("event_moon_v2_moon_taxcollector", "occult")]
        [TestCase("treasure_ghost_fleet", "occult")]
        [TestCase("treasure_rival_sloop", "sloop")]
        [TestCase("event_reef_open_water_ambush", "sloop")]
        [TestCase("event_trade_open_water_ambush", "privateer")]
        [TestCase("event_trade_merchant_distress", "privateer")]
        [TestCase("event_trade_v2_powder_barge", "privateer")]
        [TestCase("event_reef_v2_whaler_signal", "privateer")]
        [TestCase("event_reef_coral_ambush", "privateer")]
        [TestCase("random_ambush", "privateer")]
        [TestCase("random_ambush_warning", "privateer")]
        [TestCase(CoreGuidedTutorialContent.BossEventId, "privateer")]
        [TestCase(CoreBattleBalance.TreasureBossId, "relic")]
        public void AuthoredOrdinaryIdentity_WinsOverAnUnrelatedFallbackProfile(string identity, string expected)
        {
            var encounter = new CoreEncounter(identity, "Test", "Test", "", false, false, 0, 0);
            Assert.That(NavalTacticalArtCatalog.EnemyShipResourcePath(
                new CoreBattleState { Profile = CoreBattleBalance.FirstRouteBoss }, encounter, 2),
                Is.EqualTo("NavalTactics/Ships/" + expected + "_v2"));
        }

        [Test]
        public void UnknownLegendaryProfile_DoesNotImpersonateANamedLegend()
        {
            var profile = new CoreBattleProfile(CoreBossKind.Legendary, CoreBattleArchetype.Ironclad,
                100, 1, 1, 1, 0, 1, 1, 1);
            Assert.That(NavalTacticalArtCatalog.EnemyShipResourcePath(new CoreBattleState { Profile = profile }, null, 0),
                Is.EqualTo("NavalTactics/Ships/gunship_v2"));
        }

        [Test]
        public void EveryTerrain_ResolvesAnImportedClampedSea_WithSixDistinctPaintings()
        {
            var textures = new HashSet<Texture2D>();
            foreach (SeaTerrainType terrain in Enum.GetValues(typeof(SeaTerrainType)))
            {
                string path = NavalTacticalArtCatalog.SeaResourcePath(terrain);
                Texture2D texture = Resources.Load<Texture2D>(path);
                Assert.That(texture, Is.Not.Null, path);
                Assert.That(NavalTacticalArtCatalog.ForSea(terrain), Is.SameAs(texture));
                Assert.That(texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), path);
                textures.Add(texture);
            }
            Assert.That(textures.Count, Is.EqualTo(6));
            Assert.That(NavalTacticalArtCatalog.SeaResourcePath(SeaTerrainType.Beach), Does.EndWith("coast_v2"));
            Assert.That(NavalTacticalArtCatalog.SeaResourcePath(SeaTerrainType.StormFront), Does.EndWith("storm_v2"));
        }

        private static void AssertIdentity(CoreEncounter encounter, CoreBattleProfile profile, string key, HashSet<Sprite> sprites)
        {
            string path = "NavalTactics/Ships/" + key + "_v2";
            Assert.That(NavalTacticalArtCatalog.EnemyShipResourcePath(null, encounter, 0), Is.EqualTo(path));
            Assert.That(NavalTacticalArtCatalog.EnemyShipResourcePath(new CoreBattleState { Profile = profile }, null, 0), Is.EqualTo(path));
            Sprite sprite = Resources.Load<Sprite>(path);
            Assert.That(sprite, Is.Not.Null, path);
            Assert.That(sprite.texture.mipmapCount, Is.GreaterThan(1), "Tactical hulls need filtered minification: " + path);
            Assert.That(sprites.Add(sprite), Is.True, path);
        }
    }
}
