param(
    [int]$Seeds = 16,
    [string]$Output = 'D:/Game/UnityClient/Artifacts/ShipProgression20261003/Production-Verification',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$productionWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$productionOutput = [IO.Path]::GetFullPath($Output)
$productionCore = Join-Path $productionWorkspace 'UnityClient/Assets/_Project/Core/Runtime'
$productionSnapshot = Join-Path $productionOutput 'CoreSnapshot'
$productionBuild = Join-Path $productionOutput 'Build'
if (Test-Path -LiteralPath (Join-Path $productionOutput 'source-hashes.csv')) { throw 'Use a fresh output directory to preserve the source provenance.' }
New-Item -ItemType Directory -Force -Path $productionSnapshot, $productionBuild | Out-Null
$productionSources = @(Get-ChildItem -LiteralPath $productionCore -Filter '*.cs' | Sort-Object Name)
$productionHashes = foreach ($productionFile in $productionSources) {
    $productionTarget = Join-Path $productionSnapshot $productionFile.Name
    $productionBefore = (Get-FileHash -LiteralPath $productionFile.FullName -Algorithm SHA256).Hash
    Copy-Item -LiteralPath $productionFile.FullName -Destination $productionTarget
    $productionCopy = (Get-FileHash -LiteralPath $productionTarget -Algorithm SHA256).Hash
    $productionAfter = (Get-FileHash -LiteralPath $productionFile.FullName -Algorithm SHA256).Hash
    if ($productionBefore -ne $productionCopy -or $productionAfter -ne $productionCopy) { throw "Source changed during copy: $($productionFile.Name)" }
    [pscustomobject]@{ Name=$productionFile.Name; Source=$productionFile.FullName; Snapshot=$productionTarget; SourceSHA256=$productionBefore; CompiledSHA256=$productionCopy; Identical=$true }
}
$productionHashes | Export-Csv -LiteralPath (Join-Path $productionOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
$productionFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$productionCompiler = Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'
$productionRuntime = Join-Path $UnityData 'NetCoreRuntime/dotnet.exe'
$productionMono = Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe'
$productionRefs = @()
foreach ($productionAssembly in @('mscorlib.dll','System.dll','System.Core.dll')) { $productionRefs += '-r:' + (Join-Path $productionFramework $productionAssembly) }
$productionDll = Join-Path $productionBuild 'ShatteredSail.Production.Core.dll'
$productionArgs = @($productionCompiler,'-nologo','-target:library','-langversion:9','-noconfig','-nostdlib',"-out:$productionDll") + $productionRefs
$productionArgs += @($productionHashes | ForEach-Object Snapshot)
& $productionRuntime @productionArgs 2>&1 | Tee-Object -FilePath (Join-Path $productionOutput 'core-compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Production Core compilation failed.' }
$productionExe = Join-Path $productionBuild 'ProductionVerification.exe'
$productionArgs = @($productionCompiler,'-nologo','-target:exe','-langversion:9','-noconfig','-nostdlib',"-out:$productionExe","-r:$productionDll") + $productionRefs
foreach ($productionTool in @('ShipProgressionSimulator.cs','ProductionVerificationProgram.cs')) {
    $productionTarget = Join-Path $productionBuild $productionTool
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $productionTool) -Destination $productionTarget
    $productionArgs += $productionTarget
}
& $productionRuntime @productionArgs 2>&1 | Tee-Object -FilePath (Join-Path $productionOutput 'simulator-compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Separate simulator assembly compilation failed.' }

# The real test fixture is a third assembly; internal setters cannot be accessed by accident.
$productionPriorValidation = Join-Path $productionWorkspace 'UnityClient/Artifacts/ShipProgression20261003/ProposedPatch/Validation'
foreach ($productionTestFile in @('nunit.framework.dll','RunShipBoardingGrowthTests.cs')) { Copy-Item -LiteralPath (Join-Path $productionPriorValidation $productionTestFile) -Destination (Join-Path $productionBuild $productionTestFile) }
Copy-Item -LiteralPath (Join-Path $productionWorkspace 'UnityClient/Assets/_Project/Tests/EditMode/ShipBoardingGrowthTests.cs') -Destination (Join-Path $productionBuild 'ShipBoardingGrowthTests.cs')
$productionTestExe = Join-Path $productionBuild 'ShipBoardingGrowthTests.exe'
$productionArgs = @($productionCompiler,'-nologo','-target:exe','-langversion:9','-noconfig','-nostdlib',"-out:$productionTestExe","-r:$productionDll",('-r:' + (Join-Path $productionBuild 'nunit.framework.dll'))) + $productionRefs
$productionArgs += Join-Path $productionBuild 'ShipBoardingGrowthTests.cs'
$productionArgs += Join-Path $productionBuild 'RunShipBoardingGrowthTests.cs'
& $productionRuntime @productionArgs 2>&1 | Tee-Object -FilePath (Join-Path $productionOutput 'tests-compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Separate test assembly compilation failed.' }
& $productionMono $productionTestExe 2>&1 | Tee-Object -FilePath (Join-Path $productionOutput 'tests.log')
if ($LASTEXITCODE -ne 0) { throw 'Production boarding growth regression failed.' }
Get-ChildItem -LiteralPath $productionBuild -File | Get-FileHash -Algorithm SHA256 | Select-Object Path,Hash | Export-Csv -LiteralPath (Join-Path $productionOutput 'build-hashes.csv') -NoTypeInformation -Encoding utf8
[IO.File]::WriteAllText((Join-Path $productionOutput 'provenance.txt'), "Unmodified production Core snapshot. Every source file and compiled source SHA256 matches; no text replacement, candidate tables, or synthetic combat scaling. Core DLL, simulator EXE and test EXE are separate assemblies. Card policy reads GetBattleCardEffects and actual effects are resolved by production PlayCard. Snapshot captured at $([DateTime]::UtcNow.ToString('o')); Core files=$($productionHashes.Count). Command: RunProductionVerification.ps1 -Seeds $Seeds -Output $productionOutput")
& $productionMono $productionExe $Seeds $productionOutput 2>&1 | Tee-Object -FilePath (Join-Path $productionOutput 'run.log')
if ($LASTEXITCODE -ne 0) { throw 'Production simulation failed or exceeded watchdog.' }
