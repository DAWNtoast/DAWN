'use strict';

// Refresh the fixed battle inventory from its reviewed JSON inputs. No Unity
// process, source rescan, dependency installation, or temporary project is used.
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const { spawnSync } = require('child_process');
const assert = require('assert/strict');

const root = path.resolve(__dirname, '..', '..');
const out = path.join(root, 'outputs', 'battle-icon-tooltips-20261026');
if (fs.existsSync(path.join(out, 'status-revision.json'))) {
  const result = spawnSync('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe',
    [path.join(root, 'tools/BattleStatusRevision20261027/build_report.py'), '--publish'],
    {cwd:root, encoding:'utf8', windowsHide:true, env:{...process.env,PYTHONIOENCODING:'utf-8'}});
  if(result.error) throw result.error;
  if(result.status!==0) throw new Error(result.stderr || 'Status revision refresh failed.');
  process.stdout.write(result.stdout);
  process.exit(0);
}
// Preserve the newer reachability audit when only the shared icon art changes.
if (fs.existsSync(path.join(out, 'reachability-summary.json'))) {
  const result = spawnSync('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe',
    [path.join(root, 'tools/BattleIconReachability20261026/build_report.py'), '--refresh-current-atlas'],
    { cwd: root, encoding: 'utf8', windowsHide: true, env: { ...process.env, PYTHONIOENCODING: 'utf-8' } });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(result.stderr || 'Reachability report refresh failed.');
  process.stdout.write(result.stdout);
  process.exit(0);
}
const jsonPath = path.join(out, 'all-icons.json');
const markdownPath = path.join(out, '战斗图标与悬停提示.md');
const htmlPath = path.join(out, '战斗图标与悬停提示.html');
const suppressedIds = new Set([
  'resource_energy', 'resource_action_points', 'resource_mana', 'resource_powder',
  'resource_cannonball', 'resource_planks', 'resource_medicine'
]);
const updateNote = '本次局部更新：2026-10-26。能量、行动点、法力、火药、炮弹、木板、药品这七项资源图标继续不显示悬停提示；火药、炮弹、木板、药品四项基础图形只保留内部物品本体，去除圆环、徽章和外边框。其他状态、意图与关键词沿用原清单的核对结果，未重新扫描或核对当前主工程的全部状态。原始源码快照与核对记录保留。';
const readJson = file => JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, ''));
const digest = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const canonical = value => JSON.stringify(value, Object.keys(value).sort());
const isUpdatedResource = entry => entry.origin === 'controls.json' && suppressedIds.has(entry.id);

function evidenceHashes() {
  const files = [path.join(out, 'source-manifest.json')];
  const walk = folder => {
    for (const item of fs.readdirSync(folder, { withFileTypes: true })) {
      const file = path.join(folder, item.name);
      if (item.isSymbolicLink()) throw new Error('Unexpected link in source evidence: ' + file);
      if (item.isDirectory()) walk(file);
      else if (item.isFile()) files.push(file);
    }
  };
  walk(path.join(out, 'source-snapshot'));
  return files.sort().map(file => ({ file, sha256: digest(file) }));
}

function verifyEntries(entries) {
  assert.equal(entries.length, 153, 'Keep every record from the complete inventory.');
  assert.equal(new Set(entries.map(entry => entry.category)).size, 18);
  assert.equal(entries.filter(entry => entry.hasTooltip === false).length, 29);
  for (const id of suppressedIds) {
    const matches = entries.filter(entry => entry.origin === 'controls.json' && entry.id === id);
    assert.equal(matches.length, 1, 'Expected exactly one updated resource: ' + id);
    assert.equal(matches[0].hasTooltip, false, id);
    assert.equal(matches[0].tooltip, '无悬停提示。', id);
  }
  for (const id of ['resource_chain', 'resource_tide', 'resource_curse'])
    assert.equal(entries.find(entry => entry.origin === 'controls.json' && entry.id === id)?.hasTooltip, true, id);
}

const before = readJson(jsonPath);
const originalEvidence = evidenceHashes();
const result = spawnSync(process.execPath, [path.join(out, 'build-report.cjs'), '--resource-icons-update'],
  { cwd: root, encoding: 'utf8', windowsHide: true });
if (result.error) throw result.error;
if (result.status !== 0) throw new Error(result.stderr || result.stdout || 'HTML builder failed.');
const entries = readJson(jsonPath);
verifyEntries(entries);
assert.deepEqual(entries.filter(entry => !isUpdatedResource(entry)).map(canonical),
  before.filter(entry => !isUpdatedResource(entry)).map(canonical), 'Unrelated inventory records must stay unchanged.');
assert.deepEqual(evidenceHashes(), originalEvidence, 'Preserve the original source evidence.');

