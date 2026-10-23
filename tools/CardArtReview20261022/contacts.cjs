const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root = 'D:/Game';
const output = root + '/Artifacts/CardArtReview20261022';
const sources = ['CardArt/Western', 'CardSchools/Art'];
const xml = s => String(s).replace(/[&<>"']/g, c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&apos;'}[c]));
async function main() {
  fs.mkdirSync(output + '/contacts', {recursive:true});
  const images=[];
  for (const group of sources) {
    const dir=root+'/UnityClient/Assets/_Project/Resources/'+group;
    for(const file of fs.readdirSync(dir).filter(f=>f.endsWith('.png')).sort()) {
      const full=dir+'/'+file, meta=await sharp(full).metadata();
      images.push({id:path.basename(file,'.png'),group,path:full,width:meta.width,height:meta.height,sha256:crypto.createHash('sha256').update(fs.readFileSync(full)).digest('hex')});
    }
  }
  const groups={western:images.filter(i=>i.group==='CardArt/Western')};
  for(const school of ['fire','arcane','curse','tide','thunder','blood','neutral']) groups[school]=images.filter(i=>i.id.startsWith('card_school_'+school+'_'));
  const contacts=[];
  for(const [group,items] of Object.entries(groups)) {
    for(let start=0;start<items.length;start+=12) {
      const batch=items.slice(start,start+12),width=1200,height=Math.ceil(batch.length/4)*320+42;
      const layers=[],labels=[`<text x="14" y="29" font-size="22" fill="white">${group} ${start+1}-${start+batch.length}</text>`];
      for(const [index,item] of batch.entries()) {
        const x=index%4*300,y=42+Math.floor(index/4)*320;
        const thumb=await sharp(item.path).resize(280,280,{fit:'contain',background:'#17212c'}).png().toBuffer();
        layers.push({input:thumb,left:x+10,top:y});
        labels.push(`<text x="${x+8}" y="${y+299}" font-size="11" fill="white">${xml(item.id.replace('card_special_','sp_').replace('card_school_',''))}</text>`);
      }
      layers.push({input:Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}"><g font-family="Arial">${labels.join('')}</g></svg>`),left:0,top:0});
      const file=output+'/contacts/'+group+'-'+String(start/12+1).padStart(2,'0')+'.jpg';
      await sharp({create:{width,height,channels:3,background:'#17212c'}}).composite(layers).jpeg({quality:92}).toFile(file);
      contacts.push({group,file,images:batch.map(i=>i.id)});
    }
  }
  fs.writeFileSync(output+'/inventory.json',JSON.stringify({date:'2026-10-22',images,contacts},null,2));
  console.log(JSON.stringify({images:images.length,contacts}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
