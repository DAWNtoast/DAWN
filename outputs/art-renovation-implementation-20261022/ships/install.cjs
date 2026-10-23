const fs=require('fs'),path=require('path'),crypto=require('crypto');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root='D:/Game';
const dir=__dirname;
const target=path.join(root,'UnityClient/Assets/_Project/Resources/NavalTactics/Ships');
const template=fs.readFileSync(path.join(root,'UnityClient/Assets/_Project/Resources/NavalTactics/tactical_ship_v1.png.meta'),'utf8');
const ids=['sloop','merchant','patrol','relic','salvage','courier','provision','gunship','barge','privateer','occult','clipper',...['boss','legendary'].flatMap(k=>['fire','tide','blood','thunder','curse','arcane'].map(s=>k+'_'+s))];
function meta(file){
 if(fs.existsSync(file+'.meta')){
  fs.writeFileSync(file+'.meta',fs.readFileSync(file+'.meta','utf8').replace(/enableMipMap: 0/,'enableMipMap: 1'));
  return;
 }
 const guid=crypto.createHash('md5').update('naval-tactics-20261022:'+path.basename(file)).digest('hex');
 fs.writeFileSync(file+'.meta',template.replace(/enableMipMap: 0/,'enableMipMap: 1').replace(/guid: [0-9a-f]+/,'guid: '+guid).replace(/spriteID: [0-9a-f]+/,'spriteID: '+crypto.createHash('md5').update(guid+':sprite').digest('hex')));
}
async function main(){
 const rows=JSON.parse(fs.readFileSync(path.join(dir,'accepted.json'),'utf8'));
 if(!process.argv.includes('--partial') && (rows.length!==24 || ids.some(id=>!rows.some(r=>r.id===id))))throw Error('Requires exactly 24 approved ships');
 const selected = ids.filter(id=>rows.some(r=>r.id===id));
 fs.mkdirSync(target,{recursive:true});fs.mkdirSync(path.join(dir,'originals'),{recursive:true});
 if(!fs.existsSync(target+'.meta'))fs.writeFileSync(target+'.meta','fileFormatVersion: 2\nguid: '+crypto.createHash('md5').update('naval-tactics-20261022:Ships').digest('hex')+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n');
 const qa=[];
 for(const id of selected){
  const r=rows.find(r=>r.id===id);
  fs.copyFileSync(r.path,path.join(dir,'originals',id+'.png'));
  const {data,info}=await sharp(r.path).rotate(r.rotate||0).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  let l=info.width,t=info.height,rr=-1,b=-1,zero=0;
  for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++){
   const a=data[(y*info.width+x)*4+3];if(a===0)zero++;
   if(a>2){l=Math.min(l,x);rr=Math.max(rr,x);t=Math.min(t,y);b=Math.max(b,y)}
  }
  if(zero/(info.width*info.height)<.1)throw Error(id+' lacks real background transparency');
  const bounds={left:Math.max(0,l-4),top:Math.max(0,t-4),width:Math.min(info.width,rr+5)-Math.max(0,l-4),height:Math.min(info.height,b+5)-Math.max(0,t-4)};
  const sprite=await sharp(data,{raw:info}).extract(bounds).resize({width:900,height:1290,fit:'inside'}).png().toBuffer();
  const sm=await sharp(sprite).metadata();
  const out=path.join(target,id+'_v2.png');
  await sharp({create:{width:1024,height:1536,channels:4,background:{r:0,g:0,b:0,alpha:0}}}).composite([{input:sprite,left:Math.round((1024-sm.width)/2),top:Math.round((1536-sm.height)/2)}]).png().toFile(out);
  meta(out);
  qa.push({id,source:r.path,rotationDegrees:r.rotate||0,crop:bounds,sourceDimensions:[info.width,info.height],outputDimensions:[1024,1536],visibleSize:[sm.width,sm.height],transparentSourceFraction:zero/(info.width*info.height),asset:path.relative(root,out).replaceAll('\\','/'),sha256:crypto.createHash('sha256').update(fs.readFileSync(out)).digest('hex')});
 }
 const comps=[],w=220,h=368,cols=6;
 for(let i=0;i<selected.length;i++){
  const buf=await sharp(path.join(target,selected[i]+'_v2.png')).resize(204,306).png().toBuffer();
  comps.push({input:buf,left:i%cols*w+8,top:Math.floor(i/cols)*h+34});
  comps.push({input:Buffer.from('<svg width="220" height="28"><text x="110" y="21" font-family="Arial" font-size="17" text-anchor="middle" fill="#fff">'+selected[i]+'</text></svg>'),left:i%cols*w,top:Math.floor(i/cols)*h});
 }
 await sharp({create:{width:cols*w,height:4*h,channels:4,background:'#173a44'}}).composite(comps).png().toFile(path.join(dir,'ships-contact.png'));
 fs.writeFileSync(path.join(dir,'verification.json'),JSON.stringify({builtInImageGeneration:true,preparation:'Lossless direction normalization, transparent crop and proportional resize/padding only; no procedural repaint.',count:qa.length,ships:qa},null,2));
 console.log(JSON.stringify({count:qa.length,output:target,contact:path.join(dir,'ships-contact.png')}));
}
main().catch(e=>{console.error(e);process.exit(1)});
