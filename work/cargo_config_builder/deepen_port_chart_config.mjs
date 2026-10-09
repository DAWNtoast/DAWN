import fs from "node:fs/promises";
import path from "node:path";
import { Workbook } from "@oai/artifact-tool";

const root = path.resolve(import.meta.dirname, "../..");
const portFile = path.join(root, "outputs/balance_configs/port_config.csv");
const previewDir = path.join(import.meta.dirname, "previews_standard_routes");

const chartOffices = {
  "断锚港": {
    chart_office: "锚链测绘局",
    chart_reward_multiplier: 0.95,
    chart_bonus: "wood:12",
    standing_focus: "维修与打捞",
  },
  "白贝港": {
    chart_office: "岛链引水厅",
    chart_reward_multiplier: 1,
    chart_bonus: "water:50|food:30",
    standing_focus: "补给与引航",
  },
  "铜钟港": {
    chart_office: "航标工造署",
    chart_reward_multiplier: 1.05,
    chart_bonus: "wood:8|iron:4",
    standing_focus: "维修与派遣",
  },
  "盐冠港": {
    chart_office: "黄金潮航务所",
    chart_reward_multiplier: 1.15,
    chart_bonus: "cloth:12",
    standing_focus: "贸易与护航",
  },
  "暮潮港": {
    chart_office: "月潮档案馆",
    chart_reward_multiplier: 1.2,
    chart_bonus: "map:1",
    standing_focus: "遗迹与测绘",
  },
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

const sourceText = (await fs.readFile(portFile, "utf8")).replace(/^\uFEFF/, "");
const sourceBook = await Workbook.fromCSV(sourceText, { sheetName: "Ports" });
const sourceValues = sourceBook.worksheets.getItem("Ports").getUsedRange().values;
const sourceHeaders = sourceValues[0].map(String);
const headers = sourceHeaders.concat([
  "chart_office",
  "chart_reward_multiplier",
  "chart_bonus",
  "standing_focus",
].filter((header) => !sourceHeaders.includes(header)));
const records = sourceValues.slice(1).map((row) => {
  const record = Object.fromEntries(sourceHeaders.map((header, index) => [header, row[index] ?? ""]));
  const office = chartOffices[record.name];
  if (!office) throw new Error("missing chart office configuration for " + record.name);
  return Object.assign(record, office);
});

if (records.length !== 5) throw new Error("port config must contain exactly 5 ports");
await fs.writeFile(portFile, toCsv(records, headers), "utf8");
await fs.mkdir(previewDir, { recursive: true });

const outputText = (await fs.readFile(portFile, "utf8")).replace(/^\uFEFF/, "");
const workbook = await Workbook.fromCSV(outputText, { sheetName: "PortChartValue" });
const sheet = workbook.worksheets.getItem("PortChartValue");
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
  kind: "table",
  sheetId: "PortChartValue",
  range: `A1:${endColumn}${values.length}`,
  tableMaxRows: values.length,
  tableMaxCols: values[0].length,
  maxChars: 3200,
});
if (!inspection.ndjson) throw new Error("port chart value artifact-tool inspection failed");
const preview = await workbook.render({
  sheetName: "PortChartValue",
  range: `A1:${endColumn}${values.length}`,
  scale: 1,
  format: "png",
});
await fs.writeFile(path.join(previewDir, "port_chart_value.png"), new Uint8Array(await preview.arrayBuffer()));
console.log(JSON.stringify({ rows: values.length - 1, columns: values[0].length, portFile }));
