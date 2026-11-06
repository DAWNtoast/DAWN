namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreBattleState
    {
        public bool PlayerNavalExecutedByCurse { get; internal set; }
        public bool EnemyNavalExecutedByCurse { get; internal set; }
    }

    public sealed partial class CoreVoyageSession
    {
        internal void ApplyNavalEnemyCurseMarks(int amount)
        {
            if (Battle == null || Battle.Stage == NavalBattleStage.Boarding || amount <= 0) return;
            Battle.Schools.CurseMarks = SchoolSum(Battle.Schools.CurseMarks, amount);
            Battle.Schools.CurseTurns = Battle.Schools.CurseMarks;
            Battle.Schools.CurseDamagePerMark = Battle.Schools.CurseWeaknessPercent = 0;
            CheckNavalCurseExecution();
        }

        internal void ApplyNavalPlayerCurseMarks(int amount)
        {
            if (Battle == null || Battle.Stage == NavalBattleStage.Boarding || amount <= 0) return;
            Battle.PlayerCurseMarks = SchoolSum(Battle.PlayerCurseMarks, amount);
            CheckNavalCurseExecution();
        }

        // This hook changes health only. The caller resolves the outcome before any next action,
        // avoiding a nested victory transition while a damage/card pipeline is still unwinding.
        internal bool CheckNavalCurseExecution()
        {
            if (Battle == null || Battle.Stage == NavalBattleStage.Boarding) return false;
            if (CoreCurseRules.ReachesExecutionThreshold(Battle.PlayerCurseMarks, Hull))
            {
                int executedHealth = Hull;
                Hull = 0;
                Battle.RecordSettlement(CoreBattleSettlementKind.CurseExecution, true, executedHealth, healthAfter: 0);
                Battle.PlayerNavalExecutedByCurse = true;
                AddLog("己舰咒印达到剩余生命的 " + CoreCurseRules.DefaultExecutionThresholdPercent + "%，立即被消灭。");
            }
            if (CoreCurseRules.ReachesExecutionThreshold(Battle.Schools.CurseMarks, Battle.EnemyHull,
                Battle.Schools.EnemyCurseExecutionThresholdPercent))
            {
                int executedHealth = Battle.EnemyHull;
                Battle.EnemyHull = 0;
                Battle.RecordSettlement(CoreBattleSettlementKind.CurseExecution, false, executedHealth, healthAfter: 0);
                Battle.EnemyNavalExecutedByCurse = true;
                AddLog("敌舰达到咒印消灭条件，立即被消灭。");
            }
            return Hull <= 0 || Battle.EnemyHull <= 0;
        }

        private bool CheckCardCurseExecution()
        {
            if (Battle.Stage == NavalBattleStage.Boarding)
            {
                BResolveCurseExecutions();
                return BState.PlayerExecutedByCurse || BState.EnemyExecutedByCurse;
            }
            CheckNavalCurseExecution();
            return Battle.PlayerNavalExecutedByCurse || Battle.EnemyNavalExecutedByCurse;
        }

        private bool FinishCardCurseExecution(CoreBattleCard card, int cardIndex, bool alreadyDiscarded, int actualDamage = 0)
        {
            if (!alreadyDiscarded) Battle.DiscardPile.Add(cardIndex);
            Battle.LastAction = "打出「" + card.Name + "」" +
                (actualDamage > 0 ? "；已造成 " + actualDamage + " 点实际伤害" : string.Empty) + "；咒印触发，角色立即被消灭。";
            ResolveBattleOutcome();
            return true;
        }
    }
}
