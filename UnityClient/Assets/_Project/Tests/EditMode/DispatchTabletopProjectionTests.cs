using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class DispatchTabletopProjectionTests
    {
        private static readonly Vector2[] Corners =
        {
            Vector2.zero, Vector2.right, Vector2.one, Vector2.up
        };

        private GameObject host;

        [TearDown]
        public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        [Test]
        public void AllThreeChartsFitInsideTheTabletopWithoutOverlapping()
        {
            Vector2[] table = Corners.Select(DispatchTabletopLayout.TablePoint).ToArray();
            Vector2[][] charts = new Vector2[3][];
            for (int index = 0; index < charts.Length; index++)
            {
                int chartIndex = index;
                charts[index] = Corners.Select(uv => DispatchTabletopLayout.ChartCorner(chartIndex, uv)).ToArray();
                Rect bounds = DispatchTabletopLayout.ChartBounds(index);
                foreach (Vector2 corner in charts[index])
                {
                    Assert.That(IsInsideConvex(table, corner), Is.True,
                        "The entire paper must lie on the painted tabletop, not its front apron or legs.");
                    Assert.That(corner.x, Is.InRange(bounds.xMin - 0.01f, bounds.xMax + 0.01f));
                    Assert.That(corner.y, Is.InRange(bounds.yMin - 0.01f, bounds.yMax + 0.01f));
                }
                Assert.That(bounds.width, Is.GreaterThan(0f));
                Assert.That(bounds.height, Is.GreaterThan(0f));
            }

            for (int first = 0; first < charts.Length; first++)
                for (int second = first + 1; second < charts.Length; second++)
                    Assert.That(HaveSeparatingAxis(charts[first], charts[second]), Is.True,
                        "Mission papers need separate, unambiguous click areas.");
        }

        [Test]
        public void ProjectionAndRaycastFilterUseThePaperOutlineNotItsBoundingRectangle()
        {
            TabletopQuadProjection projection = CreateProjection();
            projection.Configure(new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.1f),
                new Vector2(0.2f, 0.9f), new Vector2(0.8f, 0.9f));
            Assert.That(projection.Project(Vector2.zero), Is.EqualTo(new Vector2(0.05f, 0.1f)));
            Assert.That(projection.Project(Vector2.one), Is.EqualTo(new Vector2(0.8f, 0.9f)));
            Vector2 center = projection.Project(new Vector2(0.5f, 0.5f));
            Assert.That(projection.Contains(center), Is.True);
            Assert.That(projection.Contains(new Vector2(0.01f, 0.99f)), Is.False);

            RectTransform rect = host.GetComponent<RectTransform>();
            foreach (Vector2 point in new[] { center, new Vector2(0.01f, 0.99f), new Vector2(1.1f, 0.5f) })
            {
                Vector3 local = new Vector3(Mathf.LerpUnclamped(rect.rect.xMin, rect.rect.xMax, point.x),
                    Mathf.LerpUnclamped(rect.rect.yMin, rect.rect.yMax, point.y), 0f);
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(local));
                Assert.That(projection.IsRaycastLocationValid(screen, null), Is.EqualTo(projection.Contains(point)),
                    "The visible trapezoid and the interactive region must agree.");
            }
        }

        [Test]
        public void ProjectedPaperMeshKeepsTextureCoordinatesAndSubdividesTheSurface()
        {
            TabletopQuadProjection projection = CreateProjection();
            DispatchTabletopLayout.ConfigureProjection(projection, 1);
            Rect rect = host.GetComponent<RectTransform>().rect;
            using (VertexHelper mesh = new VertexHelper())
            {
                foreach (Vector2 uv in Corners)
                    mesh.AddVert(new Vector3(Mathf.Lerp(rect.xMin, rect.xMax, uv.x),
                        Mathf.Lerp(rect.yMin, rect.yMax, uv.y), 0f), Color.white, uv);
                mesh.AddTriangle(0, 1, 2);
                mesh.AddTriangle(0, 2, 3);
                projection.ModifyMesh(mesh);
                Assert.That(mesh.currentVertCount, Is.GreaterThan(4),
                    "Subdivisions avoid a visible diagonal distortion across the map art.");
                Assert.That(mesh.currentIndexCount, Is.GreaterThan(6));
                UIVertex vertex = default;
                for (int index = 0; index < mesh.currentVertCount; index++)
                {
                    mesh.PopulateUIVertex(ref vertex, index);
                    Vector2 uv = new Vector2(vertex.uv0.x, vertex.uv0.y);
                    Assert.That(uv.x, Is.InRange(0f, 1f));
                    Assert.That(uv.y, Is.InRange(0f, 1f));
                    Vector2 expected = projection.Project(uv);
                    Vector2 actual = new Vector2((vertex.position.x - rect.xMin) / rect.width,
                        (vertex.position.y - rect.yMin) / rect.height);
                    Assert.That(Vector2.Distance(actual, expected), Is.LessThan(0.001f));
                }
            }
        }

        [TestCase(CoreGameplayPreviewPort.WhiteShell)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        [TestCase(CoreGameplayPreviewPort.TwilightTide)]
        public void EditorPreviewPlacesProjectedChartsOnTheSameTablePlane(CoreGameplayPreviewPort port)
        {
            host = new GameObject("Dispatch Tabletop Preview Test");
            CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Dispatch, port, 0);
            Transform preview = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
            Assert.That(preview, Is.Not.Null);
            RectTransform stage = Find<RectTransform>(preview, "Dispatch Table Stage");
            RawImage room = Find<RawImage>(preview, "Dispatch Background");
            Assert.That(room.texture.name, Is.EqualTo("dispatch_room_integrated_v3"));
            Assert.That(room.rectTransform.sizeDelta, Is.EqualTo(DispatchTabletopLayout.BackgroundSize));
            Assert.That(preview.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Dispatch Command Table"), Is.False,
                "A standalone table would cover the miniature ships and duplicate the room's furniture.");

            for (int index = 0; index < 3; index++)
            {
                RawImage art = Find<RawImage>(preview, "Dispatch Mission Chart Art " + index);
                TabletopQuadProjection projection = art.GetComponent<TabletopQuadProjection>();
                Assert.That(projection, Is.Not.Null, "Each paper must share the table's perspective.");
                Assert.That(art.texture, Is.Not.Null);
                foreach (Vector2 uv in Corners)
                {
                    Vector2 projected = projection.Project(uv);
                    Rect rect = art.rectTransform.rect;
                    Vector3 local = new Vector3(Mathf.LerpUnclamped(rect.xMin, rect.xMax, projected.x),
                        Mathf.LerpUnclamped(rect.yMin, rect.yMax, projected.y), 0f);
                    Vector2 actual = stage.InverseTransformPoint(art.transform.TransformPoint(local));
                    Assert.That(Vector2.Distance(actual, DispatchTabletopLayout.ChartCorner(index, uv)),
                        Is.LessThan(0.05f), "Preview and runtime geometry must use the same table anchors.");
                }
                Button button = Find<Button>(preview, "Dispatch Mission Chart " + index);
                Assert.That(button.interactable, Is.True);
            }
        }

        [Test]
        public void TableAnchorsMatchTheIntegratedRoomAndRemainAboveForegroundMiniatures()
        {
            Vector2[] imageCorners = { new Vector2(490f, 583f), new Vector2(1100f, 634f),
                new Vector2(1180f, 538f), new Vector2(645f, 500f) };
            for (int index = 0; index < Corners.Length; index++)
            {
                Vector2 point = DispatchTabletopLayout.TablePoint(Corners[index]) + DispatchTabletopLayout.StagePosition;
                Vector2 contentPoint = point;
                Vector2 imageUv = PhaseSurfaceLayout.ContentToSourceUv(contentPoint, new Vector2(1672f, 941f));
                Vector2 imagePixel = new Vector2(imageUv.x * 1672f, imageUv.y * 941f);
                Assert.That(Vector2.Distance(imagePixel, imageCorners[index]), Is.LessThan(0.01f),
                    "Changing table anchors without matching the room art would put charts in mid-air again.");
            }
            for (int index = 0; index < 3; index++)
                foreach (Vector2 uv in Corners)
                {
                    Vector2 point = DispatchTabletopLayout.ChartCorner(index, uv) + DispatchTabletopLayout.StagePosition;
                    float imageY = PhaseSurfaceLayout.ContentToSourceUv(point, new Vector2(1672f, 941f)).y;
                    Assert.That(imageY, Is.LessThan(0.68f), "Interactive charts must not cover the foreground boat-model area.");
                }
        }

        private TabletopQuadProjection CreateProjection()
        {
            host = new GameObject("Dispatch Projection Test", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            host.GetComponent<RectTransform>().sizeDelta = new Vector2(400f, 200f);
            return host.AddComponent<TabletopQuadProjection>();
        }

        private static T Find<T>(Transform root, string name) where T : Component
        {
            T result = root.GetComponentsInChildren<T>(true).FirstOrDefault(value => value.name == name);
            Assert.That(result, Is.Not.Null, name);
            return result;
        }

        private static bool IsInsideConvex(Vector2[] polygon, Vector2 point)
        {
            float direction = 0f;
            for (int index = 0; index < polygon.Length; index++)
            {
                Vector2 edge = polygon[(index + 1) % polygon.Length] - polygon[index];
                Vector2 offset = point - polygon[index];
                float cross = edge.x * offset.y - edge.y * offset.x;
                if (Mathf.Abs(cross) < 0.01f) continue;
                if (direction == 0f) direction = Mathf.Sign(cross);
                else if (Mathf.Sign(cross) != direction) return false;
            }
            return true;
        }

        private static bool HaveSeparatingAxis(Vector2[] first, Vector2[] second)
        {
            foreach (Vector2[] polygon in new[] { first, second })
            {
                for (int index = 0; index < polygon.Length; index++)
                {
                    Vector2 edge = polygon[(index + 1) % polygon.Length] - polygon[index];
                    Vector2 axis = new Vector2(-edge.y, edge.x).normalized;
                    float firstMin = first.Min(point => Vector2.Dot(point, axis));
                    float firstMax = first.Max(point => Vector2.Dot(point, axis));
                    float secondMin = second.Min(point => Vector2.Dot(point, axis));
                    float secondMax = second.Max(point => Vector2.Dot(point, axis));
                    if (firstMax <= secondMin + 0.01f || secondMax <= firstMin + 0.01f) return true;
                }
            }
            return false;
        }
    }
}
