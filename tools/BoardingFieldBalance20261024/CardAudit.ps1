param(
    [string]$Catalog = 'D:/Game/outputs/boarding-field-balance-20261024/Inputs/Catalog',
    [string]$Output = 'D:/Game/outputs/boarding-field-balance-20261024/card-audit'
)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $Output | Out-Null
$auditCatalog = Get-Content -LiteralPath (Join-Path $Catalog 'card-upgrade-data.json') -Raw | ConvertFrom-Json
$auditRows = @(
    foreach ($auditCard in $auditCatalog.Cards) {
        foreach ($auditStage in $auditCard.Stages) {
            if ($auditStage.Stage -ne '接舷') { continue }
            foreach ($auditLevel in $auditStage.Levels) {
                [pscustomobject]@{
                    Id=$auditCard.Id; Number=$auditCard.Number; Name=$auditCard.Name; School=$auditCard.School
                    Source=$auditCard.Source; MaximumLevel=$auditStage.MaximumLevel; Level=$auditLevel.Level
                    Energy=$auditLevel.Energy; OtherCost=$auditLevel.OtherCost; Effect=$auditLevel.Effect
                }
            }
        }
    }
)
$auditRows | Export-Csv -LiteralPath (Join-Path $Output 'all-boarding-card-ranks.csv') -NoTypeInformation -Encoding utf8
$auditCardList = [Collections.Generic.List[string]]::new()
$auditCardList.Add('# 当前接舷卡牌全部等级审计快照')
$auditCardList.Add('')
$auditCardList.Add('从本轮修改前固定清单导入。含可接舷的当前有效共享牌与船员牌；历史别名不重复计算。')
foreach ($auditSchool in ($auditRows | Group-Object School)) {
    $auditCardList.Add('')
    $auditCardList.Add('## ' + $auditSchool.Name)
    $auditCardList.Add('')
    $auditCardList.Add('| 名称 | 来源 | 等级 | 能量 | 额外成本 | 效果 |')
    $auditCardList.Add('|---|---|---:|---:|---|---|')
    foreach ($auditRow in $auditSchool.Group) {
        $auditEffect = ($auditRow.Effect -replace '\r?\n','<br>') -replace '\|','\|'
        $auditCardList.Add("| $($auditRow.Name) | $($auditRow.Source) | +$($auditRow.Level) | $($auditRow.Energy) | $($auditRow.OtherCost) | $auditEffect |")
    }
}
$auditCardList | Set-Content -LiteralPath (Join-Path $Output '全部接舷卡牌与强化.md') -Encoding utf8

function Get-FieldBurnBudget([int]$Duration) {
    # An actor's first end applies fire after its existing burn settles. This
    # reference is the side seeing T,T-1,...,1; ownership can offset by one.
    $auditBurn = 0
    $auditTotal = 0
    $auditFirstSix = 0
    $auditTick = 0
    $auditSequence = [Collections.Generic.List[int]]::new()
    while ($Duration -gt 0 -or $auditBurn -gt 0) {
        $auditTick++
        $auditSequence.Add($auditBurn)
        $auditTotal += $auditBurn
        if ($auditTick -le 6) { $auditFirstSix += $auditBurn }
        $auditBurn = [Math]::Max(0,$auditBurn - 1) + $Duration
        $Duration = [Math]::Max(0,$Duration - 1)
    }
    [pscustomobject]@{Total=$auditTotal;FirstSix=$auditFirstSix;Ticks=$auditTick;Sequence=($auditSequence -join '/')}
}
$auditFieldRows = foreach ($auditDuration in @(1,2,3,4,5,6,7,8,10,13)) {
    $auditBudget = Get-FieldBurnBudget $auditDuration
    [pscustomobject]@{
        InitialDuration=$auditDuration;BurnFirstSixEnds=$auditBudget.FirstSix;BurnLifetime=$auditBudget.Total
        BurnEndsToClear=$auditBudget.Ticks;ManaOrMarksTotal=($auditDuration*($auditDuration+1)/2)
        Sequence=$auditBudget.Sequence
    }
}
$auditFieldRows | Export-Csv -LiteralPath (Join-Path $Output 'field-raw-budgets.csv') -NoTypeInformation -Encoding utf8

$auditBurnRows = foreach ($auditStacks in @(3,4,5,6,7,8,9,10,11,12,13,14,16,21,26)) {
    [pscustomobject]@{
        Stacks=$auditStacks;Lifetime=($auditStacks*($auditStacks+1)/2)
        FirstThree=([Math]::Max(0,$auditStacks)+[Math]::Max(0,$auditStacks-1)+[Math]::Max(0,$auditStacks-2))
        TwoApplicationsOneTurnApart=($auditStacks+(2*$auditStacks-1)*(2*$auditStacks)/2)
    }
}
$auditBurnRows | Export-Csv -LiteralPath (Join-Path $Output 'burn-raw-budgets.csv') -NoTypeInformation -Encoding utf8
$auditRows | Where-Object Level -eq 0 | Group-Object School | Select-Object Name,Count | Format-Table
$auditFieldRows | Select-Object InitialDuration,BurnFirstSixEnds,BurnLifetime,ManaOrMarksTotal | Format-Table
