using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Core.Tests
{
    public sealed class BattleCardFrameGraphicTests
    {
        private static readonly string[] CardTypes = { "炮击", "机动", "登船", "维修", "防御", "策略" };
        private readonly List<Object> temporary = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = temporary.Count - 1; i >= 0; i--)
                if (temporary[i] != null) Object.DestroyImmediate(temporary[i]);
            temporary.Clear();
        }

        [Test]
        public void SixTypes_HaveDifferentGeometryEvenWithoutTheirColors()
        {
            Mesh[] frames = CardTypes.Select(type => Populate(CreateFrame(type, true, new Vector2(202f, 296f)))).ToArray();
            for (int first = 0; first < frames.Length; first++)
            {
                Assert.That(frames[first].vertexCount, Is.GreaterThan(0), CardTypes[first]);
                for (int second = first + 1; second < frames.Length; second++)
                {
                    bool sameGeometry = frames[first].vertices.SequenceEqual(frames[second].vertices) &&
                        frames[first].triangles.SequenceEqual(frames[second].triangles);
                    Assert.That(sameGeometry, Is.False,
                        CardTypes[first] + " and " + CardTypes[second] + " need different frame silhouettes or ornaments, not just another tint.");
                }
            }
        }

        [TestCase(202f, 296f)]
        [TestCase(206f, 300f)]
        [TestCase(270f, 408f)]
        [TestCase(162f, 240f)]
        [TestCase(96f, 72f)]
        [TestCase(24f, 20f)]
        public void AllFrames_KeepTheirMeshInsideTheCardAndOutOfTheTextAndArtwork(float width, float height)
        {
            foreach (string type in CardTypes)
            {
                BattleCardFrameGraphic frame = CreateFrame(type, true, new Vector2(width, height));
                Mesh mesh = Populate(frame);
                Rect bounds = frame.rectTransform.rect;
                Assert.That(frame.raycastTarget, Is.False, type + " must leave card clicks and hover to the existing button.");
                Assert.That(frame.GetComponent<CanvasRenderer>(), Is.Not.Null);
                Assert.That(frame.CardType, Is.EqualTo(type));
                Assert.That(mesh.vertexCount, Is.LessThan(2000), type + " should remain a small UI mesh.");
                foreach (Vector3 vertex in mesh.vertices)
                {
                    Assert.That(vertex.x, Is.InRange(bounds.xMin - .001f, bounds.xMax + .001f), type);
                    Assert.That(vertex.y, Is.InRange(bounds.yMin - .001f, bounds.yMax + .001f), type);
                }

                // Edge trim is at most 10 reference pixels deep; only the four
                // 18-pixel corner squares may have a larger ornament. Testing
                // triangle intersections catches a solid quad whose vertices
                // are all outside the safe area but still covers the artwork.
                float scale = Mathf.Min(width / 202f, height / 220f);
                float edge = 10f * scale;
                float corner = 18f * scale;
                Rect verticalSafe = Rect.MinMaxRect(bounds.xMin + corner, bounds.yMin + edge,
                    bounds.xMax - corner, bounds.yMax - edge);
                Rect horizontalSafe = Rect.MinMaxRect(bounds.xMin + edge, bounds.yMin + corner,
                    bounds.xMax - edge, bounds.yMax - corner);
                AssertMeshDoesNotCover(mesh, verticalSafe, type);
                AssertMeshDoesNotCover(mesh, horizontalSafe, type);
            }
        }

        [TestCase("炮击")]
        [TestCase("机动")]
        [TestCase("登船")]
        [TestCase("维修")]
        [TestCase("防御")]
        [TestCase("策略")]
        public void UnavailableFrame_PreservesItsShapeWhileChangingItsColors(string type)
        {
            Mesh available = Populate(CreateFrame(type, true, new Vector2(202f, 296f)));
            Mesh unavailable = Populate(CreateFrame(type, false, new Vector2(202f, 296f)));
            CollectionAssert.AreEqual(available.vertices, unavailable.vertices, type);
            CollectionAssert.AreEqual(available.triangles, unavailable.triangles, type);
            CollectionAssert.AreNotEqual(available.colors32, unavailable.colors32, type);
            Assert.That(unavailable.colors32.Any(color => color.a > 0), Is.True,
                "Unavailable cards retain an identifiable category border.");
        }

        [Test]
        public void RetainedFrame_CanBeReconfiguredAcrossTypesWithoutChildrenOrGlobalRandomChanges()
        {
            BattleCardFrameGraphic frame = CreateFrame("炮击", true, new Vector2(202f, 296f));
            UnityEngine.Random.State original = UnityEngine.Random.state;
            try
            {
                foreach (string type in CardTypes)
                {
                    frame.Configure(type, true);
                    Mesh first = Populate(frame);
                    frame.Configure(type, true);
                    Mesh repeated = Populate(frame);
                    Mesh fresh = Populate(CreateFrame(type, true, new Vector2(202f, 296f)));
                    Assert.That(frame.CardType, Is.EqualTo(type));
                    CollectionAssert.AreEqual(first.vertices, repeated.vertices, type + " repeated configuration");
                    CollectionAssert.AreEqual(first.colors32, repeated.colors32, type + " repeated configuration");
                    CollectionAssert.AreEqual(fresh.vertices, first.vertices, type + " retained card reuse");
                    CollectionAssert.AreEqual(fresh.colors32, first.colors32, type + " retained card reuse");
                    Assert.That(frame.transform.childCount, Is.Zero, "Frame decorations should reuse one Graphic, not grow a child hierarchy.");
                    Assert.That(frame.GetComponents<BattleCardFrameGraphic>().Length, Is.EqualTo(1));
                }
                Assert.That(UnityEngine.Random.state, Is.EqualTo(original), "Drawing cards must not alter random gameplay outcomes.");
            }
            finally
            {
                UnityEngine.Random.state = original;
            }
        }

        [TestCase(0f, 0f)]
        [TestCase(0f, 296f)]
        [TestCase(202f, 0f)]
        public void EmptyLayout_DoesNotProduceInvalidGeometry(float width, float height)
        {
            Mesh mesh = Populate(CreateFrame("策略", true, new Vector2(width, height)));
            Assert.That(mesh.vertexCount, Is.Zero);
        }

        private BattleCardFrameGraphic CreateFrame(string type, bool available, Vector2 size)
        {
            GameObject host = Track(new GameObject("Card Frame Test", typeof(RectTransform), typeof(BattleCardFrameGraphic)));
            BattleCardFrameGraphic frame = host.GetComponent<BattleCardFrameGraphic>();
            frame.rectTransform.sizeDelta = size;
            frame.Configure(type, available);
            return frame;
        }

        private T Track<T>(T item) where T : Object
        {
            temporary.Add(item);
            return item;
        }

        private Mesh Populate(BattleCardFrameGraphic graphic)
        {
            MethodInfo method = typeof(BattleCardFrameGraphic).GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(VertexHelper) }, null);
            Assert.That(method, Is.Not.Null);
            Mesh mesh = Track(new Mesh());
            using (VertexHelper helper = new VertexHelper())
            {
                method.Invoke(graphic, new object[] { helper });
                helper.FillMesh(mesh);
            }
            return mesh;
        }

        private static void AssertMeshDoesNotCover(Mesh mesh, Rect safeArea, string type)
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            for (int index = 0; index < triangles.Length; index += 3)
            {
                var polygon = new List<Vector2>
                {
                    vertices[triangles[index]], vertices[triangles[index + 1]], vertices[triangles[index + 2]]
                };
                polygon = Clip(polygon, 0, safeArea.xMin, true);
                polygon = Clip(polygon, 0, safeArea.xMax, false);
                polygon = Clip(polygon, 1, safeArea.yMin, true);
                polygon = Clip(polygon, 1, safeArea.yMax, false);
                float area = 0f;
                for (int point = 0; point < polygon.Count; point++)
                {
                    Vector2 a = polygon[point];
                    Vector2 b = polygon[(point + 1) % polygon.Count];
                    area += a.x * b.y - b.x * a.y;
                }
                Assert.That(Mathf.Abs(area) * .5f, Is.LessThan(.02f),
                    type + " triangle " + index / 3 + " covers the transparent center.");
            }
        }

        private static List<Vector2> Clip(List<Vector2> polygon, int axis, float limit, bool keepGreater)
        {
            var result = new List<Vector2>();
            if (polygon.Count == 0) return result;
            Vector2 previous = polygon[polygon.Count - 1];
            bool previousInside = keepGreater ? previous[axis] >= limit : previous[axis] <= limit;
            foreach (Vector2 current in polygon)
            {
                bool currentInside = keepGreater ? current[axis] >= limit : current[axis] <= limit;
                if (currentInside != previousInside)
                {
                    float alongEdge = (limit - previous[axis]) / (current[axis] - previous[axis]);
                    result.Add(Vector2.LerpUnclamped(previous, current, alongEdge));
                }
                if (currentInside) result.Add(current);
                previous = current;
                previousInside = currentInside;
            }
            return result;
        }
    }
}
