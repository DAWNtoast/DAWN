'use strict';
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../..');
const out = path.join(root, 'Artifacts/CardDeckSupplyAudit20261012');
const schoolNames = {ordinary:'普通',fire:'火焰',arcane:'秘术',curse:'诅咒',tide:'潮汐',thunder:'雷霆',blood:'血契'};
const schools = Object.keys(schoolNames);
function tsv(file) {
  const lines = fs.readFileSync(file,'utf8').trim().split(/\r?\n/);
  const headers = lines.shift().replace(/^\uFEFF/,'').split('\t');
  return lines.map(line => Object.fromEntries(line.split('\t').map((v,i)=>[headers[i],v])));
}
function csv(file) {
  const text=fs.readFileSync(file,'utf8').replace(/^\uFEFF/,'');
  let rows=[], row=[], value='', quoted=false;
  for(let i=0;i<text.length;i++) {
    const c=text[i];
    if(c==='"') { if(quoted && text[i+1]==='"') {value+='"';i++;} else quoted=!quoted; }
    else if(c===',' && !quoted) {row.push(value);value='';}
    else if(c==='\n' && !quoted) {row.push(value.replace(/\r$/,'')); rows.push(row);row=[];value='';}
    else value+=c;
  }
  if(value || row.length){row.push(value.replace(/\r$/,''));rows.push(row);}
  const headers=rows.shift();
  return rows.filter(r=>r.length>1).map(r=>Object.fromEntries(headers.map((h,i)=>[h,r[i]??''])));
}
const mean = a => a.length ? a.reduce((s,v)=>s+v,0)/a.length : null;
const quantile=(a,q)=>{if(!a.length)return null;const b=a.slice().sort((a,b)=>a-b),p=(b.length-1)*q,i=Math.floor(p);return b[i]+(b[Math.ceil(p)]-b[i])*(p-i);};
function group(rows,key) {const result=new Map();for(const r of rows){const k=key(r);if(!result.has(k))result.set(k,[]);result.get(k).push(r);}return result;}
function stats(rows) {
  const wins=rows.filter(r=>r.outcome==='win');
  const costs=wins.map(r=>Number(r.powderUsed)+Number(r.cannonballsUsed)+Number(r.medicineUsed));
  return {battles:rows.length,wins:wins.length,winRate:wins.length/rows.length,zeroCostWins:costs.filter(n=>n===0).length,
    meanTotal:mean(costs),medianTotal:quantile(costs,.5),p10Total:quantile(costs,.1),p90Total:quantile(costs,.9),
    powder:mean(wins.map(r=>Number(r.powderUsed))),cannonballs:mean(wins.map(r=>Number(r.cannonballsUsed))),medicine:mean(wins.map(r=>Number(r.medicineUsed))),
    meanWinTurns:mean(wins.map(r=>Number(r.turns))),meanSchoolPlays:mean(wins.map(r=>Number(r.schoolCards)))};
}
function staticAudit() {
  const file=path.join(root,'UnityClient/Artifacts/CardBalanceReview20261012/CoreAfter/cards.tsv');
  const cards=tsv(file).filter(c=>c.UpgradeLevel==='0').map(c=>({id:c.Id,name:c.Name,school:c.SchoolId||'ordinary',stage:c.AllowedStages,
    basic:!c.RequiredCrew&&!c.RequiredComponent,powder:Number(c.PowderCost),cannonballs:Number(c.CannonballCost),damage:Number(c.Damage),block:Number(c.Block),repair:Number(c.Repair),
    mechanic:c['SchoolEffect.Mechanic'],power:Number(c['SchoolEffect.Power']),
    medicine:'ordinary boarding healing: optional bonus only; BloodMend/BloodDrain: no medicine',
    description:c.Description}));
  const bySchool=schools.map(s=>{const a=cards.filter(c=>c.school===s);return {school:s,name:schoolNames[s],cards:a.length,basic:a.filter(c=>c.basic).length,
    directDamageWithNoPowderOrBalls:a.filter(c=>c.damage>0&&c.powder===0&&c.cannonballs===0).length,
    freeBasicDirectDamage:a.filter(c=>c.basic&&c.damage>0&&c.powder===0&&c.cannonballs===0).length,
    mandatoryPowderOrBalls:a.filter(c=>c.powder+c.cannonballs>0).length};});
  const result={source:'Previous final Core card export; values unchanged in this rule/audit pass. Counts are definitions, not proof that a combat strategy wins.',cards,bySchool};
  fs.mkdirSync(out,{recursive:true});fs.writeFileSync(path.join(out,'card-supply-audit.json'),JSON.stringify(result,null,2));
  return result;
}
if(require.main===module) console.log(JSON.stringify(staticAudit().bySchool,null,2));
module.exports={root,out,schoolNames,schools,csv,tsv,stats,group,mean,quantile,staticAudit};
