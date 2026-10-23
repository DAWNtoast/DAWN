param(
    [ValidateSet('Install','Inspect','Preview')][string]$Stage = 'Install'
)
$ErrorActionPreference = 'Stop'
$woodRoot = 'D:\Game'
$woodTarget = Join-Path $woodRoot 'UnityClient/Assets/_Project/Resources/CardSchools/Frames/Integrated'
$woodOutput = Join-Path $woodRoot 'outputs/imagegen/card-wood-integrated-20261022'
$woodSource = 'C:\Users\Admin\.codex\generated_images\01a16360-4051-77a2-b59f-3897d35570b3\exec-d3534e9b-54fd-4c95-a1e0-2970d0a82def.png'
$woodAsset = Join-Path $woodTarget 'frame-neutral-ap-wood.png'
Add-Type -AssemblyName System.Drawing
if ($Stage -eq 'Install') {
    [IO.Directory]::CreateDirectory($woodOutput) | Out-Null
    Copy-Item -LiteralPath $woodSource -Destination $woodAsset -Force
    Copy-Item -LiteralPath $woodSource -Destination (Join-Path $woodOutput 'frame-neutral-ap-wood.png') -Force
    $woodMetaPath = $woodAsset + '.meta'
    if (-not (Test-Path -LiteralPath $woodMetaPath)) {
        $woodMeta = [IO.File]::ReadAllText((Join-Path $woodTarget 'frame-neutral-ap.png.meta'))
        $woodMeta = $woodMeta -replace 'guid: [a-f0-9]{32}', ('guid: ' + [Guid]::NewGuid().ToString('N'))
        [IO.File]::WriteAllText($woodMetaPath, $woodMeta)
    }
    $woodLayoutPath = Join-Path $woodTarget 'frame-layouts.json'
    $woodLayoutText = [IO.File]::ReadAllText($woodLayoutPath)
    $woodLayout = $woodLayoutText | ConvertFrom-Json
    if (-not ($woodLayout.entries | Where-Object { $_.id -eq 'frame-neutral-ap-wood' })) {
        $woodEntry = ($woodLayout.entries | Where-Object { $_.id -eq 'frame-neutral-ap' } | Select-Object -First 1) | ConvertTo-Json -Depth 12 | ConvertFrom-Json
        $woodEntry.id = 'frame-neutral-ap-wood'
        $woodEntry | Add-Member -NotePropertyName supplyPosition -NotePropertyValue ([ordered]@{
            x = [Math]::Round(($woodEntry.rect.x + 849.0 / 965.0 * $woodEntry.rect.width), 3)
            y = [Math]::Round(($woodEntry.rect.y + $woodEntry.rect.height - 462.0 / 1630.0 * $woodEntry.rect.height), 3)
        })
        $woodClosing = $woodLayoutText.LastIndexOf(']')
        $woodPrefix = $woodLayoutText.Substring(0, $woodClosing).TrimEnd()
        $woodNewJson = $woodEntry | ConvertTo-Json -Depth 12
        [IO.File]::WriteAllText($woodLayoutPath, $woodPrefix + ",`r`n" + $woodNewJson + "`r`n  " + $woodLayoutText.Substring($woodClosing))
    }
    Write-Output 'Installed user-selected baked wood frame without altering its pixels.'
}
if ($Stage -in @('Install','Inspect')) {
    $woodImage = [Drawing.Bitmap]::FromFile($woodAsset)
    try {
        $woodEvidence = [ordered]@{
            width = $woodImage.Width
            height = $woodImage.Height
            format = $woodImage.PixelFormat.ToString()
            exteriorAlpha = $woodImage.GetPixel(0,0).A
            illustrationAlpha = $woodImage.GetPixel(480,460).A
            parchmentAlpha = $woodImage.GetPixel(480,1200).A
            woodIconAlpha = $woodImage.GetPixel(850,450).A
            sourceHash = (Get-FileHash -LiteralPath $woodSource).Hash
            installedHash = (Get-FileHash -LiteralPath $woodAsset).Hash
            selectedVersion = 'Large baked wood icon, explicitly selected by user'
        }
        if ($woodEvidence.width -ne 965 -or $woodEvidence.height -ne 1630) { throw 'Unexpected generated dimensions.' }
        if ($woodEvidence.exteriorAlpha -ne 0 -or $woodEvidence.illustrationAlpha -ne 0) { throw 'Transparent areas were lost.' }
        if ($woodEvidence.sourceHash -ne $woodEvidence.installedHash) { throw 'Asset differs from selected image.' }
        $woodEvidence | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $woodOutput 'asset-verification.json') -Encoding utf8
        $woodEvidence | ConvertTo-Json
    } finally { $woodImage.Dispose() }
}
if ($Stage -eq 'Preview') {
    # Preserve the production Unity screenshot, no source asset editing.
    $woodCapture = Join-Path $woodRoot 'Artifacts/CardSupplyFrames20261021/wood-integrated-20261022-final/captures/emergency-repair.png'
    if (-not (Test-Path -LiteralPath $woodCapture)) { throw 'Production capture not ready.' }
    Copy-Item -LiteralPath $woodCapture -Destination (Join-Path $woodOutput 'emergency-repair-preview.png') -Force
}

