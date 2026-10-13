param(
    [ValidateSet('Sync','EditMode','PlayMode','Capture','FixedCapture','Scenes','SavedPreviews')][string]$Stage = 'Sync',
    [string]$ArtifactRoot = 'D:\Game\Artifacts\NavigationMapFill20261013',
    [string]$Filter = ''
)

$ErrorActionPreference = 'Stop'
# Retired by user request; exit before filesystem or Unity actions.
if (@('Scenes','SavedPreviews') -contains $Stage) { throw 'UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.' }
$taskSource = 'D:\Game\UnityClient'
$taskProject = 'D:\Game\tmp\navigation-map-label-clean-20261012'
$taskOutput = $ArtifactRoot
$taskUnity = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
[IO.Directory]::CreateDirectory($taskOutput) | Out-Null
if (Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($taskProject) }) {
    throw 'Validation project is already running.'
}
if ($Stage -eq 'Sync') {
    foreach ($taskPart in @('Assets','Packages','ProjectSettings')) {
        & robocopy (Join-Path $taskSource $taskPart) (Join-Path $taskProject $taskPart) /E /R:1 /W:1 /NFL /NDL /NP /NJH /NJS ('/LOG+:' + (Join-Path $taskOutput 'Sync.log')) | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "Copy failed: $taskPart" }
    }
    $taskScratchAssets = [IO.Path]::GetFullPath((Join-Path $taskProject 'Assets\_Project'))
    if (-not $taskScratchAssets.StartsWith('D:\Game\tmp\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid scratch root.' }
    foreach ($taskOld in Get-ChildItem -LiteralPath $taskScratchAssets -Filter '*.cs' -File -Recurse) {
        $taskRelative = $taskOld.FullName.Substring($taskScratchAssets.Length + 1)
        if (Test-Path -LiteralPath (Join-Path $taskSource ('Assets\_Project\' + $taskRelative))) { continue }
        if (-not ([IO.Path]::GetFullPath($taskOld.FullName)).StartsWith($taskScratchAssets + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Stale source outside scratch root.' }
        Remove-Item -LiteralPath $taskOld.FullName -Force
        if (Test-Path -LiteralPath ($taskOld.FullName + '.meta')) { Remove-Item -LiteralPath ($taskOld.FullName + '.meta') -Force }
    }
    $taskSettingsPath = Join-Path $taskProject 'ProjectSettings\ProjectSettings.asset'
    $taskSettings = [IO.File]::ReadAllText($taskSettingsPath)
    $taskSettings = [regex]::Replace($taskSettings, '(?m)^  companyName:.*$', '  companyName: ShatteredSailNavigationMapFillAudit20261013')
    $taskSettings = [regex]::Replace($taskSettings, '(?m)^  productName:.*$', '  productName: Shattered Sail Navigation Map Fill Audit 20261013')
    [IO.File]::WriteAllText($taskSettingsPath, $taskSettings, [Text.UTF8Encoding]::new($false))
    Write-Output 'Current source synced to isolated validation project with separate save identity.'
    exit 0
}
$taskArgs = @('-batchmode','-projectPath',$taskProject,'-logFile',(Join-Path $taskOutput ($Stage + '.log')))
if ($Stage -eq 'FixedCapture') {
    Copy-Item -LiteralPath 'D:\Game\tools\NavigationMapFillAudit\FixedAspectCapture.cs' -Destination (Join-Path $taskProject 'Assets\_Project\Editor\FixedAspectCapture.cs') -Force
    $taskArgs += @('-executeMethod','ShatteredSail.Editor.FixedAspectCapture.CaptureFromCommandLine','-uiAuditPath',(Join-Path $taskOutput 'ScreenshotsFinal'),'-quit')
} elseif ($Stage -eq 'Capture') {
    Copy-Item -LiteralPath 'D:\Game\tools\NavigationMapFillAudit\NavigationMapFillCapture.cs' -Destination (Join-Path $taskProject 'Assets\_Project\Editor\NavigationMapFillCapture.cs') -Force
    $taskArgs += @('-executeMethod','ShatteredSail.Editor.NavigationMapFillCapture.CaptureFromCommandLine','-uiAuditPath',(Join-Path $taskOutput 'ScreenshotsFinal'),'-quit')
} elseif ($Stage -eq 'Scenes') {
    $taskArgs += @('-executeMethod','ShatteredSail.Editor.NavigationThreePanelPreviewSceneSync.SyncFromCommandLine','-navigationThreePanelBackupPath',(Join-Path $taskOutput 'scene-backup'),'-navigationThreePanelReportPath',(Join-Path $taskOutput 'scene-sync.json'),'-quit')
} else {
    $taskFilter = if ($Stage -eq 'EditMode') { 'NavigationDashboardTests;PresentationIdleMotionTests' } elseif ($Stage -eq 'SavedPreviews') { 'NavigationEndurancePreviewSceneTests' } else { 'NavigationMapFill_;NavigationArrival_;DestinationPort_HitAreaPreservesOverlappingEventAndFreeWaterSelection;DestinationPort_NearbyMapClickSnapsToNearestPortOrEvent;DestinationPort_EdgeClickTargetsPierAndKeepsBossBlockade' }
    if ($Filter) { $taskFilter = $Filter }
    $taskPlatform = if ($Stage -eq 'SavedPreviews') { 'EditMode' } else { $Stage }
    $taskArgs += @('-runTests','-testPlatform',$taskPlatform,'-testFilter',$taskFilter,'-testResults',(Join-Path $taskOutput ($Stage + '.xml')))
}
$taskQuotedArgs = $taskArgs | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }
$taskProcess = Start-Process -FilePath $taskUnity -ArgumentList $taskQuotedArgs -WindowStyle Hidden -PassThru
[pscustomobject]@{ Stage=$Stage; ProcessId=$taskProcess.Id; Arguments=$taskArgs } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskOutput ($Stage + '-launch.json')) -Encoding utf8
Write-Output ('Unity PID ' + $taskProcess.Id)
$taskProcess.WaitForExit()
Write-Output ('Unity exit ' + $taskProcess.ExitCode)
if ($taskProcess.ExitCode -ne 0) { throw ('Unity failed: ' + $taskProcess.ExitCode) }
