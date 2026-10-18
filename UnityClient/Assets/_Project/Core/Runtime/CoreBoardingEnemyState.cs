using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public sealed class CoreBoardingEnemyState
    {
        public const int BurnLimit = 6, ManaLimit = 3, ThunderChainLimit = 6;
        public const int CurseMarkLimit = 6, TidePowerLimit = 12, BloodLossLimit = 12;
        public CoreBoardingEnemyDeck Deck { get; internal set; }
        public int PlayerBurnDamage { get; internal set; }
        public int PlayerBurnTurns { get; internal set; }
        public int SelfBurnDamage { get; internal set; }
        public int SelfBurnTurns { get; internal set; }
        public int Mana { get; internal set; }
        public int ThunderChain { get; internal set; }
        public int CurseMarks => CurseDurations.Count;
        public int TidePower { get; internal set; }
        public int RetainedGuard { get; internal set; }
        public int BloodLoss { get; internal set; }
        internal readonly List<int> CurseDurations = new List<int>();
        internal readonly List<CoreBoardingEnemyActionResult> ExecutedActions = new List<CoreBoardingEnemyActionResult>();
        public IReadOnlyList<CoreBoardingEnemyActionResult> LastActions => ExecutedActions.AsReadOnly();
        public bool LastAttackExecuted { get; internal set; }
        public int LastAttackPlayerHealth { get; internal set; }
        public int LastAttackHealthLost { get; internal set; }
        public int LastBurnPlayerHealth { get; internal set; }
        public int LastBurnHealthLost { get; internal set; }
        public int LastSelfBurnHealthLost { get; internal set; }

        internal CoreBoardingEnemyState CopyResources()
        {
            var copy = new CoreBoardingEnemyState {
                Deck = Deck, PlayerBurnDamage = PlayerBurnDamage, PlayerBurnTurns = PlayerBurnTurns,
                SelfBurnDamage = SelfBurnDamage, SelfBurnTurns = SelfBurnTurns, Mana = Mana,
                ThunderChain = ThunderChain, TidePower = TidePower, RetainedGuard = RetainedGuard, BloodLoss = BloodLoss
            };
            copy.CurseDurations.AddRange(CurseDurations);
            return copy;
        }

        internal void ResetTurnResults()
        {
            ExecutedActions.Clear(); LastAttackExecuted = false;
            LastAttackPlayerHealth = LastAttackHealthLost = 0;
            LastBurnPlayerHealth = LastBurnHealthLost = LastSelfBurnHealthLost = 0;
        }

        internal void RecordAttack(int health, int lost)
        {
            if (!LastAttackExecuted) LastAttackPlayerHealth = health;
            LastAttackExecuted = true;
            LastAttackHealthLost = (int)Math.Min(int.MaxValue, (long)LastAttackHealthLost + Math.Min(Math.Max(0, health), Math.Max(0, lost)));
        }
    }

    public sealed class CoreBoardingEnemyActionResult
    {
        public string CardId { get; internal set; }
        public string Name { get; internal set; }
        public string Detail { get; internal set; }
        public int RawAttack { get; internal set; }
        public int AttackDamage { get; internal set; }
        public int PiercingDamage { get; internal set; }
        public int AbsorbedDamage { get; internal set; }
        public int HealthLost { get; internal set; }
        public int GuardGained { get; internal set; }
        public int Healed { get; internal set; }
        public int SelfDamage { get; internal set; }
        public int PlayerHealthBefore { get; internal set; }
        public int PlayerHealthAfter { get; internal set; }
        public int PlayerGuardBefore { get; internal set; }
        public int PlayerGuardAfter { get; internal set; }
        public int EnemyHealthBefore { get; internal set; }
        public int EnemyHealthAfter { get; internal set; }
        public int EnemyGuardBefore { get; internal set; }
        public int EnemyGuardAfter { get; internal set; }
        public CoreBoardingEnemyState ResourcesAfter { get; internal set; }
    }

    public sealed class CoreBoardingEnemyPreview
    {
        internal readonly List<CoreBoardingEnemyActionResult> Steps = new List<CoreBoardingEnemyActionResult>();
        public IReadOnlyList<CoreBoardingEnemyActionResult> Actions => Steps.AsReadOnly();
        public int AttackDamage { get; internal set; }
        public int PiercingDamage { get; internal set; }
        public int PlayerHealthLoss { get; internal set; }
        public int PlayerBurnDamage { get; internal set; }
        public int EnemySelfBurnDamage { get; internal set; }
        public CoreBoardingEnemyState ResourcesAfter { get; internal set; }
    }
}
