'use strict';
// Offline authoring helper. Only explicit integer card fields are candidates;
// the game never loads this model or applies a school-wide multiplier.
const fs=require('node:fs'),path=require('node:path'),p=require('./power-model.cjs');
const root=path.resolve(__dirname,'../..'),out=path.join(root,'Artifacts/CardBalanceReview20261012');
const reference=p.readTsv(path.join(root,'UnityClient/Artifacts/CardBalanceReview20261012/CoreBefore/cards.tsv'));
const model=p.buildModel(reference),bases=reference.filter(r=>r.UpgradeLevel===0&&r.SchoolId);
const e=k=>'SchoolEffect.'+k;
function ratios(r){return [0,1,2,3].map(l=>p.evaluateRow(p.upgradeRow(r,l),model).ratio);}
function prepare(base){
 const r={...base},m=r[e('Mechanic')];
 if(m==='FireLoad')r[e('Upgrades')]='Power';
 if(m==='BloodMend')r[e('Upgrades')]='Power, Limit';
 if(m==='BloodRisk'||m==='BloodWard')r[e('Upgrades')]=[...new Set(r[e('Upgrades')].split(', ').concat('Threshold'))].filter(x=>x!=='None').join(', ');
 // Marks are discrete resources; rank 1 increases their weakness, not their count.
 if(m==='CurseMark')r[e('Upgrades')]=[...new Set(r[e('Upgrades')].split(', ').concat('Secondary'))].filter(x=>x!=='None').join(', ');
 return r;
}
function fields(r){
 const m=r[e('Mechanic')],a=[];
 for(const k of ['Damage','Block','Repair'])if(r[k]>0)a.push([k,Math.max(1,Math.floor(r[k]*.25)),Math.max(28,Math.ceil(r[k]*1.8))]);
 if(r[e('Power')]>0){
  let lo=1,hi=Math.max(28,Math.ceil(r[e('Power')]*1.8));
  if(['ArcaneCharge'].includes(m)){lo=1;hi=8;}
  if(m==='CurseMark'){lo=1;hi=3;}
  if(m==='ThunderPrime'){lo=1;hi=4;}
  if(['ArcaneEcho','ThunderEcho'].includes(m)){lo=1;hi=3;}
  if(['FireExtend'].includes(m)){lo=r[e('Power')];hi=lo;}
  if(m==='BloodMend'){lo=10;hi=50;}
  if(m==='BloodDrain'){lo=15;hi=65;}
  a.push([e('Power'),lo,hi]);
 }
 if(m==='FireBoardingSeed')a.push([e('Secondary'),1,20]);
 if(m==='BloodMend'||m==='BloodDrain')a.push([e('Limit'),6,40]);
 if(['ArcaneRelease','ArcaneWard','ArcaneMend','ArcaneEcho','CurseDrain'].includes(m))a.push([e('Threshold'),1,r[e('Threshold')]]);
 if(m==='CurseMark')a.push([e('Secondary'),4,4]);
 const cycleNames=['刻印誓敌','风眼潮势','烟幕退帆','幽帆牵制','幽流换位','巨肩承潮','雾幕立誓','灰烬借风','雾铁偿伤','隔热掩护','护身刻印'];
 if(cycleNames.includes(r.Name)&&r.Draw===0)a.push(['Draw',0,1]);
 if(m==='CurseMark'&&r.Damage===0&&r.Block===0){const i=a.findIndex(x=>x[0]==='Draw');if(i>=0)a.splice(i,1);a.push(['Draw',1,2]);}
 // Healing remains useful with modest protection; existing pure recovery cards
 // may gain a small explicitly authored guard amount rather than wasteful overheal.
 if(['ArcaneMend','TideRenew'].includes(m)&&r.Block===0)a.push(['Block',0,16]);
 if(r.MovementMeters>0)a.push([e('Movement'),r[e('Movement')]<0?-120:30,r[e('Movement')]<0?-30:120]);
 return a;
}
function objective(r,original){
 const v=ratios(r),errors=v.map(x=>Math.abs(x-1.3));
 let score=errors.reduce((s,x)=>s+x*x,0)+errors[0]**2+Math.max(...errors)**2;
 for(const [key]of fields(original))score+=0.003*((r[key]-original[key])/Math.max(3,Math.abs(original[key])))**2;
 return score;
}
function descend(original,start){
 let best={...start},bestScore=objective(best,original);
 // Deterministic coordinate search, keeping every original effect and its role.
 for(let pass=0;pass<12;pass++){
  let changed=false;
  for(const [key,lo,hi]of fields(original)){
   let next=best,score=bestScore;
   for(let value=lo;value<=hi;value+=(key===e('Movement')?10:1)){
    const candidate={...best,[key]:value},s=objective(candidate,original);
    if(s+1e-10<score){next=candidate;score=s;}
   }
   if(next!==best){best=next;bestScore=score;changed=true;}
  }
  if(!changed)break;
 }
 return best;
}
function tune(base){
 const original=prepare(base),starts=[original];
 const thresholds=fields(original).find(x=>x[0]===e('Threshold'));
 if(thresholds)for(let t=thresholds[1];t<=thresholds[2];t++)starts.push({...original,[e('Threshold')]:t});
 for(const scale of [.55,.8,1.25,1.6]){
  const r={...original};for(const [key,lo,hi]of fields(original))
   if(['Damage','Block','Repair',e('Power')].includes(key))r[key]=Math.max(lo,Math.min(hi,Math.round(original[key]*scale)));
  starts.push(r);
 }
 const drawRange=fields(original).find(x=>x[0]==='Draw');
 if(drawRange)for(const start of starts.slice())for(let draw=drawRange[1];draw<=drawRange[2];draw++)starts.push({...start,Draw:draw});
 const results=starts.map(start=>descend(original,start)).sort((a,b)=>objective(a,original)-objective(b,original));
 return results[0];
}
const candidates=bases.map(tune);
fs.mkdirSync(out,{recursive:true});fs.writeFileSync(path.join(out,'school-candidates.json'),JSON.stringify(candidates,null,2));
const audit=p.audit(p.expandUpgrades(candidates),model);
fs.writeFileSync(path.join(out,'school-candidate-audit.json'),JSON.stringify(audit,null,2));
console.log(JSON.stringify({schools:audit.schools.filter(x=>x.scenario==='typical'),outliers:candidates.map(r=>({id:r.Id,name:r.Name,m:r[e('Mechanic')],ratios:ratios(r),changes:Object.fromEntries(Object.entries(r).filter(([k,v])=>v!==bases.find(b=>b.Id===r.Id)[k]))})).filter(r=>r.ratios.some(x=>Math.abs(x-1.3)>.1))},null,2));
