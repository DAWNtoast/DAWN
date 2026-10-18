param(
  [string]$Baseline='D:/Game/Artifacts/MemoryRelease20261015/Baseline-Measure01',
  [string]$Optimized='D:/Game/Artifacts/MemoryRelease20261015/Optimized-Measure01',
  [string]$Output='D:/Game/Artifacts/MemoryRelease20261015/PairComparison01.json'
)
$ErrorActionPreference='Stop'
$memoryRoot=[IO.Path]::GetFullPath('D:/Game/Artifacts/MemoryRelease20261015')+'\'
foreach($memoryPath in @($Baseline,$Optimized,$Output)){if(-not [IO.Path]::GetFullPath($memoryPath).StartsWith($memoryRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'This audit root only.'}}
if(Test-Path -LiteralPath $Output){throw 'Preserve previous comparison.'}
$memoryBefore=Get-Content -LiteralPath (Join-Path $Baseline 'summary.json') -Raw|ConvertFrom-Json
$memoryAfter=Get-Content -LiteralPath (Join-Path $Optimized 'summary.json') -Raw|ConvertFrom-Json
if($memoryBefore.Result -ne 'PASS' -or $memoryAfter.Result -ne 'PASS'){throw 'Both measurements must pass.'}
$memoryBeforeLaunch=Get-Content -LiteralPath (Join-Path $Baseline 'launch.json') -Raw|ConvertFrom-Json
$memoryAfterLaunch=Get-Content -LiteralPath (Join-Path $Optimized 'launch.json') -Raw|ConvertFrom-Json
$memoryBeforeProbe=@($memoryBeforeLaunch.Assemblies|Where-Object Name -eq 'ShatteredSail.RuntimeMemoryAudit.dll')
$memoryAfterProbe=@($memoryAfterLaunch.Assemblies|Where-Object Name -eq 'ShatteredSail.RuntimeMemoryAudit.dll')
if($memoryBeforeProbe.Count -ne 1 -or $memoryAfterProbe.Count -ne 1 -or $memoryBeforeProbe[0].SHA256 -ne $memoryAfterProbe[0].SHA256){throw 'Comparable measurements require the identical diagnostic probe DLL.'}
if($memoryBefore.GPU -ne $memoryAfter.GPU -or $memoryBefore.GraphicsApi -ne $memoryAfter.GraphicsApi -or $memoryBefore.Unity -ne $memoryAfter.Unity){throw 'Changed measurement graphics/runtime environment.'}
$memoryBeforeRows=@(Import-Csv -LiteralPath (Join-Path $Baseline 'checkpoints.csv'))
$memoryAfterRows=@(Import-Csv -LiteralPath (Join-Path $Optimized 'checkpoints.csv'))
$memoryRows=@(foreach($memoryBeforeRow in $memoryBeforeRows){
  $memoryMatches=@($memoryAfterRows|Where-Object Checkpoint -eq $memoryBeforeRow.Checkpoint)
  if($memoryMatches.Count -ne 1){throw 'Checkpoint mismatch.'}
  $memoryAfterRow=$memoryMatches[0]
  foreach($memoryMetric in @('StablePrivateMiB','StableWorkingSetMiB','ManagedUsedMiB','UnityAllocatedMiB','StepPeakPrivateMiB','StepPeakWorkingSetMiB','GameObjects','Graphics','Textures','Sprites','Materials','AudioClips','Fonts')){
    if($memoryBeforeRow.Transient -eq 'True' -and $memoryMetric.StartsWith('Stable')){continue}
    $memoryA=[double]$memoryBeforeRow.$memoryMetric; $memoryB=[double]$memoryAfterRow.$memoryMetric
    [pscustomobject]@{Checkpoint=$memoryBeforeRow.Checkpoint;Metric=$memoryMetric;Baseline=$memoryA;Optimized=$memoryB;Reduction=$memoryA-$memoryB;ReductionPercent=if($memoryA -gt 0){100*($memoryA-$memoryB)/$memoryA}else{$null}}
  }
})
[pscustomobject]@{Method='One fresh Player before and after; directional evidence, not a repeated-trial performance estimate. Same diagnostic DLL, runtime, GPU and graphics API. Controlled offscreen rendering includes probe and RT overhead. Private Bytes and Working Set are separate metrics.';Baseline=$Baseline;Optimized=$Optimized;ProbeSHA256=$memoryBeforeProbe[0].SHA256;Checkpoints=57;BaselinePeakPrivateMiB=$memoryBefore.GameplayStepPeakPrivateMiB;OptimizedPeakPrivateMiB=$memoryAfter.GameplayStepPeakPrivateMiB;BaselinePeakWorkingSetMiB=$memoryBefore.GameplayStepPeakWorkingSetMiB;OptimizedPeakWorkingSetMiB=$memoryAfter.GameplayStepPeakWorkingSetMiB;Rows=$memoryRows}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $Output -Encoding utf8
$memoryRows|Where-Object {$_.Checkpoint -in @('01_MainMenu_Fresh','03_Port_Fresh','13_MainMenu_Returned','15_SceneCycle_Menu_3') -and $_.Metric -in @('StablePrivateMiB','StableWorkingSetMiB','UnityAllocatedMiB')}|Format-Table -AutoSize
