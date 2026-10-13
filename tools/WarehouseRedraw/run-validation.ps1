param(
    [ValidateSet('Prepare','Sync','Compile','Capture','SyncSaved','CaptureSaved','EditMode','PlayMode')]
    [string]$Action = 'Prepare',
    [string]$RunName = 'final'
)
$ErrorActionPreference = 'Stop'
# Retired by user request; exit before filesystem or Unity actions.
if (@('SyncSaved','CaptureSaved') -contains $Action) { throw 'UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.' }
$taskSource = [IO.Path]::GetFullPath('D:\Game\UnityClient')
$taskScratch = [IO.Path]::GetFullPath('D:\Game\tmp\warehouse-redraw-20261012')
$taskOutputRoot = [IO.Path]::GetFullPath('D:\Game\Artifacts\WarehouseRedraw20261012\validation')
if ($RunName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'RunName must be a simple directory name.' }
$taskOutput = Join-Path $taskOutputRoot $RunName
$taskMarker = Join-Path $taskScratch '.warehouse-redraw-validation'
if ($taskScratch -ne 'D:\Game\tmp\warehouse-redraw-20261012') { throw 'Unexpected scratch path.' }
if (Test-Path -LiteralPath (Join-Path $taskScratch 'Temp\UnityLockfile')) { throw 'Scratch Unity may be running.' }
if ($Action -eq 'Prepare') {
    if (Test-Path -LiteralPath $taskScratch) { throw 'Refusing to overwrite an existing scratch project.' }
    New-Item -ItemType Directory -Path $taskScratch | Out-Null
    foreach ($taskPart in @('Assets','Packages','ProjectSettings')) {
        & robocopy (Join-Path $taskSource $taskPart) (Join-Path $taskScratch $taskPart) /E /NFL /NDL /NJH /NJS /NP
        if ($LASTEXITCODE -gt 7) { throw "Initial copy failed: $taskPart, exit $LASTEXITCODE" }
    }
    $taskSettings = Join-Path $taskScratch 'ProjectSettings\ProjectSettings.asset'
    $taskText = [IO.File]::ReadAllText($taskSettings)
    $taskText = $taskText -replace '(?m)^  companyName:.*$', '  companyName: WarehouseRedrawValidation' -replace '(?m)^  productName:.*$', '  productName: WarehouseRedrawValidation20261012' -replace '(?m)^    Standalone: com\.shatteredsail\.shattered-sail\r?$', '    Standalone: com.validation.warehouseredraw20261012'
    [IO.File]::WriteAllText($taskSettings,$taskText,[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($taskMarker,'Dedicated warehouse validation project; production never launched.')
    Write-Output "Prepared dedicated scratch: $taskScratch"
    exit 0
}
if (-not (Test-Path -LiteralPath $taskMarker)) { throw 'Dedicated scratch marker missing.' }
if ($Action -eq 'Sync') {
    foreach ($taskPart in @('Assets\_Project\Presentation\Runtime','Assets\_Project\Tests','Assets\_Project\Resources\PortFacilities','Assets\_Project\Presentation\EditablePreviewAssets')) {
        & robocopy (Join-Path $taskSource $taskPart) (Join-Path $taskScratch $taskPart) /E /NFL /NDL /NJH /NJS /NP
        if ($LASTEXITCODE -gt 7) { throw "Scratch sync failed: $taskPart, exit $LASTEXITCODE" }
    }
    foreach ($taskHelper in @('WarehouseRedrawCapture.cs','WarehouseRedrawPreviewSync.cs')) {
        if (Test-Path -LiteralPath (Join-Path $PSScriptRoot $taskHelper)) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $taskHelper) -Destination (Join-Path $taskScratch ('Assets\_Project\Editor\' + $taskHelper)) -Force }
    }
    Write-Output 'Updated disposable project sources, tests and art; retired UIPreviewsForMe scenes are excluded.'
    exit 0
}
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskLog = Join-Path $taskOutput ($Action.ToLowerInvariant() + '.log')
if (Test-Path -LiteralPath $taskLog) { throw 'Use a fresh RunName to preserve evidence.' }
$taskArguments = @('-batchmode','-projectPath',$taskScratch,'-logFile',$taskLog)
switch ($Action) {
    'Compile' { $taskArguments += '-quit' }
    'Capture' { $taskArguments += @('-quit','-executeMethod','ShatteredSail.Editor.WarehouseRedrawCapture.CaptureFromCommandLine','-warehouseRedrawCapturePath',(Join-Path $taskOutput 'ui-review')) }
    'CaptureSaved' { $taskArguments += @('-quit','-executeMethod','ShatteredSail.Editor.WarehouseRedrawCapture.CaptureSavedFromCommandLine','-warehouseSavedCapturePath',(Join-Path $taskOutput 'saved-preview-review')) }
    'SyncSaved' { $taskArguments += @('-quit','-executeMethod','ShatteredSail.Editor.GameplayPreviewSceneBuilder.SyncWarehouseRedrawFromCommandLine','-warehouseRedrawSyncPath',(Join-Path $taskOutput 'saved-sync')) }
    'EditMode' { $taskArguments += @('-runTests','-testPlatform','EditMode','-testFilter','Warehouse;RegionalPortFacilityArtCatalogTests;PortSurfaceGroundingTests;DesktopBackgroundCompressionTests;NauticalSurfaceSkinTests','-testResults',(Join-Path $taskOutput 'editmode.xml')) }
    'PlayMode' { $taskArguments += @('-runTests','-testPlatform','PlayMode','-testFilter','Warehouse','-testResults',(Join-Path $taskOutput 'playmode.xml')) }
}
$taskProcess = Start-Process -FilePath 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' -WindowStyle Hidden -ArgumentList $taskArguments -Wait -PassThru
Write-Output "Unity $Action exit code: $($taskProcess.ExitCode); log: $taskLog"
exit $taskProcess.ExitCode
