using System.Globalization;
using System.Text.RegularExpressions;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Keep effect formulas without repeating their calculated examples on card faces.</summary>
    internal static class CoreBattleCardFormulaText
    {
        private static readonly Regex CalculatedExamples = new Regex(
            @"[，；]\s*(?:即|总计|合计)\s*\d+(?:\s*(?:[/／]|至|或|～|~)\s*\d+)+(?:\s*(?:层|点(?:伤害|格挡)?))?(?=\s*(?:[，；。！？]|$))");

        private static readonly Regex SelfBurnResults = new Regex(
            @"按出牌前\s*0\s*/\s*1\s*/\s*2\s*/\s*至少\s*3\s*层自燃\s*，\s*分别造成\s*(?:\d+\s*/\s*){3}(?<cap>\d+)\s*(?:点\s*)?伤害");

        internal static string RemoveCalculatedExamples(string text, CoreBattleCard card)
        {
            if (string.IsNullOrEmpty(text) || card == null) return text;

            // This upgraded card previously had only a result table. Restore its whole-formula
            // rounding and cap before removing examples; separately rounded terms would be wrong.
            if (card.BoardingSpec?.Key == "fire_11" && card.UpgradeLevel > 0)
                text = SelfBurnResults.Replace(text, match =>
                    "造成 (30+2×出牌前自燃层数)×" + CoreCardUpgradeDesign.EffectMultiplier(card, card.UpgradeLevel)
                        .ToString(CultureInfo.InvariantCulture) + "（结果四舍五入） 点伤害，最多 " +
                    match.Groups["cap"].Value + " 点伤害");

            // The base mixed attack states both components and their sum. Upgrades already
            // contain one rounded damage amount, which remains the sole effect value.
            if (card.BoardingSpec?.Key == "fire_01")
                text = Regex.Replace(text,
                    @"(?<blade>\d+)\s*白刃\s*\+\s*(?<fire>\d+)\s*即时火焰\s*，\s*合计\s*\d+\s*(?:点\s*)?伤害",
                    "造成 ${blade}+${fire} 点白刃与即时火焰混合伤害");

            // A single live total can be the only effect amount, so only remove labelled
            // lists/ranges here. Selection ranges, timing, conditions and caps remain intact.
            return CalculatedExamples.Replace(text, string.Empty);
        }
    }
}
