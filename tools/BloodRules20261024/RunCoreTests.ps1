param(
    [string]$RunTag = 'Current',
    [string[]]$Fixtures = @(
        'CoreBloodNavalRevision20261024Tests', 'CoreBloodBoardingRevisionTests', 'CoreBloodCatalogRevisionTests', 'CoreBloodFieldRevision20261024Tests', 'CoreThunderNavalRevision20261024Tests', 'CoreThunderBoardingRevisionTests', 'CoreThunderCatalogRevisionTests', 'CoreThunderFieldRevision20261024Tests', 'CoreBattleIntentPresentationTests', 'CoreBoardingBossPacingTests', 'CoreNavalMovementRamTests', 'CoreTideNavalRevision20261024Tests', 'CoreTideFieldRevision20261024Tests', 'CoreTideCatalogRevisionTests', 'CoreTideBoardingRevisionTests', 'CoreCurseCatalogRevisionTests', 'CoreCurseNavalRevision20261024Tests', 'CoreCurseBoardingRevisionTests', 'CoreBoardingCurseTideBloodTests',
        'CoreArcaneManaRevisionTests', 'CoreArcaneCatalogRevisionTests', 'CoreArcaneNavalRevision20261024Tests', 'CoreFireRules20261024Tests', 'CoreBattleCardKeywordTextTests', 'CoreBattleStatusPresentationTests',
        'CoreBoardingFireRevisionTests', 'CoreBoardingFireArcaneThunderTests', 'CoreBoardingManuscriptRulesTests', 'CoreBoardingEnemyTests',
        'CoreBoardingEnemySchoolTests', 'CoreBoardingTransactionTests', 'CoreBoardingCardCatalogTests',
        'CoreNavalFieldsTests', 'CoreNavalExpansionCardTests', 'CoreComponentFieldCardsTests',
        'CoreNavalMovementReachTests', 'CoreGuidedBossScriptTests',
        'CoreNavalSixSchoolIntegrationTests', 'BattleMixedBuffCardTests', 'CoreSchoolBattleMechanicsTests', 'CoreSchoolRebalanceRulesTests', 'CoreCardAuthoredDamageTests',
        'CoreBattleCardFaceDescriptionTests', 'CoreBattleCardDescriptionConsistencyTests',
        'CoreCardUpgradeTrailingPlateauTests', 'CoreCardUpgradeLimitTests',
        'CoreBattleCardWordingTests', 'CoreBattleCardIconCostTextTests'
    )
)
$ErrorActionPreference = 'Stop'
$bloodWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$bloodOutput = Join-Path $bloodWorkspace ('Artifacts/BloodRules20261024/' + $RunTag)
$bloodBuild = Join-Path $bloodOutput 'Build'
$bloodUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
$bloodFramework = Join-Path $bloodUnity 'MonoBleedingEdge/lib/mono/4.7.2-api'
$bloodNunit = Get-ChildItem -LiteralPath (Join-Path $bloodWorkspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' |
    ForEach-Object { Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
$bloodTests = Join-Path $bloodWorkspace 'UnityClient/Assets/_Project/Tests/EditMode'
$bloodFixtures = @($Fixtures | ForEach-Object { Get-Item -LiteralPath (Join-Path $bloodTests ($_ + '.cs')) })
$bloodSources = @(Get-ChildItem -LiteralPath (Join-Path $bloodWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
$bloodSources += $bloodFixtures
$bloodSources += Get-Item -LiteralPath (Join-Path $bloodWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$bloodSources += Get-Item -LiteralPath (Join-Path $bloodWorkspace 'tools/CourseTargetLock20261014/CoreRunner.cs')
New-Item -ItemType Directory -Force -Path $bloodBuild | Out-Null
$bloodHashes = @($bloodSources | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$bloodHashes | Export-Csv -LiteralPath (Join-Path $bloodOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
Copy-Item -LiteralPath $bloodNunit -Destination (Join-Path $bloodBuild 'nunit.framework.dll') -Force
$bloodFixtures.Name | Set-Content -LiteralPath (Join-Path $bloodOutput 'fixtures.txt') -Encoding utf8
$bloodExe = Join-Path $bloodBuild 'BloodRulesTests.exe'
$bloodArgs = @((Join-Path $bloodUnity 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$bloodExe", "-r:$bloodNunit")
foreach ($bloodAssembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll')) {
    $bloodArgs += '-r:' + (Join-Path $bloodFramework $bloodAssembly)
}
$bloodArgs += $bloodSources.FullName
& (Join-Path $bloodUnity 'NetCoreRuntime/dotnet.exe') @bloodArgs 2>&1 | Tee-Object -FilePath (Join-Path $bloodOutput 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Blood rules Core compilation failed.' }
& (Join-Path $bloodUnity 'MonoBleedingEdge/bin/mono.exe') $bloodExe $bloodOutput 2>&1 | Tee-Object -FilePath (Join-Path $bloodOutput 'tests.log')
if ($LASTEXITCODE -ne 0) { throw 'Blood rules Core tests failed; see results.xml and failures.txt.' }
foreach ($bloodHash in $bloodHashes) {
    if ((Get-FileHash -LiteralPath $bloodHash.Source -Algorithm SHA256).Hash -ne $bloodHash.SHA256) {
        throw ('Source changed during tests: ' + $bloodHash.Source)
    }
}
Write-Output 'PASS source hashes stable during core regression.'
