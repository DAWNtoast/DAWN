param([string]$UnityEditor = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe', [switch]$SkipPreparation)
$ErrorActionPreference = 'Stop'
$tooltipWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$tooltipMain = Join-Path $tooltipWorkspace 'UnityClient'
$tooltipProject = Join-Path $tooltipWorkspace 'tmp/card-keyword-tooltip-20261023-validation'
$tooltipOutput = Join-Path $tooltipWorkspace 'Artifacts/CardKeywordTooltip20261023'
if (-not (Test-Path -LiteralPath (Join-Path $tooltipProject 'OWNED_CARD_KEYWORD_VALIDATION.txt'))) { throw 'Run the task test script first.' }
if (Test-Path -LiteralPath (Join-Path $tooltipProject 'Temp/UnityLockfile')) { throw 'The isolated tooltip project is running.' }
if (-not $SkipPreparation) {
    & (Join-Path $PSScriptRoot 'RunTests.ps1') -PrepareOnly
    Copy-Item -LiteralPath (Join-Path $tooltipMain 'Assets/_Project/Resources') -Destination (Join-Path $tooltipProject 'Assets') -Recurse -Force
    Get-ChildItem -LiteralPath (Join-Path $tooltipMain 'Assets/_Project/Tests/PlayMode') -Filter '*.cs' -File |
        ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $tooltipProject 'Assets/Tests/PlayMode') -Force }
    Copy-Item -LiteralPath (Join-Path $tooltipMain 'Assets/_Project/Tests/EditMode/CardSchoolPresentationTests.cs') -Destination (Join-Path $tooltipProject 'Assets/Tests/Editor') -Force
    Copy-Item -LiteralPath (Join-Path $tooltipMain 'Assets/_Project/Tests/EditMode/CardDescriptionTextTests.cs') -Destination (Join-Path $tooltipProject 'Assets/Tests/Editor') -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PrepareIntegration.cs') -Destination (Join-Path $tooltipProject 'Assets/Editor') -Force
    $tooltipPrepareLog = Join-Path $tooltipOutput 'IntegrationPrepare.log'
    $tooltipPrepareArguments = @('-batchmode','-projectPath',('"' + $tooltipProject + '"'),'-executeMethod','ShatteredSail.Editor.CardKeywordTooltipIntegration.Prepare','-quit','-logFile',('"' + $tooltipPrepareLog + '"'))
    $tooltipPrepareProcess = Start-Process -FilePath $UnityEditor -ArgumentList $tooltipPrepareArguments -WindowStyle Hidden -PassThru
    $tooltipPrepareProcess.WaitForExit()
    if ($tooltipPrepareProcess.ExitCode -ne 0) { throw "Integration setup failed. Inspect $tooltipPrepareLog" }
}

$tooltipPlayCases = @(
    'CoreGameplayDemoSceneTests.BattleRules_ActualPointerCardsQuoteSharedCostAndResetOnTurnAndStage',
    'CoreGameplayDemoSceneTests.BattleRules_EnemyRamIntentAndPointerEndTurnExposeFirstStrikeAndCrewHealth',
    'CoreGameplayDemoSceneTests.BattleRules_AllCardRanksExplainVisibleKeywordsWithoutCalculations',
    'CoreGameplayDemoSceneTests.BoardingMedicine_RealCardsConsumeOneBottleAndRefreshRetainedHealingAndHud',
    'CoreGameplayDemoSceneTests.BoardingMedicine_FullHealthAndSmallWoundCardsClearlyPreserveSupplies',
    'CoreGameplayDemoSceneTests.SchoolCards_RealButtonsRefreshStateAndRejectLethalLifePaymentWithoutSpending',
    'BattlePresentationTests.BattleOverlay_UsesTacticalSeaAndKeepsTerrainAndCardComposition',
    'BattlePresentationTests.EightCardHand_FansWithinBottomAreaAndRevealsHoveredCard'
) | ForEach-Object { 'ShatteredSail.Presentation.Tests.' + $_ }
$tooltipEditCases = @(
    'ShatteredSail.Core.Tests.CardSchoolPresentationTests.BattleCardTooltip_OnlyExplainsVisibleKeywordsWithoutCurrentQuotes',
    'ShatteredSail.Core.Tests.CardSchoolPresentationTests.BloodTooltip_DoesNotAppendLiveLifePaymentOrUnavailableReasons'
)
foreach ($tooltipMode in @('EditMode','PlayMode')) {
    $tooltipFilter = if ($tooltipMode -eq 'EditMode') { $tooltipEditCases -join ';' } else { $tooltipPlayCases -join ';' }
    $tooltipExpected = if ($tooltipMode -eq 'EditMode') { 2 } else { 8 }
    $tooltipResult = Join-Path $tooltipOutput ('Integration' + $tooltipMode + '.xml')
    $tooltipLog = Join-Path $tooltipOutput ('Integration' + $tooltipMode + '.log')
    $tooltipArguments = @('-batchmode','-projectPath',('"' + $tooltipProject + '"'),'-runTests','-testPlatform',$tooltipMode,
        '-testFilter',('"' + $tooltipFilter + '"'),'-testResults',('"' + $tooltipResult + '"'),'-logFile',('"' + $tooltipLog + '"'))
    $tooltipStarted = [DateTime]::UtcNow
    $tooltipProcess = Start-Process -FilePath $UnityEditor -ArgumentList $tooltipArguments -WindowStyle Hidden -PassThru
    $tooltipProcess.WaitForExit()
    if (-not (Test-Path -LiteralPath $tooltipResult)) { throw "Unity produced no integration result. Inspect $tooltipLog" }
    if ((Get-Item -LiteralPath $tooltipResult).LastWriteTimeUtc -lt $tooltipStarted.AddSeconds(-2)) { throw 'Unity did not replace the previous integration result.' }
    [xml]$tooltipXml = Get-Content -LiteralPath $tooltipResult
    $tooltipRun = $tooltipXml.'test-run'
    Write-Output ("Integration $tooltipMode Unity: " + $tooltipRun.result + ' total=' + $tooltipRun.total + ' passed=' + $tooltipRun.passed + ' failed=' + $tooltipRun.failed)
    if ($tooltipProcess.ExitCode -ne 0 -or $tooltipRun.result -ne 'Passed' -or [int]$tooltipRun.total -ne $tooltipExpected) { throw "Integration $tooltipMode tests did not all pass; inspect $tooltipResult" }
}
