param([ValidateSet('EditMode','PlayMode')][string]$Mode = 'EditMode')
$ErrorActionPreference = 'Stop'
$thunderWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$thunderProject = Join-Path $thunderWorkspace 'tmp/card-keyword-tooltip-20261023-validation'
$thunderCompile = Join-Path $thunderWorkspace 'Artifacts/ThunderRules20261024/Compile'
$thunderOutput = Join-Path $thunderWorkspace 'Artifacts/ThunderRules20261024/UI'
if (!(Test-Path -LiteralPath (Join-Path $thunderProject 'OWNED_CARD_KEYWORD_VALIDATION.txt'))) { throw 'Owned isolated validation project required.' }
if (Test-Path -LiteralPath (Join-Path $thunderProject 'Temp/UnityLockfile')) { throw 'Validation project is already running.' }
New-Item -ItemType Directory -Force -Path $thunderOutput | Out-Null
foreach ($thunderAssembly in @('Core','Content','Persistence','Presentation')) {
    Copy-Item -LiteralPath (Join-Path $thunderCompile ('ShatteredSail.' + $thunderAssembly + '.dll')) -Destination (Join-Path $thunderProject 'Assets/Plugins') -Force
}
foreach ($thunderTest in @('CoreBattleCardKeywordTextTests','CoreSchoolCheckpointBoundaryTests','CardSchoolPresentationTests','CardDescriptionTextTests','NavalFieldMotionTests','NavalFieldPresentationTests','CardSupplyCostPresentationTests','CoreBoardingResolvedAttackTests')) {
    Copy-Item -LiteralPath (Join-Path $thunderWorkspace ('UnityClient/Assets/_Project/Tests/EditMode/' + $thunderTest + '.cs')) -Destination (Join-Path $thunderProject 'Assets/Tests/Editor') -Force
}
Copy-Item -LiteralPath (Join-Path $thunderWorkspace 'UnityClient/Assets/_Project/Tests/PlayMode/CardKeywordTooltipTests.cs') -Destination (Join-Path $thunderProject 'Assets/Tests/PlayMode') -Force
foreach ($thunderTest in @('NavalFieldSceneTests','NavalFieldStatusMotionTests')) { Copy-Item -LiteralPath (Join-Path $thunderWorkspace ('UnityClient/Assets/_Project/Tests/PlayMode/' + $thunderTest + '.cs')) -Destination (Join-Path $thunderProject 'Assets/Tests/PlayMode') -Force }
$thunderFilter = if ($Mode -eq 'EditMode') {
    'ShatteredSail.Core.Tests.CoreBattleCardKeywordTextTests;ShatteredSail.Core.Tests.CoreSchoolCheckpointBoundaryTests;ShatteredSail.Core.Tests.CardSchoolPresentationTests.SchoolHud_ShowsManaCurseTicksAndCurrentTideWithoutRetiredReactionState;ShatteredSail.Core.Tests.NavalFieldMotionTests;ShatteredSail.Core.Tests.NavalFieldPresentationTests;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.ThunderUtilityCardsHaveNoChainCostBadgeAtAnyRank;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.AllResourceCardsDisplayXOnTheMatchingIconAtEveryRank;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.FieldComponentsDoNotDisplayRetiredSupplyOrSchoolResourceCostsAtAnyRank;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.TideBarrierCostsTwoAndPermanentConversionHasNoResourcePaymentAtEveryRank;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.CurseRequirementsNeverDisplayAnActualMarkPayment;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.CurseHealingMedicineBadgeTracksItsMarkRequirement;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.NoMedicineFallback_AlsoRemovesTheLinkedSchoolPayment;ShatteredSail.Core.Tests.CoreBoardingResolvedAttackTests'
} else { 'ShatteredSail.Presentation.Tests.CardKeywordTooltipTests;ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.NavalField;ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.EnemyCurrent' }
$thunderResult = Join-Path $thunderOutput ($Mode + '.xml')
$thunderLog = Join-Path $thunderOutput ($Mode + '.log')
$thunderStarted = [DateTime]::UtcNow
$thunderArguments = @('-batchmode','-nographics','-projectPath',('"' + $thunderProject + '"'),'-runTests','-testPlatform',$Mode,
    '-testFilter',$thunderFilter,'-testResults',('"' + $thunderResult + '"'),'-logFile',('"' + $thunderLog + '"'))
$thunderProcess = Start-Process -FilePath 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' -ArgumentList $thunderArguments -WindowStyle Hidden -PassThru
$thunderProcess.WaitForExit()
if ($thunderProcess.ExitCode -ne 0) { throw ('Unity validation failed: ' + $thunderLog) }
if (!(Test-Path -LiteralPath $thunderResult)) { throw 'Unity produced no test result.' }
if ((Get-Item -LiteralPath $thunderResult).LastWriteTimeUtc -lt $thunderStarted.AddSeconds(-2)) { throw 'Unity did not replace the old test result.' }
[xml]$thunderXml = Get-Content -LiteralPath $thunderResult
$thunderRun = $thunderXml.'test-run'
Write-Output ($Mode + ': ' + $thunderRun.result + ' total=' + $thunderRun.total + ' passed=' + $thunderRun.passed + ' failed=' + $thunderRun.failed)
if ($thunderRun.result -ne 'Passed' -or [int]$thunderRun.total -lt 1 -or [int]$thunderRun.failed -ne 0) { throw 'Some focused UI tests failed.' }
