param(
    [string]$RunTag = 'Current',
    [string[]]$Fixtures = @(
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
$arcaneWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$arcaneOutput = Join-Path $arcaneWorkspace ('Artifacts/ArcaneRules20261024/' + $RunTag)
$arcaneBuild = Join-Path $arcaneOutput 'Build'
$arcaneUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
$arcaneFramework = Join-Path $arcaneUnity 'MonoBleedingEdge/lib/mono/4.7.2-api'
$arcaneNunit = Get-ChildItem -LiteralPath (Join-Path $arcaneWorkspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' |
    ForEach-Object { Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
$arcaneTests = Join-Path $arcaneWorkspace 'UnityClient/Assets/_Project/Tests/EditMode'
$arcaneFixtures = @($Fixtures | ForEach-Object { Get-Item -LiteralPath (Join-Path $arcaneTests ($_ + '.cs')) })
$arcaneSources = @(Get-ChildItem -LiteralPath (Join-Path $arcaneWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
$arcaneSources += $arcaneFixtures
$arcaneSources += Get-Item -LiteralPath (Join-Path $arcaneWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$arcaneSources += Get-Item -LiteralPath (Join-Path $arcaneWorkspace 'tools/CourseTargetLock20261014/CoreRunner.cs')
New-Item -ItemType Directory -Force -Path $arcaneBuild | Out-Null
Copy-Item -LiteralPath $arcaneNunit -Destination (Join-Path $arcaneBuild 'nunit.framework.dll') -Force
$arcaneFixtures.Name | Set-Content -LiteralPath (Join-Path $arcaneOutput 'fixtures.txt') -Encoding utf8
$arcaneExe = Join-Path $arcaneBuild 'ArcaneRulesTests.exe'
$arcaneArgs = @((Join-Path $arcaneUnity 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$arcaneExe", "-r:$arcaneNunit")
foreach ($arcaneAssembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll')) {
    $arcaneArgs += '-r:' + (Join-Path $arcaneFramework $arcaneAssembly)
}
$arcaneArgs += $arcaneSources.FullName
& (Join-Path $arcaneUnity 'NetCoreRuntime/dotnet.exe') @arcaneArgs 2>&1 | Tee-Object -FilePath (Join-Path $arcaneOutput 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Arcane rules Core compilation failed.' }
& (Join-Path $arcaneUnity 'MonoBleedingEdge/bin/mono.exe') $arcaneExe $arcaneOutput 2>&1 | Tee-Object -FilePath (Join-Path $arcaneOutput 'tests.log')
if ($LASTEXITCODE -ne 0) { throw 'Arcane rules Core tests failed; see results.xml and failures.txt.' }
