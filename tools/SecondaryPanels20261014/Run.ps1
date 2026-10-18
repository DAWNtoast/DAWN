param(
    [ValidateSet('Prepare','Compile','EditMode','PlayMode','Capture')][string]$Stage = 'Prepare',
    [string]$RunName = 'run1',
    [string]$ProjectPath = 'D:\Game\tmp\departure-dock-scene-20261012',
    [string]$TestFilter = ''
)
$ErrorActionPreference = 'Stop'
$panelSource = 'D:\Game\UnityClient'
$panelProject = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\')
$panelRoot = 'D:\Game\Artifacts\SecondaryPanels20261014\validation'
$panelUnity = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
if (-not $panelProject.StartsWith('D:\Game\tmp\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Use an isolated project below D:\Game\tmp.' }
if ($RunName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'RunName must be a simple identifier.' }
if (-not (Test-Path -LiteralPath (Join-Path $panelProject 'Library'))) { throw 'A warm isolated project is required.' }
if (@(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($panelProject) }).Count) { throw 'Isolated project is already running.' }
$panelUnityLock = Join-Path $panelProject 'Temp\UnityLockfile'
if (Test-Path -LiteralPath $panelUnityLock) {
    try { $panelCheck = [IO.File]::Open($panelUnityLock, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None); $panelCheck.Dispose() }
    catch { throw 'The isolated Unity project is already open.' }
}
[IO.Directory]::CreateDirectory($panelRoot) | Out-Null
$panelLease = $null
try {
    # This lease also serializes this task's Prepare and Unity invocations.
    $panelLease = [IO.File]::Open((Join-Path $panelProject 'SecondaryPanels20261014.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    if ($Stage -eq 'Prepare') {
        foreach ($panelPart in @('Assets','Packages','ProjectSettings')) {
            $panelCopy = @((Join-Path $panelSource $panelPart), (Join-Path $panelProject $panelPart), '/E','/R:1','/W:1','/NFL','/NDL','/NP','/NJH','/NJS')
            if ($panelPart -eq 'Assets') { $panelCopy += @('/XD', (Join-Path $panelSource 'Assets\_Project\Scenes\UIPreviewsForMe')) }
            & robocopy @panelCopy | Out-Null
            if ($LASTEXITCODE -ge 8) { throw ('Copy failed: ' + $panelPart) }
        }
        $panelScratch = [IO.Path]::GetFullPath((Join-Path $panelProject 'Assets\_Project'))
        foreach ($panelOld in @(Get-ChildItem -LiteralPath $panelScratch -Filter '*.cs' -File -Recurse)) {
            $panelRelative = $panelOld.FullName.Substring($panelScratch.Length + 1)
            if (Test-Path -LiteralPath (Join-Path $panelSource ('Assets\_Project\' + $panelRelative))) { continue }
            $panelResolved = [IO.Path]::GetFullPath($panelOld.FullName)
            if (-not $panelResolved.StartsWith($panelScratch + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Stale source is outside scratch project.' }
            Remove-Item -LiteralPath $panelResolved -Force
            if (Test-Path -LiteralPath ($panelResolved + '.meta')) { Remove-Item -LiteralPath ($panelResolved + '.meta') -Force }
        }
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Capture.cs') -Destination (Join-Path $panelProject 'Assets\_Project\Editor\SecondaryPanelsCapture20261014.cs') -Force
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'RuntimeCapture.cs') -Destination (Join-Path $panelProject 'Assets\_Project\Tests\PlayMode\SecondaryPanelsRuntimeCapture20261014.cs') -Force
        $panelSettingsPath = Join-Path $panelProject 'ProjectSettings\ProjectSettings.asset'
        $panelSettings = [IO.File]::ReadAllText($panelSettingsPath)
        $panelSettings = [regex]::Replace($panelSettings, '(?m)^  companyName:.*$', '  companyName: SecondaryPanelsValidation20261014')
        $panelSettings = [regex]::Replace($panelSettings, '(?m)^  productName:.*$', '  productName: SecondaryPanelsValidation20261014')
        [IO.File]::WriteAllText($panelSettingsPath, $panelSettings, [Text.UTF8Encoding]::new($false))
        Write-Output 'Prepared production source and assets with separate PlayerPrefs identity; warm Library retained. No scenes generated.'
        exit 0
    }
    $panelOutput = Join-Path $panelRoot $RunName
    if (Test-Path -LiteralPath $panelOutput) { throw 'Use a fresh RunName to preserve existing output.' }
    [IO.Directory]::CreateDirectory($panelOutput) | Out-Null
    $panelArgs = @('-batchmode','-force-d3d11','-projectPath',$panelProject,'-logFile',(Join-Path $panelOutput ($Stage + '.log')))
    if ($Stage -eq 'Capture') {
        $panelArgs += @('-quit','-executeMethod','ShatteredSail.Editor.SecondaryPanelsCapture20261014.Run','-secondaryPanelsOutput',(Join-Path $panelOutput 'Screens'))
    } elseif ($Stage -eq 'Compile') {
        $panelArgs += '-quit'
    } else {
        if (-not $TestFilter) {
            $TestFilter = if ($Stage -eq 'EditMode') {
                'MainMenuLayoutTests;AchievementCatalogTests;PortSettingsModalTests;VoyageSettingsModalTests;MainMenu_AchievementHeadingAndFooterStayWithinTheRuleAcrossPages'
            } else {
                'MainMenu_LoadsGeneratedArtAndFunctionalPanels;MainMenu_OptionalPanelsAreCreatedOnFirstUseAndReused;MainMenu_PrefabsBindControlsAndReuseSaveNotices;VoyageCargo_ItemClicksRetainVisibleSheetGridAndDetailsWithoutRebuilding;VoyageCargo_RetainedSelectionPreservesQuantityAndRetiresPreviousItemActions;VoyageCargo_RealBasicAndSpecialDiscardsValidateQuantityAndLeavePortProfileUntouched;VoyageSettings_SaveReturnAndContinueRestoresCurrentVoyageWithBackupRecovery;SettingsPrefab_;PortSettingsRuntimeIsolationTests;AchievementIcons_RealMenuPagesReachEveryBadgeWithoutChangingProgress;AchievementExperience_RealFiltersReuseRowsRefreshAndHandleEmptyResults;SecondaryPanels_RuntimeProductionScreens'
            }
        }
        $panelArgs += @('-runTests','-testPlatform',$Stage,'-testFilter',$TestFilter,'-testResults',(Join-Path $panelOutput 'results.xml'))
        if ($Stage -eq 'PlayMode') { $panelArgs += @('-navalVisualCapturePath',(Join-Path $panelOutput 'LiveScreens')) }
    }
    $panelQuoted = $panelArgs | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }
    $panelProcess = Start-Process -FilePath $panelUnity -WindowStyle Hidden -ArgumentList $panelQuoted -PassThru
    [pscustomobject]@{ Stage=$Stage; ProcessId=$panelProcess.Id; Project=$panelProject; Arguments=$panelArgs } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $panelOutput 'launch.json') -Encoding utf8
    Write-Output ('Unity ' + $Stage + ' PID ' + $panelProcess.Id + '; output=' + $panelOutput)
    $panelProcess.WaitForExit()
    $panelProcess.Refresh()
    if ($panelProcess.ExitCode -ne 0) { throw ('Unity failed with exit ' + $panelProcess.ExitCode + '; inspect ' + $panelOutput) }
    if ($Stage -eq 'EditMode' -or $Stage -eq 'PlayMode') {
        [xml]$panelResults = Get-Content -Raw -LiteralPath (Join-Path $panelOutput 'results.xml')
        if ($panelResults.'test-run'.result -ne 'Passed') { throw ('Tests failed; inspect ' + $panelOutput) }
        Write-Output ('Passed ' + $panelResults.'test-run'.passed + ' tests.')
    }
    Write-Output ('Completed ' + $Stage + ': ' + $panelOutput)
} finally {
    if ($panelLease) { $panelLease.Dispose() }
}
