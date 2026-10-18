param(
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$RunTag = 'SecondBaseline50',
    [ValidateRange(1,500)][int]$Seeds = 50,
    [ValidateRange(0,100000)][int]$StartSeed = 0,
    [string]$CandidateFile = '',
    [ValidateSet('legacy','final')][string]$ScenarioSet = 'legacy',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$matrixWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$matrixRoot = Join-Path $matrixWorkspace 'Artifacts/EarlyChartBossTargets20261016'
$matrixFirstTag = $RunTag + '-second-unprepared'
& (Join-Path $PSScriptRoot 'RunSimulation.ps1') -RunTag $matrixFirstTag -Seeds $Seeds -StartSeed $StartSeed -Scenario second-unprepared -PolicySet balanced -EntryPercent 100 -CandidateFile $CandidateFile -UnityData $UnityData
$matrixCompiledRoot = Join-Path $matrixRoot $matrixFirstTag
$matrixExe = Join-Path $matrixCompiledRoot 'Build/SimulateEarlyCharts.exe'
$matrixCandidate = if ($CandidateFile) { Join-Path $matrixCompiledRoot 'Build/candidates.csv' } else { '-' }
$matrixScenarios = if ($ScenarioSet -eq 'final') {
    @('second-upgrades', 'second-ship', 'second-prepared-no-special', 'second-prepared-isha', 'second-prepared-mixed')
} else {
    @('second-upgrades', 'second-ship', 'second-ship-upgrades', 'second-prepared-no-special', 'second-prepared', 'second-prepared-2med')
}
foreach ($matrixScenario in $matrixScenarios) {
    $matrixOutput = Join-Path $matrixRoot ($RunTag + '-' + $matrixScenario)
    New-Item -ItemType Directory -Force -Path $matrixOutput | Out-Null
    Copy-Item -LiteralPath (Join-Path $matrixCompiledRoot 'source-hashes.csv') -Destination (Join-Path $matrixOutput 'source-hashes.csv') -Force
    $matrixExe | Set-Content -LiteralPath (Join-Path $matrixOutput 'compiled-assembly.txt') -Encoding utf8
    & (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $matrixExe $matrixOutput $Seeds $StartSeed $matrixScenario 'balanced' 100 $matrixCandidate 2>&1 |
        Tee-Object -FilePath (Join-Path $matrixOutput 'simulation.log')
    if ($LASTEXITCODE -ne 0) { throw "Second-chart fixture failed: $matrixScenario" }
}
$matrixCombined = foreach ($matrixScenario in (@('second-unprepared') + $matrixScenarios)) {
    foreach ($matrixRow in (Import-Csv -LiteralPath (Join-Path $matrixRoot ($RunTag + '-' + $matrixScenario + '/summary.csv')))) {
        $matrixRow | Add-Member -MemberType NoteProperty -Name scenario -Value $matrixScenario -PassThru
    }
}
$matrixCombined | Export-Csv -LiteralPath (Join-Path $matrixRoot ($RunTag + '-all-summaries.csv')) -NoTypeInformation -Encoding utf8
