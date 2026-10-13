param([Parameter(Mandatory=$true)][string]$Manifest)
$ErrorActionPreference = 'Stop'
$artDestinationRoot = [IO.Path]::GetFullPath('D:\Game\UnityClient\Assets\_Project\Resources\CardSchools\Art').TrimEnd('\')
$artLedgerPath = 'D:\Game\Artifacts\CardSchools20261011\generated-art-western-v2.json'
$artById = @{}
if (Test-Path -LiteralPath $artLedgerPath) {
    foreach ($artOld in (Get-Content -LiteralPath $artLedgerPath -Raw | ConvertFrom-Json)) { $artById[$artOld.artId] = $artOld }
}
$artBatch = @(Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json)
foreach ($artRow in $artBatch) {
    if ($artRow.version -ne 2 -or $artRow.artId -notmatch '^card_school_[a-z0-9_]+$') { throw 'Invalid selected art identity or revision.' }
    $artExpectedTarget = [IO.Path]::GetFullPath((Join-Path $artDestinationRoot ($artRow.artId + '.png')))
    if ([IO.Path]::GetFullPath($artRow.target) -ne $artExpectedTarget -or -not $artExpectedTarget.StartsWith($artDestinationRoot + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Art target escaped the resource directory.' }
    if (-not (Test-Path -LiteralPath $artRow.source -PathType Leaf)) { throw ('Missing generated original: '+$artRow.source) }
    Copy-Item -LiteralPath $artRow.source -Destination $artExpectedTarget -Force
    $artById[$artRow.artId] = $artRow
}
@($artById.Values | Sort-Object artId) | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $artLedgerPath -Encoding utf8
& 'D:\Game\Artifacts\CardSchoolPresentation20261011\EnsureArtMetadata.ps1'
Write-Output ('Western version selected: '+$artById.Count+'; installed this batch: '+$artBatch.Count)
