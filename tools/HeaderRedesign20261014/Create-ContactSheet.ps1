param([Parameter(Mandatory=$true)][string]$CaptureDirectory)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$directory = [IO.Path]::GetFullPath($CaptureDirectory)
$allowed = [IO.Path]::GetFullPath('D:/Game/Artifacts/HeaderRedesign20261014') + [IO.Path]::DirectorySeparatorChar
if (-not $directory.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Use a verified capture directory below HeaderRedesign20261014 artifacts.' }
$cases = @(
    @('断锚港 / 港口总览', 'BrokenAnchor_PortHub_1920x1080.png'),
    @('白贝港 / 港口总览', 'WhiteShell_PortHub_1920x1080.png'),
    @('铜钟港 / 港口总览', 'BronzeBell_PortHub_1920x1080.png'),
    @('盐冠港 / 港口总览', 'SaltCrown_PortHub_1920x1080.png'),
    @('暮潮港 / 港口总览', 'TwilightTide_PortHub_1920x1080.png'),
    @('灰鸥港 / 港口总览', 'GrayGull_PortHub_1920x1080.png'),
    @('断锚港 / 出航码头', 'BrokenAnchor_VoyageDeparture_1920x1080.png'),
    @('断锚港 / 部件商店', 'BrokenAnchor_ComponentShop_1920x1080.png'),
    @('航行探索', 'GrayGull_Navigation_1920x1080.png'),
    @('海战', 'GrayGull_Battle_1920x1080.png'),
    @('接舷战斗', 'GrayGull_Boarding_1920x1080.png')
)
$output = Join-Path $directory 'header-contact-sheet.png'
if (Test-Path -LiteralPath $output) { throw 'Preserve existing contact sheet; use a fresh capture directory.' }
$sheet = New-Object Drawing.Bitmap(1920, ($cases.Count * 144))
$graphics = [Drawing.Graphics]::FromImage($sheet)
$font = New-Object Drawing.Font('Microsoft YaHei', 16, [Drawing.FontStyle]::Regular)
$brush = New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(235,222,195))
try {
    $graphics.Clear([Drawing.Color]::FromArgb(17,24,29))
    for ($index = 0; $index -lt $cases.Count; $index++) {
        $graphics.DrawString($cases[$index][0], $font, $brush, [single]12, [single]($index * 144 + 2))
        $image = [Drawing.Image]::FromFile((Join-Path $directory $cases[$index][1]))
        try {
            $destination = New-Object Drawing.Rectangle(0, ($index * 144 + 32), 1920, 108)
            $source = New-Object Drawing.Rectangle(0, 0, 1920, 108)
            $graphics.DrawImage($image, $destination, $source, [Drawing.GraphicsUnit]::Pixel)
        } finally { $image.Dispose() }
    }
    $sheet.Save($output, [Drawing.Imaging.ImageFormat]::Png)
} finally { $graphics.Dispose(); $sheet.Dispose(); $font.Dispose(); $brush.Dispose() }
Write-Output $output