using System.Collections;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class BattleShipCutoutRenderTests
    {
        [UnityTest]
        public IEnumerator AllRosterCells_KeepTheirActualEdgesTransparentThroughMirroredTurns()
        {
            PortArtCatalog catalog = new PortArtCatalog();
            control.gameObject.SetActive(false);
            cutout.rectTransform.anchoredPosition = Vector2.zero;
            cutout.rectTransform.sizeDelta = new Vector2(280f, 280f);
            Vector2[] edges =
            {
                new Vector2(0.002f, 0.25f), new Vector2(0.002f, 0.5f), new Vector2(0.002f, 0.75f),
                new Vector2(0.998f, 0.25f), new Vector2(0.998f, 0.5f), new Vector2(0.998f, 0.75f),
                new Vector2(0.25f, 0.002f), new Vector2(0.5f, 0.002f), new Vector2(0.75f, 0.002f),
                new Vector2(0.25f, 0.998f), new Vector2(0.5f, 0.998f), new Vector2(0.75f, 0.998f),
            };
            for (int ship = 0; ship < CoreVoyageSession.Ships.Count; ship++)
            {
                cutout.texture = catalog.ShipTexture(ship);
                Assert.That(cutout.texture, Is.Not.Null, "Ship art " + ship);
                foreach (bool mirror in new[] { false, true })
                {
                    // Deliberately do not inset: the departure/player preview
                    // uses the complete atlas cell, unlike the enemy boss.
                    Rect uv = catalog.ShipUv(ship);
                    cutout.uvRect = mirror ? new Rect(uv.xMax, uv.y, -uv.width, uv.height) : uv;
                    foreach (float yaw in new[] { 0f, 55f, 125f, 180f })
                    {
                        cutout.rectTransform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                        yield return null;
                        Texture2D frame = Capture();
                        Color background = frame.GetPixel(8, 8);
                        foreach (Vector2 edge in edges)
                        {
                            Color pixel = SampleTurned(frame, cutout.rectTransform, edge);
                            Assert.That(Distance(pixel, background), Is.LessThan(0.035f),
                                "Atlas frame must not become a black border; ship=" + ship + ", mirror=" + mirror +
                                ", yaw=" + yaw + ", edge=" + edge + ", pixel=" + pixel);
                        }
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator BilinearSilhouette_IsKeyedBeforeFilteringSoTurnsDoNotGrowDarkHalos()
        {
            Texture2D navySource = Track(new Texture2D(8, 8, TextureFormat.RGBA32, false, false));
            Texture2D transparentReference = Track(new Texture2D(8, 8, TextureFormat.RGBA32, false, false));
            navySource.filterMode = transparentReference.filterMode = FilterMode.Bilinear;
            navySource.wrapMode = transparentReference.wrapMode = TextureWrapMode.Clamp;
            Color32[] navy = new Color32[64];
            Color32[] reference = new Color32[64];
            Color32 timber = new Color32(190, 115, 55, 255);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    navy[y * 8 + x] = x < 4 ? new Color32(1, 13, 32, 255) : x < 6 ? timber : new Color32(1, 1, 1, 255);
                    reference[y * 8 + x] = x < 4 ? new Color32(timber.r, timber.g, timber.b, 0) : navy[y * 8 + x];
                }
            navySource.SetPixels32(navy);
            transparentReference.SetPixels32(reference);
            navySource.Apply(false, true);
            transparentReference.Apply(false, true);
            cutout.texture = navySource;
            control.texture = transparentReference;
            foreach (bool mirror in new[] { false, true })
            {
                cutout.uvRect = control.uvRect = mirror ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 0f, 1f, 1f);
                foreach (float yaw in new[] { 0f, 55f, 180f })
                {
                    cutout.rectTransform.localRotation = control.rectTransform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    yield return null;
                    Texture2D frame = Capture();
                    foreach (float u in new[] { 0.46f, 0.49f, 0.53f, 0.90f })
                    {
                        Vector2 point = new Vector2(mirror ? 1f - u : u, 0.5f);
                        Color expected = SampleTurned(frame, control.rectTransform, point);
                        Color actual = SampleTurned(frame, cutout.rectTransform, point);
                        Assert.That(Distance(actual, expected), Is.LessThan(0.035f),
                            "Filtered cutout must match a genuinely transparent silhouette, including opaque black detail; " +
                            "mirror=" + mirror + ", yaw=" + yaw + ", u=" + u + ", actual=" + actual + ", expected=" + expected);
                    }
                }
            }
        }

        private Color SampleTurned(Texture2D frame, RectTransform rect, Vector2 normalized)
        {
            Vector3 local = new Vector3((normalized.x - 0.5f) * rect.rect.width,
                (normalized.y - 0.5f) * rect.rect.height, 0f);
            Vector3 screen = camera.WorldToScreenPoint(rect.TransformPoint(local));
            return frame.GetPixel(Mathf.FloorToInt(screen.x), Mathf.FloorToInt(screen.y));
        }
    }
}
