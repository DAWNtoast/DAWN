param([switch]$Install)
$ErrorActionPreference = 'Stop'
$taskRoot = 'D:\Game\outputs\art-resource-optimization-20261104'
$resourceRoot = 'D:\Game\UnityClient\Assets\_Project\Resources'
$reviewedRoot = Join-Path $taskRoot 'reviewed-panels'
$destinationRoot = Join-Path $resourceRoot 'ComicPanelsHD'
$plan = Get-Content -LiteralPath (Join-Path $taskRoot 'comic-source-plan.json') -Raw | ConvertFrom-Json
$rows = [Collections.Generic.List[object]]::new()
$missing = [Collections.Generic.List[string]]::new()

function Read-PngSize([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    try {
        $bytes = New-Object byte[] 24
        if ($stream.Read($bytes, 0, 24) -ne 24 -or $bytes[0] -ne 137 -or $bytes[1] -ne 80) { throw "Invalid PNG: $Path" }
        $width = [int64]$bytes[16]*16777216 + [int64]$bytes[17]*65536 + [int64]$bytes[18]*256 + $bytes[19]
        $height = [int64]$bytes[20]*16777216 + [int64]$bytes[21]*65536 + [int64]$bytes[22]*256 + $bytes[23]
        return @($width, $height)
    } finally { $stream.Dispose() }
}

foreach ($page in $plan.pages) {
    if ((Get-FileHash -LiteralPath $page.sourcePath -Algorithm SHA256).Hash -ne $page.sourceSha256) {
        throw ('Original reference changed after review: ' + $page.sourcePath)
    }
    foreach ($panel in 1..4) {
        $relative = $page.imageResource + '_panel' + $panel.ToString('00') + '.png'
        $source = Join-Path $reviewedRoot $relative
        if (!(Test-Path -LiteralPath $source)) { $missing.Add($relative); continue }
        $size = Read-PngSize $source
        if ($size[0] -lt 1600 -or $size[1] -lt 900 -or [Math]::Abs($size[0]/[double]$size[1] - 16.0/9.0) -gt .015) {
            throw ('Independent panel lacks native HD dimensions/aspect: ' + $source)
        }
        $rows.Add([pscustomobject]@{
            Original=$page.sourcePath; OriginalSHA256=$page.sourceSha256; Panel=$panel
            Reviewed=$source; Resource=('ComicPanelsHD/' + $relative.Substring(0,$relative.Length-4))
            Destination=(Join-Path $destinationRoot $relative); Width=$size[0]; Height=$size[1]
            SHA256=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
            LinearResolutionGain=([Math]::Round($size[0]/[double]$page.nominalQuadrantWidth,3))
        })
    }
}
if ($missing.Count) {
    [pscustomobject]@{Ready=$rows.Count;Expected=104;Missing=$missing.ToArray()} | ConvertTo-Json -Depth 4
    if ($Install) { throw 'All 104 reviewed slots must exist before production installation.' }
    exit
}
if ($rows.Count -ne 104) { throw 'Unexpected comic-panel plan size.' }
if (!$Install) { [pscustomobject]@{Ready=$rows.Count;Expected=104;Missing=@()} | ConvertTo-Json; exit }

$template = Get-Content -LiteralPath (Join-Path $resourceRoot 'OpeningComics\opening_page01.png.meta') -Raw
foreach ($directory in @($destinationRoot, (Join-Path $destinationRoot 'OpeningComics'),
    (Join-Path $destinationRoot 'ArrivalComics'), (Join-Path $destinationRoot 'LegendaryFleetComics'))) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    if (!(Test-Path -LiteralPath ($directory+'.meta'))) {
        $guid=[Guid]::NewGuid().ToString('N')
        "fileFormatVersion: 2`nguid: $guid`nfolderAsset: yes`nDefaultImporter:`n  externalObjects: {}`n  userData: `n  assetBundleName: `n  assetBundleVariant: `n" |
            Set-Content -LiteralPath ($directory+'.meta') -Encoding utf8
    }
}
foreach ($row in $rows) {
    if (Test-Path -LiteralPath $row.Destination) {
        if ((Get-FileHash -LiteralPath $row.Destination).Hash -ne $row.SHA256) { throw ('Unexpected existing production panel: '+$row.Destination) }
    } else { Copy-Item -LiteralPath $row.Reviewed -Destination $row.Destination }
    if (!(Test-Path -LiteralPath ($row.Destination+'.meta'))) {
        $meta=[regex]::Replace($template,'(?m)^guid: .*$',('guid: '+[Guid]::NewGuid().ToString('N')))
        [IO.File]::WriteAllText($row.Destination+'.meta',$meta,[Text.UTF8Encoding]::new($false))
    }
    if ((Get-FileHash -LiteralPath $row.Destination).Hash -ne $row.SHA256) { throw ('Installation hash mismatch: '+$row.Destination) }
}
$rows.ToArray() | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskRoot 'installed-comic-panels.json') -Encoding utf8
Write-Output ('Installed and hash-verified '+$rows.Count+' native HD panels; original pages untouched. No resampling or image processing performed.')
