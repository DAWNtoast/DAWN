param()
$ErrorActionPreference = 'Stop'
$comicWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$comicTaskRoot = Join-Path $comicWorkspace 'outputs/opening-port-comics-20261031'
$comicResourceRoot = Join-Path $comicWorkspace 'UnityClient/Assets/_Project/Resources'
$comicManifest = Get-Content -LiteralPath (Join-Path $comicTaskRoot 'resource-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$comicTemplate = Get-Content -LiteralPath (Join-Path $comicTaskRoot 'input-backup/OpeningComics/opening_page01.png.meta') -Raw -Encoding UTF8
$comicUtf8 = [Text.UTF8Encoding]::new($false)
if ($comicManifest.resources.Count -ne 14) { throw 'Expected exactly 14 approved pages.' }
$comicSources = @{}
foreach ($comicEntry in $comicManifest.resources) {
    $comicName = [IO.Path]::GetFileName($comicEntry.destinationFile)
    $comicSource = Join-Path $comicTaskRoot ('generated/' + $comicName)
    $comicTarget = [IO.Path]::GetFullPath((Join-Path $comicResourceRoot ($comicEntry.imageResource + '.png')))
    if (-not $comicTarget.StartsWith($comicResourceRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Asset target outside resources.' }
    if (-not (Test-Path -LiteralPath $comicSource -PathType Leaf)) { throw "Missing approved source: $comicSource" }
    $comicHash = (Get-FileHash -LiteralPath $comicSource -Algorithm SHA256).Hash
    if ($comicSources.ContainsKey($comicHash)) { throw "Duplicated page: $comicName" }
    $comicSources[$comicHash] = $comicName
    if ($comicEntry.series -eq 'opening') {
        $comicBackup = Join-Path $comicTaskRoot ('input-backup/OpeningComics/' + $comicName)
        if (-not (Test-Path -LiteralPath $comicBackup -PathType Leaf)) { throw 'Opening input backup missing.' }
        if ((Get-FileHash -LiteralPath ($comicBackup + '.meta')).Hash -ne (Get-FileHash -LiteralPath ($comicTarget + '.meta')).Hash) { throw "Opening meta has changed: $comicName" }
        if (Test-Path -LiteralPath $comicTarget) {
            $comicExistingHash = (Get-FileHash -LiteralPath $comicTarget).Hash
            if ($comicExistingHash -ne $comicHash -and $comicExistingHash -ne (Get-FileHash -LiteralPath $comicBackup).Hash) { throw "Unreviewed opening asset modification: $comicName" }
        }
    } elseif (Test-Path -LiteralPath $comicTarget) {
        if ((Get-FileHash -LiteralPath $comicTarget).Hash -ne $comicHash) { throw "Unreviewed arrival asset modification: $comicName" }
    }
}
$comicArrivalRoot = Join-Path $comicResourceRoot 'ArrivalComics'
New-Item -ItemType Directory -Path $comicArrivalRoot -Force | Out-Null
if (-not (Test-Path -LiteralPath ($comicArrivalRoot + '.meta'))) {
    [IO.File]::WriteAllText($comicArrivalRoot + '.meta', "fileFormatVersion: 2`nguid: $([guid]::NewGuid().ToString('N'))`nfolderAsset: yes`nDefaultImporter:`n  externalObjects: {}`n  userData: `n  assetBundleName: `n  assetBundleVariant: `n", $comicUtf8)
}
foreach ($comicEntry in $comicManifest.resources) {
    $comicName = [IO.Path]::GetFileName($comicEntry.destinationFile)
    $comicSource = Join-Path $comicTaskRoot ('generated/' + $comicName)
    $comicTarget = Join-Path $comicResourceRoot ($comicEntry.imageResource + '.png')
    Copy-Item -LiteralPath $comicSource -Destination $comicTarget
    if (-not (Test-Path -LiteralPath ($comicTarget + '.meta'))) {
        $comicMeta = [regex]::Replace($comicTemplate, '(?m)^guid: [a-fA-F0-9]+\r?$', ('guid: ' + [guid]::NewGuid().ToString('N')))
        [IO.File]::WriteAllText($comicTarget + '.meta', $comicMeta, $comicUtf8)
    }
}
& (Join-Path $PSScriptRoot 'ValidateResources.ps1')
