'use strict';
// One-time materialization against the immutable reviewed export, never a runtime multiplier.
const fs = require('node:fs');
const path = require('node:path');
const { tsv } = require('./summarize-dummy-audit.cjs');
const root = path.resolve(__dirname, '../..');
const out = path.join(root, 'Artifacts/CardDefenseReduction20261012');
const beforePath = path.join(root, 'Artifacts/CardSchoolRebalance20261012/CoreReviewed/cards.tsv');
const before = tsv(beforePath).filter(c => c.UpgradeLevel === '0');
const lower = n => Number(n) > 0 ? Math.max(1, Math.round(Number(n) * 0.7)) : 0;
const changes = before.filter(c => lower(c.Block) !== Number(c.Block) || lower(c.Repair) !== Number(c.Repair));
const sourceFiles = ['UnityClient/Assets/_Project/Core/Runtime/CoreNavalBattle.cs', 'UnityClient/Assets/_Project/Core/Runtime/CoreSpecialCardCatalog.cs', 'tools/CardSchools/build-catalog.cjs'];
const originals = sourceFiles.map(f => fs.readFileSync(path.join(root, f), 'utf8'));
let [naval, special, generator] = originals;
for (const c of before.filter(c => !c.SchoolId)) {
  const values = [c.Energy, c.Damage, c.Block, c.Repair, c.Draw];
  if (c.Id.startsWith('card_special_')) {
    const key = '(cards, "' + c.Id.slice('card_special_'.length) + '"';
    let matches = 0;
    special = special.split('\n').map(line => {
      if (!line.includes(key)) return line;
      matches++;
      for (const [field, column] of [['block', 'Block'], ['repair', 'Repair']]) {
        const pattern = new RegExp('\\b' + field + ': (\\d+)');
        const found = line.match(pattern);
        const current = Number(found?.[1] || 0);
        if (current !== Number(c[column])) throw Error(`Baseline mismatch ${c.Id} ${field}: ${current}`);
        if (found) line = line.replace(pattern, field + ': ' + lower(current));
      }
      return line;
    }).join('\n');
    if (matches !== 1) throw Error(`Expected one ordinary source ${c.Id}: ${matches}`);
  } else {
    const pattern = new RegExp('(new CoreBattleCard\\("' + c.Id + '", "[^"\\n]*", "[^"\\n]*", )(\\d+), (\\d+), (\\d+), (\\d+), (\\d+),');
    const found = naval.match(pattern);
    if (!found || values.some((v, i) => Number(v) !== Number(found[i + 2]))) throw Error('Baseline mismatch ' + c.Id);
    naval = naval.replace(pattern, (_, prefix, energy, damage, block, repair, draw) =>
      prefix + [energy, damage, lower(block), lower(repair), draw].join(', ') + ',');
  }
}
const generatorPath = path.join(root, sourceFiles[2]);
const schoolCards = require(generatorPath).cards;
let index = 0;
generator = generator.replace(/^add\((.*)\);$/gm, (line, args) => {
  const card = schoolCards[index++];
  const expected = before.find(c => c.Id === card.id);
  if (!expected || Number(expected.Block) !== (card.combat.b || 0) || Number(expected.Repair) !== (card.combat.h || 0))
    throw Error('School baseline mismatch ' + card.id);
  // Locate the two JSON objects without touching the scene, mechanic or resource rules.
  const match = args.match(/^("[^"]*","[^"]*","[^"]*",\d+,"[^"]*",\{[^}]*\},)(\{[^}]*\})(,.*)$/);
  if (!match) throw Error('Unexpected authored school line ' + card.id);
  const combat = JSON.parse(match[2]);
  if ('b' in combat) combat.b = lower(combat.b);
  if ('h' in combat) combat.h = lower(combat.h);
  return 'add(' + match[1] + JSON.stringify(combat) + match[3] + ');';
});
if (index !== 132) throw Error('School line count mismatch');
fs.mkdirSync(path.join(out, 'BeforeSource'), { recursive: true });
for (let i = 0; i < sourceFiles.length; i++) {
  const backup = path.join(out, 'BeforeSource', path.basename(sourceFiles[i]));
  if (fs.existsSync(backup)) throw Error('This reduction already has a source snapshot; do not apply twice.');
}
for (let i = 0; i < sourceFiles.length; i++) fs.writeFileSync(path.join(out, 'BeforeSource', path.basename(sourceFiles[i])), originals[i]);
[naval, special, generator].forEach((text, i) => fs.writeFileSync(path.join(root, sourceFiles[i]), text));
delete require.cache[require.resolve(generatorPath)];
require(generatorPath).write({ catalogOnly: true, contentExport: 'Artifacts/CardDefenseReduction20261012/school-card-content.json' });
const manifest = {
  baseline: path.relative(root, beforePath), formula: 'positive fixed Block/Repair => max(1, round(value * 0.7)); zero remains zero',
  scope: 'All published original-rank fixed Block/Repair definitions, including ordinary, school, crew and equipment cards.',
  exclusions: 'SchoolEffect conditional/paid bonuses, damage, resource costs, energy/draw, the fixed 2-health no-supply fallback, and the 1/1.2/1.5/2 upgrade curve.',
  reviewedCards: before.length, positiveBaseDefenseCards: before.filter(c => Number(c.Block) || Number(c.Repair)).length,
  changedCards: changes.length,
  cards: changes.map(c => ({ id: c.Id, name: c.Name, school: c.SchoolId || 'ordinary', before: { block: Number(c.Block), repair: Number(c.Repair) }, after: { block: lower(c.Block), repair: lower(c.Repair) } }))
};
fs.writeFileSync(path.join(out, 'base-defense-changes.json'), JSON.stringify(manifest, null, 2));
console.log(JSON.stringify({ positiveBaseDefenseCards: manifest.positiveBaseDefenseCards, changedCards: changes.length }));
