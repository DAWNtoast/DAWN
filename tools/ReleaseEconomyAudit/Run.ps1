param(
    [string]$Output = 'D:/Game/UnityClient/Artifacts/ReleaseAudit/Economy',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$economyWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$economyBuild = Join-Path $economyWorkspace 'tmp/release-economy-console'
New-Item -ItemType Directory -Force -Path $economyBuild | Out-Null
$economyCompiler = Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'
$economyRuntime = Join-Path $UnityData 'NetCoreRuntime/dotnet.exe'
$economyMono = Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe'
$economyFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$economyNunit = Get-ChildItem -LiteralPath (Join-Path $economyWorkspace 'UnityClient/Library/PackageCache') -Filter 'nunit.framework.dll' -Recurse | Select-Object -First 1 -ExpandProperty FullName
$economySources = @(Get-ChildItem -LiteralPath (Join-Path $economyWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | ForEach-Object FullName)
foreach ($economyTest in @('CoreEconomySafetyTests.cs', 'CoreVoyageSessionTests.cs', 'CoreVoyageSafetyAuditTests.cs', 'EncounterDiscoveryRulesTests.cs')) {
    $economySources += Join-Path $economyWorkspace ('UnityClient/Assets/_Project/Tests/EditMode/' + $economyTest)
}
$economySources += Join-Path $PSScriptRoot 'Program.cs'
$economyExe = Join-Path $economyBuild 'ReleaseEconomyAudit.exe'
$economyArguments = @($economyCompiler, '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$economyExe", "-r:$economyNunit")
foreach ($economyAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll')) {
    $economyArguments += '-r:' + (Join-Path $economyFramework $economyAssembly)
}
$economyArguments += $economySources
& $economyRuntime @economyArguments
if ($LASTEXITCODE -ne 0) { throw 'Economy audit compilation failed.' }
Copy-Item -LiteralPath $economyNunit -Destination (Join-Path $economyBuild 'nunit.framework.dll')
& $economyMono $economyExe $Output
if ($LASTEXITCODE -ne 0) { throw 'Economy audit regression failed.' }
