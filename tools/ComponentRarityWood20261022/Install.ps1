$ErrorActionPreference = 'Stop'
$assetDirectory = 'D:\Game\UnityClient\Assets\_Project\Resources\UIWood\ComponentRarity'
$recordPath = 'D:\Game\outputs\imagegen\component-rarity-wood-20261022\generation-record.json'
$generation = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
[IO.Directory]::CreateDirectory($assetDirectory) | Out-Null
$folderMeta = $assetDirectory + '.meta'
if (-not (Test-Path -LiteralPath $folderMeta)) {
    [IO.File]::WriteAllText($folderMeta, "fileFormatVersion: 2`nguid: " + [Guid]::NewGuid().ToString('N') + "`nfolderAsset: yes`nDefaultImporter:`n  externalObjects: {}`n  userData:`n  assetBundleName:`n  assetBundleVariant:`n", [Text.UTF8Encoding]::new($false))
}
$template = [IO.File]::ReadAllText('D:\Game\UnityClient\Assets\_Project\Resources\UIWood\wood_button_face_v1.png.meta')
$verification = foreach ($asset in $generation.assets) {
    $target = [IO.Path]::GetFullPath($asset.target)
    if (-not $target.StartsWith($assetDirectory + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected asset target.' }
    if (Test-Path -LiteralPath $target) {
        if ((Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $asset.source).Hash) { throw ('Refusing to overwrite different asset: ' + $target) }
    } else { Copy-Item -LiteralPath $asset.source -Destination $target }
    if (-not (Test-Path -LiteralPath ($target + '.meta'))) {
        $meta = $template -replace 'guid: [a-f0-9]+', ('guid: ' + [Guid]::NewGuid().ToString('N'))
        [IO.File]::WriteAllText($target + '.meta', $meta, [Text.UTF8Encoding]::new($false))
    }
    $bytes = [IO.File]::ReadAllBytes($target)
    $width = [Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes,16))
    $height = [Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes,20))
    [pscustomobject]@{ Rarity=$asset.rarity; Target=$target; Width=$width; Height=$height; SHA256=(Get-FileHash -LiteralPath $target).Hash }
}
$verification | ConvertTo-Json | Set-Content -LiteralPath 'D:\Game\outputs\imagegen\component-rarity-wood-20261022\asset-verification.json' -Encoding utf8
$verification | Format-Table Rarity,Width,Height,Target -AutoSize
