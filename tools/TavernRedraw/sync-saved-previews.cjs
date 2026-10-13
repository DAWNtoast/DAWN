'use strict';
// Retired by user request; historical implementation remains below.
throw new Error("UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.");

'use strict';

// Deliberately patches YAML text, never opens or serializes a Unity scene.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const projectRoot = 'D:\\Game\\UnityClient';
const artifactRoot = 'D:\\Game\\Artifacts\\TavernRedraw20261012';
const sceneRoot = path.join(projectRoot, 'Assets', '_Project', 'Scenes', 'UIPreviewsForMe');
const references = ['UI_Tavern.unity', 'UITavern/UITavern_Roster.unity', 'TutorialVoyage/08_Tavern.unity'];
const ports = ['断锚港', '白贝港', '铜钟港', '盐冠港', '暮潮港', '灰鸥港'];
const blockPattern = /^--- !u!(\d+) &(-?\d+)[^\r\n]*\r?\n[\s\S]*?(?=^--- !u!|$(?![\s\S]))/gm;
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const escape = text => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const near = (a, b) => Math.abs(a - b) < 0.0001;
function requireThat(condition, message) { if (!condition) throw new Error(message); }
function single(items, message) { requireThat(items.length === 1, message + ': count=' + items.length); return items[0]; }
function property(block, key) {
  return single([...block.text.matchAll(new RegExp('^  ' + escape(key) + ': ([^\\r\\n]*)', 'gm'))], 'Expected property ' + key)[1];
}
function vector(block, key) {
  const text = property(block, key);
  const result = Object.fromEntries([...text.matchAll(/([xyzw]): ([-+\deE.]+)/g)].map(match => [match[1], Number(match[2])]));
  requireThat(Number.isFinite(result.x) && Number.isFinite(result.y), 'Invalid vector ' + key);
  return result;
}
function component(blocks, gameObject, type, marker = null) {
  return single(blocks.filter(block => block.type === type && new RegExp('^  m_GameObject: \\{fileID: ' + gameObject.id + '\\}\\r?$', 'm').test(block.text) && (!marker || block.text.includes(marker))), 'Expected component on ' + property(gameObject, 'm_Name'));
}
function named(blocks, name) {
  return single(blocks.filter(block => block.type === 1 && new RegExp('^  m_Name: ' + escape(name) + '\\r?$', 'm').test(block.text)), 'Expected GameObject ' + name);
}
function requireCenteredRect(block, label) {
  for (const key of ['m_AnchorMin', 'm_AnchorMax', 'm_Pivot']) {
    const v = vector(block, key);
    requireThat(near(v.x, .5) && near(v.y, .5), label + ': custom anchors/pivot require explicit handling');
  }
  const scale = vector(block, 'm_LocalScale');
  const rotation = vector(block, 'm_LocalRotation');
  requireThat(near(scale.x, 1) && near(scale.y, 1) && near(scale.z, 1), label + ': custom scale requires explicit handling');
  requireThat(near(rotation.x, 0) && near(rotation.y, 0) && near(rotation.z, 0) && near(rotation.w, 1), label + ': custom rotation requires explicit handling');
}
function format(number) {
  requireThat(Number.isFinite(number), 'Non-finite geometry');
  return String(Math.round(number * 100000) / 100000);
}
function setVector(block, key, next, edits, objectName) {
  const old = property(block, key);
  // Replace only requested vector axes, retaining the line's existing formatting.
  let updated = old;
  for (const [axis, value] of Object.entries(next)) {
    requireThat(['x', 'y'].includes(axis), 'Unsupported vector axis');
    updated = updated.replace(new RegExp('(' + axis + ': )[-+\\deE.]+'), (_, prefix) => prefix + format(value));
  }
  if (old === updated) return;
  block.text = block.text.replace(new RegExp('(^  ' + escape(key) + ': )[^\\r\\n]*', 'm'), (_, prefix) => prefix + updated);
  edits.push({object: objectName, fileId: block.id, property: key, before: old, after: updated});
}
function uvRect(rawImage) {
  const value = single([...rawImage.text.matchAll(/^  m_UVRect:\r?\n    serializedVersion: \d+\r?\n    x: ([-+\deE.]+)\r?\n    y: ([-+\deE.]+)\r?\n    width: ([-+\deE.]+)\r?\n    height: ([-+\deE.]+)/gm)], 'Expected background UV rectangle');
  const result = {x: +value[1], y: +value[2], width: +value[3], height: +value[4]};
  requireThat(result.width > 0 && result.height > 0, 'Invalid UV extent');
  return result;
}
function prepare(reference, layout) {
  const target = path.join(sceneRoot, reference);
  const original = fs.readFileSync(target);
  const text = original.toString('utf8');
  const meta = fs.readFileSync(target + '.meta');
  const blocks = [...text.matchAll(blockPattern)].map(match => ({type: +match[1], id: match[2], original: match[0], text: match[0]}));
  const byId = new Map(blocks.map(block => [block.id, block]));
  requireThat(blocks.length === byId.size && blocks.length > 0, reference + ': duplicate or missing YAML blocks');
  const portIndex = +single([...text.matchAll(/^  editorPreviewPort: (\d+)\r?$/gm)], 'Expected preview port')[1];
  const portName = ports[portIndex];
  const port = single(layout.ports.filter(item => item.name === portName), 'Missing layout for ' + portName);
  const bgObject = named(blocks, 'Tavern Background');
  const bg = component(blocks, bgObject, 224);
  const rawImage = component(blocks, bgObject, 114, '  m_UVRect:');
  const stage = component(blocks, named(blocks, 'Tavern Crew Stage'), 224);
  requireCenteredRect(bg, 'Tavern Background'); requireCenteredRect(stage, 'Tavern Crew Stage');
  requireThat(property(bg, 'm_Father') === property(stage, 'm_Father'), 'Background/stage must share their existing content parent');
  const bgSize = vector(bg, 'm_SizeDelta'), bgPosition = vector(bg, 'm_AnchoredPosition');
  const stagePosition = vector(stage, 'm_AnchoredPosition'), uv = uvRect(rawImage);
  const pointInStage = (x, y) => ({
    x: bgPosition.x + ((x / layout.sourceWidth - uv.x) / uv.width - .5) * bgSize.x - stagePosition.x,
    y: bgPosition.y + ((1 - y / layout.sourceHeight - uv.y) / uv.height - .5) * bgSize.y - stagePosition.y,
  });
  const edits = [], geometry = [];
  for (let index = 0; index < 6; index++) {
    const [x, y, width, height] = port.bounds[index];
    const crewName = 'Tavern Crew ' + index;
    const crew = component(blocks, named(blocks, crewName), 224);
    requireCenteredRect(crew, crewName);
    requireThat(property(crew, 'm_Father') === '{fileID: ' + stage.id + '}', crewName + ': unexpected hierarchy');
    const size = {x: width / layout.sourceWidth / uv.width * bgSize.x, y: height / layout.sourceHeight / uv.height * bgSize.y};
    const position = pointInStage(x + width / 2, y + height / 2);
    const nameCenter = pointInStage(...port.nameCenters[index]);
    const nameOffset = {x: nameCenter.x - position.x, y: nameCenter.y - position.y};
    setVector(crew, 'm_AnchoredPosition', position, edits, crewName);
    setVector(crew, 'm_SizeDelta', size, edits, crewName);
    for (const [labelName, offset] of [['Tavern Crew Name ' + index, 0], ['Tavern Crew Status ' + index, 28]]) {
      const label = component(blocks, named(blocks, labelName), 224);
      requireCenteredRect(label, labelName);
      requireThat(property(label, 'm_Father') === '{fileID: ' + crew.id + '}', labelName + ': unexpected hierarchy');
      setVector(label, 'm_AnchoredPosition', {x: nameOffset.x, y: nameOffset.y + offset}, edits, labelName);
    }
    geometry.push({index, sourcePixels: port.bounds[index], localPosition: position, size,
      nameSourcePixels: port.nameCenters[index], nameLocalPosition: nameOffset,
      statusLocalPosition: {x: nameOffset.x, y: nameOffset.y + 28}});
  }
  // Protect all untargeted bytes including every component, callback and disabled backing.
  for (const block of blocks) {
    const allowed = edits.filter(edit => edit.fileId === block.id).map(edit => edit.property);
    let oldMasked = block.original, newMasked = block.text;
    for (const key of allowed) {
      const pattern = new RegExp('(^  ' + escape(key) + ': )[^\\r\\n]*', 'gm');
      oldMasked = oldMasked.replace(pattern, '$1<allowed>'); newMasked = newMasked.replace(pattern, '$1<allowed>');
    }
    requireThat(oldMasked === newMasked, reference + ': unexpected bytes changed in block ' + block.id);
  }
  const after = Buffer.from(text.replace(blockPattern, match => byId.get(/^--- !u!\d+ &(-?\d+)/.exec(match)[1]).text), 'utf8');
  return {reference, target, original, after, meta, beforeSha256: hash(original), afterSha256: hash(after), metaSha256: hash(meta), edits, geometry,
    port: portName, background: {size: bgSize, position: bgPosition, uv}, unchangedBlocks: blocks.filter(block => block.text === block.original).length,
    changedBlocks: blocks.filter(block => block.text !== block.original).length};
}
function main() {
  const args = process.argv.slice(2); const apply = args.includes('--apply');
  requireThat(args.every(arg => arg === '--apply'), 'Usage: node sync-saved-previews.cjs [--apply]');
  const layoutFile = path.join(artifactRoot, 'layout-v2.json'); const layoutBytes = fs.readFileSync(layoutFile);
  const layout = JSON.parse(layoutBytes.toString('utf8').replace(/^\uFEFF/, ''));
  requireThat(layout.sourceWidth === 1672 && layout.sourceHeight === 941, 'Expected layout reference 1672 x 941');
  requireThat(Array.isArray(layout.ports) && layout.ports.length === 6, 'Expected six regional layout records');
  for (const name of ports) {
    const port = single(layout.ports.filter(item => item.name === name), 'Expected port ' + name);
    requireThat(Array.isArray(port.bounds) && port.bounds.length === 6, name + ': expected six profession bounds');
    requireThat(Array.isArray(port.nameCenters) && port.nameCenters.length === 6, name + ': expected six profession name centers');
    for (const bounds of port.bounds) {
      requireThat(Array.isArray(bounds) && bounds.length === 4 && bounds.every(Number.isFinite), name + ': malformed bounds');
      const [x, y, w, h] = bounds;
      requireThat(x >= 0 && y >= 0 && w > 0 && h > 0 && x + w <= layout.sourceWidth && y + h <= layout.sourceHeight, name + ': out-of-image bounds');
    }
    for (const point of port.nameCenters) {
      requireThat(Array.isArray(point) && point.length === 2 && point.every(Number.isFinite), name + ': malformed name center');
      requireThat(point[0] >= 0 && point[0] <= layout.sourceWidth && point[1] >= 0 && point[1] <= layout.sourceHeight, name + ': out-of-image name center');
    }
  }
  const patches = references.map(reference => prepare(reference, layout));
  const checkUnchanged = () => {
    requireThat(hash(fs.readFileSync(layoutFile)) === hash(layoutBytes), 'Layout changed during planning; rerun');
    for (const patch of patches) {
      requireThat(hash(fs.readFileSync(patch.target)) === patch.beforeSha256, 'Concurrent scene edit: ' + patch.reference);
      requireThat(hash(fs.readFileSync(patch.target + '.meta')) === patch.metaSha256, 'Concurrent meta edit: ' + patch.reference);
    }
  };
  checkUnchanged();
  const report = {mode: apply ? 'apply' : 'dry-run', utc: new Date().toISOString(), layoutSha256: hash(layoutBytes),
    scope: 'Only 18 crew button RectTransform positions/sizes and 36 name/status XY positions across three saved gray-gull roster scenes; names map from authored source centers, statuses remain 28 UI units above names; all other YAML bytes retained.',
    scenes: patches.map(({reference, beforeSha256, afterSha256, metaSha256, edits, geometry, port, background, unchangedBlocks, changedBlocks}) =>
      ({reference, beforeSha256, afterSha256, metaSha256, edits, geometry, port, background, unchangedBlocks, changedBlocks}))};
  const reportDirectory = path.join(artifactRoot, 'validation', 'saved-preview-sync');
  fs.mkdirSync(reportDirectory, {recursive: true});
  const reportFile = path.join(reportDirectory, report.mode + '-' + new Date().toISOString().replace(/[:.]/g, '-') + '.json');
  if (apply) {
    for (const patch of patches) {
      const backup = path.join(artifactRoot, 'originals', 'scenes', patch.reference);
      fs.mkdirSync(path.dirname(backup), {recursive: true});
      if (!fs.existsSync(backup)) fs.writeFileSync(backup, patch.original, {flag: 'wx'});
      if (!fs.existsSync(backup + '.meta')) fs.writeFileSync(backup + '.meta', patch.meta, {flag: 'wx'});
      requireThat(hash(fs.readFileSync(backup + '.meta')) === patch.metaSha256, 'Original meta backup differs: ' + patch.reference);
    }
    checkUnchanged();
    for (const patch of patches) {
      requireThat(hash(fs.readFileSync(patch.target)) === patch.beforeSha256, 'Concurrent scene edit before write: ' + patch.reference);
      if (patch.beforeSha256 !== patch.afterSha256) fs.writeFileSync(patch.target, patch.after);
      requireThat(hash(fs.readFileSync(patch.target)) === patch.afterSha256, 'Written scene verification failed: ' + patch.reference);
      requireThat(hash(fs.readFileSync(patch.target + '.meta')) === patch.metaSha256, 'Meta changed during write: ' + patch.reference);
    }
  }
  fs.writeFileSync(reportFile, JSON.stringify(report, null, 2) + '\n', {flag: 'wx'});
  console.log(JSON.stringify({mode: report.mode, scenes: patches.length, fieldEdits: patches.reduce((sum, patch) => sum + patch.edits.length, 0), report: reportFile}));
}
if (require.main === module) main();
