param([ValidateSet('EditMode','PlayMode')][string]$Mode = 'EditMode')
$ErrorActionPreference = 'Stop'
$boardingWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$boardingProject = Join-Path $boardingWorkspace 'tmp/card-keyword-tooltip-20261023-validation'
$boardingCompile = Join-Path $boardingWorkspace 'Artifacts/BoardingFieldBalance20261024/Compile'
$boardingOutput = Join-Path $boardingWorkspace 'Artifacts/BoardingFieldBalance20261024/UI'
if (!(Test-Path -LiteralPath (Join-Path $boardingProject 'OWNED_CARD_KEYWORD_VALIDATION.txt'))) { throw 'Owned isolated validation project required.' }
if (Test-Path -LiteralPath (Join-Path $boardingProject 'Temp/UnityLockfile')) { throw 'Validation project is already running.' }
New-Item -ItemType Directory -Force -Path $boardingOutput | Out-Null
foreach ($boardingAssembly in @('Core','Content','Persistence','Presentation')) {
    Copy-Item -LiteralPath (Join-Path $boardingCompile ('ShatteredSail.' + $boardingAssembly + '.dll')) -Destination (Join-Path $boardingProject 'Assets/Plugins') -Force
}
foreach ($boardingTest in @('CoreBattleCardKeywordTextTests','CoreSchoolCheckpointBoundaryTests','CardSchoolPresentationTests','CardDescriptionTextTests','NavalFieldMotionTests','NavalFieldPresentationTests','CardSupplyCostPresentationTests','CardResourceBadgeTests','CoreBoardingResolvedAttackTests')) {
    Copy-Item -LiteralPath (Join-Path $boardingWorkspace ('UnityClient/Assets/_Project/Tests/EditMode/' + $boardingTest + '.cs')) -Destination (Join-Path $boardingProject 'Assets/Tests/Editor') -Force
}
Copy-Item -LiteralPath (Join-Path $boardingWorkspace 'UnityClient/Assets/_Project/Tests/PlayMode/CardKeywordTooltipTests.cs') -Destination (Join-Path $boardingProject 'Assets/Tests/PlayMode') -Force
# CardResourceBadgeTests shares the existing guided-session factory with this fixture.
Copy-Item -LiteralPath (Join-Path $boardingWorkspace 'UnityClient/Assets/_Project/Tests/EditMode/CoreGuidedBossScriptTests.cs') -Destination (Join-Path $boardingProject 'Assets/Tests/Editor') -Force
foreach ($boardingTest in @('NavalFieldSceneTests','NavalFieldStatusMotionTests','BattleCompactHudTests')) { Copy-Item -LiteralPath (Join-Path $boardingWorkspace ('UnityClient/Assets/_Project/Tests/PlayMode/' + $boardingTest + '.cs')) -Destination (Join-Path $boardingProject 'Assets/Tests/PlayMode') -Force }
$boardingFilter = if ($Mode -eq 'EditMode') {
    'ShatteredSail.Core.Tests.CoreBattleCardKeywordTextTests;ShatteredSail.Core.Tests.CoreSchoolCheckpointBoundaryTests;ShatteredSail.Core.Tests.CardSchoolPresentationTests.SchoolHud_ShowsManaCurseTicksAndCurrentTideWithoutRetiredReactionState;ShatteredSail.Core.Tests.NavalFieldMotionTests;ShatteredSail.Core.Tests.NavalFieldPresentationTests;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.OptionalBloodFeeBadgeUsesActualAffordablePayment;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.DesperateCannonBadgeRequiresBothDesperationAndAffordablePayment;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.NonFinisherThunderCardsHaveNoChainCostBadgeAtAnyRank;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.OnlyTheThreePublishedNavalFinishersDisplayChainCostBadges;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.AllResourceCardsDisplayXOnTheMatchingIconAtEveryRank;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.FieldComponentsDoNotDisplayRetiredSupplyOrSchoolResourceCostsAtAnyRank;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.TideBarrierCostsFourAndPermanentConversionHasNoResourcePaymentAtEveryRank;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.CurseRequirementsNeverDisplayAnActualMarkPayment;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.CurseHealingMedicineBadgeTracksItsMarkRequirement;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.NoMedicineFallback_AlsoRemovesTheLinkedSchoolPayment;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.TideHealingBadgesMatchActualPaymentWhenMedicineExists;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.BoardingMedicineBadge_RespectsFallbackAndPayOnCast;ShatteredSail.Core.Tests.CardResourceBadgeTests.SpecialResourceNumber_MatchesTheResourceActuallyPaidBySuccessfulPlay;ShatteredSail.Core.Tests.CardResourceBadgeTests.LifePaymentNumber_TracksTheHealthPoolOfTheActualStage;ShatteredSail.Core.Tests.CardResourceBadgeTests.ConsumeAllResourceFacesKeepXWithEmptyAndFundedResources;ShatteredSail.Core.Tests.CardResourceBadgeTests.NavalCircuitThresholdControlsAvailabilityWithoutInventingAChainCostBadge;ShatteredSail.Core.Tests.CoreBoardingResolvedAttackTests'
} else { 'ShatteredSail.Presentation.Tests.CardKeywordTooltipTests;ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.NavalField;ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.EnemyCurrent;ShatteredSail.Presentation.Tests.BattleCompactHudTests.Resources_RefreshInThreeRowsAndActionsStackAboveTheDiscardPile' }
$boardingResult = Join-Path $boardingOutput ($Mode + '.xml')
$boardingLog = Join-Path $boardingOutput ($Mode + '.log')
$boardingStarted = [DateTime]::UtcNow
$boardingArguments = @('-batchmode','-nographics','-projectPath',('"' + $boardingProject + '"'),'-runTests','-testPlatform',$Mode,
    '-testFilter',$boardingFilter,'-testResults',('"' + $boardingResult + '"'),'-logFile',('"' + $boardingLog + '"'))
$boardingProcess = Start-Process -FilePath 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' -ArgumentList $boardingArguments -WindowStyle Hidden -PassThru
$boardingProcess.WaitForExit()
if ($boardingProcess.ExitCode -ne 0) { throw ('Unity validation failed: ' + $boardingLog) }
if (!(Test-Path -LiteralPath $boardingResult)) { throw 'Unity produced no test result.' }
if ((Get-Item -LiteralPath $boardingResult).LastWriteTimeUtc -lt $boardingStarted.AddSeconds(-2)) { throw 'Unity did not replace the old test result.' }
[xml]$boardingXml = Get-Content -LiteralPath $boardingResult
$boardingRun = $boardingXml.'test-run'
Write-Output ($Mode + ': ' + $boardingRun.result + ' total=' + $boardingRun.total + ' passed=' + $boardingRun.passed + ' failed=' + $boardingRun.failed)
if ($boardingRun.result -ne 'Passed' -or [int]$boardingRun.total -lt 1 -or [int]$boardingRun.failed -ne 0) { throw 'Some focused UI tests failed.' }
