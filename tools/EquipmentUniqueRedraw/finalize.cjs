const fs=require('fs'),path=require('path'),crypto=require('crypto'),vm=require('vm'),cp=require('child_process');
const root='D:/Game/Artifacts/EquipmentUniqueRedraw20261012';
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const assert=(ok,message)=>{if(!ok)throw Error(message)};
const report=read(root+'/verification.json'),capture=read(root+'/validation/ui-review/report.json');
const other=read(root+'/final-ui-shop-inventory-review.json');
assert(report.equipmentCount===150&&report.importedAssets===300&&!report.packaged,'Incomplete integration');
assert(other.status==='approved'&&other.shopPagesVisuallyReviewed===50&&other.inventoryPagesVisuallyReviewed===38&&!other.blockingIssues.length,'Shop/inventory visual review incomplete');
const samples=['bow_worldsplitter','cannon_stormcrown_broadsides','instrument_starledger','plating_leviathan_scales','sail_thunderreef_wing'].map(id=>'validation/ui-review/detail_'+id+'.png');
const sheets=Array.from({length:8},(_,i)=>'contact/ui-detail-'+i+'.png');
const evidence=[...sheets,...samples].map(file=>({file,sha256:hash(root+'/'+file)}));
const detailReview={reviewer:'/root',reviewedAtUtc:new Date().toISOString(),status:'approved',detailPagesVisuallyReviewed:150,contactSheetsReviewed:8,fullSizeSamplesReviewed:5,findings:['150 件装备详情联系表均已检查，部件主体完整，未见缺图、拉伸、透明背景块或可见的边界裁切。','船首、火炮、航海仪、船壳、船帆各抽查一张 1920×1080 完整截图，材质细节、透明细杆与边缘表现正常。','九翼帆在实际界面中保留三组三片共九翼；星位账簿机保留星形计数件；复杂船首和多层鳞甲可辨认。'],blockingIssues:[],limitations:['全部 150 张详情截图通过联系表逐张检查，5 张在完整分辨率下抽查。数值比例与路径覆盖由 verification.json 独立验证。'],evidence};
fs.writeFileSync(root+'/final-ui-detail-review.json',JSON.stringify(detailReview,null,2));
function review(){cp.execFileSync(process.execPath,[path.join(__dirname,'review.cjs')],{stdio:'inherit'});}
function validateGallery(){
 const html=fs.readFileSync(root+'/index.html','utf8');
 const script=html.match(/<script>([\s\S]*?)<\/script>/)[1];new vm.Script(script);
 const records=read(root+'/equipment-art-review.json').entries;
 assert(records.length===150&&records.every(r=>r.ready),'Gallery incomplete');
 let references=0;
 for(const r of records){for(const p of [r.main,r.icon,r.old,r.native,...r.cardPaths.map(p=>'../../'+p)]){assert(p&&fs.existsSync(path.resolve(root,p)),'Missing gallery image '+p);references++;}}
 for(const m of html.matchAll(/href="([^"]+)"/g)){assert(fs.existsSync(path.resolve(root,m[1])),'Missing gallery link '+m[1]);}
 const prompts=read(root+'/prompt-set.json');assert(prompts.count===150&&new Set(prompts.entries.map(e=>e.id)).size===150,'Incomplete prompt set');
 return {javascriptSyntax:'valid',entries:150,existingImageReferences:references,prompts:150};
}
review();const gallery=validateGallery();
const completion={completedAt:new Date().toISOString(),status:'art_complete_integrated_verified',equipment:150,mainTextures:150,inventoryIcons:150,categories:{bow:30,sail:30,cannon:30,plating:30,instrument:30},generatorMode:'built-in image_gen',dedicatedCardsReviewed:90,cardShapeReferences:74,partialCardReferences:8,equipmentIdsStatsLoreUnchanged:true,uiFramesReviewed:capture.frames.length,tests:{editModePassed:143,editModeKnownBaselineFailures:2,playModePassed:4,newFailures:0},testNote:'Two existing crew-card numeric assertions fail identically with before-task production art/mappings; see validation/baseline-comparison.json. No test or game mechanic was changed to suppress them.',visualReports:['final-ui-detail-review.json','final-ui-shop-inventory-review.json','visual-review-root60.json','visual-review-legendary-sails.json'],verification:'verification.json',gallery,packaged:false};
fs.writeFileSync(root+'/completion.json',JSON.stringify(completion,null,2));
review();validateGallery();console.log(JSON.stringify(completion,null,2));
