import fs from "node:fs/promises";
import path from "node:path";
import { Workbook } from "@oai/artifact-tool";

const root = path.resolve(import.meta.dirname, "../..");
const outputCsv = path.join(root, "outputs/balance_configs/voyage_balance_report.csv");
const outputMd = path.join(root, "outputs/balance_configs/voyage_balance_report.md");

function parseCsv(text) {
  const rows = []; let row = []; let field = ""; let quoted = false;
  for (let index = 0; index < text.length; index += 1) {
    const character = text[index];
    if (quoted) {
      if (character === '"' && text[index + 1] === '"') { field += '"'; index += 1; }
      else if (character === '"') quoted = false;
      else field += character;
    } else if (character === '"') quoted = true;
    else if (character === ",") { row.push(field); field = ""; }
    else if (character === "\n") {
      row.push(field.replace(/\r$/, ""));
      if (row.some((value) => value !== "")) rows.push(row);
      row = []; field = "";
    } else field += character;
  }
  if (field || row.length) { row.push(field.replace(/\r$/, "")); rows.push(row); }
  const headers = rows.shift();
  return rows.map((values) => Object.fromEntries(headers.map((header, index) => [header, values[index] || ""])));
}
async function readCsv(relative) {
  return parseCsv((await fs.readFile(path.join(root, relative), "utf8")).replace(/^\uFEFF/, ""));
}
function hashSeed(value) {
  let hash = 2166136261;
  for (const character of String(value)) {
    hash ^= character.charCodeAt(0);
    hash = Math.imul(hash, 16777619);
  }
  return hash >>> 0;
}
function mulberry32(seed) {
  return function () {
    let value = seed += 0x6d2b79f5;
    value = Math.imul(value ^ value >>> 15, value | 1);
    value ^= value + Math.imul(value ^ value >>> 7, value | 61);
    return ((value ^ value >>> 14) >>> 0) / 4294967296;
  };
}
function clamp(value, minimum, maximum) { return Math.max(minimum, Math.min(maximum, value)); }
function choiceIndex(rng, strategy) {
  const roll = rng();
  const weights = strategy === "cautious" ? [0.65, 0.25, 0.10] :
    strategy === "daring" ? [0.20, 0.45, 0.35] : [0.34, 0.40, 0.26];
  return roll < weights[0] ? 0 : roll < weights[0] + weights[1] ? 1 : 2;
}
function weightedSample(records, count, rng) {
  const pool = records.slice(); const selected = [];
  while (selected.length < count && pool.length) {
    const total = pool.reduce((sum, record) => sum + Math.max(1, Number(record.weight) || 1), 0);
    let roll = rng() * total; let index = 0;
    for (; index < pool.length; index += 1) {
      roll -= Math.max(1, Number(pool[index].weight) || 1);
      if (roll <= 0) break;
    }
    selected.push(pool.splice(Math.min(index, pool.length - 1), 1)[0]);
  }
  return selected;
}
const effectMap = {
  "淡水": "water", "食物": "food", "啤酒": "beer", "药品": "medicine", "木材": "wood",
  "铁矿": "iron", "布料": "cloth", "宝石": "gem", "火药": "powder", "炮弹": "cannonball",
  "修理锤": "hammer", "航海灯": "lantern", "金币": "gold", "声望": "fame",
  "契约卷轴": "contractScroll", "古代罗盘": "ancientCompass", "黑珍珠": "blackPearl",
  "诅咒金币": "cursedCoin", "稀有遗物": "relic", "遗物": "relic", "古代材料": "ancientMaterial",
};
function parseEffects(text) {
  const result = {};
  for (const [label, key] of Object.entries(effectMap)) {
    const pattern = new RegExp(label + "\\s*([+-])\\s*(\\d+(?:\\.\\d+)?)", "g");
    let match;
    while ((match = pattern.exec(text))) {
      const value = Number(match[2]) * (match[1] === "-" ? -1 : 1);
      result[key] = (result[key] || 0) + value;
    }
  }
  const hull = text.match(/船体\s*([+-])\s*(\d+(?:\.\d+)?)/);
  if (hull) result.hull = Number(hull[2]) * (hull[1] === "-" ? -1 : 1);
  const morale = text.match(/士气\s*([+-])\s*(\d+(?:\.\d+)?)/);
  if (morale) result.morale = Number(morale[2]) * (morale[1] === "-" ? -1 : 1);
  return result;
}
function logistic(delta) { return 1 / (1 + Math.exp(-delta / 18)); }
function cargoVolumeUsed(resources, cargoRows) {
  return cargoRows.reduce((sum, cargo) =>
    sum + Math.max(0, resources[cargo.resource_id] || 0) * Number(cargo.volume_per_unit || 0), 0);
}
function round(value, digits = 1) { const scale = 10 ** digits; return Math.round(value * scale) / scale; }
function csvCell(value) {
  const text = String(value ?? "");
  return /[",\n]/.test(text) ? '"' + text.replace(/"/g, '""') + '"' : text;
}

const [routes, events, ships, cargoRows] = await Promise.all([
  readCsv("outputs/routes_v2/route_config.csv"),
  readCsv("outputs/routes_v2/event_point_config.csv"),
  readCsv("outputs/balance_configs/ship_config.csv"),
  readCsv("outputs/balance_configs/voyage_cargo_config.csv"),
]);
const shipByName = Object.fromEntries(ships.map((ship) => [ship.name, ship]));
const cargoById = Object.fromEntries(cargoRows.map((cargo) => [cargo.resource_id, cargo]));
const strategies = {
  cautious: { name: "谨慎", minimum: 9, spread: 4, battle: -5, provisionBufferDays: 6 },
  balanced: { name: "均衡", minimum: 12, spread: 5, battle: 0, provisionBufferDays: 4 },
  daring: { name: "冒险", minimum: 16, spread: 4, battle: 5, provisionBufferDays: 2 },
};
const runsPerCase = 5000;
const results = [];

for (const route of routes) {
  const routeEvents = events.filter((event) => event.route_id === route.route_id);
  const fixed = routeEvents.filter((event) => event.trigger_mode !== "random" && event.node_type !== "boss_gate");
  const boss = routeEvents.find((event) => event.node_type === "boss_gate");
  const ambient = routeEvents.filter((event) => event.random_rule_id === "ambient_by_route_distance");
  const ship = shipByName[route.recommended_ship];
  if (!ship || !boss || ambient.length !== 8) throw new Error(route.route_id + ": incomplete simulation inputs");
  const crewNumbers = [...route.recommended_manifest.matchAll(/(\d+)/g)].map((match) => Number(match[1]));
  const crewCount = crewNumbers.reduce((sum, value) => sum + value, 0);
  const cargoColumn = ship.ship_id === "ship_merchant" ? "recommended_merchant" :
    ship.ship_id === "ship_boarding" ? "recommended_boarding" : "recommended_sloop";

  for (const [strategyId, strategy] of Object.entries(strategies)) {
    const totals = {
      success: 0, profit: 0, days: 0, encounters: 0, randomEvents: 0,
      cargoRatio: 0, hull: 0, waterMargin: 0, foodMargin: 0, battleWins: 0, battles: 0,
    };
    for (let run = 0; run < runsPerCase; run += 1) {
      const rng = mulberry32(hashSeed(route.route_id + "::" + strategyId + "::" + run));
      const resources = {};
      for (const cargo of cargoRows) resources[cargo.resource_id] = Number(cargo[cargoColumn]) || 0;
      const plannedProvisionDays = Number(route.recommended_voyage_days) + strategy.provisionBufferDays;
      resources.water = crewCount * plannedProvisionDays;
      resources.food = crewCount * plannedProvisionDays;
      if (cargoVolumeUsed(resources, cargoRows) > Number(ship.cargo_capacity)) {
        throw new Error(route.route_id + ": planned provisions exceed cargo capacity");
      }
      let hull = Number(ship.hp);
      let morale = 3;
      let rewardValue = 0;
      let costValue = 0;
      let eventHours = 0;
      let battleWins = 0;
      let battles = 0;
      const encounterCount = Math.min(19, strategy.minimum + Math.floor(rng() * strategy.spread));
      const selected = weightedSample(fixed, encounterCount, rng);
      const travelDays = Number(route.recommended_voyage_days) * (0.88 + rng() * 0.18);
      const travelNm = travelDays * (70 + Number(ship.speed_kn) * 2);
      const ambientRate = ambient.reduce((sum, event) => sum + Number(event.random_chance_per_100nm || 0), 0);
      const ambientCount = rng() < 1 - Math.pow(1 - ambientRate, travelNm / 100) ? 1 : 0;
      if (ambientCount) selected.push(ambient[Math.floor(rng() * ambient.length)]);

      for (const event of selected) {
        const index = choiceIndex(rng, strategyId);
        const suffix = ["a", "b", "c"][index];
        const choiceText = event["choice_" + suffix] || event.choice_a;
        const hours = Number(event["choice_" + suffix + "_hours"] || event.choice_a_hours) || 0;
        eventHours += hours;
        const effects = parseEffects(choiceText);
        for (const [key, value] of Object.entries(effects)) {
          if (key === "hull") hull += value;
          else if (key === "morale") morale += value;
          else {
            const cargo = cargoById[key];
            let applied = value;
            if (value > 0 && Number(cargo?.volume_per_unit || 0) > 0) {
              const free = Math.max(0, Number(ship.cargo_capacity) - cargoVolumeUsed(resources, cargoRows));
              applied = Math.min(value, Math.floor(free / Number(cargo.volume_per_unit)));
            }
            resources[key] = (resources[key] || 0) + applied;
            const price = applied >= 0 ? Number(cargo?.port_sale_price || 0) : Number(cargo?.base_purchase_price || cargo?.port_sale_price || 0);
            if (applied >= 0) rewardValue += applied * price;
            else costValue += -applied * price;
          }
        }
        const isBattle = event.node_type === "battle" && /战斗|迎战|迎击|压制|追兵/.test(choiceText);
        if (isBattle) {
          battles += 1;
          const power = Number(route.recommended_power) + 18 + Number(route.unlock_level) * 4 + strategy.battle;
          const chance = clamp(logistic(power - Number(event.power_check || route.recommended_power)), 0.2, 0.95);
          if (rng() < chance) battleWins += 1;
          else hull -= 12 + Number(event.risk_level) * 3;
        }
      }

      const totalDays = travelDays + eventHours / 24;
      const rationNeed = crewCount * totalDays;
      resources.water -= rationNeed;
      resources.food -= rationNeed;
      costValue += rationNeed * (Number(cargoById.water.base_purchase_price) + Number(cargoById.food.base_purchase_price));
      if (totalDays > 30 && resources.food > 0) resources.food *= Math.pow(0.94, totalDays - 30);

      battles += 1;
      const bossPower = Number(route.recommended_power) + 40 + Number(route.unlock_level) * 5 + strategy.battle;
      const bossChance = clamp(logistic(bossPower - Number(route.boss_power)), 0.25, 0.9);
      const bossWon = rng() < bossChance;
      if (bossWon) { battleWins += 1; rewardValue += Number(route.boss_power) * 1.4; }
      else hull -= Number(ship.hp) * 0.45;

      hull = Math.min(Number(ship.hp), hull);
      morale = clamp(morale, 0, 5);
      const success = resources.water >= 0 && resources.food >= 0 && hull > 0 && bossWon;
      const repairCost = Math.max(0, Number(ship.hp) - hull) * 2.5;
      const netProfit = rewardValue - costValue - repairCost;
      const cargoUsed = cargoVolumeUsed(resources, cargoRows);
      totals.success += success ? 1 : 0;
      totals.profit += netProfit;
      totals.days += totalDays;
      totals.encounters += encounterCount;
      totals.randomEvents += ambientCount;
      totals.cargoRatio += cargoUsed / Number(ship.cargo_capacity);
      totals.hull += Math.max(0, hull) / Number(ship.hp);
      totals.waterMargin += resources.water / Math.max(1, rationNeed);
      totals.foodMargin += resources.food / Math.max(1, rationNeed);
      totals.battleWins += battleWins;
      totals.battles += battles;
    }
    const successRate = totals.success / runsPerCase;
    const cargoRatio = totals.cargoRatio / runsPerCase;
    const warnings = [];
    if (successRate < 0.55) warnings.push("成功率偏低");
    if (successRate > 0.82) warnings.push("成功率偏高");
    if (cargoRatio < 0.20) warnings.push("返港货舱偏空");
    if (cargoRatio > 1.02) warnings.push("平均超载");
    results.push({
      route_id: route.route_id,
      route_name: route.name,
      strategy: strategy.name,
      runs: runsPerCase,
      success_rate: round(successRate * 100),
      average_days: round(totals.days / runsPerCase),
      average_fixed_encounters: round(totals.encounters / runsPerCase),
      ambient_event_rate: round(totals.randomEvents / runsPerCase * 100),
      battle_win_rate: round(totals.battleWins / Math.max(1, totals.battles) * 100),
      average_net_value: round(totals.profit / runsPerCase),
      end_cargo_ratio: round(cargoRatio * 100),
      hull_remaining: round(totals.hull / runsPerCase * 100),
      water_margin_days_ratio: round(totals.waterMargin / runsPerCase * 100),
      food_margin_days_ratio: round(totals.foodMargin / runsPerCase * 100),
      warnings: warnings.join("|") || "通过",
    });
  }
}

const headers = Object.keys(results[0]);
const csv = "\uFEFF" + [headers, ...results.map((record) => headers.map((header) => record[header]))]
  .map((row) => row.map(csvCell).join(",")).join("\n") + "\n";
await fs.writeFile(outputCsv, csv, "utf8");
const md = [
  "# 航行平衡模拟报告",
  "",
  "每个航线与策略组合运行 " + runsPerCase + " 次，共 " + (runsPerCase * results.length) + " 次。该模型用于比较补给、事件、战斗与收益趋势，不代替真实玩家测试。",
  "",
  "| 航线 | 策略 | 成功率 | 天数 | 固定事件 | 随机遭遇率 | 战斗胜率 | 净价值 | 返港货舱 | 船体剩余 | 诊断 |",
  "|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---|",
  ...results.map((row) => "| " + row.route_name + " | " + row.strategy + " | " + row.success_rate + "% | " +
    row.average_days + " | " + row.average_fixed_encounters + " | " + row.ambient_event_rate + "% | " +
    row.battle_win_rate + "% | " + row.average_net_value + " | " + row.end_cargo_ratio + "% | " +
    row.hull_remaining + "% | " + row.warnings + " |"),
  "",
  "目标区间：均衡策略成功率 60%-75%；返港货舱因路线收益结构不同允许 20%-95%，但不得超载。",
  "",
];
await fs.writeFile(outputMd, md.join("\n"), "utf8");

const workbook = await Workbook.fromCSV(csv.replace(/^\uFEFF/, ""), { sheetName: "Balance" });
const inspection = await workbook.inspect({
  kind: "table", sheetId: "Balance", range: "A1:N10", tableMaxRows: 10, tableMaxCols: 14, maxChars: 2600,
});
if (!inspection.ndjson) throw new Error("artifact-tool inspection failed");
console.log(JSON.stringify({ runs: runsPerCase * results.length, outputCsv, outputMd, results }, null, 2));
