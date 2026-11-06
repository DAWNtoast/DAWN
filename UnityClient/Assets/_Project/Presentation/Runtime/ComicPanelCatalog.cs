using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Authored reading order and source bounds; textures remain untouched.</summary>
    public static class ComicPanelCatalog
    {
        public const int QuadPanelCount = 4;
        private static readonly IReadOnlyList<Rect> quadrants = Array.AsReadOnly(new[]
        {
            QuadPanel(0), QuadPanel(1), QuadPanel(2), QuadPanel(3)
        });
        private static readonly IReadOnlyList<Rect> singlePage =
            Array.AsReadOnly(new[] { new Rect(0f, 0f, 1f, 1f) });

        public static IReadOnlyList<Rect> PanelsFor(string imageResource)
        {
            if (imageResource != null && (imageResource.StartsWith("OpeningComics/", StringComparison.Ordinal) ||
                imageResource.StartsWith("ArrivalComics/", StringComparison.Ordinal) ||
                imageResource.StartsWith("LegendaryFleetComics/", StringComparison.Ordinal))) return quadrants;
            return singlePage;
        }

        /// <summary>Optional independent panels retain the original page identity and reading order.</summary>
        public static string HighResolutionPanelResource(string imageResource, int panelIndex)
        {
            IReadOnlyList<Rect> panels = PanelsFor(imageResource);
            if (panelIndex < 0 || panelIndex >= panels.Count) throw new ArgumentOutOfRangeException(nameof(panelIndex));
            return panels.Count == QuadPanelCount
                ? "ComicPanelsHD/" + imageResource + "_panel" + (panelIndex + 1).ToString("00", System.Globalization.CultureInfo.InvariantCulture)
                : null;
        }

        /// <summary>
        /// Uses the selected independent panel when installed, otherwise the exact original page crop.
        /// No texture cache is retained here; live comic pages own their borrowed resource references.
        /// </summary>
        public static Texture2D LoadPanel(string imageResource, int panelIndex, out Rect source,
            Func<string, Texture2D> resourceLoader = null)
        {
            if (string.IsNullOrWhiteSpace(imageResource)) throw new ArgumentException("A comic resource is required.", nameof(imageResource));
            string highResolutionResource = HighResolutionPanelResource(imageResource, panelIndex);
            source = PanelsFor(imageResource)[panelIndex];
            if (resourceLoader == null) resourceLoader = Resources.Load<Texture2D>;
            if (highResolutionResource != null)
            {
                Texture2D panel = resourceLoader(highResolutionResource);
                if (panel != null)
                {
                    source = new Rect(0f, 0f, 1f, 1f);
                    return panel;
                }
            }
            return resourceLoader(imageResource);
        }

        public static Rect QuadPanel(int index)
        {
            if (index < 0 || index >= QuadPanelCount) throw new ArgumentOutOfRangeException(nameof(index));
            return new Rect(index % 2 * .5f, index < 2 ? .5f : 0f, .5f, .5f);
        }
    }
}
