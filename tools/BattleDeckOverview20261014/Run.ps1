param(
    [ValidateSet('Prepare','Compile','EditMode','PlayMode')][string]$Stage = 'Prepare',
    [string]$RunName = 'run1',
    [string]$TestFilter = '',
    [switch]$Capture
)
$ErrorActionPreference = 'Stop'
$deckSource = 'D:\Game\UnityClient'
$deckProject = [IO.Path]::GetFullPath('D:\Game\tmp\battle-deck-overview-20261014').TrimEnd('\')
$deckWarm = 'D:\Game\tmp\escape-navigation-20261014\Library'
$deckRoot = 'D:\Game\Artifacts\BattleDeckOverview20261014\validation'
$deckUnity = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
$deckIdentity = 'BattleDeckOverviewValidation20261014'
$deckRetired = [IO.Path]::GetFullPath((Join-Path $deckProject 'Assets\_Project\Scenes\UIPreviewsForMe'))
if ($RunName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'RunName must be a simple identifier.' }
if ($Capture -and $Stage -ne 'PlayMode') { throw 'Capture is available only with PlayMode tests that call NavalBattleVisualCapture.' }
if (-not (Test-Path -LiteralPath $deckUnity)) { throw 'The required Unity editor is unavailable.' }
if (@(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/','\').IndexOf($deckProject, [StringComparison]::OrdinalIgnoreCase) -ge 0 }).Count) { throw 'The isolated project is already running.' }
$deckUnityLock = Join-Path $deckProject 'Temp\UnityLockfile'
if (Test-Path -LiteralPath $deckUnityLock) {
    try { $deckCheck = [IO.File]::Open($deckUnityLock, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None); $deckCheck.Dispose() }
    catch { throw 'The isolated Unity project is already open.' }
}
if (Test-Path -LiteralPath $deckRetired) { throw 'Retired preview assets must remain absent from this isolated project.' }
[IO.Directory]::CreateDirectory($deckProject) | Out-Null
[IO.Directory]::CreateDirectory($deckRoot) | Out-Null
$deckLease = $null
try {
    $deckLease = [IO.File]::Open((Join-Path $deckProject 'BattleDeckOverview20261014.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $deckSettingsPath = Join-Path $deckProject 'ProjectSettings\ProjectSettings.asset'
    if ($Stage -eq 'Prepare') {
        if (-not (Test-Path -LiteralPath (Join-Path $deckProject 'Library'))) {
            if (-not (Test-Path -LiteralPath $deckWarm)) { throw 'The expected read-only warm Library is missing.' }
            & robocopy $deckWarm (Join-Path $deckProject 'Library') /E /XJ /R:1 /W:1 /NFL /NDL /NP /NJH /NJS /XD 'Bee' 'TempArtifacts' /XF '*-lock' 'ilpp.pid' | Out-Null
            if ($LASTEXITCODE -ge 8) { throw 'Warm Library copy failed.' }
        }
        foreach ($deckPart in @('Assets','Packages','ProjectSettings')) {
            $deckCopy = @((Join-Path $deckSource $deckPart), (Join-Path $deckProject $deckPart), '/E','/XJ','/R:1','/W:1','/NFL','/NDL','/NP','/NJH','/NJS')
            if ($deckPart -eq 'Assets') {
                $deckCopy += @('/XD', (Join-Path $deckSource 'Assets\_Project\Scenes\UIPreviewsForMe'), '/XF', 'UIPreviewsForMe.meta')
            }
            & robocopy @deckCopy | Out-Null
            if ($LASTEXITCODE -ge 8) { throw ('Copy failed: ' + $deckPart) }
        }
        # Remove only stale C# files in this task's isolated project, never source assets.
        $deckScratch = [IO.Path]::GetFullPath((Join-Path $deckProject 'Assets\_Project'))
        foreach ($deckOld in @(Get-ChildItem -LiteralPath $deckScratch -Filter '*.cs' -File -Recurse)) {
            $deckRelative = $deckOld.FullName.Substring($deckScratch.Length + 1)
            if (Test-Path -LiteralPath (Join-Path $deckSource ('Assets\_Project\' + $deckRelative))) { continue }
            $deckResolved = [IO.Path]::GetFullPath($deckOld.FullName)
            if (-not $deckResolved.StartsWith($deckScratch + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Stale source is outside the isolated project.' }
            Remove-Item -LiteralPath $deckResolved -Force
            if (Test-Path -LiteralPath ($deckResolved + '.meta')) { Remove-Item -LiteralPath ($deckResolved + '.meta') -Force }
        }
        $deckSettings = [IO.File]::ReadAllText($deckSettingsPath)
        $deckSettings = [regex]::Replace($deckSettings, '(?m)^  companyName:.*$', ('  companyName: ' + $deckIdentity))
        $deckSettings = [regex]::Replace($deckSettings, '(?m)^  productName:.*$', ('  productName: ' + $deckIdentity))
        [IO.File]::WriteAllText($deckSettingsPath, $deckSettings, [Text.UTF8Encoding]::new($false))
        if (Test-Path -LiteralPath $deckRetired) { throw 'Retired preview assets must remain absent.' }
        Write-Output ('Prepared current source with separate PlayerPrefs identity: ' + $deckProject + '. Warm Library retained; no Unity launched or scenes generated.')
        exit 0
    }
    if (-not (Test-Path -LiteralPath $deckSettingsPath)) { throw 'Run Prepare first.' }
    $deckSettings = [IO.File]::ReadAllText($deckSettingsPath)
    foreach ($deckSetting in @('companyName','productName')) {
        if ($deckSettings -notmatch ('(?m)^  ' + $deckSetting + ': ' + [regex]::Escape($deckIdentity) + '\s*$')) { throw 'Run Prepare to isolate PlayerPrefs before launching Unity.' }
    }
    if (($Stage -eq 'EditMode' -or $Stage -eq 'PlayMode') -and -not $TestFilter) {
        $TestFilter = if ($Stage -eq 'EditMode') {
            'CoreBattleDeckInspectionTests;VoyageSettingsModalTests;BattleHudLayoutTests'
        } else {
            'BattleDeckOverview_;BattleResponsive_SameStageEnemyTurnRetainsSceneAndRefreshesAllLiveHudValues'
        }
    }
    $deckOutput = Join-Path $deckRoot $RunName
    if (Test-Path -LiteralPath $deckOutput) { throw 'Use a fresh RunName to preserve existing output.' }
    [IO.Directory]::CreateDirectory($deckOutput) | Out-Null
    $deckArgs = @('-batchmode','-force-d3d11','-projectPath',$deckProject,'-logFile',(Join-Path $deckOutput ($Stage + '.log')))
    if ($Stage -eq 'Compile') {
        $deckArgs += '-quit'
    } else {
        $deckArgs += @('-runTests','-testPlatform',$Stage,'-testFilter',$TestFilter,'-testResults',(Join-Path $deckOutput 'results.xml'))
        if ($Capture) { $deckArgs += @('-navalVisualCapturePath',(Join-Path $deckOutput 'Screens')) }
    }
    $deckQuoted = $deckArgs | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }
    $deckProcess = Start-Process -FilePath $deckUnity -WindowStyle Hidden -ArgumentList $deckQuoted -PassThru
    [pscustomobject]@{ Stage=$Stage; ProcessId=$deckProcess.Id; Project=$deckProject; Arguments=$deckArgs } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $deckOutput 'launch.json') -Encoding utf8
    Write-Output ('Unity ' + $Stage + ' PID ' + $deckProcess.Id + '; output=' + $deckOutput)
    $deckProcess.WaitForExit()
    $deckProcess.Refresh()
    if (Test-Path -LiteralPath $deckRetired) { throw ('Unity unexpectedly created retired preview assets; inspect ' + $deckOutput) }
    if ($deckProcess.ExitCode -ne 0) { throw ('Unity failed with exit ' + $deckProcess.ExitCode + '; inspect ' + $deckOutput) }
    if ($Stage -eq 'EditMode' -or $Stage -eq 'PlayMode') {
        [xml]$deckResults = Get-Content -Raw -LiteralPath (Join-Path $deckOutput 'results.xml')
        if ($deckResults.'test-run'.result -ne 'Passed' -or [int]$deckResults.'test-run'.passed -lt 1) { throw ('Tests failed or no tests matched; inspect ' + $deckOutput) }
        Write-Output ('Passed ' + $deckResults.'test-run'.passed + ' tests.')
    }
    if ($Capture -and -not (Test-Path -LiteralPath (Join-Path $deckOutput 'Screens\report.json'))) { throw 'Tests passed but no visual captures were recorded; add explicit NavalBattleVisualCapture.Capture calls to the targeted tests.' }
    Write-Output ('Completed ' + $Stage + ': ' + $deckOutput)
} finally {
    if ($deckLease) { $deckLease.Dispose() }
}
