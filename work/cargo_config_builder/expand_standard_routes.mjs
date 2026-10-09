import fs from "node:fs/promises";
import path from "node:path";
import { Workbook } from "@oai/artifact-tool";

const root = path.resolve(import.meta.dirname, "../..");
const files = {
  routes: path.join(root, "outputs/routes_v2/route_config.csv"),
  events: path.join(root, "outputs/routes_v2/event_point_config.csv"),
  art: path.join(root, "outputs/routes_v2/art_manifest.csv"),
};
const previewDir = path.join(import.meta.dirname, "previews_standard_routes");
await fs.mkdir(previewDir, { recursive: true });

const routeOrder = [
  "route_shattered_sail_reef",
  "route_golden_tide_trade",
  "route_moon_tide_ruins",
];
const assets = {
  route_shattered_sail_reef: {
    map: "outputs/routes_v2/shattered_sail_route_map_v2.png",
    sheet: "outputs/routes_v2/shattered_sail_event_sheet_v2.png",
    expansionSheet: "outputs/routes_v2/shattered_sail_event_sheet_expansion_v3.png",
    uniqueSheet: "outputs/routes_v2/shattered_sail_event_sheet_unique_v4.png",
    mapName: "碎帆礁海航线图 v2",
  },
  route_golden_tide_trade: {
    map: "outputs/routes_v2/golden_tide_route_map_v2.png",
    sheet: "outputs/routes_v2/golden_tide_event_sheet_v2.png",
    expansionSheet: "outputs/routes_v2/golden_tide_event_sheet_expansion_v3.png",
    uniqueSheet: "outputs/routes_v2/golden_tide_event_sheet_unique_v4.png",
    mapName: "黄金潮汐商路航线图 v2",
  },
  route_moon_tide_ruins: {
    map: "outputs/routes_v2/moon_tide_route_map_v2.png",
    sheet: "outputs/routes_v2/moon_tide_event_sheet_v2.png",
    expansionSheet: "outputs/routes_v2/moon_tide_event_sheet_expansion_v3.png",
    uniqueSheet: "outputs/routes_v2/moon_tide_event_sheet_unique_v4.png",
    mapName: "月潮遗迹海航线图 v2",
  },
};

