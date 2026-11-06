param([string]$CatalogDirectory = 'D:/Game/outputs/naval-balance-20261024/VerifiedCatalog')
$ErrorActionPreference = 'Stop'
$balanceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$balanceFixed = Join-Path $balanceRoot 'outputs/card-list-by-school-20261023'
$balanceInputs = Join-Path $balanceRoot 'outputs/naval-balance-20261024/Inputs'
& (Join-Path $PSScriptRoot 'VerifyCatalog.ps1') -CatalogDirectory $CatalogDirectory
foreach ($balanceName in @('全部卡牌清单.html', '全部卡牌清单.md', 'card-list-data.json')) {
    $balanceCurrentHash = (Get-FileHash -LiteralPath (Join-Path $balanceFixed $balanceName)).Hash
    $balanceOriginalHash = (Get-FileHash -LiteralPath (Join-Path $balanceInputs $balanceName)).Hash
    $balanceGeneratedHash = (Get-FileHash -LiteralPath (Join-Path $CatalogDirectory $balanceName)).Hash
    if ($balanceCurrentHash -ne $balanceOriginalHash -and $balanceCurrentHash -ne $balanceGeneratedHash) {
        throw ('The fixed catalogue has an unreviewed edit since the input backup: ' + $balanceName)
    }
}
foreach ($balanceName in @('全部卡牌清单.html', '全部卡牌清单.md', 'card-list-data.json',
    'export.log', 'source-hashes.csv', 'validation.txt', 'audit-validation.txt')) {
    Copy-Item -LiteralPath (Join-Path $CatalogDirectory $balanceName) -Destination (Join-Path $balanceFixed $balanceName) -Force
    if ((Get-FileHash -LiteralPath (Join-Path $balanceFixed $balanceName)).Hash -ne
        (Get-FileHash -LiteralPath (Join-Path $CatalogDirectory $balanceName)).Hash) {
        throw ('Catalogue sync hash mismatch: ' + $balanceName)
    }
}
Write-Output 'PASS fixed HTML/Markdown/JSON catalogue synchronized and byte-verified; input backups preserved.'
