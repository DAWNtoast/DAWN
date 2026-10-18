param(
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$ObservationTag = 'FinalBalanceObserve',
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$CombatTag = 'FinalBalanceCombat3Seeds',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$reportWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$reportRoot = Join-Path $reportWorkspace 'Artifacts/BoardingBossPacing20261016'
$reportBuild = Join-Path $reportRoot 'FinalBalance/Build'
$reportCycles = Join-Path $reportRoot ($ObservationTag + '/boss-cycles.csv')
$reportExe = Join-Path $reportBuild 'ExportBossCycles.exe'
$reportArgs = @((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$reportExe",
    ('-r:' + (Join-Path $reportBuild 'BoardingBossPacingTests.exe')))
foreach ($reportAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll')) {
    $reportArgs += '-r:' + (Join-Path $UnityData ('MonoBleedingEdge/lib/mono/4.7.2-api/' + $reportAssembly))
}
$reportArgs += Join-Path $PSScriptRoot 'ExportBossCycles.cs'
& (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @reportArgs
if ($LASTEXITCODE -ne 0) { throw 'Boss cycle export compilation failed.' }
& (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $reportExe $reportCycles
if ($LASTEXITCODE -ne 0) { throw 'Boss cycle export failed.' }
$reportCycleRows = Import-Csv -LiteralPath $reportCycles
$reportCycleMap = @{}
foreach ($reportCycle in $reportCycleRows) { $reportCycleMap[$reportCycle.enemy + '|' + $reportCycle.cycleTurn] = $reportCycle }
$reportObservation = Import-Csv -LiteralPath (Join-Path $reportRoot ($ObservationTag + '/runs.csv'))
$reportCombat = Import-Csv -LiteralPath (Join-Path $reportRoot ($CombatTag + '/runs.csv'))
$reportCombatSummary = @($reportCombat | Group-Object enemy | ForEach-Object {
    [pscustomobject]@{Enemy=$_.Name;Runs=$_.Count;Wins=@($_.Group | Where-Object outcome -eq 'win').Count;
        AvgRemainingHp=[Math]::Round(($_.Group | Measure-Object remainingHp -Average).Average,1);
        MaxTurns=($_.Group | Measure-Object turns -Maximum).Maximum}
})
$reportCombatSummary | Export-Csv -LiteralPath (Join-Path $reportRoot ($CombatTag + '/boss-summary.csv')) -NoTypeInformation -Encoding utf8
$reportCombatSummary | Format-Table -AutoSize
$reportRankSummary = @($reportCombat | Group-Object enemy,requestedUpgradeRank | ForEach-Object {
    [pscustomobject]@{Enemy=$_.Group[0].enemy;Rank=$_.Group[0].requestedUpgradeRank;Runs=$_.Count;
        Wins=@($_.Group | Where-Object outcome -eq 'win').Count;
        WinPercent=[Math]::Round(100 * @($_.Group | Where-Object outcome -eq 'win').Count / $_.Count,1);
        AverageRemainingHp=[Math]::Round(($_.Group | Measure-Object remainingHp -Average).Average,1)}
})
$reportRankSummary | Export-Csv -LiteralPath (Join-Path $reportRoot ($CombatTag + '/boss-rank-summary.csv')) -NoTypeInformation -Encoding utf8
$reportRankSummary | Where-Object { $_.Enemy.StartsWith('boss_route_') -or $_.Enemy.StartsWith('legendary_pirate_') } | Format-Table -AutoSize
$reportObservation | Select-Object enemy,ship,requestedUpgradeRank,outcome,remainingHp,maximumHp,enemyCycleLength,completeEnemyCycles,enemyHealing | Format-Table -AutoSize
$reportActions = Import-Csv -LiteralPath (Join-Path $reportRoot ($ObservationTag + '/actions.csv'))
$reportTurns = @($reportActions | Where-Object actor -eq 'enemy' | Group-Object enemy,ship,requestedUpgradeRank,phaseTurn | ForEach-Object {
    $reportFirst = $_.Group[0]
    $reportCycle = $reportCycleMap[$reportFirst.enemy + '|1']
    $reportCycleTurn = if ($reportCycle) { (([int]$reportFirst.phaseTurn - 1) % [int]$reportCycle.cycleLength) + 1 } else { '' }
    $reportCurrentCycle = if ($reportCycle) { $reportCycleMap[$reportFirst.enemy + '|' + $reportCycleTurn] } else { $null }
    [pscustomobject]@{Enemy=$reportFirst.enemy;Ship=$reportFirst.ship;Rank=$reportFirst.requestedUpgradeRank;Turn=$reportFirst.phaseTurn;
        CycleTurn=$reportCycleTurn;Energy=if ($reportCurrentCycle) { $reportCurrentCycle.energy } else { '' };
        Attack=($_.Group | Measure-Object attack -Sum).Sum;Piercing=($_.Group | Measure-Object piercing -Sum).Sum;
        Guard=($_.Group | Measure-Object guardGained -Sum).Sum;Heal=($_.Group | Measure-Object healed -Sum).Sum;
        HpLost=($_.Group | Measure-Object healthLost -Sum).Sum;Actions=($_.Group.name -join '+')}
})
$reportTurns | Export-Csv -LiteralPath (Join-Path $reportRoot ($ObservationTag + '/boss-turns.csv')) -NoTypeInformation -Encoding utf8
$reportTurns | Where-Object { [int]$_.Rank -ne 3 -and $_.Enemy -ne 'event_reef_coral_ambush' -and $_.Enemy -ne 'gold_customs' -and $_.Enemy -ne 'moon_ghost' } | Format-Table Enemy,Rank,Turn,Energy,Attack,Piercing,Guard,Heal,HpLost -AutoSize
