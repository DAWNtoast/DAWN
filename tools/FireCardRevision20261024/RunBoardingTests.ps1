param([string]$RunTag = 'Boarding')
$ErrorActionPreference = 'Stop'
$fireWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fireOutput = Join-Path $fireWorkspace ('Artifacts/FireCardRevision20261024/' + $RunTag)
$fireBuild = Join-Path $fireOutput 'Build'
$fireUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
$fireFramework = Join-Path $fireUnity 'MonoBleedingEdge/lib/mono/4.7.2-api'
$fireNunit = Get-ChildItem -LiteralPath (Join-Path $fireWorkspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' |
    ForEach-Object { Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $fireNunit) { throw 'Existing Unity NUnit cache is required.' }
$fireTests = Join-Path $fireWorkspace 'UnityClient/Assets/_Project/Tests/EditMode'
$fireNames = @(
    'CoreBoardingFireRevisionTests.cs', 'CoreBoardingFireArcaneThunderTests.cs',
    'CoreBoardingManuscriptRulesTests.cs', 'CoreBoardingEnemyTests.cs',
    'CoreBoardingEnemySchoolTests.cs', 'CoreBoardingTransactionTests.cs',
    'CoreBoardingCardCatalogTests.cs', 'CoreBoardingCurseTideBloodTests.cs'
)
$fireFixtures = @($fireNames | ForEach-Object { Get-Item -LiteralPath (Join-Path $fireTests $_) })
$fireSources = @(Get-ChildItem -LiteralPath (Join-Path $fireWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
$fireSources += $fireFixtures
$fireSources += Get-Item -LiteralPath (Join-Path $fireWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$fireSources += Get-Item -LiteralPath (Join-Path $fireWorkspace 'tools/CourseTargetLock20261014/CoreRunner.cs')
New-Item -ItemType Directory -Force -Path $fireBuild | Out-Null
Copy-Item -LiteralPath $fireNunit -Destination (Join-Path $fireBuild 'nunit.framework.dll') -Force
$fireExe = Join-Path $fireBuild 'BoardingFireTests.exe'
$fireArgs = @((Join-Path $fireUnity 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$fireExe", "-r:$fireNunit")
foreach ($fireAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Xml.dll')) {
    $fireArgs += '-r:' + (Join-Path $fireFramework $fireAssembly)
}
$fireArgs += $fireSources.FullName
& (Join-Path $fireUnity 'NetCoreRuntime/dotnet.exe') @fireArgs 2>&1 | Tee-Object -FilePath (Join-Path $fireOutput 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Boarding fire compilation failed.' }
& (Join-Path $fireUnity 'MonoBleedingEdge/bin/mono.exe') $fireExe $fireOutput 2>&1 | Tee-Object -FilePath (Join-Path $fireOutput 'tests.log')
if ($LASTEXITCODE -ne 0) { throw 'Boarding fire tests failed; see results.xml and failures.txt.' }
