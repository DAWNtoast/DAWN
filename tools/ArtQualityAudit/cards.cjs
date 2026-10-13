const fs = require('fs');
const output = process.env.ART_QUALITY_AUDIT_OUTPUT || 'D:/Game/Artifacts/ArtQualityAudit20261012';
const inventory = JSON.parse(fs.readFileSync(output+'/source-inventory.json','utf8'));
const images = group => inventory.images.filter(i=>i.group===group).map(i=>'UnityClient/Assets/_Project/'+i.relative);
const evidence = (path,line,claim) => ({path:'UnityClient/Assets/_Project/Presentation/Runtime/'+path,line,claim});
const entries = [
  {
    id:'cards-paint-style',category:'普通、特殊和流派卡面绘画',priority:'P2',usage:'现役显示',
    count:216,files:[...images('CardArt/Western'),...images('CardSchools/Art')],
    visual_evidence:['逐组目视80张Western与136张流派卡面；人物服装、木纹、金属、帆布和海浪都有较高绘制细节，没有旧船图那种低分辨率块状轮廓。','多数卡面有明显照片/油画感和细碎颗粒，新船图采用较清晰的手绘轮廓与受控高光；这是画风统一项，不能把216张都归为精度不足。'],
    optimization_direction:'建议先统一12张高频代表卡面的笔触、木材和黄铜高光、人物与背景层次，再按同一标准分批微调。保留动作和流派色彩差异，不因统一风格重复同一构图；雷萨专属卡面与角色身份条目合并，不重复计算工作。',
    action:'风格统一，先做代表样板；不建议立即全量重画',
    acceptance:'与船图并排时材质与轮廓处理协调；在实际206×175插画区域中能看清动作/器具；各流派依然可辨。',
    evidence:[evidence('CardSchoolArtCatalog.cs',106,'优先按CardId加载CardArt/Western，缺失时按ArtId加载CardSchools/Art。'),evidence('CardSchoolArtCatalog.cs',143,'插画按CardFaceLayout.ArtSize比例裁切。'),evidence('CardFaceLayout.cs',15,'现役卡牌插画区域为206×175；原图1254²、Standalone导入512²，不应只按导入尺寸判定低精度。')],
    reviewed_contacts:inventory.contacts.filter(c=>c.group==='CardArt/Western'||c.group==='CardSchools/Art').map(c=>c.file)
  },
  {
    id:'cards-subject-readability',category:'少量卡面主体辨识与暗部层次',priority:'P3',usage:'现役显示',
    count:4,files:['Resources/CardArt/Western/card_special_instrument_guild_engine.png','Resources/CardArt/Western/card_component_instrument.png','Resources/CardSchools/Art/card_school_curse_e02.png','Resources/CardSchools/Art/card_school_arcane_l05.png'].map(p=>'UnityClient/Assets/_Project/'+p),
    visual_evidence:['仪器类原图细节充足，但人物、海图、船只和机械同时进入画面，小尺寸下主体信息较多；诅咒夜景与奥术大炮保留了较重暗部。','这些是构图/对比优化候选；本轮没有发现必须重画的像素精度问题。'],
    optimization_direction:'在实际卡牌尺寸下审阅裁切，适度放大器具/动作或降低次要背景对比；夜景只校准主体轮廓与局部明度，不统一改成白天。',
    action:'局部构图、裁切和明暗复核',acceptance:'缩小后不用放大就能辨认器具与主要动作；保留夜景与流派氛围。',
    evidence:[evidence('CardFaceLayout.cs',15,'实际插画尺寸206×175。'),evidence('CardSchoolArtCatalog.cs',139,'IllustrationUv中心裁切源图；主体位置影响最终小卡可读性。')]
  },
  {
    id:'cards-integrated-frames',category:'当前流派卡框与费用徽章',priority:'保留',usage:'现役显示',
    count:36,files:images('CardSchools/Frames/Integrated'),
    visual_evidence:['目视全部36个成品框；木纹、金属浮雕、火焰/雷光、羊皮纸和费用图案精度足够，透明区域和边缘构造完整。','当前成品框统一费用位置与七种流派/中立材质体系；无需因为船只重画再次重画整套。'],
    optimization_direction:'保留。后续如统一调色，保持现有卡框孔位、文字区和费用位置标定。',action:'保留现图',acceptance:'不破坏框内插画、数字和效果文字布局。',
    evidence:[evidence('CardSchoolArtCatalog.cs',43,'当前FrameResourcePath只选择Frames/Integrated成品框。'),evidence('CardSchoolArtCatalog.cs',65,'费用位置和框边界由frame-layouts标定。')]
  },
  {
    id:'cards-old-frame-sources',category:'旧流派卡框基底',priority:'保留',usage:'旧制作来源；无当前显示引用',
    count:7,files:images('CardSchools/Frames'),
    visual_evidence:['7张旧基底细节并不差，但当前显示路径采用36张Integrated成品框；旧基底没有费用/纸区整合。','检查七张png的GUID：当前项目scene/prefab/asset/material中没有序列化引用；runtime中没有frame-*-v1加载路径。'],
    optimization_direction:'制作来源保留，退出当前重画排期。存在文件不等于当前游戏可见，不建议因本轮审核删除。',action:'不排入当前重画',acceptance:'后续优化只替换实际Integrated显示资源。',
    evidence:[evidence('CardSchoolArtCatalog.cs',43,'现役路径使用Frames/Integrated，不使用此组-v1基底。')]
  },
  {
    id:'procedural-fx-review',category:'炮火、烟雾、刀光及船体水效',priority:'待动态审查',usage:'现役程序绘制；非位图资源',
    count:0,files:[],
    visual_evidence:['源码确认炮口闪光、烟雾、撞击等由BattleFxGraphic网格绘制；登船刀光/盾/治疗与船体水效有独立Graphic。','本轮没有录制当前动态效果，不能从几何实现推断其精度不足；静态位图清单不能覆盖时序、节奏与运动观感。'],
    optimization_direction:'后续在新船和新部件接入后，录制一轮炮击、近战、治疗和航行水效；对照手绘风格检查烟雾层次、拖尾和闪光强度，再确定是否需要纹理或动画优化。',action:'补充动态观感检查；暂不宣判重画',acceptance:'效果与新船材质协调且战斗动作清晰。',
    evidence:[evidence('BattleFxGraphic.cs',58,'OnPopulateMesh按10种效果绘制，无额外纹理四边形。'),evidence('BoardingCombatFxGraphic.cs',59,'登船5类动作效果通过网格绘制。'),evidence('BattleShipWaterGraphic.cs',6,'船体水效是程序Graphic，不属于PNG资源家族。')]
  }
];
const data={date:'2026-10-12',scope:'卡面、卡框及非位图特效补充；只读原图与源码，不改游戏资源，不出包。',reviewedCardImages:259,entries};
fs.writeFileSync(output+'/cards-audit.json',JSON.stringify(data,null,2));
console.log(JSON.stringify({entries:entries.length,cardFiles:259,output:output+'/cards-audit.json'}));
