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
        // The current sea chart has square logical cells (48 by 32), even
        // though the surrounding dashboard viewport has a different aspect.
        private const float Height = Width / 48f * 32f;
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        private readonly List<Mesh> meshes = new List<Mesh>();
        private GameObject host;
        private CoreGameplayDemoController controller;
        private CoreVoyageMap map;
        private RectTransform chart;
        private float CellWidth => Width / CoreVoyageMap.ChartColumns;
        private float CellHeight => Height / CoreVoyageMap.ChartRows;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Chart Quad Batch Test");
            host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            CoreVoyageSession session = new CoreVoyageSession();
            map = new CoreVoyageMap { Hash = 0xA321B7u, Seed = "QUAD-BATCH-REGRESSION" };
            map.InitializeChartExploration();
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
        public void CompleteChart_FineFogMatchesEveryCellWithoutOverlapAndKeepsFrontierAboveTheMask()
        {
            for (int index = 0; index < map.ChartExploredCells.Length; index += 1)
                map.ChartExploredCells[index] = index % 5 == 0;
            Invoke("BuildVoyageTerrainLayers", chart);
            RectTransform visibility = chart.Find("Visibility Mask Layer").GetComponent<RectTransform>();
            VoyageMapTileLayerGraphic fog = visibility.Find("Chart Fog Tiles").GetComponent<VoyageMapTileLayerGraphic>();
            Assert.That(visibility.childCount, Is.EqualTo(2), "One fog mesh and one frontier mesh replace the per-cell UI objects.");
            Assert.That(visibility.GetChild(1).name, Is.EqualTo("Chart Fog Frontier"));
            Assert.That(fog.GetComponent<CanvasRenderer>(), Is.Not.Null,
                "Custom UGUI graphics need their own CanvasRenderer requirement to be visible.");
            Assert.That(fog.raycastTarget, Is.False);
            Assert.That(fog.TileCount, Is.EqualTo(6144));
            Mesh mesh = Populate(fog);
            Vector3[] vertices = mesh.vertices;
            Color32[] colors = mesh.colors32;
            Assert.That(vertices.Length, Is.EqualTo(24576));
            Assert.That(vertices.Length, Is.LessThan(65000));
            Assert.That(mesh.triangles.Length, Is.EqualTo(36864));
            for (int index = 0; index < map.ChartExploredCells.Length; index += 1)
            {
                int column = index % CoreVoyageMap.ChartColumns;
                int row = index / CoreVoyageMap.ChartColumns;
                float alpha = map.ChartExploredCells[index] ? .025f : 1f;
                Vector2 minimum = new Vector2(-Width * .5f + column * CellWidth, -Height * .5f + row * CellHeight);
                Vector2 maximum = new Vector2(-Width * .5f + (column + 1) * CellWidth, -Height * .5f + (row + 1) * CellHeight);
                AssertQuadBounds(vertices, colors, index, minimum, maximum,
                    new Color(6f / 255f, 18f / 255f, 24f / 255f, alpha));
                if (column + 1 < CoreVoyageMap.ChartColumns)
                    Assert.That(vertices[index * 4 + 2].x,
                        Is.EqualTo(vertices[(index + 1) * 4].x), "Horizontal fog seam");
                if (row + 1 < CoreVoyageMap.ChartRows)
                    Assert.That(vertices[index * 4 + 2].y,
                        Is.EqualTo(vertices[(index + CoreVoyageMap.ChartColumns) * 4].y), "Vertical fog seam");
            }
        }

        [Test]
        public void Grid_PreservesAllMeridiansParallelsAndTheirMajorLineWeights()
        {
            Invoke("BuildChartGrid", chart, CellWidth, CellHeight);
            VoyageMapTileLayerGraphic lines = chart.GetComponentInChildren<VoyageMapTileLayerGraphic>(true);
            Assert.That(chart.childCount, Is.EqualTo(1));
            Assert.That(lines.TileCount, Is.EqualTo(162));
            Assert.That(lines.raycastTarget, Is.False);
            Mesh mesh = Populate(lines);
            Vector3[] vertices = mesh.vertices;
            Color32[] colors = mesh.colors32;
            Color minor = new Color(.58f, .78f, .78f, .018f);
            Color major = new Color(.72f, .87f, .82f, .044f);
            for (int column = 0; column <= CoreVoyageMap.ChartColumns; column += 1)
            {
                bool isMajor = column % 16 == 0;
                AssertQuad(vertices, colors, column, new Vector2(-580f + column * CellWidth, 0f),
                    new Vector2(isMajor ? 1.15f : .55f, Height), isMajor ? major : minor);
            }
            for (int row = 0; row <= CoreVoyageMap.ChartRows; row += 1)
            {
                bool isMajor = row % 8 == 0;
                AssertQuad(vertices, colors, 97 + row, new Vector2(0f, -Height * .5f + row * CellHeight),
                    new Vector2(Width, isMajor ? 1.15f : .55f), isMajor ? major : minor);
            }
        }

        [Test]
        public void Frontier_OnlyOutlinesUnexploredNeighboursWithoutDoubleBlendingCorners()
        {
            map.ChartExploredCells[0] = true;
            map.ChartExploredCells[10 * CoreVoyageMap.ChartColumns + 10] = true;
            map.ChartExploredCells[10 * CoreVoyageMap.ChartColumns + 11] = true;
            Invoke("BuildVisibilityFrontier", chart, CellWidth, CellHeight);
            VoyageMapTileLayerGraphic frontier = chart.GetComponentInChildren<VoyageMapTileLayerGraphic>(true);
            Assert.That(frontier.TileCount, Is.EqualTo(8),
                "Do not draw at the chart edge or between two explored cells.");
            Mesh mesh = Populate(frontier);
            Vector3[] vertices = mesh.vertices;
            Color32[] colors = mesh.colors32;
            Color color = new Color(.3f, .54f, .57f, .22f);
            const float thickness = 1.1f;
            Vector2 horizontal = new Vector2(CellWidth, thickness);
            Vector2 vertical = new Vector2(thickness, CellHeight - thickness * 2f);
            Vector2 north = new Vector2(0f, (CellHeight - thickness) * .5f);
            Vector2 east = new Vector2((CellWidth - thickness) * .5f, 0f);
            AssertQuad(vertices, colors, 0, Center(0, 0) + north, horizontal, color);
            AssertQuad(vertices, colors, 1, Center(0, 0) + east - new Vector2(0f, thickness * .5f),
                new Vector2(thickness, CellHeight - thickness), color);
            AssertQuad(vertices, colors, 2, Center(10, 10) + north, horizontal, color);
            AssertQuad(vertices, colors, 3, Center(10, 10) - north, horizontal, color);
            AssertQuad(vertices, colors, 4, Center(10, 10) - east, vertical, color);
            AssertQuad(vertices, colors, 5, Center(11, 10) + north, horizontal, color);
            AssertQuad(vertices, colors, 6, Center(11, 10) + east, vertical, color);
            AssertQuad(vertices, colors, 7, Center(11, 10) - north, horizontal, color);
            AssertNoOverlappingQuads(vertices);
        }

        [Test]
        public void CheckerboardExploration_WorstCaseFrontierStaysWithinUguiMeshLimit()
        {
            for (int row = 0; row < CoreVoyageMap.ChartRows; row += 1)
            for (int column = 0; column < CoreVoyageMap.ChartColumns; column += 1)
                map.ChartExploredCells[row * CoreVoyageMap.ChartColumns + column] = (row + column) % 2 == 0;
            Invoke("BuildVisibilityFrontier", chart, CellWidth, CellHeight);
            VoyageMapTileLayerGraphic frontier = chart.GetComponentInChildren<VoyageMapTileLayerGraphic>(true);
            Assert.That(frontier.TileCount, Is.EqualTo(96 * 63 + 95 * 64));
            Mesh mesh = Populate(frontier);
            Assert.That(mesh.vertexCount, Is.EqualTo(48512));
            Assert.That(mesh.vertexCount, Is.LessThan(65000));
            Assert.That(frontier.raycastTarget, Is.False);
            foreach (int index in mesh.triangles)
                Assert.That(index, Is.InRange(0, mesh.vertexCount - 1));
        }

        [Test]
        public void SerializedChartBatch_RetainsQuadsAndTheirNonOverlappingCorners()
        {
            RectTransform originalRect = new GameObject("Original Batch", typeof(RectTransform)).GetComponent<RectTransform>();
            originalRect.SetParent(chart, false);
            VoyageMapTileLayerGraphic original = originalRect.gameObject.AddComponent<VoyageMapTileLayerGraphic>();
            original.Initialize(null);
            original.AddCellFrontier(Vector2.zero, new Vector2(CellWidth, CellHeight), 1.1f,
                new Color(.3f, .54f, .57f, .22f), true, true, true, true);
            original.Complete();
            RectTransform restoredRect = new GameObject("Restored Batch", typeof(RectTransform)).GetComponent<RectTransform>();
            restoredRect.SetParent(chart, false);
            VoyageMapTileLayerGraphic restored = restoredRect.gameObject.AddComponent<VoyageMapTileLayerGraphic>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(original), restored);
            Mesh originalMesh = Populate(original);
            Mesh restoredMesh = Populate(restored);
            Assert.That(restored.TileCount, Is.EqualTo(4), "Saved editor previews must retain their batched geometry.");
            Assert.That(restored.raycastTarget, Is.False);
            CollectionAssert.AreEqual(originalMesh.vertices, restoredMesh.vertices);
            CollectionAssert.AreEqual(originalMesh.colors32, restoredMesh.colors32);
            CollectionAssert.AreEqual(originalMesh.triangles, restoredMesh.triangles);
            AssertNoOverlappingQuads(restoredMesh.vertices);
        }

        private Vector2 Center(int column, int row)
        {
            VoyagePoint point = map.ChartCellCenter(column, row);
            return new Vector2(Mathf.Lerp(-Width * .5f, Width * .5f, point.X / 100f),
                Mathf.Lerp(-Height * .5f, Height * .5f, point.Y / 64f));
        }

        private static void AssertNoOverlappingQuads(Vector3[] vertices)
        {
            for (int first = 0; first < vertices.Length; first += 4)
            for (int second = first + 4; second < vertices.Length; second += 4)
            {
                float overlapWidth = Mathf.Min(vertices[first + 2].x, vertices[second + 2].x)
                    - Mathf.Max(vertices[first].x, vertices[second].x);
                float overlapHeight = Mathf.Min(vertices[first + 2].y, vertices[second + 2].y)
                    - Mathf.Max(vertices[first].y, vertices[second].y);
                Assert.That(overlapWidth > .0001f && overlapHeight > .0001f, Is.False,
                    "Translucent frontier strips must not brighten one another at a corner.");
            }
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
            Vector2 max = center + size * .5f;
            AssertQuadBounds(vertices, colors, index, min, max, expectedColor);
        }

        private static void AssertQuadBounds(Vector3[] vertices, Color32[] colors, int index,
            Vector2 min, Vector2 max, Color expectedColor)
        {
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
