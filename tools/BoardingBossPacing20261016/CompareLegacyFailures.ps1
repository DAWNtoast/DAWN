param([string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$legacyWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$legacyOutput = Join-Path $legacyWorkspace 'Artifacts/BoardingBossPacing20261016/LegacyFailureComparison'
$legacyBuild = Join-Path $legacyOutput 'Build'
New-Item -ItemType Directory -Force -Path $legacyBuild | Out-Null
$legacyNunit = Get-ChildItem -LiteralPath (Join-Path $legacyWorkspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' |
    ForEach-Object { Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
Copy-Item -LiteralPath $legacyNunit -Destination (Join-Path $legacyBuild 'nunit.framework.dll') -Force
$legacySources = @(Get-ChildItem -LiteralPath (Join-Path $legacyWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Where-Object Name -ne 'CoreNavalBattleCombat.cs')
$legacyOriginal = [IO.File]::ReadAllText((Join-Path $legacyWorkspace 'UnityClient/Assets/_Project/Core/Runtime/CoreNavalBattleCombat.cs'))
$legacyAnchor = 'CoreBoardingEnemyCatalog.ForEncounter(encounter.Id)'
if (-not $legacyOriginal.Contains($legacyAnchor)) { throw 'Expected encounter dispatch changed; review comparison.' }
$legacyReplacement = $legacyOriginal.Replace($legacyAnchor, 'CoreBoardingEnemyCatalog.ForSchool(CoreBoardingEnemyCatalog.ResolveSchool(encounter.Id))')
$legacyCopy = Join-Path $legacyBuild 'CoreNavalBattleCombat.cs'
[IO.File]::WriteAllText($legacyCopy, $legacyReplacement, (New-Object Text.UTF8Encoding($false)))
$legacySources += Get-Item -LiteralPath $legacyCopy
foreach ($legacyFixture in @('CoreBattleDeckInspectionTests.cs', 'NavalBoardingApproachTests.cs')) {
    $legacySources += Get-Item -LiteralPath (Join-Path $legacyWorkspace ('UnityClient/Assets/_Project/Tests/EditMode/' + $legacyFixture))
}
$legacySources += Get-Item -LiteralPath (Join-Path $legacyWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$legacySources += Get-Item -LiteralPath (Join-Path $legacyWorkspace 'tools/CourseTargetLock20261014/CoreRunner.cs')
'Comparison only: current Core with ForEncounter reverted to the former ForSchool(ResolveSchool(encounter.Id)) dispatch in an Artifacts copy. Workspace production files and complete test fixtures are unchanged.' |
    Set-Content -LiteralPath (Join-Path $legacyOutput 'scope.txt') -Encoding utf8
$legacyExe = Join-Path $legacyBuild 'LegacyComparison.exe'
$legacyArgs = @((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$legacyExe", "-r:$legacyNunit")
foreach ($legacyAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Xml.dll')) {
    $legacyArgs += '-r:' + (Join-Path $UnityData ('MonoBleedingEdge/lib/mono/4.7.2-api/' + $legacyAssembly))
}
$legacyArgs += $legacySources.FullName
& (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @legacyArgs 2>&1 | Tee-Object -FilePath (Join-Path $legacyOutput 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Legacy comparison compilation failed.' }
& (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $legacyExe $legacyOutput 2>&1 | Tee-Object -FilePath (Join-Path $legacyOutput 'tests.log')
if ($LASTEXITCODE -ne 0) { Write-Output 'Comparison retained failing cases; inspect failures.txt to compare with the current dispatch.' }
