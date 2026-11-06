namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Every school formula uses the same authored rank definition as its execution.</summary>
    public static class CoreBoardingCardDescriptions
    {
        public static string AtLevel(CoreBattleCard design, int level)
        {
            if (design == null) return string.Empty;
            string text = design.DescriptionTemplate;
            if (design.BoardingSpec == null || level <= 0) return text;
            // All school keys are owned by the current manuscript. Do not revive
            // old whole-formula multiplier substitutions behind that definition.
            return CoreBoardingManuscriptDescriptions.ForKey(design.BoardingSpec.Key, design, level) ?? text;
        }
    }
}
