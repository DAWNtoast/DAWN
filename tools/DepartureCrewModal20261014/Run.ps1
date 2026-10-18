param(
    [ValidateSet('Prepare','Compile','EditMode','PlayMode','Capture')][string]$Stage = 'Prepare',
    [string]$RunName = 'run1',
    [string]$TestFilter = ''
)
$ErrorActionPreference = 'Stop'
$crewSource = 'D:\Game\UnityClient'
$crewProject = [IO.Path]::GetFullPath('D:\Game\tmp\departure-chart-inline-20261014')
$crewWarm = 'D:\Game\tmp\prefab-runtime-20261014\Library'
$crewRoot = 'D:\Game\Artifacts\DepartureCrewModal20261014\validation'
$crewUnity = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
if ($RunName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'RunName must be a simple identifier.' }
if (@(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($crewProject) }).Count) { throw 'Isolated project is already running.' }
$crewUnityLock = Join-Path $crewProject 'Temp\UnityLockfile'
if (Test-Path -LiteralPath $crewUnityLock) {
    try { $crewCheck = [IO.File]::Open($crewUnityLock, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None); $crewCheck.Dispose() }
    catch { throw 'The isolated Unity project is already open.' }
}
[IO.Directory]::CreateDirectory($crewProject) | Out-Null
[IO.Directory]::CreateDirectory($crewRoot) | Out-Null
$crewLease = $null
try {
    $crewLease = [IO.File]::Open((Join-Path $crewProject 'DepartureCrewModal20261014.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    if ($Stage -eq 'Prepare') {
        if (-not (Test-Path -LiteralPath (Join-Path $crewProject 'Library'))) {
            if (-not (Test-Path -LiteralPath $crewWarm)) { throw 'Expected read-only warm cache is missing.' }
            & robocopy $crewWarm (Join-Path $crewProject 'Library') /E /R:1 /W:1 /NFL /NDL /NP /NJH /NJS /XD 'Bee' 'TempArtifacts' /XF '*-lock' 'ilpp.pid' | Out-Null
            if ($LASTEXITCODE -ge 8) { throw 'Warm Library copy failed.' }
        }
        foreach ($crewPart in @('Assets','Packages','ProjectSettings')) {
            $crewCopy = @((Join-Path $crewSource $crewPart), (Join-Path $crewProject $crewPart), '/E','/R:1','/W:1','/NFL','/NDL','/NP','/NJH','/NJS')
            if ($crewPart -eq 'Assets') { $crewCopy += @('/XD', (Join-Path $crewSource 'Assets\_Project\Scenes\UIPreviewsForMe'), '/XF', 'UIPreviewsForMe.meta') }
            & robocopy @crewCopy | Out-Null
            if ($LASTEXITCODE -ge 8) { throw ('Copy failed: ' + $crewPart) }
        }
        $crewScratch = [IO.Path]::GetFullPath((Join-Path $crewProject 'Assets\_Project'))
        foreach ($crewOld in @(Get-ChildItem -LiteralPath $crewScratch -Filter '*.cs' -File -Recurse)) {
            $crewRelative = $crewOld.FullName.Substring($crewScratch.Length + 1)
            if (Test-Path -LiteralPath (Join-Path $crewSource ('Assets\_Project\' + $crewRelative))) { continue }
            $crewResolved = [IO.Path]::GetFullPath($crewOld.FullName)
            if (-not $crewResolved.StartsWith($crewScratch + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Stale source is outside scratch project.' }
            Remove-Item -LiteralPath $crewResolved -Force
            if (Test-Path -LiteralPath ($crewResolved + '.meta')) { Remove-Item -LiteralPath ($crewResolved + '.meta') -Force }
        }
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Capture.cs') -Destination (Join-Path $crewProject 'Assets\_Project\Editor\DepartureCrewModalCapture20261014.cs') -Force
        $crewSettingsPath = Join-Path $crewProject 'ProjectSettings\ProjectSettings.asset'
        $crewSettings = [IO.File]::ReadAllText($crewSettingsPath)
        $crewSettings = [regex]::Replace($crewSettings, '(?m)^  companyName:.*$', '  companyName: DepartureCrewModalValidation20261014')
        $crewSettings = [regex]::Replace($crewSettings, '(?m)^  productName:.*$', '  productName: DepartureCrewModalValidation20261014')
        [IO.File]::WriteAllText($crewSettingsPath, $crewSettings, [Text.UTF8Encoding]::new($false))
        Write-Output 'Prepared isolated source and assets with separate PlayerPrefs identity; no Unity launch or scenes generated.'
        exit 0
    }
    if (-not (Test-Path -LiteralPath (Join-Path $crewProject 'Assets\_Project\Editor\DepartureCrewModalCapture20261014.cs'))) { throw 'Run Prepare first.' }
    $crewOutput = Join-Path $crewRoot $RunName
    if (Test-Path -LiteralPath $crewOutput) { throw 'Use a fresh RunName to preserve output.' }
    [IO.Directory]::CreateDirectory($crewOutput) | Out-Null
    $crewArgs = @('-batchmode','-force-d3d11','-projectPath',$crewProject,'-logFile',(Join-Path $crewOutput ($Stage + '.log')))
    if ($Stage -eq 'Capture') {
        $crewArgs += @('-quit','-executeMethod','ShatteredSail.Editor.DepartureCrewModalCapture20261014.Run','-departureCrewModalOutput',(Join-Path $crewOutput 'Screens'))
    } elseif ($Stage -eq 'Compile') {
        $crewArgs += '-quit'
    } else {
        if (-not $TestFilter) {
            $TestFilter = if ($Stage -eq 'EditMode') { 'DepartureDockSceneLayoutTests;VoyageDepartureShipLayoutTests;ParchmentFrameLayoutTests' } else { 'DepartureCrew_' }
        }
        $crewArgs += @('-runTests','-testPlatform',$Stage,'-testFilter',$TestFilter,'-testResults',(Join-Path $crewOutput 'results.xml'))
        if ($Stage -eq 'PlayMode') { $crewArgs += @('-navalVisualCapturePath',(Join-Path $crewOutput 'LiveScreens')) }
    }
    $crewQuoted = $crewArgs | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }
    $crewProcess = Start-Process -FilePath $crewUnity -WindowStyle Hidden -ArgumentList $crewQuoted -PassThru
    [pscustomobject]@{ Stage=$Stage; ProcessId=$crewProcess.Id; Project=$crewProject; Arguments=$crewArgs } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $crewOutput 'launch.json') -Encoding utf8
    Write-Output ('Unity ' + $Stage + ' PID ' + $crewProcess.Id + '; output=' + $crewOutput)
    $crewProcess.WaitForExit()
    $crewProcess.Refresh()
    if ($crewProcess.ExitCode -ne 0) { throw ('Unity failed with exit ' + $crewProcess.ExitCode + '; inspect ' + $crewOutput) }
    if ($Stage -eq 'EditMode' -or $Stage -eq 'PlayMode') {
        [xml]$crewResults = Get-Content -Raw -LiteralPath (Join-Path $crewOutput 'results.xml')
        if ($crewResults.'test-run'.result -ne 'Passed') { throw ('Tests failed; inspect ' + $crewOutput) }
        Write-Output ('Passed ' + $crewResults.'test-run'.passed + ' tests.')
    }
    Write-Output ('Completed ' + $Stage + ': ' + $crewOutput)
} finally {
    if ($crewLease) { $crewLease.Dispose() }
}
