# UI 场景预览

共 18 个独立预览场景，覆盖港口经营、航海探索与海战等功能页。双击场景后即可在 Game 或 Scene 视图检查，不需要点击 Play。

## 打开与刷新

- 目录：`Assets/_Project/Scenes/UIPreviews`。
- 快速定位：`Shattered Sail > Scenes > Open UI Preview Folder`。
- 刷新当前已打开的预览：`Shattered Sail > Scenes > Refresh Open UI Previews`。不会切换场景、保存或重建场景文件。
- 也可选中 Hierarchy 中的 `UI Preview · …`，在控制器组件的右键菜单选择“刷新当前 UI 预览”。
- 需要重新生成整套场景文件时，使用 `Shattered Sail > Scenes > Create or Refresh All UI Preview Scenes`。此操作会重建下表中的场景，请先保存或备份自己的场景修改。

## 检查目录

| 场景 | 内容 | 默认港口 |
| --- | --- | --- |
| UI_PortHub | 港口总览与四项特色顶栏 | 断锚港 |
| UI_Warehouse | 六格珍藏柜、贵重品／基础物品页签、首胜罗盘陈列样本 | 断锚港 |
| UI_Shipyard | 水上船坞 · 左右逐艘浏览 | 铜钟港 |
| UI_ShipyardDetail | 三页船只档案 · 性能／背景／配件装配，购买／旗舰切换 | 铜钟港 |
| UI_Tavern | 酒馆招募 | 灰鸥港 |
| UI_TavernCrewDetail | 三页船员档案（背景／异闻／技能） | 灰鸥港 |
| UI_ComponentShop | 部件陈列 | 铜钟港 |
| UI_ComponentDetail | 部件详情 | 铜钟港 |
| UI_CardWorkshop | 四种地面卡箱 | 暮潮港 |
| UI_DeckBuilder | 卡组构筑 | 暮潮港 |
| UI_TreasureMap | 藏宝图桌 | 暮潮港 |
| UI_Maintenance | 右舷侧视船坞、左右木板与采购 | 盐冠港 |
| UI_VoyageDeparture | 出航准备 | 断锚港 |
| UI_Market | 港口市场 | 白贝港 |
| UI_Navigation | 航海探索 | 白贝港 |
| UI_Encounter | 航海事件 | 铜钟港 |
| UI_Battle | 海战 | 暮潮港 |
| UI_Settlement | 航行结算 | 灰鸥港 |

## 港口、海域与编辑方式

选择 `UI Preview · …` 对象，通过 Inspector 中的 `Editor Preview Port` 切换六个港口。`Editor Preview Route Index` 控制海域。

船只档案新增 `Editor Preview Ship Page`：0 性能、1 背景、2 配件。配件页展示本船五槽和独立演示库存，编辑模式不执行装配或写入玩家存档。游戏中可卸下、替换，每艘船独立保存。三页实际界面与首胜装配引导见 `Artifacts/ShipyardEquipment20260916/UI/index.html`。

船员档案新增 `Editor Preview Crew Index`（0–5，选择角色）及 `Editor Preview Crew Page`（0 人物背景、1 航海异闻、2 职业技能）。这些参数在编辑模式中自动重绘，无需进入 Play；运行游戏时也可用页签或上一页／下一页浏览。刷新当前预览会保留 Inspector 中选择的角色与页码。

预览与游戏共用布局和美术代码。场景文件保存入口及默认配置，打开时生成最新 UI；自动生成的 Canvas 不会写入场景，直接拖动它的子对象不会永久保存。长期布局调整应修改对应的共用布局代码，再刷新预览。

预览使用演示数据，编辑模式下不执行采购或游戏存档操作。这些场景不加入正式游戏构建。

## 截图验收

### 卡组构筑页签收边 · 2026-09-16

`UI_DeckBuilder` 的三个阶段页签与“卡牌强化”改为同一行等宽木质按钮，整体锚定在左侧羊皮纸内，避开纸张破损边缘。强化入口不再伸出纸面；卡牌网格、右侧牌组列表和原有强化逻辑保持不变。预览与实际游戏共用修正后的布局，重新打开场景或刷新当前 UI 预览即可检查。

### 酒馆角色清理 · 2026-09-16

已移除未配立绘的“女巫·烬潮”独立招聘木牌及其相关玩法。六港酒馆只保留正式名册中的六名入座角色；刷新现有预览即可看到变化。六港实际截帧见 `Artifacts/TavernRosterCleanup20260916/UI/index.html`，旧档兼容与验证范围见 `Documentation/TavernRosterCleanup.md`。

### 仓库珍藏室 · 2026-09-09

`UI_Warehouse` 使用新增橡木黄铜柜与透明罗盘素材，右侧为羊皮纸物品账簿。点击贵重品、选择展位、陈列或收回，基础物品页保留出售与备用金存回、后勤入口。预览初始化展示首胜罗盘样本，不代表真实拥有；预览操作不写玩家存档。新手首领胜利后正常结算入库，并引导实际陈列后才开放自由探索。

