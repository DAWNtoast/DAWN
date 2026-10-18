param([ValidateSet('Edit','Play','Capture')][string]$Stage,
    [string]$Output = 'D:/Game/Artifacts/ShipDetailTavern20261014/validation',
    [switch]$CaptureInteractions,
    [switch]$HeaderReturn)
$ErrorActionPreference = 'Stop'
$detailProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../tmp/shipyard-detail-clean-20261011')).TrimEnd('\','/')
$detailUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe'
$detailRunning = @(Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'Unity.exe' -and $_.CommandLine -and $_.CommandLine.Replace('\','/').Contains($detailProject.Replace('\','/')) })
if ($detailRunning.Count -gt 0) { throw 'Isolated validation project is occupied.' }
[IO.Directory]::CreateDirectory($Output) | Out-Null
$detailArgs = @('-batchmode','-projectPath',('"' + $detailProject + '"'),'-logFile',('"' + (Join-Path $Output ($Stage.ToLowerInvariant() + '.log')) + '"'))
if ($Stage -eq 'Capture') {
    $detailArgs += @('-quit','-executeMethod','ShatteredSail.Editor.ShipDetailTavernCapture.CaptureFromCommandLine', '-shipDetailCapturePath',('"' + (Join-Path $Output 'captures') + '"'))
    if ($HeaderReturn) { $detailArgs += '-shipDetailSingleFrame' }
} else {
    $detailFilter = if ($Stage -eq 'Edit') {
        'ShatteredSail.Presentation.Tests.ShipyardGalleryTests'
    } else {
        'ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.ShipyardEquipment_ComparisonFollowsRealSelectionReplacementAndEmptySlots;ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.ShipyardEquipment_RealClicksReplaceUnloadAndRetainIndependentVesselsAfterReload;ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.ShipyardEquipment_UnownedVesselsCannotInstallEvenThroughRetainedCallbacks'
    }
    if ($HeaderReturn) {
        $detailFilter = if ($Stage -eq 'Edit') {
            'ShatteredSail.Presentation.Tests.ShipyardGalleryTests;ShatteredSail.Presentation.Tests.VoyageDepartureShipLayoutTests.DeparturePreviewOpensEquipmentAndAllTabsReturnToTheSamePortsDeparture;ShatteredSail.Presentation.Tests.VoyageDepartureShipLayoutTests.ExplicitShipyardPreviewConfigurationClearsAPreviousDepartureReturnTarget'
        } else {
            'ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.ShipyardEquipment_ComparisonFollowsRealSelectionReplacementAndEmptySlots;ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.DepartureShips_DossierRefitsSelectedVesselAndReturnsWithRouteSeedDecksBeforeSailing;ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.DeckBuilderSpecial_ComponentConfigurationReturnsToSameStagePageAndUpdatesMembership;ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.DepartureFacilityReturn_MarketAlwaysReturnsToPortAndRetainsSelectedVoyage'
        }
    }
    $detailArgs += @('-runTests','-testPlatform',($Stage + 'Mode'),'-testFilter',('"' + $detailFilter + '"'),'-testResults',('"' + (Join-Path $Output ($Stage.ToLowerInvariant() + '-results.xml')) + '"'))
    if ($Stage -eq 'Play' -and $CaptureInteractions) {
        $detailArgs += @('-navalVisualCapturePath', ('"' + (Join-Path $Output 'interactions') + '"'))
    }
}
$detailProcess = Start-Process -FilePath $detailUnity -ArgumentList $detailArgs -WorkingDirectory $detailProject -WindowStyle Hidden -PassThru
Write-Output ("Started {0} validation, PID {1}, log {2}" -f $Stage,$detailProcess.Id,(Join-Path $Output ($Stage.ToLowerInvariant() + '.log')))
