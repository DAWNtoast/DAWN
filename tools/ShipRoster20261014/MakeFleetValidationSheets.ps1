param([string]$CapturePath = 'D:/Game/Artifacts/FleetRedesign20261014/validation/captures')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$fleetReport = Get-Content -LiteralPath (Join-Path $CapturePath 'report.json') -Raw | ConvertFrom-Json
$fleetFont = [Drawing.Font]::new('Microsoft YaHei',15,[Drawing.FontStyle]::Regular)
$fleetBrush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(240,221,188))
try {
    foreach ($group in @('Shipyard','ShipyardDetail','Battle','Introduction')) {
        $frames = @($fleetReport.frames | Where-Object { if($group -eq 'Shipyard' -or $group -eq 'ShipyardDetail'){$_.state -eq $group}else{$_.state.EndsWith($group)} })
        $sheet = [Drawing.Bitmap]::new(1440,1440)
        $graphics = [Drawing.Graphics]::FromImage($sheet)
        try {
            $graphics.Clear([Drawing.Color]::FromArgb(15,26,32))
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            for($index=0;$index -lt $frames.Count;$index++) {
                $frame = $frames[$index]
                $source = [Drawing.Image]::FromFile((Join-Path $CapturePath $frame.file))
                try {
                    $x = ($index % 3) * 480
                    $y = [Math]::Floor($index / 3) * 360
                    $crop = switch($group) {
                        'Shipyard' { [Drawing.Rectangle]::new(560,310,800,490) }
                        'ShipyardDetail' { [Drawing.Rectangle]::new(235,450,515,390) }
                        'Battle' { [Drawing.Rectangle]::new(1075,325,520,390) }
                        'Introduction' { [Drawing.Rectangle]::new(1270,110,650,500) }
                    }
                    $graphics.DrawString(($frame.index.ToString('D2') + ' ' + $frame.name),$fleetFont,$fleetBrush,$x+12,$y+7)
                    $scale = [Math]::Min(470.0 / $crop.Width,310.0 / $crop.Height)
                    $drawWidth = [int][Math]::Round($crop.Width * $scale)
                    $drawHeight = [int][Math]::Round($crop.Height * $scale)
                    $graphics.DrawImage($source,[Drawing.Rectangle]::new($x+5+([int]((470-$drawWidth)/2)),$y+40+([int]((310-$drawHeight)/2)),$drawWidth,$drawHeight),$crop,[Drawing.GraphicsUnit]::Pixel)
                } finally { $source.Dispose() }
            }
            $sheet.Save((Join-Path $CapturePath ('contact_' + $group.ToLowerInvariant() + '.png')),[Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose(); $sheet.Dispose() }
    }
} finally { $fleetFont.Dispose(); $fleetBrush.Dispose() }
Write-Output 'Four contact sheets created from the actual Unity screenshots.'