function csvCell(value) {
  const text = value === null || value === undefined ? "" : String(value);
  return /[",\r\n]/.test(text) ? '"' + text.replace(/"/g, '""') + '"' : text;
}

function toCsv(records, headers) {
  const rows = [headers, ...records.map((record) => headers.map((header) => record[header] ?? ""))];
  return "\uFEFF" + rows.map((row) => row.map(csvCell).join(",")).join("\n") + "\n";
}

async function readRecords(file, sheetName) {
  const text = (await fs.readFile(file, "utf8")).replace(/^\uFEFF/, "");
  const workbook = await Workbook.fromCSV(text, { sheetName });
  const values = workbook.worksheets.getItem(sheetName).getUsedRange().values;
  const headers = values[0].map(String);
  const records = values.slice(1).map((row) =>
    Object.fromEntries(headers.map((header, index) => [header, row[index] ?? ""])),
  );
  return { headers, records };
}

const routeUpdates = {
  route_shattered_sail_reef: {
    recommended_voyage_days: 24,
    recommended_cargo: 57.6,
    recommended_cargo_ratio: 0.64,
    fixed_event_point_count: 20,
    fixed_event_template_count: 70,
    random_event_template_count: 10,
    min_event_spacing_map_units: 9,
    min_event_spacing_nm: 243,
    ambush_chance_per_100nm: 0.012,
    ambush_detection_base_chance: 0.30,
    provision_pressure: "中",
    special_rule: "按种子选1条三阶段事件链，另抽取16个非链支线并加入裂桅号；海图记录链进度、一次性事件与衰减收益，打捞、修船、纯淡水事件形成新手资源循环",
  },
  route_golden_tide_trade: {
    recommended_voyage_days: 22,
    recommended_cargo: 86.8,
    recommended_cargo_ratio: 0.62,
    fixed_event_point_count: 20,
    fixed_event_template_count: 70,
    random_event_template_count: 10,
    min_event_spacing_map_units: 10,
    min_event_spacing_nm: 150,
    ambush_chance_per_100nm: 0.008,
    ambush_detection_base_chance: 0.3,
    provision_pressure: "中",
    special_rule: "按种子选1条三阶段事件链，另抽取16个非链支线并加入金秤号；海图记录链进度、一次性事件与衰减收益，契约进度、商会好感、追击和仓位共同决定净收益",
  },
  route_moon_tide_ruins: {
    recommended_voyage_days: 25,
    recommended_cargo: 73.7,
    recommended_cargo_ratio: 0.67,
    fixed_event_point_count: 20,
    fixed_event_template_count: 70,
    random_event_template_count: 10,
    min_event_spacing_map_units: 9.5,
    min_event_spacing_nm: 228,
    ambush_chance_per_100nm: 0.018,
    ambush_detection_base_chance: 0.2,
    provision_pressure: "高",
    special_rule: "按种子选1条三阶段事件链，另抽取16个非链支线并加入银环号；海图记录链进度、一次性事件与衰减收益，月潮印记提高遗迹收益并同步强化首领与诅咒压力",
  },
};

const eventCrewLabels = [
  ["航海士", "航海士"], ["木匠", "木匠"], ["厨师", "厨师"], ["炮手", "炮手"],
  ["财务官", "财务官"], ["宝藏潜水员", "宝藏潜水员"], ["潜水员", "宝藏潜水员"],
  ["深海祭司", "深海祭司"], ["船长", "船长"], ["医生", "医生"], ["水手长", "水手长"],
  ["走私客", "走私客"], ["帝国逃兵", "帝国逃兵"],
];

function inferredCrew(choices) {
  const text = (choices || []).join("|");
  return Array.from(new Set(eventCrewLabels.filter(function (entry) {
    return text.includes(entry[0]);
  }).map(function (entry) { return entry[1]; }))).join("|");
}

function event(routeId, index, id, name, nodeType, rarity, row, col, options = {}) {
  return {
    route_id: routeId,
    event_index: index,
    event_id: id,
    name,
    node_type: nodeType,
    rarity,
    art_sheet: options.artSheet || assets[routeId].sheet,
    art_grid_rows: options.artGridRows || 4,
    art_grid_cols: options.artGridCols || 5,
    grid_row: row,
    grid_col: col,
    participation_mode: options.participation || "optional",
    trigger_mode: options.trigger || "fixed_visible",
    map_visibility: options.visibility || "vision_or_chart",
    random_rule_id: options.randomRule || "",
    random_chance_per_100nm: options.randomChance || 0,
    risk_level: options.risk || 1,
    power_check: options.power || 0,
    choice_a: options.choices[0],
    choice_b: options.choices[1],
    choice_c: options.choices[2],
    choice_a_hours: options.hours[0],
    choice_b_hours: options.hours[1],
    choice_c_hours: options.hours[2],
    main_reward: options.reward || "",
    penalty: options.penalty || "",
    required_crew: options.crew || inferredCrew(options.choices),
    weight: options.weight || 60,
    repeat_policy: options.repeatPolicy || "",
    repeatable: options.repeatable || "",
    reward_decay: options.rewardDecay || "",
    repeat_reward_multipliers: options.repeatMultipliers || "",
    repeat_scope: options.repeatScope || "",
    notes: options.notes + "；基础淡水与食物由选项耗时按船员日结算",
  };
}

const repeatProfiles = {
  once_per_chart: {
    repeatable: "no",
    rewardDecay: "exhausted",
    multipliers: "1|0",
    scope: "chart_seed_endpoints",
  },
  diminishing: {
    repeatable: "yes",
    rewardDecay: "diminishing",
    multipliers: "1|0.5|0.25|0",
    scope: "chart_seed_endpoints",
  },
  renewable: {
    repeatable: "yes",
    rewardDecay: "none",
    multipliers: "1",
    scope: "chart_seed_endpoints",
  },
  random_voyage: {
    repeatable: "yes",
    rewardDecay: "none",
    multipliers: "1",
    scope: "voyage",
  },
};

function repeatPolicyFor(record) {
  if (record.repeat_policy) return record.repeat_policy;
  if (record.trigger_mode === "random") return "random_voyage";
  const rewardText = String(record.main_reward || "") + "|" + String(record.choice_a || "") +
    "|" + String(record.choice_b || "") + "|" + String(record.choice_c || "");
  if (record.node_type === "boss_gate" || record.node_type === "treasure" || record.rarity === "rare" ||
      /黑珍珠|稀有遗物|遗物\+1|强力卡牌|复制1张牌/.test(rewardText)) return "once_per_chart";
  if (record.node_type === "shop" || record.node_type === "trade") return "renewable";
  return "diminishing";
}

function applyRepeatSettings(record) {
  const policy = repeatPolicyFor(record);
  const settings = repeatProfiles[policy] || repeatProfiles.diminishing;
  record.repeat_policy = policy;
  record.repeatable = record.repeatable || settings.repeatable;
  record.reward_decay = record.reward_decay || settings.rewardDecay;
  record.repeat_reward_multipliers = record.repeat_reward_multipliers || settings.multipliers;
  record.repeat_scope = record.repeat_scope || settings.scope;
  const marker = policy === "once_per_chart"
    ? "本海图仅首次可结算"
    : policy === "diminishing"
      ? "同海图收益100%/50%/25%/0%"
      : policy === "renewable"
        ? "可重复且收益不衰减"
        : "随机事件不计海图复刷";
  if (!String(record.notes || "").includes(marker)) record.notes = String(record.notes || "") + "；" + marker;
  return record;
}

const addedEvents = [
  event("route_shattered_sail_reef", 23, "event_reef_sounding_buoy", "风裂测深浮标", "event", "common", 0, 1, {
    risk: 1, weight: 78, choices: [
      "A校准浮标:显示2点 声望+6",
      "B拆取铜件:铁矿+14 声望-5",
      "C航海士重绘水深:显示3点",
    ], hours: [2, 3, 1], reward: "显示隐藏点+3|铁矿+14", penalty: "声望-5", crew: "航海士",
    notes: "固定显性情报事件；提高新手路线规划密度",
  }),
  event("route_shattered_sail_reef", 24, "event_reef_rain_cistern", "崖顶雨水池", "rest", "uncommon", 0, 4, {
    trigger: "fixed_hidden", visibility: "intel_or_encounter", risk: 1, weight: 58, choices: [
      "A接引雨水:淡水+80",
      "B清洗水池:木材-4 淡水+120",
      "C厨师过滤藻味:淡水+100 士气+1",
    ], hours: [4, 6, 3], reward: "淡水+120", penalty: "木材-4", crew: "厨师",
    notes: "固定隐形纯淡水补给点；不直接补充食物",
  }),
  event("route_shattered_sail_reef", 25, "event_reef_shellback_nest", "岩甲龟巢", "treasure", "uncommon", 2, 1, {
    participation: "optional_internal_exit", risk: 2, weight: 62, choices: [
      "A拾取空甲:铁矿+18",
      "B采集龟蛋:食物+40 声望-8",
      "C厨师只取散卵:食物+28 士气+1",
    ], hours: [3, 5, 4], reward: "铁矿+18|食物+40", penalty: "声望-8", crew: "厨师",
    notes: "固定显性生态取舍事件；提供纯食物补给分支",
  }),
  event("route_shattered_sail_reef", 26, "event_reef_powder_wreck", "火药舱残骸", "salvage", "uncommon", 1, 1, {
    trigger: "fixed_hidden", visibility: "intel_or_encounter", risk: 3, power: 85, weight: 55, choices: [
      "A湿拆火药桶:火药+10 船体-4",
      "B拖离暗礁再拆:木材-6 火药+15",
      "C炮手辨认干燥舱:火药+18 炮弹+8",
    ], hours: [4, 7, 3], reward: "火药+18|炮弹+8", penalty: "船体-4|木材-6", crew: "炮手",
    notes: "固定隐形高价值打捞；火药收益伴随船体风险",
  }),
  event("route_shattered_sail_reef", 27, "event_reef_storm_anchorage", "雷云锚地", "rest", "common", 3, 3, {
    participation: "mandatory", risk: 2, weight: 68, choices: [
      "A抛双锚等候:后续1航段受逆流影响",
      "B顶风穿过:船体-7",
      "C木匠加固索具:木材-5 船体+5",
    ], hours: [6, 2, 4], reward: "船体+5", penalty: "船体-7|逆流1航段", crew: "木匠",
    notes: "固定显性强制天气事件；主要成本来自耗时",
  }),
  event("route_shattered_sail_reef", 28, "event_reef_kelp_farm", "铜藻养殖架", "shop", "common", 0, 4, {
    risk: 1, weight: 72, choices: [
      "A购买晒藻饼:金币-35 食物+45",
      "B帮忙修架:木材-5 食物+35 声望+8",
      "C厨师交换配方:啤酒-1 食物+55",
    ], hours: [1, 3, 2], reward: "食物+55|声望+8", penalty: "金币-35|木材-5", crew: "厨师",
    notes: "固定显性食物补给与声望转换点",
  }),
  event("route_shattered_sail_reef", 29, "event_reef_splitmast_scout", "裂桅斥候艇", "battle", "uncommon", 2, 0, {
    trigger: "fixed_hidden", visibility: "intel_or_encounter", participation: "mandatory", risk: 3, power: 98, weight: 48, choices: [
      "A熄灯停船:后续1航段受逆流影响",
      "B迎战斥候艇:战斗胜利金币+125",
      "C炮手压制桅杆:降低战斗难度 声望+12",
    ], hours: [4, 2, 1], reward: "金币+125|声望+12", penalty: "逆流1航段|战斗失败", crew: "炮手",
    notes: "固定隐形精英战；削弱过度依赖随机伏击的战斗供给",
  }),
  event("route_shattered_sail_reef", 30, "event_reef_barrel_trail", "漂桶水迹", "event", "common", 0, 3, {
    trigger: "fixed_hidden", visibility: "intel_or_encounter", risk: 1, weight: 70, choices: [
      "A逐桶打捞:淡水+55",
      "B循迹找补给船:显示2点 淡水+30",
      "C航海士判断来向:显示3点",
    ], hours: [4, 3, 1], reward: "淡水+55|显示隐藏点+3", penalty: "", crew: "航海士",
    notes: "固定隐形纯淡水与情报二选一事件",
  }),
  event("route_shattered_sail_reef", 31, "event_reef_collapsed_arch", "珊瑚塌门", "event", "uncommon", 3, 2, {
    trigger: "fixed_hidden", visibility: "intel_or_encounter", participation: "mandatory", risk: 3, power: 92, weight: 52, choices: [
      "A停船清理:无额外效果",
      "B撞开缺口:船体-9",
      "C木匠搭滑木:木材-7 船体+2",
    ], hours: [8, 2, 4], reward: "船体+2", penalty: "耗时8小时|船体-9", crew: "木匠",
    notes: "固定隐形强制障碍；安全分支只消耗时间口粮",
  }),
  event("route_shattered_sail_reef", 32, "event_reef_tidewright_platform", "潮工浮台", "shop", "rare", 3, 1, {
    risk: 1, weight: 42, choices: [
      "A购买修理锤:金币-95 修理锤+2",
      "B出售废铁:铁矿-12 金币+105",
      "C木匠交换工艺:木材-8 本局改造费-20%",
    ], hours: [1, 1, 3], reward: "修理锤+2|金币+105|改造费降低", penalty: "金币-95|木材-8", crew: "木匠",
    notes: "固定显性稀有经济点；强化打捞到维修的转换",
  }),

  event("route_golden_tide_trade", 23, "event_trade_floating_exchange", "海上汇兑所", "trade", "common", 0, 0, {
    risk: 1, weight: 82, choices: [
      "A兑现远期票据:契约进度+1 金币+45",
      "B兑换港币:金币-60 商会好感+1",
      "C财务官套汇:金币+90 追击+1",
    ], hours: [1, 1, 2], reward: "契约进度+1|金币+90|商会好感+1", penalty: "追击+1", crew: "财务官",
    notes: "固定显性契约推进与风险套利事件",
  }),
  event("route_golden_tide_trade", 24, "event_trade_quarantine_flag", "检疫黄旗", "event", "common", 0, 1, {
    participation: "mandatory", risk: 2, weight: 72, choices: [
      "A停船受检:药品-1 商会好感+1",
      "B绕开检疫线:追击+1",
      "C厨师检查粮舱:食物-10 声望+10",
    ], hours: [5, 2, 2], reward: "商会好感+1|声望+10", penalty: "药品-1|追击+1|食物-10", crew: "厨师",
    notes: "固定显性强制行政事件；安全分支主要消耗时间",
  }),
  event("route_golden_tide_trade", 25, "event_trade_spice_barge", "香料驳船搁浅", "salvage", "uncommon", 1, 1, {
    trigger: "fixed_hidden", visibility: "intel_or_encounter", risk: 2, weight: 62, choices: [
      "A申报后打捞:金币+85 商会好感+1",
      "B私吞香料:金币+155 追击+1",
      "C财务官联系货主:契约进度+1 金币+110",
    ], hours: [5, 3, 3], reward: "金币+155|契约进度+1", penalty: "追击+1", crew: "财务官",
    notes: "固定隐形经济事件；合法、走私与契约三分支",
  }),
  event("route_golden_tide_trade", 26, "event_trade_wedding_fleet", "商船婚礼", "event", "uncommon", 3, 0, {
    risk: 1, weight: 56, choices: [
      "A送上贺礼:金币-60 士气+2 商会好感+1",
      "B提供啤酒:啤酒-2 声望+15",
      "C厨师承办宴席:食物-30 金币+120 士气+1",
    ], hours: [3, 4, 6], reward: "士气+2|金币+120|声望+15", penalty: "金币-60|食物-30", crew: "厨师",
    notes: "固定显性社交事件；把补给转化为声望和金币",
  }),
  event("route_golden_tide_trade", 27, "event_trade_false_checkpoint", "假税卡", "battle", "uncommon", 0, 1, {
    trigger: "fixed_hidden", visibility: "intel_or_encounter", participation: "mandatory", risk: 3, power: 120, weight: 48, choices: [
      "A停船辨认:追击+1",
      "B揭穿后迎战:战斗胜利金币+145",
      "C财务官查验印章:金币+55 声望+8",
    ], hours: [4, 2, 1], reward: "金币+145|声望+8", penalty: "追击+1|战斗失败", crew: "财务官",
    notes: "固定隐形强制骗局；为商路提供可预判战斗",
  }),
  event("route_golden_tide_trade", 28, "event_trade_pearl_appraiser", "珍珠鉴定船", "shop", "uncommon", 2, 3, {
    risk: 1, weight: 60, choices: [
      "A出售宝石:宝石-1 金币+190",
      "B购买鉴定证:金币-90 契约卷轴+1",
      "C财务官议价:金币-65 契约卷轴+1",
    ], hours: [1, 1, 1], reward: "金币+190|契约卷轴+1", penalty: "宝石-1|金币-90", crew: "财务官",
    notes: "固定显性特色货物兑换点",
  }),
  event("route_golden_tide_trade", 29, "event_trade_freeport_broker", "自由港掮客", "trade", "rare", 1, 4, {
    trigger: "fixed_hidden", visibility: "intel_or_encounter", risk: 2, weight: 44, choices: [
      "A购买暗仓坐标:金币-100 显示3点",
      "B出售商会情报:商会好感-1 金币+160",
      "C财务官牵线:黑市折扣+15% 追击+1",
    ], hours: [2, 2, 1], reward: "显示隐藏点+3|金币+160|黑市折扣", penalty: "商会好感-1|追击+1", crew: "财务官",
    notes: "固定隐形稀有信息交易点",
  }),
  event("route_golden_tide_trade", 30, "event_trade_convoy_distress", "护航船遇险", "battle", "uncommon", 2, 0, {
    risk: 3, power: 130, weight: 52, choices: [
      "A拖走伤船:木材-8 契约进度+1 商会好感+1",
      "B迎击追兵:战斗胜利金币+165 契约进度+1",
      "C炮手掩护撤离:降低战斗难度 声望+15",
    ], hours: [6, 2, 1], reward: "金币+165|契约进度+1|商会好感+1", penalty: "木材-8|战斗失败", crew: "炮手",
    notes: "固定显性护航战；契约路线的稳定战斗来源",
  }),
  event("route_golden_tide_trade", 31, "event_trade_dry_market", "枯水集市", "shop", "common", 2, 3, {
    risk: 1, weight: 68, choices: [
      "A购买淡水:金币-45 淡水+80",
      "B批量采购:金币-80 淡水+150",
      "C财务官赊购:淡水+100 追击+1",
    ], hours: [1, 2, 1], reward: "淡水+150", penalty: "金币-80|追击+1", crew: "财务官",
    notes: "固定显性纯淡水市场；价格高于起航港采购",
  }),
  event("route_golden_tide_trade", 32, "event_trade_closing_bell", "收市钟潮", "event", "uncommon", 2, 1, {
    participation: "mandatory", risk: 2, weight: 58, choices: [
      "A等候次日开市:契约进度+1",
      "B支付夜航费:金币-70",
      "C航海士追上潮窗:显示2点",
    ], hours: [8, 1, 2], reward: "契约进度+1|显示隐藏点+2", penalty: "耗时8小时|金币-70", crew: "航海士",
    notes: "固定显性强制时间事件；检验补给与契约期限",
  }),

  event("route_moon_tide_ruins", 23, "event_moon_well_siphon", "月井虹吸", "ruin", "uncommon", 0, 1, {
    trigger: "fixed_hidden", visibility: "intel_or_encounter", participation: "mandatory", risk: 3, power: 155, weight: 56, choices: [
      "A截断虹吸:后续1航段受逆流影响",
      "B顺流汲取银潮:淡水+90 印记+1",
      "C航海士逆转虹吸:淡水+55 显示2点",
    ], hours: [5, 4, 2], reward: "淡水+90|显示隐藏点+2", penalty: "逆流1航段|印记+1", crew: "航海士",
    notes: "固定隐形强制纯淡水事件；收益分支积累月潮印记",
  }),
  event("route_moon_tide_ruins", 24, "event_moon_barnacle_oracle", "藤壶神谕", "ruin", "uncommon", 1, 0, {
    trigger: "fixed_hidden", visibility: "intel_or_encounter", risk: 2, weight: 58, choices: [
      "A听取低语:显示3点 诅咒+1",
      "B刮下符壳:古代材料+2",
      "C航海士译读:显示3点 声望+12",
    ], hours: [2, 4, 2], reward: "显示隐藏点+3|古代材料+2|声望+12", penalty: "诅咒牌+1", crew: "航海士",
    notes: "固定隐形情报与材料事件",
  }),
  event("route_moon_tide_ruins", 25, "event_moon_drowned_choir", "沉歌回廊", "ruin", "uncommon", 1, 3, {
    trigger: "fixed_hidden", visibility: "intel_or_encounter", risk: 3, power: 160, weight: 52, choices: [
      "A听完古歌:随机祝福 诅咒+1",
      "B堵住回声孔:木材-6 士气+1",
      "C厨师调制醒神汤:啤酒-1 随机祝福 印记-1",
    ], hours: [4, 3, 2], reward: "随机祝福|印记-1", penalty: "诅咒牌+1|木材-6|啤酒-1", crew: "厨师",
    notes: "固定隐形诅咒调节事件",
  }),
  event("route_moon_tide_ruins", 26, "event_moon_bronze_leviathan", "铜铸海兽", "battle", "rare", 2, 2, {
    trigger: "fixed_hidden", visibility: "intel_or_encounter", participation: "mandatory", risk: 4, power: 190, weight: 40, choices: [
      "A伏低船帆:后续2航段受逆流影响",
      "B迎战铜铸海兽:战斗胜利古代材料+3",
      "C炮手击断关节:降低战斗难度 古代材料+2",
    ], hours: [6, 3, 2], reward: "古代材料+3", penalty: "逆流2航段|战斗失败", crew: "炮手",
    notes: "固定隐形稀有精英战；产出首领升级材料",
  }),
  event("route_moon_tide_ruins", 27, "event_moon_starfall_shard", "坠星礁", "treasure", "rare", 0, 2, {
    trigger: "fixed_hidden", visibility: "intel_or_encounter", risk: 3, weight: 42, choices: [
      "A开凿星铁:铁矿+28 印记+1",
      "B收集星屑:宝石+1 古代材料+1",
      "C宝藏潜水员寻核心:黑珍珠+1",
    ], hours: [6, 4, 5], reward: "黑珍珠+1|宝石+1|古代材料+1", penalty: "印记+1", crew: "宝藏潜水员",
    notes: "固定隐形稀有宝藏；三类特色资源分流",
  }),
  event("route_moon_tide_ruins", 28, "event_moon_tidesleep_camp", "潮眠营地", "rest", "uncommon", 3, 1, {
    risk: 1, weight: 50, choices: [
      "A让船员沉睡:清除受伤 士气+1",
      "B采集夜露:淡水+70",
      "C厨师守夜:清除受伤 食物+20",
    ], hours: [8, 5, 6], reward: "清除受伤|淡水+70|食物+20", penalty: "长时间停留", crew: "厨师",
    notes: "固定显性恢复点；用航期换伤病与淡水",
  }),
  event("route_moon_tide_ruins", 29, "event_moon_mirror_double", "镜影船员", "event", "uncommon", 2, 0, {
    trigger: "fixed_hidden", visibility: "intel_or_encounter", participation: "mandatory", risk: 3, power: 170, weight: 50, choices: [
      "A与镜影对视:复制1张牌 诅咒+1",
      "B打碎镜面:船体-6 士气+1",
      "C航海士辨认本体:复制1张牌 无额外效果",
    ], hours: [3, 1, 2], reward: "复制1张牌|士气+1", penalty: "诅咒牌+1|船体-6", crew: "航海士",
    notes: "固定隐形强制卡组成长事件",
  }),
  event("route_moon_tide_ruins", 30, "event_moon_relic_diver", "遗物潜客", "event", "uncommon", 2, 3, {
    participation: "optional_internal_exit", risk: 2, weight: 54, choices: [
      "A购买遗物线索:金币-120 显示3点",
      "B交换潜水装备:修理锤-1 古代罗盘+1",
      "C宝藏潜水员合作:藏宝图碎片+2 声望+10",
    ], hours: [2, 3, 4], reward: "古代罗盘+1|藏宝图碎片+2|声望+10", penalty: "金币-120|修理锤-1", crew: "宝藏潜水员",
    notes: "固定显性职业事件；连接遗迹资源与藏宝图循环",
  }),
  event("route_moon_tide_ruins", 31, "event_moon_current_well", "月流井", "ruin", "uncommon", 2, 4, {
    trigger: "fixed_hidden", visibility: "intel_or_encounter", participation: "mandatory", risk: 3, power: 165, weight: 50, choices: [
      "A抛锚测流:后续2航段受逆流影响",
      "B借潮跃迁:船体-8 显示2点",
      "C航海士锁定潮窗:显示3点",
    ], hours: [5, 2, 1], reward: "显示隐藏点+3", penalty: "逆流2航段|船体-8", crew: "航海士",
    notes: "固定隐形强制路径事件",
  }),
  event("route_moon_tide_ruins", 32, "event_moon_obsidian_toll", "黑曜门税", "ruin", "rare", 0, 0, {
    trigger: "fixed_hidden", visibility: "intel_or_encounter", participation: "mandatory", risk: 4, power: 185, weight: 38, choices: [
      "A献上诅咒金币:诅咒金币-1 遗物+1",
      "B强行破门:船体-12 古代材料+2",
      "C宝藏潜水员寻找暗槽:印记-1 遗物+1",
    ], hours: [2, 4, 3], reward: "遗物+1|古代材料+2|印记-1", penalty: "诅咒金币-1|船体-12", crew: "宝藏潜水员",
    notes: "固定隐形稀有资源门；消耗诅咒产物换取稳定遗物",
  }),
];

const expansionBlueprints = {
  route_shattered_sail_reef: [
    ["flooded_belfry", "没潮钟楼", "intel"],
    ["drift_rope_beacon", "漂绳信标", "intel"],
    ["windworn_markers", "风蚀航标阵", "intel"],
    ["whalebone_gauge", "鲸骨测流台", "intel"],
    ["salt_crane", "盐蚀起重机", "salvage"],
    ["ship_nail_beach", "船钉滩", "salvage"],
    ["sunken_rudder_hold", "沉舵仓", "salvage"],
    ["seabed_cable_reel", "海底缆盘", "salvage"],
    ["cliff_fresh_spring", "崖缝淡泉", "supply"],
    ["rain_sail_camp", "雨帆营地", "supply"],
    ["kelp_drying_field", "海藻晒场", "supply"],
    ["turtle_cistern", "龟背水窖", "supply"],
    ["coral_sluice", "珊瑚闸口", "obstacle"],
    ["broken_anchor_maze", "断锚迷阵", "obstacle"],
    ["capsized_ferry", "倾覆浮桥", "obstacle"],
    ["narrow_tide_gate", "窄潮石门", "obstacle"],
    ["redclaw_watch", "红钳守礁队", "battle"],
    ["eel_hunter", "电鳗猎船", "battle"],
    ["split_sail_tollboat", "裂帆收税艇", "battle"],
    ["black_reef_battery", "黑礁炮台", "battle"],
    ["tidewright_exchange", "潮工换料站", "trade"],
    ["shell_coin_market", "贝币小市", "trade"],
    ["seafolk_rope_shop", "海民绳铺", "trade"],
    ["raft_repair_shop", "浮筏修理铺", "trade"],
    ["still_tide_berth", "静潮泊地", "rest"],
    ["warm_reef_pool", "暖泉礁池", "rest"],
    ["seabird_roost", "海鸟栖台", "rest"],
    ["star_sand_fire", "星沙营火", "rest"],
    ["sea_glass_shrine", "海玻璃神龛", "treasure"],
    ["whale_belly_hold", "鲸腹密舱", "treasure"],
    ["smuggler_culvert", "走私暗渠", "treasure"],
    ["coral_ore_vein", "珊瑚矿脉", "treasure"],
    ["floating_drydock", "漂浮船坞", "repair"],
    ["sunken_woodshop", "沉木工坊", "repair"],
    ["iron_ring_pier", "铁环栈桥", "repair"],
    ["old_sail_stitchery", "旧帆缝场", "repair"],
    ["blue_crab_nursery", "蓝蟹育床", "ecology"],
    ["storm_petrel_flock", "风暴海燕群", "ecology"],
    ["armored_turtle_migration", "甲龟洄游", "ecology"],
    ["giant_kelp_bloom", "巨藻花海", "ecology"],
  ],
  route_golden_tide_trade: [
    ["floating_spice_auction", "浮香拍卖台", "market"],
    ["futures_office", "远洋票据所", "market"],
    ["silk_barge", "绸卷驳船", "market"],
    ["pearl_scale_ship", "珍珠称量船", "market"],
    ["chain_customs_gate", "链闸税关", "customs"],
    ["night_inspection", "夜航查验站", "customs"],
    ["quarantine_platform", "检疫浮台", "customs"],
    ["navy_seal_ship", "海军封签船", "customs"],
    ["drifting_granary", "漂粮仓", "salvage"],
    ["stalled_courier", "失速信使船", "salvage"],
    ["insurance_wreck", "沉箱保险船", "salvage"],
    ["torn_silk_hold", "破舱丝货", "salvage"],
    ["false_flag_interceptors", "伪旗截船队", "battle"],
    ["unpaid_mercenary", "欠饷雇佣舰", "battle"],
    ["smuggler_gunboat", "走私火炮艇", "battle"],
    ["gold_chain_privateer", "金链私掠船", "battle"],
    ["lost_convoy", "迷航商队", "escort"],
    ["wedding_escort", "婚礼护航船", "escort"],
    ["medicine_convoy", "药材船队", "escort"],
    ["refugee_transport", "难民运输船", "escort"],
    ["water_broker", "淡水经纪船", "supply"],
    ["biscuit_bakery", "烘饼作坊船", "supply"],
    ["ale_cold_barge", "啤酒冷藏驳", "supply"],
    ["timber_cooperative", "木料合作社", "supply"],
    ["exchange_signal_tower", "汇率信号塔", "intel"],
    ["lantern_market_tip", "灯笼夜市密报", "intel"],
    ["shipping_guild_desk", "航运公会台", "intel"],
    ["storm_schedule_office", "风暴排期所", "intel"],
    ["floating_banquet", "漂浮宴会厅", "social"],
    ["crew_recruiter_ship", "船员招聘船", "social"],
    ["contract_notary", "赎契公证船", "social"],
    ["sea_theatre", "海上剧团", "social"],
    ["marine_insurance", "海险理赔所", "finance"],
    ["counterfeit_foundry", "假币熔炉", "finance"],
    ["bond_burning_dock", "债券焚毁台", "finance"],
    ["gold_scale_fort", "金秤结算堡", "finance"],
    ["harbor_thunderstorm", "封港雷暴", "weather"],
    ["countertide_jam", "逆潮拥堵", "weather"],
    ["burning_warehouse", "失火货栈", "weather"],
    ["broken_chain_channel", "断链航道", "weather"],
  ],
  route_moon_tide_ruins: [
    ["lunar_observatory", "月相观测台", "ruin"],
    ["drowned_court", "沉没王庭", "ruin"],
    ["silver_tide_organ", "银潮管风琴", "ruin"],
    ["star_track_ring", "星轨石环", "ruin"],
    ["obsidian_pearl_altar", "黑珍珠祭坛", "curse"],
    ["mirror_tomb", "镜影墓廊", "curse"],
    ["silent_banquet", "无声宴会船", "curse"],
    ["tide_curse_library", "潮咒经库", "curse"],
    ["bronze_gatekeeper", "青铜守门人", "battle"],
    ["moon_jelly_swarm", "月蛰水母群", "battle"],
    ["ghost_ferry", "幽灵渡船", "battle"],
    ["silver_guard", "银甲祭卫", "battle"],
    ["star_iron_forge", "星铁锻炉", "treasure"],
    ["chained_relic_barge", "锁链遗物舟", "treasure"],
    ["abyss_diving_bell", "深渊潜钟", "treasure"],
    ["moon_crown_vault", "月冠宝库", "treasure"],
    ["eclipse_mirror_gate", "蚀月镜门", "puzzle"],
    ["tide_clock", "潮汐时钟", "puzzle"],
    ["broken_moon_bridge", "断月石桥", "puzzle"],
    ["sealed_great_door", "封印巨门", "puzzle"],
    ["crescent_well", "新月井", "rest"],
    ["silent_courtyard", "静默庭院", "rest"],
    ["silver_algae_bed", "银藻温床", "rest"],
    ["dreamless_anchorage", "无梦锚地", "rest"],
    ["bone_lighthouse", "骸骨灯塔", "intel"],
    ["flooded_archive", "淹没档案库", "intel"],
    ["tide_star_room", "潮纹星图室", "intel"],
    ["echo_steles", "回声碑林", "intel"],
    ["moon_salt_altar", "月盐圣坛", "ritual"],
    ["inverted_candles", "倒悬烛台", "ritual"],
    ["starlight_offering", "星辉献祭池", "ritual"],
    ["tide_prayer_hall", "巡潮祷堂", "ritual"],
    ["sunken_instruments", "沉没仪器库", "salvage"],
    ["broken_chain_statue", "断链铜像", "salvage"],
    ["ruin_drain_wheel", "遗迹排水轮", "salvage"],
    ["star_sand_store", "星砂储藏井", "salvage"],
    ["reverse_waterfall", "倒流瀑布", "anomaly"],
    ["double_moon_reflection", "双月倒影", "anomaly"],
    ["time_rift_tide", "时间裂潮", "anomaly"],
    ["shadowless_dock", "无影船坞", "anomaly"],
  ],
};

const expansionNodeTypes = {
  intel: "event", salvage: "salvage", supply: "shop", obstacle: "event", battle: "battle",
  trade: "trade", rest: "rest", treasure: "treasure", repair: "salvage", ecology: "event",
  market: "trade", customs: "event", escort: "event", social: "event", finance: "trade",
  weather: "event", ruin: "ruin", curse: "ruin", puzzle: "ruin", ritual: "ruin", anomaly: "event",
};
const expansionMandatory = new Set(["obstacle", "battle", "customs", "weather", "puzzle", "anomaly"]);
const expansionHidden = new Set(["salvage", "battle", "treasure", "finance", "ruin", "curse", "ritual"]);
const expansionRenewable = new Set(["trade", "market", "supply", "customs", "obstacle", "weather", "puzzle", "anomaly"]);
const expansionCrew = {
  intel: "航海士", salvage: "木匠", supply: "厨师", obstacle: "木匠", battle: "炮手",
  trade: "财务官", rest: "厨师", treasure: "宝藏潜水员", repair: "木匠", ecology: "厨师",
  market: "财务官", customs: "财务官", escort: "炮手", social: "厨师", finance: "财务官",
  weather: "航海士", ruin: "航海士", curse: "厨师", puzzle: "航海士", ritual: "宝藏潜水员",
  anomaly: "航海士",
};

function expansionChoiceSet(routeId, archetype, variant) {
  const step = variant + 1;
  if (routeId === "route_shattered_sail_reef") {
    const sets = {
      intel: { choices: ["A测绘潮线:显示2点 声望+" + (5 + step), "B拆取旧标:铁矿+" + (10 + step * 2), "C航海士校正:显示3点 声望+" + (8 + step)], hours: [2, 3, 1], reward: "显示隐藏点+3|声望|铁矿", penalty: "" },
      salvage: { choices: ["A清理外围:木材+" + (22 + step * 3), "B潜入残骸:铁矿+" + (14 + step * 2) + " 船体-" + (3 + step), "C木匠拆取承力件:木材+" + (16 + step * 2) + " 修理锤+1"], hours: [4, 6, 3], reward: "木材|铁矿|修理锤", penalty: "船体损伤" },
      supply: { choices: ["A购买淡水:金币-" + (28 + step * 3) + " 淡水+" + (55 + step * 8), "B帮忙整修:木材-5 淡水+" + (75 + step * 8), "C厨师整理储粮:啤酒-1 食物+" + (30 + step * 5)], hours: [1, 3, 2], reward: "淡水|食物", penalty: "金币|木材|啤酒" },
      obstacle: { choices: ["A停船等潮:无额外效果", "B顶浪穿行:船体-" + (5 + step), "C木匠铺设滑木:木材-" + (4 + step) + " 船体+2"], hours: [6, 2, 3], reward: "船体+2", penalty: "耗时|船体|木材" },
      battle: { choices: ["A藏入礁沟:后续1航段受逆流影响", "B正面迎战:战斗胜利金币+" + (105 + step * 12), "C炮手先制压制:降低战斗难度 声望+" + (8 + step)], hours: [4, 2, 1], reward: "金币|声望", penalty: "逆流|战斗失败" },
      trade: { choices: ["A购买修材:金币-" + (45 + step * 5) + " 木材+" + (12 + step * 2), "B出售废铁:铁矿-" + (8 + step) + " 金币+" + (70 + step * 6), "C财务官换取情报:啤酒-1 显示2点"], hours: [1, 1, 2], reward: "木材|金币|情报", penalty: "等价交易" },
      rest: { choices: ["A靠泊休息:士气+1", "B修补船壳:木材-" + (4 + step) + " 船体+" + (6 + step), "C厨师准备热食:食物-" + (8 + step * 2) + " 士气+2"], hours: [6, 4, 5], reward: "士气|船体", penalty: "耗时|木材|食物" },
      treasure: { choices: ["A谨慎开掘:宝石+1", "B拆走古物:铁矿+" + (20 + step * 3) + " 诅咒+1", "C宝藏潜水员寻主藏:古代材料+" + (2 + step)], hours: [5, 4, 3], reward: "宝石|古代材料|铁矿", penalty: "诅咒" },
      repair: { choices: ["A回收旧板:木材+" + (18 + step * 2), "B现场抢修:木材-" + (5 + step) + " 船体+" + (8 + step), "C木匠改良结构:修理锤-1 船体+" + (12 + step)], hours: [3, 4, 3], reward: "木材|船体", penalty: "木材|修理锤" },
      ecology: { choices: ["A保持距离观察:声望+" + (6 + step), "B采集可食部分:食物+" + (20 + step * 5), "C厨师可持续采集:食物+" + (16 + step * 4) + " 士气+1"], hours: [2, 4, 3], reward: "声望|食物|士气", penalty: "" },
    };
    return sets[archetype];
  }
  if (routeId === "route_golden_tide_trade") {
    const sets = {
      market: { choices: ["A小额采购:金币-" + (55 + step * 5) + " 布料+" + (10 + step * 2), "B转售货单:布料-" + (8 + step) + " 金币+" + (80 + step * 8), "C财务官套利:金币+" + (42 + step * 5) + " 追击+1"], hours: [1, 1, 2], reward: "贸易差价", penalty: "金币|布料|追击" },
      customs: { choices: ["A排队受检:声望+" + (5 + step), "B缴纳通行费:金币-" + (35 + step * 6), "C财务官核对印契:契约进度+1"], hours: [5, 1, 2], reward: "声望|契约进度", penalty: "耗时|金币" },
      salvage: { choices: ["A登记打捞:金币+" + (70 + step * 8) + " 商会好感+1", "B私吞货物:布料+" + (15 + step * 3) + " 追击+1", "C木匠稳住船舱:木材-" + (5 + step) + " 契约进度+1"], hours: [5, 3, 4], reward: "金币|布料|契约进度", penalty: "追击|木材" },
      battle: { choices: ["A交付买路钱:金币-" + (60 + step * 8), "B迎击敌船:战斗胜利金币+" + (135 + step * 12), "C炮手切断索具:降低战斗难度 声望+" + (10 + step)], hours: [1, 2, 1], reward: "金币|声望", penalty: "金币|战斗失败" },
      escort: { choices: ["A拖带掉队船:木材-" + (5 + step) + " 契约进度+1", "B护送至航标:商会好感+1 声望+" + (8 + step), "C炮手驱散尾随者:金币+" + (65 + step * 6)], hours: [6, 4, 2], reward: "契约进度|商会好感|金币", penalty: "木材|耗时" },
      supply: { choices: ["A按市价补给:金币-" + (35 + step * 4) + " 淡水+" + (65 + step * 10), "B批量采购食物:金币-" + (45 + step * 5) + " 食物+" + (55 + step * 8), "C厨师交换配方:啤酒-1 食物+" + (35 + step * 6)], hours: [1, 2, 2], reward: "淡水|食物", penalty: "金币|啤酒" },
      intel: { choices: ["A购买航运消息:金币-" + (30 + step * 4) + " 显示2点", "B上报异常:声望+" + (8 + step) + " 商会好感+1", "C航海士核对潮表:显示3点"], hours: [1, 2, 1], reward: "情报|声望|商会好感", penalty: "金币" },
      social: { choices: ["A参加聚会:啤酒-1 士气+1", "B提供物资:食物-" + (12 + step * 2) + " 声望+" + (10 + step), "C厨师承办餐席:食物-" + (18 + step * 2) + " 金币+" + (75 + step * 6)], hours: [3, 4, 6], reward: "士气|声望|金币", penalty: "啤酒|食物" },
      finance: { choices: ["A提交凭证:契约进度+1 金币+" + (45 + step * 5), "B销毁账册:追击-1 声望-" + (4 + step), "C财务官重算账目:金币+" + (90 + step * 7) + " 商会好感+1"], hours: [2, 3, 2], reward: "金币|契约进度|商会好感", penalty: "声望" },
      weather: { choices: ["A等待放行:无额外效果", "B支付应急引航:金币-" + (45 + step * 5), "C航海士寻找潮窗:显示2点"], hours: [8, 1, 2], reward: "情报", penalty: "耗时|金币" },
    };
    return sets[archetype];
  }
  const sets = {
    ruin: { choices: ["A拓印结构:显示2点 声望+" + (7 + step), "B拆取机关:古代材料+" + (1 + step), "C航海士复原星位:显示3点 印记-1"], hours: [3, 5, 2], reward: "情报|古代材料|印记降低", penalty: "" },
    curse: { choices: ["A聆听低语:随机祝福 诅咒+1", "B封闭回声孔:木材-" + (4 + step) + " 士气+1", "C厨师调制醒神汤:啤酒-1 印记-1"], hours: [4, 3, 2], reward: "随机祝福|士气|印记降低", penalty: "诅咒|木材|啤酒" },
    battle: { choices: ["A退入月雾:后续2航段受逆流影响", "B唤醒并迎战:战斗胜利古代材料+" + (2 + step), "C炮手击碎核心:降低战斗难度 声望+" + (12 + step)], hours: [5, 3, 2], reward: "古代材料|声望", penalty: "逆流|战斗失败" },
    treasure: { choices: ["A开启外层匣:宝石+1 古代材料+" + step, "B强拆封印:船体-" + (6 + step) + " 遗物+1", "C宝藏潜水员寻找暗槽:黑珍珠+1"], hours: [5, 3, 4], reward: "宝石|古代材料|遗物|黑珍珠", penalty: "船体" },
    puzzle: { choices: ["A停船推演:显示2点", "B强行通过:船体-" + (7 + step), "C航海士校准机关:显示3点 印记-1"], hours: [7, 2, 3], reward: "情报|印记降低", penalty: "耗时|船体" },
    rest: { choices: ["A沉入安眠:士气+2", "B收集月露:淡水+" + (45 + step * 8), "C厨师守夜:清除受伤 食物+" + (12 + step * 3)], hours: [8, 5, 6], reward: "士气|淡水|食物|治疗", penalty: "耗时" },
    intel: { choices: ["A记录潮纹:显示3点", "B拆取铭牌:古代材料+" + (1 + step), "C航海士译读碑文:显示3点 声望+" + (10 + step)], hours: [3, 4, 2], reward: "情报|古代材料|声望", penalty: "" },
    ritual: { choices: ["A献上啤酒:啤酒-1 随机祝福", "B献上诅咒金币:诅咒金币-1 遗物+1", "C宝藏潜水员开启祭槽:古代材料+" + (2 + step) + " 印记-1"], hours: [2, 2, 3], reward: "祝福|遗物|古代材料|印记降低", penalty: "啤酒|诅咒金币" },
    salvage: { choices: ["A回收外层金属:铁矿+" + (18 + step * 3), "B深入拆解:船体-" + (4 + step) + " 古代材料+" + (2 + step), "C木匠稳固结构:木材-" + (5 + step) + " 修理锤+1"], hours: [4, 6, 3], reward: "铁矿|古代材料|修理锤", penalty: "船体|木材" },
    anomaly: { choices: ["A抛锚观察:后续1航段受逆流影响", "B借异常跃迁:船体-" + (6 + step) + " 显示2点", "C航海士锁定出口:显示3点"], hours: [5, 2, 1], reward: "情报", penalty: "逆流|船体" },
  };
  return sets[archetype];
}

Object.entries(expansionBlueprints).forEach(([routeId, blueprints]) => {
  const routePower = routeId === "route_shattered_sail_reef" ? 82 :
    routeId === "route_golden_tide_trade" ? 128 : 172;
  blueprints.forEach((blueprint, ordinal) => {
    const [suffix, name, archetype] = blueprint;
    const eventIndex = 33 + ordinal;
    const variant = ordinal % 4;
    const rare = (ordinal + 1) % 10 === 0;
    const repeatPolicy = rare || archetype === "treasure"
      ? "once_per_chart"
      : expansionRenewable.has(archetype) ? "renewable" : "diminishing";
    const repeat = repeatProfiles[repeatPolicy];
    const choiceSet = expansionChoiceSet(routeId, archetype, variant);
    const hidden = expansionHidden.has(archetype) || ordinal % 2 === 1;
    const mandatory = expansionMandatory.has(archetype);
    const cell = ordinal % 20;
    addedEvents.push(event(routeId, eventIndex, "event_" +
      (routeId === "route_shattered_sail_reef" ? "reef_" : routeId === "route_golden_tide_trade" ? "trade_" : "moon_") +
      suffix, name, expansionNodeTypes[archetype], rare ? "rare" : ordinal % 3 === 0 ? "common" : "uncommon",
      Math.floor(cell / 5), cell % 5, {
        artSheet: assets[routeId].expansionSheet,
        participation: mandatory ? "mandatory" : "optional_internal_exit",
        trigger: hidden ? "fixed_hidden" : "fixed_visible",
        visibility: hidden ? "intel_or_encounter" : "vision_or_chart",
        risk: rare ? 4 : mandatory ? 3 : ordinal % 3 + 1,
        power: archetype === "battle" ? routePower + variant * 8 : mandatory ? Math.round(routePower * 0.8) : 0,
        weight: rare ? 34 : ordinal % 3 === 0 ? 72 : 56,
        choices: choiceSet.choices,
        hours: choiceSet.hours,
        reward: choiceSet.reward,
        penalty: choiceSet.penalty,
        crew: expansionCrew[archetype] || "",
        repeatPolicy: repeatPolicy,
        repeatable: repeat.repeatable,
        rewardDecay: repeat.rewardDecay,
        repeatMultipliers: repeat.multipliers,
        repeatScope: repeat.scope,
        notes: "70事件扩展：" + name + "；" +
          (repeatPolicy === "once_per_chart" ? "本海图仅首次可结算" :
            repeatPolicy === "diminishing" ? "同海图重复收益依次为100%/50%/25%/0%" :
            "等价交易或航路障碍，可重复且收益不衰减"),
      }));
  });
});

const ambientRandomBlueprints = {
  route_shattered_sail_reef: [
    ["sudden_squall", "突发礁雨", "event", 2, 0.0020, "mandatory", "", ["A收帆等待:无额外效果", "B穿雨航行:船体-6", "C航海士读取云隙:显示2点"], [4, 1.5, 1], "显示隐藏点+2", "耗时|船体"],
    ["rigging_snap", "索具断裂", "event", 2, 0.0018, "mandatory", "木匠", ["A减帆修理:木材-5", "B带伤前进:后续1航段受逆流影响", "C木匠更换支索:木材-3 船体+3"], [3, 1, 2], "船体+3", "木材|逆流"],
    ["freshwater_slick", "漂桶淡水", "rest", 1, 0.0018, "optional", "厨师", ["A收集完整水桶:淡水+45", "B循迹扩大打捞:淡水+70", "C厨师过滤桶水:啤酒-1 淡水+65 士气+1"], [2, 4, 2], "淡水+70|士气+1", "啤酒-1"],
    ["shoal_fish_run", "鱼群迁徙", "rest", 1, 0.0016, "optional", "厨师", ["A撒网短捕:食物+24", "B追逐鱼群:食物+42", "C厨师挑选成鱼:食物+34 声望+6"], [2, 5, 3], "食物+42|声望+6", "耗时"],
    ["crew_fever", "船员发热", "event", 2, 0.0016, "mandatory", "医生", ["A使用药品:药品-1", "B轮班照料:士气-1", "C医生隔离治疗:药品-1 士气+1"], [3, 8, 2], "士气+1", "药品|士气|耗时"],
    ["drifting_crate", "漂流货箱", "salvage", 1, 0.0015, "optional", "木匠", ["A捞取木箱:木材+12", "B拆开封舱:布料+10", "C木匠回收箱架:木材+18"], [2, 3, 2], "木材+18|布料+10", ""],
    ["favorable_current", "顺流窗口", "rest", 1, 0.0015, "optional", "航海士", ["A顺势加帆:后续2航段航速+1", "B偏航追流:后续2航段航速+1.5", "C航海士锁定流轴:后续3航段航速+1"], [1, 2, 1], "航速提升", "偏离原航向"],
    ["night_fog", "夜雾迷航", "event", 2, 0.0015, "mandatory", "航海士", ["A降速鸣钟:无额外效果", "B保持航向:后续1航段受逆流影响", "C航海士观星定位:显示2点"], [5, 2, 1], "显示隐藏点+2", "耗时|逆流"],
  ],
  route_golden_tide_trade: [
    ["customs_patrol", "海军临检", "event", 2, 0.0017, "mandatory", "财务官", ["A排队受检:声望+5", "B缴纳快检费:金币-55", "C财务官核对舱单:契约进度+1"], [5, 1, 2], "声望+5|契约进度+1", "耗时|金币"],
    ["cargo_shift", "货舱移位", "event", 2, 0.0016, "mandatory", "木匠", ["A停船重新绑扎:无额外效果", "B继续航行:船体-5", "C木匠加固隔舱:木材-4 船体+2"], [4, 1, 2], "船体+2", "耗时|船体|木材"],
    ["market_rumor", "市场传闻", "event", 1, 0.0015, "optional", "财务官", ["A购买消息:金币-30 显示2点", "B转卖传闻:金币+45 追击+1", "C财务官验证来源:商会好感+1"], [1, 2, 1], "情报|金币|商会好感", "金币|追击"],
    ["merchant_distress", "商船求援", "battle", 3, 0.0015, "optional", "炮手", ["A拖带伤船:木材-6 声望+10", "B迎战追兵:战斗胜利金币+125", "C炮手远程掩护:降低战斗难度 声望+12"], [5, 2, 1], "金币+125|声望+12", "木材|战斗失败"],
    ["seasonal_wind", "季风顺航", "rest", 1, 0.0014, "optional", "航海士", ["A跟随商船加速:后续2航段航速+1", "B抢占风带:后续2航段航速+1.5", "C航海士规划风路:显示2点 后续2航段航速+1"], [1, 2, 1], "航速提升|情报", ""],
    ["deck_fire", "甲板失火", "event", 3, 0.0015, "mandatory", "木匠", ["A使用淡水灭火:淡水-35", "B隔离燃烧货物:食物-18", "C木匠拆除火板:木材-5 船体+2"], [2, 1, 2], "船体+2", "淡水|食物|木材"],
    ["floating_auction", "临时拍卖", "trade", 1, 0.0014, "optional", "财务官", ["A竞拍布料:金币-70 布料+14", "B出售闲置木材:木材-8 金币+60", "C财务官代客竞价:金币+55 商会好感+1"], [2, 1, 2], "布料|金币|商会好感", "等价交易"],
    ["stowaway", "偷渡客现身", "event", 2, 0.0014, "mandatory", "走私客", ["A送往下个港口:食物-12 声望+8", "B收取船费:金币+45 追击+1", "C走私客安排暗舱:金币+70"], [4, 1, 2], "声望|金币", "食物|追击"],
  ],
  route_moon_tide_ruins: [
    ["moon_sleepwalker", "月潮梦游", "event", 3, 0.0021, "mandatory", "深海祭司", ["A绑住梦游者:士气-1", "B轮班守夜:无额外效果", "C深海祭司驱散低语:诅咒-1"], [2, 6, 2], "诅咒-1", "士气|耗时"],
    ["ghost_current", "幽灵洋流", "event", 3, 0.0020, "mandatory", "航海士", ["A降帆等待:无额外效果", "B闯过冷流:船体-7", "C航海士追踪星光:显示2点"], [6, 2, 1], "情报", "耗时|船体"],
    ["falling_starlight", "坠落星辉", "treasure", 2, 0.0018, "optional", "宝藏潜水员", ["A收集星砂:古代材料+2", "B触碰核心:宝石+1 诅咒+1", "C宝藏潜水员封存碎片:古代材料+3"], [3, 2, 2], "古代材料|宝石", "诅咒"],
    ["relic_whisper", "遗物低语", "ruin", 2, 0.0018, "optional", "深海祭司", ["A聆听片刻:随机祝福 诅咒+1", "B投入啤酒祭奠:啤酒-1 士气+1", "C深海祭司辨认真名:印记-1"], [3, 2, 2], "随机祝福|士气|印记降低", "诅咒|啤酒"],
    ["silver_fog", "银雾迷航", "event", 3, 0.0018, "mandatory", "航海士", ["A抛锚鸣钟:无额外效果", "B凭感觉前进:后续2航段受逆流影响", "C航海士测定月位:显示3点"], [7, 2, 1], "情报", "耗时|逆流"],
    ["dead_calm", "月下无风", "event", 2, 0.0017, "mandatory", "航海士", ["A等待风起:无额外效果", "B划桨脱离:食物-16", "C航海士寻找潮汐:后续2航段航速+1"], [8, 3, 2], "航速提升", "耗时|食物"],
    ["spectral_debris", "幽灵残骸", "salvage", 2, 0.0017, "optional", "木匠", ["A打捞外层金属:铁矿+16", "B深入残舱:古代材料+2 诅咒+1", "C木匠切断幽链:木材-4 修理锤+1"], [3, 5, 2], "铁矿|古代材料|修理锤", "诅咒|木材"],
    ["curse_surge", "诅咒涌动", "event", 3, 0.0018, "mandatory", "厨师", ["A封闭舱门:士气-1", "B分发啤酒:啤酒-2 士气+1", "C厨师准备醒神餐:食物-14 诅咒-1"], [3, 2, 3], "士气|诅咒降低", "士气|啤酒|食物"],
  ],
};

Object.entries(ambientRandomBlueprints).forEach(([routeId, blueprints]) => {
  const power = routeId === "route_shattered_sail_reef" ? 86 :
    routeId === "route_golden_tide_trade" ? 126 : 168;
  blueprints.forEach((blueprint, ordinal) => {
    const [suffix, name, nodeType, risk, chance, participation, crew, choices, hours, reward, penalty] = blueprint;
    addedEvents.push(event(routeId, 73 + ordinal, "event_" +
      (routeId === "route_shattered_sail_reef" ? "reef_" : routeId === "route_golden_tide_trade" ? "trade_" : "moon_") +
      suffix, name, nodeType, risk >= 3 ? "uncommon" : "common", Math.floor(ordinal / 6), ordinal % 6, {
        artSheet: assets[routeId].uniqueSheet,
        artGridRows: 5,
        artGridCols: 6,
        participation,
        trigger: "random",
        visibility: "never",
        randomRule: "ambient_by_route_distance",
        randomChance: chance,
        risk,
        power: nodeType === "battle" ? power : 0,
        weight: Math.round(chance * 100000),
        choices,
        hours,
        reward,
        penalty,
        crew,
        repeatPolicy: "random_voyage",
        repeatable: repeatProfiles.random_voyage.repeatable,
        rewardDecay: repeatProfiles.random_voyage.rewardDecay,
        repeatMultipliers: repeatProfiles.random_voyage.multipliers,
        repeatScope: repeatProfiles.random_voyage.scope,
        notes: "开放海域随机事件；每百海里基础概率" + (chance * 100).toFixed(2) + "%",
      }));
  });
});

const routeData = await readRecords(files.routes, "航线");
if (!routeData.headers.includes("fixed_event_template_count")) {
  const insertAt = routeData.headers.indexOf("fixed_event_point_count") + 1;
  routeData.headers.splice(insertAt, 0, "fixed_event_template_count");
}
routeData.records.forEach((record) => {
  Object.assign(record, routeUpdates[record.route_id] || {});
});
await fs.writeFile(files.routes, toCsv(routeData.records, routeData.headers), "utf8");

const eventData = await readRecords(files.events, "事件");
const structuredChoiceHeaders = ["choice_a_config", "choice_b_config", "choice_c_config"];
["art_grid_rows", "art_grid_cols", "random_chance_per_100nm", "repeat_policy", "repeatable", "reward_decay", "repeat_reward_multipliers", "repeat_scope", ...structuredChoiceHeaders].forEach((header) => {
  if (!eventData.headers.includes(header)) eventData.headers.push(header);
});
const existingEventsById = Object.fromEntries(eventData.records.map((record) => [record.event_id, record]));
const addedIds = new Set(addedEvents.map((record) => record.event_id));
const eventRecords = eventData.records
  .filter((record) => !addedIds.has(record.event_id))
  .map((record) => {
    if (assets[record.route_id]) {
      record.art_sheet = assets[record.route_id].sheet;
      record.art_grid_rows = 4;
      record.art_grid_cols = 5;
    }
    return applyRepeatSettings(record);
  });
eventRecords.push(...addedEvents.map((record) => {
  const existing = existingEventsById[record.event_id];
  structuredChoiceHeaders.forEach((header) => {
    if (!existing || !String(existing[header] || "").trim()) {
      throw new Error(`${record.event_id}: missing ${header}; complete this structured choice field in event_point_config.csv before expanding routes`);
    }
    record[header] = existing[header];
  });
  return applyRepeatSettings(record);
}));
eventRecords.sort((first, second) =>
  routeOrder.indexOf(first.route_id) - routeOrder.indexOf(second.route_id) ||
  Number(first.event_index) - Number(second.event_index),
);

for (const routeId of routeOrder) {
  const fixed = eventRecords.filter((record) =>
    record.route_id === routeId && record.trigger_mode !== "random");
  const originalArtwork = new Set();
  let uniqueFrame = 0;
  fixed.forEach((record) => {
    const artworkKey = [record.art_sheet, record.grid_row, record.grid_col].join("|");
    if (!originalArtwork.has(artworkKey)) {
      originalArtwork.add(artworkKey);
      record.art_grid_rows = Number(record.art_grid_rows) || 4;
      record.art_grid_cols = Number(record.art_grid_cols) || 5;
      return;
    }
    record.art_sheet = assets[routeId].uniqueSheet;
    record.art_grid_rows = 5;
    record.art_grid_cols = 6;
    record.grid_row = Math.floor(uniqueFrame / 6);
    record.grid_col = uniqueFrame % 6;
    record.notes = String(record.notes || "") + "；使用6×5独立事件美术格";
    uniqueFrame += 1;
  });
  if (uniqueFrame !== 30) {
    throw new Error(`${routeId}: expected 30 remapped unique frames, got ${uniqueFrame}`);
  }
  const finalArtwork = new Set(fixed.map((record) =>
    [record.art_sheet, record.grid_row, record.grid_col].join("|")));
  if (finalArtwork.size !== fixed.length) {
    throw new Error(`${routeId}: fixed events do not have one-to-one artwork`);
  }
}

const hiddenTargets = {
  route_shattered_sail_reef: 35,
  route_golden_tide_trade: 32,
  route_moon_tide_ruins: 38,
};
for (const routeId of routeOrder) {
  const fixed = eventRecords.filter((record) =>
    record.route_id === routeId && record.trigger_mode !== "random");
  let hiddenCount = fixed.filter((record) => record.trigger_mode === "fixed_hidden").length;
  const candidates = fixed
    .filter((record) =>
      record.trigger_mode === "fixed_hidden" &&
      Number(record.event_index) >= 23 &&
      !["battle", "treasure", "boss_gate"].includes(record.node_type) &&
      record.rarity !== "rare")
    .sort((first, second) =>
      Number(first.risk_level) - Number(second.risk_level) ||
      Number(first.event_index) - Number(second.event_index));
  while (hiddenCount > hiddenTargets[routeId] && candidates.length) {
    const record = candidates.shift();
    record.trigger_mode = "fixed_visible";
    record.map_visibility = "vision_or_chart";
    record.notes = String(record.notes || "") + "；远景预兆使地点进入视野后可见";
    hiddenCount -= 1;
  }
  if (hiddenCount !== hiddenTargets[routeId]) {
    throw new Error(`${routeId}: expected ${hiddenTargets[routeId]} hidden events, got ${hiddenCount}`);
  }
}

for (const routeId of routeOrder) {
  const routeEvents = eventRecords.filter((record) => record.route_id === routeId);
  const fixed = routeEvents.filter((record) => record.trigger_mode !== "random");
  const random = routeEvents.filter((record) => record.trigger_mode === "random");
  if (fixed.length !== 70 || random.length !== 10) {
    throw new Error(`${routeId}: expected 70 fixed and 10 random templates, got ${fixed.length}/${random.length}`);
  }
  const ids = new Set(routeEvents.map((record) => record.event_id));
  if (ids.size !== routeEvents.length) throw new Error(`${routeId}: duplicate event id`);
}
await fs.writeFile(files.events, toCsv(eventRecords, eventData.headers), "utf8");

const artData = await readRecords(files.art, "美术");
const preservedArt = artData.records.filter((record) => !routeOrder.includes(record.route_id));
const routeNames = Object.fromEntries(routeData.records.map((record) => [record.route_id, record.name]));
const artRecords = [];
for (const routeId of routeOrder) {
  artRecords.push({
    asset_id: `${routeId}_map`,
    name: assets[routeId].mapName,
    file: assets[routeId].map,
    asset_type: "route_map",
    route_id: routeId,
    grid_rows: 1,
    grid_cols: 1,
    grid_row: 0,
    grid_col: 0,
    linked_id: routeId,
    notes: `${routeNames[routeId]}新版路线预览；不含固定航线与事件标记`,
  });
  eventRecords
    .filter((record) => record.route_id === routeId && record.trigger_mode !== "random")
    .forEach((record) => {
      artRecords.push({
        asset_id: record.event_id,
        name: record.name,
        file: record.art_sheet,
        asset_type: "event_illustration",
        route_id: routeId,
        grid_rows: Number(record.art_grid_rows) || 4,
        grid_cols: Number(record.art_grid_cols) || 5,
        grid_row: record.grid_row,
        grid_col: record.grid_col,
        linked_id: record.event_id,
        notes: String(record.art_sheet).includes("_unique_v4.png")
          ? "6×5独立像素图集；该格仅映射一个固定事件"
          : String(record.art_sheet).includes("_expansion_v3.png")
            ? "5×4扩展像素图集；该格仅映射一个固定事件"
            : "5×4基础像素图集；该格仅映射一个固定事件",
      });
    });
}
const allArt = [...artRecords, ...preservedArt];
await fs.writeFile(files.art, toCsv(allArt, artData.headers), "utf8");

function columnName(number) {
  let result = "";
  for (let value = number; value > 0; value = Math.floor((value - 1) / 26)) {
    result = String.fromCharCode(65 + (value - 1) % 26) + result;
  }
  return result;
}

for (const [key, file] of Object.entries(files)) {
  const text = (await fs.readFile(file, "utf8")).replace(/^\uFEFF/, "");
  const workbook = await Workbook.fromCSV(text, { sheetName: key });
  const sheet = workbook.worksheets.getItem(key);
  const values = sheet.getUsedRange().values;
  const endColumn = columnName(values[0].length);
  sheet.showGridLines = false;
  sheet.freezePanes.freezeRows(1);
  sheet.getRange(`A1:${endColumn}1`).format = {
    fill: "#17444A",
    font: { bold: true, color: "#F4ECD2" },
    wrapText: true,
  };
  sheet.getRange(`A1:${endColumn}${Math.min(values.length, 10)}`).format.autofitColumns();
  sheet.getRange(`A1:${endColumn}${Math.min(values.length, 10)}`).format.autofitRows();
  const inspection = await workbook.inspect({
    kind: "table",
    sheetId: key,
    range: `A1:${endColumn}${Math.min(values.length, 8)}`,
    tableMaxRows: 8,
    tableMaxCols: values[0].length,
    maxChars: 2200,
  });
  if (!inspection.ndjson) throw new Error(`${key}: artifact-tool inspection failed`);
  const preview = await workbook.render({
    sheetName: key,
    range: `A1:${endColumn}${Math.min(values.length, 10)}`,
    scale: 1,
    format: "png",
  });
  await fs.writeFile(
    path.join(previewDir, `${key}.png`),
    new Uint8Array(await preview.arrayBuffer()),
  );
  console.log(JSON.stringify({
    key,
    rows: values.length - 1,
    columns: values[0].length,
    endColumn,
  }));
}
