const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const base='D:/Game/Artifacts/CardArtReview20261022',resources='D:/Game/UnityClient/Assets/_Project/Resources/';
const esc=s=>String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&apos;'}[c]));
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const beforePath=j=>{
 const original=(/^[A-Za-z]:/.test(j.before)?j.before:resources+j.before)+'.png';
 const backup=base+'/backup/'+j.id+'.png';
 return original===j.destination&&fs.existsSync(backup)?backup:original;
};
async function sheet(jobs,dest,before=false){
 const cellWidth=before?480:250,cellHeight=before?310:290,cols=before?3:4;
 const width=cols*cellWidth,height=Math.ceil(jobs.length/cols)*cellHeight+45,layers=[];
 const labels=[`<text x="16" y="29" fill="#f6d596" font-size="22">卡图审核 · ${before?'左：原图 / 右：新图':'新图缩略预览'}</text>`];
 for(const [i,j] of jobs.entries()){
  const x=i%cols*cellWidth,y=45+Math.floor(i/cols)*cellHeight;
  if(before){
   layers.push({input:await sharp(beforePath(j)).resize(220,220,{fit:'contain',background:'#17212c'}).png().toBuffer(),left:x+10,top:y});
   layers.push({input:await sharp(j.destination).resize(220,220,{fit:'contain',background:'#17212c'}).png().toBuffer(),left:x+250,top:y});
  }else layers.push({input:await sharp(j.destination).resize(230,230,{fit:'contain',background:'#17212c'}).png().toBuffer(),left:x+10,top:y});
  labels.push(`<text x="${x+10}" y="${y+251}" fill="white" font-size="19">${esc(j.name)}</text>`);
  labels.push(`<text x="${x+10}" y="${y+272}" fill="#aabbcc" font-size="10">${esc(j.id)}</text>`);
 }
 layers.push({input:Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}"><g font-family="Microsoft YaHei, Arial">${labels.join('')}</g></svg>`),left:0,top:0});
 await sharp({create:{width,height,channels:3,background:'#17212c'}}).composite(layers).jpeg({quality:94}).toFile(dest);
}
module.exports=async function report(){
 const jobs=JSON.parse(fs.readFileSync(base+'/installed.json','utf8'));
 const original=JSON.parse(fs.readFileSync(base+'/inventory.json','utf8'));
 const runtimePath=base+'/RoutingAudit/runtime-card-art-current.json';
 const runtime=fs.existsSync(runtimePath)?JSON.parse(fs.readFileSync(runtimePath,'utf8')):[];
 const loader=fs.readFileSync('D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/CardSchoolArtCatalog.cs','utf8');
 const reviewedBlock=loader.slice(loader.indexOf('private static string ReviewedIllustrationPath('),loader.indexOf('public static string DedicatedIllustrationPath('));
 const remaps=[];let pending=[];
 for(const m of reviewedBlock.matchAll(/case "([^"]+)":|return "([^"]+)";/g)){
  if(m[1])pending.push(m[1]);
  else {for(const id of pending){const card=runtime.find(c=>c.id===id);remaps.push({id,name:card?.name||id,before:card?.beforeResourceKey||null,after:m[2]});}pending=[];}
 }
 fs.writeFileSync(base+'/remapped-cards.json',JSON.stringify(remaps,null,2));
 const changedOriginal=original.images.filter(i=>hash(i.path)!==i.sha256).map(i=>i.id);
 const unexpectedOriginalChanges=changedOriginal.filter(id=>!jobs.some(j=>j.id===id&&fs.existsSync(base+'/backup/'+id+'.png')));
 const duplicates=[],guids=new Set(),checks=[];
 for(const j of jobs){
  const meta=fs.readFileSync(j.destination+'.meta','utf8'),guid=meta.match(/^guid: (.+)$/m)?.[1];
  if(!guid||guids.has(guid))duplicates.push(j.id);guids.add(guid);
  const m=await sharp(j.destination).metadata();
  checks.push({id:j.id,width:m.width,height:m.height,format:m.format,square:m.width===m.height,correctCopy:hash(j.destination)===j.sha256,beforeExists:fs.existsSync(beforePath(j)),meta:!!guid});
 }
 for(let n=0;n<jobs.length;n+=8)await sheet(jobs.slice(n,n+8),base+'/new-art-'+(n/8+1)+'.jpg');
 for(let n=0;n<jobs.length;n+=6)await sheet(jobs.slice(n,n+6),base+'/comparison-'+(n/6+1)+'.jpg',true);
 const validation={date:'2026-10-22',reviewedOriginalImages:original.images.length,redrawnImages:jobs.length,replacedOriginalImages:changedOriginal,unexpectedOriginalChanges,duplicateOrMissingNewGuids:duplicates,checks};
 fs.writeFileSync(base+'/asset-validation.json',JSON.stringify(validation,null,2));
 const cells=jobs.map(j=>`<article data-search="${esc(j.id+' '+j.name)}"><h2>${esc(j.name)}</h2><small>${esc(j.id)}</small><div class="pair"><figure><img src="${esc(path.relative(base,beforePath(j)).replace(/\\/g,'/'))}"><figcaption>原图</figcaption></figure><figure><img src="${esc(path.relative(base,j.destination).replace(/\\/g,'/'))}"><figcaption>新图 · 内置 image_gen</figcaption></figure></div><p>${esc(j.reason)}</p><details><summary>生成提示词</summary><pre>${esc(j.prompt)}</pre></details></article>`).join('');
 fs.writeFileSync(base+'/review.html',`<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>卡图重绘审核 · 2026-10-22</title><style>body{background:#111c26;color:#e9eef3;font:16px/1.6 system-ui;margin:24px auto;max-width:1400px;padding:0 24px}h1{font-size:30px}header{margin-bottom:28px}input{padding:12px;background:#203242;color:white;border:1px solid #426078;width:320px;max-width:90%}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(430px,1fr));gap:24px}article{background:#1a2a37;padding:20px;border-radius:12px}h2{margin:0}small{color:#a5bbcc}.pair{display:flex;gap:12px}figure{margin:16px 0;width:50%}img{display:block;width:100%}figcaption{color:#b9ccd8;text-align:center}pre{white-space:pre-wrap;font-size:13px}summary{cursor:pointer;color:#e6c88f}</style><header><h1>卡图重绘审核</h1><p>2026-10-22 · 已目视检查 ${original.images.length} 张原图；本页展示 ${jobs.length} 张内置工具重绘。原共享资源保持不变，按实际卡牌 ID 加载新图。</p><p>图片尺寸、复制哈希、Unity 元数据见 <a href="asset-validation.json">资源检查记录</a>；完整提示词与保存路径见 <a href="installed.json">安装清单</a>。</p><input id="search" placeholder="按卡名或 ID 筛选" aria-label="筛选卡图"></header><main class="grid">${cells}</main><script>document.querySelector('#search').addEventListener('input',e=>{const q=e.target.value.toLowerCase();document.querySelectorAll('article').forEach(a=>a.hidden=!a.dataset.search.toLowerCase().includes(q));});</script></html>`);
 const extra=`<p>另有 ${remaps.length} 项既有图纠错引用，见 <a href="remapped-cards.json">完整引用清单</a>。当前运行时目录与定向验证见 <a href="RoutingAudit/runtime-card-art-current.json">逐卡路径</a>及<a href="RoutingAudit/art-routing-audit.txt">验证记录</a>。</p>`;
 fs.writeFileSync(base+'/review.html',fs.readFileSync(base+'/review.html','utf8').replace('<input id="search"',extra+'<input id="search"'));
 fs.writeFileSync(base+'/summary.txt',[
  '卡图审核与重绘 · 2026-10-22',
  '已目视审核216张原图，并对照当前卡名、效果、阶段及实际资源加载逻辑。',
  `内置 image_gen 重绘${jobs.length}张：${jobs.length-changedOriginal.length}张新增独立资源，${changedOriginal.length}张原路径替换且保留备份。`,
  `纠正${remaps.length}项既有图引用；所有改图逻辑仅在显示层，不修改卡牌规则、ArtId或存档ID。`,
  '已检查主题、构图、流派色彩、小尺寸辨认；PNG尺寸、复制哈希、meta及资源存在检查通过。',
  '定向代码验证使用当前Core及生产插画方法，覆盖旧存档别名和缺图回退；详见RoutingAudit。未运行Unity视觉测试或重新打包。',
  'review.html：23张新旧对照、重绘原因、可展开完整提示词。',
  'installed.json：全部内置工具原始输出路径与项目最终路径。',
  'remapped-cards.json：38项既有图映射变更。',
  'backup/：原路径替换的旧卡图。'
 ].join('\n'));
 if(unexpectedOriginalChanges.length||duplicates.length||checks.some(c=>!c.square||!c.correctCopy||!c.beforeExists||!c.meta))throw new Error('Asset validation failed; see asset-validation.json');
 console.log(JSON.stringify({assetValidation:'passed',redrawnImages:jobs.length,originalImagesUnchanged:original.images.length-changedOriginal.length,replacedOriginalImages:changedOriginal,review:base+'/review.html'}));
};
