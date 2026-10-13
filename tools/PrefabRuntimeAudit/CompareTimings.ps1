param([Parameter(Mandatory=$true)][string]$TrialsRoot)
$ErrorActionPreference = 'Stop'
function Timing-Median($Values) {
    $a=@($Values|Sort-Object); if($a.Count-eq0){return $null};$i=[int][Math]::Floor($a.Count/2)
    if($a.Count%2){return [double]$a[$i]}; return ([double]$a[$i-1]+[double]$a[$i])/2
}
$runs=@{}
foreach($variant in @('Baseline','Optimized')) { foreach($trial in 1..3) {
    $folder=Join-Path $TrialsRoot ($variant+'-'+$trial)
    $summary=Get-Content -LiteralPath (Join-Path $folder 'timing-summary.json') -Raw|ConvertFrom-Json
    if($summary.Result-ne'PASS' -or $summary.VisualCaptureRun -or $summary.AnalysisVersion-ne3 -or $summary.ProbeRevision-ne'v2'){throw 'Only valid noncapture v2 probe trials with analysis version 3 may enter comparison.'}
    if($summary.RawReportSHA256-ne(Get-FileHash -LiteralPath (Join-Path $folder 'memory.json')).Hash){throw 'Raw report changed after timing analysis.'}
    $runs[$variant+'-'+$trial]=@(Import-Csv -LiteralPath (Join-Path $folder 'timings.csv'))
} }
$reference=$runs['Baseline-1']
foreach($key in $runs.Keys) {
    if($runs[$key].Count-ne$reference.Count){throw ('Action count differs: '+$key)}
    for($i=0;$i-lt$reference.Count;$i++) {
        if($runs[$key][$i].Id-ne$reference[$i].Id -or $runs[$key][$i].Stage-ne$reference[$i].Stage -or $runs[$key][$i].Action-ne$reference[$i].Action -or $runs[$key][$i].Category-ne$reference[$i].Category -or $runs[$key][$i].Occurrence-ne$reference[$i].Occurrence){throw ('Action order/classification differs: '+$key+' '+$i)}
    }
}
$groups=@($reference|Group-Object Category,Action)
$comparisons=@(foreach($group in $groups) { foreach($metric in @('CallbackMs','BindAndLayoutMs','TargetSceneLoadedMs','FirstRenderedMs','ReadyRenderedMs')) {
    $groupIds=@($group.Group.Id)
    $ids=@(foreach($id in $groupIds) {
        $complete=$true
        foreach($key in $runs.Keys) {
            $candidate=$runs[$key]|Where-Object Id -eq $id
            if($null-eq$candidate -or $candidate.$metric-eq'' -or [double]$candidate.$metric-lt0){$complete=$false;break}
        }
        if($complete){$id}
    })
    if($ids.Count-eq0){continue}
    $values=@{}
    foreach($variant in @('Baseline','Optimized')) {
        $values[$variant]=@(foreach($trial in 1..3) {
            $samples=@($runs[$variant+'-'+$trial]|Where-Object { $_.Id-in$ids -and $_.$metric-ne'' }|ForEach-Object { [double]$_.$metric })
            if($samples.Count-eq0){$null}else{Timing-Median $samples}
        })
    }
    if(@($values.Baseline|Where-Object {$null-ne$_}).Count-ne3 -or @($values.Optimized|Where-Object {$null-ne$_}).Count-ne3){continue}
    $before=Timing-Median $values.Baseline;$after=Timing-Median $values.Optimized
    [pscustomobject]@{Category=$group.Group[0].Category;Action=$group.Group[0].Action;Metric=$metric;ActionsPerTrial=$ids.Count;ExcludedUnpairedActions=$groupIds.Count-$ids.Count;
        BaselineMedianMs=$before;OptimizedMedianMs=$after;ReductionMs=$before-$after;ReductionPercent=if($before-gt0){100*($before-$after)/$before}else{$null};
        BaselineTrialMedians=$values.Baseline;OptimizedTrialMedians=$values.Optimized}
} })
$comparisons|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $TrialsRoot 'timing-comparison.json') -Encoding utf8
$comparisons|Select-Object Category,Action,Metric,ActionsPerTrial,ExcludedUnpairedActions,BaselineMedianMs,OptimizedMedianMs,ReductionMs,ReductionPercent|Export-Csv -LiteralPath (Join-Path $TrialsRoot 'timing-comparison.csv') -NoTypeInformation -Encoding utf8
$comparisons|Where-Object {$_.Metric-eq'CallbackMs' -and $_.Category-in@('MenuModalFirst','MenuModalWarm','PortModalFirst','PortModalWarm','FirstPort','Startup')}|Select-Object Category,Action,BaselineMedianMs,OptimizedMedianMs|Format-Table -AutoSize
