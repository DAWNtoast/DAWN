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
    public sealed class VoyageMapQuadBatchTests
    {
        private const float Width = 1160f;
        private const float Height = 630f;
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        private readonly List<Mesh> meshes = new List<Mesh>();
        private GameObject host;
        private CoreGameplayDemoController controller;
        private CoreVoyageMap map;
        private RectTransform chart;
        private float CellWidth => Width / CoreVoyageMap.GridColumns;
        private float CellHeight => Height / CoreVoyageMap.GridRows;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Chart Quad Batch Test");
            host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            CoreVoyageSession session = new CoreVoyageSession();
            map = new CoreVoyageMap { Hash = 0xA321B7u, Seed = "QUAD-BATCH-REGRESSION" };
            typeof(CoreVoyageSession).GetProperty("Map").GetSetMethod(true)
                .Invoke(session, new object[] { map });
            typeof(CoreGameplayDemoController).GetField("session", PrivateInstance).SetValue(controller, session);
            chart = new GameObject("Chart", typeof(RectTransform)).GetComponent<RectTransform>();
            chart.SetParent(host.transform, false);
            chart.sizeDelta = new Vector2(Width, Height);
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            foreach (Mesh mesh in meshes) Object.DestroyImmediate(mesh);
            meshes.Clear();
        }

        [Test]
        public void CompleteChart_FogMatchesEveryCellAndKeepsFrontierAboveTheMask()
        {
            for (int index = 0; index < map.CellCount; index += 1)
            {
                map.ExploredCells[index] = index % 5 == 0;
                map.TerrainVariants[index] = (byte)(index % 7);
            }
            Invoke("BuildVoyageTerrainLayers", chart);
            RectTransform visibility = chart.Find("Visibility Mask Layer").GetComponent<RectTransform>();
            VoyageMapTileLayerGraphic fog = visibility.Find("Chart Fog Tiles").GetComponent<VoyageMapTileLayerGraphic>();
            Assert.That(visibility.childCount, Is.EqualTo(2), "One fog mesh and one frontier mesh replace the per-cell UI objects.");
            Assert.That(visibility.GetChild(1).name, Is.EqualTo("Chart Fog Frontier"));
            Assert.That(fog.GetComponent<CanvasRenderer>(), Is.Not.Null,
                "Custom UGUI graphics need their own CanvasRenderer requirement to be visible.");
            Assert.That(fog.raycastTarget, Is.False);
            Assert.That(fog.TileCount, Is.EqualTo(1536));
            Mesh mesh = Populate(fog);
            Vector3[] vertices = mesh.vertices;
            Color32[] colors = mesh.colors32;
            Assert.That(vertices.Length, Is.EqualTo(6144));
            Assert.That(mesh.triangles.Length, Is.EqualTo(9216));
            for (int index = 0; index < map.CellCount; index += 1)
            {
                int column = index % CoreVoyageMap.GridColumns;
                int row = index / CoreVoyageMap.GridColumns;
                uint hash = VoyageMapArtCatalog.VisualHash(map.Hash, column, row,
                    (uint)map.TerrainVariants[index] * 0x27D4EB2Du);
                float alpha = map.ExploredCells[index] ? .025f : .965f + ((hash >> 24) & 15u) / 600f;
                AssertQuad(vertices, colors, index, Center(column, row),
                    new Vector2(CellWidth + .7f, CellHeight + .7f), new Color(6f / 255f, 18f / 255f, 24f / 255f, alpha));
            }
        }

        [Test]
        public void Grid_PreservesAllMeridiansParallelsAndTheirMajorLineWeights()
        {
            Invoke("BuildChartGrid", chart, CellWidth, CellHeight);
            VoyageMapTileLayerGraphic lines = chart.GetComponentInChildren<VoyageMapTileLayerGraphic>(true);
            Assert.That(chart.childCount, Is.EqualTo(1));
            Assert.That(lines.TileCount, Is.EqualTo(82));
            Assert.That(lines.raycastTarget, Is.False);
            Mesh mesh = Populate(lines);
            Vector3[] vertices = mesh.vertices;
            Color32[] colors = mesh.colors32;
            Color minor = new Color(.58f, .78f, .78f, .018f);
            Color major = new Color(.72f, .87f, .82f, .044f);
            for (int column = 0; column <= CoreVoyageMap.GridColumns; column += 1)
            {
                bool isMajor = column % 8 == 0;
                AssertQuad(vertices, colors, column, new Vector2(-580f + column * CellWidth, 0f),
                    new Vector2(isMajor ? 1.15f : .55f, Height), isMajor ? major : minor);
            }
            for (int row = 0; row <= CoreVoyageMap.GridRows; row += 1)
            {
                bool isMajor = row % 4 == 0;
                AssertQuad(vertices, colors, 49 + row, new Vector2(0f, -315f + row * CellHeight),
                    new Vector2(Width, isMajor ? 1.15f : .55f), isMajor ? major : minor);
            }
        }

        [Test]
        public void Frontier_OnlyOutlinesUnexploredNeighboursAndPreservesCornerOverlapOrder()
        {
            map.ExploredCells[0] = true;
            map.ExploredCells[10 * 48 + 10] = true;
            map.ExploredCells[10 * 48 + 11] = true;
            Invoke("BuildVisibilityFrontier", chart, CellWidth, CellHeight);
            VoyageMapTileLayerGraphic frontier = chart.GetComponentInChildren<VoyageMapTileLayerGraphic>(true);
            Assert.That(frontier.TileCount, Is.EqualTo(8),
                "Do not draw at the chart edge or between two explored cells.");
            Mesh mesh = Populate(frontier);
            Vector3[] vertices = mesh.vertices;
            Color32[] colors = mesh.colors32;
            Color color = new Color(.3f, .54f, .57f, .22f);
            Vector2 horizontal = new Vector2(CellWidth, 2.8f);
            Vector2 vertical = new Vector2(2.8f, CellHeight);
            Vector2 north = new Vector2(0f, CellHeight * .5f);
            Vector2 east = new Vector2(CellWidth * .5f, 0f);
            AssertQuad(vertices, colors, 0, Center(0, 0) + north, horizontal, color);
            AssertQuad(vertices, colors, 1, Center(0, 0) + east, vertical, color);
            AssertQuad(vertices, colors, 2, Center(10, 10) + north, horizontal, color);
            AssertQuad(vertices, colors, 3, Center(10, 10) - north, horizontal, color);
            AssertQuad(vertices, colors, 4, Center(10, 10) - east, vertical, color);
            AssertQuad(vertices, colors, 5, Center(11, 10) + north, horizontal, color);
            AssertQuad(vertices, colors, 6, Center(11, 10) + east, vertical, color);
            AssertQuad(vertices, colors, 7, Center(11, 10) - north, horizontal, color);
        }

        [Test]
        public void CheckerboardExploration_WorstCaseFrontierStaysWithinUguiMeshLimit()
        {
            for (int row = 0; row < 32; row += 1)
            for (int column = 0; column < 48; column += 1)
                map.ExploredCells[row * 48 + column] = (row + column) % 2 == 0;
            Invoke("BuildVisibilityFrontier", chart, CellWidth, CellHeight);
            VoyageMapTileLayerGraphic frontier = chart.GetComponentInChildren<VoyageMapTileLayerGraphic>(true);
            Assert.That(frontier.TileCount, Is.EqualTo(48 * 31 + 47 * 32));
            Mesh mesh = Populate(frontier);
            Assert.That(mesh.vertexCount, Is.EqualTo(11968));
            Assert.That(mesh.vertexCount, Is.LessThan(65000));
            Assert.That(frontier.raycastTarget, Is.False);
            foreach (int index in mesh.triangles)
                Assert.That(index, Is.InRange(0, mesh.vertexCount - 1));
        }

        private Vector2 Center(int column, int row)
        {
            VoyagePoint point = map.CellCenter(column, row);
            return new Vector2(Mathf.Lerp(-580f, 580f, point.X / 100f), Mathf.Lerp(-315f, 315f, point.Y / 64f));
        }

        private void Invoke(string method, params object[] args)
        {
            typeof(CoreGameplayDemoController).GetMethod(method, PrivateInstance).Invoke(controller, args);
        }

        private Mesh Populate(VoyageMapTileLayerGraphic graphic)
        {
            Mesh mesh = new Mesh();
            meshes.Add(mesh);
            using (VertexHelper helper = new VertexHelper())
            {
                typeof(VoyageMapTileLayerGraphic).GetMethod("OnPopulateMesh", PrivateInstance,
                    null, new[] { typeof(VertexHelper) }, null)
                    .Invoke(graphic, new object[] { helper });
                helper.FillMesh(mesh);
            }
            return mesh;
        }

        private static void AssertQuad(Vector3[] vertices, Color32[] colors, int index,
            Vector2 center, Vector2 size, Color expectedColor)
        {
            Vector2 min = center - size * .5f;
            Vector2 max = min + size;
            Vector3[] expected = { new Vector3(min.x, min.y), new Vector3(min.x, max.y),
                new Vector3(max.x, max.y), new Vector3(max.x, min.y) };
            for (int corner = 0; corner < 4; corner += 1)
            {
                Assert.That(Vector3.Distance(vertices[index * 4 + corner], expected[corner]),
                    Is.LessThan(.0001f), "Quad " + index + " corner " + corner);
                Assert.That(colors[index * 4 + corner], Is.EqualTo((Color32)expectedColor), "Quad " + index);
            }
        }
    }
}
