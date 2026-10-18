早期海图 Boss 接舷战离线验证（2026-10-16）

本目录编译真实 Core Runtime 源码快照，只写工具产物，不修改生产、存档或场景。
产物根目录：D:/Game/Artifacts/EarlyChartBossTargets20261016/。
最终结论只使用 FirstProductionValidation200、SecondProductionMixedValidation200
两个独立验证，以及冻结参数后的 SecondFinalAblation200-all-summaries.csv。
其它目录是历史探索或校准，不能当作最终验证。FirstGrid30 因HP缓存未同步无效，
已有INVALID.txt。旧FirstProductionHoldout100后来纳入再校准，不再是最终holdout。

最终配置
first-starter：原生12张初始接舷牌全部+0，10普通船员、60HP，无特殊船员，2药。
second-prepared-mixed：最终二图标准准备，与旧second-prepared双治疗不同。
  ship4 salvage打捞船·拾潮，16船员位；14普通+厨师塔姆+潜水员伊沙，76HP。
  原生12牌保留，再加真实特殊来源牌，总14牌，不调用SetBattleDeck。
  card_cutlass_raid、card_crew_thrust、card_crew_guard各+1，同ID两张同步。
  crew_broken_anchor_cook_tam -> fire_05；crew_salt_crown_diver_isha -> blood_07。
  仅cook/diver能力，4药4火药实际消耗，无装备属性加成。伊沙可在首图盐冠港合法招募。
每场断言真实手牌+抽牌堆与配置牌组一致，检查船员生命、手牌5、能量3、药品、
升级和特殊牌key。starter-deck.csv导出实际ForBoarding映射。

最终消融场景
second-unprepared：10普通60HP、12张+0、2药。
second-upgrades：未准备配置只强化上述三种牌。
second-ship：只换ship4，16普通84HP，12张+0、2药。
second-prepared-no-special：16普通84HP、12牌、三种+1、4药4火药。
second-prepared-isha：15普通+伊沙80HP、13牌、三种+1、基础2药和原始火药库存。
second-prepared-mixed：上述最终标准。
其它历史场景：second-ship-upgrades为16普通+强化但基础2药；second-prepared-cook
为15普通+塔姆80HP/13牌/4药；second-prepared及second-prepared-2med为医生+塔姆
双治疗76HP/14牌，分别4药和2药，均不是最终标准。历史prepared配置火药为4。

样本划分
所有配置种子前缀为FIRST-CHART-NOVICE-20261016-，后接三位整数。
0–199用于校准、配置选择和最终消融，不属于独立验证。
200–399在各图数值和标准准备固定后首次运行，是最终独立验证；未据此继续调参。
每Boss每策略200场，两Boss三策略各1200场。跨配置同seed并不保证逐张相同抽牌，
因为牌组大小和随机调用序列可能不同。
首图最终HP119/105，独立balanced胜率66.5%/73%；二图最终HP133/147，80%/82%。
总体结论另见主任务的Artifacts/EarlyChartBossTargets20261016/FinalBalanceReport.txt。

PowerShell复现（D:/Game目录，使用新RunTag避免覆盖历史）
& ./tools/EarlyChartBossTargets20261016/RunSimulation.ps1 -RunTag ReproFirst -Seeds 200 -StartSeed 200 -Scenario first-starter -PolicySet all
& ./tools/EarlyChartBossTargets20261016/RunSimulation.ps1 -RunTag ReproSecond -Seeds 200 -StartSeed 200 -Scenario second-prepared-mixed -PolicySet all
& ./tools/EarlyChartBossTargets20261016/RunSecondMatrix.ps1 -RunTag ReproAblation -Seeds 200 -StartSeed 0 -ScenarioSet final
RunSimulation：Seeds默认30、范围1–500；StartSeed默认0；Scenario默认first-starter；
PolicySet为balanced或all；EntryPercent默认100，只改变入场当前HP、不改最大HP。
UnityData默认D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data，使用随Unity提供的Roslyn与Mono。
RunSecondMatrix复用一次编译；ScenarioSet final跑上述6组，legacy保留旧7组探索。
CandidateFile可选，只用于离线校准；最终生产验证绝不传入。
候选CSV为id,boardingHp,mainAttack,fireConsumeAttack，或在id后加boss列定向某Boss。
HP在本场profile与接舷缓存中同时替换；0保持原值。攻击列只支持首图对应固定卡，
二图HP候选攻击列始终0。没有静态catalog修改、隐性buff或按玩家行为动态调整。

真实核心与假定
执行真实遭遇、StartBattle、EndBattleTurn、TryBoard、CanPlayCard、PlayCard、预告
及敌人结算。跳过导航、购买操作和海战消耗，直接配置已核实合法的准备；二图恢复
解锁航线所需最低经验。在100米平行同航向接触调用真实TryBoard。
满血与给定补给是接舷入场假定，不覆盖途中损耗、海战或经济积累难度。
三策略都是固定单步评分，只看当前手牌、效果和可见预告，无未来抽牌或多回合搜索。
balanced取中等攻防权重，conservative偏防御治疗，aggressive偏攻击；均优先当前
击杀和净回能。不为特定Boss写解法，不模拟真人失误。结果是程序策略胜率，不能
等同真实玩家群体胜率，也不能用最高策略结果替代预定balanced目标。
每回合32动作、每场60回合上限，超限明确计timeout，不计胜利。

审计输出
CoreSnapshot与source-hashes.csv保留编译来源和SHA256，复制时逐个核对源文件。
Build保留工具源码、可执行文件及可选候选副本；compile.log记录编译。
baseline.txt/run-settings.txt/candidate-parameters.csv记录fixture、种子和覆盖参数。
runs.csv为逐场，summary.csv为汇总，turns.csv/actions.csv记录每组前三seed细节。
矩阵复用目录的compiled-assembly.txt指向首组编译，source-hashes.csv相同。
final-source-audit.txt记录最终源码与报告快照的哈希核对结果。
