using System.Globalization;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Explicit test commands, kept separate from ordinary progression.</summary>
    public static class CoreGameMasterTools
    {
        // Leaves ample headroom for normal rewards and settlement arithmetic.
        public const int MaximumProfileGold = CoreEconomyBalance.MaximumProfileGold;

        public static bool TryParseTreasureFragments(string input, out int fragments)
        {
            fragments = 0;
            if (string.IsNullOrEmpty(input) || input.Length > 64) return false;
            for (int index = 0; index < input.Length; index++)
                if (input[index] < '0' || input[index] > '9') return false;
            return int.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out fragments);
        }

        public static bool TryParseProfileGold(string input, out decimal gold)
        {
            gold = 0m;
            if (string.IsNullOrEmpty(input) || input.Length > 64) return false;
            // Accept exact ASCII amounts, not signs, whitespace, grouping or exponent
            // notation. Inspect precision before parsing, which can otherwise round
            // a very long fractional input to a different decimal value.
            int point = -1;
            for (int index = 0; index < input.Length; index++)
            {
                char digit = input[index];
                if (digit == '.' && point < 0 && index > 0 && index < input.Length - 1)
                {
                    point = index;
                    continue;
                }
                if (digit < '0' || digit > '9' || (point >= 0 && index - point > 4 && digit != '0')) return false;
            }
            if (!decimal.TryParse(input, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal parsed) ||
                !CoreEconomyBalance.IsValidGoldAmount(parsed)) return false;
            gold = parsed;
            return true;
        }
    }

    public sealed partial class CoreVoyageSession
    {
        public bool TryGameMasterSetTreasureFragments(int fragments)
        {
            if (Phase != CoreDemoPhase.Port || fragments < 0) return false;
            TreasureFragments = fragments;
            AddLog("GM 测试工具：未鉴定藏宝图碎片设置为 " + fragments + " 个。");
            return true;
        }

        public bool TryGameMasterSetProfileGold(decimal gold)
        {
            if (Phase != CoreDemoPhase.Port || !CoreEconomyBalance.IsValidGoldAmount(gold))
                return false;
            ProfileGold = gold;
            AddLog("GM 测试工具：船长资金设置为 " + gold + " 金币。");
            return true;
        }
    }
}
