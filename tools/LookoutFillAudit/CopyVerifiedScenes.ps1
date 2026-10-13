$ErrorActionPreference = 'Stop'
# Retired by user request; exit before filesystem or Unity actions.
throw 'UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.'
Set-StrictMode -Version Latest

$taskSourceRoot = [IO.Path]::GetFullPath('D:\Game\UnityClient')
$taskScratchRoot = [IO.Path]::GetFullPath('D:\Game\tmp\lookout-fill-20261011')
$taskArtifactRoot = [IO.Path]::GetFullPath('D:\Game\Artifacts\LookoutFill20261011')
$taskReportPath = Join-Path $taskArtifactRoot 'scene-sync.json'
$taskVerificationPath = Join-Path $taskArtifactRoot 'scene-copy-verification.json'

function Get-BytesHash([byte[]]$Bytes) {
    $taskHasher = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($taskHasher.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant() }
    finally { $taskHasher.Dispose() }
}

function Get-StreamBytes([IO.Stream]$Stream) {
    $Stream.Position = 0
    $taskMemory = [IO.MemoryStream]::new()
    try {
        $Stream.CopyTo($taskMemory)
        return ,$taskMemory.ToArray()
    } finally { $taskMemory.Dispose() }
}

function Assert-Hash([string]$Path, [string]$Expected, [string]$Description) {
    if (-not [IO.File]::Exists($Path)) { throw "$Description is missing: $Path" }
    $taskActual = Get-BytesHash ([IO.File]::ReadAllBytes($Path))
    if ($taskActual -cne $Expected) { throw "$Description hash changed: $Path" }
}

