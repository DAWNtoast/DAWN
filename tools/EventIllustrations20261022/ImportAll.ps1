param([switch]$AllowPartial)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$outputRoot = Join-Path $projectRoot 'outputs/imagegen/event-illustrations-20261022'
$resourceRoot = Join-Path $projectRoot 'UnityClient/Assets/_Project/Resources/EncounterIllustrations'
$records = @()
foreach ($manifest in Get-ChildItem -LiteralPath $outputRoot -Filter '*-prompts.json' -File) {
    $records += @(Get-Content -LiteralPath $manifest.FullName -Raw | ConvertFrom-Json)
}
$duplicates = @($records | Group-Object eventId | Where-Object Count -gt 1)
if ($duplicates.Count) { throw ('Duplicate IDs: ' + ($duplicates.Name -join ', ')) }
if (!$AllowPartial -and $records.Count -ne 93) { throw "Expected 93 final records, got $($records.Count)." }
foreach ($record in $records) {
    if (!$record.eventId -or !$record.source -or !$record.prompt) { throw 'Invalid manifest record.' }
    if (!(Test-Path -LiteralPath $record.source)) { throw "Missing source $($record.source)" }
}
foreach ($record in $records) {
    $name = if ($record.eventId.StartsWith('legacy_')) { $record.eventId + '.png' } else { $record.eventId + '_v1.png' }
    $destination = Join-Path $resourceRoot $name
    if (Test-Path -LiteralPath $destination) {
        if ((Get-FileHash -LiteralPath $destination).Hash -ne (Get-FileHash -LiteralPath $record.source).Hash) { throw "Existing asset differs: $destination" }
        if (!(Test-Path -LiteralPath ($destination + '.meta'))) { throw "Missing metadata: $destination" }
        $metadata = [IO.File]::ReadAllText($destination + '.meta')
        if ($metadata.Contains('Built-in image_gen event illustration 2026-10-22')) {
            $metadata = [regex]::Replace($metadata, '(?m)^  assetBundleName:[ \t]*\r?$', "  assetBundleName: ''")
            $metadata = [regex]::Replace($metadata, '(?m)^  assetBundleVariant:[ \t]*\r?$', "  assetBundleVariant: ''")
            [IO.File]::WriteAllText($destination + '.meta', $metadata.TrimEnd() + "`n", [Text.UTF8Encoding]::new($false))
        }
        continue
    }
    $group = if ($record.eventId.StartsWith('legacy_')) { 'atlas' } elseif ($record.eventId.Contains('_moon_')) { 'moon' } elseif ($record.eventId.Contains('_trade_')) { 'trade' } elseif ($record.eventId.Contains('_reef_') -or $record.eventId -eq 'common_whale_song') { 'reef' } else { 'shared' }
    & (Join-Path $PSScriptRoot 'Install.ps1') -Source $record.source -EventId $record.eventId -Prompt $record.prompt -Group $group
}
Add-Type -AssemblyName System.Drawing
$titles = @{}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $projectRoot 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' -File) {
    $text = [IO.File]::ReadAllText($file.FullName)
    foreach ($match in [regex]::Matches($text, 'case "(?<id>[^"]+)":\s*return R\(id, "(?<name>[^"]+)"')) { $titles[$match.Groups['id'].Value] = $match.Groups['name'].Value }
    foreach ($match in [regex]::Matches($text, '(?:E|F|new CoreEncounter)\(\s*"(?<id>[^"]+)"\s*,\s*"(?<name>[^"]+)"')) { $titles[$match.Groups['id'].Value] = $match.Groups['name'].Value }
}
$titles['provisions_spoilage'] = '粮仓霉变（共用）'
$titles['common_whale_song'] = '鲸群回响'
$titles['legacy_reef_v3'] = '碎帆礁海旧事件重绘图集'
$titles['legacy_trade_v3'] = '金潮商路旧事件重绘图集'
$titles['legacy_moon_v3'] = '月潮遗迹旧事件重绘图集'
$titles['legacy_treasure_v3'] = '藏宝旧事件重绘图集'
$review = foreach ($record in $records) {
    $name = if ($record.eventId.StartsWith('legacy_')) { $record.eventId + '.png' } else { $record.eventId + '_v1.png' }
    $asset = Join-Path $resourceRoot $name
    $bytes = [IO.File]::ReadAllBytes($asset)
    if ($bytes[0] -ne 137 -or $bytes[1] -ne 80 -or $bytes[2] -ne 78 -or $bytes[3] -ne 71) { throw "Invalid PNG: $asset" }
    $width = [int]($bytes[16]*16777216 + $bytes[17]*65536 + $bytes[18]*256 + $bytes[19])
    $height = [int]($bytes[20]*16777216 + $bytes[21]*65536 + $bytes[22]*256 + $bytes[23])
    if ($width -lt 1024 -or $height -lt 768) { throw "Unexpected small image: $asset" }
    [pscustomobject]@{ eventId=$record.eventId; title=$titles[$record.eventId]; path=$asset; width=$width; height=$height; bytes=$bytes.Length; sha256=(Get-FileHash -LiteralPath $asset).Hash }
}
$review | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $outputRoot 'asset-validation.json') -Encoding utf8
$review | Export-Csv -LiteralPath (Join-Path $outputRoot 'asset-catalog.csv') -NoTypeInformation -Encoding utf8
@{ tool='built-in image_gen'; generatedOn='2026-10-22'; resourceCount=$records.Count; assets=$records } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outputRoot 'generation-manifest.json') -Encoding utf8
foreach ($region in @('reef','trade','moon','treasure')) {
    $items = @($review | Where-Object { !$_.eventId.StartsWith('legacy_') -and ($_.eventId.Contains('_' + $region + '_') -or ($region -eq 'reef' -and $_.eventId -eq 'common_whale_song') -or ($region -eq 'trade' -and $_.eventId -eq 'provisions_spoilage') -or ($region -eq 'treasure' -and $_.eventId -match '^(weathered_|moonlit_)')) } | Sort-Object eventId)
    if ($items.Count -eq 0) { continue }
    $columns=4; $cellWidth=360; $cellHeight=302
    $rows=[int][Math]::Ceiling($items.Count / $columns)
    $canvas=[Drawing.Bitmap]::new($columns*$cellWidth,$rows*$cellHeight)
    $graphics=[Drawing.Graphics]::FromImage($canvas)
    $graphics.Clear([Drawing.Color]::FromArgb(15,25,32))
    $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $font=[Drawing.Font]::new('Microsoft YaHei',12)
    $small=[Drawing.Font]::new('Consolas',8)
    $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(228,222,205))
    for ($i=0;$i -lt $items.Count;$i++) {
        $item=$items[$i]; $x=($i % $columns)*$cellWidth; $y=[Math]::Floor($i/$columns)*$cellHeight
        $picture=[Drawing.Image]::FromFile($item.path)
        $graphics.DrawImage($picture,[Drawing.Rectangle]::new($x+6,$y+6,348,261))
        $picture.Dispose()
        $title=if($item.title){$item.title}else{$item.eventId}
        $graphics.DrawString($title,$font,$brush,$x+8,$y+267)
        $graphics.DrawString($item.eventId,$small,$brush,$x+8,$y+286)
    }
    $canvas.Save((Join-Path $outputRoot ($region + '-contact-sheet.jpg')),[Drawing.Imaging.ImageFormat]::Jpeg)
    $brush.Dispose();$small.Dispose();$font.Dispose();$graphics.Dispose();$canvas.Dispose()
}
[pscustomobject]@{ resourceCount=$review.Count; totalMegabytes=[Math]::Round(($review | Measure-Object bytes -Sum).Sum/1MB,2); minimumWidth=($review | Measure-Object width -Minimum).Minimum; minimumHeight=($review | Measure-Object height -Minimum).Minimum; outputRoot=$outputRoot } | ConvertTo-Json
