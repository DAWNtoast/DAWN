const fs = require('fs');
const path = require('path');
const sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root = 'D:/Game/UnityClient/Assets/_Project';
const output = process.env.ART_QUALITY_AUDIT_OUTPUT || 'D:/Game/Artifacts/ArtQualityAudit20261012';
const resourceRoot = path.join(root, 'Resources');
const slash = value => value.replace(/\\/g, '/');
const walk = dir => fs.readdirSync(dir, {withFileTypes:true}).flatMap(entry => entry.isDirectory() ? walk(path.join(dir, entry.name)) : [path.join(dir, entry.name)]);
const xml = value => String(value).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&apos;'}[c]));
const runtimeFiles = walk(path.join(root, 'Presentation/Runtime')).concat(walk(path.join(root, 'Core/Runtime'))).filter(p => p.endsWith('.cs'));
const code = runtimeFiles.map(file => ({file: slash(path.relative('D:/Game/UnityClient',file)), text: fs.readFileSync(file,'utf8')}));
const integer = (text, key) => {const match = text.match(new RegExp('(?:^|\\n)\\s*'+key+':\\s*(-?\\d+)')); return match ? Number(match[1]) : null;};

async function main() {
  fs.mkdirSync(path.join(output,'contacts'),{recursive:true});
  const files = walk(resourceRoot).filter(p=>/\.(png|jpe?g|webp)$/i.test(p)).sort();
  const images = [];
  for (let begin=0; begin<files.length; begin+=8) {
    const chunk = await Promise.all(files.slice(begin,begin+8).map(async file => {
      const m = await sharp(file).metadata();
      const meta = fs.existsSync(file+'.meta') ? fs.readFileSync(file+'.meta','utf8') : '';
      const resource = slash(path.relative(resourceRoot,file)).replace(/\.[^.]+$/,'');
      const basename = path.basename(resource);
      return {path:slash(file),relative:slash(path.relative(root,file)),resource,family:resource.split('/')[0],group:slash(path.dirname(resource)),width:m.width,height:m.height,channels:m.channels,hasAlpha:m.hasAlpha,bytes:fs.statSync(file).size,import:{filterMode:integer(meta,'filterMode'),maxTextureSize:integer(meta,'maxTextureSize'),compression:integer(meta,'textureCompression')},staticCodeMentions:code.filter(c=>c.text.includes('"'+resource+'"')||c.text.includes('"'+basename+'"')).map(c=>c.file)};
    }));
    images.push(...chunk);
  }
  const archiveRoot=path.join(root,'Editor/ArchivedArt');
  const archived=fs.existsSync(archiveRoot)?walk(archiveRoot).filter(p=>/\.(png|jpe?g|webp)$/i.test(p)).map(p=>slash(path.relative(root,p))):[];
  const groups = new Map();
  for (const item of images) {if(!groups.has(item.group))groups.set(item.group,[]);groups.get(item.group).push(item);}
  const contacts=[];
  for (const [group,items] of groups) {
    for(let begin=0;begin<items.length;begin+=12) {
      const batch=items.slice(begin,begin+12), rows=Math.ceil(batch.length/4), width=1440,height=rows*300+40;
      const composites=[];
      const labels=[`<text x="16" y="28" font-size="22" fill="#e6c88f">${xml(group)} | ${begin+1}-${begin+batch.length} / ${items.length}</text>`];
      for(let index=0;index<batch.length;index++) {
        const item=batch[index],x=(index%4)*360,y=40+Math.floor(index/4)*300;
        const png=await sharp(item.path).resize({width:340,height:246,fit:'inside',withoutEnlargement:true}).png().toBuffer();
        const size=await sharp(png).metadata();
        composites.push({input:png,left:x+Math.floor((360-size.width)/2),top:y+Math.floor((246-size.height)/2)});
        labels.push(`<text x="${x+10}" y="${y+267}" font-size="12" fill="#fff">${xml(path.basename(item.path).slice(0,52))}</text><text x="${x+10}" y="${y+287}" font-size="12" fill="#aabbc5">${item.width} x ${item.height}; filter ${item.import.filterMode}; max ${item.import.maxTextureSize}</text>`);
      }
      composites.push({input:Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}"><g font-family="Arial">${labels.join('')}</g></svg>`),left:0,top:0});
      const name=group.replace(/[^a-z0-9_-]/gi,'_')+'_'+String(Math.floor(begin/12)+1).padStart(2,'0')+'.jpg';
      await sharp({create:{width,height,channels:3,background:'#152630'}}).composite(composites).jpeg({quality:91}).toFile(path.join(output,'contacts',name));
      contacts.push({group,file:slash(path.join(output,'contacts',name)),images:batch.map(i=>i.resource)});
    }
  }
  const data={date:'2026-10-12',basis:'Current production Resources raster files; original starter and new ship rendering as visual reference. Import metadata is supporting evidence, not a quality score. Static code mentions are incomplete for dynamically assembled resource paths; no mention is not proof of unused status.',images,archived,contacts};
  fs.writeFileSync(path.join(output,'source-inventory.json'),JSON.stringify(data,null,2));
  const html=`<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>美术资源审核图集</title><style>body{font:16px system-ui;background:#10202b;color:#edf0ed;margin:28px}img{max-width:100%;display:block;margin:12px 0 36px}a{color:#e8c58d}h1{font-size:26px}</style><h1>当前生产美术资源审核图集 · 2026-10-12</h1><p>${images.length} 张 Resources 原图；${archived.length} 张 Editor 归档图单独列于清单。缩略图只用于筛查，质量判断结合原图和实际用途。</p>${contacts.map(c=>`<h2>${xml(c.group)}</h2><a href="contacts/${path.basename(c.file)}"><img loading="lazy" src="contacts/${path.basename(c.file)}"></a>`).join('')}</html>`;
  fs.writeFileSync(path.join(output,'source-contact-index.html'),html);
  const counts={};images.forEach(i=>counts[i.family]=(counts[i.family]||0)+1);
  console.log(JSON.stringify({images:images.length,archived:archived.length,sheets:contacts.length,families:counts,output},null,2));
}
main().catch(error=>{console.error(error);process.exitCode=1;});
