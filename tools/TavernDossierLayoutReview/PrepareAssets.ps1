param([switch]$RequireComplete)
$ErrorActionPreference = 'Stop'
$taskRoot = 'D:\Game\UnityClient\Assets\_Project\Resources\PortArt\CrewDossiers'
$taskManifest = Get-Content -LiteralPath 'D:\Game\outputs\imagegen\tavern-dossier\crew-manifest.json' -Raw | ConvertFrom-Json
if ($RequireComplete) {
    foreach ($taskCrew in $taskManifest) {
        if (-not (Test-Path -LiteralPath (Join-Path $taskRoot ($taskCrew.id + '_dossier_v1.png')))) { throw ('Missing ' + $taskCrew.id) }
    }
}
if (-not (Test-Path -LiteralPath ($taskRoot + '.meta'))) {
    [IO.File]::WriteAllText($taskRoot + '.meta', "fileFormatVersion: 2`nguid: $([Guid]::NewGuid().ToString('N'))`nfolderAsset: yes`nDefaultImporter:`n  externalObjects: {}`n  userData: `n  assetBundleName: `n  assetBundleVariant: `n", [Text.UTF8Encoding]::new($false))
}
foreach ($taskImage in Get-ChildItem -LiteralPath $taskRoot -Filter '*.png') {
    $taskMeta = $taskImage.FullName + '.meta'
    if (Test-Path -LiteralPath $taskMeta) {
        $taskExisting = [IO.File]::ReadAllText($taskMeta)
        if (-not $taskExisting.EndsWith("`n")) { [IO.File]::WriteAllText($taskMeta, $taskExisting + "`n", [Text.UTF8Encoding]::new($false)) }
        continue
    }
    $taskImport = @"
fileFormatVersion: 2
guid: $([Guid]::NewGuid().ToString('N'))
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  isReadable: 0
  streamingMipmaps: 0
  maxTextureSize: 4096
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  compressionQuality: 100
  spriteMode: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 0
  textureShape: 1
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 4096
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 100
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
  userData: Integrated crew dossier. Native resolution and authored alpha.
  assetBundleName:
  assetBundleVariant:
"@
    [IO.File]::WriteAllText($taskMeta, $taskImport + "`n", [Text.UTF8Encoding]::new($false))
}
Write-Output ('Prepared ' + @(Get-ChildItem -LiteralPath $taskRoot -Filter '*.png').Count + ' dossier textures.')
