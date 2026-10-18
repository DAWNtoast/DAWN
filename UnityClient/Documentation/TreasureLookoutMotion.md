# 藏宝结果与航海瞭望窗动效

## 藏宝结果

`TreasureResultMotion` 只演出已经成功提交的鉴定或合成结果，不修改碎片、藏宝图、种子和存档。

- 鉴定持续 1.2 秒：显示归属提示，碎片沿短弧线飞向对应海图，使用 `TreasureChartDisplayLayout` 的四角投影落位。
- 合成持续 1.85 秒：四片聚合、接缝短暂发光，再展开完整海图。图册直接定位到新海图所在页，包括第三张及后续海图。
- 支持跳过，也允许立即鉴定下一片、合成、翻页或离开。新操作重建页面并结束旧演出，不等待动画再次提交结果。
- 按钮回调校验所属会话、对象活跃状态和本次回调是否已使用，防止延迟销毁对象的旧回调重复结算。
- `CurrentTreasureView` 只选择当前活跃的同名面板，避免 `Render` 后绑定到尚未被 Unity 延迟销毁的旧树。
- 完成、禁用或销毁时恢复目标原有透明度，只移除动效自己添加的 `CanvasGroup`。演出使用非缩放时间。

## 航海瞭望窗

`NavigationLookoutMotion` 与现有航海地图标记共用航行进度。模型仍先执行一次航行；随后显示的是已提交航程的表现。

- 开始播放前沿真实路径准备 5 个快照，采样位置、当地天气、游戏昼夜时间和可见目标，再插值景物的位置、尺寸与透明度。
- 景物保留贴图比例与船体水线接触位置；进出视野时淡入淡出，固定标题和说明不随景物移动。
- `NavigationLookoutOceanGraphic` 只对海平线以下的海面施加轻微 UV 扰动，天空、海平线和图像边缘保持固定，继续使用瞭望窗遮罩。
- 天气与昼夜背景按快照权重混合，并按绘制层次归一化透明度，避免过渡期间出现暗闪或透明空洞。
- 目标投影、贴图加载和轨道数组准备在播放前完成；每帧只插值已有数据、更新水面网格，不加载资源或创建景物。
- 失焦时暂停推进；应用暂停或控制器停用时结束旧航行表现。恢复只重建已提交结果，不重放航行、不重复扣除时间和补给。进入事件页面时退役旧瞭望动效。

## 验证入口

- EditMode：`TreasureResultMotionTests;NavigationLookoutMotionTests`，检查透明度恢复、已满海图、投影落点、海面 UV 边界及天气/昼夜混合权重。
- PlayMode：`TreasureResultMotion_;NavigationLookoutMotion_;DestroyedRevealRestoresItsTarget_;PresentationUsesUnscaledTime_`，覆盖真实按钮提交一次、立即继续、新海图翻页、跳过/销毁/禁用、航行视差、暂停恢复及事件切换。
- 相关回归：`TreasureFragments_;TreasureFolio_;NavigationArrival_;NavigationLifecycle_;SpeedBasedVoyage_`。事件到达允许右列从瞭望窗扩为全高事件页；海图边界、透明度、后续帧稳定性及单次成本仍需保持。

验收输出位于 `Artifacts/TreasureLookoutMotion20261017/`，各次 `Edit*`、`Play*` 目录保存日志与 NUnit XML；动态截图位于对应 `Play*/play-captures/`。以各轮实际结果为准，不将早期失败记录当作最终结论。

测试通过 `tools/BattleHud20261017/RunValidation.ps1` 在 `tmp` 下的独立 Unity 工程执行，使用独立玩家存储身份；同步和截图均不写回玩家存档，不刷新或恢复 `Scenes/UIPreviewsForMe`。动态截图用于检查演出过程，多分辨率布局由独立布局断言验证。
