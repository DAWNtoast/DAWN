using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        private int NavalBossAttack(EnemyBattlePlan plan, CoreNavalMovementFieldPreview preview = null)
        {
            CoreNavalBossMove move = plan.BossMove;
            if (move == null) return plan.Attack;
            CoreNavalFieldShipState ship = preview?.FinalShip ?? Battle.NavalFields.Enemy;
            int attack = SchoolSum(SchoolSum(plan.Attack, move.PiercingDamage), Battle.EnemyLoadedShot);
            attack = SchoolSum(attack, SchoolProduct(ship.Mana, move.ManaDamagePerPoint));
            attack = SchoolSum(attack, (int)Math.Min(int.MaxValue, (long)Battle.EnemyTidePower * move.TideDamagePercent / 100));
            if (ship.ThunderChain >= 5) attack = SchoolSum(attack, move.ChainFiveBonus);
            if (ship.ThunderChain >= 10) attack = SchoolSum(attack, move.ChainTenBonus);
            int health = Math.Max(0, Battle.EnemyHull - (preview?.HullDamage ?? 0));
            if (move.BloodCost > 0 && health > move.BloodCost) attack = SchoolSum(attack, move.BloodDamageBonus);
            return attack;
        }

        private int NavalBossPiercing(EnemyBattlePlan plan, int attack) =>
            Math.Min(Math.Max(0, attack), Math.Max(0, plan.BossMove?.PiercingDamage ?? 0));

        private bool NavalBossPaymentExecutesActor(EnemyBattlePlan plan, CoreNavalMovementFieldPreview preview)
        {
            // Execution rejects an out-of-range shot before paying its optional
            // life cost. Movement previews must preserve that same ordering.
            if (plan.Action == EnemyBattleAction.Cannon && !NavalCannonInRange(
                NavalPlayerPositionForEnemyPlan, preview?.FinalPositionMeters ?? Battle.EnemyPositionMeters)) return false;
            int cost = plan.BossMove?.BloodCost ?? 0;
            int health = Math.Max(0, Battle.EnemyHull - (preview?.HullDamage ?? 0));
            return cost > 0 && health > cost && CoreCurseRules.ReachesExecutionThreshold(
                Battle.Schools.CurseMarks, health - cost, Battle.Schools.EnemyCurseExecutionThresholdPercent);
        }

        // Quote the guard held after the action, before end-turn burn. The blood
        // rebate follows ordinary guard and pays RuneClamp only if still unused.
        // This is separate from damage quotes: no unspent life becomes attack power.
        private int NavalEnemyActionGuard(EnemyBattlePlan plan, CoreNavalMovementFieldPreview preview,
            NavalFieldPreviewContext context = null)
        {
            if (preview?.Sinks == true) return 0;
            bool clampConsumed = preview?.RuneClampConsumed == true;
            int guard = SchoolSum(preview?.FinalBlock ?? 0, clampConsumed
                ? Math.Max(0, plan.Guard) : NavalExpansionEnemyBlockGain(plan.Guard));
            int cost = plan.BossMove?.BloodCost ?? 0;
            if (!NavalFieldsActive || cost <= 0 || preview?.EntersBoarding == true) return guard;
            VoyagePoint position = preview?.FinalPositionMeters ?? Battle.EnemyPositionMeters;
            if (context == null)
            {
                context = CreateNavalFieldPreviewContext(false);
                PreviewNavalBossPlacedField(context.State, position, context.PlayerPosition);
            }
            if (plan.Action == EnemyBattleAction.Cannon && !NavalCannonInRange(context.PlayerPosition, position)) return guard;
            int health = Math.Max(0, context.Hull - (preview?.HullDamage ?? 0));
            if (health <= cost || CoreCurseRules.ReachesExecutionThreshold(Battle.Schools.CurseMarks,
                health - cost, Battle.Schools.EnemyCurseExecutionThresholdPercent) ||
                !NavalTradeoffField(false, CoreNavalFieldKind.Blood, context.State, position)) return guard;
            int poolGuard = CoreFieldTradeoffRules.BloodGuard(cost);
            clampConsumed |= plan.Guard > 0;
            return SchoolSum(guard, clampConsumed ? poolGuard : NavalExpansionEnemyBlockGain(poolGuard));
        }

        private void HealNavalBossAttack(EnemyBattlePlan plan, int actualHealthLost)
        {
            int percent = plan.BossMove?.LifeStealPercent ?? 0;
            if (percent <= 0 || actualHealthLost <= 0 || Battle.EnemyHull <= 0) return;
            int amount = (int)Math.Min(int.MaxValue, (long)actualHealthLost * percent / 100);
            Battle.EnemyHull = SchoolSum(Battle.EnemyHull, NavalExpansionHealing(false, amount));
        }

        private void RecordEnemyTidalAbsorption(int absorbed)
        {
            if (Battle == null || Battle.Stage != NavalBattleStage.Naval || absorbed <= 0) return;
            int paid = Math.Min(Math.Max(0, Battle.EnemyTidalGuard), absorbed);
            Battle.EnemyTidalGuard -= paid;
            Battle.EnemyTidePower = SchoolSum(Battle.EnemyTidePower, paid);
        }

        // Called after movement/range validation. Intent queries never enter this path.
        private bool BeginNavalBossMove(EnemyBattlePlan plan)
        {
            CoreNavalBossMove move = plan.BossMove;
            if (move == null) return false;
            CoreNavalFieldShipState ship = Battle.NavalFields.Enemy;
            ship.ManaLimit = SchoolSum(ship.ManaLimit, move.ManaLimitGain);
            ship.Mana = SchoolSum(ship.Mana, move.ManaGain);
            PlaceNavalBossField(move);
            Battle.EnemyTidalGuard = Math.Min(Battle.EnemyBlock, Math.Max(0, move.TidalGuard));
            if (move.BloodCost > 0 && Battle.EnemyHull > move.BloodCost)
            {
                PayBattleHealthCost(false, move.BloodCost);
                if (CheckNavalCurseExecution()) return ResolveBattleOutcome();
            }
            return false;
        }

        private void CompleteNavalBossMove(EnemyBattlePlan plan, int actualHealthLost)
        {
            CoreNavalBossMove move = plan.BossMove;
            if (move == null || Battle.Stage != NavalBattleStage.Naval || Battle.EnemyHull <= 0) return;
            Battle.EnemyNavalBossActionsCompleted = SchoolSum(Battle.EnemyNavalBossActionsCompleted, 1);
            if (move.SpendMana) Battle.NavalFields.Enemy.Mana = 0;
            if (move.SpendTide) Battle.EnemyTidePower = 0;
            if (Hull > 0)
            {
                ApplyFireBurnFromSource(true, move.Burn, false);
                ApplyNavalPlayerCurseMarks(move.Marks);
            }
            if (ResolveBattleOutcome()) return;
            if (move.ChainGain > 0 && GainNavalThunderChain(false, move.ChainGain)) return;
            if (move.SpendChain) ConsumeThunderChain(false, ThunderChainFor(false));
        }

        private string NavalBossMoveDescription(EnemyBattlePlan plan, CoreNavalMovementFieldPreview preview = null)
        {
            CoreNavalBossMove move = plan.BossMove;
            if (move == null) return string.Empty;
            if (NavalBossPaymentExecutesActor(plan, preview))
                return move.Name + "：支付 " + move.BloodCost + " 点船体后会触发已有咒印的即时消灭，无法继续攻击。";
            var parts = new List<string> { "循环 " + move.CycleStep + "/" + move.CycleLength + " · " + move.StageName + " · " + move.Name };
            if (move.Burn > 0) parts.Add("施加 " + move.Burn + " 层燃烧");
            if (move.Marks > 0) parts.Add("施加 " + move.Marks + " 层咒印，即时判定消灭");
            if (move.PiercingDamage > 0) parts.Add("其中至多 " + move.PiercingDamage + " 点伤害无视格挡");
            if (move.ManaLimitGain > 0) parts.Add("本场法力上限 +" + move.ManaLimitGain + "（不立即补满）");
            if (move.ManaGain > 0) parts.Add("获得 " + move.ManaGain + " 点法力");
            if (move.SpendMana) parts.Add("消耗全部法力增伤");
            if (move.TidalGuard > 0) parts.Add("蓄潮格挡：实际吸收伤害时获得等量潮势");
            if (move.SpendTide) parts.Add("消耗全部潮势反击（当前 " + Battle.EnemyTidePower + "）");
            if (move.FieldKind.HasValue) parts.Add(NavalBossFieldDescription(move,
                preview?.FinalPositionMeters ?? Battle.EnemyPositionMeters, NavalPlayerPositionForEnemyPlan));
            if (move.ChainGain > 0) parts.Add("行动后连锁 +" + move.ChainGain);
            int chainAfter = SchoolSum((preview?.FinalShip ?? Battle.NavalFields.Enemy).ThunderChain, move.ChainGain);
            if (move.ChainGain > 0 && chainAfter >= CoreThunderRules.OverloadThreshold)
                parts.Add("行动后连锁达到 " + chainAfter + "；若仍位于雷栅且没有伤害免疫，将受到 1000 点伤害（先扣格挡）");
            if (move.SpendChain) parts.Add("终结后消耗全部连锁，雷栅内免耗");
            if (move.BloodCost > 0)
            {
                int health = Math.Max(0, Battle.EnemyHull - (preview?.HullDamage ?? 0));
                parts.Add(health > move.BloodCost ? "主动消耗 " + move.BloodCost + " 点船体换取增伤" : "船体不足，不支付生命且不获得付血增伤");
            }
            if (move.LifeStealPercent > 0) parts.Add("吸血：恢复实际扣除船体的 " + move.LifeStealPercent + "%（受禁疗限制）");
            parts.Add("下一步：" + move.NextName + "（" + move.NextStageName +
                (move.NextStageName == "回整" ? "；近身船首可能冲撞" : string.Empty) + "）");
            return string.Join("；", parts);
        }

        private void AddNavalBossIntent(List<CoreBattleIntent> result, EnemyBattlePlan plan, CoreNavalMovementFieldPreview preview)
        {
            if (plan.BossMove == null) return;
            CoreBattleIntentKind kind;
            switch (plan.BossMove.SchoolId)
            {
                case "fire": kind = CoreBattleIntentKind.Fire; break;
                case "arcane": kind = CoreBattleIntentKind.Arcane; break;
                case "curse": kind = CoreBattleIntentKind.Curse; break;
                case "tide": kind = CoreBattleIntentKind.Tide; break;
                case "thunder": kind = CoreBattleIntentKind.Thunder; break;
                case "blood": kind = CoreBattleIntentKind.Blood; break;
                default: kind = CoreBattleIntentKind.Wait; break;
            }
            result.Add(new CoreBattleIntent("enemy_boss_cycle", kind, plan.BossMove.Name, null, NavalBossMoveDescription(plan, preview)));
        }
    }
}
