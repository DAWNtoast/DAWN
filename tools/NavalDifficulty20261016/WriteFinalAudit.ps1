$ErrorActionPreference = 'Stop'
$navalWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$navalRoot = Join-Path $navalWorkspace 'Artifacts/NavalDifficulty20261016'
$navalBaselineTags = @('FinalBaselineStarter100','BaselineMixed100','BaselineTideP100Fixed','BaselineTideM100')
$navalFinalTags = @('FinalStarter200','FinalMixed200','FinalTideP200','FinalTideM200','FinalTideH100','FinalThunderM50V2','FinalBloodM50V2','FinalOrdinaryStarter100','FinalOrdinaryMixed100','FinalOrdinaryTideP100')
$navalReference = @{}
$navalSourceChecks = 0
$navalHarnessChecks = 0
$navalBaselineRows = @()
$navalFinalRows = @()
$navalBaselineSummaries = @()
$navalFinalSummaries = @()
foreach ($navalTag in ($navalBaselineTags + $navalFinalTags)) {
    $navalDir = Join-Path $navalRoot $navalTag
    foreach ($navalHash in (Import-Csv -LiteralPath (Join-Path $navalDir 'harness-hashes.csv'))) {
        $navalName = [IO.Path]::GetFileName($navalHash.Path)
        if ((Get-FileHash -LiteralPath $navalHash.Path).Hash -ne $navalHash.Hash) { throw "Copied harness changed: $navalTag/$navalName" }
        if ($navalReference.ContainsKey($navalName) -and $navalReference[$navalName] -ne $navalHash.Hash) { throw "Harness differs: $navalTag/$navalName" }
        $navalReference[$navalName] = $navalHash.Hash
        $navalHarnessChecks++
    }
    foreach ($navalHash in (Import-Csv -LiteralPath (Join-Path $navalDir 'source-hashes.csv'))) {
        $navalCopy = Join-Path (Join-Path $navalDir 'CoreSnapshot') ([IO.Path]::GetFileName($navalHash.Source))
        if ((Get-FileHash -LiteralPath $navalHash.Source).Hash -ne $navalHash.SHA256 -or
            (Get-FileHash -LiteralPath $navalCopy).Hash -ne $navalHash.SHA256) { throw "Source differs: $navalTag/$navalCopy" }
        $navalSourceChecks++
    }
    $navalRuns = @(Import-Csv -LiteralPath (Join-Path $navalDir 'runs.csv'))
    $navalSummary = @(Import-Csv -LiteralPath (Join-Path $navalDir 'summary.csv'))
    if (($navalSummary | Measure-Object -Property runs -Sum).Sum -ne $navalRuns.Count) { throw "Count mismatch: $navalTag" }
    if ($navalBaselineTags -contains $navalTag) {
        $navalBaselineRows += $navalRuns
        $navalBaselineSummaries += $navalSummary
    } else {
        $navalFinalRows += $navalRuns
        $navalFinalSummaries += $navalSummary
    }
}
$navalBaselineSummaries | Export-Csv -LiteralPath (Join-Path $navalRoot 'baseline-summary.csv') -NoTypeInformation -Encoding utf8
$navalFinalSummaries | Export-Csv -LiteralPath (Join-Path $navalRoot 'final-summary.csv') -NoTypeInformation -Encoding utf8
$navalFinalRows | Export-Csv -LiteralPath (Join-Path $navalRoot 'final-runs.csv') -NoTypeInformation -Encoding utf8
$navalAllRows = @($navalBaselineRows + $navalFinalRows)
$navalTimeouts = @($navalAllRows | Where-Object { $_.outcome -eq 'timeout' }).Count
$navalForced = @($navalAllRows | Where-Object { $_.outcome -eq 'forced-boarding' }).Count
$navalPaired = foreach ($navalOld in $navalBaselineSummaries) {
    $navalOldRuns = @($navalBaselineRows | Where-Object { $_.build -eq $navalOld.build -and $_.enemy -eq $navalOld.enemy })
    $navalNewRuns = @($navalFinalRows | Where-Object { $_.build -eq $navalOld.build -and $_.enemy -eq $navalOld.enemy -and [int]$_.seed -ge 1000 -and [int]$_.seed -lt 1100 })
    if ($navalOldRuns.Count -ne 100 -or $navalNewRuns.Count -ne 100) { throw 'Paired sample count differs' }
    [pscustomobject]@{ build=$navalOld.build; enemy=$navalOld.enemy; pairedSeeds=100; oldWins=@($navalOldRuns | Where-Object outcome -eq 'win').Count; finalWins=@($navalNewRuns | Where-Object outcome -eq 'win').Count }
}
$navalPaired | Export-Csv -LiteralPath (Join-Path $navalRoot 'paired-comparison.csv') -NoTypeInformation -Encoding utf8
@(
    'BaselineRuns=' + $navalBaselineRows.Count
    'FinalRuns=' + $navalFinalRows.Count
    'TotalRuns=' + $navalAllRows.Count
    'Timeouts=' + $navalTimeouts
    'ForcedBoarding=' + $navalForced
    'SourceAndSnapshotHashChecks=' + $navalSourceChecks
    'SourceAndSnapshotHashMismatches=0'
    'IdenticalHarnessFileChecks=' + $navalHarnessChecks
    'HarnessHashMismatches=0'
    'FinalTags=' + ($navalFinalTags -join ',')
    'BaselineTags=' + ($navalBaselineTags -join ',')
    'Earlier Candidate/Current/Pilot runs are developmental records and are excluded.'
) | Set-Content -LiteralPath (Join-Path $navalRoot 'final-verification.txt') -Encoding utf8
Get-Content -LiteralPath (Join-Path $navalRoot 'final-verification.txt')
