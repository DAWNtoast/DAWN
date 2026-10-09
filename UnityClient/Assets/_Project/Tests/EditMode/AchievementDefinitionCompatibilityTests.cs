using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class AchievementDefinitionCompatibilityTests
    {
        // Frozen from the shipping UI catalog and journal writer schema before the
        // Core metadata migration. These literals deliberately do not read Artifacts
        // or derive expectations from either production catalog at test runtime.
        internal static readonly string[][] MetadataBefore =
        {
            new[] { "tutorial_departure", "初次扬帆", "完成新手引导并获得航海许可", "航程进度" },
            new[] { "tutorial_reward", "第一枚罗盘", "完成引导并领取第一件纪念藏品", "航程进度" },
            new[] { "tutorial_checkpoint", "航海学徒", "完成第一次新手航程并结算返港", "航程进度" },
            new[] { "tutorial_deck", "整备完毕", "在卡牌工坊编辑并保存一次战斗卡组", "航程进度" },
            new[] { "tutorial_component", "亲手改装", "领取教学奖励部件或获得一件备用部件", "航程进度" },
            new[] { "tutorial_recovery", "逆风归港", "在船体受损后结束航程并返港", "航程进度" },
            new[] { "tutorial_navigator", "听见潮声", "完成一次有航海士随行的航程", "航程进度" },
            new[] { "tutorial_warehouse", "留住纪念", "把第一件藏品收入仓库", "收藏" },
            new[] { "component_loadout", "船匠入门", "保存一套有效的部件装配", "整备" },
            new[] { "gold_600", "海上生意", "船长资金达到 600 金币", "财富" },
            new[] { "gold_900", "黄金船长", "船长资金达到 900 金币", "财富" },
            new[] { "gold_1200", "满载而归", "船长资金达到 1,200 金币", "财富" },
            new[] { "gold_2000", "七港富商", "船长资金达到 2,000 金币", "财富" },
            new[] { "port_gray_gull", "陌生港口", "抵达灰鸥港，完成第一次跨港航行", "航程进度" },
            new[] { "port_white_shell", "白贝潮汐", "抵达白贝港", "航程进度" },
            new[] { "port_bronze_bell", "钟声入海", "抵达铜钟港", "航程进度" },
            new[] { "port_salt_crown", "盐冠之下", "抵达盐冠港", "航程进度" },
            new[] { "port_twilight", "暮潮观星", "抵达暮潮港", "航程进度" },
            new[] { "all_ports", "六港通行", "抵达过六座已知港口", "航程进度" },
            new[] { "invest_port", "共同泊位", "在任一港口完成一次投资", "港口建设" },
            new[] { "invest_all_ports", "共享航路", "六座港口都完成过投资", "港口建设" },
            new[] { "invest_ten", "港务元老", "累计港口投资等级达到 10", "港口建设" },
            new[] { "ship_second", "换舷新舰", "获得第二艘船", "船队" },
            new[] { "ship_third", "三桅远征", "获得第三艘船", "船队" },
            new[] { "ship_fourth", "舰队成形", "获得第四艘船", "船队" },
            new[] { "all_ships", "四海船旗", "收集全部港口的十二种船只", "船队" },
            new[] { "component_inventory", "零件入库", "将部件加入库存", "整备" },
            new[] { "component_cannon", "火炮上膛", "装备一门火炮", "整备" },
            new[] { "component_full", "全船改装", "同时装备四个部件槽位", "整备" },
            new[] { "component_shop", "港口采购员", "从部件商店购入一件部件", "整备" },
            new[] { "crew_one", "招募同伴", "招募第一名船员", "船员" },
            new[] { "crew_three", "小小船团", "招募三名船员", "船员" },
            new[] { "crew_six", "老船长旧部", "招募六名船员", "船员" },
            new[] { "crew_variants", "各港知己", "招募六名不同港口的船员", "船员" },
            new[] { "dispatch_prepare", "远航派遣", "派遣船员执行一次任务", "派遣" },
            new[] { "dispatch_long", "长航守望", "安排一次不少于 12 小时的派遣", "派遣" },
            new[] { "dispatch_resolved", "带回消息", "完成一次派遣结算", "派遣" },
            new[] { "warehouse_one", "第一件珍藏", "收藏一件可展览物品", "收藏" },
            new[] { "warehouse_three", "柜中三宝", "收藏三件可展览物品", "收藏" },
            new[] { "warehouse_ten", "船长展室", "收藏十件可展览物品", "收藏" },
            new[] { "warehouse_display_six", "满柜荣光", "同时陈列六件收藏品", "收藏" },
            new[] { "chart_drawn", "亲绘航图", "绘制一张海图", "探索" },
            new[] { "battle_deck", "甲板策略", "保存一套战斗卡组", "战斗" },
            new[] { "battle_stages", "三种战法", "为三种战斗阶段分别配置卡组", "战斗" },
            new[] { "card_upgrade", "磨亮牌面", "强化一张战斗卡牌", "战斗" },
            new[] { "special_card", "专属传承", "获得一张船员或部件专属卡", "战斗" },
            new[] { "cargo_ready", "补给充足", "同时储备 100 单位水与食物", "补给" },
            new[] { "cargo_all", "满舱远征", "让所有基础补给都不再为空", "补给" },
            new[] { "fame", "自由之名", "声望达到 20", "传承" },
            new[] { "seventh_harbor", "第七港口", "完成引导、抵达六港并拥有一艘额外船只", "传承" },
        };

        internal static readonly string[] RetiredPrototypeIds =
        {
            "six_port_chart",
            "seventh_harbor_sighting",
            "open_sea_first_wake",
            "open_sea_ten_wakes",
            "hundred_days_at_sea",
            "storm_survivor",
            "storm_master",
            "route_cartographer",
            "hidden_current",
            "all_six_routes",
            "first_battle",
            "ten_battles",
            "flawless_duel",
            "boarding_master",
            "broadside_master",
            "legendary_hunter",
            "fallen_captain_redeemed",
            "successor_duelist",
            "godbreaker",
            "no_retreat",
            "crew_recruiter",
            "full_crew",
            "old_crew_reunion",
            "tavern_storyteller",
            "crew_trust",
            "component_finder",
            "full_loadout",
            "component_master",
            "deck_builder",
            "card_collector",
            "special_card_awakened",
            "special_card_set",
            "warehouse_ten_treasures",
            "warehouse_full_catalog",
            "warehouse_curator",
            "chart_keeper",
            "market_bargain",
            "market_magnate",
            "captains_mark",
            "dispatch_first",
            "dispatch_master",
            "rescue_mission",
            "free_harbor_builder",
            "lore_keeper",
            "seventh_captain",
        };

        private static readonly string[] AchievementKeysBefore =
        {
            "ShatteredSail.Achievements.Unlocked.v1.tutorial_departure",
            "ShatteredSail.Achievements.Unlocked.v1.tutorial_reward",
            "ShatteredSail.Achievements.Unlocked.v1.tutorial_checkpoint",
            "ShatteredSail.Achievements.Unlocked.v1.tutorial_deck",
            "ShatteredSail.Achievements.Unlocked.v1.tutorial_component",
            "ShatteredSail.Achievements.Unlocked.v1.tutorial_recovery",
            "ShatteredSail.Achievements.Unlocked.v1.tutorial_navigator",
            "ShatteredSail.Achievements.Unlocked.v1.tutorial_warehouse",
            "ShatteredSail.Achievements.Unlocked.v1.component_loadout",
            "ShatteredSail.Achievements.Unlocked.v1.gold_600",
            "ShatteredSail.Achievements.Unlocked.v1.gold_900",
            "ShatteredSail.Achievements.Unlocked.v1.gold_1200",
            "ShatteredSail.Achievements.Unlocked.v1.gold_2000",
            "ShatteredSail.Achievements.Unlocked.v1.port_gray_gull",
            "ShatteredSail.Achievements.Unlocked.v1.port_white_shell",
            "ShatteredSail.Achievements.Unlocked.v1.port_bronze_bell",
            "ShatteredSail.Achievements.Unlocked.v1.port_salt_crown",
            "ShatteredSail.Achievements.Unlocked.v1.port_twilight",
            "ShatteredSail.Achievements.Unlocked.v1.all_ports",
            "ShatteredSail.Achievements.Unlocked.v1.invest_port",
            "ShatteredSail.Achievements.Unlocked.v1.invest_all_ports",
            "ShatteredSail.Achievements.Unlocked.v1.invest_ten",
            "ShatteredSail.Achievements.Unlocked.v1.ship_second",
            "ShatteredSail.Achievements.Unlocked.v1.ship_third",
            "ShatteredSail.Achievements.Unlocked.v1.ship_fourth",
            "ShatteredSail.Achievements.Unlocked.v1.all_ships",
            "ShatteredSail.Achievements.Unlocked.v1.component_inventory",
            "ShatteredSail.Achievements.Unlocked.v1.component_cannon",
            "ShatteredSail.Achievements.Unlocked.v1.component_full",
            "ShatteredSail.Achievements.Unlocked.v1.component_shop",
            "ShatteredSail.Achievements.Unlocked.v1.crew_one",
            "ShatteredSail.Achievements.Unlocked.v1.crew_three",
            "ShatteredSail.Achievements.Unlocked.v1.crew_six",
            "ShatteredSail.Achievements.Unlocked.v1.crew_variants",
            "ShatteredSail.Achievements.Unlocked.v1.dispatch_prepare",
            "ShatteredSail.Achievements.Unlocked.v1.dispatch_long",
            "ShatteredSail.Achievements.Unlocked.v1.dispatch_resolved",
            "ShatteredSail.Achievements.Unlocked.v1.warehouse_one",
            "ShatteredSail.Achievements.Unlocked.v1.warehouse_three",
            "ShatteredSail.Achievements.Unlocked.v1.warehouse_ten",
            "ShatteredSail.Achievements.Unlocked.v1.warehouse_display_six",
            "ShatteredSail.Achievements.Unlocked.v1.chart_drawn",
            "ShatteredSail.Achievements.Unlocked.v1.battle_deck",
            "ShatteredSail.Achievements.Unlocked.v1.battle_stages",
            "ShatteredSail.Achievements.Unlocked.v1.card_upgrade",
            "ShatteredSail.Achievements.Unlocked.v1.special_card",
            "ShatteredSail.Achievements.Unlocked.v1.cargo_ready",
            "ShatteredSail.Achievements.Unlocked.v1.cargo_all",
            "ShatteredSail.Achievements.Unlocked.v1.fame",
            "ShatteredSail.Achievements.Unlocked.v1.seventh_harbor",
            "ShatteredSail.Achievements.Visited.v1.断锚港",
            "ShatteredSail.Achievements.Visited.v1.白贝港",
            "ShatteredSail.Achievements.Visited.v1.铜钟港",
            "ShatteredSail.Achievements.Visited.v1.盐冠港",
            "ShatteredSail.Achievements.Visited.v1.暮潮港",
            "ShatteredSail.Achievements.Visited.v1.灰鸥港",
        };

        private static readonly string[][] JournalKeysBefore =
        {
            new[] { "ShatteredSail.Achievements.Unlocked.v1.all_ports", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.all_ships", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.battle_deck", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.battle_stages", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.card_upgrade", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.cargo_all", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.cargo_ready", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.chart_drawn", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.component_cannon", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.component_full", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.component_inventory", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.component_loadout", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.component_shop", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.crew_one", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.crew_six", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.crew_three", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.crew_variants", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.dispatch_long", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.dispatch_prepare", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.dispatch_resolved", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.fame", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.gold_1200", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.gold_2000", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.gold_600", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.gold_900", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.invest_all_ports", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.invest_port", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.invest_ten", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.port_bronze_bell", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.port_gray_gull", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.port_salt_crown", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.port_twilight", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.port_white_shell", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.seventh_harbor", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.ship_fourth", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.ship_second", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.ship_third", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.special_card", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.tutorial_checkpoint", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.tutorial_component", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.tutorial_deck", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.tutorial_departure", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.tutorial_navigator", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.tutorial_recovery", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.tutorial_reward", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.tutorial_warehouse", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.warehouse_display_six", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.warehouse_one", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.warehouse_ten", "int" },
            new[] { "ShatteredSail.Achievements.Unlocked.v1.warehouse_three", "int" },
            new[] { "ShatteredSail.Achievements.Visited.v1.断锚港", "int" },
            new[] { "ShatteredSail.Achievements.Visited.v1.暮潮港", "int" },
            new[] { "ShatteredSail.Achievements.Visited.v1.灰鸥港", "int" },
            new[] { "ShatteredSail.Achievements.Visited.v1.白贝港", "int" },
            new[] { "ShatteredSail.Achievements.Visited.v1.盐冠港", "int" },
            new[] { "ShatteredSail.Achievements.Visited.v1.铜钟港", "int" },
            new[] { "ShatteredSail.BattleCardUpgrades.v1", "string" },
            new[] { "ShatteredSail.BattleDeck.v1", "string" },
            new[] { "ShatteredSail.BattleDeck.v2.Boarding", "string" },
            new[] { "ShatteredSail.BattleDeck.v2.Naval", "string" },
            new[] { "ShatteredSail.BattleDeck.v2.Preparation", "string" },
            new[] { "ShatteredSail.Cargo.v1.Beer", "int" },
            new[] { "ShatteredSail.Cargo.v1.Cannonballs", "int" },
            new[] { "ShatteredSail.Cargo.v1.Cloth", "int" },
            new[] { "ShatteredSail.Cargo.v1.Fame", "int" },
            new[] { "ShatteredSail.Cargo.v1.Food", "int" },
            new[] { "ShatteredSail.Cargo.v1.Gold", "int" },
            new[] { "ShatteredSail.Cargo.v1.Iron", "int" },
            new[] { "ShatteredSail.Cargo.v1.Medicine", "int" },
            new[] { "ShatteredSail.Cargo.v1.Powder", "int" },
            new[] { "ShatteredSail.Cargo.v1.Water", "int" },
            new[] { "ShatteredSail.Cargo.v1.Wood", "int" },
            new[] { "ShatteredSail.Collection.Progress.v1", "string" },
            new[] { "ShatteredSail.ComponentInventory.v2", "string" },
            new[] { "ShatteredSail.CurrentPort.v1", "string" },
            new[] { "ShatteredSail.GameClockHours.v1", "string" },
            new[] { "ShatteredSail.PortWarehouse.Cargo.v1", "string" },
            new[] { "ShatteredSail.ProfileGold.Decimal.v2", "string" },
            new[] { "ShatteredSail.ProfileGold.v1", "int" },
            new[] { "ShatteredSail.SeaChart.Progress.v1", "string" },
            new[] { "ShatteredSail.SelectedShip.v1", "int" },
            new[] { "ShatteredSail.SpecialCards.Acquired.v1", "string" },
            new[] { "ShatteredSail.SpecialCards.Experience.v1", "string" },
            new[] { "ShatteredSail.TreasureProgress.v1", "string" },
            new[] { "ShatteredSail.Warehouse.Display.v1", "string" },
            new[] { "ShatteredSail.Warehouse.DisplayByPort.v1.断锚港", "string" },
            new[] { "ShatteredSail.Warehouse.DisplayByPort.v1.暮潮港", "string" },
            new[] { "ShatteredSail.Warehouse.DisplayByPort.v1.灰鸥港", "string" },
            new[] { "ShatteredSail.Warehouse.DisplayByPort.v1.白贝港", "string" },
            new[] { "ShatteredSail.Warehouse.DisplayByPort.v1.盐冠港", "string" },
            new[] { "ShatteredSail.Warehouse.DisplayByPort.v1.铜钟港", "string" },
            new[] { "ShatteredSail.Warehouse.DisplayRecord.v1", "int" },
            new[] { "ShatteredSail.Warehouse.Owned.v1", "string" },
            new[] { "ShatteredSail.Warehouse.OwnedByPort.v1.断锚港", "string" },
            new[] { "ShatteredSail.Warehouse.OwnedByPort.v1.暮潮港", "string" },
            new[] { "ShatteredSail.Warehouse.OwnedByPort.v1.灰鸥港", "string" },
            new[] { "ShatteredSail.Warehouse.OwnedByPort.v1.白贝港", "string" },
            new[] { "ShatteredSail.Warehouse.OwnedByPort.v1.盐冠港", "string" },
            new[] { "ShatteredSail.Warehouse.OwnedByPort.v1.铜钟港", "string" },
            new[] { "ShatteredSail.Warehouse.PortMigration.v1", "int" },
        };

        [Test]
        public void CanonicalMetadata_PreservesAllFiftyUiDefinitionsAndTheirOrder()
        {
            Assert.That(MetadataBefore.Length, Is.EqualTo(50));
            Assert.That(MetadataBefore.Select(row => row[0]).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(50));
            Assert.That(CoreAchievementCatalog.All, Is.SameAs(AchievementCatalog.All));
            Assert.That(CoreAchievementCatalog.All, Is.SameAs(CoreAchievementCatalog.Catalog));
            Assert.That(CoreAchievementCatalog.All.Count, Is.EqualTo(50));
            for (int index = 0; index < MetadataBefore.Length; index++)
            {
                CoreAchievementDefinition actual = CoreAchievementCatalog.All[index];
                string[] expected = MetadataBefore[index];
                Assert.That(new[] { actual.Id, actual.Title, actual.Description, actual.CategoryLabel },
                    Is.EqualTo(expected), "Canonical metadata index " + index);
                Assert.That(actual.Name, Is.EqualTo(actual.Title), actual.Id);
                Assert.That(CoreAchievementCatalog.Find(expected[0]), Is.SameAs(actual), actual.Id);
            }
            CoreAchievementDefinition upgrade = CoreAchievementCatalog.Find("card_upgrade");
            Assert.That(upgrade.Target, Is.EqualTo(1));
            Assert.That(upgrade.Title, Is.EqualTo("磨亮牌面"));
        }

        [TestCase("first_sail", "tutorial_departure")]
        [TestCase("stranger_port", "port_gray_gull")]
        [TestCase("sea_trade", "gold_600")]
        [TestCase("golden_captain", "gold_900")]
        public void OnlyExplicitAliases_FindTheCanonicalObjectWithoutAddingCatalogRows(string alias, string canonical)
        {
            CoreAchievementDefinition definition = CoreAchievementCatalog.Find(alias);
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition, Is.SameAs(CoreAchievementCatalog.Find(canonical)));
            Assert.That(definition.Id, Is.EqualTo(canonical));
            Assert.That(definition.LegacyId, Is.EqualTo(alias));
            Assert.That(CoreAchievementCatalog.IsKnown(alias), Is.True);
            Assert.That(CoreAchievementCatalog.All.Any(item => item.Id == alias), Is.False);
            Assert.That(CoreAchievementCatalog.All.Count(item => item.LegacyId.Length != 0), Is.EqualTo(4));
            Assert.That(AchievementCatalog.PersistenceKeys,
                Does.Not.Contain("ShatteredSail.Achievements.Unlocked.v1." + alias));
        }

        [Test]
        public void RetiredPrototypeIds_RemainUnknownAndNeverAcquirePersistenceKeys()
        {
            Assert.That(RetiredPrototypeIds.Length, Is.EqualTo(45));
            Assert.That(RetiredPrototypeIds.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(45));
            string[] persistenceKeys = AchievementCatalog.PersistenceKeys.ToArray();
            foreach (string id in RetiredPrototypeIds)
            {
                Assert.That(CoreAchievementCatalog.Find(id), Is.Null, id);
                Assert.That(CoreAchievementCatalog.IsKnown(id), Is.False, id);
                Assert.That(persistenceKeys, Does.Not.Contain("ShatteredSail.Achievements.Unlocked.v1." + id), id);
            }
        }

        [Test]
        public void Persistence_PreservesExactFiftyUnlockAndSixVisitKeysWithoutAliases()
        {
            Assert.That(AchievementKeysBefore.Length, Is.EqualTo(56));
            Assert.That(AchievementKeysBefore.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(56));
            Assert.That(AchievementKeysBefore.Count(key => key.StartsWith("ShatteredSail.Achievements.Unlocked.v1.", StringComparison.Ordinal)), Is.EqualTo(50));
            Assert.That(AchievementKeysBefore.Count(key => key.StartsWith("ShatteredSail.Achievements.Visited.v1.", StringComparison.Ordinal)), Is.EqualTo(6));
            Assert.That(AchievementCatalog.PersistenceKeys, Is.EqualTo(AchievementKeysBefore));
        }

        [Test]
        public void PendingJournalCompatibility_PreservesAllOneHundredSortedKeysAndNativeTypes()
        {
            Assert.That(JournalKeysBefore.Length, Is.EqualTo(100));
            string[] expectedKeys = JournalKeysBefore.Select(row => row[0]).ToArray();
            Assert.That(expectedKeys.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(100));
            Assert.That(expectedKeys, Is.EqualTo(expectedKeys.OrderBy(key => key, StringComparer.Ordinal)));
            Assert.That(JournalKeysBefore.Count(row => row[1] == "string"), Is.EqualTo(29));
            Assert.That(JournalKeysBefore.Count(row => row[1] == "int"), Is.EqualTo(71));
            Assert.That(ProfileSaveJournal.ProtectedKeys, Is.EqualTo(expectedKeys));

            FieldInfo specsField = typeof(ProfileSaveJournal).GetField("Specs", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(specsField, Is.Not.Null, "The actual journal schema must be compared, not just its exported key names.");
            Array specs = (Array)specsField.GetValue(null);
            Assert.That(specs.Length, Is.EqualTo(100));
            for (int index = 0; index < specs.Length; index++)
            {
                object spec = specs.GetValue(index);
                Type specType = spec.GetType();
                string key = (string)specType.GetField("Key", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(spec);
                string type = (string)specType.GetField("Type", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(spec);
                Assert.That(new[] { key, type }, Is.EqualTo(JournalKeysBefore[index]), "Journal schema index " + index);
            }
        }
    }
}
