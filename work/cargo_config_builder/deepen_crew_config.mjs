import fs from "node:fs/promises";
import path from "node:path";
import { Workbook } from "@oai/artifact-tool";

const root = path.resolve(import.meta.dirname, "../..");
const crewFile = path.join(root, "outputs/balance_configs/crew_config.csv");
const previewDir = path.join(import.meta.dirname, "previews_standard_routes");

const crewGrowth = {
  captain: ["旧旗重挂", "gold:120|cloth:10", "fame:10|map:1"],
  gunner: ["最后一门旧炮", "powder:8|cannonball:12", "fame:8|iron:6"],
  navigator: ["失踪星位", "water:120|map:1", "fame:10|lantern:1"],
  carpenter: ["龙骨上的名字", "wood:24|iron:6", "fame:8|hammer:1"],
  doctor: ["无名伤员", "medicine:6|gold:80", "fame:10|food:80"],
  cook: ["盐锅旧配方", "food:180|beer:8", "fame:8|medicine:2"],
  boatswain: ["铁钩誓约", "iron:10|beer:8", "fame:10|cannonball:10"],
  treasurer: ["被撕掉的账页", "gold:160|contractScroll:1", "fame:12|cloth:12"],
  smuggler: ["红巾暗号", "cloth:18|gold:100", "fame:10|gem:1"],
  priest: ["深井回声", "curse:3|medicine:4", "fame:14|relic:1"],
  deserter: ["帝国旧军令", "cannonball:16|medicine:3", "fame:10|powder:8"],
  diver: ["沉底的铜牌", "map:2|lantern:1", "fame:12|blackPearl:1"],
};

function csvCell(value) {
  const text = String(value ?? "");
  return /[",\r\n]/.test(text) ? '"' + text.replace(/"/g, '""') + '"' : text;
}

function toCsv(records, headers) {
  return "\uFEFF" + [headers, ...records.map((record) => headers.map((header) => record[header] ?? ""))]
    .map((row) => row.map(csvCell).join(",")).join("\n") + "\n";
}

function columnName(number) {
  let result = "";
  for (let value = number; value > 0; value = Math.floor((value - 1) / 26)) {
    result = String.fromCharCode(65 + (value - 1) % 26) + result;
  }
  return result;
}

async function updateCsv(filePath, sheetName, additions, decorate) {
  const sourceText = (await fs.readFile(filePath, "utf8")).replace(/^\uFEFF/, "");
  const sourceBook = await Workbook.fromCSV(sourceText, { sheetName });
  const sourceValues = sourceBook.worksheets.getItem(sheetName).getUsedRange().values;
  const sourceHeaders = sourceValues[0].map(String);
  const headers = sourceHeaders.concat(additions.filter((header) => !sourceHeaders.includes(header)));
  const records = sourceValues.slice(1).map((row) => {
    const record = Object.fromEntries(sourceHeaders.map((header, index) => [header, row[index] ?? ""]));
    return decorate(record);
  });
  await fs.writeFile(filePath, toCsv(records, headers), "utf8");

  const outputText = (await fs.readFile(filePath, "utf8")).replace(/^\uFEFF/, "");
  const workbook = await Workbook.fromCSV(outputText, { sheetName });
  const sheet = workbook.worksheets.getItem(sheetName);
  const values = sheet.getUsedRange().values;
  const endColumn = columnName(values[0].length);
  sheet.showGridLines = false;
  sheet.freezePanes.freezeRows(1);
  sheet.getRange(`A1:${endColumn}1`).format = {
    fill: "#17444A",
    font: { bold: true, color: "#F4ECD2" },
    wrapText: true,
  };
  sheet.getRange(`A1:${endColumn}${values.length}`).format.autofitColumns();
  sheet.getRange(`A1:${endColumn}${values.length}`).format.autofitRows();
  const inspection = await workbook.inspect({
    kind: "table", sheetId: sheetName, range: `A1:${endColumn}${values.length}`,
    tableMaxRows: values.length, tableMaxCols: values[0].length, maxChars: 4200,
  });
  if (!inspection.ndjson) throw new Error(sheetName + " artifact-tool inspection failed");
  const preview = await workbook.render({
    sheetName, range: `A1:${endColumn}${values.length}`, scale: 0.8, format: "png",
  });
  await fs.writeFile(path.join(previewDir, sheetName + ".png"), new Uint8Array(await preview.arrayBuffer()));
  return { rows: records.length, columns: headers.length };
}

await fs.mkdir(previewDir, { recursive: true });
const crewResult = await updateCsv(crewFile, "CrewGrowth", [
  "mastery_max_level", "personal_event_level", "personal_event_name",
  "personal_event_cost", "personal_event_reward",
], (record) => {
  const growth = crewGrowth[record.demo_id];
  if (!growth) throw new Error("missing crew growth configuration for " + record.demo_id);
  return Object.assign(record, {
    mastery_max_level: 5,
    personal_event_level: 3,
    personal_event_name: growth[0],
    personal_event_cost: growth[1],
    personal_event_reward: growth[2],
  });
});

console.log(JSON.stringify({ crewResult, crewFile }, null, 2));
