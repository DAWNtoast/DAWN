'use strict';
const fs=require('node:fs'),path=require('node:path');
const {root,out,schoolNames,schools,csv,stats,group,staticAudit}=require('./summarize-dummy-audit.cjs');
const raw=process.argv[2]||path.join(root,'UnityClient/Artifacts/CardDeckSupplyAudit20261012/DummyBaseline');
const rows=csv(path.join(raw,'battles.csv'));
// Accept the explicit manifest field names, without combining source-card or search runs.
for(const r of rows) {
  r.coreCopies=r.coreCopies??r.copies;r.size=r.size??r.deckSize;
  r.powderUsed=r.powderUsed??r.powder;r.cannonballsUsed=r.cannonballsUsed??r.balls;r.medicineUsed=r.medicineUsed??r.medicine;
  for(const k of ['rank','size','coreCopies','powderUsed','cannonballsUsed','medicineUsed','turns','schoolCards'])
    if(!Number.isFinite(Number(r[k])))throw new Error('Missing numeric field '+k);
}
const staticData=staticAudit();
const cells=[...group(rows,r=>[r.school,r.size,r.coreCopies,r.rank,r.tactic,r.supply,r.target].join('|')).values()]
  .map(a=>({...Object.fromEntries(['school','size','coreCopies','rank','tactic','supply','target'].map(k=>[k,a[0][k]])),...stats(a)}));
const overview=schools.map(s=>({school:s,full:stats(rows.filter(r=>r.school===s&&r.supply==='full')),none:stats(rows.filter(r=>r.school===s&&r.supply==='none'))}));
const targets=csv(path.join(raw,'targets.csv'));
const manifest=csv(path.join(raw,'deck-manifest.csv'));
const zeroWins=rows.filter(r=>r.outcome==='win' && Number(r.powderUsed)+Number(r.cannonballsUsed)+Number(r.medicineUsed)===0);
const summary={rows:rows.length,zeroCostWins:zeroWins.length,zeroSupplyWins:zeroWins.filter(r=>r.supply==='none').length,
  outcomes:Object.fromEntries([...group(rows,r=>r.outcome)].map(([k,v])=>[k,v.length])),overview,cells,
  limits:'Seeded policies and finite deck recipes; not exhaustive. Source-card and search/holdout runs are separate. Prior +30% single-card estimates do not demonstrate +30% deck strength.'};
