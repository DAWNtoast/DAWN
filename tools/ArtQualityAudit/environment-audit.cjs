// Read-only source-art review. Writes a report, never production assets.
const fs = require('fs');
const path = require('path');
const project = 'D:/Game';
const outDir = process.env.ART_QUALITY_AUDIT_OUTPUT || path.join(project, 'Artifacts/ArtQualityAudit20261012');
const inventory = JSON.parse(fs.readFileSync(path.join(outDir, 'source-inventory.json'), 'utf8'));
require('./cleanup-20261013.cjs').validateInventory(inventory);
const base = 'UnityClient/Assets/_Project/';
const runtime = base + 'Presentation/Runtime/';
const evidence = (file, line, detail) => ({ file: runtime + file, line, detail });
const groups = [];
function family(id, category, priority, select, visual, direction, usage, options = {}) {
  const selected = inventory.images.filter(select);
  if (!selected.length) throw new Error('No images for ' + id);
  groups.push({
    id, category, priority, status: options.status || '当前使用',
    count: selected.length,
    scope: options.scope || category,
    files: selected.map(i => base + i.relative),
    resources: selected.map(i => i.resource),
    sourceDimensions: [...new Set(selected.map(i => i.width + 'x' + i.height))],
    representatives: (options.representatives || selected.slice(0, 2).map(i => i.relative)).map(p => base + p),
    visualEvidence: {
      contacts: inventory.contacts.filter(c => c.images.some(r => selected.some(i => i.resource === r))).map(c => c.file),
      observations: visual
    },
    optimizationDirection: direction,
    runtimeEvidence: usage,
    ...Object.fromEntries(Object.entries(options).filter(([k]) => !['status', 'scope', 'representatives'].includes(k)))
  });
}
const inGroup = name => i => i.group === name;

family('port-hubs', '六港主场景', '保留', i => /^PortArt\/port_hub_.+_v3$/.test(i.resource),
  ['港楼、栈桥、砖木、灯火和海面均有充分细节，六港的建筑轮廓与色调能区分。远景建筑逐层概括是景深处理，并非粗糙旧图。'],
  '保留六港构图与材质；若后续统一像素颗粒，先以灰鸥港和新初始船并排做样板，不需要整批补纹理。',
  [evidence('PortArtCatalog.cs', 80, 'PortBackgroundFor按港口名称加载六个port_hub_*_v3。'), evidence('CoreGameplayPortController.cs', 120, '港口界面使用当前港口背景。')]);

family('legacy-battle-backdrops', '旧海战背景图集', 'P3', i => i.resource === 'PortArt/battle_backdrop_sheet',
  ['2×2旧图集的小格和烘焙装饰比现行单张海战背景更概括；不能据此评价当前海战画面。'],
  '当前不排重画；保留兼容/资源验证用途，恢复实际显示前再审核。',
  [evidence('PortArtCatalog.cs', 48, 'getter加载此图集；IsAvailable也检查它。'), evidence('PortArtCatalog.cs', 214, 'BattleBackdropUv仍存在，但当前Presentation/Runtime未找到实际视图调用。'), evidence('CoreGameplayDemoController.cs', 1281, '当前海战改用BattleEnvironmentArtCatalog.ForTerrain。')],
  { status: '目录与可用性验证引用，未找到当前显示调用', cellCount: 4 });

family('generic-facilities', '通用设施场景及市场', '保留', i => i.group === 'PortFacilities' || i.group === 'PortFacilities/Market',
  ['仓库、船坞、配件店、卡组工坊、调度室、藏宝室、投资场景、出航场景与市场的木料、金属、工具和灯光已有较高精细度。', '船坞水面与空白操作区是船只和UI的承载区；纸板、桌面留白具有功能，不应以填满细节为目标。'],
  '保留，后续检查统一材质颜色和轮廓颗粒即可。通用酒馆/配件店作为区域图缺失时的备用背景，不因备用身份单独重画。',
  [evidence('PortFacilityArtCatalog.cs', 61, 'BackgroundResourcePaths使用当前通用设施和Market/market_scene_background_v2；投资与寻宝已改用整合场景。'), evidence('CoreGameplayPortController.cs', 279, '先加载区域背景；缺失时按设施回退到通用背景，修理另有MaintenanceV2回退。')],
  { status: '当前主场景或区域背景回退', scope: '按当前inventory统计通用设施与市场，非人物/船只/控件图集' });

