#!/usr/bin/env node
'use strict';

// Changes only three serialized values per saved departure preview. No YAML
// serializer is used: latin1 round-trips all source bytes without normalization.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '../..');
const sceneRoot = path.join(root, 'UnityClient/Assets/_Project/Scenes');
const taskRoot = path.join(root, 'Artifacts/DepartureArtRedraw20261012');
const dryPath = path.join(taskRoot, 'saved-previews-dry-run.json');
const reportPath = path.join(taskRoot, 'saved-previews-report.json');
const textureMeta = path.join(root, 'UnityClient/Assets/_Project/Resources/PortFacilities/Regional/Voyage/voyage_broken_anchor_v1.png.meta');
const genericGuid = 'a1f67c9e46d94c56b60208e5d8e8e00b';
const apply = process.argv.includes('--apply');
const sha = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const relative = file => path.relative(root, file).split(path.sep).join('/');
const assert = (ok, message) => { if (!ok) throw new Error(message); };
const exactlyOne = (values, label) => {
  assert(values.length === 1, `${label}: expected one, got ${values.length}`);
  return values[0];
};

function walk(directory) {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
    const file = path.join(directory, entry.name);
    return entry.isDirectory() ? walk(file) : [file];
  });
}

function documents(source) {
  const headers = [...source.matchAll(/^--- !u!(\d+) &(-?\d+)[^\r\n]*\r?\n/gm)];
  return headers.map((header, index) => ({
    type: header[1], id: header[2], start: header.index,
    body: source.slice(header.index, headers[index + 1]?.index ?? source.length),
  }));
}

function component(docs, name, className) {
  const go = exactlyOne(docs.filter(doc => doc.type === '1' &&
    doc.body.split(/\r?\n/).includes(`  m_Name: ${name}`)), `${name} GameObject`);
  const ids = [...go.body.matchAll(/^  - component: \{fileID: (-?\d+)\}\r?$/gm)].map(match => match[1]);
  const node = exactlyOne(docs.filter(doc => ids.includes(doc.id) && doc.type === '114' &&
    doc.body.split(/\r?\n/).includes(`  m_EditorClassIdentifier: ${className}`)), `${name} ${className}`);
  assert(node.body.split(/\r?\n/).includes(`  m_GameObject: {fileID: ${go.id}}`), `${name}: reverse GameObject link mismatch`);
  return { go, node };
}

function replaceNodeLine(edits, node, expression, expected, replacement, label) {
  const match = exactlyOne([...node.body.matchAll(expression)], label);
  assert(match[0] === expected, `${label}: unexpected value ${JSON.stringify(match[0])}`);
  edits.push({ start: node.start + match.index, old: match[0], replacement, label, nodeFileId: node.id });
}

function plan(file, newGuid) {
  const original = fs.readFileSync(file);
  const source = original.toString('latin1');
  const docs = documents(source);
  const controller = exactlyOne(docs.filter(doc => doc.body.includes('m_EditorClassIdentifier: ShatteredSail.Presentation::ShatteredSail.Presentation.CoreGameplayDemoController')), `${relative(file)} controller`);
  const port = exactlyOne([...controller.body.matchAll(/^  editorPreviewPort: (\d+)\r?$/gm)], 'editorPreviewPort')[1];
  assert(port === '0', `${relative(file)}: unsupported editorPreviewPort=${port}`);
  const background = component(docs, 'Voyage Background', 'UnityEngine.UI::UnityEngine.UI.RawImage');
  const tint = component(docs, 'Facility Backdrop Tint', 'UnityEngine.UI::UnityEngine.UI.Image');
  const edits = [];
  replaceNodeLine(edits, background.node, /^  m_Texture: [^\r\n]+/gm,
    `  m_Texture: {fileID: 2800000, guid: ${genericGuid}, type: 3}`,
    `  m_Texture: {fileID: 2800000, guid: ${newGuid}, type: 3}`, 'Voyage Background texture GUID');
  replaceNodeLine(edits, background.node, /^  m_Color: [^\r\n]+/gm,
    '  m_Color: {r: 0.9, g: 0.92, b: 0.94, a: 1}',
    '  m_Color: {r: 1, g: 1, b: 1, a: 1}', 'Voyage Background white color');
  replaceNodeLine(edits, tint.node, /^  m_Color: [^\r\n]+/gm,
    '  m_Color: {r: 0.005, g: 0.025, b: 0.035, a: 0.13}',
    '  m_Color: {r: 0.005, g: 0.025, b: 0.035, a: 0}', 'Facility Backdrop Tint alpha');
  let updated = source;
  for (const edit of [...edits].sort((a, b) => b.start - a.start)) {
    assert(updated.slice(edit.start, edit.start + edit.old.length) === edit.old, 'Edit offset mismatch');
    updated = updated.slice(0, edit.start) + edit.replacement + updated.slice(edit.start + edit.old.length);
  }
  const output = Buffer.from(updated, 'latin1');
  // Rebuild from untouched byte spans to verify no incidental YAML edits.
  const spans = [];
  let cursor = 0;
  for (const edit of [...edits].sort((a, b) => a.start - b.start)) {
    spans.push(original.subarray(cursor, edit.start), Buffer.from(edit.replacement, 'latin1'));
    cursor = edit.start + Buffer.byteLength(edit.old, 'latin1');
  }
  spans.push(original.subarray(cursor));
  assert(output.equals(Buffer.concat(spans)), 'Non-target byte changes detected');
  const metaFile = `${file}.meta`;
  assert(fs.existsSync(metaFile), `${relative(file)} missing scene meta`);
  return {
    file, original, output,
    record: {
      path: relative(file), editorPreviewPort: Number(port),
      originalSha256: sha(original), updatedSha256: sha(output),
      originalBytes: original.length, updatedBytes: output.length,
      sceneMetaSha256: sha(fs.readFileSync(metaFile)),
      nodes: {
        controller: controller.id,
        background: { gameObject: background.go.id, rawImage: background.node.id },
        backdropTint: { gameObject: tint.go.id, image: tint.node.id },
      },
      edits,
      backupPath: `Artifacts/DepartureArtRedraw20261012/originals/${relative(file)}`,
    },
  };
}

