[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SourcePath,
    [Parameter(Mandatory = $true)][string]$ReviewPath,
    [Parameter(Mandatory = $true)][string]$OutputPath
)
$ErrorActionPreference = 'Stop'
$source = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $SourcePath).Path)
$review = Get-Content -Raw -LiteralPath $ReviewPath | ConvertFrom-Json
if ($source -notmatch 'const PHASES = ' -or $source -notmatch 'let state = ') {
    throw 'Expected the exported UI refactor plan, with its original task IDs.'
}
if ($source.Contains('const LOCAL_REVIEW =')) { throw 'Use the preserved original plan as SourcePath.' }

# Keep verified evidence in the file and manual notes in browser storage.
# Never reconnect the exported page to its former remote progress database.
$source = [regex]::Replace($source, '(?s)\(async \(\) => \{\s*if \(!window\.claude.*?\}\)\(\);', '// Verified local progress is loaded below.')
$source = [regex]::Replace($source, '(?s)async function toggle\(id, done\) \{.*?\r?\n\}\r?\n\r?\npaint\(\);', "async function toggle(id, done) {} // Local handler is installed below.`npaint();")
$source = $source.Replace('正在连接进度记录…', '正在加载本地记录…')
$source = $source.Replace('重构前基线数据', '历史基线与本轮验收')
$source = $source.Replace('之后每一页的改后截图和测试结果都与这里比较。', '下表保留原计划的历史基线；本轮测试结果及截图来源见验收说明，不能把历史截图视为本轮精确源码基线。')
$source = $source.Replace('每一项勾选后，所有打开此页的人都会看到同一份进度。', '本页按验证结果勾选并记录备注；本轮完成阶段 1 后停止，阶段 2 留待逐页实施。')
$source = $source.Replace('需要你拍板的事', '已经确认的方向')
$source = $source.Replace('勾选表示已经定下来；具体结论写在对应会话里。', '按本次确认的新版方向执行；备注说明依据与验证范围。')
$source = $source.Replace('推荐：保留木质、羊皮纸、像素风，只改排版和交互', '已确认：保留木质、羊皮纸、像素风，优化排版与通用组件')
$source = $source.Replace('结算精度不变，显示改为整数或 2 位小数？', '已确认：统一显示两位小数，结算精度不变（阶段 2 市场页实施）')
$source = $source.Replace('海图扩大为主区域（约 60%），前方视野改为可折叠氛围条？', '已确认：海图放大；前方视野保持常驻、不折叠（阶段 2 航海页实施）')
$source = $source.Replace('ESC 返回、数字键出牌、空格结束回合；手柄不在本版范围', '已确认：本轮不制作键盘快捷键')
$source = $source.Replace('推荐：阶段 0 → 阶段 1 → 顶栏 → 海战', '已确认：阶段 0 → 阶段 1，完成后停止；阶段 2 以后按界面顺序逐页实施')
$source = $source.Replace('ESC 返回或关闭、悬停提示、数字键出牌、空格结束回合、统一确认弹窗', '统一悬停提示、确认弹窗与 Toast 生命周期（本轮不做键盘快捷键）')
$source = $source.Replace('每页拆成 View，状态变化只刷新受影响控件，不再整页 ClearChildren', '建立公共 View 框架，接入海战与顶栏局部刷新；其他页面随阶段 2 逐页迁移')
$source = $source.Replace('海图约 60% 为主区域；前方视野改为可折叠氛围条', '海图放大为主区域；前方视野保持常驻、不折叠')
$source = $source.Replace('分组列表，按每单位标价，跨港参考价与利润提示', '分组列表，按每单位标价并统一显示两位小数（结算精度不变），跨港参考价与利润提示')
# Encode script delimiters so review notes are always data, never executable HTML.
$json = ($review | ConvertTo-Json -Depth 12 -Compress).Replace('<', '\u003c').Replace('>', '\u003e').Replace('&', '\u0026')
$script = @'
<script>
const LOCAL_REVIEW = __REVIEW_JSON__;
const LOCAL_STORAGE_KEY = "seventh-harbor-ui-refactor-cb8c1d44-manual-v1";
const validNoteIds = new Set([...allIds, ...PHASES.find(p => p.id === "p2").screens.map(s => s[0])]);
const noteEditors = {};
let manual = { checks: {}, notes: {} };
let storageNotice = "";
function normalizeManual(value) {
  const clean = { checks: {}, notes: {} };
  if (!value || typeof value !== "object") return clean;
  for (const [id, row] of Object.entries(value.checks || {})) {
    if (allIds.includes(id) && row && typeof row.done === "boolean")
      clean.checks[id] = { done: row.done, at: typeof row.at === "string" ? row.at : "" };
  }
  for (const [id, note] of Object.entries(value.notes || {}))
    if (validNoteIds.has(id) && typeof note === "string") clean.notes[id] = note.slice(0, 5000);
  return clean;
}
try { manual = normalizeManual(JSON.parse(localStorage.getItem(LOCAL_STORAGE_KEY) || "null")); }
catch (e) { storageNotice = "浏览器未能读取本地记录；可使用导入与导出备份。"; }
function saveManual() {
  try {
    localStorage.setItem(LOCAL_STORAGE_KEY, JSON.stringify(manual));
    storageNotice = "已保存在此浏览器；建议导出备份。";
  } catch (e) { storageNotice = "浏览器不允许本地保存，本次记录仅在当前页面保留；请导出备份。"; }
  setStatus(storageNotice, storageNotice.includes("不允许"));
}
async function renderLocal() {
  state = {};
  for (const id of allIds) {
    const verified = LOCAL_REVIEW.items[id];
    const change = manual.checks[id];
    state[id] = change ? { done: change.done, by: null, at: change.at } :
      { done: verified?.done === true, by: null, at: verified?.done ? LOCAL_REVIEW.updatedAt : "" };
  }
  await paint();
  for (const id of allIds) {
    const meta = document.getElementById("meta-" + id);
    if (!meta) continue;
    const change = manual.checks[id];
    if (change) meta.textContent = "手动记录：" + (change.done ? "已勾选" : "未勾选") + (change.at ? " · " + fmt(change.at) : "");
    else if (LOCAL_REVIEW.items[id]?.done) meta.textContent = "已核验 · " + fmt(LOCAL_REVIEW.updatedAt);
  }
  setDisabled(false);
}
toggle = async function(id, done) {
  if (!allIds.includes(id)) return;
  manual.checks[id] = { done, at: new Date().toISOString() };
  saveManual();
  await renderLocal();
};
for (const [id, record] of Object.entries(LOCAL_REVIEW.items)) {
  if (!allIds.includes(id)) continue;
  if (record.note) {
    const row = document.getElementById("row-" + id);
    const note = document.createElement("div");
    note.className = "review-note";
    note.textContent = "验收备注：" + record.note;
    if (row) row.append(note);
  }
}
for (const [screenId, noteText] of Object.entries(LOCAL_REVIEW.screenNotes || {})) {
  const row = document.getElementById("row-" + screenId);
  if (!row) continue;
  const note = document.createElement("p");
  note.className = "review-note";
  note.textContent = noteText;
  row.append(note);
}
for (const phase of PHASES) {
  const rows = phase.tasks ? phase.tasks.map(t => [t[0], t[1]]) : phase.screens.map(s => [s[0], s[1]]);
  for (const [id, title] of rows) {
    const row = document.getElementById("row-" + id);
    const details = document.createElement("details");
    details.className = "manual-note";
    const summary = document.createElement("summary");
    summary.textContent = "我的备注";
    const editor = document.createElement("textarea");
    editor.rows = 2; editor.maxLength = 5000;
    editor.setAttribute("aria-label", title + "：我的备注");
    editor.placeholder = "填写备注，自动保存在此浏览器";
    editor.value = manual.notes[id] || "";
    editor.addEventListener("input", () => { manual.notes[id] = editor.value; saveManual(); });
    noteEditors[id] = editor;
    details.append(summary, editor); row.append(details);
  }
}
const localTools = document.createElement("div");
localTools.className = "local-tools";
function toolButton(caption, action) {
  const button = document.createElement("button"); button.type = "button";
  button.textContent = caption; button.addEventListener("click", action); localTools.append(button);
}
toolButton("导出勾选与备注", () => {
  const blob = new Blob([JSON.stringify({ format: "seventh-harbor-ui-manual-v1", manual }, null, 2)], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a"); link.href = url; link.download = "第七港口-UI重构-勾选备注.json";
  document.body.append(link); link.click(); link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
});
const importer = document.createElement("input"); importer.type = "file"; importer.accept = ".json,application/json"; importer.hidden = true;
importer.addEventListener("change", async () => {
  const file = importer.files?.[0]; if (!file) return;
  try {
    const data = JSON.parse(await file.text());
    if (data.format !== "seventh-harbor-ui-manual-v1") throw new Error("请选择此页面导出的勾选备注 JSON 文件。");
    const incoming = normalizeManual(data.manual);
    if (!window.confirm("导入文件将替换当前浏览器的手动勾选与备注，是否继续？")) return;
    manual = incoming;
    for (const [id, editor] of Object.entries(noteEditors)) editor.value = manual.notes[id] || "";
    saveManual(); await renderLocal();
  } catch (e) { setStatus("导入失败：" + (e.message || "文件格式错误"), true); }
  finally { importer.value = ""; }
});
toolButton("导入备份", () => importer.click());
toolButton("恢复核验记录", async () => {
  if (!window.confirm("清除本浏览器的手动勾选与备注，恢复文件中的核验记录？建议先导出备份。")) return;
  manual = { checks: {}, notes: {} };
  for (const editor of Object.values(noteEditors)) editor.value = "";
  saveManual(); await renderLocal();
});
localTools.append(importer);
document.querySelector(".toolbar").after(localTools);
renderLocal();
setStatus(storageNotice || "本地可操作 · 勾选和备注保存在此浏览器，手动勾选不等于已核验", !!storageNotice);
document.getElementById("testFact").textContent = LOCAL_REVIEW.testSummary;
document.getElementById("testFactNote").textContent = LOCAL_REVIEW.testNote;
const reviewBox = document.createElement("section");
reviewBox.className = "review-summary";
const heading = document.createElement("h2");
heading.textContent = "本轮验收说明";
const summary = document.createElement("p");
summary.textContent = LOCAL_REVIEW.summary;
reviewBox.append(heading, summary);
for (const link of LOCAL_REVIEW.links || []) {
  if (!/^file:\/\//i.test(link.url)) continue;
  const paragraph = document.createElement("p");
  const anchor = document.createElement("a");
  anchor.href = link.url;
  anchor.textContent = link.title;
  paragraph.append(anchor);
  reviewBox.append(paragraph);
}
document.querySelector(".toolbar").after(reviewBox);
</script>
<style>
.review-note { margin: 0 4px 12px 38px; font-size: 13px; line-height: 1.7; color: var(--sea); white-space: pre-line; }
.screen .review-note { margin: 4px 0; }
.review-summary { margin-top: 18px; padding: 18px; border: 1px solid var(--sea); background: var(--sheet); }
.review-summary p { margin-bottom: 0; white-space: pre-line; }
.local-tools { display:flex; flex-wrap:wrap; gap:8px; margin:12px 0; }
.local-tools button { padding:7px 12px; border:1px solid var(--rule); border-radius:4px; background:var(--sheet); color:var(--ink); cursor:pointer; }
.manual-note { margin:0 4px 12px 38px; color:var(--muted); font-size:13px; }
.manual-note summary { cursor:pointer; }
.manual-note textarea { display:block; margin-top:8px; width:100%; resize:vertical; padding:8px; border:1px solid var(--rule); background:var(--sheet); color:var(--ink); font:inherit; }
</style>
'@
$script = $script.Replace('__REVIEW_JSON__', $json)
$source = $source.Replace('</body>', $script + "`n</body>")
[IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), $source, [Text.UTF8Encoding]::new($false))
Write-Output "Prepared local review page: $OutputPath"
