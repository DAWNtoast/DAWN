const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root='D:/Game/Artifacts/EquipmentUniqueRedraw20261012',workspace='D:/Game';
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const assert=(ok,message)=>{if(!ok)throw Error(message)};
const unique=(values,count,message)=>assert(new Set(values).size===count,message);
const audit=read(root+'/card-reference-audit.json'),stage=read(root+'/staging.json'),integration=read(root+'/integration.json'),baseline=read(root+'/baseline.json');
assert(stage.entries.length===150,'Need 150 staged');
for(const key of ['id','nativeSha256','mainSha256','iconSha256'])unique(stage.entries.map(e=>e[key]),150,'Non-unique '+key);
const generations=[];for(const file of fs.readdirSync(root).filter(n=>/^generation-.*\.json$/.test(n)))generations.push(...read(root+'/'+file).entries);
for(const e of stage.entries){const records=generations.filter(g=>g.id===e.id&&(g.generatedMetadata?.qaStatus==='approved'||g.qaStatus==='approved'));assert(records.length===1,'Need one approved generation '+e.id);assert(records[0].prompt||records[0].finalPrompt||records[0].promptPath,'Missing prompt '+e.id);assert(hash(e.nativePath)===e.nativeSha256&&hash(e.mainPath)===e.mainSha256&&hash(e.iconPath)===e.iconSha256,'Staged image modified '+e.id);}
assert(audit.status==='visual_review_complete','Card audit incomplete');
assert(audit.entries.filter(e=>e.cardIds.length).length===90,'Expected 90 card links');
for(const e of audit.entries.filter(e=>e.cardIds.length)){assert(e.visualReviewStatus==='reviewed','Card not reviewed '+e.equipmentId);for(const p of e.artPaths)assert(fs.existsSync(path.isAbsolute(p)?p:workspace+'/'+p),'Missing card '+p);}
const csvPaths=['UnityClient/Assets/_Project/Resources/Data/component_catalog_v2.csv','outputs/balance_configs/component_config.csv'];
for(const p of csvPaths){const rows=s=>fs.readFileSync(s,'utf8').trim().split(/\r?\n/);const before=rows(root+'/baseline/'+p),after=rows(workspace+'/'+p);assert(before.length===after.length,'CSV row count changed '+p);for(let i=0;i<before.length;i++){if(!i||before[i].startsWith('#')){assert(before[i]===after[i],'CSV header changed');continue;}const b=before[i].split(','),a=after[i].split(',');assert(a.length===22&&b.length===22,'CSV columns');assert(a[20]===a[0],'Art ID not independent '+a[0]);a[20]=b[20];assert(a.join(',')===b.join(','),'Equipment stats/lore changed '+b[0]);}}
const catalog='UnityClient/Assets/_Project/Presentation/Runtime/ComponentArtCatalog.cs';
const normalize=s=>s.replace(/stable while (ArtSourceId may point several variants at one compatible model\.|each equipment ID has its own independent visual identity\.)/g,'stable while ART_COMMENT').replace(/result\.Add\(Extra\(([^\r\n]+)\)\);/g,(line,args)=>{const s=[...args.matchAll(/"((?:[^"\\]|\\.)*)"/g)],a=s[4];return 'result.Add(Extra('+args.slice(0,a.index)+'"ART_SOURCE"'+args.slice(a.index+a[0].length)+'));'}).replace(/\r\n/g,'\n');
assert(normalize(fs.readFileSync(root+'/baseline/'+catalog,'utf8'))===normalize(fs.readFileSync(workspace+'/'+catalog,'utf8')),'Catalog changed beyond art mapping/comment');
const assets=integration.assets;assert(assets.length===300,'Need 300 installed textures');const guids=[];let preserved=0;
for(const e of assets){const full=workspace+'/'+e.path;assert(hash(full)===e.sha256&&hash(full+'.meta')===e.metaSha256,'Installed asset changed '+e.path);const guid=fs.readFileSync(full+'.meta','utf8').match(/^guid: (.+)$/m)?.[1];assert(guid,'Missing GUID');guids.push(guid);const old=baseline.files.find(b=>b.path===e.path+'.meta');if(old){assert(fs.readFileSync(root+'/baseline/'+old.path,'utf8').match(/^guid: (.+)$/m)[1]===guid,'Old GUID changed');preserved++;}}
unique(guids,300,'Repeated texture GUID');assert(preserved===50,'Expected preserved 50 original image GUIDs');
const xmlAttributes=tag=>Object.fromEntries([...String(tag||'').matchAll(/([\w-]+)="([^"]*)"/g)].map(m=>[m[1],m[2].replace(/&quot;/g,'"').replace(/&apos;/g,"'").replace(/&lt;/g,'<').replace(/&gt;/g,'>').replace(/&amp;/g,'&')]));
const failedCases=xml=>[...xml.matchAll(/<test-case\b([^>]*?)(?:\/>|>([\s\S]*?)<\/test-case>)/g)].map(m=>({attributes:xmlAttributes(m[1]),body:m[2]||''})).filter(c=>c.attributes.result==='Failed').map(c=>({fullname:c.attributes.fullname,message:(c.body.match(/<message>\s*<!\[CDATA\[([\s\S]*?)\]\]>\s*<\/message>/)?.[1]||'').replace(/\r\n/g,'\n').trim()})).sort((a,b)=>a.fullname.localeCompare(b.fullname));
const baselineXmlPath=root+'/validation/baseline-cards.xml',baselineXml=fs.readFileSync(baselineXmlPath,'utf8');
const baselineFailures=failedCases(baselineXml),baselineAttributes=xmlAttributes(baselineXml.match(/<test-run\b[^>]+>/)?.[0]);
const knownCrewFailures=[
 'ShatteredSail.Core.Tests.SpecialCardCatalogTests.BlueFireGunner_KeepsHerAuthoredIdentityAndFireRoundMechanics',
 'ShatteredSail.Core.Tests.SpecialCardCatalogTests.SameProfessionAcrossSixPorts_HasDistinctMechanicsNotRenamedCopies("cook")',
];
assert(+baselineAttributes.total===50&&+baselineAttributes.passed===48&&+baselineAttributes.failed===2,'Baseline crew suite did not execute as expected');
assert(baselineFailures.length===2&&baselineFailures.every(f=>knownCrewFailures.includes(f.fullname)&&f.message),'Unexpected baseline failures');
const requiredTestTargets={
 editmode:[
  ['class','ShatteredSail.Core.Tests.ComponentCatalogTests'],
  ['class','ShatteredSail.Core.Tests.ExpandedComponentCatalogTests'],
  ['class','ShatteredSail.Core.Tests.ComponentShopDisplayGeometryTests'],
  ['class','ShatteredSail.Presentation.Tests.ComponentShopShowroomLayoutTests'],
  ['class','ShatteredSail.Core.Tests.ComponentArtLazyLoadingTests'],
  ['class','ShatteredSail.Presentation.Tests.RegionalShopFurnitureTests'],
  ['class','ShatteredSail.Presentation.Tests.OwnedShipComponentInventoryTests'],
  ['class','ShatteredSail.Core.Tests.SpecialCardCatalogTests'],
 ],
 playmode:[
  ['class','ShatteredSail.Presentation.Tests.ComponentShopShowroomPaginationTests'],
  ['method','ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.ComponentPurchase_BuyOnlyStoresOneItemWithoutChangingFlagshipAndPersists'],
  ['method','ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.ComponentPurchase_BuyAndEquipReplacesOneItemAndPersists'],
 ],
};
function verifyTestCoverage(xml,suite){
 const attrs=xmlAttributes(xml.match(/<test-run\b[^>]+>/)?.[0]);
 const failures=failedCases(xml);
 if(suite==='editmode')assert(+attrs.total===145&&+attrs.passed===143&&+attrs.failed===2&&JSON.stringify(failures)===JSON.stringify(baselineFailures),'EditMode has failures beyond the reproduced baseline');
 else assert(attrs.result==='Passed'&&+attrs.failed===0&&+attrs.passed>0,'Tests not passing '+suite);
 const cases=[...xml.matchAll(/<test-case\b[^>]+>/g)].map(m=>xmlAttributes(m[0]));
 const targets=requiredTestTargets[suite].map(([kind,target])=>{
  // NUnit writes parameterized arguments after the method's full name; use
  // test-case results, never a suite name or a textual filter mention.
  const matched=cases.filter(c=>kind==='class'?(c.classname===target||String(c.fullname||'').startsWith(target+'.')):(c.fullname===target||String(c.fullname||'').startsWith(target+'(')));
  const executed=matched.filter(c=>c.result==='Passed'||c.result==='Failed');
  assert(executed.length>0,'Required test target never executed: '+suite+' '+target);
  assert(executed.every(c=>c.result==='Passed'||suite==='editmode'&&baselineFailures.some(f=>f.fullname===c.fullname)),'Required target failed beyond baseline: '+target);
  return {kind,target,executed:executed.length,skipped:matched.length-executed.length};
 });
 return {result:attrs.result,passed:+attrs.passed,total:+attrs.total,failed:+attrs.failed,skipped:+attrs.skipped,newFailures:0,knownBaselineFailures:failures,targets};
}
const tests={};for(const suite of ['editmode','playmode'])tests[suite]=verifyTestCoverage(fs.readFileSync(root+'/validation/'+suite+'.xml','utf8'),suite);
const baselineComparison={verifiedAt:new Date().toISOString(),method:'In the disposable project, restored before-task production mapping, geometry and original textures from the recorded snapshot. Kept the same current test files; production was read-only. Re-ran all 50 SpecialCardCatalogTests, then restored current production files in the disposable project.',baselineXmlSha256:hash(baselineXmlPath),currentXmlSha256:hash(root+'/validation/editmode.xml'),baseline:{total:50,passed:48,failed:2},sameFailures:true,failures:baselineFailures};
fs.writeFileSync(root+'/validation/baseline-comparison.json',JSON.stringify(baselineComparison,null,2));
const captures=read(root+'/validation/ui-review/report.json');assert(captures.frames.length===238&&captures.assets.length===300,'Incomplete UI capture');
for(const f of captures.frames){assert(!f.error&&f.scalerVerified,'UI frame error '+f.file);assert(fs.existsSync(root+'/validation/ui-review/'+f.file),'Missing screenshot');for(const a of f.visibleArt){assert(Math.abs(a.sourceAspect-a.displayAspect)<=.002,'Stretched UI');assert(a.screenRect.width>0&&a.screenRect.height>0,'Empty rect');}}
const assetPath=p=>String(p||'').replace(/\\/g,'/');
const mainAsset=id=>'Assets/_Project/Resources/ModularShip/Layers/'+id+'.png';
const iconAsset=id=>'Assets/_Project/Resources/ComponentArt/'+id+'.png';
const detailFrames=captures.frames.filter(f=>f.state==='Detail');
assert(detailFrames.length===150,'Need 150 detail frames');
unique(detailFrames.map(f=>f.id),150,'Repeated detail identity');
for(const f of detailFrames){assert(stage.entries.some(e=>e.id===f.id),'Unknown detail identity '+f.id);assert(f.visibleArt.some(a=>assetPath(a.assetPath)===mainAsset(f.id)),'Detail does not show its own main artwork: '+f.id);}
const observed={};for(const state of ['Detail','Shop','Inventory']){
 const frames=captures.frames.filter(f=>f.state===state);
 const ids=[...new Set(frames.flatMap(f=>f.visibleArt.filter(a=>{
  const p=assetPath(a.assetPath),id=path.posix.basename(p,'.png');
  if(state==='Inventory')return String(a.name||'').startsWith('Shipyard Inventory Art ')&&p===iconAsset(id);
  if(state==='Detail')return p===mainAsset(f.id);
  return p===mainAsset(id);
 }).map(a=>path.posix.basename(assetPath(a.assetPath),'.png'))))];
 assert(ids.length===150,state+' does not show every equipment: '+ids.length);for(const e of stage.entries)assert(ids.includes(e.id),'Missing '+state+' '+e.id);observed[state]={frames:frames.length,uniqueEquipment:ids.length};
}
for(const a of captures.assets){const p='UnityClient/'+a.path,e=assets.find(e=>e.path===p);assert(e&&e.sha256===a.sha256,'Capture used stale asset '+p);assert(a.alpha&&a.filter==='Bilinear'&&a.mipmapCount>1,'Wrong import settings '+p);}
const report={verifiedAt:new Date().toISOString(),equipmentCount:150,uniqueMainImages:150,uniqueInventoryIcons:150,dedicatedCardsReviewed:90,confirmedCardShapes:audit.entries.filter(e=>e.depictsEquipment===true).length,partialCardEvidence:audit.entries.filter(e=>e.depictsEquipment==='uncertain').length,preservedTextureGuids:preserved,unchangedEquipmentIdsStatsAndLore:true,tests,ui:observed,importedAssets:300,packaged:false};
fs.writeFileSync(root+'/verification.json',JSON.stringify(report,null,2));console.log(JSON.stringify(report));
