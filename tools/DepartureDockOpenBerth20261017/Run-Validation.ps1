param(
    [ValidateSet('Stage', 'EditMode', 'PlayMode', 'Capture')][string]$Mode = 'Stage',
    [string]$IsolatedProject = 'D:/Game/tmp/departure-chart-inline-20261014',
    [string]$Output = 'D:/Game/Artifacts/DepartureDockOpenBerth20261017/Validation',
    [switch]$IsolateUnrelatedTestCompileFailure,
    [ValidatePattern('^(all|\d+(,\d+)*)$')][string]$CapturePorts = 'all',
    [ValidatePattern('^(all|\d+(,\d+)*)$')][string]$CaptureShips = 'all'
)
$ErrorActionPreference = 'Stop'
$dockRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\', '/')
$dockSource = Join-Path $dockRoot 'UnityClient'
$dockTarget = [IO.Path]::GetFullPath($IsolatedProject).TrimEnd('\', '/')
$dockOutput = [IO.Path]::GetFullPath($Output).TrimEnd('\', '/')
$dockTempRoot = [IO.Path]::GetFullPath((Join-Path $dockRoot 'tmp')).TrimEnd('\', '/') + '\'
if (-not $dockTarget.StartsWith($dockTempRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Use a disposable project under this repository tmp directory.' }
if (-not $dockOutput.StartsWith($dockRoot + '\Artifacts\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Write validation output only under repository Artifacts.' }
if (-not (Test-Path -LiteralPath (Join-Path $dockTarget 'Library'))) { throw 'Expected an existing cached validation project.' }
$dockEditors = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object {
    $_.ExecutablePath -match '[\\/]Editor[\\/]Unity\.exe$' -or $_.CommandLine -match '(?i)-projectPath\s'
})
if ($dockEditors.Count -gt 0) { throw ('Leave the running Unity Editor untouched: ' + ($dockEditors.ProcessId -join ', ')) }
[IO.Directory]::CreateDirectory($dockOutput) | Out-Null

function Sync-DockDirectory([string]$Relative) {
    $dockFrom = Join-Path $dockSource $Relative
    $dockTo = Join-Path $dockTarget $Relative
    & robocopy $dockFrom $dockTo /E /COPY:DAT /DCOPY:DAT /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Failed to stage $Relative" }
    # Remove obsolete files only inside this explicitly validated disposable target.
    # This prevents yesterday's capture/test scripts from compiling against today's runtime.
    foreach ($dockFile in Get-ChildItem -LiteralPath $dockTo -File -Recurse) {
        $dockAbsolute = [IO.Path]::GetFullPath($dockFile.FullName)
        if (-not $dockAbsolute.StartsWith($dockTarget + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected staging path.' }
        $dockSuffix = $dockAbsolute.Substring($dockTo.Length).TrimStart('\', '/')
        if (-not (Test-Path -LiteralPath (Join-Path $dockFrom $dockSuffix))) { Remove-Item -LiteralPath $dockAbsolute }
    }
}
# Never synchronize any preview scene directory or run its retired generators.
foreach ($dockRelative in @('Packages', 'ProjectSettings', 'Assets/_Project/Core', 'Assets/_Project/Content',
    'Assets/_Project/Persistence', 'Assets/_Project/Presentation/Runtime', 'Assets/_Project/Editor',
    'Assets/_Project/Tests', 'Assets/_Project/Resources')) { Sync-DockDirectory $dockRelative }
foreach ($dockScene in @('MainMenu', 'CoreGameplayDemo', 'TutorialVoyage')) {
    foreach ($dockExtension in @('.unity', '.unity.meta')) {
        $dockRelative = 'Assets/_Project/Scenes/' + $dockScene + $dockExtension
        Copy-Item -LiteralPath (Join-Path $dockSource $dockRelative) -Destination (Join-Path $dockTarget $dockRelative) -Force
    }
}
$dockCaptureSource = Join-Path $PSScriptRoot 'DepartureDockOpenBerthCapture.cs'
$dockCaptureTarget = Join-Path $dockTarget 'Assets/_Project/Editor/DepartureDockOpenBerthCapture.cs'
Copy-Item -LiteralPath $dockCaptureSource -Destination $dockCaptureTarget -Force
$dockExclusions = @()
if ($IsolateUnrelatedTestCompileFailure) {
    $dockExcludedRelative = 'Assets/_Project/Tests/EditMode/CoreBattleCardDeduplicationTests.cs'
    $dockExcluded = [IO.Path]::GetFullPath((Join-Path $dockTarget $dockExcludedRelative))
    $dockQuarantine = [IO.Path]::GetFullPath((Join-Path $dockTarget 'ValidationExcluded'))
    if (-not $dockExcluded.StartsWith($dockTarget + '\', [StringComparison]::OrdinalIgnoreCase) -or
        -not $dockQuarantine.StartsWith($dockTarget + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid exclusion path.' }
    [IO.Directory]::CreateDirectory($dockQuarantine) | Out-Null
    $dockExclusions += [ordered]@{ path = $dockExcludedRelative; sourceSha256 = (Get-FileHash -LiteralPath (Join-Path $dockSource $dockExcludedRelative)).Hash;
        reason = 'Existing unrelated CS1503 int-to-string errors at line 273; excluded only from the disposable focused-test project.' }
    Move-Item -LiteralPath $dockExcluded -Destination (Join-Path $dockQuarantine 'CoreBattleCardDeduplicationTests.cs') -Force
    if (Test-Path -LiteralPath ($dockExcluded + '.meta')) { Move-Item -LiteralPath ($dockExcluded + '.meta') -Destination (Join-Path $dockQuarantine 'CoreBattleCardDeduplicationTests.cs.meta') -Force }
}
$dockMetadataRepairs = @()
$dockMetadataRelative = 'Assets/_Project/Presentation/Runtime/SettlementFireworksGraphic.cs.meta'
$dockMetadataPath = Join-Path $dockTarget $dockMetadataRelative
$dockInvalidGuid = '70ce14ca94d44bffb754927cc2fa421e5'
$dockTemporaryGuid = '70ce14ca94d44bffb754927cc2fa421e'
if (Test-Path -LiteralPath $dockMetadataPath) {
    $dockMetadata = Get-Content -LiteralPath $dockMetadataPath -Raw
    if ($dockMetadata.Contains('guid: ' + $dockInvalidGuid)) {
        $dockMetadataRepairs += [ordered]@{ path = $dockMetadataRelative; originalGuid = $dockInvalidGuid; temporaryGuid = $dockTemporaryGuid;
            reason = 'Known unrelated 33-character GUID makes Unity ignore the fireworks script. Normalize only the disposable copy; production metadata remains untouched.';
            sourceSha256 = (Get-FileHash -LiteralPath (Join-Path $dockSource $dockMetadataRelative)).Hash }
        [IO.File]::WriteAllText($dockMetadataPath, $dockMetadata.Replace('guid: ' + $dockInvalidGuid, 'guid: ' + $dockTemporaryGuid), [Text.UTF8Encoding]::new($false))
    }
}
$dockSettingsPath = Join-Path $dockTarget 'ProjectSettings/ProjectSettings.asset'
$dockSettings = Get-Content -LiteralPath $dockSettingsPath -Raw
$dockSettings = $dockSettings -replace '(?m)^  productName:.*$', '  productName: ShatteredSail_DepartureDockOpenBerthValidation'
[IO.File]::WriteAllText($dockSettingsPath, $dockSettings, [Text.UTF8Encoding]::new($false))

$dockManifest = [Collections.Generic.List[object]]::new()
$dockTracked = @(Get-ChildItem -LiteralPath (Join-Path $dockSource 'Assets/_Project/Presentation/Runtime') -File | Where-Object { $_.Name -match 'Departure|ShipWaterline' })
$dockTracked += @(Get-ChildItem -LiteralPath (Join-Path $dockSource 'Assets/_Project/Resources/PortFacilities/Regional/Voyage') -File)
$dockTracked += @(Get-ChildItem -LiteralPath (Join-Path $dockSource 'Assets/_Project/Tests') -Recurse -File | Where-Object { $_.Name -match 'Departure.*\.cs$' })
foreach ($dockFile in $dockTracked) {
    $dockRelative = $dockFile.FullName.Substring($dockSource.Length).TrimStart('\', '/')
    $dockStaged = Join-Path $dockTarget $dockRelative
    $dockSourceHash = (Get-FileHash -LiteralPath $dockFile.FullName -Algorithm SHA256).Hash
    $dockStagedHash = (Get-FileHash -LiteralPath $dockStaged -Algorithm SHA256).Hash
    if ($dockSourceHash -ne $dockStagedHash) { throw "Staging hash mismatch: $dockRelative" }
    $dockManifest.Add([ordered]@{ path = $dockRelative; sha256 = $dockSourceHash; stagedSha256 = $dockStagedHash })
}
$dockManifest.Add([ordered]@{ path = 'tools/DepartureDockOpenBerth20261017/DepartureDockOpenBerthCapture.cs'; sha256 = (Get-FileHash -LiteralPath $dockCaptureSource).Hash; stagedSha256 = (Get-FileHash -LiteralPath $dockCaptureTarget).Hash })
[ordered]@{ mode = $Mode; stagedUtc = [DateTime]::UtcNow.ToString('o'); source = $dockSource; project = $dockTarget; exclusions = $dockExclusions; temporaryMetadataRepairs = $dockMetadataRepairs; files = $dockManifest } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $dockOutput 'stage-manifest.json') -Encoding utf8
if ($Mode -eq 'Stage') { Write-Output "Current code and resources staged. $dockOutput/stage-manifest.json"; exit 0 }

$dockUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe'
$dockLog = Join-Path $dockOutput ($Mode.ToLowerInvariant() + '-editor.log')
if (Test-Path -LiteralPath $dockLog) { Copy-Item -LiteralPath $dockLog -Destination ($dockLog + '.' + [DateTime]::UtcNow.ToString('yyyyMMddHHmmss') + '.previous') }
$dockArguments = @('-batchmode', '-projectPath', $dockTarget, '-logFile', $dockLog)
if ($Mode -eq 'Capture') {
    $dockPreviousReport = Join-Path $dockOutput 'screenshots/capture-report.json'
    if (Test-Path -LiteralPath $dockPreviousReport) { Remove-Item -LiteralPath $dockPreviousReport }
    $dockArguments += @('-quit', '-executeMethod', 'ShatteredSail.Editor.DepartureDockOpenBerthCapture.Capture', '-dockCaptureOutput', (Join-Path $dockOutput 'screenshots'),
        '-dockCapturePorts', $CapturePorts, '-dockCaptureShips', $CaptureShips)
} else {
    $dockFilter = if ($Mode -eq 'EditMode') {
        'ShatteredSail.Presentation.Tests.DepartureDockSceneLayoutTests;ShatteredSail.Presentation.Tests.PortFacilityArtCatalogLazyTests;ShatteredSail.Presentation.Tests.VoyageDepartureShipLayoutTests;ShatteredSail.Presentation.Tests.DepartureTreasureSeedSelectionTests'
    } else { 'DepartureShips_;DepartureDockRetained_' }
    $dockResults = Join-Path $dockOutput ($Mode.ToLowerInvariant() + '-results.xml')
    if (Test-Path -LiteralPath $dockResults) {
        Copy-Item -LiteralPath $dockResults -Destination ($dockResults + '.' + [DateTime]::UtcNow.ToString('yyyyMMddHHmmss') + '.previous')
        Remove-Item -LiteralPath $dockResults
    }
    $dockArguments += @('-runTests', '-testPlatform', $Mode, '-testFilter', $dockFilter, '-testResults', $dockResults)
    if ($Mode -eq 'EditMode') { $dockArguments += '-nographics' }
}
$dockProcess = Start-Process -FilePath $dockUnity -ArgumentList $dockArguments -WindowStyle Hidden -PassThru
$dockProcess.WaitForExit()
if ($dockProcess.ExitCode -ne 0) { throw "Unity $Mode failed: $($dockProcess.ExitCode). Inspect $dockLog" }
if ($Mode -eq 'Capture') {
    $dockReportPath = Join-Path $dockOutput 'screenshots/capture-report.json'
    if (-not (Test-Path -LiteralPath $dockReportPath)) { throw 'Capture did not produce a report.' }
    $dockReport = Get-Content -LiteralPath $dockReportPath -Raw | ConvertFrom-Json
    if ($dockReport.expectedPages -lt 1 -or $dockReport.pages.Count -ne $dockReport.expectedPages -or @($dockReport.pages | Where-Object { -not $_.passed }).Count -gt 0) { throw 'Expected every selected current-source port/vessel capture to pass.' }
    Write-Output "Captured all $($dockReport.pages.Count) selected port/vessel combinations. $dockReportPath"
} else {
    if (-not (Test-Path -LiteralPath $dockResults)) { throw 'Unity did not produce a test report.' }
    [xml]$dockReport = Get-Content -LiteralPath $dockResults -Raw
    if ($dockReport.'test-run'.result -ne 'Passed' -or [int]$dockReport.'test-run'.total -eq 0) { throw "Unity reported failed or empty tests; inspect $dockResults" }
    Write-Output "$Mode passed: $($dockReport.'test-run'.total) tests. $dockResults"
}
