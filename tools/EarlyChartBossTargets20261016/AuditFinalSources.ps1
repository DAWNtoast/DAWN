$ErrorActionPreference = 'Stop'
$auditWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$auditRoot = Join-Path $auditWorkspace 'Artifacts/EarlyChartBossTargets20261016'
$auditTags = @('FirstProductionValidation200', 'SecondProductionMixedValidation200', 'SecondFinalAblation200-second-unprepared')
$auditLines = [Collections.Generic.List[string]]::new()
foreach ($auditTag in $auditTags) {
    $auditRows = @(Import-Csv -LiteralPath (Join-Path $auditRoot ($auditTag + '/source-hashes.csv')))
    $auditDifferences = [Collections.Generic.List[string]]::new()
    foreach ($auditRow in $auditRows) {
        $auditCurrent = (Get-FileHash -LiteralPath $auditRow.Source -Algorithm SHA256).Hash
        if ($auditCurrent -ne $auditRow.SHA256) { $auditDifferences.Add([IO.Path]::GetFileName($auditRow.Source)) }
        if ($auditTag -eq 'SecondProductionMixedValidation200' -and [IO.Path]::GetFileName($auditRow.Source) -match '^(CoreBattleBalance|CoreBoardingBossCatalog|CoreRouteBossCatalog)\.cs$') {
            $auditLines.Add(('Current SHA256 ' + [IO.Path]::GetFileName($auditRow.Source) + ' = ' + $auditCurrent))
        }
    }
    $auditLines.Add(($auditTag + ': checked ' + $auditRows.Count + ' source hashes; mismatches=' + $auditDifferences.Count))
    foreach ($auditDifference in $auditDifferences) { $auditLines.Add(('  Changed since snapshot: ' + $auditDifference)) }
    if ($auditTag -ne 'FirstProductionValidation200' -and $auditDifferences.Count -gt 0) {
        throw ('Final second-chart production source changed: ' + ($auditDifferences -join ', '))
    }
}
$auditLines.Add('First-chart validation predates subsequent production changes; the files listed above require scope comparison. Its snapshot is retained unchanged.')
$auditLines.Add('Final validation and final ablation use production values without CandidateFile overrides.')
$auditLines | Set-Content -LiteralPath (Join-Path $auditRoot 'final-source-audit.txt') -Encoding utf8
$auditLines
