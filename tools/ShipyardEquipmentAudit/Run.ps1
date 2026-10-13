param([ValidateSet('Sync','EditMode','PlayMode','Capture','PreviewSync')][string]$Stage = 'Sync', [string]$Suffix = '', [string]$TestFilter = '')
$ErrorActionPreference = 'Stop'
# Retired by user request; exit before filesystem or Unity actions.
if (@('PreviewSync') -contains $Stage) { throw 'UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.' }
$taskSource = 'D:\Game\UnityClient'
$taskProject = 'D:\Game\tmp\prefab-ui-20261009'
$taskOutput = 'D:\Game\Artifacts\ShipyardEquipment20261010'
$taskUnity = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
[IO.Directory]::CreateDirectory($taskOutput) | Out-Null
$taskLock = Join-Path $taskProject 'Temp\UnityLockfile'
if (Test-Path -LiteralPath $taskLock) {
    try { $taskLockStream = [IO.File]::Open($taskLock, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None); $taskLockStream.Dispose() }
    catch { throw 'The isolated audit project is already open; do not overlap runs in this project.' }
}
if ($Stage -eq 'Sync') {
    $taskScratchAssets = [IO.Path]::GetFullPath((Join-Path $taskProject 'Assets\_Project'))
    $taskSourceAssets = Join-Path $taskSource 'Assets\_Project'
    foreach ($taskOldSource in @(Get-ChildItem -LiteralPath $taskScratchAssets -Filter '*.cs' -File -Recurse)) {
        if ($taskOldSource.Name -in @('ShipyardEquipmentCapture.cs','ShipyardEquipmentPreviewSync.cs')) { continue }
        $taskRelative = $taskOldSource.FullName.Substring($taskScratchAssets.Length + 1)
        if (Test-Path -LiteralPath (Join-Path $taskSourceAssets $taskRelative)) { continue }
        $taskResolved = [IO.Path]::GetFullPath($taskOldSource.FullName)
        if (-not $taskResolved.StartsWith($taskScratchAssets + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Stale source outside scratch project.' }
        Remove-Item -LiteralPath $taskResolved -Force
        if (Test-Path -LiteralPath ($taskResolved + '.meta')) { Remove-Item -LiteralPath ($taskResolved + '.meta') -Force }
        Write-Output ('Removed stale scratch source: ' + $taskRelative)
    }
    foreach ($taskPart in @('Assets','Packages','ProjectSettings')) {
        & robocopy (Join-Path $taskSource $taskPart) (Join-Path $taskProject $taskPart) /E /R:1 /W:1 /NFL /NDL /NP /NJH /NJS ('/LOG+:' + (Join-Path $taskOutput ('Sync' + $Suffix + '.log'))) | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "Copy failed: $taskPart" }
    }
    $taskSettingsPath = Join-Path $taskProject 'ProjectSettings\ProjectSettings.asset'
    $taskSettings = [IO.File]::ReadAllText($taskSettingsPath).Replace('  companyName: ShatteredSail', '  companyName: ShatteredSailEquipmentAudit20261010').Replace('  productName: Shattered Sail', '  productName: Shattered Sail Equipment Audit 20261010')
    [IO.File]::WriteAllText($taskSettingsPath, $taskSettings, [Text.UTF8Encoding]::new($false))
    Copy-Item -LiteralPath 'D:\Game\tools\ShipyardEquipmentAudit\ShipyardEquipmentCapture.cs' -Destination (Join-Path $taskProject 'Assets\_Project\Editor\ShipyardEquipmentCapture.cs') -Force
    Copy-Item -LiteralPath 'D:\Game\tools\ShipyardEquipmentAudit\ShipyardEquipmentPreviewSync.cs' -Destination (Join-Path $taskProject 'Assets\_Project\Editor\ShipyardEquipmentPreviewSync.cs') -Force
    Write-Output 'Synced source into isolated project and applied unique test identity.'
    exit 0
}
$taskArgs = @('-batchmode','-projectPath', $taskProject, '-logFile', (Join-Path $taskOutput ($Stage + $Suffix + '.log')))
if ($Stage -eq 'Capture') {
    $taskArgs += @('-executeMethod','ShatteredSail.Editor.ShipyardEquipmentCapture.CaptureFreshScreensFromCommandLine','-uiAuditPath', (Join-Path $taskOutput ('Screens' + $Suffix)), '-quit')
} elseif ($Stage -eq 'PreviewSync') {
    $taskArgs += @('-executeMethod','ShatteredSail.Editor.GameplayPreviewSceneBuilder.SyncEquipmentPreviewsFromCommandLine','-quit')
} else {
    $taskFilter = if ($TestFilter) { $TestFilter } elseif ($Stage -eq 'EditMode') { 'ShipyardGalleryTests;ShipyardEquipment;ShipyardComponentComparisonTests;OwnedShipComponentInventoryTests;VoyageDepartureShipLayoutTests' } else { 'ShipyardEquipment_;DepartureShips_' }
    $taskArgs += @('-runTests','-testPlatform',$Stage,'-testFilter',$taskFilter,'-testResults',(Join-Path $taskOutput ($Stage + $Suffix + '.xml')))
}
$taskProcess = Start-Process -FilePath $taskUnity -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
Write-Output ('Unity PID ' + $taskProcess.Id)
$taskProcess.WaitForExit()
Write-Output ('Unity exit ' + $taskProcess.ExitCode)
if ($taskProcess.ExitCode -ne 0) { throw ('Unity failed: ' + $taskProcess.ExitCode) }
