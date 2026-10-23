param([string]$UnityEditor = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe', [switch]$SkipCompile)
$ErrorActionPreference = 'Stop'
$collectionWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$collectionMain = Join-Path $collectionWorkspace 'UnityClient'
$collectionProject = Join-Path $collectionWorkspace 'tmp/card-keyword-tooltip-20261023-validation'
$collectionOutput = Join-Path $collectionWorkspace 'Artifacts/CollectionCardKeywords20261023'
if (-not (Test-Path -LiteralPath (Join-Path $collectionProject 'OWNED_CARD_KEYWORD_VALIDATION.txt'))) { throw 'Reuse only the owned keyword validation project.' }
if (Test-Path -LiteralPath (Join-Path $collectionProject 'Temp/UnityLockfile')) { throw 'The isolated validation project is running.' }
New-Item -ItemType Directory -Force -Path $collectionOutput | Out-Null
# Recompile the final production sources; keep the previously imported resources.
& (Join-Path $collectionWorkspace 'tools/CardKeywordTooltip20261023/RunTests.ps1') -PrepareOnly -SkipCompile:$SkipCompile
foreach ($collectionTest in @('CollectionCardKeywordHoverTests.cs','CollectionCardKeywordFlowTests.cs')) {
    Copy-Item -LiteralPath (Join-Path $collectionMain ('Assets/_Project/Tests/PlayMode/' + $collectionTest)) -Destination (Join-Path $collectionProject 'Assets/Tests/PlayMode') -Force
}
# Preparation restores isolated PlayerPrefs identity after ProjectSettings refresh.
$collectionPrepareLog = Join-Path $collectionOutput 'Prepare.log'
$collectionPrepareArgs = @('-batchmode','-projectPath',('"' + $collectionProject + '"'),'-executeMethod','ShatteredSail.Editor.CardKeywordTooltipIntegration.Prepare','-quit','-logFile',('"' + $collectionPrepareLog + '"'))
$collectionProcess = Start-Process -FilePath $UnityEditor -ArgumentList $collectionPrepareArgs -WindowStyle Hidden -PassThru
$collectionProcess.WaitForExit()
if ($collectionProcess.ExitCode -ne 0) { throw "Preparation failed: $collectionPrepareLog" }
foreach ($collectionMode in @('EditMode','PlayMode')) {
    $collectionResult = Join-Path $collectionOutput ($collectionMode + '.xml')
    $collectionLog = Join-Path $collectionOutput ($collectionMode + '.log')
    $collectionFilter = if ($collectionMode -eq 'EditMode') { 'ShatteredSail.Core.Tests.CoreBattleCardKeywordTextTests' } else {
        'ShatteredSail.Presentation.Tests.CollectionCardKeywordHoverTests;ShatteredSail.Presentation.Tests.CardKeywordTooltipTests;ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.CollectionKeyword_'
    }
    $collectionArgs = @('-batchmode','-projectPath',('"' + $collectionProject + '"'),'-runTests','-testPlatform',$collectionMode,
        '-testFilter',('"' + $collectionFilter + '"'),'-testResults',('"' + $collectionResult + '"'),'-logFile',('"' + $collectionLog + '"'))
    if ($collectionMode -eq 'PlayMode') {
        $collectionCapture = Join-Path $collectionOutput ('Capture-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
        $collectionArgs += @('-navalVisualCapturePath',('"' + $collectionCapture + '"'))
    }
    $collectionStarted = [DateTime]::UtcNow
    $collectionProcess = Start-Process -FilePath $UnityEditor -ArgumentList $collectionArgs -WindowStyle Hidden -PassThru
    $collectionProcess.WaitForExit()
    if (-not (Test-Path -LiteralPath $collectionResult)) { throw "No test result: $collectionLog" }
    if ((Get-Item -LiteralPath $collectionResult).LastWriteTimeUtc -lt $collectionStarted.AddSeconds(-2)) { throw 'Stale test results.' }
    [xml]$collectionXml = Get-Content -LiteralPath $collectionResult
    $collectionRun = $collectionXml.'test-run'
    Write-Output ("$collectionMode Unity: " + $collectionRun.result + ' total=' + $collectionRun.total + ' passed=' + $collectionRun.passed + ' failed=' + $collectionRun.failed)
    if ($collectionProcess.ExitCode -ne 0 -or $collectionRun.result -ne 'Passed' -or [int]$collectionRun.total -lt 1) { throw "Tests failed: $collectionResult" }
}
Write-Output $collectionOutput
