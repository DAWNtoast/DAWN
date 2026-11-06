using System;

namespace ShatteredSail.Core.Gameplay
{
    public static class CoreThunderRules
    {
        public const int OverloadThreshold = 50;
        public const int OverloadDamage = 1000;

        public static bool TriggersOverload(int chain, int added) => added > 0 && chain >= OverloadThreshold;

        public static bool IsInThunderField(CoreBattleState battle, bool player)
        {
            if (battle == null) return false;
            if (battle.Stage == NavalBattleStage.Boarding)
                return battle.BoardingFields.Contains(CoreNavalFieldKind.Thunder);
            if (battle.Stage != NavalBattleStage.Naval || battle.NavalFields == null) return false;
            VoyagePoint point = player ? battle.PlayerPositionMeters : battle.EnemyPositionMeters;
            int x = (int)Math.Round(point.X / CoreVoyageSession.NavalGridCellMeters);
            int y = (int)Math.Round(point.Y / CoreVoyageSession.NavalGridCellMeters);
            return battle.NavalFields.Cells.Exists(cell => cell.Kind == CoreNavalFieldKind.Thunder && cell.X == x && cell.Y == y);
        }
    }

    public sealed partial class CoreVoyageSession
    {
        private bool CanRetainNavalThunderChain(bool player) => CoreThunderRules.IsInThunderField(Battle, player);

        private int ThunderChainFor(bool player) => player ? Battle.Schools.ThunderChain :
            BActive ? Battle.BoardingEnemy.ThunderChain : Battle.NavalFields.Enemy.ThunderChain;

        private void SetThunderChain(bool player, int amount)
        {
            if (player) Battle.Schools.ThunderChain = Math.Max(0, amount);
            else if (BActive) Battle.BoardingEnemy.ThunderChain = Math.Max(0, amount);
            else Battle.NavalFields.Enemy.ThunderChain = Math.Max(0, amount);
        }

        private void ResetNavalThunderChain(bool player)
        {
            if (Battle != null && !CanRetainNavalThunderChain(player)) SetThunderChain(player, 0);
        }

        private void ConsumeThunderChain(bool player, int amount)
        {
            if (Battle == null || CanRetainNavalThunderChain(player)) return;
            SetThunderChain(player, Math.Max(0, ThunderChainFor(player) - Math.Max(0, amount)));
        }

        private bool GainNavalThunderChain(bool player, int amount)
        {
            if (Battle == null || amount <= 0) return false;
            int before = ThunderChainFor(player);
            int after = SchoolSum(before, amount);
            SetThunderChain(player, after);
            if (!CanRetainNavalThunderChain(player) || !CoreThunderRules.TriggersOverload(after, after - before)) return false;
            if (BActive) BBoardingFieldDamage(player, CoreThunderRules.OverloadDamage);
            else
            {
                if (!NavalFieldsActive || NavalFieldImmune(FieldShip(player), CoreNavalFieldKind.Thunder, FieldOwnerTurn(player))) return false;
                NavalExpansionDirectDamage(player, CoreThunderRules.OverloadDamage, 0);
            }
            AddLog((player ? "己方" : "敌方") + "连锁达到 " + after + "，雷栅造成 " + CoreThunderRules.OverloadDamage + " 点伤害；连锁保留。");
            return ResolveBattleOutcome();
        }

        private void CompleteThunderCard(CoreBattleCard card, bool attack)
        {
            if (Battle == null || card == null) return;
            if (card.SchoolId != "thunder") ResetNavalThunderChain(true);
            else if (attack) GainNavalThunderChain(true, 1);
        }
    }
}
