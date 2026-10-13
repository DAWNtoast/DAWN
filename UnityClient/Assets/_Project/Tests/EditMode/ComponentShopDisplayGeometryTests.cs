using System;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class ComponentShopDisplayGeometryTests
    {
        [Test]
        public void All125Components_RetainNativeAspectAndClearTheirSigns()
        {
            var catalog = new ComponentArtCatalog();
            Assert.That(catalog.Entries.Count, Is.EqualTo(125));
            foreach (var entry in catalog.Entries)
            {
                var geometry = ComponentShopDisplayGeometry.For(entry);
                // Independent artwork includes narrow instruments and long beams.
                // A fixed minimum on both axes would require stretching these shapes.
                Assert.That(geometry.ModelSize.x, Is.GreaterThan(0f).And.LessThanOrEqualTo(301f), entry.Id);
                Assert.That(geometry.ModelSize.y, Is.GreaterThan(0f).And.LessThanOrEqualTo(261f), entry.Id);
                Assert.That(geometry.ModelUv.xMin, Is.GreaterThanOrEqualTo(0f), entry.Id);
                Assert.That(geometry.ModelUv.yMin, Is.GreaterThanOrEqualTo(0f), entry.Id);
                Assert.That(geometry.ModelUv.xMax, Is.LessThanOrEqualTo(1f), entry.Id);
                Assert.That(geometry.ModelUv.yMax, Is.LessThanOrEqualTo(1f), entry.Id);
                Assert.That(geometry.ModelSize.x / geometry.ModelSize.y,
                    Is.EqualTo(geometry.ModelUv.width / geometry.ModelUv.height).Within(0.0001f), entry.Id);
                Assert.That(geometry.ModelBounds.yMax, Is.LessThanOrEqualTo(ComponentShopDisplayGeometry.MaximumModelTop + 0.001f));
                Assert.That(geometry.ModelBounds.Overlaps(geometry.PlaqueBounds), Is.False, entry.Id);
            }
        }

        [Test]
        public void ExpandedMarketComponents_UseMeasuredIndividualBounds()
        {
            var catalog = new ComponentArtCatalog();
            Assert.That(catalog.ExpandedEntries.Count, Is.EqualTo(ComponentArtCatalog.ExpandedEntryCount));
            foreach (var entry in catalog.ExpandedEntries)
            {
                var geometry = ComponentShopDisplayGeometry.For(entry);
                Assert.That(geometry.ModelSize.x, Is.GreaterThan(0f).And.LessThanOrEqualTo(301f), entry.Id);
                Assert.That(geometry.ModelSize.y, Is.GreaterThan(0f).And.LessThanOrEqualTo(261f), entry.Id);
                Assert.That(geometry.ModelSize.x / geometry.ModelSize.y,
                    Is.EqualTo(geometry.ModelUv.width / geometry.ModelUv.height).Within(0.0001f), entry.Id);
                Assert.That(geometry.ModelBounds.Overlaps(geometry.PlaqueBounds), Is.False, entry.Id);
                Assert.That(geometry.ModelBounds.yMax,
                    Is.LessThanOrEqualTo(ComponentShopDisplayGeometry.MaximumModelTop + 0.001f), entry.Id);
            }
        }

        [Test]
        public void ShowroomFit_PreservesEveryComponentAspectRatioInsideAuthoredBox()
        {
            var catalog = new ComponentArtCatalog();
            foreach (var entry in catalog.ExpandedEntries)
            {
                var geometry = ComponentShopDisplayGeometry.For(entry);
                Vector2 size = ComponentShopDisplayGeometry.FitShowroomModelSize(geometry.ModelUv);
                Assert.That(size.x, Is.LessThanOrEqualTo(ComponentShopDisplayGeometry.ShowroomModelSize.x + 0.001f), entry.Id);
                Assert.That(size.y, Is.LessThanOrEqualTo(ComponentShopDisplayGeometry.ShowroomModelSize.y + 0.001f), entry.Id);
                Assert.That(size.x / size.y,
                    Is.EqualTo(geometry.ModelUv.width / geometry.ModelUv.height).Within(0.0001f), entry.Id);
                Vector2 position = geometry.ShowroomModelPositionFor(size);
                Assert.That(position.y - size.y * 0.5f,
                    Is.EqualTo(geometry.PlaqueBounds.yMax + ComponentShopDisplayGeometry.MinimumPlaqueClearance).Within(0.001f), entry.Id);
            }
        }

        [Test]
        public void DetailFitIsLargerThanCompactShopFitWithoutStretchingArtwork()
        {
            foreach (var entry in new ComponentArtCatalog().ExpandedEntries)
            {
                var geometry = ComponentShopDisplayGeometry.For(entry);
                Vector2 shopSize = ComponentShopDisplayGeometry.FitShowroomModelSize(geometry.ModelUv);
                Vector2 detailSize = ComponentShopDisplayGeometry.FitDetailModelSize(geometry.ModelUv);

                Assert.That(detailSize.x, Is.GreaterThan(shopSize.x), entry.Id);
                Assert.That(detailSize.y, Is.GreaterThan(shopSize.y), entry.Id);
                Assert.That(detailSize.x / detailSize.y,
                    Is.EqualTo(geometry.ModelUv.width / geometry.ModelUv.height).Within(0.0001f), entry.Id);
            }
        }

        [Test]
        public void PlaqueGapSurvivesAllShowroomAndDetailScales()
        {
            foreach (var entry in new ComponentArtCatalog().ExpandedEntries)
                foreach (float scale in new[] { 0.78f, 0.9f, 0.96f, 1f, 1.08f })
                {
                    var geometry = ComponentShopDisplayGeometry.For(entry);
                    float clearance = (geometry.ModelBounds.yMin - geometry.PlaqueBounds.yMax) * scale;
                    Assert.That(clearance, Is.GreaterThanOrEqualTo(ComponentShopDisplayGeometry.MinimumPlaqueClearance * scale - 0.001f), entry.Id);
                }
        }

        [Test]
        public void RarityAuraAlsoRemainsAboveTheSign()
        {
            foreach (string port in RegionalShopFurniture.Ports)
                foreach (var entry in new ComponentArtCatalog().ExpandedEntries)
                {
                    var geometry = ComponentShopDisplayGeometry.For(entry, port);
                    float auraBottom = geometry.ModelPosition.y - geometry.ModelSize.y * 1.035f * 0.5f;
                    Assert.That(auraBottom - geometry.PlaqueBounds.yMax, Is.GreaterThan(15f), port + entry.Id);
                }
        }

        [Test]
        public void UnknownPortStillGetsASafeFallbackPlaque()
        {
            var entry = new ComponentArtCatalog().Entries[0];
            var geometry = ComponentShopDisplayGeometry.For(entry, "unknown-port");
            Assert.That(geometry.RegionalFurniture, Is.Null);
            Assert.That(geometry.ModelBounds.Overlaps(geometry.PlaqueBounds), Is.False);
            float foot = geometry.PlaquePosition.y + (42f / 1024f - 0.5f) * geometry.PlaqueSize.y;
            Assert.That(foot, Is.EqualTo(ComponentShopDisplayGeometry.PlaqueFloorY).Within(0.001f));
        }

        [Test]
        public void MissingComponentIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => ComponentShopDisplayGeometry.For(null));
        }
    }
}
