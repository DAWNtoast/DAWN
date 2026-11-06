using System.Text.RegularExpressions;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Keep effect formulas without repeating their calculated examples on card faces.</summary>
    internal static class CoreBattleCardFormulaText
    {
        private static readonly Regex CalculatedExamples = new Regex(
            @"[，；]\s*(?:即|总计|合计)\s*\d+(?:\s*(?:[/／]|至|或|～|~)\s*\d+)+(?:\s*(?:层|点(?:伤害|格挡)?))?(?=\s*(?:[，；。！？]|$))");

        internal static string RemoveCalculatedExamples(string text, CoreBattleCard card)
        {
            if (string.IsNullOrEmpty(text) || card == null) return text;

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
