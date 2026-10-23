const fs=require('fs'),path=require('path'),crypto=require('crypto');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const dir='D:/Game/UnityClient/Assets/_Project/Resources/NavalTactics/Ships';
(async()=>{
 const files=fs.readdirSync(dir).filter(n=>n.endsWith('.png')).sort(),rows=[],guids=[];
 for(const name of files){
  const file=path.join(dir,name),meta=fs.readFileSync(file+'.meta','utf8'),guid=meta.match(/guid: ([a-f0-9]+)/)?.[1];guids.push(guid);
  const {data,info}=await sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true});let zero=0,edge=0,l=info.width,t=info.height,r=-1,b=-1;
  for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++){
   const a=data[(y*info.width+x)*4+3];if(a===0)zero++;
   if((x===0||y===0||x===info.width-1||y===info.height-1)&&a>0)edge++;
   if(a>2){l=Math.min(l,x);t=Math.min(t,y);r=Math.max(r,x);b=Math.max(b,y);}
  }
  rows.push({name,size:[info.width,info.height],zeroAlphaFraction:zero/(info.width*info.height),edgeNonzero:edge,margins:[l,t,info.width-1-r,info.height-1-b],guid,spriteMode1:/spriteMode: 1/.test(meta),alphaIsTransparency:/alphaIsTransparency: 1/.test(meta),sha256:crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex')});
 }
 const failures=rows.filter(r=>r.size[0]!==1024||r.size[1]!==1536||r.edgeNonzero!==0||Math.min(...r.margins)<60||r.zeroAlphaFraction<.45||!r.spriteMode1||!r.alphaIsTransparency);
 const result={count:rows.length,uniqueGuids:new Set(guids).size,failures:failures.map(r=>r.name),ships:rows};
 fs.writeFileSync(path.join(__dirname,'independent-qa.json'),JSON.stringify(result,null,2));
 console.log(JSON.stringify({count:result.count,uniqueGuids:result.uniqueGuids,failures:result.failures,minMargin:Math.min(...rows.flatMap(r=>r.margins)),alphaMin:Math.min(...rows.map(r=>r.zeroAlphaFraction)),alphaMax:Math.max(...rows.map(r=>r.zeroAlphaFraction))}));
})().catch(e=>{console.error(e);process.exit(1)});
