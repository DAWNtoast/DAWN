// Contact sheets only; original production captures remain untouched.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const run=process.argv[2]||'visualall';
if(!/^[a-zA-Z0-9_-]+$/.test(run))throw Error('Invalid run');
const input='D:/Game/outputs/comic-pagination-20261103/art-opt-01-20261104/'+run+'/Artifacts/Battle';
const output='D:/Game/outputs/art-resource-optimization-20261104/scene-review/'+run;
const xml=s=>s.replace(/[&<>\"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','\"':'&quot;'}[c]));
(async()=>{
 fs.mkdirSync(output,{recursive:true});const items=[];
 for(const name of fs.readdirSync(input).filter(x=>/^(Art\d+|ram_).*\.png$/.test(x)).sort()){
  const file=input+'/'+name,m=await sharp(file).metadata();
  items.push({name,file,width:m.width,height:m.height,sha256:crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex')});
 }
 const sheets=[];
 for(const group of [...new Set(items.map(x=>x.name.slice(0,5)))]){
  const all=items.filter(x=>x.name.startsWith(group));
  for(let start=0;start<all.length;start+=12){
   const batch=all.slice(start,start+12),layers=[];
   for(const [i,item]of batch.entries()){
    const x=i%3*640,y=Math.floor(i/3)*390;
    layers.push({input:await sharp(item.file).resize(640,360).png().toBuffer(),left:x,top:y});
    layers.push({input:Buffer.from('<svg xmlns="http://www.w3.org/2000/svg" width="640" height="30"><text x="5" y="21" font-family="Arial" font-size="17" fill="#eee">'+xml(item.name)+'</text></svg>'),left:x,top:y+360});
   }
   const file=output+'/'+group+'-'+(start/12+1)+'.jpg';
   await sharp({create:{width:1920,height:Math.ceil(batch.length/3)*390,channels:3,background:'#17232b'}}).composite(layers).jpeg({quality:94}).toFile(file);sheets.push(file);
  }
 }
 fs.writeFileSync(output+'/capture-manifest.json',JSON.stringify(items,null,2));
 fs.writeFileSync(output+'/index.html','<!doctype html><meta charset="utf-8"><title>游戏美术实际画面验收</title><style>body{font:16px system-ui;background:#15232c;color:#eee;margin:24px}main{display:grid;grid-template-columns:repeat(2,1fr);gap:18px}figure{margin:0}img{width:100%}a{color:#daeaff}</style><h1>实际 Unity 场景截图</h1><p>点击图片查看完整尺寸。04 为前后对照；07 为固定进度采样。具体覆盖范围见验收记录。</p><main>'+items.map(x=>{const href=encodeURI(path.relative(output,x.file).replace(/\\/g,'/'));return '<figure><a href="'+href+'"><img loading="lazy" src="'+href+'"></a><figcaption>'+xml(x.name)+' · '+x.width+'×'+x.height+'</figcaption></figure>';}).join('')+'</main>');
 console.log(JSON.stringify({count:items.length,sheets}));
})().catch(e=>{console.error(e);process.exitCode=1});
