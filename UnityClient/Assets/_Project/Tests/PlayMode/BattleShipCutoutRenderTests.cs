using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    /// <summary>
    /// Real GPU checks, not just component/property checks. Each frame contains
    /// the unmodified source on the left and the keyed source on the right, so
    /// comparison remains valid in both Gamma and Linear project color spaces.
    /// The imported atlas test exercises the platform's actual compression.
    /// </summary>
    public sealed partial class BattleShipCutoutRenderTests
    {
        private const int Width = 512;
        private const int Height = 320;
        private const int Layer = 30;
        private const float ShipSize = 224f;
        private readonly List<Object> owned = new List<Object>();
        private Camera camera;
        private RenderTexture target;
        private RawImage control;
        private RawImage cutout;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("This test requires a GPU; run the graphics-enabled PlayMode suite.");

            GameObject cameraHost = Track(new GameObject("Cutout GPU Test Camera", typeof(Camera)));
            camera = cameraHost.GetComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = Height * 0.5f;
            camera.aspect = Width / (float)Height;
            camera.transform.position = new Vector3(0f, 0f, -1000f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 2000f;
            camera.cullingMask = 1 << Layer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.17f, 0.47f, 0.29f, 1f);
            camera.allowHDR = false;
            camera.allowMSAA = false;
            target = Track(new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB));
            target.Create();
            camera.targetTexture = target;

            GameObject canvasHost = Track(new GameObject("Cutout GPU Test Canvas", typeof(RectTransform), typeof(Canvas)));
            canvasHost.layer = Layer;
            Canvas canvas = canvasHost.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.sortingOrder = 10000;
            canvasHost.GetComponent<RectTransform>().sizeDelta = new Vector2(Width, Height);
            control = CreateImage("Unmodified Atlas Control", canvasHost.transform, -128f);
            cutout = CreateImage("Keyed Atlas Test", canvasHost.transform, 128f);
            BattleShipCutout.Attach(cutout);
            Assert.That(cutout.material.shader.isSupported, Is.True, "The cutout shader must compile on the active graphics device.");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (camera != null) camera.targetTexture = null;
            if (target != null) target.Release();
            for (int index = owned.Count - 1; index >= 0; index--)
                if (owned[index] != null) Object.Destroy(owned[index]);
            owned.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator UncompressedSrgbAtlas_RemovesNavyButPreservesBlackPurpleAndTimber()
        {
            Texture2D source = Track(new Texture2D(16, 16, TextureFormat.RGBA32, false, false));
            source.filterMode = FilterMode.Point;
            source.wrapMode = TextureWrapMode.Clamp;
            Color32[] colors =
            {
                new Color32(1, 13, 32, 255),
                new Color32(1, 1, 1, 255),
                new Color32(32, 12, 46, 255),
                new Color32(95, 51, 27, 255),
            };
            Color32[] pixels = new Color32[16 * 16];
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    pixels[y * 16 + x] = colors[x / 4];
            source.SetPixels32(pixels);
            source.Apply(false, true);
            control.texture = source;
            cutout.texture = source;
            yield return null;

            Texture2D frame = Capture();
            Color uncovered = frame.GetPixel(8, Height / 2);
            Assert.That(Distance(Sample(frame, true, 0.125f, 0.5f), uncovered), Is.LessThan(0.025f),
                "Known navy must reveal the nonblack camera background, including in Linear projects.");
            for (int stripe = 1; stripe < 4; stripe++)
            {
                float x = (stripe + 0.5f) * 0.25f;
                Color before = Sample(frame, false, x, 0.5f);
                Color after = Sample(frame, true, x, 0.5f);
                Assert.That(Distance(after, before), Is.LessThan(0.025f), "Foreground stripe " + stripe + " must remain unchanged.");
                Assert.That(Distance(after, uncovered), Is.GreaterThan(0.15f), "Dark foreground must not become a transparent hole.");
            }
            Color blackSail = Sample(frame, true, 0.375f, 0.5f);
            Assert.That(Mathf.Max(blackSail.r, blackSail.g, blackSail.b), Is.LessThan(0.12f),
                "The black sail must still render as opaque black, not blend with the camera's green background.");
        }

        [UnityTest]
        public IEnumerator BothImportedAtlases_AllTwelveCellsRemoveInteriorMatteAndPreserveForeground()
        {
            // Enumerate physical atlas cells, not ship IDs: some new ships share
            // art, so twelve ship IDs would omit other cells in the 4x2 sheet.
            Texture2D[] atlases = { new PortArtCatalog().ShipSheet,
                Resources.Load<Texture2D>("LegacyDemo/ship_sprite_sheet") };
            for (int atlasIndex = 0; atlasIndex < atlases.Length; atlasIndex++)
            {
                Texture2D atlas = atlases[atlasIndex];
                Assert.That(atlas, Is.Not.Null);
                int columns = atlasIndex == 0 ? 2 : 4;
                control.texture = cutout.texture = atlas;
                string context = "Atlas=" + atlas.name + ", format=" + atlas.format + ", color space=" +
                    QualitySettings.activeColorSpace + ", graphics=" + SystemInfo.graphicsDeviceType;
                TestContext.Progress.WriteLine(context);
                for (int cellIndex = 0; cellIndex < columns * 2; cellIndex++)
                {
                Rect cell = new Rect((cellIndex % columns) / (float)columns,
                    (1 - cellIndex / columns) * 0.5f, 1f / columns, 0.5f);
                float insetX = 3f / atlas.width;
                float insetY = 3f / atlas.height;
                Rect uv = new Rect(cell.xMin + insetX, cell.yMin + insetY,
                    cell.width - 2f * insetX, cell.height - 2f * insetY);
                control.uvRect = uv;
                cutout.uvRect = uv;
                yield return null;
                Texture2D frame = Capture();
                Color uncovered = frame.GetPixel(8, Height / 2);
                Color atlasNavy = Sample(frame, false, 0.05f, 0.95f);
                string detail = "Cell " + cellIndex + "; " + context + "; sampled navy=" + atlasNavy;
                foreach (Vector2 point in new[] { new Vector2(0.05f, 0.95f), new Vector2(0.95f, 0.95f),
                    new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.05f) })
                {
                    Color after = Sample(frame, true, point.x, point.y);
                    Assert.That(Distance(after, uncovered), Is.LessThan(0.035f),
                        "Actual imported/compressed navy must not leave a rectangle. " + detail + "; at " + point + "; rendered=" + after);
                }

                // Separator-only assertions can pass while an opaque matte
                // still fills the cell. Probe navy throughout its interior,
                // using the unmodified GPU control as the independent mask.
                int interiorBackgroundChecks = 0;
                for (int y = 1; y < 10; y++)
                    for (int x = 1; x < 10; x++)
                    {
                        Color before = Sample(frame, false, x * 0.1f, y * 0.1f);
                        // A single near-navy texel can be an antialiased rope
                        // or sail edge (e.g. legacy cell 1 at 0.3,0.6). Demand
                        // a flat 5x5 control patch before calling it pure matte.
                        if (!IsFlatMattePatch(frame, x * 0.1f, y * 0.1f, atlasNavy)) continue;
                        Color after = Sample(frame, true, x * 0.1f, y * 0.1f);
                        Assert.That(Distance(after, uncovered), Is.LessThan(0.035f),
                            "Opaque interior matte survived despite clear cell edges. " + detail + "; at " + x + "," + y +
                            "; control=" + before + "; keyed=" + after);
                        interiorBackgroundChecks++;
                    }
                Assert.That(interiorBackgroundChecks, Is.GreaterThanOrEqualTo(10),
                    "Each physical cell must exercise a substantial interior matte region. " + detail);

                int foregroundChecks = 0;
                foreach (float y in new[] { 0.35f, 0.50f, 0.65f })
                {
                    foreach (float x in new[] { 0.40f, 0.50f, 0.60f })
                    {
                        Color before = Sample(frame, false, x, y);
                        // Skip genuine gaps between sails. The same atlas is
                        // rendered twice, with no CPU readback/decompression of
                        // its imported source and no guessed foreground color.
                        if (Distance(before, atlasNavy) < 0.06f) continue;
                        Color after = Sample(frame, true, x, y);
                        Assert.That(Distance(after, before), Is.LessThan(0.04f),
                            "A sail or hull pixel changed while removing the background. " + detail + "; at " + x + "," + y);
                        foregroundChecks++;
                    }
                }
                Assert.That(foregroundChecks, Is.GreaterThanOrEqualTo(3), "The GPU image must contain actual ship foreground. " + detail);
                }
            }
        }

        private RawImage CreateImage(string name, Transform parent, float x)
        {
            GameObject host = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            host.layer = Layer;
            RectTransform rect = host.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = Vector2.one * ShipSize;
            rect.anchoredPosition = new Vector2(x, 0f);
            RawImage image = host.GetComponent<RawImage>();
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        private Texture2D Capture()
        {
            Canvas.ForceUpdateCanvases();
            if (GraphicsSettings.currentRenderPipeline != null)
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            else camera.Render();

            RenderTexture previous = RenderTexture.active;
            Texture2D result = Track(new Texture2D(Width, Height, TextureFormat.RGBA32, false, false));
            try
            {
                RenderTexture.active = target;
                result.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                result.Apply(false, false);
            }
            finally
            {
                RenderTexture.active = previous;
            }
            return result;
        }

        private static Color Sample(Texture2D frame, bool keyed, float x, float y)
        {
            float centerX = Width * 0.5f + (keyed ? 128f : -128f);
            int pixelX = Mathf.RoundToInt(centerX + (x - 0.5f) * ShipSize);
            int pixelY = Mathf.RoundToInt(Height * 0.5f + (y - 0.5f) * ShipSize);
            return frame.GetPixel(pixelX, pixelY);
        }

        private static bool IsFlatMattePatch(Texture2D frame, float x, float y, Color navy)
        {
            int pixelX = Mathf.RoundToInt(Width * 0.5f - 128f + (x - 0.5f) * ShipSize);
            int pixelY = Mathf.RoundToInt(Height * 0.5f + (y - 0.5f) * ShipSize);
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                    if (Distance(frame.GetPixel(pixelX + dx, pixelY + dy), navy) > 0.008f) return false;
            return true;
        }

        private static float Distance(Color first, Color second)
        {
            return Mathf.Max(Mathf.Abs(first.r - second.r), Mathf.Abs(first.g - second.g), Mathf.Abs(first.b - second.b));
        }

        private T Track<T>(T value) where T : Object
        {
            owned.Add(value);
            return value;
        }
    }
}
