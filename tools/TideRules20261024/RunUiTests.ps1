param([ValidateSet('EditMode','PlayMode')][string]$Mode = 'EditMode')
$ErrorActionPreference = 'Stop'
$tideWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$tideProject = Join-Path $tideWorkspace 'tmp/card-keyword-tooltip-20261023-validation'
$tideCompile = Join-Path $tideWorkspace 'Artifacts/TideRules20261024/Compile'
$tideOutput = Join-Path $tideWorkspace 'Artifacts/TideRules20261024/UI'
if (!(Test-Path -LiteralPath (Join-Path $tideProject 'OWNED_CARD_KEYWORD_VALIDATION.txt'))) { throw 'Owned isolated validation project required.' }
if (Test-Path -LiteralPath (Join-Path $tideProject 'Temp/UnityLockfile')) { throw 'Validation project is already running.' }
New-Item -ItemType Directory -Force -Path $tideOutput | Out-Null
foreach ($tideAssembly in @('Core','Content','Persistence','Presentation')) {
    Copy-Item -LiteralPath (Join-Path $tideCompile ('ShatteredSail.' + $tideAssembly + '.dll')) -Destination (Join-Path $tideProject 'Assets/Plugins') -Force
}
foreach ($tideTest in @('CoreBattleCardKeywordTextTests','CoreSchoolCheckpointBoundaryTests','CardSchoolPresentationTests','CardDescriptionTextTests','NavalFieldMotionTests','NavalFieldPresentationTests','CardSupplyCostPresentationTests','CoreBoardingResolvedAttackTests')) {
    Copy-Item -LiteralPath (Join-Path $tideWorkspace ('UnityClient/Assets/_Project/Tests/EditMode/' + $tideTest + '.cs')) -Destination (Join-Path $tideProject 'Assets/Tests/Editor') -Force
}
Copy-Item -LiteralPath (Join-Path $tideWorkspace 'UnityClient/Assets/_Project/Tests/PlayMode/CardKeywordTooltipTests.cs') -Destination (Join-Path $tideProject 'Assets/Tests/PlayMode') -Force
foreach ($tideTest in @('NavalFieldSceneTests','NavalFieldStatusMotionTests')) { Copy-Item -LiteralPath (Join-Path $tideWorkspace ('UnityClient/Assets/_Project/Tests/PlayMode/' + $tideTest + '.cs')) -Destination (Join-Path $tideProject 'Assets/Tests/PlayMode') -Force }
$tideFilter = if ($Mode -eq 'EditMode') {
    'ShatteredSail.Core.Tests.CoreBattleCardKeywordTextTests;ShatteredSail.Core.Tests.CoreSchoolCheckpointBoundaryTests;ShatteredSail.Core.Tests.CardSchoolPresentationTests.SchoolHud_ShowsManaCurseTicksAndCurrentTideWithoutRetiredReactionState;ShatteredSail.Core.Tests.NavalFieldMotionTests;ShatteredSail.Core.Tests.NavalFieldPresentationTests;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.TideBarrierCostsTwoAndPermanentConversionHasNoResourcePaymentAtEveryRank;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.CurseRequirementsNeverDisplayAnActualMarkPayment;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.CurseHealingMedicineBadgeTracksItsMarkRequirement;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.NoMedicineFallback_AlsoRemovesTheLinkedSchoolPayment;ShatteredSail.Core.Tests.CoreBoardingResolvedAttackTests'
} else { 'ShatteredSail.Presentation.Tests.CardKeywordTooltipTests;ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.NavalField;ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.EnemyCurrent' }
$tideResult = Join-Path $tideOutput ($Mode + '.xml')
$tideLog = Join-Path $tideOutput ($Mode + '.log')
$tideStarted = [DateTime]::UtcNow
$tideArguments = @('-batchmode','-nographics','-projectPath',('"' + $tideProject + '"'),'-runTests','-testPlatform',$Mode,
    '-testFilter',$tideFilter,'-testResults',('"' + $tideResult + '"'),'-logFile',('"' + $tideLog + '"'))
$tideProcess = Start-Process -FilePath 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' -ArgumentList $tideArguments -WindowStyle Hidden -PassThru
$tideProcess.WaitForExit()
if ($tideProcess.ExitCode -ne 0) { throw ('Unity validation failed: ' + $tideLog) }
if (!(Test-Path -LiteralPath $tideResult)) { throw 'Unity produced no test result.' }
if ((Get-Item -LiteralPath $tideResult).LastWriteTimeUtc -lt $tideStarted.AddSeconds(-2)) { throw 'Unity did not replace the old test result.' }
[xml]$tideXml = Get-Content -LiteralPath $tideResult
$tideRun = $tideXml.'test-run'
Write-Output ($Mode + ': ' + $tideRun.result + ' total=' + $tideRun.total + ' passed=' + $tideRun.passed + ' failed=' + $tideRun.failed)
if ($tideRun.result -ne 'Passed' -or [int]$tideRun.total -lt 1 -or [int]$tideRun.failed -ne 0) { throw 'Some focused UI tests failed.' }
