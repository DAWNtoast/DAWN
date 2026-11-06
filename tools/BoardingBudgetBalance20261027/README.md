# 同新增金币预算的接舷校准

最终报告：`../../outputs/boarding-budget-balance-20261027/接舷难度调整与复核.md`。

现役工具对应最终固定策略。`FinalBoard44Direct`、`FinalBoard44Soft`、`FinalNaval45`、`Curse46` 是最终源码下从 2000 开始的独立验证；各目录的 `Inputs`、`SourceSnapshot`、数据和哈希可复现当时运行。先前 1000 起的 `HeldoutBoard39` / `HeldoutNaval40` / `Curse41` 未完全达标，不能与最终版本混合计算。

运行使用现役 Core/Content 及明确列出的报价目录代码，创建独立源码快照，编译临时 Mono 控制台程序，结束后自动移除该次 `Build`。不会读取或改写用户存档，不启动 Unity Editor，不复制主工程 Library。

从 `D:/Game` 使用一个未存在的新 RunTag，例如：

```powershell
& tools/BoardingBudgetBalance20261027/Run.ps1 -RunTag ReproduceDirect01 -Seeds 512 -SeedStart 2000 -Cohorts 'starter,patrol,gunship' -Modes direct
& tools/BoardingBudgetBalance20261027/Run.ps1 -RunTag ReproduceSoft01 -Seeds 256 -SeedStart 2000 -Cohorts 'starter,patrol,gunship' -Modes soften60-repair
& tools/BoardingBudgetBalance20261027/Run.ps1 -RunTag ReproduceNaval01 -Seeds 256 -SeedStart 2000 -Cohorts 'starter,patrol,gunship' -Modes cannon
```

不要覆盖已有运行目录。期间不要修改生产源码；每次运行会检查源码漂移。以上复现已知种子，不属于新的独立验收；后续据结果调参时，应另外预留未查看的种子。

- 共同资产按阶段固定，新增预算为 720 / 1200 / 1200；每次升级经正式 API 实付，详细账在 `budgetLedger`。
- `direct` 和 `soften60-repair` 接舷卡组、强化、来源经验及初始补给相同。后者保留真实前置损伤、消耗、场地和接触先手。
- `diagnostic*` 及 `CrewScales` / `AttackScales` 仅用于阶段反应诊断，不能计入完整路径验收；最终参数均为 1。
- `BoardingDeck curse` 为固定 12 张咒印组合，合法参考阶段仅 `patrol,gunship`；不可为早期测试擅自提高等级。
- 强制接舷不是炮战击沉胜利；前置死亡、超时和区间外入场必须保留。分别报告合规入场后接舷胜率与全流程合规夺船率。

`AnalyzeResults.py` 独立统计单批结果；`AuditFinal.py` 检查最终四批的源码/工具/数据身份、共同资产、费用、真实血量继承与 Build 清理；`WriteReport.py` 从最终结果生成 MD。输出和独立审计均不应隐藏未达目标或构筑间差异。

最终核心回归入口与结果保存在同输出目录的 `core-regression-candidate43-summary.md`；52 个选定 fixture 共 2448 项通过。该数字不是整个 Unity 项目的所有测试。
