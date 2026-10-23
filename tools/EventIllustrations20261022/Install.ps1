param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$EventId,
    [string]$Prompt = '',
    [string]$Group = 'moon'
)
$ErrorActionPreference = 'Stop'
if ($EventId -notmatch '^[a-z0-9_]+$' -or $Group -notmatch '^[a-z0-9_-]+$') { throw 'Invalid event/group name.' }
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$resourceRoot = Join-Path $projectRoot 'UnityClient/Assets/_Project/Resources/EncounterIllustrations'
$outputRoot = Join-Path $projectRoot 'outputs/imagegen/event-illustrations-20261022'
New-Item -ItemType Directory -Path $resourceRoot,$outputRoot -Force | Out-Null
$isAtlas = $EventId.StartsWith('legacy_')
$fileName = if ($isAtlas) { $EventId + '.png' } else { $EventId + '_v1.png' }
$maxTexture = if ($isAtlas) { 4096 } else { 2048 }
$destination = Join-Path $resourceRoot $fileName
if (Test-Path -LiteralPath $destination) { throw "Refusing to overwrite $destination" }
Copy-Item -LiteralPath $Source -Destination $destination
$guid = [guid]::NewGuid().ToString('N')
$meta = @"
fileFormatVersion: 2
guid: $guid
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  streamingMipmaps: 0
  textureFormat: 1
  maxTextureSize: $maxTexture
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
  alphaUsage: 0
  alphaIsTransparency: 0
  textureType: 0
  textureShape: 1
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: $maxTexture
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 100
    overridden: 0
  - serializedVersion: 4
    buildTarget: Standalone
    maxTextureSize: $maxTexture
    resizeAlgorithm: 0
    textureFormat: 25
    textureCompression: 2
    compressionQuality: 100
    overridden: 1
  spriteSheet:
    serializedVersion: 2
    sprites: []
  userData: Built-in image_gen event illustration 2026-10-22
  assetBundleName: ''
  assetBundleVariant: ''
"@
[IO.File]::WriteAllText($destination + '.meta', $meta + "`n", [Text.UTF8Encoding]::new($false))
$record = [ordered]@{ eventId = $EventId; source = $Source; destination = $destination; tool = 'built-in image_gen'; prompt = $Prompt }
$recordRoot = Join-Path $outputRoot $Group
New-Item -ItemType Directory -Path $recordRoot -Force | Out-Null
$record | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $recordRoot ($EventId + '.json')) -Encoding utf8
Write-Output $destination
