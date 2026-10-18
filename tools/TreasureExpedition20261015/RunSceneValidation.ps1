param(
    [ValidateSet('Prepare','EditMode','PlayMode')][string]$Stage = 'Prepare',
    [string]$ProjectPath = 'D:\Game\tmp\runtime-memory-release-20261014',
    [string]$Output = 'D:\Game\Artifacts\TreasureExpedition20261015\SceneRun1',
    [string]$TestFilter = '',
    [int]$TimeoutMinutes = 20
)
$ErrorActionPreference = 'Stop'
$sceneSource = 'D:\Game\UnityClient'
$sceneProject = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\')
$sceneOutput = [IO.Path]::GetFullPath($Output).TrimEnd('\')
if (-not $sceneProject.StartsWith('D:\Game\tmp\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Use an isolated project below D:\Game\tmp.' }
if (-not $sceneOutput.StartsWith('D:\Game\Artifacts\TreasureExpedition20261015\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Use the exploration artifact directory.' }
if ($TimeoutMinutes -lt 3 -or $TimeoutMinutes -gt 60) { throw 'Timeout must be 3–60 minutes.' }
if (-not (Test-Path -LiteralPath (Join-Path $sceneProject 'Library'))) { throw 'A warm isolated project is required.' }
$sceneMarker = Join-Path $sceneProject 'TREASURE_EXPEDITION_VALIDATION_20261015.json'
if ($Stage -ne 'Prepare') {
    if (-not (Test-Path -LiteralPath $sceneMarker)) { throw 'Run Prepare before validation; the isolation marker is missing.' }
    $sceneIdentity = [IO.File]::ReadAllText((Join-Path $sceneProject 'ProjectSettings\ProjectSettings.asset'))
    if ($sceneIdentity -notmatch '(?m)^  companyName: TreasureExpeditionValidation20261015\r?$' -or
        $sceneIdentity -notmatch '(?m)^  productName: TreasureExpeditionValidation20261015\r?$') { throw 'Validation must use a separate PlayerPrefs identity.' }
    $scenePrepared = Get-Content -Raw -LiteralPath $sceneMarker | ConvertFrom-Json
    if ($scenePrepared.Project -cne $sceneProject -or $scenePrepared.Source -cne $sceneSource) { throw 'Isolation marker does not match this project.' }
}
if (@(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($sceneProject) }).Count) { throw 'Isolated project is already running.' }
$sceneLock = Join-Path $sceneProject 'Temp\UnityLockfile'
if (Test-Path -LiteralPath $sceneLock) {
    try { $sceneCheck = [IO.File]::Open($sceneLock, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None); $sceneCheck.Dispose() }
    catch { throw 'Isolated project is locked by Unity.' }
}
$sceneLease = [IO.File]::Open((Join-Path $sceneProject 'TreasureExpeditionValidation.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
$sceneProcess = $null
try {
    if ($Stage -eq 'Prepare') {
        foreach ($scenePart in @('Assets','Packages','ProjectSettings')) {
            $sceneCopy = @((Join-Path $sceneSource $scenePart), (Join-Path $sceneProject $scenePart), '/E','/R:1','/W:1','/NFL','/NDL','/NP','/NJH','/NJS')
            if ($scenePart -eq 'Assets') { $sceneCopy += @('/XD', (Join-Path $sceneSource 'Assets\_Project\Scenes\UIPreviewsForMe')) }
            & robocopy @sceneCopy | Out-Null
            if ($LASTEXITCODE -ge 8) { throw ('Copy failed: ' + $scenePart) }
        }
        $sceneScratch = [IO.Path]::GetFullPath((Join-Path $sceneProject 'Assets\_Project'))
        foreach ($sceneOld in @(Get-ChildItem -LiteralPath $sceneScratch -Filter '*.cs' -File -Recurse)) {
            $sceneRelative = $sceneOld.FullName.Substring($sceneScratch.Length + 1)
            if (Test-Path -LiteralPath (Join-Path $sceneSource ('Assets\_Project\' + $sceneRelative))) { continue }
            $sceneResolved = [IO.Path]::GetFullPath($sceneOld.FullName)
            if (-not $sceneResolved.StartsWith($sceneScratch + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Stale source is outside scratch project.' }
            Remove-Item -LiteralPath $sceneResolved -Force
            if (Test-Path -LiteralPath ($sceneResolved + '.meta')) { Remove-Item -LiteralPath ($sceneResolved + '.meta') -Force }
        }
        $sceneSettingsPath = Join-Path $sceneProject 'ProjectSettings\ProjectSettings.asset'
        $sceneSettings = [IO.File]::ReadAllText($sceneSettingsPath)
        $sceneSettings = [regex]::Replace($sceneSettings, '(?m)^  companyName:.*$', '  companyName: TreasureExpeditionValidation20261015')
        $sceneSettings = [regex]::Replace($sceneSettings, '(?m)^  productName:.*$', '  productName: TreasureExpeditionValidation20261015')
        [IO.File]::WriteAllText($sceneSettingsPath, $sceneSettings, [Text.UTF8Encoding]::new($false))
        [pscustomobject]@{ Project=$sceneProject; Source=$sceneSource; PreparedUtc=[DateTime]::UtcNow } | ConvertTo-Json | Set-Content -LiteralPath $sceneMarker -Encoding utf8
        Write-Output 'Prepared production scenes, assets and source; separate profile identity; warm Library retained.'
        exit 0
    }
    if (Test-Path -LiteralPath $sceneOutput) { throw 'Use a fresh Output directory to preserve evidence.' }
    [IO.Directory]::CreateDirectory($sceneOutput) | Out-Null
    if (Test-Path -LiteralPath (Join-Path $PSScriptRoot '..\VoyageExploration20261015\ExplorationUiCapture.cs')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\VoyageExploration20261015\ExplorationUiCapture.cs') -Destination (Join-Path $sceneProject 'Assets\_Project\Tests\PlayMode\ExplorationUiCapture.cs') -Force
    }
    if (-not $TestFilter) {
        $TestFilter = if ($Stage -eq 'EditMode') { 'CoreTreasure;CoreUsedTreasure;TreasureExpeditionClockTests;TreasureMapPresentationTests;EncounterDiscoveryRulesTests;CoreVoyageExploration;NavigationDashboardTests;EncounterDossierLayoutTests;DepartureTreasureSeedSelectionTests;TreasureProgressReadProtectionTests;TreasureComicDialogueLayoutTests;GameplayFlowPreviewTests.FlowState_UsesIndependentDataAndBuildsItsRealScreen' } else { 'TreasureExpedition_' }
    }
    $sceneLog = Join-Path $sceneOutput 'editor.log'
    $sceneArgs = @('-batchmode','-force-d3d11','-projectPath',$sceneProject,'-logFile',$sceneLog,'-runTests','-testPlatform',$Stage,'-testFilter',$TestFilter,'-testResults',(Join-Path $sceneOutput 'results.xml'))
    if ($Stage -eq 'PlayMode') { $sceneArgs += @('-navalVisualCapturePath',(Join-Path $sceneOutput 'Screens'),'-explorationUiCapturePath',(Join-Path $sceneOutput 'Screens')) }
    $sceneQuoted = $sceneArgs | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }
    $sceneProcess = Start-Process -FilePath 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' -WindowStyle Hidden -ArgumentList $sceneQuoted -PassThru
    $null = $sceneProcess.Handle
    [pscustomobject]@{ Stage=$Stage; ProcessId=$sceneProcess.Id; Project=$sceneProject; Arguments=$sceneArgs; StartedUtc=[DateTime]::UtcNow } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $sceneOutput 'launch.json') -Encoding utf8
    $sceneTimer = [Diagnostics.Stopwatch]::StartNew()
    Write-Output ('Unity ' + $Stage + ' PID ' + $sceneProcess.Id + '; output=' + $sceneOutput)
    while (-not $sceneProcess.WaitForExit(15000)) {
        $sceneProcess.Refresh()
        $sceneTail = if (Test-Path -LiteralPath $sceneLog) { (Get-Content -LiteralPath $sceneLog -Tail 1) } else { 'Waiting for editor log.' }
        Write-Output ('[' + [int]$sceneTimer.Elapsed.TotalSeconds + 's] CPU=' + [int]$sceneProcess.TotalProcessorTime.TotalSeconds + 's; ' + $sceneTail)
        if ($sceneTimer.Elapsed.TotalMinutes -gt $TimeoutMinutes) {
            # Only stop this invocation's process; never touch the main editor or other validation runs.
            $sceneProcess.Kill()
            $null = $sceneProcess.WaitForExit(10000)
            throw ('Validation exceeded ' + $TimeoutMinutes + ' minutes; log retained: ' + $sceneLog)
        }
    }
    $sceneProcess.Refresh()
    if ($sceneProcess.ExitCode -ne 0) { throw ('Unity exit ' + $sceneProcess.ExitCode + '; inspect ' + $sceneLog) }
    $sceneResultsPath = Join-Path $sceneOutput 'results.xml'
    if (-not (Test-Path -LiteralPath $sceneResultsPath)) { throw 'Unity exited without NUnit results.' }
    [xml]$sceneResults = Get-Content -Raw -LiteralPath $sceneResultsPath
    $sceneRun = $sceneResults.'test-run'
    if ($sceneRun.result -ne 'Passed' -or [int]$sceneRun.total -le 0 -or [int]$sceneRun.passed -ne [int]$sceneRun.total) { throw ('Tests incomplete or failed: ' + $sceneResultsPath) }
    if ($Stage -eq 'PlayMode' -and $TestFilter -eq 'VoyageExploration_') {
        $sceneExpectedTests = @('VoyageExploration_ReefChain_RealButtonsAndDiskResume','VoyageExploration_TradeChain_RealButtonsAndDiskResume','VoyageExploration_MoonChain_RealButtonsAndDiskResume','VoyageExploration_LockedTargets_SkipAndResumeKeepUnclaimedLocations')
        $sceneCases = @($sceneResults.SelectNodes('//test-case'))
        if ($sceneCases.Count -ne 4) { throw 'Expected all four exploration PlayMode tests.' }
        foreach ($sceneExpectedTest in $sceneExpectedTests) {
            if (@($sceneCases | Where-Object { $_.fullname -ceq ('ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.' + $sceneExpectedTest) -and $_.result -ceq 'Passed' }).Count -ne 1) { throw ('Missing expected test: ' + $sceneExpectedTest) }
        }
        $sceneScreens = Join-Path $sceneOutput 'Screens'
        $sceneEvidence = Get-Content -Raw -LiteralPath (Join-Path $sceneScreens 'exploration-ui-report.json') | ConvertFrom-Json
        if (@($sceneEvidence.frames).Count -ne 36) { throw 'Expected 36 rendered UI frames (18 states at two sizes).' }
        foreach ($sceneRegion in @('reef','trade','moon')) {
            foreach ($sceneState in @('source','clue_acquired','target_unlocked','completed','restored_completed','target_locked')) {
                foreach ($sceneSize in @('1920x1080','1280x1024')) {
                    $sceneImageName = $sceneRegion + '_' + $sceneState + '_' + $sceneSize + '.png'
                    $sceneFrame = @($sceneEvidence.frames | Where-Object { $_.file -ceq $sceneImageName -and $_.verifiedScaler -and -not $_.error })
                    if ($sceneFrame.Count -ne 1 -or (Get-Item -LiteralPath (Join-Path $sceneScreens $sceneImageName)).Length -lt 1000) { throw ('Missing valid screenshot: ' + $sceneImageName) }
                }
            }
            foreach ($sceneSave in @('clue','reward','locked_skipped')) {
                $sceneSavePath = Join-Path $sceneScreens ('save-evidence\' + $sceneRegion + '_' + $sceneSave + '.json')
                $scenePayload = Get-Content -Raw -LiteralPath $sceneSavePath
                if ($scenePayload -notmatch 'explorationDiscoveries') { throw ('Missing save evidence: ' + $sceneSavePath) }
            }
        }
        Write-Output 'Verified all four named PlayMode tests, 36 rendered frames and nine disk-save payloads.'
    }
    if ($Stage -eq 'PlayMode' -and $TestFilter -eq 'TreasureExpedition_') {
        $sceneCases = @($sceneResults.SelectNodes('//test-case'))
        $sceneExpected = @('TreasureExpedition_Weathered_ToolsOpenWrecksAndSurviveDiskResume',
            'TreasureExpedition_Gilded_CreditAndHeatChangeChoicesAndSurviveDiskResume',
            'TreasureExpedition_Moonlit_TimeCurseAndResonanceSurviveDiskResume',
            'TreasureExpedition_FolioAndDepartureExplainThreeDistinctObjectives')
        if ($sceneCases.Count -ne $sceneExpected.Count) { throw 'Expected all four treasure expedition PlayMode tests.' }
        foreach ($sceneName in $sceneExpected) {
            if (@($sceneCases | Where-Object { $_.name -ceq $sceneName -and $_.result -ceq 'Passed' }).Count -ne 1) { throw ('Missing test: ' + $sceneName) }
        }
        $sceneScreens = Join-Path $sceneOutput 'Screens'
        $sceneEvidence = Get-Content -Raw -LiteralPath (Join-Path $sceneScreens 'exploration-ui-report.json') | ConvertFrom-Json
        if (@($sceneEvidence.frames).Count -ne 52) { throw 'Expected 52 rendered frames, 26 live states at two sizes.' }
        foreach ($sceneFrame in $sceneEvidence.frames) {
            if (-not $sceneFrame.verifiedScaler -or $sceneFrame.error -or
                (Get-Item -LiteralPath (Join-Path $sceneScreens $sceneFrame.file)).Length -lt 1000) { throw ('Invalid frame: ' + $sceneFrame.file) }
        }
        foreach ($sceneState in @('weathered_winch','gilded_forged_manifest','moonlit_day_altar',
            'weathered_side_reward','gilded_side_reward','moonlit_side_reward')) {
            $sceneSave = Get-Content -Raw -LiteralPath (Join-Path $sceneScreens ('save-evidence\treasure_' + $sceneState + '.json'))
            if ($sceneSave -notmatch 'treasureExpeditionMarks' -or $sceneSave -notmatch '"contentVersion":1') { throw ('Missing versioned save evidence: ' + $sceneState) }
        }
        Write-Output 'Verified four named PlayMode tests, 52 rendered frames and six versioned disk-save payloads.'
    }
    Write-Output ('Passed ' + $sceneRun.passed + '/' + $sceneRun.total + ' tests in ' + [int]$sceneTimer.Elapsed.TotalSeconds + ' seconds.')
} finally {
    if ($null -ne $sceneProcess -and -not $sceneProcess.HasExited) {
        $sceneProcess.Kill()
        $null = $sceneProcess.WaitForExit(10000)
    }
    $sceneLease.Dispose()
}