function main() {
  fs.mkdirSync(taskRoot, { recursive: true });
  const textureBytes = fs.readFileSync(textureMeta);
  const newGuid = exactlyOne([...textureBytes.toString('utf8').matchAll(/^guid: ([a-f0-9]{32})\r?$/gm)], 'target texture GUID')[1];
  const files = walk(sceneRoot).filter(file => file.endsWith('.unity') &&
    /^  m_Name: Voyage Background\r?$/m.test(fs.readFileSync(file).toString('latin1'))).sort();
  assert(files.length === 14, `Expected 14 departure previews, found ${files.length}`);
  const plans = files.map(file => plan(file, newGuid));
  const report = {
    mode: apply ? 'apply' : 'dry-run', status: apply ? 'prepared' : 'validated',
    createdAt: new Date().toISOString(), sourceGuid: genericGuid, targetGuid: newGuid,
    textureMetaPath: relative(textureMeta), textureMetaSha256: sha(textureBytes),
    sceneCount: plans.length, changeCount: plans.reduce((sum, item) => sum + item.record.edits.length, 0),
    preservation: 'Only targeted ASCII values change; other scene bytes and all meta files remain unchanged.',
    scenes: plans.map(item => item.record),
  };
  if (!apply) {
    fs.writeFileSync(dryPath, JSON.stringify(report, null, 2) + '\n');
    console.log(JSON.stringify({ status: report.status, scenes: report.sceneCount, edits: report.changeCount, targetGuid: newGuid, report: relative(dryPath), paths: report.scenes.map(scene => scene.path) }, null, 2));
    return;
  }
  const approvedPlan = JSON.parse(fs.readFileSync(dryPath, 'utf8'));
  assert(approvedPlan.status === 'validated' && approvedPlan.sceneCount === 14, 'A valid dry-run is required');
  assert(approvedPlan.textureMetaSha256 === report.textureMetaSha256, 'Texture meta changed since dry-run');
  assert(JSON.stringify(approvedPlan.scenes) === JSON.stringify(report.scenes), 'Scene plan changed since dry-run');
  const verifySources = () => {
    assert(sha(fs.readFileSync(textureMeta)) === report.textureMetaSha256, 'Texture meta concurrent modification');
    for (const item of plans) {
      assert(sha(fs.readFileSync(item.file)) === item.record.originalSha256, `${item.record.path}: concurrent scene modification`);
      assert(sha(fs.readFileSync(`${item.file}.meta`)) === item.record.sceneMetaSha256, `${item.record.path}: concurrent meta modification`);
    }
  };
  verifySources();
  // Create all backups before touching any scene. Existing backups are never overwritten.
  for (const item of plans) {
    const backup = path.join(root, item.record.backupPath);
    fs.mkdirSync(path.dirname(backup), { recursive: true });
    if (fs.existsSync(backup)) {
      assert(sha(fs.readFileSync(backup)) === item.record.originalSha256, `${item.record.path}: existing backup differs; refusing overwrite`);
    } else {
      fs.writeFileSync(backup, item.original, { flag: 'wx' });
    }
    assert(sha(fs.readFileSync(backup)) === item.record.originalSha256, `${item.record.path}: backup verification failed`);
  }
  verifySources();
  report.status = 'applying';
  fs.writeFileSync(reportPath, JSON.stringify(report, null, 2) + '\n');
  for (const item of plans) {
    assert(sha(fs.readFileSync(item.file)) === item.record.originalSha256, `${item.record.path}: changed immediately before write`);
    fs.writeFileSync(item.file, item.output);
    item.record.verifiedSha256 = sha(fs.readFileSync(item.file));
    assert(item.record.verifiedSha256 === item.record.updatedSha256, `${item.record.path}: written hash mismatch`);
    item.record.nonTargetBytesPreserved = true;
    fs.writeFileSync(reportPath, JSON.stringify(report, null, 2) + '\n');
  }
  assert(sha(fs.readFileSync(textureMeta)) === report.textureMetaSha256, 'Texture meta changed during write');
  for (const item of plans) {
    assert(sha(fs.readFileSync(item.file)) === item.record.updatedSha256, `${item.record.path}: post-write concurrent modification`);
    assert(sha(fs.readFileSync(`${item.file}.meta`)) === item.record.sceneMetaSha256, `${item.record.path}: scene meta changed`);
  }
  report.status = 'applied-and-verified';
  report.completedAt = new Date().toISOString();
  fs.writeFileSync(reportPath, JSON.stringify(report, null, 2) + '\n');
  console.log(JSON.stringify({ status: report.status, scenes: report.sceneCount, edits: report.changeCount, report: relative(reportPath), backups: 'Artifacts/DepartureArtRedraw20261012/originals/UnityClient/Assets/_Project/Scenes' }, null, 2));
}

fs.mkdirSync(taskRoot, { recursive: true });
const lockPath = path.join(taskRoot, 'saved-preview-sync.lock');
let lock;
try {
  lock = fs.openSync(lockPath, 'wx');
  fs.writeFileSync(lock, `pid=${process.pid}\nmode=${apply ? 'apply' : 'dry-run'}\n`);
  main();
} catch (error) {
  console.error(error.stack);
  process.exitCode = 1;
} finally {
  if (lock !== undefined) { fs.closeSync(lock); fs.unlinkSync(lockPath); }
}
