param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('Plan','Snapshot','EditMode','PlayMode','Verify','Windows','Smoke','Package','Seal','Cleanup')]
    [string]$Stage,
    [ValidatePattern('^[0-9]{2}$')][string]$Revision = '01',
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')][string]$Version = '0.3.20261105.1',
    [ValidateRange(1,240)][int]$TimeoutMinutes = 90,
    [string]$TestFilter = '',
    [ValidatePattern('^([0-9]{2})?$')][string]$ReferenceBackupRevision = '',
    [string]$ScopeFile = ''
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$source = Join-Path $workspace 'UnityClient'
$runRoot = Join-Path $workspace 'Artifacts/IntegratedRelease20261105'
$frozen = Join-Path $runRoot ('FrozenSource' + $Revision + '/UnityClient')
$project = Join-Path $workspace 'tmp/shipyard-detail-clean-20261011'
$backupRevision = if ($ReferenceBackupRevision) { $ReferenceBackupRevision } else { $Revision }
$lease = Join-Path $project 'IntegratedRelease20261105.lock'
$snapshot = Join-Path $runRoot ('Snapshot' + $Revision)
$edit = Join-Path $runRoot ('Edit' + $Revision)
$play = Join-Path $runRoot ('Play' + $Revision)
$verify = Join-Path $runRoot ('Verification' + $Revision)
$release = Join-Path $workspace ('Builds/第七港口-统一验收20261105-r' + $Revision)
$entry = Join-Path $workspace 'tools/ReleaseBuild/DesktopRelease.ps1'
$arguments = @{
    ReleaseDirectory = $release
    EvidenceDirectory = $snapshot
    Version = $Version
    ZipFileName = '第七港口.zip'
    ValidationSummary = (Join-Path $verify 'verification-summary.json')
    ProjectPath = $project
    SourceProjectPath = $frozen
}

