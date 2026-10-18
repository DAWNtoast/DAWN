param(
    [string]$Output = 'D:/Game/Artifacts/ShipPortLocation20261014/CoreTests',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [switch]$IncludeVoyageSessionRegression
)
$ErrorActionPreference = 'Stop'
$portWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$portOutput = [IO.Path]::GetFullPath($Output)
$portBuild = Join-Path $portOutput 'Build'
New-Item -ItemType Directory -Force -Path $portBuild | Out-Null
$portFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$portNunit = Get-ChildItem -LiteralPath (Join-Path $portWorkspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' |
    ForEach-Object { Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $portNunit) { throw 'The Unity NUnit package cache is required. No editor process will be started.' }
Copy-Item -LiteralPath $portNunit -Destination (Join-Path $portBuild 'nunit.framework.dll')
$portSources = @(Get-ChildItem -LiteralPath (Join-Path $portWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
$portTestsRoot = Join-Path $portWorkspace 'UnityClient/Assets/_Project/Tests/EditMode'
$portFixtures = @('CoreShipMarketCatalogTests.cs', 'CoreShipPortProgressTests.cs')
if ($IncludeVoyageSessionRegression) { $portFixtures += 'CoreVoyageSessionTests.cs' }
foreach ($portFixture in $portFixtures) {
    $portSources += Get-Item -LiteralPath (Join-Path $portTestsRoot $portFixture)
}
$portSources += Get-Item -LiteralPath (Join-Path $portWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$portSources += Get-Item -LiteralPath (Join-Path $portWorkspace 'tools/CardBalanceAudit/Runner.cs')
$portHashes = @($portSources | ForEach-Object { [pscustomobject]@{ Source=$_.FullName; SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } })
$portHashes | Export-Csv -LiteralPath (Join-Path $portOutput 'source-hashes.csv') -Encoding utf8 -NoTypeInformation
$portFixtures | Set-Content -LiteralPath (Join-Path $portOutput 'fixtures.txt') -Encoding utf8
$portExe = Join-Path $portBuild 'ShipPortLocationTests.exe'
$portArgs = @((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$portExe", "-r:$portNunit")
foreach ($portAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Xml.dll')) {
    $portArgs += '-r:' + (Join-Path $portFramework $portAssembly)
}
$portArgs += $portSources.FullName
& (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @portArgs 2>&1 | Tee-Object -FilePath (Join-Path $portOutput 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Ship port location Core tests compilation failed.' }
foreach ($portHash in $portHashes) {
    if ((Get-FileHash -LiteralPath $portHash.Source -Algorithm SHA256).Hash -ne $portHash.SHA256) {
        throw "Source changed during compilation: $($portHash.Source). Run again after the edit finishes."
    }
}
& (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $portExe $portOutput 2>&1 | Tee-Object -FilePath (Join-Path $portOutput 'tests.log')
if ($LASTEXITCODE -ne 0) { throw "Ship port location Core tests failed. See $portOutput/results.xml and tests.log." }
