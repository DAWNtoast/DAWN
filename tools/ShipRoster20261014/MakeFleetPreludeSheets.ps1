param([string]$Folder = 'D:/Game/Artifacts/FleetRedesign20261014/validation/preludes')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
foreach ($fleetPage in @(1,2)) {
    $fleetSheet = [Drawing.Bitmap]::new(1920,1620)
    $fleetGraphics = [Drawing.Graphics]::FromImage($fleetSheet)
    try {
        $fleetGraphics.Clear([Drawing.Color]::Black)
        $fleetGraphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        foreach ($fleetIndex in 0..5) {
            $fleetPath = Join-Path $Folder ('Legendary_{0:D2}_Prelude_{1}.png' -f $fleetIndex,$fleetPage)
            $fleetFrame = [Drawing.Image]::FromFile($fleetPath)
            try {
                $fleetBounds = [Drawing.Rectangle]::new(($fleetIndex % 2)*960,[int][Math]::Floor($fleetIndex/2)*540,960,540)
                $fleetGraphics.DrawImage($fleetFrame,$fleetBounds)
            } finally { $fleetFrame.Dispose() }
        }
        $fleetSheet.Save((Join-Path $Folder ('contact_prelude_page{0}.png' -f $fleetPage)),[Drawing.Imaging.ImageFormat]::Png)
    } finally { $fleetGraphics.Dispose(); $fleetSheet.Dispose() }
}
Write-Output 'Created two contact sheets from the twelve actual Unity prelude screenshots.'
