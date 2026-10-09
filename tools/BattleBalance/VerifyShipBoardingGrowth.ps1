param(
    [string]$PatchRoot = 'D:/Game/UnityClient/Artifacts/ShipProgression20261003/ProposedPatch',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$growthWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$growthValidation = Join-Path $PatchRoot 'Validation'
$growthFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$growthSources = @{}
Get-ChildItem -LiteralPath (Join-Path $growthWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | ForEach-Object { $growthSources[$_.Name] = $_.FullName }
Get-ChildItem -LiteralPath (Join-Path $PatchRoot 'Core/Runtime') -Filter '*.cs' | ForEach-Object { $growthSources[$_.Name] = $_.FullName }
$growthExe = Join-Path $growthValidation 'ShipBoardingGrowthTests.exe'
$growthArgs = @((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$growthExe")
foreach ($growthAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll')) { $growthArgs += '-r:' + (Join-Path $growthFramework $growthAssembly) }
$growthArgs += '-r:' + (Join-Path $growthValidation 'nunit.framework.dll')
$growthCompileFiles = @($growthSources.Values | Sort-Object)
$growthCompileFiles += Join-Path $PatchRoot 'Tests/EditMode/ShipBoardingGrowthTests.cs'
$growthCompileFiles += Join-Path $growthValidation 'RunShipBoardingGrowthTests.cs'
$growthCompileFiles | ForEach-Object { Get-FileHash -LiteralPath $_ -Algorithm SHA256 } | Select-Object Path,Hash | Export-Csv -LiteralPath (Join-Path $growthValidation 'boarding-growth-source-hashes.csv') -NoTypeInformation -Encoding utf8
$growthArgs += $growthCompileFiles
& (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @growthArgs 2>&1 | Tee-Object -FilePath (Join-Path $growthValidation 'boarding-growth-compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Ship boarding growth test compile failed.' }
& (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $growthExe 2>&1 | Tee-Object -FilePath (Join-Path $growthValidation 'boarding-growth-test.log')
if ($LASTEXITCODE -ne 0) { throw 'Ship boarding growth tests failed.' }