function Assert-NoReparsePath([string]$Path, [string]$Root) {
    $taskCurrent = [IO.Path]::GetFullPath($Path)
    $taskBoundary = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    if (-not $taskCurrent.StartsWith($taskBoundary + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escapes permitted root: $Path"
    }
    while ($taskCurrent.Length -ge $taskBoundary.Length) {
        if (([IO.File]::GetAttributes($taskCurrent) -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Refusing linked scene path: $taskCurrent"
        }
        if ($taskCurrent.Equals($taskBoundary, [StringComparison]::OrdinalIgnoreCase)) { break }
        $taskCurrent = [IO.Path]::GetDirectoryName($taskCurrent)
    }
}

function Get-ScenePath([string]$Root, [string]$Relative) {
    if ([IO.Path]::IsPathRooted($Relative) -or $Relative -notmatch '^Assets/_Project/.+\.unity$' -or
        @($Relative.Split('/') | Where-Object { $_ -eq '..' -or $_ -eq '.' -or $_ -eq '' }).Count -gt 0 -or
        $Relative.Contains(':') -or $Relative.Contains('\')) {
        throw "Invalid report scene path: $Relative"
    }
    $taskPath = [IO.Path]::GetFullPath((Join-Path $Root $Relative.Replace('/', '\')))
    $taskAllowedRoot = Join-Path $Root 'Assets\_Project'
    Assert-NoReparsePath $taskPath $taskAllowedRoot
    Assert-NoReparsePath ($taskPath + '.meta') $taskAllowedRoot
    return $taskPath
}

$taskReport = [IO.File]::ReadAllText($taskReportPath) | ConvertFrom-Json
$taskBackupRoot = [IO.Path]::GetFullPath([string]$taskReport.backupRoot)
if (-not $taskBackupRoot.StartsWith($taskArtifactRoot + '\scene-backup', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Migration backup directory must belong to this lookout-fill audit.'
}
Assert-NoReparsePath $taskBackupRoot $taskArtifactRoot
$taskUpdated = @($taskReport.scenes | Where-Object { $_.updated -eq $true })
if ($taskUpdated.Count -ne [int]$taskReport.updatedScenes) { throw 'Report updated-scene count does not match its entries.' }
$taskSeen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$taskPrepared = [Collections.Generic.List[object]]::new()

# Validate the complete copy set before acquiring write handles or changing a file.
foreach ($taskScene in $taskUpdated) {
    $taskRelative = [string]$taskScene.path
    if (-not $taskSeen.Add($taskRelative)) { throw "Duplicate scene in report: $taskRelative" }
    $taskBeforeHash = ([string]$taskScene.beforeSha256).ToLowerInvariant()
    $taskAfterHash = ([string]$taskScene.afterSha256).ToLowerInvariant()
    $taskMetaHash = ([string]$taskScene.metaSha256).ToLowerInvariant()
    foreach ($taskDigest in @($taskBeforeHash, $taskAfterHash, $taskMetaHash)) {
        if ($taskDigest -notmatch '^[0-9a-f]{64}$') { throw "Invalid SHA-256 in report: $taskRelative" }
    }
    $taskDestination = Get-ScenePath $taskSourceRoot $taskRelative
    $taskScratch = Get-ScenePath $taskScratchRoot $taskRelative
    $taskBackup = Get-ScenePath $taskBackupRoot $taskRelative
    Assert-Hash $taskDestination $taskBeforeHash 'Production scene'
    Assert-Hash $taskScratch $taskAfterHash 'Migrated scratch scene'
    Assert-Hash ($taskDestination + '.meta') $taskMetaHash 'Production scene metadata'
    Assert-Hash ($taskScratch + '.meta') $taskMetaHash 'Scratch scene metadata'
    Assert-Hash $taskBackup $taskBeforeHash 'Migration backup'
    Assert-Hash ($taskBackup + '.meta') $taskMetaHash 'Migration backup metadata'
    $taskPrepared.Add([pscustomobject]@{
        Path = $taskRelative; Source = $taskScratch; Destination = $taskDestination; Backup = $taskBackup
        BeforeHash = $taskBeforeHash; AfterHash = $taskAfterHash; MetaHash = $taskMetaHash
        BeforeBytes = $null; AfterBytes = $null; Stream = $null; MetaStream = $null
        Attempted = $false; Verified = $false
    })
}

$taskVerified = [Collections.Generic.List[object]]::new()
$taskSucceeded = $false
try {
    # Keep all production scenes exclusively locked throughout the transaction.
    # Read locks on metadata prevent GUID changes; metadata is never written.
    foreach ($taskItem in $taskPrepared) {
        $taskItem.Stream = [IO.File]::Open($taskItem.Destination, [IO.FileMode]::Open,
            [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        $taskItem.MetaStream = [IO.File]::Open(($taskItem.Destination + '.meta'), [IO.FileMode]::Open,
            [IO.FileAccess]::Read, [IO.FileShare]::Read)
    }
    foreach ($taskItem in $taskPrepared) {
        $taskItem.BeforeBytes = Get-StreamBytes $taskItem.Stream
        if ((Get-BytesHash $taskItem.BeforeBytes) -cne $taskItem.BeforeHash) {
            throw "Production scene changed before its write lock: $($taskItem.Path)"
        }
        if ((Get-BytesHash (Get-StreamBytes $taskItem.MetaStream)) -cne $taskItem.MetaHash) {
            throw "Production metadata changed before its read lock: $($taskItem.Path)"
        }
        $taskItem.AfterBytes = [IO.File]::ReadAllBytes($taskItem.Source)
        if ((Get-BytesHash $taskItem.AfterBytes) -cne $taskItem.AfterHash) {
            throw "Scratch scene changed after preflight: $($taskItem.Path)"
        }
        Assert-Hash ($taskItem.Source + '.meta') $taskItem.MetaHash 'Scratch scene metadata'
        Assert-Hash $taskItem.Backup $taskItem.BeforeHash 'Migration backup'
        Assert-Hash ($taskItem.Backup + '.meta') $taskItem.MetaHash 'Migration backup metadata'
    }
    foreach ($taskItem in $taskPrepared) {
        $taskItem.Attempted = $true
        $taskItem.Stream.Position = 0
        $taskItem.Stream.Write($taskItem.AfterBytes, 0, $taskItem.AfterBytes.Length)
        $taskItem.Stream.SetLength($taskItem.AfterBytes.Length)
        $taskItem.Stream.Flush($true)
        if ((Get-BytesHash (Get-StreamBytes $taskItem.Stream)) -cne $taskItem.AfterHash) {
            throw "Copied scene verification failed: $($taskItem.Path)"
        }
        $taskItem.Verified = $true
        $taskVerified.Add([pscustomobject]@{
            Path = $taskItem.Path; Source = $taskItem.Source; Destination = $taskItem.Destination
            Backup = $taskItem.Backup; BeforeHash = $taskItem.BeforeHash
            AfterHash = $taskItem.AfterHash; MetaHash = $taskItem.MetaHash
            ExclusiveWriteLock = $true; Verified = $true
        })
    }
    [pscustomobject]@{
        Status = 'PASS'; CopiedScenes = $taskVerified.Count; Report = $taskReportPath
        CompletedUtc = [DateTime]::UtcNow.ToString('o'); PreservedMetadata = $true
        Scenes = @($taskVerified.ToArray())
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $taskVerificationPath -Encoding utf8
    $taskSucceeded = $true
} finally {
    # A failed transaction restores only bytes written by this script while it
    # still owns every production write lock. Concurrent edits are never replaced.
    foreach ($taskItem in $taskPrepared) {
        try {
            if (-not $taskSucceeded -and $taskItem.Attempted) {
                $taskItem.Stream.Position = 0
                $taskItem.Stream.Write($taskItem.BeforeBytes, 0, $taskItem.BeforeBytes.Length)
                $taskItem.Stream.SetLength($taskItem.BeforeBytes.Length)
                $taskItem.Stream.Flush($true)
                if ((Get-BytesHash (Get-StreamBytes $taskItem.Stream)) -cne $taskItem.BeforeHash) {
                    Write-Error -Message "Rollback verification failed: $($taskItem.Path)" -ErrorAction Continue
                }
            }
        } catch { Write-Error -Message "Rollback failed for $($taskItem.Path): $_" -ErrorAction Continue }
        finally {
            if ($null -ne $taskItem.MetaStream) { $taskItem.MetaStream.Dispose() }
            if ($null -ne $taskItem.Stream) { $taskItem.Stream.Dispose() }
        }
    }
}
Write-Output ("Verified and copied {0} scenes; original metadata preserved." -f $taskVerified.Count)
