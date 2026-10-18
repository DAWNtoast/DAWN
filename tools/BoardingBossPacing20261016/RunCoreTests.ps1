param(
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$RunTag = 'CurrentCore',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)

$ErrorActionPreference = 'Stop'
$bossWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$bossOutput = Join-Path $bossWorkspace ('Artifacts/BoardingBossPacing20261016/' + $RunTag)
$bossBuild = Join-Path $bossOutput 'Build'
$bossTests = Join-Path $bossWorkspace 'UnityClient/Assets/_Project/Tests/EditMode'
$bossFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$bossNunit = Get-ChildItem -LiteralPath (Join-Path $bossWorkspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' |
    ForEach-Object { Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $bossNunit) { throw 'The existing local Unity NUnit cache is required.' }

$bossFixtures = @(Get-ChildItem -LiteralPath $bossTests -Filter 'CoreBoarding*.cs' | Sort-Object Name)
foreach ($bossName in @(
    'ShipBoardingGrowthTests.cs', 'BoardingMedicineTests.cs',
    'NavalBoardingApproachTests.cs', 'RamBoardingTransitionTests.cs', 'BoardingStageBlockTests.cs',
    'CoreBattleDeckInspectionTests.cs', 'CoreTrainingBattleTests.cs',
    'CoreTrainingBattleContinuityTests.cs', 'CoreGuidedBossScriptTests.cs'
)) {
    $bossFixtures += Get-Item -LiteralPath (Join-Path $bossTests $bossName)
}
foreach ($bossFixture in $bossFixtures) {
    if ([IO.File]::ReadAllText($bossFixture.FullName) -match 'UnityEngine|UnityEditor|ShatteredSail.Presentation') {
        throw "Fixture requires Unity and cannot run in this Core runner: $($bossFixture.Name)"
    }
}
$bossSources = @(Get-ChildItem -LiteralPath (Join-Path $bossWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
$bossSources += $bossFixtures
$bossSources += Get-Item -LiteralPath (Join-Path $bossWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$bossSources += Get-Item -LiteralPath (Join-Path $bossWorkspace 'tools/CourseTargetLock20261014/CoreRunner.cs')

New-Item -ItemType Directory -Force -Path $bossBuild | Out-Null
Copy-Item -LiteralPath $bossNunit -Destination (Join-Path $bossBuild 'nunit.framework.dll') -Force
$bossHashes = @($bossSources | ForEach-Object { [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } })
$bossHashes | Export-Csv -LiteralPath (Join-Path $bossOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
$bossFixtures.Name | Set-Content -LiteralPath (Join-Path $bossOutput 'fixtures.txt') -Encoding utf8
@'
Complete current Core Runtime sources and complete selected NUnit fixtures, using the existing cached NUnit runner.
This script does not start Unity, generate scenes, or modify a player profile.
GameplayBalanceTests.cs is excluded because its ComponentArtCatalog/PortComponentShopCatalog test requires actual Unity Resources; no test body is removed or replaced with a stub.
'@ | Set-Content -LiteralPath (Join-Path $bossOutput 'scope.txt') -Encoding utf8

$bossExe = Join-Path $bossBuild 'BoardingBossPacingTests.exe'
$bossArguments = @((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$bossExe", "-r:$bossNunit")
foreach ($bossAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Xml.dll')) {
    $bossArguments += '-r:' + (Join-Path $bossFramework $bossAssembly)
}
$bossArguments += $bossSources.FullName
& (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @bossArguments 2>&1 | Tee-Object -FilePath (Join-Path $bossOutput 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Boarding Boss pacing Core compilation failed.' }
foreach ($bossHash in $bossHashes) {
    if ((Get-FileHash -LiteralPath $bossHash.Source -Algorithm SHA256).Hash -ne $bossHash.SHA256) {
        throw "Source changed during compilation: $($bossHash.Source)"
    }
}
& (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $bossExe $bossOutput 2>&1 | Tee-Object -FilePath (Join-Path $bossOutput 'tests.log')
if ($LASTEXITCODE -ne 0) { throw "Boarding Boss pacing tests failed; inspect $bossOutput/results.xml and failures.txt." }
