using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>A static glossary entry, independent of card values and battle calculations.</summary>
    public sealed class CoreBattleCardKeyword
    {
        internal CoreBattleCardKeyword(string keyword, string meaning)
        {
            Keyword = keyword;
            Meaning = meaning;
        }

        public string Keyword { get; }
        public string Meaning { get; }
        public string Text => Keyword + "：" + Meaning;
    }

    /// <summary>Only explains keywords present on the visible card face, in their reading order.</summary>
    public static class CoreBattleCardKeywordText
    {
        private sealed class Definition
        {
            internal readonly string Id;
            internal readonly string Meaning;
            internal readonly Regex Pattern;

            internal Definition(string id, string meaning, string pattern)
            {
                Id = id;
                Meaning = meaning;
                Pattern = new Regex(pattern, RegexOptions.CultureInvariant);
            }
        }

        private sealed class Occurrence
        {
            internal Definition Definition;
            internal Match Match;
        }

        // Keep these concise and qualitative. Card-specific quantities, formulas, costs,
        // upgrades and live damage quotes remain on their existing card surfaces.
        private static readonly Definition[] Definitions =
        {
            new Definition("extra_load", "下一次炮击造成额外伤害", @"额外装药"),
            new Definition("extra_guard", "本场战斗中，船体防御牌提供额外格挡", @"额外格挡(?=\s*[：:])"),
            new Definition("attached_fire", "下一次攻击造成对应层数的燃烧", @"附火"),
            new Definition("burn", "回合结束时受到对应层数的伤害", @"燃烧"),
            new Definition("curse", "当咒印层数大于或等于该角色剩余生命值的 " + CoreCurseRules.DefaultExecutionThresholdPercent + "% 时，立即消灭该角色", @"咒印"),
            new Definition("healing", "己方回合结束时恢复生命值，层数逐回合减少", @"持续恢复"),
            new Definition("exhaust", "将指定卡牌移出本场战斗", @"焚烧"),
            new Definition("delay", "在下个己方回合开始时触发效果", @"延迟"),
            new Definition("tide_guard", "使用蓄潮格挡格挡伤害后，获得格挡伤害层数的潮势", @"蓄潮格挡"),
            new Definition("chain", "使用雷霆攻击牌后连锁数增加 1，使用非雷霆牌后归零", @"连锁"),
            new Definition("desperate", "当前生命值不高于生命上限的 40%", @"绝境"),
            new Definition("life_steal", "按目标实际扣除的船体或生命值，恢复等量自身船体或生命值", @"吸血"),
            new Definition("curse_damage", "无视格挡造成伤害", @"咒术伤害"),
            new Definition("fire_field", "区域中的角色主动攻击伤害提高 25%；自身回合结束时，获得等于火场剩余持续时间的燃烧层数", @"火场(?!免疫)"),
            new Definition("arcane_field", "区域中的角色自身回合开始时，获得等于折射阵剩余持续时间的法力；受到的主动攻击伤害提高 25%", @"折射阵"),
            new Definition("curse_field", "禁域中的角色受到的主动攻击伤害降低 25%；自身回合结束时，获得等于禁域剩余持续时间的咒印层数", @"禁域"),
            new Definition("tide_field", "定向水流中的角色不可以向水流相反的方向移动，回合结束时，向水流方向移动 1 格", @"定向水流"),
            new Definition("thunder_field", "每移动至雷栅中的一格，获得等于该格雷栅剩余持续时间的连锁；处于雷栅时连锁不会清空或消耗；连锁增加至至少 50 时，受到 1000 点伤害", @"雷栅"),
            new Definition("blood_field", "血池中的角色主动支付船体或生命值时，获得支付量两倍的格挡；自身回合结束时，流失等于血池剩余持续时间的船体或生命值，无视格挡、可致死，且不触发格挡返还", @"血池"),
            new Definition("oil", "获得燃烧时，额外获得一倍的燃烧层数", @"浸油"),
            new Definition("rune_clamp", "减少下一次获得的格挡", @"符文钳制"),
            new Definition("load_seal", "降低下一次炮击伤害", @"装填封印"),
            new Definition("healing_ban", "无法恢复船体或生命值", @"禁疗"),
        };

        private static readonly Regex RichTextTags = new Regex(@"<[^>]*>", RegexOptions.CultureInvariant);

        /// <summary>
        /// Pass the actual face description for live or stage-specific cards. An explicitly
        /// empty description stays empty; card names, hidden rules and numbers are never added.
        /// </summary>
        public static IReadOnlyList<CoreBattleCardKeyword> Build(CoreBattleCard card, string visibleDescription = null)
        {
            string text = visibleDescription ?? card?.Description;
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<CoreBattleCardKeyword>();
            text = RichTextTags.Replace(text, string.Empty);

            var result = new List<CoreBattleCardKeyword>();
            var included = new HashSet<string>(StringComparer.Ordinal);
            foreach (Occurrence occurrence in FindOccurrences(text))
                if (included.Add(occurrence.Definition.Id))
                    result.Add(new CoreBattleCardKeyword(occurrence.Match.Value, occurrence.Definition.Meaning));
            return result.AsReadOnly();
        }

        /// <summary>Emphasize every visible glossary keyword using the same contextual rules as tooltips.</summary>
        public static string EmphasizeKeywords(string visibleDescription)
        {
            if (string.IsNullOrEmpty(visibleDescription)) return string.Empty;
            // Rebuild emphasis from plain text so old range/condition markup cannot
            // survive, and formatting an already formatted description is idempotent.
            string text = RichTextTags.Replace(visibleDescription, string.Empty);
            var result = new StringBuilder();
            int position = 0;
            foreach (Occurrence occurrence in FindOccurrences(text))
            {
                Match match = occurrence.Match;
                result.Append(text, position, match.Index - position);
                result.Append("<b>").Append(match.Value).Append("</b>");
                position = match.Index + match.Length;
            }
            return result.Append(text, position, text.Length - position).ToString();
        }

        private static IEnumerable<Occurrence> FindOccurrences(string text)
        {
            var occurrences = new List<Occurrence>();
            foreach (Definition definition in Definitions)
                foreach (Match match in definition.Pattern.Matches(text))
                    occurrences.Add(new Occurrence { Definition = definition, Match = match });

            // Prefer a complete keyword when two patterns match overlapping text.
            occurrences.Sort((left, right) =>
            {
                int order = left.Match.Index.CompareTo(right.Match.Index);
                return order != 0 ? order : right.Match.Length.CompareTo(left.Match.Length);
            });

            int coveredUntil = 0;
            foreach (Occurrence occurrence in occurrences)
            {
                Match match = occurrence.Match;
                if (match.Index < coveredUntil) continue;
                coveredUntil = match.Index + match.Length;
                yield return occurrence;
            }
        }
    }
}
