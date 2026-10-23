const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const base='D:/Game/Artifacts/CardArtReview20261022';
const resources='D:/Game/UnityClient/Assets/_Project/Resources';
async function main(){
 const jobs=fs.readdirSync(base).filter(f=>f.endsWith('-generated.json')).flatMap(f=>JSON.parse(fs.readFileSync(base+'/'+f,'utf8')));
 const seen=new Set(),result=[];
 for(const job of jobs){
  if(!/^card_[a-z0-9_]+$/.test(job.id)||seen.has(job.id))throw new Error('Invalid or duplicate id: '+job.id);
  seen.add(job.id);
  const dest=resources+'/CardArt/Western/'+job.id+'.png';
  const source=path.resolve(job.source);
  if(!fs.existsSync(source))throw new Error('Missing source: '+source);
  const metadata=await sharp(source).metadata();
  if(metadata.format!=='png'||metadata.width<1024||metadata.height<1024)throw new Error('Invalid generated image: '+job.id);
  const digest=crypto.createHash('sha256').update(fs.readFileSync(source)).digest('hex');
  if(fs.existsSync(dest)){
   const old=fs.readFileSync(dest),oldHash=crypto.createHash('sha256').update(old).digest('hex');
   if(oldHash!==digest){
    fs.mkdirSync(base+'/backup',{recursive:true});
    const backup=base+'/backup/'+job.id+'.png';
    if(!fs.existsSync(backup))fs.copyFileSync(dest,backup);
   }
  }
  fs.copyFileSync(source,dest);
  if(!fs.existsSync(dest+'.meta')){
   const template=fs.readFileSync(resources+'/CardArt/Western/card_crew_rally.png.meta','utf8');
   fs.writeFileSync(dest+'.meta',template.replace(/^guid: .+$/m,'guid: '+crypto.randomBytes(16).toString('hex')));
  }
  result.push({...job,destination:dest,width:metadata.width,height:metadata.height,sha256:digest,tool:'built-in image_gen',review:'generated output visually inspected'});
 }
 fs.writeFileSync(base+'/installed.json',JSON.stringify(result,null,2));
 console.log(JSON.stringify({installed:result.length,ids:result.map(r=>r.id)}));
 await require('./report.cjs')();
}
main().catch(e=>{console.error(e);process.exitCode=1;});
