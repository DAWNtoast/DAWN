**项目美术翻新检查 · 2026-10-22**

优先投入战术海战舰船与海面、旧船员头像统一，以及宝藏漫画画风细化。卡牌原画、部件、主菜单及多套港口场景已经完成较大幅度更新，不宜重新整批制作。另有遭遇插画与两张新卡图的接入待办，应先处理已有成果的落盘和绑定。

检查依据为当前工作区（含未提交内容），不是旧提交或 10 月 12 日历史报告。本次全量盘点 Resources 内 **33 个一级目录、964 张图片文件**；数量包含图集、遮罩及装配层，不代表 964 幅独立插画，也不表示每张都在当前流程使用。结合当前加载代码、重点原图、24 张审阅联系表及已有实机截图进行人工抽查。未启动 Unity 重新遍历所有场景，因此以下区分确定的资源/引用问题与视觉优化建议。

**建议排期**

| 顺序 | 范围 | 当前证据与影响 | 建议工作 |
|---|---|---|---|
| 先收尾 | 遭遇插画接入 | 当前映射预期 85 张独立图＋4 张合集，共 89 个路径；目录实际只有 provisions_spoilage_v1，缺少其余 88 个路径。加载器对已识别事件即使得到空纹理也返回成功，因此旧图回退不会补齐这一情况。 | 核对本轮已有生成输出，补齐落盘和资源绑定，再抽查事件。这里的 88 是资源路径缺口，不能直接当成需要重新生成的 88 张图。 |
| 高 | 战术海战俯视舰船 | 敌我统一加载 tactical_ship_v1，普通船与特殊敌舰在当前棋盘上缺少外形身份差异；阵营主要靠色圈区分。 | 优先补玩家基础舰型、普通敌船、Boss/传奇与学派特征变体，保持朝向和占格轮廓一致。现有正侧船图不必重画。 |
| 高 | 战术海面 | 当前棋盘固定 tactical_sea_v1；已有 10 月 21 日画面中的放大浪纹很抢眼，网格、状态和航线的阅读负担偏高。 | 重做适合棋盘阅读的海面纹理尺度和对比度，再增加风暴、雾海、峡湾等环境差异。已有六张侧视海战背景不能自动覆盖俯视棋盘。 |
| 高 | 出航船员旧头像 | 出航使用 CrewRegional 六套图集（36 个头像位），酒馆档案已用 36 张 CrewDossiers 新画像。抽查断锚领航员，脸、肤色和眼睛等身份特征明显不同。 | 以新档案为形象基准，裁切并替换旧头像，核对六港全部 36 位角色；这主要是复用、裁切和接入。 |
| 中 | 战斗资源/状态/意图图标与学派场地 | 现行图标和场地多为黑描边、高饱和色块、线条及几何符号，与写实船图、卡图和木框材质存在差异。它们目前可辨识，不属于缺图。 | 优先统一常驻资源与高频状态，再细化火场、水流、雷栅等材质；保留小尺寸轮廓、方向和格界识别。今天的图标 atlas 是现有程序图形导出，不是新绘制素材。 |
| 中 | 宝藏漫画 6 页 | weathered/gilded/moonlit 各两页，仍有明显硬色块、较重暗部和像素式轮廓，与新场景的细腻绘制存在差异。 | 若继续统一全项目手绘画风，保留分镜及对白留白，只细化人物、海浪和建筑边缘。文件虽为 3344×1882，尺寸本身不等于笔触已翻新；空对白框由程序上字，是正常设计。 |
| 中 | 飞剪商船档案 1 张 | 新 ship_clipper_dossier_v1 是三桅，现有船坞图集是四桅，代码也明确四桅设定。 | 局部统一为四桅形制；其他新船档案无需因此重画。 |
| 低／清晰度适配 | 部分活跃全屏背景 | 六港 voyage_*_crew_integrated_v1、投资建设、藏宝桌原生 1672×941；部件店大厅为 1672×940。1080p 满屏需约 1.15 倍放大，4K 需要更多像素。 | 保留现有构图，按目标显示分辨率逐场景验收后做高清细化。不仅凭尺寸判为画质不合格，也不要把 maxTextureSize=4096 当成原图 4K。 |

**关键定位与证据**

