using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Broad progress area used to group captain achievements in every UI.</summary>
    public enum CoreAchievementCategory
    {
        Voyage,
        Battle,
        Crew,
        Components,
        Cards,
        Collection,
        Economy,
        Story,
        PortConstruction,
        Fleet,
        Dispatch,
        Exploration,
        Supplies,
    }

    /// <summary>How a game statistic completes an achievement.</summary>
    public enum CoreAchievementRequirementKind
    {
        Flag,
        Counter,
        // Requires source-specific evidence evaluated by the gameplay adapter.
        Predicate,
    }

    /// <summary>
    /// Immutable achievement metadata. RequirementKey is deliberately a stable, Unity-free
    /// statistic name so PlayerPrefs, a save service, and preview tooling can all evaluate the
    /// same authored catalogue without taking a dependency on one another.
    /// </summary>
    public sealed class CoreAchievementDefinition
    {
        public CoreAchievementDefinition(string id, string name, string description,
            CoreAchievementCategory category, CoreAchievementRequirementKind requirementKind,
            string requirementKey, int target = 1, string legacyId = "", string categoryLabel = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Achievement id is required.", "id");
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Achievement name is required.", "name");
            if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Achievement description is required.", "description");
            if (string.IsNullOrWhiteSpace(requirementKey)) throw new ArgumentException("Achievement requirement key is required.", "requirementKey");
            if (target < 1) throw new ArgumentOutOfRangeException("target", target, "Achievement target must be positive.");

            Id = id;
            Name = name;
            Description = description;
            Category = category;
            CategoryLabel = categoryLabel ?? category.ToString();
            RequirementKind = requirementKind;
            RequirementKey = requirementKey;
            Target = target;
            LegacyId = legacyId ?? string.Empty;
        }

        public string Id { get; }
        public string Name { get; }
        public string Title => Name;
        public string CategoryLabel { get; }
        public string Description { get; }
        public CoreAchievementCategory Category { get; }
        public CoreAchievementRequirementKind RequirementKind { get; }
        public string RequirementKey { get; }
        public int Target { get; }
        /// <summary>Optional retired prototype lookup alias; never an additional player-save key.</summary>
        public string LegacyId { get; }
    }

    /// <summary>
    /// Small, serialisation-agnostic progress bag for evaluating the catalogue. The gameplay
    /// controller can populate it from PlayerPrefs or a future save file; no UI state is stored
    /// here. Counter values are clamped at zero and flags are idempotent.
    /// </summary>
    public sealed class CoreAchievementProgress
    {
        private readonly Dictionary<string, int> counters = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> flags = new HashSet<string>(StringComparer.Ordinal);

        public int GetCounter(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && counters.TryGetValue(key, out int value) ? value : 0;
        }

        public bool IsFlagSet(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && flags.Contains(key);
        }

        public void SetCounter(string key, int value)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            counters[key] = Math.Max(0, value);
        }

        public int Increment(string key, int amount = 1)
        {
            if (string.IsNullOrWhiteSpace(key)) return 0;
            int next = (int)Math.Max(0L, Math.Min(int.MaxValue, (long)GetCounter(key) + amount));
            SetCounter(key, next);
            return GetCounter(key);
        }

        public void SetFlag(string key, bool value = true)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            if (value) flags.Add(key);
            else flags.Remove(key);
        }

        public bool IsUnlocked(CoreAchievementDefinition achievement)
        {
            if (achievement == null) return false;
            switch (achievement.RequirementKind)
            {
                case CoreAchievementRequirementKind.Flag: return IsFlagSet(achievement.RequirementKey);
                case CoreAchievementRequirementKind.Counter: return GetCounter(achievement.RequirementKey) >= achievement.Target;
                default: return false; // Composite source evidence belongs to the gameplay adapter.
            }
        }

        public int CountUnlocked(IEnumerable<CoreAchievementDefinition> catalogue)
        {
            if (catalogue == null) return 0;
            int count = 0;
            foreach (CoreAchievementDefinition achievement in catalogue)
                if (IsUnlocked(achievement)) count += 1;
            return count;
        }
    }

    /// <summary>Authored captain achievements covering the complete Seventh Harbor loop.</summary>
    public static class CoreAchievementCatalog
    {
        public const int CatalogCount = 50;
        public const int LongDispatchHours = 12;

        private static readonly IReadOnlyList<CoreAchievementDefinition> all = Array.AsReadOnly(new[]
        {
            // Canonical player IDs and text are the shipped main-menu catalog.
            A("tutorial_departure", "初次扬帆", "完成新手引导并获得航海许可", CoreAchievementCategory.Voyage, F, "tutorial_completed", 1, "first_sail", "航程进度"),
            A("tutorial_reward", "第一枚罗盘", "完成引导并领取第一件纪念藏品", CoreAchievementCategory.Voyage, P, "tutorial_reward_claimed", 1, "", "航程进度"),
            A("tutorial_checkpoint", "航海学徒", "完成第一次新手航程并结算返港", CoreAchievementCategory.Voyage, P, "tutorial_checkpoint", 1, "", "航程进度"),
            A("tutorial_deck", "整备完毕", "在卡牌工坊编辑并保存一次战斗卡组", CoreAchievementCategory.Voyage, F, "tutorial_deck_edited", 1, "", "航程进度"),
            A("tutorial_component", "亲手改装", "领取教学奖励部件或获得一件备用部件", CoreAchievementCategory.Voyage, P, "tutorial_component_reward", 1, "", "航程进度"),
            A("tutorial_recovery", "逆风归港", "在船体受损后结束航程并返港", CoreAchievementCategory.Voyage, F, "damaged_return", 1, "", "航程进度"),
            A("tutorial_navigator", "听见潮声", "完成一次有航海士随行的航程", CoreAchievementCategory.Voyage, F, "navigator_voyage", 1, "", "航程进度"),
            A("tutorial_warehouse", "留住纪念", "把第一件藏品收入仓库", CoreAchievementCategory.Collection, C, "warehouse_treasures_owned", 1, "", "收藏"),
            A("component_loadout", "船匠入门", "保存一套有效的部件装配", CoreAchievementCategory.Components, C, "components_equipped", 1, "", "整备"),
            A("gold_600", "海上生意", "船长资金达到 600 金币", CoreAchievementCategory.Economy, C, "profile_gold", 600, "sea_trade", "财富"),
            A("gold_900", "黄金船长", "船长资金达到 900 金币", CoreAchievementCategory.Economy, C, "profile_gold", 900, "golden_captain", "财富"),
            A("gold_1200", "满载而归", "船长资金达到 1,200 金币", CoreAchievementCategory.Economy, C, "profile_gold", 1200, "", "财富"),
            A("gold_2000", "七港富商", "船长资金达到 2,000 金币", CoreAchievementCategory.Economy, C, "profile_gold", 2000, "", "财富"),
            A("port_gray_gull", "陌生港口", "抵达灰鸥港，完成第一次跨港航行", CoreAchievementCategory.Voyage, F, "reached_gray_gull", 1, "stranger_port", "航程进度"),
            A("port_white_shell", "白贝潮汐", "抵达白贝港", CoreAchievementCategory.Voyage, F, "reached_white_shell", 1, "", "航程进度"),
            A("port_bronze_bell", "钟声入海", "抵达铜钟港", CoreAchievementCategory.Voyage, F, "reached_bronze_bell", 1, "", "航程进度"),
            A("port_salt_crown", "盐冠之下", "抵达盐冠港", CoreAchievementCategory.Voyage, F, "reached_salt_crown", 1, "", "航程进度"),
            A("port_twilight", "暮潮观星", "抵达暮潮港", CoreAchievementCategory.Voyage, F, "reached_twilight", 1, "", "航程进度"),
            A("all_ports", "六港通行", "抵达过六座已知港口", CoreAchievementCategory.Voyage, C, "ports_visited", CoreVoyageSession.DestinationPorts.Count, "", "航程进度"),
            A("invest_port", "共同泊位", "在任一港口完成一次投资", CoreAchievementCategory.PortConstruction, C, "port_investment_levels", 1, "", "港口建设"),
            A("invest_all_ports", "共享航路", "六座港口都完成过投资", CoreAchievementCategory.PortConstruction, C, "ports_invested", CoreVoyageSession.DestinationPorts.Count, "", "港口建设"),
            A("invest_ten", "港务元老", "累计港口投资等级达到 10", CoreAchievementCategory.PortConstruction, C, "port_investment_levels", 10, "", "港口建设"),
            A("ship_second", "换舷新舰", "获得第二艘船", CoreAchievementCategory.Fleet, C, "ships_owned", 2, "", "船队"),
            A("ship_third", "三桅远征", "获得第三艘船", CoreAchievementCategory.Fleet, C, "ships_owned", 3, "", "船队"),
            A("ship_fourth", "舰队成形", "获得第四艘船", CoreAchievementCategory.Fleet, C, "ships_owned", 4, "", "船队"),
            A("all_ships", "四海船旗", "收集全部港口的十二种船只", CoreAchievementCategory.Fleet, C, "ships_owned", CoreVoyageSession.Ships.Count, "", "船队"),
            A("component_inventory", "零件入库", "将部件加入库存", CoreAchievementCategory.Components, F, "component_owned", 1, "", "整备"),
            A("component_cannon", "火炮上膛", "装备一门火炮", CoreAchievementCategory.Components, F, "cannon_equipped", 1, "", "整备"),
            A("component_full", "全船改装", "同时装备四个部件槽位", CoreAchievementCategory.Components, C, "components_equipped", 4, "", "整备"),
            A("component_shop", "港口采购员", "从部件商店购入一件部件", CoreAchievementCategory.Components, P, "component_purchased", 1, "", "整备"),
            A("crew_one", "招募同伴", "招募第一名船员", CoreAchievementCategory.Crew, C, "crew_recruited", 1, "", "船员"),
            A("crew_three", "小小船团", "招募三名船员", CoreAchievementCategory.Crew, C, "crew_recruited", 3, "", "船员"),
            A("crew_six", "老船长旧部", "招募六名船员", CoreAchievementCategory.Crew, C, "crew_recruited", 6, "", "船员"),
            A("crew_variants", "各港知己", "招募六名不同港口的船员", CoreAchievementCategory.Crew, C, "crew_ports_recruited", 6, "", "船员"),
            A("dispatch_prepare", "远航派遣", "派遣船员执行一次任务", CoreAchievementCategory.Dispatch, P, "dispatch_started", 1, "", "派遣"),
            A("dispatch_long", "长航守望", "安排一次不少于 12 小时的派遣", CoreAchievementCategory.Dispatch, P, "dispatch_hours", LongDispatchHours, "", "派遣"),
            A("dispatch_resolved", "带回消息", "完成一次派遣结算", CoreAchievementCategory.Dispatch, F, "dispatch_completed", 1, "", "派遣"),
            A("warehouse_one", "第一件珍藏", "收藏一件可展览物品", CoreAchievementCategory.Collection, C, "warehouse_treasures_owned", 1, "", "收藏"),
            A("warehouse_three", "柜中三宝", "收藏三件可展览物品", CoreAchievementCategory.Collection, C, "warehouse_treasures_owned", 3, "", "收藏"),
            A("warehouse_ten", "船长展室", "收藏十件可展览物品", CoreAchievementCategory.Collection, C, "warehouse_treasures_owned", 10, "", "收藏"),
            A("warehouse_display_six", "满柜荣光", "同时陈列六件收藏品", CoreAchievementCategory.Collection, C, "warehouse_treasures_displayed", CoreWarehouseCollection.DisplaySlotCount, "", "收藏"),
            A("chart_drawn", "亲绘航图", "绘制一张海图", CoreAchievementCategory.Exploration, F, "chart_drawn", 1, "", "探索"),
            A("battle_deck", "甲板策略", "保存一套战斗卡组", CoreAchievementCategory.Battle, C, "configured_cards", 1, "", "战斗"),
            A("battle_stages", "三种战法", "为三种战斗阶段分别配置卡组", CoreAchievementCategory.Battle, C, "configured_deck_stages", Enum.GetValues(typeof(NavalBattleStage)).Length, "", "战斗"),
            A("card_upgrade", "磨亮牌面", "强化一张战斗卡牌", CoreAchievementCategory.Battle, P, "card_upgrade_record", 1, "", "战斗"),
            A("special_card", "专属传承", "获得一张船员或部件专属卡", CoreAchievementCategory.Battle, C, "special_cards_owned", 1, "", "战斗"),
            A("cargo_ready", "补给充足", "同时储备 100 单位水与食物", CoreAchievementCategory.Supplies, P, "prepared_water_and_food", 100, "", "补给"),
            A("cargo_all", "满舱远征", "让所有基础补给都不再为空", CoreAchievementCategory.Supplies, P, "all_base_cargo_present", 1, "", "补给"),
            A("fame", "自由之名", "声望达到 20", CoreAchievementCategory.Story, C, "profile_fame", 20, "", "传承"),
            A("seventh_harbor", "第七港口", "完成引导、抵达六港并拥有一艘额外船只", CoreAchievementCategory.Story, P, "seventh_harbor_prerequisites", 2, "", "传承"),
        });

        private static readonly Dictionary<string, CoreAchievementDefinition> byId = BuildIndex();

        public static IReadOnlyList<CoreAchievementDefinition> All => all;
        /// <summary>Catalog is kept as an alias for other Core catalogs and older tooling.</summary>
        public static IReadOnlyList<CoreAchievementDefinition> Catalog => all;

        public static CoreAchievementDefinition Find(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && byId.TryGetValue(id, out CoreAchievementDefinition value) ? value : null;
        }

        public static bool IsKnown(string id)
        {
            return Find(id) != null;
        }

        private static Dictionary<string, CoreAchievementDefinition> BuildIndex()
        {
            var index = new Dictionary<string, CoreAchievementDefinition>(StringComparer.Ordinal);
            foreach (CoreAchievementDefinition achievement in all)
            {
                index.Add(achievement.Id, achievement);
                if (!string.IsNullOrEmpty(achievement.LegacyId)) index.Add(achievement.LegacyId, achievement);
            }
            return index;
        }

        private static CoreAchievementDefinition A(string id, string name, string description,
            CoreAchievementCategory category, CoreAchievementRequirementKind kind,
            string key, int target, string legacyId, string categoryLabel)
        {
            return new CoreAchievementDefinition(id, name, description, category, kind, key, target, legacyId, categoryLabel);
        }

        private const CoreAchievementRequirementKind F = CoreAchievementRequirementKind.Flag;
        private const CoreAchievementRequirementKind C = CoreAchievementRequirementKind.Counter;
        private const CoreAchievementRequirementKind P = CoreAchievementRequirementKind.Predicate;
    }
}