const categories = [...new Set(entries.map(entry => entry.category))];
const keywordCount = entries.filter(entry => entry.origin === 'keywords.json').length;
const fullTooltip = entry => !entry.hasTooltip || entry.origin === 'other.json' || entry.origin === 'keywords.json'
  ? entry.tooltip : entry.name + '\n' + entry.tooltip;
const text = value => String(value ?? '').replace(/\r\n?/g, '\n');
const cell = value => text(value).replace(/\|/g, '\\|').replace(/\n/g, '<br>');
const lines = [
  '# 战斗期间的图标与鼠标悬停提示', '',
  '原始核对日期：2026-10-26。此文件是已交付图文清单的 Markdown 版本，覆盖准备阶段、海战与接舷战。', '',
  updateNote, '',
  `共 **${entries.length - keywordCount} 条图标／用途记录**，另附 **${keywordCount} 条整卡悬停关键词**，合计 **${entries.length} 条**。同一图形可能对应多种效果，本清单按显示名称及用途列出，并非 ${entries.length - keywordCount} 张不同图案。`, '',
  '- 提示中的 `{花括号}` 表示随战况变化的数值、文本或条件分支。',
  '- 有独立提示的项目列出标题与正文；无独立提示的项目明确标注。',
  '- 状态图通常由基础图形加来源角标、层数构成，角标见各项备注。',
  '- 状态栏在规划移动、选择朝向或船只动画期间可能暂时隐藏。',
  '- 整卡关键词是悬停整张卡时显示的说明，不是独立图标提示。',
  '- 原清单由源码静态核对，并未在游戏中逐项触发全部状态；除本次资源更新外，源码定位对应原始核对版本。', '',
  `[查看配套图文版](<${htmlPath.replace(/\\/g, '/')}>)`, '',
  '## 分类总览', '', '| 类别 | 条目数 |', '| --- | ---: |',
  ...categories.map(category => `| ${cell(category)} | ${entries.filter(entry => entry.category === category).length} |`), ''
];
let number = 0;
for (const category of categories) {
  lines.push('## ' + category, '');
  for (const entry of entries.filter(entry => entry.category === category)) {
    const status = entry.origin === 'keywords.json' ? '整卡悬停提示（非独立图标）'
      : entry.hasTooltip ? '有独立悬停提示' : '无独立悬停提示';
    lines.push(`### ${++number}. ${entry.name}`, '', `**提示状态：** ${status}`, '',
      `**图标标识：** \`${entry.id}\`${entry.assetId ? `；基础图形：\`${entry.assetId}\`` : ''}`, '',
      '**出现条件：** ' + text(entry.condition), '', '**鼠标悬停文字：**', '',
      ...text(fullTooltip(entry)).split('\n').map(line => '> ' + line), '');
    if (entry.note) lines.push('**备注：** ' + text(entry.note), '');
    const source = text(entry.source).replace(/\\/g, '/');
    const location = source + (entry.line ? ':' + entry.line : '');
    lines.push(`**源码：** [${path.posix.basename(source)}${entry.line ? ':' + entry.line : ''}](<${location}>)`, '');
  }
}
fs.writeFileSync(markdownPath, lines.join('\n'), 'utf8');

// Read back both fixed outputs, retaining all original fields and explanations.
const markdown = fs.readFileSync(markdownPath, 'utf8');
const html = fs.readFileSync(htmlPath, 'utf8');
assert.equal((markdown.match(/^### \d+\. /gm) || []).length, 153);
assert.equal((html.match(/<tr data-category=/g) || []).length, 153);
assert.equal((html.match(/data-tooltip="no"/g) || []).length, 29);
assert.ok(markdown.includes(updateNote) && html.includes(updateNote));
for (const entry of entries) {
  const quoted = text(fullTooltip(entry)).split('\n').map(line => '> ' + line).join('\n');
  assert.ok(markdown.includes(quoted), 'Missing tooltip text: ' + entry.id);
  if (entry.note) assert.ok(markdown.includes('**备注：** ' + text(entry.note)), 'Missing note: ' + entry.id);
  assert.ok(markdown.includes('**出现条件：** ' + text(entry.condition)), 'Missing condition: ' + entry.id);
}
const atlas = path.join(root, 'UnityClient', 'Assets', '_Project', 'Resources', 'BattleIcons', 'battle_icons_v1.png');
assert.ok(html.includes('data:image/png;base64,' + fs.readFileSync(atlas).toString('base64')),
  'The HTML must embed the installed battle icon atlas.');
verifyEntries(readJson(jsonPath));
assert.deepEqual(evidenceHashes(), originalEvidence);
console.log(JSON.stringify({ records: entries.length, categories: categories.length, noTooltip: 29,
  updatedResources: [...suppressedIds], otherRecordsUnchanged: true, originalEvidencePreserved: true,
  atlasSha256: digest(atlas), html: htmlPath, markdown: markdownPath }, null, 2));
