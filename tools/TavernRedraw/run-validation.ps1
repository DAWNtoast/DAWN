param(
    [ValidateSet('Prepare', 'SyncArt', 'SyncCode', 'Compile', 'Capture', 'CaptureSaved', 'EditMode')]
    [string]$Action = 'Prepare',
    [string]$RunName = 'final'
)
$ErrorActionPreference = 'Stop'
# Retired by user request; exit before filesystem or Unity actions.
if (@('CaptureSaved') -contains $Action) { throw 'UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.' }
$taskSource = [IO.Path]::GetFullPath('D:\Game\UnityClient')
$taskScratch = [IO.Path]::GetFullPath('D:\Game\tmp\tavern-redraw-20261012')
$taskOutputRoot = [IO.Path]::GetFullPath('D:\Game\Artifacts\TavernRedraw20261012\validation')
if ($RunName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'RunName must be a simple directory name.' }
$taskOutput = Join-Path $taskOutputRoot $RunName
$taskMarker = Join-Path $taskScratch '.tavern-redraw-validation'
if ($taskScratch -ne 'D:\Game\tmp\tavern-redraw-20261012') { throw 'Unexpected scratch path.' }
if (Test-Path -LiteralPath (Join-Path $taskScratch 'Temp\UnityLockfile')) { throw 'Scratch Unity may be running; stop it before using this script.' }

