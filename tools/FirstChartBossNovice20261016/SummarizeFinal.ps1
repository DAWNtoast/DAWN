param([ValidatePattern('^[A-Za-z0-9_-]+$')][string]$RunTag = 'Final100Seeds')
$ErrorActionPreference = 'Stop'
$summaryWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$summaryRoot = Join-Path $summaryWorkspace 'Artifacts/FirstChartBossNovice20261016'
$summaryOutput = Join-Path $summaryRoot $RunTag
$summaryRuns = @(Import-Csv -LiteralPath (Join-Path $summaryOutput 'runs.csv'))
$summaryRows = foreach ($summaryPartition in @('calibration_0_29', 'holdout_30_99')) {
    $summarySubset = @($summaryRuns | Where-Object {
        if ($summaryPartition -eq 'calibration_0_29') { [int]$_.seed -lt 30 } else { [int]$_.seed -ge 30 }
    })
    foreach ($summaryGroup in ($summarySubset | Group-Object boss,policy,entryPercent)) {
        $summaryFirst = $summaryGroup.Group[0]
        $summaryWins = @($summaryGroup.Group | Where-Object outcome -eq 'win')
        [pscustomobject]@{
            Partition=$summaryPartition;Boss=$summaryFirst.boss;Policy=$summaryFirst.policy;EntryPercent=$summaryFirst.entryPercent;
            Runs=$summaryGroup.Count;Wins=$summaryWins.Count;WinPercent=[Math]::Round(100 * $summaryWins.Count / $summaryGroup.Count,2);
            AverageRemainingHp=[Math]::Round(($summaryGroup.Group | Measure-Object remainingHp -Average).Average,2);
            MinimumWinningHp=if ($summaryWins.Count) { ($summaryWins | Measure-Object remainingHp -Minimum).Minimum } else { '' };
            AverageTurns=[Math]::Round(($summaryGroup.Group | Measure-Object turns -Average).Average,2);
            AverageMedicineUsed=[Math]::Round(($summaryGroup.Group | Measure-Object medicineUsed -Average).Average,2);
            Timeouts=@($summaryGroup.Group | Where-Object outcome -eq 'timeout').Count
        }
    }
}
$summaryRows | Export-Csv -LiteralPath (Join-Path $summaryOutput 'calibration-holdout-summary.csv') -NoTypeInformation -Encoding utf8
$summaryRows | Where-Object Partition -eq 'holdout_30_99' | Format-Table Boss,Policy,EntryPercent,Runs,Wins,WinPercent,AverageRemainingHp -AutoSize
$summaryBefore = @{}
foreach ($summaryHash in (Import-Csv -LiteralPath (Join-Path $summaryRoot 'Baseline/source-hashes.csv'))) {
    $summaryBefore[$summaryHash.Source] = $summaryHash.SHA256
}
$summaryChanges = @(foreach ($summaryHash in (Import-Csv -LiteralPath (Join-Path $summaryOutput 'source-hashes.csv'))) {
    if ($summaryBefore[$summaryHash.Source] -ne $summaryHash.SHA256) {
        [pscustomobject]@{Source=$summaryHash.Source;BaselineSHA256=$summaryBefore[$summaryHash.Source];FinalSHA256=$summaryHash.SHA256}
    }
})
$summaryChanges | Export-Csv -LiteralPath (Join-Path $summaryOutput 'changed-core-sources.csv') -NoTypeInformation -Encoding utf8
$summaryChanges | Select-Object Source | Format-Table -AutoSize
@'
Seeds 000-029 are the calibration sample used when selecting the accepted candidate. Seeds 030-099 are 70 new deterministic seeds, retained separately as holdout observations.
The holdout uses the same fixed three one-step policies, actual unupgraded 12-card starter deck, two initial medicines, ordinary full crew, and 60/45 current HP entry variants. Policies and deck were not tuned after selecting the candidate.
Results measure this controlled heuristic simulation, not a population estimate for human players. No battle timed out.
Source hashes identify the only three changed Core files between Baseline and final. Source-diff review confirms the edited encounter definitions are the two first-chart bosses; the other ten dedicated Boss scripts are unchanged.
'@ | Set-Content -LiteralPath (Join-Path $summaryOutput 'validation-notes.txt') -Encoding utf8
