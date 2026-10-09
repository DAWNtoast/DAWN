# 海战界面：海面接触与卡牌材质

运行时 `CoreGameplayDemo` 和编辑预览 `Scenes/UIPreviews/UI_Battle.unity`
共用 `BuildBattleOverlay()`，不需要进入 Play Mode 才能查看静态效果。

## 船体与海面

- 六类局部海战背景保留原画；取画幅下方 86%，给海面留出更大比例。
- 两船处于海平线以下，收拢至画面左右的开阔航道，保留上方名称、意图与下方血条。
- 船底的水面阴影、前景碎浪与小幅浮动分层显示。水面不会跟随船只一起悬空起伏。
- 敌舰沿用 `PortArt/ship_roster_sheet.png`，只在海战渲染时使用
  `Resources/Shaders/BattleShipCutout.shader` 去掉已知的深蓝底色。
  不使用全局亮度阈值，所以黑帆、深色木材与幽灵船的荧光保留。
- 图集 UV 内缩 10 像素以避开黑色分割线及压缩块污染，再按船型画幅裁取、水平翻转，使敌舰朝向旗舰。
- 原图集未覆盖或改写。Imagegen 的两次透明图尝试因实际输出仍是 RGB 棋盘底而未采用。

## 卡牌与操作

| 类型 | 边饰材质 | 文字区 |
| --- | --- | --- |
| 炮击 | 铜锈金属、铆钉 | 米黄纸面 |
| 机动 | 青色帆布、缝线 | 青白纸面 |
| 登船 | 酒红皮革、针脚 | 暖粉纸面 |
| 维修 | 橄榄色木纹 | 淡绿纸面 |
| 防御 | 蓝灰钢板、接缝 | 灰白纸面 |

图标仍是原有方形卡图。新边饰与纸面由 `BattleCardSurfaceGraphic` 绘制，
保证不同分辨率下边框清晰；文字、消耗与悬停词条仍是可更新的 UI 内容。
卡牌悬停抬升但不挡船体；无法使用的牌降低饱和度，而不改回黑底。
结束回合按钮与右下「敌舰状态」等宽，放在该框正上方，不覆盖手牌。

## 验证

- `BattlePresentationTests`：五种卡牌样式、三种 Boss、悬停、真实出牌和结束回合后的布局。
- `BattleShipCutoutRenderTests`：真实 GPU 测试未压缩色条及实际导入／压缩的三种 Boss 图集，检查透明背景和深色前景。
- `BattleCardSurfaceGraphicTests` / `BattleShipCutoutTests`：材质、几何、渲染组件、资源包含与材质释放。
- 编辑器菜单 `Shattered Sail > Art Review > Capture Battle Surfaces` 生成六种地形与三种 Boss 的实际渲染验收图。
  必须使用全新输出目录，且不能带 `-nographics`；相机近景并非后期放大或修图。
