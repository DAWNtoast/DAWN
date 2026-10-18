param([string]$RunTag = 'Current')
$ErrorActionPreference = 'Stop'
$specialWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$specialOutput = Join-Path $specialWorkspace ('Artifacts/SpecialCardRedesign20261017/' + $RunTag)
# Run all pure Core card/battle fixtures, including numbering, saved identity and the new doctrines.
& (Join-Path $specialWorkspace 'tools/CardBalanceAudit/RunCoreTests.ps1') -Output $specialOutput
if ($LASTEXITCODE -ne 0) { throw 'Special-card redesign Core regressions failed.' }
