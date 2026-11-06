param(
    [Parameter(Mandatory=$true)][string]$Before,
    [Parameter(Mandatory=$true)][string]$After,
    [string]$Output = '',
    [int]$HoldoutStart = 16
)
$ErrorActionPreference = 'Stop'
if (!$Output) { $Output = Join-Path $After 'paired-comparison.csv' }
$taskBefore = @(Import-Csv -LiteralPath (Join-Path $Before 'samples.csv'))
$taskAfter = @(Import-Csv -LiteralPath (Join-Path $After 'samples.csv'))
if ($taskBefore.Count -ne $taskAfter.Count) { throw 'Paired sample counts differ.' }
function Key($r) { @($r.cohort,$r.loadout,$r.tier,$r.school,$r.target,$r.policy,$r.seed) -join '|' }
function Mean($rows, [string]$field) {
    if (!$rows.Count) { return 0.0 }
    return [double](($rows | ForEach-Object { [double]$_.$field } | Measure-Object -Average).Average)
}
function IsKill($r) { return $r.outcome -in @('kill','sunk') }
function Loss($rows) { return [double](($rows | ForEach-Object { [double]$_.initialHP - [double]$_.endHP } | Measure-Object -Average).Average) }
$taskMap = @{}
foreach ($r in $taskBefore) { $k=Key $r; if ($taskMap.ContainsKey($k)) { throw ('Duplicate baseline sample: '+$k) }; $taskMap[$k]=$r }
$taskSeenAfter = @{}
foreach ($r in $taskAfter) {
    $k=Key $r
    if ($taskSeenAfter.ContainsKey($k)) { throw ('Duplicate final sample: '+$k) }
    $taskSeenAfter[$k]=$true
    if (!$taskMap.ContainsKey($k)) { throw ('Unpaired sample: '+$k) }
    if ($r.school -eq 'ordinary') {
        foreach ($field in @('outcome','turns','initialHP','endHP','enemyHP','playsIncludingPreparation','energySpentIncludingPreparation','powder','balls','wood','medicine')) {
            if ($r.$field -cne $taskMap[$k].$field) { throw ('Ordinary reference changed: '+$k+'/'+$field) }
        }
    }
}
foreach ($name in @('SchoolPowerProbe.cs','NavalBalanceProbe.cs','BossBalanceProbe.cs','BossProfileAudit.cs','decks.csv','calibration-loadouts.csv')) {
    if ((Get-FileHash -LiteralPath (Join-Path $Before $name)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $After $name)).Hash) {
        throw ('Comparison inputs differ: '+$name)
    }
}
$taskResult = foreach ($subset in @('all','holdout')) {
    $subsetRows = @($taskAfter | Where-Object { $subset -eq 'all' -or [int]$_.seed -ge $HoldoutStart })
    foreach ($group in ($subsetRows | Group-Object cohort,loadout,tier,school,target,policy)) {
        $rows = @($group.Group); $first=$rows[0]
        $old = @($rows | ForEach-Object { $taskMap[(Key $_)] })
        $ordinary = @($subsetRows | Where-Object { $_.cohort -eq $first.cohort -and $_.loadout -eq $first.loadout -and $_.target -eq $first.target -and $_.policy -eq $first.policy -and $_.school -eq 'ordinary' })
        $beforeAll = @($old | Where-Object { !(IsKill $_) }).Count -eq 0
        $afterAll = @($rows | Where-Object { !(IsKill $_) }).Count -eq 0
        $ordinaryAll = @($ordinary | Where-Object { !(IsKill $_) }).Count -eq 0
        $baseTurns=Mean $ordinary 'turns'; $beforeTurns=Mean $old 'turns'; $afterTurns=Mean $rows 'turns'
        $baseLoss=Loss $ordinary; $beforeLoss=Loss $old; $afterLoss=Loss $rows
        [pscustomobject]@{
            Subset=$subset; Cohort=$first.cohort; Tier=$first.tier; School=$first.school; Target=$first.target; Policy=$first.policy; N=$rows.Count
            BeforeKills=@($old | Where-Object { IsKill $_ }).Count; AfterKills=@($rows | Where-Object { IsKill $_ }).Count
            BeforeDeaths=@($old | Where-Object outcome -in @('death','dead')).Count; AfterDeaths=@($rows | Where-Object outcome -in @('death','dead')).Count
            OrdinaryTurns=$baseTurns; BeforeTurns=$beforeTurns; AfterTurns=$afterTurns
            BeforeSpeed=$(if ($beforeAll -and $ordinaryAll) { $baseTurns/$beforeTurns } else { '' })
            AfterSpeed=$(if ($afterAll -and $ordinaryAll) { $baseTurns/$afterTurns } else { '' })
            OrdinaryHullLoss=$baseLoss; BeforeHullLoss=$beforeLoss; AfterHullLoss=$afterLoss
            BeforeHullLossReduction=$(if ($baseLoss -gt 0) { 1-$beforeLoss/$baseLoss } else { '' })
            AfterHullLossReduction=$(if ($baseLoss -gt 0) { 1-$afterLoss/$baseLoss } else { '' })
            BeforePaidLife=(Mean $old 'paidLife'); AfterPaidLife=(Mean $rows 'paidLife')
            BeforeEnergy=(Mean $old 'energySpentIncludingPreparation'); AfterEnergy=(Mean $rows 'energySpentIncludingPreparation')
            BeforeWood=(Mean $old 'wood'); AfterWood=(Mean $rows 'wood')
        }
    }
}
$taskResult | Export-Csv -LiteralPath $Output -NoTypeInformation -Encoding utf8
Write-Output ('PASS '+$taskAfter.Count+' paired samples, ordinary outcomes/actions/resources unchanged, strategy/loadout/decks identical; saved '+$Output)
