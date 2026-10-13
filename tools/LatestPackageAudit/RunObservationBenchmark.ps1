param(
    [string]$OriginalSnapshotSource = 'D:/Game/tmp/naval-ammunition-rules-20261011/Assets/_Project/Core/Runtime/CoreVoyageSession.cs'
)
$ErrorActionPreference = 'Stop'
$benchWorkspace = 'D:/Game'
$benchDirectory = 'D:/Game/tmp/rules-review'
$benchUnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
$benchSources = @(Get-ChildItem -LiteralPath "$benchWorkspace/UnityClient/Assets/_Project/Core/Runtime" -Filter '*.cs' | ForEach-Object FullName)
$benchOriginalDirectory = Join-Path $benchDirectory 'baseline'
New-Item -ItemType Directory -Force -Path $benchOriginalDirectory | Out-Null
foreach ($benchSource in $benchSources) {
    Copy-Item -LiteralPath $benchSource -Destination $benchOriginalDirectory -Force
}
$benchSessionSource = Join-Path $benchOriginalDirectory 'CoreVoyageSession.cs'
$benchSessionText = [IO.File]::ReadAllText($benchSessionSource)
$benchOptimizedCalls = @'
            RevealVisibleCells(position, radius, CoreVoyageMap.GridColumns, CoreVoyageMap.GridRows, Map.ExploredCells, 1f);
            RevealVisibleCells(position, radius, CoreVoyageMap.ChartColumns, CoreVoyageMap.ChartRows, Map.ChartExploredCells, 0.5f);
'@
$benchOriginalCalls = @'
            for (int row = 0; row < CoreVoyageMap.GridRows; row += 1)
            {
                for (int column = 0; column < CoreVoyageMap.GridColumns; column += 1)
                {
                    if (CanSeeCell(position, column, row, radius))
                    {
                        Map.ExploredCells[row * CoreVoyageMap.GridColumns + column] = true;
                    }
                }
            }
            for (int row = 0; row < CoreVoyageMap.ChartRows; row++)
            {
                for (int column = 0; column < CoreVoyageMap.ChartColumns; column++)
                {
                    int index = row * CoreVoyageMap.ChartColumns + column;
                    if (Map.ChartExploredCells[index]) continue;
                    VoyagePoint center = Map.ChartCellCenter(column, row);
                    float edgeVariation = VisibilityEdgeVariation(Map.Hash, column, row) * 0.5f;
                    if (CanSeePoint(position, center, radius, edgeVariation)) Map.ChartExploredCells[index] = true;
                }
            }
'@
$benchSessionText = $benchSessionText.Replace("`r`n", "`n")
$benchOptimizedCalls = $benchOptimizedCalls.Replace("`r`n", "`n")
if (!$benchSessionText.Contains($benchOptimizedCalls)) { throw 'Expected optimized method not found.' }
$benchSessionText = $benchSessionText.Replace($benchOptimizedCalls, $benchOriginalCalls.Replace("`r`n", "`n"))
[IO.File]::WriteAllText($benchSessionSource, $benchSessionText, [Text.UTF8Encoding]::new($false))
$benchSnapshotText = [IO.File]::ReadAllText($OriginalSnapshotSource)
$benchSnapshotMethod = [regex]::Match($benchSnapshotText, '(?s)        private void RevealAt\(VoyagePoint position\).*?(?=        private bool CanSeeCell)').Value
$benchReconstructedMethod = [regex]::Match($benchSessionText, '(?s)        private void RevealAt\(VoyagePoint position\).*?(?=        private void RevealVisibleCells)').Value
if (!$benchSnapshotMethod -or !$benchReconstructedMethod -or
    [regex]::Replace($benchSnapshotMethod, '\s+', '') -cne [regex]::Replace($benchReconstructedMethod, '\s+', '')) {
    throw 'Reconstructed baseline RevealAt does not match the archived source snapshot.'
}
$benchRuntime = Join-Path $benchUnityData 'NetCoreRuntime/dotnet.exe'
$benchCompiler = Join-Path $benchUnityData 'DotNetSdkRoslyn/csc.dll'
$benchFramework = Join-Path $benchUnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$benchCompileOptions = @($benchCompiler, '-nologo', '-langversion:9', '-noconfig', '-nostdlib', '-optimize+')
$benchProgramSource = Join-Path $PSScriptRoot 'ObservationBenchmark.cs'
$benchSourceInputs = @($benchSources) + @($benchProgramSource, $PSCommandPath, $OriginalSnapshotSource)
$benchBaselineSources = @(Get-ChildItem -LiteralPath $benchOriginalDirectory -Filter '*.cs' | ForEach-Object FullName)
$benchHashManifest = [pscustomobject]@{
    Utc = [DateTime]::UtcNow.ToString('o')
    BaselineReconstruction = 'Current Core sources with RevealAt full-grid loops reconstructed from the pre-optimization implementation; not an old Player binary benchmark.'
    OriginalSnapshotSource = [IO.Path]::GetFullPath($OriginalSnapshotSource)
    RevealAtMatchesSnapshotIgnoringWhitespace = $true
    SourceInputs = @($benchSourceInputs | ForEach-Object { $benchHash = Get-FileHash -LiteralPath $_ -Algorithm SHA256; [pscustomobject]@{Path=$benchHash.Path; SHA256=$benchHash.Hash} })
    ReconstructedBaselineInputs = @($benchBaselineSources | ForEach-Object { $benchHash = Get-FileHash -LiteralPath $_ -Algorithm SHA256; [pscustomobject]@{Path=$benchHash.Path; SHA256=$benchHash.Hash} })
    ToolchainInputs = @(@($benchRuntime, $benchCompiler, (Join-Path $benchUnityData 'MonoBleedingEdge/bin/mono.exe')) | ForEach-Object { $benchHash = Get-FileHash -LiteralPath $_ -Algorithm SHA256; [pscustomobject]@{Path=$benchHash.Path; SHA256=$benchHash.Hash} })
}
$benchHashManifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $benchDirectory 'observation-benchmark-inputs.json') -Encoding utf8
foreach ($benchAssembly in @('mscorlib.dll','System.dll','System.Core.dll')) {
    $benchCompileOptions += '-r:' + (Join-Path $benchFramework $benchAssembly)
}
& $benchRuntime @benchCompileOptions '-target:library' "-out:$benchDirectory/BeforeCore.dll" @benchBaselineSources
if ($LASTEXITCODE -ne 0) { throw 'Baseline Core compilation failed.' }
& $benchRuntime @benchCompileOptions '-target:library' "-out:$benchDirectory/AfterCore.dll" @benchSources
if ($LASTEXITCODE -ne 0) { throw 'Optimized Core compilation failed.' }
& $benchRuntime @benchCompileOptions '-target:exe' "-out:$benchDirectory/ObservationBenchmark.exe" "-r:baseline=$benchDirectory/BeforeCore.dll" "-r:optimized=$benchDirectory/AfterCore.dll" $benchProgramSource
if ($LASTEXITCODE -ne 0) { throw 'Benchmark compilation failed.' }
& (Join-Path $benchUnityData 'MonoBleedingEdge/bin/mono.exe') "$benchDirectory/ObservationBenchmark.exe" | Tee-Object -FilePath "$benchDirectory/observation-benchmark.txt"
if ($LASTEXITCODE -ne 0) { throw 'Observation benchmark failed.' }
