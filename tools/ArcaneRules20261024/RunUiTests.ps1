param([ValidateSet('EditMode','PlayMode')][string]$Mode = 'EditMode')
$ErrorActionPreference = 'Stop'
$arcaneWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$arcaneProject = Join-Path $arcaneWorkspace 'tmp/card-keyword-tooltip-20261023-validation'
$arcaneCompile = Join-Path $arcaneWorkspace 'Artifacts/ArcaneRules20261024/Compile'
$arcaneOutput = Join-Path $arcaneWorkspace 'Artifacts/ArcaneRules20261024/UI'
if (!(Test-Path -LiteralPath (Join-Path $arcaneProject 'OWNED_CARD_KEYWORD_VALIDATION.txt'))) { throw 'Owned isolated validation project required.' }
if (Test-Path -LiteralPath (Join-Path $arcaneProject 'Temp/UnityLockfile')) { throw 'Validation project is already running.' }
New-Item -ItemType Directory -Force -Path $arcaneOutput | Out-Null
foreach ($arcaneAssembly in @('Core','Content','Persistence','Presentation')) {
    Copy-Item -LiteralPath (Join-Path $arcaneCompile ('ShatteredSail.' + $arcaneAssembly + '.dll')) -Destination (Join-Path $arcaneProject 'Assets/Plugins') -Force
}
foreach ($arcaneTest in @('CoreBattleCardKeywordTextTests','CoreSchoolCheckpointBoundaryTests','CardSchoolPresentationTests','CardDescriptionTextTests','NavalFieldMotionTests')) {
    Copy-Item -LiteralPath (Join-Path $arcaneWorkspace ('UnityClient/Assets/_Project/Tests/EditMode/' + $arcaneTest + '.cs')) -Destination (Join-Path $arcaneProject 'Assets/Tests/Editor') -Force
}
Copy-Item -LiteralPath (Join-Path $arcaneWorkspace 'UnityClient/Assets/_Project/Tests/PlayMode/CardKeywordTooltipTests.cs') -Destination (Join-Path $arcaneProject 'Assets/Tests/PlayMode') -Force
$arcaneFilter = if ($Mode -eq 'EditMode') {
    'ShatteredSail.Core.Tests.CoreBattleCardKeywordTextTests;ShatteredSail.Core.Tests.CoreSchoolCheckpointBoundaryTests;ShatteredSail.Core.Tests.CardSchoolPresentationTests.SchoolHud_ShowsManaCurseTicksAndCurrentTideWithoutRetiredReactionState;ShatteredSail.Core.Tests.NavalFieldMotionTests'
} else { 'ShatteredSail.Presentation.Tests.CardKeywordTooltipTests' }
$arcaneResult = Join-Path $arcaneOutput ($Mode + '.xml')
$arcaneLog = Join-Path $arcaneOutput ($Mode + '.log')
$arcaneStarted = [DateTime]::UtcNow
$arcaneArguments = @('-batchmode','-nographics','-projectPath',('"' + $arcaneProject + '"'),'-runTests','-testPlatform',$Mode,
    '-testFilter',$arcaneFilter,'-testResults',('"' + $arcaneResult + '"'),'-logFile',('"' + $arcaneLog + '"'))
$arcaneProcess = Start-Process -FilePath 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' -ArgumentList $arcaneArguments -WindowStyle Hidden -PassThru
$arcaneProcess.WaitForExit()
if ($arcaneProcess.ExitCode -ne 0) { throw ('Unity validation failed: ' + $arcaneLog) }
if (!(Test-Path -LiteralPath $arcaneResult)) { throw 'Unity produced no test result.' }
if ((Get-Item -LiteralPath $arcaneResult).LastWriteTimeUtc -lt $arcaneStarted.AddSeconds(-2)) { throw 'Unity did not replace the old test result.' }
[xml]$arcaneXml = Get-Content -LiteralPath $arcaneResult
$arcaneRun = $arcaneXml.'test-run'
Write-Output ($Mode + ': ' + $arcaneRun.result + ' total=' + $arcaneRun.total + ' passed=' + $arcaneRun.passed + ' failed=' + $arcaneRun.failed)
if ($arcaneRun.result -ne 'Passed' -or [int]$arcaneRun.total -lt 1 -or [int]$arcaneRun.failed -ne 0) { throw 'Some focused UI tests failed.' }
