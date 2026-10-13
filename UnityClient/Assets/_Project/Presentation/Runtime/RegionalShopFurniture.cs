using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Six separately painted furniture atlases, with measured local bearing points.
    /// Source PNGs are immutable. Offline coverage masks remove the pale export matte and enclosed gaps.
    /// No port is represented by tinting another port's furniture.</summary>
    public sealed class RegionalShopFurniture
    {
        public sealed class Piece
        {
            public Rect Pixels;
            public Vector2 Bearing, SecondBearing;
            public Vector2[] Feet;
            public Rect WritingPixels;
            public float Width;
            public Rect Uv => new Rect(Pixels.x / 1536f, 1f - Pixels.yMax / 1024f, Pixels.width / 1536f, Pixels.height / 1024f);
            public Vector2 Size => new Vector2(Width, Width * Pixels.height / Pixels.width);
            public Vector2 Local(Vector2 pixel) => new Vector2((pixel.x - Pixels.center.x) * Width / Pixels.width,
                (Pixels.center.y - pixel.y) * Width / Pixels.width);
        }

        private static readonly Dictionary<string, RegionalShopFurniture> Cache = new Dictionary<string, RegionalShopFurniture>();
        static RegionalShopFurniture()
        {
            Application.quitting += ReleaseGeneratedResources;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseGeneratedResources;
#endif
        }

        public static void ReleaseGeneratedResources()
        {
            ReleaseUnusedGeneratedResources(null);
        }

        public static void ReleaseUnusedGeneratedResources(string retainedPort)
        {
            var released = new List<string>();
            foreach (var pair in Cache)
            {
                if (pair.Key == retainedPort) continue;
                var value = pair.Value;
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(value.Material);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(value.Material);
                }
                released.Add(pair.Key);
            }
            foreach (string port in released) Cache.Remove(port);
        }
        public static readonly string[] Ports = { "灰鸥港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "断锚港" };
        private static readonly string[] Slugs = { "gray_gull", "white_shell", "bronze_bell", "salt_crown", "twilight_tide", "broken_anchor" };
        public Texture2D Atlas { get; private set; }
        public Texture2D Coverage { get; private set; }
        public Material Material { get; private set; }
        public Piece[] Pieces { get; private set; }
        public Piece Mount(ComponentSlot slot) => Pieces[(int)slot];
        public Piece Sign => Pieces[5];
        public static string ResourcePath(string port)
        {
            int index = Array.IndexOf(Ports, port);
            return index < 0 ? string.Empty : "ComponentShopRegional/furniture_" + Slugs[index] + "_v1";
        }
        public static string CoverageResourcePath(string port)
        {
            string atlas = ResourcePath(port);
            return string.IsNullOrEmpty(atlas) ? string.Empty : atlas + "_coverage";
        }
        public static RegionalShopFurniture For(string port)
        {
            int index = Array.IndexOf(Ports, port);
            if (index < 0) return null;
            if (Cache.TryGetValue(port, out RegionalShopFurniture cached) && cached.Atlas != null) return cached;
            Texture2D atlas = Resources.Load<Texture2D>(ResourcePath(port));
            if (atlas == null) return null;
            Texture2D coverage = Resources.Load<Texture2D>(CoverageResourcePath(port));
            if (coverage == null)
            {
                Debug.LogError("Missing baked regional furniture coverage: " + CoverageResourcePath(port));
                return null;
            }
            atlas.filterMode = FilterMode.Bilinear;
            atlas.wrapMode = TextureWrapMode.Clamp;
            // Both textures are borrowed Resources assets. Keep no CPU pixel
            // copy and never destroy them while a live Graphic may own them.
            var result = new RegionalShopFurniture { Atlas = atlas, Coverage = coverage, Pieces = Describe(index) };
            result.Material = new Material(Resources.Load<Shader>("Shaders/RegionalFurniture"))
            { name = "Regional Furniture · " + port, hideFlags = HideFlags.HideAndDontSave };
            result.Material.SetTexture("_Coverage", result.Coverage);
            Cache[port] = result;
            return result;
        }

        private static Piece P(float x, float y, float w, float h, float width, Vector2 bearing, Vector2 second, params Vector2[] feet)
            => new Piece { Pixels = new Rect(x, y, w, h), Width = width, Bearing = bearing, SecondBearing = second, Feet = feet };
        private static Vector2 V(float x, float y) => new Vector2(x, y);
        private static Piece[] Describe(int port)
        {
            // Source measurements are top-left pixels, not guessed atlas-cell bottoms.
            // The grey-gull bow and keel extend beyond a nominal 512px cell.
            Piece[] pieces;
            switch (port)
            {
                case 0:
                    pieces = new[] {
                        P(25,125,545,345,280,V(173,220),V(439,316),V(104,360),V(392,452)),
                        P(600,220,330,230,140,V(764,265),V(764,265),V(647,371),V(771,428)),
                        P(985,225,525,225,310,V(1100,305),V(1390,300),V(1020,355),V(1096,426),V(1467,370)),
                        P(40,595,565,275,300,V(322,736),V(322,736),V(119,846),V(551,771)),
                        P(660,535,240,335,142,V(780,574),V(780,574),V(725,848),V(859,827)),
                        P(1100,490,325,400,180,V(1260,620),V(1260,620),V(1216,855),V(1305,877)) };
                    pieces[5].WritingPixels = new Rect(1146,552,235,104); break;
                case 1:
                    pieces = new[] {
                        P(25,135,495,310,280,V(154,198),V(382,287),V(84,348),V(458,428)),
                        P(590,220,350,205,140,V(767,254),V(767,254),V(622,366),V(808,405)),
                        P(1005,200,500,240,310,V(1100,290),V(1380,304),V(1051,356),V(1320,425),V(1463,348)),
                        P(35,575,480,295,300,V(300,738),V(300,738),V(75,780),V(302,854)),
                        P(630,520,265,350,142,V(764,561),V(764,561),V(684,825),V(796,849)),
                        P(1080,525,315,365,180,V(1240,620),V(1240,620),V(1160,862),V(1234,871)) };
                    pieces[5].WritingPixels = new Rect(1126,566,236,105); break;
                case 2:
                    pieces = new[] {
                        P(45,110,495,330,280,V(167,187),V(415,284),V(169,353),V(422,418)),
                        P(585,205,365,230,140,V(771,242),V(771,242),V(622,365),V(815,417)),
                        P(995,210,515,235,310,V(1100,281),V(1390,315),V(1033,350),V(1370,432),V(1480,340)),
                        P(50,580,480,295,300,V(290,723),V(290,723),V(105,780),V(410,851)),
                        P(660,530,240,350,142,V(783,566),V(783,566),V(715,844),V(827,860)),
                        P(1065,535,355,385,180,V(1230,620),V(1230,620),V(1185,881),V(1289,902)) };
                    pieces[5].WritingPixels = new Rect(1110,579,258,103); break;
                case 3:
                    pieces = new[] {
                        P(30,135,510,335,280,V(161,221),V(402,328),V(107,387),V(386,454)),
                        P(600,255,330,195,140,V(770,288),V(770,288),V(627,394),V(787,432)),
                        P(1005,255,515,195,310,V(1100,321),V(1400,333),V(1056,405),V(1462,436)),
                        P(45,600,475,255,300,V(290,728),V(290,728),V(80,747),V(398,834)),
                        P(650,520,250,325,142,V(774,560),V(774,560),V(699,811),V(835,832)),
                        P(1050,500,400,385,180,V(1240,620),V(1240,620),V(1180,868),V(1300,868)) };
                    pieces[5].WritingPixels = new Rect(1094,565,320,127); break;
                case 4:
                    pieces = new[] {
                        P(50,125,470,300,280,V(154,179),V(388,274),V(89,307),V(435,404)),
                        P(580,185,325,245,140,V(749,228),V(749,228),V(622,355),V(801,407)),
                        P(1010,220,495,225,310,V(1100,283),V(1390,310),V(1053,350),V(1355,428)),
                        P(45,615,485,255,300,V(290,735),V(290,735),V(96,747),V(444,850)),
                        P(630,520,280,350,142,V(768,562),V(768,562),V(667,852),V(858,844)),
                        P(1035,490,370,430,180,V(1220,620),V(1220,620),V(1174,893),V(1238,908)) };
                    pieces[5].WritingPixels = new Rect(1084,565,281,115); break;
                default:
                    pieces = new[] {
                        P(20,130,505,320,280,V(153,195),V(408,294),V(68,363),V(437,433)),
                        P(575,220,380,235,140,V(760,261),V(760,261),V(616,377),V(797,433)),
                        P(1000,210,515,245,310,V(1090,286),V(1390,303),V(1045,356),V(1328,436),V(1480,381)),
                        P(40,610,490,280,300,V(288,750),V(288,750),V(85,790),V(341,870)),
                        P(615,525,295,370,142,V(765,573),V(765,573),V(668,852),V(831,878)),
                        P(1075,500,365,410,180,V(1250,620),V(1250,620),V(1188,874),V(1306,894)) };
                    pieces[5].WritingPixels = new Rect(1124,549,269,143); break;
            }
            return pieces;
        }

#if UNITY_EDITOR
        // The unchanged coverage algorithm is used only by the offline baker
        // and pixel-equivalence tests, never when opening a shop in a player.
        public static bool IsExportMatte(Color32 c) => c.r >= 192 && c.g >= 192 && c.b >= 192 &&
            Math.Max(c.r, Math.Max(c.g, c.b)) - Math.Min(c.r, Math.Min(c.g, c.b)) <= 16;

        public static Texture2D BuildCoverage(Texture2D atlas)
        {
            Color32[] pixels = atlas.GetPixels32();
            int w = atlas.width, h = atlas.height;
            byte[] outside = new byte[pixels.Length];
            int[] queue = new int[pixels.Length];
            int head = 0, tail = 0;
            void Visit(int index)
            {
                if (outside[index] != 0 || !IsExportMatte(pixels[index])) return;
                outside[index] = 1; queue[tail++] = index;
            }
            for (int x = 0; x < w; x++) { Visit(x); Visit((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { Visit(y * w); Visit(y * w + w - 1); }
            while (head < tail)
            {
                int p = queue[head++], x = p % w, y = p / w;
                if (x > 0) Visit(p - 1); if (x + 1 < w) Visit(p + 1);
                if (y > 0) Visit(p - w); if (y + 1 < h) Visit(p + w);
            }
            // Enclosed spaces in the timber also contain the export checker.
            // Retain small bright inlays/highlights instead of globally keying white.
            var inspected = new bool[pixels.Length];
            for (int p = 0; p < pixels.Length; p++)
            {
                if (outside[p] != 0 || inspected[p] || !IsExportMatte(pixels[p])) continue;
                head = 0; tail = 1; queue[0] = p; inspected[p] = true;
                long brightness = 0;
                void Inspect(int index)
                {
                    if (outside[index] != 0 || inspected[index] || !IsExportMatte(pixels[index])) return;
                    inspected[index] = true; queue[tail++] = index;
                }
                while (head < tail)
                {
                    int at = queue[head++], x = at % w, y = at / w;
                    brightness += Math.Min(pixels[at].r, Math.Min(pixels[at].g, pixels[at].b));
                    if (x > 0) Inspect(at - 1); if (x + 1 < w) Inspect(at + 1);
                    if (y > 0) Inspect(at - w); if (y + 1 < h) Inspect(at + w);
                }
                if (tail >= 80 && brightness / tail >= 225)
                    for (int index = 0; index < tail; index++) outside[queue[index]] = 1;
            }
            var mask = new Color32[pixels.Length];
            for (int p = 0; p < mask.Length; p++)
            {
                byte alpha = outside[p] == 1 ? (byte)0 : (byte)255;
                // Contract only one source pixel at the exterior to suppress
                // matte fringes; this never globally dims the furniture.
                if (alpha != 0 && p % w > 0 && p % w < w - 1 && p >= w && p < mask.Length - w &&
                    (outside[p - 1] != 0 || outside[p + 1] != 0 || outside[p - w] != 0 || outside[p + w] != 0)) alpha = 180;
                mask[p] = new Color32(alpha, alpha, alpha, 255);
            }
            var texture = new Texture2D(w, h, TextureFormat.R8, false, true)
            { name = atlas.name + " Coverage", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(mask); texture.Apply(false, false);
            return texture;
        }
#endif
    }
}
