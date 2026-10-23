param([Parameter(Mandatory=$true)][string]$RecordJson)
$ErrorActionPreference = 'Stop'
$record = $RecordJson | ConvertFrom-Json
$root = 'D:\Game\UnityClient\Assets\_Project\Resources\EncounterIllustrations\Reviewed20261023'
if ($record.key -notmatch '^[a-z0-9_]+$') { throw 'Invalid key' }
$target = Join-Path $root ($record.key + '.png')
[System.IO.Directory]::CreateDirectory($root) | Out-Null
Copy-Item -LiteralPath $record.source -Destination $target -Force
$template = 'D:\Game\UnityClient\Assets\_Project\Resources\EncounterIllustrations\event_trade_v2_courier_pigeon_v1.png.meta'
$metaPath = $target + '.meta'
if (-not (Test-Path -LiteralPath $metaPath)) {
  $meta = [System.IO.File]::ReadAllText($template)
  $meta = $meta -replace 'guid: [0-9a-f]+', ('guid: ' + [guid]::NewGuid().ToString('N'))
  $meta = $meta -replace 'Built-in image_gen event illustration 2026-10-22', 'Built-in image_gen reviewed event illustration 2026-10-23'
  [System.IO.File]::WriteAllText($metaPath, $meta, [System.Text.UTF8Encoding]::new($false))
}
$record | Add-Member -Force NoteProperty target $target
$record | Add-Member -Force NoteProperty sha256 ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLower())
$pngBytes = [System.IO.File]::ReadAllBytes($target)
$widthBytes = $pngBytes[16..19]
$heightBytes = $pngBytes[20..23]
[array]::Reverse($widthBytes)
[array]::Reverse($heightBytes)
$record | Add-Member -Force NoteProperty width ([BitConverter]::ToInt32($widthBytes, 0))
$record | Add-Member -Force NoteProperty height ([BitConverter]::ToInt32($heightBytes, 0))
$manifestPath = 'D:\Game\outputs\event-consistency-audit-20261023\generated-trade-moon.json'
$rows = @()
if (Test-Path -LiteralPath $manifestPath) { $rows = @((Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json) | Where-Object { $_.key -ne $record.key }) }
$rows += $record
[System.IO.File]::WriteAllText($manifestPath, (ConvertTo-Json -InputObject @($rows) -Depth 10), [System.Text.UTF8Encoding]::new($false))
$record | ConvertTo-Json -Depth 5 -Compress