if ($Action -eq 'Prepare') {
    if (Test-Path -LiteralPath $taskScratch) { throw 'Refusing to overwrite an existing scratch project.' }
    New-Item -ItemType Directory -Path $taskScratch -Force | Out-Null
    foreach ($taskPart in @('Assets', 'Packages', 'ProjectSettings')) {
        & robocopy (Join-Path $taskSource $taskPart) (Join-Path $taskScratch $taskPart) /E /NFL /NDL /NJH /NJS /NP
        if ($LASTEXITCODE -gt 7) { throw "Initial copy failed: $taskPart, exit $LASTEXITCODE" }
    }
    $taskSettings = Join-Path $taskScratch 'ProjectSettings\ProjectSettings.asset'
    $taskText = [IO.File]::ReadAllText($taskSettings)
    $taskText = $taskText -replace '(?m)^  companyName:.*$', '  companyName: TavernRedrawValidation' -replace '(?m)^  productName:.*$', '  productName: TavernRedrawValidation20261012' -replace '(?m)^    Standalone: com\.shatteredsail\.shattered-sail\r?$', '    Standalone: com.validation.tavernredraw20261012'
    [IO.File]::WriteAllText($taskSettings, $taskText, [Text.UTF8Encoding]::new($false))
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'TavernRedrawCapture.cs') -Destination (Join-Path $taskScratch 'Assets\_Project\Editor\TavernRedrawCapture.cs')
    [IO.File]::WriteAllText($taskMarker, 'Dedicated tavern validation project; production remains read-only.')
    Write-Output "Prepared dedicated scratch: $taskScratch (Unity not started)."
    exit 0
}
if (-not (Test-Path -LiteralPath $taskMarker)) { throw 'Dedicated scratch marker missing; run Prepare first.' }
if ($Action -eq 'SyncCode') {
    $taskCodeSource = Join-Path $taskSource 'Assets\_Project'
    $taskCodeScratch = Join-Path $taskScratch 'Assets\_Project'
    $taskCodeRecords = [System.Collections.Generic.List[object]]::new()
    $taskSourceFiles = @(Get-ChildItem -LiteralPath $taskCodeSource -File -Recurse | Where-Object { $_.Extension -in @('.cs', '.asmdef') })
    foreach ($taskFile in $taskSourceFiles) {
        $taskRelative = $taskFile.FullName.Substring($taskCodeSource.Length + 1)
        foreach ($taskSuffix in @('', '.meta')) {
            $taskInput = $taskFile.FullName + $taskSuffix
            if (-not (Test-Path -LiteralPath $taskInput)) {
                if ($taskSuffix -eq '.meta') {
                    $taskCodeRecords.Add([PSCustomObject]@{ path=$taskRelative+$taskSuffix; action='source-meta-not-yet-authored'; sha256=$null })
                    continue
                }
                throw "Source code disappeared during sync: $taskInput"
            }
            $taskDestination = [IO.Path]::GetFullPath((Join-Path $taskCodeScratch ($taskRelative + $taskSuffix)))
            if (-not $taskDestination.StartsWith($taskScratch + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Code destination escaped dedicated scratch.' }
            New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($taskDestination)) -Force | Out-Null
            $taskHash = (Get-FileHash -LiteralPath $taskInput -Algorithm SHA256).Hash
            Copy-Item -LiteralPath $taskInput -Destination $taskDestination -Force
            if ((Get-FileHash -LiteralPath $taskDestination -Algorithm SHA256).Hash -ne $taskHash) { throw 'Source changed during code copy; rerun SyncCode.' }
            $taskCodeRecords.Add([PSCustomObject]@{ path=$taskRelative+$taskSuffix; action='copied'; sha256=$taskHash })
        }
    }
    $taskScratchFiles = @(Get-ChildItem -LiteralPath $taskCodeScratch -File -Recurse | Where-Object { $_.Extension -in @('.cs', '.asmdef') })
    foreach ($taskFile in $taskScratchFiles) {
        $taskRelative = $taskFile.FullName.Substring($taskCodeScratch.Length + 1)
        if ($taskRelative -eq 'Editor\TavernRedrawCapture.cs') { continue }
        if (Test-Path -LiteralPath (Join-Path $taskCodeSource $taskRelative)) { continue }
        foreach ($taskSuffix in @('', '.meta')) {
            $taskObsolete = [IO.Path]::GetFullPath($taskFile.FullName + $taskSuffix)
            if (-not $taskObsolete.StartsWith($taskScratch + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Obsolete path escaped dedicated scratch.' }
            if (Test-Path -LiteralPath $taskObsolete) {
                $taskHash = (Get-FileHash -LiteralPath $taskObsolete -Algorithm SHA256).Hash
                Remove-Item -LiteralPath $taskObsolete -Force
                $taskCodeRecords.Add([PSCustomObject]@{ path=$taskRelative+$taskSuffix; action='removed-from-scratch'; sha256=$taskHash })
            }
        }
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'TavernRedrawCapture.cs') -Destination (Join-Path $taskCodeScratch 'Editor\TavernRedrawCapture.cs') -Force
    New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
    $taskCodeReport = Join-Path $taskOutput 'code-sync.json'
    if (Test-Path -LiteralPath $taskCodeReport) { throw 'Use a fresh RunName for code-sync evidence.' }
    [IO.File]::WriteAllText($taskCodeReport, ($taskCodeRecords | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
    Write-Output "Synchronized current source code only to dedicated scratch; $($taskCodeRecords.Count) records in $taskCodeReport"
    exit 0
}
if ($Action -eq 'SyncArt') {
    $taskArtPaths = @('Assets\_Project\Resources\PortFacilities\tavern_background_seated_v2.png')
    foreach ($taskPort in @('broken_anchor','white_shell','bronze_bell','salt_crown','twilight_tide','gray_gull')) {
        $taskArtPaths += 'Assets\_Project\Resources\PortFacilities\Regional\Tavern\tavern_' + $taskPort + '_v1.png'
    }
    $taskArtPaths += @(
        'Assets\_Project\Presentation\Runtime\TavernCrewLayoutCatalog.cs',
        'Assets\_Project\Presentation\Runtime\CoreGameplayPortController.cs',
        'Assets\_Project\Tests\EditMode\TavernRegionalInteractionLayoutTests.cs'
    )
    foreach ($taskArtPath in $taskArtPaths) {
        foreach ($taskSuffix in @('', '.meta')) {
            Copy-Item -LiteralPath (Join-Path $taskSource ($taskArtPath + $taskSuffix)) -Destination (Join-Path $taskScratch ($taskArtPath + $taskSuffix)) -Force
        }
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'TavernRedrawCapture.cs') -Destination (Join-Path $taskScratch 'Assets\_Project\Editor\TavernRedrawCapture.cs') -Force
    Write-Output 'Copied the explicit allowlist: seven PNGs, two tavern source files, the regional interaction test, their metas, and the scratch-only capture helper; retired roster scenes are excluded.'
    exit 0
}
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskLog = Join-Path $taskOutput ($Action.ToLowerInvariant() + '.log')
if (Test-Path -LiteralPath $taskLog) { throw 'Use a fresh RunName to preserve existing evidence.' }
$taskArguments = @('-batchmode', '-projectPath', $taskScratch, '-logFile', $taskLog)
if ($Action -eq 'Compile') {
    $taskArguments += @('-quit')
} elseif ($Action -eq 'Capture') {
    $taskArguments += @('-quit', '-executeMethod', 'ShatteredSail.Editor.TavernRedrawCapture.CaptureFromCommandLine', '-tavernRedrawCapturePath', (Join-Path $taskOutput 'ui-review'))
} elseif ($Action -eq 'CaptureSaved') {
    $taskArguments += @('-quit', '-executeMethod', 'ShatteredSail.Editor.TavernRedrawCapture.CaptureSavedFromCommandLine', '-tavernSavedCapturePath', (Join-Path $taskOutput 'saved-preview-review'))
} else {
    $taskFilter = 'ShatteredSail.Core.Tests.RegionalPortFacilityArtCatalogTests;ShatteredSail.Presentation.Tests.TavernRosterIntegrityTests;ShatteredSail.Presentation.Tests.TavernRegionalInteractionLayoutTests;ShatteredSail.Presentation.Tests.PortSurfaceGroundingTests;ShatteredSail.Presentation.Tests.PortFacilityArtCatalogLazyTests;ShatteredSail.Presentation.Tests.DesktopBackgroundCompressionTests'
    $taskArguments += @('-runTests', '-testPlatform', 'EditMode', '-testFilter', $taskFilter, '-testResults', (Join-Path $taskOutput 'editmode.xml'))
}
$taskProcess = Start-Process -FilePath 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' -WindowStyle Hidden -ArgumentList $taskArguments -Wait -PassThru
Write-Output "Unity $Action exit code: $($taskProcess.ExitCode); log: $taskLog"
exit $taskProcess.ExitCode
