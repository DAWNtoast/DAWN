using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ShatteredSail.Presentation
{
    /// <summary>A shared row keeps the two component panes aligned, including missing effects.</summary>
    public sealed class ShipyardComponentComparisonRow
    {
        public string Key { get; }
        public string Label { get; }
        public string CurrentValue { get; }
        public string SelectedValue { get; }
        public bool HasCurrent { get; }
        public bool HasSelected { get; }
        public bool HasDelta { get; }
        public float Delta { get; }

        internal ShipyardComponentComparisonRow(string key, string label, float current, float selected,
            bool hasCurrent, bool hasSelected, bool comparable, bool percentage = false)
        {
            Key = key;
            Label = label;
            HasCurrent = hasCurrent;
            HasSelected = hasSelected;
            HasDelta = comparable && hasCurrent && hasSelected;
            Delta = HasDelta ? selected - current : 0f;
            CurrentValue = !hasCurrent ? "—" : percentage
                ? ShipyardComponentComparison.Number(current) + "%" : ShipyardComponentComparison.SignedNumber(current);
            SelectedValue = !hasSelected ? "—" : percentage
                ? ShipyardComponentComparison.Number(selected) + "%" : ShipyardComponentComparison.SignedNumber(selected);
            if (HasDelta) SelectedValue += " (" + ShipyardComponentComparison.SignedNumber(Delta) + (percentage ? "%" : string.Empty) + ")";
        }
    }

    /// <summary>
    /// Compares actual component effects, not parsed dossier prose. Appraisal does not scale
    /// the five base bonuses in gameplay, so it is kept separate from those numbers here too.
    /// </summary>
    public sealed class ShipyardComponentComparison
    {
        private static readonly string[] Keys = { "hull", "armor", "firepower", "speed", "vision" };
        private static readonly string[] Labels = { "船体", "装甲", "火力", "航速", "视野" };

        public IReadOnlyList<ShipyardComponentComparisonRow> Rows { get; }
        public string CurrentText { get; }
        public string SelectedText { get; }
        public string CurrentQualityText { get; }
        public string SelectedQualityText { get; }

        private ShipyardComponentComparison(List<ShipyardComponentComparisonRow> rows,
            string currentQuality, string selectedQuality)
        {
            Rows = rows.AsReadOnly();
            CurrentQualityText = currentQuality;
            SelectedQualityText = selectedQuality;
            StringBuilder current = new StringBuilder();
            StringBuilder selected = new StringBuilder();
            foreach (ShipyardComponentComparisonRow row in rows)
            {
                if (current.Length > 0)
                {
                    current.Append('\n');
                    selected.Append('\n');
                }
                current.Append(row.Label).Append("　").Append(row.CurrentValue);
                selected.Append(row.Label).Append("　").Append(row.SelectedValue);
            }
            CurrentText = current.ToString();
            SelectedText = selected.ToString();
        }

        public static ShipyardComponentComparison Build(ComponentArtEntry currentEntry, float currentQuality,
            ComponentArtEntry selectedEntry, float selectedQuality)
        {
            bool comparable = currentEntry != null && selectedEntry != null && currentEntry.Slot == selectedEntry.Slot;
            float[] current = Bonuses(currentEntry);
            float[] selected = Bonuses(selectedEntry);
            List<ShipyardComponentComparisonRow> rows = new List<ShipyardComponentComparisonRow>();
            for (int index = 0; index < Keys.Length; index++)
            {
                // Zero bonuses are absent effects in the canonical component dossier.
                bool hasCurrent = currentEntry != null && current[index] != 0f;
                bool hasSelected = selectedEntry != null && selected[index] != 0f;
                if (!hasCurrent && !hasSelected) continue;
                rows.Add(new ShipyardComponentComparisonRow(Keys[index], Labels[index], current[index], selected[index],
                    hasCurrent, hasSelected, comparable));
            }

            float currentPercent = SafeQuality(currentQuality) * 100f;
            float selectedPercent = SafeQuality(selectedQuality) * 100f;
            ShipyardComponentComparisonRow quality = new ShipyardComponentComparisonRow("quality", "品质",
                currentPercent, selectedPercent, currentEntry != null, selectedEntry != null, comparable, true);
            if (currentEntry != null || selectedEntry != null) rows.Add(quality);
            return new ShipyardComponentComparison(rows, quality.CurrentValue, quality.SelectedValue);
        }

        private static float[] Bonuses(ComponentArtEntry entry)
        {
            return entry == null ? new float[5] : new[]
            {
                (float)entry.HullBonus, entry.ArmorBonus, entry.FirepowerBonus, entry.SpeedBonus, entry.VisionBonus
            };
        }

        private static float SafeQuality(float quality) => float.IsNaN(quality) || float.IsInfinity(quality)
            ? 1f : Math.Max(0.9f, Math.Min(1.1f, quality));

        internal static string SignedNumber(float value)
        {
            double rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
            return (rounded > 0d ? "+" : string.Empty) + Number((float)rounded);
        }

        internal static string Number(float value)
        {
            double rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
            return rounded == 0d ? "0" : rounded.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
