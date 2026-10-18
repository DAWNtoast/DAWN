param([ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Name='FinalCandidate')
$ErrorActionPreference='Stop'
$sealProject='D:/Game/tmp/runtime-memory-release-20261014'
$sealEvidence=Join-Path 'D:/Game/Artifacts/MemoryRelease20261015' $Name
$sealOriginal=Get-Content 'D:/Game/Artifacts/MemoryRelease20261015/FrozenSource/source-manifest.json' -Raw|ConvertFrom-Json
if(Test-Path -LiteralPath $sealEvidence){throw 'Final candidate already sealed.'}
$sealRepairs=@{}
foreach($repair in (Get-Content 'D:/Game/Artifacts/MemoryRelease20261015/FrozenUiValidation/changes-sha256.json' -Raw|ConvertFrom-Json)){$sealRepairs[$repair.Path.Replace('/','\')]=$repair.AfterSHA256}
foreach($repair in (Get-Content 'D:/Game/Artifacts/MemoryRelease20261015/Validation/CardContractsStatic/ChangedFilesSHA256.json' -Raw|ConvertFrom-Json)){$sealRepairs['Assets\_Project\Tests\EditMode\'+$repair.File]=$repair.NewSHA256}
foreach($repairGroup in @('FrozenPlayBugRepairs','FrozenPlayUiRepairs','FrozenPlayTradeRepairs','FrozenSmokeRepairs')){
 $repairManifest=Join-Path ('D:/Game/Artifacts/MemoryRelease20261015/'+$repairGroup) 'changes-sha256.json'
 if(Test-Path -LiteralPath $repairManifest){foreach($repair in (Get-Content -LiteralPath $repairManifest -Raw|ConvertFrom-Json)){$sealRepairs[$repair.Path.Replace('/','\')]=$repair.AfterSHA256}}
}
$sealImported=@('ProjectSettings\ProjectSettings.asset','Assets\_Project\Resources\PortFacilities\DepartureCharts4K\chart-golden-trade.png.meta','Assets\_Project\Resources\PortFacilities\DepartureCharts4K\chart-moon-ruins.png.meta','Assets\_Project\Resources\PortFacilities\DepartureCharts4K\chart-shattered-reef.png.meta','Assets\_Project\Resources\UISurfaces\journal_parchment_v1.png.meta','Assets\_Project\Resources\UISurfaces\nautical_canvas_v1.png.meta')
$sealRows=@(foreach($entry in $sealOriginal){
 $path=Join-Path $sealProject $entry.Path
 if(-not(Test-Path -LiteralPath $path -PathType Leaf)){throw ('Frozen file missing: '+$entry.Path)}
 $hash=(Get-FileHash -LiteralPath $path).Hash
 if($sealRepairs.ContainsKey($entry.Path)){if($hash -ne $sealRepairs[$entry.Path]){throw ('Repair changed after review: '+$entry.Path)}}
 elseif($entry.Path -notin $sealImported -and $hash -ne $entry.SHA256){throw ('Unreviewed frozen-source change: '+$entry.Path)}
 [pscustomobject]@{Path=$entry.Path;Length=(Get-Item -LiteralPath $path).Length;SHA256=$hash;ChangedFromFreeze=($hash -ne $entry.SHA256)}
})
$sealExpected=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($entry in $sealRows){[void]$sealExpected.Add($entry.Path)}
foreach($part in @('Assets','Packages','ProjectSettings')){
 foreach($file in (Get-ChildItem -LiteralPath (Join-Path $sealProject $part) -File -Recurse)){
  $relative=$file.FullName.Substring($sealProject.Length+1)
  if($relative -eq 'Assets\_Project\Scenes\UIPreviewsForMe.meta'){continue}
  if(-not $sealExpected.Contains($relative)){throw ('Unexpected final source file: '+$relative)}
 }
}
[IO.Directory]::CreateDirectory($sealEvidence)|Out-Null
foreach($entry in $sealRows|Where-Object ChangedFromFreeze){
 $destination=Join-Path (Join-Path $sealEvidence 'Overrides') $entry.Path
 [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))|Out-Null
 Copy-Item -LiteralPath (Join-Path $sealProject $entry.Path) -Destination $destination
}
$sealRows|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $sealEvidence 'source-manifest.json') -Encoding utf8
[pscustomobject]@{Result='PASS';Version='0.3.20261015.1';Files=$sealRows.Count;ReviewedCodeRepairs=$sealRepairs.Count;SourceManifestSHA256=(Get-FileHash -LiteralPath (Join-Path $sealEvidence 'source-manifest.json')).Hash;Reconstruction=('FrozenSource plus '+$Name+'/Overrides');LaterWorkspaceChangesIncluded=$false}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $sealEvidence 'seal-result.json') -Encoding utf8
Get-Content -LiteralPath (Join-Path $sealEvidence 'seal-result.json')