family('regional-taverns', '六港酒馆整合人物背景', 'P2', inGroup('PortFacilities/Regional/Tavern'),
  ['原图可见面部、衣褶、杯具、木材与暖光细节，并非低分辨率粗糙资源。', '整合人物的柔和油画塑形与新船较清晰的像素轮廓不同；这是绘制语言差异，不能靠简单锐化或扩图解决。'],
  '先用灰鸥酒馆做风格样板，统一人物边缘、木材颗粒和光源色。保留六港人物身份、座位、手部动作与已有点击热区；样板确认后再决定六张是否整批重画。',
  [evidence('PortFacilityArtCatalog.cs', 238, '动态路径Regional/Tavern/tavern_{portSlug}_v1。'), evidence('CoreGameplayPortController.cs', 373, '区域背景存在时使用整合酒馆人物画面。')],
  { priorityReason: '统一风格候选；精细程度已足', representatives: ['Resources/PortFacilities/Regional/Tavern/tavern_gray_gull_v1.png', 'Resources/PortFacilities/Regional/Tavern/tavern_twilight_tide_v1.png'] });

family('regional-component-shops', '六港配件店背景', '保留', inGroup('PortFacilities/Regional/ComponentShop'),
  ['工作台、木柜、工具陈列与铜件有丰富层次，六港冷暖和饰物变化清楚，空台面便于叠加角色/配件。'],
  '保留背景；人物、铭牌和配件图的风格统一由各自家族处理，不需要因它们的不一致连带重画背景。',
  [evidence('PortFacilityArtCatalog.cs', 240, '动态路径Regional/ComponentShop/component_shop_{portSlug}_v1。'), evidence('CoreGameplayPortController.cs', 279, '当前设施选取当前港口的区域图。')]);

family('regional-maintenance', '六港修理场侧视背景', '保留', inGroup('PortFacilities/Regional/Maintenance'),
  ['木质坡道、吊具、栏杆、水面和远方港口细节充分；中间修理船停靠区的概括是为了前景主体读图。'],
  '保留侧视构图；以后只需跟随船只材质统一色调和接触阴影。',
  [evidence('PortFacilityArtCatalog.cs', 242, '动态路径Regional/Maintenance/maintenance_{portSlug}_side_v2，覆盖六港。'), evidence('CoreGameplayPortController.cs', 279, '区域修理图优先于MaintenanceV2和旧通用回退。')]);

family('main-menu', '主菜单背景', '保留', inGroup('MainMenu'),
  ['灯塔、沿岸城镇、木栈桥与波光构成完整层次，细节与港口背景一致；远景天空的低信息量适合承载菜单。'],
  '保留；后续若主题船型或主角身份调整，仅局部更新叙事元素。',
  [evidence('MainMenuController.cs', 252, 'Resources.Load MainMenu/main_menu_background并显示于菜单。')]);

family('naval-environments', '现行六类海战背景', '保留', inGroup('BattleEnvironments'),
  ['海面、浪花、云层、岸礁与海峡具备足够细节。远处的雾、风暴和地平线使用柔化/低对比，属于空间层次，不是旧图粗糙。'],
  '保留六种环境；前景船只比海面更清晰是合理焦点分配，不要把远景补成近景材质密度。',
  [evidence('BattleEnvironmentArtCatalog.cs', 32, 'ForTerrain/KeyFor把当前地形映射到六张单独海战图。'), evidence('CoreGameplayDemoController.cs', 1281, '非登船战使用ForTerrain返回的海战背景。'), evidence('CoreGameplayEncounterPresentation.cs', 37, '教学事件背景也使用当前地形海景。')]);

