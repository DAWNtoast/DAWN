param(
    [ValidatePattern('^[0-9]{2}$')][string]$Revision = '01'
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$project = Join-Path $workspace 'tmp\shipyard-detail-clean-20261011'
$main = Join-Path $workspace 'UnityClient'
$runRoot = Join-Path $workspace 'Artifacts\UiRelease20261104'
$snapshot = Join-Path $runRoot ('Snapshot' + $Revision)
$release = Join-Path $workspace ('Builds\第七港口-界面优化20261104-r' + $Revision)
$backups = Join-Path $runRoot ('PreviousValidationInputs' + $Revision)
$quarantine = Join-Path $snapshot 'ExcludedValidationFiles'
$lease = Join-Path $project 'UiRelease20261104.lock'
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
function Read-Manifest([string]$Path) {
    Assert-Unlinked $Path $runRoot
    $map=[Collections.Generic.Dictionary[string,object]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in @(Import-Csv -LiteralPath $Path)) {
        $relative=Relative-Path $entry.Path
        if ($entry.SHA256 -notmatch '^[A-Fa-f0-9]{64}$' -or [long]$entry.Bytes -lt 0 -or $map.ContainsKey($relative)) {
            throw ('Invalid/duplicate manifest entry: '+$relative)
        }
        $map.Add($relative,$entry)
    }
    if (!$map.Count) { throw ('Empty input manifest: '+$Path) }
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
if (!(Test-Path -LiteralPath $lease -PathType Leaf) -or [IO.File]::ReadAllText($lease).Trim() -ne $runRoot) {
    throw 'This task lease is missing or belongs to another task.'
}
$seal=Get-Content -LiteralPath (Join-Path $release 'release-manifest.json') -Raw | ConvertFrom-Json
$cleanup=Get-Content -LiteralPath (Join-Path $snapshot 'cleanup.json') -Raw | ConvertFrom-Json
$snapshotRecord=Get-Content -LiteralPath (Join-Path $snapshot 'snapshot.json') -Raw | ConvertFrom-Json
if ($seal.Result -ne 'PASS' -or $seal.Evidence -ne $snapshot -or $cleanup.Result -ne 'PASS' -or !$cleanup.MainProjectUnchanged -or
    $snapshotRecord.Result -ne 'PASS' -or $snapshotRecord.Project -ne $project) { throw 'Seal and scoped build-cache cleanup must complete successfully first.' }
$null=Assert-Within $seal.Executable $release
$null=Assert-Within $seal.Package.Zip $release
if (!(Test-Path -LiteralPath $seal.Executable -PathType Leaf) -or !(Test-Path -LiteralPath $seal.Package.Zip -PathType Leaf)) { throw 'The sealed deliverables must be retained.' }
foreach ($pair in @(@('source-manifest.csv','SourceManifestSHA256'),@('project-input-manifest.csv','ProjectInputManifestSHA256'))) {
    $hash=(Get-FileHash -LiteralPath (Join-Path $snapshot $pair[0])).Hash
    if ($hash -ne $snapshotRecord.($pair[1]) -or $hash -ne $seal.($pair[1])) { throw 'Sealed snapshot manifest changed.' }
}
$original=Read-Manifest (Join-Path $runRoot ('previous-validation-full-inputs-'+$Revision+'.csv'))
$changed=Read-Manifest (Join-Path $runRoot ('previous-validation-inputs-'+$Revision+'.csv'))
$prepared=Read-Manifest (Join-Path $snapshot 'project-input-manifest.csv')
$excluded=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($value in @(Get-Content -LiteralPath (Join-Path $snapshot 'excluded-validation-files.json') -Raw | ConvertFrom-Json)) {
    $relative=Relative-Path $value
    if (!$original.ContainsKey($relative) -or !$excluded.Add($relative)) { throw ('Excluded file has no unique original record: '+$relative) }
}

# Preflight all backup bytes and all current inputs before making any mutation.
foreach ($relative in $changed.Keys) {
    if (!$original.ContainsKey($relative) -or $changed[$relative].SHA256 -ne $original[$relative].SHA256) { throw ('Backup manifest differs from original inputs: '+$relative) }
    $backup=Join-Path $backups $relative
    Assert-Unlinked $backup $backups
    if (!(Test-Path -LiteralPath $backup -PathType Leaf) -or (Get-FileHash -LiteralPath $backup).Hash -ne $original[$relative].SHA256) { throw ('Missing or changed restore backup: '+$relative) }
}
$backupFiles=@(Tree-Files $backups $runRoot)
foreach ($file in $backupFiles) {
    $relative=$file.FullName.Substring($backups.Length+1)
    if (!$changed.ContainsKey($relative)) { throw ('Unarchived file in restore backups: '+$file.FullName) }
}
$excludedHashes=[Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
$quarantineFiles=if(Test-Path -LiteralPath $quarantine){@(Tree-Files $quarantine $snapshot)}else{@()}
foreach ($file in $quarantineFiles) {
    $relative=$file.FullName.Substring($quarantine.Length+1)
    if (!$excluded.Contains($relative) -or !$original.ContainsKey($relative)) { throw ('Unknown quarantined input: '+$relative) }
    $hash=(Get-FileHash -LiteralPath $file.FullName).Hash
    if ($hash -ne $original[$relative].SHA256) { throw ('Quarantined bytes differ from original: '+$relative) }
    $excludedHashes.Add($relative,$hash)
}
if ($excludedHashes.Count -ne $excluded.Count) { throw 'Quarantine evidence is incomplete.' }
$current=Input-Hashes $project
$operations=[Collections.Generic.List[object]]::new()
foreach ($relative in $current.Keys) {
    $hash=$current[$relative]
    if ($original.ContainsKey($relative)) {
        if ($hash -eq $original[$relative].SHA256) { continue }
        if (!$prepared.ContainsKey($relative) -or $hash -ne $prepared[$relative].SHA256) { throw ('Unknown modification; preserve current input: '+$relative) }
        if (!$changed.ContainsKey($relative)) { throw ('Original bytes are unavailable: '+$relative) }
        $operations.Add([pscustomobject]@{Path=$relative;Action='Restore';BeforeSHA256=$hash;AfterSHA256=$original[$relative].SHA256;Backup=(Join-Path $backups $relative)})
    } else {
        $known=($prepared.ContainsKey($relative) -and $hash -eq $prepared[$relative].SHA256) -or ($excludedHashes.ContainsKey($relative) -and $hash -eq $excludedHashes[$relative])
        if (!$known) { throw ('Unknown addition; preserve current input: '+$relative) }
        $operations.Add([pscustomobject]@{Path=$relative;Action='RemoveTaskAddition';BeforeSHA256=$hash;AfterSHA256='ABSENT';Backup=$null})
    }
}
foreach ($relative in $original.Keys) {
    if ($current.ContainsKey($relative)) { continue }
    if ($prepared.ContainsKey($relative)) { throw ('Unexpected missing snapshot input: '+$relative) }
    if (!$excluded.Contains($relative) -or !$changed.ContainsKey($relative)) { throw ('Missing original input lacks quarantine and restore evidence: '+$relative) }
    $operations.Add([pscustomobject]@{Path=$relative;Action='Restore';BeforeSHA256='ABSENT';AfterSHA256=$original[$relative].SHA256;Backup=(Join-Path $backups $relative)})
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
        Assert-Unlinked $operation.Backup $backups
        if ((Get-FileHash -LiteralPath $operation.Backup).Hash -ne $operation.AfterSHA256) { throw 'Restore backup changed after preflight.' }
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
if ($restored.Count -ne $original.Count) { throw 'Restored input count differs from the original manifest.' }
foreach ($relative in $original.Keys) {
    if (!$restored.ContainsKey($relative) -or $restored[$relative] -ne $original[$relative].SHA256) { throw ('Final original input verification failed: '+$relative) }
}
$operations | Export-Csv -LiteralPath (Join-Path $snapshot 'borrowed-input-restoration.csv') -NoTypeInformation -Encoding utf8

# Every redundant backup must still equal the now-restored original before removal.
foreach ($directory in @($backups,$quarantine)) {
    if (!(Test-Path -LiteralPath $directory)) { continue }
    Assert-Idle
    Assert-Unlinked $directory $runRoot
    $files=@(Tree-Files $directory $runRoot)
    foreach ($file in $files) {
        $relative=$file.FullName.Substring($directory.Length+1)
        if (!$original.ContainsKey($relative) -or (Get-FileHash -LiteralPath $file.FullName).Hash -ne $original[$relative].SHA256 -or
            (Get-FileHash -LiteralPath (Join-Path $project $relative)).Hash -ne $original[$relative].SHA256) { throw ('Backup still has unique bytes; preserve it: '+$file.FullName) }
    }
    $freed+=($files | Measure-Object Length -Sum).Sum
    Assert-Idle
    Remove-Item -LiteralPath $directory -Recurse -Force
    if (Test-Path -LiteralPath $directory) { throw ('Redundant backup directory remains: '+$directory) }
}
$mainAfter=Input-Hashes $main
$mainChanges=@(Main-Differences $mainBefore $mainAfter)
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
    Result='PASS';RecordedUtc=[DateTime]::UtcNow.ToString('o');Project=$project;
    OriginalInputs=$original.Count;RestoredInputs=$restored.Count;
    RestoredFiles=@($operations|Where-Object Action -eq 'Restore').Count;
    RemovedTaskFiles=@($operations|Where-Object Action -eq 'RemoveTaskAddition').Count;
    MainInputCount=$mainBefore.Count;MainProjectUnchanged=$true;MainDifferencesDuringRestoration=0;
    FreedBytes=$freed;RemovedDuplicateDirectories=@($backups,$quarantine);TaskLeaseRemoved=$true;
    OldTutorialLeasePreserved=($null -ne $oldLeaseHash);Retained=@('Frozen release source','Manifests, logs and verification evidence','Windows player and verified ZIP','Shared warm Library','Retired directory left untouched')
} | ConvertTo-Json -Depth 4 | Tee-Object -FilePath $reportPath
