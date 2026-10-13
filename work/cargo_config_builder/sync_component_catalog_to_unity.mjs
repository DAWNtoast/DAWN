import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

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

const expectedHeaders = [
  "component_id", "name", "slot", "rarity", "install_wood", "max_hull", "armor",
  "firepower", "speed", "vision", "power", "cannon_slots", "shop_rarity_chance",
  "shop_rarity_price", "shop_slot_multiplier", "description", "effect",
  "specialization", "art_source_id", "story_id",
];
export function validateComponentCatalog(sourceText) {
  const text = sourceText.replace(/^\uFEFF/, "");
  const lines = text.split(/\r?\n/).filter((line) => line.trim().length > 0);
  if (lines.length !== 126 || lines[0] !== expectedHeaders.join(",")) {
    throw new Error("component_config.csv must contain the current 20-column header in runtime order and exactly 125 component rows");
  }
  const records = lines.slice(1).map((line, index) => {
    const cells = line.split(",");
    if (cells.length !== expectedHeaders.length) {
      throw new Error("component_config.csv row " + (index + 2) + ": expected 20 columns");
    }
    return Object.fromEntries(expectedHeaders.map((header, cell) => [header, cells[cell].trim()]));
  });
  const ids = new Set();
  for (const record of records) {
    if (!record.component_id || ids.has(record.component_id) ||
        record.art_source_id !== record.component_id ||
        !record.name || !record.description || !record.effect || !record.specialization || !record.story_id) {
      throw new Error("component_config.csv: each component needs a unique ID, its own matching art_source_id and complete identity: " + record.component_id);
    }
    ids.add(record.component_id);
  }
  for (const slot of ["bow", "sail", "cannon", "plating", "instrument"]) {
    for (const rarity of ["common", "uncommon", "rare", "epic", "legendary"]) {
      if (records.filter((record) => record.slot === slot && record.rarity === rarity).length !== 5) {
        throw new Error("component_config.csv: expected five variants for " + slot + "/" + rarity);
      }
    }
  }
  return { text: text.endsWith("\n") ? text : text + "\n", records };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  if (process.argv.slice(2).some((argument) => argument !== "--check")) {
    throw new Error("Usage: node sync_component_catalog_to_unity.mjs [--check]");
  }
  const checkOnly = process.argv.includes("--check");
  const { text, records } = validateComponentCatalog(await fs.readFile(sourcePath, "utf8"));
  if (!checkOnly) {
    await fs.mkdir(path.dirname(unityPath), { recursive: true });
    await fs.writeFile(unityPath, text, "utf8");
  }
  console.log(JSON.stringify({ sourcePath, unityPath, components: records.length, columns: expectedHeaders.length,
    independentArtMappings: true, mode: checkOnly ? "check-only" : "synced", written: !checkOnly }, null, 2));
}
