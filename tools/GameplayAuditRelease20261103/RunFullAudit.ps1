param([ValidatePattern('^[0-9]{2}$')][string]$Revision='02')
$ErrorActionPreference='Stop'
$releaseWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$releaseEntry=Join-Path $PSScriptRoot 'Release.ps1'
$releaseShell=(Get-Process -Id $PID).Path
$pipelineOutput=Join-Path $releaseWorkspace ('Artifacts/GameplayAuditRelease20261103/Pipeline'+$Revision)
if(Test-Path -LiteralPath $pipelineOutput){throw 'Preserve prior pipeline evidence; choose a fresh revision.'}
[IO.Directory]::CreateDirectory($pipelineOutput)|Out-Null
$records=[Collections.Generic.List[object]]::new()
function Save-PipelineStatus([string]$State,[string]$Stage){
 [pscustomobject]@{State=$State;Stage=$Stage;Revision=$Revision;UpdatedAt=[DateTimeOffset]::Now.ToString('o');Stages=@($records)} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $pipelineOutput 'status.json') -Encoding utf8
}
function Assert-LiveInputs {
 $live=Join-Path $releaseWorkspace 'UnityClient'
 $manifest=Join-Path $releaseWorkspace ('Artifacts/GameplayAuditRelease20261103/Snapshot'+$Revision+'/source-manifest.csv')
 $expected=@(Import-Csv -LiteralPath $manifest)
 $paths=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
 foreach($file in $expected){
  $relative=$file.Path.Replace('\','/')
  [void]$paths.Add($relative)
  $target=[IO.Path]::GetFullPath((Join-Path $live $relative))
  if(!$target.StartsWith($live+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Invalid manifest path'}
  if(!(Test-Path -LiteralPath $target -PathType Leaf) -or (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $file.SHA256){throw ('Main input changed after freeze: '+$relative)}
 }
 foreach($part in @('Assets','Packages','ProjectSettings')){
  foreach($file in Get-ChildItem -LiteralPath (Join-Path $live $part) -File -Recurse -Force){
   $relative=$file.FullName.Substring($live.Length+1).Replace('\','/')
   if($relative -match '(^|/)UIPreviewsForMe(?:/|\.|$)'){continue}
   if(!$paths.Contains($relative)){throw ('New main input after freeze: '+$relative)}
  }
 }
 [pscustomobject]@{Result='PASS';CheckedAt=[DateTimeOffset]::Now.ToString('o');Files=$expected.Count;SourceManifestSHA256=(Get-FileHash -LiteralPath $manifest).Hash} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $pipelineOutput 'main-source-identity.json') -Encoding utf8
}
try {
 foreach($stage in @('Snapshot','EditMode','PlayMode','Verify','Windows','Smoke','Package','Seal')){
  Save-PipelineStatus 'RUNNING' $stage
  if($stage -eq 'Windows'){Assert-LiveInputs}
  $started=[DateTimeOffset]::Now
  Write-Output ('RELEASE_STAGE_START '+$stage+' '+$started.ToString('o'))
  & $releaseShell -NoProfile -File $releaseEntry -Stage $stage -Revision $Revision -TimeoutMinutes 90 2>&1 | Tee-Object -FilePath (Join-Path $pipelineOutput ($stage+'.log'))
  $stageExit=$LASTEXITCODE
  $records.Add([pscustomobject]@{Stage=$stage;ExitCode=$stageExit;StartedAt=$started.ToString('o');CompletedAt=[DateTimeOffset]::Now.ToString('o')})
  if($stageExit -ne 0){throw ('Stage failed: '+$stage+' exit '+$stageExit)}
  Save-PipelineStatus 'RUNNING' $stage
  Write-Output ('RELEASE_STAGE_PASS '+$stage)
 }
 Assert-LiveInputs
 Save-PipelineStatus 'PASS' 'Seal'
} catch {
 Save-PipelineStatus 'FAIL' $stage
 $_ | Out-String | Set-Content -LiteralPath (Join-Path $pipelineOutput 'failure.txt') -Encoding utf8
 throw
}
