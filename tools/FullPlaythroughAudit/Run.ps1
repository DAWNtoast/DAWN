param([string]$Output='D:/Game/Artifacts/FullPlaythrough20261011/CoreRun',[int]$MaxCycles=120,[string]$Mode='Prudent')
$ErrorActionPreference='Stop'
$auditRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$auditUnity='D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
$auditManaged=Join-Path $auditRoot 'Builds/第七港口-启动内存优化20261011/Windows/第七港口_Data/Managed'
$auditBuild=Join-Path $Output 'Build'
New-Item -ItemType Directory -Force -Path $auditBuild | Out-Null
$auditRefs=@('ShatteredSail.Core.dll','ShatteredSail.Content.dll','ShatteredSail.Presentation.dll','UnityEngine.CoreModule.dll','netstandard.dll')
$auditArgs=@((Join-Path $auditUnity 'DotNetSdkRoslyn/csc.dll'),'-nologo','-target:exe','-langversion:9','-noconfig','-nostdlib',('-out:'+(Join-Path $auditBuild 'FullPlaythroughAudit.exe')))
foreach($auditDll in @('mscorlib.dll','System.dll','System.Core.dll','System.Web.Extensions.dll')){$auditArgs+='-r:'+(Join-Path $auditUnity ('MonoBleedingEdge/lib/mono/4.7.2-api/'+$auditDll))}
foreach($auditDll in $auditRefs){$auditSource=Join-Path $auditManaged $auditDll;Copy-Item -LiteralPath $auditSource -Destination $auditBuild;$auditArgs+='-r:'+$auditSource}
$auditArgs+=Join-Path $PSScriptRoot 'Program.cs'
& (Join-Path $auditUnity 'NetCoreRuntime/dotnet.exe') @auditArgs
if($LASTEXITCODE-ne 0){throw 'Compilation failed'}
$auditRefs | ForEach-Object { Get-FileHash -LiteralPath (Join-Path $auditManaged $_) -Algorithm SHA256 } | Select-Object Path,Hash | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Output 'production-assembly-hashes.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Program.cs') -Destination $Output
& (Join-Path $auditUnity 'MonoBleedingEdge/bin/mono.exe') (Join-Path $auditBuild 'FullPlaythroughAudit.exe') $Output $MaxCycles $Mode
if($LASTEXITCODE-ne 0){throw 'Audit did not achieve its objectives; retained evidence describes the result'}
