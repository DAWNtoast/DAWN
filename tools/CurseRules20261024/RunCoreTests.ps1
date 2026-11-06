param(
    [string]$RunTag = 'Current',
    [string[]]$Fixtures = @(
        'CoreCurseCatalogRevisionTests', 'CoreCurseNavalRevision20261024Tests', 'CoreCurseBoardingRevisionTests', 'CoreBoardingCurseTideBloodTests',
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
$curseWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$curseOutput = Join-Path $curseWorkspace ('Artifacts/CurseRules20261024/' + $RunTag)
$curseBuild = Join-Path $curseOutput 'Build'
$curseUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
$curseFramework = Join-Path $curseUnity 'MonoBleedingEdge/lib/mono/4.7.2-api'
$curseNunit = Get-ChildItem -LiteralPath (Join-Path $curseWorkspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' |
    ForEach-Object { Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
$curseTests = Join-Path $curseWorkspace 'UnityClient/Assets/_Project/Tests/EditMode'
$curseFixtures = @($Fixtures | ForEach-Object { Get-Item -LiteralPath (Join-Path $curseTests ($_ + '.cs')) })
$curseSources = @(Get-ChildItem -LiteralPath (Join-Path $curseWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
$curseSources += $curseFixtures
$curseSources += Get-Item -LiteralPath (Join-Path $curseWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$curseSources += Get-Item -LiteralPath (Join-Path $curseWorkspace 'tools/CourseTargetLock20261014/CoreRunner.cs')
New-Item -ItemType Directory -Force -Path $curseBuild | Out-Null
$curseHashes = @($curseSources | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$curseHashes | Export-Csv -LiteralPath (Join-Path $curseOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
Copy-Item -LiteralPath $curseNunit -Destination (Join-Path $curseBuild 'nunit.framework.dll') -Force
$curseFixtures.Name | Set-Content -LiteralPath (Join-Path $curseOutput 'fixtures.txt') -Encoding utf8
$curseExe = Join-Path $curseBuild 'CurseRulesTests.exe'
$curseArgs = @((Join-Path $curseUnity 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$curseExe", "-r:$curseNunit")
foreach ($curseAssembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll')) {
    $curseArgs += '-r:' + (Join-Path $curseFramework $curseAssembly)
}
$curseArgs += $curseSources.FullName
& (Join-Path $curseUnity 'NetCoreRuntime/dotnet.exe') @curseArgs 2>&1 | Tee-Object -FilePath (Join-Path $curseOutput 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Curse rules Core compilation failed.' }
& (Join-Path $curseUnity 'MonoBleedingEdge/bin/mono.exe') $curseExe $curseOutput 2>&1 | Tee-Object -FilePath (Join-Path $curseOutput 'tests.log')
if ($LASTEXITCODE -ne 0) { throw 'Curse rules Core tests failed; see results.xml and failures.txt.' }
foreach ($curseHash in $curseHashes) {
    if ((Get-FileHash -LiteralPath $curseHash.Source -Algorithm SHA256).Hash -ne $curseHash.SHA256) {
        throw ('Source changed during tests: ' + $curseHash.Source)
    }
}
Write-Output 'PASS source hashes stable during core regression.'
