param([string]$TrialsRoot = 'D:\Game\Artifacts\PrefabRuntimeOptimization20261014\Trials')
$ErrorActionPreference = 'Stop'
function Trial-Median($Values) { $sorted=@($Values|Sort-Object); if($sorted.Count-ne 3){throw 'Exactly three valid repetitions required.'}; return [double]$sorted[1] }
$all = @{}
$hardware = $null
$probeHash = $null
$summaryRows = @(foreach ($variant in @('Baseline','Optimized')) {
    for ($round=1; $round -le 3; $round++) {
        $directory=Join-Path $TrialsRoot ($variant+'-'+$round)
        $summary=Get-Content -LiteralPath (Join-Path $directory 'summary.json') -Raw|ConvertFrom-Json
        if($summary.Result-ne'PASS'){throw 'A trial failed.'}
        $raw=Get-Content -LiteralPath (Join-Path $directory 'memory.json') -Raw|ConvertFrom-Json
        if($raw.visualCaptureRun){throw 'Screenshot readback runs cannot enter memory/timing performance comparison.'}
        if($raw.probeRevision-ne'v2'){throw 'Only the target-scene-verified v2 probe may enter final comparison.'}
        $hardwareKey=@($summary.Unity,$summary.GPU,$summary.GraphicsApi,$raw.width,$raw.height)-join'|'
        if($null-eq$hardware){$hardware=$hardwareKey}elseif($hardware-ne$hardwareKey){throw 'Runtime/graphics/device/output dimensions differ between trials.'}
        $launch=Get-Content -LiteralPath (Join-Path $directory 'launch.json') -Raw|ConvertFrom-Json
        $probe=@($launch.Assemblies|Where-Object Name -eq 'ShatteredSail.PrefabRuntimeAudit.dll')
        if($probe.Count-ne1){throw 'Missing unique probe assembly identity.'}
        if($null-eq$probeHash){$probeHash=$probe[0].SHA256}elseif($probeHash-ne$probe[0].SHA256){throw 'Actual measured probe DLLs differ between trials.'}
        $all[$variant+'-'+$round]=@(Import-Csv -LiteralPath (Join-Path $directory 'checkpoints.csv'))
        [pscustomobject]@{Variant=$variant;Round=$round;PeakPrivateMiB=$summary.GameplayStepPeakPrivateMiB;PeakWorkingSetMiB=$summary.GameplayStepPeakWorkingSetMiB;MenuPrivateDeltaMiB=$summary.MenuPrivateDeltaMiB;MenuWorkingSetDeltaMiB=$summary.MenuWorkingSetDeltaMiB;RenderedFrames=$summary.RenderedFrames;LastMenuNavigationTextureInstances=$summary.LastMenuNavigationTextureInstances;LastMenuNavigationTextureNames=$summary.LastMenuNavigationTextureNames}
    }
})
foreach($run in $all.Keys) {
    if(($all[$run].Checkpoint-join'|')-ne($all['Baseline-1'].Checkpoint-join'|')){throw ('Checkpoint order/content differs: '+$run)}
}
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
& (Join-Path $PSScriptRoot 'CompareTimings.ps1') -TrialsRoot $TrialsRoot
