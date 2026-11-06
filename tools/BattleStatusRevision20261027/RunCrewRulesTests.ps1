$ErrorActionPreference = 'Stop'
$crewWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$crewOutput = Join-Path $crewWorkspace 'Artifacts/BossCalibration20261024/CrewPenaltyRemoval20261027'
$crewBuild = Join-Path $crewOutput 'Build'
$crewFixtures = @('CoreNavalStatImpactTests', 'BattleTurnCostAndInitiativeTests',
    'DepartureCrewMinimumTests', 'CrewSpeedRulesTests', 'CoreShipMarketCatalogTests', 'CoreVoyageSessionTests',
    'CoreBattleStatusPresentationTests', 'CoreTideAudit20261024Tests', 'CoreNavalBossBehaviorTests')
try {
    & (Join-Path $crewWorkspace 'tools/BossCalibration20261024/RunCoreTests.ps1') `
        -RunTag 'CrewPenaltyRemoval20261027' -Fixtures $crewFixtures
} finally {
    # Only remove this invocation's reproducible binaries, after the synchronous runner exits.
    $crewExpected = [IO.Path]::GetFullPath((Join-Path $crewWorkspace 'Artifacts/BossCalibration20261024/CrewPenaltyRemoval20261027/Build'))
    if ([IO.Path]::GetFullPath($crewBuild) -ne $crewExpected) { throw 'Unexpected crew test cleanup directory.' }
    foreach ($crewDirectory in @($crewWorkspace, (Join-Path $crewWorkspace 'Artifacts'),
        (Join-Path $crewWorkspace 'Artifacts/BossCalibration20261024'), $crewOutput, $crewBuild)) {
        if ((Test-Path -LiteralPath $crewDirectory) -and
            ((Get-Item -LiteralPath $crewDirectory).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw ('Refusing linked test cleanup directory: ' + $crewDirectory)
        }
    }
    $crewConsumers = @(Get-CimInstance Win32_Process | Where-Object {
        $_.Name -in @('mono.exe', 'dotnet.exe', 'Unity.exe') -and $_.CommandLine -and
        ($_.CommandLine.Contains($crewBuild) -or $_.CommandLine.Contains($crewBuild.Replace('\', '/')))
    })
    if ($crewConsumers.Count) { throw 'Crew test binaries still used by another process.' }
    $crewFreed = 0L
    foreach ($crewName in @('NavalBalanceTests.exe', 'nunit.framework.dll')) {
        $crewFile = Join-Path $crewBuild $crewName
        if (Test-Path -LiteralPath $crewFile) {
            $crewItem = Get-Item -LiteralPath $crewFile
            if ($crewItem.PSIsContainer -or ($crewItem.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                throw ('Refusing unexpected crew test artifact: ' + $crewFile)
            }
            $crewFreed += $crewItem.Length
            Remove-Item -LiteralPath $crewFile -Force
            if (Test-Path -LiteralPath $crewFile) { throw ('Crew test cleanup failed: ' + $crewFile) }
        }
    }
    if ((Test-Path -LiteralPath $crewBuild) -and @(Get-ChildItem -LiteralPath $crewBuild -Force).Count -eq 0) {
        Remove-Item -LiteralPath $crewBuild
    }
    if (Test-Path -LiteralPath $crewOutput) {
        ('Removed task test binaries: ' + $crewFreed + ' bytes; retained logs, results and source hashes. Main Unity project unchanged by cleanup.') |
            Add-Content -LiteralPath (Join-Path $crewOutput 'cleanup.log') -Encoding utf8
    }
}
