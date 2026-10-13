'use strict';
// Retired by user request; historical implementation remains below.
throw new Error("UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.");

// Keep saved Unity previews aligned with the runtime's borderless event artwork.
// Run with --write to apply; without it this only reports pending changes.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');

const root = path.resolve(__dirname, '..', '..');
const sceneRoot = path.join(root, 'UnityClient/Assets/_Project/Scenes/UIPreviewsForMe');
const artifactRoot = path.join(root, 'Artifacts/NavigationEventMarker20261013');
const write = process.argv.includes('--write');
const imageGuid = 'fe87c0e1cc204ed48ad3b37840f39efc';
const outlineGuid = 'e19747de3f5aca642ab2be37e372fb86';
const pulseGuid = '310aa09bb3d8414ca9fa851be1fe836d';

function files(dir) {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap(entry => {
    const file = path.join(dir, entry.name);
    return entry.isDirectory() ? files(file) : file.endsWith('.unity') ? [file] : [];
  });
}
function parse(text) {
  const headers = [...text.matchAll(/^--- !u!(\d+) &(-?\d+)[^\r\n]*/gm)];
  return headers.map((header, index) => {
    const start = header.index;
    const end = index + 1 < headers.length ? headers[index + 1].index : text.length;
    const body = text.slice(start, end);
    return { id: header[2], type: header[1], start, end, body, updated: body,
      owner: body.match(/^  m_GameObject: \{fileID: (-?\d+)\}/m)?.[1],
      name: body.match(/^  m_Name: (.*)$/m)?.[1].trim(),
      guid: body.match(/^  m_Script: \{fileID: [^,]+, guid: ([a-f0-9]+),/m)?.[1] };
  });
}
function replaceOne(block, pattern, replacement) {
  const matches = [...block.updated.matchAll(new RegExp(pattern.source, pattern.flags.includes('g') ? pattern.flags : pattern.flags + 'g'))];
  if (matches.length !== 1) throw new Error(`Expected one field on component ${block.id}, got ${matches.length}`);
  block.updated = block.updated.replace(pattern, replacement);
}
function hash(bytes) { return crypto.createHash('sha256').update(bytes).digest('hex'); }

const results = [];
for (const file of files(sceneRoot)) {
  const original = fs.readFileSync(file);
  const text = original.toString('utf8');
  if (!text.includes('  m_Name: Event Node ')) continue;
  const blocks = parse(text);
  const byOwner = new Map();
  for (const block of blocks) {
    if (!block.owner) continue;
    if (!byOwner.has(block.owner)) byOwner.set(block.owner, []);
    byOwner.get(block.owner).push(block);
  }
  const nodes = blocks.filter(block => block.type === '1' && block.name?.startsWith('Event Node '));
  const icons = blocks.filter(block => block.type === '1' && block.name?.startsWith('Event Icon '));
  const report = { scene: path.relative(root, file).replaceAll('\\', '/'), nodes: nodes.length,
    artNodes: 0, outlinesDisabled: 0, framesCleared: 0, pulseColorsCleared: 0, changes: [] };
  for (const node of nodes) {
    const components = byOwner.get(node.id) || [];
    const rect = components.find(block => block.type === '224');
    if (!rect) throw new Error(`Missing marker RectTransform: ${file} ${node.name}`);
    const icon = icons.find(candidate => candidate.name === node.name.replace('Event Node ', 'Event Icon ') &&
      (byOwner.get(candidate.id) || []).some(block => block.type === '224' &&
        block.body.match(/^  m_Father: \{fileID: (-?\d+)\}/m)?.[1] === rect.id));
    const hasArtwork = !!icon && (byOwner.get(icon.id) || []).some(block =>
      /^  m_Texture: \{fileID: (?!0\})/m.test(block.body));
    if (hasArtwork) report.artNodes++;
    for (const component of components) {
      if (component.guid === outlineGuid && /^  m_Enabled: 1\r?$/m.test(component.body)) {
        replaceOne(component, /^  m_Enabled: 1(?=\r?$)/m, '  m_Enabled: 0');
        report.outlinesDisabled++;
        report.changes.push({ node: node.name, component: component.id, field: 'm_Enabled', value: 0 });
      }
      if (hasArtwork && component.guid === imageGuid) {
        const alpha = component.body.match(/^  m_Color: \{[^\r\n]*, a: ([^}]+)\}/m)?.[1];
        if (alpha === undefined) throw new Error(`Missing marker Image color: ${file} ${node.name}`);
        if (Number(alpha) !== 0) {
          replaceOne(component, /(^  m_Color: \{[^\r\n]*, a: )[^}]+(\})/m, '$10$2');
          report.framesCleared++;
          report.changes.push({ node: node.name, component: component.id, field: 'm_Color.a', value: 0 });
        }
      }
      if (hasArtwork && component.guid === pulseGuid && /^  originColor: /m.test(component.body)) {
        const alpha = component.body.match(/^  originColor: \{[^\r\n]*, a: ([^}]+)\}/m)?.[1];
        if (alpha !== undefined && Number(alpha) !== 0) {
          replaceOne(component, /(^  originColor: \{[^\r\n]*, a: )[^}]+(\})/m, '$10$2');
          report.pulseColorsCleared++;
          report.changes.push({ node: node.name, component: component.id, field: 'originColor.a', value: 0 });
        }
      }
    }
  }
  const changed = blocks.filter(block => block.updated !== block.body);
  let updated = text;
  for (const block of changed.sort((a, b) => b.start - a.start))
    updated = updated.slice(0, block.start) + block.updated + updated.slice(block.end);
  const reparsed = parse(updated);
  if (reparsed.length !== blocks.length || reparsed.some((block, index) => block.id !== blocks[index].id || block.guid !== blocks[index].guid))
    throw new Error(`Component identities changed in ${file}`);
  report.changedComponents = changed.length;
  report.beforeSha256 = hash(original);
  report.afterSha256 = hash(Buffer.from(updated, 'utf8'));
  if (write && changed.length) {
    const backup = path.join(artifactRoot, 'saved-previews-before', path.relative(sceneRoot, file));
    fs.mkdirSync(path.dirname(backup), { recursive: true });
    if (!fs.existsSync(backup)) fs.writeFileSync(backup, original);
    fs.writeFileSync(file, updated, 'utf8');
  }
  results.push(report);
}
const summary = { write, scannedScenes: files(sceneRoot).length, eventScenes: results.length,
  changedScenes: results.filter(report => report.changedComponents).length,
  nodes: results.reduce((total, report) => total + report.nodes, 0),
  artNodes: results.reduce((total, report) => total + report.artNodes, 0),
  outlinesDisabled: results.reduce((total, report) => total + report.outlinesDisabled, 0),
  framesCleared: results.reduce((total, report) => total + report.framesCleared, 0),
  pulseColorsCleared: results.reduce((total, report) => total + report.pulseColorsCleared, 0) };
if (write) {
  fs.mkdirSync(artifactRoot, { recursive: true });
  fs.writeFileSync(path.join(artifactRoot, 'saved-preview-sync-report.json'), JSON.stringify({ ...summary, scenes: results }, null, 2) + '\n');
}
console.log(JSON.stringify(summary, null, 2));
