'use strict';
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),p=require('./power-model.cjs');
const root=path.resolve(__dirname,'../..'),out=path.join(root,'Artifacts/CardBalanceReview20261012');
const beforePath=path.join(root,'UnityClient/Artifacts/CardBalanceReview20261012/CoreBefore/cards.tsv');
const afterPath=path.join(root,'UnityClient/Artifacts/CardBalanceReview20261012/CoreAfter/cards.tsv');
const before=p.readTsv(beforePath),after=p.readTsv(afterPath),model=p.buildModel(before),audit=p.audit(after,model);
const names={ordinary:'普通',fire:'火焰',arcane:'奥术',curse:'咒印',tide:'潮汐',thunder:'雷霆',blood:'血契'};
const stageLabel=s=>({Preparation:'准备',Naval:'炮战',Boarding:'接舷',Ship:'准备／炮战',All:'全阶段'}[s]||s);
const pct=n=>(n*100).toFixed(1)+'%',esc=s=>String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const fields=['Energy','Damage','Block','Repair','Draw','PowderCost','CannonballCost','MovementMeters','StrategyAmount','StrategyLimit','BurnDamage','BurnDuration',...['Power','Secondary','Threshold','Duration','Limit','Movement','Upgrades'].map(x=>'SchoolEffect.'+x)];
const mean=xs=>xs.reduce((a,b)=>a+b,0)/xs.length;
const cards=after.filter(x=>x.UpgradeLevel===0).map(base=>{
 const old=before.find(x=>x.Id===base.Id&&x.UpgradeLevel===0),ranks=[0,1,2,3].map(l=>after.find(x=>x.Id===base.Id&&x.UpgradeLevel===l));
 const values=audit.cards.filter(x=>x.id===base.Id&&x.scenario==='typical');
 return {id:base.Id,name:base.Name,school:base.SchoolId||'ordinary',stages:base.AllowedStages,source:base.RequiredCrew||base.RequiredComponent||'基础牌',
  changed:fields.some(f=>base[f]!==old[f]),changes:Object.fromEntries(fields.filter(f=>base[f]!==old[f]).map(f=>[f,{before:old[f],after:base[f]}])),
  before:old,after:base,ranks,ratios:values.map(x=>x.ratio),average:mean(values.map(x=>x.ratio)),
  low:mean(audit.cards.filter(x=>x.id===base.Id&&x.scenario==='low').map(x=>x.ratio)),high:mean(audit.cards.filter(x=>x.id===base.Id&&x.scenario==='high').map(x=>x.ratio))};
});
const schools=Object.keys(names).map(s=>({id:s,name:names[s],count:cards.filter(c=>c.school===s).length,
 ranks:[0,1,2,3].map(l=>audit.schools.find(x=>x.school===s&&x.scenario==='typical'&&x.level===l).ratio),
 average:mean(cards.filter(c=>c.school===s).map(c=>c.average))}));
const exceptions=cards.filter(c=>c.school!=='ordinary'&&(c.average<1.2||c.average>1.4));
const metadata={status:'待用户审核；未打包',cardCount:cards.length,changedCount:cards.filter(c=>c.changed).length,modelVersion:model.version,
 baselineSha256:crypto.createHash('sha256').update(fs.readFileSync(beforePath)).digest('hex'),candidateSha256:crypto.createHash('sha256').update(fs.readFileSync(afterPath)).digest('hex'),
 schools,perCardFourRankTarget:[1.2,1.4],exceptions:exceptions.map(c=>({name:c.name,id:c.id,average:c.average})),singleRankOutside20To40:audit.outsideCardTolerance.filter(c=>c.school!=='ordinary').length,
 limitations:audit.limitations};
