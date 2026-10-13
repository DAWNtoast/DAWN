'use strict';
// Retired by user request; historical implementation remains below.
throw new Error("UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.");

// Update only serialized Unity Text strings for the reviewed 2-copy / 10..20 rule.
// Existing objects, artwork, layout and all other component fields stay byte-identical.
const fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto');
const root = path.resolve(__dirname, '../..');
const sceneRoot = path.join(root, 'UnityClient/Assets/_Project/Scenes/UIPreviewsForMe');
const output = path.join(root, 'Artifacts/CardDeckSupplyAudit20261012/PreviewText');
const apply = process.argv.includes('--apply');
const files = dir => fs.readdirSync(dir, {withFileTypes:true}).flatMap(entry =>
  entry.isDirectory() ? files(path.join(dir, entry.name)) : entry.name.endsWith('.unity') ? [path.join(dir, entry.name)] : []);
const hash = text => crypto.createHash('sha256').update(text).digest('hex');
const encode = text => JSON.stringify(text).replace(/[\u007f-\uffff]/g, ch => '\\u' + ch.charCodeAt(0).toString(16).padStart(4, '0').toUpperCase());
function update(value) {
  if (value === '基础牌 8–10 张　·　同名最多 3 张') return '基础牌 10–20 张　·　每种 2 张';
  if (/^已加入 ×[012]$/.test(value)) return '已加入 ' + value.slice(-1) + ' / 2 张';
  return value.replace(/^(基础(?:卡| )\d+)\/10(?=$|[\s　])/u, '$1/20');
}
const changes = [];
for (const file of files(sceneRoot)) {
  const source = fs.readFileSync(file, 'utf8'), patches = [];
  for (const doc of source.matchAll(/^--- !u!(\d+) &(-?\d+)\r?\n([\s\S]*?)(?=^--- !u!|$(?![\s\S]))/gm)) {
    if (doc[1] !== '114') continue;
    const body = doc[3], match = body.match(/^  m_Text: ([\s\S]*?)(?=\r?\n  \w|$(?![\s\S]))/m);
    if (!match) continue;
    const raw = match[1].replace(/\r?\n\s+/g, ' ').trim();
    let before;
    try { before = raw.startsWith('"') ? JSON.parse(raw.replace(/\\x([0-9a-f]{2})/gi, '\\u00$1')) : raw; }
    catch { continue; }
    const after = update(before);
    if (after === before) continue;
    const offset = doc.index + doc[0].indexOf(body) + match.index;
    patches.push({fileId:doc[2], before, after, offset, raw:match[0], replacement:'  m_Text: ' + encode(after) + match[0].match(/\s*$/)[0]});
  }
  if (!patches.length) continue;
  let updated = source;
  for (const patch of patches.sort((a,b) => b.offset-a.offset))
    updated = updated.slice(0, patch.offset) + patch.replacement + updated.slice(patch.offset + patch.raw.length);
  const documentIds = text => [...text.matchAll(/^--- !u!\d+ &-?\d+/gm)].map(match => match[0]).join('\n');
  if (documentIds(source) !== documentIds(updated)) throw new Error('Unity document structure changed: ' + file);
  const relative = path.relative(sceneRoot, file);
  if (apply) {
    const backup = path.join(output, 'Before', relative);
    fs.mkdirSync(path.dirname(backup), {recursive:true});
    if (!fs.existsSync(backup)) fs.writeFileSync(backup, source);
    fs.writeFileSync(file, updated);
  }
  changes.push({file, beforeSHA256:hash(source), afterSHA256:hash(updated), strings:patches.map(({fileId,before,after}) => ({fileId,before,after}))});
}
fs.mkdirSync(output, {recursive:true});
fs.writeFileSync(path.join(output, apply ? 'changes.json' : 'plan.json'), JSON.stringify({applied:apply, scenes:changes.length, strings:changes.reduce((n,c)=>n+c.strings.length,0), changes}, null, 2));
console.log(JSON.stringify({applied:apply, scenes:changes.length, strings:changes.reduce((n,c)=>n+c.strings.length,0)}));
