param([Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$RunTag,[int]$MaxCycles=200,[string]$SourceSnapshot='',[ValidateSet('original','direct-patrol')][string]$Policy='original')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOut=Join-Path $taskRoot ('outputs/campaign-audit-20261027/'+$RunTag)
if(Test-Path -LiteralPath $taskOut){throw 'Use a fresh RunTag to preserve prior evidence.'}
$taskSource=if($SourceSnapshot){[IO.Path]::GetFullPath($SourceSnapshot)}else{Join-Path $taskRoot 'UnityClient/Assets/_Project'}
$taskBuild=Join-Path $taskOut 'Build';$taskFrozen=Join-Path $taskOut 'SourceSnapshot';$taskInput=Join-Path $taskOut 'Inputs'
$taskUnity='D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
New-Item -ItemType Directory -Force -Path $taskOut,$taskBuild,$taskFrozen,$taskInput | Out-Null
$taskFiles=@((Get-ChildItem -LiteralPath (Join-Path $taskSource 'Core/Runtime') -Filter '*.cs' -File).FullName)
$taskFiles+=@((Get-ChildItem -LiteralPath (Join-Path $taskSource 'Content/Runtime') -Filter '*.cs' -File).FullName)
foreach($name in @('PortMarketCatalog.cs','PortInvestmentCatalog.cs','ComponentArtCatalog.cs','OwnedShipComponentInventory.cs','ComponentVoyageContextResolver.cs','BoardingComponentDropSelection.cs','PortComponentShopCatalog.cs')){$taskFiles+=Join-Path $taskSource ('Presentation/Runtime/'+$name)}
$taskHashes=@(foreach($f in $taskFiles){$relative=$f.Substring($taskSource.Length).TrimStart('\','/');$to=Join-Path $taskFrozen $relative;New-Item -ItemType Directory -Force -Path (Split-Path $to) | Out-Null;Copy-Item -LiteralPath $f -Destination $to;[pscustomobject]@{Source=$f;Snapshot=$to;SHA256=(Get-FileHash -LiteralPath $f).Hash}})
$taskHashes|Export-Csv -LiteralPath (Join-Path $taskOut 'source-hashes.csv') -NoTypeInformation -Encoding utf8
foreach($name in @('Program.cs','Campaign.cs','UnityDataAdapter.cs','Run.ps1')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $taskInput}
$taskResources=Join-Path $taskOut 'Resources/Data';New-Item -ItemType Directory -Force -Path $taskResources | Out-Null
$dataSource=Join-Path $taskRoot 'tmp/shipyard-detail-clean-20261011/Assets/_Project/Resources/Data/component_catalog_v2.csv'
$expectedData=Import-Csv -LiteralPath (Join-Path $taskRoot 'Artifacts/CampaignAudit20261027/Snapshot/source-manifest.csv')|Where-Object {$_.Path -eq 'Assets/_Project/Resources/Data/component_catalog_v2.csv'}
if(!$expectedData -or (Get-FileHash -LiteralPath $dataSource).Hash -ne $expectedData.SHA256){throw 'Frozen component data does not match audited source manifest.'}
Copy-Item -LiteralPath $dataSource -Destination $taskResources
Get-FileHash -LiteralPath (Join-Path $taskResources 'component_catalog_v2.csv')|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $taskOut 'data-hashes.json') -Encoding utf8
$taskExe=Join-Path $taskBuild 'CampaignAudit.exe';$taskArgs=@("$taskUnity/DotNetSdkRoslyn/csc.dll",'-nologo','-target:exe','-langversion:9','-noconfig','-nostdlib',"-out:$taskExe")
foreach($dll in @('mscorlib.dll','System.dll','System.Core.dll','System.Web.Extensions.dll')){$taskArgs+="-r:$taskUnity/MonoBleedingEdge/lib/mono/4.7.2-api/$dll"}
$taskArgs+=@($taskHashes|ForEach-Object Snapshot);$taskArgs+=@((Get-ChildItem -LiteralPath $taskInput -Filter '*.cs').FullName)
$taskCode=1;$taskFreed=0
try {
 & "$taskUnity/NetCoreRuntime/dotnet.exe" @taskArgs 2>&1 | Tee-Object -FilePath (Join-Path $taskOut 'compile.log')
 if($LASTEXITCODE -ne 0){throw 'Campaign source compilation failed.'}
 & "$taskUnity/MonoBleedingEdge/bin/mono.exe" $taskExe $taskOut $MaxCycles (Split-Path $taskResources) $Policy 2>&1 | Tee-Object -FilePath (Join-Path $taskOut 'run.log')
 $taskCode=$LASTEXITCODE
 foreach($row in $taskHashes){if((Get-FileHash -LiteralPath $row.Snapshot).Hash -ne $row.SHA256){throw 'Frozen source changed.'}}
} finally {
 $resolved=[IO.Path]::GetFullPath($taskBuild);$allowed=[IO.Path]::GetFullPath((Join-Path $taskOut 'Build'))
 if($resolved -ne $allowed -or !$resolved.StartsWith([IO.Path]::GetFullPath((Join-Path $taskRoot 'outputs/campaign-audit-20261027')),[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe cleanup path.'}
 $entry=Get-Item -LiteralPath $taskBuild
 if($entry.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Refuse linked cleanup directory.'}
 foreach($f in Get-ChildItem -LiteralPath $taskBuild -File){if($f.Extension -notin @('.exe','.dll','.pdb','.mdb')){throw 'Unexpected build artifact'};$taskFreed+=$f.Length;Remove-Item -LiteralPath $f.FullName -Force}
 if(@(Get-ChildItem -LiteralPath $taskBuild -Force).Count -eq 0){Remove-Item -LiteralPath $taskBuild}
 [pscustomobject]@{ExitCode=$taskCode;ReleasedBytes=$taskFreed;BuildRemaining=(Test-Path -LiteralPath $taskBuild);Retained='Source snapshot, inputs, hashes, data, logs and results';MainProjectModified=$false;UserSavesAccessed=$false}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $taskOut 'cleanup.json') -Encoding utf8
}
exit $taskCode
