using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ShatteredSail.Core.Gameplay
{
    public enum CoreBattleIntentKind
    {
        Cannon, Melee, Ram, Guard, Heal, Load, Move, Fire, Arcane, Thunder, Curse, Tide, Blood, Wait
    }

    /// <summary>One visible part of the next enemy action; Sequence groups compound card effects.</summary>
    public sealed class CoreBattleIntent
    {
        public CoreBattleIntent(string id, CoreBattleIntentKind kind, string name, int? amount, string tooltip, int sequence = 0)
        {
            Id = id; Kind = kind; Name = name; Amount = amount;
            Value = amount.HasValue ? amount.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
            Tooltip = name + "\n" + tooltip; Sequence = sequence;
        }
        public string Id { get; }
        public CoreBattleIntentKind Kind { get; }
        public string Name { get; }
        public int? Amount { get; }
        public string Value { get; }
        public string Tooltip { get; }
        public int Sequence { get; }
        public bool IsAttack => Kind == CoreBattleIntentKind.Cannon || Kind == CoreBattleIntentKind.Melee || Kind == CoreBattleIntentKind.Ram;
    }

    public static class CoreBattleIntentPresentation
    {
        public static IReadOnlyList<CoreBattleIntent> Build(CoreVoyageSession session) =>
            session == null ? Array.Empty<CoreBattleIntent>() : session.BuildBattleIntentPresentation();
    }

    public sealed partial class CoreVoyageSession
    {
        // Stay beside the rules assembly so the UI never parses localized intent text,
        // copies damage formulas, consumes a debuff, or advances the enemy decision RNG.
        internal IReadOnlyList<CoreBattleIntent> BuildBattleIntentPresentation()
        {
            if (Phase != CoreDemoPhase.Battle || Battle == null) return Array.Empty<CoreBattleIntent>();
            var result = new List<CoreBattleIntent>();
            EnemyBattlePlan plan = BuildEnemyPlan();
            if (plan.BoardingTurn != null)
            {
                AddBoardingIntentPresentation(result, plan.BoardingTurn);
                return result.AsReadOnly();
            }

            bool boarding = Battle.Stage == NavalBattleStage.Boarding;
            bool preparing = Battle.Stage == NavalBattleStage.Preparation;
            if (!boarding && !preparing && HasSchoolFire && SchoolEnemyHealth <= Battle.EnemyBurnDamage)
            {
                result.Add(new CoreBattleIntent("enemy_cannot_act", CoreBattleIntentKind.Wait, "无法行动", null,
                    "敌方行动前的燃烧将造成 " + Battle.EnemyBurnDamage + " 点伤害并击败敌舰，因此不会行动。"));
                return result.AsReadOnly();
            }

            CoreNavalMovementFieldPreview fields = NavalFieldsActive ? GetEnemyNavalFieldPreview(plan.Move) : null;
            string terrain = fields == null || string.IsNullOrEmpty(fields.Summary) ? string.Empty : "\n" + fields.Summary;
            if (plan.Move > 0f)
            {
                int steps = fields == null ? (int)(QuantizeNavalEnemyTravel(plan.Move) / NavalGridCellMeters) : fields.ActiveSteps;
                result.Add(new CoreBattleIntent("enemy_move", CoreBattleIntentKind.Move, "机动", steps,
                    "预计主动移动 " + steps + " 格（每格 100 米）。海流可能额外推动，雷栅可能提前终止移动。" + terrain));
            }
            if (fields != null && fields.Sinks)
            {
                result.Add(new CoreBattleIntent("enemy_cannot_act", CoreBattleIntentKind.Wait, "无法行动", null,
                    "敌舰将在行动前或移动途中沉没，不会继续获得格挡、装填、治疗或攻击。" + terrain));
                return result.AsReadOnly();
            }

            int guard = boarding ? CTBEnemyBlockGain(plan.Guard, false) : fields == null
                ? NavalExpansionEnemyBlockGain(plan.Guard)
                : SchoolSum(fields.FinalBlock, fields.RuneClampConsumed ? Math.Max(0, plan.Guard) : NavalExpansionEnemyBlockGain(plan.Guard));
            if (plan.Guard > 0 || guard > 0)
                result.Add(new CoreBattleIntent("enemy_guard", CoreBattleIntentKind.Guard, "防御", guard,
                    "本次行动后的格挡预计为 " + guard + "；已计入当前减盾效果和移动途中获得的护盾。普通旧格挡在行动开始时清除。" + terrain));
            if (plan.Load > 0)
            {
                int load = Math.Min(Math.Max(0, 12 - Battle.EnemyLoadedShot), NavalExpansionEnemyLoadGain(plan.Load));
                result.Add(new CoreBattleIntent("enemy_load", CoreBattleIntentKind.Load, "装填", load,
                    "装填加伤增加 " + load + "，总装填上限 12；用于之后的炮击，开炮后消耗。已计入装填封印。"));
            }
            if (plan.Repair > 0)
            {
                int health = boarding ? Battle.EnemyBoardingHealth : Battle.EnemyHull;
                if (!boarding && !preparing && HasSchoolFire) health = Math.Max(0, health - Battle.EnemyBurnDamage);
                if (fields != null) health = Math.Max(0, health - fields.HullDamage);
                int maximum = boarding ? Battle.EnemyMaxBoardingHealth : Battle.EnemyMaxHull;
                int heal = Math.Min(Math.Max(0, maximum - health), plan.Repair);
                if (!boarding && Battle.NavalExpansion.EnemyHealingHalved) heal /= 2;
                result.Add(new CoreBattleIntent("enemy_heal", CoreBattleIntentKind.Heal, "治疗", heal,
                    "本次行动预计恢复 " + heal + " 点" + (boarding ? "船员生命" : "船体") +
                    "；基础恢复上限 " + plan.Repair + "，受缺失生命及禁疗限制。"));
            }

            switch (plan.Action)
            {
                case EnemyBattleAction.Cannon:
                    bool inRange = fields == null ? NavalCannonTargetInRange :
                        NavalCannonInRange(Battle.PlayerPositionMeters, fields.FinalPositionMeters);
                    if (!inRange)
                        result.Add(new CoreBattleIntent("enemy_out_of_range", CoreBattleIntentKind.Wait, "无法开炮", null,
                            "机动后的目标仍超出 4 格炮击范围，本轮不会开炮。" + terrain));
                    else
                    {
                        int attack = EnemyCannonDamage(plan.Attack, false, fields?.AttackBonus);
                        result.Add(new CoreBattleIntent("enemy_cannon", CoreBattleIntentKind.Cannon, "炮击", attack,
                            "预计炮击伤害 " + attack + "（装甲后、格挡前）。已计入装填、地形强化、航行诅咒和脉冲弱化；玩家格挡可继续吸收。" + terrain));
                    }
                    break;
                case EnemyBattleAction.Melee:
                    int melee = ApplyCurseBattleDamage(SchoolEnemyRawAttack(plan.Attack));
                    if (boarding) melee = BEnemyAttack(melee, false);
                    result.Add(new CoreBattleIntent("enemy_melee", CoreBattleIntentKind.Melee, "白刃攻击", melee,
                        "预计船员伤害 " + melee + "（格挡前）；已计入航行诅咒与当前一次性弱化，玩家格挡可继续吸收。"));
                    break;
                case EnemyBattleAction.Ram:
                    RamDamage(Battle.EnemyBowQuality, Battle.PlayerHullQuality, out int ram, out int recoil);
                    ram = NavalExpansionEnemyAttackDamage(ApplyCurseBattleDamage(SchoolEnemyRawAttack(ram)));
                    result.Add(new CoreBattleIntent("enemy_ram", CoreBattleIntentKind.Ram, "冲撞", ram,
                        "己舰承受 " + ram + " 点船体伤害，敌舰反伤 " + recoil + "，均穿透格挡。双方存活则进入接舷，敌方先手攻击。航行离开相邻格或改变到达朝向可避让。"));
                    int opening = ApplyCurseBattleDamage(plan.Attack);
                    result.Add(new CoreBattleIntent("enemy_boarding_first_strike", CoreBattleIntentKind.Melee, "接舷先手", opening,
                        "冲撞后双方仍存活时，敌方先手造成 " + opening + " 点船员伤害（接舷掩护前）；船体格挡不会转入接舷。"));
                    break;
            }
            if (result.Count == 0)
                result.Add(new CoreBattleIntent("enemy_wait", CoreBattleIntentKind.Wait,
                    preparing ? "准备" : "等待", null, "本轮不攻击。"));
            return result.AsReadOnly();
        }

        private void AddBoardingIntentPresentation(List<CoreBattleIntent> result, CoreBoardingEnemyTurn turn)
        {
            CoreBoardingEnemyPreview preview = GetBoardingEnemyPreview();
            CoreBoardingEnemyState previous = Battle.BoardingEnemy;
            for (int index = 0; index < preview.Actions.Count; index++)
            {
                CoreBoardingEnemyActionResult action = preview.Actions[index];
                CoreBoardingEnemyCard card = turn.Cards[index];
                string id = "enemy_boarding_" + index + "_" + action.CardId;
                string detail = BoardingIntentDetail(action, index) + "\n规则：" + GetBoardingEnemyCardDescription(card);
                int before = result.Count;
                if (card.GuardPercent > 0)
                    result.Add(new CoreBattleIntent(id + "_guard", CoreBattleIntentKind.Guard, card.Name + " · 格挡",
                        action.GuardGained, detail, index));
                if (card.HealPercent > 0 || action.Healed > 0)
                    result.Add(new CoreBattleIntent(id + "_heal", CoreBattleIntentKind.Heal, card.Name + " · 治疗",
                        action.Healed, detail, index));
                if (card.AttackPercent > 0 || action.RawAttack > 0)
                    result.Add(new CoreBattleIntent(id + "_attack", CoreBattleIntentKind.Melee, card.Name,
                        action.AttackDamage, detail, index));
                if (result.Count == before)
                {
                    CoreBattleIntentKind kind = CoreBattleIntentKind.Wait;
                    int? amount = null;
                    CoreBoardingEnemyState after = action.ResourcesAfter;
                    switch (card.Mechanic)
                    {
                        case CoreBoardingEnemyMechanic.FireSelfBurn:
                            kind = CoreBattleIntentKind.Fire; amount = Math.Max(0, after.SelfBurnDamage - previous.SelfBurnDamage); break;
                        case CoreBoardingEnemyMechanic.FireTransfer:
                            kind = CoreBattleIntentKind.Fire; amount = Math.Max(0, previous.SelfBurnDamage - after.SelfBurnDamage); break;
                        case CoreBoardingEnemyMechanic.ArcaneGain:
                            kind = CoreBattleIntentKind.Arcane; amount = Math.Max(0, after.Mana - previous.Mana); break;
                        case CoreBoardingEnemyMechanic.CurseMark:
                            kind = CoreBattleIntentKind.Curse; amount = Math.Max(0, after.CurseMarks - previous.CurseMarks); break;
                        case CoreBoardingEnemyMechanic.TideGuard:
                            kind = CoreBattleIntentKind.Tide; amount = after.RetainedGuard; break;
                        case CoreBoardingEnemyMechanic.ThunderStrike:
                        case CoreBoardingEnemyMechanic.ThunderFinisher: kind = CoreBattleIntentKind.Thunder; break;
                        case CoreBoardingEnemyMechanic.BloodStrike:
                        case CoreBoardingEnemyMechanic.BloodHarvest: kind = CoreBattleIntentKind.Blood; break;
                    }
                    result.Add(new CoreBattleIntent(id, kind, card.Name, amount, detail, index));
                }
                previous = action.ResourcesAfter;
            }
            if (result.Count == 0)
                result.Add(new CoreBattleIntent("enemy_cannot_act", CoreBattleIntentKind.Wait, "无法行动", null,
                    "当前玩家回合末效果将结束战斗，敌方不会行动。"));
        }

        private static string BoardingIntentDetail(CoreBoardingEnemyActionResult action, int index)
        {
            var text = new StringBuilder("行动 ").Append(index + 1).Append("：").Append(action.Name).Append('。');
            if (action.RawAttack > 0)
            {
                text.Append("\n攻击伤害 ").Append(action.AttackDamage).Append("（格挡前，已计入诅咒与一次性弱化）");
                if (action.PiercingDamage > 0) text.Append("，其中 ").Append(action.PiercingDamage).Append(" 点穿透格挡");
                text.Append("；当前格挡预计吸收 ").Append(action.AbsorbedDamage).Append("，预计实际失去 ").Append(action.HealthLost).Append(" 点生命。");
            }
            if (action.GuardGained > 0) text.Append("\n获得 ").Append(action.GuardGained).Append(" 点格挡。");
            if (action.Healed > 0) text.Append("\n预计恢复 ").Append(action.Healed).Append(" 点生命（含实际吸血，已受缺失生命限制）。");
            if (action.SelfDamage > 0) text.Append("\n支付自身生命 ").Append(action.SelfDamage).Append("。");
            if (!string.IsNullOrEmpty(action.Detail)) text.Append('\n').Append(action.Detail).Append('。');
            text.Append("\n按顺序行动；回合末效果、格挡与反击改变时，预览会更新。");
            return text.ToString();
        }
    }
}
