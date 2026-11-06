using System;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class OptimizedEncounterIllustrationTests
    {
        internal static readonly string[] LegacyClues = {
            "treasure_clue_bottle", "treasure_clue_whalebone", "treasure_clue_star",
            "treasure_clue_fogbell", "treasure_clue_twinmoon"
        };

        [TestCase("treasure_clue_bottle")]
        [TestCase("treasure_clue_whalebone")]
        [TestCase("treasure_clue_star")]
        [TestCase("treasure_clue_fogbell")]
        [TestCase("treasure_clue_twinmoon")]
        [TestCase("event_reef_shortcut")]
        [TestCase("event_reef_sunken_boat")]
        [TestCase("event_trade_floating_auction")]
        [TestCase("weathered_camp")]
        [TestCase("gilded_bonded_vault")]
        [TestCase("moonlit_pearl_moon")]
        public void ExactStoryLoadsItsOwnCompletePainting(string id)
        {
            Assert.That(EncounterIllustrationCatalog.TryGetLocation(id, out var location), Is.True);
            Assert.That(location.ResourcePath, Is.EqualTo(EncounterIllustrationCatalog.OptimizedResourceRoot + id));
            Assert.That(location.Uv, Is.EqualTo(new Rect(0, 0, 1, 1)));
            var catalog = new EncounterIllustrationCatalog();
            Assert.That(catalog.TryLoad(id, out var texture, out var uv), Is.True);
            try
            {
                Assert.That(texture, Is.Not.Null, location.ResourcePath);
                Assert.That(texture.width, Is.GreaterThanOrEqualTo(1024));
                Assert.That(texture.height, Is.GreaterThanOrEqualTo(768));
                Assert.That(texture.mipmapCount, Is.GreaterThan(1));
                Assert.That(texture.isReadable, Is.False);
                Assert.That(texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(uv, Is.EqualTo(location.Uv));
            }
            finally
            {
                catalog.ReleaseLoadedTextures();
                if (texture != null) Resources.UnloadAsset(texture);
            }
        }

        [TestCase("event_reef_favorable_current", 0, 1, 0)]
        [TestCase("event_reef_powder_wreck", 0, 2, 0)]
        [TestCase("gold_wreck", 0, 2, 0)]
        [TestCase("event_trade_spice_auction", 1, 1, 1)]
        [TestCase("event_reef_hidden_repair_dock", 0, 3, 3)]
        public void FormerAtlasAliasesKeepTheirDifferentStory(string id, int route, int column, int row)
        {
            Assert.That(EncounterIllustrationCatalog.TryGetLocation(id, out var location), Is.True);
            Assert.That(location.ResourcePath, Is.EqualTo(EncounterIllustrationCatalog.RouteResourcePath(route)));
            Assert.That(location.Uv, Is.EqualTo(EncounterIllustrationCatalog.AtlasCellUv(column, row)));
        }

        [TestCase("gilded_auction")]
        [TestCase("moonlit_dawn")]
        [TestCase("moonlit_dusk")]
        [TestCase("moonlit_midnight")]
        public void ExistingReviewedStoriesKeepTheirOwnPaintings(string id)
        {
            Assert.That(EncounterIllustrationCatalog.TryGetLocation(id, out var location), Is.True);
            Assert.That(location.ResourcePath, Is.EqualTo(EncounterIllustrationCatalog.ReviewedResourceRoot + id));
            Assert.That(location.Uv, Is.EqualTo(new Rect(0, 0, 1, 1)));
        }

        [Test]
        public void LegacyClueOverridesDoNotIntroduceOldStoriesIntoCurrentExpeditions()
        {
            foreach (CoreTreasureMapRank rank in CoreTreasureHuntCatalog.Ranks)
            {
                CoreEncounter[] legacy = CoreTreasureHuntCatalog.CreateEncounters(rank)
                    .Where(encounter => LegacyClues.Contains(encounter.Id)).ToArray();
                CollectionAssert.AreEquivalent(LegacyClues, legacy.Select(encounter => encounter.Id));
                foreach (CoreEncounter encounter in legacy)
                {
                    Assert.That(EncounterIllustrationCatalog.TryGetLocation(encounter, out var location), Is.True);
                    Assert.That(location.ResourcePath, Is.EqualTo(EncounterIllustrationCatalog.OptimizedResourceRoot + encounter.Id));
                }
                Assert.That(CoreTreasureExpeditionCatalog.CreateEncounters(rank)
                    .Any(encounter => LegacyClues.Contains(encounter.Id)), Is.False, rank.Id);
            }
            Assert.That(EncounterIllustrationCatalog.TryGetLocation("treasure_clue_bottle_future", out _), Is.False);
            Assert.That(EncounterIllustrationCatalog.TryGetLocation("EVENT_REEF_SHORTCUT", out _), Is.False);
        }
    }
}
