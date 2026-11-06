param(
    [string]$Manifest = 'D:/Game/outputs/new-card-art-release-20261024/installed-art-manifest.json',
    [string]$SmokeDirectory = ''
)
$ErrorActionPreference = 'Stop'
$taskEntries = @(Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json)
$taskExpected = @('card_school_arcane_n04','card_school_arcane_n05','card_school_arcane_n06','card_school_arcane_n07',
    'card_school_fire_n07','card_school_fire_n08','card_school_thunder_n07','card_school_thunder_n08','card_school_thunder_n09')
if ($taskEntries.Count -ne 9 -or @(Compare-Object ($taskExpected | Sort-Object) ($taskEntries.id | Sort-Object)).Count) {
    throw 'The installation manifest does not contain exactly the approved nine IDs.'
}
$taskGuids = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$taskHashes = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($entry in $taskEntries) {
    if ($entry.resourcePath -ne ('CardArt/Western/' + $entry.id)) { throw ('Wrong resource path: ' + $entry.id) }
    if ((Get-FileHash -LiteralPath $entry.source).Hash -ne $entry.sourceSHA256 -or
        (Get-FileHash -LiteralPath $entry.asset).Hash -ne $entry.sourceSHA256 -or $entry.assetSHA256 -ne $entry.sourceSHA256) {
        throw ('Installed or generated painting changed: ' + $entry.id)
    }
    if ((Get-FileHash -LiteralPath ($entry.asset + '.meta')).Hash -ne $entry.metaSHA256) { throw ('Metadata changed: ' + $entry.id) }
    if (-not $taskGuids.Add([string]$entry.metaGUID) -or -not $taskHashes.Add([string]$entry.sourceSHA256)) { throw 'Repeated artwork or metadata GUID.' }
}
if ($SmokeDirectory) {
    $taskSmoke = @(Import-Csv -LiteralPath (Join-Path $SmokeDirectory 'packaged-school-card-assets.csv'))
    foreach ($entry in $taskEntries) {
        $rows = @($taskSmoke | Where-Object cardId -eq $entry.id)
        if ($rows.Count -ne 1 -or $rows[0].resourcePath -ne $entry.resourcePath -or
            $rows[0].width -ne '1024' -or $rows[0].height -ne '1024' -or
            $rows[0].fullSourceUv -ne 'True' -or $rows[0].sourceAspectPreserved -ne 'True') {
            throw ('Packaged player did not load the complete dedicated painting: ' + $entry.id)
        }
    }
    Write-Output 'PASS: the built player loaded all nine dedicated paintings with complete square UVs at 1024 pixels.'
}
Write-Output 'PASS: nine distinct source paintings, installed files and metadata match the installation manifest.'
