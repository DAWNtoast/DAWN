param(
    [ValidateSet('Prepare','Compile','EditMode','PlayMode','Capture')][string]$Stage = 'Prepare',
    [string]$RunName = 'run1',
    [string]$TestFilter = ''
)
$ErrorActionPreference = 'Stop'
$treasureSource = 'D:\Game\UnityClient'
$treasureProject = [IO.Path]::GetFullPath('D:\Game\tmp\departure-treasure-inline-20261014')
$treasureWarm = 'D:\Game\tmp\prefab-runtime-20261014\Library'
$treasureRoot = 'D:\Game\Artifacts\DepartureTreasureInline20261014\validation'
$treasureUnity = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
if ($RunName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'RunName must be a simple identifier.' }
if (@(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($treasureProject) }).Count) { throw 'Isolated project is already running.' }
$treasureUnityLock = Join-Path $treasureProject 'Temp\UnityLockfile'
if (Test-Path -LiteralPath $treasureUnityLock) {
    try { $treasureCheck = [IO.File]::Open($treasureUnityLock, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None); $treasureCheck.Dispose() }
    catch { throw 'The isolated Unity project is already open.' }
}
[IO.Directory]::CreateDirectory($treasureProject) | Out-Null
[IO.Directory]::CreateDirectory($treasureRoot) | Out-Null
$treasureLease = $null
try {
    $treasureLease = [IO.File]::Open((Join-Path $treasureProject 'DepartureTreasureInline20261014.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    if ($Stage -eq 'Prepare') {
        if (-not (Test-Path -LiteralPath (Join-Path $treasureProject 'Library'))) {
            if (-not (Test-Path -LiteralPath $treasureWarm)) { throw 'Expected read-only warm cache is missing.' }
            & robocopy $treasureWarm (Join-Path $treasureProject 'Library') /E /R:1 /W:1 /NFL /NDL /NP /NJH /NJS /XD 'Bee' 'TempArtifacts' /XF '*-lock' 'ilpp.pid' | Out-Null
            if ($LASTEXITCODE -ge 8) { throw 'Warm Library copy failed.' }
        }
        foreach ($treasurePart in @('Assets','Packages','ProjectSettings')) {
            $treasureCopy = @((Join-Path $treasureSource $treasurePart), (Join-Path $treasureProject $treasurePart), '/E','/R:1','/W:1','/NFL','/NDL','/NP','/NJH','/NJS')
            if ($treasurePart -eq 'Assets') { $treasureCopy += @('/XD', (Join-Path $treasureSource 'Assets\_Project\Scenes\UIPreviewsForMe'), '/XF', 'UIPreviewsForMe.meta') }
            & robocopy @treasureCopy | Out-Null
            if ($LASTEXITCODE -ge 8) { throw ('Copy failed: ' + $treasurePart) }
        }
        $treasureScratch = [IO.Path]::GetFullPath((Join-Path $treasureProject 'Assets\_Project'))
        foreach ($treasureOld in @(Get-ChildItem -LiteralPath $treasureScratch -Filter '*.cs' -File -Recurse)) {
            $treasureRelative = $treasureOld.FullName.Substring($treasureScratch.Length + 1)
            if (Test-Path -LiteralPath (Join-Path $treasureSource ('Assets\_Project\' + $treasureRelative))) { continue }
            $treasureResolved = [IO.Path]::GetFullPath($treasureOld.FullName)
            if (-not $treasureResolved.StartsWith($treasureScratch + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Stale source is outside scratch project.' }
            Remove-Item -LiteralPath $treasureResolved -Force
            if (Test-Path -LiteralPath ($treasureResolved + '.meta')) { Remove-Item -LiteralPath ($treasureResolved + '.meta') -Force }
        }
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Capture.cs') -Destination (Join-Path $treasureProject 'Assets\_Project\Editor\DepartureTreasureInlineCapture20261014.cs') -Force
        $treasureSettingsPath = Join-Path $treasureProject 'ProjectSettings\ProjectSettings.asset'
        $treasureSettings = [IO.File]::ReadAllText($treasureSettingsPath)
        $treasureSettings = [regex]::Replace($treasureSettings, '(?m)^  companyName:.*$', '  companyName: DepartureTreasureInlineValidation20261014')
        $treasureSettings = [regex]::Replace($treasureSettings, '(?m)^  productName:.*$', '  productName: DepartureTreasureInlineValidation20261014')
        [IO.File]::WriteAllText($treasureSettingsPath, $treasureSettings, [Text.UTF8Encoding]::new($false))
        Write-Output 'Prepared isolated source and assets with separate PlayerPrefs identity; no Unity launch or scenes generated.'
        exit 0
    }
    if (-not (Test-Path -LiteralPath (Join-Path $treasureProject 'Assets\_Project\Editor\DepartureTreasureInlineCapture20261014.cs'))) { throw 'Run Prepare first.' }
    $treasureOutput = Join-Path $treasureRoot $RunName
    if (Test-Path -LiteralPath $treasureOutput) { throw 'Use a fresh RunName to preserve output.' }
    [IO.Directory]::CreateDirectory($treasureOutput) | Out-Null
    $treasureArgs = @('-batchmode','-force-d3d11','-projectPath',$treasureProject,'-logFile',(Join-Path $treasureOutput ($Stage + '.log')))
    if ($Stage -eq 'Capture') {
        $treasureArgs += @('-quit','-executeMethod','ShatteredSail.Editor.DepartureTreasureInlineCapture20261014.Run','-departureTreasureInlineOutput',(Join-Path $treasureOutput 'Screens'))
    } elseif ($Stage -eq 'Compile') {
        $treasureArgs += '-quit'
    } else {
        if (-not $TestFilter) {
            $TestFilter = if ($Stage -eq 'EditMode') { 'DepartureTreasureSeedSelectionTests;DepartureDockSceneLayoutTests;VoyageDepartureShipLayoutTests' } else { 'DepartureDockTreasure_;TreasureFragments_IdentifyAssembleAndChooseSeedFromDeparture' }
        }
        $treasureArgs += @('-runTests','-testPlatform',$Stage,'-testFilter',$TestFilter,'-testResults',(Join-Path $treasureOutput 'results.xml'))
        if ($Stage -eq 'PlayMode') { $treasureArgs += @('-navalVisualCapturePath',(Join-Path $treasureOutput 'LiveScreens')) }
    }
    $treasureQuoted = $treasureArgs | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }
    $treasureProcess = Start-Process -FilePath $treasureUnity -WindowStyle Hidden -ArgumentList $treasureQuoted -PassThru
    [pscustomobject]@{ Stage=$Stage; ProcessId=$treasureProcess.Id; Project=$treasureProject; Arguments=$treasureArgs } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $treasureOutput 'launch.json') -Encoding utf8
    Write-Output ('Unity ' + $Stage + ' PID ' + $treasureProcess.Id + '; output=' + $treasureOutput)
    $treasureProcess.WaitForExit()
    $treasureProcess.Refresh()
    if ($treasureProcess.ExitCode -ne 0) { throw ('Unity failed with exit ' + $treasureProcess.ExitCode + '; inspect ' + $treasureOutput) }
    if ($Stage -eq 'EditMode' -or $Stage -eq 'PlayMode') {
        [xml]$treasureResults = Get-Content -Raw -LiteralPath (Join-Path $treasureOutput 'results.xml')
        if ($treasureResults.'test-run'.result -ne 'Passed') { throw ('Tests failed; inspect ' + $treasureOutput) }
        Write-Output ('Passed ' + $treasureResults.'test-run'.passed + ' tests.')
    }
    Write-Output ('Completed ' + $Stage + ': ' + $treasureOutput)
} finally {
    if ($treasureLease) { $treasureLease.Dispose() }
}
