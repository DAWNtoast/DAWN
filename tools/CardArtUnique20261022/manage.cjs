const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root='D:/Game',base=root+'/Artifacts/CardArtUnique20261022',previous=root+'/Artifacts/CardArtReview20261022';
const resources=root+'/UnityClient/Assets/_Project/Resources';
const hash=f=>crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
const xml=s=>String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&apos;'}[c]));
function prepare(){
 fs.mkdirSync(base,{recursive:true});
 const remaps=JSON.parse(fs.readFileSync(previous+'/remapped-cards.json','utf8'));
 const cards=JSON.parse(fs.readFileSync(previous+'/RoutingAudit/runtime-card-art-current.json','utf8'));
 const scope=remaps.map(r=>{const c=cards.find(c=>c.id===r.id);if(!c)throw new Error('Missing card '+r.id);return {id:r.id,name:r.name,description:c.description,school:c.schoolId,before:r.after,beforeSha256:hash(resources+'/'+r.after+'.png'),destination:resources+'/CardArt/Western/'+r.id+'.png'};});
 if(scope.length!==38||new Set(scope.map(s=>s.id)).size!==38)throw new Error('Expected exactly 38 cards');
 const target=base+'/scope.json';
 if(fs.existsSync(target))throw new Error('Scope already prepared; do not overwrite baseline');
 fs.writeFileSync(target,JSON.stringify(scope,null,2));
 console.log(JSON.stringify({prepared:scope.length,scope:target,tide:scope.filter(s=>s.id.includes('_tide_'))}));
}
async function sheet(jobs,file){
 const width=1200,rows=Math.ceil(jobs.length/4),height=rows*330+45,layers=[],labels=[`<text x="15" y="29" font-size="23" fill="#e6c88f">独立卡图 · ${jobs.length} 张</text>`];
 for(const [i,j]of jobs.entries()){
  const x=i%4*300,y=45+Math.floor(i/4)*330;
  layers.push({input:await sharp(j.destination).resize(280,280,{fit:'contain',background:'#17212c'}).png().toBuffer(),left:x+10,top:y});
  labels.push(`<text x="${x+10}" y="${y+304}" font-size="19" fill="white">${xml(j.name)}</text><text x="${x+10}" y="${y+322}" font-size="10" fill="#aabbcc">${xml(j.id)}</text>`);
 }
 layers.push({input:Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}"><g font-family="Microsoft YaHei,Arial">${labels.join('')}</g></svg>`),left:0,top:0});
 await sharp({create:{width,height,channels:3,background:'#17212c'}}).composite(layers).jpeg({quality:94}).toFile(file);
}
async function install(){
 const scope=JSON.parse(fs.readFileSync(base+'/scope.json','utf8'));
 const jobs=fs.readdirSync(base).filter(f=>f.endsWith('-generated.json')).flatMap(f=>JSON.parse(fs.readFileSync(base+'/'+f,'utf8')));
 const seen=new Set(),seenHashes=new Set(),installed=[];
 for(const j of jobs){
  const s=scope.find(s=>s.id===j.id);if(!s||seen.has(j.id))throw new Error('Unexpected/duplicate ID '+j.id);seen.add(j.id);
  const m=await sharp(j.source).metadata();if(m.format!=='png'||m.width!==m.height||m.width<1024)throw new Error('Invalid generated PNG '+j.id);
  const sha=hash(j.source);if(seenHashes.has(sha)||sha===s.beforeSha256)throw new Error('Duplicate or reused pixels '+j.id);seenHashes.add(sha);
  if(hash(resources+'/'+s.before+'.png')!==s.beforeSha256)throw new Error('Shared reference unexpectedly changed '+j.id);
  if(fs.existsSync(s.destination)&&hash(s.destination)!==sha){fs.mkdirSync(base+'/backup',{recursive:true});const b=base+'/backup/'+j.id+'.png';if(!fs.existsSync(b))fs.copyFileSync(s.destination,b);}
  if(!fs.existsSync(s.destination)||hash(s.destination)!==sha)fs.copyFileSync(j.source,s.destination);
  if(!fs.existsSync(s.destination+'.meta')){const template=fs.readFileSync(resources+'/CardArt/Western/card_crew_rally.png.meta','utf8');fs.writeFileSync(s.destination+'.meta',template.replace(/^guid: .+$/m,'guid: '+crypto.randomBytes(16).toString('hex')));}
  installed.push({...s,...j,destination:s.destination,width:m.width,height:m.height,sha256:sha,tool:'built-in image_gen'});
 }
 installed.sort((a,b)=>scope.findIndex(s=>s.id===a.id)-scope.findIndex(s=>s.id===b.id));
 fs.writeFileSync(base+'/installed.json',JSON.stringify(installed,null,2));
 const missing=scope.filter(s=>!seen.has(s.id)).map(s=>s.id);
 const duplicateGuids=[],guids=new Set();for(const j of installed){const g=fs.readFileSync(j.destination+'.meta','utf8').match(/^guid: (.+)$/m)?.[1];if(!g||guids.has(g))duplicateGuids.push(j.id);guids.add(g);}
 const validation={expected:scope.length,installed:installed.length,missing,uniqueImageHashes:seenHashes.size,duplicateGuids,sharedReferencesUnchanged:true};
 fs.writeFileSync(base+'/asset-validation.json',JSON.stringify(validation,null,2));
 for(let i=0;i<installed.length;i+=12)await sheet(installed.slice(i,i+12),base+'/contact-'+(i/12+1)+'.jpg');
 const rel=p=>path.relative(base,p).replace(/\\/g,'/');
 const articles=installed.map(j=>`<article data-search="${xml(j.id+' '+j.name)}"><h2>${xml(j.name)}</h2><small>${xml(j.id)}</small><div class="pair"><figure><img loading="lazy" src="${rel(resources+'/'+j.before+'.png')}"><figcaption>之前复用的卡图</figcaption></figure><figure><img loading="lazy" src="${rel(j.destination)}"><figcaption>本牌独立卡图</figcaption></figure></div><p>${xml(j.description)}</p><details><summary>完整生成提示词</summary><pre>${xml(j.prompt)}</pre></details></article>`).join('');
 fs.writeFileSync(base+'/review.html',`<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>38张独立卡图</title><style>body{max-width:1500px;margin:28px auto;padding:0 20px;background:#11212c;color:#eef3f5;font:16px/1.6 system-ui}header{margin-bottom:24px}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(440px,1fr));gap:22px}article{padding:18px;background:#1b2e3c;border-radius:10px}h2{margin:0}small,figcaption{color:#b8cad6}.pair{display:flex;gap:12px}figure{margin:14px 0;width:50%}img{width:100%;display:block}pre{white-space:pre-wrap;font-size:13px}input{background:#263c4a;color:white;padding:12px;width:320px;max-width:90%;border:1px solid #546b7d}a,summary{color:#e7c789}</style><header><h1>复用卡牌的独立卡图</h1><p>2026-10-22 · ${installed.length} / ${scope.length} 张已绘制并接入。每张均由内置 image_gen 独立生成。</p><p><a href="installed.json">最终资源路径与提示词</a> · <a href="asset-validation.json">资源验证</a></p><input id="search" placeholder="按卡名或 ID 筛选" aria-label="筛选卡图"></header><main class="grid">${articles}</main><script>document.querySelector('#search').addEventListener('input',e=>{const q=e.target.value.toLowerCase();document.querySelectorAll('article').forEach(a=>a.hidden=!a.dataset.search.toLowerCase().includes(q));});</script></html>`);
 if(duplicateGuids.length)throw new Error('Duplicate GUIDs');
 console.log(JSON.stringify(validation));
}
(process.argv[2]==='prepare'?Promise.resolve().then(prepare):install()).catch(e=>{console.error(e);process.exitCode=1;});
