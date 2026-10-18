param([ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Name='Baseline')
$ErrorActionPreference='Stop'
$memorySource=[IO.Path]::GetFullPath('D:/Game/UnityClient').TrimEnd('\','/')
$memoryProject=[IO.Path]::GetFullPath('D:/Game/tmp/runtime-memory-probe-20261014').TrimEnd('\','/')
$memoryEvidence=[IO.Path]::GetFullPath((Join-Path 'D:/Game/Artifacts/MemoryRelease20261015' ($Name+'-Snapshot')))
if(Test-Path -LiteralPath $memoryEvidence){throw 'Snapshot evidence already exists; preserve it.'}
if(-not(Test-Path -LiteralPath (Join-Path $memoryProject '.runtime-memory-probe-owned.json'))){throw 'Expected owned warm diagnostic project.'}
if(@(Get-CimInstance Win32_Process | Where-Object {$_.Name -eq 'Unity.exe' -or $_.Name -eq 'MemoryAudit.exe'}).Count){throw 'Unity or diagnostic Player is already running.'}
$memoryLock=Join-Path $memoryProject 'Temp/UnityLockfile'
if(Test-Path -LiteralPath $memoryLock){try{$memoryCheck=[IO.File]::Open($memoryLock,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None);$memoryCheck.Dispose()}catch{throw 'Diagnostic project lock is occupied.'}}
New-Item -ItemType Directory -Path $memoryEvidence | Out-Null
$memoryFiles=@(foreach($memoryPart in @('Assets','Packages','ProjectSettings')){Get-ChildItem -LiteralPath (Join-Path $memorySource $memoryPart) -File -Recurse | Where-Object {$_.FullName -notmatch '[\\/]UIPreviewsForMe(?:[\\/.]|$)'}})
$memorySourceManifest=@(foreach($memoryFile in $memoryFiles){[pscustomobject]@{Path=$memoryFile.FullName.Substring($memorySource.Length+1);Length=$memoryFile.Length;SHA256=(Get-FileHash -LiteralPath $memoryFile.FullName).Hash}})
$memorySourceManifest|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $memoryEvidence 'source-manifest.json') -Encoding utf8
$memorySourcePaths=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($memoryFile in $memorySourceManifest){[void]$memorySourcePaths.Add($memoryFile.Path)}
$memoryRemoved=[Collections.Generic.List[string]]::new()
foreach($memoryPart in @('Assets','Packages','ProjectSettings')){
  $memoryDestination=Join-Path $memoryProject $memoryPart
  foreach($memoryOld in @(Get-ChildItem -LiteralPath $memoryDestination -File -Recurse)){
    $memoryRelative=$memoryOld.FullName.Substring($memoryProject.Length+1)
    if($memoryRelative -match '[\\/]UIPreviewsForMe(?:[\\/.]|$)'){continue}
    if($memoryRelative -match '^Assets[\\/]RuntimeMemoryAudit(?:[\\/.]|$)'){continue}
    if($memorySourcePaths.Contains($memoryRelative)){continue}
    $memoryTarget=[IO.Path]::GetFullPath($memoryOld.FullName)
    if(-not $memoryTarget.StartsWith($memoryProject+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Stale target escaped diagnostic project.'}
    Remove-Item -LiteralPath $memoryTarget -Force
    $memoryRemoved.Add($memoryRelative)
  }
  $memoryCopyArgs=@((Join-Path $memorySource $memoryPart),$memoryDestination,'/E','/R:1','/W:1','/NFL','/NDL','/NP','/NJH','/NJS')
  if($memoryPart -eq 'Assets'){$memoryCopyArgs+=@('/XD',(Join-Path $memorySource 'Assets/_Project/Scenes/UIPreviewsForMe'),'/XF','UIPreviewsForMe.meta')}
  & robocopy @memoryCopyArgs | Out-Null
  if($LASTEXITCODE -ge 8){throw ('Source copy failed: '+$memoryPart)}
}
$memoryRemoved | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $memoryEvidence 'removed-stale-files.json') -Encoding utf8
$memoryMismatches=@(foreach($memoryFile in $memorySourceManifest){$memoryTarget=Join-Path $memoryProject $memoryFile.Path;if(-not(Test-Path -LiteralPath $memoryTarget) -or (Get-FileHash -LiteralPath $memoryTarget).Hash -ne $memoryFile.SHA256){$memoryFile.Path}})
if($memoryMismatches.Count){$memoryMismatches|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $memoryEvidence 'mismatches.json') -Encoding utf8;throw 'Snapshot changed during copying or differs from source manifest.'}
[pscustomobject]@{Result='PASS';Source=$memorySource;Project=$memoryProject;Files=$memorySourceManifest.Count;Bytes=($memorySourceManifest|Measure-Object Length -Sum).Sum;ForMeCopied=0;CreatedUtc=[DateTime]::UtcNow.ToString('o');SourceManifestSHA256=(Get-FileHash -LiteralPath (Join-Path $memoryEvidence 'source-manifest.json')).Hash}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $memoryEvidence 'snapshot-result.json') -Encoding utf8
Get-Content -LiteralPath (Join-Path $memoryEvidence 'snapshot-result.json')
