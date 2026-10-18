param(
    [string]$RunTag = 'CurrentCore',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [string[]]$AdditionalFixtures = @()
)
$ErrorActionPreference = 'Stop'
if ($RunTag -notmatch '^[A-Za-z0-9_-]+$') { throw 'RunTag must be a simple identifier.' }
$boardingWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$boardingOutput = Join-Path $boardingWorkspace ('Artifacts/BoardingEnemyBalance20261015/' + $RunTag)
$boardingBuild = Join-Path $boardingOutput 'Build'
New-Item -ItemType Directory -Force -Path $boardingBuild | Out-Null
$boardingFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$boardingNunit = Get-ChildItem -LiteralPath (Join-Path $boardingWorkspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' |
    ForEach-Object { Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $boardingNunit) { throw 'The existing local Unity NUnit cache is required.' }
Copy-Item -LiteralPath $boardingNunit -Destination (Join-Path $boardingBuild 'nunit.framework.dll') -Force
$boardingSources = @(Get-ChildItem -LiteralPath (Join-Path $boardingWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
$boardingFixtures = @(
    'CoreBoardingBaselineTests.cs',
    'CoreBoardingCardCatalogTests.cs',
    'CoreBoardingTransactionTests.cs',
    'CoreBoardingFireArcaneThunderTests.cs',
    'CoreBoardingCurseTideBloodTests.cs',
    'ShipBoardingGrowthTests.cs',
    'NavalBoardingApproachTests.cs',
    'RamBoardingTransitionTests.cs',
    'BoardingStageBlockTests.cs',
    'BoardingMedicineTests.cs',
    'CoreBattleDeckInspectionTests.cs',
    'CoreTrainingBattleTests.cs',
    'CoreTrainingBattleContinuityTests.cs',
    'CoreGuidedBossScriptTests.cs'
) + $AdditionalFixtures | Select-Object -Unique
foreach ($boardingFixture in $boardingFixtures) {
    if ([IO.Path]::GetFileName($boardingFixture) -ne $boardingFixture -or -not $boardingFixture.EndsWith('.cs')) {
        throw "Fixture must name one C# file in Tests/EditMode: $boardingFixture"
    }
    $boardingFile = Get-Item -LiteralPath (Join-Path $boardingWorkspace ('UnityClient/Assets/_Project/Tests/EditMode/' + $boardingFixture))
    if ([IO.File]::ReadAllText($boardingFile.FullName) -match 'UnityEngine|UnityEditor|ShatteredSail.Presentation') {
        throw "Use Unity for this whole fixture: $boardingFixture"
    }
    $boardingSources += $boardingFile
}
$boardingSources += Get-Item -LiteralPath (Join-Path $boardingWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$boardingSources += Get-Item -LiteralPath (Join-Path $boardingWorkspace 'tools/CourseTargetLock20261014/CoreRunner.cs')
$boardingHashes = @($boardingSources | ForEach-Object { [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } })
$boardingHashes | Export-Csv -LiteralPath (Join-Path $boardingOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
$boardingFixtures | Set-Content -LiteralPath (Join-Path $boardingOutput 'fixtures.txt') -Encoding utf8
'Full current Core sources and complete selected NUnit fixtures, executed by the real cached NUnit assembly runner. No Unity editor, scene generation, or player profile changes.' | Set-Content -LiteralPath (Join-Path $boardingOutput 'scope.txt') -Encoding utf8
$boardingExe = Join-Path $boardingBuild 'BoardingEnemyBalanceTests.exe'
$boardingArguments = @((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$boardingExe", "-r:$boardingNunit")
foreach ($boardingAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Xml.dll')) {
    $boardingArguments += '-r:' + (Join-Path $boardingFramework $boardingAssembly)
}
$boardingArguments += $boardingSources.FullName
& (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @boardingArguments 2>&1 | Tee-Object -FilePath (Join-Path $boardingOutput 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Boarding enemy balance Core compilation failed.' }
foreach ($boardingHash in $boardingHashes) {
    if ((Get-FileHash -LiteralPath $boardingHash.Source -Algorithm SHA256).Hash -ne $boardingHash.SHA256) {
        throw "Source changed during compilation: $($boardingHash.Source)"
    }
}
& (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $boardingExe $boardingOutput 2>&1 | Tee-Object -FilePath (Join-Path $boardingOutput 'tests.log')
if ($LASTEXITCODE -ne 0) { throw "Boarding enemy balance tests failed; inspect $boardingOutput/results.xml and failures.txt." }
