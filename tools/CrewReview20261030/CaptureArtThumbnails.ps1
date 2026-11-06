param([string]$Output='D:/Game/outputs/crew-review-20261030')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$artRoot=[IO.Path]::GetFullPath($Output)
$artManifest=Join-Path $artRoot 'crew-data.json'
$artData=Get-Content -LiteralPath $artManifest -Raw | ConvertFrom-Json
$artDirectory=Join-Path $artRoot 'art/thumbnails'
New-Item -ItemType Directory -Force -Path $artDirectory | Out-Null
$artCodec=[Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object MimeType -eq 'image/jpeg'
$artParams=[Drawing.Imaging.EncoderParameters]::new(1)
$artParams.Param[0]=[Drawing.Imaging.EncoderParameter]::new([Drawing.Imaging.Encoder]::Quality,[long]92)
$artCount=0;$artUnscaled=0
try {
foreach($person in $artData.crew){foreach($key in @('portrait','regionalPortrait','legacyFullBody','tavern')){
$row=$person.art.$key
if(!$row -or !$row.path){continue}
$source=Join-Path $artRoot $row.path
if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ine $row.sha256){throw ('Original hash differs: '+$row.path)}
$original=[Drawing.Image]::FromFile($source)
try{
if([Math]::Max($original.Width,$original.Height) -le 640){$thumbPath=$row.path;$artUnscaled++}
else{
$scale=640.0/[Math]::Max($original.Width,$original.Height);$width=[int][Math]::Round($original.Width*$scale);$height=[int][Math]::Round($original.Height*$scale)
$transparent=$row.transparent -eq $true;$extension=if($transparent){'.png'}else{'.jpg'}
$thumbPath='art/thumbnails/'+[IO.Path]::GetFileNameWithoutExtension($row.path)+$extension
$target=Join-Path $artRoot $thumbPath
$format=if($transparent){[Drawing.Imaging.PixelFormat]::Format32bppArgb}else{[Drawing.Imaging.PixelFormat]::Format24bppRgb}
$bitmap=[Drawing.Bitmap]::new($width,$height,$format);$graphics=[Drawing.Graphics]::FromImage($bitmap)
try{$graphics.CompositingQuality=[Drawing.Drawing2D.CompositingQuality]::HighQuality;$graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic;$graphics.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality;$graphics.DrawImage($original,[Drawing.Rectangle]::new(0,0,$width,$height));if($transparent){$bitmap.Save($target,[Drawing.Imaging.ImageFormat]::Png)}else{$bitmap.Save($target,$artCodec,$artParams)}}finally{$graphics.Dispose();$bitmap.Dispose()}
$artCount++
}
$row | Add-Member -NotePropertyName thumbPath -NotePropertyValue $thumbPath -Force
$row | Add-Member -NotePropertyName thumbSha256 -NotePropertyValue (Get-FileHash -LiteralPath (Join-Path $artRoot $thumbPath) -Algorithm SHA256).Hash.ToLowerInvariant() -Force
}finally{$original.Dispose()}
}}
}finally{$artParams.Dispose()}
$artData | ConvertTo-Json -Depth 25 | Set-Content -LiteralPath $artManifest -Encoding utf8
$capturePath=Join-Path $artRoot 'capture-manifest.json';$capture=Get-Content -LiteralPath $capturePath -Raw | ConvertFrom-Json
foreach($person in $capture.crew){$port=$capture.ports | Where-Object path -eq $person.tavernScenePath;$person | Add-Member -NotePropertyName tavernThumbPath -NotePropertyValue $port.thumbPath -Force}
$capture | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $capturePath -Encoding utf8
[pscustomobject]@{Generated=$artCount;AlreadySmall=$artUnscaled;MaximumEdge=640;OpaqueFormat='JPEG quality 92';Transparency='Preserved as PNG';OriginalsUnchanged=$true;TotalBytes=(Get-ChildItem -LiteralPath $artDirectory -File | Measure-Object Length -Sum).Sum} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $artRoot 'capture-evidence/art-thumbnail-validation.json') -Encoding utf8
Get-Content -LiteralPath (Join-Path $artRoot 'capture-evidence/art-thumbnail-validation.json')
