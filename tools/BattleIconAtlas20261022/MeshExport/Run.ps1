param(
    [string]$Output = 'D:/Game/outputs/battle-icon-atlas-20261022/mesh-icons.json',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$atlasRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$atlasBuild = Join-Path $PSScriptRoot 'Build'
New-Item -ItemType Directory -Force -Path $atlasBuild | Out-Null
New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($Output)) | Out-Null
$atlasSources = @('BattleStatusIconGraphic.cs','BattleCommandIconGraphic.cs','BattleHeaderIconGraphic.cs','BattleCardFlightBackGraphic.cs','NavalFieldGraphic.cs','NavalTacticalTrackGraphic.cs') | ForEach-Object { Join-Path $atlasRoot ('UnityClient/Assets/_Project/Presentation/Runtime/' + $_) }
$atlasHashes = @($atlasSources | ForEach-Object { [pscustomobject]@{ Source=$_; SHA256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash } })
$atlasHashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'source-hashes.json') -Encoding utf8
$atlasExe = Join-Path $atlasBuild 'BattleIconMeshExport.exe'
$atlasArguments = @((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'),'-nologo','-target:exe','-langversion:9','-noconfig','-nostdlib',"-out:$atlasExe")
foreach ($atlasAssembly in @('mscorlib.dll','System.dll','System.Core.dll')) { $atlasArguments += '-r:'+(Join-Path $UnityData ('MonoBleedingEdge/lib/mono/4.7.2-api/'+$atlasAssembly)) }
$atlasArguments += $atlasSources
$atlasArguments += Join-Path $PSScriptRoot 'UnityStubs.cs'
$atlasArguments += Join-Path $PSScriptRoot 'Export.cs'
& (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @atlasArguments 2>&1 | Tee-Object -FilePath (Join-Path $PSScriptRoot 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Icon mesh compilation failed.' }
foreach ($atlasHash in $atlasHashes) { if ((Get-FileHash -LiteralPath $atlasHash.Source -Algorithm SHA256).Hash -ne $atlasHash.SHA256) { throw "Source changed during export: $($atlasHash.Source)" } }
& (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $atlasExe $atlasRoot $Output
if ($LASTEXITCODE -ne 0) { throw 'Icon mesh execution failed.' }
