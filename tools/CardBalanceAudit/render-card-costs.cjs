'use strict';
const fs=require('node:fs'),path=require('node:path');
const root=path.resolve(__dirname,'../..');
const dir=path.join(root,'Artifacts/CardDeckSupplyAudit20261012');
const file=path.join(root,'UnityClient/Assets/_Project/Core/Runtime/CoreSchoolCardCatalog.cs');
const source=fs.readFileSync(file,'utf8');
const names={fire:'火焰',arcane:'秘术',curse:'诅咒',tide:'潮汐',thunder:'雷霆',blood:'血契'};
const stageNames={Preparation:'准备',Naval:'炮战',Boarding:'接舷',Ship:'准备／炮战',All:'全部阶段'};
const cards=[];
// Read the live authored C# table, with its exact fixed constructor schema.
const str='("(?:\\\\.|[^"\\\\])*")';
const re=new RegExp('new CoreBattleCard\\('+[str,str,str,'(\\d+)','(\\d+)','(\\d+)','(\\d+)','(\\d+)','(\\d+)',str,'S\\.(\\w+)','A\\.(\\w+)',str,str,'(\\d+)',str,'(\\d+)',str].join(',\\s*')+',\\s*new CoreSchoolCardEffect\\(M\\.(\\w+), power: (\\d+), secondary: (\\d+), threshold: (\\d+), duration: (\\d+), limit: (\\d+), movement: (-?\\d+), upgrades: ([^)]+)\\)\\)');
for(const line of source.split(/\r?\n/)){
  if(!line.includes('new CoreBattleCard("'))continue;
  const m=line.match(re);if(!m)throw Error('Unparsed card: '+line);
  const v=m.slice(1),q=i=>JSON.parse(v[i]),n=i=>Number(v[i]);
  cards.push({id:q(0),name:q(1),type:q(2),energy:n(3),damage:n(4),block:n(5),repair:n(6),draw:n(7),powder:n(8),template:q(9),stage:v[10],action:v[11],crew:q(12),art:q(13),balls:n(14),component:q(15),school:q(17),mechanic:v[18],power:n(19),secondary:n(20),threshold:n(21),duration:n(22),limit:n(23),movement:n(24),upgrades:v[25].split('|').map(s=>s.trim().replace('U.',''))});
}
if(cards.length!==132||new Set(cards.map(c=>c.id)).size!==132)throw Error('Expected 132 unique live school cards');
function extraCost(c,rank=0){
  const t=c.threshold*(rank>0&&c.upgrades.includes('Threshold')?2:1);
  if(['ArcaneRelease','ArcaneWard','ArcaneMend','ArcaneEcho'].includes(c.mechanic))return `${t} 法力`;
  if(c.mechanic==='CurseDrain')return `${t} 层咒印`;
  if(c.mechanic==='CurseDetonate')return '清空全部咒印及其减伤状态（0 层也可打出）';
  if(c.mechanic==='ThunderFinish')return '清空全部连锁（0 层也可打出）';
  if(['BloodRisk','BloodWard'].includes(c.mechanic))return `${t} 当前阶段生命；支付后至少剩 1`;
  return '无';
}
function medicine(c){
  if(c.mechanic==='BloodMend')return '不耗药';
  const canHeal=c.repair>0||['FireHeal','ArcaneMend','CurseDrain','TideRenew'].includes(c.mechanic);
  return canHeal&&['Boarding','All'].includes(c.stage)?'条件性自动消耗 1；无药仍可使用':'不耗药';
}
function conditions(c){
  const a=[];if(c.action==='Cannon')a.push('距离 ≤350 米');
  else if(c.action==='Sabotage')a.push('距离 ≤150 米');
  else if(c.stage==='Naval'&&c.damage>0)a.push('距离 ≤350 米');
  const t=Math.max(1,c.threshold);
  if(['FireExploit','FireGuard','FireHeal','FireBoardingCover'].includes(c.mechanic))a.push('燃烧为加成条件，不消耗燃烧');
  if(['FireIgnite','FireIgniteOrExtend','FireBoardingSeed','FireExtraTick'].includes(c.mechanic))a.push('点燃需造成有效直伤');
  if(c.mechanic==='FireExtend')a.push('需已有燃烧才能延长，攻击牌需有效命中');
  if(c.mechanic==='FireNaturalGuard')a.push('自然燃烧造成伤害后触发');
  if(c.mechanic==='FireScorch')a.push('预置有限次数额外灼伤；之后命中燃烧目标扣 1 次');
  if(c.mechanic==='FireOverflow')a.push('满额延烧触发增益，每回合首次');
  if(['CurseExploit','CurseWard'].includes(c.mechanic))a.push(`至少 ${t} 层咒印才获加成，不扣层数`);
  if(['TideRiposte','TideRenew'].includes(c.mechanic))a.push(`上次有效格挡 ≥${t} 才获加成，不消耗该记录`);
  if(c.mechanic==='TideWard')a.push(`有效格挡 ≥${t} 触发反击；不支付格挡值`);
  if(c.mechanic==='TideShift')a.push('切换姿态，不消耗潮汐资源');
  if(['ThunderChain','ThunderWard','ThunderEcho'].includes(c.mechanic))a.push(`此前连锁 ≥${t} 才获加成，不扣层数`);
  if(c.mechanic==='ThunderEcho')a.push('额外抽牌共用每回合一次触发标记');
  if(c.mechanic==='BloodExecute')a.push(`敌方生命 ≤${c.threshold}% 才获加成；不支付自身生命`);
  if(c.mechanic==='BloodDrain')a.push('按有效直伤吸血，不支付生命或药品');
  if(c.mechanic==='BloodMend')a.push('按已失生命回复，不支付生命或药品');
  return a.join('；')||'无额外触发门槛';
}
for(const c of cards){
  c.sourceKind=c.crew?'船员':c.component?'部件':'基础';
  c.mandatoryExtra=extraCost(c);c.medicine=medicine(c);c.conditions=conditions(c);
  c.currentRankCosts=[0,1,2,3].map(rank=>({rank,energy:Math.max(0,c.energy-(rank>=2?1:0)),extra:extraCost(c,rank)}));
}
const summaries=Object.keys(names).map(s=>({school:s,name:names[s],total:cards.filter(c=>c.school===s).length,basic:cards.filter(c=>c.school===s&&c.sourceKind==='基础').length,ammoCards:cards.filter(c=>c.school===s&&c.powder+c.balls>0).length,medicineCards:cards.filter(c=>c.school===s&&c.medicine.startsWith('条件')).length}));
fs.writeFileSync(path.join(dir,'school-card-costs.json'),JSON.stringify({scope:'Live code, six schools; base and source cards. New 1/1.2/1.75/3 proposal and AP/draw cards have not been implemented.',source:path.relative(root,file),summaries,cards},null,2));
const esc=s=>String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('"','&quot;');
function row(c){return `<tr data-school="${c.school}" data-kind="${c.sourceKind}"><td><strong>${esc(c.name)}</strong><br><small>${names[c.school]} · ${c.sourceKind} · ${stageNames[c.stage]}</small></td><td>${c.energy}</td><td>${c.powder}</td><td>${c.balls}</td><td>${esc(c.mandatoryExtra)}</td><td>${esc(c.medicine)}</td><td>${esc(c.conditions)}</td><td><details><summary>旧强化费用／来源</summary><p>${c.currentRankCosts.map(r=>`+${r.rank}：${r.energy} 行动；${r.extra}`).join('<br>')}</p><small>${esc(c.crew||c.component||'基础卡，每种库存 2 张')}<br>${esc(c.id)}</small></details></td></tr>`;}
const html=`<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>六体系逐卡使用代价</title><style>:root{color-scheme:dark}body{margin:0;background:#101b24;color:#e4edf0;font:15px/1.65 'Microsoft YaHei',sans-serif}main{max-width:1500px;margin:auto;padding:35px 28px}h1{font-size:30px}a{color:#7fd0bb}.panel{background:#182a37;border:1px solid #304753;border-radius:12px;padding:20px;margin:20px 0}.filters{display:flex;gap:15px;flex-wrap:wrap}select,input{font:inherit;background:#101b24;color:inherit;border:1px solid #456070;border-radius:5px;padding:7px}small,.note{color:#a7bac4}table{border-collapse:collapse;width:100%;font-size:14px}th,td{padding:12px 10px;border-bottom:1px solid #304753;text-align:left;vertical-align:top}th{white-space:nowrap;color:#7fd0bb;position:sticky;top:0;background:#182a37}td:first-child{min-width:160px}td:nth-child(5),td:nth-child(6){min-width:130px}td:nth-child(7){min-width:240px}td:nth-child(8){min-width:160px}details{max-width:300px}small{overflow-wrap:anywhere}tbody tr:hover{background:#203744}.tablewrap{overflow:auto;max-height:75vh}.note{font-size:14px}li{margin:8px 0}button{cursor:pointer}</style><main><p class="note">第七港口 · 当前源码费用清单 · 2026.10.12</p><h1>六个体系，132 张牌的实际使用代价</h1><p>60 张基础牌＋72 张船员／部件来源牌。主表按<strong>未强化原卡、本回合首次使用</strong>列出；费用与触发条件分别展示。新倍率 1／1.2／1.75／3 及新回费／抽牌基础牌尚未写入代码，不混入当前结果。</p><div class="panel"><ul><li><strong>行动点：</strong>当前旧规则 +0／+1 为原费用，+2／+3 减 1（最低 0）。同 ID 每成功打出一次，下次在本回合加 1 行动点；两份同名牌共享计数。阶段切换会重置。</li><li><strong>药品：</strong>符合条件的接舷治疗，在有药且基础回复不足以填满伤口时自动扣 1 药并获得额外回复；无药仍可使用。不存在“所有治疗都必须付药”的规则。血契吸血、按已失生命回复不耗药。</li><li><strong>血契生命：</strong>准备／炮战扣船体，接舷扣船员生命；必须足额支付且至少留 1。当前有付血标记的牌从 +1 起支付量翻倍，见每行展开信息；不把斩杀血线误作支付。</li><li><strong>炮击附带消耗：</strong>表内粉弹为必付。若已有附火储备，每次炮击另扣 1 发附火；炮击清空装填增伤。获得附火的卡本身不因此扣物品。已预置的额外灼伤次数在命中燃烧目标时消耗。</li><li><strong>来源与条件：</strong>来源牌须携带对应船员／部件，但打出不会消耗该船员／部件。距离、敌人燃烧、历史格挡值、连锁阈值等属于条件，不统一视为资源费用。</li></ul></div><div class="panel"><div class="filters"><label>体系 <select id="school"><option value="">全部</option>${Object.entries(names).map(([k,v])=>`<option value="${k}">${v}</option>`).join('')}</select></label><label>来源 <select id="kind"><option value="">全部</option><option>基础</option><option>船员</option><option>部件</option></select></label><label>搜索 <input id="query" placeholder="牌名或费用关键词"></label><span id="count"></span></div><div class="tablewrap"><table><thead><tr><th>卡牌</th><th>行动</th><th>火药</th><th>炮弹</th><th>额外必付／清空</th><th>药品</th><th>触发与使用条件</th><th>等级费用／来源</th></tr></thead><tbody>${Object.keys(names).flatMap(s=>cards.filter(c=>c.school===s)).map(row).join('')}</tbody></table></div></div><p><a href="school-card-costs.json">完整结构化数据</a> · <a href="index.html">返回五档木桩审核</a></p><p class="note">来源：CoreSchoolCardCatalog.cs、CoreSchoolBattleExecution.cs、CoreBattleCardEffects.cs、CoreNavalBattleCombat.cs、CoreBattleCardUpgrades.cs。当前每个体系 22 张，其中基础 10 张、来源 12 张。</p></main><script>const $=id=>document.getElementById(id);function filter(){let n=0;for(const row of document.querySelectorAll('tbody tr')){const yes=(!$('school').value||row.dataset.school===$('school').value)&&(!$('kind').value||row.dataset.kind===$('kind').value)&&row.textContent.includes($('query').value.trim());row.hidden=!yes;if(yes)n++;}$('count').textContent=n+' / 132 张';}document.querySelectorAll('select,input').forEach(e=>e.addEventListener('input',filter));filter();</script></html>`;
fs.writeFileSync(path.join(dir,'school-card-costs.html'),html);
console.log(JSON.stringify({output:path.join(dir,'school-card-costs.html'),summaries},null,2));
