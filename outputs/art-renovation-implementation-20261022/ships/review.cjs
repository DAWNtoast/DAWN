const fs=require('fs'), path=require('path');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
(async()=>{
 const dir=__dirname, rows=JSON.parse(fs.readFileSync(path.join(dir,'current-results.json')));
 const cellw=280,cellh=440,cols=6;
 for(let page=0;page<Math.ceil(rows.length/6);page++){
  const group=rows.slice(page*6,(page+1)*6),composites=[];
  for(let i=0;i<group.length;i++){
   const r=group[i],m=await sharp(r.path).metadata();
   const stat=await sharp(r.path).stats();
   console.log(JSON.stringify({id:r.id,width:m.width,height:m.height,alpha:m.hasAlpha,alphaMin:stat.channels[3]?.min,alphaMax:stat.channels[3]?.max}));
   const preview=await sharp(r.path).resize(260,395,{fit:'contain',background:'#163a42'}).flatten({background:'#163a42'}).png().toBuffer();
   composites.push({input:preview,left:i*cellw+10,top:32});
   composites.push({input:Buffer.from('<svg width="280" height="28"><text x="140" y="21" font-size="18" text-anchor="middle" fill="white">'+r.id+'</text></svg>'),left:i*cellw,top:0});
  }
  await sharp({create:{width:cols*cellw,height:cellh,channels:4,background:'#163a42'}}).composite(composites).png().toFile(path.join(dir,'review-'+page+'.png'));
 }
})().catch(e=>{console.error(e);process.exit(1)});
