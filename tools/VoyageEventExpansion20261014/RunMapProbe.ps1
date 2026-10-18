$ErrorActionPreference='Stop'
$probeDir='D:/Game/tools/VoyageEventExpansion20261014'
$unityData='D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
$runtime=Join-Path $unityData 'NetCoreRuntime/dotnet.exe'
$compiler=Join-Path $unityData 'DotNetSdkRoslyn/csc.dll'
$framework=Join-Path $unityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$outputDir='D:/Game/tmp/VoyageEventExpansion20261014'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$options=@($compiler,'-nologo','-langversion:9','-noconfig','-nostdlib','-optimize+','-target:exe',"-out:$outputDir/MapProbe.exe")
foreach($assembly in @('mscorlib.dll','System.dll','System.Core.dll')) { $options+='-r:'+(Join-Path $framework $assembly) }
$sources=@(Get-ChildItem -LiteralPath 'D:/Game/UnityClient/Assets/_Project/Core/Runtime' -Filter '*.cs' | ForEach-Object FullName)
if($args -contains 'baseline') {
    $sources=@($sources | Where-Object { [IO.Path]::GetFileName($_) -notin @('VoyageMapGenerator.cs','VoyageMapEventPacing.cs','CoreVoyageSession.cs') })
    $sources+="$outputDir/LegacyVoyageMapGenerator.cs"
    $sources+="$outputDir/LegacyCoreVoyageSession.cs"
}
$options+=$sources
$options+="$probeDir/MapProbe.cs"
& $runtime @options
if($LASTEXITCODE -ne 0) { throw 'Map probe compile failed' }
& (Join-Path $unityData 'MonoBleedingEdge/bin/mono.exe') "$outputDir/MapProbe.exe" $outputDir @args
if($LASTEXITCODE -ne 0) { throw 'Map probe execution failed' }
