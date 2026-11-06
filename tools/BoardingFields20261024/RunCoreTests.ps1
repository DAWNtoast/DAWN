param(
    [string]$RunTag = 'Current',
    [string[]]$Fixtures = @(
        'CoreBoardingFieldInheritanceTests', 'CoreBoardingFieldEnemyTests', 'CoreBoardingInheritedFieldEffectsTests', 'CoreBloodNavalRevision20261024Tests', 'CoreBloodBoardingRevisionTests', 'CoreBloodCatalogRevisionTests', 'CoreBloodFieldRevision20261024Tests', 'CoreThunderNavalRevision20261024Tests', 'CoreThunderBoardingRevisionTests', 'CoreThunderCatalogRevisionTests', 'CoreThunderFieldRevision20261024Tests', 'CoreBattleIntentPresentationTests', 'CoreBoardingBossPacingTests', 'CoreNavalMovementRamTests', 'CoreTideNavalRevision20261024Tests', 'CoreTideFieldRevision20261024Tests', 'CoreTideCatalogRevisionTests', 'CoreTideBoardingRevisionTests', 'CoreCurseCatalogRevisionTests', 'CoreCurseNavalRevision20261024Tests', 'CoreCurseBoardingRevisionTests', 'CoreBoardingCurseTideBloodTests',
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
$boardingWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$boardingOutput = Join-Path $boardingWorkspace ('Artifacts/BoardingFields20261024/' + $RunTag)
$boardingBuild = Join-Path $boardingOutput 'Build'
$boardingUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
$boardingFramework = Join-Path $boardingUnity 'MonoBleedingEdge/lib/mono/4.7.2-api'
$boardingNunit = Get-ChildItem -LiteralPath (Join-Path $boardingWorkspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' |
    ForEach-Object { Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
$boardingTests = Join-Path $boardingWorkspace 'UnityClient/Assets/_Project/Tests/EditMode'
$boardingFixtures = @($Fixtures | ForEach-Object { Get-Item -LiteralPath (Join-Path $boardingTests ($_ + '.cs')) })
$boardingSources = @(Get-ChildItem -LiteralPath (Join-Path $boardingWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
$boardingSources += $boardingFixtures
$boardingSources += Get-Item -LiteralPath (Join-Path $boardingWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$boardingSources += Get-Item -LiteralPath (Join-Path $boardingWorkspace 'tools/CourseTargetLock20261014/CoreRunner.cs')
New-Item -ItemType Directory -Force -Path $boardingBuild | Out-Null
$boardingHashes = @($boardingSources | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$boardingHashes | Export-Csv -LiteralPath (Join-Path $boardingOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
Copy-Item -LiteralPath $boardingNunit -Destination (Join-Path $boardingBuild 'nunit.framework.dll') -Force
$boardingFixtures.Name | Set-Content -LiteralPath (Join-Path $boardingOutput 'fixtures.txt') -Encoding utf8
$boardingExe = Join-Path $boardingBuild 'BoardingFieldsTests.exe'
$boardingArgs = @((Join-Path $boardingUnity 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$boardingExe", "-r:$boardingNunit")
foreach ($boardingAssembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll')) {
    $boardingArgs += '-r:' + (Join-Path $boardingFramework $boardingAssembly)
}
$boardingArgs += $boardingSources.FullName
& (Join-Path $boardingUnity 'NetCoreRuntime/dotnet.exe') @boardingArgs 2>&1 | Tee-Object -FilePath (Join-Path $boardingOutput 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Boarding field rules Core compilation failed.' }
& (Join-Path $boardingUnity 'MonoBleedingEdge/bin/mono.exe') $boardingExe $boardingOutput 2>&1 | Tee-Object -FilePath (Join-Path $boardingOutput 'tests.log')
if ($LASTEXITCODE -ne 0) { throw 'Boarding field rules Core tests failed; see results.xml and failures.txt.' }
foreach ($boardingHash in $boardingHashes) {
    if ((Get-FileHash -LiteralPath $boardingHash.Source -Algorithm SHA256).Hash -ne $boardingHash.SHA256) {
        throw ('Source changed during tests: ' + $boardingHash.Source)
    }
}
Write-Output 'PASS source hashes stable during core regression.'
