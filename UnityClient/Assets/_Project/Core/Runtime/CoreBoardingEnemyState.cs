using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public sealed class CoreBoardingEnemyState
    {
        public const int BurnLimit = int.MaxValue, ManaLimit = 3, ThunderChainLimit = int.MaxValue;
        public const int CurseMarkLimit = int.MaxValue, TidePowerLimit = int.MaxValue, BloodLossLimit = 12;
        public CoreBoardingEnemyDeck Deck { get; internal set; }
        private List<CoreBoardingTimedStacks> playerBurnPool, enemyBurnPool;
        private int playerBurnDamage, playerBurnTurns, selfBurnDamage, selfBurnTurns;
        // Historical names remain API-compatible, but a live battle has one burn
        // pool per target, regardless of who applied it. Preview copies are detached.
        public int PlayerBurnDamage { get => BurnTotal(playerBurnPool, playerBurnDamage); internal set => SetBurn(playerBurnPool, ref playerBurnDamage, value); }
        public int PlayerBurnTurns { get => playerBurnPool == null ? playerBurnTurns : PlayerBurnDamage; internal set => playerBurnTurns = Math.Max(0, value); }
        public int SelfBurnDamage { get => BurnTotal(enemyBurnPool, selfBurnDamage); internal set => SetBurn(enemyBurnPool, ref selfBurnDamage, value); }
        public int SelfBurnTurns { get => enemyBurnPool == null ? selfBurnTurns : SelfBurnDamage; internal set => selfBurnTurns = Math.Max(0, value); }

        internal void BindBurnPools(List<CoreBoardingTimedStacks> player, List<CoreBoardingTimedStacks> enemy)
        {
            if (ReferenceEquals(playerBurnPool, player) && ReferenceEquals(enemyBurnPool, enemy)) return;
            int oldPlayer = PlayerBurnDamage, oldEnemy = SelfBurnDamage;
            playerBurnPool = player; enemyBurnPool = enemy;
            PlayerBurnDamage = (int)Math.Min(int.MaxValue, (long)BurnTotal(player, 0) + oldPlayer);
            SelfBurnDamage = (int)Math.Min(int.MaxValue, (long)BurnTotal(enemy, 0) + oldEnemy);
        }

        private static int BurnTotal(List<CoreBoardingTimedStacks> pool, int fallback)
        {
            if (pool == null) return fallback;
            long total = 0;
            foreach (var batch in pool) total = Math.Min(int.MaxValue, total + Math.Max(0, batch.Amount));
            return (int)total;
        }

        private static void SetBurn(List<CoreBoardingTimedStacks> pool, ref int fallback, int value)
        {
            fallback = Math.Max(0, value);
            if (pool == null) return;
            pool.Clear();
            if (fallback > 0) pool.Add(new CoreBoardingTimedStacks(fallback, fallback));
        }
        public int Mana { get; internal set; } = ManaLimit;
        public int ThunderChain { get; internal set; }
        public int CurseMarks => CurseDurations.Count;
        public int TidePower { get; internal set; }
        public int TideGuard { get; internal set; }
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
        public int LastCursePlayerHealth { get; internal set; }
        public int LastCurseHealthLost { get; internal set; }
        public int LastSelfBurnHealthLost { get; internal set; }

        internal CoreBoardingEnemyState CopyResources()
        {
            var copy = new CoreBoardingEnemyState {
                Deck = Deck, PlayerBurnDamage = PlayerBurnDamage, PlayerBurnTurns = PlayerBurnTurns,
                SelfBurnDamage = SelfBurnDamage, SelfBurnTurns = SelfBurnTurns, Mana = Mana,
                ThunderChain = ThunderChain, TidePower = TidePower, TideGuard = TideGuard, RetainedGuard = RetainedGuard, BloodLoss = BloodLoss
            };
            copy.CurseDurations.AddRange(CurseDurations);
            return copy;
        }

        internal void ResetTurnResults()
        {
            ExecutedActions.Clear(); LastAttackExecuted = false;
            LastAttackPlayerHealth = LastAttackHealthLost = 0;
            LastBurnPlayerHealth = LastBurnHealthLost = LastSelfBurnHealthLost = 0;
            LastCursePlayerHealth = LastCurseHealthLost = 0;
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
        public bool PlayerExecutedByCurse { get; internal set; }
        public bool EnemyExecutedByCurse { get; internal set; }
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
        public int PlayerBloodPoolLoss { get; internal set; }
        public int PlayerCurseDamage { get; internal set; }
        public bool PlayerExecutedByCurse { get; internal set; }
        public bool EnemyExecutedByCurse { get; internal set; }
        public int EnemySelfBurnDamage { get; internal set; }
        public int EnemyBloodPoolLoss { get; internal set; }
        public CoreBoardingEnemyState ResourcesAfter { get; internal set; }
    }
}
