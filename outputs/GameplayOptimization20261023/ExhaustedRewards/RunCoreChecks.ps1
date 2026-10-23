param([string]$Label='Validation02')
$ErrorActionPreference='Stop'
$outDir=Join-Path $PSScriptRoot $Label
if(Test-Path -LiteralPath $outDir){throw 'Output exists; choose a new validation label.'}
New-Item -ItemType Directory -Path $outDir | Out-Null
$data='D:\Unity\Hub\Editor\6000.3.23f1\Editor\Data'
$nunit='D:\Game\UnityClient\Library\PackageCache\com.unity.ext.nunit@d8c07649098d\net40\unity-custom\nunit.framework.dll'
$src=@(Get-ChildItem -LiteralPath 'D:\Game\UnityClient\Assets\_Project\Core\Runtime' -Filter '*.cs' | ForEach-Object FullName)
$src+='D:\Game\UnityClient\Assets\_Project\Content\Runtime\CrewNarrativeCatalog.cs'
$tests=@('CoreSeaChartExhaustedRewardTests','CoreSeaChartProgressTests','CoreGuidedBossScriptTests','CoreVoyageExplorationTests','CoreVoyageExplorationRevisitTests','CoreVoyageEventConditionTests','CoreVoyageEventChoiceEffectTests','CoreTreasureExpeditionMechanicsTests')
foreach($name in $tests){$src+=Join-Path 'D:\Game\UnityClient\Assets\_Project\Tests\EditMode' ($name+'.cs')}
$src | ForEach-Object {Get-FileHash -LiteralPath $_ -Algorithm SHA256} | Select-Object Path,Hash | Export-Csv -LiteralPath (Join-Path $outDir 'source-hashes.csv') -NoTypeInformation -Encoding UTF8
$argsCompile=@((Join-Path $data 'DotNetSdkRoslyn\csc.dll'),'-nologo','-target:exe','-langversion:9','-noconfig','-nostdlib',('-out:'+(Join-Path $outDir 'Tests.exe')),('-r:'+$nunit))
foreach($dll in @('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll')){$argsCompile+='-r:'+(Join-Path $data ('MonoBleedingEdge\lib\mono\4.7.2-api\'+$dll))}
$argsCompile+=$src
$argsCompile+=(Join-Path $PSScriptRoot 'Runner.cs')
& (Join-Path $data 'NetCoreRuntime\dotnet.exe') @argsCompile
if($LASTEXITCODE -ne 0){throw 'Compile failed'}
Copy-Item -LiteralPath $nunit -Destination $outDir
& (Join-Path $data 'MonoBleedingEdge\bin\mono.exe') (Join-Path $outDir 'Tests.exe') $outDir 2>&1 | Tee-Object -FilePath (Join-Path $outDir 'results.txt')
if($LASTEXITCODE -ne 0){throw 'Tests failed'}
$manifest=Import-Csv -LiteralPath (Join-Path $outDir 'source-hashes.csv')
$changes=@($manifest | Where-Object {(Get-FileHash -LiteralPath $_.Path -Algorithm SHA256).Hash -ne $_.Hash})
'Changed compiled source files: '+$changes.Count
$changes | Select-Object Path
