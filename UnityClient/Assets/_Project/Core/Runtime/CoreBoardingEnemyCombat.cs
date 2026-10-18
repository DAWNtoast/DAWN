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
            if (preview.PlayerBurnDamage > 0) text.Append("\n己方回合末敌施灼烧 ").Append(preview.PlayerBurnDamage);
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
            if (preview.Actions.Count == 0) text.Append("\n回合末伤害将结束战斗，敌方无法行动");
            else if (preview.AttackDamage == 0) text.Append("\n本轮不攻击");
            if (preview.EnemySelfBurnDamage > 0) text.Append("\n敌方回合末自燃 ").Append(preview.EnemySelfBurnDamage);
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
                case "fire": return "自燃 " + s.SelfBurnDamage + "/" + CoreBoardingEnemyState.BurnLimit + "（" + s.SelfBurnTurns + "次）\n敌施灼烧 " + s.PlayerBurnDamage + "/" + CoreBoardingEnemyState.BurnLimit + "（" + s.PlayerBurnTurns + "次）";
                case "arcane": return "魔力 " + s.Mana + "/" + CoreBoardingEnemyState.ManaLimit;
                case "thunder": return "连锁 " + s.ThunderChain + "/" + CoreBoardingEnemyState.ThunderChainLimit + "（敌回合末清空）";
                case "curse": return "对玩家咒痕 " + s.CurseMarks + "/" + CoreBoardingEnemyState.CurseMarkLimit + "（不自动伤害）";
                case "tide": return "潮势 " + s.TidePower + "/" + CoreBoardingEnemyState.TidePowerLimit + "\n保留盾 " + s.RetainedGuard;
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
            if (card.DrainPercent > 0) parts.Add("吸血上限 " + BoardingEnemyScale(attack, card.DrainPercent));
            if (card.SelfDamagePercent > 0) parts.Add("自损 " + BoardingEnemyScale(attack, card.SelfDamagePercent) + "（至少留1生命）");
            if (card.BurnPercent > 0) parts.Add("伤及生命后施灼烧 " +
                Math.Min(CoreBoardingEnemyState.BurnLimit, BoardingEnemyScale(attack, card.BurnPercent)) + "，持续2次己方回合末");
            string resource = null;
            switch (card.Mechanic)
            {
                case CoreBoardingEnemyMechanic.FireSelfBurn: parts.Add("自燃 +" + card.ResourceAmount); break;
                case CoreBoardingEnemyMechanic.FireTransfer: parts.Add("转移最多 " + card.ResourceAmount + " 自燃，保留剩余时长"); break;
                case CoreBoardingEnemyMechanic.FireConsume: resource = "敌施灼烧"; break;
                case CoreBoardingEnemyMechanic.ArcaneGain: parts.Add("魔力 +" + card.ResourceAmount); break;
                case CoreBoardingEnemyMechanic.ArcaneSpend: resource = "魔力"; break;
                case CoreBoardingEnemyMechanic.ThunderStrike: parts.Add("连锁 +1"); break;
                case CoreBoardingEnemyMechanic.ThunderFinisher: resource = "连锁"; break;
                case CoreBoardingEnemyMechanic.CurseMark: parts.Add("施加咒痕 " + card.ResourceAmount); break;
                case CoreBoardingEnemyMechanic.CurseDetonate:
                    resource = "咒痕"; parts.Add("消耗咒痕时无视格挡"); break;
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
            if (card.PiercePercent > 0) parts.Add(card.PiercePercent + "%伤害穿盾");
            return string.Join("；", parts);
        }

        public string GetBoardingEnemyMechanicDescription()
        {
            if (!HasBoardingEnemyScript) return string.Empty;
            switch (Battle.BoardingEnemy.Deck?.SchoolId)
            {
                case "fire": return "自燃与敌施灼烧各上限6，分别在敌/己方回合末结算，最多持续2次。自燃可转移到玩家，保留剩余时长；焚尽消耗玩家身上的敌施灼烧，按各张敌牌的数值增伤。点燃攻击须造成实际生命损失。";
                case "arcane": return "魔力上限3，可跨回合保留。蓄能、消费数量与每点增伤以各张敌牌为准；资源不足仅按实际消费强化。普通攻击、格挡与治疗不额外消耗魔力。";
                case "thunder": return "雷击按出牌顺序积累连锁，终结消耗本轮连锁提高伤害；上限6，敌回合末清空。普通攻击不积累连锁。每一击分别承受格挡、一次性弱化和反击，任一方死亡立即停止。";
                case "curse": return "对玩家叠加最多6层咒痕，每批持续3次敌方回合末，新增不刷新旧批。咒痕不自动造成伤害；引爆消费最早到期咒痕，按各张敌牌的倍率增伤并穿透格挡。无咒痕时执行普通攻击。";
                case "tide": return "只有实际格挡吸收的伤害才转为潮势，上限12，紧接的敌方行动可消费，敌回合末清空。留潮盾最多保留基础盾值到下一次敌行动一次。反击消耗潮势增伤；治疗按实际付出的潮势比例恢复，零潮势不恢复。";
                case "blood": return "实际受伤及自损积累失血，上限12；血刃攻击消费失血，按各张敌牌的倍率增伤。自损最多支付至剩1生命，支付后生命≤40%额外增加25%基础攻击；吸血不超过该击实际生命伤害、牌面吸血上限和缺失生命。普通治疗只受缺失生命限制。";
                default: return "固定三轮：攻击、格挡、攻击。";
            }
        }

        private void TickBoardingEnemyFire()
        {
            CoreBoardingEnemyState state = Battle.BoardingEnemy;
            if (state.PlayerBurnTurns <= 0) return;
            int lost = Math.Min(Battle.PlayerBoardingHealth, state.PlayerBurnDamage);
            state.LastBurnPlayerHealth = Battle.PlayerBoardingHealth;
            state.LastBurnHealthLost = lost;
            Battle.PlayerBoardingHealth -= lost;
            state.PlayerBurnTurns--;
            if (state.PlayerBurnTurns == 0) state.PlayerBurnDamage = 0;
            AddLog("敌方点燃的灼烧结算：船员生命 -" + lost + "；剩余 " + state.PlayerBurnTurns + " 次。");
        }
    }
}
