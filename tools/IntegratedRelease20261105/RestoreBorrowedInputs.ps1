param(
    [ValidatePattern('^[0-9]{2}$')][string]$Revision = '01',
    [ValidatePattern('^([0-9]{2})?$')][string]$ReferenceBackupRevision = ''
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$project = Join-Path $workspace 'tmp\shipyard-detail-clean-20261011'
$main = Join-Path $workspace 'UnityClient'
$runRoot = Join-Path $workspace 'Artifacts\IntegratedRelease20261105'
$snapshot = Join-Path $runRoot ('Snapshot' + $Revision)
$release = Join-Path $workspace ('Builds\第七港口-统一验收20261105-r' + $Revision)
$lease = Join-Path $project 'IntegratedRelease20261105.lock'
$oldLease = Join-Path $project 'TutorialCompletion20261014.lock'
$reportPath = Join-Path $snapshot 'borrowed-input-restoration.json'
$parts = @('Assets','Packages','ProjectSettings')
function Assert-Within([string]$Path, [string]$Boundary) {
    $full = [IO.Path]::GetFullPath($Path).TrimEnd('\','/')
    $limit = [IO.Path]::GetFullPath($Boundary).TrimEnd('\','/')
    if ($full -ne $limit -and -not $full.StartsWith($limit+'\',[StringComparison]::OrdinalIgnoreCase)) {
        throw ('Path escaped permitted boundary: ' + $full)
    }
    return $full
}
function Assert-Unlinked([string]$Path, [string]$Boundary) {
    $full = Assert-Within $Path $Boundary
    for ($at=$full; $at; $at=Split-Path $at -Parent) {
        if (Test-Path -LiteralPath $at) {
            $item=Get-Item -LiteralPath $at -Force
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw ('Linked path: '+$at) }
        }
    }
}
function Assert-Idle {
    $active = @(Get-CimInstance Win32_Process | Where-Object {
        $_.ProcessId -ne $PID -and $_.CommandLine -and
        $_.CommandLine.Replace('/','\').IndexOf($project,[StringComparison]::OrdinalIgnoreCase) -ge 0
    })
    if ($active.Count) { throw ('Borrowed project is referenced by running processes: '+($active.ProcessId -join ',')) }
    if (Test-Path -LiteralPath (Join-Path $project 'Temp\UnityLockfile')) { throw 'Unity lock remains; preserve borrowed inputs.' }
}
function Is-Retired([string]$Path) { return $Path -match '(^|[\\/])UIPreviewsForMe(?:[\\/.]|$)' }
function Relative-Path([string]$Value) {
    $relative=$Value.Replace('/','\')
    if ([IO.Path]::IsPathRooted($relative) -or $relative -notmatch '^(Assets|Packages|ProjectSettings)\\' -or
        $relative -match '(^|\\)\.\.?($|\\)' -or (Is-Retired $relative)) { throw ('Invalid input manifest path: '+$Value) }
    $null=Assert-Within (Join-Path $project $relative) $project
    return $relative
}
function Read-Manifest([string]$Path,[bool]$AllowEmpty=$false) {
    Assert-Unlinked $Path $runRoot
    $map=[Collections.Generic.Dictionary[string,object]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in @(Import-Csv -LiteralPath $Path)) {
        $relative=Relative-Path $entry.Path
        if ($entry.SHA256 -notmatch '^[A-Fa-f0-9]{64}$' -or [long]$entry.Bytes -lt 0 -or $map.ContainsKey($relative)) {
            throw ('Invalid/duplicate manifest entry: '+$relative)
        }
        $map.Add($relative,$entry)
    }
    if (!$map.Count -and !$AllowEmpty) { throw ('Empty input manifest: '+$Path) }
    return ,$map
}
function Tree-Files([string]$Root,[string]$Boundary,[bool]$SkipRetired=$false) {
    Assert-Unlinked $Root $Boundary
    if (!(Test-Path -LiteralPath $Root -PathType Container)) { throw ('Missing input directory: '+$Root) }
    $pending=[Collections.Generic.Stack[string]]::new()
    $pending.Push($Root)
    while ($pending.Count) {
        foreach ($item in Get-ChildItem -LiteralPath $pending.Pop() -Force) {
            if ($SkipRetired -and (Is-Retired $item.FullName)) { continue }
            Assert-Unlinked $item.FullName $Boundary
            if ($item.PSIsContainer) { $pending.Push($item.FullName) } else { $item }
        }
    }
}
function Input-Hashes([string]$Root) {
    $map=[Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($part in $parts) {
        foreach ($file in @(Tree-Files (Join-Path $Root $part) $Root $true)) {
            $map.Add($file.FullName.Substring($Root.Length+1),(Get-FileHash -LiteralPath $file.FullName).Hash)
        }
    }
    return ,$map
}
function Main-Differences($Before,$After) {
    return @(@($Before.Keys)+@($After.Keys) | Select-Object -Unique | Where-Object {
        !$Before.ContainsKey($_) -or !$After.ContainsKey($_) -or $Before[$_] -ne $After[$_]
    })
}

Assert-Idle
foreach ($path in @($project,$main,$runRoot,$snapshot,$release,$lease,$oldLease)) { Assert-Unlinked $path $workspace }
if (Test-Path -LiteralPath $reportPath) { throw 'Preserve completed restoration evidence; do not run restoration twice.' }
if (!(Test-Path -LiteralPath $lease -PathType Leaf) -or [IO.File]::ReadAllText($lease).Trim() -ne $runRoot) { throw 'This task lease is missing or belongs to another task.' }
$borrowingPath=Join-Path $runRoot ('borrowing-'+$Revision+'.json')
Assert-Unlinked $borrowingPath $runRoot
$borrowing=Get-Content -LiteralPath $borrowingPath -Raw | ConvertFrom-Json
if ($borrowing.Revision -ne $Revision -or $borrowing.ReferenceBackupRevision -notmatch '^[0-9]{2}$' -or $borrowing.Lease -ne $lease) { throw 'Invalid borrowing record.' }
$backupRevision=[string]$borrowing.ReferenceBackupRevision
if ($ReferenceBackupRevision -and $ReferenceBackupRevision -ne $backupRevision) { throw 'Explicit backup revision differs from the recorded initial baseline.' }
$backups=Join-Path $runRoot ('PreviousValidationInputs'+$backupRevision)
$referenceFrozen=Join-Path $runRoot ('FrozenSource'+$backupRevision+'/UnityClient')
foreach ($path in @($backups,$referenceFrozen)) { Assert-Unlinked $path $runRoot }
$seal=Get-Content -LiteralPath (Join-Path $release 'release-manifest.json') -Raw | ConvertFrom-Json
$cleanup=Get-Content -LiteralPath (Join-Path $snapshot 'cleanup.json') -Raw | ConvertFrom-Json
$snapshotRecord=Get-Content -LiteralPath (Join-Path $snapshot 'snapshot.json') -Raw | ConvertFrom-Json
if ($seal.Result -ne 'PASS' -or $seal.Evidence -ne $snapshot -or $cleanup.Result -ne 'PASS' -or !$cleanup.MainProjectUnchanged -or $snapshotRecord.Result -ne 'PASS' -or $snapshotRecord.Project -ne $project) { throw 'Seal and scoped build-cache cleanup must complete successfully first.' }
$null=Assert-Within $seal.Executable $release
$null=Assert-Within $seal.Package.Zip $release
if (!(Test-Path -LiteralPath $seal.Executable -PathType Leaf) -or !(Test-Path -LiteralPath $seal.Package.Zip -PathType Leaf)) { throw 'The sealed deliverables must be retained.' }
foreach ($pair in @(@('source-manifest.csv','SourceManifestSHA256'),@('project-input-manifest.csv','ProjectInputManifestSHA256'))) {
    $hash=(Get-FileHash -LiteralPath (Join-Path $snapshot $pair[0])).Hash
    if ($hash -ne $snapshotRecord.($pair[1]) -or $hash -ne $seal.($pair[1])) { throw 'Sealed snapshot manifest changed.' }
}
$original=Read-Manifest (Join-Path $runRoot ('previous-validation-full-inputs-'+$backupRevision+'.csv'))
$changed=Read-Manifest (Join-Path $runRoot ('previous-validation-inputs-'+$backupRevision+'.csv')) $true
$prepared=Read-Manifest (Join-Path $snapshot 'project-input-manifest.csv')
function Original-Bytes([string]$Relative) {
    if (!$original.ContainsKey($Relative)) { throw ('No original record: '+$Relative) }
    $candidate=if($changed.ContainsKey($Relative)){Join-Path $backups $Relative}else{Join-Path $referenceFrozen $Relative}
    Assert-Unlinked $candidate $runRoot
    if (!(Test-Path -LiteralPath $candidate -PathType Leaf) -or (Get-FileHash -LiteralPath $candidate).Hash -ne $original[$Relative].SHA256) { throw ('Original bytes unavailable; preserve the borrowing session: '+$Relative) }
    return $candidate
}
# Every baseline byte is available either in the first differential backup or
# its immutable frozen source. This remains valid across source-fix revisions.
foreach ($relative in $original.Keys) { $null=Original-Bytes $relative }
foreach ($relative in $changed.Keys) {
    if (!$original.ContainsKey($relative) -or $changed[$relative].SHA256 -ne $original[$relative].SHA256) { throw ('Backup manifest differs from original: '+$relative) }
}
$backupFiles=if(Test-Path -LiteralPath $backups){@(Tree-Files $backups $runRoot)}else{@()}
foreach ($file in $backupFiles) {
    if (!$changed.ContainsKey($file.FullName.Substring($backups.Length+1))) { throw ('Unarchived original backup: '+$file.FullName) }
}
$current=Input-Hashes $project
$operations=[Collections.Generic.List[object]]::new()
foreach ($relative in $current.Keys) {
    $hash=$current[$relative]
    if ($original.ContainsKey($relative)) {
        if ($hash -eq $original[$relative].SHA256) { continue }
        if (!$prepared.ContainsKey($relative) -or $hash -ne $prepared[$relative].SHA256) { throw ('Unknown modification; preserve current input: '+$relative) }
        $operations.Add([pscustomobject]@{Path=$relative;Action='Restore';BeforeSHA256=$hash;AfterSHA256=$original[$relative].SHA256;Backup=(Original-Bytes $relative)})
    } else {
        if (!$prepared.ContainsKey($relative) -or $hash -ne $prepared[$relative].SHA256) { throw ('Unknown addition; preserve current input: '+$relative) }
        $operations.Add([pscustomobject]@{Path=$relative;Action='RemoveTaskAddition';BeforeSHA256=$hash;AfterSHA256='ABSENT';Backup=$null})
    }
}
foreach ($relative in $original.Keys) {
    if ($current.ContainsKey($relative)) { continue }
    if ($prepared.ContainsKey($relative)) { throw ('Unexpected missing snapshot input: '+$relative) }
    $operations.Add([pscustomobject]@{Path=$relative;Action='Restore';BeforeSHA256='ABSENT';AfterSHA256=$original[$relative].SHA256;Backup=(Original-Bytes $relative)})
}
# Quarantine files must still match the actual pre-snapshot inputs of each retry.
$quarantines=[Collections.Generic.List[object]]::new()
$frozenSources=[Collections.Generic.List[string]]::new()
foreach ($recordFile in Get-ChildItem -LiteralPath $runRoot -File -Filter 'borrowing-??.json') {
    Assert-Unlinked $recordFile.FullName $runRoot
    $record=Get-Content -LiteralPath $recordFile.FullName -Raw | ConvertFrom-Json
    if ($record.ReferenceBackupRevision -ne $backupRevision -or $record.Revision -notmatch '^[0-9]{2}$' -or $record.Lease -ne $lease) { continue }
    $archiveSource=Join-Path $runRoot ('FrozenSource'+$record.Revision+'/UnityClient')
    Assert-Unlinked $archiveSource $runRoot
    if (Test-Path -LiteralPath $archiveSource -PathType Container) { $frozenSources.Add($archiveSource) }
    $directory=Join-Path $runRoot ('Snapshot'+$record.Revision+'/ExcludedValidationFiles')
    if (!(Test-Path -LiteralPath $directory)) { continue }
    Assert-Unlinked $directory $runRoot
    $prior=Read-Manifest (Join-Path $runRoot ('pre-snapshot-inputs-'+$record.Revision+'.csv'))
    $files=@(Tree-Files $directory $runRoot)
    foreach ($file in $files) {
        $relative=$file.FullName.Substring($directory.Length+1)
        if (!$prior.ContainsKey($relative) -or (Get-FileHash -LiteralPath $file.FullName).Hash -ne $prior[$relative].SHA256) { throw ('Unknown or changed quarantined input: '+$file.FullName) }
    }
    $quarantines.Add([pscustomobject]@{Directory=$directory;Files=$files;Manifest=$prior})
}
$mainBefore=Input-Hashes $main
$mainBefore.GetEnumerator() | Sort-Object Key | ForEach-Object {[pscustomobject]@{Path=$_.Key;SHA256=$_.Value}} | Export-Csv -LiteralPath (Join-Path $snapshot 'restore-main-before.csv') -NoTypeInformation -Encoding utf8
$oldLeaseHash=if(Test-Path -LiteralPath $oldLease){(Get-FileHash -LiteralPath $oldLease).Hash}else{$null}
$freed=0L
foreach ($operation in $operations) {
    Assert-Idle
    $target=Join-Path $project $operation.Path
    Assert-Unlinked $target $project
    $before=if(Test-Path -LiteralPath $target -PathType Leaf){(Get-FileHash -LiteralPath $target).Hash}else{'ABSENT'}
    if ($before -ne $operation.BeforeSHA256) { throw ('Input changed after preflight: '+$operation.Path) }
    if ($operation.Action -eq 'Restore') {
        Assert-Unlinked $operation.Backup $runRoot
        if ((Get-FileHash -LiteralPath $operation.Backup).Hash -ne $operation.AfterSHA256) { throw 'Original bytes changed after preflight.' }
        [IO.Directory]::CreateDirectory((Split-Path $target -Parent)) | Out-Null
        Copy-Item -LiteralPath $operation.Backup -Destination $target -Force
        if ((Get-FileHash -LiteralPath $target).Hash -ne $operation.AfterSHA256) { throw ('Restored hash mismatch: '+$operation.Path) }
    } else {
        $freed+=(Get-Item -LiteralPath $target).Length
        Remove-Item -LiteralPath $target -Force
        if (Test-Path -LiteralPath $target) { throw ('Task addition remains: '+$operation.Path) }
    }
}
$restored=Input-Hashes $project
if ($restored.Count -ne $original.Count) { throw 'Restored input count differs from the initial borrowed baseline.' }
foreach ($relative in $original.Keys) {
    if (!$restored.ContainsKey($relative) -or $restored[$relative] -ne $original[$relative].SHA256) { throw ('Final original input verification failed: '+$relative) }
}
$operations | Export-Csv -LiteralPath (Join-Path $snapshot 'borrowed-input-restoration.csv') -NoTypeInformation -Encoding utf8
$removedDirectories=[Collections.Generic.List[string]]::new()
$retainedDirectories=[Collections.Generic.List[object]]::new()
if (Test-Path -LiteralPath $backups) {
    Assert-Idle
    Assert-Unlinked $backups $runRoot
    $files=@(Tree-Files $backups $runRoot)
    foreach ($file in $files) {
        $relative=$file.FullName.Substring($backups.Length+1)
        if (!$original.ContainsKey($relative) -or (Get-FileHash -LiteralPath $file.FullName).Hash -ne $original[$relative].SHA256 -or (Get-FileHash -LiteralPath (Join-Path $project $relative)).Hash -ne $original[$relative].SHA256) { throw ('Backup still has unique bytes: '+$file.FullName) }
    }
    $freed+=($files|Measure-Object Length -Sum).Sum
    Assert-Idle
    Remove-Item -LiteralPath $backups -Recurse -Force
    if (Test-Path -LiteralPath $backups) { throw 'Redundant initial backup remains.' }
    $removedDirectories.Add($backups)
}
foreach ($quarantine in $quarantines) {
    Assert-Idle
    Assert-Unlinked $quarantine.Directory $runRoot
    $files=@(Tree-Files $quarantine.Directory $runRoot)
    $safe=$true
    foreach ($file in $files) {
        $relative=$file.FullName.Substring($quarantine.Directory.Length+1)
        $hash=(Get-FileHash -LiteralPath $file.FullName).Hash
        if (!$quarantine.Manifest.ContainsKey($relative) -or $hash -ne $quarantine.Manifest[$relative].SHA256) { throw 'Quarantine changed after preflight.' }
        $duplicate=$original.ContainsKey($relative) -and (Get-FileHash -LiteralPath (Join-Path $project $relative)).Hash -eq $hash
        if (!$duplicate) {
            foreach ($archiveSource in $frozenSources) {
                $candidate=Join-Path $archiveSource $relative
                Assert-Unlinked $candidate $runRoot
                if ((Test-Path -LiteralPath $candidate -PathType Leaf) -and (Get-FileHash -LiteralPath $candidate).Hash -eq $hash) { $duplicate=$true; break }
            }
        }
        if (!$duplicate) { $safe=$false }
    }
    if (!$safe) {
        $retainedDirectories.Add([pscustomobject]@{Path=$quarantine.Directory;Reason='Contains bytes without an identical restored input or frozen source copy; retain as diagnostic evidence.'})
        continue
    }
    $freed+=($files|Measure-Object Length -Sum).Sum
    Assert-Idle
    Remove-Item -LiteralPath $quarantine.Directory -Recurse -Force
    if (Test-Path -LiteralPath $quarantine.Directory) { throw 'Redundant quarantine remains.' }
    $removedDirectories.Add($quarantine.Directory)
}
$mainChanges=@(Main-Differences $mainBefore (Input-Hashes $main))
if ($mainChanges.Count) {
    $mainChanges | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $snapshot 'restore-concurrent-main-changes.json') -Encoding utf8
    throw 'Main inputs changed during restoration; retain the task lease and investigate.'
}
if ($null -ne $oldLeaseHash -and (!(Test-Path -LiteralPath $oldLease) -or (Get-FileHash -LiteralPath $oldLease).Hash -ne $oldLeaseHash)) { throw 'The unrelated old task lock changed.' }
Assert-Idle
Assert-Unlinked $lease $project
if ([IO.File]::ReadAllText($lease).Trim() -ne $runRoot) { throw 'Task lease changed; do not remove it.' }
$freed+=(Get-Item -LiteralPath $lease).Length
Remove-Item -LiteralPath $lease -Force
if (Test-Path -LiteralPath $lease) { throw 'Task lease was not removed.' }
[pscustomobject]@{
    Result='PASS';RecordedUtc=[DateTime]::UtcNow.ToString('o');Project=$project;ReferenceBackupRevision=$backupRevision;
    OriginalInputs=$original.Count;RestoredInputs=$restored.Count;
    RestoredFiles=@($operations|Where-Object Action -eq 'Restore').Count;
    RemovedTaskFiles=@($operations|Where-Object Action -eq 'RemoveTaskAddition').Count;
    MainInputCount=$mainBefore.Count;MainProjectUnchanged=$true;MainDifferencesDuringRestoration=0;
    FreedBytes=$freed;RemovedDuplicateDirectories=@($removedDirectories);RetainedQuarantines=@($retainedDirectories);TaskLeaseRemoved=$true;
    OldTutorialLeasePreserved=($null -ne $oldLeaseHash);Retained=@('Frozen source for all tested revisions','Manifests, logs and verification evidence','Windows player and verified ZIP','Shared warm Library','Retired directory left untouched')
} | ConvertTo-Json -Depth 5 | Tee-Object -FilePath $reportPath
