using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>One already committed queued attack. This is presentation evidence, never a combat command.</summary>
    public readonly struct CoreBoardingResolvedAttack
    {
        public long Sequence { get; }
        public string SourceSchoolId { get; }
        public string SourceCardId { get; }
        public string Label { get; }
        public int HealthBefore { get; }
        public int HealthAfter { get; }
        public int GuardBefore { get; }
        public int GuardAfter { get; }
        public int HealthLost => Math.Max(0, HealthBefore - HealthAfter);
        public int GuardAbsorbed => Math.Max(0, GuardBefore - GuardAfter);

        internal CoreBoardingResolvedAttack(long sequence, CoreBoardingDelayedDamage source,
            int healthBefore, int healthAfter, int guardBefore, int guardAfter)
        {
            Sequence = sequence;
            SourceSchoolId = source.SourceSchoolId ?? string.Empty;
            SourceCardId = source.SourceCardId ?? string.Empty;
            Label = source.Label ?? string.Empty;
            HealthBefore = healthBefore; HealthAfter = healthAfter;
            GuardBefore = guardBefore; GuardAfter = guardAfter;
        }
    }

    public sealed partial class CoreBoardingBattleState
    {
        public const int ResolvedAttackHistoryLimit = 64;
        // An active battle is not saved. Keep this observation history out of any future
        // field serializer too; previews must not inherit/replay presentation evidence.
        [NonSerialized] private readonly List<CoreBoardingResolvedAttack> resolvedAttacks = new List<CoreBoardingResolvedAttack>();
        [NonSerialized] private long resolvedAttackSequence;
        [NonSerialized] private IReadOnlyList<CoreBoardingResolvedAttack> resolvedAttackView;
        public IReadOnlyList<CoreBoardingResolvedAttack> ResolvedAttacks => resolvedAttackView ?? (resolvedAttackView = resolvedAttacks.AsReadOnly());
        public long ResolvedAttackSequence => resolvedAttackSequence;

        internal void RecordResolvedAttack(CoreBoardingDelayedDamage source,
            int healthBefore, int healthAfter, int guardBefore, int guardAfter)
        {
            if (resolvedAttacks.Count >= ResolvedAttackHistoryLimit) resolvedAttacks.RemoveAt(0);
            resolvedAttacks.Add(new CoreBoardingResolvedAttack(++resolvedAttackSequence, source,
                healthBefore, healthAfter, guardBefore, guardAfter));
        }
    }
}
