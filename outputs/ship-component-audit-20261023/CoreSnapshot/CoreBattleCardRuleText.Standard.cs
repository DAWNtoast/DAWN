using System.Text.RegularExpressions;

namespace ShatteredSail.Core.Gameplay
{
    internal static partial class CoreBattleCardRuleText
    {
        private static string RemoveStandardRules(string text, CoreBattleCard card)
        {
            if (card.BoardingSpec != null) return text;

            // Action-point controls and ordinary guard behavior belong to the battle rules.
            text = Remove(text, "，用于海图移动或转向", "；保留当前航向与距离",
                "拖动规划航线；到达余量可用于接舷或冲撞，普通停船后清空。",
                "两舰平行且舷侧相邻（100 米）时，消耗 1 行动点接舷；同向或反向均可，首尾相邻不可接舷。",
                "；格挡不跨回合", "；准备阶段的格挡保留到首轮炮战", "；准备阶段的格挡保留到首轮海战");

            if (card.Action == NavalBattleCardAction.Load || card.Action == NavalBattleCardAction.PrecisionLoad)
                text = Remove(text, "；开炮另付弹药并清空增益", "；开炮时消耗增益", "；开炮后清空");
            if (card.Action == NavalBattleCardAction.Sabotage && card.PowderCost == 0 && card.CannonballCost == 0)
                text = Remove(text, "，无需弹药", "；无需弹药");

            CoreSchoolMechanic mechanic = card.SchoolEffect?.Mechanic ?? CoreSchoolMechanic.None;
            if (CoreSpecialCardRules.IsDoctrine(card))
            {
                text = Remove(text, "，接舷或战斗结束清除");
                switch (mechanic)
                {
                    case CoreSchoolMechanic.SpecialArcaneDoctrine:
                    case CoreSchoolMechanic.SpecialThunderDoctrine:
                        text = Remove(text, "，不超过上限");
                        break;
                    case CoreSchoolMechanic.SpecialTideDoctrine:
                        text = Remove(text, "，上限 20");
                        break;
                }
                // The curse doctrine's six-mark cap is its own effect, not the curse resource limit.
                return text;
            }

            switch (mechanic)
            {
                case CoreSchoolMechanic.FireLoad:
                    text = Remove(text, "，带入炮战");
                    break;
                case CoreSchoolMechanic.FireIgniteOrExtend:
                case CoreSchoolMechanic.FireExtend:
                    text = Remove(text, "，上限 3 回合");
                    break;
                case CoreSchoolMechanic.ArcaneCharge:
                    text = Remove(text, "，不超过当前法力上限", "，不超过上限");
                    break;
                case CoreSchoolMechanic.ArcaneCapacity:
                    text = Remove(text, "，不恢复法力");
                    break;
                case CoreSchoolMechanic.ArcaneTemporary:
                    text = Remove(text, "，本回合结束清除");
                    break;
                case CoreSchoolMechanic.CurseMark:
                    text = Remove(text, "，不造成直接伤害");
                    break;
                case CoreSchoolMechanic.CurseWard:
                    text = Remove(text, "，无数值上限");
                    break;
                case CoreSchoolMechanic.TideWard:
                    text = RemovePaidTideGuardDefinition(text);
                    break;
                case CoreSchoolMechanic.ThunderChain:
                case CoreSchoolMechanic.ThunderFinish:
                    text = Remove(text,
                        "；本次雷霆攻击后连锁 +1，上限 6，回合结束清空",
                        "；本次攻击后连锁 +1，上限 6，回合结束清空",
                        "；本次攻击后连锁 +1，上限 6",
                        "；雷攻后连锁 +1，上限 6");
                    break;
                case CoreSchoolMechanic.ThunderReady:
                    text = Remove(text, "，不增加连锁");
                    break;
                case CoreSchoolMechanic.ThunderEcho:
                    text = Remove(text, "，不增加连锁");
                    break;
                case CoreSchoolMechanic.BloodRisk:
                case CoreSchoolMechanic.BloodWard:
                    text = Remove(text, "（至少留 1）", "，支付后至少留 1 点");
                    break;
            }
            return text;
        }

        private static string RemovePaidTideGuardDefinition(string text)
        {
            // Preserve which guard can generate tide before removing the keyword's shared definition.
            // A resource-shortage fallback still explicitly grants ordinary guard.
            text = Regex.Replace(text,
                @"(^|[；。\r\n])\s*(?:格挡\s*[+＋]\s*(\d+)|获得\s*(\d+)\s*(?:点\s*)?格挡)",
                match => match.Groups[1].Value + "获得 " +
                    (match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value) + " 点蓄潮格挡");
            if (!text.Contains("蓄潮格挡")) return text;
            text = Remove(text,
                "；付费护盾吸收伤害才蓄积潮势",
                "；此盾挡伤蓄潮势",
                "；本牌格挡实际吸收敌方攻击时等量获得潮势，上限 20");
            text = Rewrite(text, "无木板时基础格挡 2，不蓄积潮势，保底不随强化增长", "无木板时仅获得 2 点普通格挡");
            return Rewrite(text, "无木板仅基础格挡 2，不蓄势", "无木板时仅获得 2 点普通格挡");
        }
    }
}
