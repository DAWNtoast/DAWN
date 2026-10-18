param([string]$Output = 'D:/Game/Artifacts/NavalCannonRange20261015/CoreTests')
$ErrorActionPreference = 'Stop'
$rangeWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$rangeBuild = Join-Path $Output 'Build'
New-Item -ItemType Directory -Force -Path $rangeBuild | Out-Null
$rangeUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
$rangeFramework = Join-Path $rangeUnity 'MonoBleedingEdge/lib/mono/4.7.2-api'
$rangeNunit = Get-ChildItem -LiteralPath (Join-Path $rangeWorkspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' |
    ForEach-Object { Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
Copy-Item -LiteralPath $rangeNunit -Destination (Join-Path $rangeBuild 'nunit.framework.dll')
$rangeSources = @(Get-ChildItem -LiteralPath (Join-Path $rangeWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
foreach ($rangeFixture in @('CoreNavalCannonRangeTests.cs','CoreNavalTacticsTests.cs','CoreNavalActionMovementTests.cs','CoreGuidedBossScriptTests.cs','CoreTrainingBattleTests.cs')) {
    $rangeSources += Get-Item -LiteralPath (Join-Path $rangeWorkspace ('UnityClient/Assets/_Project/Tests/EditMode/' + $rangeFixture))
}
$rangeSources += Get-Item -LiteralPath (Join-Path $rangeWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$rangeSources += Get-Item -LiteralPath (Join-Path $rangeWorkspace 'tools/CardBalanceAudit/Runner.cs')
$rangeExe = Join-Path $rangeBuild 'NavalCannonRangeTests.exe'
$rangeArgs = @((Join-Path $rangeUnity 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$rangeExe", "-r:$rangeNunit")
foreach ($rangeAssembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll')) {
    $rangeArgs += '-r:' + (Join-Path $rangeFramework $rangeAssembly)
}
$rangeArgs += $rangeSources.FullName
& (Join-Path $rangeUnity 'NetCoreRuntime/dotnet.exe') @rangeArgs 2>&1 | Tee-Object -FilePath (Join-Path $Output 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Naval range Core compilation failed.' }
& (Join-Path $rangeUnity 'MonoBleedingEdge/bin/mono.exe') $rangeExe $Output 2>&1 | Tee-Object -FilePath (Join-Path $Output 'tests.log')
if ($LASTEXITCODE -ne 0) { throw 'Naval range Core tests failed. See tests.log and results.xml.' }
