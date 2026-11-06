// Review-only contact sheets from actual Unity captures. Never edits production artwork.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root='D:/Game/outputs/art-resource-optimization-20261104';
const input='D:/Game/outputs/comic-pagination-20261103/art-opt-01-20261104/comicfinal4k/screenshots';
const plan=JSON.parse(fs.readFileSync(root+'/comic-source-plan.json','utf8').replace(/^\uFEFF/,''));
const xml=s=>s.replace(/[&<>"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));
(async()=>{
 const output=root+'/comic-review';fs.mkdirSync(output,{recursive:true});
 const items=[];
 for(const page of plan.pages)for(let i=1;i<=4;i++){
  const filename=path.basename(page.imageResource)+'_panel'+i+'.png',full=input+'/'+filename;
  const m=await sharp(full).metadata();if(m.width!==3840||m.height!==2160)throw Error('Not 4K: '+full);
  items.push({resource:page.imageResource,panel:i,file:full,width:m.width,height:m.height,sha256:crypto.createHash('sha256').update(fs.readFileSync(full)).digest('hex')});
 }
 const sheets=[];
 for(let start=0;start<items.length;start+=28){
  const batch=items.slice(start,start+28),w=1600,h=Math.ceil(batch.length/4)*250,layers=[];
  for(const [i,item]of batch.entries()){
   const x=(i%4)*400,y=Math.floor(i/4)*250;
   const label=path.basename(item.resource)+' / '+item.panel;
   layers.push({input:await sharp(item.file).resize(400,225).png().toBuffer(),left:x,top:y});
   layers.push({input:Buffer.from('<svg xmlns="http://www.w3.org/2000/svg" width="400" height="25"><text x="8" y="18" font-family="Arial" font-size="14" fill="#eee">'+xml(label)+'</text></svg>'),left:x,top:y+225});
  }
  const file=output+'/comic-contact-'+(start/28+1)+'.jpg';
  await sharp({create:{width:w,height:h,channels:3,background:'#17232b'}}).composite(layers).jpeg({quality:94}).toFile(file);
  sheets.push(file);
 }
 fs.writeFileSync(output+'/4k-capture-manifest.json',JSON.stringify(items,null,2));
 const body=items.map(x=>{const href=encodeURI(path.relative(output,x.file).replace(/\\/g,'/'));return '<figure><a href="'+href+'"><img loading="lazy" src="'+href+'"></a><figcaption>'+xml(path.basename(x.resource))+' / '+x.panel+' · 3840×2160</figcaption></figure>';}).join('');
 fs.writeFileSync(output+'/index.html','<!doctype html><meta charset="utf-8"><title>独立分镜实际Unity画面验收</title><style>body{font:16px system-ui;background:#15232c;color:#eee;margin:24px}main{display:grid;grid-template-columns:repeat(4,1fr);gap:12px}figure{margin:0}img{width:100%}figcaption{padding:6px;font-size:13px}</style><h1>104格独立分镜 · Unity 4K画面</h1><p>源画为1672×940/941；本页图片是3840×2160实际Unity渲染，点击可打开完整截图。</p><main>'+body+'</main>');
 console.log(JSON.stringify({verified:items.length,sheets}));
})().catch(e=>{console.error(e);process.exitCode=1});
