param(
    [ValidateSet('Prepare', 'Sync', 'Compile', 'EditMode', 'PlayMode', 'Capture', 'CheckSaved', 'SyncPreview', 'Build')]
    [string]$Action = 'Prepare',
    [string]$RunName = 'final'
)
$ErrorActionPreference = 'Stop'
$taskSource = 'D:\Game\UnityClient'
$taskScratch = 'D:\Game\tmp\treasure-table-scene-20261013'
$taskOutput = Join-Path 'D:\Game\Artifacts\TreasureTableScene20261013\validation' $RunName
$taskMarker = Join-Path $taskScratch '.treasure-table-validation'
if ($RunName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Use a simple RunName.' }
if (Test-Path -LiteralPath (Join-Path $taskScratch 'Temp\UnityLockfile')) { throw 'Dedicated scratch Unity is open; stop it before this action.' }
function Install-Capture {
    $taskHelper = Join-Path $PSScriptRoot 'TreasureTableSceneCapture.cs'
    if (Test-Path -LiteralPath $taskHelper) {
        Copy-Item -LiteralPath $taskHelper -Destination (Join-Path $taskScratch 'Assets\_Project\Editor\TreasureTableSceneCapture.cs') -Force
    }
}
if ($Action -eq 'Prepare') {
    if (Test-Path -LiteralPath $taskScratch) { throw 'Refusing to overwrite an existing validation project.' }
    New-Item -ItemType Directory -Path $taskScratch | Out-Null
    foreach ($taskPart in @('Assets', 'Packages', 'ProjectSettings')) {
        & robocopy (Join-Path $taskSource $taskPart) (Join-Path $taskScratch $taskPart) /E /NFL /NDL /NJH /NJS /NP
        if ($LASTEXITCODE -gt 7) { throw "Copy failed: $taskPart ($LASTEXITCODE)" }
    }
    $taskSettings = Join-Path $taskScratch 'ProjectSettings\ProjectSettings.asset'
    $taskText = [IO.File]::ReadAllText($taskSettings)
    $taskText = $taskText -replace '(?m)^  companyName:.*$', '  companyName: TreasureTableValidation' -replace '(?m)^  productName:.*$', '  productName: TreasureTableValidation20261013' -replace '(?m)^    Standalone: com\.shatteredsail\.shattered-sail\r?$', '    Standalone: com.validation.treasuretable20261013'
    [IO.File]::WriteAllText($taskSettings, $taskText, [Text.UTF8Encoding]::new($false))
    Install-Capture
    [IO.File]::WriteAllText($taskMarker, 'Dedicated treasure table scene validation project.')
    Write-Output "Prepared $taskScratch; Unity was not started."
    exit 0
}
if (-not (Test-Path -LiteralPath $taskMarker)) { throw 'Run Prepare first.' }
if ($Action -eq 'Sync') {
    # No deletion and no settings copy: retain isolated save identity and generated evidence.
    foreach ($taskPart in @('Assets', 'Packages')) {
        & robocopy (Join-Path $taskSource $taskPart) (Join-Path $taskScratch $taskPart) /E /NFL /NDL /NJH /NJS /NP
        if ($LASTEXITCODE -gt 7) { throw "Sync failed: $taskPart ($LASTEXITCODE)" }
    }
    Install-Capture
    Write-Output "Updated source snapshot in $taskScratch; Unity was not started."
    exit 0
}
Install-Capture
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskLog = Join-Path $taskOutput ($Action.ToLowerInvariant() + '.log')
if (Test-Path -LiteralPath $taskLog) { throw 'Use a new RunName to preserve evidence.' }
$taskArguments = @('-batchmode', '-projectPath', $taskScratch, '-logFile', $taskLog)
switch ($Action) {
    'Compile' { $taskArguments += '-quit' }
    'EditMode' {
        $taskFilter = 'TreasureMapPresentationTests;TreasureProgressReadProtectionTests;CoreTreasureHuntTests;CoreUsedTreasureMapTests;TreasureMapsStayOnSeparateFrontFacingBoardsDuringHover;TreasureRoomThreeChartsAreAvailableInEveryPort;TreasureTable_DoesNotExposeTheOrdinaryRouteSeed;BossTreasureDropPreviewSceneTests;DesktopBackgroundCompressionTests'
        $taskArguments += @('-runTests', '-testPlatform', 'EditMode', '-testFilter', $taskFilter, '-testResults', (Join-Path $taskOutput 'editmode.xml'))
    }
    'PlayMode' {
        $taskFilter = 'TreasureFragments_;ItemIcons_TreasureWorkshopAndAllMapRanksKeepCraftingAndBrowsingReachable;TreasureFolio_;TreasureBoardClicksInspectFragmentsWithoutChangingOrdinaryRoutes'
        $taskArguments += @('-runTests', '-testPlatform', 'PlayMode', '-testFilter', $taskFilter, '-testResults', (Join-Path $taskOutput 'playmode.xml'))
    }
    'Capture' {
        $taskArguments += @('-quit', '-executeMethod', 'ShatteredSail.Editor.TreasureTableSceneCapture.Run', '-treasureTableCapturePath', (Join-Path $taskOutput 'screens'))
    }
    'CheckSaved' {
        $taskArguments += @('-quit', '-executeMethod', 'ShatteredSail.Editor.TreasureTableSceneCapture.CheckSaved', '-treasureTableSavedPath', (Join-Path $taskOutput 'saved-scenes'))
    }
    'SyncPreview' { $taskArguments += @('-quit', '-executeMethod', 'ShatteredSail.Editor.GameplayPreviewSceneBuilder.SyncTreasureWorktableFromCommandLine') }
    'Build' {
        $taskArguments += @('-quit', '-buildTarget', 'Win64', '-executeMethod', 'ShatteredSail.Editor.WindowsReleaseBuilder.BuildFromCommandLine', '-releaseOutput', (Join-Path $taskOutput 'windows\ShatteredSail.exe'))
    }
}
$taskProcess = Start-Process -FilePath 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' -WindowStyle Hidden -ArgumentList $taskArguments -Wait -PassThru
Write-Output "Unity $Action exited $($taskProcess.ExitCode); $taskLog"
if ($taskProcess.ExitCode -ne 0) { exit $taskProcess.ExitCode }
if ($Action -in @('EditMode', 'PlayMode')) {
    $taskResult = Join-Path $taskOutput ($Action.ToLowerInvariant() + '.xml')
    if (-not (Test-Path -LiteralPath $taskResult)) { throw 'Unity exited without test results.' }
    [xml]$taskXml = Get-Content -LiteralPath $taskResult -Raw
    if ([int]$taskXml.'test-run'.total -le 0 -or $taskXml.'test-run'.result -ne 'Passed') { throw "Tests did not pass: $taskResult" }
    Write-Output "$($taskXml.'test-run'.passed) / $($taskXml.'test-run'.total) tests passed."
}
exit 0
