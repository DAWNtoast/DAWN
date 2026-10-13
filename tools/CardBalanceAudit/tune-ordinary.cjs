'use strict';

// Candidate search against the immutable pre-review export. Roles and peer
// budgets are never rebuilt from edited cards. This is an effect-budget aid,
// not an automated claim of equal win rate.
const fs = require('node:fs');
const path = require('node:path');
const power = require('./power-model.cjs');
const root = path.resolve(__dirname, '../..');
const before = power.readTsv(path.join(root, 'UnityClient/Artifacts/CardBalanceReview20261012/CoreBefore/cards.tsv'));
const model = power.buildModel(before);
const originals = before.filter(row => row.UpgradeLevel === 0 && !row.SchoolId);
const globals = { Polish: 5, Load: 8, PrecisionLoad: 8, FireRoundsBurn: 4 };
const rates = row => [0, 1, 2, 3].map(level => power.evaluateRow(power.upgradeRow(row, level), model).ratio);
function loss(row, original) {
  const ratios = rates(row);
  const deviations = ratios.map(value => Math.abs(value - 1));
  const changes = ['Damage', 'Block', 'Repair'].reduce((sum, key) =>
    sum + Math.abs(row[key] - original[key]) / Math.max(1, original[key]), 0);
  // Minimax keeps one upgrade from compensating for a bad different upgrade.
  return Math.max(...deviations) + deviations.reduce((sum, value) => sum + value * value, 0) / 4
    + changes * 0.003 + Math.abs(row.Energy - original.Energy) * 0.04;
}
function candidate(original) {
  let best = { ...original };
  if (['Polish', 'Load', 'PrecisionLoad'].includes(best.Action)) best.StrategyAmount = globals[best.Action];
  if (best.Action === 'FireRounds') best.BurnDamage = globals.FireRoundsBurn;
  // Extra loading now also replaces the preparation card spent, as explicitly
  // authored in the catalogue. It still pays its extra powder before firing.
  if (best.Id === 'card_load_shot') {
    best.Draw = 1;
    best.DescriptionTemplate = best.DescriptionTemplate.replace('；消耗 {powder}', '；抽 {draw} 张；消耗 {powder}');
  }
  if (best.Id === 'card_chain_shot') return best; // User-visible numerical anchor.
  if (rates(best).every(value => value >= .9 && value <= 1.1)) return best;
  const keys = ['Damage', 'Block', 'Repair'].filter(key => original[key] > 0);
  const base = { ...best };
  let bestLoss = loss(best, original);
  const visit = (row, i) => {
    if (i === keys.length) {
      // Preserve the authored mix: a doctor must not become a shield card just
      // because integer rounding makes that combination closer to one score.
      const scales = keys.map(key => row[key] / original[key]);
      if (scales.length > 1 && Math.max(...scales) - Math.min(...scales) > .35) return;
      const score = loss(row, original);
      if (score < bestLoss - 1e-10) { best = { ...row }; bestLoss = score; }
      return;
    }
    const key = keys[i];
    const max = Math.max(30, Math.ceil(original[key] * 2));
    for (let value = 1; value <= max; value++) visit({ ...row, [key]: value }, i + 1);
  };
  // Preserve energy and draw identities. These are both discrete access to
  // actions, not arbitrary coefficients to flatten into fractional prices.
  visit(base, 0);
  return best;
}
const candidates = originals.map(candidate).map(row => {
  const fields = { damage: 'Damage', block: 'Block', repair: 'Repair', draw: 'Draw', movement: 'MovementMeters',
    strategyAmount: 'StrategyAmount', strategyLimit: 'StrategyLimit', burnDamage: 'BurnDamage',
    burnDuration: 'BurnDuration', powder: 'PowderCost', cannonballs: 'CannonballCost' };
  row.Description = row.DescriptionTemplate.replace(/\{(\w+)\}/g, (all, key) => fields[key] ? String(row[fields[key]]) : all);
  return row;
});
const output = path.join(root, 'UnityClient/Artifacts/CardBalanceReview20261012');
fs.mkdirSync(output, { recursive: true });
fs.writeFileSync(path.join(output, 'ordinary-candidates.json'), JSON.stringify(candidates, null, 2) + '\n');
const rootOutput = path.join(root, 'Artifacts/CardBalanceReview20261012');
fs.mkdirSync(rootOutput, { recursive: true });
fs.writeFileSync(path.join(rootOutput, 'ordinary-candidates.json'), JSON.stringify(candidates, null, 2) + '\n');
const details = candidates.map((row, i) => ({ id: row.Id, name: row.Name,
  changes: Object.fromEntries(['Energy', 'Damage', 'Block', 'Repair', 'Draw', 'StrategyAmount', 'BurnDamage']
    .filter(key => row[key] !== originals[i][key]).map(key => [key, { before: originals[i][key], after: row[key] }])),
  before: rates(originals[i]), after: rates(row) }));
