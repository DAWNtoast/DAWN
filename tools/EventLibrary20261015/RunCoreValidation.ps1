param(
    [string]$Output = 'D:/Game/Artifacts/EventLibrary20261015/Core',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [string[]]$AdditionalFixtures=@(),
    [switch]$ProbeOnly
)
$ErrorActionPreference = 'Stop'
$eventWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$eventOutput = [IO.Path]::GetFullPath($Output)
$eventBuild = Join-Path $eventOutput 'Build'
New-Item -ItemType Directory -Force -Path $eventBuild | Out-Null
$eventFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$eventNunit = Get-ChildItem -LiteralPath (Join-Path $eventWorkspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' |
    ForEach-Object { Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $eventNunit) { throw 'Unity NUnit package cache is required; no Unity editor process is started.' }
Copy-Item -LiteralPath $eventNunit -Destination (Join-Path $eventBuild 'nunit.framework.dll') -Force
$eventSources = @(Get-ChildItem -LiteralPath (Join-Path $eventWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
$eventTestsRoot = Join-Path $eventWorkspace 'UnityClient/Assets/_Project/Tests/EditMode'
$eventFixtures = @('CoreGuidedBossScriptTests.cs', 'CoreGuidedTutorialVoyageTests.cs', 'CoreSeaChartProgressTests.cs', 'CoreSeaChartEventPacingTests.cs', 'CoreVoyageEventCatalogTests.cs', 'VoyageEventDistributionTests.cs', 'CoreVoyageDestinationTests.cs',
    'CoreVoyageEventConditionTests.cs', 'CoreVoyageSafetyAuditTests.cs', 'CoreVoyageSessionTests.cs',
    'EncounterDiscoveryRulesTests.cs', 'TutorialVoyageSessionTests.cs', 'VoyageLookoutProjectorTests.cs',
    'CoreVoyageEventExpansionV2Tests.cs', 'CoreVoyageExplorationTests.cs', 'CoreVoyageExplorationRevisitTests.cs',
    'CoreSailingDurationTests.cs', 'CoreTreasureExpeditionCatalogTests.cs',
    'CoreTreasureExpeditionMechanicsTests.cs', 'CoreTreasureContentVersionTests.cs')
$eventFixtures += $AdditionalFixtures
if (-not $ProbeOnly) {
    foreach ($eventFixture in $eventFixtures) {
        $eventFile = Get-Item -LiteralPath (Join-Path $eventTestsRoot $eventFixture)
        if ([IO.File]::ReadAllText($eventFile.FullName) -match 'UnityEngine|UnityEditor|ShatteredSail.Presentation') {
            throw "Fixture gained Unity dependencies; do not silently strip tests: $eventFixture"
        }
        $eventSources += $eventFile
    }
}
$eventSources += Get-Item -LiteralPath (Join-Path $eventWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$eventSources += Get-Item -LiteralPath (Join-Path $PSScriptRoot 'CoreRunner.cs')
$eventHashes = @($eventSources | ForEach-Object { [pscustomobject]@{ Source=$_.FullName; SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } })
$eventHashes | Export-Csv -LiteralPath (Join-Path $eventOutput 'source-hashes.csv') -Encoding utf8 -NoTypeInformation
$eventFixtures | Set-Content -LiteralPath (Join-Path $eventOutput 'fixtures.txt') -Encoding utf8
$eventExe = Join-Path $eventBuild 'EventLibraryValidation.exe'
$eventArguments = @((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$eventExe", "-r:$eventNunit")
foreach ($eventAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Xml.dll')) {
    $eventArguments += '-r:' + (Join-Path $eventFramework $eventAssembly)
}
$eventArguments += $eventSources.FullName
& (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @eventArguments 2>&1 | Tee-Object -FilePath (Join-Path $eventOutput 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Voyage event Core compilation failed.' }
foreach ($eventHash in $eventHashes) {
    if ((Get-FileHash -LiteralPath $eventHash.Source -Algorithm SHA256).Hash -ne $eventHash.SHA256) {
        throw "Source changed during compilation: $($eventHash.Source). Wait for the edit to finish and run again."
    }
}
& (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $eventExe $eventOutput $PSScriptRoot 2>&1 | Tee-Object -FilePath (Join-Path $eventOutput 'tests.log')
if ($LASTEXITCODE -ne 0) { throw "Voyage event Core checks failed. See $eventOutput/results.xml and tests.log." }