检查：`Artifacts/Warehouse/UI-verified/index.html`。新资源与提示词：`Documentation/WarehouseArtPrompts.md`；规则：`Documentation/Warehouse.md`。

### 全界面材质统一 · 2026-09-08

当前最新入口为 `Artifacts/UI-Materials/index.html`。`Verified/UIPreviews` 导出全部 21 个实际预览场景，`Verified/Dialogs` 补充主菜单、设置、成就、确认窗口、港口设置与教程共 6 张。使用新增帆布与羊皮纸纹理、共用木质操作按钮，替换原来的纯色底板；保留各港口专属边框、分类卡框、场景插画与透明热区。旧预览刷新后即可显示，不需要重新保存自动生成的 Canvas。素材、完整生成提示词与验收说明见 `Artifacts/UI-Materials/ASSET-NOTES.md`。

### 结算演出更新 · 2026-09-08

`UI_Settlement` 已改为全屏船舱战报：大标题、宝箱／航海日志、主收益大字及三格资源、保护率印记与航次记录。运行时约 2.8 秒分阶段揭晓，支持跳过和直接确认返回港口；失败与零收益不会出现庆祝宝箱。原先顶部原型蓝条在结算页隐藏。

预览默认使用明显标注的正收益演示结果，便于检查视觉效果，绝不写入游戏会话或存档；真实游戏仍使用实际航次收益。专项图集覆盖抵港、主动返航、失败、零收益、材料优先、大数值与演出阶段。素材及完整生成提示词见 `Artifacts/Settlement-Reveal/ASSET-NOTES.md`，最终图集入口在该目录的 `index.html`。

### 航海四区更新 · 2026-09-08

`UI_Navigation` 现为左上海图、左下航向／距离、右上动态前方视野、右下滚动航海日志。`UI_Encounter` 在同一布局的右上方展示羊皮纸事件，不再遮盖全页；`UI_Battle` 为完整独立海战画面，不创建航海四区。教程仍保留整页事件教学。

本轮检查入口：`Artifacts/Navigation-Lookout/index.html`。整套预览图集：`Artifacts/Navigation-Lookout/UIPreviews-final/index.html`。事件多选项与三海域边框图集：`Artifacts/Navigation-Lookout/Events-final/index.html`。距离、航向、天气遮挡对照：`Artifacts/Navigation-Lookout/Distance-Bearing-final/`（明确标注的临时验收数据，不是玩家存档）。新素材与完整生成提示词：`Artifacts/Navigation-Lookout/ASSET-NOTES.md`。

前方视野由地图地形、事件位置、当前朝向与船员／部件视距共同计算；隐藏或已经解决的事件不会泄露。新的 `Resources/NavigationLookout/` 海面与透明景物分层组合，所有景物共用水线深度。刷新现有预览即可检查，不必重建或保存自动生成的 Canvas。

### 既有场景记录

船坞新增 `Editor Preview Ship Index`（0–3）预览四种船型。`UI_Shipyard` 展示水上泊位，`UI_ShipyardDetail` 展示羊皮纸档案；刷新会保留所选船型。游戏中的左右按钮仅浏览，悬停船身高亮，点击只打开档案，档案中确认后才购买或设为旗舰。四种船型均为独立图层，不再借用背景中烘焙的船只；水影在后、船体居中、近侧细浪在前，不放置木质托架。背景由内置 imagegen 重绘，原背景保留，提示词见 `Artifacts/ShipyardGallery-source/PROMPT.md`。

船坞专项图集：`Artifacts/ShipyardGallery-final/index.html`，包含四艘船的普通、悬停与羊皮纸档案，共 12 张。测试结果与验收说明位于同目录。

`ShatteredSail.Editor.UIPreviewSceneAcceptance.CaptureFromCommandLine` 会逐个打开实际保存的场景，输出 1920×1080 PNG、`index.html` 图集及 `report.json`。命令行参数 `-uiPreviewCapturePath` 指定全新或空的输出目录；运行时必须启用图形，不能使用 `-nographics`。

报告检查功能页覆盖、场景是否完整生成、Unity 错误以及源场景文件是否保持不变，适合后续集中检查。

最新的整套预览图集位于项目根目录的 `Artifacts/UIPreviews-ShipyardGallery-review/index.html`，共 21 页，新增水上船坞与船只档案，包含六港后勤侧向船坞、部件商店中央地面陈列／船匠工作台接地，以及此前的角色抠图白边精修、港口总览四项顶栏、三页船员档案及顶栏贴边／航海事件优化。后勤六港对照图集位于 `Artifacts/MaintenanceSide-review/index.html`。商店专项图集位于 `Artifacts/ShopFloor-v1-review/index.html`，覆盖六个港口、五类部件和一至五件库存的分组陈列；其中标注“固定验收样例”的页面不是实际月度库存。六港口总览与设施顶栏的对照图集位于 `Artifacts/PortHubHeaders-verified/index.html`。船员档案专项图集位于 `Artifacts/CrewDossier-Edges-v3-verified/index.html`：保存全部 36 位角色的背景页及七位代表角色的其余页面，共 50 张，同时检查完整 108 页。验收说明见同目录 `ACCEPTANCE.md`。三种海域、完整四项操作和整合教学事件的专项图集仍位于 `Artifacts/EncounterUI-verified/index.html`。

