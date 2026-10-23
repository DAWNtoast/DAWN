param([switch]$AllowPartial)
$ErrorActionPreference='Stop'
$sceneRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$sceneOutput=Join-Path $sceneRoot 'outputs/imagegen/boss-event-scenes-20261023'
$sceneResources=Join-Path $sceneRoot 'UnityClient/Assets/_Project/Resources/EncounterIllustrations/BossScenes'
$sceneKeys=@('boss_fire','boss_tide','boss_blood','boss_thunder','boss_curse','boss_arcane','legendary_fire','legendary_tide','legendary_blood','legendary_thunder','legendary_curse','legendary_arcane')
$sceneNames=@('裂桅号','幽礁潜猎号','税金女王号','极光敕令号','月蚀圣骸','月织星槎号','余烬方舟号','潮汐镇魂号','绯刃寡妇号','雷冠审判号','铁誓不沉号','终界观星号')
$sceneRecords=@()
foreach($file in Get-ChildItem -LiteralPath $sceneOutput -Filter '*-prompts.json' -File) { $sceneRecords+=@(Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json) }
if(@($sceneRecords.key | Select-Object -Unique).Count -ne $sceneRecords.Count) { throw 'Duplicate scene identities.' }
if(!$AllowPartial -and $sceneRecords.Count -ne 12) { throw ('Expected twelve scenes, got '+$sceneRecords.Count) }
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies @('System.Drawing.Common','System.Drawing.Primitives','System.Private.Windows.GdiPlus','System.Private.Windows.Core','System.Runtime','System.Runtime.InteropServices') -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class BossSceneOpacity {
    public static int Minimum(string path) {
        using (var src=new Bitmap(path)) using(var bmp=src.Clone(new Rectangle(0,0,src.Width,src.Height),PixelFormat.Format32bppArgb)) {
            var data=bmp.LockBits(new Rectangle(0,0,bmp.Width,bmp.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            int minimum=255; var row=new byte[Math.Abs(data.Stride)];
            try { for(int y=0;y<bmp.Height;y++) { Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),row,0,row.Length); for(int x=0;x<bmp.Width;x++) minimum=Math.Min(minimum,row[x*4+3]); } }
            finally { bmp.UnlockBits(data); }
            return minimum;
        }
    }
}
'@
$sceneReview=foreach($record in $sceneRecords) {
    if($record.key -notin $sceneKeys -or !$record.source -or !$record.prompt) { throw 'Invalid scene record.' }
    $picture=[Drawing.Image]::FromFile($record.source)
    $width=$picture.Width; $height=$picture.Height; $picture.Dispose()
    if($width -ne 1536 -or $height -ne 1024) { throw ('Unexpected scene dimensions: '+$record.key) }
    $alpha=[BossSceneOpacity]::Minimum($record.source)
    if($alpha -ne 255) { throw ('Scene must have an entirely opaque painted background: '+$record.key) }
    $sha=(Get-FileHash -LiteralPath $record.source).Hash
    $target=Join-Path $sceneResources ('scene_'+$record.key+'.png')
    if((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target).Hash -ne $sha) { throw ('Existing scene differs: '+$target) }
    [pscustomobject]@{key=$record.key;name=$sceneNames[[Array]::IndexOf($sceneKeys,$record.key)];source=$record.source;destination=$target;width=$width;height=$height;minimumAlpha=$alpha;sha256=$sha}
}
if(@($sceneReview.sha256 | Select-Object -Unique).Count -ne $sceneReview.Count) { throw 'Duplicate scene image content.' }
New-Item -ItemType Directory -Path $sceneResources -Force | Out-Null
$folderMeta=$sceneResources+'.meta'
if(!(Test-Path -LiteralPath $folderMeta)) {
    $folderText="fileFormatVersion: 2`nguid: "+[guid]::NewGuid().ToString('N')+"`nfolderAsset: yes`nDefaultImporter:`n  externalObjects: {}`n  userData: ''`n  assetBundleName: ''`n  assetBundleVariant: ''`n"
    [IO.File]::WriteAllText($folderMeta,$folderText,[Text.UTF8Encoding]::new($false))
}
$template=[IO.File]::ReadAllText((Join-Path $sceneRoot 'UnityClient/Assets/_Project/Resources/EncounterIllustrations/moonlit_shadow_dial_v1.png.meta'))
foreach($item in $sceneReview) {
    Copy-Item -LiteralPath $item.source -Destination $item.destination -Force
    if(!(Test-Path -LiteralPath ($item.destination+'.meta'))) {
        $meta=[regex]::Replace($template,'(?m)^guid: [a-f0-9]+',('guid: '+[guid]::NewGuid().ToString('N')))
        $meta=$meta.Replace('Built-in image_gen event illustration 2026-10-22','Built-in image_gen opaque boss sea scene 2026-10-23')
        [IO.File]::WriteAllText($item.destination+'.meta',$meta.TrimEnd()+"`n",[Text.UTF8Encoding]::new($false))
    }
}
$sceneReview | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $sceneOutput 'asset-validation.json') -Encoding utf8
@{tool='built-in image_gen';date='2026-10-23';count=$sceneRecords.Count;assets=$sceneRecords;installed=$sceneReview} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $sceneOutput 'generation-manifest.json') -Encoding utf8
foreach($group in @('boss','legendary')) {
    $offset=if($group -eq 'boss'){0}else{6}
    $board=[Drawing.Bitmap]::new(1500,740); $graphics=[Drawing.Graphics]::FromImage($board)
    $graphics.Clear([Drawing.Color]::FromArgb(18,27,36)); $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $font=[Drawing.Font]::new('Microsoft YaHei',15); $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(232,226,213))
    for($i=0;$i -lt 6;$i++) {
        $key=$sceneKeys[$offset+$i]; $item=$sceneReview | Where-Object key -eq $key
        if(!$item) { continue }
        $x=($i%3)*500; $y=[Math]::Floor($i/3)*370
        $picture=[Drawing.Image]::FromFile($item.destination)
        $graphics.DrawImage($picture,[Drawing.Rectangle]::new($x+4,$y+4,492,328)); $picture.Dispose()
        $graphics.DrawString($item.name,$font,$brush,$x+10,$y+337)
    }
    $board.Save((Join-Path $sceneOutput ($group+'-scenes-preview.jpg')),[Drawing.Imaging.ImageFormat]::Jpeg)
    $brush.Dispose(); $font.Dispose(); $graphics.Dispose(); $board.Dispose()
}
Write-Output ('Installed '+$sceneReview.Count+' full-background boss scenes; every pixel opaque, original generated PNGs preserved.')
