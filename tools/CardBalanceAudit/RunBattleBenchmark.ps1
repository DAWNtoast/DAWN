param(
    [int]$Seeds = 16,
    [string]$Output = 'D:/Game/UnityClient/Artifacts/CardBalanceReview20261012/BattlesCurrent',
    [string]$CoreSource = '',
    [switch]$Sources,
    [string]$CompareTo = '',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
if ($Seeds -lt 1) { throw 'At least one seed is required (use 16 or more for review evidence).' }
$schoolWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if ([string]::IsNullOrWhiteSpace($CoreSource)) { $CoreSource = Join-Path $schoolWorkspace 'UnityClient/Assets/_Project/Core/Runtime' }
$schoolBuild = Join-Path $schoolWorkspace ('tmp/card-school-benchmark-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $schoolBuild | Out-Null
$schoolCompiler = Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'
$schoolRuntime = Join-Path $UnityData 'NetCoreRuntime/dotnet.exe'
$schoolMono = Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe'
$schoolFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$schoolSources = @(Get-ChildItem -LiteralPath $CoreSource -Filter '*.cs' | ForEach-Object FullName)
if ($schoolSources.Count -eq 0) { throw 'CoreSource must be the directory containing the Core C# source files.' }
$schoolSources += Join-Path $PSScriptRoot 'SchoolBattleBenchmark.cs'
$schoolExe = Join-Path $schoolBuild 'SchoolBattleBenchmark.exe'
$schoolArguments = @($schoolCompiler, '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$schoolExe")
foreach ($schoolAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll')) {
    $schoolArguments += '-r:' + (Join-Path $schoolFramework $schoolAssembly)
}
$schoolArguments += $schoolSources
if (-not [string]::IsNullOrWhiteSpace($CompareTo)) {
    $schoolCaptainBefore = Get-Content -LiteralPath (Join-Path $CompareTo 'benchmark-source-sha256.txt')
    $schoolCaptainNow = @('SchoolBattleBenchmark.cs', 'RunBattleBenchmark.ps1') | ForEach-Object {
        "$((Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $_) -Algorithm SHA256).Hash)  $_"
    }
    if (Compare-Object $schoolCaptainBefore $schoolCaptainNow) { throw 'Before/after benchmark source differs. Re-run the baseline with this captain before comparing.' }
}
& $schoolRuntime @schoolArguments
if ($LASTEXITCODE -ne 0) { throw 'School battle benchmark compilation failed.' }
& $schoolMono $schoolExe $Seeds $Output $(if ($Sources) { 'sources' } else { 'basic' })
if ($LASTEXITCODE -ne 0) { throw 'School battle benchmark failed.' }
Get-ChildItem -LiteralPath $CoreSource -Filter '*.cs' | Sort-Object Name | ForEach-Object {
    $schoolHash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    "$schoolHash  $($_.Name)"
} | Set-Content -LiteralPath (Join-Path $Output 'core-source-sha256.txt') -Encoding UTF8
@('SchoolBattleBenchmark.cs', 'RunBattleBenchmark.ps1') | ForEach-Object {
    $schoolHash = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $_) -Algorithm SHA256).Hash
    "$schoolHash  $_"
} | Set-Content -LiteralPath (Join-Path $Output 'benchmark-source-sha256.txt') -Encoding UTF8
if (-not [string]::IsNullOrWhiteSpace($CompareTo)) {
    $schoolBeforeRows = @{}
    foreach ($schoolRow in (Import-Csv -LiteralPath (Join-Path $CompareTo 'battles.csv'))) {
        $schoolKey = @($schoolRow.school, $schoolRow.rank, $schoolRow.tactic, $schoolRow.enemy, $schoolRow.seed, $schoolRow.sourceCardId) -join '/'
        if ($schoolBeforeRows.ContainsKey($schoolKey)) { throw "Duplicate baseline matchup: $schoolKey" }
        $schoolBeforeRows[$schoolKey] = $schoolRow
    }
    $schoolAfterRows = @(Import-Csv -LiteralPath (Join-Path $Output 'battles.csv'))
    if ($schoolBeforeRows.Count -ne $schoolAfterRows.Count) { throw 'Before/after battle counts differ.' }
    $schoolPairs = foreach ($schoolRow in $schoolAfterRows) {
        $schoolKey = @($schoolRow.school, $schoolRow.rank, $schoolRow.tactic, $schoolRow.enemy, $schoolRow.seed, $schoolRow.sourceCardId) -join '/'
        if (-not $schoolBeforeRows.ContainsKey($schoolKey)) { throw "Missing baseline matchup: $schoolKey" }
        $schoolBefore = $schoolBeforeRows[$schoolKey]
        [pscustomobject]@{
            school = $schoolRow.school; rank = $schoolRow.rank; tactic = $schoolRow.tactic
            enemy = $schoolRow.enemy; seed = $schoolRow.seed; sourceCardId = $schoolRow.sourceCardId
            outcomeBefore = $schoolBefore.outcome; outcomeAfter = $schoolRow.outcome
            winDelta = [int]($schoolRow.outcome -eq 'win') - [int]($schoolBefore.outcome -eq 'win')
            commonWinTurnsDelta = $(if ($schoolBefore.outcome -eq 'win' -and $schoolRow.outcome -eq 'win') { [int]$schoolRow.turns - [int]$schoolBefore.turns } else { '' })
            hullDamageDelta = [int]$schoolRow.hullDamageTaken - [int]$schoolBefore.hullDamageTaken
            crewDamageDelta = [int]$schoolRow.crewDamageTaken - [int]$schoolBefore.crewDamageTaken
            resourceLockedTurnsDelta = [int]$schoolRow.resourceLockedTurns - [int]$schoolBefore.resourceLockedTurns
        }
    }
    $schoolPairs | Export-Csv -LiteralPath (Join-Path $Output 'paired-comparison.csv') -NoTypeInformation -Encoding UTF8
    $schoolPairs | Group-Object school, rank, tactic, sourceCardId | ForEach-Object {
        $schoolGroup = $_.Group
        $schoolFirst = $schoolGroup[0]
        $schoolCommon = @($schoolGroup | Where-Object { $_.outcomeBefore -eq 'win' -and $_.outcomeAfter -eq 'win' })
        [pscustomobject]@{
            school = $schoolFirst.school; rank = $schoolFirst.rank; tactic = $schoolFirst.tactic; sourceCardId = $schoolFirst.sourceCardId
            pairedBattles = $schoolGroup.Count; newWins = @($schoolGroup | Where-Object winDelta -eq 1).Count
            lostWins = @($schoolGroup | Where-Object winDelta -eq -1).Count
            winRateDelta = ($schoolGroup | Measure-Object winDelta -Average).Average
            commonWins = $schoolCommon.Count
            meanCommonWinTurnsDelta = $(if ($schoolCommon.Count -gt 0) { ($schoolCommon | Measure-Object commonWinTurnsDelta -Average).Average } else { '' })
            meanHullDamageDelta = ($schoolGroup | Measure-Object hullDamageDelta -Average).Average
            meanCrewDamageDelta = ($schoolGroup | Measure-Object crewDamageDelta -Average).Average
            meanResourceLockedTurnsDelta = ($schoolGroup | Measure-Object resourceLockedTurnsDelta -Average).Average
        }
    } | Export-Csv -LiteralPath (Join-Path $Output 'paired-summary.csv') -NoTypeInformation -Encoding UTF8
}
