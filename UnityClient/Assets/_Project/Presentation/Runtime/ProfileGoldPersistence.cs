using System;
using System.Globalization;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Exact decimal account storage, with migration from integer saves.</summary>
    public static class ProfileGoldPersistence
    {
        public const string DecimalKey = "ShatteredSail.ProfileGold.Decimal.v2";

        public static decimal Load()
        {
            string saved = PlayerPrefs.GetString(DecimalKey, string.Empty);
            if (decimal.TryParse(saved, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal amount) &&
                IsValid(amount)) return amount;
            return Math.Max(0, PlayerPrefs.GetInt(CoreGameplayDemoController.ProfileGoldKey, 500));
        }

        public static void Save(decimal amount)
        {
            if (!IsValid(amount)) throw new ArgumentOutOfRangeException(nameof(amount));
            PlayerPrefs.SetString(DecimalKey, amount.ToString("0.####", CultureInfo.InvariantCulture));
            // Keep the legacy whole-gold field readable by older tools. Runtime
            // readers always use the exact decimal value above.
            PlayerPrefs.SetInt(CoreGameplayDemoController.ProfileGoldKey, (int)decimal.Floor(amount));
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(DecimalKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.ProfileGoldKey);
        }

        private static bool IsValid(decimal amount) => amount >= 0m &&
            amount <= int.MaxValue && decimal.Round(amount, 4) == amount;
    }
}
