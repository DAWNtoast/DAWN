using System;
using System.Globalization;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Real-time income from displayed keepsakes, shared by every port.</summary>
    public sealed class CoreWarehouseIncome
    {
        public const int GoldPerItemPerHour = 10;
        public const int MaximumPendingGold = 300;
        private const string FormatVersion = "1";
        private const long UnitsPerGold = TimeSpan.TicksPerHour;
        private const long MaximumPendingUnits = MaximumPendingGold * UnitsPerGold;
        private long pendingUnits;

        public bool IsInitialized { get; private set; }
        public long LastUtcTicks { get; private set; }
        public decimal PendingGold => pendingUnits / (decimal)UnitsPerGold;
        public int CurrentDisplayedCount { get; private set; }

        /// <summary>
        /// Accrues the previous display count before recording the new count. Call after a
        /// placement or removal; the first observation starts the clock without back pay.
        /// Unspecified DateTime values are interpreted as UTC.
        /// </summary>
        public void Observe(DateTime utcNow, int displayedCount)
        {
            if (displayedCount < 0) throw new ArgumentOutOfRangeException(nameof(displayedCount));
            long nowTicks = utcNow.Kind == DateTimeKind.Local ? utcNow.ToUniversalTime().Ticks : utcNow.Ticks;
            if (!IsInitialized)
            {
                IsInitialized = true;
                LastUtcTicks = nowTicks;
                CurrentDisplayedCount = displayedCount;
                return;
            }

            // A clock rollback cannot make an already accrued interval payable again.
            if (nowTicks > LastUtcTicks)
            {
                long elapsedTicks = nowTicks - LastUtcTicks;
                long remainingUnits = MaximumPendingUnits - pendingUnits;
                long unitsPerTick = (long)GoldPerItemPerHour * CurrentDisplayedCount;
                if (unitsPerTick > 0 && remainingUnits > 0)
                {
                    // Saturate before multiplying: a long absence or an extreme count
                    // must not overflow. Integer units also prevent rounding loss when
                    // the same elapsed interval is observed in many small pieces.
                    pendingUnits += elapsedTicks > remainingUnits / unitsPerTick
                        ? remainingUnits : elapsedTicks * unitsPerTick;
                }
                // Discard time beyond the cap, so another login cannot claim overflow.
                LastUtcTicks = nowTicks;
            }
            CurrentDisplayedCount = displayedCount;
        }

        /// <summary>Accrues through login time, pays whole gold, and retains the fractional remainder.</summary>
        public int Claim(DateTime utcNow)
        {
            Observe(utcNow, CurrentDisplayedCount);
            int gold = (int)(pendingUnits / UnitsPerGold);
            pendingUnits -= gold * UnitsPerGold;
            return gold;
        }

        public string Serialize()
        {
            if (!IsInitialized) return string.Empty;
            return string.Join("|", FormatVersion,
                LastUtcTicks.ToString(CultureInfo.InvariantCulture),
                pendingUnits.ToString(CultureInfo.InvariantCulture),
                CurrentDisplayedCount.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Validates the entire snapshot before replacing state. Empty data starts a new clock.</summary>
        public bool Restore(string serialized)
        {
            if (string.IsNullOrEmpty(serialized))
            {
                IsInitialized = false;
                LastUtcTicks = 0;
                pendingUnits = 0;
                CurrentDisplayedCount = 0;
                return true;
            }

            string[] fields = serialized.Split('|');
            if (fields.Length != 4 || fields[0] != FormatVersion ||
                !long.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out long lastUtcTicks) ||
                lastUtcTicks < 0 || lastUtcTicks > DateTime.MaxValue.Ticks ||
                !long.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out long restoredUnits) ||
                restoredUnits < 0 || restoredUnits > MaximumPendingUnits ||
                !int.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out int count) || count < 0)
                return false;

            IsInitialized = true;
            LastUtcTicks = lastUtcTicks;
            pendingUnits = restoredUnits;
            CurrentDisplayedCount = count;
            return true;
        }
    }
}
