import fs from 'node:fs/promises';
const enc = value => String(value).replace(/[&<>\"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','\"':'&quot;'}[c]));
async function csv(path) {
  const lines=(await fs.readFile(path,'utf8')).replace(/^\uFEFF/,'').trim().split(/\r?\n/), heads=lines.shift().split(',');
  return lines.map(line=>{ const values=line.startsWith('"') ? [...line.matchAll(/"((?:""|[^"])*)"/g)].map(m=>m[1].replaceAll('""','"')):line.split(','); return Object.fromEntries(heads.map((h,i)=>[h,values[i]])); });
}
export async function generate(root) {
  const dir=root+'/Fleet-reference28-full', rows=await csv(dir+'/runs.csv'), fleet=await csv(dir+'/FleetCandidate.csv');
  const names=['轻帆船·灰鸥','双桅商船·金潮','巡防舰·铜钟','遗迹猎船·月蚀','打捞船·拾潮','快帆邮船·白羽','补给船·丰穗','重炮舰·雷铸','远洋货驳·盐山','武装商船·誓金','雾行船·夜灯','飞剪商船·逐风'];
  const roles=['初始探索，小船仍可挑战；容量和接舷续航受限。','800金低成本运货；长时间续航打法依赖成熟专卡与少带专家。','低价战斗升级；可用持续治疗过终盘，炮战与效率逊于重炮。','高速探索，14人编制让接舷权重有限。','比商船多35舱且人数相同；慢1节，偏打捞载重。','最快档21节、10人，省水粮；75舱最小，接舷弱。','230舱而仅16人；远航容量好，炮弱。','最高炮甲船体；28人使接舷更强，造价和水粮较高。','330舱最大；22人可兼接舷，但速度10节。','230舱兼24人/24炮；介于商运与重战之间。','21节与22炮偏高速探索；14人限制近战。','280舱/21节兼顾运输周转；5200金最贵，重战逊于专用舰。'];
  const legend=rows.filter(r=>r.enemyId.startsWith('legendary_')), groups={};
  const mean=(rs,k)=>rs.length ? Number((rs.reduce((s,r)=>s+Number(r[k]),0)/rs.length).toFixed(2)):null;
  for (const r of legend) {const k=[r.ship,r.specialists,r.upgrade,r.deck].join('/');(groups[k]??=[]).push(r);}
  const results={};
  for (const [key,group] of Object.entries(groups)) {
    const wins=group.filter(r=>r.outcome==='win'), enemyGroups={};for(const r of group)(enemyGroups[r.enemyId]??=[]).push(r);
    results[key]={wins:wins.length,total:group.length,naval:wins.filter(r=>r.finalStage==='Naval').length,forced:group.filter(r=>r.boardingEntry==='enemy-ram').length,
      ordinary:+group[0].ordinaryCrew,cost:+group[0].upgradeCost,turns:mean(wins,'turns'),hull:mean(wins,'hull'),crew:mean(wins,'crewHealth'),
      hullDamage:mean(wins,'hullDamageTaken'),crewDamage:mean(wins,'crewDamageTaken'),heal:mean(wins,'crewHealed'),guard:mean(wins,'guardAbsorbed'),
      weakest:Math.min(...Object.values(enemyGroups).map(rs=>rs.filter(r=>r.outcome==='win').length))};
  }
  fleet.forEach((s,i)=>{s.name=names[i];s.role=roles[i];s.rations=Math.ceil(Number(s.crew)*.9*8-1e-9);s.supplyVolume=Number((s.rations*.16).toFixed(2));s.freeCargo=Number((Number(s.cargo)-s.supplyVolume).toFixed(2));});
  const footer='本报告为离线Core候选，不是Unity实玩，也未修改生产Assets。每组16种子；两类固定策略只读当前手牌和公开意图，不能证明失败局不存在玩家解法。每场满血、独立补给，未计航行损耗与连续战斗。水粮按满编+厨师8天、每天每人0.9份各取整，余舱仅扣水粮，不扣弹药木药与贸易品。船价不含强化、招募或装备。II/III基础牌实付，来源专卡按360/840航海经验恢复，不代表这些经验已在本轮实际赚取。';
  let text='候选船型成长对照：普通水手 / 28\n'+rows.length+'场，12船×2/3/6专家×II/III×两战术×9首领×16种子。\n'+footer+'\n\n';
  text+='专家：2=佩佩+诺雅；3加伊芙琳；6加巴洛、莫尔、雷萨。普通人数=编制−专家数；缺员惩罚保留。\n';
  text+='II续航牌2520金，III5880；II炮组5760，III13440。不要把不同策略当等价投入。\n\n';
  for(const s of fleet) {
    text+=s.name+' | 船价'+s.price+' | '+s.hull+'船/'+s.armor+'甲/'+s.firepower+'炮 | '+s.speed+'节 | '+s.crew+'人 | '+s.cargo+'舱 | 8日水粮'+s.supplyVolume+'舱，理论余舱'+s.freeCargo+'\n'+s.role+'\n';
    for(const experts of [2,3,6])for(const level of [2,3]) {
      const a=results[[s.id,experts,level,'sustain'].join('/')],b=results[[s.id,experts,level,'focused'].join('/')];
      text+='  '+experts+'专家/'+a.ordinary+'普通 基础'+level+'阶：续航'+a.wins+'/'+a.total+'（最弱'+a.weakest+'/16，胜局'+a.turns+'回合/人受伤'+a.crewDamage+'）；炮组'+b.wins+'/'+b.total+'（纯炮'+b.naval+'，胜局'+b.turns+'回合）\n';
    }
    text+='\n';
  }
  await fs.writeFile(dir+'/overview.txt',text);
  await fs.writeFile(dir+'/ship-benefits.csv','id,name,price,hull,armor,firepower,speed,cargo,crew,waterFor8CookedDays,foodFor8CookedDays,rationVolume,freeCargoBeforeOtherStores\n'+fleet.map(s=>[s.id,s.name,s.price,s.hull,s.armor,s.firepower,s.speed,s.cargo,s.crew,s.rations,s.rations,s.supplyVolume,s.freeCargo].join(',')).join('\n')+'\n');
  const html='<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>船只成长候选 · 28人参照</title><style>body{margin:0;background:#f2f0e9;color:#1b292e;font:15px/1.6 system-ui,"Microsoft YaHei",sans-serif}main{max-width:1400px;margin:auto;padding:36px 28px}h1{font-size:30px;margin:0}h2{margin:32px 0 10px;font-size:22px}.note{background:#e8e0c8;border-left:4px solid #a0792d;padding:12px 18px;margin:16px 0}.meta{color:#52636a}select{padding:7px 12px;margin:0 18px 0 6px;background:white;border:1px solid #b5c1c4;border-radius:5px}.table{overflow:auto;border:1px solid #c6cecd;border-radius:7px;background:#fff}table{border-collapse:collapse;min-width:1050px;width:100%;font-size:14px}th,td{padding:11px 10px;border-bottom:1px solid #dde2df;text-align:left;white-space:nowrap}th{background:#234750;color:white}td:nth-child(1){font-weight:600}.good{color:#176848;font-weight:600}.low{color:#a03c26;font-weight:600}.mid{color:#926817;font-weight:600}.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(290px,1fr));gap:12px}.card{background:#fff;padding:16px;border:1px solid #d8dfd9;border-radius:7px}.card h3{margin:0 0 8px}.tiny{font-size:13px;color:#58696e}.cost{padding:8px 0;color:#58696e}a{color:#14677b}footer{margin-top:24px;padding-top:16px;border-top:1px solid #aebeba}</style><main><h1>船只成长候选 · 28人参照</h1><p class="meta">'+rows.length.toLocaleString()+'场离线模拟 · 12艘船 · 16种子/敌人/构筑 · 2026-10-03</p><div class="note"><b>仅实验快照，尚不是生产版本。</b>不要求固定换船次数，可攒钱跨级购买。小船仍能挑战，目标是让额外船体、船员和火炮带来实际收益。</div><p>接舷伤害、格挡和治疗按 clamp(普通水手 ÷ 28, 0.3, 1.6) 缩放，四舍五入。角色来源牌使用相应 II/III 航行经验；厨师、船医当前不提供专属战斗牌。</p><h2>五传奇表现</h2><label>专家人数<select id="crew"><option>2</option><option>3</option><option>6</option></select></label><label>基础牌强化<select id="level"><option value="2">II</option><option value="3">III</option></select></label><div class="cost" id="cost"></div><div class="table"><table><thead><tr><th>船型 / 船价</th><th>普通人数</th><th>续航胜场</th><th>最难传奇</th><th>续航回合</th><th>人伤 / 回血</th><th>炮组胜场</th><th>纯炮击杀</th><th>炮组回合</th></tr></thead><tbody id="body"></tbody></table></div><p class="tiny">每列分母80=5传奇×16种子；回合/受伤/回血仅胜局均值。炮组胜场包含被迫接舷，纯炮击杀另列，不能当成海战胜率。比较相同构筑投入，不按种子挑选最优策略。</p><h2>每船运货与航行用途</h2><div class="cards">'+fleet.map(s=>'<section class="card"><h3>'+enc(s.name)+'</h3><b>'+s.price+'金 · '+s.cargo+'舱 · '+s.speed+'节 · '+s.crew+'人</b><p>'+enc(s.role)+'</p><div class="tiny">8天厨师水粮：各'+s.rations+'份，占'+s.supplyVolume+'舱<br>仅扣水粮后的装货上限：'+s.freeCargo+'舱<br>船体'+s.hull+' / 甲'+s.armor+' / 炮'+s.firepower+'</div></section>').join('')+'</div><h2>结论与边界</h2><p>800金商船可以用更高强化、更少专家和较长战斗争取终盘；巡防保留便宜战斗路线；重炮以更短回合、更小承伤和更可靠炮击体现价格。免费重复治疗仍会放大长回合构筑，不把优化构筑的胜利一律视作问题。</p><p>'+enc(footer)+'</p><footer><a href="runs.csv">逐场数据</a> · <a href="group-summary.csv">按敌人汇总</a> · <a href="formations.csv">实际编组/缺员/专卡</a> · <a href="ship-benefits.csv">货舱与水粮</a> · <a href="overview.txt">文本结论</a> · <a href="FleetCandidate.csv">候选船型表</a></footer></main><script>const fleet='+JSON.stringify(fleet)+',results='+JSON.stringify(results)+';function render(){const c=document.querySelector("#crew").value,l=document.querySelector("#level").value;document.querySelector("#cost").textContent=l==="2"?"续航基础牌2520金；炮组5760金。来源牌经验360，非金币购买。":"续航基础牌5880金；炮组13440金。来源牌经验840，非金币购买。";document.querySelector("#body").innerHTML=fleet.map(s=>{const a=results[[s.id,c,l,"sustain"].join("/")],b=results[[s.id,c,l,"focused"].join("/")];const cls=n=>n===80?"good":n<40?"low":"mid";return "<tr><td>"+s.name+" / "+s.price+"金</td><td>"+a.ordinary+"</td><td class=\""+cls(a.wins)+"\">"+a.wins+"/80</td><td>"+a.weakest+"/16</td><td>"+(a.turns??"—")+"</td><td>"+(a.crewDamage??"—")+" / "+(a.heal??"—")+"</td><td class=\""+cls(b.wins)+"\">"+b.wins+"/80</td><td>"+b.naval+"/80</td><td>"+(b.turns??"—")+"</td></tr>";}).join("");}document.querySelectorAll("select").forEach(e=>e.addEventListener("change",render));render();</script></html>';
  await fs.writeFile(dir+'/overview.html',html);
  return {rows:rows.length,ships:fleet.length,groups:Object.keys(results).length,html:dir+'/overview.html'};
}
