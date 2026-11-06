namespace ShatteredSail.Core.Gameplay
{
    public static class CoreCurseRules
    {
        public const int DefaultExecutionThresholdPercent = 100;
        public const int EntangleThresholdReductionPercent = 5;

        public static bool ReachesExecutionThreshold(int marks, int health,
            int thresholdPercent = DefaultExecutionThresholdPercent) =>
            health > 0 && (thresholdPercent <= 0 ||
                marks > 0 && (long)marks * 100 >= (long)health * thresholdPercent);
    }

    public sealed partial class CoreBattleState
    {
        public int PlayerCurseMarks { get; internal set; }
    }
}
