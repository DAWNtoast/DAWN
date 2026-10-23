param([switch]$AllowPartial)
$ErrorActionPreference = 'Stop'
$reviewRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$reviewOutput = Join-Path $reviewRoot 'outputs/event-consistency-audit-20261023'
$reviewAssets = Join-Path $reviewRoot 'UnityClient/Assets/_Project/Resources/EncounterIllustrations/Reviewed20261023'
$reviewPlan = @(Get-Content -LiteralPath (Join-Path $reviewOutput 'redraw-plan.json') -Raw | ConvertFrom-Json)
$reviewRecords = @()
foreach ($reviewFile in Get-ChildItem -LiteralPath $reviewOutput -Filter 'generated-batch-*.json') {
    $reviewRecords += @(Get-Content -LiteralPath $reviewFile.FullName -Raw | ConvertFrom-Json)
}
if (@($reviewRecords.key | Select-Object -Unique).Count -ne $reviewRecords.Count) { throw 'Duplicate generated key.' }
if (!$AllowPartial -and $reviewRecords.Count -ne $reviewPlan.Count) { throw 'Some planned images are missing.' }
New-Item -ItemType Directory -Force -Path $reviewAssets | Out-Null
if (!(Test-Path -LiteralPath ($reviewAssets + '.meta'))) {
    $reviewFolderMeta = "fileFormatVersion: 2`nguid: " + [guid]::NewGuid().ToString('N') + "`nfolderAsset: yes`nDefaultImporter:`n  externalObjects: {}`n  userData: ''`n  assetBundleName: ''`n  assetBundleVariant: ''`n"
    [IO.File]::WriteAllText($reviewAssets + '.meta', $reviewFolderMeta, [Text.UTF8Encoding]::new($false))
}
$reviewTemplate = [IO.File]::ReadAllText((Join-Path $reviewRoot 'UnityClient/Assets/_Project/Resources/EncounterIllustrations/moonlit_shadow_dial_v1.png.meta'))
Add-Type -AssemblyName System.Drawing
$reviewInstalled = foreach ($reviewRecord in $reviewRecords) {
    if ($reviewRecord.key -notmatch '^[a-z0-9_]+$' -or $reviewRecord.key -notin $reviewPlan.key) { throw 'Invalid generated key.' }
    $reviewMatch = [regex]::Match($reviewRecord.hint, ' as (.+?\.png) by default')
    if (!$reviewMatch.Success) { throw 'Generated file path missing.' }
    $reviewSource = $reviewMatch.Groups[1].Value
    $reviewTarget = Join-Path $reviewAssets ($reviewRecord.key + '.png')
    $reviewImage = [Drawing.Bitmap]::new($reviewSource)
    try {
        if ($reviewImage.Width -ne 1536 -or $reviewImage.Height -ne 1024) { throw 'Unexpected image dimensions.' }
        for ($reviewY=0; $reviewY -lt $reviewImage.Height; $reviewY+=16) {
            for ($reviewX=0; $reviewX -lt $reviewImage.Width; $reviewX+=16) {
                if ($reviewImage.GetPixel($reviewX,$reviewY).A -ne 255) { throw 'Transparent scene detected.' }
            }
        }
    } finally { $reviewImage.Dispose() }
    Copy-Item -LiteralPath $reviewSource -Destination $reviewTarget -Force
    if (!(Test-Path -LiteralPath ($reviewTarget + '.meta'))) {
        $reviewMeta = [regex]::Replace($reviewTemplate, '(?m)^guid: [a-f0-9]+', ('guid: ' + [guid]::NewGuid().ToString('N')))
        $reviewMeta = $reviewMeta.Replace('Built-in image_gen event illustration 2026-10-22', 'Built-in image_gen reviewed event scene 2026-10-23')
        [IO.File]::WriteAllText($reviewTarget + '.meta', $reviewMeta.TrimEnd() + "`n", [Text.UTF8Encoding]::new($false))
    }
    [pscustomobject]@{ key=$reviewRecord.key; source=$reviewSource; destination=$reviewTarget; prompt=$reviewRecord.prompt; width=1536; height=1024; opacity='sampled alpha 255 on 16px grid'; sha256=(Get-FileHash -LiteralPath $reviewTarget).Hash }
}
@{ tool='built-in image_gen'; count=$reviewInstalled.Count; assets=$reviewInstalled } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $reviewOutput 'generation-manifest.json') -Encoding utf8
Write-Output ('Installed ' + $reviewInstalled.Count + ' reviewed scenes.')
