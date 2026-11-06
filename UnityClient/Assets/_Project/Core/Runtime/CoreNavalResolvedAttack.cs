using System;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>
    /// A committed enemy cannon contact, excluding turn-end burn, field damage, guard
    /// expiry and later reactions. Presentation metadata only; never drives combat rules.
    /// </summary>
    public sealed class CoreNavalResolvedAttack
    {
        public bool Executed => true;
        public int HealthBefore { get; }
        public int HealthAfter { get; }
        public int GuardBefore { get; }
        public int GuardAfter { get; }
        public int HealthLost => Math.Max(0, HealthBefore - HealthAfter);
        public int GuardAbsorbed => Math.Max(0, GuardBefore - GuardAfter);

        internal CoreNavalResolvedAttack(int healthBefore, int healthAfter, int guardBefore, int guardAfter)
        {
            HealthBefore = healthBefore; HealthAfter = healthAfter;
            GuardBefore = guardBefore; GuardAfter = guardAfter;
        }
    }

    public sealed partial class CoreBattleState
    {
        // Reset before player-end effects on each accepted EndBattleTurn. A null result
        // means no cannon contact occurred, even if the previous intent announced one.
        [field: NonSerialized]
        public CoreNavalResolvedAttack LastNavalAttack { get; internal set; }
    }
}
