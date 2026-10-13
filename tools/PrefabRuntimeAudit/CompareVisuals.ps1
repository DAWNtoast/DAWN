param(
    [Parameter(Mandatory=$true)][string]$BaselineDirectory,
    [Parameter(Mandatory=$true)][string]$OptimizedDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$auditRoot = 'D:\Game\Artifacts\PrefabRuntimeOptimization20261014\'
foreach($directory in @($BaselineDirectory,$OptimizedDirectory,$OutputDirectory)) {
    if(-not [IO.Path]::GetFullPath($directory).StartsWith($auditRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Use this audit output directories.'}
}
if(Test-Path -LiteralPath $OutputDirectory){throw 'Preserve earlier visual comparison evidence.'}
$baseline=Get-Content -LiteralPath (Join-Path $BaselineDirectory 'memory.json') -Raw|ConvertFrom-Json
$optimized=Get-Content -LiteralPath (Join-Path $OptimizedDirectory 'memory.json') -Raw|ConvertFrom-Json
if($baseline.status-ne'passed' -or $optimized.status-ne'passed' -or -not $baseline.visualCaptureRun -or -not $optimized.visualCaptureRun){throw 'Two completed separate visual-capture runs required.'}
if(($baseline.checkpoints.name-join'|')-ne($optimized.checkpoints.name-join'|')){throw 'Checkpoint order differs.'}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
Add-Type -AssemblyName System.Drawing.Common
Add-Type -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public sealed class PrefabVisualDifference {
    public int Width, Height, DifferentPixels, PixelsOverOne, PixelsOverEight, MaximumChannelDelta;
    public double RootMeanSquaredChannelError;
}
public static class PrefabVisualPixels {
    public static PrefabVisualDifference Compare(string first, string second, string destination) {
        using (var ai = new Bitmap(first)) using (var bi = new Bitmap(second)) {
            if(ai.Size != bi.Size) throw new InvalidOperationException("Screenshot dimensions differ.");
            int w=ai.Width,h=ai.Height; var rect=new Rectangle(0,0,w,h);
            using(var a=ai.Clone(rect,PixelFormat.Format32bppArgb)) using(var b=bi.Clone(rect,PixelFormat.Format32bppArgb)) using(var diff=new Bitmap(w,h,PixelFormat.Format32bppArgb)) {
                var ad=a.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                var bd=b.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                var dd=diff.LockBits(rect,ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
                var result=new PrefabVisualDifference{Width=w,Height=h};double squares=0;
                try {
                    var av=new byte[w*4];var bv=new byte[w*4];var dv=new byte[w*4];
                    for(int y=0;y<h;y++) {
                        Marshal.Copy(IntPtr.Add(ad.Scan0,y*ad.Stride),av,0,av.Length);Marshal.Copy(IntPtr.Add(bd.Scan0,y*bd.Stride),bv,0,bv.Length);
                        for(int x=0;x<w;x++) {
                            int p=x*4,max=0;
                            for(int c=0;c<3;c++){int delta=Math.Abs(av[p+c]-bv[p+c]);max=Math.Max(max,delta);squares+=delta*delta;}
                            if(max>0)result.DifferentPixels++;if(max>1)result.PixelsOverOne++;if(max>8)result.PixelsOverEight++;
                            result.MaximumChannelDelta=Math.Max(result.MaximumChannelDelta,max);
                            byte gray=(byte)((av[p]+av[p+1]+av[p+2])/18);
                            dv[p]=max>0?(byte)0:gray;dv[p+1]=max>0?(byte)0:gray;dv[p+2]=max>0?(byte)Math.Min(255,max*8):gray;dv[p+3]=255;
                        }
                        Marshal.Copy(dv,0,IntPtr.Add(dd.Scan0,y*dd.Stride),dv.Length);
                    }
                } finally {a.UnlockBits(ad);b.UnlockBits(bd);diff.UnlockBits(dd);}
                result.RootMeanSquaredChannelError=Math.Sqrt(squares/(w*(double)h*3));
                if(result.DifferentPixels>0)diff.Save(destination,ImageFormat.Png);
                return result;
            }
        }
    }
}
'@ -ReferencedAssemblies @('System.Drawing.Common','System.Drawing.Primitives','System.Private.Windows.GdiPlus','System.Private.Windows.Core')
function Normalize-Ui($graphic) {
    [ordered]@{
        type=$graphic.type;text=$graphic.text;font=$graphic.font;fontStyle=$graphic.fontStyle;alignment=$graphic.alignment;fontSize=$graphic.fontSize
        texture=$graphic.texture;sprite=$graphic.sprite;raycast=$graphic.raycast;interactable=$graphic.interactable
        color=@($graphic.color.r,$graphic.color.g,$graphic.color.b,$graphic.color.a|ForEach-Object{[Math]::Round([double]$_,4)})
        corners=@($graphic.corners|ForEach-Object{[Math]::Round([double]$_.x,2);[Math]::Round([double]$_.y,2)})
        uv=@($graphic.uv.x,$graphic.uv.y,$graphic.uv.width,$graphic.uv.height|ForEach-Object{[Math]::Round([double]$_,5)})
    }|ConvertTo-Json -Depth 6 -Compress
}
$rows=@(foreach($name in @($baseline.checkpoints.name)+@('final-menu')) {
    $before=Join-Path $BaselineDirectory ($name+'.png');$after=Join-Path $OptimizedDirectory ($name+'.png')
    $diff=Join-Path $OutputDirectory ($name+'.diff.png')
    $pixels=[PrefabVisualPixels]::Compare($before,$after,$diff)
    $a=Get-Content -LiteralPath (Join-Path $BaselineDirectory ($name+'.ui.json')) -Raw|ConvertFrom-Json
    $b=Get-Content -LiteralPath (Join-Path $OptimizedDirectory ($name+'.ui.json')) -Raw|ConvertFrom-Json
    $left=@{};$right=@{}
    foreach($g in $a.graphics){$left[$g.path]=@($left[$g.path])+@(Normalize-Ui $g)}
    foreach($g in $b.graphics){$right[$g.path]=@($right[$g.path])+@(Normalize-Ui $g)}
    $changes=@(foreach($path in @(@($left.Keys)+@($right.Keys)|Sort-Object -Unique)) {
        $beforeUi=@($left[$path]|Where-Object {$null-ne$_}|Sort-Object)-join"`n"
        $afterUi=@($right[$path]|Where-Object {$null-ne$_}|Sort-Object)-join"`n"
        if($beforeUi-ne$afterUi){[pscustomobject]@{Path=$path;Before=$beforeUi;After=$afterUi}}
    })
    if($changes.Count){$changes|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $OutputDirectory ($name+'.ui-diff.json')) -Encoding utf8}
    [pscustomobject]@{Checkpoint=$name;BaselineSHA256=(Get-FileHash -LiteralPath $before).Hash;OptimizedSHA256=(Get-FileHash -LiteralPath $after).Hash;
        Width=$pixels.Width;Height=$pixels.Height;DifferentPixels=$pixels.DifferentPixels;DifferentPercent=100*$pixels.DifferentPixels/($pixels.Width*$pixels.Height);
        PixelsOverOne=$pixels.PixelsOverOne;PixelsOverEight=$pixels.PixelsOverEight;MaximumChannelDelta=$pixels.MaximumChannelDelta;RMSE=$pixels.RootMeanSquaredChannelError;
        BaselineGraphics=$a.graphics.Count;OptimizedGraphics=$b.graphics.Count;UiChanges=$changes.Count;DifferenceImage=if($pixels.DifferentPixels){$diff}else{$null}}
})
$rows|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $OutputDirectory 'visual-comparison.json') -Encoding utf8
$rows|Export-Csv -LiteralPath (Join-Path $OutputDirectory 'visual-comparison.csv') -NoTypeInformation -Encoding utf8
[pscustomobject]@{Screenshots=$rows.Count;PixelIdentical=@($rows|Where-Object DifferentPixels -eq 0).Count;StructureIdentical=@($rows|Where-Object UiChanges -eq 0).Count;
    Scope='Differences require human inspection: time-dependent animation/selection can change pixels. UI snapshots compare active graphic geometry/font/text/resources/colors; no interaction proof.'}|ConvertTo-Json
