using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Core.Tests
{
    public sealed class BattleFxGraphicTests
    {
        private readonly List<Object> temporary = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = temporary.Count - 1; i >= 0; i--)
                if (temporary[i] != null) Object.DestroyImmediate(temporary[i]);
            temporary.Clear();
        }

        [TestCaseSource(nameof(Kinds))]
        public void Effects_AreBoundedNonBlockingAndStayWithinMeshBudget(BattleFxKind kind)
        {
            BattleFxGraphic graphic = Create(kind);
            Assert.That(graphic.raycastTarget, Is.False);
            Assert.That(graphic.GetComponent<CanvasRenderer>(), Is.Not.Null);
            foreach (float progress in new[] { 0f, 0.15f, 0.5f, 0.85f, 0.99f })
            {
                graphic.Progress = progress;
                Mesh mesh = Populate(graphic);
                Assert.That(mesh.vertexCount, Is.GreaterThan(0), kind + " must have geometry before completion.");
                Assert.That(mesh.vertexCount, Is.LessThan(1500));
                Rect rect = graphic.rectTransform.rect;
                foreach (Vector3 position in mesh.vertices)
                {
                    Assert.That(position.x, Is.InRange(rect.xMin - 0.001f, rect.xMax + 0.001f));
                    Assert.That(position.y, Is.InRange(rect.yMin - 0.001f, rect.yMax + 0.001f));
                    Assert.That(float.IsNaN(position.x) || float.IsNaN(position.y), Is.False);
                }
            }
        }

        [TestCaseSource(nameof(Kinds))]
        public void Completion_RemovesAllGeometryAndProgressIsClamped(BattleFxKind kind)
        {
            BattleFxGraphic graphic = Create(kind);
            graphic.Progress = 8f;
            Assert.That(graphic.Progress, Is.EqualTo(1f));
            Assert.That(Populate(graphic).vertexCount, Is.Zero);
            graphic.Progress = -2f;
            Assert.That(graphic.Progress, Is.Zero);
            graphic.Progress = float.NaN;
            Assert.That(graphic.Progress, Is.Zero);
            graphic.Progress = float.PositiveInfinity;
            Assert.That(graphic.Progress, Is.Zero);
        }

        [TestCaseSource(nameof(Kinds))]
        public void Rebuilds_AreDeterministicAndDoNotTouchGlobalRandom(BattleFxKind kind)
        {
            BattleFxGraphic graphic = Create(kind);
            graphic.Progress = 0.5f;
            UnityEngine.Random.State original = UnityEngine.Random.state;
            Mesh first = Populate(graphic);
            Mesh second = Populate(graphic);
            CollectionAssert.AreEqual(first.vertices, second.vertices);
            CollectionAssert.AreEqual(first.colors32, second.colors32);
            Assert.That(UnityEngine.Random.state, Is.EqualTo(original));
        }

        [Test]
        public void Seed_ChangesImpactDebrisButConfigureResetsLifetime()
        {
            BattleFxGraphic graphic = Create(BattleFxKind.Impact);
            graphic.Progress = 0.5f;
            Mesh first = Populate(graphic);
            graphic.Configure(BattleFxKind.Impact, Color.white, 73);
            Assert.That(graphic.Progress, Is.Zero);
            graphic.SetProgress(0.5f);
            CollectionAssert.AreNotEqual(first.vertices, Populate(graphic).vertices);
        }

        private static IEnumerable<BattleFxKind> Kinds => (BattleFxKind[])Enum.GetValues(typeof(BattleFxKind));

        private BattleFxGraphic Create(BattleFxKind kind)
        {
            GameObject host = Track(new GameObject("Battle Effect Test", typeof(RectTransform), typeof(BattleFxGraphic)));
            BattleFxGraphic graphic = host.GetComponent<BattleFxGraphic>();
            graphic.rectTransform.sizeDelta = new Vector2(220f, 160f);
            graphic.Configure(kind, Color.white, 19);
            return graphic;
        }

        private Mesh Populate(BattleFxGraphic graphic)
        {
            MethodInfo method = typeof(BattleFxGraphic).GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(VertexHelper) }, null);
            Mesh mesh = Track(new Mesh());
            using (VertexHelper helper = new VertexHelper())
            {
                method.Invoke(graphic, new object[] { helper });
                helper.FillMesh(mesh);
            }
            return mesh;
        }

        private T Track<T>(T value) where T : Object
        {
            temporary.Add(value);
            return value;
        }
    }
}
