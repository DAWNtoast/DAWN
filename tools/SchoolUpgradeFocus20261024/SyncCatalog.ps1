param()
$ErrorActionPreference = 'Stop'
$taskWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOutput = Join-Path $taskWorkspace 'outputs/school-upgrade-focus-20261024'
$taskVerified = Join-Path $taskOutput 'VerifiedCatalog'
$taskFixed = Join-Path $taskWorkspace 'outputs/card-list-by-school-20261023'
$taskInput = Join-Path $taskOutput 'Inputs/Catalog'
$taskNames = @('全部卡牌清单.html', '全部卡牌清单.md', 'card-list-data.json', '全部卡牌强化清单.html', '全部卡牌强化清单.md', 'card-upgrade-data.json')
foreach ($taskName in $taskNames) {
    $taskCurrentPath = Join-Path $taskFixed $taskName
    $taskInputPath = Join-Path $taskInput $taskName
    $taskCurrentHash = if (Test-Path -LiteralPath $taskCurrentPath) { (Get-FileHash -LiteralPath $taskCurrentPath).Hash } else { '' }
    $taskInputHash = if (Test-Path -LiteralPath $taskInputPath) { (Get-FileHash -LiteralPath $taskInputPath).Hash } else { '' }
    $taskFinalHash = (Get-FileHash -LiteralPath (Join-Path $taskVerified $taskName)).Hash
    if ($taskCurrentHash -ne $taskInputHash -and $taskCurrentHash -ne $taskFinalHash) {
        throw ('Fixed catalog was edited after capture; preserve and review before replacing: ' + $taskName)
    }
}
foreach ($taskManifest in @('source-hashes.csv', 'upgrade-source-hashes.csv')) {
    foreach ($taskRow in Import-Csv -LiteralPath (Join-Path $taskVerified $taskManifest)) {
        if ((Get-FileHash -LiteralPath $taskRow.Source).Hash -ne $taskRow.SHA256) {
            throw ('Verified export is stale: ' + $taskRow.Source)
        }
    }
}
foreach ($taskName in ($taskNames + @('source-hashes.csv', 'export.log', 'validation.txt', 'upgrade-source-hashes.csv', 'upgrade-export.log', 'upgrade-validation.txt', 'upgrade-inputs.json'))) {
    Copy-Item -LiteralPath (Join-Path $taskVerified $taskName) -Destination (Join-Path $taskFixed $taskName) -Force
}
$taskHashes = foreach ($taskName in $taskNames) {
    $taskSourceHash = (Get-FileHash -LiteralPath (Join-Path $taskVerified $taskName)).Hash
    $taskFixedHash = (Get-FileHash -LiteralPath (Join-Path $taskFixed $taskName)).Hash
    if ($taskSourceHash -ne $taskFixedHash) { throw ('Catalog copy differs: ' + $taskName) }
    [pscustomobject]@{ Name=$taskName; SHA256=$taskFixedHash; FixedPath=(Join-Path $taskFixed $taskName) }
}
$taskHashes | Export-Csv -LiteralPath (Join-Path $taskOutput 'fixed-catalog-hashes.csv') -NoTypeInformation -Encoding utf8
Write-Output 'PASS fixed HTML, Markdown and JSON synchronized; captured user input retained.'
