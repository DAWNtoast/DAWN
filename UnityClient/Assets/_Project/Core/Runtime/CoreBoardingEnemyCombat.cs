using System;
using System.Collections.Generic;
using System.Text;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        private static int BoardingEnemyScale(int value, int percent) =>
            CoreBattleBalance.ScaleBattleValue(value, Math.Max(0, percent) / 100m);

        private bool HasBoardingEnemyScript => BActive && Battle.Profile?.TrainingTarget == null;

        // Includes the current player window's pending end effects. Never mutates live state.
        public CoreBoardingEnemyPreview GetBoardingEnemyPreview() => HasBoardingEnemyScript
            ? RunBoardingEnemyTurn(false, true) : new CoreBoardingEnemyPreview();

        private EnemyBattlePlan BuildBoardingEnemyPlan()
        {
            CoreBoardingEnemyDeck deck = Battle.BoardingEnemy.Deck ?? CoreBoardingEnemyCatalog.ForSchool("neutral");
            return new EnemyBattlePlan { Action = EnemyBattleAction.Melee, BoardingTurn = deck.TurnAt(Battle.PhaseTurn) };
        }

        // Structured values keep AI/test clients independent of localized intent text.
        // Preview never consumes weakness, curse seals, RNG or the fixed deck position.
        public int EnemyBoardingAttackDamage
        {
            get
            {
                if (Phase != CoreDemoPhase.Battle || Battle == null || Battle.Stage != NavalBattleStage.Boarding) return 0;
                if (HasBoardingEnemyScript) return GetBoardingEnemyPreview().AttackDamage;
                return BoardingEnemyAttack(BuildEnemyPlan(), false);
            }
        }

        public int EnemyBoardingPiercingDamage
        {
            get
            {
                if (Phase != CoreDemoPhase.Battle || Battle == null || Battle.Stage != NavalBattleStage.Boarding) return 0;
                return HasBoardingEnemyScript ? GetBoardingEnemyPreview().PiercingDamage : 0;
            }
        }

        private int BoardingEnemyAttack(EnemyBattlePlan plan, bool consume)
        {
            if (plan.Attack <= 0) return 0;
            int damage = ApplyCurseBattleDamage(SchoolEnemyRawAttack(plan.Attack));
            return BActive ? BEnemyAttack(damage, consume) : damage;
        }

        private string BoardingEnemyIntent(EnemyBattlePlan plan, string enemyBurn)
        {
            CoreBoardingEnemyState state = Battle.BoardingEnemy;
            CoreBoardingEnemyDeck deck = state.Deck ?? CoreBoardingEnemyCatalog.ForSchool("neutral");
            CoreBoardingEnemyPreview preview = GetBoardingEnemyPreview();
            var text = new StringBuilder(enemyBurn);
            text.Append(deck.SchoolId == "neutral" ? "水手" : CoreWarshipDesignCatalog.SchoolName(deck.SchoolId))
                .Append(" · ").Append((Math.Max(1, Battle.PhaseTurn) - 1) % deck.Turns.Count + 1)
                .Append('/').Append(deck.Turns.Count).Append(' ').Append(plan.BoardingTurn.Name);
            string resources = GetBoardingEnemyResourceSummary();
            if (resources.Length > 0) text.Append('\n').Append(resources);
            if (preview.PlayerBurnDamage > 0) text.Append("\n己方回合末燃烧伤害 ").Append(preview.PlayerBurnDamage);
            if (preview.PlayerBloodPoolLoss > 0) text.Append("\n己方回合末血池流失生命 ").Append(preview.PlayerBloodPoolLoss).Append("（无视格挡）");
            if (preview.PlayerExecutedByCurse) text.Append(UsesSparringBattleText
                ? "\n咒印将使己方立即达到认输条件，结束切磋" : "\n咒印将立即消灭我方");
            if (preview.EnemyExecutedByCurse) text.Append(UsesSparringBattleText
                ? "\n咒印将使对方立即达到认输条件，结束切磋" : "\n咒印将立即消灭敌方");
            for (int index = 0; index < preview.Actions.Count; index++)
            {
                CoreBoardingEnemyActionResult action = preview.Actions[index];
                text.Append('\n').Append(index + 1).Append(". ").Append(action.Name);
                if (action.AttackDamage > 0) text.Append("：船员伤害 ").Append(action.AttackDamage);
                if (action.PiercingDamage > 0) text.Append("（穿盾 ").Append(action.PiercingDamage).Append('）');
                if (action.GuardGained > 0) text.Append("；格挡 +").Append(action.GuardGained);
                if (action.SelfDamage > 0) text.Append("；自损 ").Append(action.SelfDamage);
                if (action.Healed > 0) text.Append("；恢复 ").Append(action.Healed);
                if (action.Detail.Length > 0) text.Append("；").Append(action.Detail);
            }
            if (preview.Actions.Count == 0) text.Append(UsesSparringBattleText
                ? "\n回合末伤害将触发认输条件，结束切磋，敌方无法行动" : "\n回合末伤害将结束战斗，敌方无法行动");
            else if (preview.AttackDamage == 0) text.Append("\n本轮不攻击");
            if (preview.EnemySelfBurnDamage > 0) text.Append("\n敌方回合末燃烧伤害 ").Append(preview.EnemySelfBurnDamage);
            if (preview.EnemyBloodPoolLoss > 0) text.Append("\n敌方回合末血池流失生命 ").Append(preview.EnemyBloodPoolLoss).Append("（无视格挡）");
            text.Append("\n下轮：").Append(deck.TurnAt(Battle.PhaseTurn == int.MaxValue ? 1 : Battle.PhaseTurn + 1).Name);
            return text.ToString();
        }

        private void ExecuteBoardingEnemyPlan(EnemyBattlePlan plan, string intent)
        {
            CoreBoardingEnemyPreview result = RunBoardingEnemyTurn(true, false);
            Battle.LastAction = Battle.EnemyName + "：" + intent + "；格挡吸收 " + Battle.LastEnemyGuardAbsorbed +
                "，实际损失 " + result.PlayerHealthLoss + "。";
        }

        public string GetBoardingEnemyResourceSummary()
        {
            if (!HasBoardingEnemyScript) return string.Empty;
            CoreBoardingEnemyState s = Battle.BoardingEnemy;
            switch (s.Deck?.SchoolId)
            {
                case "fire": return "敌方燃烧 " + s.SelfBurnDamage + " 层\n己方燃烧 " + s.PlayerBurnDamage + " 层";
                case "arcane": return "魔力 " + s.Mana + "/" + CoreBoardingEnemyState.ManaLimit;
                case "thunder": return "连锁 " + s.ThunderChain + (Battle.BoardingFields.Contains(CoreNavalFieldKind.Thunder)
                    ? "（雷栅中不清空或消耗）" : "（敌回合开始时清空）");
                case "curse": return "对玩家咒印 " + s.CurseMarks + " 层";
                case "tide": return "潮势 " + s.TidePower + "\n保留盾 " + s.RetainedGuard;
                case "blood": return "失血 " + s.BloodLoss + "/" + CoreBoardingEnemyState.BloodLossLimit + "\n" + ((long)Battle.EnemyBoardingHealth * 100 <= (long)Battle.EnemyMaxBoardingHealth * 40 ? "绝境：攻击强化" : "生命≤40%进入绝境");
                default: return string.Empty;
            }
        }

        public string GetBoardingEnemyStateSummary() => GetBoardingEnemyResourceSummary();

        // Authored card text is independent of current resources; the live intent
        // above resolves the same card with weakness, caps, missing health and reactions.
        public string GetBoardingEnemyCardDescription(CoreBoardingEnemyCard card)
        {
            if (!HasBoardingEnemyScript || card == null) return string.Empty;
            int attack = Battle.Profile?.MeleeDamage ?? (Battle.IsBoss ? 12 : 8);
            int guard = Battle.Profile?.BoardingGuard ?? (Battle.IsBoss ? 8 : 4);
            var parts = new List<string>();
            if (card.AttackPercent > 0) parts.Add("基础伤害 " + BoardingEnemyScale(attack, card.AttackPercent));
            if (card.GuardPercent > 0) parts.Add("格挡 +" + BoardingEnemyScale(guard, card.GuardPercent));
            if (card.HealPercent > 0) parts.Add("恢复上限 " + BoardingEnemyScale(attack, card.HealPercent));
            if (card.DrainPercent > 0) parts.Add("吸血：恢复实际造成的生命伤害");
            if (card.SelfDamagePercent > 0) parts.Add("自损 " + BoardingEnemyScale(attack, card.SelfDamagePercent) + "（至少留1生命）");
            if (card.BurnPercent > 0) parts.Add("伤及生命后施加燃烧 " +
                BoardingEnemyScale(attack, card.BurnPercent) + " 层");
            string resource = null;
            switch (card.Mechanic)
            {
                case CoreBoardingEnemyMechanic.FireSelfBurn: parts.Add("自身燃烧 +" + card.ResourceAmount); break;
                case CoreBoardingEnemyMechanic.FireTransfer: parts.Add("转移最多 " + card.ResourceAmount + " 层燃烧"); break;
                case CoreBoardingEnemyMechanic.FireConsume: resource = "目标燃烧"; break;
                case CoreBoardingEnemyMechanic.ArcaneGain: parts.Add("魔力 +" + card.ResourceAmount); break;
                case CoreBoardingEnemyMechanic.ArcaneSpend: resource = "魔力"; break;
                case CoreBoardingEnemyMechanic.ThunderStrike: parts.Add("连锁 +1"); break;
                case CoreBoardingEnemyMechanic.ThunderFinisher: resource = "连锁"; break;
                case CoreBoardingEnemyMechanic.CurseMark: parts.Add("施加咒印 " + card.ResourceAmount); break;
                case CoreBoardingEnemyMechanic.CurseDetonate:
                    resource = "咒印"; parts.Add("消耗咒印时无视格挡"); break;
                case CoreBoardingEnemyMechanic.TideGuard: parts.Add("留盾至下次敌方行动，吸收伤害转为潮势"); break;
                case CoreBoardingEnemyMechanic.TideStrike: resource = "潮势"; break;
                case CoreBoardingEnemyMechanic.TideHeal:
                    parts.Add("消耗最多 " + card.ResourceAmount + " 潮势，按实际支付比例恢复"); break;
                case CoreBoardingEnemyMechanic.BloodStrike:
                case CoreBoardingEnemyMechanic.BloodHarvest:
                    resource = "失血"; parts.Add("生命≤40%时再加 " +
                        (attack * .25m).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " 伤害"); break;
            }
            if (resource != null) parts.Add("消耗最多 " + card.ResourceAmount + " " + resource +
                "，每点加 " + (attack * card.BonusPercent / 100m).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) +
                " 伤害（与基础伤害合计取整）");
            if (card.Mechanic == CoreBoardingEnemyMechanic.ThunderFinisher && Battle.BoardingFields.Contains(CoreNavalFieldKind.Thunder))
                parts.Add("雷栅中免除连锁消耗，仍按可用连锁提高伤害");
            if (card.SelfDamagePercent > 0 && Battle.BoardingFields.Contains(CoreNavalFieldKind.Blood))
                parts.Add("血池：主动支付生命时获得两倍格挡；回合末流失等于血池剩余持续时间的生命，无视格挡");
            if (card.PiercePercent > 0) parts.Add(card.PiercePercent + "%伤害穿盾");
            return string.Join("；", parts);
        }

        public string GetBoardingEnemyMechanicDescription()
        {
            if (!HasBoardingEnemyScript) return string.Empty;
            switch (Battle.BoardingEnemy.Deck?.SchoolId)
            {
                case "fire": return "燃烧在目标回合末造成当前层数的伤害，先扣格挡，再扣生命，然后减少1层。来自双方的燃烧层数合并；转移按实际层数处理。";
                case "arcane": return "敌方回合开始时法力恢复到上限3；回合中获得法力可以超出上限，下回合重新恢复到上限。蓄能、消耗数量与每点增伤以各张敌牌为准；资源不足仅按实际消耗强化。普通攻击、格挡与治疗不额外消耗法力。";
                case "thunder": return "雷霆攻击结算后积累1连锁，终结先消耗连锁提高伤害，再获得1连锁；使用非雷霆牌后清空，敌回合开始时清空。" +
                    (Battle.BoardingFields.Contains(CoreNavalFieldKind.Thunder)
                        ? "当前处于雷栅，连锁不会清空或消耗；每次增加至至少50时受到1000点伤害，先扣格挡。" : string.Empty) +
                    "每一击分别承受格挡、一次性弱化和反击，" +
                    (UsesSparringBattleText ? "任一方达到认输条件立即结束切磋。" : "任一方死亡立即停止。");
                case "curse": return "咒印层数大于或等于角色剩余生命的" + CoreCurseRules.DefaultExecutionThresholdPercent +
                    (UsesSparringBattleText ? "%时，该方立即达到认输条件，结束切磋。" : "%时，立即消灭该角色。") +
                    "咒印不会自然减少；咒术伤害无视格挡。引爆先消耗咒印，再按实际消耗层数提高伤害。";
                case "tide": return "蓄潮格挡实际吸收的伤害转为等量潮势，无上限，保留至战斗结束。留潮盾最多保留基础盾值到下一次敌行动一次。反击消耗潮势增伤；治疗按实际付出的潮势比例恢复，零潮势不恢复。";
                case "blood": return "实际受伤及自损积累失血，上限12；血刃攻击消费失血，按各张敌牌的倍率增伤。自损最多支付至剩1生命，支付后生命≤40%额外增加25%基础攻击；吸血恢复该击实际造成的生命伤害，最多恢复至生命上限。禁疗期间无法恢复生命。";
                default: return "固定三轮：攻击、格挡、攻击。";
            }
        }

        private void TickBoardingEnemyFire()
        {
            CoreBoardingEnemyState state = Battle.BoardingEnemy;
            int absorbed = Math.Min(Battle.PlayerBlock, state.PlayerBurnDamage);
            Battle.PlayerBlock -= absorbed;
            // All absorbed damage can charge tide guard, while burning still
            // does not trigger retaliation or other enemy-attack-only reactions.
            BRecordBlockedDamage(absorbed);
            int lost = Math.Min(Battle.PlayerBoardingHealth, state.PlayerBurnDamage - absorbed);
            state.LastBurnPlayerHealth = Battle.PlayerBoardingHealth;
            state.LastBurnHealthLost = lost;
            Battle.PlayerBoardingHealth -= lost;
            Battle.RecordSettlement(CoreBattleSettlementKind.Burn, true, lost, absorbed, healthAfter: Battle.PlayerBoardingHealth);
            BResolveCurseExecutions();
            if (lost > 0 && Battle.PlayerBoardingHealth > 0) CTBAddMarks(BCurseContractAmount);
            state.PlayerBurnDamage = Math.Max(0, state.PlayerBurnDamage - 1);
            state.PlayerBurnTurns = state.PlayerBurnDamage;
            if (lost > 0 || absorbed > 0) AddLog("燃烧结算：格挡 -" + absorbed + "；船员生命 -" + lost + "；剩余 " + state.PlayerBurnDamage + " 层。");
        }
    }
}
