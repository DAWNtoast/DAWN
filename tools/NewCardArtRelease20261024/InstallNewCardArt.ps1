param(
    [Parameter(Mandatory=$true)][string]$Manifest,
    [string]$Output = 'D:/Game/outputs/new-card-art-release-20261024'
)
$ErrorActionPreference = 'Stop'
$taskWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$taskOutput = [IO.Path]::GetFullPath($Output).TrimEnd('\','/')
$taskGenerated = Join-Path $taskOutput 'generated'
$taskAssets = Join-Path $taskWorkspace 'UnityClient/Assets'
$taskArtDirectory = Join-Path $taskAssets '_Project/Resources/CardArt/Western'
$taskExpected = @(
    'card_school_arcane_n04','card_school_arcane_n05','card_school_arcane_n06','card_school_arcane_n07',
    'card_school_fire_n07','card_school_fire_n08',
    'card_school_thunder_n07','card_school_thunder_n08','card_school_thunder_n09'
)
if (-not $taskOutput.StartsWith((Join-Path $taskWorkspace 'outputs') + '\',[StringComparison]::OrdinalIgnoreCase)) {
    throw 'Preview and audit outputs must stay inside this workspace/outputs.'
}
function Read-PngSize([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    try { $header = [byte[]]::new(24); if ($stream.Read($header,0,24) -ne 24) { throw 'Truncated PNG.' } }
    finally { $stream.Dispose() }
    if ([Convert]::ToHexString($header[0..7]) -ne '89504E470D0A1A0A' -or
        [Text.Encoding]::ASCII.GetString($header,12,4) -ne 'IHDR') { throw ('Expected a PNG: ' + $Path) }
    $width = [long]$header[16]*16777216 + [long]$header[17]*65536 + [long]$header[18]*256 + $header[19]
    $height = [long]$header[20]*16777216 + [long]$header[21]*65536 + [long]$header[22]*256 + $header[23]
    return @($width,$height)
}
$taskRows = @(Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json)
if ($taskRows.Count -ne 9 -or @(Compare-Object ($taskExpected | Sort-Object) ($taskRows.id | Sort-Object)).Count) {
    throw 'The manifest must contain exactly the nine approved new card IDs once each.'
}
$taskTemplatePath = Join-Path $taskArtDirectory 'card_boarding_fire_04.png.meta'
$taskTemplate = [IO.File]::ReadAllText($taskTemplatePath)
# Existing authored metadata can predate the current postprocessor. Normalize
# only the new copies so their first import cannot rewrite the release snapshot.
$taskTemplate = [regex]::Replace($taskTemplate,'(?m)^  compressionQuality: \d+','  compressionQuality: 100')
$taskExpectedSettings = @(
    '    enableMipMap: 0','    sRGBTexture: 1','  isReadable: 0',
    '  maxTextureSize: 1024','    filterMode: 1','    wrapU: 1','    wrapV: 1','    wrapW: 1',
    '  nPOTScale: 0','  compressionQuality: 100','  alphaUsage: 1','  alphaIsTransparency: 0',
    '  textureType: 0','  textureShape: 1'
)
foreach ($setting in $taskExpectedSettings) {
    if ($taskTemplate -notmatch ('(?m)^' + [regex]::Escape($setting) + '\r?$')) {
        throw ('Template no longer matches CardSchoolAssetImport: ' + $setting.Trim())
    }
}
foreach ($platform in @('DefaultTexturePlatform','Standalone')) {
    $platformBlock = [regex]::Match($taskTemplate,'(?ms)^    buildTarget: ' + $platform + '\r?\n(?:(?!^  - serializedVersion:|^  spriteSheet:).)*').Value
    $platformFormat = if ($platform -eq 'Standalone') { 25 } else { -1 }
    $platformOverride = if ($platform -eq 'Standalone') { 1 } else { 0 }
    foreach ($setting in @('maxTextureSize: 1024',('textureFormat: ' + $platformFormat),'textureCompression: 2',
        'compressionQuality: 100',('overridden: ' + $platformOverride))) {
        if ($platformBlock -notmatch ('(?m)^    ' + [regex]::Escape($setting) + '\r?$')) {
            throw ('Platform template no longer matches CardSchoolAssetImport: ' + $platform + ' / ' + $setting)
        }
    }
}
if ($taskTemplate -notmatch 'enableMipMap: 0' -or $taskTemplate -notmatch 'buildTarget: Standalone[\s\S]*?maxTextureSize: 1024[\s\S]*?textureFormat: 25' -or
    @([regex]::Matches($taskTemplate,'maxTextureSize: 1024')).Count -ne 3) {
    throw 'The current complete-card texture template no longer matches the reviewed 1024 / no-mip / BC7 policy.'
}
$taskHashes = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$taskPlans = foreach ($row in $taskRows) {
    $source = [IO.Path]::GetFullPath([string]$row.source)
    if (-not $source.StartsWith($taskGenerated + '\',[StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $source -PathType Leaf)) { throw ('Source must exist under generated/: ' + $source) }
    $size = Read-PngSize $source
    if ($size[0] -ne $size[1] -or $size[0] -lt 1024) { throw ('A complete square source of at least 1024 pixels is required: ' + $row.id) }
    $hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    if (-not $taskHashes.Add($hash)) { throw ('Repeated source artwork: ' + $row.id) }
    $destination = Join-Path $taskArtDirectory ($row.id + '.png')
    $meta = $destination + '.meta'
    if ((Test-Path -LiteralPath $destination) -and (Get-FileHash -LiteralPath $destination).Hash -ne $hash) {
        throw ('Refusing to overwrite existing artwork: ' + $destination)
    }
    if ((Test-Path -LiteralPath $meta) -and -not (Test-Path -LiteralPath $destination)) { throw ('Unexpected orphan metadata: ' + $meta) }
    [pscustomobject]@{ id=$row.id; name=$row.name; school=$row.school; source=$source; sourceSHA256=$hash;
        sourceWidth=$size[0]; sourceHeight=$size[1]; destination=$destination; meta=$meta }
}
$taskGuids = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($file in Get-ChildItem -LiteralPath $taskAssets -Filter '*.meta' -File -Recurse) {
    $match = [regex]::Match([IO.File]::ReadAllText($file.FullName),'(?m)^guid: ([a-fA-F0-9]{32})\s*$')
    if ($match.Success) { $null = $taskGuids.Add($match.Groups[1].Value) }
}
$taskInstalled = foreach ($plan in $taskPlans) {
    if (-not (Test-Path -LiteralPath $plan.destination)) { Copy-Item -LiteralPath $plan.source -Destination $plan.destination }
    if (-not (Test-Path -LiteralPath $plan.meta)) {
        do { $guid = [Guid]::NewGuid().ToString('N') } while (-not $taskGuids.Add($guid))
        $metadata = [regex]::Replace($taskTemplate,'(?m)^guid: [a-fA-F0-9]{32}',('guid: ' + $guid))
        [IO.File]::WriteAllText($plan.meta,$metadata,[Text.UTF8Encoding]::new($false))
    }
    $metadata = [IO.File]::ReadAllText($plan.meta)
    $guid = [regex]::Match($metadata,'(?m)^guid: ([a-fA-F0-9]{32})').Groups[1].Value
    if (-not $guid -or [regex]::Replace($metadata,'(?m)^guid: [a-fA-F0-9]{32}','guid: TEMPLATE') -ne
        [regex]::Replace($taskTemplate,'(?m)^guid: [a-fA-F0-9]{32}','guid: TEMPLATE')) { throw ('Unexpected texture import settings: ' + $plan.meta) }
    [pscustomobject]@{ id=$plan.id; name=$plan.name; school=$plan.school; source=$plan.source; sourceSHA256=$plan.sourceSHA256;
        sourceWidth=$plan.sourceWidth; sourceHeight=$plan.sourceHeight; asset=$plan.destination;
        assetSHA256=(Get-FileHash -LiteralPath $plan.destination).Hash; metaGUID=$guid;
        metaSHA256=(Get-FileHash -LiteralPath $plan.meta).Hash; resourcePath=('CardArt/Western/' + $plan.id);
        importedMaximum=1024; mipmaps=$false; compressionQuality=100; standaloneFormat='BC7'; fullSourceUV='0,0,1,1' }
}
$taskInstalled | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskOutput 'installed-art-manifest.json') -Encoding utf8
$taskCards = foreach ($entry in $taskInstalled) {
    $relative = [IO.Path]::GetRelativePath($taskOutput,$entry.source).Replace('\','/')
    $url = ($relative.Split('/') | ForEach-Object { [Uri]::EscapeDataString($_) }) -join '/'
    $name = [Net.WebUtility]::HtmlEncode([string]$entry.name)
    $school = [Net.WebUtility]::HtmlEncode([string]$entry.school)
    '<figure><a href="' + $url + '"><img src="' + $url + '" alt="' + $name + '"></a><figcaption><b>' + $name + '</b><span>' + $school + '</span><small>' + $entry.id + '</small></figcaption></figure>'
}
$taskHtml = @"
<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>新增卡牌插画 · 2026-10-24</title>
<style>body{margin:0;background:#111720;color:#e8e4db;font:16px/1.6 system-ui,sans-serif}main{max-width:1440px;margin:auto;padding:40px 28px}h1{font-size:30px;margin:0}p{color:#a9b5c1}section{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:24px}figure{margin:0;background:#1b2430;border:1px solid #394550;border-radius:12px;overflow:hidden}img{width:100%;display:block;aspect-ratio:1;object-fit:contain}figcaption{padding:14px 18px}b{font-size:21px}span{float:right;color:#c2b08a}small{display:block;color:#9daaba;font-size:12px}@media(max-width:800px){section{grid-template-columns:repeat(2,minmax(0,1fr))}}@media(max-width:480px){section{grid-template-columns:1fr}}</style>
<main><h1>新增卡牌插画</h1><p>九张独立插画 · 点击查看完整原图 · 游戏内收藏、强化与海战卡面共用该卡牌的专属插画。</p><section>
$($taskCards -join "`n")
</section></main></html>
"@
[IO.File]::WriteAllText((Join-Path $taskOutput '新增卡牌插画总览.html'),$taskHtml,[Text.UTF8Encoding]::new($false))
Write-Output ('Installed and hash-verified ' + $taskInstalled.Count + ' complete card paintings. No gameplay ArtId changes.')
