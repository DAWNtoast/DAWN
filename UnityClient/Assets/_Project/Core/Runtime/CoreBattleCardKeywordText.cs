using System;
using System.Collections.Generic;
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
            new Definition("attached_fire", "炮击造成实际伤害后，给敌方施加燃烧", @"附火|烈焰强化"),
            new Definition("burn", "回合结束时造成无视格挡的伤害，层数逐回合减少", @"灼烧|燃烧"),
            new Definition("self_burn", "己方回合结束时受到无视格挡的伤害，层数逐回合减少", @"自燃"),
            new Definition("curse", "回合结束时造成无视格挡的咒术伤害，层数逐回合减少", @"咒印|咒痕"),
            new Definition("healing", "己方回合结束时恢复生命值，层数逐回合减少", @"持续恢复"),
            new Definition("exhaust", "将指定卡牌移出本场战斗", @"焚烧|燃尽"),
            new Definition("delay", "在下个己方回合开始时触发效果", @"延迟"),
            new Definition("retained_guard", "格挡可以跨回合保留，直到牌面指定的时间", @"保留格挡"),
            new Definition("tide_guard", "吸收敌方攻击伤害时，积累潮势的格挡", @"蓄潮格挡|潮汐格挡"),
            new Definition("tide", "由蓄潮格挡等效果积累，可供潮汐卡牌消耗或强化效果", @"潮势"),
            new Definition("temporary_mana", "仅在本回合内可用的额外法力", @"临时法力|临时魔力"),
            new Definition("mana", "可供秘术卡牌消耗，跨回合保留且不会自然恢复", @"法力|魔力"),
            new Definition("chain", "使用雷霆攻击积累，可强化雷霆效果，回合结束时清空", @"连锁|雷链"),
            new Definition("desperate", "当前生命值不高于生命上限的 40%", @"绝境"),
            new Definition("life_steal", "根据对敌方造成的实际生命损失，恢复己方生命值", @"吸血"),
            new Definition("curse_damage", "无视格挡的伤害", @"咒术伤害"),
            new Definition("fire_field", "进入或停留在其中的船只会受到火焰伤害", @"火场"),
            new Definition("arcane_field", "为进入或停留其中的己舰提供临时法力和攻击穿透效果", @"折射阵"),
            new Definition("curse_field", "敌舰在其中结束回合时，承受额外咒印伤害", @"禁域"),
            new Definition("tide_field", "船只在其中结束移动后，会沿指定方向被推动", @"定向水流"),
            new Definition("thunder_field", "船只进入时受到伤害，并停止本次主动航行", @"雷栅"),
            new Definition("blood_field", "为进入或停留其中的己舰提供格挡，并强化下一次攻击", @"血誓阵"),
            new Definition("oil", "下次受到火焰主动攻击并实际受伤时，承受额外伤害和燃烧", @"浸油|油浸标记"),
            new Definition("anchor", "抵抗强制位移", @"定锚"),
            new Definition("fire_immunity", "免疫火场造成的伤害", @"火场免疫"),
            new Definition("precharge", "下一次雷霆攻击结算后，获得额外连锁", @"预充电"),
            new Definition("rune_clamp", "减少下一次获得的格挡", @"符文钳制"),
            new Definition("load_seal", "减少下一次获得的装填加伤", @"装填封印"),
            new Definition("healing_ban", "降低受到的治疗效果", @"禁疗"),
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

            var occurrences = new List<Occurrence>();
            foreach (Definition definition in Definitions)
                foreach (Match match in definition.Pattern.Matches(text))
                    occurrences.Add(new Occurrence { Definition = definition, Match = match });

            // Prefer a complete term such as 临时法力 or 火场免疫 over its nested term.
            occurrences.Sort((left, right) =>
            {
                int order = left.Match.Index.CompareTo(right.Match.Index);
                return order != 0 ? order : right.Match.Length.CompareTo(left.Match.Length);
            });

            var result = new List<CoreBattleCardKeyword>();
            var included = new HashSet<string>(StringComparer.Ordinal);
            int coveredUntil = 0;
            foreach (Occurrence occurrence in occurrences)
            {
                Match match = occurrence.Match;
                if (match.Index < coveredUntil) continue;
                coveredUntil = match.Index + match.Length;
                if (included.Add(occurrence.Definition.Id))
                    result.Add(new CoreBattleCardKeyword(match.Value, occurrence.Definition.Meaning));
            }
            return result.AsReadOnly();
        }
    }
}
