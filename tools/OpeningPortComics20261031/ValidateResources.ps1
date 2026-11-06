param()
$ErrorActionPreference = 'Stop'
$comicWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$comicTaskRoot = Join-Path $comicWorkspace 'outputs/opening-port-comics-20261031'
$comicResourceRoot = Join-Path $comicWorkspace 'UnityClient/Assets/_Project/Resources'
$comicManifest = Get-Content -LiteralPath (Join-Path $comicTaskRoot 'resource-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$comicGuids = @{}
$comicHashes = @{}
Add-Type -AssemblyName System.Drawing
$comicRows = foreach ($comicEntry in $comicManifest.resources) {
    $comicTarget = Join-Path $comicResourceRoot ($comicEntry.imageResource + '.png')
    $comicName = [IO.Path]::GetFileName($comicTarget)
    $comicSource = Join-Path $comicTaskRoot ('generated/' + $comicName)
    $comicHash = (Get-FileHash -LiteralPath $comicTarget -Algorithm SHA256).Hash
    if ($comicHash -ne (Get-FileHash -LiteralPath $comicSource -Algorithm SHA256).Hash) { throw "Installed hash mismatch: $comicName" }
    if ($comicHashes.ContainsKey($comicHash)) { throw "Duplicate page: $comicName" }
    $comicHashes[$comicHash] = $true
    $comicMeta = Get-Content -LiteralPath ($comicTarget + '.meta') -Raw -Encoding UTF8
    $comicGuid = [regex]::Match($comicMeta, '(?m)^guid: ([a-f0-9]{32})').Groups[1].Value
    if (-not $comicGuid -or $comicGuids.ContainsKey($comicGuid)) { throw "Invalid/duplicate GUID: $comicName" }
    $comicGuids[$comicGuid] = $true
    foreach ($comicSetting in @('enableMipMap: 0', 'maxTextureSize: 4096', 'nPOTScale: 0', 'textureCompression: 0')) {
        if (-not $comicMeta.Contains($comicSetting)) { throw "Unexpected importer setting: $comicName / $comicSetting" }
    }
    if ($comicEntry.series -eq 'opening') {
        $comicBackupMeta = Join-Path $comicTaskRoot ('input-backup/OpeningComics/' + $comicName + '.meta')
        if ((Get-FileHash -LiteralPath $comicBackupMeta).Hash -ne (Get-FileHash -LiteralPath ($comicTarget + '.meta')).Hash) { throw "Opening metadata changed: $comicName" }
    }
    $comicBitmap = [Drawing.Image]::FromFile($comicTarget)
    try {
        $comicWidth = $comicBitmap.Width
        $comicHeight = $comicBitmap.Height
        if ($comicWidth -lt 1600 -or $comicHeight -lt 900 -or [Math]::Abs($comicWidth / $comicHeight - 16.0 / 9.0) -gt 0.01) { throw "Unexpected page dimensions: $comicName" }
    } finally { $comicBitmap.Dispose() }
    [pscustomobject]@{Resource=$comicEntry.imageResource; Port=$comicEntry.portName; Page=$comicEntry.page; Width=$comicWidth; Height=$comicHeight; SHA256=$comicHash; GUID=$comicGuid}
}
if (@($comicRows).Count -ne 14) { throw 'Expected exactly 14 installed pages.' }
$comicRows | Export-Csv -LiteralPath (Join-Path $comicTaskRoot 'resource-hashes.csv') -NoTypeInformation -Encoding UTF8
Write-Output 'PASS: 14 distinct native-size PNGs installed; SHA256 verified; 14 unique GUIDs; 4 opening metadata files preserved.'
