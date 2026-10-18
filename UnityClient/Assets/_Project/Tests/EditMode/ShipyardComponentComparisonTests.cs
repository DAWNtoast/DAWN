using System.Globalization;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Presentation;

namespace ShatteredSail.Core.Tests
{
    public sealed class ShipyardComponentComparisonTests
    {
        [Test]
        public void SharedEffects_ShowPositiveNegativeAndZeroDifferencesOnlyOnSelectedSide()
        {
            ShipyardComponentComparison comparison = ShipyardComponentComparison.Build(
                Entry(hull: 2, armor: 5, firepower: 3),
                Entry(hull: 5, armor: 2, firepower: 3));

            Assert.That(comparison.Rows.Select(row => row.Key), Is.EqualTo(new[] { "hull", "armor", "firepower" }));
            Assert.That(comparison.Rows.Select(row => row.CurrentValue), Is.EqualTo(new[] { "+2", "+5", "+3" }));
            Assert.That(comparison.Rows.Select(row => row.SelectedValue), Is.EqualTo(new[] { "+5 (+3)", "+2 (-3)", "+3 (0)" }));
            Assert.That(comparison.Rows.All(row => row.HasDelta), Is.True);
        }

        [Test]
        public void DifferentEffects_KeepRowsAlignedWithoutInventingDeltasForMissingFields()
        {
            ShipyardComponentComparison comparison = ShipyardComponentComparison.Build(
                Entry(hull: 2, speed: -0.4f), Entry(armor: 1, speed: 0.2f));

            Assert.That(comparison.Rows.Select(row => row.Key), Is.EqualTo(new[] { "hull", "armor", "speed" }));
            Assert.That(comparison.CurrentText, Is.EqualTo("船体　+2\n装甲　—\n航速　-0.4"));
            Assert.That(comparison.SelectedText, Is.EqualTo("船体　—\n装甲　+1\n航速　+0.2 (+0.6)"));
            Assert.That(comparison.Rows[0].HasSelected, Is.False);
            Assert.That(comparison.Rows[1].HasCurrent, Is.False);
            Assert.That(comparison.Rows[0].HasDelta || comparison.Rows[1].HasDelta, Is.False);
        }

        [Test]
        public void EmptySlot_ShowsCandidateWithoutPretendingAnEquippedItemExists()
        {
            ShipyardComponentComparison comparison = ShipyardComponentComparison.Build(null,
                Entry(hull: 4, vision: 5f));

            Assert.That(comparison.CurrentText, Is.EqualTo("船体　—\n视野　—"));
            Assert.That(comparison.SelectedText, Is.EqualTo("船体　+4\n视野　+5"));
            Assert.That(comparison.Rows.All(row => !row.HasDelta), Is.True);
        }

        [Test]
        public void MatchingComponents_ShowOnlyActualBonusesAndZeroDifferences()
        {
            ComponentArtEntry entry = Entry(hull: 10, speed: 1.5f);
            ShipyardComponentComparison comparison = ShipyardComponentComparison.Build(entry, entry);

            Assert.That(comparison.Rows.Select(row => row.Key), Is.EqualTo(new[] { "hull", "speed" }));
            Assert.That(comparison.CurrentText, Is.EqualTo("船体　+10\n航速　+1.5"));
            Assert.That(comparison.SelectedText, Is.EqualTo("船体　+10 (0)\n航速　+1.5 (0)"));
        }

        [Test]
        public void DifferentSlots_DoNotCompareUnrelatedComponents()
        {
            ShipyardComponentComparison comparison = ShipyardComponentComparison.Build(
                Entry(hull: 3, slot: ComponentSlot.Bow),
                Entry(hull: 8, slot: ComponentSlot.Plating));

            Assert.That(comparison.Rows[0].SelectedValue, Is.EqualTo("+8"));
            Assert.That(comparison.Rows[0].HasDelta, Is.False);
        }

        [Test]
        public void NoSelection_LeavesCandidateEmptyAndHandlesBothEmpty()
        {
            ShipyardComponentComparison currentOnly = ShipyardComponentComparison.Build(Entry(armor: 2), null);
            Assert.That(currentOnly.SelectedText, Is.EqualTo("装甲　—"));
            ShipyardComponentComparison empty = ShipyardComponentComparison.Build(null, null);
            Assert.That(empty.Rows, Is.Empty);
            Assert.That(empty.CurrentText, Is.Empty);
            Assert.That(empty.SelectedText, Is.Empty);
        }

        [Test]
        public void FloatNoiseAndDecimalCulture_DoNotCreateSpuriousDeltasOrInconsistentFormatting()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                ShipyardComponentComparison comparison = ShipyardComponentComparison.Build(
                    Entry(speed: 0.3f, vision: 1.5f),
                    Entry(speed: 0.1f + 0.2f, vision: 1.25f));

                Assert.That(comparison.SelectedText, Is.EqualTo("航速　+0.3 (0)\n视野　+1.25 (-0.25)"));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        private static ComponentArtEntry Entry(int hull = 0, int armor = 0, int firepower = 0,
            float speed = 0f, float vision = 0f, ComponentSlot slot = ComponentSlot.Bow)
        {
            return new ComponentArtEntry("test_component", "测试部件", slot, ComponentRarity.Common,
                1, 1, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                hull, armor, firepower, speed, vision);
        }
    }
}
