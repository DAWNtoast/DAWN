$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOutput = Join-Path $taskRoot 'outputs/boss-calibration-20261024'
$taskArtifacts = Join-Path $taskRoot 'Artifacts/BossCalibration20261024'
$taskManifests = @(
    (Join-Path $taskOutput 'final-all16/source-hashes.csv'),
    (Join-Path $taskOutput 'VerifiedDesign/source-hashes.csv'),
    (Join-Path $taskOutput 'intent-final/source-hashes.csv'),
    (Join-Path $taskOutput 'VerifiedCatalog/source-hashes.csv'),
    (Join-Path $taskOutput 'VerifiedCatalog/upgrade-source-hashes.csv'),
    (Join-Path $taskArtifacts 'Compile/source-hashes.csv'),
    (Join-Path $taskArtifacts 'Final/source-hashes.csv')
)
$taskChecked = 0
foreach ($taskManifest in $taskManifests) {
    foreach ($taskRow in Import-Csv -LiteralPath $taskManifest) {
        if ((Get-FileHash -LiteralPath $taskRow.Source).Hash -ne $taskRow.SHA256) {
            throw ('Current source differs from validated source: ' + $taskRow.Source)
        }
        if ($taskRow.PSObject.Properties.Name -contains 'Snapshot') {
            if ((Get-FileHash -LiteralPath $taskRow.Snapshot).Hash -ne $taskRow.SHA256) {
                throw ('Frozen source changed: ' + $taskRow.Snapshot)
            }
        }
        $taskChecked++
    }
}
$taskTestResults = foreach ($taskPair in @(
    @('Core','Final/results.xml'), @('UI-EditMode','UI/EditMode.xml'), @('UI-PlayMode','UI/PlayMode.xml')
)) {
    $taskXml = [xml](Get-Content -LiteralPath (Join-Path $taskArtifacts $taskPair[1]) -Raw)
    $taskSuite = $taskXml.DocumentElement
    if ($taskSuite.result -ne 'Passed' -or [int]$taskSuite.failed -ne 0) { throw ('Failed validation: ' + $taskPair[0]) }
    [pscustomobject]@{ Suite=$taskPair[0]; Total=[int]$taskSuite.total; Passed=[int]$taskSuite.passed; Failed=[int]$taskSuite.failed }
}
$taskSamples = @(Import-Csv -LiteralPath (Join-Path $taskOutput 'final-all16/samples.csv'))
$taskBaseline = @(Import-Csv -LiteralPath (Join-Path $taskOutput 'before-all16/samples.csv'))
if ($taskSamples.Count -ne 5824 -or $taskBaseline.Count -ne 5824) { throw 'Expected 5824 paired samples in each run.' }
function Get-TaskKey($Row) { return @($Row.target,$Row.school,$Row.policy,$Row.seed) -join '|' }
$taskBaselineMap = @{}
foreach ($taskRow in $taskBaseline) { $taskBaselineMap.Add((Get-TaskKey $taskRow),$taskRow) }
$taskKeys = @{}
foreach ($taskRow in $taskSamples) {
    $taskKey = Get-TaskKey $taskRow
    if (!$taskBaselineMap.ContainsKey($taskKey) -or $taskKeys.ContainsKey($taskKey)) { throw ('Unpaired or duplicate sample: ' + $taskKey) }
    $taskKeys.Add($taskKey,$true)
    $taskOld = $taskBaselineMap[$taskKey]
    foreach ($taskProperty in @('loadout','tier','maxHP','initialHP','navalDeckSize')) {
        if ($taskRow.$taskProperty -ne $taskOld.$taskProperty) { throw ('Changed player input: ' + $taskKey + ' / ' + $taskProperty) }
    }
}
foreach ($taskFile in @('calibration-loadouts.csv','decks.csv','used-decks.csv')) {
    if ((Get-FileHash -LiteralPath (Join-Path $taskOutput ('final-all16/'+$taskFile))).Hash -ne
        (Get-FileHash -LiteralPath (Join-Path $taskOutput ('before-all16/'+$taskFile))).Hash) { throw ('Player loadouts/decks changed: '+$taskFile) }
}
$taskCoreAudit = @(Import-Csv -LiteralPath (Join-Path $taskOutput 'production-source-audit.csv'))
foreach ($taskRow in $taskCoreAudit) {
    if ((Get-FileHash -LiteralPath (Join-Path $taskRoot ('UnityClient/Assets/_Project/Core/Runtime/'+$taskRow.File))).Hash -ne $taskRow.AfterSHA256) { throw ('Source audit stale: '+$taskRow.File) }
}
$taskCardAudit = Get-Content -LiteralPath (Join-Path $taskOutput 'VerifiedCatalog/catalog-integrity.json') -Raw | ConvertFrom-Json
$taskSummary = [ordered]@{
    VerifiedAtUtc=[DateTime]::UtcNow.ToString('o'); SourceHashChecks=$taskChecked
    Tests=@($taskTestResults); PairedSamples=$taskSamples.Count
    Outcomes=@($taskSamples | Group-Object outcome | ForEach-Object { [pscustomobject]@{Outcome=$_.Name;Count=$_.Count} })
    ChangedCoreFiles=@($taskCoreAudit | Where-Object Change -ne 'Unchanged' | Select-Object File,Change)
    SamePlayerLoadoutsAndDecks=$true; CardIntegrity=$taskCardAudit
}
$taskSummary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $taskOutput 'final-validation.json') -Encoding utf8
Write-Output ('PASS final validation: '+$taskChecked+' current/frozen source checks; 5824 strictly paired samples; identical player inputs.')
$taskTestResults | Format-Table
