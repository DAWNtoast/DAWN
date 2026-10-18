param(
    [ValidateSet('Prepare','Compile','EditMode','PlayMode')][string]$Stage = 'Prepare',
    [string]$RunName = 'run1',
    [string]$TestFilter = '',
    [switch]$Capture
)
$ErrorActionPreference = 'Stop'
$secondarySource = 'D:\Game\UnityClient'
$secondaryProject = [IO.Path]::GetFullPath('D:\Game\tmp\battle-deck-overview-20261014').TrimEnd('\')
$secondaryRoot = 'D:\Game\Artifacts\DepartureSecondaryStyle20261014\validation'
$secondaryUnity = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
$secondaryIdentity = 'DepartureSecondaryStyleValidation20261014'
$secondaryRetired = Join-Path $secondaryProject 'Assets\_Project\Scenes\UIPreviewsForMe'
$secondaryDriver = Join-Path $secondaryProject 'Assets\_Project\Tests\PlayMode\DepartureSecondaryStyleCapture20261014.cs'
if ($RunName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'RunName must be a simple identifier.' }
if ($Capture -and $Stage -ne 'PlayMode') { throw 'Capture is available only with PlayMode.' }
if (-not (Test-Path -LiteralPath (Join-Path $secondaryProject 'Library'))) { throw 'The existing warm isolated project is required.' }
if (-not (Test-Path -LiteralPath $secondaryUnity)) { throw 'The required Unity editor is unavailable.' }
if (@(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/','\').IndexOf($secondaryProject, [StringComparison]::OrdinalIgnoreCase) -ge 0 }).Count) { throw 'The isolated project is already running.' }
$secondaryUnityLock = Join-Path $secondaryProject 'Temp\UnityLockfile'
if (Test-Path -LiteralPath $secondaryUnityLock) {
    try { $secondaryCheck = [IO.File]::Open($secondaryUnityLock, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None); $secondaryCheck.Dispose() }
    catch { throw 'The isolated Unity project is already open.' }
}
if (Test-Path -LiteralPath $secondaryRetired) { throw 'Retired preview assets must remain absent.' }
[IO.Directory]::CreateDirectory($secondaryRoot) | Out-Null
$secondaryLease = $null
try {
    # Share the owner's existing lease because the warm project is intentionally reused.
    $secondaryLease = [IO.File]::Open((Join-Path $secondaryProject 'BattleDeckOverview20261014.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $secondarySettingsPath = Join-Path $secondaryProject 'ProjectSettings\ProjectSettings.asset'
    if ($Stage -eq 'Prepare') {
        foreach ($secondaryPart in @('Assets','Packages','ProjectSettings')) {
            $secondaryCopy = @((Join-Path $secondarySource $secondaryPart), (Join-Path $secondaryProject $secondaryPart), '/E','/XJ','/R:1','/W:1','/NFL','/NDL','/NP','/NJH','/NJS')
            if ($secondaryPart -eq 'Assets') { $secondaryCopy += @('/XD',(Join-Path $secondarySource 'Assets\_Project\Scenes\UIPreviewsForMe'),'/XF','UIPreviewsForMe.meta') }
            & robocopy @secondaryCopy | Out-Null
            if ($LASTEXITCODE -ge 8) { throw ('Copy failed: ' + $secondaryPart) }
        }
        $secondaryScratch = [IO.Path]::GetFullPath((Join-Path $secondaryProject 'Assets\_Project'))
        foreach ($secondaryOld in @(Get-ChildItem -LiteralPath $secondaryScratch -Filter '*.cs' -File -Recurse)) {
            $secondaryRelative = $secondaryOld.FullName.Substring($secondaryScratch.Length + 1)
            if (Test-Path -LiteralPath (Join-Path $secondarySource ('Assets\_Project\' + $secondaryRelative))) { continue }
            $secondaryResolved = [IO.Path]::GetFullPath($secondaryOld.FullName)
            if (-not $secondaryResolved.StartsWith($secondaryScratch + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Stale source is outside the isolated project.' }
            Remove-Item -LiteralPath $secondaryResolved -Force
            if (Test-Path -LiteralPath ($secondaryResolved + '.meta')) { Remove-Item -LiteralPath ($secondaryResolved + '.meta') -Force }
        }
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Capture.cs') -Destination $secondaryDriver -Force
        $secondarySettings = [IO.File]::ReadAllText($secondarySettingsPath)
        $secondarySettings = [regex]::Replace($secondarySettings, '(?m)^  companyName:.*$', ('  companyName: ' + $secondaryIdentity))
        $secondarySettings = [regex]::Replace($secondarySettings, '(?m)^  productName:.*$', ('  productName: ' + $secondaryIdentity))
        [IO.File]::WriteAllText($secondarySettingsPath, $secondarySettings, [Text.UTF8Encoding]::new($false))
        if (Test-Path -LiteralPath $secondaryRetired) { throw 'Retired preview assets must remain absent.' }
        Write-Output ('Prepared latest source and assets with separate PlayerPrefs identity: ' + $secondaryProject + '. Warm Library retained; no Unity launched or scenes generated.')
        exit 0
    }
    if (-not (Test-Path -LiteralPath $secondaryDriver)) { throw 'Run Prepare first to install the isolated live capture driver.' }
    $secondarySettings = [IO.File]::ReadAllText($secondarySettingsPath)
    foreach ($secondarySetting in @('companyName','productName')) {
        if ($secondarySettings -notmatch ('(?m)^  ' + $secondarySetting + ': ' + [regex]::Escape($secondaryIdentity) + '\s*$')) { throw 'Run Prepare to isolate PlayerPrefs before launching Unity.' }
    }
    if (($Stage -eq 'EditMode' -or $Stage -eq 'PlayMode') -and -not $TestFilter) {
        $TestFilter = if ($Stage -eq 'EditMode') { 'DepartureBasicSuppliesLayoutTests;DepartureTreasureSeedSelectionTests' } else {
            'DepartureBasicSupplies_;EscapeNavigation_DepartureSuppliesClosesBeforeTheDockWithoutPurchasing;DepartureSecondaryStyleCapture_'
        }
    }
    $secondaryOutput = Join-Path $secondaryRoot $RunName
    if (Test-Path -LiteralPath $secondaryOutput) { throw 'Use a fresh RunName to preserve existing output.' }
    [IO.Directory]::CreateDirectory($secondaryOutput) | Out-Null
    $secondaryArgs = @('-batchmode','-force-d3d11','-projectPath',$secondaryProject,'-logFile',(Join-Path $secondaryOutput ($Stage + '.log')))
    if ($Stage -eq 'Compile') { $secondaryArgs += '-quit' } else {
        $secondaryArgs += @('-runTests','-testPlatform',$Stage,'-testFilter',$TestFilter,'-testResults',(Join-Path $secondaryOutput 'results.xml'))
        if ($Capture) { $secondaryArgs += @('-navalVisualCapturePath',(Join-Path $secondaryOutput 'Screens')) }
    }
    $secondaryQuoted = $secondaryArgs | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }
    $secondaryProcess = Start-Process -FilePath $secondaryUnity -WindowStyle Hidden -ArgumentList $secondaryQuoted -PassThru
    [pscustomobject]@{ Stage=$Stage; ProcessId=$secondaryProcess.Id; Project=$secondaryProject; Arguments=$secondaryArgs } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $secondaryOutput 'launch.json') -Encoding utf8
    Write-Output ('Unity ' + $Stage + ' PID ' + $secondaryProcess.Id + '; output=' + $secondaryOutput)
    $secondaryProcess.WaitForExit()
    $secondaryProcess.Refresh()
    if (Test-Path -LiteralPath $secondaryRetired) { throw ('Unity unexpectedly created retired preview assets; inspect ' + $secondaryOutput) }
    if ($secondaryProcess.ExitCode -ne 0) { throw ('Unity failed with exit ' + $secondaryProcess.ExitCode + '; inspect ' + $secondaryOutput) }
    if ($Stage -eq 'EditMode' -or $Stage -eq 'PlayMode') {
        [xml]$secondaryResults = Get-Content -Raw -LiteralPath (Join-Path $secondaryOutput 'results.xml')
        if ($secondaryResults.'test-run'.result -ne 'Passed' -or [int]$secondaryResults.'test-run'.passed -lt 1) { throw ('Tests failed or no tests matched; inspect ' + $secondaryOutput) }
        Write-Output ('Passed ' + $secondaryResults.'test-run'.passed + ' tests.')
    }
    if ($Capture -and -not (Test-Path -LiteralPath (Join-Path $secondaryOutput 'Screens\report.json'))) { throw 'Tests passed but no visual captures were recorded.' }
    Write-Output ('Completed ' + $Stage + ': ' + $secondaryOutput)
} finally {
    if ($secondaryLease) { $secondaryLease.Dispose() }
}
