using System;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreBattleState
    {
        // Enemy naval burning retains its existing EnemyBurnTurns/EnemyBurnDamage
        // mirrors. Boarding stores each side's unified stacks in Boarding.
        public int PlayerBurnStacks { get; set; }
    }

    public sealed partial class CoreVoyageSession
    {
        private int PrepareFireBurnApplication(bool targetPlayer, int amount, bool consume = true)
        {
            if (Battle == null || amount <= 0 || IsFireBurnPrevented(targetPlayer)) return 0;
            int health = targetPlayer ? (Battle.Stage == NavalBattleStage.Boarding ? Battle.PlayerBoardingHealth : Hull) : SchoolEnemyHealth;
            if (health <= 0) return 0;
            if (!targetPlayer && Battle.NavalExpansion.OilPlayerEndsRemaining > 0)
            {
                amount = SchoolProduct(amount, 2);
                if (consume) Battle.NavalExpansion.OilPlayerEndsRemaining = 0;
            }
            return amount;
        }

        private void ApplyFireBurn(bool targetPlayer, int amount) => ApplyFireBurnFromSource(targetPlayer, amount, true);

        private void ApplyFireBurnFromSource(bool targetPlayer, int amount, bool playerCaused)
        {
            if (Battle == null || amount <= 0) return;
            if (BActive)
            {
                if (targetPlayer) BApplyPlayerBurn(amount, false);
                else BAddEnemyBurn(amount);
                return;
            }
            amount = PrepareFireBurnApplication(targetPlayer, amount);
            if (amount <= 0) return;
            if (targetPlayer) Battle.PlayerBurnStacks = SchoolSum(Battle.PlayerBurnStacks, amount);
            else
            {
                Battle.EnemyBurnTurns = SchoolSum(Battle.EnemyBurnTurns, amount);
                Battle.EnemyBurnDamage = Battle.EnemyBurnTurns;
                if (playerCaused && Battle.Stage == NavalBattleStage.Naval && Battle.Schools.PrepareFireGuard > 0)
                {
                    AddSchoolGuard(Battle.Schools.PrepareFireGuard);
                    Battle.Schools.PrepareFireGuard = 0;
                }
            }
        }

        private int ApplyAttachedFireAfterAttack(CoreBattleCard card, bool targetInAttack = true)
        {
            if (Battle == null || card == null || Battle.PlayerFireRounds <= 0 ||
                !(card.BoardingSpec?.IsAttack == true || card.Damage > 0 ||
                  card.SchoolEffect?.Mechanic == CoreSchoolMechanic.CurseDetonate)) return 0;
            int stacks = Battle.PlayerFireRounds;
            Battle.PlayerFireRounds = 0;
            // The attack, including a fully blocked one, spends the entire reserve.
            // This is a status application, not damage or another attack.
            if (targetInAttack) ApplyFireBurn(false, stacks);
            return stacks;
        }

        private bool ApplyNavalPlayerBurn()
        {
            if (Battle == null || Battle.Stage != NavalBattleStage.Naval || Battle.PlayerBurnStacks <= 0) return false;
            int amount = Battle.PlayerBurnStacks;
            int absorbed = Math.Min(Battle.PlayerBlock, amount);
            Battle.PlayerBlock -= absorbed;
            RecordPaidTideGuard(absorbed);
            int lost = Math.Min(Hull, amount - absorbed);
            Hull -= lost;
            Battle.RecordSettlement(CoreBattleSettlementKind.Burn, true, lost, absorbed, healthAfter: Hull);
            Battle.PlayerBurnStacks--;
            Battle.LastAction += " 燃烧：格挡 -" + absorbed + "，己方船体 -" + lost + "。";
            return ResolveBattleOutcome();
        }
    }
}
