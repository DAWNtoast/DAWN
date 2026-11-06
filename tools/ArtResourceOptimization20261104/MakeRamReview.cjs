// Review-only composites of Unity GPU readbacks; never edits production artwork.
const fs=require('node:fs'),path=require('node:path');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const input='D:/Game/outputs/comic-pagination-20261103/art-opt-01-20261104/ramall/ram-compression';
const output='D:/Game/outputs/art-resource-optimization-20261104/ram-review';
(async()=>{
 fs.mkdirSync(output,{recursive:true});
 for(const name of ['ram_player_head_on_page01','ram_boss_broadside_page01']){
  const layers=[];
  for(const [i,variant] of ['original','candidate'].entries()){
   const file=input+'/'+name+'_'+variant+'-gpu.png';
   layers.push({input:await sharp(file).resize(1000,563).png().toBuffer(),left:i*1000,top:0});
   // Native pixel crops: rigging, high contrast ship details, waves, and gradient.
   for(const [j,rect] of [{left:1040,top:280,width:500,height:380},{left:1580,top:900,width:500,height:380}].entries())
    layers.push({input:await sharp(file).extract(rect).png().toBuffer(),left:i*1000+j*500,top:590});
  }
  const target=output+'/'+name+'_comparison.png';
  await sharp({create:{width:2000,height:970,channels:3,background:'#19252f'}}).composite(layers).png().toFile(target);
  console.log(target);
 }
})().catch(e=>{console.error(e);process.exitCode=1});
