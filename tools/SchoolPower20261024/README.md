# 六体系与普通牌同档比较

`RunProbe.ps1` 编译真实 Core 快照，使用合法洗抽、费用、同名增费和回合结算。默认不读取正在编辑的生产目录，而使用 `outputs/school-power-20261024/Inputs/CoreSnapshot`。每个输出标签冻结 Core、工具、配装与牌组，旧标签只能按冻结版本重跑。

- 每组 12 张海战牌、10 张准备牌；普通及六系都实际出准备牌。准备能量、起手、手牌上限相同，准备牌内容不同是体系设计的一部分。
- `base` 为上一轮 12 张参考牌组；`candidate` 使用 `candidate-decks.csv`。普通对照强制固定。新卡在旧 Core 不存在时必须只跑 `-Cohorts base`，不会偷偷以其他卡替代。
- v3 新增 `flow` 队列，只比较普通和雷霆，雷霆为无雷栅的双预充、双电弧引信、双接地回流路线。它只诊断场外启动和短连锁；按设定，高连锁的跨回合保留和连续终结高度依赖雷栅，不要求场外构筑达到同等强度。`grid-risk` 使用与 candidate 完全相同的雷霆牌组，故意取消策略的雷栅 50 连锁回避，是风险敏感性样本，不是推荐玩法。两者均需 `-Cohorts 'flow,grid-risk'` 显式选择。
- 雷霆终结策略把终雷轰击、雷暴收束和极限放电视为场外清空储备的终结牌；会先打当前手牌中可合法支付的预充、引信、回能，再终结。预充线圈保持固定额外 2 连锁、不可强化；双预充可叠加，但第二张同名增费仍真实支付。雷栅内由 Core 决定免耗；绝不为测量补连锁。
- 默认隔离来源牌，同一物理配装下比较七系。`-Sources` 保留装备自动附加牌。若输入分系配装表，则每个具体配装同时跑普通与该系，不能跨不同船体/装甲/火力直接比较。
- `dummy` 为五档原木桩；`guard-pressure` 仅在样本中把每轮格挡提高至 3 倍，其余不变，是高格挡压力诊断，不能冒充正式潮汐 Boss。
- `boss` 保留真实 600 米开战、移动、冲撞；满血与 70% 分列；转入接舷独立记作 `boarding`。支持 `-BossFilter`。强制终止于 14 个海战回合，超时不按击杀计。
- 两种公开信息策略同时保留，不看未来牌序；任何固定策略不是最优玩法。`naval` 卡位与 `preparation` 卡位 CSV 可改，但正式前后比较必须冻结同一工具版本。

指标不合成一个武断总分：击沉回合、有效格挡、敌方回合损血、净血损、最低血量、主动付血、药木弹支出、能量支出（含准备）与伤害分别展示。输出 `ordinarySpeedRatioIfAllKilled=1.5` 表示同档同策略下击沉速度提高 50%；慢系可通过血损下降与对高格挡目标的效率体现优势，不要求同样速度。`damagePerEnergyRatioToOrdinary` 是行动窗口实际敌血下降/已支付能量的比值，执行咒印即死也计入敌血下降，不能叫基础伤害倍率。

注意：敌方回合中回血与燃烧可能抵消，`enemyHullLossAcrossWindows` 是各动作/回合窗口的正净血差，非逐伤害事件总和；Boss 血契恢复列为窗口净恢复，实际付血另列。格挡统计来自 Core 的敌方攻击有效吸收值，不把未使用的护盾算收益。基础补给固定为火药/炮弹各 100、木板/药物各 30，仅隔离航行经济。实际强化受每张上限截断，在 `used-decks.csv` 逐牌记录，不能据“同档”声称总强化成本相同。

`maxChain` 为动作边界可观察峰值；终结攻击内部新增连锁再清空不虚构为跨动作保留。用 `samples.csv` 的 `maxChain >= 5/10` 计算达到门槛的样本比例，结合 traces 查看终结前后的连锁。`grid-risk` 允许真实过载扣血和死亡；不能与安全策略混成一项胜率。

示例（新标签）：

```powershell
./tools/SchoolPower20261024/RunProbe.ps1 -OutputTag before-v1 -Cohorts base -Seeds 8
./tools/SchoolPower20261024/RunProbe.ps1 -OutputTag after-reproduction -SourceSnapshot D:/Game/outputs/school-power-20261024/final-v3/CoreSnapshot -Seeds 8
./tools/SchoolPower20261024/RunProbe.ps1 -OutputTag boss-reproduction -SourceSnapshot D:/Game/outputs/school-power-20261024/final-v3/CoreSnapshot -Mode boss -BossFilter legendary_pirate_tide_requiem -Seeds 4
```

最终正式输出为 `before-v2`（冻结旧规则）、`final-v3`（最终木桩）、`boss-final`、`pressure-final`。`overload-risk-final` 另用 `risk-decks.csv` 把两个终结卡位替换成预充线圈，延长战斗以检查过载；它不能计入正式构筑的胜率或强度。该冻结工具在 Boss 支援移动后可能漏记一次峰值和最低血量，动作明细及独立观察重放用于核实风险，不更改既有样本。

`VerifyCatalog.ps1` 对照的是原有 +0 基础卡清单，`catalog-changes.csv` 只列基础卡面变化；仅强化阶数变化的卡牌请查主报告与对应规则测试。固定 HTML、Markdown、JSON 通过 `SyncCatalog.ps1` 同步；脚本发现输入捕获后有新编辑时会停止覆盖。
