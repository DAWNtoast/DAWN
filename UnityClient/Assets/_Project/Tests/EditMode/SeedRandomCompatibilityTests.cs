using NUnit.Framework;
using ShatteredSail.Core.Random;

namespace ShatteredSail.Core.Tests
{
    public sealed class SeedRandomCompatibilityTests
    {
        [Test]
        public void Hash_UsesLegacyFallback_ForNullAndEmptyValues()
        {
            Assert.That(SeedHash.Compute(null), Is.EqualTo(2685179343u));
            Assert.That(SeedHash.Compute(string.Empty), Is.EqualTo(2685179343u));
        }

        [Test]
        public void Hash_MatchesBrowserDemo_ForAsciiAndChineseText()
        {
            Assert.That(SeedHash.Compute("SHATTERED-SAIL"), Is.EqualTo(2685179343u));
            Assert.That(SeedHash.Compute("碎帆纪元"), Is.EqualTo(1748935690u));
            Assert.That(SeedHash.Compute("route_shattered_sail_reef:Tutorial"), Is.EqualTo(2007029834u));
        }

        [Test]
        public void Mulberry32_MatchesBrowserDemo_ReferenceSequence()
        {
            var random = new Mulberry32Random(SeedHash.Compute("SHATTERED-SAIL"));
            uint[] expected =
            {
                210835731u,
                400406821u,
                4062652827u,
                355908009u,
                2392970617u,
            };

            foreach (uint value in expected)
            {
                Assert.That(random.NextUInt(), Is.EqualTo(value));
            }
        }
    }
}

