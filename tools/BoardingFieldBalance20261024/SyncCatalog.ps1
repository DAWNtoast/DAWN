$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOutput=Join-Path $taskRoot 'outputs/boarding-field-balance-20261024'
$taskVerified=Join-Path $taskOutput 'VerifiedCatalog'
$taskFixed=Join-Path $taskRoot 'outputs/card-list-by-school-20261023'
$taskNames=@('全部卡牌清单.html','全部卡牌清单.md','card-list-data.json','全部卡牌强化清单.html','全部卡牌强化清单.md','card-upgrade-data.json')
foreach($taskName in $taskNames){
    $taskCurrent=Join-Path $taskFixed $taskName
    $taskBefore=Join-Path (Join-Path $taskOutput 'Inputs/Catalog') $taskName
    if((Get-FileHash -LiteralPath $taskCurrent).Hash -ne (Get-FileHash -LiteralPath $taskBefore).Hash){throw ('Fixed catalog changed after the original backup: '+$taskName)}
}
$taskEvidence=Get-Content -LiteralPath (Join-Path $taskVerified 'non-boarding-catalog-integrity.json') -Raw | ConvertFrom-Json
if(!$taskEvidence.NonBoardingUnchanged -or $taskEvidence.Differences -ne 0){throw 'Non-boarding catalog gate did not pass'}
$taskNames+=@('source-hashes.csv','validation.txt','export.log','upgrade-source-hashes.csv','upgrade-validation.txt','upgrade-export.log','upgrade-inputs.json','non-boarding-catalog-integrity.json')
$taskManifest=foreach($taskName in $taskNames){
    $taskSource=Join-Path $taskVerified $taskName
    if(!(Test-Path -LiteralPath $taskSource)){throw ('Missing final export: '+$taskName)}
    Copy-Item -LiteralPath $taskSource -Destination (Join-Path $taskFixed $taskName) -Force
    $taskHash=(Get-FileHash -LiteralPath $taskSource).Hash
    if((Get-FileHash -LiteralPath (Join-Path $taskFixed $taskName)).Hash -ne $taskHash){throw ('Catalog copy mismatch: '+$taskName)}
    [pscustomobject]@{File=$taskName;SHA256=$taskHash;Verified=$taskSource;Published=(Join-Path $taskFixed $taskName)}
}
$taskManifest|Export-Csv -LiteralPath (Join-Path $taskOutput 'published-catalog-hashes.csv') -NoTypeInformation -Encoding utf8
Write-Output 'PASS fixed HTML/Markdown/base/upgrade catalogs match the verified production exports.'
