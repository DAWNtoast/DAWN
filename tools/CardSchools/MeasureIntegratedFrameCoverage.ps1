param(
    [string]$NativeDirectory = 'D:\Game\Artifacts\CardFramesSquare20261012\Native',
    [string]$OutputPath = 'D:\Game\Artifacts\CardFramesSquare20261012\window-coverage.json',
    [double]$ArtSize = 216,
    [double]$ArtCenterX = -1.14093,
    [double]$ArtCenterY = 64
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies @([Drawing.Bitmap].Assembly.Location,[Drawing.Color].Assembly.Location,(Join-Path $PSHOME 'System.Private.Windows.GdiPlus.dll'),(Join-Path $PSHOME 'System.Private.Windows.Core.dll')) -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class IntegratedWindowProbe {
    public static int[] Bounds(string path, int threshold) {
        using(var b = new Bitmap(path)) {
            int w=b.Width,h=b.Height;
            var data=b.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            var bytes=new byte[data.Stride*h];
            Marshal.Copy(data.Scan0,bytes,0,bytes.Length);
            b.UnlockBits(data);
            var visited=new bool[w*h]; var queue=new int[w*h];
            int head=0,tail=0,seed=500*w+w/2;
            visited[seed]=true;queue[tail++]=seed;
            int minX=w,maxX=0,minY=h,maxY=0;
            int[] dx={-1,1,0,0},dy={0,0,-1,1};
            while(head<tail) {
                int p=queue[head++],x=p%w,y=p/w;
                minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);
                minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);
                for(int d=0;d<4;d++) {
                    int nx=x+dx[d],ny=y+dy[d];
                    if(nx<95||nx>870||ny<130||ny>995)continue;
                    int q=ny*w+nx;
                    if(visited[q]||bytes[ny*data.Stride+nx*4+3]>threshold)continue;
                    visited[q]=true;queue[tail++]=q;
                }
            }
            return new[]{minX,minY,maxX,maxY,tail,w,h};
        }
    }
}
'@
$layouts=(Get-Content -LiteralPath 'D:\Game\UnityClient\Assets\_Project\Resources\CardSchools\Frames\Integrated\frame-layouts.json' -Raw | ConvertFrom-Json).entries
$samples=@()
foreach($file in Get-ChildItem -LiteralPath $NativeDirectory -Filter 'frame-*.png') {
    $entry=$layouts | Where-Object id -eq $file.BaseName
    $layout=$entry.rect
    if(-not $layout) { throw "Missing layout for $($file.BaseName)" }
    $artRect=$entry.illustrationRect
    $size=$ArtSize; $centerX=$ArtCenterX; $centerY=$ArtCenterY
    if($null -ne $artRect -and $artRect.width -gt 0) {
        $size=$artRect.width; $centerX=$artRect.x+$artRect.width/2; $centerY=$artRect.y+$artRect.height/2
        if($artRect.width -ne $artRect.height) { throw "Illustration must remain square: $($file.BaseName)" }
    }
    foreach($threshold in @(32,250)) {
        $b=[IntegratedWindowProbe]::Bounds($file.FullName,$threshold)
        $left=($centerX-$size/2-$layout.x)/$layout.width*$b[5]
        $right=($centerX+$size/2-$layout.x)/$layout.width*$b[5]
        $top=($layout.y+$layout.height-$centerY-$size/2)/$layout.height*$b[6]
        $bottom=($layout.y+$layout.height-$centerY+$size/2)/$layout.height*$b[6]
        $isolated=$b[0] -gt 95 -and $b[1] -gt 130 -and $b[2] -lt 870 -and $b[3] -lt 995
        $covered=$isolated -and $left -le $b[0] -and $top -le $b[1] -and $right -ge $b[2] -and $bottom -ge $b[3]
        $samples += [pscustomobject]@{
            id=$file.BaseName; artSize=$size; artCenter=@($centerX,$centerY); alphaThreshold=$threshold; nativeWindowBounds=@($b[0],$b[1],$b[2],$b[3])
            artNativeBounds=@($left,$top,$right,$bottom); isolated=$isolated; covered=$covered
            marginsLogical=@((($b[0]-$left)*$layout.width/$b[5]),(($b[1]-$top)*$layout.height/$b[6]),(($right-$b[2])*$layout.width/$b[5]),(($bottom-$b[3])*$layout.height/$b[6]))
        }
    }
}
$report=[pscustomobject]@{generatedAt=[DateTimeOffset]::Now.ToString('o');artSize=$ArtSize;artCenter=@($ArtCenterX,$ArtCenterY);frames=$samples.Count/2;passed=(@($samples | Where-Object {-not $_.covered}).Count -eq 0);samples=$samples}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding utf8
$report | Select-Object frames,passed,artSize,artCenter | ConvertTo-Json
$samples | Where-Object {-not $_.covered} | ConvertTo-Json -Depth 5
if(-not $report.passed) { throw 'Integrated frame illustration coverage failed.' }
