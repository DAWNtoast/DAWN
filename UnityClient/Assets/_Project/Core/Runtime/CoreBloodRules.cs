using System;

namespace ShatteredSail.Core.Gameplay
{
    public static class CoreBloodRules
    {
        public static bool IsDesperate(int health, int maximum) => maximum > 0 && (long)health * 100 <= (long)maximum * 40;
    }

    public sealed partial class CoreVoyageSession
    {
        public bool IsBattleHealingBlocked(bool player) => Battle != null &&
            (player ? Battle.NavalExpansion.PlayerHealingBanTurns : Battle.NavalExpansion.EnemyHealingBanTurns) > 0;

        public void ApplyBattleHealingBan(bool player, int turns)
        {
            if (Battle == null || turns <= 0) return;
            if (player) Battle.NavalExpansion.PlayerHealingBanTurns = Math.Max(Battle.NavalExpansion.PlayerHealingBanTurns, turns);
            else Battle.NavalExpansion.EnemyHealingBanTurns = Math.Max(Battle.NavalExpansion.EnemyHealingBanTurns, turns);
        }

        private void TickBattleHealingBan(bool player)
        {
            if (Battle == null) return;
            if (player) Battle.NavalExpansion.PlayerHealingBanTurns = Math.Max(0, Battle.NavalExpansion.PlayerHealingBanTurns - 1);
            else Battle.NavalExpansion.EnemyHealingBanTurns = Math.Max(0, Battle.NavalExpansion.EnemyHealingBanTurns - 1);
        }

        public int GetBattleCardHealthCost(CoreBattleCard card) => CoreSpecialCardRules.HealthCost(card, Battle,
            Battle == null ? (int?)null : SchoolPlayerHealth, Battle == null ? (int?)null : SchoolPlayerMaximum);

        public bool HasBloodPaidBenefit(CoreBattleCard card) => Battle != null &&
            CoreSpecialCardRules.HasBloodPaidBenefit(card, Battle, SchoolPlayerHealth, SchoolPlayerMaximum);

        private void PayBattleHealthCost(bool player, int amount)
        {
            if (Battle == null || amount <= 0) return;
            int health = Battle.Stage == NavalBattleStage.Boarding
                ? (player ? Battle.PlayerBoardingHealth : Battle.EnemyBoardingHealth)
                : (player ? Hull : Battle.EnemyHull);
            int paid = Math.Min(Math.Max(0, amount), Math.Max(0, health - 1));
            if (Battle.Stage == NavalBattleStage.Boarding)
            {
                if (player) Battle.PlayerBoardingHealth -= paid;
                else Battle.EnemyBoardingHealth -= paid;
            }
            else if (player) Hull -= paid;
            else Battle.EnemyHull -= paid;
            if (Battle.Stage == NavalBattleStage.Boarding) BApplyBloodPoolGuard(player, paid);
            else ApplyNavalBloodPoolGuard(player, paid);
        }
    }
}
