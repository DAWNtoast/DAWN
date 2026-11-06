using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShatteredSail.Core.Tests
{
    public sealed class ComicPanelResourceTests
    {
        private Texture2D original;
        private Texture2D independent;

        [SetUp]
        public void CreateBorrowedTextures()
        {
            original = new Texture2D(2, 2) { name = "Original four-panel page" };
            independent = new Texture2D(2, 2) { name = "Independent panel" };
        }

        [TearDown]
        public void ReleaseBorrowedTextures()
        {
            Object.DestroyImmediate(original);
            Object.DestroyImmediate(independent);
        }

        [TestCase("OpeningComics/opening_page01", 0, "ComicPanelsHD/OpeningComics/opening_page01_panel01")]
        [TestCase("OpeningComics/opening_page04", 3, "ComicPanelsHD/OpeningComics/opening_page04_panel04")]
        [TestCase("ArrivalComics/bronze_bell_page02", 1, "ComicPanelsHD/ArrivalComics/bronze_bell_page02_panel02")]
        [TestCase("LegendaryFleetComics/ember_ark_before", 2, "ComicPanelsHD/LegendaryFleetComics/ember_ark_before_panel03")]
        [TestCase("LegendaryFleetComics/last_horizon_after", 3, "ComicPanelsHD/LegendaryFleetComics/last_horizon_after_panel04")]
        public void IndependentResourceNames_PreserveTheOriginalPageAndOneBasedPanel(string resource, int panel, string expected)
        {
            Assert.That(ComicPanelCatalog.HighResolutionPanelResource(resource, panel), Is.EqualTo(expected));
        }

        [Test]
        public void InstalledPanel_UsesWholeImageAndDoesNotRequireTheOriginalSheet()
        {
            var requests = new List<string>();
            Texture2D texture = ComicPanelCatalog.LoadPanel("OpeningComics/opening_page03", 2, out Rect uv, path =>
            {
                requests.Add(path);
                return path == "ComicPanelsHD/OpeningComics/opening_page03_panel03" ? independent : null;
            });
            Assert.That(texture, Is.SameAs(independent));
            Assert.That(uv, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)), "An independent image must not be quartered again.");
            CollectionAssert.AreEqual(new[] { "ComicPanelsHD/OpeningComics/opening_page03_panel03" }, requests);
        }

        [TestCase(0, 0f, .5f)]
        [TestCase(1, .5f, .5f)]
        [TestCase(2, 0f, 0f)]
        [TestCase(3, .5f, 0f)]
        public void MissingIndependentPanel_UsesTheSameOriginalPageAndCorrectReadingOrder(int panel, float x, float y)
        {
            const string resource = "ArrivalComics/white_shell_page02";
            var requests = new List<string>();
            Texture2D texture = ComicPanelCatalog.LoadPanel(resource, panel, out Rect uv, path =>
            {
                requests.Add(path);
                return path == resource ? original : null;
            });
            Assert.That(texture, Is.SameAs(original));
            Assert.That(uv, Is.EqualTo(new Rect(x, y, .5f, .5f)));
            Assert.That(requests, Has.Count.EqualTo(2));
            Assert.That(requests[1], Is.EqualTo(resource), "Fallback cannot select another page or story.");
        }

        [Test]
        public void MissingBothVersions_ReturnsNullWithoutInventingAnotherPanel()
        {
            var requests = new List<string>();
            Texture2D texture = ComicPanelCatalog.LoadPanel("LegendaryFleetComics/tide_requiem_after", 3,
                out Rect uv, path => { requests.Add(path); return null; });
            Assert.That(texture, Is.Null);
            Assert.That(uv, Is.EqualTo(new Rect(.5f, 0f, .5f, .5f)));
            CollectionAssert.AreEqual(new[]
            {
                "ComicPanelsHD/LegendaryFleetComics/tide_requiem_after_panel04",
                "LegendaryFleetComics/tide_requiem_after"
            }, requests);
        }

        [Test]
        public void PartialDelivery_DoesNotSkipAMissingPanelOrReuseItsNeighboursImage()
        {
            const string resource = "OpeningComics/opening_page01";
            Func<string, Texture2D> load = path => path == resource ? original
                : path == "ComicPanelsHD/OpeningComics/opening_page01_panel03" ? independent : null;
            Assert.That(ComicPanelCatalog.LoadPanel(resource, 1, out Rect second, load), Is.SameAs(original));
            Assert.That(second, Is.EqualTo(new Rect(.5f, .5f, .5f, .5f)));
            Assert.That(ComicPanelCatalog.LoadPanel(resource, 2, out Rect third, load), Is.SameAs(independent));
            Assert.That(third, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
            Assert.That(ComicPanelCatalog.LoadPanel(resource, 3, out Rect fourth, load), Is.SameAs(original));
            Assert.That(fourth, Is.EqualTo(new Rect(.5f, 0f, .5f, .5f)));
        }

        [Test]
        public void ExistingFullPageComics_LoadOnlyTheirOriginalResource()
        {
            const string resource = "BattleComics/HD/ram_player_head_on_page01";
            var requests = new List<string>();
            Texture2D texture = ComicPanelCatalog.LoadPanel(resource, 0, out Rect uv,
                path => { requests.Add(path); return original; });
            Assert.That(texture, Is.SameAs(original));
            Assert.That(uv, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
            Assert.That(ComicPanelCatalog.HighResolutionPanelResource(resource, 0), Is.Null);
            CollectionAssert.AreEqual(new[] { resource }, requests);
        }

        [TestCase("OpeningComics/opening_page01", -1)]
        [TestCase("OpeningComics/opening_page01", 4)]
        [TestCase("BattleComics/HD/ram_player_head_on_page01", 1)]
        public void InvalidIndex_IsRejectedBeforeLoadingAnyArtwork(string resource, int panel)
        {
            var requests = new List<string>();
            Assert.Throws<ArgumentOutOfRangeException>(() => ComicPanelCatalog.LoadPanel(resource, panel, out _,
                path => { requests.Add(path); return original; }));
            Assert.That(requests, Is.Empty);
        }
    }
}
