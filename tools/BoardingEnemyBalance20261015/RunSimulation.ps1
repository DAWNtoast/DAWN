param(
    [int]$Seeds = 16,
    [string]$Output = 'D:/Game/UnityClient/Artifacts/BoardingEnemyBalance20261015/Simulation',
    [ValidateSet('Legacy','Full','Progression')][string]$Scope = 'Legacy',
    [ValidateSet('Combat','Observe')][string]$Mode = 'Combat',
    [ValidateRange(0,256)][int]$TraceSeeds = 1,
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$simulationWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$simulationOutput = [IO.Path]::GetFullPath($Output)
$simulationSnapshot = Join-Path $simulationOutput 'CoreSnapshot'
$simulationBuild = Join-Path $simulationOutput 'Build'
New-Item -ItemType Directory -Force -Path $simulationOutput,$simulationSnapshot,$simulationBuild | Out-Null
$simulationSources = @(Get-ChildItem -LiteralPath (Join-Path $simulationWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs')
$simulationSources += Get-Item -LiteralPath (Join-Path $simulationWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$simulationHashes = foreach ($simulationSource in $simulationSources) {
    $simulationCopy = Join-Path $simulationSnapshot $simulationSource.Name
    Copy-Item -LiteralPath $simulationSource.FullName -Destination $simulationCopy
    $simulationOriginalHash = (Get-FileHash -LiteralPath $simulationSource.FullName -Algorithm SHA256).Hash
    $simulationCopyHash = (Get-FileHash -LiteralPath $simulationCopy -Algorithm SHA256).Hash
    if ($simulationOriginalHash -ne $simulationCopyHash) { throw "Snapshot mismatch: $($simulationSource.Name)" }
    [PSCustomObject]@{ File=$simulationSource.Name; Source=$simulationSource.FullName; SHA256=$simulationCopyHash }
}
$simulationHashes | Export-Csv -LiteralPath (Join-Path $simulationOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SimulateBoarding.cs') -Destination (Join-Path $simulationBuild 'SimulateBoarding.cs')
Copy-Item -LiteralPath $PSCommandPath -Destination (Join-Path $simulationBuild 'RunSimulation.ps1')
$simulationFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$simulationExecutable = Join-Path $simulationBuild 'SimulateBoarding.exe'
$simulationArgs = @((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$simulationExecutable")
foreach ($simulationAssembly in @('mscorlib.dll','System.dll','System.Core.dll')) { $simulationArgs += '-r:' + (Join-Path $simulationFramework $simulationAssembly) }
$simulationArgs += @(Get-ChildItem -LiteralPath $simulationSnapshot -Filter '*.cs' | ForEach-Object FullName)
$simulationArgs += Join-Path $simulationBuild 'SimulateBoarding.cs'
& (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @simulationArgs 2>&1 | Tee-Object -FilePath (Join-Path $simulationOutput 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Boarding simulation compilation failed.' }
& (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $simulationExecutable $Seeds $simulationOutput $Scope $Mode $TraceSeeds 2>&1 | Tee-Object -FilePath (Join-Path $simulationOutput 'simulation.log')
if ($LASTEXITCODE -ne 0) { throw 'Boarding simulation failed.' }
