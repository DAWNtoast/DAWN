[CmdletBinding(DefaultParameterSetName='Root')]
param(
    [Parameter(Mandatory=$true,ParameterSetName='Explicit')][string[]]$Trials,
    [Parameter(Mandatory=$true,ParameterSetName='Root')][string]$TrialsRoot,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath('D:\Game\Artifacts\RuntimeMemory20261104').TrimEnd('\')+'\'
function Assert-TaskPath([string]$Path) {
    if(!$Path.StartsWith($taskRoot,[StringComparison]::OrdinalIgnoreCase)){throw ('Outside task artifacts: '+$Path)}
    for($at=$Path;$at -and $at.Length -ge $taskRoot.TrimEnd('\').Length;$at=Split-Path $at -Parent){
        if((Test-Path -LiteralPath $at) -and ((Get-Item -LiteralPath $at -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw ('Linked path: '+$at)}
    }
}
function Median3($Values){$sorted=@($Values|ForEach-Object{[double]$_}|Sort-Object);if($sorted.Count -ne 3){throw 'Exactly three repetitions required.'};return $sorted[1]}
function As-UtcInstant([object]$Value) {
    # Preserve the timezone when ConvertFrom-Json returns DateTime instead of an ISO string.
    if($Value -is [DateTimeOffset]){return $Value.ToUniversalTime()}
    if($Value -is [DateTime]){
        if($Value.Kind -eq [DateTimeKind]::Unspecified){throw 'Timestamp lost its timezone; require an explicit UTC/local Kind.'}
        return [DateTimeOffset]::new($Value).ToUniversalTime()
    }
    return [DateTimeOffset]::Parse([string]$Value,[Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::AssumeUniversal -bor [Globalization.DateTimeStyles]::AdjustToUniversal)
}
if($PSCmdlet.ParameterSetName -eq 'Root'){$Trials=@('B1','O1','B2','O2','B3','O3'|ForEach-Object{Join-Path $TrialsRoot $_})}
if($Trials.Count -ne 6){throw 'Supply exactly six directories ordered B1,O1,B2,O2,B3,O3.'}
$labels=@('B1','O1','B2','O2','B3','O3')
$trialRecords=[Collections.Generic.List[object]]::new()
$hashes=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$previousEnd=$null
$expectedNames=@('menu_initial','art_open_first','art_live_after_collection','art_closed_first','art_reopened','art_closed_second')
for($i=0;$i -lt 6;$i++){
    $directory=[IO.Path]::GetFullPath($Trials[$i]);Assert-TaskPath $directory
    $file=Join-Path $directory 'trial-summary.json'
    if(!(Test-Path -LiteralPath $file -PathType Leaf)){throw ('Missing summary: '+$file)}
    $trial=Get-Content -LiteralPath $file -Raw|ConvertFrom-Json
    if($trial.Result -ne 'PASS' -or $trial.TimedOut -or $trial.ExitCode -ne 0 -or @($trial.ValidationErrors).Count){throw ('Invalid trial: '+$labels[$i])}
    if([string]::IsNullOrWhiteSpace($trial.ProbeDllSHA256)){throw 'Probe DLL hash was not recorded.'}
    $launchPath=Join-Path $directory 'launch.json'
    if(!(Test-Path -LiteralPath $launchPath -PathType Leaf)){throw ('Missing launch manifest: '+$labels[$i])}
    $launch=Get-Content -LiteralPath $launchPath -Raw|ConvertFrom-Json
    if($launch.ProbeDllSHA256 -ne $trial.ProbeDllSHA256 -or $launch.ExecutableSHA256 -ne $trial.ExecutableSHA256){throw ('Launch/summary hashes differ: '+$labels[$i])}
    if(!@($launch.Assemblies).Count){throw ('Production assembly manifest missing: '+$labels[$i])}
    [void]$hashes.Add($trial.ProbeDllSHA256)
    $start=As-UtcInstant $trial.StartedUtc;$end=As-UtcInstant $trial.CompletedUtc
    if($end -le $start -or ($null -ne $previousEnd -and $start -lt $previousEnd)){throw 'Trials must be sequential and alternate in actual B1/O1/B2/O2/B3/O3 time order.'}
    $previousEnd=$end
    if(@($trial.Windows).Count -ne 6){throw ('Expected six windows: '+$labels[$i])}
    foreach($name in $expectedNames){
        $window=@($trial.Windows|Where-Object Name -eq $name)
        if($window.Count -ne 1 -or $window[0].Samples -lt 4 -or $window[0].RenderedFrames -lt 30){throw ('Invalid window '+$name+' in '+$labels[$i])}
    }
    $trialRecords.Add([pscustomobject]@{Label=$labels[$i];Variant=if($i%2){'Optimized'}else{'Baseline'};Round=[int][Math]::Floor($i/2)+1;Directory=$directory;SummarySHA256=(Get-FileHash -LiteralPath $file).Hash;Assemblies=$launch.Assemblies;Summary=$trial})
}
if($hashes.Count -ne 1){throw 'All six trials must have byte-identical diagnostic probe DLLs.'}
# Per-variant executables must remain fixed across repetitions. Production assembly manifests are preserved in launch.json.
foreach($variant in @('Baseline','Optimized')){
    $variantHashes=@($trialRecords|Where-Object Variant -eq $variant|ForEach-Object{$_.Summary.ExecutableSHA256}|Select-Object -Unique)
    if($variantHashes.Count -ne 1){throw ('Executable changed between '+$variant+' repetitions.')}
    $assemblyManifests=@($trialRecords|Where-Object Variant -eq $variant|ForEach-Object{
        (@($_.Assemblies|Sort-Object Name|ForEach-Object{$_.Name+':'+$_.SHA256}) -join "`n")
    }|Select-Object -Unique)
    if($assemblyManifests.Count -ne 1){throw ('Production assemblies changed between '+$variant+' repetitions.')}
}
$comparison=[Collections.Generic.List[object]]::new()
$resourceRows=[Collections.Generic.List[object]]::new()
$unavailableGpu=[Collections.Generic.List[object]]::new()
foreach($name in $expectedNames){
    foreach($metric in @('PrivateBytesMedian','WorkingSetBytesMedian','GpuDedicatedBytesMedian','GpuSharedBytesMedian')){
        $gpuMetric=$metric.StartsWith('Gpu',[StringComparison]::Ordinal)
        if($gpuMetric){
            $missing=@($trialRecords|Where-Object {$w=$_.Summary.Windows|Where-Object Name -eq $name;[int]$w.GpuValidSamples -lt 4 -or $null -eq $w.$metric}|ForEach-Object Label)
            if($missing.Count){$unavailableGpu.Add([pscustomobject]@{Window=$name;Metric=$metric;MissingTrials=$missing;Reason='Each trial requires at least four valid Windows GPU-counter samples; missing data are not treated as zero.'});continue}
        }
        $before=@($trialRecords|Where-Object Variant -eq 'Baseline'|ForEach-Object{[double](($_.Summary.Windows|Where-Object Name -eq $name).$metric)})
        $after=@($trialRecords|Where-Object Variant -eq 'Optimized'|ForEach-Object{[double](($_.Summary.Windows|Where-Object Name -eq $name).$metric)})
        $a=Median3 $before;$b=Median3 $after
        $comparison.Add([pscustomobject]@{
            Window=$name;Metric=$metric;Domain=if($gpuMetric){'Windows process GPU-memory counter'}else{'Windows CPU process-memory counter'}
            BaselineMedianMiB=$a/1MB;OptimizedMedianMiB=$b/1MB;ReductionMiB=($a-$b)/1MB
            ReductionPercent=if($a -gt 0){100*($a-$b)/$a}else{$null}
            BaselineMinimumMiB=($before|Measure-Object -Minimum).Minimum/1MB;BaselineMaximumMiB=($before|Measure-Object -Maximum).Maximum/1MB
            OptimizedMinimumMiB=($after|Measure-Object -Minimum).Minimum/1MB;OptimizedMaximumMiB=($after|Measure-Object -Maximum).Maximum/1MB
            BaselineTrialsMiB=@($before|ForEach-Object{$_/1MB});OptimizedTrialsMiB=@($after|ForEach-Object{$_/1MB})
            RangesOverlap=(($before|Measure-Object -Minimum).Minimum -le ($after|Measure-Object -Maximum).Maximum -and ($after|Measure-Object -Minimum).Minimum -le ($before|Measure-Object -Maximum).Maximum)
        })
    }
    foreach($record in $trialRecords){
        $window=$record.Summary.Windows|Where-Object Name -eq $name
        $resourceRows.Add([pscustomobject]@{Trial=$record.Label;Window=$name;TargetTextureCount=$window.TargetTextureCount;TargetSpriteCount=$window.TargetSpriteCount;TargetTextureNames=$window.TargetTextureNames;RuntimeTextureByteApiAvailable=$window.RuntimeTextureByteApiAvailable;TargetTextureBytes=$window.TargetTextureBytes})
    }
}
$output=[IO.Path]::GetFullPath($OutputDirectory);Assert-TaskPath $output
if(Test-Path -LiteralPath $output){throw 'Preserve prior comparisons; OutputDirectory must be new.'}
New-Item -ItemType Directory -Path $output|Out-Null
$result=[pscustomobject]@{
    Result='PASS';CreatedUtc=[DateTime]::UtcNow.ToString('O');ProbeDllSHA256=@($hashes)[0];Order=$labels
    Method='For each UTC window, take each trial OS sample median, then compare the median of three baseline trials with the median of three optimized trials. Positive reduction means lower optimized usage. Ranges span the three trial medians. MiB is bytes/1048576.'
    Limitations='Synthetic production-resource lifecycle fixture with a common offscreen target. CPU PrivateBytes, CPU WorkingSet, Windows process GPU Dedicated Usage and Shared Usage are independent metrics and never added together. GPU values are Windows process counters, not inferred texture sizes or a CPU-RAM total. Runtime texture bytes may be unavailable. Range overlap is reported, not hidden. This does not establish whole-game peak RAM or FPS.'
    Trials=@($trialRecords.ToArray());Windows=@($comparison.ToArray());Resources=@($resourceRows.ToArray());UnavailableGpuMetrics=@($unavailableGpu.ToArray())
}
$result|ConvertTo-Json -Depth 12|Set-Content -LiteralPath (Join-Path $output 'comparison.json') -Encoding utf8
$comparison|Select-Object Window,Metric,Domain,BaselineMedianMiB,OptimizedMedianMiB,ReductionMiB,ReductionPercent,BaselineMinimumMiB,BaselineMaximumMiB,OptimizedMinimumMiB,OptimizedMaximumMiB,RangesOverlap|Export-Csv -LiteralPath (Join-Path $output 'comparison.csv') -NoTypeInformation -Encoding utf8
$resourceRows|Select-Object Trial,Window,TargetTextureCount,TargetSpriteCount,RuntimeTextureByteApiAvailable,TargetTextureBytes|Export-Csv -LiteralPath (Join-Path $output 'resource-counts.csv') -NoTypeInformation -Encoding utf8
$comparison|Where-Object Window -in @('art_closed_first','art_closed_second')|Format-Table Window,Metric,BaselineMedianMiB,OptimizedMedianMiB,ReductionMiB,RangesOverlap -AutoSize
Write-Output ('PASS comparison: '+$output)
