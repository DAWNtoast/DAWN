using System;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        // The inherited arena is shared by both combatants. Ownership only
        // determines which opposing turn end reduces its remaining duration.
        private void BBoardingFieldsBeginTurn(bool player)
        {
            if (!BActive) return;
            int mana = Battle.BoardingFields.Remaining(CoreNavalFieldKind.Arcane);
            if (player) Battle.Schools.Mana = SchoolSum(Battle.Schools.Mana, mana);
            else Battle.BoardingEnemy.Mana = SchoolSum(Battle.BoardingEnemy.Mana, mana);
        }

        private bool BBoardingFieldsEndTurn(bool player)
        {
            if (!BActive) return false;
            if (BResolveCurseExecutions()) return ResolveBattleOutcome();

            // Callers have already settled and reduced the actor's old burn.
            // Environmental fire is not a self-applied card effect.
            int burn = Battle.BoardingFields.Remaining(CoreNavalFieldKind.Fire);
            if (player) BApplyPlayerBurn(burn, false);
            else BAddEnemyBurn(burn);

            int marks = Battle.BoardingFields.Remaining(CoreNavalFieldKind.Curse);
            if (player)
            {
                for (int index = 0; index < marks; index++)
                    Battle.BoardingEnemy.CurseDurations.Add(int.MaxValue);
            }
            else CTBAddMarks(marks);

            if (BResolveCurseExecutions()) return ResolveBattleOutcome();
            int bloodLoss = Battle.BoardingFields.Remaining(CoreNavalFieldKind.Blood);
            if (bloodLoss > 0)
            {
                int healthBefore = player ? Battle.PlayerBoardingHealth : Battle.EnemyBoardingHealth;
                // Terrain loss is lethal and bypasses guard. It is neither an
                // active life payment nor an attack, so it grants no pool guard.
                if (player) Battle.PlayerBoardingHealth = Math.Max(0, Battle.PlayerBoardingHealth - bloodLoss);
                else
                {
                    int lost = Math.Min(Battle.EnemyBoardingHealth, bloodLoss);
                    Battle.EnemyBoardingHealth -= lost;
                    OnBoardingEnemyDamaged(0, lost);
                }
                Battle.RecordSettlement(CoreBattleSettlementKind.BloodField, player, Math.Min(healthBefore, bloodLoss),
                    healthAfter: Math.Max(0, healthBefore - bloodLoss));
                AddLog((player ? "我方" : "敌方") + "血池结算：生命 -" + bloodLoss + "（无视格挡）。");
                BResolveCurseExecutions();
                if (ResolveBattleOutcome()) return true;
            }
            Battle.BoardingFields.EndTurn(player);
            BSyncBurn();
            return false;
        }

        private void BPayLifeCost(int amount)
        {
            int paid = Math.Min(Math.Max(0, amount), Math.Max(0, Battle.PlayerBoardingHealth - 1));
            Battle.PlayerBoardingHealth -= paid;
            BApplyBloodPoolGuard(true, paid);
        }

        private void BApplyBloodPoolGuard(bool player, int paid)
        {
            if (!BActive || paid <= 0 || !Battle.BoardingFields.Contains(CoreNavalFieldKind.Blood)) return;
            int guard = CoreFieldTradeoffRules.BloodGuard(paid);
            if (player) BGuard(guard);
            else Battle.EnemyBlock = SchoolSum(Battle.EnemyBlock, CTBEnemyBlockGain(guard, true));
        }

        // Boarding inherits a shared arena, so both attacker and target stand in
        // every active inherited field. Apply combined modifiers only once.
        private int BBoardingFieldAttackDamage(int amount)
            => BBoardingFieldAttackDamage(Battle.BoardingFields, amount);

        private static int BBoardingFieldAttackDamage(CoreBoardingFieldState fields, int amount)
            => CoreFieldTradeoffRules.ApplyAttackDamage(amount,
                fields.Contains(CoreNavalFieldKind.Fire), fields.Contains(CoreNavalFieldKind.Arcane),
                fields.Contains(CoreNavalFieldKind.Curse));
    }
}
