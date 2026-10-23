using System.Text.RegularExpressions;

namespace ShatteredSail.Core.Gameplay
{
    internal static partial class CoreBattleCardRuleText
    {
        private static string RemoveBoardingRules(string text, CoreBattleCard card)
        {
            string key = card.BoardingSpec.Key;
            // These definitions are owned by the boarding status and payment pipelines.
            // Keep authored application counts, damage formulas, costs and trigger windows.
            text = Remove(text,
                "每层在敌方回合结束造成1伤害，共2次。",
                "，每层每次1伤害，持续2次敌方回合结束",
                "每批独立结算2次。", "，每层结算2次",
                "烧掉的牌本场不能取回。",
                "自燃每层每次失去1生命，共2次，不受格挡。",
                "；即时支付后至少留1生命", "，至少留1",
                "生命支付不受格挡与减伤。", "生命先支付，不被本牌格挡抵消。",
                "，不能退回已经损失的生命",
                "，不超过当前上限", "；魔力跨回合保留，不自然恢复",
                "；不抵消同名加价", "，再次使用正常付费", "；不能取回燃尽或烧掉的牌",
                "，删除原定触发，不重复结算或触发出牌收益",
                "，作为一次攻击结算", "多段合为一次出牌。",
                "咒痕上限6，每批持续3次敌方回合结束，本身不造成伤害。",
                "；每批持续3次敌方回合结束，不刷新旧层，本身不造成伤害",
                "咒痕本身不造成伤害。", "；优先消耗最早到期层数",
                "；每实际吸收1点敌方攻击获得1潮势，上限12，保留到紧接的己方回合结束",
                "；每实际吸收1点敌方攻击获得1潮势，上限12",
                "实际吸收敌方攻击时等量产势，上限12。", "不消耗木材。",
                "，不能再次产势");

            if (key == "curse_02") text = Remove(text, "，上限6");
            if (key == "curse_05") text = Remove(text, "；多段只算一次", "咒术伤害、反伤与持续伤害不触发；");
            if (key == "curse_10") text = Remove(text, "；每层仅可延长一次，不新增层数、不造成伤害");
            if (key == "tide_05") text = Remove(text, "；每一点只能延长一次，不产生新潮势");
            if (key == "fire_10") text = Remove(text, "未用次数回合结束失效。");

            // Queued and reactive damage use BDeal without active critical rolls. The
            // curse damage category bypasses guard; thunder_14's own piercing stays explicit.
            text = Remove(text, "；受格挡，不暴击", "，受格挡、不暴击", "，受格挡，不暴击",
                "；反伤受格挡，不暴击");
            if (key == "curse_09" || key == "curse_11")
                text = Remove(text, "，无视格挡，不暴击", "；不算主动攻击");

            if (card.SchoolId == "thunder")
            {
                text = Remove(text, "，结算后连锁+1，上限6", "；结算后连锁+1，上限6",
                    "；整张结算后连锁+1，上限6", "；整张结算后仅连锁+1", "；结算后连锁+1",
                    "；回合结束清空连锁", "，结算后不加链");
                text = Rewrite(text, "；施放时锁定并连锁+1，到期不再加链、不暴击", "；施放时锁定");
                if (key == "thunder_10") text = Remove(text, "不暴击、不产链，");
            }

            if (card.SchoolId == "blood")
            {
                text = Rewrite(text, "绝境：治疗前生命不高于最大生命40%", "按治疗前状态判断绝境");
                text = Remove(text, "绝境为生命不高于40%。");
                text = Rewrite(text, "当前生命不高于最大生命40%时", "绝境时");
                text = Rewrite(text, "仅生命不高于最大生命40%时可用", "仅绝境时可用");
                text = Rewrite(text, "在生命不高于40%时打出的", "在绝境时打出的");
                if (key == "blood_06")
                {
                    text = Rewrite(text, "按敌方实际生命损失吸血", "吸血");
                    text = Remove(text, "，不消耗药物");
                    text = Regex.Replace(text, @"吸血，最多\s*(\d+)", "吸血，最多恢复 $1 点生命");
                }
            }
            return text;
        }
    }
}