港口投资建设采用六张独立重绘的 `Resources/PortFacilities/Regional/Maintenance/maintenance_<port>_side_v2.png`：从右侧码头看横向停泊的船，船艉在左，船艏向右侧出海口。水道、后墙和码头同步改为侧向透视，保留各港建筑身份而非换色。船体展示放大到原基础布局的 1.10 倍，保留模块化装备比例、透明船体与水线裁切；左右检查板与采购板按近侧码头地面重新测定支脚和阴影落点。六项检查、六种物资的纸内滚动采购及数量、费用逻辑保持不变。新背景由内置 imagegen 生成，完整提示词见 `Artifacts/MaintenanceSide-source/PROMPTS.md`；旧背景保留。

部件商店将部件、支架、接触阴影与木牌作为整体布置在中央地板，左侧前景货架保持露出，右侧为船匠工作台。每组最多展示三件，更多库存通过“上一组／下一组”浏览，月度库存、价格、已售状态与详情／采购逻辑不变。船匠与工作台一同下移到前景地面，保留桶、木箱、靴子与桌脚各自的接地点；说明文字附在柜台正面，不再悬挂在脚下。

档案姓名 36 号、章节 28 号、正文 26 号，禁用自动缩字，完整保留已有背景与技能。角色专用渲染蒙版去除与画面边缘连通的黄纸，底部使用窄范围柔化；原六张角色图集、面容和服装像素保持不变，不影响其他页面头像。蒙版和材质仅在内存中生成，随界面释放，不写回场景或 PNG。

白边精修加入透明度参与的边缘插值，避免缩放时将已移除的浅色纸底重新混入轮廓，并限制采样在当前角色图块内。只在外沿两像素范围对比邻近底色与角色内部颜色，保护白发和浅色衣物；对灰鸥港发卷、暮潮港道具间隙等已确认的空白补充局部蒙版标记。没有重画面孔、修改原始图集或全局删除浅色像素。

港口功能顶栏现与场景左右边缘等宽、顶部齐平，消除原来左右各 16 像素、顶部 9 像素的深蓝露底。场景背景的尺寸、UV 与道具接地点未改变；六港口保留各自的九切边框素材及原有导航顺序。

港口总览也复用当前港口的同一套特色边框，从左到右仅保留“时间、港口名称、总金币、设置”。原蓝色原型标题栏及“今日港务”横幅不再显示，操作提示保留在底部。总览与设施页共用实时读数和设置弹窗；进入设施时恢复八项导航，返回总览后恢复四项顶栏，不改变港口背景或八处设施的点击位置。

航海事件页改为左侧插画、右侧羊皮纸正文及木质选项，按海域使用不同边框，明确显示风险、耗时和不可选原因。正文、选项和页脚避开边框装饰；保留原有事件选择、略过与结算逻辑。已打开的场景请使用上方的刷新菜单，便可看到最新效果，无需重建场景文件。

上一轮物品摆放修正图集保留在 `Artifacts/UIPreviews-ScenePlacement-verified/index.html`，覆盖部件与支架的透视朝向及承点、木牌文字安全区、后勤采购账本的纸内滚动，以及船员档案按钮的纸内布局。六港口部件商店、后勤及五类部件的补充陈列检查仍位于 `Artifacts/ScenePlacement-v2-verified/index.html`。

2026-09-07 较早的图集 `Artifacts/UIPreviews-20260907/index.html` 作为历史记录保留，不代表当前布局。

### 出航切船与快捷配装 · 2026-09-16

`UI_VoyageDeparture` 的旗舰示意图两侧加入木质箭头，运行时仅切换已拥有且舱容足够的船只。点击船身进入同船档案的「配件装配」页，可切换性能与背景页；底部按钮直接返回出航码头，保留海图、种子与三阶段牌组。出航配置显示全部五类真实配件，卸下后显示空槽。

编辑预览固定只有一艘已拥有船，因此箭头禁用，但船身可以打开档案并返回；预览操作不会写入真实配件或船只存档。刷新既有预览场景即可检查，无需重建场景。专项运行时图集位于 `Artifacts/DepartureShipSelection20260916/Visuals-verified/`。

### 海战转向与出牌响应 · 2026-09-16

`UI_Battle` 共用的船图材质现会屏蔽图集黑色分格线，保留黑帆和船体暗部，兼容镜像及转向。运行时同阶段出牌不再重建整片战场与所有手牌，独立增益/抽牌提示并行播放；准备、炮击及接舷之间仍正常切换场景。动态截图见 `Artifacts/BattleResponsiveness20260916/Visuals-verified/index.html`，测试与改前/改后耗时见同目录上级的 `README.md`。
