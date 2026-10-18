param([ValidateSet('Edit','Play','Capture','Prelude','Import')][string]$Stage,
    [string]$Output = 'D:/Game/Artifacts/FleetRedesign20261014/validation')
$ErrorActionPreference = 'Stop'
$fleetProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../tmp/shipyard-detail-clean-20261011')).TrimEnd('\','/')
$fleetUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe'
$fleetRunning = @(Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'Unity.exe' -and $_.CommandLine -and $_.CommandLine.Replace('\','/').Contains($fleetProject.Replace('\','/')) })
if ($fleetRunning.Count -gt 0) { throw 'Isolated validation project is occupied.' }
[IO.Directory]::CreateDirectory($Output) | Out-Null
$fleetArgs = @('-batchmode','-projectPath',('"' + $fleetProject + '"'),'-logFile',('"' + (Join-Path $Output ($Stage.ToLowerInvariant() + '.log')) + '"'))
if ($Stage -eq 'Import') {
    $fleetArgs += @('-quit','-executeMethod','ShatteredSail.Editor.FleetRedrawCapture.RefreshValidationImports')
} elseif ($Stage -eq 'Capture' -or $Stage -eq 'Prelude') {
    $fleetMethod = if ($Stage -eq 'Prelude') { 'ShatteredSail.Editor.FleetRedrawCapture.CapturePreludeFromCommandLine' } else { 'ShatteredSail.Editor.FleetRedrawCapture.CaptureFromCommandLine' }
    $fleetFolder = if ($Stage -eq 'Prelude') { 'preludes' } else { 'captures' }
    $fleetArgs += @('-quit','-executeMethod',$fleetMethod,
        '-fleetCapturePath',('"' + (Join-Path $Output $fleetFolder) + '"'))
} else {
    if ($Stage -eq 'Edit') {
        $fleetFilter = 'ShatteredSail.Core.Tests.CoreWarshipRosterTests;ShatteredSail.Core.Tests.SpecialWarshipArtCatalogTests;ShatteredSail.Core.Tests.CoreVoyageCheckpointTests;ShatteredSail.Core.Tests.CoreSeaChartProgressTests;ShatteredSail.Core.Tests.LegendaryWarshipTests;ShatteredSail.Core.Tests.CoreCollectionProgressionTests;ShatteredSail.Presentation.Tests.BoardingBattleArtCatalogTests;ShatteredSail.Presentation.Tests.ShipyardGalleryTests;ShatteredSail.Core.Tests.BattleShipCutoutTests;ShatteredSail.Core.Tests.BattleShipTurnCutoutTests;ShatteredSail.Presentation.Tests.UiImageAspectGeometryTests'
    } else {
        $fleetFilter = 'ShatteredSail.Presentation.Tests.NavalBattleVisualFeedbackTests;ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.ShipVisuals_AllTwelveOwnedShipsRetainBaseHullAfterRefitsAndAtDeparture;ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.NavigationFlagship_MapMarkerMatchesEverySelectedVesselWithoutBlockingCourseInput;ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.ShipyardEquipment_RealClicksReplaceUnloadAndRetainIndependentVesselsAfterReload;ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.ShipyardEquipment_UnownedVesselsCannotInstallEvenThroughRetainedCallbacks'
    }
    $fleetArgs += @('-runTests','-testPlatform',($Stage + 'Mode'),'-testFilter',('"' + $fleetFilter + '"'),'-testResults',('"' + (Join-Path $Output ($Stage.ToLowerInvariant() + '-results.xml')) + '"'))
}
$fleetProcess = Start-Process -FilePath $fleetUnity -ArgumentList $fleetArgs -WorkingDirectory $fleetProject -WindowStyle Hidden -PassThru
Write-Output ("Started {0} validation, PID {1}, log {2}" -f $Stage,$fleetProcess.Id,(Join-Path $Output ($Stage.ToLowerInvariant() + '.log')))
