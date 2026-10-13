param(
    [ValidateSet('Sync','PreviewSync','EditMode','PlayMode','Capture','Build')][string]$Stage = 'Sync',
    [string]$Suffix = '',
    [string]$TestFilter = '',
    [string]$ProjectPath = 'D:\Game\tmp\encounter-paper-20261011',
    [string]$BuildPath = 'D:\Game\Builds\第七港口-事件羊皮纸20261011\Windows\第七港口.exe'
)

$ErrorActionPreference = 'Stop'
# Retired by user request; exit before filesystem or Unity actions.
if (@('PreviewSync') -contains $Stage) { throw 'UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.' }
$taskSource = 'D:\Game\UnityClient'
$taskOutput = 'D:\Game\Artifacts\EncounterPaper20261011'
$taskUnity = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
$taskProject = [IO.Path]::GetFullPath($ProjectPath)
$taskCaptureSource = 'D:\Game\tools\EncounterPaperAudit\EncounterPaperCapture.cs'
if (-not $taskProject.StartsWith('D:\Game\tmp\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Validation project must remain under D:\Game\tmp.'
}
if ($Suffix -notmatch '^[A-Za-z0-9_-]*$') { throw 'Suffix must be a simple identifier.' }
[IO.Directory]::CreateDirectory($taskOutput) | Out-Null

$taskEditors = @()
try { $taskEditors = @(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" -ErrorAction Stop) }
catch { Write-Output 'Process inventory unavailable; checking exclusive UnityLockfile instead.' }
if ($taskEditors | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($taskProject) }) {
    throw 'This isolated Unity Editor project is already running; do not overlap validation runs.'
}
$taskLock = Join-Path $taskProject 'Temp\UnityLockfile'
if (Test-Path -LiteralPath $taskLock) {
    try {
        $taskStream = [IO.File]::Open($taskLock, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
        $taskStream.Dispose()
    } catch { throw 'Isolated validation project is already open.' }
}

if ($Stage -eq 'Sync') {
    if (-not (Test-Path -LiteralPath $taskCaptureSource)) { throw 'Encounter paper capture helper is missing.' }
    foreach ($taskPart in @('Assets','Packages','ProjectSettings')) {
        & robocopy (Join-Path $taskSource $taskPart) (Join-Path $taskProject $taskPart) /E /R:1 /W:1 /NFL /NDL /NP /NJH /NJS ('/LOG+:' + (Join-Path $taskOutput ('Sync' + $Suffix + '.log'))) | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "Copy failed: $taskPart" }
    }
    # Keep the warm Library. Remove only obsolete C# sources in the selected
    # scratch Assets/_Project tree, including temporary helpers from older audits.
    $taskScratchAssets = [IO.Path]::GetFullPath((Join-Path $taskProject 'Assets\_Project'))
    $taskSourceAssets = Join-Path $taskSource 'Assets\_Project'
    $taskRemoved = @()
    foreach ($taskOldSource in @(Get-ChildItem -LiteralPath $taskScratchAssets -Filter '*.cs' -File -Recurse)) {
        $taskRelative = $taskOldSource.FullName.Substring($taskScratchAssets.Length + 1)
        if (Test-Path -LiteralPath (Join-Path $taskSourceAssets $taskRelative)) { continue }
        $taskResolved = [IO.Path]::GetFullPath($taskOldSource.FullName)
        if (-not $taskResolved.StartsWith($taskScratchAssets + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Stale validation source outside scratch project.'
        }
        Remove-Item -LiteralPath $taskResolved -Force
        if (Test-Path -LiteralPath ($taskResolved + '.meta')) {
            Remove-Item -LiteralPath ($taskResolved + '.meta') -Force
        }
        $taskRemoved += $taskRelative.Replace('\', '/')
    }
    $taskRemoved | Set-Content -LiteralPath (Join-Path $taskOutput ('stale-sources-removed' + $Suffix + '.txt')) -Encoding utf8
    $taskSettingsPath = Join-Path $taskProject 'ProjectSettings\ProjectSettings.asset'
    $taskSettings = [IO.File]::ReadAllText($taskSettingsPath)
    $taskSettings = [regex]::Replace($taskSettings, '(?m)^  companyName:.*$', '  companyName: ShatteredSailEncounterPaperAudit20261011')
    $taskSettings = [regex]::Replace($taskSettings, '(?m)^  productName:.*$', '  productName: Shattered Sail Encounter Paper Audit 20261011')
    [IO.File]::WriteAllText($taskSettingsPath, $taskSettings, [Text.UTF8Encoding]::new($false))
    Copy-Item -LiteralPath $taskCaptureSource -Destination (Join-Path $taskProject 'Assets\_Project\Editor\EncounterPaperCapture.cs') -Force
    [pscustomobject]@{
        Root = $taskProject
        Source = $taskSource
        SyncedUtc = [DateTime]::UtcNow.ToString('o')
        Identity = 'ShatteredSailEncounterPaperAudit20261011'
        RetainedLibrary = Test-Path -LiteralPath (Join-Path $taskProject 'Library')
        RemovedStaleSources = $taskRemoved.Count
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskOutput ('validation-project' + $Suffix + '.json')) -Encoding utf8
    Write-Output 'Production source synced into isolated project with separate save identity; warm Library retained.'
    exit 0
}

$taskArgs = @('-batchmode','-projectPath',$taskProject,'-logFile',(Join-Path $taskOutput ($Stage + $Suffix + '.log')))
if ($Stage -eq 'PreviewSync') {
    $taskArgs += @(
        '-executeMethod','ShatteredSail.Editor.EncounterPaperPreviewSceneSync.SyncFromCommandLine',
        '-encounterPaperBackupPath',(Join-Path $taskOutput ('scene-backup' + $Suffix)),
        '-encounterPaperReportPath',(Join-Path $taskOutput ('scene-sync' + $Suffix + '.json')),
        '-quit'
    )
} elseif ($Stage -eq 'Capture') {
    $taskArgs += @(
        '-executeMethod','ShatteredSail.Editor.EncounterPaperCapture.CaptureFromCommandLine',
        '-uiAuditPath',(Join-Path $taskOutput ('Screens' + $Suffix)),
        '-quit'
    )
} elseif ($Stage -eq 'Build') {
    $taskArgs += @(
        '-nographics','-executeMethod','ShatteredSail.Editor.WindowsReleaseBuilder.BuildFromCommandLine',
        '-releaseOutput',$BuildPath,'-quit'
    )
} else {
    $taskFilter = if ($TestFilter) {
        $TestFilter
    } elseif ($Stage -eq 'EditMode') {
        'EncounterDossierLayoutTests;NavigationDashboardTests;NavigationEndurancePreviewSceneTests'
    } else {
        'GuidedTutorial_RedFocusGuidesDepartureHeadingDistanceSailAndEventWithoutDimming;GuidedTutorial_OptionalEncounterAllowsNormalChoiceAsWellAsSkip;RareResourceEvents_FourthChoicesScrollAndSpendOnlyCarriedStock'
    }
    # Test Runner owns shutdown. Graphics remain enabled for real layout and input checks.
    $taskArgs += @('-runTests','-testPlatform',$Stage,'-testFilter',$taskFilter,'-testResults',(Join-Path $taskOutput ($Stage + $Suffix + '.xml')))
}

$taskQuotedArgs = $taskArgs | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }
$taskProcess = Start-Process -FilePath $taskUnity -ArgumentList $taskQuotedArgs -WindowStyle Hidden -PassThru
[pscustomobject]@{ Stage=$Stage; Suffix=$Suffix; ProcessId=$taskProcess.Id; Args=$taskArgs } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskOutput ($Stage + $Suffix + '-launch.json')) -Encoding utf8
Write-Output ('Unity PID ' + $taskProcess.Id)
$taskProcess.WaitForExit()
$taskExit = $taskProcess.ExitCode
[pscustomobject]@{ Stage=$Stage; Suffix=$Suffix; ExitCode=$taskExit; CompletedUtc=[DateTime]::UtcNow.ToString('o') } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskOutput ($Stage + $Suffix + '-exit.json')) -Encoding utf8
Write-Output ('Unity exit ' + $taskExit)
if ($taskExit -ne 0) { throw ('Unity failed: ' + $taskExit) }
