param([string]$Output='D:/Game/outputs/crew-review-20261030')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$previewRoot=[IO.Path]::GetFullPath($Output)
$previewManifest=Join-Path $previewRoot 'capture-manifest.json'
$previewData=Get-Content -LiteralPath $previewManifest -Raw | ConvertFrom-Json
$previewDirectory=Join-Path $previewRoot 'game-view/thumbnails'
New-Item -ItemType Directory -Force -Path $previewDirectory | Out-Null
$previewCodec=[Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object MimeType -eq 'image/jpeg'
$previewParameters=[Drawing.Imaging.EncoderParameters]::new(1)
$previewParameters.Param[0]=[Drawing.Imaging.EncoderParameter]::new([Drawing.Imaging.Encoder]::Quality,[long]92)
$previewRows=@($previewData.ports)+@($previewData.crew | ForEach-Object {$_.pages})
try {
foreach($previewRow in $previewRows){
$previewSource=Join-Path $previewRoot $previewRow.path
if((Get-FileHash -LiteralPath $previewSource -Algorithm SHA256).Hash -ine $previewRow.sha256){throw 'Source capture hash differs'}
$previewPath='game-view/thumbnails/'+[IO.Path]::GetFileNameWithoutExtension($previewRow.path)+'.jpg'
$previewTarget=Join-Path $previewRoot $previewPath
$previewOriginal=[Drawing.Image]::FromFile($previewSource)
$previewBitmap=[Drawing.Bitmap]::new(960,540,[Drawing.Imaging.PixelFormat]::Format24bppRgb)
$previewGraphics=[Drawing.Graphics]::FromImage($previewBitmap)
try {
$previewGraphics.CompositingQuality=[Drawing.Drawing2D.CompositingQuality]::HighQuality
$previewGraphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$previewGraphics.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$previewGraphics.DrawImage($previewOriginal,[Drawing.Rectangle]::new(0,0,960,540))
$previewBitmap.Save($previewTarget,$previewCodec,$previewParameters)
} finally {$previewGraphics.Dispose();$previewBitmap.Dispose();$previewOriginal.Dispose()}
$previewRow | Add-Member -NotePropertyName thumbPath -NotePropertyValue $previewPath -Force
$previewRow | Add-Member -NotePropertyName thumbSha256 -NotePropertyValue (Get-FileHash -LiteralPath $previewTarget -Algorithm SHA256).Hash.ToLowerInvariant() -Force
}
}finally{$previewParameters.Dispose()}
$previewData | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $previewManifest -Encoding utf8
[pscustomobject]@{Count=$previewRows.Count;Width=960;Height=540;Format='JPEG';Quality=92;SourceCapturesUnchanged=$true;TotalBytes=(Get-ChildItem -LiteralPath $previewDirectory -File | Measure-Object Length -Sum).Sum} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $previewRoot 'capture-evidence/thumbnail-validation.json') -Encoding utf8
Get-Content -LiteralPath (Join-Path $previewRoot 'capture-evidence/thumbnail-validation.json')
