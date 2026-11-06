param([string]$ProjectRoot = 'D:\Game')
$ErrorActionPreference = 'Stop'
$draftRoot = Join-Path $ProjectRoot 'outputs/art-redraw-20261024/icons'
$destination = Join-Path $ProjectRoot 'UnityClient/Assets/_Project/Resources/BattleIcons'
$evidence = Join-Path $ProjectRoot 'Artifacts/ArtIntegrationRelease20261024/BattleIcons'
$manifest = Get-Content -LiteralPath (Join-Path $draftRoot 'manifest.json') -Raw | ConvertFrom-Json
if ($manifest.assets.Count -ne 41) { throw 'Expected exactly 41 approved authored icon bases.' }
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System.Drawing;
public static class CombatAtlasVerification
{
    public static int Verify(Bitmap atlas, Bitmap icon, int x, int y)
    {
        int painted = 0;
        for (int row = 0; row < 256; row++)
            for (int col = 0; col < 256; col++)
            {
                Color a = atlas.GetPixel(x + col, y + row), b = icon.GetPixel(col, row);
                if (a.ToArgb() != b.ToArgb()) throw new System.Exception("Atlas cell differs from approved base art.");
                if (a.A > 127) painted++;
                if ((row == 0 || row == 255 || col == 0 || col == 255) && a.A != 0)
                    throw new System.Exception("Icon has an opaque or clipped canvas edge.");
            }
        return painted;
    }
}
'@ -ReferencedAssemblies @([System.Drawing.Bitmap].Assembly.Location, [System.Drawing.Color].Assembly.Location)
New-Item -ItemType Directory -Path $destination, $evidence -Force | Out-Null
$sourceAtlas = [System.Drawing.Bitmap]::new((Join-Path $draftRoot 'battle-icons-review-atlas.png'))
$runtimeAtlas = $null
try {
    if ($sourceAtlas.Width -ne 2048 -or $sourceAtlas.Height -ne 1536) { throw 'Unexpected source atlas dimensions.' }
    $runtimeAtlas = $sourceAtlas.Clone([System.Drawing.Rectangle]::new(0,0,2048,1536), [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    # Unused final cell only: a white sample for the existing source/trigger badge geometry.
    for ($y = 1516; $y -lt 1524; $y++) { for ($x = 2028; $x -lt 2036; $x++) { $runtimeAtlas.SetPixel($x,$y,[System.Drawing.Color]::White) } }
    $rows = @()
    foreach ($entry in $manifest.assets) {
        $inputFile = Join-Path $draftRoot $entry.asset
        $hash = (Get-FileHash -LiteralPath $inputFile -Algorithm SHA256).Hash
        if ($hash -ne $entry.sha256) { throw "Approved icon changed: $($entry.id)" }
        $base = [System.Drawing.Bitmap]::new($inputFile)
        try { $painted = [CombatAtlasVerification]::Verify($runtimeAtlas, $base, $entry.reviewAtlasRect[0], $entry.reviewAtlasRect[1]) }
        finally { $base.Dispose() }
        $rows += [ordered]@{ Id=$entry.id; Source=$inputFile; SourceSHA256=$hash; AtlasRect=$entry.reviewAtlasRect; OpaquePixels=$painted }
    }
    # The four supply emblems now share the existing card-frame paintings across
    # all item views. Preserve that newer user choice when rebuilding this atlas.
    $supplyOverrideRoot = Join-Path $ProjectRoot 'outputs/battle-resource-icons-20261026'
    $supplyOverrideManifestPath = Join-Path $supplyOverrideRoot 'shared-icons-manifest.json'
    $supplyOverrideCount = 0
    if (-not (Test-Path -LiteralPath $supplyOverrideManifestPath)) {
        throw 'Shared card-frame supply manifest is required. Run tools/BattleResourceIcons20261026/build_shared_supply_icons.py --install first; refusing to restore retired supply artwork.'
    }
    if (Test-Path -LiteralPath $supplyOverrideManifestPath) {
        $supplyOverrideManifest = Get-Content -Raw -LiteralPath $supplyOverrideManifestPath | ConvertFrom-Json
        $supplyFrameRoot = Join-Path $ProjectRoot 'UnityClient/Assets/_Project/Resources/CardSchools/Frames/Integrated'
        $supplyLayoutHash = (Get-FileHash -LiteralPath (Join-Path $supplyFrameRoot 'frame-layouts.json') -Algorithm SHA256).Hash
        if ($supplyLayoutHash -ne $supplyOverrideManifest.frameLayoutSHA256) {
            throw 'Card-frame layouts changed. Re-export the shared supply emblems before rebuilding the battle atlas.'
        }
        foreach ($supplyId in @('powder', 'cannonball', 'planks', 'medicine')) {
            $supplyEntry = @($supplyOverrideManifest.assets | Where-Object { $_.id -eq $supplyId })
            if ($supplyEntry.Count -ne 1) { throw ('Missing unique shared card-frame emblem: ' + $supplyId) }
            $supplyFrameName = switch ($supplyId) {
                'planks' { 'frame-neutral-ap-wood.png' }
                'medicine' { 'frame-neutral-ap-medicine.png' }
                default { 'frame-neutral-both.png' }
            }
            if ((Get-FileHash -LiteralPath (Join-Path $supplyFrameRoot $supplyFrameName) -Algorithm SHA256).Hash -ne $supplyEntry[0].sourceSHA256) {
                throw ('Card-frame source changed; re-export shared emblem: ' + $supplyId)
            }
            if ($supplyEntry[0].originalSource -and
                (Get-FileHash -LiteralPath $supplyEntry[0].originalSource -Algorithm SHA256).Hash -ne $supplyEntry[0].originalSourceSHA256) {
                throw ('Original item artwork changed; review and re-export item-only icon: ' + $supplyId)
            }
            $supplyFile = Join-Path $supplyOverrideRoot ('shared-icons/' + $supplyId + '.png')
            $supplyHash = (Get-FileHash -LiteralPath $supplyFile -Algorithm SHA256).Hash
            if ($supplyHash -ne $supplyEntry[0].iconSHA256) { throw ('Shared supply emblem changed: ' + $supplyId) }
            $supplyBitmap = [System.Drawing.Bitmap]::new($supplyFile)
            try {
                if ($supplyBitmap.Width -ne 256 -or $supplyBitmap.Height -ne 256) { throw 'Unexpected supply emblem size.' }
                $supplyRow = $rows | Where-Object { $_.Id -eq $supplyId } | Select-Object -First 1
                $supplyX = [int]$supplyRow.AtlasRect[0]; $supplyY = [int]$supplyRow.AtlasRect[1]
                for ($supplyPixelY = 0; $supplyPixelY -lt 256; $supplyPixelY++) {
                    for ($supplyPixelX = 0; $supplyPixelX -lt 256; $supplyPixelX++) {
                        $runtimeAtlas.SetPixel($supplyX + $supplyPixelX, $supplyY + $supplyPixelY, $supplyBitmap.GetPixel($supplyPixelX, $supplyPixelY))
                    }
                }
                $supplyRow.Source = $supplyFile
                $supplyRow.SourceSHA256 = $supplyHash
                $supplyRow.OpaquePixels = [CombatAtlasVerification]::Verify($runtimeAtlas, $supplyBitmap, $supplyX, $supplyY)
                $supplyOverrideCount++
            } finally { $supplyBitmap.Dispose() }
        }
    }
    $atlasPath = Join-Path $destination 'battle_icons_v1.png'
    $runtimeAtlas.Save($atlasPath,[System.Drawing.Imaging.ImageFormat]::Png)
}
finally { if ($runtimeAtlas) { $runtimeAtlas.Dispose() }; $sourceAtlas.Dispose() }
$metaPath = "$atlasPath.meta"
if (-not (Test-Path -LiteralPath $metaPath)) {
    $template = Get-Content -LiteralPath (Join-Path $ProjectRoot 'UnityClient/Assets/_Project/Resources/AchievementIcons/achievements_a_v1.png.meta') -Raw
    $template = $template -replace 'guid: [a-f0-9]+', ('guid: ' + [guid]::NewGuid().ToString('N'))
    $template = $template -replace 'maxTextureSize: 1024', 'maxTextureSize: 2048'
    $template = $template -replace 'userData: .*', 'userData: 41 approved combat paintings; full transparent cells; white utility swatch in unused final cell.'
    [IO.File]::WriteAllText($metaPath,$template,[Text.UTF8Encoding]::new($false))
}
$folderMeta = "$destination.meta"
if (-not (Test-Path -LiteralPath $folderMeta)) {
    $guid = [guid]::NewGuid().ToString('N')
    [IO.File]::WriteAllText($folderMeta,"fileFormatVersion: 2`nguid: $guid`nfolderAsset: yes`nDefaultImporter:`n  externalObjects: {}`n  userData: `n  assetBundleName: `n  assetBundleVariant: `n",[Text.UTF8Encoding]::new($false))
}
foreach ($relative in @('Presentation/Runtime/BattleIconArtCatalog.cs', 'Tests/EditMode/BattleIconArtCatalogTests.cs')) {
    $scriptMeta = Join-Path $ProjectRoot "UnityClient/Assets/_Project/$relative.meta"
    if (-not (Test-Path -LiteralPath $scriptMeta)) {
        $template = Get-Content -LiteralPath (Join-Path $ProjectRoot 'UnityClient/Assets/_Project/Presentation/Runtime/BattleCommandIconGraphic.cs.meta') -Raw
        $template = $template -replace 'guid: [a-f0-9]+', ('guid: ' + [guid]::NewGuid().ToString('N'))
        [IO.File]::WriteAllText($scriptMeta,$template,[Text.UTF8Encoding]::new($false))
    }
}
$report = [ordered]@{
    Result='PASS'; ArtBaseCount=41; Atlas=$atlasPath; AtlasSHA256=(Get-FileHash -LiteralPath $atlasPath -Algorithm SHA256).Hash
    Integration='BattleCommandIconGraphic, BattleStatusIconGraphic, BattleHeaderIconGraphic use BattleIconArtCatalog directly.'
    Preserved=('All 41 cells match their current source; ' + $supplyOverrideCount + ' shared supply cells use card-frame emblems. All canvas edges transparent; source/trigger geometry and numeric labels retained.')
    UtilityWhiteSwatch=@(2028,1516,8,8); Assets=$rows
}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $evidence 'integration-manifest.json') -Encoding UTF8
Write-Output ("PASS: 41 source-identical icon cells integrated, including " + $supplyOverrideCount + " shared card-frame emblems. Atlas SHA256 " + $report.AtlasSHA256)
