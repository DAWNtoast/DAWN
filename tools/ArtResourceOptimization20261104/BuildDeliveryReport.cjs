// Build the final local review index from preserved validation evidence.
const fs=require('node:fs'),path=require('node:path');
const root='D:/Game/outputs/art-resource-optimization-20261104';
const validation='D:/Game/outputs/comic-pagination-20261103/art-opt-01-20261104';
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const cleanup=read(validation+'/cleanup-verification.json');
if(cleanup.DifferencesDuringCleanup!==0)throw Error('Cleanup changed main inputs');
const installed=read(validation+'/finaledit/ram-compression-installed/report.json');
if(installed.pages.length!==16||installed.pages.some(x=>!x.qualityPassed))throw Error('Installed ram validation incomplete');
const tests=[];
for(const [run,mode] of [['finaledit','EditMode'],['comicfinal4k','EditMode'],['comicplay','PlayMode'],['visualfinal','PlayMode'],['ramfinal4k','PlayMode'],['deliveryedit','EditMode'],['deliveryvisual','PlayMode']]){
 const file=validation+'/'+run+'/'+mode+'.xml',text=fs.readFileSync(file,'utf8');
 const tag=text.match(/<test-run\b[^>]*>/)[0],attrs=Object.fromEntries([...tag.matchAll(/([\w-]+)="([^"]*)"/g)].map(x=>[x[1],x[2]]));
 if(+attrs.failed!==0||+attrs.passed===0)throw Error('Unresolved validation: '+run);
 tests.push({run,mode,file,total:+attrs.total,passed:+attrs.passed,skipped:+attrs.skipped});
}
const scene=read(root+'/scene-review/deliveryvisual/capture-manifest.json');
const comic=read(root+'/comic-review/4k-capture-manifest.json');
const ram=read(root+'/scene-review/ramfinal4k/capture-manifest.json');
if(scene.length!==89||comic.length!==104||ram.length!==16)throw Error('Missing final captures');
const baseline=installed.pages.reduce((s,x)=>s+x.baselineRuntimeBytes,0);
const candidate=installed.pages.reduce((s,x)=>s+x.candidateRuntimeBytes,0);
const reduction=100*(1-candidate/baseline),psnr=installed.pages.map(x=>x.originalToCandidate.psnrDb);
const mib=x=>(x/1048576).toFixed(2);
const lines=[
 '《第七港口》美术优化验收｜2026-11-05',
 '',
 '已按11月4日审核的01—10范围写入Unity工程。以下为最终采用版本，早期handoff、失败测试和废弃候选仅作诊断记录。',
 '',
 '01 分镜：26页共104个独立显示资源，原生1672×940/941；原四格单格约836×470，线性有效像素约翻倍。保留26张原页与缺图回退。六段传奇后篇第3格沿用同一独自离港情节，属于有意共图。104格分别完成1080p及3840×2160生产UI渲染；4K是渲染输出，不是原图原生分辨率。',
 '02 海战环境光：六种海域同步航程昼/暮/夜，海面反光及船体照明随时段刷新；27帧覆盖18种海域时段组合及航行/海战/登船衔接。',
 '03 阵容：终界观星号、月织星槎号新增专属透明三人队，保留静态呈现、原站位和六靴接触阴影。',
 '04 甲板：采用1774×887同尺寸细节修正v3，改善索具、滑轮、栏杆及木纹。旧v2完整保留；原裁切、透视、角色位置与昼夜着色保持。昼夜1080p/4K前后对照通过。它是重新细化，不是4K原生素材；先前收益微弱的叠加细纹材质方案已撤下。',
 '05 商店：保留断锚港原大厅，另外五港使用独立建筑、窗景、灯具与材质，六港共12张1080p/4K实景；大商品展示、分类和点击区域不变。',
 '06 遭遇：11幅独立1448×1086插画；6项现役事件与藏宝节点、5项旧版存档线索。按精确ID绑定，旧别名不被误替换。22张实景核对正文、主体与边界；沿用8像素防串色裁边和原4:3版面。',
 '07 特效：补充挥砍、格挡、破盾、治疗的图集笔触与衰减；12帧为固定进度采样，另以实际回合测试核对静态人物和规则提交。',
 '08 状态：34设计单位角标简化，主图占比81%→90%；状态提示保留，已审定物资图和资源栏悬停规则不变。',
 '09 控件：设置勾选、成就下拉的纸面风格和选择态统一；主菜单仍使用文字“设置”。',
 '10 撞击漫画：16页源PNG字节不变，边缘填充2行对齐BC7，显示UV裁除填充；四页加载/切换/关闭时序保持。',
 `   安装后16/16图像质量通过，PSNR ${Math.min(...psnr).toFixed(3)}—${Math.max(...psnr).toFixed(3)} dB。逐纹理Profiler原生分配相加 ${mib(baseline)}→${mib(candidate)} MiB，降低${reduction.toFixed(2)}%。这不是总GPU显存、包体大小，也不表示16页同时加载；未测首次播放速度，不宣称加载提速。`,
 '',
 '验证与范围',
 ...tests.map(x=>`${x.run} / ${x.mode}：通过${x.passed}，跳过${x.skipped}，失败0。${x.file}`),
 'finaledit跳过的是与installed模式互斥的候选试验入口；完整候选16页另由ramall通过。测试间有重叠，不将计数相加作为独立用例数量。',
 '最终图库：104张分镜4K、89张场景及前后对照、16张撞击实播4K。测试使用实际生产UI与隔离内存状态，并非手工拼接界面；环境光/特效采样不冒充完整通关。未进行全游戏完整流程验收。',
 '卡牌定义、卡面文字和排版不在本轮范围；未新增玩家说明小字。图像生成/编辑仅使用内置工具，未调用外部图像API。',
 '',
 '来源与恢复',
 `${root}/final-source-verification.json`,
 `${validation}/validated-source/（最终同步源码快照）`,
 '原画、生成来源、安装清单、source-before备份和必要日志/截图保留。',
 `借用验证工程已按journal恢复并核验；清除任务新增文件、Temp与重复恢复备份，回收${mib(cleanup.ReclaimedBytes)} MiB。主工程${cleanup.ComparedInputs}个同步输入清理期间哈希变化0。`,
 '主工程UnityClient/Library未清理；借用工程共享Library保留给现有验证工具复用。',
 '',
 '查看入口：'+root+'/验收索引.html'
];
fs.writeFileSync(root+'/美术优化验收.txt',lines.join('\r\n')+'\r\n');
const xml=s=>s.replace(/[&<>"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));
const rel=p=>encodeURI(path.relative(root,p).replace(/\\/g,'/'));
const links=[['104格分镜 · 4K实际画面',root+'/comic-review/index.html'],['各场景与甲板前后对照',root+'/scene-review/deliveryvisual/index.html'],['16页撞击漫画 · 4K实播',root+'/scene-review/ramfinal4k/index.html'],['完整验收记录',root+'/美术优化验收.txt'],['来源核验',root+'/final-source-verification.json']];
const samples=[['昼夜海战',root+'/scene-review/deliveryvisual/Art02-1.jpg'],['专属登船阵容',root+'/scene-review/deliveryvisual/Art03-1.jpg'],['六港部件商店',root+'/scene-review/deliveryvisual/Art05-1.jpg'],['甲板细节前后对照',root+'/scene-review/deliveryvisual/Art04-1.jpg']];
fs.writeFileSync(root+'/验收索引.html','<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>第七港口 · 美术优化验收</title><style>body{margin:0;background:#13212a;color:#f5eddc;font:17px/1.6 system-ui}main{max-width:1280px;margin:auto;padding:32px}h1{margin:0 0 12px}a{color:#e6c28b}nav{display:flex;flex-wrap:wrap;gap:16px;padding:20px 0}section{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:20px}figure{margin:0;padding:14px;background:#20313c;border-radius:10px}img{width:100%;height:auto}figcaption{margin-bottom:10px}@media(max-width:800px){section{grid-template-columns:1fr}}</style><main><h1>第七港口 · 美术优化验收</h1><p>104格独立分镜、昼夜海战、专属阵容、六港商店与11幅遭遇已接入。甲板采用同尺寸细节修正；撞击漫画纹理原生分配占用降低约71.4%。</p><nav>'+links.map(([label,p])=>'<a href="'+rel(p)+'">'+xml(label)+'</a>').join('')+'</nav><section>'+samples.map(([label,p])=>'<figure><figcaption>'+xml(label)+'</figcaption><a href="'+rel(p)+'"><img loading="lazy" src="'+rel(p)+'"></a></figure>').join('')+'</section><p>4K指实际渲染输出；漫画分镜源图1672×940/941，甲板源图1774×887。完整测试范围与清理记录见验收文本。</p></main></html>');
fs.writeFileSync(root+'/delivery-validation.json',JSON.stringify({recordedAtUtc:new Date().toISOString(),tests,captures:{comic:comic.length,scene:scene.length,ram:ram.length},ram:{baseline,candidate,reduction,minimumPsnr:Math.min(...psnr),maximumPsnr:Math.max(...psnr)},cleanup},null,2));
console.log(JSON.stringify({report:root+'/美术优化验收.txt',index:root+'/验收索引.html',tests:tests.length}));
