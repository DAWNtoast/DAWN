$ErrorActionPreference = 'Stop'
$cardWesternRoot = 'D:\Game\UnityClient\Assets\_Project\Resources\CardArt'
$cardWesternDirectory = Join-Path $cardWesternRoot 'Western'
[IO.Directory]::CreateDirectory($cardWesternDirectory) | Out-Null
foreach ($cardWesternFolder in @($cardWesternRoot, $cardWesternDirectory)) {
    $cardWesternFolderMeta = $cardWesternFolder + '.meta'
    if (Test-Path -LiteralPath $cardWesternFolderMeta) { continue }
    $cardWesternFolderText = @(
        'fileFormatVersion: 2', ('guid: ' + [Guid]::NewGuid().ToString('N')), 'folderAsset: yes',
        'DefaultImporter:', '  externalObjects: {}', '  userData: ', '  assetBundleName: ', '  assetBundleVariant: '
    ) -join "`n"
    [IO.File]::WriteAllText($cardWesternFolderMeta, $cardWesternFolderText + "`n", [Text.UTF8Encoding]::new($false))
}
$cardWesternTemplate = [IO.File]::ReadAllText('D:\Game\UnityClient\Assets\_Project\Resources\CardSchools\Art\card_school_fire_l03.png.meta')
$cardWesternCreated = 0
foreach ($cardWesternImage in Get-ChildItem -LiteralPath $cardWesternDirectory -Filter '*.png') {
    if ($cardWesternImage.BaseName -notmatch '^card_[a-z0-9_]+$') { throw ('Expected stable CardId filename: ' + $cardWesternImage.Name) }
    $cardWesternMeta = $cardWesternImage.FullName + '.meta'
    if (Test-Path -LiteralPath $cardWesternMeta) { continue }
    $cardWesternText = [regex]::Replace($cardWesternTemplate, '(?m)^guid:.*$', ('guid: ' + [Guid]::NewGuid().ToString('N')))
    [IO.File]::WriteAllText($cardWesternMeta, $cardWesternText, [Text.UTF8Encoding]::new($false))
    $cardWesternCreated++
}
Write-Output ('Created ' + $cardWesternCreated + ' missing Western CardId metadata files (512px, BC7); existing GUIDs preserved.')
