param([string]$OutputRoot = 'D:\Game\Artifacts\StartupMemory20261011', [ValidateSet('Trials','VisibleTrials')][string]$TrialDirectory = 'Trials')
$ErrorActionPreference = 'Stop'
function Comparison-Median($Values) {
    $comparisonSorted = @($Values | Sort-Object)
    $comparisonMid = [int][Math]::Floor($comparisonSorted.Count / 2)
    if ($comparisonSorted.Count % 2) { return [double]$comparisonSorted[$comparisonMid] }
    return ([double]$comparisonSorted[$comparisonMid-1] + [double]$comparisonSorted[$comparisonMid]) / 2
}
$comparisonTrials = @()
foreach ($comparisonVariant in @('Baseline','Optimized')) {
    foreach ($comparisonFixture in @('Empty','Voyage')) {
        for ($comparisonIndex=1; $comparisonIndex -le 3; $comparisonIndex++) {
            $comparisonPath = Join-Path $OutputRoot ($TrialDirectory + '\' + $comparisonVariant + '-' + $comparisonFixture + '-' + $comparisonIndex)
            $comparisonSummary = Get-Content -LiteralPath (Join-Path $comparisonPath 'summary.json') -Raw | ConvertFrom-Json
            if ($comparisonSummary.Result -ne 'PASS' -or $comparisonSummary.Fixture -ne $comparisonFixture -or $comparisonSummary.RenderedCameraFrames -lt 100) { throw ('Failed trial: ' + $comparisonPath) }
            $comparisonSummary | Add-Member -NotePropertyName Variant -NotePropertyValue $comparisonVariant
            $comparisonSummary | Add-Member -NotePropertyName Trial -NotePropertyValue $comparisonIndex
            $comparisonTrials += $comparisonSummary
        }
    }
}
if (@($comparisonTrials | Select-Object -ExpandProperty Offscreen -Unique).Count -ne 1 -or
    @($comparisonTrials | Select-Object -ExpandProperty Visible -Unique).Count -ne 1) { throw 'All compared trials must use the same rendering/window mode.' }
$comparisonTrials | Export-Csv -LiteralPath (Join-Path $OutputRoot 'trial-summary.csv') -NoTypeInformation -Encoding utf8
$comparisonMetrics = @('PeakPrivateMiB','PeakWorkingSetMiB','StablePrivateMiB','StableWorkingSetMiB','UnityAllocatedMiB','UnityReservedMiB','ManagedUsedMiB','ManagedReservedMiB','FirstMenuReadySeconds','Textures','GameObjects','AudioClips')
$comparisonRows = @()
foreach ($comparisonFixture in @('Empty','Voyage')) {
    foreach ($comparisonMetric in $comparisonMetrics) {
        $comparisonBefore = Comparison-Median @($comparisonTrials | Where-Object { $_.Fixture -eq $comparisonFixture -and $_.Variant -eq 'Baseline' } | ForEach-Object { $_.$comparisonMetric })
        $comparisonAfter = Comparison-Median @($comparisonTrials | Where-Object { $_.Fixture -eq $comparisonFixture -and $_.Variant -eq 'Optimized' } | ForEach-Object { $_.$comparisonMetric })
        $comparisonRows += [pscustomobject]@{Fixture=$comparisonFixture;Metric=$comparisonMetric;Before=[Math]::Round($comparisonBefore,3);After=[Math]::Round($comparisonAfter,3);Reduction=[Math]::Round($comparisonBefore-$comparisonAfter,3);ReductionPercent=if($comparisonBefore -ne 0){[Math]::Round(100*($comparisonBefore-$comparisonAfter)/$comparisonBefore,2)}else{$null}}
    }
}
$comparisonRows | Export-Csv -LiteralPath (Join-Path $OutputRoot 'comparison.csv') -NoTypeInformation -Encoding utf8
$comparisonRows | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputRoot 'comparison.json') -Encoding utf8
$comparisonRows | Where-Object Metric -in @('StablePrivateMiB','StableWorkingSetMiB','PeakPrivateMiB','PeakWorkingSetMiB','ManagedReservedMiB') | Format-Table -AutoSize
