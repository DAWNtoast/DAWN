param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[0-9]{2}$')][string]$Revision
)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$runRoot=Join-Path $workspace 'Artifacts\IntegratedRelease20261105'
$main=Join-Path $workspace 'UnityClient'
$project=Join-Path $workspace 'tmp\shipyard-detail-clean-20261011'
$lease=Join-Path $project 'IntegratedRelease20261105.lock'
$finalRoot=Join-Path $runRoot ('FrozenSource'+$Revision)
$finalSource=Join-Path $finalRoot 'UnityClient'
$finalSnapshot=Join-Path $runRoot ('Snapshot'+$Revision)
$release=Join-Path $workspace ('Builds\第七港口-统一验收20261105-r'+$Revision)
$evidence=Join-Path $runRoot ('SourceDeduplication'+$Revision)
$parts=@('Assets','Packages','ProjectSettings')
function Assert-Within([string]$Path,[string]$Boundary) {
    $full=[IO.Path]::GetFullPath($Path).TrimEnd('\','/')
    $limit=[IO.Path]::GetFullPath($Boundary).TrimEnd('\','/')
    if ($full -ne $limit -and !$full.StartsWith($limit+'\',[StringComparison]::OrdinalIgnoreCase)) { throw ('Path escaped its permitted boundary: '+$full) }
    return $full
}
function Assert-Unlinked([string]$Path,[string]$Boundary) {
    $full=Assert-Within $Path $Boundary
    for($at=$full;$at;$at=Split-Path $at -Parent) {
        if((Test-Path -LiteralPath $at) -and ((Get-Item -LiteralPath $at -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw ('Linked path is forbidden: '+$at) }
    }
}
function Tree-Files([string]$Root,[string]$Boundary,[bool]$SkipRetired=$false) {
    Assert-Unlinked $Root $Boundary
    if(!(Test-Path -LiteralPath $Root -PathType Container)){throw ('Missing directory: '+$Root)}
    $pending=[Collections.Generic.Stack[string]]::new();$pending.Push($Root)
    while($pending.Count) {
        foreach($item in Get-ChildItem -LiteralPath $pending.Pop() -Force) {
            if($SkipRetired -and $item.FullName -match '(^|[\\/])UIPreviewsForMe(?:[\\/.]|$)'){continue}
            if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw ('Directory contains a link: '+$item.FullName)}
            $null=Assert-Within $item.FullName $Boundary
            if($item.PSIsContainer){$pending.Push($item.FullName)}else{$item}
        }
    }
}
function Assert-Idle {
    if(Test-Path -LiteralPath $lease){throw 'The release task lease still exists; restore the borrowed project first.'}
    $active=@(Get-CimInstance Win32_Process|Where-Object{
        if($_.ProcessId -eq $PID -or !$_.CommandLine){return $false}
        $command=$_.CommandLine.Replace('/','\')
        return $command.IndexOf($runRoot,[StringComparison]::OrdinalIgnoreCase) -ge 0 -or
            $command.IndexOf($project,[StringComparison]::OrdinalIgnoreCase) -ge 0 -or
            $command.IndexOf($release,[StringComparison]::OrdinalIgnoreCase) -ge 0
    })
    if($active.Count){throw ('Release snapshots or shared project remain referenced by processes: '+($active.ProcessId -join ','))}
    if(Test-Path -LiteralPath (Join-Path $project 'Temp\UnityLockfile')){throw 'The shared project still has a Unity lock.'}
}
function Relative-Path([string]$Value) {
    $relative=$Value.Replace('/','\')
    if([IO.Path]::IsPathRooted($relative) -or $relative -notmatch '^(Assets|Packages|ProjectSettings)\\' -or
        $relative -match '[:*?"<>|]' -or $relative -match '(^|\\)\.\.?($|\\)' -or
        $relative -match '(^|[\\/])UIPreviewsForMe(?:[\\/.]|$)' -or @($relative.Split('\')|Where-Object{!$_ -or $_ -ne $_.TrimEnd(' ','.')}).Count){throw ('Invalid source manifest path: '+$Value)}
    return $relative
}
function Read-Snapshot([string]$Number) {
    $directory=Join-Path $runRoot ('Snapshot'+$Number)
    $manifestPath=Join-Path $directory 'source-manifest.csv'
    $recordPath=Join-Path $directory 'snapshot.json'
    foreach($path in @($directory,$manifestPath,$recordPath)){Assert-Unlinked $path $runRoot}
    $record=Get-Content -LiteralPath $recordPath -Raw|ConvertFrom-Json
    $manifestHash=(Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
    if($record.Result -ne 'PASS' -or $record.SourceManifestSHA256 -ne $manifestHash){throw ('Snapshot source manifest is unsealed or changed: '+$Number)}
    $map=[Collections.Generic.Dictionary[string,object]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($entry in @(Import-Csv -LiteralPath $manifestPath)) {
        $relative=Relative-Path $entry.Path
        if($entry.SHA256 -notmatch '^[A-Fa-f0-9]{64}$' -or [long]$entry.Bytes -lt 0 -or $map.ContainsKey($relative)){throw ('Invalid or duplicate source record: '+$relative)}
        $map.Add($relative,$entry)
    }
    if(!$map.Count){throw 'A source manifest cannot be empty.'}
    return [pscustomobject]@{Revision=$Number;Manifest=$manifestPath;ManifestSHA256=$manifestHash;Files=$map;Record=$record}
}
function Assert-Frozen([string]$Root,$Info) {
    $expectedRoot=Join-Path $runRoot ('FrozenSource'+$Info.Revision)
    if($Root -ne $expectedRoot){throw 'Frozen root differs from its exact revision path.'}
    Assert-Unlinked $Root $runRoot
    $source=Join-Path $Root 'UnityClient'
    $files=@(Tree-Files $Root $runRoot)
    if($files.Count -ne $Info.Files.Count){throw ('Unexpected file count in '+$Root)}
    foreach($file in $files) {
        if(!$file.FullName.StartsWith($source+'\',[StringComparison]::OrdinalIgnoreCase)){throw ('Unknown file outside frozen UnityClient: '+$file.FullName)}
        $relative=$file.FullName.Substring($source.Length+1)
        if(!$Info.Files.ContainsKey($relative) -or [long]$Info.Files[$relative].Bytes -ne $file.Length -or
            (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne $Info.Files[$relative].SHA256){throw ('Unknown or changed frozen source file: '+$file.FullName)}
    }
    if((Get-FileHash -LiteralPath $Info.Manifest).Hash -ne $Info.ManifestSHA256){throw 'A source manifest changed during verification.'}
}
function Main-Hashes {
    $map=[Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($part in $parts){foreach($file in @(Tree-Files (Join-Path $main $part) $main $true)){$map.Add($file.FullName.Substring($main.Length+1),(Get-FileHash -LiteralPath $file.FullName).Hash)}}
    return ,$map
}
function Assert-MainUnchanged($Before) {
    $after=Main-Hashes
    $changes=@(@($Before.Keys)+@($after.Keys)|Sort-Object -Unique|Where-Object{!$Before.ContainsKey($_) -or !$after.ContainsKey($_) -or $Before[$_] -ne $after[$_]})
    if($changes.Count){$changes|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $evidence 'main-input-drift.json') -Encoding utf8;throw 'Main inputs changed during deduplication; preserve evidence and investigate.'}
}
function Verify-Reconstruction($Plan) {
    Assert-Unlinked $Plan.Archive $runRoot
    if((Get-FileHash -LiteralPath $Plan.MappingPath).Hash -ne $Plan.MappingSHA256){throw 'The reconstruction map changed.'}
    $mapping=@(Import-Csv -LiteralPath $Plan.MappingPath)
    if($mapping.Count -ne $Plan.Info.Files.Count){throw 'The reconstruction map is incomplete.'}
    $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $expectedArchives=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    [void]$expectedArchives.Add('reconstruction-map.csv');[void]$expectedArchives.Add('receipt.json')
    foreach($entry in $mapping) {
        $relative=Relative-Path $entry.Path
        if(!$seen.Add($relative) -or !$Plan.Info.Files.ContainsKey($relative) -or $entry.SHA256 -ne $Plan.Info.Files[$relative].SHA256 -or [long]$entry.Bytes -ne [long]$Plan.Info.Files[$relative].Bytes){throw 'The reconstruction map differs from the original source manifest.'}
        if($entry.Storage -eq 'FinalSnapshot'){$expected=Join-Path $finalSource $relative}
        elseif($entry.Storage -eq 'Difference'){$expected=Join-Path (Join-Path $Plan.Archive 'UnityClient') $relative;[void]$expectedArchives.Add('UnityClient\'+$relative)}
        else{throw 'Unknown reconstruction storage type.'}
        $actual=[IO.Path]::GetFullPath((Join-Path $runRoot $entry.ReconstructionPath))
        if($actual -ne $expected){throw ('Reconstruction path drift: '+$entry.Path)}
        Assert-Unlinked $actual $runRoot
        if(!(Test-Path -LiteralPath $actual -PathType Leaf) -or (Get-Item -LiteralPath $actual).Length -ne [long]$entry.Bytes -or (Get-FileHash -LiteralPath $actual).Hash -ne $entry.SHA256){throw ('Reconstruction byte verification failed: '+$relative)}
    }
    $archiveFiles=@(Tree-Files $Plan.Archive $runRoot)
    if($archiveFiles.Count -ne $expectedArchives.Count){throw 'Unknown file in the differences archive.'}
    foreach($file in $archiveFiles){if(!$expectedArchives.Contains($file.FullName.Substring($Plan.Archive.Length+1))){throw ('Unknown differences archive input: '+$file.FullName)}}
    $receipt=Get-Content -LiteralPath (Join-Path $Plan.Archive 'receipt.json') -Raw|ConvertFrom-Json
    if($receipt.Result -ne 'VERIFIED_RECONSTRUCTABLE' -or $receipt.SourceManifestSHA256 -ne $Plan.Info.ManifestSHA256 -or $receipt.FinalSourceManifestSHA256 -ne $finalInfo.ManifestSHA256 -or $receipt.ReconstructionMapSHA256 -ne $Plan.MappingSHA256){throw 'Reconstruction receipt does not match the verified inputs.'}
}
Assert-Idle
foreach($path in @($runRoot,$main,$project,$finalRoot,$finalSnapshot,$release,$evidence)){Assert-Unlinked $path $workspace}
if(Test-Path -LiteralPath $evidence){throw 'Preserve existing deduplication evidence; do not silently rerun a partial or completed cleanup.'}
if(@(Get-ChildItem -LiteralPath $runRoot -Directory -Filter 'SupersededSourceDifferences*').Count){throw 'Existing reconstruction archives may depend on an earlier final source; preserve them and review dependencies before another deduplication.'}
$sealPath=Join-Path $release 'release-manifest.json'
$restorePath=Join-Path $finalSnapshot 'borrowed-input-restoration.json'
foreach($path in @($sealPath,$restorePath)){Assert-Unlinked $path $workspace}
$seal=Get-Content -LiteralPath $sealPath -Raw|ConvertFrom-Json
$restore=Get-Content -LiteralPath $restorePath -Raw|ConvertFrom-Json
$finalInfo=Read-Snapshot $Revision
if($seal.Result -ne 'PASS' -or $seal.Evidence -ne $finalSnapshot -or $seal.SourceManifestSHA256 -ne $finalInfo.ManifestSHA256 -or
    $restore.Result -ne 'PASS' -or !$restore.MainProjectUnchanged -or !$restore.TaskLeaseRemoved -or $restore.Project -ne $project){throw 'The final release must be sealed and the borrowed project fully restored first.'}
foreach($path in @([string]$seal.Executable,[string]$seal.Package.Zip)){Assert-Unlinked $path $release;if(!(Test-Path -LiteralPath $path -PathType Leaf)){throw 'Keep both sealed deliverables before deduplication.'}}
if($seal.Package.ZipSHA256 -notmatch '^[A-Fa-f0-9]{64}$' -or (Get-FileHash -LiteralPath $seal.Package.Zip).Hash -ne $seal.Package.ZipSHA256){throw 'The sealed package hash does not match.'}
if($seal.Package.ExeSHA256 -notmatch '^[A-Fa-f0-9]{64}$' -or (Get-FileHash -LiteralPath $seal.Executable).Hash -ne $seal.Package.ExeSHA256){throw 'The sealed executable hash does not match.'}
Assert-Frozen $finalRoot $finalInfo
$plans=[Collections.Generic.List[object]]::new()
foreach($directory in Get-ChildItem -LiteralPath $runRoot -Directory -Filter 'FrozenSource*'|Sort-Object Name) {
    if($directory.Name -notmatch '^FrozenSource([0-9]{2})$'){throw ('Unknown frozen source directory: '+$directory.FullName)}
    $number=$Matches[1]
    if([int]$number -gt [int]$Revision){throw 'A newer frozen source exists; the selected final revision may be stale.'}
    if($number -eq $Revision){continue}
    $info=Read-Snapshot $number
    Assert-Frozen $directory.FullName $info
    $archive=Join-Path $runRoot ('SupersededSourceDifferences'+$number)
    Assert-Unlinked $archive $runRoot
    if(Test-Path -LiteralPath $archive){throw ('Preserve existing differences archive: '+$archive)}
    $plans.Add([pscustomobject]@{Revision=$number;Root=$directory.FullName;Archive=$archive;Info=$info;MappingPath=(Join-Path $archive 'reconstruction-map.csv');MappingSHA256=$null;OriginalBytes=0L;DifferenceBytes=0L;SharedFiles=0;DifferenceFiles=0})
}
Assert-Idle
$mainBefore=Main-Hashes
[IO.Directory]::CreateDirectory($evidence)|Out-Null
$mainBefore.GetEnumerator()|Sort-Object Key|ForEach-Object{[pscustomobject]@{Path=$_.Key;SHA256=$_.Value}}|Export-Csv -LiteralPath (Join-Path $evidence 'main-before.csv') -NoTypeInformation -Encoding utf8
foreach($plan in $plans) {
    Assert-Idle
    [IO.Directory]::CreateDirectory($plan.Archive)|Out-Null
    $rows=[Collections.Generic.List[object]]::new()
    foreach($relative in $plan.Info.Files.Keys|Sort-Object) {
        $entry=$plan.Info.Files[$relative]
        $plan.OriginalBytes+=[long]$entry.Bytes
        if($finalInfo.Files.ContainsKey($relative) -and $finalInfo.Files[$relative].SHA256 -eq $entry.SHA256 -and [long]$finalInfo.Files[$relative].Bytes -eq [long]$entry.Bytes){$storage='FinalSnapshot';$target=Join-Path $finalSource $relative;$plan.SharedFiles++}
        else {
            $storage='Difference';$origin=Join-Path (Join-Path $plan.Root 'UnityClient') $relative
            $target=Join-Path (Join-Path $plan.Archive 'UnityClient') $relative
            Assert-Unlinked $origin $plan.Root;Assert-Unlinked $target $plan.Archive
            [IO.Directory]::CreateDirectory((Split-Path $target -Parent))|Out-Null
            Copy-Item -LiteralPath $origin -Destination $target
            $plan.DifferenceBytes+=[long]$entry.Bytes;$plan.DifferenceFiles++
        }
        if((Get-FileHash -LiteralPath $target).Hash -ne $entry.SHA256){throw ('Saved reconstruction bytes mismatch: '+$relative)}
        $rows.Add([pscustomobject]@{Path=$relative;Bytes=$entry.Bytes;SHA256=$entry.SHA256;Storage=$storage;ReconstructionPath=$target.Substring($runRoot.Length+1)})
    }
    $rows|Export-Csv -LiteralPath $plan.MappingPath -NoTypeInformation -Encoding utf8
    $plan.MappingSHA256=(Get-FileHash -LiteralPath $plan.MappingPath).Hash
    [pscustomobject]@{Result='VERIFIED_RECONSTRUCTABLE';RecordedUtc=[datetime]::UtcNow.ToString('o');Revision=$plan.Revision;FinalRevision=$Revision;ReconstructionBase=$runRoot;OriginalFrozenSource=$plan.Root;SourceManifest=$plan.Info.Manifest;SourceManifestSHA256=$plan.Info.ManifestSHA256;FinalSourceManifest=$finalInfo.Manifest;FinalSourceManifestSHA256=$finalInfo.ManifestSHA256;ReconstructionMap=$plan.MappingPath;ReconstructionMapSHA256=$plan.MappingSHA256;Files=$plan.Info.Files.Count;SharedFiles=$plan.SharedFiles;DifferenceFiles=$plan.DifferenceFiles;OriginalBytes=$plan.OriginalBytes;DifferenceBytes=$plan.DifferenceBytes}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $plan.Archive 'receipt.json') -Encoding utf8
    Verify-Reconstruction $plan
}
# All reconstruction archives must validate before deleting any original copy.
Assert-MainUnchanged $mainBefore
Assert-Frozen $finalRoot $finalInfo
$removed=[Collections.Generic.List[object]]::new()
foreach($plan in $plans) {
    Assert-Idle
    $exactTarget=Join-Path $runRoot ('FrozenSource'+$plan.Revision)
    if($plan.Root -ne $exactTarget -or $plan.Root -eq $finalRoot){throw 'Deletion target is not an explicitly verified superseded snapshot.'}
    Assert-Unlinked $plan.Root $runRoot
    Assert-Frozen $plan.Root $plan.Info
    Verify-Reconstruction $plan
    Assert-Idle
    Remove-Item -LiteralPath $plan.Root -Recurse -Force
    if(Test-Path -LiteralPath $plan.Root){throw ('Superseded snapshot was not removed: '+$plan.Root)}
    $archiveBytes=(@(Tree-Files $plan.Archive $runRoot)|Measure-Object Length -Sum).Sum
    $record=[pscustomobject]@{Revision=$plan.Revision;Removed=$plan.Root;Archive=$plan.Archive;OriginalBytes=$plan.OriginalBytes;ArchiveBytes=$archiveBytes;NetFreedBytes=($plan.OriginalBytes-$archiveBytes);ReconstructionMapSHA256=$plan.MappingSHA256}
    $removed.Add($record)
    $record|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $evidence ('removed-'+$plan.Revision+'.json')) -Encoding utf8
}
Assert-Frozen $finalRoot $finalInfo
foreach($plan in $plans){Verify-Reconstruction $plan}
Assert-MainUnchanged $mainBefore
[pscustomobject]@{Result='PASS';RecordedUtc=[datetime]::UtcNow.ToString('o');FinalRevision=$Revision;FinalFrozenSource=$finalRoot;FinalSourceManifestSHA256=$finalInfo.ManifestSHA256;Removed=@($removed);RemovedBytes=($removed|Measure-Object OriginalBytes -Sum).Sum;DifferenceArchivesBytes=($removed|Measure-Object ArchiveBytes -Sum).Sum;NetFreedBytes=($removed|Measure-Object NetFreedBytes -Sum).Sum;MainInputCount=$mainBefore.Count;MainProjectUnchanged=$true;TaskLeaseAbsent=$true;ReconstructionVerified=$true;Retained=@('Final frozen source','All snapshot manifests and test/build evidence','Per-revision differing original bytes and reconstruction maps','Sealed executable and package')}|ConvertTo-Json -Depth 6|Tee-Object -FilePath (Join-Path $evidence 'summary.json')
