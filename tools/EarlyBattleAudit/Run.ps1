param(
    [int]$Seeds = 32,
    [string]$Output = 'D:/Game/UnityClient/Artifacts/EarlyBattleAudit20260909',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$earlyWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$earlyBuild = Join-Path $earlyWorkspace 'tmp/early-battle-audit-console'
New-Item -ItemType Directory -Force -Path $earlyBuild | Out-Null
$earlyFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$earlyExe = Join-Path $earlyBuild 'EarlyBattleAudit.exe'
$earlySources = @(Get-ChildItem -LiteralPath (Join-Path $earlyWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | ForEach-Object FullName)
$earlySources += Join-Path $earlyWorkspace 'UnityClient/Assets/_Project/Tests/EditMode/NavalBattleBalanceSimulator.cs'
$earlySources += Join-Path $PSScriptRoot 'Program.cs'
$earlyArguments = @((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$earlyExe")
foreach ($earlyAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll')) {
    $earlyArguments += '-r:' + (Join-Path $earlyFramework $earlyAssembly)
}
$earlyArguments += $earlySources
& (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @earlyArguments
if ($LASTEXITCODE -ne 0) { throw 'Early battle audit compilation failed.' }
& (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $earlyExe $Seeds $Output
if ($LASTEXITCODE -ne 0) { throw 'Early battle audit failed.' }
