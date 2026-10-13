[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReportPath,
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
# Retired by user request; exit before filesystem or Unity actions.
throw 'UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.'
$taskProduction = [IO.Path]::GetFullPath('D:\Game\UnityClient').TrimEnd('\')
$taskIsolated = [IO.Path]::GetFullPath('D:\Game\tmp\departure-structure-20261012').TrimEnd('\')
$taskReportFile = (Resolve-Path -LiteralPath $ReportPath).Path
$taskReport = Get-Content -LiteralPath $taskReportFile -Raw | ConvertFrom-Json
if (-not $taskReport.complete -or @($taskReport.scenes).Count -ne 14) {
    throw 'The report must be a completed 14-scene departure structure synchronization.'
}
if ([IO.Path]::GetFullPath($taskReport.productionProject).TrimEnd('\') -ne $taskProduction -or
    [IO.Path]::GetFullPath($taskReport.isolatedProject).TrimEnd('\') -ne $taskIsolated) {
    throw 'Unexpected production or isolated project in the report.'
}
foreach ($taskScene in $taskReport.scenes) {
    if (-not $taskScene.objectIdsUnchanged -or -not $taskScene.nonVisualStateUnchanged -or -not $taskScene.reloaded -or
        $taskScene.nonVisualBeforeSha256 -ne $taskScene.nonVisualAfterSha256 -or
        $taskScene.yamlNonVisualBeforeSha256 -ne $taskScene.yamlNonVisualAfterSha256) {
        throw "A scene has not passed preservation checks: $($taskScene.path)"
    }
}

$taskPrepared = @()
$taskSeen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($taskFile in $taskReport.copyBackFiles) {
    if (-not $taskFile.assetPath.StartsWith('Assets/', [StringComparison]::Ordinal) -or
        [IO.Path]::IsPathRooted($taskFile.assetPath) -or -not $taskSeen.Add($taskFile.assetPath)) {
        throw "Invalid or duplicated asset path: $($taskFile.assetPath)"
    }
    $taskDestination = [IO.Path]::GetFullPath([IO.Path]::Combine($taskProduction, $taskFile.assetPath))
    $taskSource = [IO.Path]::GetFullPath([IO.Path]::Combine($taskIsolated, $taskFile.assetPath))
    if (-not $taskDestination.StartsWith($taskProduction + '\', [StringComparison]::OrdinalIgnoreCase) -or
        -not $taskSource.StartsWith($taskIsolated + '\', [StringComparison]::OrdinalIgnoreCase) -or
        $taskSource -ne [IO.Path]::GetFullPath($taskFile.sourcePath)) {
        throw "Asset escapes the expected project paths: $($taskFile.assetPath)"
    }
    if (-not (Test-Path -LiteralPath $taskSource -PathType Leaf) -or
        (Get-FileHash -LiteralPath $taskSource -Algorithm SHA256).Hash -ne $taskFile.sha256) {
        throw "Isolated reviewed file has changed: $taskSource"
    }
    if ($taskFile.originallyExisted) {
        if (-not (Test-Path -LiteralPath $taskDestination -PathType Leaf) -or
            (Get-FileHash -LiteralPath $taskDestination -Algorithm SHA256).Hash -ne $taskFile.originalSha256) {
            throw "Production changed since synchronization; no files have been copied: $taskDestination"
        }
    }
    elseif (Test-Path -LiteralPath $taskDestination) {
        throw "New asset collides with an existing production file: $taskDestination"
    }
    $taskPrepared += [pscustomobject]@{ Source = $taskSource; Destination = $taskDestination; Entry = $taskFile }
}

Write-Output "Verified $($taskPrepared.Count) reviewed files against current production hashes."
if (-not $Apply) {
    Write-Output 'Verification only. Add -Apply after reviewing Before, After, visual-fields.diff and preview-sync-report.json.'
    return
}

$taskBackupRoot = Join-Path ([IO.Path]::GetDirectoryName($taskReportFile)) 'CopyBackBackup'
if (Test-Path -LiteralPath $taskBackupRoot) { throw 'Use a fresh synchronization report; copy-back backup already exists.' }
[IO.Directory]::CreateDirectory($taskBackupRoot) | Out-Null
foreach ($taskItem in $taskPrepared) {
    if ($taskItem.Entry.originallyExisted) {
        $taskBackupPath = [IO.Path]::GetFullPath([IO.Path]::Combine($taskBackupRoot, $taskItem.Entry.assetPath))
        if (-not $taskBackupPath.StartsWith($taskBackupRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Backup path escaped its root.' }
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskBackupPath)) | Out-Null
        Copy-Item -LiteralPath $taskItem.Destination -Destination $taskBackupPath -ErrorAction Stop
    }
}
foreach ($taskItem in $taskPrepared) {
    # Recheck immediately before each write in addition to the full preflight.
    if ($taskItem.Entry.originallyExisted) {
        if ((Get-FileHash -LiteralPath $taskItem.Destination -Algorithm SHA256).Hash -ne $taskItem.Entry.originalSha256) {
            throw "Concurrent production edit detected; stopped copying: $($taskItem.Destination)"
        }
    }
    elseif (Test-Path -LiteralPath $taskItem.Destination) { throw "Concurrent asset creation detected: $($taskItem.Destination)" }
    if ((Get-FileHash -LiteralPath $taskItem.Source -Algorithm SHA256).Hash -ne $taskItem.Entry.sha256) {
        throw "Concurrent isolated edit detected: $($taskItem.Source)"
    }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskItem.Destination)) | Out-Null
    Copy-Item -LiteralPath $taskItem.Source -Destination $taskItem.Destination -Force -ErrorAction Stop
    if ((Get-FileHash -LiteralPath $taskItem.Destination -Algorithm SHA256).Hash -ne $taskItem.Entry.sha256) {
        throw "Copy verification failed: $($taskItem.Destination)"
    }
}
Write-Output "Copied and verified $($taskPrepared.Count) files. Original changed files are backed up at $taskBackupRoot."