function Assert-Unlinked([string]$Path) {
    Assert-InWorkspace $Path
    for ($at=[IO.Path]::GetFullPath($Path); $at; $at=Split-Path $at -Parent) {
        if ((Test-Path -LiteralPath $at) -and ((Get-Item -LiteralPath $at -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw ('Linked path: '+$at) }
    }
}
function Assert-Idle {
    $active = @(Get-CimInstance Win32_Process | Where-Object {
        $_.ProcessId -ne $PID -and $_.CommandLine -and $_.CommandLine.Replace('/','\').IndexOf($project,[StringComparison]::OrdinalIgnoreCase) -ge 0
    })
    if ($active.Count) { throw ('Validation project is referenced by running processes: '+($active.ProcessId -join ',')) }
    if (Test-Path -LiteralPath (Join-Path $project 'Temp/UnityLockfile')) { throw 'Unity lock remains; preserve the borrowed project.' }
}
function Assert-Lease {
    Assert-Unlinked $lease
    if (!(Test-Path -LiteralPath $lease -PathType Leaf) -or [IO.File]::ReadAllText($lease).Trim() -ne $runRoot) { throw 'The current release task lease is missing or belongs to another task.' }
}
function Assert-InWorkspace([string]$Path) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($workspace + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
        throw ('Path escaped workspace: ' + $fullPath)
    }
}

if ($Stage -eq 'Plan') {
    [pscustomobject]@{
        Version=$Version; Source=$source; FrozenSource=$frozen; Project=$project;
        Snapshot=$snapshot; EditMode=$edit; PlayMode=$play; Verification=$verify;
        Release=$release; Executable=(Join-Path $release 'Windows/第七港口.exe');
        Package=(Join-Path $release $arguments.ZipFileName);
        Stages=@('Snapshot','EditMode','PlayMode','Verify','Windows','Smoke','Package','Seal','Cleanup');
        SourceChanges='None; source is copied into a fresh immutable release mirror.';
        GitChanges='None; this wrapper does not commit or push.';
        TestScope='User-authorized changed and related tests only; Verify requires an explicit scope file and does not claim a full PlayMode regression.';
        ScopeFile=$ScopeFile;
        ReferenceBackupRevision=$backupRevision; Lease=$lease;
        OriginalProjectInputs=(Join-Path $runRoot ('previous-validation-full-inputs-' + $backupRevision + '.csv'))
    } | ConvertTo-Json -Depth 3
    exit 0
}

if ($Stage -eq 'Cleanup') {
    Assert-Lease
    & (Join-Path $workspace 'tools/GameplayAuditRelease20261103/Cleanup.ps1') -SnapshotDirectory $snapshot -ReleaseDirectory $release -ProjectPath $project
    exit $LASTEXITCODE
}

if ($Stage -eq 'Snapshot') {
    Assert-Idle
    foreach ($path in @($source,$project,$runRoot)) { Assert-Unlinked $path }
    foreach ($path in @($frozen,$snapshot,$release,$edit,$play,$verify)) {
        Assert-Unlinked $path
        if (Test-Path -LiteralPath $path) { throw ('Use a fresh revision; preserve existing directory: ' + $path) }
    }
    $retired = Join-Path $source 'Assets/_Project/Scenes/UIPreviewsForMe'
    if ((Test-Path -LiteralPath $retired) -and @(Get-ChildItem -LiteralPath $retired -Force).Count) {
        throw 'The retired preview directory must remain empty.'
    }
    & (Join-Path $workspace 'tools/ReleaseBuild/AssertMetaGuids.ps1') -AssetsPath (Join-Path $source 'Assets')
    # The first revision owns the original borrowed baseline. Retries explicitly
    # reference it and never replace those manifests or bytes.
    $previousInputs = Join-Path $runRoot ('PreviousValidationInputs' + $backupRevision)
    $originalManifest = Join-Path $runRoot ('previous-validation-full-inputs-' + $backupRevision + '.csv')
    $backupManifest = Join-Path $runRoot ('previous-validation-inputs-' + $backupRevision + '.csv')
    $referenceFrozen = Join-Path $runRoot ('FrozenSource' + $backupRevision + '/UnityClient')
    $isRetry = $backupRevision -ne $Revision
    if ($isRetry) {
        Assert-Lease
        foreach ($path in @($originalManifest,$backupManifest,$referenceFrozen)) {
            Assert-Unlinked $path
            if (!(Test-Path -LiteralPath $path)) { throw ('Initial borrowing evidence is missing: '+$path) }
        }
        if (Test-Path -LiteralPath (Join-Path $runRoot ('Snapshot'+$backupRevision+'/borrowed-input-restoration.json'))) { throw 'The reference borrowing session was already restored; start a new independent baseline.' }
    } else {
        foreach ($path in @($previousInputs,$originalManifest,$backupManifest,$lease)) {
            Assert-Unlinked $path
            if (Test-Path -LiteralPath $path) { throw ('Preserve existing borrowing evidence or lease: '+$path) }
        }
        foreach ($other in Get-ChildItem -LiteralPath $project -File -Filter '*.lock' -Force) {
            if ($other.Name -ne 'TutorialCompletion20261014.lock' -or $other.Length -ne 0) { throw ('Another task lease exists: '+$other.FullName) }
        }
        Assert-Idle
        $stream=[IO.File]::Open($lease,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
        try { $bytes=[Text.UTF8Encoding]::new($false).GetBytes($runRoot); $stream.Write($bytes,0,$bytes.Length) } finally { $stream.Dispose() }
    }
    $previousManifest = [Collections.Generic.List[object]]::new()
    $previousFullManifest = [Collections.Generic.List[object]]::new()
    foreach ($root in @($source, $project)) {
        foreach ($part in @('Assets','Packages','ProjectSettings')) {
            $inputRoot = Join-Path $root $part
            Assert-Unlinked $inputRoot
            if (-not (Test-Path -LiteralPath $inputRoot)) { continue }
            $inputNodes = @(Get-Item -LiteralPath $inputRoot) + @(Get-ChildItem -LiteralPath $inputRoot -Recurse -Force)
            if (@($inputNodes | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw ('Input tree contains a link: ' + $inputRoot) }
        }
    }
    foreach ($part in @('Assets','Packages','ProjectSettings')) {
        foreach ($old in Get-ChildItem -LiteralPath (Join-Path $project $part) -File -Recurse -Force) {
            $relative = $old.FullName.Substring($project.Length+1)
            if ($relative -match '[\\/]UIPreviewsForMe(?:[\\/.]|$)') { continue }
            $oldHash = (Get-FileHash -LiteralPath $old.FullName).Hash
            $previousFullManifest.Add([pscustomobject]@{Path=$relative; Bytes=$old.Length; SHA256=$oldHash})
            if ($isRetry) { continue }
            $current = Join-Path $source $relative
            if ((Test-Path -LiteralPath $current) -and (Get-FileHash -LiteralPath $current).Hash -eq $oldHash) { continue }
            $archive = Join-Path $previousInputs $relative
            Assert-Unlinked $archive
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($archive)) | Out-Null
            Copy-Item -LiteralPath $old.FullName -Destination $archive
            if ((Get-FileHash -LiteralPath $archive).Hash -ne $oldHash) { throw ('Validation input backup failed: ' + $relative) }
            $previousManifest.Add([pscustomobject]@{Path=$relative; Bytes=$old.Length; SHA256=$oldHash})
        }
    }
    [IO.Directory]::CreateDirectory($runRoot) | Out-Null
    $previousFullManifest | Export-Csv -LiteralPath (Join-Path $runRoot ('pre-snapshot-inputs-' + $Revision + '.csv')) -NoTypeInformation -Encoding utf8
    if (!$isRetry) {
        if ($previousManifest.Count) { $previousManifest | Export-Csv -LiteralPath $backupManifest -NoTypeInformation -Encoding utf8 }
        else { '"Path","Bytes","SHA256"' | Set-Content -LiteralPath $backupManifest -Encoding utf8 }
        $previousFullManifest | Export-Csv -LiteralPath $originalManifest -NoTypeInformation -Encoding utf8
    }
    [pscustomobject]@{Revision=$Revision;ReferenceBackupRevision=$backupRevision;Lease=$lease;OriginalManifest=$originalManifest;BackupManifest=$backupManifest} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runRoot ('borrowing-'+$Revision+'.json')) -Encoding utf8
    $manifest = [Collections.Generic.List[object]]::new()
    foreach ($part in @('Assets','Packages','ProjectSettings')) {
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $source $part) -File -Recurse -Force) {
            if ($file.FullName -match '[\\/]UIPreviewsForMe(?:[\\/.]|$)') { continue }
            $relative = $file.FullName.Substring($source.Length+1)
            $target = Join-Path $frozen $relative
            Assert-InWorkspace $target
            $originalHash = (Get-FileHash -LiteralPath $file.FullName).Hash
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $target
            if ((Get-FileHash -LiteralPath $target).Hash -ne $originalHash -or
                (Get-FileHash -LiteralPath $file.FullName).Hash -ne $originalHash) {
                throw ('Source changed while freezing release inputs: ' + $relative)
            }
            $manifest.Add([pscustomobject]@{ Path=$relative; Bytes=$file.Length; SHA256=$originalHash })
        }
    }
    $manifest | Export-Csv -LiteralPath (Join-Path $runRoot ('frozen-source-' + $Revision + '.csv')) -NoTypeInformation -Encoding utf8
    # Prove the original borrowed checkout is fully recoverable before any
    # synchronization. Unchanged first-round inputs live in its frozen source.
    $initialInputs = @(Import-Csv -LiteralPath $originalManifest)
    if (!$initialInputs.Count) { throw 'The initial borrowed input manifest is empty.' }
    $initialBackupMap = @{}
    foreach ($record in @(Import-Csv -LiteralPath $backupManifest)) { $initialBackupMap[$record.Path]=$record }
    foreach ($record in $initialInputs) {
        $relative=[string]$record.Path
        if ($relative -notmatch '^(Assets|Packages|ProjectSettings)\\' -or $relative -match '(^|\\)\.\.?($|\\)' -or $record.SHA256 -notmatch '^[A-Fa-f0-9]{64}$') { throw 'Invalid initial borrowed input record.' }
        $restoreFile=if($initialBackupMap.ContainsKey($relative)){Join-Path $previousInputs $relative}else{Join-Path $referenceFrozen $relative}
        Assert-Unlinked $restoreFile
        if (!(Test-Path -LiteralPath $restoreFile -PathType Leaf) -or (Get-FileHash -LiteralPath $restoreFile).Hash -ne $record.SHA256) { throw ('Initial borrowed bytes are unavailable: '+$relative) }
    }
    foreach ($part in @('Assets','Packages','ProjectSettings')) {
        [IO.Directory]::CreateDirectory((Join-Path $project $part)) | Out-Null
    }
    Copy-Item -LiteralPath (Join-Path $frozen 'ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $project 'ProjectSettings/ProjectVersion.txt')
    & $entry -Stage Snapshot @arguments
    $snapshotExit = $LASTEXITCODE
    if ($snapshotExit -ne 0) { exit $snapshotExit }
    # Quarantining old files leaves their directories behind. Unity would create
    # new directory meta files for these orphan folders on the next import.
    # Only empty, unlinked directories absent from the frozen source are removed.
    Assert-Idle
    $removedEmptyDirectories = [Collections.Generic.List[string]]::new()
    foreach ($part in @('Assets','Packages','ProjectSettings')) {
        $inputRoot = Join-Path $project $part
        foreach ($directory in @(Get-ChildItem -LiteralPath $inputRoot -Directory -Recurse -Force | Sort-Object { $_.FullName.Length } -Descending)) {
            $full = [IO.Path]::GetFullPath($directory.FullName)
            if (-not $full.StartsWith($inputRoot + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Empty directory escaped the borrowed input root.' }
            $relative = $full.Substring($project.Length+1)
            if ($relative -match '[\\/]UIPreviewsForMe(?:[\\/.]|$)') { continue }
            if ((Test-Path -LiteralPath (Join-Path $frozen $relative)) -or (Test-Path -LiteralPath ((Join-Path $frozen $relative) + '.meta'))) { continue }
            Assert-Unlinked $full
            if (@(Get-ChildItem -LiteralPath $full -Force).Count -ne 0) { continue }
            Remove-Item -LiteralPath $full -Force
            if (Test-Path -LiteralPath $full) { throw ('Empty directory was not removed: '+$full) }
            $removedEmptyDirectories.Add($relative)
        }
    }
    [pscustomobject]@{Result='PASS';RemovedEmptyDirectories=@($removedEmptyDirectories);NoFilesRemoved=$true;Boundary=$project} |
        ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $snapshot 'orphan-empty-directories.json') -Encoding utf8
    exit 0
}

if ($Stage -in @('EditMode','PlayMode')) {
    Assert-Lease
    $destination = if ($Stage -eq 'EditMode') { $edit } else { $play }
    if ($TestFilter) { $destination = Join-Path $runRoot ('Diagnostics'+$Revision+'/'+$Stage+'-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')) }
    & (Join-Path $workspace 'tools/ReleaseBuild/RunTests.ps1') -Platform $Stage -ProjectPath $project -Output $destination -SourceManifest (Join-Path $snapshot 'source-manifest.csv') -TimeoutMinutes $TimeoutMinutes -TestFilter $TestFilter
    exit $LASTEXITCODE
}

if ($Stage -eq 'Verify') {
    Assert-Lease
    if ([string]::IsNullOrWhiteSpace($ScopeFile)) { throw 'Provide the user-authorized changed-scope JSON with -ScopeFile.' }
    & (Join-Path $PSScriptRoot 'ChangedScopeVerify.ps1') -ScopeFile $ScopeFile -SnapshotDirectory $snapshot -OutputDirectory $verify
    exit 0
}

Assert-Lease
& $entry -Stage $Stage @arguments
exit $LASTEXITCODE
