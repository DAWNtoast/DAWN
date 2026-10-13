// Copy only verified Unity Text strings; retain every existing object, layout and artwork reference.
const fs=require('node:fs'),path=require('node:path');
const root=path.resolve(__dirname,'../..'),output=process.env.CARD_PREVIEW_AUDIT_OUTPUT?path.resolve(process.env.CARD_PREVIEW_AUDIT_OUTPUT):path.join(root,'Artifacts/CardDefinitionValues20261012');
if(!output.toLowerCase().startsWith(path.join(root,'Artifacts').toLowerCase()+path.sep))throw new Error('Preview audit output must be inside workspace Artifacts.');
const records=JSON.parse(fs.readFileSync(path.join(output,'preview-copy-plan.json'),'utf8').replace(/^\uFEFF/,''));
const allowedPath=process.env.CARD_PREVIEW_TEXT_PATH_PATTERN?new RegExp(process.env.CARD_PREVIEW_TEXT_PATH_PATTERN):null;
function scalar(value){value=value.replace(/\r?\n\s+/g,' ');return value.startsWith('"')?JSON.parse(value.replace(/\\x([0-9a-f]{2})/gi,'\\u00$1')):value;}
function texts(source){
  const docs=[...source.matchAll(/^--- !u!(\d+) &(-?\d+)\r?\n([\s\S]*?)(?=^--- !u!|$(?![\s\S]))/gm)],names=new Map(),transforms=new Map(),byObject=new Map(),result=new Map();
  for(const d of docs){const [,type,id,body]=d;
    if(type==='1'){const name=body.match(/^  m_Name: ([\s\S]*?)(?=\r?\n  \w|$(?![\s\S]))/m);if(name)names.set(id,scalar(name[1].replace(/\r$/,'')));}
    if(type==='4'||type==='224'){const go=body.match(/m_GameObject: \{fileID: (-?\d+)\}/),parent=body.match(/m_Father: \{fileID: (-?\d+)\}/);if(go&&parent){transforms.set(id,{go:go[1],parent:parent[1]});byObject.set(go[1],id);}}
  }
  function nameFor(go){const t=transforms.get(byObject.get(go));if(!t)return names.get(go);const parent=transforms.get(t.parent);return (parent?nameFor(parent.go)+'/':'')+names.get(go);}
  for(const d of docs){if(d[1]!=='114')continue;const body=d[3],text=body.match(/^  m_Text: ([\s\S]*?)(?=\r?\n  \w|$(?![\s\S]))/m),go=body.match(/m_GameObject: \{fileID: (-?\d+)\}/);if(!text||!go)continue;
    const key=nameFor(go[1]);if(result.has(key))throw Error('Ambiguous text path '+key);
    result.set(key,{raw:text[0],offset:d.index+d[0].indexOf(body)+text.index,value:text[1]});
  }
  if(!result.size)throw Error('No serialized Text components');return result;
}
const changes=[];
for(const record of records){
  const source=fs.readFileSync(record.Source,'utf8'),verified=fs.readFileSync(record.Verified,'utf8'),before=texts(source),after=texts(verified),patches=[];
  for(const [key,old] of before){const next=after.get(key);if(next&&old.value!==next.value&&(!allowedPath||allowedPath.test(key)))patches.push({key,old,next});}
  if(!patches.length)continue;
  changes.push({scene:record.Scene,strings:patches.map(p=>({path:p.key,before:p.old.value,after:p.next.value}))});
  if(process.argv.includes('--apply')){
    const backup=path.join(output,'PreviewTextBefore',record.Scene);fs.mkdirSync(path.dirname(backup),{recursive:true});if(!fs.existsSync(backup))fs.writeFileSync(backup,source);
    let updated=source;for(const p of patches.sort((a,b)=>b.old.offset-a.old.offset))updated=updated.slice(0,p.old.offset)+p.next.raw.replace(/\r?\n/g,source.includes('\r\n')?'\r\n':'\n')+updated.slice(p.old.offset+p.old.raw.length);
    const check=texts(updated);for(const p of patches)if(check.get(p.key).value.replace(/\r/g,'')!==p.next.value.replace(/\r/g,''))throw Error('Text replacement failed '+p.key);
    fs.writeFileSync(record.Source,updated);
  }
}
fs.writeFileSync(path.join(output,process.argv.includes('--apply')?'preview-text-changes.json':'preview-text-plan.json'),JSON.stringify(changes,null,2));
console.log(JSON.stringify({scenes:changes.length,strings:changes.reduce((n,x)=>n+x.strings.length,0),applied:process.argv.includes('--apply')}));
