$ErrorActionPreference='Stop'
$freezeManifestPath='D:/Game/Artifacts/MemoryRelease20261015/Validation/RegressionReady02-Snapshot/source-manifest.json'
$freezeRoot='D:/Game/Artifacts/MemoryRelease20261015/FrozenSource'
if(Test-Path -LiteralPath $freezeRoot){throw 'Frozen source already exists; preserve it.'}
$freezeManifest=Get-Content -LiteralPath $freezeManifestPath -Raw|ConvertFrom-Json
$freezeSources=[Collections.Generic.List[object]]::new()
foreach($entry in $freezeManifest){
 $selected=$null
 foreach($root in @('D:/Game/tmp/runtime-memory-release-20261014','D:/Game/UnityClient')){
  $candidate=Join-Path $root $entry.Path
  if((Test-Path -LiteralPath $candidate) -and (Get-FileHash -LiteralPath $candidate).Hash -eq $entry.SHA256){$selected=$candidate;break}
 }
 if($null -eq $selected){throw ('No exact frozen source for '+$entry.Path)}
 $freezeSources.Add([pscustomobject]@{Entry=$entry;Source=$selected})
}
[IO.Directory]::CreateDirectory($freezeRoot)|Out-Null
foreach($selected in $freezeSources){
 $destination=Join-Path $freezeRoot $selected.Entry.Path
 [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))|Out-Null
 Copy-Item -LiteralPath $selected.Source -Destination $destination
 if((Get-FileHash -LiteralPath $destination).Hash -ne $selected.Entry.SHA256){throw ('Source changed during freeze: '+$selected.Entry.Path)}
}
Copy-Item -LiteralPath $freezeManifestPath -Destination (Join-Path $freezeRoot 'source-manifest.json')
[pscustomobject]@{Result='PASS';Files=$freezeManifest.Count;SnapshotManifestSHA256=(Get-FileHash -LiteralPath $freezeManifestPath).Hash;FrozenUtc=[DateTime]::UtcNow.ToString('o');Policy='User selected locked validation version; later workspace edits excluded.'}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $freezeRoot 'freeze-result.json') -Encoding utf8
Get-Content -LiteralPath (Join-Path $freezeRoot 'freeze-result.json')
