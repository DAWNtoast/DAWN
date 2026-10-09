using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class AchievementIconCatalogTests
    {
        [Test]
        public void AllFiftyDisplayedAchievements_HaveDistinctNonOverlappingArtInThreeSharedAtlases()
        {
            string[] canonicalIds = AchievementDefinitionCompatibilityTests.MetadataBefore.Select(row => row[0]).ToArray();
            Assert.That(canonicalIds.Length, Is.EqualTo(50));
            Assert.That(AchievementCatalog.All.Select(item => item.Id), Is.EqualTo(canonicalIds));
            Texture2D[] textures =
            {
                Resources.Load<Texture2D>(AchievementIconCatalog.AtlasAResourcePath),
                Resources.Load<Texture2D>(AchievementIconCatalog.AtlasBResourcePath),
                Resources.Load<Texture2D>(AchievementIconCatalog.AtlasCResourcePath),
            };
            Assert.That(textures.All(texture => texture != null), Is.True, "All three achievement atlases must be installed.");
            Assert.That(textures.Distinct().Count(), Is.EqualTo(3));
            var cells = new[] { new HashSet<Rect>(), new HashSet<Rect>(), new HashSet<Rect>() };
            for (int index = 0; index < canonicalIds.Length; index++)
            {
                string id = canonicalIds[index];
                int group = index / 20;
                ItemIconArt art = AchievementIconCatalog.ForId(id);
                Assert.That(art.IsAvailable, Is.True, id);
                Assert.That(art.Texture, Is.SameAs(textures[group]), id);
                Assert.That(art.Uv.xMin, Is.GreaterThanOrEqualTo(0f), id);
                Assert.That(art.Uv.yMin, Is.GreaterThanOrEqualTo(0f), id);
                Assert.That(art.Uv.xMax, Is.LessThanOrEqualTo(1f), id);
                Assert.That(art.Uv.yMax, Is.LessThanOrEqualTo(1f), id);
                Assert.That(art.Uv.width, Is.GreaterThan(0f), id);
                Assert.That(art.Uv.height, Is.GreaterThan(0f), id);
                foreach (Rect previous in cells[group])
                    Assert.That(art.Uv.Overlaps(previous), Is.False, id + " overlaps another achievement.");
                Assert.That(cells[group].Add(art.Uv), Is.True, id + " reuses another achievement's cell.");
                ItemIconArt repeated = AchievementIconCatalog.ForId(id);
                Assert.That(repeated.Texture, Is.SameAs(art.Texture), id);
                Assert.That(repeated.Uv, Is.EqualTo(art.Uv), id);
            }
            Assert.That(cells.Select(group => group.Count), Is.EqualTo(new[] { 20, 20, 10 }));
        }

        [TestCase("first_sail", "tutorial_departure")]
        [TestCase("stranger_port", "port_gray_gull")]
        [TestCase("sea_trade", "gold_600")]
        [TestCase("golden_captain", "gold_900")]
        public void ExplicitCoreLegacyAliases_ResolveTheSameUiAchievementArt(string coreId, string uiId)
        {
            CoreAchievementDefinition definition = CoreAchievementCatalog.Find(coreId);
            Assert.That(definition, Is.SameAs(CoreAchievementCatalog.Find(uiId)));
            Assert.That(definition.Id, Is.EqualTo(uiId));
            Assert.That(definition.LegacyId, Is.EqualTo(coreId));
            ItemIconArt canonical = AchievementIconCatalog.ForId(uiId);
            ItemIconArt alias = AchievementIconCatalog.ForId(coreId);
            Assert.That(canonical.IsAvailable, Is.True, uiId);
            Assert.That(alias.IsAvailable, Is.True, coreId);
            Assert.That(alias.Texture, Is.SameAs(canonical.Texture), coreId);
            Assert.That(alias.Uv, Is.EqualTo(canonical.Uv), coreId);
        }

        [Test]
        public void UnknownIdsAndFortyFiveRetiredPrototypeIds_DoNotBorrowAnyOtherAchievementArt()
        {
            foreach (string id in new[] { null, "", " ", "unknown", "tutorial_departure_typo", "FIRST_SAIL" })
                Assert.That(AchievementIconCatalog.ForId(id).IsAvailable, Is.False, id);
            string[] retired = AchievementDefinitionCompatibilityTests.RetiredPrototypeIds;
            Assert.That(retired.Length, Is.EqualTo(45));
            Assert.That(retired.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(45));
            foreach (string id in retired)
            {
                Assert.That(CoreAchievementCatalog.Find(id), Is.Null, id);
                Assert.That(CoreAchievementCatalog.IsKnown(id), Is.False, id);
                Assert.That(AchievementIconCatalog.ForId(id).IsAvailable, Is.False, id);
            }
            Assert.That(AchievementIconCatalog.ForId("card_upgrade").IsAvailable, Is.True,
                "The retained UI achievement is distinct from the retired five-upgrade prototype semantics.");
        }

        [Test]
        public void Atlases_LoadOnlyWhenTheirOwnAchievementGroupIsRequested_AndResetBetweenSubsystems()
        {
            ResetCache();
            AssertCache(false, false, false);
            Assert.That(AchievementIconCatalog.ForId("unknown").IsAvailable, Is.False);
            AssertCache(false, false, false);
            Assert.That(AchievementIconCatalog.ForId("tutorial_departure").IsAvailable, Is.True);
            AssertCache(true, false, false);
            Assert.That(AchievementIconCatalog.ForId("invest_all_ports").IsAvailable, Is.True);
            AssertCache(true, true, false);
            Assert.That(AchievementIconCatalog.ForId("warehouse_display_six").IsAvailable, Is.True);
            AssertCache(true, true, true);
            ResetCache();
            AssertCache(false, false, false);
        }

        private static void ResetCache() => typeof(AchievementIconCatalog)
            .GetMethod("ResetCache", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

        private static void AssertCache(params bool[] expected)
        {
            string[] suffixes = { "A", "B", "C" };
            for (int index = 0; index < suffixes.Length; index++)
            {
                FieldInfo texture = typeof(AchievementIconCatalog).GetField("atlas" + suffixes[index], BindingFlags.Static | BindingFlags.NonPublic);
                FieldInfo loaded = typeof(AchievementIconCatalog).GetField("atlas" + suffixes[index] + "Loaded", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(texture.GetValue(null) != null, Is.EqualTo(expected[index]), "Atlas " + suffixes[index]);
                Assert.That(loaded.GetValue(null), Is.EqualTo(expected[index]), "Atlas " + suffixes[index] + " load state");
            }
        }
    }
}
