param(
    [Parameter(Mandatory = $true)][string]$Manifest
)
$ErrorActionPreference = 'Stop'
$reviewWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$reviewRoot = Join-Path $reviewWorkspace 'outputs/imagegen/special-warships-20261023/candidate-review'
$reviewRecords = @(Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json)
if ($reviewRecords.Count -eq 0) { throw 'The candidate manifest is empty.' }
Add-Type -AssemblyName System.Drawing
if (-not ('WarshipImageInspection' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'Inspect.cs') -ReferencedAssemblies @(
        'System.Drawing.Common','System.Drawing.Primitives','System.Private.Windows.GdiPlus',
        'System.Private.Windows.Core','System.Runtime','System.Collections','System.Runtime.InteropServices'
    )
}
if (-not ('WarshipCandidateAlphaReview' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public sealed class WarshipAlphaBounds
{
    public int threshold, left, top, right, bottom, minMargin;
    public long pixels;
}
public sealed class WarshipAlphaReview
{
    public int width, height;
    public long totalPixels, alpha0, alpha1To15, alpha16To63, alpha64To127, alpha128To254, alpha255;
    public long outerSevenPercentPixels, outerSevenPercentNonzero;
    public double outerSevenPercentAverageAlpha;
    public int outerSevenPercentMaxAlpha, maxEdgeAlpha;
    public double foregroundAverageAlpha;
    public List<string> edgeSamples = new List<string>();
    public List<WarshipAlphaBounds> bounds = new List<WarshipAlphaBounds>();
    public Dictionary<string, int> alphaSamples = new Dictionary<string, int>();
}
public static class WarshipCandidateAlphaReview
{
    public static WarshipAlphaReview Read(string path)
    {
        using (var source = new Bitmap(path))
        using (var bitmap = source.Clone(new Rectangle(0,0,source.Width,source.Height), PixelFormat.Format32bppArgb))
        {
            int width = bitmap.Width, height = bitmap.Height;
            var result = new WarshipAlphaReview { width=width, height=height, totalPixels=(long)width*height };
            foreach (int threshold in new[] {1,16,128,254})
                result.bounds.Add(new WarshipAlphaBounds { threshold=threshold, left=width, top=height, right=-1, bottom=-1 });
            var alpha = new byte[width*height];
            var data = bitmap.LockBits(new Rectangle(0,0,width,height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var row = new byte[Math.Abs(data.Stride)];
            try
            {
                for (int y=0;y<height;y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),row,0,row.Length);
                    for (int x=0;x<width;x++) alpha[y*width+x]=row[x*4+3];
                }
            }
            finally { bitmap.UnlockBits(data); }
            long borderAlpha=0, foregroundAlpha=0, foregroundPixels=0;
            int borderX=(int)Math.Ceiling(width*.07), borderY=(int)Math.Ceiling(height*.07);
            for (int y=0;y<height;y++) for (int x=0;x<width;x++)
            {
                int a=alpha[y*width+x];
                if (a==0) result.alpha0++;
                else if (a<16) result.alpha1To15++;
                else if (a<64) result.alpha16To63++;
                else if (a<128) result.alpha64To127++;
                else if (a<255) result.alpha128To254++;
                else result.alpha255++;
                if (a>=128) { foregroundAlpha+=a; foregroundPixels++; }
                if (a>0 && (x==0 || x==width-1 || y==0 || y==height-1))
                {
                    result.maxEdgeAlpha=Math.Max(result.maxEdgeAlpha,a);
                    if (result.edgeSamples.Count<32) result.edgeSamples.Add(x+","+y+": alpha="+a);
                }
                foreach (var bounds in result.bounds) if (a>=bounds.threshold)
                {
                    bounds.pixels++;
                    bounds.left=Math.Min(bounds.left,x); bounds.top=Math.Min(bounds.top,y);
                    bounds.right=Math.Max(bounds.right,x); bounds.bottom=Math.Max(bounds.bottom,y);
                }
                if (x<borderX || x>=width-borderX || y<borderY || y>=height-borderY)
                {
                    result.outerSevenPercentPixels++;
                    if (a>0) result.outerSevenPercentNonzero++;
                    borderAlpha+=a;
                    result.outerSevenPercentMaxAlpha=Math.Max(result.outerSevenPercentMaxAlpha,a);
                }
            }
            result.outerSevenPercentAverageAlpha=(double)borderAlpha/result.outerSevenPercentPixels;
            result.foregroundAverageAlpha=foregroundPixels==0 ? 0 : (double)foregroundAlpha/foregroundPixels;
            foreach (var bounds in result.bounds)
                bounds.minMargin=bounds.pixels==0 ? Math.Min(width,height) :
                    Math.Min(Math.Min(bounds.left,bounds.top),Math.Min(width-1-bounds.right,height-1-bounds.bottom));
            result.alphaSamples["topLeft"]=alpha[0];
            result.alphaSamples["topCenter"]=alpha[width/2];
            result.alphaSamples["topRight"]=alpha[width-1];
            result.alphaSamples["leftCenter"]=alpha[(height/2)*width];
            result.alphaSamples["rightCenter"]=alpha[(height/2)*width+width-1];
            result.alphaSamples["bottomLeft"]=alpha[(height-1)*width];
            result.alphaSamples["bottomCenter"]=alpha[(height-1)*width+width/2];
            result.alphaSamples["bottomRight"]=alpha[height*width-1];
            return result;
        }
    }
}
'@ -ReferencedAssemblies @(
        'System.Drawing.Common','System.Drawing.Primitives','System.Private.Windows.GdiPlus',
        'System.Private.Windows.Core','System.Runtime','System.Collections','System.Runtime.InteropServices'
    )
}
New-Item -ItemType Directory -Path $reviewRoot -Force | Out-Null
$reviewReports = foreach ($record in $reviewRecords) {
    if ($record.key -notmatch '^[a-z0-9_]+$' -or -not $record.source) { throw 'Expected candidate {key,source}.' }
    if (-not (Test-Path -LiteralPath $record.source)) { throw "Candidate does not exist: $($record.source)" }
    $hash = (Get-FileHash -LiteralPath $record.source -Algorithm SHA256).Hash
    $candidateRoot = Join-Path $reviewRoot ($record.key + '-' + $hash.Substring(0,12).ToLowerInvariant())
    New-Item -ItemType Directory -Path $candidateRoot -Force | Out-Null
    $copy = Join-Path $candidateRoot ($record.key + '.png')
    if (Test-Path -LiteralPath $copy) {
        if ((Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne $hash) { throw "Candidate copy differs: $copy" }
    } else {
        Copy-Item -LiteralPath $record.source -Destination $copy
    }
    $profile = [WarshipImageInspection]::Read($copy)
    $alpha = [WarshipCandidateAlphaReview]::Read($copy)
    $previews = [ordered]@{}
    foreach ($background in @(
        @{name='deep-blue';color=[Drawing.Color]::FromArgb(255,21,57,75)},
        @{name='light';color=[Drawing.Color]::FromArgb(255,230,224,211)}
    )) {
        $preview = Join-Path $candidateRoot ($record.key + '-' + $background.name + '.png')
        $bitmap = [Drawing.Bitmap]::new($profile.width,$profile.height)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        $image = [Drawing.Image]::FromFile($copy)
        try {
            $graphics.Clear($background.color)
            $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceOver
            $graphics.DrawImageUnscaled($image,0,0)
            $bitmap.Save($preview,[Drawing.Imaging.ImageFormat]::Png)
        } finally {
            $image.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
        }
        $previews[$background.name] = $preview
    }
    $minimumVisibleMargin = [Math]::Min([Math]::Min($profile.left,$profile.top),[Math]::Min($profile.width-1-$profile.right,$profile.height-1-$profile.bottom))
    $report = [pscustomobject]@{
        key=$record.key; source=$record.source; candidateCopy=$copy; sha256=$hash
        profile=$profile; minimumVisibleMargin=$minimumVisibleMargin
        alpha=$alpha; previews=$previews
        productionModified=$false
    }
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $candidateRoot 'profile.json') -Encoding utf8
    $report
}
$reviewReports | ConvertTo-Json -Depth 8