fs.mkdirSync(out,{recursive:true});
fs.writeFileSync(path.join(out,'review-summary.json'),JSON.stringify(metadata,null,2));
fs.writeFileSync(path.join(out,'full-power-audit.json'),JSON.stringify({...audit,parameters:model.params,scenarios:model.scenarios,shadowPrices:model.shadowByLevel},null,2));
fs.writeFileSync(path.join(out,'card-changes.json'),JSON.stringify(cards,null,2));
const table=schools.map(s=>`<tr><th>${s.name}</th><td>${s.count}</td>${s.ranks.map(v=>`<td>${pct(v)}</td>`).join('')}<td><b>${pct(s.average)}</b></td></tr>`).join('');
const rows=cards.sort((a,b)=>Object.keys(names).indexOf(a.school)-Object.keys(names).indexOf(b.school)||a.after.LibraryIndex-b.after.LibraryIndex).map(c=>`<article data-school="${c.school}" data-changed="${c.changed}" data-name="${esc(c.name+' '+c.id)}"><details><summary><span class="name">${esc(c.name)}</span><span>${names[c.school]} · ${c.stages}</span><span class="ratio">${pct(c.average)}</span><span>${c.changed?'有调整':'保留'}</span></summary><p class="id">${esc(c.id)} · 来源 ${esc(c.source)}</p><div class="diff"><div><h4>调整前 · 原卡</h4><p>费用 ${c.before.Energy}</p>${esc(c.before.Description)}</div><div><h4>调整后 · 原卡</h4><p>费用 ${c.after.Energy}</p>${esc(c.after.Description)}</div></div><table><thead><tr><th>等级</th><th>费用</th><th>预期强度</th><th>新卡面说明</th></tr></thead><tbody>${c.ranks.map((r,i)=>`<tr><td>${i===0?'原卡':'+'+i}</td><td>${r.Energy}</td><td>${pct(c.ratios[i])}</td><td>${esc(r.Description)}</td></tr>`).join('')}</tbody></table><p>低触发场景 ${pct(c.low)}；高触发场景 ${pct(c.high)}。这是条件敏感性，不是胜率范围。</p><details><summary>逐字段数据变化</summary><pre>${esc(JSON.stringify(c.changes,null,2))}</pre></details></details></article>`).join('');
const html=`<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>216 张卡牌数值审核</title><style>body{margin:0;background:#f3f0e8;color:#24353a;font:16px/1.65 'Microsoft YaHei',sans-serif}main{max-width:1180px;margin:auto;padding:36px 28px}h1{font-size:34px;margin:0}h2{margin-top:32px}p{max-width:1000px}.badge{display:inline-block;border-radius:30px;padding:4px 14px;background:#eadbbb;color:#654316}.panel,article{background:#fffdf7;border:1px solid #d6d6c9;border-radius:10px;padding:18px;margin:14px 0}table{width:100%;border-collapse:collapse;font-size:14px}th,td{padding:10px;text-align:left;border-bottom:1px solid #ddd;vertical-align:top}th{color:#4d6768}summary{cursor:pointer}article>details>summary{display:grid;grid-template-columns:2fr 2fr 1fr 1fr;gap:14px;align-items:center}.name{font-weight:bold;font-size:18px}.ratio{font-weight:bold;color:#176b6b}.id{font-size:12px;color:#637477;overflow-wrap:anywhere}.diff{display:grid;grid-template-columns:1fr 1fr;gap:24px}.diff>div{padding:14px;background:#f3f2e9;border-radius:6px}h4{margin:0}.filters{position:sticky;top:0;background:#f3f0e8ee;padding:12px 0;display:flex;gap:12px;z-index:2}input,select{padding:8px;border:1px solid #9bb0ac;border-radius:5px;font-size:15px}input[type=search]{flex:1}pre{white-space:pre-wrap;font-size:12px}.note{border-left:4px solid #ac8134;padding-left:16px}.muted{color:#637477}a{color:#176b6b}@media(max-width:700px){main{padding:18px}.diff{grid-template-columns:1fr}article>details>summary{grid-template-columns:1fr 1fr}.filters{flex-wrap:wrap}table{font-size:12px}}</style><main><span class="badge">待你审核 · 尚未打包</span><h1>216 张卡牌数值审核</h1><p>普通 84 张，六个特殊体系各 22 张；逐卡审核原卡和 +1／+2／+3，共 864 个卡牌等级。${metadata.changedCount} 张有数值调整。沿用已取消普通伤害额外 ×2 的原始数据基准，链弹基础伤害仍为 8。</p><div class="panel"><h2>各体系的预期收益</h2><p>100% 为冻结的同费用、同阶段、同职能普通牌中位值；特殊体系目标约 130%。四阶平均用于比较体系整体，单阶和单卡差异在下表中完整展开。</p><table><thead><tr><th>体系</th><th>张数</th><th>原卡</th><th>+1</th><th>+2</th><th>+3</th><th>四阶均值</th></tr></thead><tbody>${table}</tbody></table></div><p class="note">这些数字是统一条件下的预期收益估算，不代表所有对局都恰好强 30%，也不代表胜率提升 30%。缺血不足、资源不足、护甲吸收、燃烧持续时间和出牌顺序会改变实际收益。${exceptions.length} 张特殊牌的四阶均值仍在 120%–140% 之外；${metadata.singleRankOutside20To40} 个特殊牌等级在该范围之外，保留在审核数据中。</p><details class="panel"><summary>计算方法与可复核资料</summary><p>原始普通基准固定，不用调整后的特殊牌反推目标。伤害按 1，格挡按 0.78，治疗按实际缺血封顶后 ×0.8，抽牌按 2.5；持续效果观察 3 个敌回合，后续回合按 0.8 折现。法力、咒印、雷链计入生成收益及消耗机会成本，血契扣除实际生命代价。准备雷链进入炮战清零。所有权重、场景、逐卡收益分项均可复核。</p><p><a href="review-summary.json">审核汇总 JSON</a> · <a href="card-changes.json">216 张逐字段变化</a> · <a href="full-power-audit.json">完整模型与分项结果</a> · <a href="verification-summary.json">验证结果</a></p><p>模型 ${esc(model.version)}；基础 TSV SHA-256：${metadata.baselineSha256}；候选 TSV SHA-256：${metadata.candidateSha256}。</p></details><h2>逐卡审阅</h2><p>点击卡名展开调整前后、四阶卡面与数值变化。显示的文案来自实际编译导出的卡库。</p><div class="filters"><input id="q" type="search" placeholder="搜索名称或卡牌 ID"><select id="school"><option value="">全部体系</option>${Object.entries(names).map(([k,v])=>`<option value="${k}">${v}</option>`).join('')}</select><label><input id="changed" type="checkbox">只看调整</label><span id="count"></span></div>${rows}<p class="muted">审核通过后再打包。本页与旧发布包独立，旧发布包没有包含本轮修改。</p></main><script>const q=document.getElementById('q'),school=document.getElementById('school'),changed=document.getElementById('changed');function filter(){let n=0;document.querySelectorAll('article').forEach(a=>{const show=(!q.value||a.dataset.name.toLowerCase().includes(q.value.toLowerCase()))&&(!school.value||school.value===a.dataset.school)&&(!changed.checked||a.dataset.changed==='true');a.hidden=!show;if(show)n++});document.getElementById('count').textContent=n+' 张';}q.oninput=school.onchange=changed.onchange=filter;filter();</script></html>`;
const battlePath=path.join(root,'UnityClient/Artifacts/CardBalanceReview20261012/battle-review-summary.json');
let battleHtml='';
if(fs.existsSync(battlePath)){
 const battle=JSON.parse(fs.readFileSync(battlePath,'utf8').replace(/^\uFEFF/,''));
 fs.copyFileSync(battlePath,path.join(out,'battle-review-summary.json'));
 const battleRows=Object.entries(names).map(([id,name])=>{
  const r=[0,1].map(rank=>battle.lowRankSchoolComparison.find(x=>x.school===id&&x.tactic==='all'&&Number(x.rank)===rank));
  return `<tr><th>${name}</th>${r.map(x=>`<td>${pct(Number(x.winRate))}</td><td>${Number(x.commonWinTurnRatio).toFixed(2)}×</td>`).join('')}</tr>`;
 }).join('');
 battleHtml=`<section class="panel"><h2>实际战斗对照</h2><p>相同 16 个种子、12 种真实敌人、四个等级、炮战及接舷两种策略，修改前后合计 ${battle.totalBeforeAndAfterBattles.toLocaleString('zh-CN')} 局；60 张体系基础牌和 126 张现行来源牌均有真实出牌记录。没有非法出牌、超弹药预算、终局负生命或循环超时。</p><table><thead><tr><th>体系</th><th>原卡胜率</th><th>原卡取胜速度</th><th>+1 胜率</th><th>+1 取胜速度</th></tr></thead><tbody>${battleRows}</tbody></table><p>速度为与普通组共同获胜的同一对局中，普通耗时÷该体系耗时；大于 1 表示更快。火、雷偏速攻，奥术、咒印、潮汐偏持久战，实战仍有体系差异。+2 胜率约 98%–100%，+3 全胜，存在饱和，不能用这些胜率证明强度完全相等。</p><p>固定策略不会为每个来源机制专门规划连招，例如「熔鳞战阵」的灼伤后续攻击；这类来源牌结果保留供人工体验复核。<a href="battle-review-summary.json">完整战斗摘要与限制</a></p></section>`;
}
const verificationPath=path.join(out,'verification-summary.json');
let verificationHtml='';
if(fs.existsSync(verificationPath)){
 const v=JSON.parse(fs.readFileSync(verificationPath,'utf8').replace(/^\uFEFF/,''));
 verificationHtml=`<section class="panel"><h2>验证与发布状态</h2><p>${esc(v.summary)}</p><p><a href="verification-summary.json">验证明细</a> · <a href="数值口径与例外.txt">37 张逐阶例外及完整口径</a> · <a href="PersistentPreviewsFinal02/index.html">23 个保存预览</a></p></section>`;
}
const finalHtml=html.replace('<h2>逐卡审阅</h2>',battleHtml+verificationHtml+'<h2>逐卡审阅</h2>')
 .replace(/ · (Preparation|Naval|Boarding|Ship|All)(?=<\/span>)/g,(_,s)=>' · '+stageLabel(s))
 .replace(/\\n/g,' ');
fs.writeFileSync(path.join(out,'index.html'),finalHtml);
console.log(JSON.stringify(metadata,null,2));
