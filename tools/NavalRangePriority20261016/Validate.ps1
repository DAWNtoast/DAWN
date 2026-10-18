param([Parameter(Mandatory=$true)][ValidateSet('Sync','Edit','Play','Read','Summary')][string]$Stage,
    [string]$Run = 'First')
$ErrorActionPreference = 'Stop'
if ($Run -notmatch '^[A-Za-z0-9_-]+$') { throw 'Use a simple run name.' }
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = Join-Path $workspace 'Artifacts/NavalRangePriority20261016'
$output = Join-Path $evidence $Run
if ($Stage -eq 'Sync') {
    & (Join-Path $PSScriptRoot '../NavalTactics20261015/SyncValidation.ps1') -Scope Code
    exit $LASTEXITCODE
}
if ($Stage -in @('Edit','Play')) {
    if (Test-Path -LiteralPath $output) { throw 'Preserve prior evidence; choose a new run.' }
    $filter = if ($Stage -eq 'Edit') { 'NavalTacticalAttackRangeGraphicTests' } else {
        'NavalAttackRange_;NavalDragRoute_ShipClickShowsWholeReachableAreaAlongsideAttackAndCardHover;NavalDragRoute_DisableEnableCancelsGestureAndRestoresPreviewControls'
    }
    & (Join-Path $PSScriptRoot '../NavalTactics20261015/RunValidation.ps1') -Stage $Stage -Output $output -TestFilter $filter
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
$reports = @(Get-ChildItem -LiteralPath $evidence -Filter '*-results.xml' -Recurse -File | Sort-Object LastWriteTimeUtc)
foreach ($report in $reports) {
    [xml]$xml = Get-Content -LiteralPath $report.FullName -Raw
    $suite = if ($report.Name -eq 'edit-results.xml') { 'UnityEditMode' } else { 'UnityPlayMode' }
    foreach ($test in $xml.SelectNodes('//test-case')) { $cases[$suite + ':' + $test.fullname] = [pscustomobject]@{ Suite=$suite; Name=$test.fullname; Result=$test.result; Report=$report.FullName } }
}
if (@($cases.Values | Where-Object Result -ne 'Passed').Count) { throw 'Latest results contain non-passing tests.' }
$suites = @($cases.Values | Group-Object Suite | ForEach-Object { [pscustomobject]@{ Suite=$_.Name; Total=$_.Count; Passed=$_.Count } })
if ($suites.Count -ne 2) { throw 'Both Unity suites must pass.' }
$cases.Values | Sort-Object Suite,Name | Export-Csv -LiteralPath (Join-Path $evidence 'verification-cases.csv') -NoTypeInformation -Encoding utf8
[pscustomobject]@{ Result='PASS'; RecordedUtc=[DateTime]::UtcNow.ToString('o'); Scope='Existing attack-range rendering and drag selection regression; not full-project validation. Overlap colors additionally checked in real scene screenshots.'; Suites=$suites; Reports=$reports.FullName } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $evidence 'verification-summary.json') -Encoding utf8
Get-Content -LiteralPath (Join-Path $evidence 'verification-summary.json')
