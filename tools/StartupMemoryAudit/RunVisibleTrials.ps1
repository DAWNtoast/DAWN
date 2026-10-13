$ErrorActionPreference = 'Stop'
# Use only after the user explicitly permits temporary visible game windows.
$startupRoot = 'D:\Game\Artifacts\StartupMemory20261011'
foreach ($startupIndex in 1..3) {
    foreach ($startupFixture in @('Empty','Voyage')) {
        $startupOrder = if (($startupIndex + [int]($startupFixture -eq 'Voyage')) % 2) { @('Baseline','Optimized') } else { @('Optimized','Baseline') }
        foreach ($startupVariant in $startupOrder) {
            & (Join-Path $PSScriptRoot 'Measure.ps1') -Executable ($startupRoot + '\' + $startupVariant + '\Windows\StartupAudit.exe') `
                -OutputDirectory ($startupRoot + '\VisibleTrials\' + $startupVariant + '-' + $startupFixture + '-' + $startupIndex) -Fixture $startupFixture -Visible
        }
    }
}
& (Join-Path $PSScriptRoot 'Compare.ps1') -OutputRoot $startupRoot -TrialDirectory VisibleTrials
