using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class TavernPortraitEdgeSamplingTests
    {
        private const int Size = 128;
        private const int Layer = 30;
        private readonly List<Object> owned = new List<Object>();
        private Camera camera;
        private RenderTexture target;
        private RawImage portrait;
        private Material material;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Portrait edge sampling requires a graphics-enabled PlayMode run.");

            Shader shader = Resources.Load<Shader>(TavernPortraitPaperBlend.ShaderResourcePath);
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.isSupported, Is.True, "The portrait shader must compile on the active GPU.");
            material = Track(new Material(shader));
            material.SetFloat("_BottomFeather", 0f);

            GameObject cameraHost = Track(new GameObject("Portrait Edge Test Camera", typeof(Camera)));
            camera = cameraHost.GetComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = Size * 0.5f;
            camera.aspect = 1f;
            camera.transform.position = new Vector3(0f, 0f, -1000f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 2000f;
            camera.cullingMask = 1 << Layer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            // Synthetic source and target are linear data. The expected blend
            // equations therefore apply in both Gamma and Linear projects.
            target = Track(new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear));
            target.Create();
            camera.targetTexture = target;

            GameObject canvasHost = Track(new GameObject("Portrait Edge Test Canvas", typeof(RectTransform), typeof(Canvas)));
            canvasHost.layer = Layer;
            Canvas canvas = canvasHost.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.sortingOrder = 10000;
            canvasHost.GetComponent<RectTransform>().sizeDelta = Vector2.one * Size;
            GameObject imageHost = new GameObject("Synthetic Portrait", typeof(RectTransform), typeof(RawImage));
            imageHost.layer = Layer;
            RectTransform rect = imageHost.GetComponent<RectTransform>();
            rect.SetParent(canvasHost.transform, false);
            rect.sizeDelta = Vector2.one * Size;
            portrait = imageHost.GetComponent<RawImage>();
            portrait.material = material;
            portrait.color = Color.white;
            portrait.raycastTarget = false;
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
        public IEnumerator DarkEdgesAndWhiteHairDoNotBlendRemovedPaperBackIntoThePortrait()
        {
            Color32 paper = new Color32(244, 235, 210, 255);
            foreach (Color32 foreground in new[] { new Color32(31, 23, 15, 255), new Color32(239, 233, 214, 255) })
            {
                SetPortrait(Texture(2, 2, new[] { foreground, paper, foreground, paper }),
                    Mask(255, 0, 255, 0), new Rect(0f, 0f, 1f, 1f));
                yield return null;

                Texture2D frame = Capture();
                foreach (int x in new[] { 40, 63, 80 })
                {
                    float coverage = 1f - Mathf.Clamp01(PixelUv(x) * 2f - 0.5f);
                    AssertRgb(frame.GetPixel(x, 64), (Color)foreground * coverage,
                        "Fractional silhouette sample " + x + " must contain foreground RGB only.");
                }
                AssertRgb(frame.GetPixel(8, 64), foreground, "Opaque hair or dark ink remains unchanged.");
                AssertRgb(frame.GetPixel(119, 64), Color.black, "Removed paper reveals the background.");
            }
        }

        [UnityTest]
        public IEnumerator BothAxesFilterCoveredColorsAndAuthoredAlphaBeforeUiTint()
        {
            Color32 ink = new Color32(64, 32, 16, 255);
            Color32 cloth = new Color32(235, 229, 210, 128);
            Color32 paper = new Color32(250, 240, 214, 255);
            SetPortrait(Texture(2, 2, new[] { ink, paper, paper, cloth }), Mask(255, 0, 0, 200),
                new Rect(0f, 0f, 1f, 1f));
            portrait.color = new Color(1f, 1f, 1f, 0.6f);
            yield return null;

            Texture2D frame = Capture();
            const int x = 61;
            const int y = 69;
            float horizontal = PixelUv(x) * 2f - 0.5f;
            float vertical = PixelUv(y) * 2f - 0.5f;
            Color expected = ((Color)ink * ((1f - horizontal) * (1f - vertical)) +
                (Color)cloth * (horizontal * vertical * (200f / 255f) * (128f / 255f))) * 0.6f;
            AssertRgb(frame.GetPixel(x, y), expected,
                "All four taps must apply their own matte and original alpha before interpolation; UI alpha applies once.");
        }

        [UnityTest]
        public IEnumerator OpaqueClothRetainsAtlasDetailWhenCoverageHasFewerTexels()
        {
            Color32[] columns =
            {
                new Color32(255, 0, 255, 255), new Color32(255, 0, 255, 255),
                new Color32(240, 236, 225, 255), new Color32(35, 30, 26, 255),
                new Color32(165, 169, 172, 255), new Color32(222, 203, 178, 255),
                new Color32(255, 0, 255, 255), new Color32(255, 0, 255, 255),
            };
            Color32[] pixels = new Color32[8 * 4];
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 8; x++) pixels[y * 8 + x] = columns[x];
            SetPortrait(Texture(8, 4, pixels), Mask(255, 255, 255, 255), new Rect(0.25f, 0f, 0.5f, 1f));
            yield return null;

            Texture2D frame = Capture();
            foreach (int x in new[] { 16, 40, 63, 87, 111 })
            {
                float atlasPixel = Mathf.Clamp((0.25f + PixelUv(x) * 0.5f) * 8f - 0.5f, 2f, 5f);
                int first = Mathf.FloorToInt(atlasPixel);
                Color expected = Color.Lerp(columns[first], columns[Mathf.Min(first + 1, 5)], atlasPixel - first);
                AssertRgb(frame.GetPixel(x, 64), expected,
                    "Opaque source detail must not be resampled to the smaller coverage grid at " + x);
            }
        }

        [UnityTest]
        public IEnumerator CellEdgesClampWithinTheSelectedPortraitIncludingReversedUvs()
        {
            Color32 adjacentCell = new Color32(255, 0, 255, 255);
            Color32 foreground = new Color32(45, 72, 91, 255);
            Color32[] pixels = new Color32[6 * 4];
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 6; x++)
                    pixels[y * 6 + x] = x >= 2 && x < 4 && y >= 2 ? foreground : adjacentCell;
            Texture2D source = Texture(6, 4, pixels);
            foreach (Rect cell in new[] { new Rect(1f / 3f, 0.5f, 1f / 3f, 0.5f),
                new Rect(2f / 3f, 1f, -1f / 3f, -0.5f) })
            {
                SetPortrait(source, Mask(255, 255, 255, 255), cell);
                yield return null;
                Texture2D frame = Capture();
                foreach (int y in new[] { 0, 64, 127 })
                    foreach (int x in new[] { 0, 64, 127 })
                        AssertRgb(frame.GetPixel(x, y), foreground, "A selected-cell boundary must not borrow adjacent atlas RGB.");
            }
        }

        private void SetPortrait(Texture2D source, Texture2D mask, Rect cell)
        {
            portrait.texture = source;
            portrait.uvRect = cell;
            material.SetTexture("_PaperMask", mask);
            material.SetVector("_MaskUv", new Vector4(1f / cell.width, 1f / cell.height,
                -cell.x / cell.width, -cell.y / cell.height));
        }

        private Texture2D Mask(byte lowerLeft, byte lowerRight, byte upperLeft, byte upperRight)
        {
            return Texture(2, 2, new[] { new Color32(lowerLeft, lowerLeft, lowerLeft, 255),
                new Color32(lowerRight, lowerRight, lowerRight, 255), new Color32(upperLeft, upperLeft, upperLeft, 255),
                new Color32(upperRight, upperRight, upperRight, 255) });
        }

        private Texture2D Texture(int width, int height, Color32[] pixels)
        {
            Texture2D texture = Track(new Texture2D(width, height, TextureFormat.RGBA32, false, true));
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private Texture2D Capture()
        {
            Canvas.ForceUpdateCanvases();
            if (GraphicsSettings.currentRenderPipeline != null)
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            else camera.Render();
            RenderTexture previous = RenderTexture.active;
            Texture2D frame = Track(new Texture2D(Size, Size, TextureFormat.RGBA32, false, true));
            try
            {
                RenderTexture.active = target;
                frame.ReadPixels(new Rect(0f, 0f, Size, Size), 0, 0);
                frame.Apply(false, false);
            }
            finally
            {
                RenderTexture.active = previous;
            }
            return frame;
        }

        private static float PixelUv(int coordinate) => (coordinate + 0.5f) / Size;

        private static void AssertRgb(Color actual, Color expected, string context)
        {
            Assert.That(Mathf.Max(Mathf.Abs(actual.r - expected.r), Mathf.Abs(actual.g - expected.g),
                Mathf.Abs(actual.b - expected.b)), Is.LessThan(0.018f), context + " Expected=" + expected + "; actual=" + actual);
        }

        private T Track<T>(T value) where T : Object
        {
            owned.Add(value);
            return value;
        }
    }
}
