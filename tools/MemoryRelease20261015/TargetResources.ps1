param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$memoryDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if(-not $memoryDirectory.StartsWith('D:\Game\Artifacts\MemoryRelease20261015\',[StringComparison]::OrdinalIgnoreCase)){throw 'This audit output only.'}
$memoryReport=Get-Content -LiteralPath (Join-Path $memoryDirectory 'memory.json') -Raw|ConvertFrom-Json
if($memoryReport.status -ne 'passed'){throw 'Complete the measurement first.'}
$memoryRows=@(foreach($memoryPoint in $memoryReport.checkpoints){
  $memoryDetails=Get-Content -LiteralPath $memoryPoint.detailsFile -Raw|ConvertFrom-Json
  $memoryRam=@($memoryDetails.textures|Where-Object {$_.name -match '^ram_(?:player|boss)_.*_page\d+$'})
  $memoryAmbient=@($memoryDetails.audio|Where-Object {$_.name -like '*_ambience'})
  [pscustomobject]@{Checkpoint=$memoryPoint.name;RamTextures=$memoryRam.Count;RamTextureNames=($memoryRam.name -join ';');RamKnownBytes=($memoryRam|Where-Object {$_.bytes -gt 0}|Measure-Object bytes -Sum).Sum;RamByteUnavailable=@($memoryRam|Where-Object {$_.bytes -le 0}).Count;EnvironmentClips=$memoryAmbient.Count;EnvironmentLoadedClips=@($memoryAmbient|Where-Object loadState -eq 'Loaded').Count;EnvironmentNames=($memoryAmbient.name -join ';');AllAudioClips=@($memoryDetails.audio).Count}
})
$memoryRows|Export-Csv -LiteralPath (Join-Path $memoryDirectory 'target-resources.csv') -NoTypeInformation -Encoding utf8
$memoryRows|Where-Object {$_.Checkpoint -match '^10_|^11_|^12_|^13_|^15_'}|Select-Object Checkpoint,RamTextures,EnvironmentClips,EnvironmentLoadedClips,AllAudioClips|Format-Table -AutoSize
