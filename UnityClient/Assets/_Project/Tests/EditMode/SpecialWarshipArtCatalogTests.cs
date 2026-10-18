using System;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Core.Tests
{
    public sealed class SpecialWarshipArtCatalogTests
    {
        [Test]
        public void EveryGuardianAndLegendHasADistinctCompleteInstalledPainting()
        {
            string[] ids = CoreRouteBossCatalog.All.Select(boss => boss.Id)
                .Concat(CoreLegendaryWarships.All.Select(ship => ship.Id)).ToArray();
            Assert.That(ids.Length, Is.EqualTo(12));
            Assert.That(ids.Select(SpecialWarshipArtCatalog.ResourcePath).Distinct().Count(), Is.EqualTo(12));
            foreach (string id in ids)
            {
                Assert.That(SpecialWarshipArtCatalog.TryLoad(id, out Texture2D texture, out Rect uv), Is.True, id);
                Assert.That(texture, Is.Not.Null, id);
                Assert.That(texture.width, Is.EqualTo(1536), id);
                Assert.That(texture.height, Is.EqualTo(1024), id);
                Assert.That(uv, Is.EqualTo(new Rect(0, 0, 1, 1)), "Never sample a special ship as a player atlas cell.");
                Assert.That(SpecialWarshipArtCatalog.IsSpecialTexture(texture), Is.True);
            }
            Assert.That(SpecialWarshipArtCatalog.ResourcePath("ordinary_ambush"), Is.Empty);
            Assert.That(SpecialWarshipArtCatalog.TryLoad("ordinary_ambush", out _, out _), Is.False);
        }

        [Test]
        public void ContactMetadataTracksFullAndMirroredSourceCoordinates()
        {
            var texture = new Texture2D(3, 2) { name = "boss_arcane" };
            var go = new GameObject("Special Waterline Test", typeof(RectTransform), typeof(RawImage));
            try
            {
                RawImage image = go.GetComponent<RawImage>();
                image.texture = texture;
                image.rectTransform.sizeDelta = new Vector2(300, 200);
                SpecialWarshipArtCatalog.ConfigureWaterline("boss_route_moon_tide_ruins_arcane", new Vector2(.3f, .2f), .7f);
                image.uvRect = SpecialWarshipArtCatalog.FullUv;
                Assert.That(SpecialWarshipArtCatalog.HullContactLocal(image).x, Is.EqualTo(-60).Within(.001));
                Assert.That(SpecialWarshipArtCatalog.HullContactLocal(image).y, Is.EqualTo(-60).Within(.001));
                image.uvRect = new Rect(1, 0, -1, 1);
                Assert.That(SpecialWarshipArtCatalog.HullContactLocal(image).x, Is.EqualTo(60).Within(.001));
                Assert.That(SpecialWarshipArtCatalog.HullWidthRatio(texture), Is.EqualTo(.7f));
            }
            finally
            {
                SpecialWarshipArtCatalog.ClearWaterlineOverrides();
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void SixBattleStoriesMatchTheSixWarshipsAndBothMoonlitLegends()
        {
            Assert.That(CoreTreasureStoryCatalog.Battles.Count, Is.EqualTo(6));
            foreach (CoreLegendaryWarship ship in CoreLegendaryWarships.All)
            {
                TreasureBattleStory story = CoreTreasureStoryCatalog.FindBattle(ship.Id);
                Assert.That(story, Is.Not.Null, ship.Id);
                Assert.That(story.ShipName, Is.EqualTo(ship.Name));
                Assert.That(story.Pages.Count, Is.EqualTo(2));
                Assert.That(story.Pages.All(page => page.Length > 20), Is.True);
            }
            foreach (TreasureStoryChapter chapter in CoreTreasureStoryCatalog.Chapters)
                CollectionAssert.AreEquivalent(CoreLegendaryWarships.CreateTreasureEncounters(chapter.RankId).Select(item => item.Id), chapter.LegendaryIds);
        }

        [Test]
        public void DepartureListsBothCandidatesAndSixthLegendHasAnExistingTrack()
        {
            foreach (CoreRouteDefinition route in CoreVoyageSession.Routes)
            {
                string label = SpecialWarshipArtCatalog.RouteCandidatesLabel(route);
                foreach (CoreRouteBoss boss in CoreRouteBossCatalog.ForRoute(route.Id)) Assert.That(label, Does.Contain(boss.Name));
                Assert.That(label, Does.Contain("随机一位"));
            }
            Assert.That(CoreGameplayDemoController.LegendaryBattleMusicKey("legendary_pirate_last_horizon"),
                Is.EqualTo(CoreGameplayDemoController.LegendaryBattleMusicKey("legendary_pirate_tide_requiem")));
        }

        [Test]
        public void LookoutObservationsCarryEncounterIdentityWithoutGuessingFromTheName()
        {
            var session = new CoreVoyageSession(); session.StartVoyage();
            CoreMapNode boss = session.Map.Nodes.Single(node => node.IsBoss);
            boss.Discovered = true;
            var origin = new VoyagePoint(boss.Position.X, boss.Position.Y - 2f);
            var observation = VoyageLookoutProjector.Project(session.Map, origin, SailingDirection.North, 1000)
                .Single(item => item.Key == "event-" + boss.Id);
            Assert.That(observation.EncounterId, Is.EqualTo(boss.Encounter.Id));
            Assert.That(SpecialWarshipArtCatalog.ResourcePath(observation.EncounterId), Is.Not.Empty);
        }
    }
}
