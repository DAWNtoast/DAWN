#Requires -Version 5.1
param(
    [Parameter(Mandatory=$true)][string]$InputPath,
    [Parameter(Mandatory=$true)][string]$OutputPath
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$taskInput = (Resolve-Path -LiteralPath $InputPath).Path
$taskOutput = [IO.Path]::GetFullPath($OutputPath)
if (-not $taskOutput.StartsWith('D:\Game\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must remain in the project workspace.' }
if (Test-Path -LiteralPath $taskOutput) { throw 'Refusing to overwrite an existing deliverable.' }
$taskImage = [Drawing.Image]::FromFile($taskInput)
$taskBitmap = $null
$taskGraphics = $null
$taskAttributes = $null
try {
    if ($taskImage.Width -lt 3840 -or $taskImage.Height -lt 2160) { throw 'Source is smaller than UHD; upscaling is not permitted by this exporter.' }
    $taskRatio = 3840.0 / 2160.0
    $taskSourceWidth = [double]$taskImage.Width
    $taskSourceHeight = [double]$taskImage.Height
    if ($taskSourceWidth / $taskSourceHeight -gt $taskRatio) { $taskSourceWidth = $taskSourceHeight * $taskRatio }
    else { $taskSourceHeight = $taskSourceWidth / $taskRatio }
    $taskSourceX = ($taskImage.Width - $taskSourceWidth) / 2.0
    $taskSourceY = ($taskImage.Height - $taskSourceHeight) / 2.0
    $taskBitmap = [Drawing.Bitmap]::new(3840, 2160, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $taskGraphics = [Drawing.Graphics]::FromImage($taskBitmap)
    $taskGraphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
    $taskGraphics.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
    $taskGraphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $taskGraphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $taskAttributes = [Drawing.Imaging.ImageAttributes]::new()
    $taskAttributes.SetWrapMode([Drawing.Drawing2D.WrapMode]::TileFlipXY)
    $taskDestinationRect = [Drawing.Rectangle]::new(0, 0, 3840, 2160)
    $taskGraphics.DrawImage($taskImage, $taskDestinationRect, [single]$taskSourceX, [single]$taskSourceY, [single]$taskSourceWidth, [single]$taskSourceHeight, [Drawing.GraphicsUnit]::Pixel, $taskAttributes)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskOutput)) | Out-Null
    $taskStream = [IO.File]::Open($taskOutput, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
    try { $taskBitmap.Save($taskStream, [Drawing.Imaging.ImageFormat]::Png) } finally { $taskStream.Dispose() }
    [ordered]@{
        source = $taskInput; output = $taskOutput
        sourceWidth = $taskImage.Width; sourceHeight = $taskImage.Height
        width = 3840; height = 2160; format = 'PNG'; upscaled = $false
        process = 'Small centered aspect-ratio crop, then high-quality bicubic downsample; no semantic image changes.'
        crop = @{ x = $taskSourceX; y = $taskSourceY; width = $taskSourceWidth; height = $taskSourceHeight }
        sha256 = (Get-FileHash -LiteralPath $taskOutput -Algorithm SHA256).Hash
    } | ConvertTo-Json -Depth 4 -Compress
}
finally {
    if ($null -ne $taskAttributes) { $taskAttributes.Dispose() }
    if ($null -ne $taskGraphics) { $taskGraphics.Dispose() }
    if ($null -ne $taskBitmap) { $taskBitmap.Dispose() }
    $taskImage.Dispose()
}
