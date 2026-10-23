const fs=require('fs'),path=require('path'),crypto=require('crypto');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const base='D:/Game/UnityClient/Assets/_Project',src=base+'/Resources/PortArt/CrewDossiers',dest=base+'/Resources/PortArt/CrewAvatars',out=__dirname,install=process.argv.includes('--install');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex'),guid=s=>crypto.createHash('md5').update('departure-avatar-20261022:'+s).digest('hex');
const cat=fs.readFileSync(base+'/Content/Runtime/CrewNarrativeCatalog.cs','utf8');
const people=[...cat.matchAll(/Crew\("(crew_[^"]+)",\s*"([^"]+)",\s*(\d),\s*"([^"]+)"/g)].map(m=>({id:m[1],port:m[2],profession:+m[3],name:m[4]}));
const tuning=fs.existsSync(out+'/crops.json')?JSON.parse(fs.readFileSync(out+'/crops.json','utf8')):{};
function svg(w,h,body){return Buffer.from('<svg xmlns="http://www.w3.org/2000/svg" width="'+w+'" height="'+h+'"><g font-family="Arial">'+body+'</g></svg>');}
function text(x,y,size,fill,value){return '<text x="'+x+'" y="'+y+'" fill="'+fill+'" font-size="'+size+'">'+value+'</text>';}
async function main(){
 if(people.length!==36||new Set(people.map(p=>p.id)).size!==36)throw Error('Expected 36 identities');
 fs.mkdirSync(out+'/contacts',{recursive:true});
 if(install){fs.mkdirSync(dest,{recursive:true});if(!fs.existsSync(dest+'.meta'))fs.writeFileSync(dest+'.meta','fileFormatVersion: 2\nguid: '+guid('CrewAvatars')+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n');}
 for(const p of people){
  const input=src+'/'+p.id+'_portrait_v2.png',bytes=fs.readFileSync(input),m=await sharp(bytes).metadata(),t=tuning[p.id]||{centerX:.5,top:.025,width:.64};
  const units=Math.max(1,Math.floor(Math.min(m.width*t.width/32,m.height/42))),w=units*32,h=units*42;
  const crop={left:Math.max(0,Math.min(m.width-w,Math.round(m.width*t.centerX-w/2))),top:Math.max(0,Math.min(m.height-h,Math.round(m.height*t.top))),width:w,height:h};
  const avatar=await sharp(bytes).extract(crop).resize(256,336,{kernel:'lanczos3'}).removeAlpha().png().toBuffer();
  p.source=input;p.size=[m.width,m.height];p.crop=crop;p.tuning=t;p.sourceSha256=hash(bytes);p.avatarSha256=hash(avatar);p.output=dest+'/'+p.id+'_avatar_v1.png';p.bytes=avatar;
  if(install){fs.writeFileSync(p.output,avatar);if(!fs.existsSync(p.output+'.meta')){let meta=fs.readFileSync(input+'.meta','utf8').replace(/^guid:.*$/m,'guid: '+guid(p.id)).replace(/maxTextureSize: 2048/g,'maxTextureSize: 512').replace(/^  userData:.*$/m,'  userData: Local aspect-preserving crop of '+p.id+'_portrait_v2; reviewed 2026-10-22; no image generation.');fs.writeFileSync(p.output+'.meta',meta);}}
 }
 for(let start=0;start<36;start+=6){
  const batch=people.slice(start,start+6),layers=[],labels=[];
  for(let i=0;i<6;i++){const p=batch[i],x=i%3*512,y=Math.floor(i/3)*430+40,c=p.crop;
   const frame=svg(p.size[0],p.size[1],'<rect x="'+c.left+'" y="'+c.top+'" width="'+c.width+'" height="'+c.height+'" fill="none" stroke="#66ffbb" stroke-width="8"/>');
   const thumb=await sharp(await sharp(p.source).composite([{input:frame}]).png().toBuffer()).resize({width:232,height:340,fit:'inside'}).png().toBuffer();
   layers.push({input:thumb,left:x+8,top:y+34},{input:p.bytes,left:x+248,top:y+34});
   labels.push(text(x+8,y+20,16,'white',p.profession+': '+p.id.replace(/^crew_[a-z]+_[a-z]+_/,''))+text(x+8,y+396,13,'#aebfc8','crop '+[c.left,c.top,c.width,c.height].join(',')));
  }
  const slug=batch[0].id.replace(/^crew_/,'').replace(/_navigator_.*/,'');
  layers.push({input:svg(1536,900,text(16,28,24,'#e5c88a',slug+': source / departure avatar 256x336')+labels.join('')),left:0,top:0});
  await sharp({create:{width:1536,height:900,channels:3,background:'#21323c'}}).composite(layers).jpeg({quality:96}).toFile(out+'/contacts/'+slug+'.jpg');
 }
 const layers=[],labels=[];
 for(let i=0;i<36;i++){const p=people[i],x=i%6*240,y=Math.floor(i/6)*266;layers.push({input:await sharp(p.bytes).resize(160,210).png().toBuffer(),left:x+40,top:y+8});labels.push(text(x+8,y+238,12,'white',p.id.replace('crew_','').replace(/_(navigator|carpenter|doctor|gunner|cook|diver)_/,' / ')));}
 layers.push({input:svg(1440,1596,labels.join('')),left:0,top:0});
 await sharp({create:{width:1440,height:1596,channels:3,background:'#21323c'}}).composite(layers).jpeg({quality:96}).toFile(out+'/all-36-avatars.jpg');
 fs.writeFileSync(out+'/manifest.json',JSON.stringify({date:'2026-10-22',operation:'Local crop/resample only; source dossiers untouched',installed:install,outputSize:[256,336],crew:people.map(({bytes,...p})=>p)},null,2));
 console.log(JSON.stringify({crew:36,installed:install,contacts:7,totalAvatarBytes:people.reduce((s,p)=>s+p.bytes.length,0)}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
