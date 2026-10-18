using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class BattleCommandIconGraphicTests
    {
        private GameObject host;
        [TearDown]
        public void TearDown() { if (host != null) Object.DestroyImmediate(host); }

        [TestCase(36f, 36f)]
        [TestCase(64f, 40f)]
        public void EveryCommandAndResource_HasADistinctFiniteBoundedSymbol(float width, float height)
        {
            BattleCommandIconGraphic graphic = Create(width, height);
            var fingerprints = new HashSet<int>();
            foreach (BattleCommandIconKind kind in Enum.GetValues(typeof(BattleCommandIconKind)))
            {
                graphic.Configure(kind);
                var mesh = new Mesh();
                try
                {
                    Populate(graphic, mesh);
                    Assert.That(mesh.vertexCount, Is.InRange(1, 2048), kind.ToString());
                    Assert.That(mesh.triangles.Length, Is.GreaterThan(0), kind.ToString());
                    Rect rect = graphic.rectTransform.rect;
                    int fingerprint = 17;
                    foreach (Vector3 vertex in mesh.vertices)
                    {
                        Assert.That(float.IsNaN(vertex.x) || float.IsInfinity(vertex.x), Is.False, kind.ToString());
                        Assert.That(float.IsNaN(vertex.y) || float.IsInfinity(vertex.y), Is.False, kind.ToString());
                        Assert.That(vertex.x, Is.InRange(rect.xMin, rect.xMax), kind.ToString());
                        Assert.That(vertex.y, Is.InRange(rect.yMin, rect.yMax), kind.ToString());
                        unchecked
                        {
                            fingerprint = fingerprint * 31 + Mathf.RoundToInt(vertex.x * 1000f);
                            fingerprint = fingerprint * 31 + Mathf.RoundToInt(vertex.y * 1000f);
                        }
                    }
                    foreach (Color32 tint in mesh.colors32)
                        unchecked { fingerprint = fingerprint * 31 + tint.r * 65536 + tint.g * 256 + tint.b; }
                    Assert.That(fingerprints.Add(fingerprint), Is.True, "Distinct symbol required for " + kind);
                    Assert.That(graphic.Kind, Is.EqualTo(kind));
                    Assert.That(graphic.raycastTarget, Is.False, "The outer HUD cell owns hover and click events.");
                }
                finally { Object.DestroyImmediate(mesh); }
            }
        }

        [Test]
        public void EmptyRect_DoesNotGenerateInvalidGeometry()
        {
            BattleCommandIconGraphic graphic = Create(0f, 0f);
            graphic.Configure(BattleCommandIconKind.DrawPile);
            var mesh = new Mesh();
            try { Populate(graphic, mesh); Assert.That(mesh.vertexCount, Is.Zero); }
            finally { Object.DestroyImmediate(mesh); }
        }

        private BattleCommandIconGraphic Create(float width, float height)
        {
            host = new GameObject("Command icon test", typeof(RectTransform), typeof(BattleCommandIconGraphic));
            BattleCommandIconGraphic graphic = host.GetComponent<BattleCommandIconGraphic>();
            graphic.rectTransform.sizeDelta = new Vector2(width, height);
            return graphic;
        }

        private static void Populate(BattleCommandIconGraphic graphic, Mesh mesh)
        {
            using (var helper = new VertexHelper())
            {
                typeof(BattleCommandIconGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { helper });
                helper.FillMesh(mesh);
            }
        }
    }
}
