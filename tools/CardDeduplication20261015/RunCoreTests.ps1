param([string]$Output = 'D:/Game/Artifacts/CardDeduplication20261015/CoreTests')
$ErrorActionPreference = 'Stop'
$dedupWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$dedupBuild = Join-Path $Output 'Build'
New-Item -ItemType Directory -Force -Path $dedupBuild | Out-Null
$dedupUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
$dedupFramework = Join-Path $dedupUnity 'MonoBleedingEdge/lib/mono/4.7.2-api'
$dedupNunit = Get-ChildItem -LiteralPath (Join-Path $dedupWorkspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' |
    ForEach-Object { Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $dedupNunit) { throw 'The project NUnit framework was not found.' }
Copy-Item -LiteralPath $dedupNunit -Destination (Join-Path $dedupBuild 'nunit.framework.dll')
$dedupSources = @(Get-ChildItem -LiteralPath (Join-Path $dedupWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
# CoreVoyageCheckpointTests uses UnityEngine.JsonUtility and must run in Unity.
# The new fixture independently exercises deck/upgrade checkpoint DTO migration in pure Core.
foreach ($dedupFixture in @(
    'CoreBattleCardDeduplicationTests.cs',
    'CoreNavalCannonRangeTests.cs',
    'CoreNavalTacticsTests.cs',
    'CoreNavalActionMovementTests.cs',
    'CoreGuidedBossScriptTests.cs',
    'CoreTrainingBattleTests.cs',
    'CoreBaseBattleDeckRulesTests.cs',
    'CoreCardUpgradeLimitTests.cs',
    'CoreCardUpgradeTrailingPlateauTests.cs',
    'BattleCardDefinitionCacheTests.cs',
    'CoreBoardingCardCatalogTests.cs'
)) {
    $dedupSources += Get-Item -LiteralPath (Join-Path $dedupWorkspace ('UnityClient/Assets/_Project/Tests/EditMode/' + $dedupFixture))
}
$dedupSources += Get-Item -LiteralPath (Join-Path $dedupWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$dedupSources += Get-Item -LiteralPath (Join-Path $dedupWorkspace 'tools/CardBalanceAudit/Runner.cs')
$dedupExe = Join-Path $dedupBuild 'CardDeduplicationTests.exe'
$dedupArgs = @((Join-Path $dedupUnity 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$dedupExe", "-r:$dedupNunit")
foreach ($dedupAssembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll')) {
    $dedupArgs += '-r:' + (Join-Path $dedupFramework $dedupAssembly)
}
$dedupArgs += $dedupSources.FullName
& (Join-Path $dedupUnity 'NetCoreRuntime/dotnet.exe') @dedupArgs 2>&1 | Tee-Object -FilePath (Join-Path $Output 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Card deduplication Core compilation failed.' }
& (Join-Path $dedupUnity 'MonoBleedingEdge/bin/mono.exe') $dedupExe $Output 2>&1 | Tee-Object -FilePath (Join-Path $Output 'tests.log')
if ($LASTEXITCODE -ne 0) { throw 'Card deduplication Core tests failed. See tests.log and results.xml.' }
