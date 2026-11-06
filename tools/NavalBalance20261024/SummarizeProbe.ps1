param([ValidatePattern('^[A-Za-z0-9_-]+$')][string]$FinalTag='final-v2')
$ErrorActionPreference='Stop'
$probeRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../outputs/naval-balance-20261024'))
$probeNames=@{ordinary='普通';fire='火焰';arcane='秘术';curse='咒术';tide='潮汐';thunder='雷霆';blood='血契'}
$probeBuilder=[Text.StringBuilder]::new()
[void]$probeBuilder.AppendLine('# 海战固定木桩采样结果')
[void]$probeBuilder.AppendLine('')
[void]$probeBuilder.AppendLine('正式比较使用 `before` 与 `'+$FinalTag+'`；`after`、`final` 均为中间候选，不能当成本次最终结果。两端使用相同采样器源码、配装、种子和策略。所有数值是有限策略在固定种子下的结果频数，不代表体系胜率。')
[void]$probeBuilder.AppendLine('')
$probeToolHashes=@('before',$FinalTag) | ForEach-Object{(Get-FileHash (Join-Path $probeRoot ($_+'/NavalBalanceProbe.cs'))).Hash}
if($probeToolHashes[0] -ne $probeToolHashes[1]){throw 'Probe sources differ'}
[void]$probeBuilder.AppendLine('采样器 SHA256：`'+$probeToolHashes[0]+'`。Core 修改前 141 文件，最终 142 文件；逐文件哈希见各目录 `source-hashes.csv`。')
[void]$probeBuilder.AppendLine('')
[void]$probeBuilder.AppendLine('固定木桩为 120 / 220 / 360 / 600 / 1000 船体，真实每轮护盾、修理、攻击均执行。统一海盗等级 10 仅为隔离机制解锁，不能解释为真实前期可用所有系别。五档卡牌请求强化为 0 / 1 / 1 / 2 / 3，逐牌按自身上限截断；各系实际数值不假定同档完全相等。')
[void]$probeBuilder.AppendLine('')
[void]$probeBuilder.AppendLine('双方起始距 2 格。真实每回合 3 能量、5 张抽牌、8 张手牌上限、洗牌、同名增费；最多 12 轮。火药与炮弹固定各 100，木板与药物各 30，仅排除航行补给不足干扰，逐局记录消耗。没有人工增加法力、格挡或生命。')
[void]$probeBuilder.AppendLine('')
foreach($probeCohort in @('base','formed')) {
 $probeRelative=if($probeCohort -eq 'formed'){'/formed'}else{''}
 $probeBefore=@(Import-Csv (Join-Path $probeRoot ('before'+$probeRelative+'/summary.csv')))
 $probeFinal=@(Import-Csv (Join-Path $probeRoot ($FinalTag+$probeRelative+'/summary.csv')))
 $probeCompare=foreach($probeB in $probeBefore){
  $probeA=@($probeFinal | Where-Object {$_.tier -eq $probeB.tier -and $_.school -eq $probeB.school -and $_.policy -eq $probeB.policy})
  if($probeA.Count -ne 1 -or $probeA[0].n -ne $probeB.n){throw 'Unpaired comparison'}
  $probeA=$probeA[0]
  [pscustomobject]@{Tier=$probeB.tier;School=$probeB.school;Policy=$probeB.policy;N=$probeB.n;BeforeKills=$probeB.kills;FinalKills=$probeA.kills;BeforeDeaths=$probeB.deaths;FinalDeaths=$probeA.deaths;BeforeTimeouts=$probeB.timeouts;FinalTimeouts=$probeA.timeouts;BeforeKillTurns=$probeB.meanTurnsOnKills;FinalKillTurns=$probeA.meanTurnsOnKills;BeforeEndHP=$probeB.meanEndHP;FinalEndHP=$probeA.meanEndHP;FinalEnemyHP=$probeA.meanEnemyHP;FinalPowder=$probeA.meanPowder;FinalBalls=$probeA.meanBalls;FinalWood=$probeA.meanWood}
 }
 $probeCompare | Export-Csv (Join-Path $probeRoot ($probeCohort+'-comparison.csv')) -NoTypeInformation -Encoding utf8
 [void]$probeBuilder.AppendLine($(if($probeCohort -eq 'base'){'## 基础组：普通与六系'}else{'## 成型组：准备牌、场地与真实部件'}))
 [void]$probeBuilder.AppendLine('')
 [void]$probeBuilder.AppendLine($(if($probeCohort -eq 'base'){'每组 12 张基础牌，准备阶段不出牌，主动隔离装备身份附加牌与场地。使用五档固定合法配装的净属性。这是基础机制诊断，实战装备对应部件还会自动加入附加牌，不能把这个隔离牌堆当成完整实战配装。'}else{'每组 10 张海战基础牌，加实际装备自动提供的 5 张身份牌，总海战牌堆 15 张。准备组也是合法 10 张：本系 b01 / b02、整顿号令、战术筹划、稳固船身各 2 张；真实洗抽和支付费用。T3 仍用稀有通用配装，T4 / T5 分别使用本系完整史诗 / 传说部件组，净属性详见 `mature-loadouts.csv`。因此基础组与成型组之间不是只改变场地的单变量实验。'}))
 [void]$probeBuilder.AppendLine('')
 [void]$probeBuilder.AppendLine('下表为输出优先策略。击杀回合均值只统计该行击杀局；超时局不能当作第 12 轮击杀。生存优先策略及逐局数据见 CSV。')
 [void]$probeBuilder.AppendLine('')
 [void]$probeBuilder.AppendLine('| 档 | 体系 | 前击杀 / 样本 | 后击杀 / 样本 | 前击杀均轮 | 后击杀均轮 | 后平均余船体 | 后平均敌船体 |')
 [void]$probeBuilder.AppendLine('|---|---|---|---|---|---|---|---|')
 foreach($probeRow in ($probeCompare | Where-Object {$_.Policy -eq 'damage'})) {
  [void]$probeBuilder.AppendLine('| '+($probeRow.Tier,$probeNames[$probeRow.School],($probeRow.BeforeKills+'/'+$probeRow.N),($probeRow.FinalKills+'/'+$probeRow.N),$probeRow.BeforeKillTurns,$probeRow.FinalKillTurns,$probeRow.FinalEndHP,$probeRow.FinalEnemyHP -join ' | ')+' |')
 }
 [void]$probeBuilder.AppendLine('')
 $probeSamples=@(Import-Csv (Join-Path $probeRoot ($FinalTag+$probeRelative+'/samples.csv')))
 [void]$probeBuilder.AppendLine('最终此队列共 '+$probeSamples.Count+' 局：击杀 '+@($probeSamples | Where-Object outcome -eq 'kill').Count+'，死亡 '+@($probeSamples | Where-Object outcome -eq 'death').Count+'，超时 '+@($probeSamples | Where-Object outcome -eq 'timeout').Count+'。')
 [void]$probeBuilder.AppendLine('')
}
[void]$probeBuilder.AppendLine('## 使用边界与复核')
[void]$probeBuilder.AppendLine('')
[void]$probeBuilder.AppendLine('- 策略仅看公开手牌、费用、状态和意图，不读抽牌堆顺序。会先回能再施放攻击、先产法力再聚束、先蓄潮防守再消耗、先积累咒印再兑现；它仍是有限启发式，不证明最优组牌或最优出牌。')
[void]$probeBuilder.AppendLine('- 选择场地由真实选择接口校验，禁域避开己方，折射阵优先己方，雷栅 / 水流优先覆盖双方；移动支付真实行动点，朝向仅取合法候选。雷栅移动叠链保守在 24 链前停止，攻击即将触发过载时先等待免疫。')
[void]$probeBuilder.AppendLine('- 秘术的基础组后期超时不能替代成型结论：成型组包含准备提高法力上限、真实回合中额外产法力、场地及部件炮击；也不能据成型结果宣称纯基础法术已经同样强。')
[void]$probeBuilder.AppendLine('- T3 成型秘术有 5 张通用稀有部件牌，场地专属部件从史诗开始；四种子冒烟曾漏掉超时个案，因此最终采用十六种子，并保留超时和剩余敌方生命。')
[void]$probeBuilder.AppendLine('- 资源统计按行动前后状态记录，法力每回合旧值到期与新上限补充单列，场地产法力另计；链消耗在雷栅内实际免除。攻击击杀后的过量伤害不被写成实际船体损失。伤害、吸收与修理以真实日志和净变化为准。')
[void]$probeBuilder.AppendLine('- 木桩静止、不具备 Boss 的场地、位移和特殊机制；本报告负责卡牌校准，Boss 结论应引用另行实战采样。')
[void]$probeBuilder.AppendLine('')
[void]$probeBuilder.AppendLine('复跑命令：`tools/NavalBalance20261024/RunProbe.ps1 -OutputTag before -UseSnapshot -Seeds 16` 与 `-OutputTag '+$FinalTag+' -UseSnapshot -Seeds 16`；添加 `-Cohort mature` 运行成型组。既有快照拒绝覆盖。')
[IO.File]::WriteAllText((Join-Path $probeRoot 'probe-summary.md'),$probeBuilder.ToString(),[Text.UTF8Encoding]::new($false))
Write-Output 'Wrote paired base/formed comparisons and probe-summary.md.'
