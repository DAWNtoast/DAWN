$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$previewSamples = @(
    @{ File='01_common_bow_ironwood_beak.png'; Label='普通 · 银灰'; Name='铁木穿潮喙'; Color='#B8C0C8' },
    @{ File='02_uncommon_sail_storm_canvas.png'; Label='精良 · 绿色'; Name='风暴帆布'; Color='#48D66B' },
    @{ File='03_rare_cannon_chain_repeater.png'; Label='稀有 · 蓝色'; Name='链弹连发炮'; Color='#3B8BFF' },
    @{ File='04_epic_plating_moonsilver_skin.png'; Label='史诗 · 紫色'; Name='月银覆板'; Color='#B05CFF' },
    @{ File='05_legendary_instrument_allseeing_sextant.png'; Label='传说 · 金色'; Name='全视六分仪'; Color='#FFB52E' }
) | Where-Object { Test-Path -LiteralPath (Join-Path $PSScriptRoot $_.File) }
$previewWidth = 400 * $previewSamples.Count
$previewBitmap = [Drawing.Bitmap]::new($previewWidth, 600, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
$previewGraphics = [Drawing.Graphics]::FromImage($previewBitmap)
$previewTitleFont = [Drawing.Font]::new('Microsoft YaHei', 24, [Drawing.FontStyle]::Bold, [Drawing.GraphicsUnit]::Pixel)
$previewLabelFont = [Drawing.Font]::new('Microsoft YaHei', 26, [Drawing.FontStyle]::Bold, [Drawing.GraphicsUnit]::Pixel)
$previewNameFont = [Drawing.Font]::new('Microsoft YaHei', 21, [Drawing.FontStyle]::Regular, [Drawing.GraphicsUnit]::Pixel)
$previewFormat = [Drawing.StringFormat]::new()
$previewFormat.Alignment = [Drawing.StringAlignment]::Center
$previewFormat.LineAlignment = [Drawing.StringAlignment]::Center
try {
    $previewGraphics.Clear([Drawing.ColorTranslator]::FromHtml('#101B22'))
    $previewGraphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $previewGraphics.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
    $previewGraphics.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $previewGraphics.DrawString('部件轮廓光晕 · 稀有度试作', $previewTitleFont, [Drawing.Brushes]::WhiteSmoke, [Drawing.RectangleF]::new(0, 10, $previewWidth, 60), $previewFormat)
    for ($previewIndex=0; $previewIndex -lt $previewSamples.Count; $previewIndex++) {
        $previewSample = $previewSamples[$previewIndex]
        $previewTint = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($previewSample.Color))
        $previewImage = [Drawing.Image]::FromFile((Join-Path $PSScriptRoot $previewSample.File))
        try {
            $previewLeft = $previewIndex * 400
            $previewGraphics.DrawString($previewSample.Label, $previewLabelFont, $previewTint, [Drawing.RectangleF]::new($previewLeft, 80, 400, 42), $previewFormat)
            $previewGraphics.DrawImage($previewImage, [Drawing.Rectangle]::new($previewLeft+14, 126, 372, 372))
            $previewGraphics.DrawString($previewSample.Name, $previewNameFont, [Drawing.Brushes]::Gainsboro, [Drawing.RectangleF]::new($previewLeft, 505, 400, 40), $previewFormat)
        } finally { $previewImage.Dispose(); $previewTint.Dispose() }
    }
    $previewGraphics.DrawString('沿部件形状贴边发光 · 透明 PNG 样例', $previewNameFont, [Drawing.Brushes]::Gray, [Drawing.RectangleF]::new(0, 555, $previewWidth, 35), $previewFormat)
    $previewBitmap.Save((Join-Path $PSScriptRoot 'rarity-contour-preview.png'), [Drawing.Imaging.ImageFormat]::Png)
} finally {
    $previewFormat.Dispose(); $previewTitleFont.Dispose(); $previewLabelFont.Dispose(); $previewNameFont.Dispose()
    $previewGraphics.Dispose(); $previewBitmap.Dispose()
}
