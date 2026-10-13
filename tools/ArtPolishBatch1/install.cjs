const fs=require('fs'),path=require('path'),crypto=require('crypto');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root='D:/Game',out=root+'/Artifacts/ArtPolishBatch1_20261012';
const baseline=JSON.parse(fs.readFileSync(out+'/source-baseline.json','utf8'));
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const stage=out+'/production-ready';
const apply=process.argv.includes('--apply');
// A second installation may refine importer settings. Preserve the prior
// staged fingerprints before regenerating them, so concurrent edits still fail.
const priorHashes=new Map();
if(fs.existsSync(out+'/integration-report.json')){
 const prior=JSON.parse(fs.readFileSync(out+'/integration-report.json','utf8'));
 if(prior.applied)for(const record of prior.records)for(const suffix of ['', '.meta']){
  const file=stage+'/'+record.relative+suffix;
  if(fs.existsSync(file))priorHashes.set(record.relative+suffix,hash(file));
 }
}
const boundsCode=fs.readFileSync(root+'/UnityClient/Assets/_Project/Presentation/Runtime/ComponentShopDisplayGeometry.cs','utf8');
const bounds=Object.fromEntries([...boundsCode.matchAll(/case "([^"]+)": return Rect.MinMaxRect\((\d+)f, (\d+)f, (\d+)f, (\d+)f\);/g)].map(m=>[m[1],m.slice(2).map(Number)]));
const routes=[['shattered_sail','LegacyDemo/shattered_sail_event_sheet_v2'],['golden_tide','LegacyDemo/golden_tide_event_sheet_v2'],['moon_tide','LegacyDemo/moon_tide_event_sheet_v2'],['treasure_hunt','TreasureHunt/treasure_hunt_event_sheet']];
const assetPrefix='UnityClient/Assets/_Project/Resources/';
const records=[];
async function alphaBounds(file){
 const {data,info}=await sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 let left=info.width,top=info.height,right=-1,bottom=-1,opaque=0;
 for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++)if(data[(y*info.width+x)*4+3]>=8){left=Math.min(left,x);right=Math.max(right,x);top=Math.min(top,y);bottom=Math.max(bottom,y);opaque++;}
 if(right<left||bottom<top)throw new Error('Empty alpha: '+file);
 if(opaque>info.width*info.height*.9)throw new Error('Likely opaque background: '+file);
 return {left,top,width:right-left+1,height:bottom-top+1,sourceWidth:info.width,sourceHeight:info.height,coverage:opaque/(info.width*info.height)};
}
function destination(relative){const p=stage+'/'+relative;fs.mkdirSync(path.dirname(p),{recursive:true});return p;}
async function prepareEquipment(){
 for(const id of Object.keys(bounds)){
  const source=out+'/equipment/'+id+'.png';
  if(!fs.existsSync(source))throw new Error('Missing generated equipment: '+id);
  const box=await alphaBounds(source),[l,t,r,b]=bounds[id];
  // The existing UI is authored in a square 512px normalized canvas. Keep this
  // contract and double native precision without cropping or stretching art.
  const available={x:l*2+4,y:t*2+4,width:(r-l)*2-8,height:(b-t)*2-8};
  const piece=await sharp(source).extract({left:box.left,top:box.top,width:box.width,height:box.height}).resize({width:available.width,height:available.height,fit:'inside',kernel:'lanczos3'}).png().toBuffer();
  const size=await sharp(piece).metadata();
  const left=Math.round(available.x+(available.width-size.width)/2),top=Math.round(available.y+(available.height-size.height)/2);
  const relative=assetPrefix+'ModularShip/Layers/'+id+'.png',target=destination(relative);
  await sharp({create:{width:1024,height:1024,channels:4,background:{r:0,g:0,b:0,alpha:0}}}).composite([{input:piece,left,top}]).png().toFile(target);
  records.push({kind:'equipment',id,relative,source,staged:target,width:1024,height:1024,alphaBounds:box,placement:{left,top,width:size.width,height:size.height},preservedDisplayBounds:bounds[id]});
 }
}
async function prepareEvents(){
 const unique=new Set();
 for(const [route,resource]of routes){
  const pieces=[],sourceCells=[];
  for(let index=0;index<20;index++){
   const source=out+'/events/'+route+'/cell-'+String(index).padStart(2,'0')+'.png';
   if(!fs.existsSync(source))throw new Error('Missing generated event: '+route+'/'+index);
   const meta=await sharp(source).metadata(),sha256=hash(source);
   if(meta.width<768||meta.height<640)throw new Error('Undersized event original: '+source);
   if(unique.has(sha256))throw new Error('Duplicate generated event: '+source);unique.add(sha256);
   if(meta.hasAlpha){const stats=await sharp(source).stats();if(stats.channels[3].min!==255)throw new Error('Event environment must be opaque: '+source);}
   sourceCells.push({index,source,sha256,width:meta.width,height:meta.height});
   const input=await sharp(source).resize(768,640,{fit:'cover',position:'centre',kernel:'lanczos3'}).flatten({background:'#142c37'}).removeAlpha().png().toBuffer();
   pieces.push({input,left:index%5*768,top:Math.floor(index/5)*640});
  }
  const relative=assetPrefix+resource+'.png',target=destination(relative);
  await sharp({create:{width:3840,height:2560,channels:3,background:'#142c37'}}).composite(pieces).png().toFile(target);
  records.push({kind:'event-atlas',id:route,relative,staged:target,width:3840,height:2560,grid:{columns:5,rows:4,cellWidth:768,cellHeight:640},cells:20,originalGridPreserved:true,sourceCells});
 }
}
async function prepareIcons(){
 for(const id of ['instrument_brass_compass','instrument_guild_engine']){
  const source=out+'/icons/'+id+'.png',relative=assetPrefix+'ComponentArt/'+id+'.png',target=destination(relative);
  await sharp(source).resize(512,512,{fit:'fill',kernel:'lanczos3'}).png().toFile(target);
  records.push({kind:'icon-crop-repair',id,relative,source,staged:target,width:512,height:512});
 }
}
function rewriteMeta(record){
 const original=out+'/originals/'+record.relative+'.meta';
 let meta=fs.readFileSync(original,'utf8');
 const event=record.kind==='event-atlas',size=event?4096:record.kind==='equipment'?2048:2048;
 meta=meta.replace(/(\bmaxTextureSize: )\d+/g,(_,prefix)=>prefix+size).replace(/(\bfilterMode: )\d+/g,(_,prefix)=>prefix+'1');
 if(event){
  // High-resolution painting needs filtered minification in small UI windows.
  meta=meta.replace(/(\benableMipMap: )\d+/,(_,prefix)=>prefix+'1');
  // Only the whole-image sprite exists. Preserve its GUID/fileID and make its
  // rect cover the enlarged atlas for any retained Sprite consumers.
  meta=meta.replace(/(        width: )1536\b/g,(_,prefix)=>prefix+'3840').replace(/(        height: )1024\b/g,(_,prefix)=>prefix+'2560');
 }
 if(event||record.kind==='equipment'){
  const nl=meta.includes('\r\n')?'\r\n':'\n';
  const settings=['  - serializedVersion: 4','    buildTarget: Standalone','    maxTextureSize: '+size,'    resizeAlgorithm: 0','    textureFormat: 25','    textureCompression: 2','    compressionQuality: 100','    crunchedCompression: 0','    allowsAlphaSplitting: 0','    overridden: 1','    ignorePlatformSupport: 0','    androidETC2FallbackOverride: 0','    forceMaximumCompressionQuality_BC6H_BC7: 0'].join(nl);
  const platform=/(  - serializedVersion: 4\r?\n    buildTarget: Standalone\r?\n)([\s\S]*?)(?=\r?\n  [^- ]|\r?\n  - serializedVersion|$)/;
  if(platform.test(meta))meta=meta.replace(platform,()=>settings);
  else meta=meta.replace(/  spriteSheet:/,settings+nl+'  spriteSheet:');
 }
 fs.writeFileSync(destination(record.relative+'.meta'),meta);
 const guid=value=>value.match(/^guid: ([^\r\n]+)/m)?.[1];
 if(guid(meta)!==guid(fs.readFileSync(original,'utf8')))throw new Error('GUID changed: '+record.id);
}
async function main(){
 fs.mkdirSync(stage,{recursive:true});
 await prepareEquipment();await prepareEvents();await prepareIcons();
 for(const record of records){rewriteMeta(record);record.sha256=hash(record.staged);record.metaSha256=hash(record.staged+'.meta');}
 if(records.length!==31)throw new Error('Unexpected deliverable count');
 if(apply){
  for(const record of records)for(const suffix of ['', '.meta']){
   const relative=record.relative+suffix,original=baseline.records.find(r=>r.relative===relative),current=root+'/'+relative;
   if(!original)throw new Error('Missing backup: '+relative);
   const currentHash=hash(current),newHash=hash(stage+'/'+relative);
   if(currentHash!==original.sha256&&currentHash!==newHash&&currentHash!==priorHashes.get(relative))throw new Error('Concurrent source change: '+relative);
  }
  for(const record of records)for(const suffix of ['', '.meta'])fs.copyFileSync(stage+'/'+record.relative+suffix,root+'/'+record.relative+suffix);
 }
 fs.writeFileSync(out+'/integration-report.json',JSON.stringify({generatedAtUtc:new Date().toISOString(),applied:apply,counts:{equipment:25,eventCells:80,eventAtlases:4,icons:2},noPackage:true,records},null,2));
 console.log(JSON.stringify({applied:apply,records:records.length,staged:stage}));
}
main().catch(error=>{console.error(error);process.exitCode=1;});
