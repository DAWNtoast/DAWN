param([string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$guidedWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$guidedBuild = Join-Path $guidedWorkspace 'tmp/guided-tutorial-console'
New-Item -ItemType Directory -Force -Path $guidedBuild | Out-Null
$guidedFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$guidedNunit = @(Get-ChildItem -Path (Join-Path $guidedWorkspace 'UnityClient/Library/PackageCache/com.unity.ext.nunit*/net40/unity-custom/nunit.framework.dll'))[0].FullName
$guidedSources = @(Get-ChildItem -LiteralPath (Join-Path $guidedWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | ForEach-Object FullName)
$guidedSources += Join-Path $guidedWorkspace 'UnityClient/Assets/_Project/Tests/EditMode/NavalBattleBalanceSimulator.cs'
$guidedSources += Join-Path $guidedWorkspace 'UnityClient/Assets/_Project/Tests/EditMode/CoreGuidedTutorialVoyageTests.cs'
$guidedSources += Join-Path $PSScriptRoot 'Program.cs'
$guidedExe = Join-Path $guidedBuild 'GuidedTutorialAudit.exe'
$guidedArguments = @((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$guidedExe", "-r:$guidedNunit")
foreach ($guidedAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll')) {
    $guidedArguments += '-r:' + (Join-Path $guidedFramework $guidedAssembly)
}
$guidedArguments += $guidedSources
& (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @guidedArguments
if ($LASTEXITCODE -ne 0) { throw 'Guided tutorial Core compilation failed.' }
Copy-Item -LiteralPath $guidedNunit -Destination (Join-Path $guidedBuild 'nunit.framework.dll') -Force
& (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $guidedExe
if ($LASTEXITCODE -ne 0) { throw 'Guided tutorial Core tests failed.' }
