const fs=require('fs'),path=require('path'),crypto=require('crypto');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root='D:/Game',out=root+'/Artifacts/ComponentShopImplementation20261012',dest=root+'/UnityClient/Assets/_Project/Resources/ComponentShopV3';
const native='C:/Users/Admin/.codex/generated_images/01a12c30-792a-7823-90a1-20ed3bc62b18/';
const files={shipyard_hall:'exec-3bdd2795-2331-4c71-be66-ceba14be28c7.png',detail_parchment:'exec-a656aecc-ed16-41df-a648-420b4ce60002.png',instrument_bench:'exec-3a5906f0-60cc-4c72-9626-785c819c4d08.png',heavy_support:'exec-c2e35738-68e1-4390-81bf-c46a4cf98d08.png',sail_rig:'exec-39b75c62-e2ea-488a-a8fc-44519241d144.png'};
const guid=()=>crypto.randomUUID().replaceAll('-','');
fs.mkdirSync(dest,{recursive:true});fs.mkdirSync(out+'/generated',{recursive:true});
if(!fs.existsSync(dest+'.meta'))fs.writeFileSync(dest+'.meta',`fileFormatVersion: 2\nguid: ${guid()}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n`);
(async()=>{const report=[];for(const[id,file]of Object.entries(files)){
const active=id==='shipyard_hall',assetDir=active?dest:out+'/deferred-assets';fs.mkdirSync(assetDir,{recursive:true});
const src=native+file,archive=out+'/generated/'+id+'.png',target=assetDir+'/'+id+'.png';
fs.copyFileSync(src,archive);const metadata=await sharp(src).metadata();let pipeline=sharp(src),bounds=null;
if(id!=='shipyard_hall'){
 const{data,info}=await sharp(src).ensureAlpha().raw().toBuffer({resolveWithObject:true});let l=info.width,t=info.height,r=0,b=0;
 for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++)if(data[(y*info.width+x)*4+3]>12){l=Math.min(l,x);r=Math.max(r,x);t=Math.min(t,y);b=Math.max(b,y);}
 bounds={left:l,top:t,width:r-l+1,height:b-t+1};pipeline=pipeline.extract(bounds);
 if(id==='detail_parchment')pipeline=pipeline.resize(1040,1760,{fit:'fill'});
 else {const pad=Math.ceil(Math.max(bounds.width,bounds.height)*.008);pipeline=pipeline.extend({top:pad,bottom:pad,left:pad,right:pad,background:{r:0,g:0,b:0,alpha:0}});}
}
await pipeline.png().toFile(target);
if(!fs.existsSync(target+'.meta')){
let meta=fs.readFileSync(root+'/UnityClient/Assets/_Project/Resources/ComponentArt/bow_oak_ram.png.meta','utf8').replace(/guid: [a-f0-9]+/,`guid: ${guid()}`).replace('enableMipMap: 1','enableMipMap: 0');fs.writeFileSync(target+'.meta',meta);
}
const actual=await sharp(target).metadata();report.push({id,status:active?'installed':'deferred by user; excluded from game resources',native:src,archive,target,input:{width:metadata.width,height:metadata.height,alpha:metadata.hasAlpha},crop:bounds,output:{width:actual.width,height:actual.height,alpha:actual.hasAlpha},sha256:crypto.createHash('sha256').update(fs.readFileSync(target)).digest('hex')});
}fs.writeFileSync(out+'/asset-manifest.json',JSON.stringify({mode:'Built-in image_gen; mechanical alpha crop/padding/resize only',assets:report},null,2));console.log(JSON.stringify(report,null,2));})().catch(e=>{console.error(e);process.exit(1)});
