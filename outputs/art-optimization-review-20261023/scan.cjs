const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root = 'D:/Game/UnityClient/Assets/_Project/Resources';
const out = __dirname;
const walk = d => fs.readdirSync(d, {withFileTypes:true}).flatMap(e=>e.isDirectory()?walk(path.join(d,e.name)):[path.join(d,e.name)]);
const sha = p => crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const images = walk(root).filter(p=>p.endsWith('.png')).map(p=>{
  const fd=fs.openSync(p,'r'), header=Buffer.alloc(32);fs.readSync(fd,header,0,32,0);fs.closeSync(fd);
  const meta=fs.readFileSync(p+'.meta','utf8');
  return {resource:path.relative(root,p).replace(/\\/g,'/'),width:header.readUInt32BE(16),height:header.readUInt32BE(20),bytes:fs.statSync(p).size,isReadable:Number(meta.match(/\bisReadable:\s*(\d+)/)?.[1])};
});
const comic = images.filter(i=>/^(BattleComics\/HD|OpeningComics|TreasureHunt\/Comics)\//.test(i.resource));
const oldVoyage = images.filter(i=>i.resource.startsWith('PortFacilities/Regional/Voyage/')&&!i.resource.includes('_crew_integrated_'));
const bg = images.filter(i=>i.resource.includes('_crew_integrated_')||['PortFacilities/port_investment_scene_internal_v1.png','PortFacilities/treasure_worktable_background_v3.png','ComponentShopV3/shipyard_hall.png'].includes(i.resource));
const oldInv = JSON.parse(fs.readFileSync('D:/Game/outputs/art-renovation-audit-20261022/inventory.json','utf8')).images;
const treasure = images.filter(i=>i.resource.startsWith('TreasureHunt/Comics/')).map(i=>({...i,sha256:sha(path.join(root,i.resource)),unchangedSince1022:sha(path.join(root,i.resource))===oldInv.find(x=>x.resource===i.resource.replace('.png',''))?.sha256}));
const iconManifest=JSON.parse(fs.readFileSync('D:/Game/outputs/battle-icon-atlas-20261022/manifest.json','utf8'));
const iconHashes=iconManifest.sourceHashes.map(s=>({path:s.Source,unchanged:sha(s.Source).toUpperCase()===s.SHA256}));
fs.writeFileSync(path.join(out,'inventory.json'),JSON.stringify({date:'2026-10-23',scope:'Current Resources PNG inventory; source bytes are not build size or GPU memory.',count:images.length,sourceMiB:images.reduce((s,i)=>s+i.bytes,0)/1048576,readableCount:images.filter(i=>i.isReadable!==0).length,comicCount:comic.length,comicSourceMiB:comic.reduce((s,i)=>s+i.bytes,0)/1048576,oldVoyageCount:oldVoyage.length,oldVoyageSourceMiB:oldVoyage.reduce((s,i)=>s+i.bytes,0)/1048576,comic,oldVoyage,backgrounds:bg,treasure,iconHashes,images},null,2));
async function contact(items,file,cols,tileW,tileH){
 const w=cols*tileW,h=Math.ceil(items.length/cols)*tileH;const layers=[];let labels='';
 for(let n=0;n<items.length;n++){
  const i=items[n],x=(n%cols)*tileW,y=Math.floor(n/cols)*tileH;
  const thumb=await sharp(path.join(root,i.resource)).resize({width:tileW-16,height:tileH-50,fit:'inside'}).png().toBuffer();const m=await sharp(thumb).metadata();
  layers.push({input:thumb,left:x+Math.floor((tileW-m.width)/2),top:y+8});
  const label=path.basename(i.resource).replace('voyage_','').replace('_crew_integrated_v1','');
  labels+=`<text x="${x+10}" y="${y+tileH-25}">${label}</text><text x="${x+10}" y="${y+tileH-7}" fill="#b6c5ca">${i.width} x ${i.height}</text>`;
 }
 layers.push({input:Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}"><g fill="#f6ecd7" font-family="Arial" font-size="13">${labels}</g></svg>`),top:0,left:0});
 await sharp({create:{width:w,height:h,channels:3,background:'#203039'}}).composite(layers).jpeg({quality:92}).toFile(path.join(out,file));
}
(async()=>{
 await contact(treasure,'treasure-current.jpg',3,520,340);
 await contact(bg,'backgrounds-current.jpg',3,520,340);
 console.log(JSON.stringify({count:images.length,comicCount:comic.length,oldVoyageCount:oldVoyage.length,backgrounds:bg.map(i=>({path:i.resource,width:i.width,height:i.height})),treasureUnchanged:treasure.every(i=>i.unchangedSince1022),iconsUnchanged:iconHashes.every(i=>i.unchanged)},null,2));
})().catch(e=>{console.error(e);process.exitCode=1;});