family('boarding-environments', '登船战背景', '保留', inGroup('BoardingBattle/Backgrounds'),
  ['18张由六地形×三时段形成，甲板木纹、绑索、舷栏、桅杆和远景已充分绘制。甲板中央平整空间供战斗角色和动作呈现。'],
  '保留；未来人物换风格时先在白天/夜间两样本检查明度关系，不额外增加甲板杂物。',
  [evidence('BoardingBattleArtCatalog.cs', 36, '动态boarding_{environment}_{time}_v1路径，六环境×三时段。'), evidence('CoreGameplayDemoController.cs', 1274, '登船战根据地形和时间选ForEnvironment。')],
  { scope: '18张背景，不含19张登船角色', variants: '6 environments x 3 times' });

family('lookout-scenery', '瞭望海景与非船远景', '保留', i => (i.group === 'NavigationLookout' && i.resource !== 'NavigationLookout/lookout_pirate_v1') || i.group === 'NavigationLookout/Environments',
  ['九张天气/时段全景具有清楚云海层次；岛屿、礁石、雾与风暴剪影有足够海岸/浪/云细节。', '雾和风暴边缘的柔化、远处主体的低对比是远景叠加需要，不应按近景船体锐度判为缺陷。'],
  '保留；统一天气色和距离衰减即可。已重画pirate不在本次环境审计内。',
  [evidence('NavigationLookoutArtCatalog.cs', 33, '天气×时间动态路径，晴天白昼复用lookout_sea_v1，另8张Environments图。'), evidence('CoreGameplayNavigationPresentation.cs', 180, 'Lookout全景按环境与时间混合。'), evidence('CoreGameplayNavigationPresentation.cs', 205, '非船观察物通过lookout_{key}_v1动态加载；已在全景表现的雾/风暴避免重复。')],
  { scope: '9张全景 + 4张非船观察物，排除已重画pirate' });

family('voyage-map-v2', '当前航海地图地形与水层', '保留', inGroup('VoyageMapV2'),
  ['岛屿、暗礁、浅滩、海峡、海草、雾和风暴的俯视符号清楚；水层与地面颗粒具备足够细节。', '地图需读出可航行区域和事件位置，其俯视抽象与船坞近景的精细度不能直接同比。'],
  '保留现有地图语言；如统一颗粒，先调整ground_detail与水纹的材质笔触，保持地形边缘和路径识别。',
  [evidence('VoyageMapArtCatalog.cs', 17, '三张水纹 + 七类地形 + ChartBase/GroundDetail，动态Resources路径。'), evidence('CoreGameplayVoyageMapPresenter.cs', 433, '地图显示使用TerrainFor与程序化海岸/地形层。')],
  { scope: '12张 = 3水纹 + 7地形 + 1水底图 + 1ground_detail' });

family('event-illustration-atlases', '旧普通/教学/寻宝事件插画', 'P1', i => /^(LegacyDemo\/(shattered_sail|golden_tide|moon_tide)_event_sheet_v2|TreasureHunt\/treasure_hunt_event_sheet)$/.test(i.resource),
  ['四张1536×1024的5×4图集共80格；单格含边框约307.2×256像素。原图可见粗块轮廓、人物脸部简化、木材/浪/遗迹细节混成色块。', '各格烘焙黑金外框，实际视图会裁除约5%横边、6.5%竖边，可用主体信息比标称单格更少。', '教学事件使用506×506插图，会明显放大；普通及寻宝嵌入事件使用238×280，虽然较小，绘制密度和轮廓仍落后于新船与周边高细节背景。'],
  '下一轮优先重画。按事件主题逐格提升木纹、绳索、人物/遗迹与水层，统一清晰轮廓和材质尺度；优先教学实际可见格，再覆盖普通三航线与寻宝。保持5×4映射/事件身份，或同步制定新插画目录和裁切契约；避免仅扩图锐化。',
  [evidence('LegacyDemoArtCatalog.cs', 65, '三路线事件表动态加载；EncounterUv使用5×4单格，未知事件按哈希选格。'), evidence('CoreGameplayDemoController.cs', 491, '教学使用EventSheet(0)。'), evidence('CoreGameplayDemoController.cs', 1244, '普通按SelectedRouteIndex选EventSheet，寻宝使用treasure_hunt_event_sheet。'), evidence('CoreGameplayEncounterPresentation.cs', 78, '教学事件RawImage为506×506，先裁烘焙金框。'), evidence('CoreGameplayEmbeddedEncounter.cs', 30, '普通/寻宝事件RawImage为238×280，采用同样相对边距与CoverUv。'), evidence('CoreGameplayTreasureVoyagePresentation.cs', 100, '寻宝20个事件格固定映射，部分传奇遭遇复用既有格。')],
  { atlasCount: 4, cellCount: 80, cellsPerAtlas: 20, grid: '5x4', cellNominalPixels: [307.2, 256], displaySizes: { tutorial: [506, 506], regularAndTreasure: [238, 280] }, subjectCountNote: '80为纹理格数，非80个唯一事件；代码有复用和哈希回退。' });

