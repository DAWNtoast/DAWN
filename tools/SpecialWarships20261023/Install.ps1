param([switch]$BaselineOnly)
$ErrorActionPreference='Stop'
$shipRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$shipResources=Join-Path $shipRoot 'UnityClient/Assets/_Project/Resources/ShipArtV2/Special'
$shipOutput=Join-Path $shipRoot 'outputs/imagegen/special-warships-20261023'
$shipBackup=Join-Path $shipOutput 'originals'
$shipCode=Join-Path $shipRoot 'UnityClient/Assets/_Project/Presentation/Runtime/SpecialWarshipArtCatalog.cs'
$shipKeys=@('boss_fire','boss_tide','boss_blood','boss_thunder','boss_curse','boss_arcane','legendary_fire','legendary_tide','legendary_blood','legendary_thunder','legendary_curse','legendary_arcane')
$shipNames=@('裂桅号','幽礁潜猎号','税金女王号','极光敕令号','月蚀圣骸','月织星槎号','余烬方舟号','潮汐镇魂号','绯刃寡妇号','雷冠审判号','铁誓不沉号','终界观星号')
New-Item -ItemType Directory -Path $shipOutput,$shipBackup -Force | Out-Null
Add-Type -AssemblyName System.Drawing
Add-Type -Path (Join-Path $PSScriptRoot 'Inspect.cs') -ReferencedAssemblies @('System.Drawing.Common','System.Drawing.Primitives','System.Private.Windows.GdiPlus','System.Private.Windows.Core','System.Runtime','System.Collections','System.Runtime.InteropServices')
foreach($shipKey in $shipKeys) {
    foreach($shipSuffix in @('.png','.png.meta')) {
        $shipTarget=Join-Path $shipBackup ($shipKey+$shipSuffix)
        if(!(Test-Path -LiteralPath $shipTarget)) { Copy-Item -LiteralPath (Join-Path $shipResources ($shipKey+$shipSuffix)) -Destination $shipTarget }
    }
}
$shipCodeBackup=Join-Path $shipBackup 'SpecialWarshipArtCatalog.cs'
if(!(Test-Path -LiteralPath $shipCodeBackup)) { Copy-Item -LiteralPath $shipCode -Destination $shipCodeBackup }
if($BaselineOnly) {
    $baseline=foreach($shipKey in $shipKeys) {
        $path=Join-Path $shipResources ($shipKey+'.png')
        [pscustomobject]@{key=$shipKey;sha256=(Get-FileHash -LiteralPath $path).Hash;profile=[WarshipImageInspection]::Read($path)}
    }
    $baseline | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $shipOutput 'baseline.json') -Encoding utf8
    Write-Output 'Backed up twelve original paintings, metadata and contact coordinates.'
    return
}
$shipRecords=@()
foreach($file in Get-ChildItem -LiteralPath $shipOutput -Filter '*-prompts.json' -File) { $shipRecords+=@(Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json) }
if($shipRecords.Count -ne 12 -or @($shipRecords.key | Select-Object -Unique).Count -ne 12) { throw 'Expected exactly twelve unique generated warships.' }
$shipReview=@()
foreach($shipRecord in $shipRecords) {
    if($shipRecord.key -notin $shipKeys -or !$shipRecord.source -or !$shipRecord.prompt) { throw 'Invalid warship generation record.' }
    $profile=[WarshipImageInspection]::Read($shipRecord.source)
    if($profile.width -lt 1024 -or $profile.height -lt 768 -or $profile.width -le $profile.height) { throw ('Unexpected image dimensions: '+$shipRecord.key) }
    if($profile.transparentFraction -lt .15 -or $profile.transparentFraction -gt .9 -or $profile.edgePixels -ne 0) { throw ('Missing transparent background or clipped silhouette: '+$shipRecord.key) }
    $margin=[Math]::Min([Math]::Min($profile.left,$profile.top),[Math]::Min($profile.width-1-$profile.right,$profile.height-1-$profile.bottom))
    if($margin -lt 8) { throw ('Insufficient clear padding: '+$shipRecord.key) }
    $sha=(Get-FileHash -LiteralPath $shipRecord.source).Hash
    if($sha -eq (Get-FileHash -LiteralPath (Join-Path $shipBackup ($shipRecord.key+'.png'))).Hash) { throw ('Painting not redrawn: '+$shipRecord.key) }
    $destination=Join-Path $shipResources ($shipRecord.key+'.png')
    $guid=[regex]::Match([IO.File]::ReadAllText($destination+'.meta'),'(?m)^guid: ([a-f0-9]{32})').Groups[1].Value
    if(!$guid) { throw 'Missing existing Unity GUID.' }
    $shipReview += [pscustomobject]@{key=$shipRecord.key;name=$shipNames[[Array]::IndexOf($shipKeys,$shipRecord.key)];source=$shipRecord.source;destination=$destination;sha256=$sha;guid=$guid;profile=$profile}
}
if(@($shipReview.sha256 | Select-Object -Unique).Count -ne 12) { throw 'Duplicate warship paintings.' }
# All twelve pass preflight before replacing any production image. Existing .meta GUIDs stay intact.
$shipText=[IO.File]::ReadAllText($shipCode)
foreach($item in $shipReview) {
    $pattern='(\["'+$item.key+'"\] = new Vector3\()[^)]*(\),)'
    if([regex]::Matches($shipText,$pattern).Count -ne 1) { throw ('Contact row missing: '+$item.key) }
    $values=@($item.profile.center,$item.profile.contact,$item.profile.hullWidth) | ForEach-Object { $_.ToString('F6',[Globalization.CultureInfo]::InvariantCulture)+'f' }
    $replacement='${1}'+($values -join ', ')+'${2}'
    $shipText=[regex]::Replace($shipText,$pattern,$replacement)
}
foreach($item in $shipReview) { Copy-Item -LiteralPath $item.source -Destination $item.destination -Force }
[IO.File]::WriteAllText($shipCode,$shipText,[Text.UTF8Encoding]::new($false))
$shipReview | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $shipOutput 'asset-validation.json') -Encoding utf8
@{tool='built-in image_gen';date='2026-10-23';count=12;assets=$shipRecords} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $shipOutput 'generation-manifest.json') -Encoding utf8
$font=[Drawing.Font]::new('Microsoft YaHei',16)
$brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(235,233,217))
$pen=[Drawing.Pen]::new([Drawing.Color]::FromArgb(200,61,210,232),2)
foreach($group in @('boss','legendary')) {
    $offset=if($group -eq 'boss'){0}else{6}
    $board=[Drawing.Bitmap]::new(1500,760)
    $graphics=[Drawing.Graphics]::FromImage($board)
    $graphics.Clear([Drawing.Color]::FromArgb(29,48,59))
    $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    for($i=0;$i -lt 6;$i++) {
        $key=$shipKeys[$offset+$i]
        $item=$shipReview | Where-Object key -eq $key
        $x=($i%3)*500;$y=[Math]::Floor($i/3)*380
        $picture=[Drawing.Image]::FromFile($item.destination)
        $targetHeight=480*$picture.Height/$picture.Width
        $targetY=$y+8+(330-$targetHeight)/2
        $graphics.DrawImage($picture,[Drawing.RectangleF]::new($x+10,$targetY,480,$targetHeight))
        $waterY=$targetY+($item.profile.waterRow/$item.profile.height)*$targetHeight
        $waterX=$x+10+$item.profile.center*480
        $halfWidth=$item.profile.hullWidth*480/2
        $graphics.DrawLine($pen,[single]($waterX-$halfWidth),[single]$waterY,[single]($waterX+$halfWidth),[single]$waterY)
        $graphics.DrawString(($item.name+' · '+$item.key),$font,$brush,$x+12,$y+342)
        $picture.Dispose()
    }
    $board.Save((Join-Path $shipOutput ($group+'-preview.jpg')),[Drawing.Imaging.ImageFormat]::Jpeg)
    $graphics.Dispose();$board.Dispose()
}
$pen.Dispose();$brush.Dispose();$font.Dispose()
Write-Output 'Installed twelve original generated PNGs, preserving alpha and GUIDs; calibrated all twelve waterlines.'