const summary = { globals, count: candidates.length,
  meanByRank: [0, 1, 2, 3].map(level => details.reduce((sum, card) => sum + card.after[level], 0) / details.length),
  medianByRank: [0, 1, 2, 3].map(level => { const sorted = details.map(card => card.after[level]).sort((a, b) => a - b); return (sorted[41] + sorted[42]) / 2; }),
  changed: details.filter(card => Object.keys(card.changes).length).length,
  outside: details.filter(card => card.after.some(value => value < .9 || value > 1.1)), details };
fs.writeFileSync(path.join(output, 'ordinary-tuning-summary.json'), JSON.stringify(summary, null, 2) + '\n');
console.log(JSON.stringify({ ...summary, details: undefined }, null, 2));

if (process.argv.includes('--apply')) {
  const files = ['CoreNavalBattle.cs', 'CoreSpecialCardCatalog.cs'].map(file =>
    path.join(root, 'UnityClient/Assets/_Project/Core/Runtime', file));
  let naval = fs.readFileSync(files[0], 'utf8');
  let special = fs.readFileSync(files[1], 'utf8');
  for (const row of candidates) {
    const original = originals.find(value => value.Id === row.Id);
    if (row.Id.startsWith('card_special_')) {
      const id = row.Id.slice('card_special_'.length);
      let matches = 0;
      special = special.split('\n').map(line => {
        if (!line.includes('(cards, "' + id + '"')) return line;
        matches++;
        for (const [field, key] of Object.entries({ energy: 'Energy', damage: 'Damage', block: 'Block', repair: 'Repair', draw: 'Draw' })) {
          if (row[key] === original[key]) continue;
          const match = new RegExp('\\b' + field + ': \\d+');
          if (match.test(line)) line = line.replace(match, field + ': ' + row[key]);
          else line = line.replace(/\);\r?$/, ', ' + field + ': ' + row[key] + ');' + (line.endsWith('\r') ? '\r' : ''));
        }
        return line;
      }).join('\n');
      if (matches !== 1) throw new Error('Expected one source definition: ' + id + ', got ' + matches);
    } else {
      const expression = new RegExp('(new CoreBattleCard\\("' + row.Id + '", "[^"\\n]*", "[^"\\n]*", )\\d+, \\d+, \\d+, \\d+, \\d+,');
      if (!expression.test(naval)) throw new Error('Missing explicit ordinary definition: ' + row.Id);
      naval = naval.replace(expression, '$1' + [row.Energy, row.Damage, row.Block, row.Repair, row.Draw].join(', ') + ',');
    }
  }
  naval = naval.replace('Action == NavalBattleCardAction.Polish ? 2 :', 'Action == NavalBattleCardAction.Polish ? 5 :');
  naval = naval.replace('Action == NavalBattleCardAction.Load ? 4 :', 'Action == NavalBattleCardAction.Load ? 8 :');
  naval = naval.replace('public int BurnDamage => 5 * NumericalMultiplier;',
    'public int BurnDamage => (Action == NavalBattleCardAction.FireRounds ? 4 : 5) * NumericalMultiplier;');
  naval = naval.replace('额外装药：下次炮击 +{strategyAmount}，最多 +{strategyLimit}；消耗 {powder} 火药；开炮另付弹药并清空增益。',
    '额外装药：下次炮击 +{strategyAmount}，最多 +{strategyLimit}；抽 {draw} 张；消耗 {powder} 火药；开炮另付弹药并清空增益。');
  fs.writeFileSync(files[0], naval);
  fs.writeFileSync(files[1], special);
  for (const row of candidates) {
    const source = row.Id.startsWith('card_special_') ? special : naval;
    const needle = row.Id.startsWith('card_special_') ? '(cards, "' + row.Id.slice(13) + '"' : 'new CoreBattleCard("' + row.Id + '"';
    const lines = source.split('\n').filter(line => line.includes(needle));
    if (lines.length !== 1) throw new Error('Ambiguous catalogue definition: ' + row.Id);
    if (row.Id.startsWith('card_special_')) {
      for (const [field, key, fallback] of [['energy', 'Energy', 1], ['damage', 'Damage', 0], ['block', 'Block', 0], ['repair', 'Repair', 0], ['draw', 'Draw', 0]]) {
        const match = lines[0].match(new RegExp('\\b' + field + ': (\\d+)'));
        if ((match ? Number(match[1]) : fallback) !== row[key]) throw new Error('Mismatch: ' + row.Id + ' ' + key);
      }
    } else {
      const match = lines[0].match(/, (\d+), (\d+), (\d+), (\d+), (\d+),/);
      if (!match || ['Energy', 'Damage', 'Block', 'Repair', 'Draw'].some((key, index) => Number(match[index + 1]) !== row[key]))
        throw new Error('Mismatch: ' + row.Id);
    }
  }
  console.log('Updated only CoreNavalBattle.cs and ordinary source helper lines in CoreSpecialCardCatalog.cs.');
  console.log('Verified 84 explicit definitions match the candidate numbers.');
}