family('legacy-route-maps', '旧航线插画地图（仍使用）', '保留', i => /^LegacyDemo\/.+_route_map_v2$/.test(i.resource),
  ['海岸、岛屿、航线与港口符号较完整；俯视插画的尺度较概括，但与粗块事件图集的精度问题不同。'],
  '保留当前教学地图和出航缩略图；以后统一地图时优先语义符号，不为补密度改动地理布局。',
  [evidence('LegacyDemoArtCatalog.cs', 63, '三张RouteMapNames。'), evidence('CoreGameplayDemoController.cs', 343, '教学以1148×646使用RouteMap(0)。'), evidence('CoreGameplayDemoController.cs', 780, '普通出航选择以260×120使用RouteMap(index)。'), evidence('CoreGameplayPortController.cs', 921, '调度图缺失时可回退到RouteMap。')],
  { status: '当前教学/出航图及调度回退；普通航行主地图已采用VoyageMapV2' });

family('treasure-charts', '藏宝海图三品阶', '保留', i => /^TreasureHunt\/treasure_chart_/.test(i.resource),
  ['三种隐藏海域的岛屿、遗迹、潮汐和路线图面细节充分，具有可区分的光色与危险意象。'],
  '保留；后续如新事件重画，同步核对海图中的遗迹和事件主题连续性。',
  [evidence('CoreGameplayTreasurePresentation.cs', 16, 'TreasureChartArt按rankId动态加载三品阶海图。'), evidence('CoreGameplayTreasureVoyagePresentation.cs', 55, '出航详情在580×326内按比例显示海图。')]);

family('story-and-battle-comics', '开场/撞击/寻宝漫画', 'P2', i => ['OpeningComics', 'BattleComics/HD', 'TreasureHunt/Comics'].includes(i.group),
  ['26张3344×1882页的海浪、船木、人物、绳索、运动和光影细节均较丰富，精度足够。', '页内主体主要使用电影式油画塑形，人物软笔触和透视船型与新船坞的清晰像素轮廓存在风格差；漫画之间则较一致。'],
  '先做一张撞击页和一张开场/寻宝页样板，统一人物身份、船型细节、轮廓颗粒与用光。允许漫画保留动态透视和叙事层次，不能机械套用船坞静止视角；样板确认后决定26页批量范围。',
  [evidence('OpeningComicCatalog.cs', 30, '四页开场序列；CoreGameplayOpeningComicPresentation按ImageResource加载。'), evidence('RamComicCatalog.cs', 25, '四类撞击序列×四页，以ImageResource动态生成路径。'), evidence('CoreGameplayRamComicPresentation.cs', 29, '撞击实际加载HD图并进入漫画覆盖层。'), evidence('CoreGameplayTreasureStoryPresentation.cs', 64, '寻宝章节按ImageResources加载六页，weathered/gilded/moonlit各两页。')],
  { priorityReason: '风格与角色/船型连续性候选，非低精细度', subdivisions: { opening: 4, battleRam: 16, treasure: 6 }, representatives: ['Resources/OpeningComics/opening_page01.png', 'Resources/BattleComics/HD/ram_player_head_on_page01.png', 'Resources/TreasureHunt/Comics/weathered_page01.png'] });

