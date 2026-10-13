using System.Collections.Generic;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEngine;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class WarehouseRoomArtCatalogTests
    {
        [Test]
        public void PortMaterialsRemainDistinctAndReadableOnTheirDarkLinings()
        {
            var linings = new HashSet<Color>();
            foreach (string port in PortFacilityArtCatalog.SupportedRegionalPorts)
            {
                WarehouseRoomTheme theme = WarehouseRoomArtCatalog.ForPort(port);
                Assert.That(linings.Add(theme.Lining), Is.True, port + " needs its own room material.");
                Assert.That(Contrast(theme.Ink, theme.Lining), Is.GreaterThanOrEqualTo(4.5f), port + " primary text");
                Assert.That(Contrast(theme.Muted, theme.Lining), Is.GreaterThanOrEqualTo(4.5f), port + " secondary text");
                Assert.That(Contrast(theme.Brass, theme.Lining), Is.GreaterThanOrEqualTo(4.5f), port + " highlighted text");
            }
        }

        private static float Contrast(Color foreground, Color background)
        {
            float front = Luminance(foreground);
            float back = Luminance(background);
            return (Mathf.Max(front, back) + .05f) / (Mathf.Min(front, back) + .05f);
        }

        private static float Luminance(Color color) =>
            .2126f * Linear(color.r) + .7152f * Linear(color.g) + .0722f * Linear(color.b);

        private static float Linear(float value) =>
            value <= .04045f ? value / 12.92f : Mathf.Pow((value + .055f) / 1.055f, 2.4f);
    }
}
