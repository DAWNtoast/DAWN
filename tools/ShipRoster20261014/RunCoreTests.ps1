param([string]$Output = 'D:/Game/Artifacts/ShipRoster20261014/CoreTests')
$ErrorActionPreference = 'Stop'
$shipWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$shipBuild = Join-Path $Output 'Build'
New-Item -ItemType Directory -Force -Path $shipBuild | Out-Null
$shipUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
$shipFramework = Join-Path $shipUnity 'MonoBleedingEdge/lib/mono/4.7.2-api'
$shipNunit = Get-ChildItem -LiteralPath (Join-Path $shipWorkspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' |
    ForEach-Object { Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
Copy-Item -LiteralPath $shipNunit -Destination (Join-Path $shipBuild 'nunit.framework.dll')
$shipSources = @(Get-ChildItem -LiteralPath (Join-Path $shipWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
$shipFixtures = @('CoreWarshipRosterTests.cs', 'LegendaryWarshipTests.cs', 'CoreSeaChartProgressTests.cs',
    'CoreCollectionRewardCatalogTests.cs', 'NavalBattleBalanceSimulationTests.cs', 'CoreVoyageSafetyAuditTests.cs')
foreach ($shipFixture in $shipFixtures) {
    $shipSources += Get-Item -LiteralPath (Join-Path $shipWorkspace ('UnityClient/Assets/_Project/Tests/EditMode/' + $shipFixture))
}
$shipSources += Get-Item -LiteralPath (Join-Path $shipWorkspace 'UnityClient/Assets/_Project/Tests/EditMode/NavalBattleBalanceSimulator.cs')
$shipSources += Get-Item -LiteralPath (Join-Path $shipWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$shipSources += Get-Item -LiteralPath (Join-Path $shipWorkspace 'tools/CardBalanceAudit/Runner.cs')
$shipExe = Join-Path $shipBuild 'ShipRosterTests.exe'
$shipArgs = @((Join-Path $shipUnity 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$shipExe", "-r:$shipNunit")
foreach ($shipAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Xml.dll')) {
    $shipArgs += '-r:' + (Join-Path $shipFramework $shipAssembly)
}
$shipArgs += $shipSources.FullName
& (Join-Path $shipUnity 'NetCoreRuntime/dotnet.exe') @shipArgs 2>&1 | Tee-Object -FilePath (Join-Path $Output 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Ship roster Core tests compilation failed.' }
& (Join-Path $shipUnity 'MonoBleedingEdge/bin/mono.exe') $shipExe $Output 2>&1 | Tee-Object -FilePath (Join-Path $Output 'tests.log')
if ($LASTEXITCODE -ne 0) { throw 'Ship roster Core tests failed. See tests.log and results.xml.' }
