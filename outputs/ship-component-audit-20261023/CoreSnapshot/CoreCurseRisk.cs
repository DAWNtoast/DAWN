using System;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        public const int CurseDamagePercentPerLayer = 10;
        public const int MaximumCurseDamageBonusPercent = 100;
        public const decimal CursePurificationCost = 30m;

        // Curse is an existing captain-wide saved condition, not physical cargo.
        // Keep every layer even after the combat penalty reaches its cap.
        public int CurseCount => Math.Max(0, Cargo.Curse);
        public int CurseDamageBonusPercent => Math.Min(10, CurseCount) * CurseDamagePercentPerLayer;

        // Apply once, after armor and before guard. Use integer ceiling so the
        // displayed damage and actual damage agree without floating-point drift.
        public int ApplyCurseBattleDamage(int damage)
        {
            if (damage <= 0) return 0;
            return (int)Math.Min(int.MaxValue, ((long)damage * (100 + CurseDamageBonusPercent) + 99L) / 100L);
        }

        public bool CanPurifyCurse(out string reason)
        {
            reason = Phase != CoreDemoPhase.Port ? "返回港口后才能净化诅咒。" :
                CurseCount == 0 ? "当前没有需要净化的诅咒。" :
                ProfileGold < CursePurificationCost ? "净化 1 层诅咒需要 30 金币。" : string.Empty;
            return reason.Length == 0;
        }

        public bool TryPurifyCurse(int expectedCurseCount, out string message)
        {
            if (!CanPurifyCurse(out message)) return false;
            // A callback rendered for an older count must not buy a second
            // cleansing after the first callback has already succeeded.
            if (expectedCurseCount != CurseCount)
            {
                message = "诅咒层数已变化，请刷新后重新选择净化。";
                return false;
            }
            if (!TrySpendProfileGold(CursePurificationCost, "净化 1 层诅咒"))
            {
                message = "净化 1 层诅咒需要 30 金币。";
                return false;
            }
            Cargo.Curse -= 1;
            message = "消耗 30 金币，净化诅咒 1 层；剩余 " + CurseCount +
                " 层，战斗承伤加成 +" + CurseDamageBonusPercent + "%。";
            AddLog(message);
            return true;
        }
    }
}
