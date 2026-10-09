param(
    [int]$Seeds = 16,
    [string]$Output = 'D:/Game/UnityClient/Artifacts/BattleRules20261002/OldLoop-v2',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$battleWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$battleBuild = Join-Path $battleWorkspace 'tmp/old-loop-balance-console'
New-Item -ItemType Directory -Force -Path $battleBuild | Out-Null
$battleCompiler = Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'
$battleRuntime = Join-Path $UnityData 'NetCoreRuntime/dotnet.exe'
$battleMono = Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe'
$battleFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$battleSources = @(Get-ChildItem -LiteralPath (Join-Path $battleWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | ForEach-Object FullName)
$battleSources += Join-Path $battleWorkspace 'UnityClient/Assets/_Project/Tests/EditMode/NavalBattleBalanceSimulator.cs'
$battleSources += Join-Path $PSScriptRoot 'OldLoopProgram.cs'
$battleExe = Join-Path $battleBuild 'OldLoopBalance.exe'
$battleArguments = @($battleCompiler, '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$battleExe")
foreach ($battleAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll')) {
    $battleArguments += '-r:' + (Join-Path $battleFramework $battleAssembly)
}
$battleArguments += $battleSources
& $battleRuntime @battleArguments
if ($LASTEXITCODE -ne 0) { throw 'Battle balance simulator compilation failed.' }
& $battleMono $battleExe $Seeds $Output
if ($LASTEXITCODE -ne 0) { throw 'Battle balance simulation failed.' }
