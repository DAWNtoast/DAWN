using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public sealed class WarshipImageProfile
{
    public int width, height, left, top, right, bottom, edgePixels, waterRow, keelY;
    public double transparentFraction, center, contact, hullWidth;
}

public static class WarshipImageInspection
{
    public static WarshipImageProfile Read(string path)
    {
        using (var source = new Bitmap(path))
        using (var bitmap = source.Clone(new Rectangle(0, 0, source.Width, source.Height), PixelFormat.Format32bppArgb))
        {
            int width = bitmap.Width, height = bitmap.Height;
            var area = new Rectangle(0, 0, width, height);
            var data = bitmap.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var alpha = new byte[width * height];
            var row = new byte[Math.Abs(data.Stride)];
            try
            {
                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                    for (int x = 0; x < width; x++) alpha[y * width + x] = row[x * 4 + 3];
                }
            }
            finally { bitmap.UnlockBits(data); }
            var profile = new WarshipImageProfile { width=width, height=height, left=width, top=height, right=-1, bottom=-1 };
            int transparent=0;
            for (int y=0; y<height; y++) for (int x=0; x<width; x++)
            {
                byte a=alpha[y*width+x];
                if (a==0) transparent++;
                if (a>=16)
                {
                    profile.left=Math.Min(profile.left,x); profile.right=Math.Max(profile.right,x);
                    profile.top=Math.Min(profile.top,y); profile.bottom=Math.Max(profile.bottom,y);
                }
                // Match the visible silhouette threshold; ignore sub-visible alpha noise.
                if (a>=16 && (x==0 || x==width-1 || y==0 || y==height-1)) profile.edgePixels++;
            }
            profile.transparentFraction=(double)transparent/(width*height);
            var bottoms=new List<int>();
            for (int x=(int)(width*.45); x<(int)(width*.55); x++)
                for (int y=height-1; y>=0; y--) if (alpha[y*width+x]>=128) { bottoms.Add(y); break; }
            if (bottoms.Count < width*.05) throw new InvalidOperationException("Cannot locate central hull: "+path);
            bottoms.Sort(); profile.keelY=bottoms[bottoms.Count/2];
            // Same 4% immersion convention used by the established ship waterline layout.
            profile.waterRow=profile.keelY-(int)Math.Round(height*.04);
            int lo=width, hi=-1, band=Math.Max(1,(int)Math.Round(height*.005));
            for (int y=Math.Max(0,profile.waterRow-band); y<=Math.Min(height-1,profile.waterRow+band); y++)
                for (int x=0; x<width; x++) if (alpha[y*width+x]>=128) { lo=Math.Min(lo,x); hi=Math.Max(hi,x); }
            if (hi<lo) throw new InvalidOperationException("Cannot locate immersed hull: "+path);
            profile.center=Math.Round((lo+hi)*.5/width,6);
            profile.contact=Math.Round((height-1-profile.waterRow)/(double)height,6);
            profile.hullWidth=Math.Round((hi-lo+1)/(double)width,6);
            return profile;
        }
    }
}
