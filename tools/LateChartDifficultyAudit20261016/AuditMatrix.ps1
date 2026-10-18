param([ValidatePattern('^[A-Za-z0-9_-]+$')][string]$RunPrefix = 'SystemsValidation200')
$ErrorActionPreference = 'Stop'
$matrixWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$matrixRoot = Join-Path $matrixWorkspace 'Artifacts/LateChartDifficultyAudit20261016'
$matrixSchools = @('fire','arcane','curse','tide','thunder','blood')
$matrixBuilds = @('B1','B2','B3')
$matrixRows = @()
$matrixReferenceSources = $null
$matrixReferencePolicy = $null
$matrixReferenceSpec = $null
$matrixReferenceHarness = $null
foreach ($matrixSchool in $matrixSchools) {
    foreach ($matrixBuild in $matrixBuilds) {
        $matrixTag = $RunPrefix + '_' + $matrixSchool + '_' + $matrixBuild
        $matrixDirectory = Join-Path $matrixRoot $matrixTag
        $matrixRunRows = @(Import-Csv -LiteralPath (Join-Path $matrixDirectory 'summary.csv'))
        if ($matrixRunRows.Count -ne 9) { throw ('Incomplete matrix group: ' + $matrixTag) }
        $matrixRows += $matrixRunRows | Select-Object @{n='school';e={$matrixSchool}},@{n='build';e={$matrixBuild}},*
        $matrixSources = @(Import-Csv -LiteralPath (Join-Path $matrixDirectory 'source-hashes.csv') | Sort-Object Source | ForEach-Object { $_.Source + ':' + $_.SHA256 })
        $matrixHarness = @(Import-Csv -LiteralPath (Join-Path $matrixDirectory 'harness-hashes.csv'))
        $matrixPolicy = ($matrixHarness | Where-Object { $_.Source -like '*SystemPolicy.cs' }).SHA256
        $matrixSpec = ($matrixHarness | Where-Object { $_.Source -like '*school-spec.txt' }).SHA256
        $matrixMain = ($matrixHarness | Where-Object { $_.Source -like '*SimulateLateCharts.cs' }).SHA256
        if ($null -eq $matrixReferenceSources) {
            $matrixReferenceSources = $matrixSources; $matrixReferencePolicy = $matrixPolicy; $matrixReferenceSpec = $matrixSpec; $matrixReferenceHarness = $matrixMain
        } elseif (@(Compare-Object $matrixReferenceSources $matrixSources).Count -gt 0 -or
            $matrixPolicy -ne $matrixReferencePolicy -or $matrixSpec -ne $matrixReferenceSpec -or $matrixMain -ne $matrixReferenceHarness) {
            throw ('Mixed source/policy/spec versions in ' + $matrixTag)
        }
    }
}
$matrixRows | Export-Csv -LiteralPath (Join-Path $matrixRoot ($RunPrefix + '-verified.csv')) -NoTypeInformation -Encoding utf8
$matrixBoundRows = foreach ($matrixRow in $matrixRows) {
    $matrixN = [double]$matrixRow.runs; $matrixP = [double]$matrixRow.wins / $matrixN; $matrixZ = 1.96
    $matrixDenom = 1 + $matrixZ * $matrixZ / $matrixN
    $matrixCenter = ($matrixP + $matrixZ * $matrixZ / (2 * $matrixN)) / $matrixDenom
    $matrixSpread = $matrixZ / $matrixDenom * [Math]::Sqrt($matrixP * (1-$matrixP) / $matrixN + $matrixZ * $matrixZ / (4*$matrixN*$matrixN))
    $matrixRow | Select-Object *,@{n='wilson95LowPercent';e={[Math]::Round(100*($matrixCenter-$matrixSpread),2)}},@{n='wilson95HighPercent';e={[Math]::Round(100*($matrixCenter+$matrixSpread),2)}}
}
$matrixBoundRows | Export-Csv -LiteralPath (Join-Path $matrixRoot ($RunPrefix + '-confidence.csv')) -NoTypeInformation -Encoding utf8
$matrixAudit = @(
    ('Completed groups: ' + ($matrixSchools.Count * $matrixBuilds.Count)),
    ('Result rows: ' + $matrixRows.Count),
    ('Total battles: ' + (($matrixRows | ForEach-Object {[int]$_.runs} | Measure-Object -Sum).Sum)),
    ('Total timeouts: ' + (($matrixRows | ForEach-Object {[int]$_.timeouts} | Measure-Object -Sum).Sum)),
    'Identical production source hashes across all groups: true',
    ('Identical policy hash: ' + $matrixReferencePolicy),
    ('Identical harness hash: ' + $matrixReferenceHarness),
    ('Identical school specification hash: ' + $matrixReferenceSpec),
    'Wilson intervals describe sampling uncertainty for these fixed policies/decks, not player population uncertainty.'
)
$matrixAudit | Set-Content -LiteralPath (Join-Path $matrixRoot ($RunPrefix + '-audit.txt')) -Encoding utf8
$matrixAudit
