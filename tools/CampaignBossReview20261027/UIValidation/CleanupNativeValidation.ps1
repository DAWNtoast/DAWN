$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..')).TrimEnd('\','/')
$project=[IO.Path]::GetFullPath((Join-Path $workspace 'tmp/shipyard-detail-clean-20261011')).TrimEnd('\','/')
$evidence=Join-Path $workspace 'outputs/campaign-boss-review-20261027/UIValidation'
$archive=Join-Path $evidence 'CleanupEvidence'
$resultPath=Join-Path $evidence 'CheckpointFinal02/summary.json'
if (-not (Test-Path -LiteralPath $resultPath)) { throw 'Preserve the completed Unity result before cleanup.' }
if((Get-Content -LiteralPath $resultPath -Raw|ConvertFrom-Json).Result -ne 'Passed'){throw 'Preserve failed validation inputs for diagnosis.'}
if((Get-Content -LiteralPath (Join-Path $evidence 'CheckpointFinal02/completion.json') -Raw|ConvertFrom-Json).InputValidationResult -ne 'PASS'){throw 'Input seal validation is required before cleanup.'}
function Assert-Idle {
 $busy=@(Get-CimInstance Win32_Process | Where-Object { $_.ProcessId -ne $PID -and $_.CommandLine -and $_.CommandLine.Replace('/','\').IndexOf($project,[StringComparison]::OrdinalIgnoreCase) -ge 0 })
 if($busy.Count){throw 'The shared validation checkout is still referenced by a process.'}
}
function Assert-Safe([string]$Path) {
 $full=[IO.Path]::GetFullPath($Path).TrimEnd('\','/')
 if(-not $full.StartsWith($project+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Cleanup target escaped the shared checkout.'}
 $cursor=$full
 while($cursor){
  $item=Get-Item -LiteralPath $cursor -Force
  if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw ('Linked cleanup target: '+$cursor)}
  $parent=[IO.Directory]::GetParent($cursor)
  $cursor=if($parent){$parent.FullName}else{$null}
 }
 $stack=[Collections.Generic.Stack[string]]::new()
 if((Get-Item -LiteralPath $full).PSIsContainer){$stack.Push($full)}
 while($stack.Count){
  foreach($entry in Get-ChildItem -LiteralPath $stack.Pop() -Force){
   if($entry.Attributes -band [IO.FileAttributes]::ReparsePoint){throw ('Linked descendant: '+$entry.FullName)}
   if($entry.PSIsContainer){$stack.Push($entry.FullName)}
  }
 }
}
function MainHashes {
 $source=Join-Path $workspace 'UnityClient'
 $rows=foreach($part in @('Assets','Packages','ProjectSettings')){
  foreach($file in Get-ChildItem -LiteralPath (Join-Path $source $part) -Recurse -File -Force){$file.FullName+'|'+(Get-FileHash -LiteralPath $file.FullName).Hash}
 }
 return ($rows -join "`n")
}
Assert-Idle
if(Test-Path -LiteralPath $archive){throw 'Do not overwrite cleanup evidence.'}
[IO.Directory]::CreateDirectory($archive)|Out-Null
$before=MainHashes
$removed=@()
$freed=0L
$inputCopiesRemoved=0L
$tests=Join-Path $project 'Assets/_Project/Tests/PlayMode'
foreach($name in @('CampaignBossCheckpointUiTests.cs','CampaignBossCheckpointUiTests.cs.meta','CampaignBossCheckpointInputs','CampaignBossCheckpointInputs.meta')){
 $target=Join-Path $tests $name
 if(-not(Test-Path -LiteralPath $target)){continue}
 Assert-Safe $target
 $destination=Join-Path $archive $name
 Copy-Item -LiteralPath $target -Destination $destination -Recurse
 $files=if((Get-Item -LiteralPath $target).PSIsContainer){@(Get-ChildItem -LiteralPath $target -Recurse -File)}else{@(Get-Item -LiteralPath $target)}
 $bytes=0L
 foreach($file in $files){
  $copy=if((Get-Item -LiteralPath $target).PSIsContainer){Join-Path $destination $file.FullName.Substring($target.Length+1)}else{$destination}
  if((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $copy).Hash){throw 'Task input archive verification failed.'}
  $bytes+=$file.Length
 }
 Assert-Idle
 Remove-Item -LiteralPath $target -Recurse -Force
 if(Test-Path -LiteralPath $target){throw 'Task fixture was not removed.'}
 $inputCopiesRemoved+=$bytes
 $removed+=[pscustomobject]@{Path=$target;Bytes=$bytes;Reason='Archived task-specific test inputs; no longer installed in shared checkout.'}
}
$settings=Join-Path $project 'ProjectSettings/ProjectSettings.asset'
Assert-Safe $settings
Copy-Item -LiteralPath $settings -Destination (Join-Path $archive 'ProjectSettings-before-identity-restore.asset')
$original=Get-Content -LiteralPath (Join-Path $evidence 'Snapshot/PreviousValidationInputs/ProjectSettings/ProjectSettings.asset') -Raw
$current=Get-Content -LiteralPath $settings -Raw
$identity=@{}
foreach($key in @('companyName','productName')){
 $match=[regex]::Match($original,'(?m)^  '+$key+': (.+)\r?$')
 if(-not $match.Success){throw 'Original shared checkout identity is unavailable.'}
 $value=$match.Groups[1].Value.TrimEnd("`r")
 $identity[$key]=$value
 $current=[regex]::Replace($current,'(?m)^  '+$key+': .*$', '  '+$key+': '+$value)
}
[IO.File]::WriteAllText($settings,$current,[Text.UTF8Encoding]::new($false))
$temp=Join-Path $project 'Temp'
if(Test-Path -LiteralPath $temp){
 Assert-Safe $temp
 $bytes=0L
 foreach($file in Get-ChildItem -LiteralPath $temp -Recurse -File -Force){$bytes+=$file.Length}
 Assert-Idle
 Remove-Item -LiteralPath $temp -Recurse -Force
 if(Test-Path -LiteralPath $temp){throw 'Task Temp remained after cleanup.'}
 $freed+=$bytes
 $removed+=[pscustomobject]@{Path=$temp;Bytes=$bytes;Reason='Idle Unity PlayMode temporary cache, regenerated as needed.'}
}
$unchanged=(MainHashes) -eq $before
if(-not $unchanged){throw 'Main production inputs changed during cleanup.'}
[pscustomobject]@{
 Result='PASS';MainProjectUnchanged=$unchanged;FreedBytes=$freed;RemovedInstalledTaskInputBytes=$inputCopiesRemoved;Removed=$removed;SharedStorageIdentityRestored=$identity;
 SpaceAccounting='Removed installed test/input copies are preserved as evidence; only deleted regenerable cache bytes count as freed. Unity removed its Temp directory on exit.';
 Retained=@([pscustomobject]@{Path=(Join-Path $project 'Library');Reason='Shared editor/package/import cache reused by tools/ReleaseBuild/DesktopRelease.ps1, tools/ReleaseBuild/RunTests.ps1 and tools/GameplayUiRelease20261026/Release.ps1. No new Library copy was created.'});
 Preserved='All frozen source/input checkpoints, screenshots, result logs and isolated QA storage; formal saves never accessed.'
}|ConvertTo-Json -Depth 6|Tee-Object -FilePath (Join-Path $evidence 'cleanup.json')
