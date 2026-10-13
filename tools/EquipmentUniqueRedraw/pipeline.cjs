const fs=require('fs'),path=require('path'),crypto=require('crypto');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const workspace='D:/Game',out=workspace+'/Artifacts/EquipmentUniqueRedraw20261012';
const resources='UnityClient/Assets/_Project/Resources/';
const catalog='UnityClient/Assets/_Project/Presentation/Runtime/ComponentArtCatalog.cs';
const geometry='UnityClient/Assets/_Project/Presentation/Runtime/ComponentShopDisplayGeometry.cs';
const csv=resources+'Data/component_catalog_v2.csv';
const balanceCsv='outputs/balance_configs/component_config.csv';
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const json=p=>JSON.parse(fs.readFileSync(p,'utf8'));
const writeJson=(p,v)=>{fs.mkdirSync(path.dirname(p),{recursive:true});fs.writeFileSync(p,JSON.stringify(v,null,2)+'\n');};
function roster(){return json(out+'/card-reference-audit.json').entries;}
async function bounds(file,threshold=1){
 const {data,info}=await sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 let x0=info.width,y0=info.height,x1=-1,y1=-1,zero=0,opaque=0,border=0;
 for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++){
  const a=data[(y*info.width+x)*4+3];if(a===0)zero++;if(a>=250)opaque++;
  if(a>=threshold){x0=Math.min(x0,x);y0=Math.min(y0,y);x1=Math.max(x1,x);y1=Math.max(y1,y);}
  if(x===0||y===0||x===info.width-1||y===info.height-1)border=Math.max(border,a);
 }
 return {width:info.width,height:info.height,bounds:[x0,y0,x1+1,y1+1],zero,opaque,border};
}
async function snapshot(){
 const previous=fs.existsSync(out+'/baseline.json')?json(out+'/baseline.json'):null;
 const files=[catalog,geometry,csv,balanceCsv];
 for(const f of fs.readdirSync(workspace+'/UnityClient/Assets/_Project/Tests/EditMode'))if(/^(Component.*|ExpandedComponentCatalogTests|RegionalShopFurnitureTests)\.cs(\.meta)?$/.test(f))files.push('UnityClient/Assets/_Project/Tests/EditMode/'+f);
 for(const folder of ['ComponentArt','ModularShip/Layers'])for(const f of fs.readdirSync(workspace+'/'+resources+folder))if(/\.png(\.meta)?$/.test(f))files.push(resources+folder+'/'+f);
 const entries=previous?.files||[];
 for(const relative of files){if(entries.some(e=>e.path===relative))continue;const source=workspace+'/'+relative,target=out+'/baseline/'+relative;fs.mkdirSync(path.dirname(target),{recursive:true});fs.copyFileSync(source,target);entries.push({path:relative,sha256:hash(fs.readFileSync(source))});}
 writeJson(out+'/baseline.json',{at:previous?.at||new Date().toISOString(),equipmentCount:150,previousArtSources:25,files:entries});
 console.log('Backed up '+entries.length+' current production files.');
}
async function stage(){
 const manifestFiles=fs.readdirSync(out).filter(n=>/^generation-.*\.json$/.test(n));
 const approved=new Map();for(const file of manifestFiles)for(const e of json(out+'/'+file).entries){
  if((e.generatedMetadata?.qaStatus||e.qaStatus)!=='approved')continue;
  if(approved.has(e.id))throw Error('Duplicate approved generation: '+e.id);
  approved.set(e.id,{...e,manifest:file});
 }
 const entries=[];
 for(const e of roster()){
  const id=e.equipmentId,source=out+'/generated/'+id+'.png';if(!fs.existsSync(source))continue;
  const g=approved.get(id);if(!g)continue;
  const qa=g.generatedMetadata?.qaStatus||g.qaStatus;if(qa!=='approved')continue;
  const native=await bounds(source,8);if(!native.zero||!native.opaque||native.border>=16)throw Error('Incomplete alpha canvas: '+id);
  // Preserve the object's alpha and a soft fringe while ignoring occasional
  // near-invisible alpha=1 encoder specks on the native canvas boundary.
  const [bx0,by0,bx1,by1]=native.bounds;
  const [x0,y0,x1,y1]=[Math.max(0,bx0-8),Math.max(0,by0-8),Math.min(native.width,bx1+8),Math.min(native.height,by1+8)];
  const main=out+'/staged/ModularShip/Layers/'+id+'.png',icon=out+'/staged/ComponentArt/'+id+'.png';
  fs.mkdirSync(path.dirname(main),{recursive:true});fs.mkdirSync(path.dirname(icon),{recursive:true});
  const cropped=await sharp(source).extract({left:x0,top:y0,width:x1-x0,height:y1-y0}).resize(840,840,{fit:'inside',kernel:'lanczos3'}).png().toBuffer();
  const cm=await sharp(cropped).metadata();
  await sharp({create:{width:1024,height:1024,channels:4,background:{r:0,g:0,b:0,alpha:0}}}).composite([{input:cropped,left:Math.floor((1024-cm.width)/2),top:Math.floor((1024-cm.height)/2)}]).png().toFile(main);
  await sharp(main).resize(512,512,{kernel:'lanczos3'}).png().toFile(icon);
  const measured=await bounds(main,8);
  entries.push({id,name:e.name,slot:e.slot,rarity:e.rarity,previousArtSource:e.oldArtSource,nativePath:source,nativeSha256:hash(fs.readFileSync(source)),native,mainPath:main,iconPath:icon,mainSha256:hash(fs.readFileSync(main)),iconSha256:hash(fs.readFileSync(icon)),bounds1024:measured.bounds,bounds512:measured.bounds.map(x=>x/2),generationManifest:g.manifest,cardIds:e.cardIds,depictsEquipment:e.depictsEquipment,cardNotes:e.identityNotes,cardPaths:e.artPaths});
 }
 writeJson(out+'/staging.json',{status:entries.length===150?'all_staged':'partial',count:entries.length,entries});
 console.log('Staged '+entries.length+'/150 independent main images and matching transparent inventory icons.');
}
function textureMeta(id,existing){
 const template=fs.readFileSync(out+'/baseline/'+resources+'ModularShip/Layers/plating_braced_ribs.png.meta','utf8');
 const guid=existing?fs.readFileSync(existing,'utf8').match(/^guid: (.+)$/m)[1]:crypto.randomUUID().replaceAll('-','');
 return template.replace(/^guid: .+$/m,'guid: '+guid);
}
async function install(){
 const report=json(out+'/staging.json');if(report.count!==150)throw Error('Install requires all 150 staged and approved');
 if(new Set(report.entries.map(e=>e.nativeSha256)).size!==150)throw Error('Duplicate source art');
 const baseline=json(out+'/baseline.json');
 const changed=[];
 for(const e of report.entries){for(const [folder,from] of [['ModularShip/Layers',e.mainPath],['ComponentArt',e.iconPath]]){
  const relative=resources+folder+'/'+e.id+'.png',target=workspace+'/'+relative,meta=target+'.meta';
  const old=baseline.files.find(x=>x.path===relative);
  if(fs.existsSync(target)&&old&&hash(fs.readFileSync(target))!==old.sha256&&hash(fs.readFileSync(target))!==hash(fs.readFileSync(from)))throw Error('Asset changed since baseline: '+relative);
  fs.copyFileSync(from,target);fs.writeFileSync(meta,textureMeta(e.id,fs.existsSync(meta)?meta:null));
  changed.push({path:relative,sha256:hash(fs.readFileSync(target)),metaSha256:hash(fs.readFileSync(meta)),newAsset:!old});
 }}
 for(const csvPath of [csv,balanceCsv]){
  let data=fs.readFileSync(workspace+'/'+csvPath,'utf8');
  data=data.split(/\r?\n/).map((l,i)=>{if(!i||!l.trim()||l.startsWith('#'))return l;const c=l.split(',');if(c.length!==22)throw Error('Invalid csv columns');c[20]=c[0];return c.join(',');}).join('\n');
  fs.writeFileSync(workspace+'/'+csvPath,data);
 }
 let code=fs.readFileSync(workspace+'/'+catalog,'utf8');
 code=code.replace('stable while ArtSourceId may point several variants at one compatible model.','stable while each equipment ID has its own independent visual identity.');
 let count=0;
 code=code.replace(/result\.Add\(Extra\(([^\r\n]+)\)\);/g,(line,args)=>{const strings=[...args.matchAll(/"((?:[^"\\]|\\.)*)"/g)];if(strings.length!==7)throw Error('Extra arguments changed');const art=strings[4],id=strings[0][1];count++;return 'result.Add(Extra('+args.slice(0,art.index)+'"'+id+'"'+args.slice(art.index+art[0].length)+'));';});
 if(count!==25)throw Error('Expected 25 expanded entries');fs.writeFileSync(workspace+'/'+catalog,code);
 let geom=fs.readFileSync(workspace+'/'+geometry,'utf8');
 const start=geom.indexOf('        private static Rect OpaquePixels(string id)');if(start<0)throw Error('Geometry method not found');
 geom=geom.slice(0,start)+`        private static Rect OpaquePixels(string id)\n        {\n            // Measured at alpha >= 8 from each normalized 1024 px sprite, expressed\n            // in the existing 512 px coordinate space. The caller adds 24 native\n            // pixels of clearance for translucent edges and fine rigging tips.\n            switch (id)\n            {\n`+report.entries.map(e=>'                case "'+e.id+'": return Rect.MinMaxRect('+e.bounds512.map(v=>Number(v).toFixed(1)+'f').join(', ')+');').join('\n')+`\n                default:\n                    throw new ArgumentException("Missing measured component art: " + id, nameof(id));\n            }\n        }\n    }\n}\n`;
 fs.writeFileSync(workspace+'/'+geometry,geom);
 writeJson(out+'/integration.json',{at:new Date().toISOString(),equipmentCount:150,uniqueMainImages:150,uniqueInventoryIcons:150,packaged:false,assets:changed,code:[csv,balanceCsv,catalog,geometry]});
 console.log('Installed 150 identities, 300 textures, measured bounds and independent catalog mappings.');
}
async function main(){const action=process.argv[2];if(action==='snapshot')await snapshot();else if(action==='stage')await stage();else if(action==='install')await install();else throw Error('Unknown action');}
main().catch(e=>{console.error(e);process.exit(1);});
