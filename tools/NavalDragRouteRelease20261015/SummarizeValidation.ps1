$ErrorActionPreference = 'Stop'
$root = 'D:/Game/Artifacts/NavalDragRoute20261015'
$runs = [ordered]@{
    CoreStandalone = @('D:/Game/Artifacts/NavalTactics20261015/DragRouteCore/results.xml')
    UnityEditMode = @((Join-Path $root 'EditFirst/edit-results.xml'))
    UnityPlayMode = @((Join-Path $root 'PlayFirst/play-results.xml'),(Join-Path $root 'PlayVerified/play-results.xml'),(Join-Path $root 'PlayManualFinal/play-results.xml'))
}
$cases = @(); $suites = @()
foreach ($suite in $runs.Keys) {
    $latest = @{}
    foreach ($file in $runs[$suite]) {
        [xml]$report = Get-Content -LiteralPath $file -Raw
        foreach ($case in $report.SelectNodes('//test-case')) {
            $latest[$case.fullname] = [pscustomobject]@{Suite=$suite;Name=[string]$case.fullname;Result=[string]$case.result;Report=$file}
        }
    }
    $rows = @($latest.Values | Sort-Object Name)
    $cases += $rows
    $passed = @($rows | Where-Object Result -eq 'Passed').Count
    if (-not $rows.Count -or $passed -ne $rows.Count) { throw ('Unresolved validation in ' + $suite) }
    $suites += [pscustomobject]@{Suite=$suite;Total=$rows.Count;Passed=$passed;Reports=$runs[$suite]}
}
$cases | Export-Csv -LiteralPath (Join-Path $root 'verification-cases.csv') -NoTypeInformation -Encoding utf8
$summary = [pscustomobject]@{Result='PASS';RecordedUtc=[DateTime]::UtcNow.ToString('o');Scope='Targeted drag route, movement/contact/range, tutorial and UI regressions; not a full-project test run.';Method='Latest result for each test full name; failed intermediate reports retained.';Suites=$suites}
$summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $root 'verification-summary.json') -Encoding utf8
$summary | ConvertTo-Json -Depth 6
