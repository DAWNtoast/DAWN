param(
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$RunTag = 'Observe',
    [ValidateRange(1,256)][int]$Seeds = 1,
    [ValidateSet('Combat','Observe')][string]$Mode = 'Observe',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$probeWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$probeOutput = Join-Path $probeWorkspace ('Artifacts/BoardingBossPacing20261016/' + $RunTag)
$probeSnapshot = Join-Path $probeOutput 'CoreSnapshot'
$probeBuild = Join-Path $probeOutput 'Build'
New-Item -ItemType Directory -Force -Path $probeSnapshot,$probeBuild | Out-Null
$probeSources = @(Get-ChildItem -LiteralPath (Join-Path $probeWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs')
$probeSources += Get-Item -LiteralPath (Join-Path $probeWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$probeHashes = foreach ($probeSource in $probeSources) {
    $probeCopy = Join-Path $probeSnapshot $probeSource.Name
    Copy-Item -LiteralPath $probeSource.FullName -Destination $probeCopy -Force
    $probeHash = (Get-FileHash -LiteralPath $probeCopy -Algorithm SHA256).Hash
    if ($probeHash -ne (Get-FileHash -LiteralPath $probeSource.FullName -Algorithm SHA256).Hash) {
        throw "Snapshot mismatch: $($probeSource.Name)"
    }
    [pscustomobject]@{Source=$probeSource.FullName;SHA256=$probeHash}
}
$probeHashes | Export-Csv -LiteralPath (Join-Path $probeOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
$probeHarness = [IO.File]::ReadAllText((Join-Path $probeWorkspace 'tools/BoardingEnemyBalance20261015/SimulateBoarding.cs'))
$probeAnchor = 's.Battle.DistanceMeters = 60f; s.Battle.Alignment = NavalBattleAlignment.Parallel;'
if (-not $probeHarness.Contains($probeAnchor)) { throw 'The existing observation harness entry setup changed; review it before running.' }
$probeHarness = $probeHarness.Replace($probeAnchor, 's.Battle.DistanceMeters = 100f; s.Battle.Alignment = NavalBattleAlignment.Parallel;' + [Environment]::NewLine +
    '        typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(s.Battle, s.Battle.EnemyHeadingDegrees);')
$probeHarnessPath = Join-Path $probeBuild 'SimulateBoarding.cs'
[IO.File]::WriteAllText($probeHarnessPath, $probeHarness, (New-Object Text.UTF8Encoding($false)))
@"
Progression $Mode simulation with $Seeds deterministic seed(s), using the existing SimulateBoarding harness and an unmodified current Core snapshot.
Only the harness boarding setup uses orthogonally adjacent positions (100 meters) and aligns the actual player heading with the enemy heading, as required by current CanBoard.
Ordinary ship health, hand, energy, cards, fees, medicines, enemy scripts and victory rules remain active. Observe mode stops after two completed enemy cycles or a real terminal outcome; Combat mode plays to a real terminal outcome or the existing 60-turn watchdog.
This deterministic heuristic is calibration evidence, not a claim of optimal play or a player population win rate.
"@ | Set-Content -LiteralPath (Join-Path $probeOutput 'scope.txt') -Encoding utf8
$probeFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$probeExe = Join-Path $probeBuild 'SimulateBoarding.exe'
$probeArgs = @((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$probeExe")
foreach ($probeAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll')) {
    $probeArgs += '-r:' + (Join-Path $probeFramework $probeAssembly)
}
$probeArgs += @(Get-ChildItem -LiteralPath $probeSnapshot -Filter '*.cs' | ForEach-Object FullName)
$probeArgs += $probeHarnessPath
& (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @probeArgs 2>&1 | Tee-Object -FilePath (Join-Path $probeOutput 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Boss observation compilation failed.' }
& (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $probeExe $Seeds $probeOutput 'Progression' $Mode $Seeds 2>&1 |
    Tee-Object -FilePath (Join-Path $probeOutput 'simulation.log')
if ($LASTEXITCODE -ne 0) { throw 'Boss observation failed.' }
