using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Core.Tests
{
    public sealed class VoyageMapBlendGraphicTests
    {
        private readonly List<Object> temporaryObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = temporaryObjects.Count - 1; index >= 0; index -= 1)
            {
                if (temporaryObjects[index] != null)
                    Object.DestroyImmediate(temporaryObjects[index]);
            }
            temporaryObjects.Clear();
        }

        [Test]
        public void IdenticalSeedAndConfiguration_RebuildExactlyTheSameMesh()
        {
            VoyageMapBlendGraphic graphic = CreateBlend(new Vector2(640f, 360f));
            graphic.color = new Color(0.7f, 0.8f, 0.9f, 0.65f);
            graphic.Configure(0.16f, 0xAB12CD34u);
            Mesh first = Populate(graphic);
            graphic.Configure(0.16f, 0xAB12CD34u);
            Mesh second = Populate(graphic);

            CollectionAssert.AreEqual(first.vertices, second.vertices);
            CollectionAssert.AreEqual(first.uv, second.uv);
            CollectionAssert.AreEqual(first.colors32, second.colors32);
            CollectionAssert.AreEqual(first.triangles, second.triangles);

            graphic.Configure(0.16f, 0x389E56A1u);
            Mesh otherSeed = Populate(graphic);
            CollectionAssert.AreEqual(first.vertices, otherSeed.vertices,
                "The seed changes edge softness, not the patch's placement or UV geometry.");
            CollectionAssert.AreNotEqual(first.colors32, otherSeed.colors32,
                "Different deterministic seeds should break up repeated patch boundaries.");
        }

        [TestCase(420f, 420f)]
        [TestCase(840f, 130f)]
        [TestCase(95f, 760f)]
        public void EveryBoundaryVertex_HasExactlyZeroAlpha(float width, float height)
        {
            VoyageMapBlendGraphic graphic = CreateBlend(new Vector2(width, height));
            graphic.Configure(0.16f, 0xFA35D90Cu);
            Mesh mesh = Populate(graphic);
            Rect bounds = graphic.rectTransform.rect;
            Vector3[] vertices = mesh.vertices;
            Color32[] colors = mesh.colors32;
            int boundaryCount = 0;
            int partiallyTransparentCount = 0;

            for (int index = 0; index < vertices.Length; index += 1)
            {
                Vector3 point = vertices[index];
                bool boundary = Mathf.Approximately(point.x, bounds.xMin)
                    || Mathf.Approximately(point.x, bounds.xMax)
                    || Mathf.Approximately(point.y, bounds.yMin)
                    || Mathf.Approximately(point.y, bounds.yMax);
                if (boundary)
                {
                    boundaryCount += 1;
                    Assert.That(colors[index].a, Is.EqualTo(0), "Boundary vertex " + index);
                }
                else if (colors[index].a > 0 && colors[index].a < 255)
                {
                    partiallyTransparentCount += 1;
                }
            }

            Assert.That(boundaryCount, Is.GreaterThan(16));
            Assert.That(partiallyTransparentCount, Is.GreaterThan(16),
                "A zero-alpha rim alone is insufficient: the transition needs intermediate samples.");
        }

        [TestCase(0.16f)]
        [TestCase(0.35f)]
        public void CentralArea_PreservesTheNativeTintAlphaAndRgb(float feather)
        {
            VoyageMapBlendGraphic graphic = CreateBlend(new Vector2(480f, 480f));
            graphic.color = new Color32(103, 177, 221, 153);
            graphic.Configure(feather, 0x12345678u, 24);
            Mesh mesh = Populate(graphic);
            Vector3[] vertices = mesh.vertices;
            Color32[] colors = mesh.colors32;
            Color32 expected = graphic.color;
            int centre = -1;

            for (int index = 0; index < vertices.Length; index += 1)
            {
                Assert.That(colors[index].r, Is.EqualTo(expected.r));
                Assert.That(colors[index].g, Is.EqualTo(expected.g));
                Assert.That(colors[index].b, Is.EqualTo(expected.b));
                Assert.That(colors[index].a, Is.LessThanOrEqualTo(expected.a));
                if (vertices[index].sqrMagnitude < 0.0001f)
                    centre = index;
            }

            Assert.That(centre, Is.GreaterThanOrEqualTo(0));
            Assert.That(colors[centre].a, Is.EqualTo(expected.a),
                "Do not impose a radial vignette over the actual illustration.");
        }

        [Test]
        public void NegativeUvWidthAndHeight_FlipTheTextureWithoutChangingItsGeometry()
        {
            VoyageMapBlendGraphic graphic = CreateBlend(new Vector2(640f, 360f));
            graphic.uvRect = new Rect(0.8f, 0.7f, -0.6f, -0.5f);
            graphic.Configure(0.16f, 78u);
            Mesh mesh = Populate(graphic);
            Vector3[] positions = mesh.vertices;
            Vector2[] uv = mesh.uv;
            Rect bounds = graphic.rectTransform.rect;

            for (int index = 0; index < positions.Length; index += 1)
            {
                float u = (positions[index].x - bounds.xMin) / bounds.width;
                float v = (positions[index].y - bounds.yMin) / bounds.height;
                Assert.That(uv[index].x, Is.EqualTo(0.8f - u * 0.6f).Within(0.000001f));
                Assert.That(uv[index].y, Is.EqualTo(0.7f - v * 0.5f).Within(0.000001f));
            }
        }

        [TestCase(100000f, 1f, 24)]
        [TestCase(1f, 100000f, 24)]
        [TestCase(1920f, 1080f, 250)]
        [TestCase(100000f, 1f, int.MaxValue)]
        public void ExtremeAspectRatiosAndResolution_StayBelowUguiVertexLimit(
            float width, float height, int resolution)
        {
            VoyageMapBlendGraphic graphic = CreateBlend(new Vector2(width, height));
            graphic.Configure(0.16f, 0u, resolution);
            Mesh mesh = Populate(graphic);

            Assert.That(mesh.vertexCount, Is.GreaterThan(0));
            Assert.That(mesh.vertexCount, Is.LessThan(65000));
            int[] indices = mesh.triangles;
            Assert.That(indices.Length, Is.GreaterThan(0));
            Assert.That(indices.Length % 3, Is.EqualTo(0));
            int vertexCount = mesh.vertexCount;
            for (int index = 0; index < indices.Length; index += 1)
            {
                if (indices[index] < 0 || indices[index] >= vertexCount)
                    Assert.Fail("Triangle index " + index + " lies outside the mesh vertex range.");
            }
        }

        [Test]
        public void CoastSurface_Destroy_ReleasesOnlyItsOwnedTextureAndMaterial()
        {
            VoyageMapArtCatalog art = new VoyageMapArtCatalog();
            Texture2D land = art.GroundDetail;
            Texture2D water = art.ChartBase;
            Shader shader = Resources.Load<Shader>("VoyageMapV2/VoyageChartCoast");
            Assert.That(land, Is.Not.Null);
            Assert.That(water, Is.Not.Null);
            Assert.That(shader, Is.Not.Null,
                "Check the imported shader asset, not GPU support, so batchmode remains valid.");

            VoyageChartSurfaceGraphic graphic = CreateSurface();
            graphic.Initialize(CreateMap(59u), art, 0);
            Texture texture = graphic.texture;
            Material material = graphic.material;
            Assert.That(texture, Is.Not.Null);
            Assert.That(material, Is.Not.Null);
            Assert.That(texture, Is.Not.SameAs(land));
            Assert.That(material.shader, Is.SameAs(shader));
            Assert.That(material.GetTexture("_LandArt"), Is.SameAs(land));

            Object.DestroyImmediate(graphic.gameObject);

            Assert.That(texture == null, Is.True, "The generated coast-data texture must not leak.");
            Assert.That(material == null, Is.True, "The generated coast material must not leak.");
            Assert.That(land != null, Is.True, "The atlas belongs to Resources, not this graphic.");
            Assert.That(water != null, Is.True);
            Assert.That(shader != null, Is.True);
            Assert.That(Resources.Load<Texture2D>(VoyageMapArtCatalog.ResourceRoot + "terrain_ground_detail_v3"),
                Is.SameAs(land));
            Assert.That(Resources.Load<Shader>("VoyageMapV2/VoyageChartCoast"), Is.SameAs(shader));
        }

        [Test]
        public void CoastSurface_Reinitialize_ReleasesThePreviousOwnedResources()
        {
            VoyageMapArtCatalog art = new VoyageMapArtCatalog();
            VoyageChartSurfaceGraphic graphic = CreateSurface();
            graphic.Initialize(CreateMap(69u), art, 0);
            Texture previousTexture = graphic.texture;
            Material previousMaterial = graphic.material;
            graphic.Initialize(CreateMap(70u), art, 1);

            Assert.That(previousTexture == null, Is.True);
            Assert.That(previousMaterial == null, Is.True);
            Assert.That(graphic.texture, Is.Not.Null);
            Assert.That(graphic.material, Is.Not.Null);
            Assert.That(graphic.Field, Is.Not.Null);
            Assert.That(art.TerrainFor(SeaTerrainType.Island) != null, Is.True);
        }

        private VoyageMapBlendGraphic CreateBlend(Vector2 size)
        {
            GameObject host = new GameObject("Voyage Blend Mesh Test", typeof(RectTransform),
                typeof(VoyageMapBlendGraphic));
            temporaryObjects.Add(host);
            VoyageMapBlendGraphic graphic = host.GetComponent<VoyageMapBlendGraphic>();
            graphic.rectTransform.sizeDelta = size;
            graphic.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            return graphic;
        }

        private VoyageChartSurfaceGraphic CreateSurface()
        {
            GameObject host = new GameObject("Voyage Coast Ownership Test", typeof(RectTransform),
                typeof(VoyageChartSurfaceGraphic));
            temporaryObjects.Add(host);
            return host.GetComponent<VoyageChartSurfaceGraphic>();
        }

        private Mesh Populate(VoyageMapBlendGraphic graphic)
        {
            MethodInfo method = typeof(VoyageMapBlendGraphic).GetMethod("OnPopulateMesh",
                BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(VertexHelper) }, null);
            Assert.That(method, Is.Not.Null);
            Mesh mesh = new Mesh { name = "Temporary Voyage Blend Mesh" };
            temporaryObjects.Add(mesh);
            using (VertexHelper helper = new VertexHelper())
            {
                method.Invoke(graphic, new object[] { helper });
                helper.FillMesh(mesh);
            }
            return mesh;
        }

        private static CoreVoyageMap CreateMap(uint hash)
        {
            CoreVoyageMap map = new CoreVoyageMap { Hash = hash, Seed = "GRAPHIC-OWNERSHIP-" + hash };
            map.TerrainCells[12 * CoreVoyageMap.GridColumns + 18] = SeaTerrainType.Island;
            return map;
        }
    }
}
