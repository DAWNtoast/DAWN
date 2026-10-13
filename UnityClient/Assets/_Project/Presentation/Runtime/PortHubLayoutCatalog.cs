using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Landmarks and permanent name positions in top-origin source-image UV coordinates.</summary>
    public sealed class PortHubLayout
    {
        public IReadOnlyList<Rect> LandmarkBounds { get; }
        public IReadOnlyList<Vector2> SignAnchors { get; }

        internal PortHubLayout(Rect[] bounds, Vector2[] anchors)
        {
            if (bounds.Length != 9 || anchors.Length != 9)
                throw new ArgumentException("A port hub must define all nine facilities.");
            LandmarkBounds = Array.AsReadOnly(bounds);
            SignAnchors = Array.AsReadOnly(anchors);
        }
    }

    public static class PortHubLayoutCatalog
    {
        private static readonly PortHubLayout BrokenAnchor = new PortHubLayout(
            new[]
            {
                new Rect(0.005f, 0.035f, 0.23f, 0.307f), // Warehouse.
                new Rect(0.712f, 0.185f, 0.282f, 0.411f), // Shipyard.
                new Rect(0.004f, 0.35f, 0.233f, 0.271f), // Tavern.
                new Rect(0.255f, 0.492f, 0.225f, 0.321f), // ComponentShop.
                new Rect(0.243f, 0.17f, 0.242f, 0.286f), // CardWorkshop.
                new Rect(0.52f, 0.214f, 0.182f, 0.34f), // Treasure.
                new Rect(0.76f, 0.605f, 0.236f, 0.316f), // Maintenance.
                new Rect(0.49f, 0.602f, 0.263f, 0.32f), // Voyage.
                new Rect(0.004f, 0.664f, 0.243f, 0.264f), // Market.
            },
            new[]
            {
                new Vector2(0.113f, 0.275f),
                new Vector2(0.855f, 0.528f),
                new Vector2(0.122f, 0.525f),
                new Vector2(0.37f, 0.689f),
                new Vector2(0.326f, 0.392f),
                new Vector2(0.614f, 0.454f),
                new Vector2(0.873f, 0.812f),
                new Vector2(0.578f, 0.768f),
                new Vector2(0.137f, 0.762f),
            });

        private static readonly PortHubLayout WhiteShell = new PortHubLayout(
            new[]
            {
                new Rect(0.243f, 0.147f, 0.23f, 0.233f), // Warehouse.
                new Rect(0.748f, 0.43f, 0.247f, 0.346f), // Shipyard.
                new Rect(0.012f, 0.311f, 0.211f, 0.252f), // Tavern.
                new Rect(0.23f, 0.548f, 0.242f, 0.232f), // ComponentShop.
                new Rect(0.015f, 0.076f, 0.222f, 0.23f), // CardWorkshop.
                new Rect(0.482f, 0.23f, 0.219f, 0.303f), // Treasure.
                new Rect(0.744f, 0.198f, 0.233f, 0.218f), // Maintenance.
                new Rect(0.48f, 0.544f, 0.243f, 0.354f), // Voyage.
                new Rect(0.012f, 0.576f, 0.209f, 0.297f), // Market.
            },
            new[]
            {
                new Vector2(0.35f, 0.342f),
                new Vector2(0.858f, 0.713f),
                new Vector2(0.132f, 0.497f),
                new Vector2(0.35f, 0.723f),
                new Vector2(0.133f, 0.27f),
                new Vector2(0.591f, 0.461f),
                new Vector2(0.848f, 0.363f),
                new Vector2(0.562f, 0.821f),
                new Vector2(0.115f, 0.803f),
            });

        private static readonly PortHubLayout BronzeBell = new PortHubLayout(
            new[]
            {
                new Rect(0.04f, 0.17f, 0.2f, 0.155f), // Warehouse.
                new Rect(0.055f, 0.333f, 0.294f, 0.229f), // Shipyard.
                new Rect(0.79f, 0.493f, 0.168f, 0.305f), // Tavern.
                new Rect(0.373f, 0.176f, 0.236f, 0.211f), // ComponentShop.
                new Rect(0.697f, 0.178f, 0.199f, 0.197f), // CardWorkshop.
                new Rect(0.04f, 0.598f, 0.149f, 0.291f), // Treasure.
                new Rect(0.218f, 0.612f, 0.224f, 0.278f), // Maintenance.
                new Rect(0.455f, 0.574f, 0.3f, 0.336f), // Voyage.
                new Rect(0.552f, 0.39f, 0.232f, 0.18f), // Market.
            },
            new[]
            {
                new Vector2(0.124f, 0.286f),
                new Vector2(0.223f, 0.463f),
                new Vector2(0.874f, 0.674f),
                new Vector2(0.474f, 0.29f),
                new Vector2(0.792f, 0.331f),
                new Vector2(0.112f, 0.855f),
                new Vector2(0.317f, 0.759f),
                new Vector2(0.674f, 0.753f),
                new Vector2(0.683f, 0.511f),
            });

        private static readonly PortHubLayout SaltCrown = new PortHubLayout(
            new[]
            {
                new Rect(0.769f, 0.174f, 0.191f, 0.183f), // Warehouse.
                new Rect(0.045f, 0.572f, 0.255f, 0.338f), // Shipyard.
                new Rect(0.04f, 0.315f, 0.214f, 0.235f), // Tavern.
                new Rect(0.267f, 0.182f, 0.16f, 0.19f), // ComponentShop.
                new Rect(0.792f, 0.37f, 0.168f, 0.206f), // CardWorkshop.
                new Rect(0.573f, 0.313f, 0.184f, 0.247f), // Treasure.
                new Rect(0.432f, 0.175f, 0.17f, 0.131f), // Maintenance.
                new Rect(0.668f, 0.596f, 0.288f, 0.314f), // Voyage.
                new Rect(0.306f, 0.634f, 0.318f, 0.274f), // Market.
            },
            new[]
            {
                new Vector2(0.858f, 0.304f),
                new Vector2(0.176f, 0.788f),
                new Vector2(0.146f, 0.429f),
                new Vector2(0.326f, 0.272f),
                new Vector2(0.874f, 0.501f),
                new Vector2(0.661f, 0.515f),
                new Vector2(0.514f, 0.258f),
                new Vector2(0.888f, 0.747f),
                new Vector2(0.459f, 0.86f),
            });

        private static readonly PortHubLayout TwilightTide = new PortHubLayout(
            new[]
            {
                new Rect(0.005f, 0.047f, 0.269f, 0.354f), // Warehouse.
                new Rect(0.73f, 0.328f, 0.265f, 0.346f), // Shipyard.
                new Rect(0.285f, 0.282f, 0.215f, 0.286f), // Tavern.
                new Rect(0.578f, 0.158f, 0.145f, 0.258f), // ComponentShop.
                new Rect(0.005f, 0.452f, 0.273f, 0.36f), // CardWorkshop.
                new Rect(0.369f, 0.041f, 0.19f, 0.239f), // Treasure.
                new Rect(0.768f, 0.096f, 0.228f, 0.225f), // Maintenance.
                new Rect(0.31f, 0.594f, 0.36f, 0.334f), // Voyage.
                new Rect(0.685f, 0.685f, 0.31f, 0.243f), // Market.
            },
            new[]
            {
                new Vector2(0.16f, 0.31f),
                new Vector2(0.857f, 0.545f),
                new Vector2(0.387f, 0.485f),
                new Vector2(0.655f, 0.35f),
                new Vector2(0.15f, 0.748f),
                new Vector2(0.453f, 0.247f),
                new Vector2(0.872f, 0.281f),
                new Vector2(0.4f, 0.814f),
                new Vector2(0.841f, 0.858f),
            });

        private static readonly PortHubLayout GrayGull = new PortHubLayout(
            new[]
            {
                new Rect(0.773f, 0.34f, 0.215f, 0.268f), // Warehouse.
                new Rect(0.505f, 0.35f, 0.256f, 0.251f), // Shipyard.
                new Rect(0.387f, 0.165f, 0.232f, 0.178f), // Tavern.
                new Rect(0.308f, 0.607f, 0.266f, 0.248f), // ComponentShop.
                new Rect(0.108f, 0.198f, 0.195f, 0.15f), // CardWorkshop.
                new Rect(0.251f, 0.351f, 0.229f, 0.202f), // Treasure.
                new Rect(0.679f, 0.169f, 0.278f, 0.168f), // Maintenance.
                new Rect(0.018f, 0.56f, 0.288f, 0.326f), // Voyage.
                new Rect(0.585f, 0.618f, 0.308f, 0.287f), // Market.
            },
            new[]
            {
                new Vector2(0.872f, 0.576f),
                new Vector2(0.646f, 0.554f),
                new Vector2(0.511f, 0.317f),
                new Vector2(0.441f, 0.795f),
                new Vector2(0.2f, 0.319f),
                new Vector2(0.354f, 0.522f),
                new Vector2(0.817f, 0.307f),
                new Vector2(0.242f, 0.748f),
                new Vector2(0.744f, 0.866f),
            });

        public static PortHubLayout ForPort(string portName)
        {
            switch (portName)
            {
                case "白贝港": return WhiteShell;
                case "铜钟港": return BronzeBell;
                case "盐冠港": return SaltCrown;
                case "暮潮港": return TwilightTide;
                case "灰鸥港": return GrayGull;
                // Match PortArtCatalog: unknown or missing names use Broken Anchor.
                default: return BrokenAnchor;
            }
        }
    }
}
