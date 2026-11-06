using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Core.Tests
{
    /// <summary>Stable design-space capture of the production comic overlay.</summary>
    internal static class ComicPlaybackCapture
    {
        private const int DesignWidth = 1920, DesignHeight = 1080, Layer = 31;

        public static void AssertNativeLayout(ComicPageGraphic page)
        {
            Assert.That(page, Is.Not.Null);
            Assert.That(page.texture, Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            Rect frame = ((RectTransform)page.transform.parent).rect;
            Rect painted = page.rectTransform.rect;
            float aspect = page.texture.width * page.uvRect.width / (page.texture.height * page.uvRect.height);
            Assert.That(painted.width / painted.height, Is.EqualTo(aspect).Within(.001f),
                "The production page must preserve its sampled texture aspect before capture.");
            float expectedWidth = Mathf.Min(frame.width, frame.height * aspect);
            float expectedHeight = Mathf.Min(frame.height, frame.width / aspect);
            // In single-frame EditMode the fitter may retain the exact 16:9 parent
            // while a 1672x940/941 native page differs by roughly one design pixel.
            // Keep the same two-design-pixel bound before and after projection.
            Assert.That(painted.width, Is.EqualTo(expectedWidth).Within(2f));
            Assert.That(painted.height, Is.EqualTo(expectedHeight).Within(2f));
        }

        public static void Capture(Canvas canvas, ComicPageGraphic page, string path)
        {
            AssertNativeLayout(page);
            int width = Environment.GetEnvironmentVariable("COMIC_CAPTURE_4K") == "1" ? 3840 : DesignWidth;
            int height = width * DesignHeight / DesignWidth;
            float scale = width / (float)DesignWidth;
            RectTransform rect = (RectTransform)canvas.transform;
            RenderMode originalMode = canvas.renderMode;
            Camera originalCamera = canvas.worldCamera;
            Vector2 originalSize = rect.sizeDelta;
            Vector3 originalPosition = rect.position, originalScale = rect.localScale;
            Quaternion originalRotation = rect.rotation;
            float originalPlaneDistance = canvas.planeDistance;
            Transform[] descendants = canvas.GetComponentsInChildren<Transform>(true);
            int[] layers = descendants.Select(item => item.gameObject.layer).ToArray();
            RenderTexture previous = RenderTexture.active;
            GameObject cameraHost = null;
            RenderTexture target = null;
            Texture2D readback = null;
            try
            {
                cameraHost = new GameObject("Comic Native Aspect Capture Camera", typeof(Camera));
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.orthographicSize = DesignHeight * .5f;
                camera.aspect = DesignWidth / (float)DesignHeight;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0, 0, -1000);
                camera.cullingMask = 1 << Layer;
                camera.allowHDR = camera.allowMSAA = false;
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                rect.sizeDelta = new Vector2(DesignWidth, DesignHeight);
                rect.position = Vector3.zero;
                rect.rotation = Quaternion.identity;
                rect.localScale = Vector3.one;
                foreach (Transform item in descendants) item.gameObject.layer = Layer;
                // Do not resize the page or force its fitter: exercise the production layout.
                for (int pass = 0; pass < 2; pass++)
                {
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                AssertNativeLayout(page);
                var corners = new Vector3[4];
                page.rectTransform.GetWorldCorners(corners);
                Vector3[] screen = corners.Select(camera.WorldToScreenPoint).ToArray();
                float screenWidth = screen.Max(point => point.x) - screen.Min(point => point.x);
                float screenHeight = screen.Max(point => point.y) - screen.Min(point => point.y);
                float aspect = page.texture.width * page.uvRect.width / (page.texture.height * page.uvRect.height);
                Assert.That(screenWidth / screenHeight, Is.EqualTo(aspect).Within(.001f),
                    "Camera projection must not squeeze the correctly fitted production page.");
                Assert.That(screenWidth, Is.EqualTo(Mathf.Min(width, height * aspect)).Within(2f * scale));
                Assert.That(screenHeight, Is.EqualTo(Mathf.Min(height, width / aspect)).Within(2f * scale));
                Assert.That((screen.Max(point => point.x) + screen.Min(point => point.x)) * .5f,
                    Is.EqualTo(width * .5f).Within(1f));
                Assert.That((screen.Max(point => point.y) + screen.Min(point => point.y)) * .5f,
                    Is.EqualTo(height * .5f).Within(1f));
                // The delivered panels differ by at most one native row; allow
                // their legitimate sub-two-design-pixel fit, never 4:3 pillars.
                if (Mathf.Abs(aspect - DesignWidth / (float)DesignHeight) < .002f)
                {
                    Assert.That(screenWidth / scale, Is.GreaterThanOrEqualTo(DesignWidth - 2f));
                    Assert.That(screenHeight / scale, Is.GreaterThanOrEqualTo(DesignHeight - 2f));
                }
                RenderTexture.active = target;
                readback = new Texture2D(width, height, TextureFormat.RGB24, false, false);
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readback.Apply();
                Color32[] pixels = readback.GetPixels32();
                int minimum = 255, maximum = 0;
                for (int index = 0; index < pixels.Length; index += 997)
                {
                    int brightness = (pixels[index].r + pixels[index].g + pixels[index].b) / 3;
                    minimum = Math.Min(minimum, brightness);
                    maximum = Math.Max(maximum, brightness);
                }
                Assert.That(maximum - minimum, Is.GreaterThan(24), "The captured UI cannot be an empty black frame.");
                File.WriteAllBytes(path, readback.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                for (int index = 0; index < descendants.Length; index++)
                    if (descendants[index] != null) descendants[index].gameObject.layer = layers[index];
                canvas.renderMode = originalMode;
                canvas.worldCamera = originalCamera;
                canvas.planeDistance = originalPlaneDistance;
                rect.sizeDelta = originalSize;
                rect.position = originalPosition;
                rect.rotation = originalRotation;
                rect.localScale = originalScale;
                Canvas.ForceUpdateCanvases();
                if (cameraHost != null) Object.DestroyImmediate(cameraHost);
                if (readback != null) Object.DestroyImmediate(readback);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }
    }
}
