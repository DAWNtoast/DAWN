const fs=require('fs');
const root='D:/Game',out=root+'/Artifacts/ArtPolishBatch1_20261012';
const catalogPath='UnityClient/Assets/_Project/Resources/Data/component_catalog_v2.csv';
const runtimePath='UnityClient/Assets/_Project/Presentation/Runtime/ComponentArtCatalog.cs';
const lines=fs.readFileSync(root+'/'+catalogPath,'utf8').replace(/^\uFEFF/,'').trim().split(/\r?\n/);
const keys=lines.shift().split(',');
const original=lines.filter(l=>l.trim()&&!l.startsWith('#')).map(line=>{
 const cells=line.split(',');if(cells.length!==22)throw new Error('Unexpected catalog row');
 const e=Object.fromEntries(keys.map((k,i)=>[k,cells[i].trim()]));
 return {id:e.component_id,name:e.name,slot:e.slot,rarity:e.rarity,artSourceId:e.art_source_id,source:catalogPath};
});
const code=fs.readFileSync(root+'/'+runtimePath,'utf8'),extra=[];
for(const match of code.matchAll(/result\.Add\(Extra\(([^\r\n]+)\)\);/g)){
 const args=match[1],strings=[...args.matchAll(/"((?:[^"\\]|\\.)*)"/g)].map(m=>m[1]);
 const slot=args.match(/ComponentSlot\.(\w+)/)?.[1].toLowerCase();
 const rarity=args.match(/ComponentRarity\.(\w+)/)?.[1].toLowerCase();
 if(strings.length!==7||!slot||!rarity)throw new Error('Unrecognized extra definition');
 extra.push({id:strings[0],name:strings[1],slot,rarity,artSourceId:strings[4],source:runtimePath});
}
const entries=[...original,...extra],masters=JSON.parse(fs.readFileSync(out+'/equipment/manifest.json','utf8')).entries.map(e=>e.id);
if(original.length!==125||extra.length!==25||new Set(entries.map(e=>e.id)).size!==150||new Set(entries.map(e=>e.artSourceId)).size!==25)throw new Error('Equipment count changed; inspect definitions');
for(const e of entries){
 if(!masters.includes(e.artSourceId))throw new Error('Unredrawn source: '+e.id);
 e.masterName=original.find(x=>x.id===e.artSourceId)?.name;
 e.usesOwnNamedMaster=e.id===e.artSourceId;
 e.masterStatus='共用主图已重绘';
}
const labels={bow:'船首',sail:'船帆',cannon:'火炮',plating:'船壳／护板',instrument:'航海仪器'};
const groups=Object.entries(labels).map(([slot,name])=>({slot,name,equipmentItems:entries.filter(e=>e.slot===slot).length,sharedMasterImages:new Set(entries.filter(e=>e.slot===slot).map(e=>e.artSourceId)).size}));
const summary={equipmentItems:entries.length,baseItems:original.length,additionalItems:extra.length,sharedMasterImages:masters.length,redrawnMasterImages:masters.length,additionalMasterImagesNeededForOnePerItem:entries.filter(e=>!e.usesOwnNamedMaster).length,independentAppearanceRedrawComplete:false};
fs.writeFileSync(out+'/equipment-coverage.json',JSON.stringify({checkedAtUtc:new Date().toISOString(),summary,groups,sources:[catalogPath,runtimePath],entries},null,2));
const esc=s=>String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('"','&quot;');
const cards=entries.map(e=>`<tr data-search="${esc(e.id+' '+e.name+' '+labels[e.slot]+' '+e.masterName)}"><td>${esc(labels[e.slot])}</td><td>${esc(e.name)}<small>${esc(e.id)}</small></td><td>${esc(e.masterName)}<small>${esc(e.artSourceId)}</small></td><td>${e.usesOwnNamedMaster?'原型对应装备':'复用该原型主图'}</td><td><a href="equipment/${esc(e.artSourceId)}.png">查看已重绘的共用图</a></td></tr>`).join('');
const html=`<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>150件装备与25套共用主图对应表</title><style>body{background:#10242d;color:#efe8d7;font:16px/1.6 'Microsoft Yahei',sans-serif;max-width:1420px;margin:auto;padding:32px}a{color:#ebc57e}table{width:100%;border-collapse:collapse}td,th{text-align:left;border-bottom:1px solid #45606a;padding:12px}th{background:#203b46}small{display:block;color:#a4bcbd}input{width:96%;padding:12px;background:#203b46;color:#fff;border:1px solid #617f86;margin:24px 0}aside{padding:18px;background:#29434b;border-left:4px solid #ebc57e}tr[hidden]{display:none}</style><h1>装备数量与重绘范围核对</h1><aside><b>当前共150件装备，使用25套共用主图。</b><p>本批完成的是25套共用主图的精细度重绘，并非150件装备各自的独立外观。原目录125件，代码追加25件；每类共30件、5套主图。若后续按一件一图扩展，还需为125件另做主图。</p><p>本次仅核对并更正统计口径，未新增重绘、未改变装备配置、未出包。</p></aside><p><a href="index.html">返回本批对照与验收</a> · <a href="equipment-coverage.json">完整数据与来源</a></p><table><thead><tr><th>类别</th><th>装备数量</th><th>已重绘共用主图</th></tr></thead><tbody>${groups.map(g=>`<tr><td>${g.name}</td><td>${g.equipmentItems}</td><td>${g.sharedMasterImages}</td></tr>`).join('')}</tbody></table><input aria-label="搜索装备与美术原型" placeholder="搜索装备名、ID、类别或共用主图"><p id="count">显示150件装备</p><table><thead><tr><th>类别</th><th>装备</th><th>当前美术原型</th><th>对应关系</th><th>图片</th></tr></thead><tbody id="items">${cards}</tbody></table><script>const input=document.querySelector('input'),rows=[...document.querySelectorAll('#items tr')];input.oninput=()=>{let n=0;for(const row of rows){row.hidden=!row.dataset.search.toLowerCase().includes(input.value.toLowerCase());if(!row.hidden)n++}document.querySelector('#count').textContent='显示'+n+'件装备'};</script></html>`;
fs.writeFileSync(out+'/equipment-coverage.html',html);
console.log(JSON.stringify(summary));
