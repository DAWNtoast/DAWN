param(
    [Parameter(Mandatory=$true)][string]$BaselineExecutable,
    [Parameter(Mandatory=$true)][string]$OptimizedExecutable,
    [string]$TrialsRoot = 'D:\Game\Artifacts\RuntimeMemoryOptimization20261014\Trials'
)
$ErrorActionPreference = 'Stop'
$trialPath = [IO.Path]::GetFullPath($TrialsRoot)
if (-not $trialPath.StartsWith('D:\Game\Artifacts\RuntimeMemoryOptimization20261014\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Use this round trial root.' }
if (Test-Path -LiteralPath $trialPath) { throw 'Trial root must be new; do not replace evidence.' }
function Audit-ProbeHash([string]$Exe) {
    $managed = Join-Path ([IO.Path]::GetDirectoryName($Exe)) ([IO.Path]::GetFileNameWithoutExtension($Exe) + '_Data\Managed\ShatteredSail.RuntimeMemoryAudit.dll')
    return (Get-FileHash -LiteralPath $managed).Hash
}
$baselineProbe = Audit-ProbeHash $BaselineExecutable
$optimizedProbe = Audit-ProbeHash $OptimizedExecutable
if ($baselineProbe -ne $optimizedProbe) { throw 'A/B probe DLLs differ. Verify identical source and compilation, then use an explicitly verified identical diagnostic DLL.' }
New-Item -ItemType Directory -Path $trialPath | Out-Null
[pscustomobject]@{ProbeSHA256=$baselineProbe;Order=@('Baseline-1','Optimized-1','Baseline-2','Optimized-2','Baseline-3','Optimized-3');Method='Six sequential fresh processes with baseline/optimized alternation; identical isolated audit identity cannot run concurrently. File caches warm. No forced GC.'} | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $trialPath 'trial-order.json') -Encoding utf8
foreach ($trial in @('Baseline-1','Optimized-1','Baseline-2','Optimized-2','Baseline-3','Optimized-3')) {
    $exe = if ($trial.StartsWith('Baseline')) { $BaselineExecutable } else { $OptimizedExecutable }
    Write-Output ('TRIAL_START=' + $trial)
    & (Join-Path $PSScriptRoot 'Measure.ps1') -Executable $exe -OutputDirectory (Join-Path $trialPath $trial)
    Write-Output ('TRIAL_DONE=' + $trial)
}
& (Join-Path $PSScriptRoot 'Compare.ps1') -TrialsRoot $trialPath
