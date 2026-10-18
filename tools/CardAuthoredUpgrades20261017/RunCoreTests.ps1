param([string]$RunTag = 'Current', [switch]$OnlyAuthored)
$ErrorActionPreference = 'Stop'
$growthWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$growthOutput = Join-Path $growthWorkspace ('Artifacts/CardAuthoredUpgrades20261017/' + $RunTag)
$growthArguments = @{ Output = $growthOutput }
if ($OnlyAuthored) { $growthArguments.Fixtures = @('CoreCardAuthoredUpgradeTests.cs') }
& (Join-Path $growthWorkspace 'tools/CardBalanceAudit/RunCoreTests.ps1') @growthArguments
if ($LASTEXITCODE -ne 0) { throw 'Authored card upgrade regressions failed.' }
