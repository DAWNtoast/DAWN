param(
    [ValidateSet('Prepare','Compile','EditMode','PlayMode','Capture')][string]$Stage = 'Prepare',
    [string]$RunName = 'run1',
    [string]$TestFilter = ''
)
$ErrorActionPreference = 'Stop'
$chartSource = 'D:\Game\UnityClient'
$chartProject = [IO.Path]::GetFullPath('D:\Game\tmp\departure-chart-inline-20261014')
$chartWarm = 'D:\Game\tmp\prefab-runtime-20261014\Library'
$chartRoot = 'D:\Game\Artifacts\DepartureChartInline20261014\validation'
$chartUnity = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
if ($RunName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'RunName must be a simple identifier.' }
if (@(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($chartProject) }).Count) { throw 'Isolated project is already running.' }
$chartUnityLock = Join-Path $chartProject 'Temp\UnityLockfile'
if (Test-Path -LiteralPath $chartUnityLock) {
    try { $chartCheck = [IO.File]::Open($chartUnityLock, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None); $chartCheck.Dispose() }
    catch { throw 'The isolated Unity project is already open.' }
}
[IO.Directory]::CreateDirectory($chartProject) | Out-Null
[IO.Directory]::CreateDirectory($chartRoot) | Out-Null
$chartLease = $null
try {
    $chartLease = [IO.File]::Open((Join-Path $chartProject 'DepartureChartInline20261014.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    if ($Stage -eq 'Prepare') {
        if (-not (Test-Path -LiteralPath (Join-Path $chartProject 'Library'))) {
            if (-not (Test-Path -LiteralPath $chartWarm)) { throw 'Expected read-only warm cache is missing.' }
            & robocopy $chartWarm (Join-Path $chartProject 'Library') /E /R:1 /W:1 /NFL /NDL /NP /NJH /NJS /XD 'Bee' 'TempArtifacts' /XF '*-lock' 'ilpp.pid' | Out-Null
            if ($LASTEXITCODE -ge 8) { throw 'Warm Library copy failed.' }
        }
        foreach ($chartPart in @('Assets','Packages','ProjectSettings')) {
            $chartCopy = @((Join-Path $chartSource $chartPart), (Join-Path $chartProject $chartPart), '/E','/R:1','/W:1','/NFL','/NDL','/NP','/NJH','/NJS')
            if ($chartPart -eq 'Assets') { $chartCopy += @('/XD', (Join-Path $chartSource 'Assets\_Project\Scenes\UIPreviewsForMe'), '/XF', 'UIPreviewsForMe.meta') }
            & robocopy @chartCopy | Out-Null
            if ($LASTEXITCODE -ge 8) { throw ('Copy failed: ' + $chartPart) }
        }
        $chartScratch = [IO.Path]::GetFullPath((Join-Path $chartProject 'Assets\_Project'))
        foreach ($chartOld in @(Get-ChildItem -LiteralPath $chartScratch -Filter '*.cs' -File -Recurse)) {
            $chartRelative = $chartOld.FullName.Substring($chartScratch.Length + 1)
            if (Test-Path -LiteralPath (Join-Path $chartSource ('Assets\_Project\' + $chartRelative))) { continue }
            $chartResolved = [IO.Path]::GetFullPath($chartOld.FullName)
            if (-not $chartResolved.StartsWith($chartScratch + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Stale source is outside scratch project.' }
            Remove-Item -LiteralPath $chartResolved -Force
            if (Test-Path -LiteralPath ($chartResolved + '.meta')) { Remove-Item -LiteralPath ($chartResolved + '.meta') -Force }
        }
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Capture.cs') -Destination (Join-Path $chartProject 'Assets\_Project\Editor\DepartureChartInlineCapture20261014.cs') -Force
        $chartSettingsPath = Join-Path $chartProject 'ProjectSettings\ProjectSettings.asset'
        $chartSettings = [IO.File]::ReadAllText($chartSettingsPath)
        $chartSettings = [regex]::Replace($chartSettings, '(?m)^  companyName:.*$', '  companyName: DepartureChartInlineValidation20261014')
        $chartSettings = [regex]::Replace($chartSettings, '(?m)^  productName:.*$', '  productName: DepartureChartInlineValidation20261014')
        [IO.File]::WriteAllText($chartSettingsPath, $chartSettings, [Text.UTF8Encoding]::new($false))
        Write-Output 'Prepared isolated source and assets with separate PlayerPrefs identity; no Unity launch or scenes generated.'
        exit 0
    }
    if (-not (Test-Path -LiteralPath (Join-Path $chartProject 'Assets\_Project\Editor\DepartureChartInlineCapture20261014.cs'))) { throw 'Run Prepare first.' }
    $chartOutput = Join-Path $chartRoot $RunName
    if (Test-Path -LiteralPath $chartOutput) { throw 'Use a fresh RunName to preserve output.' }
    [IO.Directory]::CreateDirectory($chartOutput) | Out-Null
    $chartArgs = @('-batchmode','-force-d3d11','-projectPath',$chartProject,'-logFile',(Join-Path $chartOutput ($Stage + '.log')))
    if ($Stage -eq 'Capture') {
        $chartArgs += @('-quit','-executeMethod','ShatteredSail.Editor.DepartureChartInlineCapture20261014.Run','-departureChartInlineOutput',(Join-Path $chartOutput 'Screens'))
    } elseif ($Stage -eq 'Compile') {
        $chartArgs += '-quit'
    } else {
        if (-not $TestFilter) {
            $TestFilter = if ($Stage -eq 'EditMode') { 'DepartureDockSceneLayoutTests;VoyageDepartureShipLayoutTests;DepartureTreasureSeedSelectionTests' } else { 'DepartureDockCharts_' }
        }
        $chartArgs += @('-runTests','-testPlatform',$Stage,'-testFilter',$TestFilter,'-testResults',(Join-Path $chartOutput 'results.xml'))
        if ($Stage -eq 'PlayMode') { $chartArgs += @('-navalVisualCapturePath',(Join-Path $chartOutput 'LiveScreens')) }
    }
    $chartQuoted = $chartArgs | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }
    $chartProcess = Start-Process -FilePath $chartUnity -WindowStyle Hidden -ArgumentList $chartQuoted -PassThru
    [pscustomobject]@{ Stage=$Stage; ProcessId=$chartProcess.Id; Project=$chartProject; Arguments=$chartArgs } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $chartOutput 'launch.json') -Encoding utf8
    Write-Output ('Unity ' + $Stage + ' PID ' + $chartProcess.Id + '; output=' + $chartOutput)
    $chartProcess.WaitForExit()
    $chartProcess.Refresh()
    if ($chartProcess.ExitCode -ne 0) { throw ('Unity failed with exit ' + $chartProcess.ExitCode + '; inspect ' + $chartOutput) }
    if ($Stage -eq 'EditMode' -or $Stage -eq 'PlayMode') {
        [xml]$chartResults = Get-Content -Raw -LiteralPath (Join-Path $chartOutput 'results.xml')
        if ($chartResults.'test-run'.result -ne 'Passed') { throw ('Tests failed; inspect ' + $chartOutput) }
        Write-Output ('Passed ' + $chartResults.'test-run'.passed + ' tests.')
    }
    Write-Output ('Completed ' + $Stage + ': ' + $chartOutput)
} finally {
    if ($chartLease) { $chartLease.Dispose() }
}
