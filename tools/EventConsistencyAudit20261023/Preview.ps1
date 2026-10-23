param([string]$UnityEditor = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$reviewWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
# Reuse the explicitly owned disposable preview's existing navigation assets and packages.
# No scene is loaded from or saved into the game project.
$reviewProject = Join-Path $reviewWorkspace 'tmp/boss-encounter-scenes-20261023-preview'
if (-not (Test-Path -LiteralPath (Join-Path $reviewProject 'OWNED_BOSS_SCENE_PREVIEW.txt'))) { throw 'Expected the owned existing preview fixture.' }
$reviewLock = Join-Path $reviewProject 'Temp/UnityLockfile'
if (Test-Path -LiteralPath $reviewLock) {
    $reviewRunning = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -like ('*' + $reviewProject + '*') }
    if ($reviewRunning) { throw 'Preview project is running.' }
    Remove-Item -LiteralPath $reviewLock -Force
}
$reviewOutput = Join-Path $reviewWorkspace 'Artifacts/EventConsistencyAudit20261023/Previews'
$reviewResources = Join-Path $reviewProject 'Assets/Resources/EncounterIllustrations/Reviewed20261023'
New-Item -ItemType Directory -Force -Path $reviewOutput,$reviewResources | Out-Null
$reviewCompile = Join-Path $reviewWorkspace 'Artifacts/EventIllustrations20261022/Compile'
foreach ($reviewDll in @('ShatteredSail.Core.dll','ShatteredSail.Content.dll','ShatteredSail.Persistence.dll','ShatteredSail.Presentation.dll')) {
    Copy-Item -LiteralPath (Join-Path $reviewCompile $reviewDll) -Destination (Join-Path $reviewProject 'Assets/Plugins') -Force
}
$reviewSource = Join-Path $reviewWorkspace 'UnityClient/Assets/_Project/Resources/EncounterIllustrations/Reviewed20261023'
# Only the installed reviewed PNGs and metadata are copied. Missing future images are not synthesized.
Get-ChildItem -LiteralPath $reviewSource -File | Copy-Item -Destination $reviewResources -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Capture.cs') -Destination (Join-Path $reviewProject 'Assets/Editor/EventConsistencyCapture.cs') -Force
$reviewArgs = @('-batchmode','-projectPath',('"'+$reviewProject+'"'),'-executeMethod','ShatteredSail.EventConsistencyReview.Capture.Run','-eventReviewOutput',('"'+$reviewOutput+'"'),'-logFile',('"'+(Join-Path $reviewOutput 'editor.log')+'"'),'-quit')
$reviewStarted = [DateTime]::UtcNow
$reviewProcess = Start-Process -FilePath $UnityEditor -ArgumentList $reviewArgs -WindowStyle Hidden -PassThru
$reviewProcess.WaitForExit()
if ($reviewProcess.ExitCode -ne 0) { throw "Preview failed. Read $reviewOutput/editor.log" }
$reviewReport = Join-Path $reviewOutput 'report.json'
if ((Get-Item -LiteralPath $reviewReport).LastWriteTimeUtc -lt $reviewStarted.AddSeconds(-2)) { throw 'Preview report is stale.' }
$reviewData = Get-Content -LiteralPath $reviewReport -Raw | ConvertFrom-Json
if (@($reviewData.frames).Count -ne 7 -or @($reviewData.frames | Where-Object { $_.error -or -not $_.verified }).Count -gt 0) { throw 'Preview verification failed.' }
Write-Output $reviewReport
