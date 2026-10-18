param([Parameter(Mandatory=$true)][ValidateSet('Sync','Edit','Play','Read','Summary')][string]$Stage,
    [string]$Run = 'First', [string]$TestFilter)
$ErrorActionPreference = 'Stop'
if ($Run -notmatch '^[A-Za-z0-9_-]+$') { throw 'Use a simple validation run name.' }
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = Join-Path $workspace 'Artifacts/NavalArrivalContact20261016'
$output = Join-Path $evidence $Run
if ($Stage -eq 'Sync') {
    & (Join-Path $PSScriptRoot '../NavalTactics20261015/SyncValidation.ps1') -Scope Code
    exit $LASTEXITCODE
}
if ($Stage -in @('Edit','Play')) {
    if (Test-Path -LiteralPath $output) { throw 'Preserve previous run evidence; choose a new Run.' }
    if (-not $TestFilter) {
        $TestFilter = if ($Stage -eq 'Edit') {
            'CoreNavalArrivalContactTests;CoreNavalActionMovementTests;CoreNavalContactRulesTests;CoreGuidedBossScriptTests'
        } else {
            'NavalArrivalContact_;NavalSpecialActions_;NavalTactics_ManualRouteUsesAllPointsAndRequiresLegalArrivalHeading;NavalActionHud_ImmediateCardsRefreshPointsAndLockThroughArrivalHeading;NavalTactics_GuidedCourseAllowsAlternateHeadingsAndBothRealContacts;GuidedTutorial_TwoVoyagesTeachNavigatorAmbushThenScriptedBossAndDisplayReward;GuidedBossScript_PauseDuringRealRamResumesCommittedLessonWithoutReplay'
        }
    }
    & (Join-Path $PSScriptRoot '../NavalTactics20261015/RunValidation.ps1') -Stage $Stage -Output $output -TestFilter $TestFilter
    exit $LASTEXITCODE
}
if ($Stage -eq 'Read') {
    $reports = @(Get-ChildItem -LiteralPath $output -Filter '*-results.xml' -File)
    if (-not $reports.Count) {
        Get-ChildItem -LiteralPath $output -Filter '*.log' -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Tail 18 }
        exit 0
    }
    foreach ($report in $reports) {
        [xml]$xml = Get-Content -LiteralPath $report.FullName -Raw
        $runNode = $xml.'test-run'
        [pscustomobject]@{ Report=$report.FullName; Result=$runNode.result; Total=$runNode.total; Passed=$runNode.passed; Failed=$runNode.failed } | ConvertTo-Json
        foreach ($test in $xml.SelectNodes('//test-case')) {
            if ($test.result -ne 'Passed') { Write-Output ($test.fullname + ': ' + $test.result); Write-Output $test.failure.message; Write-Output $test.failure.'stack-trace' }
        }
    }
    exit 0
}
$cases = @{}
$reports = @(Get-ChildItem -LiteralPath $evidence -Filter '*-results.xml' -Recurse -File | Where-Object { $_.Name -in @('edit-results.xml','play-results.xml') } | Sort-Object LastWriteTimeUtc)
if (-not $reports.Count) { throw 'No validation reports.' }
foreach ($report in $reports) {
    [xml]$xml = Get-Content -LiteralPath $report.FullName -Raw
    $suite = if ($report.Name -eq 'edit-results.xml') { 'UnityEditMode' } else { 'UnityPlayMode' }
    foreach ($test in $xml.SelectNodes('//test-case')) {
        $cases[$suite + ':' + $test.fullname] = [pscustomobject]@{ Suite=$suite; Name=$test.fullname; Result=$test.result; Report=$report.FullName }
    }
}
if (@($cases.Values | Where-Object Result -ne 'Passed').Count) { throw 'Latest validation results include non-passing tests.' }
$suites = @($cases.Values | Group-Object Suite | ForEach-Object { [pscustomobject]@{ Suite=$_.Name; Total=$_.Count; Passed=$_.Count } })
if ($suites.Count -ne 2) { throw 'Both Unity test suites are required.' }
$coreReport = Join-Path $evidence 'Core/results.xml'
[xml]$coreXml = Get-Content -LiteralPath $coreReport -Raw
$coreCases = @($coreXml.SelectNodes('//test-case'))
if (-not $coreCases.Count -or @($coreCases | Where-Object { $_.result -ne 'Passed' }).Count) { throw 'Core standalone validation is incomplete.' }
$suites += [pscustomobject]@{ Suite='CoreStandalone'; Total=$coreCases.Count; Passed=$coreCases.Count }
$cases.Values | Sort-Object Suite,Name | Export-Csv -LiteralPath (Join-Path $evidence 'verification-cases.csv') -NoTypeInformation -Encoding utf8
[pscustomobject]@{ Result='PASS'; RecordedUtc=[DateTime]::UtcNow.ToString('o'); Scope='Targeted arrival contact, movement, contact rules and tutorial regression; suites overlap, not a full-project run.'; Method='Latest result per test and suite; intermediate reports retained.'; Suites=$suites; Reports=@($reports.FullName) + $coreReport } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $evidence 'verification-summary.json') -Encoding utf8
Get-Content -LiteralPath (Join-Path $evidence 'verification-summary.json')
