param(
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$RunTag = 'CurrentCore',
    [string]$SourceSnapshot = '',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)

$ErrorActionPreference = 'Stop'
$navalWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$navalSourceRoot = $navalWorkspace
if ($SourceSnapshot) {
    $navalSourceRoot = [IO.Path]::GetFullPath($SourceSnapshot)
    if (Test-Path -LiteralPath (Join-Path $navalSourceRoot 'Source/UnityClient')) {
        $navalSourceRoot = Join-Path $navalSourceRoot 'Source'
    }
}
$navalOutput = Join-Path $navalWorkspace ('Artifacts/NavalDifficulty20261016/' + $RunTag)
if (Test-Path -LiteralPath $navalOutput) { throw "RunTag already exists; choose a new tag: $navalOutput" }
$navalBuild = Join-Path $navalOutput 'Build'
$navalTests = Join-Path $navalWorkspace 'UnityClient/Assets/_Project/Tests/EditMode'
$navalFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$navalNunit = Get-ChildItem -LiteralPath (Join-Path $navalWorkspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' |
    ForEach-Object { Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $navalNunit) { throw 'The existing local Unity NUnit cache is required.' }

$navalFixtures = @(Get-ChildItem -LiteralPath $navalTests -Filter 'CoreNaval*.cs' | Sort-Object Name)
foreach ($navalName in @(
    'NavalBoardingApproachTests.cs', 'RamBoardingTransitionTests.cs', 'StagedNavalBattleTests.cs',
    'CoreBoardingBossPacingTests.cs', 'CoreTrainingBattleTests.cs', 'CoreTrainingBattleContinuityTests.cs',
    'CoreGuidedBossScriptTests.cs', 'LegendaryWarshipTests.cs', 'BattleTurnCostAndInitiativeTests.cs'
)) { $navalFixtures += Get-Item -LiteralPath (Join-Path $navalTests $navalName) }
foreach ($navalFixture in $navalFixtures) {
    if ([IO.File]::ReadAllText($navalFixture.FullName) -match 'UnityEngine|UnityEditor|ShatteredSail.Presentation') {
        throw "Fixture requires Unity and cannot run in this Core runner: $($navalFixture.Name)"
    }
}
$navalSources = @(Get-ChildItem -LiteralPath (Join-Path $navalSourceRoot 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
$navalSources += $navalFixtures
$navalSources += Get-Item -LiteralPath (Join-Path $navalSourceRoot 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$navalSources += Get-Item -LiteralPath (Join-Path $navalWorkspace 'tools/CourseTargetLock20261014/CoreRunner.cs')
New-Item -ItemType Directory -Force -Path $navalBuild | Out-Null
Copy-Item -LiteralPath $navalNunit -Destination (Join-Path $navalBuild 'nunit.framework.dll')
$navalHashes = @($navalSources | ForEach-Object { [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } })
$navalHashes | Export-Csv -LiteralPath (Join-Path $navalOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
$navalFixtures.Name | Set-Content -LiteralPath (Join-Path $navalOutput 'fixtures.txt') -Encoding utf8
@"
Complete selected current pure Core NUnit fixtures against Core Runtime and CrewNarrativeCatalog from:
$navalSourceRoot
Uses the local cached NUnit assembly. No Unity editor, scenes, or player profile are touched.
Fixture bodies are compiled unchanged. Historical failures remain in results.xml/failures.txt for honest comparison.
"@ | Set-Content -LiteralPath (Join-Path $navalOutput 'scope.txt') -Encoding utf8
$navalExe = Join-Path $navalBuild 'NavalDifficultyTests.exe'
$navalArguments = @((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$navalExe", "-r:$navalNunit")
foreach ($navalAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Xml.dll')) {
    $navalArguments += '-r:' + (Join-Path $navalFramework $navalAssembly)
}
$navalArguments += $navalSources.FullName
& (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @navalArguments 2>&1 | Tee-Object -FilePath (Join-Path $navalOutput 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Naval Core regression compilation failed.' }
foreach ($navalHash in $navalHashes) {
    if ((Get-FileHash -LiteralPath $navalHash.Source -Algorithm SHA256).Hash -ne $navalHash.SHA256) {
        throw "Source changed during compilation: $($navalHash.Source)"
    }
}
& (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $navalExe $navalOutput 2>&1 | Tee-Object -FilePath (Join-Path $navalOutput 'tests.log')
if ($LASTEXITCODE -ne 0) { throw "Naval Core regression tests failed; inspect $navalOutput/results.xml and failures.txt." }