fs.writeFileSync(path.join(out,'battle-audit-summary.json'),JSON.stringify(summary,null,2));
const rel=file=>path.relative(out,path.join(raw,file)).replaceAll('\\','/');
const esc=s=>String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('"','&quot;');
const targetsHtml=targets.map(t=>'<tr>'+Object.values(t).map(x=>'<td>'+esc(x)+'</td>').join('')+'</tr>').join('');
const staticHtml=staticData.bySchool.map(s=>`<tr><td>${s.name}</td><td>${s.basic}</td><td>${s.cards}</td><td>${s.freeBasicDirectDamage}</td><td>${s.directDamageWithNoPowderOrBalls}</td></tr>`).join('');
const data=JSON.stringify({cells,overview,schoolNames,schools,targets:targets.map(t=>t.target)}).replaceAll('<','\\u003c');
const supplementalFiles=['card-coverage.csv','zero-supply-wins.csv','trace-cases.csv','supply-comparison.csv','matched-school-common-wins.csv'];
const supplements=supplementalFiles.filter(f=>fs.existsSync(path.join(raw,f))).map(f=>`<a href="${esc(rel(f))}">${esc(f)}</a>`).join(' · ');
const runNames={DummySearch:'候选搜索（训练种子）',DummyHoldout:'入选配方复验（独立种子）',DummySources:'单来源牌敏感性与匹配对照'};
const supplemental=[];
const covered=new Map(staticData.cards.map(c=>[c.id,0]));
for(const dir of [raw,...Object.keys(runNames).map(n=>path.join(path.dirname(raw),n))]) {
  const coverage=path.join(dir,'card-coverage.csv');
  if(fs.existsSync(coverage))for(const c of csv(coverage))if(covered.has(c.cardId))covered.set(c.cardId,covered.get(c.cardId)+Number(c.plays));
  if(dir===raw||!fs.existsSync(path.join(dir,'battles.csv')))continue;
  const a=csv(path.join(dir,'battles.csv'));
  supplemental.push({name:runNames[path.basename(dir)],dir,rows:a.length,decks:new Set(a.map(r=>r.deck)).size,
    sourceCards:new Set(a.map(r=>r.sourceCardId).filter(Boolean)).size,
    zeroSupplyWins:a.filter(r=>r.supply==='none'&&r.outcome==='win').length,
    watchdogs:a.filter(r=>r.outcome.includes('watchdog')).length,
    overview:schools.map(s=>({school:s,full:stats(a.filter(r=>r.school===s&&r.supply==='full')),none:stats(a.filter(r=>r.school===s&&r.supply==='none'))}))});
}
const cardCoverage=staticData.cards.map(c=>({id:c.id,name:c.name,school:c.school,basic:c.basic,plays:covered.get(c.id)}));
const formalTrials=rows.length+supplemental.reduce((n,r)=>n+r.rows,0);
const totalWatchdogs=(summary.outcomes['turn-watchdog']||0)+supplemental.reduce((n,r)=>n+r.watchdogs,0);
const completionFile=path.join(path.dirname(raw),'benchmark-completion.json');
const completionLink=fs.existsSync(completionFile)?`<a href="${esc(path.relative(out,completionFile).replaceAll('\\','/'))}">正式场次与运行限制</a>`:'';
const holdoutEvidence=path.join(path.dirname(raw),'DummyHoldout/trace-cases.csv');
const holdoutLink=fs.existsSync(holdoutEvidence)?`<a href="${esc(path.relative(out,holdoutEvidence).replaceAll('\\','/'))}">独立复验零补给重放索引</a>`:'';
fs.writeFileSync(path.join(out,'supplemental-audit-summary.json'),JSON.stringify({runs:supplemental,cardCoverage},null,2));
const supplementalHtml=`<h2>扩展验证</h2><div class="panel scroll"><table><thead><tr><th>实验</th><th>场次</th><th>配方</th><th>来源卡种数</th><th>零补给胜局</th><th>超时未完成</th><th>数据</th></tr></thead><tbody>${supplemental.map(r=>`<tr><td>${r.name}</td><td>${r.rows.toLocaleString()}</td><td>${r.decks}</td><td>${r.sourceCards||'—'}</td><td class="bad">${r.zeroSupplyWins}</td><td>${r.watchdogs}</td><td><a href="${esc(path.relative(out,path.join(r.dir,'battles.csv')).replaceAll('\\','/'))}">逐局</a> · <a href="${esc(path.relative(out,path.join(r.dir,'deck-manifest.csv')).replaceAll('\\','/'))}">牌组</a></td></tr>`).join('')}</tbody></table><p class="note">搜索覆盖 10–20 张可达长度及有限混合阶段长度、不同攻防/省物品倾向；入选后冻结配方，再以不同种子验证。来源卡逐张追加，船员来源占 1 个船位，使用匹配的 21 名普通船员对照；部件使用 22 人对照。来源比较不与基础主矩阵合并为强度结论。</p><p>各实验合计实际打出 <strong>${cardCoverage.filter(c=>c.plays>0).length} / ${cardCoverage.length}</strong> 张有效牌；基础牌 <strong>${cardCoverage.filter(c=>c.basic&&c.plays>0).length} / 79</strong>。未出牌与未覆盖卡逐张列在 <a href="supplemental-audit-summary.json">覆盖清单</a>，不视作已完成实战校准。</p></div>`;
const traceFile=path.join(raw,'trace-cases.csv');
const traces=fs.existsSync(traceFile)?csv(traceFile):[];
const traceHtml=`<details class="panel"><summary>查看 ${traces.length} 个零补给胜利重放（每系／强化／策略的较高难度反例）</summary><div class="scroll"><table><thead><tr><th>体系</th><th>强化</th><th>策略</th><th>档位</th><th>牌组</th><th>回合</th><th>记录</th></tr></thead><tbody>${traces.map(t=>`<tr><td>${schoolNames[t.school]}</td><td>+${t.rank}</td><td>${t.tactic==='cannon'?'炮击优先':'接舷优先'}</td><td>${t.target.replace('training_target_','')}</td><td>${esc(t.deck)}</td><td>${t.turns}</td><td><a href="${esc(rel(t.trace))}">逐动作 CSV</a></td></tr>`).join('')}</tbody></table></div><p class="note">均已重新执行并比对结局、出牌数、回合、剩余生命与耗材。该表展示反例，不表示此配方在所有种子中都能取胜。</p></details>`;
const verificationFile=path.join(out,'validation-summary.json');
const verification=fs.existsSync(verificationFile)?JSON.parse(fs.readFileSync(verificationFile,'utf8').replace(/^\uFEFF/,'')):null;
const finalScreenshot=verification?.Screenshots?.find(s=>s.includes('TwentyCardMaximum'))||'Unity/LiveScreens/DeckBuilderCards_05_TwentyCardMaximum.png';
const verificationNote=verification?.FinalSourceSnapshot?.Complete?'最终验证使用包含全部源码和测试文件的快照，无排除文件。本次仅运行指定测试范围。D3D12 曾因原生纹理分配失败中断，没有断言结果；相同源码改用 D3D11 后 8 项交互测试全部通过。':verification?.UnityLimitation||'';
const verifyHtml=verification?`<h2>实现与验证</h2><div class="panel"><p>指定 Unity 回归：编辑器 <strong>${verification.UnityEditMode.Passed}/${verification.UnityEditMode.Total}</strong>，真实交互 <strong>${verification.UnityPlayMode.Passed}/${verification.UnityPlayMode.Total}</strong>。纯 Core 广泛回归 <strong>${verification.Core.Passed}/${verification.Core.Total}</strong>，保留 ${verification.Core.Failed} 项旧失败；新增库存与木桩 ${verification.Core.BaseDeckCases+verification.Core.TrainingCases} 项通过。该结果不代表全项目全部测试通过。</p><p>864 条卡牌等级定义哈希与上一轮完全一致；12 个既有预览仅同步 50 条文本，非文本改动为 0。</p><p class="note">${esc(verificationNote)}</p><div class="links"><a href="validation-summary.json">验证摘要及限制</a><a href="${verification.UnityEditMode.Results}">编辑器结果</a><a href="${verification.UnityPlayMode.Results}">交互结果</a><a href="cards-unchanged.json">数值未变证明</a></div><details><summary>查看 20 张上限的实机截图</summary><img alt="真实组牌界面：每种2张，牌组20/20" src="${esc(finalScreenshot)}" loading="lazy" style="display:block;width:100%;margin-top:16px;border-radius:8px"></details></div>`:'';
const html=`<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>卡组耗材与五档木桩审核</title>
<style>
:root{color-scheme:dark;--bg:#101b24;--paper:#182a37;--ink:#e4edf0;--muted:#9eb1bb;--line:#304753;--accent:#7fd0bb;--warn:#ffc785;--bad:#ffad9d}*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--ink);font:16px/1.7 'Microsoft YaHei',system-ui,sans-serif}main{max-width:1220px;margin:auto;padding:44px 28px 80px}a{color:var(--accent)}h1{font-size:34px;line-height:1.4;margin:8px 0 14px}h2{font-size:23px;margin:36px 0 14px}h3{font-size:18px;margin:0 0 10px}p{margin:10px 0}.eyebrow{letter-spacing:.13em;color:var(--accent);font-size:13px}.muted,small{color:var(--muted)}.panel{background:var(--paper);border:1px solid var(--line);border-radius:12px;padding:22px;margin:18px 0}.callout{border-left:4px solid var(--warn)}.bad{color:var(--bad)}.good{color:var(--accent)}.cards{display:grid;grid-template-columns:repeat(3,1fr);gap:14px}.metric{font-size:28px;color:var(--accent);font-weight:700}.filters{display:flex;gap:16px;flex-wrap:wrap;align-items:end}label{font-size:13px;color:var(--muted)}select{display:block;margin-top:5px;background:var(--bg);color:var(--ink);border:1px solid var(--line);padding:9px;border-radius:6px;font:inherit;min-width:105px}.scroll{overflow:auto}table{width:100%;border-collapse:collapse;font-variant-numeric:tabular-nums;font-size:14px}th,td{text-align:right;padding:11px 12px;border-bottom:1px solid var(--line);white-space:nowrap}th{color:var(--muted);font-weight:500}th:first-child,td:first-child{text-align:left}tbody tr:hover{background:#213a47}.bar{width:100%;height:7px;background:var(--line);border-radius:8px;overflow:hidden;margin-top:5px}.bar>i{display:block;height:100%;background:var(--accent)}.links{display:flex;flex-wrap:wrap;gap:10px 22px}.pill{padding:4px 9px;border:1px solid var(--line);border-radius:6px;font-size:13px}.note{font-size:14px;color:var(--muted)}.grid2{display:grid;grid-template-columns:1fr 1fr;gap:20px}ol{padding-left:24px}li{margin:9px 0}@media(max-width:750px){main{padding:25px 16px}.cards,.grid2{grid-template-columns:1fr}h1{font-size:27px}}
</style><main><div class="eyebrow">第七港口 / 数值审核 / 2026.10.12</div><h1>单体系卡组 × 五档固定木桩</h1>
<p class="muted">每种基础卡 2 张 · 每阶段基础牌组 10–20 张 · 总耗材 = 火药 + 炮弹 + 药品</p>
<div class="panel callout"><h3>审核结论：组牌规则已完成，耗材平衡尚未达标</h3><p>本轮主矩阵发现 <strong>${zeroWins.length.toLocaleString()} 局零耗材胜利</strong>，其中完全不带三类补给的胜利有 <strong>${summary.zeroSupplyWins.toLocaleString()} 局</strong>。因此不能认定所有体系已经依赖物品，也不能将上一轮单卡预期收益约 +30% 等同于本轮牌组强度。<strong>未打包，等待本轮规则与调整方向审核。</strong></p><p class="note">主矩阵另有 ${summary.outcomes['turn-watchdog']||0} 局到达 120 回合预算仍未结束，记为未完成。本文胜率指预算内成功比例；未完成局不进入获胜耗材统计，也不宣称它们必败。</p></div>
<div class="cards"><div class="panel"><div class="metric">2 张 / 每种</div><span class="muted">三个阶段分别校验；来源牌自动追加</span></div><div class="panel"><div class="metric">10–20 张</div><span class="muted">旧存档裁掉第三份，再补足 10 张</span></div><div class="panel"><div class="metric">${formalTrials.toLocaleString()} 局</div><span class="muted">正式保存场次；主矩阵 ${rows.length.toLocaleString()} 局</span></div></div>
<h2>同条件比较</h2><p>主矩阵使用相同巡防舰、22 名普通船员、装备质量 2，无来源追加卡。完整补给为 <strong>25 火药、40 炮弹、10 药品</strong>；船体、航线、种子集合与行动策略保持一致。水 20、食物 20、木材 18 在各组保持相同；本报告“零补给”仅指火药、炮弹、药品三项均为零，并非空货舱。准备、炮击、接舷三阶段的基础牌数量相同。每系核心 1 份代表每张体系基础牌放 1 张，2 份代表各放 2 张；剩余槽位由普通牌填充。</p>
<div class="panel"><div class="filters"><label>木桩难度<select id="target"></select></label><label>卡牌强化<select id="rank"><option value="0">原卡</option><option value="1">+1</option><option value="2">+2</option><option value="3">+3</option></select></label><label>每阶段张数<select id="size"><option>10</option><option>15</option><option>20</option></select></label><label>每张体系核心<select id="copies"><option value="1">1 份</option><option value="2">2 份</option></select></label><label>策略<select id="tactic"><option value="boarding">接舷优先</option><option value="cannon">炮击优先</option></select></label><label>携带补给<select id="supply"><option value="full">完整补给</option><option value="none">三项补给为零</option><option value="no-powder">无火药</option><option value="no-cannonballs">无炮弹</option><option value="no-medicine">无药品</option></select></label></div>
<p id="verdict"></p><div class="scroll"><table><thead><tr><th>体系</th><th>胜利 / 样本</th><th>胜率</th><th>胜局总耗材均值</th><th>粉 / 弹 / 药</th><th>中位数 [P10–P90]</th><th>胜局回合</th><th>零耗材胜局</th><th>体系牌出牌均次</th></tr></thead><tbody id="results"></tbody></table></div><p class="note">耗材与回合统计只计获胜局；失败局不会当作“省物品”。无胜局显示“—”。普通接舷仅 7 种牌，最多 14 张，因此 15/20 张普通对照不可构成；单份体系核心的接舷最多 18 张，因此该配置的 20 张也不可构成。炮击优先在无可供给炮击时允许合法转接舷；主策略不主动冲撞。每格样本较少，属于方向审查。</p></div>
<h2>五档木桩</h2><p>表内伤害为每回合固定名义值；正常船甲、诅咒、潮势和格挡仍可改变实际受伤。准备阶段不攻击，初距 300 米，木桩静止，不装填、不冲撞、不随回合成长。格挡每回合刷新；回血只恢复当前阶段且不超过上限。转接舷保留正常炮击伤损折算。训练结束不发战利品，不改真实航程结算。</p>
<div class="panel scroll"><table><thead><tr><th>档位</th><th>船体上限</th><th>接舷上限</th><th>炮击</th><th>近战</th><th>每回合格挡</th><th>每回合回复</th></tr></thead><tbody>${targetsHtml}</tbody></table></div>
<h2>为什么会出现零耗材通关</h2><div class="grid2"><div class="panel"><h3>公共规则给了免费获胜路径</h3><p>普通近战、机动接舷，以及部分法术、灼烧、反击都可以不扣三类物品。没有药品时基础治疗仍然有效；血契吸血与失血回复也不耗药。抽牌、弃牌重洗使这些行动可以跨回合反复使用。</p><p class="note">“不消耗粉弹的直接伤害牌”只统计定义中的 Damage &gt; 0，不含纯持续伤害、反击和冲撞；这是风险定位，不是胜率推断。</p></div><div class="panel scroll"><table><thead><tr><th>体系</th><th>基础卡</th><th>全部卡</th><th>免费直伤基础卡</th><th>免费直伤全部卡</th></tr></thead><tbody>${staticHtml}</tbody></table></div></div>
<h2>建议审核的调整方向</h2><div class="panel"><ol><li><strong>先处理公共的零耗材伤害路径。</strong>要满足不依赖物品就不能通关的目标，公共耗材规则需覆盖普通近战、法术、持续伤害、反击和冲撞；仅降低血契治疗或抬高木桩攻击无法保证这一点。</li><li><strong>可评估的具体方案：</strong>每个直接伤害行动至少支付 1 件实体耗材，已有火药/炮弹费用计入；灼烧、反击在施加时付费；纯防御和抽牌不强制收费。不同体系的物品种类、费用与伤害需另做一轮配套校准。本轮未将这项新经济规则写入正式战斗。</li><li><strong>同时复核强化节奏。</strong>+2 的降费与 +3 的额外抽牌使普通和体系牌都能形成低能量循环；应按完整牌组复核这些收益，避免仅调整基础伤害或某一个体系的治疗量。</li><li><strong>建议验收口径：</strong>同木桩、强化、牌组规模、策略的六系胜率均至少 80%，彼此胜率差不超过 10 个百分点；获胜总耗材均值在六系均值 ±15% 内，零耗材胜局为 0。±15% 和胜率阈值是待审核建议。</li><li><strong>保留约 +30% 的原始目标。</strong>在新耗材规则确认后，同时复核普通牌对照的回合、伤害效率及可达难度，不以统一卡面倍率或强迫自动策略浪费物品来制造达标。</li></ol></div>
<h2>覆盖与证据</h2><p>本次执行的是有限配方、固定种子的自动策略；能发现反例，不能证明所有合法牌组均衡。主矩阵覆盖每系全部 10 张基础卡；来源卡敏感性及搜索验证单独报告。全 216 张的静态耗材检查已列出。升级规则、已取消的普通牌额外 ×2 均沿用上一轮定义。</p>
<div class="panel links"><a href="${esc(rel('battles.csv'))}">逐局原始数据</a><a href="${esc(rel('deck-manifest.csv'))}">完整牌组清单与不可构成项</a><a href="battle-audit-summary.json">本页统计数据</a><a href="card-supply-audit.json">216 张静态耗材检查</a><a href="audit-criteria.json">建议验收口径</a><a href="../CardBalanceReview20261012/index.html">上一轮逐卡数值审核</a></div>
<p class="note">${supplements}</p><div class="panel"><p>正式保存共 ${formalTrials.toLocaleString()} 场，其中 ${totalWatchdogs.toLocaleString()} 场达到 120 回合预算，计为未完成。预演、重放与一次进程崩溃丢失的 12,600 场均未计入正式场次。</p><div class="links">${completionLink}${holdoutLink}</div></div>${traceHtml}${supplementalHtml}${verifyHtml}<p class="note">基础库存与卡组上下限已经写入源码；没有进行发布构建或压缩打包。详细测试与预览文件见同目录。</p>
<script>const DATA=${data};
const $=id=>document.getElementById(id),fmt=(n,d=1)=>n==null?'—':Number(n).toFixed(d),pc=n=>fmt(n*100,0)+'%';
$('target').innerHTML=DATA.targets.map((t,i)=>'<option value="'+t+'">'+['一','二','三','四','五'][i]+'档</option>').join('');
$('target').value=DATA.targets[2];$('rank').value='3';$('tactic').value='cannon';
function render(){const target=$('target').value,rank=$('rank').value,size=$('size').value,copies=$('copies').value,tactic=$('tactic').value,supply=$('supply').value;
const selected=DATA.schools.map(s=>DATA.cells.find(c=>c.school===s&&c.target===target&&String(c.rank)===rank&&String(c.size)===size&&String(c.coreCopies)===(s==='ordinary'?'0':copies)&&c.tactic===tactic&&c.supply===supply));
const max=Math.max(1,...selected.filter(Boolean).map(c=>c.meanTotal||0));
$('results').innerHTML=DATA.schools.map((s,i)=>{const c=selected[i];if(!c)return '<tr><td>'+DATA.schoolNames[s]+'</td><td colspan="8" class="muted">此配置不可构成／无样本</td></tr>';return '<tr><td>'+DATA.schoolNames[s]+'</td><td>'+c.wins+' / '+c.battles+'</td><td>'+pc(c.winRate)+'</td><td>'+fmt(c.meanTotal)+'<div class="bar"><i style="width:'+100*(c.meanTotal||0)/max+'%"></i></div></td><td>'+fmt(c.powder)+' / '+fmt(c.cannonballs)+' / '+fmt(c.medicine)+'</td><td>'+fmt(c.medianTotal)+' ['+fmt(c.p10Total)+'–'+fmt(c.p90Total)+']</td><td>'+fmt(c.meanWinTurns)+'</td><td class="'+(c.zeroCostWins?'bad':'')+'">'+c.zeroCostWins+'</td><td>'+fmt(c.meanSchoolPlays)+'</td></tr>';}).join('');
const special=selected.slice(1).filter(Boolean),complete=special.length===6,adequate=complete&&special.every(c=>c.winRate>=.8),avg=special.reduce((a,c)=>a+(c.meanTotal||0),0)/6,spread=complete?Math.max(...special.map(c=>c.winRate))-Math.min(...special.map(c=>c.winRate)):null,deviation=avg>0?Math.max(...special.map(c=>Math.abs(c.meanTotal-avg)/avg)):null;
const zero=special.reduce((a,c)=>a+c.zeroCostWins,0);const failed=zero>0||spread>.1||deviation>.15;
$('verdict').className=failed?'bad':'note';$('verdict').textContent=!complete?'该牌组规模缺少六系合法配方，不作均衡判断。':!adequate?'至少一系胜率不足 80%，不能仅凭低消耗认定均衡。零耗材胜局 '+zero+'。':'六系胜率差 '+fmt(spread*100,0)+' 个百分点；总耗材最大偏离 '+(deviation==null?'不适用（均值为 0）':pc(deviation))+'；零耗材胜局 '+zero+'。'+(failed?'未满足建议验收口径。':'此格在建议阈值内；有限样本不能证明全部策略均衡。');}
document.querySelectorAll('select').forEach(s=>s.addEventListener('change',render));render();</script></main></html>`;
fs.writeFileSync(path.join(out,'index.html'),html);
console.log(JSON.stringify({path:path.join(out,'index.html'),rows:rows.length,zeroCostWins:zeroWins.length,overview},null,2));
