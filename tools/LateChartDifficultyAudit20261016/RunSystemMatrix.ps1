param(
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$RunPrefix = 'SystemsScreen30',
    [ValidateRange(1,500)][int]$Seeds = 30,
    [ValidateRange(0,100000)][int]$StartSeed = 0,
    [string[]]$Schools = @('fire','arcane','curse','tide','thunder','blood'),
    [string[]]$Builds = @('B1','B2','B3'),
    [ValidateSet('balanced','all')][string]$PolicySet = 'balanced'
)
$ErrorActionPreference = 'Stop'
$auditWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$auditPolicyHash = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'SystemPolicy.cs') -Algorithm SHA256).Hash
$auditHarnessHash = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'SimulateLateCharts.cs') -Algorithm SHA256).Hash
$auditSpecPath = Join-Path $auditWorkspace 'Artifacts/LateChartDifficultyAudit20261016/school-spec.txt'
$auditSpecHash = (Get-FileHash -LiteralPath $auditSpecPath -Algorithm SHA256).Hash
$auditProductionFiles = @(Get-ChildItem -LiteralPath (Join-Path $auditWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs')
$auditProductionFiles += Get-Item -LiteralPath (Join-Path $auditWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$auditProductionHashes = @{}
foreach ($auditSource in $auditProductionFiles) { $auditProductionHashes[$auditSource.FullName] = (Get-FileHash -LiteralPath $auditSource.FullName -Algorithm SHA256).Hash }
$auditRows = @()
foreach ($auditSchool in $Schools) {
    foreach ($auditBuild in $Builds) {
        if ((Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'SimulateLateCharts.cs') -Algorithm SHA256).Hash -ne $auditHarnessHash -or
            (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'SystemPolicy.cs') -Algorithm SHA256).Hash -ne $auditPolicyHash -or
            (Get-FileHash -LiteralPath $auditSpecPath -Algorithm SHA256).Hash -ne $auditSpecHash) {
            throw 'Policy/specification changed during the matrix; do not merge different versions.'
        }
        foreach ($auditSource in $auditProductionFiles) {
            if ((Get-FileHash -LiteralPath $auditSource.FullName -Algorithm SHA256).Hash -ne $auditProductionHashes[$auditSource.FullName]) {
                throw ('Production changed during the matrix: ' + $auditSource.Name)
            }
        }
        $auditTag = $RunPrefix + '_' + $auditSchool + '_' + $auditBuild
        & (Join-Path $PSScriptRoot 'RunSimulation.ps1') -RunTag $auditTag -Seeds $Seeds -StartSeed $StartSeed -Scenario $auditBuild -School $auditSchool -PolicySet $PolicySet
        $auditSummary = Join-Path $auditWorkspace ('Artifacts/LateChartDifficultyAudit20261016/' + $auditTag + '/summary.csv')
        $auditRows += Import-Csv -LiteralPath $auditSummary | Select-Object @{n='school';e={$auditSchool}},@{n='build';e={$auditBuild}},*
    }
}
$auditRows | Export-Csv -LiteralPath (Join-Path $auditWorkspace ('Artifacts/LateChartDifficultyAudit20261016/' + $RunPrefix + '-summary.csv')) -NoTypeInformation -Encoding utf8
Write-Output ('Completed matrix ' + $RunPrefix + ': ' + $auditRows.Count + ' result rows.')
