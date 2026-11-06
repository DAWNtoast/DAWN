$ErrorActionPreference='Stop'
$root='D:/Game';$unity='D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data';$out=Join-Path $root 'outputs/naval-endurance-20261104';$build=Join-Path $out 'Build'
New-Item -ItemType Directory -Force -Path $build | Out-Null
$files=@((Get-ChildItem -LiteralPath (Join-Path $root 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' -File).FullName)+(Join-Path $root 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')+(Join-Path $root 'tools/NavalEndurance20261104/Program.cs')+(Join-Path $root 'tools/NavalEndurance20261104/Movement.cs')
$files|ForEach-Object{[pscustomobject]@{Path=$_;Hash=(Get-FileHash -LiteralPath $_).Hash}}|Export-Csv -LiteralPath (Join-Path $out 'source-hashes.csv') -NoTypeInformation -Encoding utf8
$exe=Join-Path $build 'Endurance.exe';$a=@("$unity/DotNetSdkRoslyn/csc.dll",'-nologo','-target:exe','-langversion:9','-noconfig','-nostdlib',"-out:$exe");foreach($dll in @('mscorlib.dll','System.dll','System.Core.dll','System.Web.Extensions.dll')){$a+="-r:$unity/MonoBleedingEdge/lib/mono/4.7.2-api/$dll"};$a+=$files
try {
& "$unity/NetCoreRuntime/dotnet.exe" @a 2>&1|Tee-Object -FilePath (Join-Path $out 'compile.log');if($LASTEXITCODE -ne 0){throw 'compile'}
& "$unity/MonoBleedingEdge/bin/mono.exe" $exe $out 30 2>&1|Tee-Object -FilePath (Join-Path $out 'run.log')


if($LASTEXITCODE -ne 0){throw 'Simulation failed'} 
} finally { & (Join-Path $PSScriptRoot 'Cleanup.ps1') }
