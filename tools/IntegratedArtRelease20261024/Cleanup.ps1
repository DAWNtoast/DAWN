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
$processes=@(Get-CimInstance Win32_Process | Where-Object { $_.ProcessId -ne $PID -and $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($project.Replace('/','\')) })
if ($processes.Count) { throw 'Validation project remains referenced by a running process; preserve its cache.' }
foreach($node in @((Get-Item -LiteralPath $project),(Get-Item -LiteralPath (Split-Path -Parent $project)))) {
 if($node.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Cleanup path traverses a link.' }
}
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
$temp=[IO.Path]::GetFullPath((Join-Path $project 'Temp'))
if ($temp -ne (Join-Path $expectedProject 'Temp') -or -not $temp.StartsWith($project+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected cleanup target.' }
if (Test-Path -LiteralPath $temp) {
 $entries=@(Get-Item -LiteralPath $temp)+@(Get-ChildItem -LiteralPath $temp -Force -Recurse)
 if (@($entries | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw 'Temp contains a link; preserve it for inspection.' }
 $freed=[long](($entries | Where-Object { -not $_.PSIsContainer } | Measure-Object Length -Sum).Sum)
 Remove-Item -LiteralPath $temp -Recurse -Force
 if(Test-Path -LiteralPath $temp) { throw 'Temporary directory was not removed.' }
 $targets+= $temp
}
$unchanged=(MainHashes) -eq $before
if(-not $unchanged) { throw 'Main project changed during cleanup; investigate before delivery.' }
[pscustomobject]@{
 Result='PASS'; Removed=$targets; FreedBytes=$freed; MainProjectUnchanged=$unchanged;
 Retained=@([pscustomobject]@{Path=(Join-Path $project 'Library');Reason='Shared warm cache used by tools/ReleaseBuild/DesktopRelease.ps1 and ongoing release/diagnostic workflows; no new full project cache created.'});
 Evidence='Frozen source, regression logs, screenshots, EXE and verified ZIP retained.'
} | ConvertTo-Json -Depth 5 | Tee-Object -FilePath (Join-Path $evidence 'cleanup.json')