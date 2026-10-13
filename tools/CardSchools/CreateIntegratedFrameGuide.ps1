$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$guideOutput = 'D:\Game\Artifacts\CardFramesIntegrated20261012'
[IO.Directory]::CreateDirectory($guideOutput) | Out-Null
# Technical layout reference only. Final game artwork is generated separately.
$guideBitmap = [Drawing.Bitmap]::new(780,1056,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
$guideGraphics = [Drawing.Graphics]::FromImage($guideBitmap)
$guideGraphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$guideGraphics.Clear([Drawing.Color]::Transparent)
$guideFramePen = [Drawing.Pen]::new([Drawing.Color]::FromArgb(255,110,100,85),24)
$guideGraphics.DrawRectangle($guideFramePen,81,78,618,900)
$guideBlue = [Drawing.SolidBrush]::new([Drawing.Color]::RoyalBlue)
$guideIron = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,45,55,65))
$guideWood = [Drawing.SolidBrush]::new([Drawing.Color]::SaddleBrown)
$guideRim = [Drawing.Pen]::new([Drawing.Color]::Silver,6)
$guideGraphics.FillEllipse($guideBlue,5,2,152,152)
$guideGraphics.DrawEllipse($guideRim,5,2,152,152)
$guideGraphics.FillEllipse($guideIron,23,920,116,116)
$guideGraphics.DrawEllipse($guideRim,23,920,116,116)
$guideGraphics.FillEllipse($guideWood,641,920,116,116)
$guideGraphics.DrawEllipse($guideRim,641,920,116,116)
$guideBitmap.Save((Join-Path $guideOutput 'layout-both.png'),[Drawing.Imaging.ImageFormat]::Png)
$guideRim.Dispose(); $guideWood.Dispose(); $guideIron.Dispose(); $guideBlue.Dispose()
$guideFramePen.Dispose(); $guideGraphics.Dispose(); $guideBitmap.Dispose()
Write-Output (Join-Path $guideOutput 'layout-both.png')
