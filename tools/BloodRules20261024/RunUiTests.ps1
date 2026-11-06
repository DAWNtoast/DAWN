param([ValidateSet('EditMode','PlayMode')][string]$Mode = 'EditMode')
$ErrorActionPreference = 'Stop'
$bloodWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$bloodProject = Join-Path $bloodWorkspace 'tmp/card-keyword-tooltip-20261023-validation'
$bloodCompile = Join-Path $bloodWorkspace 'Artifacts/BloodRules20261024/Compile'
$bloodOutput = Join-Path $bloodWorkspace 'Artifacts/BloodRules20261024/UI'
if (!(Test-Path -LiteralPath (Join-Path $bloodProject 'OWNED_CARD_KEYWORD_VALIDATION.txt'))) { throw 'Owned isolated validation project required.' }
if (Test-Path -LiteralPath (Join-Path $bloodProject 'Temp/UnityLockfile')) { throw 'Validation project is already running.' }
New-Item -ItemType Directory -Force -Path $bloodOutput | Out-Null
foreach ($bloodAssembly in @('Core','Content','Persistence','Presentation')) {
    Copy-Item -LiteralPath (Join-Path $bloodCompile ('ShatteredSail.' + $bloodAssembly + '.dll')) -Destination (Join-Path $bloodProject 'Assets/Plugins') -Force
}
foreach ($bloodTest in @('CoreBattleCardKeywordTextTests','CoreSchoolCheckpointBoundaryTests','CardSchoolPresentationTests','CardDescriptionTextTests','NavalFieldMotionTests','NavalFieldPresentationTests','CardSupplyCostPresentationTests','CoreBoardingResolvedAttackTests')) {
    Copy-Item -LiteralPath (Join-Path $bloodWorkspace ('UnityClient/Assets/_Project/Tests/EditMode/' + $bloodTest + '.cs')) -Destination (Join-Path $bloodProject 'Assets/Tests/Editor') -Force
}
Copy-Item -LiteralPath (Join-Path $bloodWorkspace 'UnityClient/Assets/_Project/Tests/PlayMode/CardKeywordTooltipTests.cs') -Destination (Join-Path $bloodProject 'Assets/Tests/PlayMode') -Force
foreach ($bloodTest in @('NavalFieldSceneTests','NavalFieldStatusMotionTests')) { Copy-Item -LiteralPath (Join-Path $bloodWorkspace ('UnityClient/Assets/_Project/Tests/PlayMode/' + $bloodTest + '.cs')) -Destination (Join-Path $bloodProject 'Assets/Tests/PlayMode') -Force }
$bloodFilter = if ($Mode -eq 'EditMode') {
    'ShatteredSail.Core.Tests.CoreBattleCardKeywordTextTests;ShatteredSail.Core.Tests.CoreSchoolCheckpointBoundaryTests;ShatteredSail.Core.Tests.CardSchoolPresentationTests.SchoolHud_ShowsManaCurseTicksAndCurrentTideWithoutRetiredReactionState;ShatteredSail.Core.Tests.NavalFieldMotionTests;ShatteredSail.Core.Tests.NavalFieldPresentationTests;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.OptionalBloodFeeBadgeUsesActualAffordablePayment;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.DesperateCannonBadgeRequiresBothDesperationAndAffordablePayment;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.ThunderUtilityCardsHaveNoChainCostBadgeAtAnyRank;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.AllResourceCardsDisplayXOnTheMatchingIconAtEveryRank;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.FieldComponentsDoNotDisplayRetiredSupplyOrSchoolResourceCostsAtAnyRank;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.TideBarrierCostsTwoAndPermanentConversionHasNoResourcePaymentAtEveryRank;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.CurseRequirementsNeverDisplayAnActualMarkPayment;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.CurseHealingMedicineBadgeTracksItsMarkRequirement;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.NoMedicineFallback_AlsoRemovesTheLinkedSchoolPayment;ShatteredSail.Core.Tests.CoreBoardingResolvedAttackTests'
} else { 'ShatteredSail.Presentation.Tests.CardKeywordTooltipTests;ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.NavalField;ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.EnemyCurrent' }
$bloodResult = Join-Path $bloodOutput ($Mode + '.xml')
$bloodLog = Join-Path $bloodOutput ($Mode + '.log')
$bloodStarted = [DateTime]::UtcNow
$bloodArguments = @('-batchmode','-nographics','-projectPath',('"' + $bloodProject + '"'),'-runTests','-testPlatform',$Mode,
    '-testFilter',$bloodFilter,'-testResults',('"' + $bloodResult + '"'),'-logFile',('"' + $bloodLog + '"'))
$bloodProcess = Start-Process -FilePath 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' -ArgumentList $bloodArguments -WindowStyle Hidden -PassThru
$bloodProcess.WaitForExit()
if ($bloodProcess.ExitCode -ne 0) { throw ('Unity validation failed: ' + $bloodLog) }
if (!(Test-Path -LiteralPath $bloodResult)) { throw 'Unity produced no test result.' }
if ((Get-Item -LiteralPath $bloodResult).LastWriteTimeUtc -lt $bloodStarted.AddSeconds(-2)) { throw 'Unity did not replace the old test result.' }
[xml]$bloodXml = Get-Content -LiteralPath $bloodResult
$bloodRun = $bloodXml.'test-run'
Write-Output ($Mode + ': ' + $bloodRun.result + ' total=' + $bloodRun.total + ' passed=' + $bloodRun.passed + ' failed=' + $bloodRun.failed)
if ($bloodRun.result -ne 'Passed' -or [int]$bloodRun.total -lt 1 -or [int]$bloodRun.failed -ne 0) { throw 'Some focused UI tests failed.' }
