using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// A dossier-local, boundary-connected paper matte. The authored atlas and
    /// its import settings are borrowed unchanged; only an in-memory coverage
    /// texture and a material instance belong to this component.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RawImage))]
    public sealed class TavernPortraitPaperBlend : MonoBehaviour
    {
        public const string ShaderResourcePath = "Shaders/TavernPortraitPaperBlend";
        private const int MaximumCellSize = 512;
        private RawImage target;
        private Texture sourceTexture;
        private Rect sourceUv;
        private Material previousMaterial;
        private Material ownedMaterial;
        private Texture2D maskTexture;

        public Material OwnedMaterial => ownedMaterial;
        public Texture2D MaskTexture => maskTexture;

        public static TavernPortraitPaperBlend Attach(RawImage image)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            TavernPortraitPaperBlend blend = image.GetComponent<TavernPortraitPaperBlend>();
            if (blend == null) blend = image.gameObject.AddComponent<TavernPortraitPaperBlend>();
            if (!blend.enabled) blend.enabled = true;
            if (blend.ownedMaterial == null || blend.sourceTexture != image.texture || blend.sourceUv != image.uvRect)
                blend.Rebuild();
            return blend;
        }

        private void OnEnable()
        {
            Rebuild();
        }

        private void LateUpdate()
        {
            RawImage image = GetComponent<RawImage>();
            if (image != null && (image.texture != sourceTexture || image.uvRect != sourceUv)) Rebuild();
        }

        private void Rebuild()
        {
            ReleaseOwnedObjects();
            target = GetComponent<RawImage>();
            if (target == null || target.texture == null) return;
            sourceTexture = target.texture;
            sourceUv = target.uvRect;
            // Headless EditMode layout tests have no render device. Keep the
            // original image/material and let a graphical preview build a matte.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (Mathf.Abs(sourceUv.width) < 0.00001f || Mathf.Abs(sourceUv.height) < 0.00001f) return;
            Shader shader = Resources.Load<Shader>(ShaderResourcePath);
            if (shader == null)
            {
                Debug.LogWarning("Missing dossier paper-blend shader: Resources/" + ShaderResourcePath, this);
                return;
            }

            try
            {
                int width = Mathf.Clamp(Mathf.RoundToInt(sourceTexture.width * Mathf.Abs(sourceUv.width)), 4, MaximumCellSize);
                int height = Mathf.Clamp(Mathf.RoundToInt(sourceTexture.height * Mathf.Abs(sourceUv.height)), 4, MaximumCellSize);
                Color32[] encoded = ReadCellSrgb(sourceTexture, sourceUv, width, height);
                Color32[] coverage = BuildCoverageWithPaperProfile(encoded, width, height, KnownPaperOpenings(sourceTexture, sourceUv),
                    UsesTwilightPalePaperModel(sourceTexture.name, sourceUv));
                maskTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
                {
                    name = "Tavern Portrait Connected Paper Mask (Runtime)",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                maskTexture.SetPixels32(coverage);
                // Keep this small data texture readable for editor acceptance
                // diagnostics. It is never encoded to an image file or asset.
                maskTexture.Apply(false, false);
                ownedMaterial = new Material(shader)
                {
                    name = "Tavern Portrait Paper Blend (Instance)",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                ownedMaterial.SetTexture("_PaperMask", maskTexture);
                ownedMaterial.SetVector("_MaskUv", new Vector4(1f / sourceUv.width, 1f / sourceUv.height,
                    -sourceUv.x / sourceUv.width, -sourceUv.y / sourceUv.height));
                ownedMaterial.SetFloat("_BottomFeather", 0.035f);
                previousMaterial = target.material;
                target.material = ownedMaterial;
                target.SetMaterialDirty();
            }
            catch (Exception exception)
            {
                // A failed GPU readback leaves the borrowed portrait visible.
                Debug.LogWarning("Could not build the dossier paper mask: " + exception.Message, this);
                ReleaseOwnedObjects();
            }
        }

        private static bool UsesTwilightPalePaperModel(string sourceName, Rect uv)
        {
            // Only these two authored cells have the unusually pale head halo.
            // Other portraits, especially cream headscarves, keep the default model.
            if (sourceName != "crew_twilight_tide_portraits_v2" || !Mathf.Approximately(uv.width, 1f / 3f) ||
                !Mathf.Approximately(uv.height, 0.5f)) return false;
            return (Mathf.Approximately(uv.x, 0f) && Mathf.Approximately(uv.y, 0.5f)) ||
                (Mathf.Approximately(uv.x, 2f / 3f) && Mathf.Approximately(uv.y, 0f));
        }

        private static Vector2[] KnownPaperOpenings(Texture source, Rect uv)
        {
            // Hand-checked empty openings in the original v2 artwork, expressed
            // in bottom-left cell UV. Do not infer that other enclosed pale
            // regions are background: they may be a face, sleeve or headscarf.
            if (!Mathf.Approximately(uv.width, 1f / 3f) || !Mathf.Approximately(uv.height, 0.5f)) return null;
            int column = Mathf.RoundToInt(uv.x * 3f);
            int row = Mathf.RoundToInt((1f - uv.y - uv.height) * 2f);
            int profession = row * 3 + column;
            if (source.name == "crew_white_shell_portraits_v2" && profession == 5)
                return new[] { new Vector2(0.286133f, 0.463867f) };
            if (source.name == "crew_white_shell_portraits_v2" && profession == 0)
                return new[]
                {
                    new Vector2(268.5f / 512f, 1f - 24.5f / 512f),
                    new Vector2(219.5f / 512f, 1f - 35.5f / 512f),
                    new Vector2(306.5f / 512f, 1f - 29.5f / 512f),
                    new Vector2(435.5f / 512f, 1f - 217.5f / 512f),
                    new Vector2(421.5f / 512f, 1f - 245.5f / 512f),
                    new Vector2(431.5f / 512f, 1f - 289.5f / 512f),
                    new Vector2(54.5f / 512f, 1f - 252.5f / 512f),
                    new Vector2(29.5f / 512f, 1f - 346.5f / 512f),
                    new Vector2(59.5f / 512f, 1f - 410.5f / 512f),
                };
            if (source.name == "crew_gray_gull_portraits_v2" && profession == 5)
                return new[] { new Vector2(0.389648f, 0.440430f) };
            if (source.name == "crew_gray_gull_portraits_v2" && profession == 0)
                // Original 512px cell: individually checked empty curl openings,
                // not the skin, earring, headscarf or cream sleeve.
                return new[]
                {
                    new Vector2(70.5f / 512f, 1f - 250.5f / 512f),
                    new Vector2(35.5f / 512f, 1f - 335.5f / 512f),
                    new Vector2(360.5f / 512f, 1f - 160.5f / 512f),
                    new Vector2(354.5f / 512f, 1f - 206.5f / 512f),
                    new Vector2(362.5f / 512f, 1f - 253.5f / 512f),
                    new Vector2(359.5f / 512f, 1f - 219.5f / 512f),
                };
            if (source.name == "crew_gray_gull_portraits_v2" && profession == 4)
                return new[]
                {
                    new Vector2(137.5f / 512f, 1f - 107.5f / 512f),
                    new Vector2(140.5f / 512f, 1f - 176.5f / 512f),
                    new Vector2(141.5f / 512f, 1f - 199.5f / 512f),
                    new Vector2(145.5f / 512f, 1f - 214.5f / 512f),
                };
            if (source.name == "crew_twilight_tide_portraits_v2" && profession == 5)
                return new[] { new Vector2(176.5f / 512f, 1f - 228.5f / 512f), new Vector2(146.5f / 512f, 1f - 258.5f / 512f) };
            if (source.name == "crew_bronze_bell_portraits_v2" && profession == 0)
                return new[] { new Vector2(0.188477f, 0.260742f), new Vector2(0.127930f, 0.149414f), new Vector2(0.118164f, 0.059570f) };
            return null;
        }

        private static Color32[] ReadCellSrgb(Texture source, Rect uv, int width, int height)
        {
            RenderTexture priorActive = RenderTexture.active;
            bool priorSrgbWrite = GL.sRGBWrite;
            RenderTexture cropped = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D readback = null;
            try
            {
                cropped.filterMode = FilterMode.Point;
                // In a Linear project Blit samples the sRGB atlas into linear
                // RGB, then this sRGB render target encodes it back to authored
                // bytes. In Gamma mode no extra encode is necessary.
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(source, cropped, uv.size, uv.position);
                RenderTexture.active = cropped;
                readback = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                readback.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                return readback.GetPixels32();
            }
            finally
            {
                RenderTexture.active = priorActive;
                GL.sRGBWrite = priorSrgbWrite;
                RenderTexture.ReleaseTemporary(cropped);
                DestroyOwned(readback);
            }
        }

        private static Color32[] BuildCoverage(Color32[] pixels, int width, int height)
        {
            return BuildCoverageWithSeeds(pixels, width, height, null);
        }

        private static Color32[] BuildCoverageWithSeeds(Color32[] pixels, int width, int height, Vector2[] additionalPaperSeeds)
        {
            return BuildCoverageWithPaperProfile(pixels, width, height, additionalPaperSeeds, false);
        }

        private static Color32[] BuildCoverageWithPaperProfile(Color32[] pixels, int width, int height,
            Vector2[] additionalPaperSeeds, bool allowTwilightPalePaper)
        {
            Color32[] result = new Color32[pixels.Length];
            for (int index = 0; index < result.Length; index++) result[index] = new Color32(255, 255, 255, 255);
            if (!TryEstimatePaper(pixels, width, height, out Vector3 paper)) return result;

            Vector3[] smooth = SmoothColors(pixels, width, height);
            bool[] candidate = new bool[pixels.Length];
            bool[] clearPaper = new bool[pixels.Length];
            bool[] barrier = new bool[pixels.Length];
            bool[] background = new bool[pixels.Length];
            int[] queue = new int[pixels.Length];
            int count = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    Vector3 original = Encoded(pixels[index]);
                    bool originalMatchesPaper = IsPaper(original, paper, false) ||
                        (allowTwilightPalePaper && IsTwilightPalePaper(original, paper));
                    // Bright warm paper around hair is often paler than the
                    // corner sample. Judge this original pixel before smoothing:
                    // nearby dark hair must not turn real paper into a 3px halo.
                    clearPaper[index] = originalMatchesPaper && original.y >= paper.y - 0.035f &&
                        original.x - original.y <= paper.x - paper.y + 0.025f;
                    // Smoothing must not borrow yellow from adjacent paper and
                    // misclassify a cream garment corner as removable paper.
                    candidate[index] = pixels[index].a < 4 || originalMatchesPaper;
                    Vector3 acrossX = smooth[y * width + Mathf.Min(width - 1, x + 2)] - smooth[y * width + Mathf.Max(0, x - 2)];
                    Vector3 acrossY = smooth[Mathf.Min(height - 1, y + 2) * width + x] - smooth[Mathf.Max(0, y - 2) * width + x];
                    barrier[index] = !clearPaper[index] && (MaxAbs(acrossX) > 0.18f || MaxAbs(acrossY) > 0.18f);
                    // All four edges can contribute seeds, but an edge pixel
                    // must meet the tighter paper test and have no silhouette
                    // gradient. Bright clothing alone never qualifies by value.
                    if ((x == 0 || y == 0 || x == width - 1 || y == height - 1) && candidate[index] &&
                        !barrier[index] && (pixels[index].a < 4 || IsPaper(smooth[index], paper, true)))
                    {
                        background[index] = true;
                        queue[count++] = index;
                    }
                }
            }

            if (additionalPaperSeeds != null)
            {
                foreach (Vector2 seed in additionalPaperSeeds)
                {
                    // Coordinates use the mask's bottom-left origin. Reject
                    // out-of-range/NaN positions instead of clamping a mistaken
                    // annotation onto a different part of the portrait.
                    if (!(seed.x >= 0f && seed.x <= 1f && seed.y >= 0f && seed.y <= 1f)) continue;
                    int x = Mathf.Min(width - 1, Mathf.FloorToInt(seed.x * width));
                    int y = Mathf.Min(height - 1, Mathf.FloorToInt(seed.y * height));
                    int index = y * width + x;
                    // An explicit annotation identifies a known empty opening;
                    // it still cannot seed dark skin, white cloth or colored ink.
                    if (background[index]) continue;
                    if (!IsPaper(Encoded(pixels[index]), paper, false))
                    {
                        ClearAnnotatedPaperOpening(pixels, background, width, height, x, y, paper);
                        continue;
                    }
                    background[index] = true;
                    queue[count++] = index;
                }
            }

            for (int head = 0; head < count; head++)
            {
                int current = queue[head];
                int x = current % width;
                int y = current / width;
                if (x > 0) Visit(current - 1, current);
                if (x + 1 < width) Visit(current + 1, current);
                if (y > 0) Visit(current - width, current);
                if (y + 1 < height) Visit(current + width, current);
            }

            // Only confirmed boundary-connected paper is removed. A narrow
            // one/two-pixel fringe may soften other paper-like pixels, while
            // skin, fabric and uncertain enclosed areas stay fully opaque.
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    byte alpha = 255;
                    if (background[index]) alpha = 0;
                    else if (candidate[index])
                    {
                        int distance = DistanceToBackground(x, y, width, height, background);
                        if (distance == 1) alpha = 100;
                        else if (distance == 2) alpha = 210;
                    }
                    result[index] = new Color32(alpha, alpha, alpha, 255);
                }
            }
            RemoveDetachedPaperSpecks(pixels, result, width, height, paper);
            RefinePaperFringe(pixels, result, width, height);
            RemoveDetachedPaperSpecks(pixels, result, width, height, paper);
            return result;

            void Visit(int neighbor, int current)
            {
                if (background[neighbor] || !candidate[neighbor] || barrier[neighbor]) return;
                if (!clearPaper[neighbor] && MaxAbs(smooth[neighbor] - smooth[current]) > 0.05f) return;
                background[neighbor] = true;
                queue[count++] = neighbor;
            }
        }

        private static void ClearAnnotatedPaperOpening(Color32[] source, bool[] background, int width, int height,
            int seedX, int seedY, Vector3 cornerPaper)
        {
            // Some hand-checked hair openings have different local paper than
            // the atlas corners (including import filtering). Learn ONLY from
            // that annotated opening, never from arbitrary bright skin/cloth.
            Vector3 localPaper = Encoded(source[seedY * width + seedX]);
            if (localPaper.y < 0.68f || localPaper.x - localPaper.y < 0.035f ||
                localPaper.y - localPaper.z < 0.095f || MaxAbs(localPaper - cornerPaper) > 0.3f) return;
            int radius = Mathf.Max(2, Mathf.RoundToInt(Mathf.Min(width, height) / 20f));
            int[] pending = new int[(radius * 2 + 1) * (radius * 2 + 1)];
            int count = 1;
            pending[0] = seedY * width + seedX;
            background[pending[0]] = true;
            for (int head = 0; head < count; head++)
            {
                int index = pending[head];
                int x = index % width;
                int y = index / width;
                Visit(x - 1, y); Visit(x + 1, y); Visit(x, y - 1); Visit(x, y + 1);
            }

            void Visit(int x, int y)
            {
                if (x < 0 || y < 0 || x >= width || y >= height ||
                    Mathf.Abs(x - seedX) > radius || Mathf.Abs(y - seedY) > radius) return;
                int index = y * width + x;
                if (background[index]) return;
                Vector3 color = Encoded(source[index]);
                if (color.x - color.y < 0.03f || color.y - color.z < 0.09f || MaxAbs(color - localPaper) > 0.105f) return;
                background[index] = true;
                pending[count++] = index;
            }
        }

        private static void RefinePaperFringe(Color32[] source, Color32[] coverage, int width, int height)
        {
            // A fixed chroma key misses pale, almost neutral paper flecks. Compare
            // only a two-pixel outer band with the actual neighbouring paper AND
            // a stable interior foreground sample. This is not a global white key:
            // a matching white hair/cloth interior protects its light boundary.
            bool[] background = new bool[coverage.Length];
            for (int i = 0; i < coverage.Length; i++) background[i] = coverage[i].r == 0;
            byte[] distance = new byte[coverage.Length];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    distance[y * width + x] = (byte)(background[y * width + x] ? 0 :
                        DistanceToBackground(x, y, width, height, background));

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    if (distance[index] == 0 || distance[index] > 2) continue;
                    Vector3 color = Encoded(source[index]);
                    if (color.y < 0.68f || color.x - color.y < 0.012f || color.y - color.z < 0.025f) continue;
                    float nearestPaper = float.MaxValue;
                    float nearestForeground = float.MaxValue;
                    for (int dy = -5; dy <= 5; dy++)
                        for (int dx = -5; dx <= 5; dx++)
                        {
                            if (x + dx < 0 || x + dx >= width || y + dy < 0 || y + dy >= height) continue;
                            int neighbor = (y + dy) * width + x + dx;
                            float delta = MaxAbs(color - Encoded(source[neighbor]));
                            if (background[neighbor] && Mathf.Abs(dx) <= 3 && Mathf.Abs(dy) <= 3)
                                nearestPaper = Mathf.Min(nearestPaper, delta);
                            else if (distance[neighbor] == 3 && coverage[neighbor].r == 255)
                                nearestForeground = Mathf.Min(nearestForeground, delta);
                        }
                    if (nearestForeground < 0.07f) continue;
                    if (nearestPaper < 0.105f && nearestPaper + 0.035f < nearestForeground)
                        coverage[index] = new Color32(0, 0, 0, 255);
                }
        }

        private static void RemoveDetachedPaperSpecks(Color32[] source, Color32[] coverage, int width, int height, Vector3 paper)
        {
            // At 512x512, only disconnected components of at most 256 pixels
            // qualify. Scale with cell area; never broaden the global white key.
            int areaLimit = Mathf.Max(1, Mathf.RoundToInt(width * height / 1024f));
            int[] labels = new int[coverage.Length];
            int[] queue = new int[coverage.Length];
            List<bool> removable = new List<bool> { false };
            int largestComponent = 0;
            int largestArea = 0;
            for (int start = 0; start < coverage.Length; start++)
            {
                if (coverage[start].r == 0 || labels[start] != 0) continue;
                int component = removable.Count;
                int count = 1;
                int paperPixels = 0;
                bool containsInk = false;
                queue[0] = start;
                labels[start] = component;
                for (int head = 0; head < count; head++)
                {
                    int current = queue[head];
                    Vector3 color = Encoded(source[current]);
                    float brightest = Mathf.Max(color.x, Mathf.Max(color.y, color.z));
                    float darkest = Mathf.Min(color.x, Mathf.Min(color.y, color.z));
                    bool brightPaper = IsPaper(color, paper, false) && color.y >= paper.y - 0.06f;
                    bool warmWhite = color.y >= 0.8f && color.z >= 0.67f && color.x >= color.y - 0.015f &&
                        color.y >= color.z - 0.015f && brightest - darkest <= 0.25f;
                    bool paperLike = brightPaper || warmWhite || source[current].a < 4;
                    if (paperLike) paperPixels++;
                    // A dark hair stroke or a saturated accessory protects its
                    // entire small component, even when surrounded by pale ink.
                    else if (brightest < 0.72f || brightest - darkest > 0.22f) containsInk = true;

                    int x = current % width;
                    int y = current / width;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if ((dx == 0 && dy == 0) || x + dx < 0 || x + dx >= width || y + dy < 0 || y + dy >= height) continue;
                            int neighbor = (y + dy) * width + x + dx;
                            if (coverage[neighbor].r == 0 || labels[neighbor] != 0) continue;
                            // Eight-neighbor connectivity preserves diagonally
                            // attached white hair and headwear as part of the body.
                            labels[neighbor] = component;
                            queue[count++] = neighbor;
                        }
                }
                removable.Add(count <= areaLimit && paperPixels >= count * 0.92f && !containsInk);
                if (count > largestArea)
                {
                    largestArea = count;
                    largestComponent = component;
                }
            }

            for (int index = 0; index < coverage.Length; index++)
            {
                int component = labels[index];
                if (component != largestComponent && removable[component])
                    coverage[index] = new Color32(0, 0, 0, 255);
            }
        }

        private static bool TryEstimatePaper(Color32[] pixels, int width, int height, out Vector3 paper)
        {
            // Estimate from small empty blocks at the TOP corners only. Bottom
            // corners often contain a cream sleeve and must not train the matte.
            List<float> red = new List<float>();
            List<float> green = new List<float>();
            List<float> blue = new List<float>();
            int inset = Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(width, height) * 0.015f));
            int block = Mathf.Max(2, Mathf.RoundToInt(Mathf.Min(width, height) * 0.075f));
            for (int y = Mathf.Max(0, height - inset - block); y < height - inset; y++)
            {
                for (int offset = inset; offset < inset + block && offset < width / 2; offset++)
                {
                    AddSample(pixels[y * width + offset]);
                    AddSample(pixels[y * width + width - 1 - offset]);
                }
            }
            paper = Vector3.zero;
            if (red.Count < 16) return false;
            red.Sort(); green.Sort(); blue.Sort();
            paper = new Vector3(red[red.Count / 2], green[green.Count / 2], blue[blue.Count / 2]);
            return true;

            void AddSample(Color32 sample)
            {
                Vector3 color = Encoded(sample);
                if (sample.a < 250 || color.x < 0.58f || color.y < 0.45f || color.x - color.y < 0.035f || color.y - color.z < 0.075f) return;
                red.Add(color.x); green.Add(color.y); blue.Add(color.z);
            }
        }

        private static bool IsTwilightPalePaper(Vector3 color, Vector3 paper)
        {
            // A second, brighter paper population occurs only in Twilight 0/5.
            // Keep positive warm chroma: neutral silver hair has almost no GB
            // separation and therefore cannot become a background candidate.
            float redGreen = color.x - color.y;
            float greenBlue = color.y - color.z;
            return color.y >= 0.82f && color.z >= 0.68f && redGreen >= 0.035f && greenBlue >= 0.095f &&
                redGreen <= paper.x - paper.y + 0.025f && greenBlue <= paper.y - paper.z + 0.025f;
        }

        private static bool IsPaper(Vector3 color, Vector3 paper, bool seed)
        {
            // Compare warm-paper chroma, not luminance. In particular pale
            // fabric has a much smaller green-blue separation than yellow paper.
            if (color.x - color.y < 0.035f || color.y - color.z < 0.095f) return false;
            // The v2 atlas has deliberate pale paper around the silhouette.
            // Measured corner-to-halo RG/GB shifts reach 10-17 encoded bytes;
            // allow that variation only after the stricter edge seed test.
            if (Mathf.Abs((color.x - color.y) - (paper.x - paper.y)) > (seed ? 0.027f : 0.07f)) return false;
            if (Mathf.Abs((color.y - color.z) - (paper.y - paper.z)) > (seed ? 0.032f : 0.07f)) return false;
            return MaxAbs(color - paper) <= (seed ? 0.13f : 0.28f);
        }

        private static Vector3[] SmoothColors(Color32[] pixels, int width, int height)
        {
            Vector3[] result = new Vector3[pixels.Length];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    Vector3 total = Vector3.zero;
                    int samples = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            total += Encoded(pixels[Mathf.Clamp(y + dy, 0, height - 1) * width + Mathf.Clamp(x + dx, 0, width - 1)]);
                            samples++;
                        }
                    result[y * width + x] = total / samples;
                }
            return result;
        }

        private static int DistanceToBackground(int x, int y, int width, int height, bool[] background)
        {
            int best = 3;
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    int distance = Mathf.Abs(dx) + Mathf.Abs(dy);
                    if (distance == 0 || distance >= best || x + dx < 0 || x + dx >= width || y + dy < 0 || y + dy >= height) continue;
                    if (background[(y + dy) * width + x + dx]) best = distance;
                }
            return best;
        }

        private static Vector3 Encoded(Color32 color) => new Vector3(color.r / 255f, color.g / 255f, color.b / 255f);
        private static float MaxAbs(Vector3 value) => Mathf.Max(Mathf.Abs(value.x), Mathf.Max(Mathf.Abs(value.y), Mathf.Abs(value.z)));

        private void OnDisable() => ReleaseOwnedObjects();
        private void OnDestroy() => ReleaseOwnedObjects();

        private void ReleaseOwnedObjects()
        {
            if (target != null && ownedMaterial != null && target.material == ownedMaterial)
            {
                target.material = previousMaterial;
                target.SetMaterialDirty();
            }
            DestroyOwned(ownedMaterial);
            DestroyOwned(maskTexture);
            ownedMaterial = null;
            maskTexture = null;
            previousMaterial = null;
        }

        private static void DestroyOwned(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
