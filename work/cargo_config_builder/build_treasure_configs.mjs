import fs from "node:fs/promises";
import path from "node:path";
import { Workbook } from "@oai/artifact-tool";

const outputDir = path.resolve(import.meta.dirname, "../../outputs/balance_configs");
const previewDir = path.resolve(import.meta.dirname, "previews");
await fs.mkdir(outputDir, { recursive: true });
await fs.mkdir(previewDir, { recursive: true });

function csvCell(value) {
  const text = String(value ?? "");
  return /[",\r\n]/.test(text) ? `"${text.replaceAll('"', '""')}"` : text;
}

function makeCsv(rows) {
  return rows.map((row) => row.map(csvCell).join(",")).join("\n") + "\n";
}

function columnName(index) {
  let result = "";
  let value = index;
  while (value > 0) {
    value -= 1;
    result = String.fromCharCode(65 + (value % 26)) + result;
    value = Math.floor(value / 26);
  }
  return result;
}

const rankRows = [
  ["rank_id", "rank_name", "unlock_level", "fragment_cost", "max_held", "craft_chance_level2", "craft_chance_level3_4", "craft_chance_level5_plus", "recommended_power", "recommended_voyage_days", "recommended_cargo_ratio", "reward_scale", "required_clues", "nm_per_map_unit", "side_points", "total_points", "event_template_count", "min_spacing_map_units", "min_spacing_nm", "ambush_chance_per_100nm", "ambush_detection_base", "full_gold", "full_gem", "full_ancient_material", "full_relic", "full_cargo_slots", "breach_clues", "breach_powder", "breach_hull", "breach_gold", "breach_gem", "breach_ancient_material", "breach_cargo_slots", "diver_black_pearl", "settlement_protection", "chart_eligible", "map_asset", "event_sheet_asset"],
  ["weathered", "残旧", 2, 4, 6, 1, 0.55, 0.40, 90, 18, 0.62, 1, 2, 24, 9, 10, 18, 9, 216, 0.008, 0.24, 420, 1, 6, 0, 14, 1, 3, 12, 300, 1, 4, 11, 1, 0.95, false, "outputs/routes_v2/treasure_hunt_route_map.png", "outputs/routes_v2/treasure_hunt_event_sheet.png"],
  ["gilded", "鎏金", 3, 4, 6, 0, 0.45, 0.45, 130, 22, 0.66, 1.35, 2, 27, 10, 11, 18, 9, 243, 0.012, 0.20, 570, 2, 8, 1, 27, 1, 3, 12, 405, 1, 5, 12.5, 1, 0.95, false, "outputs/routes_v2/treasure_hunt_route_map.png", "outputs/routes_v2/treasure_hunt_event_sheet.png"],
  ["moonlit", "月蚀", 5, 4, 6, 0, 0, 0.15, 175, 26, 0.70, 1.75, 3, 30, 11, 12, 18, 9.5, 285, 0.018, 0.16, 735, 2, 10, 1, 30, 2, 3, 12, 525, 1, 7, 15.5, 1, 0.95, false, "outputs/routes_v2/treasure_hunt_route_map.png", "outputs/routes_v2/treasure_hunt_event_sheet.png"],
];

const eventRows = [
  ["event_id", "name", "type", "node_type", "trigger_mode", "map_visibility", "participation_mode", "risk", "power_rule", "choice_count", "duration_hours", "profession_paths", "clue_gain_paths", "key_costs", "key_rewards", "notes"],
  ["treasure_clue_bottle", "盐封漂流瓶", "线索", "treasure", "fixed_visible", "vision_or_chart", "optional", 1, 0, 3, "1|3", "navigator", 2, "时间口粮", "线索1；声望5；或布料8×倍率；航海士航速+1持续2段", "图集0,0；线索保底池"],
  ["treasure_clue_whalebone", "鲸骨方位标", "线索", "treasure", "fixed_visible", "vision_or_chart", "optional", 2, 0, 3, "2|4", "diver", 2, "时间口粮", "线索1；古代材料4×倍率；铁矿10×倍率；潜水员宝石1", "图集0,1；线索保底池"],
  ["treasure_clue_star", "倒悬星盘", "线索", "treasure", "fixed_visible", "vision_or_chart", "optional", 2, 0, 3, "1|2|6", "navigator", 3, "时间口粮；强转船体5", "线索1；士气1；航海士古代罗盘1", "图集0,2；线索保底池"],
  ["treasure_clue_fogbell", "雾钟灯塔", "线索", "treasure", "fixed_visible", "vision_or_chart", "optional", 2, 0, 3, "1|2|3", "navigator", 2, "时间口粮", "线索1；声望6；航海灯1；药品1；航海士航速增益", "图集0,3；线索保底池"],
  ["treasure_clue_twinmoon", "双月潮碑", "线索", "treasure", "fixed_visible", "vision_or_chart", "optional", 2, 0, 3, "2|5", "navigator", 2, "时间口粮；撬盘船体4", "线索1；古代材料4×倍率；古代罗盘1", "图集0,4；线索保底池"],
  ["treasure_false_cache", "赝品石匣", "宝藏", "treasure", "fixed_hidden", "intel_or_encounter", "optional", 3, 0, 3, "1|3|5", "diver", 0, "时间口粮；开匣诅咒1", "金币90×倍率；铁矿10×倍率与火药2；潜水员宝石1", "图集1,0；情报或接触后显示"],
  ["treasure_reef_snare", "沉链礁阵", "险情", "treasure", "fixed_hidden", "intel_or_encounter", "mandatory", 3, 0, 3, "2|4", "carpenter", 0, "时间口粮；木材8与船体3；或船体10", "木匠船体4", "图集1,1；不可通用略过"],
  ["treasure_rival_sloop", "红帆寻宝队", "战斗", "battle", "fixed_visible", "vision_or_chart", "optional", 3, "推荐战力×0.82", 3, "1|2|3", "", 1, "时间口粮；购买情报金币80", "战斗金币135×倍率与声望12；或线索1", "图集1,2；战斗、交易或绕行"],
  ["treasure_drowned_shrine", "没潮神龛", "宝藏", "treasure", "fixed_hidden", "intel_or_encounter", "optional", 2, 0, 3, "1|4|6", "diver", 0, "时间口粮；供品啤酒1", "金币105×倍率与古代材料4×倍率；士气2；潜水员黑珍珠1", "图集1,3；情报或接触后显示"],
  ["treasure_salt_spring", "盐洞淡泉", "补给", "treasure", "fixed_visible", "vision_or_chart", "optional", 1, 0, 3, "2|3|4", "cook", 0, "时间口粮", "淡水70×倍率；或淡水30食物20；厨师淡水50食物30", "图集1,4；补给恢复点"],
  ["treasure_siren_conch", "歌潮海螺", "诅咒", "treasure", "fixed_hidden", "intel_or_encounter", "optional", 3, 0, 3, "1|2|3", "navigator", 2, "时间口粮；士气或啤酒", "线索1；古代材料4×倍率", "图集2,0；额外线索来源"],
  ["treasure_ghost_fleet", "鬼灯船列", "战斗", "battle", "fixed_visible", "vision_or_chart", "optional", 4, "推荐战力×0.90", 3, "2|3|4", "gunner", 0, "时间口粮；潜行啤酒1", "金币170×倍率；古代罗盘1；声望15；潜行士气1", "图集2,1；炮手可降低战斗难度"],
  ["treasure_smuggler_jetty", "弃置走私栈桥", "交易", "treasure", "fixed_hidden", "intel_or_encounter", "optional", 2, 0, 3, "2|4", "treasurer", 0, "金币120；或诅咒1", "药品3火药4；布料12×倍率；财务官折价补给", "图集2,2；情报或接触后显示"],
  ["treasure_brine_geyser", "盐卤喷泉", "险情", "treasure", "fixed_hidden", "intel_or_encounter", "mandatory", 3, 0, 3, "1.5|2|3", "carpenter", 0, "时间口粮；淡水70或淡水25船体8", "喷流航速+2；木匠降低淡水损失", "图集2,3；纯淡水额外消耗事件"],
  ["treasure_coral_maze", "活珊瑚迷阵", "险情", "treasure", "fixed_hidden", "intel_or_encounter", "mandatory", 3, 0, 3, "2|5", "navigator", 0, "时间口粮；或火药2船体4", "航海士航速+1持续2段", "图集2,4；保留无额外物资的慢速出口"],
  ["treasure_castaway_cartographer", "落难绘图师", "线索", "treasure", "fixed_visible", "vision_or_chart", "optional", 2, 0, 3, "1|6", "", 2, "救援淡水30食物30；购图金币60；或单耗食物20", "线索1；声望6至10", "图集3,0；线索保底池与纯食物分支"],
  ["treasure_pearl_crab", "珠蟹浅滩", "宝藏", "treasure", "fixed_hidden", "intel_or_encounter", "optional", 2, 0, 3, "2|4|6", "diver", 0, "时间口粮；诱离食物25", "食物40宝石1；或宝石2；潜水员黑珍珠1", "图集3,1；情报或接触后显示"],
  ["treasure_core", "秘藏核心", "秘藏", "treasure_core", "fixed_hidden", "intel_or_encounter", "mandatory", 4, "推荐战力", 4, "0.5|5|6|8", "diver", 0, "完整开启需品级线索；强拆少1线索、火药3、船体12", "完整秘藏；强拆部分秘藏；潜水员完整秘藏与黑珍珠1", "图集3,2；藏宝图叉号开局可见；可标记后撤"],
];
const rankPath = path.join(outputDir, "treasure_map_config.csv");
const eventPath = path.join(outputDir, "treasure_event_config.csv");
await fs.writeFile(rankPath, makeCsv(rankRows), "utf8");
await fs.writeFile(eventPath, makeCsv(eventRows), "utf8");

const resourcePath = path.join(outputDir, "resource_config.csv");
let resourceCsv = await fs.readFile(resourcePath, "utf8");
resourceCsv = resourceCsv.replace("res_map_fragment,藏宝图碎片,钥匙,稀有,outputs/pixel_art_assets/resource_icon_sheet.png,1,1,999,45,25,遗迹测绘,开启副本,80", "res_map_fragment,藏宝图碎片,钥匙,稀有,outputs/pixel_art_assets/resource_icon_sheet.png,1,1,999,45,25,遗迹测绘,拼合藏宝图（4枚）,80");
await fs.writeFile(resourcePath, resourceCsv, "utf8");

const artManifestPath = path.resolve(import.meta.dirname, "../../outputs/routes_v2/art_manifest.csv");
const artManifestCsv = await fs.readFile(artManifestPath, "utf8");
const artManifestWorkbook = await Workbook.fromCSV(artManifestCsv, { sheetName: "美术映射" });
const artManifestValues = artManifestWorkbook.worksheets.getItem("美术映射").getUsedRange().values;
const treasureArtRows = [
  ["route_treasure_hunt_map", "藏宝图隐藏海域", "outputs/routes_v2/treasure_hunt_route_map.png", "route_map", "route_treasure_hunt", 1, 1, 0, 0, "route_treasure_hunt", "1672x941 专属路线预览"],
  ["treasure_clue_bottle", "盐封漂流瓶", "outputs/routes_v2/treasure_hunt_event_sheet.png", "event_illustration", "route_treasure_hunt", 4, 5, 0, 0, "treasure_clue_bottle", "事件插画"],
  ["treasure_clue_whalebone", "鲸骨方位标", "outputs/routes_v2/treasure_hunt_event_sheet.png", "event_illustration", "route_treasure_hunt", 4, 5, 0, 1, "treasure_clue_whalebone", "事件插画"],
  ["treasure_clue_star", "倒悬星盘", "outputs/routes_v2/treasure_hunt_event_sheet.png", "event_illustration", "route_treasure_hunt", 4, 5, 0, 2, "treasure_clue_star", "事件插画"],
  ["treasure_clue_fogbell", "雾钟灯塔", "outputs/routes_v2/treasure_hunt_event_sheet.png", "event_illustration", "route_treasure_hunt", 4, 5, 0, 3, "treasure_clue_fogbell", "事件插画"],
  ["treasure_clue_twinmoon", "双月潮碑", "outputs/routes_v2/treasure_hunt_event_sheet.png", "event_illustration", "route_treasure_hunt", 4, 5, 0, 4, "treasure_clue_twinmoon", "事件插画"],
  ["treasure_false_cache", "赝品石匣", "outputs/routes_v2/treasure_hunt_event_sheet.png", "event_illustration", "route_treasure_hunt", 4, 5, 1, 0, "treasure_false_cache", "事件插画"],
  ["treasure_reef_snare", "沉链礁阵", "outputs/routes_v2/treasure_hunt_event_sheet.png", "event_illustration", "route_treasure_hunt", 4, 5, 1, 1, "treasure_reef_snare", "事件插画"],
  ["treasure_rival_sloop", "红帆寻宝队", "outputs/routes_v2/treasure_hunt_event_sheet.png", "event_illustration", "route_treasure_hunt", 4, 5, 1, 2, "treasure_rival_sloop", "事件插画"],
  ["treasure_drowned_shrine", "没潮神龛", "outputs/routes_v2/treasure_hunt_event_sheet.png", "event_illustration", "route_treasure_hunt", 4, 5, 1, 3, "treasure_drowned_shrine", "事件插画"],
  ["treasure_salt_spring", "盐洞淡泉", "outputs/routes_v2/treasure_hunt_event_sheet.png", "event_illustration", "route_treasure_hunt", 4, 5, 1, 4, "treasure_salt_spring", "事件插画"],
  ["treasure_siren_conch", "歌潮海螺", "outputs/routes_v2/treasure_hunt_event_sheet.png", "event_illustration", "route_treasure_hunt", 4, 5, 2, 0, "treasure_siren_conch", "事件插画"],
  ["treasure_ghost_fleet", "鬼灯船列", "outputs/routes_v2/treasure_hunt_event_sheet.png", "event_illustration", "route_treasure_hunt", 4, 5, 2, 1, "treasure_ghost_fleet", "事件插画"],
  ["treasure_smuggler_jetty", "弃置走私栈桥", "outputs/routes_v2/treasure_hunt_event_sheet.png", "event_illustration", "route_treasure_hunt", 4, 5, 2, 2, "treasure_smuggler_jetty", "事件插画"],
  ["treasure_brine_geyser", "盐卤喷泉", "outputs/routes_v2/treasure_hunt_event_sheet.png", "event_illustration", "route_treasure_hunt", 4, 5, 2, 3, "treasure_brine_geyser", "事件插画"],
  ["treasure_coral_maze", "活珊瑚迷阵", "outputs/routes_v2/treasure_hunt_event_sheet.png", "event_illustration", "route_treasure_hunt", 4, 5, 2, 4, "treasure_coral_maze", "事件插画"],
  ["treasure_castaway_cartographer", "落难绘图师", "outputs/routes_v2/treasure_hunt_event_sheet.png", "event_illustration", "route_treasure_hunt", 4, 5, 3, 0, "treasure_castaway_cartographer", "事件插画"],
  ["treasure_pearl_crab", "珠蟹浅滩", "outputs/routes_v2/treasure_hunt_event_sheet.png", "event_illustration", "route_treasure_hunt", 4, 5, 3, 1, "treasure_pearl_crab", "事件插画"],
  ["treasure_core", "秘藏核心", "outputs/routes_v2/treasure_hunt_event_sheet.png", "event_illustration", "route_treasure_hunt", 4, 5, 3, 2, "treasure_core", "事件插画"],
  ["treasure_random_food_spoilage", "藏宝海域食物变质", "outputs/routes_v2/treasure_hunt_event_sheet.png", "event_illustration", "route_treasure_hunt", 4, 5, 3, 3, "food_spoilage_by_age", "随机事件插画"],
  ["treasure_random_ambush", "藏宝海域伏击", "outputs/routes_v2/treasure_hunt_event_sheet.png", "event_illustration", "route_treasure_hunt", 4, 5, 3, 4, "ambush_by_route_distance", "随机事件与预警共用插画"],
];
const retainedArtRows = artManifestValues.filter((row, index) => index === 0 || row[4] !== "route_treasure_hunt");
await fs.writeFile(artManifestPath, makeCsv(retainedArtRows.concat(treasureArtRows)), "utf8");
async function verifyAndRender(filePath, sheetName, previewName) {
  const csv = await fs.readFile(filePath, "utf8");
  const workbook = await Workbook.fromCSV(csv, { sheetName });
  const sheet = workbook.worksheets.getItem(sheetName);
  const values = sheet.getUsedRange().values;
  if (!values || values.length < 2) throw new Error(`${sheetName}: no data rows`);
  if (values.some((row) => row.length !== values[0].length)) throw new Error(`${sheetName}: inconsistent columns`);
  const endColumn = columnName(values[0].length);
  const range = sheet.getRange(`A1:${endColumn}${values.length}`);
  sheet.showGridLines = false;
  sheet.freezePanes.freezeRows(1);
  sheet.getRange(`A1:${endColumn}1`).format = {
    fill: "#17444A",
    font: { bold: true, color: "#F4ECD2" },
    wrapText: true,
  };
  sheet.getRange(`A2:${endColumn}${values.length}`).format = {
    font: { color: "#172326" },
    wrapText: true,
  };
  range.format.borders = { preset: "inside", style: "thin", color: "#D3DDD8" };
  range.format.autofitColumns();
  range.format.autofitRows();
  const inspection = await workbook.inspect({
    kind: "table",
    sheetId: sheetName,
    range: `A1:${endColumn}${values.length}`,
    include: "values",
    tableMaxRows: values.length,
    tableMaxCols: values[0].length,
    maxChars: 7000,
  });
  if (!inspection.ndjson) throw new Error(`${sheetName}: inspect failed`);
  const preview = await workbook.render({
    sheetName,
    range: `A1:${endColumn}${values.length}`,
    scale: 0.7,
    format: "png",
  });
  await fs.writeFile(path.join(previewDir, previewName), new Uint8Array(await preview.arrayBuffer()));
  return { rows: values.length - 1, columns: values[0].length, endColumn };
}

const rankCheck = await verifyAndRender(rankPath, "藏宝图品级", "treasure_map_config_preview.png");
const eventCheck = await verifyAndRender(eventPath, "隐藏海域事件", "treasure_event_config_preview.png");
const artCheck = await verifyAndRender(artManifestPath, "美术映射", "treasure_art_manifest_preview.png");
console.log(JSON.stringify({ rankPath, eventPath, artManifestPath, rankCheck, eventCheck, artCheck }, null, 2));