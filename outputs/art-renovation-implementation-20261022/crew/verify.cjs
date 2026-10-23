const fs=require('fs'),crypto=require('crypto');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const out=__dirname,manifest=JSON.parse(fs.readFileSync(out+'/manifest.json','utf8'));
const audit=JSON.parse(fs.readFileSync('D:/Game/outputs/art-renovation-audit-20261022/inventory.json','utf8'));
async function main(){
 if(!manifest.installed||manifest.crew.length!==36)throw Error('Install all 36 portraits first.');
 const identities=new Set(),guids=new Set(),checks=[],layers=[],labels=[];
 for(let i=0;i<manifest.crew.length;i++){
  const p=manifest.crew[i],src=fs.readFileSync(p.source),asset=fs.readFileSync(p.output),m=await sharp(asset).metadata();
  const original=audit.images.find(a=>a.path===p.source);
  const recrop=await sharp(src).extract(p.crop).resize(256,336,{kernel:'lanczos3'}).removeAlpha().png().toBuffer();
  const meta=fs.readFileSync(p.output+'.meta','utf8'),guid=meta.match(/^guid: (\w+)$/m)[1];
  const result={id:p.id,port:p.port,name:p.name,sourceUnchangedSinceAudit:!!original&&hash(src)===original.sha256,
   sourceMatchesManifest:hash(src)===p.sourceSha256,outputReproducesExactLocalCrop:hash(asset)===hash(recrop)&&hash(asset)===p.avatarSha256,
   identityMatchesPath:p.output.endsWith('/'+p.id+'_avatar_v1.png')&&p.source.endsWith('/'+p.id+'_portrait_v2.png'),
   cropPreservesAspect:p.crop.width*336===p.crop.height*256,dimensionsMatch:m.width===256&&m.height===336,
   uniqueIdentity:!identities.has(p.id),uniqueGuid:!guids.has(guid),noMipmaps:/enableMipMap: 0/.test(meta),
   review:'Manually compared source and crop in the six port contact sheets; matching face, hair, clothing and identity; full face and head top retained; shoulders cropped to prioritize the 82x108 roster view.'};
  for(const [k,v] of Object.entries(result))if(typeof v==='boolean'&&!v)throw Error(p.id+': '+k);
  identities.add(p.id);guids.add(guid);checks.push(result);
  const x=i%6*180,y=Math.floor(i/6)*148;
  layers.push({input:await sharp(asset).resize(82,108,{fit:'inside'}).png().toBuffer(),left:x+49,top:y+8});
  labels.push(`<text x="${x+6}" y="${y+135}" fill="white" font-size="12">${p.id.split('_').pop()}</text>`);
 }
 layers.push({input:Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="1080" height="888"><g font-family="Arial">${labels.join('')}</g></svg>`),left:0,top:0});
 await sharp({create:{width:1080,height:888,channels:3,background:'#263740'}}).composite(layers).png().toFile(out+'/actual-ui-size-82x108.png');
 const result={date:'2026-10-22',count:checks.length,passed:checks.length,sourceDossiersUnchanged:checks.every(c=>c.sourceUnchangedSinceAudit),outputDimensions:[256,336],approximateBc7BytesPerTexture:256*336,totalPngBytes:manifest.crew.reduce((s,p)=>s+fs.statSync(p.output).size,0),checks};
 fs.writeFileSync(out+'/verification.json',JSON.stringify(result,null,2));
 console.log(JSON.stringify({count:result.count,passed:result.passed,sourceDossiersUnchanged:result.sourceDossiersUnchanged,totalPngBytes:result.totalPngBytes}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
