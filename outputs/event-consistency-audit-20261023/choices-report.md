# 事件选项、数值与实际效果核查

核查日期：2026-10-23。依据：[事件目录与文本描述](/D:/Game/docs/事件目录与文本描述_2026-10-23.md)及当前 Core 源码。最终验证结束：2026-10-23 11:36:51 UTC（北京时间 19:36:51）。

已覆盖现役 **195 个事件 ID、210 条航域／等级／分支记录、604 个选项**。修复了 7 个随机战斗选项的奖励说明、5 个通缉清除说明、1 个教程退出按钮及 3 段教程持续恢复提示。未调整金额、耗时、概率、奖励数值或结算算法。

最终当前源码编译成功，**537 项测试全部通过，0 失败、0 跳过**。161 个编译输入的散列在编译后再次核对一致；结果包含并行工作更新后的当前战斗源码。

## 覆盖清单

| 分类 | 目录记录 | 选项数 |
| --- | ---: | ---: |
| 三航域基础事件（含按航域扩充后的公共事件） | 32 | 77 |
| 三航域扩展固定事件 | 48 | 159 |
| 三航域随机事件 | 66 | 222 |
| 六系区域 Boss | 6 | 6 |
| 六艘传奇战舰 | 6 | 12 |
| 沉月王庭守藏舰 | 1 | 2 |
| 三档新版藏宝探险 | 39 | 111 |
| 现役教程，含随机事件两个文本分支 | 8 | 10 |
| 通用随机事件及发现／未发现伏击 | 4 | 5 |
| **合计** | **210** | **604** |

相同公共事件在不同航域保留各自选项，因此 604 是目录中的选项出现次数，不是按标签去重后的数量。工厂清单直接调用 `CreateEncounterPool`、`CreateExpandedFixedEncounters`、`RandomEventIds/CreateRandomEncounter`、`EnrichExploration`、Boss／传奇目录、三档藏宝目录及教程工厂，不依赖某张随机生成的海图，也不通过正则拼接事件数据。

完整可筛选数据：[choices-current.csv](/D:/Game/outputs/event-consistency-audit-20261023/validation-final/choices-current.csv)。每行包含事件 ID、上下文、重复策略、选项、原始详情、小时数、入战标记、全部条件、效果字段与海战现金报价。

旧版兼容定义另行核对：旧藏宝 18 个事件的三档定义、旧教程、旧区域 Boss 占位定义以及仅旧随机版本使用的 `random_food_spoilage`。它们不混入上述现役数量。现有兼容、旧海图几何、存档恢复测试一并执行。

## 已修正的问题

### 随机战斗的原始详情未跟随战利品折现规则

`CoreVoyageEventCatalog.C` 仍将 `VoyageEffect` 中的实物和原始金币直接描述为“胜利后获得，另计基础奖励”。当前海战结算会将正向实物战利品折为现金，再按百金币取整；例如月影伏击的 `Gem = 1` 实际不装载宝石，击沉结算为 100 金币、夺船为 200 金币。

现将 7 个随机战斗选项的工厂详情改用现行 `CoreBattleRewardDescriptions`，与正式事件弹窗和实际结算共用报价；商船求援的互助凭证、指定沉船情报仍明确列为胜利奖励。

受影响事件：`event_reef_open_water_ambush`、`event_reef_v2_whaler_signal`、`event_trade_open_water_ambush`、`event_trade_merchant_distress`、`event_trade_v2_powder_barge`、`event_moon_open_water_ambush`、`event_moon_v2_moon_taxcollector`。

改动：[CoreVoyageEventCatalog.cs:388](/D:/Game/UnityClient/Assets/_Project/Core/Runtime/CoreVoyageEventCatalog.cs:388)。新增实战回归覆盖月影伏击、月影收租人和商船求援：入战不提前领金币，击沉后得到所报的 100 金币，宝石／古代材料不会又重复入舱，线索仅胜利后发放。

### 通缉下降量应允许在零处截断

商盟的 5 个选项此前写固定“通缉 -1／-2”，而实际状态最低为 0。已改为“清除至多 1／2 点通缉”，保持所有成本、奖励和上限不变。

位置：[CoreTreasureExpeditionCatalog.cs:209](/D:/Game/UnityClient/Assets/_Project/Core/Runtime/CoreTreasureExpeditionCatalog.cs:209)，另含假章证人、引航、核销旧案及捐药担保选项。

### 教程退出按钮应说明实际去向

船员条件教学的备用选项原来写“返港招募”，实际先关闭事件、恢复海图。现改为“收好笔记，返回海图”，详情说明随后可返港招募航海士。没有把按钮改成自动结算返港。

位置：[CoreGuidedTutorialContent.cs:42](/D:/Game/UnityClient/Assets/_Project/Core/Runtime/CoreGuidedTutorialContent.cs:42)。

### 教程持续恢复提示仍使用旧的“两次治疗”规则

回归发现既有教程提示与测试预期落后于现行机制：生产实现已是持续恢复层数每回合下降 1，教程仍说每次恢复 5、共两次。已同步为施放获得 5 层、第一次结算后 4 层、第二次后 3 层；药品只在施放时消耗一次。未改动接舷战斗实现。

位置：[CoreGuidedBossLessons.cs:140](/D:/Game/UnityClient/Assets/_Project/Core/Runtime/CoreGuidedBossLessons.cs:140)、[CoreGuidedBossScriptTests.cs:251](/D:/Game/UnityClient/Assets/_Project/Tests/EditMode/CoreGuidedBossScriptTests.cs:251)。

## 数值与流程核查结论

