param()
$ErrorActionPreference = 'Stop'
$comicWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$comicTaskRoot = Join-Path $comicWorkspace 'outputs/legendary-comics-20261031'
$comicSourceRoot = Join-Path $comicTaskRoot 'generated'
$comicDestination = Join-Path $comicWorkspace 'UnityClient/Assets/_Project/Resources/LegendaryFleetComics'
$comicKeys = @('iron_oath', 'red_widow', 'thunder_crown', 'ember_ark', 'tide_requiem', 'last_horizon')
$comicTemplate = Get-Content -LiteralPath (Join-Path $comicWorkspace 'UnityClient/Assets/_Project/Resources/OpeningComics/opening_page01.png.meta') -Raw -Encoding UTF8
$comicUtf8 = [Text.UTF8Encoding]::new($false)
foreach ($comicKey in $comicKeys) {
    foreach ($comicTiming in @('before', 'after')) {
        $comicSource = Join-Path $comicSourceRoot ($comicKey + '_' + $comicTiming + '.png')
        if (-not (Test-Path -LiteralPath $comicSource -PathType Leaf)) { throw "Missing approved source: $comicSource" }
        $comicTarget = Join-Path $comicDestination ([IO.Path]::GetFileName($comicSource))
        if (Test-Path -LiteralPath $comicTarget) {
            if ((Get-FileHash -LiteralPath $comicSource).Hash -ne (Get-FileHash -LiteralPath $comicTarget).Hash) {
                throw "Existing asset differs; back it up and review before replacing: $comicTarget"
            }
        }
    }
}
New-Item -ItemType Directory -Path $comicDestination -Force | Out-Null
$comicFolderMeta = $comicDestination + '.meta'
if (-not (Test-Path -LiteralPath $comicFolderMeta)) {
    [IO.File]::WriteAllText($comicFolderMeta, "fileFormatVersion: 2`nguid: $([guid]::NewGuid().ToString('N'))`nfolderAsset: yes`nDefaultImporter:`n  externalObjects: {}`n  userData: `n  assetBundleName: `n  assetBundleVariant: `n", $comicUtf8)
}
foreach ($comicKey in $comicKeys) {
    foreach ($comicTiming in @('before', 'after')) {
        $comicSource = Join-Path $comicSourceRoot ($comicKey + '_' + $comicTiming + '.png')
        $comicTarget = Join-Path $comicDestination ([IO.Path]::GetFileName($comicSource))
        Copy-Item -LiteralPath $comicSource -Destination $comicTarget
        if (-not (Test-Path -LiteralPath ($comicTarget + '.meta'))) {
            $comicMeta = [regex]::Replace($comicTemplate, '(?m)^guid: [a-fA-F0-9]+\r?$', ('guid: ' + [guid]::NewGuid().ToString('N')))
            [IO.File]::WriteAllText($comicTarget + '.meta', $comicMeta, $comicUtf8)
        }
        if ((Get-FileHash -LiteralPath $comicSource).Hash -ne (Get-FileHash -LiteralPath $comicTarget).Hash) { throw 'Copy hash mismatch.' }
    }
}
& (Join-Path $PSScriptRoot 'ValidateResources.ps1')
