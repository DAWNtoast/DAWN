param([ValidateSet('Sync','EditMode','PlayMode','Capture')][string]$Stage = 'Sync')

$ErrorActionPreference = 'Stop'
$taskSource = 'D:\Game\UnityClient'
$taskProject = 'D:\Game\tmp\navigation-map-label-clean-20261012'
$taskOutput = 'D:\Game\Artifacts\NavigationEventMarker20261013'
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
    $taskSettings = [regex]::Replace($taskSettings, '(?m)^  companyName:.*$', '  companyName: ShatteredSailEventMarkerAudit20261013')
    $taskSettings = [regex]::Replace($taskSettings, '(?m)^  productName:.*$', '  productName: Shattered Sail Event Marker Audit 20261013')
    [IO.File]::WriteAllText($taskSettingsPath, $taskSettings, [Text.UTF8Encoding]::new($false))
    Write-Output 'Current source synced to isolated validation project with separate save identity.'
    exit 0
}
$taskArgs = @('-batchmode','-projectPath',$taskProject,'-logFile',(Join-Path $taskOutput ($Stage + '.log')))
if ($Stage -eq 'Capture') {
    Copy-Item -LiteralPath 'D:\Game\tools\NavigationEventMarkerAudit\NavigationEventMarkerCapture.cs' -Destination (Join-Path $taskProject 'Assets\_Project\Editor\NavigationEventMarkerCapture.cs') -Force
    $taskArgs += @('-executeMethod','ShatteredSail.Editor.NavigationEventMarkerCapture.CaptureFromCommandLine','-eventMarkerOutput',(Join-Path $taskOutput 'Screenshots'),'-quit')
} else {
    $taskFilter = if ($Stage -eq 'EditMode') { 'SeedVisibilityPresentationTests;NavigationDashboardTests' } else { 'DestinationPort_HitAreaPreservesOverlappingEventAndFreeWaterSelection;ChartSeed_EntryUnlocksAfterRecruitmentButNavigationKeepsItHidden_AndOnboardIncidentsNeverBecomeMarkers;TreasureCore_ContinueExploringLetsNearbyWaterEscapeAndMarkerRevisit' }
    $taskArgs += @('-runTests','-testPlatform',$Stage,'-testFilter',$taskFilter,'-testResults',(Join-Path $taskOutput ($Stage + '.xml')))
}
$taskQuotedArgs = $taskArgs | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }
$taskProcess = Start-Process -FilePath $taskUnity -ArgumentList $taskQuotedArgs -WindowStyle Hidden -PassThru
[pscustomobject]@{ Stage=$Stage; ProcessId=$taskProcess.Id; Arguments=$taskArgs } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskOutput ($Stage + '-launch.json')) -Encoding utf8
Write-Output ('Unity PID ' + $taskProcess.Id)
$taskProcess.WaitForExit()
Write-Output ('Unity exit ' + $taskProcess.ExitCode)
if ($taskProcess.ExitCode -ne 0) { throw ('Unity failed: ' + $taskProcess.ExitCode) }