- 遭遇资源：[缺失路径清单](D:/Game/outputs/art-renovation-audit-20261022/encounter-resource-check.json)；[EncounterIllustrationCatalog.cs:46](D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/EncounterIllustrationCatalog.cs:46)、[LegacyDemoArtCatalog.cs:116](D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/LegacyDemoArtCatalog.cs:116)、[CoreGameplayEmbeddedEncounter.cs:29](D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/CoreGameplayEmbeddedEncounter.cs:29)。这是静态加载链核查，未在本次启动游戏复现。
- 战术船与海面：[NavalTacticalBoardView.cs:188](D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/NavalTacticalBoardView.cs:188)、[219 行](D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/NavalTacticalBoardView.cs:219)。[2026-10-21 实机留存截图](D:/Game/artifacts/BattleHudSizingRefined20261021/captures/NavalClean_1920x1080.png)可见船型共用、海面与图标风格，截图不是本次新拍。
- 出航头像：[CoreGameplayDepartureCrewController.cs:376](D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/CoreGameplayDepartureCrewController.cs:376)、[PortArtCatalog.cs:171](D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/PortArtCatalog.cs:171)。对比[旧断锚头像图集](D:/Game/UnityClient/Assets/_Project/Resources/PortArt/CrewRegional/crew_broken_anchor_portraits_v2.png)与[新领航员档案](D:/Game/UnityClient/Assets/_Project/Resources/PortArt/CrewDossiers/crew_broken_anchor_navigator_lorne_portrait_v2.png)。36 位是待统一核对范围，非本次逐一确认每位都存在相同程度差异。
- 战斗图标与场地：[BattleCommandIconGraphic.cs:83](D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/BattleCommandIconGraphic.cs:83)、[BattleStatusIconGraphic.cs:295](D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/BattleStatusIconGraphic.cs:295)、[NavalFieldGraphic.cs:52](D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/NavalFieldGraphic.cs:52)；[资源图标预览](D:/Game/outputs/battle-icon-atlas-20261022/preview-resource.png)、[场地预览](D:/Game/outputs/battle-icon-atlas-20261022/preview-field.png)。
- 宝藏漫画：[六页联系表](D:/Game/outputs/art-renovation-audit-20261022/contacts/TreasureHunt_1.jpg)、[CoreTreasureStoryCatalog.cs:58](D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/CoreTreasureStoryCatalog.cs:58)、[CoreGameplayTreasureStoryPresentation.cs:60](D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/CoreGameplayTreasureStoryPresentation.cs:60)。
- 飞剪商船：[现用图集四桅裁切预览](D:/Game/outputs/art-renovation-audit-20261022/clipper_runtime_alpha.png)、[新三桅档案](D:/Game/UnityClient/Assets/_Project/Resources/PortArt/ShipDossiers/ship_clipper_dossier_v1.png)、[PortArtCatalog.cs:23](D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/PortArtCatalog.cs:23)。
- 全屏背景：[PortFacilityArtCatalog.cs:68](D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/PortFacilityArtCatalog.cs:68)、[252 行](D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/PortFacilityArtCatalog.cs:252)、[CoreGameplayPortController.cs:297](D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/CoreGameplayPortController.cs:297)。原生尺寸见[本次库存](D:/Game/outputs/art-renovation-audit-20261022/inventory.json)。

**另一个可直接收尾的接入项**

整顿号令和战术筹划的两张新原画已经存在且主体清楚：[card_energy_order.png](D:/Game/UnityClient/Assets/_Project/Resources/CardArt/Western/card_energy_order.png)、[card_tactical_draw.png](D:/Game/UnityClient/Assets/_Project/Resources/CardArt/Western/card_tactical_draw.png)。但当前[CardSchoolArtCatalog.cs:170](D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/CardSchoolArtCatalog.cs:170)仍分别映射 card_brace 和 card_full_sail_turn，应调整绑定，不应重复生成。

**本轮不建议整批翻新的范围**

- Western 和六学派卡牌原画、近期卡框：已是独立精绘素材；卡图语义绑定可继续专项核对。
- ComponentArt 的 150 张物品及对应 ModularShip 装配层：跨类别抽查轮廓、材质和透明边缘良好。两套分辨率服务不同显示用途，不能因存在同主题资源认定重复或落后。
- 36 张新船员档案、12 张新船只档案（除上述飞剪船一致性修正），六港主城、酒馆、收藏室：当前已有成套更新。
- 主菜单、航海昼夜天气背景、连续海岸 V3、常规物品/纪念物图标与成就徽章：本次抽查未发现值得优先整批重画的依据。
- 开场漫画、冲撞漫画、接舷角色/甲板、结算宝箱：按本次抽查和现役引用核对，已有完整绘制，优先级低于上表。
- 已退役或未发现现役引用的旧框/旧场景不占用本轮翻新预算；没有恢复 UIPreviewsForMe。

**透明边缘复核**

原图预览中看到的部分彩色边缘在标准 Alpha 合成后不可见，因此未列为缺陷。已检查[船只图集](D:/Game/outputs/art-renovation-audit-20261022/ship_roster_sheet_alpha.jpg)、[旧目录船图集](D:/Game/outputs/art-renovation-audit-20261022/ship_sprite_sheet_alpha.jpg)、[血系 Boss](D:/Game/outputs/art-renovation-audit-20261022/boss_blood_alpha.jpg)与[岛屿前景](D:/Game/outputs/art-renovation-audit-20261022/lookout_island_v1_alpha.jpg)在深、浅底的合成结果。结论仅针对这些样本。

审计产物均位于 outputs/art-renovation-audit-20261022；原美术、游戏代码、场景及导入配置均未修改。没有生成新美术或调用外部图像 API。
