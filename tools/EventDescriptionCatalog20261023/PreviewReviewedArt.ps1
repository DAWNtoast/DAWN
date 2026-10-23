$ErrorActionPreference = 'Stop'
$previewRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$previewOutput = Join-Path $previewRoot 'outputs/event-consistency-audit-20261023'
$previewAssets = Join-Path $previewRoot 'UnityClient/Assets/_Project/Resources/EncounterIllustrations/Reviewed20261023'
$previewPlan = @(Get-Content -LiteralPath (Join-Path $previewOutput 'redraw-plan.json') -Raw | ConvertFrom-Json)
$previewMapping = @(Import-Csv -LiteralPath (Join-Path $previewOutput 'illustration-audit.csv'))
$previewNames = @{}
foreach ($previewItem in $previewPlan) { $previewNames[$previewItem.key] = $previewItem.name }
foreach ($previewItem in $previewMapping) {
    $previewKey = [IO.Path]::GetFileName($previewItem.resourcePath)
    if (!$previewNames.ContainsKey($previewKey)) { $previewNames[$previewKey] = $previewItem.eventName }
}
$previewFiles = @(Get-ChildItem -LiteralPath $previewAssets -Filter '*.png' | Sort-Object Name)
Add-Type -AssemblyName System.Drawing
$previewFont = [Drawing.Font]::new('Microsoft YaHei',14)
$previewSmall = [Drawing.Font]::new('Consolas',9)
$previewBrush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(235,229,211))
for ($previewStart=0; $previewStart -lt $previewFiles.Count; $previewStart+=9) {
    $previewCount = [Math]::Min(9,$previewFiles.Count-$previewStart)
    $previewRows = [int][Math]::Ceiling($previewCount/3.0)
    $previewCanvas = [Drawing.Bitmap]::new(1500,$previewRows*380)
    $previewGraphics = [Drawing.Graphics]::FromImage($previewCanvas)
    $previewGraphics.Clear([Drawing.Color]::FromArgb(15,25,32))
    $previewGraphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    for ($previewI=0; $previewI -lt $previewCount; $previewI++) {
        $previewFile = $previewFiles[$previewStart+$previewI]
        $previewX = ($previewI%3)*500
        $previewY = [int][Math]::Floor($previewI/3.0)*380
        $previewPicture = [Drawing.Image]::FromFile($previewFile.FullName)
        $previewGraphics.DrawImage($previewPicture,[Drawing.Rectangle]::new($previewX+5,$previewY+5,490,327))
        $previewPicture.Dispose()
        $previewGraphics.DrawString($previewNames[$previewFile.BaseName],$previewFont,$previewBrush,$previewX+8,$previewY+336)
        $previewGraphics.DrawString($previewFile.BaseName,$previewSmall,$previewBrush,$previewX+8,$previewY+361)
    }
    $previewCanvas.Save((Join-Path $previewOutput ('reviewed-scenes-'+([int]($previewStart/9)+1)+'.jpg')),[Drawing.Imaging.ImageFormat]::Jpeg)
    $previewGraphics.Dispose(); $previewCanvas.Dispose()
}
$previewBrush.Dispose(); $previewSmall.Dispose(); $previewFont.Dispose()
$previewOriginal = Get-Content -LiteralPath (Join-Path $previewOutput 'generation-manifest.json') -Raw | ConvertFrom-Json
$previewExtra = @(Get-Content -LiteralPath (Join-Path $previewOutput 'generated-trade-moon.json') -Raw | ConvertFrom-Json)
$previewUnified = @($previewOriginal.assets | ForEach-Object {
    [pscustomobject]@{key=$_.key;prompt=$_.prompt;source=$_.source;destination=$_.destination;sha256=$_.sha256}
}) + @($previewExtra | ForEach-Object {
    [pscustomobject]@{key=$_.key;prompt=$_.prompt;source=$_.source;destination=$_.target;sha256=$_.sha256}
})
foreach ($previewRecord in $previewUnified) {
    if ((Get-FileHash -LiteralPath $previewRecord.destination).Hash -ne $previewRecord.sha256) { throw ('Image hash mismatch: '+$previewRecord.key) }
    if (!(Test-Path -LiteralPath ($previewRecord.destination+'.meta'))) { throw ('Missing meta: '+$previewRecord.key) }
}
@{ tool='built-in image_gen';count=$previewUnified.Count;assets=$previewUnified } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $previewOutput 'all-generated-art.json') -Encoding utf8
Write-Output ('Previewed and verified '+$previewUnified.Count+' images.')
