const fs = require('fs');
const path = require('path');
const sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root = 'D:/Game';
const output = process.env.ART_QUALITY_AUDIT_OUTPUT || root + '/Artifacts/ArtQualityAudit20261012';
const source = process.env.ART_QUALITY_AUDIT_SOURCE || output;
const read = name => JSON.parse(fs.readFileSync(output + '/' + name, 'utf8'));
const inventory = read('source-inventory.json');
const { isRetired, validateInventory } = require('./cleanup-20261013.cjs');
validateInventory(inventory);
const cards = read('cards-audit.json');
const characters = JSON.parse(fs.readFileSync(source + '/characters-audit.json', 'utf8'));
const items = JSON.parse(fs.readFileSync(source + '/items-ui-audit.json', 'utf8'));
const environments = read('environments-audit.json');
const usageLabels = {
  current_visible:'现役显示',current_visible_small_icons:'现役库存/装备小图标',
  current_catalog_reuse:'当前装备目录复用原型',current_visible_global_ui:'现役通用界面材质',
  current_visible_signs_only:'现役仅显示六港铭牌',current_signs_runtime_masked:'现役铭牌；运行时已去除导出底',
  fallback_plaque_only:'仅通用铭牌为回退，其余挂架/商人未绘制',
  retained_source_atlases_no_current_draw:'旧制作源图集；无当前绘制路径',
  catalog_compatibility_loaded_no_current_display:'目录兼容加载；无当前显示路径',
  catalog_compatibility_no_current_display:'目录兼容；无当前显示路径',
  compatibility_card_fallback:'旧卡面兼容回退；正常优先独立卡面'
};
const arr = value => value == null ? [] : Array.isArray(value) ? value : [value];
const slash = value => String(value).replace(/\\/g, '/');
const relative = value => slash(value).replace(/^D:\/Game\//i, '').replace(/^Assets\/_Project\//, 'UnityClient/Assets/_Project/');
const abs = value => /^[A-Z]:\//i.test(slash(value)) ? slash(value) : root + '/' + relative(value);
const escape = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const href = value => slash(path.relative(output, abs(value))).split('/').map(encodeURIComponent).join('/');
const filesOf = record => arr(record.files || record.paths || record.sourceFiles || record.original_file_relative_path).filter(f => typeof f === 'string').map(relative);
const evidenceOf = record => arr(record.evidence || record.usageEvidence || record.runtimeEvidence || record.code_evidence || record.used?.code_evidence).map(e => ({path:relative(e.path || e.file),line:e.line,claim:e.claim || e.finding || e.description || e.detail}));
const rows = [];
const retiredRows = [];
const push = row => {
  const originalFiles = [...new Set(arr(row.files).map(relative))];
  const files = originalFiles.filter(file => !isRetired(file));
  if (files.length !== originalFiles.length) {
    retiredRows.push({key:row.key,files:originalFiles.filter(isRetired)});
    if (!files.length) return;
    row = {...row,quantity:`${files.length} 个保留源文件（已扣除本次获批清理项）`};
  }
  rows.push({...row,usage:usageLabels[row.usage]||row.usage,files,visual_evidence:arr(row.visual_evidence),optimization_direction:arr(row.optimization_direction),acceptance:arr(row.acceptance),evidence:arr(row.evidence),review:arr(row.review)});
};
const itemToRow = record => ({
  key:'items/' + record.id,category:record.category,priority:record.priority,
  usage:record.scope?.usageStatus || '',scope:record.scope,
  quantity:record.scope?.subjects ? `${record.scope.sourceFiles} 张源图 / ${record.scope.subjects} 个对象` : `${record.scope?.sourceFiles || filesOf(record).filter(f=>f.endsWith('.png')).length} 张源图`,
  files:filesOf(record),visual_evidence:arr(record.visualIssues).length ? record.visualIssues : record.strengths,
  optimization_direction:record.optimizationDirection,
  action:record.priority === 'P1' ? '重画或局部修复' : record.priority === 'P2' ? '统一画风/造型' : record.priority === '保留' ? '保留现图' : '导出或兼容整理',
  evidence:evidenceOf(record),review:record.visualReview
});

const mainParts = items.records.find(r=>r.id === 'component_display_models');
for (const batch of mainParts.executionBatches) push({
  ...itemToRow(mainParts),key:'parts/' + path.basename(batch.paths[0]).split('_')[0],
  category:batch.category.replace(/^P1\s*/, ''),quantity:'5 个独立原型；覆盖 30 条装备定义',
  scope:{sourceFiles:5,subjects:5,coveredDefinitions:30},files:batch.paths,
  visual_evidence:batch.issues,optimization_direction:batch.direction,
  action:'重画主体；保留结构差异',acceptance:batch.acceptance
});
for (const record of items.records.filter(r=>r.id !== mainParts.id)) {
  const row = itemToRow(record);
  if (['modular_source_atlases','legacy_upgrade_sheet','legacy_resource_sheet','legacy_card_icon_sheet'].includes(record.id)) {
    row.originalPriority = row.priority;
    row.priority = '暂缓';
    row.action = '旧源图/兼容入口，不排入当前重画';
  }
  if (record.id === 'component_icon_crop_defects') {
    row.action = '局部重切/清理';
    row.acceptance = '48/64 像素显示中无下格残条，单格框完整；与新主图同步后物件身份一致。';
  }
  if (record.id === 'component_icon_style') {
    row.action = '随新主图派生图标';
    row.quantity = '25 张；其中 2 张残条修复已列 P1，不重复记工';
    row.acceptance = '小尺寸清楚；新主图和库存图标指向同一造型，框与材质统一。';
  }
  if (record.id === 'component_variant_identity') {
    row.quantity = '150 条定义共享 25 套原型（50 个相关 PNG）；先处理形制冲突明显的变体';
    row.action = '补充重点变体结构差异';
    row.acceptance = '名称指向的物件形制与图一致；狮首、美人鱼等无需靠文字才能区分。';
  }
  if (record.id === 'regional_furniture_alpha_export') {
    row.action = '重新导出真实透明度';
    row.quantity = '6 张；现役只显示 6 块铭牌';
    row.acceptance = '原画不变；透明孔洞、铁件及白色镶嵌保全，实际界面边缘干净。';
  }
  push(row);
}

push({key:'ui/dossier-parchment',category:'船员与装备档案羊皮纸',priority:'保留',usage:'现役船员/部件详情显示',quantity:'1 张源图',files:['UnityClient/Assets/_Project/Resources/PortArt/UI/tavern_crew_dossier_parchment_v1.png'],visual_evidence:['原图纸纤维、折痕、烧蚀边、绳股与封蜡层次清楚，主要文字区留白充足；精度不需要重画。'],optimization_direction:'保留。后续排版复核时保持头像窗口与文字区的装饰避让。',action:'保留现图',evidence:[{path:'UnityClient/Assets/_Project/Presentation/Runtime/PortArtCatalog.cs',line:49,claim:'加载UI/tavern_crew_dossier_parchment_v1。'},{path:'UnityClient/Assets/_Project/Presentation/Runtime/CoreGameplayTavernDossier.cs',line:37,claim:'船员详情绘制该羊皮纸。'},{path:'UnityClient/Assets/_Project/Presentation/Runtime/CoreGameplayComponentDossier.cs',line:29,claim:'部件详情同样使用该纸面。'}],review:['Artifacts/ArtQualityAudit20261012/contacts/PortArt_UI_01.jpg']});

const identity = characters.entries.filter(e=>e.shared_issue_id === 'character-identity-gray-gull-leisa');
push({key:'character/leisa',category:'雷萨：头像、登船全身、专属卡面身份统一',priority:'P2',usage:'现役显示',quantity:'头像 1 格 + 全身 1 格 + 卡面 1 张；三个入口合并一个任务',files:identity.map(e=>e.original_file_relative_path),visual_evidence:['既有叙事明确使用“她”，旧备用图为女性；三个现役入口都画为络腮胡人物。绘画细节充足，问题是跨入口身份与设定一致性。'],optimization_direction:['先确定与既有设定一致的雷萨角色基准，再同步三个入口；保留职业道具与姿态差异，头像/全身图集内其它五人无需重画。'],action:'局部角色校准',acceptance:'三个入口能认出同一雷萨，与既有设定一致；不因只修其中一个入口造成再次换人。',evidence:identity.flatMap(e=>evidenceOf(e)),review:['Artifacts/ArtQualityAudit20261012/character-review/gray-gull-identity-comparison.png']});
const fallback = characters.entries.filter(e=>e.category === '旧六职业全身备用资源');
push({key:'character/fallback',category:'旧六职业全身备用图',priority:'P3',usage:'仅兼容回退；正常区域图集完整时不显示',quantity:'最多 6 张；优先佩佩、诺雅的身份差异',files:fallback.map(e=>e.original_file_relative_path),visual_evidence:['旧图细节足够，但多复用长外套；佩佩的年龄/体形和诺雅的发型与现役形象差异明显。'],optimization_direction:'如继续保留兼容显示，同步六职业造型。排在现役资源之后。',action:'条件性兼容整理',acceptance:'只有加载回退时也保持同一人物身份。',evidence:fallback.flatMap(e=>evidenceOf(e))});
const retainedCharacters = characters.entries.filter(e=>e.priority === '保留' || (e.shared_issue_id && !e.resource.startsWith('CardArt/')));
for (const category of [...new Set(retainedCharacters.map(e=>e.category))]) {
  const group = retainedCharacters.filter(e=>e.category === category);
  const dormant = category.includes('旧');
  const regional=category.startsWith('六港');
  const quantity=regional ? '6 张图集 / 36 个角色；仅雷萨局部修正另列' : `${group.length} 张源图；${dormant ? '旧图集' : '每张 3 人，共 39 个角色形象'}`;
  push({key:'character/keep/' + category,category,priority:dormant ? '暂缓' : '保留',usage:group[0].used.status,quantity,files:group.map(e=>e.original_file_relative_path),visual_evidence:[...new Set(group.flatMap(e=>e.visual_evidence))].slice(0,4),optimization_direction:dormant ? '保留旧兼容资料，未发现当前显示调用，不安排本批重画。' : regional ? '保留整体画面；区域船员保留六港文化和各职业差异。雷萨局部身份校准另列。' : '保留普通/敌方编队人物的服装、职能与阵营差异。',action:dormant ? '不排入当前重画' : '保留整体',evidence:evidenceOf(group[0])});
}

for (const record of cards.entries) push({
  ...record,key:'cards/' + record.id,usage:record.id==='cards-paint-style' ? '当前卡面候选家族；最终显示按CardId优先映射' : record.usage,
  quantity:record.id === 'cards-paint-style' ? '216 张候选范围；先做 12 张代表样板' : record.count ? `${record.count} 张源图` : '非位图；本轮未录制动态效果',
  review:record.reviewed_contacts,
  priority:record.id === 'cards-old-frame-sources' ? '暂缓' : record.priority
});

// Environment records are kept grouped, including source-file coverage and usage evidence.
for (const record of environments.entries || environments.records || environments.families) {
  const row = {
  key:'environment/' + (record.id || record.category),category:record.category,
  priority:record.priority,usage:record.usage || record.usageStatus || record.status || record.used?.status || record.scope?.usageStatus || '',
  quantity:record.quantity || record.scope_label || `${filesOf(record).length} 张源图`,
  scope:record.scope,files:filesOf(record),
  visual_evidence:(record.visual_evidence || record.visualIssues || record.visualEvidence?.observations || record.findings || record.strengths),
  optimization_direction:record.optimization_direction || record.optimizationDirection,
  action:record.action || (record.priority === 'P1' ? '重画插图' : record.priority === 'P2' ? '先做画风样板' : record.priority === '保留' ? '保留现图' : '暂缓'),
  acceptance:record.acceptance,evidence:evidenceOf(record),
  review:record.reviewed_contacts || record.visualReview || record.visualEvidence?.contacts || record.review_artifacts
  };
  if (record.priority === 'P3') {row.originalPriority='P3';row.priority='暂缓';row.action='旧图/兼容资料，不排本批重画';}
  if (record.id === 'event-illustration-atlases') {
    row.quantity='4 张 5×4 图集，共 80 格；先做教学实际可见格';
    row.acceptance=['木材、人物、遗迹和海浪细绘与新船一致，仍能区分每个事件主题。','教学506×506及普通/寻宝238×280显示时主体清晰，不夹入旧边框或邻格。','原事件到图格的映射保持稳定；如改为独立插画须同步目录、裁切与引用。'];
    row.review=[...arr(row.review),'Artifacts/ArtQualityAudit20261012/event-detail-review.png'];
  }
  if (record.id === 'story-and-battle-comics') {
    row.quantity='26 页候选；先做撞击与开场/寻宝各 1 页样板';
    row.acceptance=['与新船并排时人物、船材质和用光协调；保留分镜、动作透视与叙事节奏。'];
  }
  if (record.id === 'regional-taverns') {
    row.quantity='6 张候选；先做灰鸥酒馆 1 张样板';
    row.acceptance=['保留六港人物身份、座位、动作与现有点击热区；材质和轮廓与新船协调。'];
  }
  if (record.id === 'card-workshop-chests') {
    row.quantity='4 张候选；先做标准箱 1 张样板';
    row.acceptance=['四类箱体保留功能符号、结构差异及接触点；木纹、铜件与轮廓处理一致。'];
  }
  row.visual_evidence=arr(row.visual_evidence).map(s=>String(s).replace(/清晰的像素轮廓|清晰像素轮廓/g,'精细的手绘轮廓').replace(/小粒度描边/g,'细腻描边'));
  push(row);
}

const shipResources = new Set(['ModularShip/light_sloop_base','PortArt/ship_roster_sheet','LegacyDemo/ship_sprite_sheet','NavigationLookout/lookout_pirate_v1']);
push({key:'ships/completed-reference',category:'船只：本轮精度参照，已完成上一轮重画',priority:'已完成',usage:'现役/参照',quantity:'4 个生产 PNG 文件',files:inventory.images.filter(i=>shipResources.has(i.resource)).map(i=>'UnityClient/Assets/_Project/'+i.relative),visual_evidence:'初始船及上一轮已更新的船只作为细节和材质标准，不重复安排本次重画。',optimization_direction:'后续其它资源与船并排验收，仍保留各自结构身份。',action:'本轮排除重复工作',review:['Artifacts/ShipArtRedraw20261012/preview/all-player-ships.png']});
push({key:'archive/excluded',category:'编辑器归档图',priority:'暂缓',usage:'Editor/ArchivedArt；不属于现役 Resources 图库',quantity:`${inventory.archived.length} 张；本轮仅记录，不评价为低精度`,files:inventory.archived.map(p=>'UnityClient/Assets/_Project/'+p),visual_evidence:'目录位于 Editor/ArchivedArt，未作为当前运行资源排期。',optimization_direction:'保留来源。后续只有重新启用实际显示入口时再审阅，不因存有旧图安排重复重画。',action:'归档记录，退出本批排期'});

const order = {'P1':0,'P2':1,'P3':2,'待动态审查':3,'保留':4,'暂缓':5,'已完成':6};
rows.sort((a,b)=>(order[a.priority] ?? 7)-(order[b.priority] ?? 7));
const counters = {};
for (const row of rows) {
  const prefix = row.priority === '保留' ? 'KEEP' : row.priority === '暂缓' ? 'HOLD' : row.priority === '已完成' ? 'REF' : row.priority === '待动态审查' ? 'FX' : 'ART';
  counters[prefix]=(counters[prefix]||0)+1;
  row.id=prefix + '-' + String(counters[prefix]).padStart(2,'0');
  if (typeof row.quantity !== 'string') row.quantity=JSON.stringify(row.quantity);
  row.evidence = [...new Map(row.evidence.map(e=>[e.path+':'+e.line,e])).values()];
}

const rowFiles = new Set(rows.flatMap(r=>r.files));
const unassigned = inventory.images.filter(i=>!rowFiles.has('UnityClient/Assets/_Project/'+i.relative));
if (unassigned.length) throw new Error('Unassigned source files: ' + unassigned.map(i=>i.resource).join(', '));
const missingFiles = [...rowFiles].filter(p=>!fs.existsSync(abs(p)));
if (missingFiles.length) throw new Error('Missing source files: ' + missingFiles.join(', '));
const missingEvidence = rows.flatMap(r=>r.evidence).filter(e=>e.path && !fs.existsSync(abs(e.path)));
if (missingEvidence.length) throw new Error('Missing evidence paths: ' + JSON.stringify(missingEvidence));

const csvCell = value => '"' + String(value ?? '').replace(/"/g,'""') + '"';
const csv = (headers, data) => '\uFEFF' + [headers,...data].map(row=>row.map(csvCell).join(',')).join('\r\n')+'\r\n';
const taskCsv = csv(['编号','优先级','资源类别','范围','当前用途','行动','可见问题/审阅结论','优化方向','验收','源文件','调用证据'], rows.map(r=>[r.id,r.priority,r.category,r.quantity,r.usage,r.action,r.visual_evidence.join('；'),r.optimization_direction.join('；'),r.acceptance.join('；'),r.files.join('\n'),r.evidence.map(e=>e.path+':'+e.line+' '+e.claim).join('\n')]));
fs.writeFileSync(output+'/optimization-list.csv',taskCsv);
const manifest = inventory.images.map(i=>{
  const applicable=rows.filter(r=>r.files.includes('UnityClient/Assets/_Project/'+i.relative));
  return {...i,report_ids:applicable.map(r=>r.id),classifications:[...new Set(applicable.map(r=>r.priority))],is_ship_reference:shipResources.has(i.resource)};
});
fs.writeFileSync(output+'/per-file-audit.csv',csv(['资源路径','类别','原图宽','原图高','源透明度','默认FilterMode','默认导入最大尺寸','审核条目','分类','船只参照'],manifest.map(i=>[i.path,i.group,i.width,i.height,i.hasAlpha,i.import.filterMode,i.import.maxTextureSize,i.report_ids.join(' / '),i.classifications.join(' / '),i.is_ship_reference])));

async function eventDetails() {
  const names=['LegacyDemo/shattered_sail_event_sheet_v2','LegacyDemo/golden_tide_event_sheet_v2','LegacyDemo/moon_tide_event_sheet_v2','TreasureHunt/treasure_hunt_event_sheet'];
  const composites=[],labels=[];
  for (let n=0;n<names.length;n++) {
    const item=inventory.images.find(i=>i.resource===names[n]);
    const tile=await sharp(item.path).extract({left:0,top:0,width:307,height:256}).resize(614,512,{kernel:'nearest'}).png().toBuffer();
    const x=n%2*634+10,y=Math.floor(n/2)*550+34;
    composites.push({input:tile,left:x,top:y});
    labels.push(`<text x="${x}" y="${y-10}" font-size="16" fill="#eef1f0">${escape(path.basename(names[n]))} / cell 0 / 2x inspection</text>`);
  }
  composites.push({input:Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="1268" height="1100"><g font-family="Arial">${labels.join('')}</g></svg>`),left:0,top:0});
  await sharp({create:{width:1268,height:1100,channels:3,background:'#14242e'}}).composite(composites).png().toFile(output+'/event-detail-review.png');
}

const bullets = values => arr(values).length ? '<ul>'+arr(values).map(v=>'<li>'+escape(v)+'</li>').join('')+'</ul>' : '';
const sources = row => `<details><summary>${row.files.length} 个源文件与 ${row.evidence.length} 条调用证据</summary><ul class="paths">${row.files.map(p=>`<li><a href="${href(p)}">${escape(p)}</a></li>`).join('')}</ul>${row.evidence.length?'<ul class="paths">'+row.evidence.map(e=>`<li><a href="${href(e.path)}">${escape(e.path)}:${escape(e.line)}</a> — ${escape(e.claim)}</li>`).join('')+'</ul>':''}</details>`;
const reviewLinks = row => {
  const known = row.review.filter(p=>fs.existsSync(abs(p)));
  return known.length ? `<details><summary>审核图证据（${known.length}）</summary><div class="review">${known.map(p=>`<a href="${href(p)}"><img loading="lazy" src="${href(p)}" alt="${escape(path.basename(p))}"><span>${escape(path.basename(p))}</span></a>`).join('')}</div></details>` : '';
};
const rowHtml = row => `<article class="entry" id="${row.id}" data-priority="${escape(row.priority)}" data-search="${escape(row.category+' '+row.files.join(' '))}"><div class="entry-title"><span class="pill ${escape(row.priority)}">${escape(row.priority)}</span><h3>${row.id} · ${escape(row.category)}</h3></div><div class="facts">${escape(row.quantity)} · ${escape(row.usage)} · ${escape(row.action)}</div><div class="columns"><div><h4>审阅结论</h4>${bullets(row.visual_evidence)}</div><div><h4>优化方向</h4>${bullets(row.optimization_direction)}${row.acceptance.length?'<h4>验收标准</h4>'+bullets(row.acceptance):''}</div></div>${sources(row)}${reviewLinks(row)}</article>`;
const briefP1 = rows.filter(r=>r.priority==='P1').map(r=>`<tr><td><a href="#${r.id}">${r.id}</a></td><td>${escape(r.category)}</td><td>${escape(r.quantity)}</td><td>${escape(r.action)}</td></tr>`).join('');
const familyCounts=Object.entries(inventory.images.reduce((a,i)=>(a[i.family]=(a[i.family]||0)+1,a),{}));
const report={date:'2026-10-12',scope:'生产位图精细程度、画风与一致性检查；不重画、不替换资源、不出包。',reference:'初始船 light_sloop_base + 上一轮更新船图',counts:{resourcesPng:inventory.images.length,shipReferencePng:inventory.images.filter(i=>shipResources.has(i.resource)).length,otherResourcesPng:inventory.images.filter(i=>!shipResources.has(i.resource)).length,editorArchivedPng:inventory.archived.length,sourceContactSheets:inventory.contacts.length,coveredOtherResourcesPng:manifest.filter(i=>!i.is_ship_reference&&i.report_ids.length).length},limitations:['已目视原图/审核联系图，并核对运行代码的显示入口和图集切格；本轮没有启动游戏、录制动态效果或运行Unity测试。','导入尺寸和压缩仅作支持信息；没有按尺寸低就判低精度，也没有将透明像素的隐藏RGB误判成可见脏边。','卡面216张和漫画/酒馆等P2项是风格样板候选，数量不是立即全量重画任务量。','同一源图会同时列出保留主体、局部身份修复、导出清理等条目；不能把各行数量相加。',`${inventory.archived.length}张现存Editor归档图只记录来源，未排入现役优化；获批清理项已从本次报告移除。`],cleanupReconciliation:{date:'2026-10-13',retiredRows,manifest:'Artifacts/UnusedAssetAudit20261013/review-manifest.json'},rows,manifest};
fs.writeFileSync(output+'/optimization-list.json',JSON.stringify(report,null,2));

async function main() {
  await eventDetails();
  const html=`<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>美术资源优化清单 · 2026-10-12</title><style>
  :root{color-scheme:light;--ink:#253641;--muted:#64717a;--line:#dde3e4;--gold:#947044}*{box-sizing:border-box}body{margin:0;background:#f4f5f3;color:var(--ink);font:15px/1.7 "Microsoft YaHei",system-ui,sans-serif}main{max-width:1200px;margin:auto;padding:44px 28px 80px}h1{font-size:32px;margin:0 0 12px;letter-spacing:.01em}h2{font-size:23px;margin-top:42px}h3{font-size:18px;margin:0}h4{font-size:14px;margin:12px 0 4px;color:var(--gold)}a{color:#236679;text-underline-offset:3px}p{margin:8px 0 16px}.kicker{color:var(--gold);font-size:12px;letter-spacing:.15em}.lead{font-size:18px;max-width:970px}.stats{display:flex;gap:12px;flex-wrap:wrap;margin:24px 0}.stat{background:#fff;border:1px solid var(--line);padding:16px 22px;min-width:150px}.stat b{font-size:28px;display:block}.muted,.facts{color:var(--muted)}.callout{border-left:4px solid #b3724b;background:#f7eee6;padding:14px 20px}.reference{display:grid;grid-template-columns:1fr 1.65fr;gap:18px}.reference figure{margin:0;background:#14242e;border-radius:6px;overflow:hidden}.reference img{display:block;width:100%;height:300px;object-fit:contain}.reference figcaption{background:#fff;padding:10px 14px;font-size:13px}.toolbar{position:sticky;top:0;z-index:2;background:#f4f5f3ee;backdrop-filter:blur(12px);display:flex;gap:10px;padding:14px 0;border-bottom:1px solid var(--line);align-items:center;flex-wrap:wrap}input,select{font:inherit;padding:8px 12px;border:1px solid #cbd4d5;border-radius:4px;background:#fff}input{flex:1;min-width:220px}.entry{background:#fff;border:1px solid var(--line);border-radius:6px;margin:18px 0;padding:22px;scroll-margin-top:100px}.entry-title{display:flex;align-items:center;gap:12px}.pill{font-size:12px;white-space:nowrap;padding:3px 10px;border-radius:30px;background:#e7eceb;color:#53635f}.P1{background:#fbe1d8;color:#9f381a}.P2{background:#f7edce;color:#805d14}.P3{background:#e5ebf7;color:#3e568d}.facts{font-size:13px;margin:8px 0}.columns{display:grid;grid-template-columns:1fr 1fr;gap:28px}ul{padding-left:20px;margin:6px 0 12px}li{margin:5px 0}details{margin:12px 0 0;border-top:1px solid #edf0ef;padding-top:10px}summary{cursor:pointer;color:#42636d}.paths{font:12px/1.8 ui-monospace,Consolas,monospace;overflow-wrap:anywhere;max-height:350px;overflow:auto}.review{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:12px;padding:12px 0}.review img{width:100%;max-height:280px;object-fit:contain;background:#14242e}.review span{font-size:11px;display:block;overflow-wrap:anywhere}table{border-collapse:collapse;width:100%;background:#fff;font-size:14px}th,td{padding:12px 14px;border-bottom:1px solid var(--line);text-align:left;vertical-align:top}th{background:#e9efed}.gallery{display:grid;grid-template-columns:1fr 1fr;gap:18px}.gallery img{width:100%;border-radius:4px;background:#14242e}.gallery figure{margin:0}.gallery figcaption{font-size:13px;color:var(--muted)}.family-list{display:flex;gap:8px;flex-wrap:wrap}.family-list span{background:#fff;border:1px solid var(--line);padding:4px 10px;font-size:12px}.downloads{display:flex;gap:18px;flex-wrap:wrap}.hidden{display:none}.foot{font-size:12px;border-top:1px solid var(--line);padding-top:20px;margin-top:40px}@media(max-width:760px){main{padding:24px 16px}h1{font-size:25px}.columns,.reference,.gallery{grid-template-columns:1fr}.entry{padding:16px}.reference img{height:220px}.entry-title{align-items:flex-start}.table-scroll{overflow:auto}table{min-width:650px}}@media print{.toolbar{display:none}.entry{break-inside:avoid}details{display:block}.downloads{display:none}body{background:white}main{padding:0}}
  </style></head><body><main><div class="kicker">ART QUALITY AUDIT · 2026-10-12</div><h1>后续美术资源优化清单</h1><p class="lead">首批建议重画 25 个装备主图原型与 80 格事件插图，并修复 2 张库存图标残条。角色、卡面和大场景大多已有足够细节，其余工作以画风样板、局部一致性和素材导出整理为主。</p><p class="muted">本轮只检查并整理任务，不重画、不替换生产资源、不出包。船只沿用上一轮已完成结果作为参照。</p><div class="stats"><div class="stat"><b>${report.counts.otherResourcesPng}</b>其他生产位图</div><div class="stat"><b>${report.counts.shipReferencePng}</b>船只参照 PNG</div><div class="stat"><b>${report.counts.editorArchivedPng}</b>编辑器归档图，仅记录</div><div class="stat"><b>${report.counts.sourceContactSheets}</b>源图审核联系表</div></div><div class="downloads"><a href="optimization-list.csv" download>下载分批优化清单 CSV</a><a href="per-file-audit.csv" download>下载全部 ${report.counts.resourcesPng} 文件映射 CSV</a><a href="source-contact-index.html">浏览全部源图审核表</a><a href="optimization-list.json">完整证据 JSON</a></div>
  <h2>统一标准：精细程度一致，结构身份仍有差异</h2><p>以初始船的木纹、板缝、绳股、黄铜受光和干净轮廓为标准。每类装备、港口与人物保留自己的功能、文化和结构；避免只换颜色、光圈或标志。</p><div class="reference"><figure><img src="${href('UnityClient/Assets/_Project/Resources/ModularShip/light_sloop_base.png')}" alt="初始船参考"><figcaption>初始船：材质、轮廓与细节基准</figcaption></figure><figure><img src="${href('Artifacts/ShipArtRedraw20261012/preview/all-player-ships.png')}" alt="上一轮已完成船只"><figcaption>上一轮船只：保持船体结构和用途差异</figcaption></figure></div>
  <h2>第一批：明确精度差距与可见瑕疵</h2><div class="table-scroll"><table><thead><tr><th>任务</th><th>资源</th><th>工作范围</th><th>行动</th></tr></thead><tbody>${briefP1}</tbody></table></div><div class="gallery" style="margin-top:20px"><figure><a href="modular-layer-subjects-contact.png"><img src="modular-layer-subjects-contact.png" alt="当前装备主图审核"></a><figcaption>装备主图：颗粒轮廓、整圈外光与局部裁切；原型保持各自结构。</figcaption></figure><figure><a href="event-detail-review.png"><img src="event-detail-review.png" alt="事件插图局部审核"></a><figcaption>4 张事件图集首格局部，2 倍最近邻放大用于检视源细节，不是游戏截图。原格约 307×256；教学显示 506×506，普通嵌入 238×280。</figcaption></figure></div>
  <div class="callout" style="margin-top:24px"><b>排期口径</b>：P1 是明确差距或瑕疵；P2 先做代表样板/重点对象，再确定扩展量；P3 是低优先局部或条件性兼容整理。216 张卡面不等于必须立即重画 216 张。文件、图集格数和装备定义是不同数量，同源图多个问题不能相加记工。</div>
  <h2>完整任务与保留清单</h2><div class="toolbar"><label>优先级 <select id="priority"><option value="all">全部</option>${Object.keys(order).map(p=>`<option>${escape(p)}</option>`).join('')}</select></label><input id="query" type="search" placeholder="搜索资源类别、文件名"><span id="visible-count" class="muted"></span></div><div id="entries">${rows.map(rowHtml).join('')}</div>
  <h2>范围与核查方式</h2>${bullets(report.limitations)}<p>每一张生产 PNG 均映射到上面的审核条目。目录统计包含船只参照；Editor 归档单独记录。字体、音频不属于本轮画面精细程度判断，程序特效另列待动态检查。</p><div class="family-list">${familyCounts.map(([name,count])=>`<span>${escape(name)} · ${count}</span>`).join('')}</div><p class="foot">证据来源：本地原图、透明合成审核图、运行代码加载/显示入口。报告未修改 Unity 生产资源、场景、导入设置或包文件。</p></main><script>const priority=document.getElementById('priority'),query=document.getElementById('query'),entries=[...document.querySelectorAll('.entry')];function update(){let visible=0;for(const entry of entries){const show=(priority.value==='all'||entry.dataset.priority===priority.value)&&entry.dataset.search.toLowerCase().includes(query.value.toLowerCase());entry.classList.toggle('hidden',!show);if(show)visible++;}document.getElementById('visible-count').textContent=visible+' / '+entries.length+' 条';}priority.addEventListener('change',update);query.addEventListener('input',update);update();</script></body></html>`;
  fs.writeFileSync(output+'/optimization-list.html',html);
  console.log(JSON.stringify({rows:rows.length,counts:report.counts,unassigned:unassigned.length,missingSources:missingFiles.length,output:output+'/optimization-list.html'},null,2));
}
main().catch(error=>{console.error(error);process.exitCode=1;});
