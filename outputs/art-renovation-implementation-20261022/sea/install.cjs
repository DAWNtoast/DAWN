const fs=require('fs'),path=require('path'),crypto=require('crypto');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const dir=__dirname,root='D:/Game',target=path.join(root,'UnityClient/Assets/_Project/Resources/NavalTactics/Seas');
const rows=JSON.parse(fs.readFileSync(path.join(dir,'accepted.json'),'utf8'));
const template=fs.readFileSync(path.join(root,'UnityClient/Assets/_Project/Resources/NavalTactics/tactical_sea_v1.png.meta'),'utf8');
const digest=x=>crypto.createHash('md5').update(x).digest('hex');
async function metrics(file){
 const {data,info}=await sharp(file).removeAlpha().raw().toBuffer({resolveWithObject:true});
 const hist=new Array(256).fill(0);let n=0,sum=0,sum2=0,bright=0;
 for(let y=Math.floor(info.height*.15);y<info.height*.85;y++)for(let x=Math.floor(info.width*.15);x<info.width*.85;x++){
  const i=(y*info.width+x)*info.channels,l=.2126*data[i]+.7152*data[i+1]+.0722*data[i+2];
  hist[Math.round(l)]++;n++;sum+=l;sum2+=l*l;if(l>180)bright++;
 }
 const quant=q=>{let c=0;for(let i=0;i<256;i++){c+=hist[i];if(c>=n*q)return i}};
 return {size:[info.width,info.height],central70Percent:{lumaMean:sum/n,lumaStdDev:Math.sqrt(sum2/n-(sum/n)**2),p05:quant(.05),p95:quant(.95),brightFraction:bright/n}};
}
(async()=>{
 if(rows.length!==6)throw Error('Expected 6 images');
 fs.mkdirSync(target,{recursive:true});fs.mkdirSync(path.join(dir,'originals'),{recursive:true});
 if(!fs.existsSync(target+'.meta'))fs.writeFileSync(target+'.meta','fileFormatVersion: 2\nguid: '+digest('naval-tactics-20261022:Seas')+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n');
 const qa=[],comps=[],cw=426,ch=460,cols=3;
 for(let i=0;i<rows.length;i++){
  const r=rows[i],out=path.join(target,r.id+'_v2.png'),m=await sharp(r.path).metadata();
  if(m.width!==m.height||m.width<1024)throw Error(r.id+' invalid dimensions');
  fs.copyFileSync(r.path,path.join(dir,'originals',r.id+'_v2.png'));fs.copyFileSync(r.path,out);
  if(!fs.existsSync(out+'.meta'))fs.writeFileSync(out+'.meta',template.replace(/guid: [0-9a-f]+/,'guid: '+digest('naval-tactics-20261022:sea:'+r.id)).replace(/enableMipMap: 0/,'enableMipMap: 1').replace(/alphaIsTransparency: 1/,'alphaIsTransparency: 0').replace(/textureCompression: 1/,'textureCompression: 0'));
  qa.push({id:r.id,source:r.path,asset:out,...await metrics(out),sha256:crypto.createHash('sha256').update(fs.readFileSync(out)).digest('hex')});
  const thumb=await sharp(out).resize(410,410).png().toBuffer();
  comps.push({input:thumb,left:i%cols*cw+8,top:Math.floor(i/cols)*ch+36});
  const label=Buffer.from('<svg width="426" height="32"><text x="213" y="24" font-family="Arial" font-size="22" text-anchor="middle" fill="white">'+r.id+'</text></svg>');
  comps.push({input:label,left:i%cols*cw,top:Math.floor(i/cols)*ch});
 }
 await sharp({create:{width:cols*cw,height:2*ch,channels:4,background:'#14252d'}}).composite(comps).png().toFile(path.join(dir,'sea-contact.png'));
 const old=await metrics(path.join(root,'UnityClient/Assets/_Project/Resources/NavalTactics/tactical_sea_v1.png'));
 fs.writeFileSync(path.join(dir,'verification.json'),JSON.stringify({builtInImageGeneration:true,preparation:'Original generated pixels copied byte-for-byte. Unity MipMap on, Bilinear, Clamp, no texture compression.',previous:old,seas:qa},null,2));
 console.log(JSON.stringify({count:qa.length,contact:path.join(dir,'sea-contact.png'),old,...Object.fromEntries(qa.map(r=>[r.id,r.central70Percent]))}));
})().catch(e=>{console.error(e);process.exit(1)});

