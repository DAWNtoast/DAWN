param(
 [Parameter(Mandatory=$true)][string]$SnapshotDirectory,
 [Parameter(Mandatory=$true)][string]$ReleaseDirectory,
 [Parameter(Mandatory=$true)][string]$ProjectPath
)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$expectedProject=Join-Path $workspace 'tmp/shipyard-detail-clean-20261011'
$project=[IO.Path]::GetFullPath($ProjectPath).TrimEnd('\','/')
if ($project -ne $expectedProject) { throw 'Cleanup is restricted to the reused validation project.' }
$release=[IO.Path]::GetFullPath($ReleaseDirectory).TrimEnd('\','/')
$evidence=[IO.Path]::GetFullPath($SnapshotDirectory).TrimEnd('\','/')
if (-not $release.StartsWith((Join-Path $workspace 'Builds')+'\',[StringComparison]::OrdinalIgnoreCase) -or
    -not $evidence.StartsWith((Join-Path $workspace 'Artifacts')+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid release/evidence paths.' }
$seal=Get-Content -LiteralPath (Join-Path $release 'release-manifest.json') -Raw | ConvertFrom-Json
if ($seal.Result -ne 'PASS' -or $seal.Evidence -ne $evidence -or
    -not (Test-Path -LiteralPath $seal.Executable) -or -not (Test-Path -LiteralPath $seal.Package.Zip)) { throw 'Save and seal the verified deliverable before cleanup.' }
function Assert-ProjectIdle {
 $processes=@(Get-CimInstance Win32_Process | Where-Object { $_.ProcessId -ne $PID -and $_.CommandLine -and $_.CommandLine.Replace('/','\').IndexOf($project,[StringComparison]::OrdinalIgnoreCase) -ge 0 })
 if ($processes.Count) { throw 'Validation project remains referenced by a running process; preserve its cache.' }
}
function Assert-UnlinkedDirectory([string]$Path,[string]$Boundary) {
 $full=[IO.Path]::GetFullPath($Path).TrimEnd('\','/')
 $limit=[IO.Path]::GetFullPath($Boundary).TrimEnd('\','/')
 if ($full -ne $limit -and -not $full.StartsWith($limit+'\',[StringComparison]::OrdinalIgnoreCase)) { throw ('Path escaped its permitted boundary: '+$full) }
 # Check every ancestor, including the volume root, before walking children.
 $nodePath=$full
 while ($nodePath) {
  $node=Get-Item -LiteralPath $nodePath -Force
  if (-not $node.PSIsContainer -or ($node.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw ('Directory path traverses a link or file: '+$nodePath) }
  $parent=[IO.Directory]::GetParent($nodePath)
  $nodePath=if($parent){$parent.FullName}else{$null}
 }
}
function Assert-UnlinkedFile([string]$Path) {
 Assert-UnlinkedDirectory (Split-Path -Parent $Path) $project
 $item=Get-Item -LiteralPath $Path -Force
 if ($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw ('Invalid build evidence file: '+$Path) }
}
function Save-BuildEvidence([string]$Path) {
 Assert-UnlinkedFile $Path
 $archive=Join-Path $evidence 'CleanupEvidence'
 Assert-UnlinkedDirectory $evidence $workspace
 if (-not (Test-Path -LiteralPath $archive)) { [IO.Directory]::CreateDirectory($archive)|Out-Null }
 Assert-UnlinkedDirectory $archive $evidence
 $destination=Join-Path $archive (Split-Path -Leaf $Path)
 $sourceHash=(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
 if (Test-Path -LiteralPath $destination) {
  $existing=Get-Item -LiteralPath $destination -Force
  if ($existing.PSIsContainer -or ($existing.Attributes -band [IO.FileAttributes]::ReparsePoint) -or (Get-FileHash -LiteralPath $destination).Hash -ne $sourceHash) { throw ('Existing cleanup evidence differs; preserve cache: '+$destination) }
 } else { Copy-Item -LiteralPath $Path -Destination $destination }
 if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $sourceHash) { throw 'Cleanup evidence copy failed verification.' }
 return [pscustomobject]@{Path=$destination;SHA256=$sourceHash}
}
function Remove-VerifiedCache([string]$Path) {
 Assert-ProjectIdle
 Assert-UnlinkedDirectory $Path $project
 # Enumerate one directory at a time and reject links before descending.
 $directories=[Collections.Generic.Stack[string]]::new()
 $directories.Push($Path)
 $bytes=0L
 while ($directories.Count) {
  foreach ($item in Get-ChildItem -LiteralPath $directories.Pop() -Force) {
   if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw ('Cache contains a link; preserve it: '+$item.FullName) }
   if ($item.PSIsContainer) { $directories.Push($item.FullName) } else { $bytes+=$item.Length }
  }
 }
 Assert-ProjectIdle
 Remove-Item -LiteralPath $Path -Recurse -Force
 if (Test-Path -LiteralPath $Path) { throw ('Cache was not removed: '+$Path) }
 return [pscustomobject]@{Path=$Path;Bytes=$bytes}
}
Assert-ProjectIdle
Assert-UnlinkedDirectory $project $workspace
Assert-UnlinkedDirectory $evidence $workspace
Assert-UnlinkedDirectory $release $workspace
function MainHashes {
 $root=Join-Path $workspace 'UnityClient'
 $lines=foreach($part in @('Assets','Packages','ProjectSettings')) {
  foreach($file in Get-ChildItem -LiteralPath (Join-Path $root $part) -File -Recurse -Force) {
   $file.FullName+'|'+(Get-FileHash -LiteralPath $file.FullName).Hash
  }
 }
 return ($lines -join [Environment]::NewLine)
}
$before=MainHashes
$targets=@()
$freed=0L
$removedDetails=@()
$retained=@()
$buildEvidence=@()
$playerTargets=@()
# Only the current sealed release's Player build may authorize Player cache removal.
# Missing/invalid evidence preserves that cache; it never broadens the whitelist.
$bee=Join-Path $project 'Library/Bee'
try {
 $expectedExecutable=[IO.Path]::GetFullPath([string]$seal.Executable)
 if (-not $expectedExecutable.StartsWith($release+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Sealed executable is outside this release.' }
 Assert-UnlinkedDirectory $bee $project
 $windowsLog=Join-Path $evidence 'Windows/unity.log'
 Assert-UnlinkedDirectory (Split-Path -Parent $windowsLog) $evidence
 $logItem=Get-Item -LiteralPath $windowsLog -Force
 if ($logItem.PSIsContainer -or ($logItem.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Windows build log is not a regular file.' }
 $logText=([IO.File]::ReadAllText($windowsLog)).Replace('\','/')
 $matched=$null
 foreach ($inputFile in Get-ChildItem -LiteralPath $bee -File -Filter 'Player*-inputdata.json' | Sort-Object LastWriteTimeUtc,Name -Descending) {
  Assert-UnlinkedFile $inputFile.FullName
  $inputData=Get-Content -LiteralPath $inputFile.FullName -Raw | ConvertFrom-Json
  $config=$inputData.'PlayerBuildProgramLibrary.Data.PlayerBuildConfig'
  $destination=[string]$config.DestinationPath
  if ($destination -and [IO.Path]::IsPathRooted($destination) -and [IO.Path]::GetFullPath($destination) -eq $expectedExecutable) {
   $matched=[pscustomobject]@{File=$inputFile;Config=$config}; break
  }
 }
 if (-not $matched) { throw 'No Player inputdata DestinationPath matches this sealed release executable.' }
 if (-not $logText.Contains('Library/Bee/'+$matched.File.Name)) { throw 'The current Windows build log does not identify the matching Player inputdata.' }
 $dataFolder=([string]$matched.Config.DataFolder).Replace('\','/')
 if ($dataFolder -notmatch '^Library/PlayerDataCache/(Win64[A-Za-z0-9_-]*)/Data$') { throw ('Player DataFolder is outside the exact Windows cache whitelist: '+$dataFolder) }
 $playerData=[IO.Path]::GetFullPath((Join-Path $project ('Library/PlayerDataCache/'+$Matches[1])))
 Assert-UnlinkedDirectory $playerData (Join-Path $project 'Library/PlayerDataCache')
 $buildEvidence+=Save-BuildEvidence $matched.File.FullName
 $playerTargets+=$playerData

 # The Player DAG itself proves the WinPlayerBuildProgram output directory.
 try {
  $dagName=$matched.File.Name.Replace('-inputdata.json','.dag.json')
  $dagPath=Join-Path $bee $dagName
  Assert-UnlinkedFile $dagPath
  $dagText=([IO.File]::ReadAllText($dagPath)).Replace('\','/')
  if (-not $logText.Contains('Library/Bee/'+$dagName) -or -not $dagText.Contains('Library/Bee/artifacts/WinPlayerBuildProgram/')) { throw 'Current Player DAG output evidence is incomplete.' }
  $buildEvidence+=Save-BuildEvidence $dagPath
  $playerTargets+=Join-Path $bee 'artifacts/WinPlayerBuildProgram'
 } catch { $retained+=[pscustomobject]@{Path=(Join-Path $bee 'artifacts/WinPlayerBuildProgram');Reason=$_.Exception.Message} }

 # P compilation records are distinct from E editor records. Never clean E/Bee as a whole.
 try {
  $compileMatch=$null
  foreach ($compileFile in Get-ChildItem -LiteralPath $bee -File -Filter '*P-inputdata.json' | Sort-Object LastWriteTimeUtc,Name -Descending) {
   if ($compileFile.Name -notmatch '^[0-9a-f]+P-inputdata\.json$') { continue }
   Assert-UnlinkedFile $compileFile.FullName
   $compileData=(Get-Content -LiteralPath $compileFile.FullName -Raw | ConvertFrom-Json).'ScriptCompilationBuildProgram.Data.ScriptCompilationData'
   if ($compileData.BuildTarget -eq 'StandaloneWindows64' -and ([string]$compileData.OutputDirectory).Replace('\','/') -eq 'Library/Bee/PlayerScriptAssemblies' -and
       ([string]$compileData.BuildPlayerDataOutput).Replace('\','/') -eq 'Library/BuildPlayerData/Player' -and $logText.Contains('Library/Bee/'+$compileFile.Name)) {
    $compileMatch=$compileFile; break
   }
  }
  if (-not $compileMatch) { throw 'Current Windows log and P inputdata do not jointly identify the Player compilation outputs.' }
  $compileDagName=$compileMatch.Name.Replace('-inputdata.json','.dag.json')
  $compileDagPath=Join-Path $bee $compileDagName
  Assert-UnlinkedFile $compileDagPath
  $artifactName=$compileMatch.Name.Replace('-inputdata.json','.dag')
  $compileDagText=([IO.File]::ReadAllText($compileDagPath)).Replace('\','/')
  if (-not $logText.Contains('Library/Bee/'+$compileDagName) -or -not $compileDagText.Contains('Library/Bee/artifacts/'+$artifactName+'/')) { throw 'Current P DAG does not prove its artifact directory.' }
  $buildEvidence+=Save-BuildEvidence $compileMatch.FullName
  $buildEvidence+=Save-BuildEvidence $compileDagPath
  $playerTargets+=@((Join-Path $bee 'PlayerScriptAssemblies'),(Join-Path $project 'Library/BuildPlayerData/Player'),(Join-Path $bee ('artifacts/'+$artifactName)))
 } catch { $retained+=[pscustomobject]@{Path='Library/Bee/PlayerScriptAssemblies; Library/BuildPlayerData/Player; Library/Bee/artifacts/*P.dag';Reason=$_.Exception.Message} }
} catch { $retained+=[pscustomobject]@{Path=(Join-Path $project 'Library player build caches');Reason=$_.Exception.Message} }

foreach ($playerTarget in $playerTargets | Select-Object -Unique) {
 try {
  if (Test-Path -LiteralPath $playerTarget) {
   $removed=Remove-VerifiedCache $playerTarget
   $freed+=$removed.Bytes; $targets+=$removed.Path; $removedDetails+=$removed
  }
 } catch { $retained+=[pscustomobject]@{Path=$playerTarget;Reason=$_.Exception.Message} }
}
$temp=[IO.Path]::GetFullPath((Join-Path $project 'Temp'))
if ($temp -ne (Join-Path $expectedProject 'Temp') -or -not $temp.StartsWith($project+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected cleanup target.' }
if (Test-Path -LiteralPath $temp) {
 $removed=Remove-VerifiedCache $temp
 $freed+=$removed.Bytes; $targets+=$removed.Path; $removedDetails+=$removed
}
$unchanged=(MainHashes) -eq $before
if(-not $unchanged) { throw 'Main project changed during cleanup; investigate before delivery.' }
[pscustomobject]@{
 Result='PASS'; Removed=$targets; RemovedDetails=$removedDetails; FreedBytes=$freed; MainProjectUnchanged=$unchanged; PlayerBuildEvidence=$buildEvidence;
 Retained=@($retained)+@([pscustomobject]@{Path=(Join-Path $project 'Library');Reason='Remaining editor import/package caches, ScriptAssemblies, E DAG artifacts and Editor TypeDB are retained as the shared warm validation cache used by tools/ReleaseBuild/DesktopRelease.ps1, tools/ReleaseBuild/RunTests.ps1 and tools/GameplayAuditRelease20261028/Release.ps1. No whole Bee or Library directory is cleaned.'});
 Evidence='Frozen source, regression logs, screenshots, EXE and verified ZIP retained.'
} | ConvertTo-Json -Depth 5 | Tee-Object -FilePath (Join-Path $evidence 'cleanup.json')