family('settlement-reveal', '结算揭示场景与道具', '保留', inGroup('SettlementReveal'),
  ['船舱木结构、宝箱铜边、日记纸皮和烛灯细节充足，景物与暖色港口设施材质接近。'],
  '保留三个资源；以后如纸面/UI统一，只同步纸色和金属高光即可。',
  [evidence('CoreGameplaySettlementPresentation.cs', 32, '显示settlement_hold；57和64行加载chest和journal道具。')]);

family('maintenance-active', '修理备用船屋/补给板/水前景', '保留', i => i.group === 'MaintenanceV2' && !i.resource.includes('inspection_board'),
  ['船屋、补给纸板与波浪前景的木铜/水纹细节足够；纸面大面积空白服务文字布局。'],
  '保留。区域侧视图优先，备用船屋不用另做同精度重画；前景水面只检查与船体接触位置。',
  [evidence('CoreGameplayPortController.cs', 287, '区域修理图缺失时使用maintenanceArt.Background。'), evidence('CoreGameplayPortController.cs', 1548, 'WaterForeground是修理场水前景。'), evidence('CoreGameplayPortController.cs', 1639, 'SupplyBoard是修理补给板。')],
  { status: '2当前道具 + 1修理场备用背景' });

family('maintenance-inspection-legacy', '旧独立修理检查板', 'P3', i => i.resource === 'MaintenanceV2/maintenance_inspection_board_v2',
  ['木铜检查板的装饰细节完整，空中心是内容区，并非缺细节。'],
  '未找到当前视图显示前不排重画。保留兼容资源与目录可用性契约。',
  [evidence('MaintenanceArtCatalog.cs', 28, 'InspectionBoard getter及IsAvailable检查存在；Runtime仅见目录引用，实际修理界面使用其他检查布局与SupplyBoard。')],
  { status: '目录可用性引用，未找到当前显示调用' });

family('card-workshop-chests', '四种卡组箱近景道具', 'P2', i => /^CardWorkshopV2\/deck_chest_/.test(i.resource),
  ['单箱约1K以上，木纹、锁扣、金属饰物足够；粗黑外轮廓、夸张立体塑形与新船的小粒度描边不同，属于风格而非图像尺寸不足。'],
  '先用标准箱对齐新船的木材/铜件、轮廓厚度和高光。保持四个箱的功能符号与落地接触点，样板通过再统一其余三箱。',
  [evidence('CardWorkshopArtCatalog.cs', 15, 'ChestPaths固定四种卡组箱。'), evidence('CoreGameplayPortController.cs', 822, '四箱由CardWorkshopChestPlacement布局，按实际绘制边界和接触点摆放。')],
  { priorityReason: '近景道具描边与材质语言统一候选，精细度已足' });

family('card-workshop-paper-frame', '工坊羊皮纸与海盗装饰框', '保留', i => i.group === 'CardWorkshopV2' && !i.resource.includes('/deck_chest_'),
  ['纸纹、木铜绳饰和框角细节充分；纸中心留白是卡片/事件文字安全区，不能按低内容密度判断粗糙。'],
  '保留三张；涉及多界面共享，后续UI整体统一时再调整纸色与框线。',
  [evidence('CardWorkshopArtCatalog.cs', 30, 'CollectionParchment、DeckBuilderCollectionParchment与PirateFrame目录。'), evidence('CoreGameplayEmbeddedEncounter.cs', 23, '事件嵌入也使用deck_builder_parchment_v2，属于共享表面。')]);

