param(
    [ValidateSet('Prepare', 'Capture', 'EditMode', 'PlayMode')]
    [string]$Action = 'Prepare',
    [string]$OutputRoot = 'D:\Game\Artifacts\ShipArtRedraw20261012\validation',
    [string]$CaptureName = 'ui-review'
)
$ErrorActionPreference = 'Stop'
$taskWorkspace = [System.IO.Path]::GetFullPath('D:\Game')
$taskSource = [System.IO.Path]::GetFullPath('D:\Game\UnityClient')
$taskScratch = [System.IO.Path]::GetFullPath('D:\Game\tmp\shipyard-detail-clean-20261011')
$taskOutput = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not $taskScratch.StartsWith($taskWorkspace + '\tmp\') -or -not $taskOutput.StartsWith($taskWorkspace + '\')) {
    throw 'Validation scratch/output must stay within D:\Game.'
}
if (-not (Test-Path -LiteralPath (Join-Path $taskScratch 'Library'))) { throw 'Expected cached disposable project missing.' }
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
if ($Action -eq 'Prepare') {
    foreach ($taskDirectory in @('Assets', 'Packages', 'ProjectSettings')) {
        $taskDestination = [System.IO.Path]::GetFullPath((Join-Path $taskScratch $taskDirectory))
        if (-not $taskDestination.StartsWith($taskScratch + '\')) { throw 'Mirror destination escaped scratch.' }
        & robocopy (Join-Path $taskSource $taskDirectory) $taskDestination /MIR /NFL /NDL /NJH /NJS /NP
        if ($LASTEXITCODE -gt 7) { throw "Scratch sync failed: $taskDirectory, code $LASTEXITCODE" }
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ShipArtRedrawCapture.cs') -Destination (Join-Path $taskScratch 'Assets\_Project\Editor\ShipArtRedrawCapture.cs')
    $taskSettings = Join-Path $taskScratch 'ProjectSettings\ProjectSettings.asset'
    $taskText = [System.IO.File]::ReadAllText($taskSettings)
    $taskText = $taskText -replace '(?m)^  companyName:.*$', '  companyName: ShipArtRedrawValidation' -replace '(?m)^  productName:.*$', '  productName: ShipArtRedrawValidation20261012' -replace '(?m)^    Standalone: com\.shatteredsail\.shattered-sail$', '    Standalone: com.validation.shipartredraw20261012'
    [System.IO.File]::WriteAllText($taskSettings, $taskText, [System.Text.UTF8Encoding]::new($false))
    Write-Output "Prepared current source in $taskScratch; no Unity run yet."
    exit 0
}
$taskLog = Join-Path $taskOutput ($Action.ToLowerInvariant() + '.log')
$taskArguments = @('-batchmode', '-projectPath', $taskScratch, '-logFile', $taskLog)
if ($Action -eq 'Capture') {
    $taskCapture = Join-Path $taskOutput $CaptureName
    if (Test-Path -LiteralPath $taskCapture) { throw 'Use a new capture directory name.' }
    $taskArguments += @('-quit', '-executeMethod', 'ShatteredSail.Editor.ShipArtRedrawCapture.CaptureFromCommandLine', '-shipArtCapturePath', $taskCapture)
} else {
    $taskResults = Join-Path $taskOutput ($Action.ToLowerInvariant() + '.xml')
    $taskFilter = if ($Action -eq 'EditMode') {
        'ShatteredSail.Presentation.Tests.ShipyardGalleryTests;ShatteredSail.Core.Tests.BattleShipCutoutTests;ShatteredSail.Core.Tests.BattleShipTurnCutoutTests;ShatteredSail.Presentation.Tests.UiImageAspectGeometryTests'
    } else {
        'ShatteredSail.Presentation.Tests.BattleShipCutoutRenderTests;ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.NavigationFlagship_MapMarkerMatchesEverySelectedVesselWithoutBlockingCourseInput;ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.ShipVisuals_AllTwelveOwnedShipsRetainBaseHullAfterRefitsAndAtDeparture;ShatteredSail.Presentation.Tests.BattlePresentationTests.AllRouteBosses_UseTransparentCutoutAndMaintainSeaContact'
    }
    $taskArguments += @('-runTests', '-testPlatform', $Action, '-testFilter', $taskFilter, '-testResults', $taskResults)
}
$taskProcess = Start-Process -FilePath 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' -WindowStyle Hidden -ArgumentList $taskArguments -Wait -PassThru
Write-Output "Unity $Action exit code: $($taskProcess.ExitCode); log: $taskLog"
exit $taskProcess.ExitCode
