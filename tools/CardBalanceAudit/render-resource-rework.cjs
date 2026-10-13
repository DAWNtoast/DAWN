'use strict';
// Build a self-contained review from the real Core export and a single frozen benchmark.
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../..');
const coreDir = path.resolve(process.argv[2]);
const battleDir = path.resolve(process.argv[3]);
const out = path.resolve(process.argv[5] || path.join(root, 'Artifacts/CardSchoolRebalance20261012'));
fs.mkdirSync(out, { recursive: true });
const names = { ordinary: '普通', fire: '火焰', arcane: '秘术', curse: '诅咒', tide: '潮汐', thunder: '雷霆', blood: '血契' };
function csv(file) {
  const input = fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, '');
  const rows = []; let row = [], value = '', quoted = false;
  for (let i = 0; i < input.length; i++) {
    const ch = input[i];
    if (ch === '"') { if (quoted && input[i + 1] === '"') { value += '"'; i++; } else quoted = !quoted; }
    else if (!quoted && (ch === ',' || ch === '\n')) {
      row.push(value.replace(/\r$/, '')); value = '';
      if (ch === '\n') { if (row.some(Boolean)) rows.push(row); row = []; }
    } else value += ch;
  }
  if (value || row.length) { row.push(value.replace(/\r$/, '')); rows.push(row); }
  const headers = rows.shift();
  return rows.map(r => Object.fromEntries(headers.map((key, i) => [key, r[i] ?? ''])));
}
function tsv(file) {
  const lines = fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, '').trimEnd().split(/\r?\n/);
  const headers = lines.shift().split('\t');
  return lines.map(line => Object.fromEntries(line.split('\t').map((value, i) => [headers[i], value.replace(/\\([\\tnr])/g, (_, c) => ({ '\\': '\\', t: '\t', n: '\n', r: '\r' }[c]))])));
}
const cards = tsv(path.join(coreDir, 'cards.tsv'));
if (!cards.length || cards.length % 4) throw Error('Expected all four ranks from Core export');
const rows = csv(path.join(battleDir, 'battles.csv'));
const cells = csv(path.join(battleDir, 'summary.csv'));
const targets = csv(path.join(battleDir, 'targets.csv'));
const coverage = csv(path.join(battleDir, 'card-coverage.csv'));
const traces = csv(path.join(battleDir, 'trace-cases.csv'));
const completionPath = path.resolve(process.argv[4] || path.join(battleDir, '../FinalAnalysis/completion.json'));
const completion = fs.existsSync(completionPath) ? JSON.parse(fs.readFileSync(completionPath, 'utf8').replace(/^\uFEFF/, '')) : null;
const activationPath = path.resolve(battleDir, '../SourceActivationProof/summary.csv');
const activations = fs.existsSync(activationPath) ? csv(activationPath) : [];
const cardCount = cards.filter(c => c.UpgradeLevel === '0').length;
const schoolCount = cards.filter(c => c.UpgradeLevel === '0' && c.SchoolId).length;
if (schoolCount !== 132) throw Error('Expected 132 school cards');
const esc = s => String(s).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');
const rel = file => path.relative(out, file).replaceAll('\\', '/');
const num = (c, key) => Number(c[key] || 0);
function costs(c) {
  const m = c['SchoolEffect.Mechanic'], t = num(c, 'SchoolEffect.Threshold');
  const extra = [];
  if (num(c, 'PowderCost')) extra.push(`${c.PowderCost} 火药${num(c, 'SchoolEffect.FallbackDamage') + num(c, 'SchoolEffect.FallbackBlock') ? '（不足时固定弱效果）' : ''}`);
  if (num(c, 'CannonballCost')) extra.push(`${c.CannonballCost} 炮弹`);
  if (['ArcaneRelease', 'ArcaneWard', 'ArcaneMend', 'ArcaneEcho'].includes(m)) extra.push(`${t} 法力，先付临时法力`);
  if (['CurseWard', 'CurseDrain'].includes(m) && t) extra.push(`${t} 层咒印`);
  if (m === 'CurseDetonate') extra.push('需已有咒印；只强化下次结算');
  if (m === 'TideWard') extra.push(`${c['SchoolEffect.WoodCost']} 木板；无木板只有弱格挡，不蓄潮势`);
  if (m === 'TideRiposte') extra.push(`需至少 ${t} 潮势，付全部潮势`);
  if (['TideRenew', 'TideCleanse'].includes(m)) extra.push(`${t} 潮势`);
  if (m === 'ThunderFinish') extra.push('清空本回合已有雷链');
  if (m === 'ThunderEcho') extra.push(`已有至少 ${t} 雷链才抽牌，每回合一次；不消耗或增加雷链`);
  if (['BloodRisk', 'BloodWard'].includes(m)) extra.push(`${t} 当前阶段生命；付后至少留 1`);
  const heals = num(c, 'Repair') > 0 || ['FireHeal', 'ArcaneMend', 'CurseDrain', 'TideRenew'].includes(m);
  if (heals && !['BloodDrain', 'BloodMend'].includes(m)) {
    const boarding = /Boarding|All/.test(c.AllowedStages), ship = !/^Boarding$/.test(c.AllowedStages);
    extra.push((ship && boarding ? '船体用 1 木板／船员用 1 药品' : boarding ? '1 药品' : '1 木板') + '：完整治疗超过固定保底且伤口需要时才扣');
  }
  return extra.join('；') || '无额外物品或资源支付';
}
const compactCards = cards.map(c => ({ ...c, costSummary: costs(c), sourceKind: c.RequiredCrew ? '船员' : c.RequiredComponent ? '部件' : '基础' }));
const mechanicRules = [
  ['火焰', '远程炮击通常付 2 火药，部分强炮付 3，另付炮弹；近战攻防通常付 1。指定近战牌无火药时使用不随强化增长的 2–4 点基础效果，不能触发火焰附加效果。'],
  ['秘术', '初始法力 0／3；常规余额保留，不自动恢复。星盘护幕每次打出使本场上限 +1，不回蓝。临时法力可超上限，先支付，玩家回合结束清除。护身刻印获取 2 法力并提供低格挡；天涯星坠需 4 法力。'],
  ['诅咒', '体系牌不造成直伤；敌方行动、回复和玩家存活判定后，咒印在敌回合末扣生命，穿过格挡。强化结算只生效一次。咒盾按付费前层数计算，强盾扣部分层数，格挡不跨回合。'],
  ['潮汐', '木板强化潮盾；只有这部分盾实际挡伤才产生潮势，普通盾先吸收。潮势上限 20，保留到下一玩家回合结束；反击花光潮势，治疗/破蓄消耗指定潮势。无木板弱盾不能产潮势。'],
  ['雷霆', '只有雷霆攻击叠 1 层雷链，上限 6；加伤为已有层数乘卡牌系数，回合结束清空。蓄势强化下一次雷攻，不叠层；体系没有格挡牌。'],
  ['血契', '付血值提高，必须支付后仍存活；格挡不能代付。免费吸血和失血回复降至较低比例并有固定上限；其余有效治疗正常消耗木板或药品。']
];
const winRows = rows.filter(r => r.outcome === 'win');
const zeroWins = winRows.filter(r => ['powderUsed', 'cannonballsUsed', 'woodUsed', 'medicineUsed'].every(k => Number(r[k]) === 0));
const failKinds = Object.fromEntries([...new Set(rows.map(r => r.outcome))].map(k => [k, rows.filter(r => r.outcome === k).length]));
const noGrowth = cards.filter(c => c.UpgradeLevel === '0' && [1, 2, 3].every(rank => {
  const other = cards.find(v => v.Id === c.Id && Number(v.UpgradeLevel) === rank);
  return ['Damage','Block','Repair','Draw','MovementMeters','StrategyAmount','SchoolEffect.Power','SchoolEffect.Secondary','SchoolEffect.Threshold','SchoolEffect.Duration','SchoolEffect.Limit'].every(k => c[k] === other[k]) &&
    (c.Action !== 'FireRounds' || c.BurnDamage === other.BurnDamage);
}));
const audit = { status: 'review_required_not_packaged', cards: cardCount, schoolCards: schoolCount, rankRows: cards.length,
  benchmark: rel(battleDir), battles: rows.length, outcomes: failKinds, zeroItemWins: zeroWins.length,
  completedAudit: completion,
  sourceActivationProof: { trials: activations.length, successes: activations.filter(r=>r.result==='activation-proven').length, path: rel(activationPath), countsTowardBalanceWinRates: false },
  noNumericalUpgrade: noGrowth.map(c => ({ id: c.Id, name: c.Name })), definitions: compactCards,
  limits: '有限牌组、种子与启发式决策；耗材只统计真实胜局，不把失败视为零消耗。单牌约30%与六系等强仍须分情境审核，不能由这批木桩直接证明。' };
