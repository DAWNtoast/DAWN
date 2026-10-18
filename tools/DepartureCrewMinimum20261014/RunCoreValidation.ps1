param([string]$RunTag = 'core1', [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data')
$ErrorActionPreference = 'Stop'
if ($RunTag -notmatch '^[A-Za-z0-9_-]+$') { throw 'RunTag must be a simple identifier.' }
$crewWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$crewOutput = Join-Path $crewWorkspace ('Artifacts/DepartureCrewMinimum20261014/' + $RunTag)
if (Test-Path -LiteralPath $crewOutput) { throw 'Use a fresh RunTag.' }
$crewBuild = Join-Path $crewOutput 'Build'
New-Item -ItemType Directory -Force -Path $crewBuild | Out-Null
$crewFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$crewNunit = Get-ChildItem -LiteralPath (Join-Path $crewWorkspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' |
    ForEach-Object { Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $crewNunit) { throw 'The existing local Unity NUnit cache is required.' }
Copy-Item -LiteralPath $crewNunit -Destination (Join-Path $crewBuild 'nunit.framework.dll')
$crewSources = @(Get-ChildItem -LiteralPath (Join-Path $crewWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
$crewFixtures = @('DepartureCrewMinimumTests.cs','CrewSpeedRulesTests.cs','NavigatorDepartureRevealTests.cs',
    'CoreVoyageSessionTests.cs','CoreGuidedTutorialVoyageTests.cs','CoreGuidedBossScriptTests.cs','CoreSeaChartProgressTests.cs',
    'SupplyEnduranceTests.cs','BoardingMedicineTests.cs','ShipBoardingGrowthTests.cs','BattleTurnCostAndInitiativeTests.cs',
    'CoreShipMarketCatalogTests.cs','CoreShipPortProgressTests.cs')
foreach ($crewFixture in $crewFixtures) {
    $crewTest = Get-Item -LiteralPath (Join-Path $crewWorkspace ('UnityClient/Assets/_Project/Tests/EditMode/' + $crewFixture))
    if ([IO.File]::ReadAllText($crewTest.FullName) -match 'UnityEngine|UnityEditor|ShatteredSail.Presentation') { throw "Use Unity for this whole fixture: $crewFixture" }
    $crewSources += $crewTest
}
$crewSources += Get-Item -LiteralPath (Join-Path $crewWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$crewSources += Get-Item -LiteralPath (Join-Path $crewWorkspace 'tools/CourseTargetLock20261014/CoreRunner.cs')
$crewHashes = @($crewSources | ForEach-Object { [pscustomobject]@{Source=$_.FullName; SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} })
$crewHashes | Export-Csv -LiteralPath (Join-Path $crewOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
$crewFixtures | Set-Content -LiteralPath (Join-Path $crewOutput 'fixtures.txt') -Encoding utf8
$crewExe = Join-Path $crewBuild 'DepartureCrewMinimumTests.exe'
$crewArguments = @((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'),'-nologo','-target:exe','-langversion:9','-noconfig','-nostdlib',"-out:$crewExe","-r:$crewNunit")
foreach ($crewAssembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll')) { $crewArguments += '-r:' + (Join-Path $crewFramework $crewAssembly) }
$crewArguments += $crewSources.FullName
& (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @crewArguments 2>&1 | Tee-Object -FilePath (Join-Path $crewOutput 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Crew minimum Core compilation failed.' }
foreach ($crewHash in $crewHashes) { if ((Get-FileHash -LiteralPath $crewHash.Source -Algorithm SHA256).Hash -ne $crewHash.SHA256) { throw "Source changed during compilation: $($crewHash.Source)" } }
& (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $crewExe $crewOutput 2>&1 | Tee-Object -FilePath (Join-Path $crewOutput 'tests.log')
if ($LASTEXITCODE -ne 0) { throw 'Crew minimum tests failed; inspect results.xml and failures.txt.' }
