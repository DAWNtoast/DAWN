param([Parameter(Mandatory=$true)][string]$Manifest)
$ErrorActionPreference = 'Stop'
$legacyDestinationRoot = [IO.Path]::GetFullPath('D:\Game\UnityClient\Assets\_Project\Resources\CardArt\Western').TrimEnd('\')
$legacyLedgerPath = 'D:\Game\Artifacts\CardSchools20261011\generated-legacy-art-western.json'
$legacyById = @{}
if (Test-Path -LiteralPath $legacyLedgerPath) {
    foreach ($legacyOld in (Get-Content -LiteralPath $legacyLedgerPath -Raw | ConvertFrom-Json)) { $legacyById[$legacyOld.cardId] = $legacyOld }
}
$legacyBatch = @(Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json)
$legacyAllowed = @{}
foreach ($legacyCard in (Get-Content -LiteralPath 'D:\Game\Artifacts\CardSchools20261011\ArtAudit\runtime-card-art-snapshot.json' -Raw | ConvertFrom-Json)) {
    if ($legacyCard.allowedStages -gt 0 -and $legacyCard.artId -notlike 'card_school_*') { $legacyAllowed[$legacyCard.id] = $true }
}
if (-not (Test-Path -LiteralPath $legacyDestinationRoot)) { New-Item -ItemType Directory -Path $legacyDestinationRoot -Force | Out-Null }
foreach ($legacyRow in $legacyBatch) {
    if (-not $legacyAllowed.ContainsKey($legacyRow.cardId) -or $legacyRow.cardId -notmatch '^card_[a-z0-9_]+$') { throw ('Invalid legacy card identity: '+$legacyRow.cardId) }
    $legacyExpectedTarget = [IO.Path]::GetFullPath((Join-Path $legacyDestinationRoot ($legacyRow.cardId + '.png')))
    if ([IO.Path]::GetFullPath($legacyRow.target) -ne $legacyExpectedTarget -or -not $legacyExpectedTarget.StartsWith($legacyDestinationRoot + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Legacy art target escaped the resource directory.' }
    if (-not (Test-Path -LiteralPath $legacyRow.source -PathType Leaf)) { throw ('Missing generated original: '+$legacyRow.source) }
    Copy-Item -LiteralPath $legacyRow.source -Destination $legacyExpectedTarget -Force
    $legacyById[$legacyRow.cardId] = $legacyRow
}
@($legacyById.Values | Sort-Object cardId) | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $legacyLedgerPath -Encoding utf8
Write-Output ('Western legacy art selected: '+$legacyById.Count+'; installed this batch: '+$legacyBatch.Count)
