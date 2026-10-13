# 固定 UI 的 Prefab 与复用

固定主菜单、公共弹窗和港口/航海顶栏使用可编辑的 Prefab，脚本绑定当前数据和交互。市场、仓库保留已有局部更新；战斗、地图格子、设施商品与其他随游戏状态变化的正文继续按原逻辑生成，不改变战斗和经济规则。

## 可编辑的公共布局

- `Assets/_Project/Resources/UI/PortHubHeader.prefab`：港口总览顶栏。
- `Assets/_Project/Resources/UI/PortFacilityHeader.prefab`：所有港口设施共用顶栏。
- `MainMenuSurface.prefab`：Canvas、固定画面范围、背景、标题和五个菜单按钮。
- `MainMenuSettings.prefab`、`MainMenuAchievements.prefab`、`MainMenuNewGameConfirmation.prefab`、`MainMenuSaveNotice.prefab`：按需实例化的菜单弹窗。成就模板仅含八个空行，图标与进度由当前页面绑定。
- `PortSettings.prefab`、`ReturnToMenuConfirmation.prefab`：港口和航海共用的设置与保存确认弹窗。
- `VoyageHeader.prefab`：航海/事件顶栏，包含时间轴、读数、船舱和设置按钮；战斗维持原有区域页眉。
- `PortHeaderView` 序列化绑定文字、按钮和框架。调整 Prefab 中的子节点布局即可影响正式游戏及重新生成的编辑预览；不要删除绑定引用。
- 港口名称、金币、日期、设施标题和六港框架由运行时赋值。标题栏高度随教学状态调整。
- 菜单 `Shattered Sail > UI > Create Missing Port Header Prefabs` 只创建缺少的 Prefab，不覆盖已有编辑。它同时保存旧动态 Sprite 的真实资源，确保重开工程后外观完整。
- `Shattered Sail > UI > Create Missing Fixed UI Prefabs and Bake Coverage` 为首次迁移入口：烘焙六港家具遮罩，再创建缺少的固定 UI Prefab。已有 Prefab 不覆盖。默认构建只验证完整绑定与遮罩导入设置，不自动重写布局。
- `SharedPrefabSkins` 保存共享皮肤 Sprite，继续引用原始纹理。动态港口/航海图及设置大纸张不放入模板默认依赖，由打开界面时绑定。

## 生命周期

`CoreGameplayPortViewCache` 每个控制器最多保留一个港口总览顶栏、一个设施顶栏、一张背景和一层色罩。停用时清除旧区域图片引用，当前港口主题继续复用，其余主题解除缓存持有并交给原有延迟回收。顶栏在运行时使用子 Canvas，减少正文变化对顶栏几何重建的影响。

航海顶栏最多保留一个实例，在航海重绘时更新读数和事件；离开航海时清除背景和指南针图片。菜单存档提示、游戏设置和保存确认分别保留一个按需创建的实例。游戏设置关闭后清除大纸张并合并安排资源回收；重新打开会重读已保存的设置，旧事件版本不能操作新草稿。主菜单设置继续保留原有的关闭再开草稿行为。

六港家具 coverage 使用原算法离线生成的、原尺寸无损 R8 线性遮罩。运行时直接读取纹理，Atlas 和 coverage 均不保留 CPU 像素副本；不改变源 PNG、着色器、过滤方式或抗锯齿边界。普通页面清理释放借用资源引用，只销毁本控制器生成的材质。

市场同页刷新保留面板、行、输入框和按钮，更新库存、报价、货物容量和收货方式。仓库按父节点和稳定名称复用藏品卡、货物行、图标及页签；未显示的节点停用，已访问的条目再次显示时复用。离开页面、改变港口或会话时正文缓存失效，释放对旧节点的引用。缓存只覆盖当前打开的市场或仓库，不永久保留每一个设施页面。

交易按钮在每次绑定时取得新的版本号。旧报价、排队事件和旧输入回调不能作用于新页面。交易弹窗关闭时先停用并脱离原层级，避免 Unity 延迟销毁导致同帧查到旧遮罩；设置和保存确认弹窗停用后留在原层级复用。

教学期间通常保留完整正文刷新，以维持引导、高亮和交互限制；既有 `GuidedTutorialRecoveryActive` 市场恢复阶段继续允许局部复用。公共顶栏每次重新绑定回调，再应用当前教学步骤的限制，避免沿用旧步骤的回调。

## 验证

- PlayMode：`MarketRetainedView_*`、`WarehouseRetainedView_*`、`PortChrome_*` 验证对象身份、状态更新、旧回调、切港和教学。
- 同时运行现有市场、仓库、设置、教学与六港布局回归。
- `tools/PrefabUiAudit/PortUiPerformanceProbe.cs` 在隔离工程中对真实按钮操作采样。使用相同探针、固定日期和样本分别测量优化前后；数据覆盖 CPU 回调加 Canvas 更新、当前线程分配和对象替换次数，不代表成品帧率。
- 首次打开页面仍需要实例化和资源加载。已访问内容以少量常驻内存换取更少的重复创建；不能把 Prefab 本身等同于性能提升。
- 本轮独立 Player 对比工具为 `tools/PrefabRuntimeAudit`：基线提交 `8f5563e`、同一探针 DLL、68 个检查点、三轮交替测量；加载回调、首个目标界面渲染、延迟回收和操作系统内存分开统计。截图与性能分开运行，详见该目录 `README.txt` 和本轮发布记录。
