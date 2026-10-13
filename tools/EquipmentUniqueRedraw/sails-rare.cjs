const fs = require('fs');
const sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root='D:/Game/Artifacts/EquipmentUniqueRedraw20261012';
const manifestPath=root+'/generation-sails-rare.json';
async function main(){
 const [action,id,source]=process.argv.slice(2);
 const spec=JSON.parse(fs.readFileSync(root+'/prompts-sails-rare.json','utf8')).entries.find(e=>e.id===id);
 if(!spec)throw Error('Unknown sail '+id);
 const lock=manifestPath+'.lock';let handle;
 for(let attempt=0;attempt<600;attempt++){try{handle=fs.openSync(lock,'wx');break;}catch(e){if(e.code!=='EEXIST')throw e;await new Promise(resolve=>setTimeout(resolve,50));}}
 if(handle===undefined)throw Error('Manifest lock timeout');
 try {
 const m=fs.existsSync(manifestPath)?JSON.parse(fs.readFileSync(manifestPath,'utf8')):{schemaVersion:1,status:'in_progress',assignedCount:6,generatorMode:'built-in image_gen',entries:[]};
 if(action==='register'){
  const target=spec.final_path;fs.mkdirSync(root+'/qa-sails-rare',{recursive:true});
  if(fs.existsSync(target))fs.copyFileSync(target,root+'/qa-sails-rare/'+id+'-previous.png');
  fs.copyFileSync(source,target);
  const {data,info}=await sharp(target).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  let x0=info.width,y0=info.height,x1=-1,y1=-1,amin=255,amax=0,border=0;
  for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++){const a=data[(y*info.width+x)*4+3];amin=Math.min(amin,a);amax=Math.max(amax,a);if(a>=8){x0=Math.min(x0,x);y0=Math.min(y0,y);x1=Math.max(x1,x);y1=Math.max(y1,y);}if(x===0||y===0||x===info.width-1||y===info.height-1)border=Math.max(border,a);}
  if(amin!==0||amax<250||border>=16)throw Error('Alpha/clipping check failed '+id);
  await sharp(target).flatten({background:'#29343a'}).png().toFile(root+'/qa-sails-rare/'+id+'-neutral.png');
  m.entries=m.entries.filter(e=>e.id!==id);m.entries.push({...spec,generated_path:source,qaNotes:[],generatedMetadata:{width:info.width,height:info.height,alphaMin:amin,alphaMax:amax,alpha8Bounds:[x0,y0,x1+1,y1+1],qaStatus:'pending_visual'},savedAt:new Date().toISOString()});
 }else if(action==='approve'){
  const e=m.entries.find(e=>e.id===id);e.generatedMetadata.qaStatus='approved';e.qaNotes=process.argv.slice(4);
 }else throw Error('Unknown action');
 if(m.entries.length===6&&m.entries.every(e=>e.generatedMetadata.qaStatus==='approved'))m.status='complete';
 fs.writeFileSync(manifestPath,JSON.stringify(m,null,2));console.log(JSON.stringify({id,action,count:m.entries.length}));
 } finally {fs.closeSync(handle);fs.unlinkSync(lock);}
}
main().catch(e=>{console.error(e);process.exit(1)});
