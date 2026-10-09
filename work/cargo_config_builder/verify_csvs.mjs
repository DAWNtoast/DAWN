import fs from "node:fs/promises";
import { Workbook } from "@oai/artifact-tool";

const files = [
  "../../outputs/balance_configs/ship_config.csv",
  "../../outputs/balance_configs/crew_config.csv",
  "../../outputs/balance_configs/voyage_cargo_config.csv",
  "../../outputs/balance_configs/card_config.csv",
  "../../outputs/balance_configs/port_config.csv",
  "../../outputs/balance_configs/dispatch_config.csv",
  "../../outputs/balance_configs/component_config.csv",
  "../../outputs/balance_configs/treasure_map_config.csv",
  "../../outputs/balance_configs/treasure_event_config.csv",
  "../../outputs/routes_v2/route_config.csv",
  "../../outputs/routes_v2/event_point_config.csv",
  "../../outputs/routes_v2/event_chain_config.csv",
  "../../outputs/routes_v2/art_manifest.csv",
];

let standardEventRecords = [];

function parseStructuredChoice(record, suffix) {
  const raw = String(record["choice_" + suffix + "_config"] || "");
  let config;
  try {
    config = JSON.parse(raw);
  } catch (error) {
    throw new Error(record.event_id + " choice " + suffix + ": invalid structured JSON");
  }
  if (!config || typeof config !== "object" || Array.isArray(config) ||
      !config.effect || typeof config.effect !== "object" || Array.isArray(config.effect) ||
      Object.values(config.effect).some((value) => typeof value !== "number" || !Number.isFinite(value))) {
    throw new Error(record.event_id + " choice " + suffix + ": invalid structured effect");
  }
  return config;
}

