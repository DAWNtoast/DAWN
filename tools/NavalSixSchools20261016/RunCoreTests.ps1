param([string]$RunTag = 'Current')
$ErrorActionPreference = 'Stop'
$schoolWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$schoolOutput = Join-Path $schoolWorkspace ('Artifacts/NavalSixSchools20261016/' + $RunTag)
$schoolBuild = Join-Path $schoolOutput 'Build'
$schoolUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
$schoolFramework = Join-Path $schoolUnity 'MonoBleedingEdge/lib/mono/4.7.2-api'
$schoolNunit = Get-ChildItem -LiteralPath (Join-Path $schoolWorkspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' |
    ForEach-Object { Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $schoolNunit) { throw 'Existing Unity NUnit cache is required.' }
$schoolTests = Join-Path $schoolWorkspace 'UnityClient/Assets/_Project/Tests/EditMode'
$schoolNames = @(
    'CoreNavalCannonRangeTests.cs', 'CoreNavalTacticsTests.cs', 'CoreNavalActionMovementTests.cs',
    'CoreGuidedBossScriptTests.cs', 'CoreTrainingBattleTests.cs', 'CoreSchoolBattleMechanicsTests.cs',
    'CoreSchoolRebalanceRulesTests.cs', 'CoreCardSchoolUnlockTests.cs', 'CoreBattleCardDeduplicationTests.cs',
    'CoreBattleCardDescriptionConsistencyTests.cs', 'CoreBattleCardFaceDescriptionTests.cs',
    'CoreCardAuthoredDamageTests.cs', 'CoreCardUpgradeLimitTests.cs', 'CoreCardUpgradeTrailingPlateauTests.cs',
    'BattleCardUpgradeTests.cs', 'BattleTurnCostAndInitiativeTests.cs', 'StagedNavalBattleTests.cs',
    'NavalBoardingApproachTests.cs', 'RamBoardingTransitionTests.cs', 'BoardingStageBlockTests.cs',
    'CoreBoardingCardCatalogTests.cs',
    'CoreNavalAttackRangeTests.cs', 'CoreNavalArrivalContactTests.cs',
    'CoreNavalMovementReachTests.cs', 'CoreNavalContactRulesTests.cs'
)
$schoolFixtures = @($schoolNames | ForEach-Object { Get-Item -LiteralPath (Join-Path $schoolTests $_) })
$schoolFixtures += @(Get-ChildItem -LiteralPath $schoolTests -Filter 'CoreNavalField*Tests.cs')
$schoolFixtures += @(Get-ChildItem -LiteralPath $schoolTests -Filter 'CoreNavalExpansion*Tests.cs')
$schoolFixtures += @(Get-ChildItem -LiteralPath $schoolTests -Filter 'CoreNavalSixSchool*Tests.cs')
$schoolFixtures = @($schoolFixtures | Sort-Object FullName -Unique)
foreach ($schoolFixture in $schoolFixtures) {
    if ([IO.File]::ReadAllText($schoolFixture.FullName) -match 'UnityEngine|UnityEditor|ShatteredSail.Presentation') {
        throw "Fixture needs Unity: $($schoolFixture.Name)"
    }
}
$schoolSources = @(Get-ChildItem -LiteralPath (Join-Path $schoolWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
$schoolSources += $schoolFixtures
$schoolSources += Get-Item -LiteralPath (Join-Path $schoolWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$schoolSources += Get-Item -LiteralPath (Join-Path $schoolWorkspace 'tools/CourseTargetLock20261014/CoreRunner.cs')
New-Item -ItemType Directory -Force -Path $schoolBuild | Out-Null
Copy-Item -LiteralPath $schoolNunit -Destination (Join-Path $schoolBuild 'nunit.framework.dll') -Force
$schoolFixtures.Name | Set-Content -LiteralPath (Join-Path $schoolOutput 'fixtures.txt') -Encoding utf8
$schoolExe = Join-Path $schoolBuild 'NavalSixSchoolTests.exe'
$schoolArgs = @((Join-Path $schoolUnity 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$schoolExe", "-r:$schoolNunit")
foreach ($schoolAssembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll')) {
    $schoolArgs += '-r:' + (Join-Path $schoolFramework $schoolAssembly)
}
$schoolArgs += $schoolSources.FullName
& (Join-Path $schoolUnity 'NetCoreRuntime/dotnet.exe') @schoolArgs 2>&1 | Tee-Object -FilePath (Join-Path $schoolOutput 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Naval six-school Core compilation failed.' }
& (Join-Path $schoolUnity 'MonoBleedingEdge/bin/mono.exe') $schoolExe $schoolOutput 2>&1 | Tee-Object -FilePath (Join-Path $schoolOutput 'tests.log')
if ($LASTEXITCODE -ne 0) { throw 'Naval six-school Core tests failed; see results.xml and failures.txt.' }