fs.writeFileSync(path.join(out, 'review-data.json'), JSON.stringify(audit, null, 2));
const validationPath = path.join(out, 'validation.json');
const validation = fs.existsSync(validationPath) ? JSON.parse(fs.readFileSync(validationPath, 'utf8').replace(/^\uFEFF/, '')) : null;
const decisionPath = path.join(out, 'balance-decision.json');
const decision = fs.existsSync(decisionPath) ? JSON.parse(fs.readFileSync(decisionPath, 'utf8').replace(/^\uFEFF/, '')) : null;
const defensePath = path.join(out, 'base-defense-changes.json');
const defense = fs.existsSync(defensePath) ? JSON.parse(fs.readFileSync(defensePath, 'utf8')) : null;
const defenseHtml = defense ? `<h2>本轮基础值下调</h2><div class="panel"><p>所有卡牌原级固定格挡、维修和治疗统一按原值 × 0.7 四舍五入，正数最低 1。${defense.positiveBaseDefenseCards} 张有相关基础值，其中 ${defense.changedCards} 张实际改变；原为 1 的数值保留 1。强化从新基础值计算，仍为 1／1.2／1.5／2。</p><p>本次下调固定基础项；体系通过支付资源、叠层和条件触发的附加盾疗保留。因此实际总效果并非每张都减少 30%。无耗材维修／治疗仍最多恢复 2，不随强化增长；施咒、伤害、行动费用、抽牌及物品费用未改。</p><details><summary>查看 ${defense.changedCards} 张卡的修改前后数值</summary><div class="scroll"><table><thead><tr><th>卡牌</th><th>体系</th><th>基础格挡：前→后</th><th>完整维修／治疗基础：前→后</th></tr></thead><tbody>${defense.cards.map(c=>`<tr><td>${esc(c.name)}</td><td>${names[c.school]}</td><td>${c.before.block} → ${c.after.block}</td><td>${c.before.repair} → ${c.after.repair}</td></tr>`).join('')}</tbody></table></div></details><p><a href="base-defense-changes.json">精确变更清单及范围</a></p></div>` : '';
const targetHtml = `<table><thead><tr>${Object.keys(targets[0]).map(k => `<th>${esc(({ target: '档位', name: '名称', hull: '船体', crewHealth: '船员生命', cannonBaseDamage: '炮击', meleeBaseDamage: '攻击', guardPerEnemyTurn: '每回合格挡', healPerEnemyTurn: '每回合回复' })[k] || k)}</th>`).join('')}</tr></thead><tbody>${targets.map(t => `<tr>${Object.values(t).map(v => `<td>${esc(v.replace('training_target_', '第 ') + (v.startsWith('training_target_') ? ' 档' : ''))}</td>`).join('')}</tr>`).join('')}</tbody></table>`;
const data = JSON.stringify({ cards: compactCards, cells, names }).replaceAll('<', '\\u003c');
const html = `<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>六体系规则与四类耗材审核</title><style>
:root{color-scheme:dark;--bg:#101c25;--card:#182a36;--line:#314953;--ink:#edf3f4;--muted:#a6bbc3;--accent:#84d3be;--warn:#ffc991}*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--ink);font:15px/1.75 'Microsoft YaHei',system-ui,sans-serif}main{max-width:1400px;margin:auto;padding:36px 25px 80px}h1{font-size:31px;line-height:1.5;margin:8px 0}h2{font-size:23px;margin-top:35px}h3{font-size:17px;margin:0 0 8px}a{color:var(--accent)}p{margin:10px 0}.muted,small{color:var(--muted)}.panel{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:20px;margin:18px 0}.warning{border-left:4px solid var(--warn)}.grid{display:grid;grid-template-columns:repeat(3,1fr);gap:14px}.grid>.panel{margin:0}.filters{display:flex;gap:14px;flex-wrap:wrap;align-items:center}label{color:var(--muted)}select,input{background:var(--bg);color:var(--ink);font:inherit;border:1px solid var(--line);border-radius:5px;padding:6px}table{border-collapse:collapse;width:100%;font-size:14px;font-variant-numeric:tabular-nums}th,td{padding:10px;border-bottom:1px solid var(--line);text-align:left;vertical-align:top}th{color:var(--accent);white-space:nowrap;position:sticky;top:0;background:var(--card)}.scroll{overflow:auto;max-height:650px}.num{text-align:right;white-space:nowrap}.card-table td:first-child{min-width:150px}.card-table td:nth-child(4){min-width:220px}.card-table td:nth-child(5){min-width:310px;white-space:pre-line}.warn{color:var(--warn)}.links{display:flex;gap:18px;flex-wrap:wrap}li{margin:7px 0}.empty{color:var(--muted)}@media(max-width:850px){.grid{grid-template-columns:1fr}main{padding:24px 14px}h1{font-size:25px}}
</style><main><p class="muted">第七港口 · 2026.10.12 · 待审核版本 / 未打包</p><h1>六体系规则与四类耗材审核</h1>
<div class="panel warning"><p><strong>${decision ? esc(decision.headline) : '机制已经调整；均衡目标需以实测差异审核，尚不宣称六系等强。'}</strong></p><p>${decision ? esc(decision.summary) : '本页逐卡列出新的等级数值、支付代价与木桩结果。所有统计来自真实 Core 出牌和库存变化。'}</p><p class="muted">${rows.length.toLocaleString()} 场本版主矩阵 · ${cardCount} 张有效卡 · ${schoolCount} 张体系牌 · ${cards.length} 个等级定义。失败、超时与没有胜局的数据不会计成“低消耗通关”。</p></div>
<h2>已经采用的规则</h2><div class="grid">${mechanicRules.map(([name, text]) => `<div class="panel"><h3>${name}</h3><p>${text}</p></div>`).join('')}</div>
<div class="panel"><p><strong>统一强化：</strong>0／1／2／3 阶倍率为 1／1.2／1.5／2；主要伤害、格挡、完整治疗成长，不再普遍降行动费用。原本抽牌的卡在三阶额外抽 1 张；不把资源产生、资源费用、持续回合、百分比、移动距离一并放大。同名牌本回合每次成功使用后，后续行动费用 +1。</p><p><strong>普通基础牌：</strong>每种每阶段库存 2 张；基础牌组 10–20 张。新加入“整顿号令”（0 费、回 1 行动、基础格挡 3）和“战术筹划”（1 费、抽 2、基础格挡 3），三个初始牌组各带一张，变为 12 张。原有存档索引保留。</p><p><strong>治疗：</strong>船体完整维修用木板，船员完整治疗用药品；无物品固定保底最多 2，不随强化增大。满血、或保底即可补满时不扣物品。普通格挡不消耗木板。潮汐布防的木板成本单独计算。</p><details><summary>纯功能牌强化边界：${noGrowth.length} 张所有等级数值相同</summary><p>${noGrowth.map(c => esc(c.Name)).join('、') || '无'}</p><p class="muted">资源生成、移动、比例与封顶固定，因此这类牌没有数值成长；部分其他抽牌功能牌仅在三阶改变抽牌数。该边界须审核，不能把每一级都解释为主效果提升。</p></details></div>
<h2>五档固定木桩</h2><div class="panel scroll">${targetHtml}</div><p class="muted">相同巡逻舰、装备品质 2、22 名普通水手；主矩阵无来源牌，只混合一个体系与普通牌。敌方不移动、不成长、不装填、不冲撞；正常装甲、格挡、灼烧、咒印和接舷损伤继承仍实际结算。</p>
<h2>获胜耗材：按同一条件比较</h2><div class="panel"><p>完整补给：火药 25、炮弹 40、木板 18、药品 10。总消耗是四类物品实际减少数量之和，不按价格加权。零补给将四项全置 0；单项缺失仅扣掉该项。</p><div class="filters"><label>强化 <select id="rank">${[0,1,2,3].map(v=>`<option value="${v}" ${v===3?'selected':''}>+${v}</option>`).join('')}</select></label><label>基础数量 <select id="size">${[10,15,20].map(v=>`<option>${v}</option>`).join('')}</select></label><label>体系同名张数 <select id="copies"><option value="1">1</option><option value="2">2</option></select></label><label>打法 <select id="tactic"><option value="cannon">炮击优先</option><option value="boarding">接舷优先</option></select></label><label>木桩 <select id="target">${[1,2,3,4,5].map(v=>`<option value="training_target_${v}" ${v===3?'selected':''}>${v} 档</option>`).join('')}</select></label><label>补给 <select id="supply"><option value="full">完整</option><option value="none">四项全无</option><option value="no-powder">无火药</option><option value="no-cannonballs">无炮弹</option><option value="no-wood">无木板</option><option value="no-medicine">无药品</option></select></label></div><div class="scroll"><table><thead><tr><th>体系</th><th>胜局 / 场次</th><th>胜率</th><th>回合</th><th>火药</th><th>炮弹</th><th>木板</th><th>药品</th><th>总消耗</th></tr></thead><tbody id="battleRows"></tbody></table></div><p class="muted">胜局均值只对该单元格实际胜局计算。体系同名张数不用于普通牌组筛选。不可行的普通 20 张接舷配方不虚构第三张库存。配方与胜利样本不同会影响均值；请同时看胜率、回合与四项明细。</p></div>
<div class="panel warning"><h3>零耗材检查</h3><p>主矩阵共发现 <strong>${zeroWins.length}</strong> 个零物品胜局（包含原本携带物品却未使用的胜局）。零耗材路径仍需通过下列重放逐项审核。</p><div class="scroll"><table><thead><tr><th>体系</th><th>强化</th><th>打法</th><th>档位</th><th>回合</th><th>复现记录</th></tr></thead><tbody>${traces.map(t => `<tr><td>${names[t.school]}</td><td>+${t.rank}</td><td>${esc(t.tactic)}</td><td>${esc(t.target.replace('training_target_',''))}</td><td>${t.turns}</td><td><a href="${esc(rel(path.join(battleDir, t.trace)))}">逐动作 CSV</a></td></tr>`).join('')}</tbody></table></div><p class="muted">有限配方和固定启发式决策不能证明不存在更强构筑。木桩胜率也不能直接换算为“单牌强 30%”。审核目标仍是六系强度接近、胜利总耗材接近，并避免某一体系独享高难度零耗材通关。</p></div>
<h2>${cardCount} 张卡牌逐级数值与支付代价</h2><div class="panel"><div class="filters"><label>体系 <select id="cardSchool"><option value="">全部</option>${Object.entries(names).map(([k,v])=>`<option value="${k}">${v}</option>`).join('')}</select></label><label>等级 <select id="cardRank">${[0,1,2,3].map(v=>`<option value="${v}">+${v}</option>`).join('')}</select></label><label>来源 <select id="cardSource"><option value="">全部</option><option>基础</option><option>船员</option><option>部件</option></select></label><label>搜索 <input id="query" placeholder="卡名、机制、代价"></label><span id="cardCount"></span></div><p class="muted">下表是数据定义和完整效果，接舷实际值还按普通船员人数缩放；没有重加普通牌 ×2。实时卡面会显示不足耗材的弱效果和本次合计值。</p><div class="scroll"><table class="card-table"><thead><tr><th>卡牌 / 来源 / 阶段</th><th>行动</th><th>伤 / 盾 / 治 / 抽</th><th>物品与体系代价</th><th>完整效果</th></tr></thead><tbody id="cardRows"></tbody></table></div></div>
<h2>验证与原始记录</h2><div class="panel"><p>${validation ? esc(validation.summary) : '验证摘要将在最终冻结后更新。'}</p><p class="muted">实战覆盖：本矩阵实际打出 ${coverage.filter(c=>Number(c.plays)>0).length} / ${coverage.length} 张记录中的有效牌；未被策略选中的牌不计为已完成实战校准。来源牌不并入基础主矩阵强度均值。</p><div class="links"><a href="review-data.json">全部等级与审核数据</a><a href="${esc(rel(path.join(coreDir,'cards.tsv')))}">Core 原始等级表</a><a href="${esc(rel(path.join(battleDir,'battles.csv')))}">每局四类耗材</a><a href="${esc(rel(path.join(battleDir,'deck-manifest.csv')))}">每个牌组完整配方</a><a href="${esc(rel(path.join(battleDir,'supply-comparison.csv')))}">逐项断供配对</a><a href="${esc(rel(path.join(battleDir,'card-coverage.csv')))}">逐卡实战覆盖</a>${validation ? '<a href="validation.json">验证详情</a>' : ''}${decision ? '<a href="balance-decision.json">调整依据与剩余差异</a>' : ''}</div><p>本轮没有打包。审核完成后再进入打包流程。</p></div></main><script>
const data=${data}; const $=id=>document.getElementById(id); const esc=s=>String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;');
const mean=x=>x===''||x==null?'—':Number(x).toFixed(2).replace(/\\.?0+$/,'');
function battles(){ $('battleRows').innerHTML=Object.keys(data.names).map(s=>{const c=data.cells.find(c=>c.school===s&&c.rank===$('rank').value&&c.size===$('size').value&&(s==='ordinary'||c.coreCopies===$('copies').value)&&c.tactic===$('tactic').value&&c.target===$('target').value&&c.supply===$('supply').value); if(!c)return '<tr><td>'+data.names[s]+'</td><td colspan="8" class="empty">此组合没有已完成数据 / 配方不可行</td></tr>'; return '<tr><td>'+data.names[s]+'</td><td>'+c.wins+' / '+c.battles+'</td><td>'+mean(Number(c.winRate)*100)+'%</td>'+['meanWinTurns','meanWinPowder','meanWinCannonballs','meanWinWood','meanWinMedicine','meanWinTotalSupplies'].map(k=>'<td class="num">'+mean(c[k])+'</td>').join('')+'</tr>';}).join(''); }
function cards(){const selected=data.cards.filter(c=>(!$('cardSchool').value||(c.SchoolId||'ordinary')===$('cardSchool').value)&&c.UpgradeLevel===$('cardRank').value&&(!$('cardSource').value||c.sourceKind===$('cardSource').value)&&JSON.stringify(c).toLowerCase().includes($('query').value.trim().toLowerCase()));$('cardCount').textContent=selected.length+' / ${cardCount} 张';$('cardRows').innerHTML=selected.map(c=>'<tr><td><strong>'+esc(c.Name)+'</strong><br><small>'+data.names[c.SchoolId||'ordinary']+' · '+c.sourceKind+' · '+esc(c.AllowedStages)+'</small><br><small>'+esc(c.Id)+'</small></td><td>'+c.Energy+'</td><td class="num">'+[c.Damage,c.Block,c.Repair,c.Draw].join(' / ')+'</td><td>'+esc(c.costSummary)+'</td><td>'+esc(c.Description)+'</td></tr>').join('');}
for(const id of ['rank','size','copies','tactic','target','supply'])$(id).addEventListener('change',battles);for(const id of ['cardSchool','cardRank','cardSource','query'])$(id).addEventListener('input',cards);battles();cards();
</script></html>`;
const sourceHtml = completion ? `<h2>来源牌补充测试与覆盖边界</h2><div class="panel"><p>基础主矩阵与来源牌敏感性合计保存 <strong>${completion.totalPersistedTrials.toLocaleString()}</strong> 场。来源牌测试每次只加入一张来源牌、使用 2 个配对种子与 0／3 阶，不能与上面的基础主矩阵合并计算强度。</p><p>目前可取得的 ${completion.coverage.basicCards + completion.coverage.activeSourceCards} 张独立牌中，实际打出 ${completion.coverage.playedCards} 张：基础 ${completion.coverage.playedBasicCards}/${completion.coverage.basicCards}；来源 ${completion.coverage.playedConfiguredSources}/${completion.coverage.activeSourceCards}。逐级表保留的 11 个旧来源别名不当作新增实战覆盖。</p><p class="muted">${Object.values(completion.runs).reduce((n,r)=>n+r.turnWatchdogs,0)} 场达到 120 回合上限，全部记为未决，不计胜局；没有发现非法出牌或库存与实际扣费不一致。来源牌未打出意味着本策略覆盖不足，不能当作已完成强度校准。</p><div class="links"><a href="${esc(rel(completionPath))}">补充测试、种子与覆盖详情</a><a href="${esc(rel(path.join(path.dirname(completionPath),'coverage-by-school.csv')))}">各体系实战覆盖</a></div></div>` : '';
const fmtMean = value => value == null || value === '' ? '—' : Number(value).toFixed(2).replace(/\.?0+$/, '');
const pairedHtml = completion?.cohorts ? `<h2>全部配对战斗：下调后的影响</h2><div class="panel"><div class="scroll"><table><thead><tr><th>矩阵</th><th>场次</th><th>原胜局→现胜局</th><th>原胜现未胜</th><th>原未胜现胜</th><th>共同胜局平均耗材：前→后</th></tr></thead><tbody>${Object.entries(completion.cohorts).map(([key,c])=>`<tr><td>${key==='baseline'?'基础牌组':'来源单牌'}</td><td>${c.paired.trials}</td><td>${c.paired.beforeWins} → ${c.paired.afterWins}</td><td>${c.paired.lostWins}</td><td>${c.paired.gainedWins}</td><td>${fmtMean(c.paired.beforeCommonWin_totalSuppliesUsed)} → ${fmtMean(c.paired.afterCommonWin_totalSuppliesUsed)}</td></tr>`).join('')}</tbody></table></div><p class="muted">每条记录使用同一牌组、种子、木桩和策略前后配对。失败和未决结果不算零耗材胜利；共同胜局均值可避免因困难胜局消失而误判为更省物资。</p><div class="links"><a href="${esc(rel(path.join(path.dirname(completionPath),'paired-group-summary.csv')))}">分组配对汇总</a><a href="${esc(rel(path.join(path.dirname(completionPath),'paired-trial-comparison.csv')))}">逐局前后对照</a></div></div>` : '';
const comparisonHtml = decision?.comparison ? `<h2>同配置修改前后：第三档接舷</h2><div class="panel"><p>三阶、10 张基础牌、体系同名 1 张、完整补给、4 个配对种子。总消耗只对胜局求均值；胜局减少时，剩下胜局的低消耗不能解释为更强。</p><div class="scroll"><table><thead><tr><th>体系</th><th>原胜局</th><th>现胜局</th><th>原胜局平均耗材</th><th>现胜局平均耗材</th></tr></thead><tbody>${decision.comparison.map(c=>`<tr><td>${names[c.school]}</td><td>${c.before.wins}/${c.before.battles}</td><td>${c.after.wins}/${c.after.battles}</td><td>${fmtMean(c.before.total)}</td><td>${fmtMean(c.after.total)}</td></tr>`).join('')}</tbody></table></div><p class="muted">各档位、炮击/接舷、10/15/20 张构筑和四类耗材明细可在下方筛选。</p></div>` : '';
const finalHtml = html
  .replaceAll('六体系规则与四类耗材审核', defense ? '基础格挡、维修与治疗下调审核' : '六体系规则与四类耗材审核')
  .replace('<h2>已经采用的规则</h2>', defenseHtml + pairedHtml + comparisonHtml + '<h2>已经采用的规则</h2>')
  .replaceAll('基础格挡 3', `基础格挡 ${compactCards.find(c=>c.Id==='card_energy_order'&&c.UpgradeLevel==='0').Block}`)
  .replace(`${cardCount} 张有效卡`, `${cardCount} 张公布定义（含 11 个旧来源别名）`)
  .replace('该边界须审核，不能把每一级都解释为主效果提升。', '迎敌推进、并排行船、星盘护幕、缝魂契约这 4 张纯功能基础牌已禁止花金币强化；月纱蓄辉属于来源牌。仅三阶增加抽牌的其他牌仍可逐阶强化。')
  .replace('<h2>验证与原始记录</h2>', sourceHtml + '<h2>验证与原始记录</h2>')
  .replace('<h2>验证与原始记录</h2>', (activations.length ? `<div class="panel"><p><strong>强力秘术牌可用性补证：</strong>天涯星坠在 ${activations.length} 条独立真实发牌序列中有 ${activations.filter(r=>r.result==='activation-proven').length} 条成功完成“扩容→蓄蓝→跨回合→施放”。其余序列未抽到扩容牌而未触发；没有注入手牌或资源。这证明存在合法使用路径，不等于强度完成校准，也不并入上面的胜率。<a href="${esc(rel(activationPath))}">查看激活记录</a></p></div>` : '') + '<h2>验证与原始记录</h2>')
  .replace('<option value="cannon">炮击优先</option><option value="boarding">接舷优先</option>', '<option value="boarding">接舷优先</option><option value="cannon">炮击优先</option>')
  .replace('实战覆盖：本矩阵实际打出', '基础矩阵覆盖：本矩阵实际打出')
  .replace('张记录中的有效牌；', '张库记录（含未加入的来源牌与退役槽位）；')
  .replace('<a href="validation.json">验证详情</a>', `<a href="validation.json">验证详情</a>${validation?.liveScreens ? `<a href="${esc(validation.liveScreens)}">真实游戏截图</a>` : ''}`);
fs.writeFileSync(path.join(out, 'index.html'), finalHtml);
console.log(JSON.stringify({ output: path.join(out, 'index.html'), cardCount, schoolCount, battles: rows.length, zeroWins: zeroWins.length, noGrowth: noGrowth.map(c=>c.Name) }));
