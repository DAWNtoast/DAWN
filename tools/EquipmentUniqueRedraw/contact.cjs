const fs=require('fs'),path=require('path');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root='D:/Game/Artifacts/EquipmentUniqueRedraw20261012';
const read=p=>JSON.parse(fs.readFileSync(p,'utf8'));
const escape=s=>s.replace(/[&<>]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;'}[c]));
async function main(){
 const report=read(root+'/staging.json'),mode=process.argv[2]||'art';
 fs.mkdirSync(root+'/contact',{recursive:true});
 if(mode==='art'){
  for(const slot of ['bow','sail','cannon','plating','instrument']){
   const entries=report.entries.filter(e=>e.slot===slot);if(!entries.length)continue;
   const overlays=[];const tile=240,height=276,columns=6,rows=Math.ceil(entries.length/columns);
   for(let i=0;i<entries.length;i++){const e=entries[i],left=i%columns*tile,top=Math.floor(i/columns)*height;overlays.push({input:await sharp(e.mainPath).resize(tile,tile).flatten({background:'#293b44'}).png().toBuffer(),left,top});overlays.push({input:Buffer.from(`<svg width="${tile}" height="36"><text x="8" y="14" font-family="sans-serif" font-size="11" fill="#eadbb8">${escape(e.id.replace(slot+'_',''))}</text><text x="8" y="30" font-family="sans-serif" font-size="11" fill="#a7bbc2">${escape(e.rarity)}</text></svg>`),left,top:top+tile});}
   await sharp({create:{width:tile*columns,height:height*rows,channels:4,background:'#152a33'}}).composite(overlays).png().toFile(root+'/contact/'+slot+'.png');
  }
 }else if(mode==='ui'){
  const capture=read(root+'/validation/ui-review/report.json');
  for(const state of ['Detail','Shop','Inventory']){const frames=capture.frames.filter(f=>f.state===state);for(let page=0;page<Math.ceil(frames.length/20);page++){
   const set=frames.slice(page*20,page*20+20),overlays=[],w=480,h=294;
   for(let i=0;i<set.length;i++){const f=set[i],left=i%4*w,top=Math.floor(i/4)*h;overlays.push({input:await sharp(root+'/validation/ui-review/'+f.file).resize(480,270).png().toBuffer(),left,top});overlays.push({input:Buffer.from(`<svg width="480" height="24"><text x="8" y="17" font-family="sans-serif" font-size="13" fill="#eadbb8">${escape(f.file)}</text></svg>`),left,top:top+270});}
   await sharp({create:{width:w*4,height:h*Math.ceil(set.length/4),channels:4,background:'#152a33'}}).composite(overlays).png().toFile(root+'/contact/ui-'+state.toLowerCase()+'-'+page+'.png');
  }}
 }else throw Error('Unknown mode');
 console.log('Contact sheets ready: '+mode);
}
main().catch(e=>{console.error(e);process.exit(1)});
