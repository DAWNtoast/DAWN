# Native API output is archived separately. This export downsamples the complete
# canvas to the production UHD size; it never crops away approved artwork.
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$InputPath,[Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$fullOutput=[IO.Path]::GetFullPath($OutputPath)
if(-not $fullOutput.StartsWith('D:\Game\Artifacts\BackgroundClarity4K20261013\final-4k\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected output directory'}
if(Test-Path -LiteralPath $fullOutput){throw 'Output exists'}
$source=[Drawing.Image]::FromFile((Resolve-Path -LiteralPath $InputPath).Path)
$bitmap=$null;$graphics=$null;$attributes=$null
try {
 if($source.Width -lt 3840 -or $source.Height -lt 2160){throw 'Returned source is below UHD; no interpolation upscale permitted'}
 if([Math]::Abs(($source.Width/[double]$source.Height)/(16.0/9.0)-1.0) -gt .015){throw 'Unexpected aspect ratio; inspect before exporting'}
 $bitmap=[Drawing.Bitmap]::new(3840,2160,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
 $graphics=[Drawing.Graphics]::FromImage($bitmap)
 $graphics.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceCopy
 $graphics.CompositingQuality=[Drawing.Drawing2D.CompositingQuality]::HighQuality
 $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
 $graphics.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
 $attributes=[Drawing.Imaging.ImageAttributes]::new();$attributes.SetWrapMode([Drawing.Drawing2D.WrapMode]::TileFlipXY)
 $graphics.DrawImage($source,[Drawing.Rectangle]::new(0,0,3840,2160),0,0,$source.Width,$source.Height,[Drawing.GraphicsUnit]::Pixel,$attributes)
 $stream=[IO.File]::Open($fullOutput,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write)
 try{$bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)}finally{$stream.Dispose()}
 [ordered]@{source=$InputPath;sourceWidth=$source.Width;sourceHeight=$source.Height;output=$fullOutput;width=3840;height=2160;upscaled=$false;cropped=$false;method='Full-frame high-quality bicubic downsample; independent axis normalization of API output; no new detail synthesized locally';sha256=(Get-FileHash -LiteralPath $fullOutput -Algorithm SHA256).Hash} | ConvertTo-Json -Compress | Set-Content -LiteralPath ($fullOutput+'.export.json') -Encoding UTF8
 Write-Output ('Exported '+$fullOutput)
} finally {
 if($null -ne $attributes){$attributes.Dispose()};if($null -ne $graphics){$graphics.Dispose()};if($null -ne $bitmap){$bitmap.Dispose()};$source.Dispose()
}
