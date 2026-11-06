param([string]$Output = 'D:/Game/outputs/dual-fields-20261026')
$ErrorActionPreference = 'Stop'
$auditRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$auditOutput = [IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Force -Path $auditOutput | Out-Null

# Independent arithmetic model, not a simulated boss fight or win-rate estimate.
# Each affected turn sees R, R-1, ..., 1. No immunity, cleansing, oil, guard or movement.
$durationRows = foreach ($duration in @(3,5,7,8,9,10,11,13,15,18)) {
    $burn = 0L; $total = 0L; $first5 = 0L; $first10 = 0L; $round = 0
    while ($round -lt $duration -or $burn -gt 0) {
        $round++
        $total += $burn
        if ($round -le 5) { $first5 += $burn }
        if ($round -le 10) { $first10 += $burn }
        $burn = [Math]::Max(0L, $burn - 1)
        if ($round -le $duration) { $burn += $duration - $round + 1 }
    }
    [pscustomobject]@{
        InitialR = $duration
        BloodLifetimeLoss = $duration * ($duration + 1) / 2
        CurseLifetimeMarks = $duration * ($duration + 1) / 2
        FireFirst5Turns = $first5
        FireFirst10Turns = $first10
        FireLifetimeDamage = $total
        FireLastDamageTurn = $round
    }
}
$durationRows | Export-Csv -LiteralPath (Join-Path $auditOutput 'duration-model.csv') -NoTypeInformation -Encoding utf8

$attackRows = foreach ($damage in @(1,4,10,16,20,32,68,100)) {
    [pscustomobject]@{
        RawDamage = $damage
        FireOnly = [Math]::Ceiling($damage * 1.25)
        ArcaneTargetOnly = [Math]::Ceiling($damage * 1.25)
        CurseTargetOnly = [Math]::Ceiling($damage * 0.75)
        FireToArcane = [Math]::Ceiling($damage * 1.25 * 1.25)
        FireToCurse = [Math]::Ceiling($damage * 1.25 * 0.75)
        ArcaneAndCurseShared = [Math]::Ceiling($damage * 1.25 * 0.75)
    }
}
$attackRows | Export-Csv -LiteralPath (Join-Path $auditOutput 'attack-multiplier-model.csv') -NoTypeInformation -Encoding utf8

$bloodRows = foreach ($remaining in @(3,5,8,10,13,18)) {
    foreach ($payment in @(0,5,6,10,12)) {
        [pscustomobject]@{
            Remaining = $remaining
            ActivePayment = $payment
            PoolGuardGain = $payment * 2
            PoolEndLoss = $remaining
            TotalHealthPaidAndLost = $payment + $remaining
            MaximumExtraProtectionVersusSamePaymentOutsidePool = $payment * 2 - $remaining
        }
    }
}
$bloodRows | Export-Csv -LiteralPath (Join-Path $auditOutput 'blood-payment-model.csv') -NoTypeInformation -Encoding utf8

$catalogPath = Join-Path $auditRoot 'outputs/card-list-by-school-20261023/card-upgrade-data.json'
$catalog = Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json
$cardIds = @('card_special_sail_starshear_rig','card_special_sail_thunderphoenix',
    'card_special_sail_fogshroud_wing','card_special_sail_thunderreef_wing',
    'card_special_sail_moonweave','card_special_sail_last_free_wind',
    'card_special_plating_fogiron_carapace','card_special_plating_living_hull',
    'card_special_instrument_ghostmap_projector','card_school_blood_b05','card_school_blood_b04',
    'card_boarding_blood_05','card_boarding_blood_04','card_boarding_blood_06')
$cardRows = foreach ($card in $catalog.Cards | Where-Object { $_.Id -in $cardIds }) {
    foreach ($stage in $card.Stages) {
        foreach ($level in $stage.Levels) {
            [pscustomobject]@{ Id=$card.Id; Name=$card.Name; Stage=$stage.Stage; Rank=$level.Level;
                Energy=$level.Energy; Cost=$level.OtherCost; Effect=$level.Effect.Replace("`n",' ') }
        }
    }
}
$cardRows | Export-Csv -LiteralPath (Join-Path $auditOutput 'relevant-card-values.csv') -NoTypeInformation -Encoding utf8
Get-FileHash -LiteralPath $catalogPath -Algorithm SHA256 | Export-Csv -LiteralPath (Join-Path $auditOutput 'balance-catalog-hash.csv') -NoTypeInformation -Encoding utf8

$report = @'
六种双面场地数值检查（2026-10-26）

结论：新增固定25%增伤/易伤/减伤和血池支付1返2格挡可形成明确取舍；但不能据此认定整体数值已平衡。现有高阶延时仍会使火场燃烧和血池/禁域的持续风险非线性增长。此次保留用户确认的参数，以下列出已量化的风险，不把数学靶场当成Boss实战胜率。

一、攻击倍率
火场输出1.25，折射阵承伤1.25，禁域承伤0.75；共同生效时相乘，最后向上取整一次，正伤害最低1。
火场攻击折射阵：1.5625倍；20原始伤害变32。
火场攻击禁域：0.9375倍；20原始伤害变19。25%增伤和25%减伤不相互抵消。
主动接舷最多继承双方所在两格，即最多两种不同场地，共同作用于双方；冲撞接舷只继承被撞方所在格。折射阵和禁域共同继承时，双方主动攻击承伤也是0.9375倍。
加成只作用于主动攻击，不能增强燃烧、血池流失、雷栅过载，也不能减免主动支付和咒印消灭。Boss站火场同样获得增伤，站禁域同样获得减伤；突进免疫期间正负场地效果均关闭。
固定比例不随剩余持续时间增长，因而新增攻击规则没有引入新的延时倍率。不过低伤害向上取整存在粒度效应，例如1点火场攻击变2点；多段攻击需要逐击观察。详见attack-multiplier-model.csv。

二、血池与禁域持续风险
若第一个自身回合末R为初始值，随后依次经历R、R-1……1，期间不延长、不免疫、不离开，则血池总流失=R(R+1)/2；禁域新增咒印总数相同。
R=3、5、8、10、13时，总流失/新增咒印分别为6、15、36、55、91。
血池基础5回合，传说延时基础+5使之升至10回合，理论总流失从15增至55；传说+3延时+13可使R达到18，总流失171。它已是强危险区，不能把延时当作纯正面强化。
延时d的额外完整生命周期流失为dR+d(d+1)/2。例如R=5加13，额外流失156。实际死亡、撤离、免疫会提前终止观察；不能把171当作一定会发生的实战伤害。
禁域的25%减伤只减主动攻击，不提高咒印消灭阈值；高咒印或低生命目标仍会被消灭。登船双方无法通过海战走位离开继承场地，延时后接舷尤其危险。
血池和禁域可合法同时继承。若初始生命为H、初始咒印为0，两者在每次自身回合末贡献相同R且无其他伤害/治疗，累计新增S咒印、流失S生命后，斩杀条件为S>(H-S)/2，即S>H/3。例如80生命、两者初始R=10，三次自身回合末累计27咒印与27生命流失，便可触发消灭。禁域的攻击减伤无法缓解这条环境组合，应重点观察高阶接舷战。

三、血池耗血、格挡与回血经济
主动支付P生命获得2P格挡，回合末另流失R生命。格挡不是生命恢复，不能抵消该次R流失，也不会使低于R的生命安全。
若这次主动支付原本也会在池外发生，血池额外保护最多为2P-R，且只有新增格挡实际挡住攻击时才实现。支付卡本身的伤害/抽牌/能量收益另算。没有主动支付时纯承担R损失，未被消耗的格挡也不能算作治疗。
示例：P=5，R=5，新增10格挡但末尾损失5生命；P=5，R=13，新增10格挡且末尾损失13生命。不可将这些数字直接并入当前生命，见blood-payment-model.csv。
海战幽图回魂耗1能量、抽1/1/2张、修复6/9/12；活舟还生耗2能量，修复8/11/15/20，且提供格挡。它们可维持较低R血池，但受能量、抽牌与牌序限制，高R需要多次修复或离开。
登船血祭汲能支付6生命得2/3能量；禁忌启示耗1能量、支付6生命抽3/4张；饮血斩耗1能量，实际扣敌生命后吸血18/22/28/36。血池使前两张合计额外获得24格挡，短回合爆发较强。这不是凭空无限回血：吸血受敌方实际失去生命、格挡、可攻击目标和牌序约束；循环每次攻击会消耗敌方生命。高阶小牌组仍应作为后续实战强度重点，不能仅凭静态卡值断言没有爆发组合。

四、火场仍是最大长期风险
按“先结算已有燃烧、层数减1、再新增当前R层”的现行规则，用无免疫、无浸油、无格挡、无移动、足够生命的静止靶衡量：
初始R=5：前5回合34伤害，前10回合79，含离场地后全部余烬共100，第16回合才烧完。
初始R=10：前5回合84，前10回合294，累计1375，第56回合才烧完。
初始R=13：前5回合114，前10回合429，累计3822，第92回合才烧完。
初始R=18：前5回合164，前10回合654，累计13737，第172回合才烧完。
上述总量包含场地消失后的燃烧尾部；不是单回合伤害，也不是Boss实战结果。延时持续抬高新增燃烧，且已有燃烧只每回合减1，因此长期伤害增长远快于延时次数。新增25%主动攻击增伤既不能减轻这些燃烧，也不会额外放大它们。
Boss第3/5/8/10……回合突进可切断新增场地效果并靠近玩家，但不会清除已有燃烧。要进一步压低极端延时的收益，应另行设计延时幅度或燃烧衰减；此项不在本次已确认规则内，未擅自调整。

范围与证据
duration-model.csv与attack-multiplier-model.csv为独立数学模型，不是战斗AI、抽牌概率或胜率仿真；relevant-card-values.csv来自当前固定强化清单，记录所读清单的SHA256。运行时规则与预览一致性由本任务功能回归测试负责。此次数值检查未修改任何卡牌基础费用、升级曲线或既有场地持续时间。
'@
[IO.File]::WriteAllText((Join-Path $auditOutput '数值检查.txt'), $report, [Text.UTF8Encoding]::new($false))
Write-Output ('Wrote ' + $durationRows.Count + ' duration cases, ' + $attackRows.Count + ' damage cases, ' + $bloodRows.Count + ' blood payment cases, and ' + $cardRows.Count + ' card levels. No build/cache artifacts created.')