// A03/A04 retired the unused route map and expanded cabinet. The warehouse
// compatibility node now has no texture; regional backgrounds own the cabinet.
family('warehouse-old-independent-art', '旧仓库柜图与独立藏品原图', 'P3', inGroup('WarehouseV2'),
  ['旧柜图与指南针、铃、封蜡海图本身已有丰富木铜/纸面细节，不是需要重画的低精度族。'],
  '四张不排当前重画。独立藏品路径保留在Core定义/测试契约中，但实际展柜和列表使用Keepsake图集；只在确认要恢复独立图显示时复核。',
  [evidence('CoreGameplayWarehouseView.cs', 144, '展柜使用ItemIconCatalog.ForTreasure，不加载definition.ArtResourcePath；列表同样使用ForTreasure。'), evidence('ItemIconCatalog.cs', 117, 'ForTreasure按ID取Keepsake A/B图集，不读取独立WarehouseV2路径。'), { file: base + 'Core/Runtime/CoreWarehouseCollection.cs', line: 46, detail: '三个独立原图路径保留于定义，另有测试断言；未找到当前渲染消费者。' }],
  { status: '1旧柜图无当前调用 + 3独立藏品定义/测试引用但当前显示由图集替代', fileStatuses: { 'WarehouseV2/trophy_cabinet_v1': '无当前运行加载调用', 'WarehouseV2/first_victory_compass_v1': '定义/测试保留，当前显示Keepsake图集', 'WarehouseV2/warehouse_boatswain_bell_v1': '定义/测试保留，当前显示Keepsake图集', 'WarehouseV2/warehouse_sealed_chart_v1': '定义/测试保留，当前显示Keepsake图集' } });

family('port-icons', '六港地图纹章', '保留', inGroup('PortIcons'),
  ['锚、鸥、贝、钟、王冠和暮潮纹章铜金层次、雕刻和边缘细节充分，小地图标记缩小后仍易辨识。'],
  '保留；优先确保小尺寸形状与明度可读，不再增加纹章内部细线。',
  [evidence('PortMapIconCatalog.cs', 10, '六港图标字典，Get动态加载PortIcons资源。'), evidence('CoreGameplayDemoController.cs', 1158, '地图港口节点使用portMapIcons.Get。')]);

family('port-headers', '六港抬头装饰横框', '保留', inGroup('PortHeaders'),
  ['木铜边、绳结、贝壳和港口符号细节充分。部分源图有大幅空白边，目录已有专用裁切与九宫格，源图空白不等于界面资源低精度。'],
  '保留六种港口主题；后续UI统一时只同步铜木色和轮廓颗粒，保持现行裁切和九宫格边界。',
  [evidence('PortHeaderArtCatalog.cs', 23, '各港按源图留白定义准确crop，创建Sprite及九宫格边框。'), evidence('CoreGameplayPortHeaderController.cs', 56, '出航抬头按港口使用ForPort；128行港口主页也使用ForPort。')]);

