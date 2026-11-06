param([Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$SnapshotName)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskEvidence=Join-Path $taskRoot 'outputs/campaign-boss-review-20261027'
$taskFrozen=Join-Path $taskEvidence $SnapshotName
if(Test-Path -LiteralPath $taskFrozen){throw 'Preserve existing source snapshots; use a fresh name.'}
$taskSource=Join-Path $taskRoot 'UnityClient/Assets/_Project'
$taskFiles=@((Get-ChildItem -LiteralPath (Join-Path $taskSource 'Core/Runtime') -Filter '*.cs' -File).FullName)
$taskFiles+=@((Get-ChildItem -LiteralPath (Join-Path $taskSource 'Content/Runtime') -Filter '*.cs' -File).FullName)
foreach($taskName in @('PortMarketCatalog.cs','PortInvestmentCatalog.cs','ComponentArtCatalog.cs','OwnedShipComponentInventory.cs','ComponentVoyageContextResolver.cs','BoardingComponentDropSelection.cs','PortComponentShopCatalog.cs')){$taskFiles+=Join-Path $taskSource ('Presentation/Runtime/'+$taskName)}
$taskFiles+=Join-Path $taskSource 'Resources/Data/component_catalog_v2.csv'
$taskRows=@(foreach($taskFile in $taskFiles){
    $taskRelative=$taskFile.Substring($taskSource.Length).TrimStart('\','/')
    $taskDestination=Join-Path $taskFrozen $taskRelative
    New-Item -ItemType Directory -Force -Path (Split-Path $taskDestination)|Out-Null
    $taskHash=(Get-FileHash -LiteralPath $taskFile).Hash
    Copy-Item -LiteralPath $taskFile -Destination $taskDestination
    if((Get-FileHash -LiteralPath $taskFile).Hash -ne $taskHash -or (Get-FileHash -LiteralPath $taskDestination).Hash -ne $taskHash){throw 'Source changed during freeze.'}
    [pscustomobject]@{Source=$taskFile;Snapshot=$taskDestination;SHA256=$taskHash}
})
$taskRows|Export-Csv -LiteralPath (Join-Path $taskEvidence ($SnapshotName+'-hashes.csv')) -NoTypeInformation -Encoding utf8
Write-Output ('Frozen '+$taskRows.Count+' current production inputs in '+$taskFrozen)
