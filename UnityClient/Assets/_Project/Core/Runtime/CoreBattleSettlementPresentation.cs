using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public enum CoreBattleSettlementKind { Burn, CurseExecution, BloodField, Recovery }
    public enum CoreBattleSettlementWindow { PlayerEnd, EnemyAction, EnemyEnd, DelayedAttack }

    /// <summary>One already committed effect. Presentation facts never execute rules or enter a save.</summary>
    public readonly struct CoreBattleSettlementFact
    {
        public readonly long Sequence;
        public readonly CoreBattleSettlementKind Kind;
        public readonly CoreBattleSettlementWindow Window;
        public readonly bool Player;
        public readonly int HealthLost, GuardLost, Healed;
        public readonly int HealthBefore, HealthAfter;
        public readonly int GuardBefore, GuardAfter;
        public CoreBattleSettlementFact(long sequence, CoreBattleSettlementKind kind, CoreBattleSettlementWindow window,
            bool player, int healthLost, int guardLost, int healed, int healthAfter = -1, int guardAfter = -1)
        {
            Sequence = sequence; Kind = kind; Window = window; Player = player;
            HealthLost = healthLost; GuardLost = guardLost; Healed = healed; HealthAfter = healthAfter;
            HealthBefore = healthAfter < 0 ? -1 : (int)Math.Min(int.MaxValue, (long)healthAfter + healthLost - healed);
            GuardAfter = guardAfter; GuardBefore = guardAfter < 0 ? -1 : (int)Math.Min(int.MaxValue, (long)guardAfter + guardLost);
        }
    }

    public sealed partial class CoreBattleState
    {
        private readonly List<CoreBattleSettlementFact> settlementFacts = new List<CoreBattleSettlementFact>(32);
        public long SettlementSequence { get; private set; }
        public long EnemyRamSequence { get; private set; }
        public NavalBoardingConnection LastEnemyRamConnection { get; private set; }
        public IReadOnlyList<CoreBattleSettlementFact> SettlementFacts => settlementFacts.AsReadOnly();
        internal CoreBattleSettlementWindow SettlementWindow;
        internal void RecordEnemyRam(NavalBoardingConnection connection)
        { LastEnemyRamConnection = connection; EnemyRamSequence++; }
        internal void RecordSettlement(CoreBattleSettlementKind kind, bool player, int healthLost = 0, int guardLost = 0, int healed = 0, int healthAfter = -1)
        {
            healthLost = Math.Max(0, healthLost); guardLost = Math.Max(0, guardLost); healed = Math.Max(0, healed);
            if (healthLost == 0 && guardLost == 0 && healed == 0) return;
            if (settlementFacts.Count == 32) settlementFacts.RemoveAt(0);
            settlementFacts.Add(new CoreBattleSettlementFact(++SettlementSequence, kind, SettlementWindow, player,
                healthLost, guardLost, healed, healthAfter, player ? PlayerBlock : EnemyBlock));
        }
    }
}
