import fs from "node:fs/promises";
import path from "node:path";
import { Workbook } from "@oai/artifact-tool";

const csvPath = path.resolve(import.meta.dirname, "../../outputs/balance_configs/voyage_cargo_config.csv");
const csv = (await fs.readFile(csvPath, "utf8")).replace(/^\uFEFF/, "");
const workbook = await Workbook.fromCSV(csv, { sheetName: "货舱资源" });
const sheet = workbook.worksheets.getItem("货舱资源");
const values = sheet.getUsedRange().values;
const lastRow = values.length;

sheet.showGridLines = false;
sheet.freezePanes.freezeRows(1);
sheet.getRange("A1:P1").format = {
  fill: "#17444A",
  font: { bold: true, color: "#F4ECD2" },
  wrapText: true,
};
sheet.getRange("A2:P" + lastRow).format = {
  font: { color: "#172326" },
  wrapText: true,
};
sheet.getRange("D2:M" + lastRow).format.numberFormat = "0.00";
sheet.getRange("A1:P" + lastRow).format.autofitColumns();
sheet.getRange("A1:P" + lastRow).format.autofitRows();

const inspection = await workbook.inspect({
  kind: "table",
  sheetId: "货舱资源",
  range: "A1:P" + lastRow,
  include: "values",
  tableMaxRows: 20,
  tableMaxCols: 16,
  maxChars: 6000,
});
console.log(inspection.ndjson);

const preview = await workbook.render({
  sheetName: "货舱资源",
  range: "A1:P" + lastRow,
  scale: 1,
  format: "png",
});
await fs.writeFile(path.resolve(import.meta.dirname, "cargo_config_preview.png"),
  new Uint8Array(await preview.arrayBuffer()));
console.log(JSON.stringify({ csvPath, rows: lastRow - 1 }));
