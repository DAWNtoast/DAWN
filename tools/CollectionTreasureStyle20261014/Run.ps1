param(
    [ValidateSet('Prepare','EditMode','PlayMode','Capture','PreviewSync')][string]$Stage = 'Prepare',
    [string]$RunName = 'run1',
    [string]$TestFilter = ''
)
$ErrorActionPreference = 'Stop'
$styleSource = 'D:\Game\UnityClient'
$styleProject = 'D:\Game\tmp\departure-dock-scene-20261012'
$styleOutput = 'D:\Game\Artifacts\CollectionTreasureStyle20261014\validation'
$styleUnity = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
if ($RunName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'RunName must be a simple identifier.' }
if (-not (Test-Path -LiteralPath (Join-Path $styleProject 'Library'))) { throw 'Expected warm isolated project is missing.' }
$styleProcesses = @(Get-CimInstance Win32_Process -Filter "name='Unity.exe'")
if ($styleProcesses | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($styleProject) }) { throw 'Isolated project already running.' }
$styleLock = Join-Path $styleProject 'Temp\UnityLockfile'
if (Test-Path -LiteralPath $styleLock) {
    try { $styleStream = [IO.File]::Open($styleLock,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None); $styleStream.Dispose() }
    catch { throw 'Isolated project already open.' }
}
[IO.Directory]::CreateDirectory($styleOutput) | Out-Null
function Copy-StyleFile([string]$Relative) {
    $styleDestination = Join-Path $styleProject $Relative
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($styleDestination)) | Out-Null
    Copy-Item -LiteralPath (Join-Path $styleSource $Relative) -Destination $styleDestination -Force
    if (Test-Path -LiteralPath (Join-Path $styleSource ($Relative + '.meta'))) {
        Copy-Item -LiteralPath (Join-Path $styleSource ($Relative + '.meta')) -Destination ($styleDestination + '.meta') -Force
    }
}
if ($Stage -eq 'Prepare') {
    foreach ($styleFile in Get-ChildItem -LiteralPath (Join-Path $styleSource 'Assets\_Project') -File -Recurse |
        Where-Object { $_.Extension -in @('.cs','.asmdef') }) {
        Copy-StyleFile $styleFile.FullName.Substring($styleSource.Length + 1)
    }
    # Keep the warm Library. Copy only resources used by these two facility pages.
    foreach ($styleRelative in @('Assets/_Project/Resources/PortFacilities/Regional/Warehouse', 'Assets/_Project/Resources/WarehouseV2')) {
        & robocopy (Join-Path $styleSource $styleRelative) (Join-Path $styleProject $styleRelative) /E /R:1 /W:1 /NFL /NDL /NP /NJH /NJS | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "Copy failed: $styleRelative" }
    }
    foreach ($styleRelative in @(
        'Assets/_Project/Resources/PortFacilities/Market/market_shop_panel_v3.png',
        'Assets/_Project/Resources/PortFacilities/warehouse_background.png',
        'Assets/_Project/Resources/PortFacilities/treasure_worktable_background_v3.png',
        'Assets/_Project/Resources/TreasureHunt/treasure_chart_weathered.png',
        'Assets/_Project/Resources/TreasureHunt/treasure_chart_gilded.png',
        'Assets/_Project/Resources/TreasureHunt/treasure_chart_moonlit.png',
        'Assets/_Project/Scenes/UIPreviews/UI_Warehouse.unity',
        'Assets/_Project/Scenes/UIPreviews/UI_TreasureMap.unity'
    )) { Copy-StyleFile $styleRelative }
    foreach ($stylePrefix in @('UI_Warehouse','UI_TreasureMap')) {
        foreach ($styleAsset in Get-ChildItem -LiteralPath (Join-Path $styleSource 'Assets/_Project/Presentation/EditablePreviewAssets') -Filter ($stylePrefix + '*') -File) {
            Copy-StyleFile ('Assets/_Project/Presentation/EditablePreviewAssets/' + $styleAsset.Name)
        }
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Capture.cs') -Destination (Join-Path $styleProject 'Assets/_Project/Editor/CollectionTreasureStyleCapture.cs') -Force
    $styleSettingsPath = Join-Path $styleProject 'ProjectSettings/ProjectSettings.asset'
    $styleSettings = [IO.File]::ReadAllText($styleSettingsPath)
    $styleSettings = [regex]::Replace($styleSettings,'(?m)^  companyName:.*$','  companyName: CollectionTreasureStyleValidation20261014')
    $styleSettings = [regex]::Replace($styleSettings,'(?m)^  productName:.*$','  productName: CollectionTreasureStyleValidation20261014')
    [IO.File]::WriteAllText($styleSettingsPath,$styleSettings,[Text.UTF8Encoding]::new($false))
    $styleManifest = @()
    foreach ($styleRelative in @('Assets/_Project/Scenes/UIPreviews/UI_Warehouse.unity','Assets/_Project/Scenes/UIPreviews/UI_TreasureMap.unity')) {
        $styleManifest += [pscustomobject]@{path=$styleRelative;sha256=(Get-FileHash -LiteralPath (Join-Path $styleSource $styleRelative) -Algorithm SHA256).Hash}
    }
    foreach ($stylePrefix in @('UI_Warehouse','UI_TreasureMap')) {
        foreach ($styleAsset in Get-ChildItem -LiteralPath (Join-Path $styleSource 'Assets/_Project/Presentation/EditablePreviewAssets') -Filter ($stylePrefix + '*') -File) {
            $styleManifest += [pscustomobject]@{path=('Assets/_Project/Presentation/EditablePreviewAssets/' + $styleAsset.Name);sha256=(Get-FileHash -LiteralPath $styleAsset.FullName -Algorithm SHA256).Hash}
        }
    }
    $styleManifestPath = Join-Path $styleOutput 'scene-production-before.json'
    if (-not (Test-Path -LiteralPath $styleManifestPath)) {
        $styleManifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $styleManifestPath -Encoding utf8
    }
    Write-Output 'Prepared isolated source, targeted facility art and two preview scenes. Warm Library retained; player preference identity isolated.'
    exit 0
}
$styleRun = Join-Path $styleOutput $RunName
if (Test-Path -LiteralPath $styleRun) { throw 'Use a new RunName.' }
[IO.Directory]::CreateDirectory($styleRun) | Out-Null
$styleArgs = @('-batchmode','-force-d3d11','-projectPath',$styleProject,'-logFile',(Join-Path $styleRun ($Stage + '.log')))
if ($Stage -eq 'PlayMode' -or $Stage -eq 'EditMode') {
    if (-not $TestFilter) {
        if ($Stage -eq 'EditMode') { $TestFilter = 'WarehouseEditorPreviewTests;WarehouseRedrawLayoutTests;TreasureMapPresentationTests;ParchmentFrameLayoutTests.TreasureFolio' }
        else { $TestFilter = 'Warehouse_SelectDisplayMoveRemove;WarehouseRetainedView_;WarehouseSorting_;PortWarehouseTrading_;RareWarehouse_TabsSixRows;TreasureFolio_PointerBrowsing;TreasureFragments_IdentifyAssemble' }
    }
    $styleArgs += @('-runTests','-testPlatform',$Stage,'-testFilter',$TestFilter,'-testResults',(Join-Path $styleRun 'results.xml'))
    if ($Stage -eq 'PlayMode') { $styleArgs += @('-navalVisualCapturePath',(Join-Path $styleRun 'LiveScreens')) }
} elseif ($Stage -eq 'Capture') {
    $env:COLLECTION_TREASURE_CAPTURE_DIR = Join-Path $styleRun 'Screens'
    $styleArgs += @('-quit','-executeMethod','ShatteredSail.Editor.CollectionTreasureStyleCapture.Run')
} else {
    $env:COLLECTION_TREASURE_PREVIEW_BACKUP = Join-Path $styleRun 'SceneBackup'
    $styleArgs += @('-quit','-executeMethod','ShatteredSail.Editor.CollectionTreasureStyleCapture.SyncPreviews')
}
$styleQuoted = $styleArgs | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }
$styleProcess = Start-Process -FilePath $styleUnity -WindowStyle Hidden -ArgumentList $styleQuoted -PassThru
Write-Output ('Unity ' + $Stage + ' PID ' + $styleProcess.Id + '; output=' + $styleRun)
$styleProcess.WaitForExit()
$styleProcess.Refresh()
$styleExit = $styleProcess.ExitCode
Write-Output ('Unity exit ' + $styleExit)
if ($styleExit -ne 0) { throw ('Unity failed; inspect ' + $styleRun) }
if ($Stage -eq 'PlayMode' -or $Stage -eq 'EditMode') {
    [xml]$styleResults = Get-Content -Raw -LiteralPath (Join-Path $styleRun 'results.xml')
    if ($styleResults.'test-run'.result -ne 'Passed' -or [int]$styleResults.'test-run'.passed -le 0) { throw 'Tests failed or no selected tests ran.' }
    Write-Output ('Passed ' + $styleResults.'test-run'.passed + ' tests.')
}
