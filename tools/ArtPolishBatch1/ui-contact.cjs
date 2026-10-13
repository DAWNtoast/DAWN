// Mechanical review crops from final Unity screenshots; no artwork edits.
const fs=require('fs');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const dir='D:/Game/Artifacts/ArtPolishBatch1_20261012/validation/ui-review-final';
const report=JSON.parse(fs.readFileSync(dir+'/report.json','utf8'));
async function main(){
 const frames=report.frames.filter(f=>f.state==='Event'||f.state==='Tutorial'),parts=[];
 for(let i=0;i<frames.length;i++){
  const f=frames[i],a=f.visibleArt.reduce((p,c)=>p.screenRect.width*p.screenRect.height>c.screenRect.width*c.screenRect.height?p:c),r=a.screenRect;
  const left=Math.max(0,Math.floor(r.x)-5),top=Math.max(0,Math.floor(1080-r.y-r.height)-5);
  const width=Math.min(1920-left,Math.ceil(r.width)+10),height=Math.min(1080-top,Math.ceil(r.height)+10);
  const input=await sharp(dir+'/'+f.file).extract({left,top,width,height}).resize(360,285,{fit:'contain',background:'#1d303a'}).png().toBuffer();
  const label=Buffer.from(`<svg width="380" height="30"><text x="8" y="21" font-size="16" font-family="sans-serif" fill="#efdfb6">${f.file}</text></svg>`);
  const x=i%3*380,y=Math.floor(i/3)*330;
  parts.push({input,left:x+10,top:y+34},{input:label,left:x,top:y});
 }
 const target=dir+'/event-ui-art-contact.png';
 await sharp({create:{width:1140,height:Math.ceil(frames.length/3)*330,channels:3,background:'#1d303a'}}).composite(parts).png().toFile(target);
 console.log(JSON.stringify({frames:frames.length,target}));
}
main().catch(e=>{console.error(e);process.exitCode=1});
