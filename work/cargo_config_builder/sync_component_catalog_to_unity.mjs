import fs from "node:fs/promises";
import path from "node:path";

const root = path.resolve(import.meta.dirname, "../..");
const sourcePath = path.join(root, "outputs", "balance_configs", "component_config.csv");
const unityPath = path.join(
  root,
  "UnityClient",
  "Assets",
  "_Project",
  "Resources",
  "Data",
  "component_catalog_v2.csv",
);

const text = (await fs.readFile(sourcePath, "utf8")).replace(/^\uFEFF/, "");
const lines = text.split(/\r?\n/).filter((line) => line.trim().length > 0);
const requiredHeaders = [
  "component_id", "name", "slot", "rarity", "volume", "install_hours", "install_wood",
  "shop_rarity_price", "description", "effect", "specialization", "art_source_id",
];
const headers = new Set(lines[0].split(","));
if (lines.length !== 126 || requiredHeaders.some((header) => !headers.has(header))) {
  throw new Error("component_config.csv must contain the 21-column header and exactly 125 component rows");
}

await fs.mkdir(path.dirname(unityPath), { recursive: true });
await fs.writeFile(unityPath, text.endsWith("\n") ? text : text + "\n", "utf8");
console.log(JSON.stringify({ sourcePath, unityPath, components: lines.length - 1 }, null, 2));
