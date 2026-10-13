const fs=require('fs'),path=require('path');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root='D:/Game',out=root+'/Artifacts/ArtPolishBatch1_20261012',review=out+'/review';
fs.mkdirSync(review,{recursive:true});
const audit=JSON.parse(fs.readFileSync(root+'/Artifacts/ArtQualityAudit20261012/optimization-list.json','utf8'));
const geometry=fs.readFileSync(root+'/UnityClient/Assets/_Project/Presentation/Runtime/ComponentShopDisplayGeometry.cs','utf8');
const bounds=Object.fromEntries([...geometry.matchAll(/case "([^"]+)": return Rect.MinMaxRect\((\d+)f, (\d+)f, (\d+)f, (\d+)f\);/g)].map(m=>[m[1],m.slice(2).map(Number)]));
const routes=[['shattered_sail','破帆海域','LegacyDemo/shattered_sail_event_sheet_v2'],['golden_tide','金潮海域','LegacyDemo/golden_tide_event_sheet_v2'],['moon_tide','月潮海域','LegacyDemo/moon_tide_event_sheet_v2'],['treasure_hunt','寻宝事件','TreasureHunt/treasure_hunt_event_sheet']];
const bg='#233b44',records=[];
const esc=s=>String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const rel=p=>path.relative(out,p).replaceAll('\\','/');
const manifests=[];
for(const file of [out+'/equipment/manifest.json',out+'/icons/manifest.json',out+'/events/manifest.json',...routes.map(r=>out+'/events/'+r[0]+'/manifest.json'),out+'/events/golden_tide/root-manifest.json',out+'/events/root-corrections.json']){
 if(fs.existsSync(file)){const parsed=JSON.parse(fs.readFileSync(file,'utf8'));manifests.push(...(Array.isArray(parsed)?parsed:parsed.entries??[]));}
}
async function bbox(file){
 const {data,info}=await sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true});let l=info.width,t=info.height,r=-1,b=-1;
 for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++)if(data[(y*info.width+x)*4+3]>=8){l=Math.min(l,x);t=Math.min(t,y);r=Math.max(r,x);b=Math.max(b,y);}
 return {left:l,top:t,width:r-l+1,height:b-t+1};
}
async function shopPreview(source,id,target,isOriginal){
 const [l,t,r,b]=bounds[id];let image;
 if(isOriginal){const box={left:Math.max(0,l-12),top:Math.max(0,t-12),width:Math.min(512,r+12)-Math.max(0,l-12),height:Math.min(512,b+12)-Math.max(0,t-12)};image=sharp(source).extract(box);}
 else if(source.includes('production-ready')){
  const box={left:Math.max(0,l-12)*2,top:Math.max(0,t-12)*2,width:(Math.min(512,r+12)-Math.max(0,l-12))*2,height:(Math.min(512,b+12)-Math.max(0,t-12))*2};image=sharp(source).extract(box);
 }else image=sharp(source).extract(await bbox(source));
 await image.resize(480,400,{fit:'contain',background:bg}).flatten({background:bg}).jpeg({quality:94}).toFile(target);
}
async function contacts(name,items,field,columns,tileW,tileH){
 if(!items.length)return;
 const parts=[];
 for(let i=0;i<items.length;i++){
  const tile=await sharp(out+'/'+items[i][field]).resize(tileW-12,tileH-36,{fit:'contain',background:bg}).flatten({background:bg}).png().toBuffer();
  parts.push({input:tile,left:i%columns*tileW+6,top:Math.floor(i/columns)*tileH+30});
  const label=Buffer.from(`<svg width="${tileW}" height="28"><text x="8" y="20" fill="#eee3c9" font-family="Segoe UI" font-size="13">${esc(items[i].label||items[i].id)}</text></svg>`);
  parts.push({input:label,left:i%columns*tileW,top:Math.floor(i/columns)*tileH});
 }
 await sharp({create:{width:columns*tileW,height:Math.ceil(items.length/columns)*tileH,channels:3,background:bg}}).composite(parts).png().toFile(review+'/'+name+'.png');
}
async function main(){
 for(const id of Object.keys(bounds)){
  const native=out+'/equipment/'+id+'.png';if(!fs.existsSync(native))continue;
  const relative='UnityClient/Assets/_Project/Resources/ModularShip/Layers/'+id+'.png';
  const staged=out+'/production-ready/'+relative,source=fs.existsSync(staged)?staged:native;
  const before=review+'/'+id+'-before.jpg',after=review+'/'+id+'-after.jpg';
  await shopPreview(out+'/originals/'+relative,id,before,true);await shopPreview(source,id,after,false);
  const row=audit.rows.find(r=>r.files?.includes(relative)&&r.priority==='P1');
  records.push({kind:'equipment',id,label:id,category:row?.category??'装备',before:rel(before),after:rel(after),full:rel(native),theme:row?.optimization_direction?.join(' ')??''});
 }
 for(const [route,label,resource]of routes){
  const original=out+'/originals/UnityClient/Assets/_Project/Resources/'+resource+'.png';const meta=await sharp(original).metadata();
  for(let index=0;index<20;index++){
   const id=route+'-'+String(index).padStart(2,'0'),native=out+'/events/'+route+'/cell-'+String(index).padStart(2,'0')+'.png';if(!fs.existsSync(native))continue;
   const left=Math.round(index%5*meta.width/5),right=Math.round((index%5+1)*meta.width/5),top=Math.round(Math.floor(index/5)*meta.height/4),bottom=Math.round((Math.floor(index/5)+1)*meta.height/4);
   const before=review+'/'+id+'-before.jpg',after=review+'/'+id+'-after.jpg',crop=review+'/'+id+'-detail.jpg';
   await sharp(original).extract({left,top,width:right-left,height:bottom-top}).resize(480,400,{fit:'fill'}).jpeg({quality:94}).toFile(before);
   const cell=await sharp(native).resize(768,640,{fit:'cover',position:'centre'}).flatten({background:bg}).png().toBuffer();
   await sharp(cell).resize(480,400).jpeg({quality:94}).toFile(after);
   // Runtime NavigationDashboardLayout settles on 4:3 after initial construction.
   // New borderless cells need only the final aspect crop, no legacy gold-frame inset.
   const cropW=752,cropH=564;
   await sharp(cell).extract({left:Math.round((768-cropW)/2),top:Math.round((640-cropH)/2),width:cropW,height:cropH}).resize(320,240).jpeg({quality:94}).toFile(crop);
   const entry=manifests.findLast(m=>m.route===route&&m.index===index);
   records.push({kind:'event',route,id,label:route+' '+String(index).padStart(2,'0'),category:label,index,theme:entry?.theme??'',before:rel(before),after:rel(after),crop:rel(crop),full:rel(native)});
  }
 }
 for(const id of ['instrument_brass_compass','instrument_guild_engine']){
  const native=out+'/icons/'+id+'.png';if(!fs.existsSync(native))continue;
  const before=review+'/icon-'+id+'-before.jpg',after=review+'/icon-'+id+'-after.jpg';
  await sharp(out+'/originals/UnityClient/Assets/_Project/Resources/ComponentArt/'+id+'.png').resize(256,256).flatten({background:bg}).jpeg({quality:94}).toFile(before);
  await sharp(native).resize(256,256).flatten({background:bg}).jpeg({quality:94}).toFile(after);
  records.push({kind:'icon',id:'icon-'+id,label:id,category:'图标边缘修复',before:rel(before),after:rel(after),full:rel(native),theme:'保留现有身份与边框，清除邻格残条。'});
 }
 await contacts('equipment-after',records.filter(r=>r.kind==='equipment'),'after',5,320,286);
 await contacts('equipment-before',records.filter(r=>r.kind==='equipment'),'before',5,320,286);
 for(const [route]of routes){const rows=records.filter(r=>r.route===route);await contacts(route+'-after',rows,'after',5,320,294);await contacts(route+'-detail-crops',rows,'crop',5,320,274);}
 const integration=fs.existsSync(out+'/integration-report.json')?JSON.parse(fs.readFileSync(out+'/integration-report.json','utf8')):null;
 const counts={equipment:records.filter(r=>r.kind==='equipment').length,events:records.filter(r=>r.kind==='event').length,icons:records.filter(r=>r.kind==='icon').length};
 const equipmentScope=JSON.parse(fs.readFileSync(out+'/equipment-coverage.json','utf8')).summary;
 fs.writeFileSync(out+'/review-index.json',JSON.stringify({createdAt:new Date().toISOString(),counts,equipmentScope,applied:integration?.applied===true,records},null,2));
 const testLink=fs.existsSync(out+'/validation/ui-review-final/index.html')?'<a href="validation/ui-review-final/index.html">Unity 实际界面 · 50 张</a>':'';
 const cards=records.map(r=>`<article data-kind="${r.kind}" data-search="${esc(r.id+' '+r.category+' '+r.theme)}"><h3>${esc(r.category)} <small>${esc(r.id)}</small></h3><p>${esc(r.theme)}</p><div class="pair"><figure><img loading="lazy" src="${r.before}"><figcaption>优化前</figcaption></figure><figure><a href="${r.full}" target="_blank"><img loading="lazy" src="${r.after}"></a><figcaption>优化后 · 点击原图</figcaption></figure>${r.crop?`<figure class="crop"><img loading="lazy" src="${r.crop}"><figcaption>详情页主体裁切</figcaption></figure>`:''}</div></article>`).join('');
 const html=`<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>第一批美术精细度优化</title><style>*{box-sizing:border-box}body{margin:0;background:#10242d;color:#efe8d7;font:16px/1.6 "Microsoft Yahei",sans-serif}header,main{max-width:1520px;margin:auto;padding:24px 32px}header{padding-top:44px}h1{margin:0;font-size:32px}h2{margin:6px 0}p{color:#b8c8c6}a{color:#eccb83}.stats{display:flex;gap:20px}.stat{padding:12px 24px;border:1px solid #47605f;border-radius:8px;background:#1c343d}.stat b{font-size:28px;color:#ebc57e}.tools{position:sticky;top:0;padding:12px 32px;background:#142a33ee;backdrop-filter:blur(10px);display:flex;gap:12px;z-index:2}button,input{padding:10px 16px;border:1px solid #65817e;border-radius:6px;background:#233b44;color:#efe8d7;font:inherit}button{cursor:pointer}button.active{background:#ebc57e;color:#10242d}input{flex:1;min-width:120px}article{padding:22px;margin-bottom:24px;border:1px solid #405a5d;border-radius:10px;background:#18303a}h3{margin:0}small{display:block;font-size:13px;font-weight:400;color:#b2c9c8}article p{margin:8px 0 16px;font-size:14px}.pair{display:flex;gap:16px;align-items:flex-start}figure{flex:1;margin:0;min-width:0}figure img{width:100%;display:block;border-radius:4px;background:#233b44}figure.crop{flex:.5}figure.crop img{width:auto;max-width:100%;max-height:400px;margin:auto}figcaption{font-size:13px;margin-top:8px;color:#c8bc9b;text-align:center}.links{display:flex;gap:20px;flex-wrap:wrap}.empty{display:none}[hidden]{display:none!important}@media(max-width:800px){header,main{padding:20px}.tools{flex-wrap:wrap;padding:12px 20px}.stats{gap:8px}.stat{padding:8px 12px}.pair{gap:8px}figure.crop{display:none}}</style><header><p>SHATTERED SAIL · ART REVIEW 01</p><h1>第一批美术精细度优化</h1><p>以初始船的细绘程度统一材质与轮廓，保留不同部件和事件的独立身份。</p><div class="stats"><div class="stat"><b>${counts.equipment} / 25</b><br>装备主图</div><div class="stat"><b>${counts.events} / 80</b><br>独立事件插画</div><div class="stat"><b>${counts.icons} / 2</b><br>图标边缘修复</div></div><p>${integration?.applied?'已接入生产资源；保留原 GUID 与事件格位。':'原图与新图审核中，生产资源尚未整体替换。'} 本轮未出包。</p><div class="links">${testLink}<a href="integration-report.json">接入记录</a><a href="prompts.json">生成提示词与来源</a><a href="../ArtQualityAudit20261012/optimization-list.html">完整后续优化清单</a></div></header><nav class="tools"><button class="active" data-filter="all">全部</button><button data-filter="equipment">装备</button><button data-filter="event">事件</button><button data-filter="icon">图标修复</button><input placeholder="搜索名称 / 海域 / 场景" aria-label="筛选资源"></nav><main>${cards}<p id="no-results" hidden>没有匹配资源</p></main><script>let filter='all';const input=document.querySelector('input'),items=[...document.querySelectorAll('article')];function apply(){let n=0;for(const item of items){item.hidden=!(filter==='all'||item.dataset.kind===filter)||!item.dataset.search.toLowerCase().includes(input.value.toLowerCase());if(!item.hidden)n++}document.querySelector('#no-results').hidden=n>0}for(const button of document.querySelectorAll('button'))button.onclick=()=>{filter=button.dataset.filter;for(const b of document.querySelectorAll('button'))b.classList.toggle('active',b===button);apply()};input.oninput=apply;</script></html>`;
 const completion=fs.existsSync(out+'/completion.json')?JSON.parse(fs.readFileSync(out+'/completion.json','utf8')):null;
 const acceptance=completion?`<p>验收通过：${completion.tests.editmode.passed} 项编辑模式检查、${completion.tests.playmode.passed} 项运行模式检查与 50 张实际界面。<a href="completion.json">完整验收记录</a></p>`:'';
 const coverageNote='<p><b>数量说明：当前共150件装备，使用25套共用主图。本批重绘这25套源图，并未制作150套独立装备外观。</b> <a href="equipment-coverage.html">查看150件装备逐项对应表</a></p>';
 fs.writeFileSync(out+'/index.html',html.replace('<div class="links">',coverageNote+acceptance+'<div class="links">').replace('<br>装备主图','<br>装备共用主图'));console.log(JSON.stringify({counts,equipmentScope,review:out+'/index.html'}));
}
main().catch(e=>{console.error(e);process.exitCode=1});