const seen = new Map();
for (const g of groups) for (const f of g.files) {
  if (seen.has(f)) throw new Error('Double audit: ' + f + ' / ' + seen.get(f));
  seen.set(f, g.id);
}
const archived = inventory.archived.filter(p => !p.startsWith('Editor/ArchivedArt/ComponentArt/'));
const priorityFiles = {};
for (const p of ['P1', 'P2', 'P3', '保留']) priorityFiles[p] = groups.filter(g => g.priority === p).reduce((n, g) => n + g.count, 0);
const report = {
  date: '2026-10-12',
  reviewer: 'ship_validation / environments',
  basis: '初始船与本轮新船的清晰轮廓、木铜材质细节；大背景/远景按实际用途允许景深概括。',
  method: '只读源图与统一contact sheets目视审阅，结合当前Runtime的静态调用及动态目录路径；未重画、未修改生产资源、未跑Unity测试或出包。',
  pathBase: project,
  inventory: path.relative(project, path.join(outDir, 'source-inventory.json')).replace(/\\/g, '/'),
  priorityDefinitions: {
    P1: '当前明显低精细度，建议优先重画',
    P2: '精细度已足，风格/角色/船型统一样板候选；样板确认后再决定批量重画',
    P3: '兼容或未找到当前显示路径，先明确使用再投入，不计入当前重画排期',
    '保留': '当前精细度充分或概括符合场景用途，无需为补精度重画'
  },
  coverage: {
    currentResourcesFiles: seen.size,
    familyGroups: groups.length,
    filesByPriority: priorityFiles,
    eventAtlasCells: 80,
    contactSheets: [...new Set(groups.flatMap(g => g.visualEvidence.contacts))].length,
    exclusions: ['已重画船只与NavigationLookout/lookout_pirate_v1', '角色、卡片、配件、组件UI及一般UI素材由其它审计处理', 'ComponentShopRegional/furniture的6张图集由UI审计处理；本报告仅含PortFacilities/Regional/ComponentShop背景'],
    fullResolutionSupplement: [
      base + 'Resources/LegacyDemo/shattered_sail_event_sheet_v2.png',
      base + 'Resources/LegacyDemo/moon_tide_event_sheet_v2.png',
      base + 'Resources/TreasureHunt/treasure_hunt_event_sheet.png',
      base + 'Resources/PortFacilities/Regional/Tavern/tavern_gray_gull_v1.png'
    ],
    caveat: '无静态提及不能证明unused；状态已结合动态拼接、调用路径和已用替代资源。报告中的无当前显示路径仅限本轮源码审计，保留兼容定义与资源。'
  },
  summary: [
    '优先4张事件图集80格；教学大图的粗块轮廓最明显，普通/寻宝嵌入图为238×280，不能统称500像素放大。',
    '26页漫画、6张酒馆整合人物背景、4个卡组箱共36张可先做P2风格样板，精度本身充分。',
    '港口、设施、战场、登船、远景、地图及大多数道具保留；远景雾化与纸面留白是用途要求。',
    `${priorityFiles.P3}张Resources旧图/目录验证资源单列P3，不排重画；${archived.length}张Editor归档另列，不混入当前资源数。`
  ],
  families: groups,
  archived: {
    status: 'Editor/ArchivedArt中的历史背景/道具，非当前Resources目录，不排重画',
    count: archived.length,
    files: archived.map(p => base + p),
    evidence: '按当前inventory归档清单统计；已获批清理的B01文件不再计入。ComponentArt归档由组件审计负责。'
  }
};
const knownGroups = new Set(['PortFacilities', 'PortFacilities/Market', 'PortFacilities/Regional/Tavern', 'PortFacilities/Regional/ComponentShop', 'PortFacilities/Regional/Maintenance', 'MainMenu', 'BattleEnvironments', 'BoardingBattle/Backgrounds', 'NavigationLookout', 'NavigationLookout/Environments', 'VoyageMapV2', 'OpeningComics', 'BattleComics/HD', 'TreasureHunt/Comics', 'SettlementReveal', 'MaintenanceV2', 'CardWorkshopV2', 'WarehouseV2', 'PortIcons', 'PortHeaders']);
const scopedImages = inventory.images.filter(i => (knownGroups.has(i.group) && i.resource !== 'NavigationLookout/lookout_pirate_v1') || /^PortArt\/port_hub_.+_v3$/.test(i.resource) || i.resource === 'PortArt/battle_backdrop_sheet' || /^LegacyDemo\/.+_(event_sheet|route_map)_v2$/.test(i.resource) || /^TreasureHunt\/(treasure_chart_|treasure_hunt_event_sheet$)/.test(i.resource));
const uncovered = scopedImages.filter(i => !seen.has(base + i.relative));
if (uncovered.length || scopedImages.length !== seen.size) throw new Error('Environment coverage mismatch: ' + uncovered.map(i => i.resource).join(', '));
fs.writeFileSync(path.join(outDir, 'environments-audit.json'), JSON.stringify(report, null, 2) + '\n', 'utf8');
console.log(JSON.stringify({ output: path.join(outDir, 'environments-audit.json'), coverage: report.coverage, archived: archived.length }, null, 2));
