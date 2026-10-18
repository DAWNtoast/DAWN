param([string]$TrialsRoot = 'D:\Game\Artifacts\MemoryRelease20261015\Trials')
$ErrorActionPreference = 'Stop'
function Trial-Median($Values) { $sorted=@($Values|Sort-Object); if($sorted.Count-ne 3){throw 'Exactly three valid repetitions required.'}; return [double]$sorted[1] }
$all = @{}
$summaryRows = @(foreach ($variant in @('Baseline','Optimized')) {
    for ($round=1; $round -le 3; $round++) {
        $directory=Join-Path $TrialsRoot ($variant+'-'+$round)
        $summary=Get-Content -LiteralPath (Join-Path $directory 'summary.json') -Raw|ConvertFrom-Json
        if($summary.Result-ne'PASS'){throw 'A trial failed.'}
        $all[$variant+'-'+$round]=@(Import-Csv -LiteralPath (Join-Path $directory 'checkpoints.csv'))
        [pscustomobject]@{Variant=$variant;Round=$round;PeakPrivateMiB=$summary.GameplayStepPeakPrivateMiB;PeakWorkingSetMiB=$summary.GameplayStepPeakWorkingSetMiB;MenuPrivateDeltaMiB=$summary.MenuPrivateDeltaMiB;MenuWorkingSetDeltaMiB=$summary.MenuWorkingSetDeltaMiB;RenderedFrames=$summary.RenderedFrames;LastMenuNavigationTextureInstances=$summary.LastMenuNavigationTextureInstances;LastMenuNavigationTextureNames=$summary.LastMenuNavigationTextureNames}
    }
})
$summaryRows|Export-Csv -LiteralPath (Join-Path $TrialsRoot 'trial-summary.csv') -NoTypeInformation -Encoding utf8
$comparison=@(foreach($point in $all['Baseline-1']) {
    foreach($metric in @('StablePrivateMiB','StableWorkingSetMiB','ManagedUsedMiB','UnityAllocatedMiB','StepPeakPrivateMiB','StepPeakWorkingSetMiB')) {
        if($point.Transient-eq'True' -and $metric.StartsWith('Stable')){continue}
        $before=@(1..3|ForEach-Object{[double](($all['Baseline-'+$_]|Where-Object Checkpoint -eq $point.Checkpoint).$metric)})
        $after=@(1..3|ForEach-Object{[double](($all['Optimized-'+$_]|Where-Object Checkpoint -eq $point.Checkpoint).$metric)})
        $a=Trial-Median $before; $b=Trial-Median $after
        [pscustomobject]@{Checkpoint=$point.Checkpoint;Metric=$metric;BaselineMedianMiB=$a;OptimizedMedianMiB=$b;ReductionMiB=$a-$b;ReductionPercent=if($a-gt 0){100*($a-$b)/$a}else{$null};BaselineTrials=$before;OptimizedTrials=$after}
    }
})
$comparison|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $TrialsRoot 'comparison.json') -Encoding utf8
$comparison|Select-Object Checkpoint,Metric,BaselineMedianMiB,OptimizedMedianMiB,ReductionMiB,ReductionPercent|Export-Csv -LiteralPath (Join-Path $TrialsRoot 'comparison.csv') -NoTypeInformation -Encoding utf8
$comparison|Where-Object{ $_.Checkpoint-in @('01_MainMenu_Fresh','13_MainMenu_Returned','15_SceneCycle_Menu_3') -and $_.Metric-in @('StablePrivateMiB','StableWorkingSetMiB') }|Format-Table -AutoSize
