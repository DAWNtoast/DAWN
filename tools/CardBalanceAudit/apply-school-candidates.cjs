'use strict';
// Materialize reviewed offline candidates as explicit authored catalog numbers.
// This is not used by gameplay, and never changes card order, IDs or sources.
const fs=require('node:fs'),path=require('node:path');
const root=path.resolve(__dirname,'../..'),file=path.join(root,'tools/CardSchools/build-catalog.cjs');
const {cards}=require(file),candidates=JSON.parse(fs.readFileSync(process.argv[2]||path.join(root,'Artifacts/CardBalanceReview20261012/school-candidates.json'),'utf8'));
const byArt=new Map(candidates.map(r=>[r.ArtId,r]));
if(cards.length!==132||byArt.size!==132)throw new Error('Expected exactly 132 school cards.');
function splitArgs(text){
 let parts=[],start=0,depth=0,quote='',escaped=false;
 for(let i=0;i<text.length;i++){
  const c=text[i];
  if(quote){if(escaped)escaped=false;else if(c==='\\')escaped=true;else if(c===quote)quote='';continue;}
  if(c==='"'||c==="'"){quote=c;continue;}
  if('{[('.includes(c))depth++;else if('}])'.includes(c))depth--;
  else if(c===','&&depth===0){parts.push(text.slice(start,i));start=i+1;}
 }
 parts.push(text.slice(start));return parts;
}
let index=0;
const text=fs.readFileSync(file,'utf8').replace(/^add\((.*)\);$/gm,(_,args)=>{
 const c=cards[index++],r=byArt.get(c.artId),a=splitArgs(args);
 if(!r||r.Id!==c.id||r.SchoolId!==c.school)throw new Error('Identity mismatch for '+c.artId);
 const rule={...c.rule},combat={...c.combat};
 for(const [key,field]of Object.entries({p:'Power',s:'Secondary',t:'Threshold',dur:'Duration',l:'Limit',m:'Movement'})){
  const value=r['SchoolEffect.'+field];if(value)rule[key]=value;else delete rule[key];
 }
 rule.up=r['SchoolEffect.Upgrades'].replace(/,\s*/g,'|');
 for(const [key,field]of Object.entries({d:'Damage',b:'Block',h:'Repair',draw:'Draw',powder:'PowderCost',balls:'CannonballCost'})){
  if(r[field])combat[key]=r[field];else delete combat[key];
 }
 a[3]=String(r.Energy);a[5]=JSON.stringify(rule);a[6]=JSON.stringify(combat);
 return 'add('+a.join(',')+');';
});
if(index!==132)throw new Error('Source line count mismatch '+index);
fs.writeFileSync(file,text);
delete require.cache[require.resolve(file)];require(file).write({catalogOnly:true});
