param(
    [string]$RunTag = 'Current',
    [string[]]$Fixtures = @(
        'CoreTideNavalRevision20261024Tests', 'CoreTideFieldRevision20261024Tests', 'CoreTideCatalogRevisionTests', 'CoreTideBoardingRevisionTests', 'CoreCurseCatalogRevisionTests', 'CoreCurseNavalRevision20261024Tests', 'CoreCurseBoardingRevisionTests', 'CoreBoardingCurseTideBloodTests',
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
$tideWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$tideOutput = Join-Path $tideWorkspace ('Artifacts/TideRules20261024/' + $RunTag)
$tideBuild = Join-Path $tideOutput 'Build'
$tideUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
$tideFramework = Join-Path $tideUnity 'MonoBleedingEdge/lib/mono/4.7.2-api'
$tideNunit = Get-ChildItem -LiteralPath (Join-Path $tideWorkspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' |
    ForEach-Object { Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
$tideTests = Join-Path $tideWorkspace 'UnityClient/Assets/_Project/Tests/EditMode'
$tideFixtures = @($Fixtures | ForEach-Object { Get-Item -LiteralPath (Join-Path $tideTests ($_ + '.cs')) })
$tideSources = @(Get-ChildItem -LiteralPath (Join-Path $tideWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
$tideSources += $tideFixtures
$tideSources += Get-Item -LiteralPath (Join-Path $tideWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$tideSources += Get-Item -LiteralPath (Join-Path $tideWorkspace 'tools/CourseTargetLock20261014/CoreRunner.cs')
New-Item -ItemType Directory -Force -Path $tideBuild | Out-Null
$tideHashes = @($tideSources | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$tideHashes | Export-Csv -LiteralPath (Join-Path $tideOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
Copy-Item -LiteralPath $tideNunit -Destination (Join-Path $tideBuild 'nunit.framework.dll') -Force
$tideFixtures.Name | Set-Content -LiteralPath (Join-Path $tideOutput 'fixtures.txt') -Encoding utf8
$tideExe = Join-Path $tideBuild 'TideRulesTests.exe'
$tideArgs = @((Join-Path $tideUnity 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$tideExe", "-r:$tideNunit")
foreach ($tideAssembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll')) {
    $tideArgs += '-r:' + (Join-Path $tideFramework $tideAssembly)
}
$tideArgs += $tideSources.FullName
& (Join-Path $tideUnity 'NetCoreRuntime/dotnet.exe') @tideArgs 2>&1 | Tee-Object -FilePath (Join-Path $tideOutput 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Tide rules Core compilation failed.' }
& (Join-Path $tideUnity 'MonoBleedingEdge/bin/mono.exe') $tideExe $tideOutput 2>&1 | Tee-Object -FilePath (Join-Path $tideOutput 'tests.log')
if ($LASTEXITCODE -ne 0) { throw 'Tide rules Core tests failed; see results.xml and failures.txt.' }
foreach ($tideHash in $tideHashes) {
    if ((Get-FileHash -LiteralPath $tideHash.Source -Algorithm SHA256).Hash -ne $tideHash.SHA256) {
        throw ('Source changed during tests: ' + $tideHash.Source)
    }
}
Write-Output 'PASS source hashes stable during core regression.'
