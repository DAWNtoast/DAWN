const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root = 'D:/Game/UnityClient/Assets/_Project/Resources';
const out = __dirname;
const walk = d => fs.readdirSync(d,{withFileTypes:true}).flatMap(e => e.isDirectory()?walk(path.join(d,e.name)):[path.join(d,e.name)]);
const esc = s => String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&apos;'}[c]));
const pick = new Set(['ItemIcons','AchievementIcons','ComponentArt','ModularShip','UIWood','UISurfaces','ComponentShopV2','ComponentShopV3','ComponentShopRegional','WarehouseV2','MaintenanceV2','CardWorkshopV2','OpeningComics','BattleComics','BoardingBattle','SettlementReveal','TreasureHunt']);
async function main(){
 const files=walk(root).filter(f=>/\.(png|jpe?g|webp|tga|psd|svg)$/i.test(f)).sort();
 const images=[];
 for(const f of files){
   const m=await sharp(f).metadata();
   const rel=path.relative(root,f).replace(/\\/g,'/');
   const meta=fs.existsSync(f+'.meta')?fs.readFileSync(f+'.meta','utf8'):'';
   const size=meta.match(/^\s*maxTextureSize:\s*(\d+)/m);
   images.push({path:f.replace(/\\/g,'/'),resource:rel.replace(/\.[^.]+$/,''),family:rel.split('/')[0],width:m.width,height:m.height,alpha:m.hasAlpha,maxTextureSize:size?Number(size[1]):null,sha256:crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex')});
 }
 fs.writeFileSync(path.join(out,'inventory.json'),JSON.stringify({date:'2026-10-22',basis:'Current workspace Resources raster/vector files. Dimensions and import limits alone do not establish visual quality; code references may be dynamic.',images},null,2));
 const groups={}; for(const im of images) (groups[im.family]??=[]).push(im);
 fs.mkdirSync(path.join(out,'contacts'),{recursive:true});
 const contacts=[];
 for(const [family,all] of Object.entries(groups)){
  if(!pick.has(family))continue;
  // Component and modular collections are sampled across their whole sorted range.
  const sample=all.length>100?all.filter((_,i)=>i%Math.ceil(all.length/24)===0):all;
  for(let start=0;start<sample.length;start+=12){
   const batch=sample.slice(start,start+12);const w=1440,h=Math.ceil(batch.length/4)*310+44;
   const layers=[],labels=[`<text x="16" y="28" fill="#eccb87" font-size="22">${esc(family)} / current 2026-10-22</text>`];
   for(let i=0;i<batch.length;i++){
    const im=batch[i],x=i%4*360,y=44+Math.floor(i/4)*310;
    const thumb=await sharp(im.path).resize({width:344,height:250,fit:'inside',withoutEnlargement:true}).png().toBuffer();const tm=await sharp(thumb).metadata();
    layers.push({input:thumb,left:x+Math.floor((360-tm.width)/2),top:y+Math.floor((250-tm.height)/2)});
    labels.push(`<text x="${x+8}" y="${y+273}" fill="white" font-size="12">${esc(path.basename(im.path).slice(0,48))}</text><text x="${x+8}" y="${y+294}" fill="#acbfcc" font-size="13">${im.width} x ${im.height}; max ${im.maxTextureSize}</text>`);
   }
   layers.push({input:Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}"><g font-family="Arial">${labels.join('')}</g></svg>`),left:0,top:0});
   const dest=path.join(out,'contacts',family+'_'+(1+start/12)+'.jpg');
   await sharp({create:{width:w,height:h,channels:3,background:'#23343e'}}).composite(layers).jpeg({quality:90}).toFile(dest);
   contacts.push({family,path:dest.replace(/\\/g,'/'),resources:batch.map(im=>im.resource)});
  }
 }
 fs.writeFileSync(path.join(out,'contacts.json'),JSON.stringify(contacts,null,2));
 const hashes={};for(const im of images)(hashes[im.sha256]??=[]).push(im.resource);
 const duplicates=Object.values(hashes).filter(a=>a.length>1);
 fs.writeFileSync(path.join(out,'duplicates.json'),JSON.stringify(duplicates,null,2));
 // Alpha compositing is diagnostic preview only; production images remain untouched.
 const candidates=['PortArt/ship_roster_sheet','LegacyDemo/ship_sprite_sheet','ShipArtV2/Special/boss_blood','NavigationLookout/lookout_island_v1'];
 for(const name of candidates){
  const im=images.find(i=>i.resource===name),layers=[],w=1600,h=600;
  for(let side=0;side<2;side++){
   const preview=await sharp(im.path).resize({width:780,height:550,fit:'inside'}).flatten({background:side?'#dceaf0':'#23343e'}).png().toBuffer();
   const m=await sharp(preview).metadata();layers.push({input:preview,left:side*800+Math.floor((800-m.width)/2),top:30+Math.floor((550-m.height)/2)});
  }
  layers.push({input:Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="1600" height="600"><text x="20" y="24" font-family="Arial" font-size="18" fill="white">${esc(name)} : alpha composite, dark / light</text></svg>`),left:0,top:0});
  await sharp({create:{width:w,height:h,channels:3,background:'#23343e'}}).composite(layers).jpeg({quality:95}).toFile(path.join(out,name.split('/').pop()+'_alpha.jpg'));
 }
 await sharp(path.join(root,'LegacyDemo/ship_sprite_sheet.png')).extract({left:4608,top:1024,width:1536,height:1024}).flatten({background:'#dceaf0'}).png().toFile(path.join(out,'clipper_runtime_alpha.png'));
 const catalog=fs.readFileSync('D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/EncounterIllustrationCatalog.cs','utf8');
 const block=catalog.match(/Standalone\(\s*((?:"[^"]+"\s*,?\s*)+)\);/)[1];
 const ids=[...block.matchAll(/"([^"]+)"/g)].map(m=>m[1]);
 const bindings=ids.map(id=>'EncounterIllustrations/'+id+'_v1').concat(['reef','trade','moon','treasure'].map(id=>'EncounterIllustrations/legacy_'+id+'_v3'));
 const installed=new Set(images.map(i=>i.resource));
 const missing=bindings.filter(p=>!installed.has(p));
 fs.writeFileSync(path.join(out,'encounter-resource-check.json'),JSON.stringify({date:'2026-10-22',standaloneBindings:ids.length,legacySheetBindings:4,installedReferenced:bindings.filter(p=>installed.has(p)),missing},null,2));
 console.log(JSON.stringify({count:images.length,families:Object.fromEntries(Object.entries(groups).map(([k,v])=>[k,v.length])),contactSheets:contacts.length,duplicateGroups:duplicates.length,encounterExpected:bindings.length,encounterMissing:missing.length},null,2));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
