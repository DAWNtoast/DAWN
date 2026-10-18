# 第二批动效：六系攻击与战斗结束

## 范围与完成边界

最初第二批包含以下四项。藏宝与瞭望窗已在前一轮完成；本轮补齐六系攻击辨识度和战斗结束差异表现。发牌、弃牌由独立任务完成，不属于本轮改动。

| 项目 | 实现范围 | 记录 |
| --- | --- | --- |
| 六系攻击辨识度 | 火、奥术、诅咒、潮汐、雷、血的轨迹与接触图形；命中、格挡、未命中；玩家即时/迟发攻击与敌方真实攻击动作 | 本文 |
| 战斗结束 | 海战击沉与接舷败阵分型，覆盖玩家和敌方，支持中断清理 | 本文 |
| 藏宝鉴定与合成 | 鉴定碎片飞入、四片聚合、接缝发光、完整海图展开 | [已完成记录](TreasureLookoutMotion.md#藏宝结果) |
| 航海瞭望窗 | 景物视差、目标接近、海面运动、天气与昼夜过渡，与地图船标共用进度 | [已完成记录](TreasureLookoutMotion.md#航海瞭望窗) |

## 六系攻击

`BattleSchoolAttackGraphic` 使用 Unity UI 网格绘制，不新增图片、材质或外部服务依赖。火焰为火舌与散射焰纹，奥术为直线晶形与旋转阵纹，诅咒为曲折链纹与封印，潮汐为并行波浪，雷电为折线与放射电弧，血系为弧形血痕与旋转短纹。

- `BattleSchoolAttackPresentation` 只分类已经成功提交的行动。即时玩家攻击读取真实生命损失与吸收格挡；敌方读取 `CoreBoardingEnemyActionResult`，不会根据卡面颜色或当前资源变化虚构伤害。
- 命中使用对应接触纹样；完全格挡使用较小接触纹样和护盾轮廓；未命中轨迹偏离目标后散去，不在目标上画命中印记。
- `CoreGameplayBattleSchoolAttackPresentation.AnimateSchoolAttack` 将双方显示目标的世界位置转换为 `Battle Card` 局部坐标。独立装饰不接收射线；数字反馈与接触时序配合，不再次结算伤害。
- 迟发攻击在排队时保留来源学校，在真正命中结算处记录 `CoreBoardingResolvedAttack` 事实。表现读取已提交的来源、生命与格挡前后值及单调序号；不是把“立即结算”卡牌自身的学校当成全部迟发攻击来源。
- `AnimateResolvedSchoolAttacks` 在播放前认领整批已提交事实，取消后不重播。普通非学校攻击继续沿用原有表现；不为原本没有攻击的补给、格挡或场地卡添加虚假弹道。

## 战斗结束

旧 `AnimateDefeat` 入口转发 `CoreGameplayBattleResolutionPresentation.AnimateBattleResolutionVisual`。`BattleResolutionMotion.DisplayedKind` 优先根据当前目标的 `BoardingPartyPresentation` 组件识别现役无标签接舷编队，同时兼容旧布局的 `Formation Label`；不以核心刚刚切换后的阶段误判仍在屏幕上的船或人物。

- 海战击沉持续约 0.95 秒。现役俯视海图保持船的海格位置与朝向，船体缩小、淡沉，水花围绕船中心扩散并伴随局部烟雾；旧教程侧视船保留向下沉没及倾斜表现。目标包围盒由四角投影得到，旋转舰船不会因只取对角线而错估范围。
- 接舷败阵持续约 0.72 秒。只让实际人物图下落、倾斜并淡出，编队标签与角色名字不随图像摇动；程序绘制的白旗沿旗杆低垂，辅以少量甲板尘迹。
- 玩家失败与敌方失败分别作用于实际败方，胜方姿态保持不动。结束横幅沿用已经提交的胜败和碎片信息；图形不会发放奖励、扣费、改变船体/船员生命或推进回合。
- 海战终局横幅放在船体血条与状态行下方的空海面，接舷横幅保持原位置，血量数字完整可读。
- 移动遭致命场地中断时，核心在终止分支保存已经实际走过的移动帧；表现沿这段轨迹抵达真实停止海格后再淡沉，不继续展示未执行步数。
- 自然完成时保留目标隐藏状态，直到现有调用者渲染已提交的结算页，避免船或人物在末帧突然恢复。暂停、停用或销毁时，`CancelBattleResolutionPresentation` 恢复原位置、旋转、缩放、透明度及原空闲动画状态，清理专属装饰；仅移除表现自己添加的 `CanvasGroup`。

## 生命周期与调用约束

控制器 `SuspendBattlePresentation` 在停止协程前取消学校攻击与战斗结束表现；应用暂停复用既有恢复路径，恢复后从权威模型重建界面，不重放出牌、迟发结算或终局奖励。学校攻击组件也检查所属控制器是否停用或销毁。所有动画使用非缩放时间，已有输入锁由控制器持有，图形组件不自行扩大锁定范围。

新的图形每帧仅更新已有网格数据与姿态。一次表现启动时可以准备临时图形和快照，过程中不加载贴图或创建人物美术。只读表现事件不参与规则判断，预览或恢复路径不得把旧表现历史当作新的命中。

## 验证入口与结果

- EditMode：`BattleSchoolAttackGraphicTests`、`CoreBoardingResolvedAttackTests`、`BattleResolutionMotionTests`，以及完整 `CoreNavalFieldsTests`、`CoreNavalActionMovementTests`。
- PlayMode：真实出牌、敌方回合、迟发结算、格挡、致命场地移动、双阵营终局、中断恢复及相关现役布局回归，详见最终 XML。
- [验证记录脚本](../../tools/BattleSecondBatch20261018/WriteVerification.ps1) 只读取最终 XML、比较交付源码与隔离工程 SHA256 并写入核对记录，不启动 Unity、不执行同步、构建或删除。

| 验收项 | 结果 |
| --- | --- |
| EditMode | 87 / 87 通过，0 失败、0 跳过；0.6652191 秒；[Edit04 报告](../../Artifacts/BattleSecondBatch20261018/Edit04/edit-results.xml) |
| PlayMode | 32 / 32 通过，0 失败、0 跳过；122.082618 秒；[Play03 报告](../../Artifacts/BattleSecondBatch20261018/Play03/play-results.xml) |
| 关键帧 | 本轮 Play03 生成 29 张；[关键帧查看页](../../Artifacts/BattleSecondBatch20261018/index.html) |
| 源码与隔离工程一致性 | 18 / 18 文件 SHA256 一致，验证结果 true；[核对记录](../../Artifacts/BattleSecondBatch20261018/verification.json) |
| 发布构建与设备性能 | 未执行 |

查看页只引用 `Play03/play-captures` 的本轮产物。攻击截图来自真实操作后自然运行的动画。终局图的 `Start` / `Finish` 文件名对应约 32% / 72% 的过程采样，后段图不是结束帧或结算页；静态关键帧不等同完整录像或设备性能验收。

Play03 四张海战胜败前后段图已逐张复核：横幅位于船体血条下方的空海面，双方血量数字完整可读，败方船体中心与朝向保持，胜方姿态稳定。致命场地的终止格、结算与中断行为由真实操作测试和核心规则测试共同检查。
