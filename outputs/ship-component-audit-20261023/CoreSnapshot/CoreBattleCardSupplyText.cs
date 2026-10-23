using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Card-only wording for the free base effect and the extra effect bought with supplies.</summary>
    internal static class CoreBattleCardSupplyText
    {
        private const string Number = @"\s*(\d+)\s*";
        private static readonly Regex RepairPhrase = new Regex(
            @"(?:(?:海战|炮战|接舷战?|白刃战)\s*)?(?:额外\s*)?(?:恢复|修复)\s*\d+\s*(?:点\s*)?(?:当前阶段生命|船员生命|生命值?|船体)");

        internal static string Rewrite(string text, CoreBattleCard card, NavalBattleStage? stage = null, int? fullRepair = null)
        {
            if (string.IsNullOrEmpty(text) || card == null) return text;
            // A second display pass must not turn a base-plus-extra quote into another bonus.
            if (Regex.IsMatch(text, @"消耗[^；。\r\n]*?(?:药物|药品|木板)[^；。，\r\n]*额外(?:恢复|修复|获得)")) return text;
            // Natural Chinese periodic healing places the paid condition before both future
            // treatments. Display copies can lose BoardingSpec but must keep this resolved quote.
            if (Regex.IsMatch(text, @"使用本牌时若消耗[^；。\r\n]*，\s*则这两次治疗各额外恢复")) return text;
            if (card.BoardingSpec != null)
            {
                string boarding = BoardingHealing(text, card);
                return boarding ?? text;
            }
            if (card.Id == "card_school_tide_n03") return TideGuard(text, card, true);
            CoreSchoolMechanic mechanic = card.SchoolEffect?.Mechanic ?? CoreSchoolMechanic.None;
            if (mechanic == CoreSchoolMechanic.TideWard) return TideGuard(text, card, false);
            if (mechanic == CoreSchoolMechanic.BloodMend || mechanic == CoreSchoolMechanic.BloodDrain) return text;

            int total = fullRepair ?? PotentialRepair(card);
            if (total <= 0 || !RepairPhrase.IsMatch(text)) return text;
            int fallback = Math.Min(CoreBattleCardEffects.UnprovisionedRepair, total);
            bool bothStages = !stage.HasValue && card.IsAllowedInStage(NavalBattleStage.Boarding) &&
                (card.IsAllowedInStage(NavalBattleStage.Naval) || card.IsAllowedInStage(NavalBattleStage.Preparation));
            bool boardingStage = stage == NavalBattleStage.Boarding || (!stage.HasValue &&
                card.AllowedStages == NavalBattleStages.Boarding);
            string replacement = bothStages
                ? "海战：" + Heal(fallback, total - fallback, "木板", false) + "；接舷战：" + Heal(fallback, total - fallback, "药物", true)
                : Heal(fallback, total - fallback, boardingStage ? "药物" : "木板", boardingStage);

            // School resource payment is a card cost; its guaranteed heal bonus is included in total.
            // Keep all non-healing effects in their original order.
            string working = text;
            if (mechanic == CoreSchoolMechanic.ArcaneMend || mechanic == CoreSchoolMechanic.CurseDrain ||
                mechanic == CoreSchoolMechanic.TideRenew)
            {
                working = Regex.Replace(working, @"恢复量提高\s*\d+\s*点", string.Empty);
                working = Regex.Replace(working, @"强化恢复", string.Empty);
            }
            if (mechanic == CoreSchoolMechanic.FireHeal)
            {
                working = Regex.Replace(working, @"敌方燃烧时(?:额外恢复\s*\d+\s*(?:点\s*)?(?:生命值?)?|强化恢复)", string.Empty);
                if (!fullRepair.HasValue && card.SchoolEffect.Power > 0)
                    replacement += "；有" + (boardingStage ? "药物" : "木板") + "且敌方燃烧时额外恢复 " + N(card.SchoolEffect.Power) + " 点生命值";
            }
            bool inserted = false;
            working = RepairPhrase.Replace(working, match =>
            {
                if (inserted) return string.Empty;
                inserted = true;
                return replacement + "；";
            });
            return Clean(working);
        }

        private static string BoardingHealing(string text, CoreBattleCard card)
        {
            int extra = Math.Max(0, card.Repair - 2);
            switch (card.BoardingSpec.Key)
            {
                case "basic_06": return Heal(2, extra, "药物", true) + "。";
                case "basic_07": return PeriodicHeal(extra, "1 药物") + "。";
                case "fire_05": return ConditionalHeal(extra, "1 药物和 1 火药") + "。";
                case "arcane_05": return ConditionalHeal(extra, "1 药物和 2 点魔力") + "。";
                case "curse_12": return ConditionalHeal(extra, "1 药物和 2 层咒痕") + "。";
                case "tide_11": return ConditionalHeal(extra, "1 药物和 6 点潮势") + "。";
                case "tide_12": return PeriodicHeal(extra, "1 药物和 8 点潮势") + "；不叠加、不刷新。";
                case "blood_09":
                    // The authored upgraded text resolves both independently rounded values.
                    Match desperate = Regex.Match(text, @"绝境时恢复" + Number);
                    if (!desperate.Success) return null;
                    int desperateFull = int.Parse(desperate.Groups[1].Value, CultureInfo.InvariantCulture);
                    return "恢复 2 点生命值，绝境时恢复 3 点生命值；消耗 1 药物额外恢复 " + N(extra) +
                        " 点生命值，绝境时额外恢复 " + N(Math.Max(0, desperateFull - 3)) + " 点生命值。按治疗前状态判断绝境。";
                default: return null;
            }
        }

        private static string PeriodicHeal(int extra, string payment) =>
            "本回合和下一己方回合结束各恢复 2 点生命值；施放时消耗 " + payment +
            "额外恢复 " + N(extra) + " 点生命值（上述两次各生效）";

        private static string ConditionalHeal(int extra, string payment) =>
            "恢复 2 点生命值；消耗 " + payment + "额外恢复 " + N(extra) + " 点生命值";

        private static string Heal(int fallback, int extra, string supply, bool boarding)
        {
            string verb = boarding ? "恢复" : "修复";
            string target = boarding ? "生命值" : "船体";
            return verb + " " + N(fallback) + " 点" + target + (extra > 0
                ? "；消耗 1 " + supply + "额外" + verb + " " + N(extra) + " 点" + target : string.Empty);
        }

        private static int PotentialRepair(CoreBattleCard card)
        {
            int extra = 0;
            switch (card.SchoolEffect?.Mechanic ?? CoreSchoolMechanic.None)
            {
                case CoreSchoolMechanic.ArcaneMend:
                case CoreSchoolMechanic.CurseDrain:
                case CoreSchoolMechanic.TideRenew: extra = card.SchoolEffect.Power; break;
            }
            return (int)Math.Min(int.MaxValue, (long)card.Repair + Math.Max(0, extra));
        }

        private static string TideGuard(string text, CoreBattleCard card, bool anchor)
        {
            // Paid wood changes the entire shield into tide-generating guard, including its base two points.
            Match guard = Regex.Match(text, @"获得\s*(\d+)\s*(?:点\s*)?蓄潮格挡");
            int total = guard.Success ? int.Parse(guard.Groups[1].Value, CultureInfo.InvariantCulture) : card.Block;
            int fallback = Math.Min(total, Math.Min(2, card.Block) + Math.Max(0, total - card.Block));
            int extra = Math.Max(0, total - fallback);
            int wood = anchor ? 1 : Math.Max(1, card.SchoolEffect.WoodCost);
            string replacement = "获得 " + N(fallback) + " 点普通格挡；消耗 " + N(wood) +
                " 木板额外获得 " + N(extra) + " 点格挡，并将本牌格挡变为蓄潮格挡";
            if (anchor) replacement += "，至下个己方回合开始可拒绝一次强制位移";
            string working = Regex.Replace(text, @"无木板[^；。\r\n]*", string.Empty);
            working = Regex.Replace(working, @"至下个己方回合开始可拒绝一次强制位移", string.Empty);
            working = Regex.Replace(working, @"(?:消耗|耗)\s*\d+\s*木板", string.Empty);
            bool inserted = false;
            working = Regex.Replace(working, @"获得\s*\d+\s*(?:点\s*)?蓄潮格挡", match =>
            {
                if (inserted) return match.Value;
                inserted = true;
                return replacement;
            });
            return inserted ? Clean(working) : text;
        }

        private static string Clean(string text)
        {
            text = Regex.Replace(text, @"\s*([，；。])\s*", "$1");
            text = Regex.Replace(text, @"[，；。]{2,}", match => match.Value.Contains("。") ? "。" : "；");
            return text.Trim().Trim('，', '；', '。') + "。";
        }

        private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
