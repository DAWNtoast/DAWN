param([ValidateSet('EditMode','PlayMode')][string]$Mode = 'EditMode')
$ErrorActionPreference = 'Stop'
$curseWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$curseProject = Join-Path $curseWorkspace 'tmp/card-keyword-tooltip-20261023-validation'
$curseCompile = Join-Path $curseWorkspace 'Artifacts/CurseRules20261024/Compile'
$curseOutput = Join-Path $curseWorkspace 'Artifacts/CurseRules20261024/UI'
if (!(Test-Path -LiteralPath (Join-Path $curseProject 'OWNED_CARD_KEYWORD_VALIDATION.txt'))) { throw 'Owned isolated validation project required.' }
if (Test-Path -LiteralPath (Join-Path $curseProject 'Temp/UnityLockfile')) { throw 'Validation project is already running.' }
New-Item -ItemType Directory -Force -Path $curseOutput | Out-Null
foreach ($curseAssembly in @('Core','Content','Persistence','Presentation')) {
    Copy-Item -LiteralPath (Join-Path $curseCompile ('ShatteredSail.' + $curseAssembly + '.dll')) -Destination (Join-Path $curseProject 'Assets/Plugins') -Force
}
foreach ($curseTest in @('CoreBattleCardKeywordTextTests','CoreSchoolCheckpointBoundaryTests','CardSchoolPresentationTests','CardDescriptionTextTests','NavalFieldMotionTests','CardSupplyCostPresentationTests','CoreBoardingResolvedAttackTests')) {
    Copy-Item -LiteralPath (Join-Path $curseWorkspace ('UnityClient/Assets/_Project/Tests/EditMode/' + $curseTest + '.cs')) -Destination (Join-Path $curseProject 'Assets/Tests/Editor') -Force
}
Copy-Item -LiteralPath (Join-Path $curseWorkspace 'UnityClient/Assets/_Project/Tests/PlayMode/CardKeywordTooltipTests.cs') -Destination (Join-Path $curseProject 'Assets/Tests/PlayMode') -Force
$curseFilter = if ($Mode -eq 'EditMode') {
    'ShatteredSail.Core.Tests.CoreBattleCardKeywordTextTests;ShatteredSail.Core.Tests.CoreSchoolCheckpointBoundaryTests;ShatteredSail.Core.Tests.CardSchoolPresentationTests.SchoolHud_ShowsManaCurseTicksAndCurrentTideWithoutRetiredReactionState;ShatteredSail.Core.Tests.NavalFieldMotionTests;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.CurseRequirementsNeverDisplayAnActualMarkPayment;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.CurseHealingMedicineBadgeTracksItsMarkRequirement;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests.NoMedicineFallback_AlsoRemovesTheLinkedSchoolPayment;ShatteredSail.Core.Tests.CoreBoardingResolvedAttackTests'
} else { 'ShatteredSail.Presentation.Tests.CardKeywordTooltipTests' }
$curseResult = Join-Path $curseOutput ($Mode + '.xml')
$curseLog = Join-Path $curseOutput ($Mode + '.log')
$curseStarted = [DateTime]::UtcNow
$curseArguments = @('-batchmode','-nographics','-projectPath',('"' + $curseProject + '"'),'-runTests','-testPlatform',$Mode,
    '-testFilter',$curseFilter,'-testResults',('"' + $curseResult + '"'),'-logFile',('"' + $curseLog + '"'))
$curseProcess = Start-Process -FilePath 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' -ArgumentList $curseArguments -WindowStyle Hidden -PassThru
$curseProcess.WaitForExit()
if ($curseProcess.ExitCode -ne 0) { throw ('Unity validation failed: ' + $curseLog) }
if (!(Test-Path -LiteralPath $curseResult)) { throw 'Unity produced no test result.' }
if ((Get-Item -LiteralPath $curseResult).LastWriteTimeUtc -lt $curseStarted.AddSeconds(-2)) { throw 'Unity did not replace the old test result.' }
[xml]$curseXml = Get-Content -LiteralPath $curseResult
$curseRun = $curseXml.'test-run'
Write-Output ($Mode + ': ' + $curseRun.result + ' total=' + $curseRun.total + ' passed=' + $curseRun.passed + ' failed=' + $curseRun.failed)
if ($curseRun.result -ne 'Passed' -or [int]$curseRun.total -lt 1 -or [int]$curseRun.failed -ne 0) { throw 'Some focused UI tests failed.' }