- **成本与耗时：**所有现役选项耗时有限且非负，范围 0–7 小时。普通航域及通用来源的 463 个选项均实际执行，按真实职业和条件核对全部随船物资、船体、士气、碎片、探索增益及耗时；另逐项验证 237 个“缺少一份所需材料”状态，确认拒绝选择且状态不变。时间口粮在奖励前扣除，奖励中的食水不能为本次等待预支；医生药品折扣、潜水员打捞加成按实际规则计算。
- **藏宝成本与出口：**三档 39 条事件、111 个选项全部实际执行。信用须足额，通缉限制、印记／工具、共鸣、时间与职业条件有对应校验。三个核心保留低门槛撤出及零时暂缓；祭坛等待保留地点，不会顺带领奖。净化写“至多”，按持有诅咒清除；诅咒最高 10 层。
- **战斗奖励：**现役 26 个入战选项均纳入工厂清单。奖励采用正式击沉／夺船报价；现有战斗结算、折现、收益递减和溢出测试通过。战斗前只支付入战费用，胜利后发放奖励，撤退不能预领现金或探索线索。
- **重复领取：**普通节点完成后标记访问；同一次弹窗再次点击不能重新结算。复用同一海图时，普通奖励按 100%→50%→25%→0 递减，替代选项共享领取记录；物资成本、时间和损伤不跟着打折。标记为可重复的交易／修船维持完整成本与报价，每次访问仍会结束。宝藏新增 7 个真实节点／存档恢复回归证明已完成节点不能重开领奖，暂缓节点能回访且不领奖。
- **探索奖励：**顺航 +25%、瞭望 +35%；持续时间最多 24 小时，重复取得保留较长剩余时长而非叠加。纯线索服务在已知、已耗尽且无其它收益时会拒绝再次收费。三个关键线索链及保存恢复已有回归通过。
- **随机触发：**每航域现役随机袋含本域 22 条及通用 2 条，抽完一袋再补充，并保留最近 16 条记录以避免短期重复。首个随机间隔为 160–240 海里，此后商路 180–280、另两域 220–340 海里；固定事件后至少留出 100 海里。选项本身不是隐含成功率的抽奖，已定义效果按规则确定结算。
- **伏击和掉落概率：**藏宝／无活动海图航行的通用伏击按距离计算，单航次最多触发一次；三档藏宝的基础每 100 海里概率为 0.8%、1.2%、1.8%，基础侦测率 24%、20%、16%，侦测加成后最高 85%。普通 Boss 碎片基础掉落率 50%，另受海图收益倍率约束；引导 Boss 使用专门的保证奖励规则。概率取值和触发路径与文本分类一致。
- **交易与数值取舍：**未发现无材料、无时间、无访问约束即可无限兑现的正常流程。可重复香料拍卖消耗 80 金币换 1 宝石，港口回收价 60 金币，不构成直接买卖套利。木材／药材交易可能有利润，但需要物资、航程和新的访问。职业选项、长短耗时、修复与货物、诅咒与稀有奖励提供不同成本组合；本次没有因主观偏好重新调奖。

## 验证与交付文件

运行命令：

```powershell
& 'D:/Game/tools/VoyageEventChoiceAudit20261022/RunCoreValidation.ps1' `
  -Output 'D:/Game/outputs/event-consistency-audit-20261023/validation-final'
```

脚本复用现有纯 Core 验证器，编译当前 Core 和完整选定 NUnit fixtures，不删减测试，不启动 Unity，也不使用退役 ForMe 入口。

- [最终结果 results.xml](/D:/Game/outputs/event-consistency-audit-20261023/validation-final/results.xml)：537 通过，0 失败，0 跳过。
- [执行日志](/D:/Game/outputs/event-consistency-audit-20261023/validation-final/tests.log)、[编译日志](/D:/Game/outputs/event-consistency-audit-20261023/validation-final/compile.log)。编译仅有 4 条既有未赋值字段警告，无错误。
- [源文件散列](/D:/Game/outputs/event-consistency-audit-20261023/validation-final/source-hashes.csv)、[完整测试集](/D:/Game/outputs/event-consistency-audit-20261023/validation-final/fixtures.txt)、[覆盖数量](/D:/Game/outputs/event-consistency-audit-20261023/validation-final/choices-coverage.txt)。
- 新增 [CoreEventChoiceCatalogAuditTests.cs](/D:/Game/UnityClient/Assets/_Project/Tests/EditMode/CoreEventChoiceCatalogAuditTests.cs) 与 [CoreTreasureChoiceAuditTests.cs](/D:/Game/UnityClient/Assets/_Project/Tests/EditMode/CoreTreasureChoiceAuditTests.cs)，均附 Unity `.meta`。
- 更新 [RunCoreValidation.ps1](/D:/Game/tools/VoyageEventChoiceAudit20261022/RunCoreValidation.ps1) 纳入新覆盖及战斗报价、折现、溢出回归。
- 涉及文件的 `git diff --check` 通过；没有回退或覆盖其它任务的既有修改。

## 剩余边界

本报告验证数据、实际结算、条件、重复领取和当前规则的一致性。它不等于证明所有船型、职业组合、港口价格、装备等级和玩家策略的长期收益完全等价。尤其百金币取整会使不同随机战斗出现相同现金报价，体验取舍还包含敌舰强度、声望、线索及战斗风险；调整这种整体经济规则需要独立实玩或模拟依据。

存档回归使用 Core 检查点 DTO；本轮未重新做 Unity JSON 序列化或全部弹窗截图。图片及图片映射由并行美术审计负责，不包含在本文件的通过结论中。
