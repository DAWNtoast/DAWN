param(
 [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$RunTag='BaselinePilot',
 [ValidatePattern('^(starter|mixed|[PHM](_thunder|_blood)?)$')][string]$Build='starter',
 [ValidateRange(1,500)][int]$Seeds=3,
 [ValidateRange(0,100000)][int]$StartSeed=0,
 [string]$SourceRoot='Artifacts/BattleBalanceBaseline20261016_PreNaval/Source',
 [string]$EnemyIds='',
 [string]$UnityData='D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)
$ErrorActionPreference='Stop'
$navalWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (![IO.Path]::IsPathRooted($SourceRoot)) { $SourceRoot=Join-Path $navalWorkspace $SourceRoot }
$navalOutput=Join-Path $navalWorkspace ('Artifacts/NavalDifficulty20261016/'+$RunTag)
$navalSnapshot=Join-Path $navalOutput 'CoreSnapshot'
$navalBuild=Join-Path $navalOutput 'Build'
New-Item -ItemType Directory -Force -Path $navalSnapshot,$navalBuild | Out-Null
$navalSources=@(Get-ChildItem -LiteralPath (Join-Path $SourceRoot 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs')
$navalSources+=Get-Item -LiteralPath (Join-Path $SourceRoot 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$navalHashes=foreach($navalSource in $navalSources){
 $navalCopy=Join-Path $navalSnapshot $navalSource.Name
 Copy-Item -LiteralPath $navalSource.FullName -Destination $navalCopy -Force
 $navalHash=(Get-FileHash -LiteralPath $navalCopy -Algorithm SHA256).Hash
 if($navalHash -ne (Get-FileHash -LiteralPath $navalSource.FullName -Algorithm SHA256).Hash){throw 'Snapshot changed'}
 [pscustomobject]@{Source=$navalSource.FullName;SHA256=$navalHash}
}
$navalHashes | Export-Csv -LiteralPath (Join-Path $navalOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
$navalHarness=@(Get-ChildItem -LiteralPath $PSScriptRoot -Filter 'SimulateNaval*.cs')
$navalHarness+=Get-Item -LiteralPath (Join-Path $PSScriptRoot 'NavalBuilds.cs')
$navalHarnessPaths=@(foreach($navalFile in $navalHarness){$navalDest=Join-Path $navalBuild $navalFile.Name;Copy-Item -LiteralPath $navalFile.FullName -Destination $navalDest -Force;$navalDest})
$navalHarnessPaths | Get-FileHash -Algorithm SHA256 | Export-Csv -LiteralPath (Join-Path $navalOutput 'harness-hashes.csv') -NoTypeInformation -Encoding utf8
$navalExe=Join-Path $navalBuild 'SimulateNaval.exe'
$navalArgs=@((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'),'-nologo','-target:exe','-langversion:9','-noconfig','-nostdlib',"-out:$navalExe")
foreach($navalAssembly in @('mscorlib.dll','System.dll','System.Core.dll')){$navalArgs+='-r:'+(Join-Path $UnityData ('MonoBleedingEdge/lib/mono/4.7.2-api/'+$navalAssembly))}
$navalArgs+=@(Get-ChildItem -LiteralPath $navalSnapshot -Filter '*.cs' | ForEach-Object FullName)
$navalArgs+=$navalHarnessPaths
& (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @navalArgs 2>&1 | Tee-Object -FilePath (Join-Path $navalOutput 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Naval audit compilation failed'}
& (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $navalExe $navalOutput $Build $Seeds $StartSeed $EnemyIds 2>&1 | Tee-Object -FilePath (Join-Path $navalOutput 'simulation.log')
if($LASTEXITCODE -ne 0){throw 'Naval audit failed'}
