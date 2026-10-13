const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root='D:/Game',out=root+'/Artifacts/ArtPolishBatch1_20261012',audit=root+'/Artifacts/ArtQualityAudit20261012';
const read=p=>JSON.parse(fs.readFileSync(p,'utf8'));
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const integration=read(out+'/integration-report.json');
if(!integration.applied||integration.records.length!==31)throw new Error('All 31 production assets must be installed.');
const capture=read(out+'/validation/ui-review-final/report.json');
if(capture.frames.length!==50||capture.assets.length!==31||capture.frames.some(f=>f.error||!f.scalerVerified))throw new Error('Final Unity capture incomplete.');
const equipmentQa=read(out+'/validation/equipment-visual-review/equipment-ui-qa.json');
if(equipmentQa.status!=='passed'||equipmentQa.equipmentAssetEvidence.some(a=>capture.assets.find(b=>b.path===a.path)?.sha256!==a.sha256))throw new Error('Equipment visual review does not match final assets.');
const tests={};
for(const name of ['editmode','playmode']){
 const xml=fs.readFileSync(out+'/validation/'+name+'.xml','utf8');
 const header=xml.match(/<test-run\b[^>]+>/)?.[0];if(!header)throw new Error('Missing test result '+name);
 const attr=n=>header.match(new RegExp('\\b'+n+'="([^"]+)"'))?.[1];
 tests[name]={total:Number(attr('total')),passed:Number(attr('passed')),failed:Number(attr('failed')),skipped:Number(attr('skipped')),result:attr('result')};
 if(tests[name].result!=='Passed'||tests[name].failed||!tests[name].passed)throw new Error('Tests did not pass: '+name);
}
const baseline=read(out+'/source-baseline.json');
for(const record of integration.records){
 const file=root+'/'+record.relative,old=baseline.records.find(r=>r.relative===record.relative),meta=fs.readFileSync(file+'.meta','utf8');
 if(hash(file)!==record.sha256||record.sha256===old.sha256)throw new Error('New production file missing/modified: '+record.id);
 if(record.metaSha256&&hash(file+'.meta')!==record.metaSha256)throw new Error('Production importer changed: '+record.id);
 const oldGuid=fs.readFileSync(old.destination+'.meta','utf8').match(/^guid: (.+)$/m)?.[1].trim(),guid=meta.match(/^guid: (.+)$/m)?.[1].trim();
 if(guid!==oldGuid)throw new Error('GUID changed: '+record.id);
 const imported=capture.assets.find(a=>'UnityClient/'+a.path===record.relative);
 if(!imported||imported.sha256!==record.sha256||imported.guid!==guid||imported.width!==record.width||imported.height!==record.height)throw new Error('Capture asset does not match production: '+record.id);
 if(record.kind==='event-atlas'&&imported.mipmapCount<=1)throw new Error('Event mipmaps missing: '+record.id);
}
const sources=[out+'/equipment/manifest.json',out+'/icons/manifest.json',out+'/events/manifest.json',out+'/events/shattered_sail/manifest.json',out+'/events/golden_tide/manifest.json',out+'/events/golden_tide/root-manifest.json',out+'/events/root-corrections.json'];
const chosen=new Map();
for(const source of sources){
 const data=read(source),entries=Array.isArray(data)?data:data.entries;
 for(const e of entries){const key=e.route?'event/'+e.route+'/'+e.index:(source.includes('/icons/')?'icon/':'equipment/')+e.id;chosen.set(key,{...chosen.get(key),...e});}
}
const prompts=[...chosen].map(([key,e])=>{
 if(!e.generated_path||!e.final_path||!e.prompt||!fs.existsSync(e.final_path))throw new Error('Missing generated deliverable metadata: '+key);
 if(hash(e.generated_path)!==hash(e.final_path))throw new Error('Selected generated original differs: '+key);
 return {key,...e,...(e.generated_metadata?{generated_metadata:{...e.generated_metadata,sha256:hash(e.final_path)}}:{}),sha256:hash(e.final_path)};
});
if(prompts.length!==107)throw new Error('Expected 107 selected deliverables, got '+prompts.length);
fs.writeFileSync(out+'/prompts.json',JSON.stringify({mode:'built-in image_gen',count:107,entries:prompts},null,2));
const complete={completedAtUtc:new Date().toISOString(),status:'complete',scope:integration.counts,productionPngs:31,selectedGeneratedImages:107,tests,actualUiFrames:50,importedAssets:31,artifacts:out,noPackage:true,validationNotes:['25件主图保持真实透明度、完整端部及原UI比例。','80格独立插图复核后按原5×4格位组图。','保留31个资源GUID，Unity导入尺寸、采样及实际界面均核对。','普通事件4:3、教学1:1，采用比例裁切、mipmap缩小采样与图格边缘保护，已移除旧金边内缩。'],codeFiles:['LegacyDemoArtCatalog.cs','CoreGameplayEmbeddedEncounter.cs','CoreGameplayEncounterPresentation.cs']};
complete.visualReview={equipment:'validation/equipment-visual-review/equipment-ui-qa.json',finalScreens:'validation/ui-review-final/index.html',eventContact:'validation/ui-review-final/event-ui-art-contact.png'};
complete.equipmentScope=read(out+'/equipment-coverage.json').summary;
complete.validationNotes[0]='25套共用装备主图已重绘，关联150件装备；本批未完成150件各自的独立外观。';
complete.runtimeImport={equipmentFormat:[...new Set(capture.assets.filter(a=>a.path.includes('/ModularShip/Layers/')).map(a=>a.graphicsFormat))],eventAtlases:capture.assets.filter(a=>a.path.includes('_event_sheet')).map(a=>({path:a.path,format:a.graphicsFormat,mipmapCount:a.mipmapCount})),note:'本机Unity实际将带mipmap的事件图集导入为RGBA；后续出包的内存核对应按此实测格式计算，不能按meta请求的BC7估算。当前四图集完整加载约200 MiB。'};
fs.writeFileSync(out+'/completion.json',JSON.stringify(complete,null,2));
// Keep the original audit as evidence, and update its task status separately.
for(const file of ['optimization-list.json','optimization-list.html','optimization-list.csv']){
 const target=out+'/originals/Artifacts/ArtQualityAudit20261012/'+file;
 fs.mkdirSync(path.dirname(target),{recursive:true});if(!fs.existsSync(target))fs.copyFileSync(audit+'/'+file,target);
}
const list=read(audit+'/optimization-list.json');
for(const row of list.rows)if(row.priority==='P1'){
 row.completion={status:'已完成',batch:'ArtPolishBatch1_20261012',completedAtUtc:complete.completedAtUtc,review:'Artifacts/ArtPolishBatch1_20261012/index.html'};
 if(row.key?.startsWith('parts/'))row.completion.scopeClarification='本项完成5套共用主图重绘，关联30件装备；不代表30件各自独立外观已完成。';
}
list.batch1Completion=complete;
list.corrections=[...(list.corrections||[]).filter(x=>!x.includes('238×280')),'238×280为嵌入事件构建初始尺寸，运行时响应式布局最终为4:3；教学仍为1:1。'];
fs.writeFileSync(audit+'/optimization-list.json',JSON.stringify(list,null,2));
const quote=s=>'"'+String(s??'').replaceAll('"','""')+'"';
const csv=[['编号','优先级','完成状态','资源类别','范围','当前用途','行动','可见问题/审阅结论（原审核）','优化方向','验收','源文件','调用证据'],...list.rows.map(r=>[r.id,r.priority,r.completion?.status||'',r.category,r.quantity,r.usage,r.action,r.visual_evidence.join('；'),r.optimization_direction.join('；'),r.acceptance.join('；'),r.files.join('\n'),r.evidence.map(e=>e.path+':'+e.line+' '+e.claim).join('\n')])].map(r=>r.map(quote).join(',')).join('\r\n');
fs.writeFileSync(audit+'/optimization-list.csv','\ufeff'+csv);
let html=fs.readFileSync(out+'/originals/Artifacts/ArtQualityAudit20261012/optimization-list.html','utf8');
const banner='<section style="padding:20px;background:#e3efe8;border-left:4px solid #3d7958;margin:24px 0"><b>第一批已完成 · 2026-10-12</b><p>25 套共用装备主图（关联150件装备）、80 格事件插图、2 张图标残条修复均已接入，保留原资源 GUID。'+tests.editmode.passed+' 项编辑模式测试与 '+tests.playmode.passed+' 项运行模式测试通过，50 张实际界面验收。未出包。</p><a href="../ArtPolishBatch1_20261012/index.html">查看本批优化前后对照与实际界面</a><p>装备重绘完成状态按25套共用源图计算，不代表150件各自独立外观已完成。<a href="../ArtPolishBatch1_20261012/equipment-coverage.html">查看逐件对应表</a>。下方审阅结论保留为优化前的问题快照；其余待办继续沿用。</p></section>';
html=html.replace('</h1>','</h1>'+banner).replace('普通嵌入 238×280','普通嵌入最终为4:3（238×280仅为构建初始尺寸）');
const completedIds=list.rows.filter(r=>r.priority==='P1').map(r=>r.id);
html=html.replace('</body>',`<script>for(const id of ${JSON.stringify(completedIds)}){const entry=document.getElementById(id);if(entry){entry.dataset.priority='已完成';const pill=entry.querySelector('.pill');pill.textContent='已完成 · P1';pill.style.background='#dceee1';pill.style.color='#276642';}}if(typeof update==='function')update();</script></body>`);
fs.writeFileSync(audit+'/optimization-list.html',html);
console.log(JSON.stringify({complete:true,tests,images:prompts.length,uiFrames:50,auditRowsCompleted:completedIds.length}));
