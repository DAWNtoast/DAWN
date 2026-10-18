param(
    [ValidateSet('Prepare','EditMode','PlayMode','Capture','PreviewSync')][string]$Stage = 'Prepare',
    [string]$RunName = 'run1',
    [string]$TestFilter = 'MarketScene_;TradeMarket_LocalResale;MarketStorage_DefaultShip;MarketStorage_AskWaits;MarketStorage_AskRejects'
)
$ErrorActionPreference = 'Stop'
$marketSource = 'D:\Game\UnityClient'
$marketProject = 'D:\Game\tmp\departure-dock-scene-20261012'
$marketOutput = 'D:\Game\Artifacts\MarketShopLayout20261014\validation'
$marketUnity = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
if ($RunName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'RunName must be a simple identifier.' }
if (-not (Test-Path -LiteralPath (Join-Path $marketProject 'Library'))) { throw 'Expected warm isolated project is missing.' }
$marketProcesses = @(Get-CimInstance Win32_Process -Filter "name='Unity.exe'")
if ($marketProcesses | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($marketProject) }) { throw 'Isolated project already running.' }
$marketLock = Join-Path $marketProject 'Temp\UnityLockfile'
if (Test-Path -LiteralPath $marketLock) {
    try { $marketStream = [IO.File]::Open($marketLock,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None); $marketStream.Dispose() }
    catch { throw 'Isolated project already open.' }
}
[IO.Directory]::CreateDirectory($marketOutput) | Out-Null
if ($Stage -eq 'Prepare') {
    # Source changes are small. Reuse imported textures and the warm Library;
    # import only new market art instead of mirroring gigabytes of unrelated assets.
    foreach ($marketFile in Get-ChildItem -LiteralPath (Join-Path $marketSource 'Assets\_Project') -File -Recurse | Where-Object { $_.Extension -in @('.cs','.asmdef') }) {
        $marketRelative = $marketFile.FullName.Substring($marketSource.Length + 1)
        $marketDestination = Join-Path $marketProject $marketRelative
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($marketDestination)) | Out-Null
        Copy-Item -LiteralPath $marketFile.FullName -Destination $marketDestination -Force
        if (Test-Path -LiteralPath ($marketFile.FullName + '.meta')) { Copy-Item -LiteralPath ($marketFile.FullName + '.meta') -Destination ($marketDestination + '.meta') -Force }
    }
    foreach ($marketRelative in @('Assets/_Project/Resources/PortFacilities/Market')) {
        & robocopy (Join-Path $marketSource $marketRelative) (Join-Path $marketProject $marketRelative) /E /R:1 /W:1 /NFL /NDL /NP /NJH /NJS | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "Copy failed: $marketRelative" }
    }
    foreach ($marketRelative in @('Assets/_Project/Scenes/UIPreviews/UI_Market.unity')) {
        Copy-Item -LiteralPath (Join-Path $marketSource $marketRelative) -Destination (Join-Path $marketProject $marketRelative) -Force
        Copy-Item -LiteralPath (Join-Path $marketSource ($marketRelative + '.meta')) -Destination (Join-Path $marketProject ($marketRelative + '.meta')) -Force
    }
    foreach ($marketAsset in Get-ChildItem -LiteralPath (Join-Path $marketSource 'Assets/_Project/Presentation/EditablePreviewAssets') -Filter 'UI_Market*' -File) {
        Copy-Item -LiteralPath $marketAsset.FullName -Destination (Join-Path $marketProject ('Assets/_Project/Presentation/EditablePreviewAssets/' + $marketAsset.Name)) -Force
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'MarketSceneCapture.cs') -Destination (Join-Path $marketProject 'Assets/_Project/Editor/MarketSceneCapture.cs') -Force
    $marketSettingsPath = Join-Path $marketProject 'ProjectSettings/ProjectSettings.asset'
    $marketSettings = [IO.File]::ReadAllText($marketSettingsPath)
    $marketSettings = [regex]::Replace($marketSettings,'(?m)^  companyName:.*$','  companyName: MarketShopLayoutValidation20261014')
    $marketSettings = [regex]::Replace($marketSettings,'(?m)^  productName:.*$','  productName: MarketShopLayoutValidation20261014')
    [IO.File]::WriteAllText($marketSettingsPath,$marketSettings,[Text.UTF8Encoding]::new($false))
    Write-Output 'Prepared isolated source, market resources/scenes and separate PlayerPrefs identity. Warm Library retained.'
    exit 0
}
$marketRun = Join-Path $marketOutput $RunName
if (Test-Path -LiteralPath $marketRun) { throw 'Use a new RunName.' }
[IO.Directory]::CreateDirectory($marketRun) | Out-Null
$marketArgs = @('-batchmode','-force-d3d11','-projectPath',$marketProject,'-logFile',(Join-Path $marketRun ($Stage + '.log')))
if ($Stage -eq 'PlayMode' -or $Stage -eq 'EditMode') {
    $marketArgs += @('-runTests','-testPlatform',$Stage,'-testFilter',$TestFilter,'-testResults',(Join-Path $marketRun 'results.xml'))
    if ($Stage -eq 'PlayMode') { $marketArgs += @('-navalVisualCapturePath',(Join-Path $marketRun 'LiveScreens')) }
} elseif ($Stage -eq 'Capture') {
    $env:MARKET_SCENE_CAPTURE_DIR = Join-Path $marketRun 'Screens'
    $marketArgs += @('-quit','-executeMethod','ShatteredSail.Editor.MarketSceneCapture.Run')
} else {
    $env:MARKET_SCENE_SYNC_BACKUP = Join-Path $marketRun 'SceneBackup'
    $marketArgs += @('-quit','-executeMethod','ShatteredSail.Editor.GameplayPreviewSceneBuilder.SyncMarketSceneFromCommandLine')
}
$marketQuoted = $marketArgs | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }
$marketProcess = Start-Process -FilePath $marketUnity -WindowStyle Hidden -ArgumentList $marketQuoted -PassThru
Write-Output ('Unity ' + $Stage + ' PID ' + $marketProcess.Id + '; output=' + $marketRun)
$marketProcess.WaitForExit()
$marketProcess.Refresh()
$marketExit = $marketProcess.ExitCode
Write-Output ('Unity exit ' + $marketExit)
if ($marketExit -ne 0) { throw ('Unity failed; inspect ' + $marketRun) }
if ($Stage -eq 'PlayMode' -or $Stage -eq 'EditMode') {
    [xml]$marketResults = Get-Content -Raw -LiteralPath (Join-Path $marketRun 'results.xml')
    if ($marketResults.'test-run'.result -ne 'Passed') { throw 'PlayMode failed.' }
    Write-Output ('Passed ' + $marketResults.'test-run'.passed + ' tests.')
}
