param([string]$UnityEditor = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$previewWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$previewProject = Join-Path $previewWorkspace 'tmp/boss-encounter-scenes-20261023-preview'
$previewMarker = Join-Path $previewProject 'OWNED_BOSS_SCENE_PREVIEW.txt'
if ((Test-Path -LiteralPath $previewProject) -and -not (Test-Path -LiteralPath $previewMarker)) { throw 'Refusing an unowned preview project.' }
$previewLock = Join-Path $previewProject 'Temp/UnityLockfile'
if (Test-Path -LiteralPath $previewLock) {
    $previewRunning = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -like ('*' + $previewProject + '*') }
    if ($previewRunning) { throw 'Preview project is running.' }
    # Compilation failures can leave this owned temporary project's lock file behind.
    Remove-Item -LiteralPath $previewLock -Force
}
$previewSource = Join-Path $previewWorkspace 'UnityClient/Assets/_Project/Resources'
$previewResources = Join-Path $previewProject 'Assets/Resources'
$previewEditor = Join-Path $previewProject 'Assets/Editor'
$previewPlugins = Join-Path $previewProject 'Assets/Plugins'
$previewOutput = Join-Path $previewWorkspace 'Artifacts/BossEncounterScenes20261023/Previews'
New-Item -ItemType Directory -Force -Path $previewResources,$previewEditor,$previewPlugins,$previewOutput,(Join-Path $previewProject 'Packages'),(Join-Path $previewProject 'ProjectSettings') | Out-Null
'Owned disposable Boss scene preview; no game scenes are saved.' | Set-Content -LiteralPath $previewMarker -Encoding utf8
$previewCompile = Join-Path $previewWorkspace 'Artifacts/EventIllustrations20261022/Compile'
foreach ($previewDll in @('ShatteredSail.Core.dll','ShatteredSail.Content.dll','ShatteredSail.Persistence.dll','ShatteredSail.Presentation.dll')) {
    Copy-Item -LiteralPath (Join-Path $previewCompile $previewDll) -Destination $previewPlugins -Force
}
# Copy only existing resources needed for the voyage chrome, two focal scenes and typography.
foreach ($previewFolder in @('Data','Fonts','NavigationUIV3','NavigationLookout','VoyageMapV2','ModularShip','Shaders','LegacyDemo','ShipArtV2','EncounterIllustrations')) {
    $previewFrom = Join-Path $previewSource $previewFolder
    if (Test-Path -LiteralPath $previewFrom) { Copy-Item -LiteralPath $previewFrom -Destination $previewResources -Recurse -Force }
}
$previewPaper = Join-Path $previewResources 'PortFacilities/Market'
New-Item -ItemType Directory -Path $previewPaper -Force | Out-Null
foreach ($previewSuffix in @('.png','.png.meta')) {
    Copy-Item -LiteralPath (Join-Path $previewSource ('PortFacilities/Market/market_shop_panel_v3' + $previewSuffix)) -Destination $previewPaper -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Capture.cs') -Destination $previewEditor -Force
$previewValidation = Join-Path $previewWorkspace 'tmp/boss-encounter-scenes-20261023-validation'
Copy-Item -LiteralPath (Join-Path $previewValidation 'Packages/manifest.json') -Destination (Join-Path $previewProject 'Packages') -Force
Copy-Item -LiteralPath (Join-Path $previewWorkspace 'UnityClient/ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $previewProject 'ProjectSettings') -Force
$previewArgs = @('-batchmode','-projectPath',('"'+$previewProject+'"'),'-executeMethod','ShatteredSail.BossSceneReview.Capture.Run','-bossSceneOutput',('"'+$previewOutput+'"'),'-logFile',('"'+(Join-Path $previewOutput 'editor.log')+'"'),'-quit')
$previewStarted = [DateTime]::UtcNow
$previewProcess = Start-Process -FilePath $UnityEditor -ArgumentList $previewArgs -WindowStyle Hidden -PassThru
$previewProcess.WaitForExit()
if ($previewProcess.ExitCode -ne 0) { throw "Preview failed. Read $previewOutput/editor.log" }
$previewReport = Join-Path $previewOutput 'report.json'
if ((Get-Item -LiteralPath $previewReport).LastWriteTimeUtc -lt $previewStarted.AddSeconds(-2)) { throw 'Preview report is stale.' }
$previewData = Get-Content -LiteralPath $previewReport -Raw | ConvertFrom-Json
if (@($previewData.frames).Count -ne 2 -or @($previewData.frames | Where-Object { $_.error -or -not $_.sourceIsScene -or -not $_.noSeparateShipLayer }).Count -gt 0) { throw 'Preview verification failed.' }
Write-Output $previewReport