for (const file of files) {
  const csvText = (await fs.readFile(new URL(file, import.meta.url), "utf8")).replace(/^\uFEFF/, "");
  const workbook = await Workbook.fromCSV(csvText, { sheetName: "Config" });
  const sheet = workbook.worksheets.getItem("Config");
  const values = sheet.getUsedRange().values;
  if (!values || values.length < 2) throw new Error(file + ": no data rows");
  if (values.some((row) => row.length !== values[0].length)) {
    throw new Error(file + ": inconsistent column count");
  }
  const headers = values[0].map(String);
  const records = values.slice(1).map((row) =>
    Object.fromEntries(headers.map((header, index) => [header, row[index] ?? ""])),
  );
  if (file.endsWith("route_config.csv")) {
    if (records.length !== 3 || records.some((record) =>
      Number(record.fixed_event_point_count) !== 20 ||
      Number(record.fixed_event_template_count) !== 70 ||
      Number(record.random_event_template_count) !== 10 ||
      !String(record.special_rule).includes("三阶段事件链") ||
      !String(record.special_rule).includes("16个非链支线")
    )) throw new Error(file + ": inconsistent standard route counts or sampling rules");
  }
  if (file.endsWith("event_point_config.csv")) {
    const routeIds = ["route_shattered_sail_reef", "route_golden_tide_trade", "route_moon_tide_ruins"];
    const hiddenTargets = {
      route_shattered_sail_reef: 35,
      route_golden_tide_trade: 32,
      route_moon_tide_ruins: 38,
    };
    if (records.length !== 240 || routeIds.some((routeId) => {
      const routeEvents = records.filter((record) => record.route_id === routeId);
      const fixedEvents = routeEvents.filter((record) => record.trigger_mode !== "random");
      const randomEvents = routeEvents.filter((record) => record.trigger_mode === "random");
      const policies = new Set(fixedEvents.map((record) => record.repeat_policy));
      const uniqueArt = new Set(fixedEvents.map((record) =>
        [record.art_sheet, record.grid_row, record.grid_col].join("|")));
      return routeEvents.length !== 80 ||
        fixedEvents.length !== 70 ||
        randomEvents.length !== 10 ||
        randomEvents.filter((record) => record.random_rule_id === "ambient_by_route_distance").length !== 8 ||
        fixedEvents.filter((record) => record.trigger_mode === "fixed_hidden").length !== hiddenTargets[routeId] ||
        new Set(routeEvents.map((record) => record.event_id)).size !== 80 ||
        uniqueArt.size !== 70 ||
        fixedEvents.filter((record) => String(record.art_sheet).includes("_unique_v4.png")).length !== 30 ||
        !["once_per_chart", "diminishing", "renewable"].every((policy) => policies.has(policy)) ||
        routeEvents.some((record) =>
          !["once_per_chart", "diminishing", "renewable", "random_voyage"].includes(String(record.repeat_policy)) ||
          !["yes", "no"].includes(String(record.repeatable)) ||
          !["exhausted", "diminishing", "none"].includes(String(record.reward_decay)) ||
          !String(record.repeat_reward_multipliers) ||
          !["chart_seed_endpoints", "voyage"].includes(String(record.repeat_scope)) ||
          !/_v2\.png|_expansion_v3\.png|_unique_v4\.png/.test(String(record.art_sheet)));
    })) throw new Error(file + ": expected 70 unique-art fixed + 10 random templates and repeat metadata per route");
    standardEventRecords = records;
    const structuredChainEvents = records.filter((record) =>
      ["a", "b", "c"].some((suffix) =>
        Object.prototype.hasOwnProperty.call(parseStructuredChoice(record, suffix).effect, "chainInfluence")));
    if (structuredChainEvents.length !== 27) {
      throw new Error(file + ": expected 27 events with structured chain influence");
    }
  }
  if (file.endsWith("event_chain_config.csv")) {
    const routeIds = ["route_shattered_sail_reef", "route_golden_tide_trade", "route_moon_tide_ruins"];
    const eventById = Object.fromEntries(standardEventRecords.map((record) => [record.event_id, record]));
    const usedEvents = new Set();
    if (records.length !== 9 || routeIds.some((routeId) =>
      records.filter((record) => record.route_id === routeId).length !== 3)) {
      throw new Error(file + ": expected three chains per route");
    }
    records.forEach((record) => {
      [1, 2, 3].forEach((stage) => {
        const eventId = record["stage_" + stage + "_event_id"];
        if (!eventById[eventId] || eventById[eventId].route_id !== record.route_id || usedEvents.has(eventId)) {
          throw new Error(file + ": invalid or duplicate chain event " + eventId);
        }
        usedEvents.add(eventId);
      });
      ["positive_effect_json", "neutral_effect_json", "negative_effect_json"].forEach((field) => {
        let effect;
        try {
          effect = JSON.parse(record[field]);
        } catch (error) {
          throw new Error(file + ": invalid " + field + " for " + record.chain_id);
        }
        if (!effect || typeof effect !== "object" || Array.isArray(effect) ||
            Object.values(effect).some((value) => typeof value !== "number" || !Number.isFinite(value))) {
          throw new Error(file + ": invalid effect values for " + record.chain_id);
        }
      });
      if (Number(record.positive_threshold) !== 2 || Number(record.negative_threshold) !== -2) {
        throw new Error(file + ": invalid resolution thresholds for " + record.chain_id);
      }
    });
    if (usedEvents.size !== 27) throw new Error(file + ": expected 27 unique chain events");
  }
  if (file.endsWith("ship_config.csv") &&
      (records.filter((record) => record.implementation_status === "current").length !== 8 ||
       records.some((record) => record.implementation_status === "current" && !record.demo_id))) {
    throw new Error(file + ": expected 8 implemented ships");
  }
  if (file.endsWith("crew_config.csv") &&
      (records.filter((record) => record.implementation_status === "current").length !== 12 ||
       records.some((record) => record.implementation_status === "current" &&
         (!record.demo_id || Number(record.wage_gold) <= 0)))) {
    throw new Error(file + ": expected 12 implemented special crew with positive wages");
  }
  if (file.endsWith("card_config.csv") && records.length !== 24) {
    throw new Error(file + ": expected 24 cards");
  }
  if (file.endsWith("port_config.csv") &&
      (records.length !== 5 || records.some((record) =>
        !String(record.chart_office).trim() ||
        Number(record.chart_reward_multiplier) < 0.9 ||
        Number(record.chart_reward_multiplier) > 1.25 ||
        !String(record.chart_bonus).trim() ||
        !String(record.standing_focus).trim()))) {
    throw new Error(file + ": expected 5 ports with complete chart offices, rewards and standing focus");
  }
  if (file.endsWith("dispatch_config.csv") &&
      (records.length !== 5 || records.some((record) => record.duration_options !== "8|12|24|48|168"))) {
    throw new Error(file + ": expected 5 dispatch tasks and five duration options");
  }
  if (file.endsWith("component_config.csv") &&
      (records.length !== 125 || new Set(records.map((record) => record.slot)).size !== 5 ||
       new Set(records.map((record) => record.rarity)).size !== 5 ||
       records.some((record) => !record.effect || !record.specialization || !record.description || !record.art_source_id) ||
       ["bow", "sail", "cannon", "plating", "instrument"].some((slot) =>
         ["common", "uncommon", "rare", "epic", "legendary"].some((rarity) =>
           records.filter((record) => record.slot === slot && record.rarity === rarity).length !== 5)))) {
    throw new Error(file + ": expected 125 authored components with five variants in every slot and rarity");
  }
  if (file.endsWith("art_manifest.csv")) {
    const standardCounts = ["route_shattered_sail_reef", "route_golden_tide_trade", "route_moon_tide_ruins"]
      .map((routeId) => records.filter((record) => record.route_id === routeId).length);
    if (records.length !== 234 || standardCounts.some((count) => count !== 71) ||
        records.filter((record) => record.route_id === "route_treasure_hunt").length !== 21) {
      throw new Error(file + ": standard or treasure art mappings are incomplete");
    }
  }
  const lastColumn = String.fromCharCode(64 + Math.min(values[0].length, 26));
  const inspection = await workbook.inspect({
    kind: "table",
    sheetId: "Config",
    range: "A1:" + lastColumn + Math.min(values.length, 6),
    tableMaxRows: 6,
    tableMaxCols: 26,
    maxChars: 1200,
  });
  console.log(JSON.stringify({
    file,
    rows: values.length - 1,
    columns: values[0].length,
    headers: values[0],
    inspection: inspection.ndjson.length > 0,
  }));
}
