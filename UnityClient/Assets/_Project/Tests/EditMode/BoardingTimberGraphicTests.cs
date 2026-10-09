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
    public sealed class BoardingTimberGraphicTests
    {
        private readonly List<Object> temporary = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = temporary.Count - 1; i >= 0; i--)
                if (temporary[i] != null) Object.DestroyImmediate(temporary[i]);
            temporary.Clear();
        }

        [TestCase(BoardingTimberKind.Deck)]
        [TestCase(BoardingTimberKind.Gangplank)]
        [TestCase(BoardingTimberKind.Beam)]
        [TestCase(BoardingTimberKind.Rope)]
        public void Timber_UsesTexturedNonBlockingDetailedGeometryWithinSmallMeshBudget(BoardingTimberKind kind)
        {
            BoardingTimberGraphic graphic = Create(kind);
            Mesh mesh = Populate(graphic);
            Assert.That(graphic.mainTexture, Is.EqualTo(WoodButtonSkin.Face.texture));
            Assert.That(graphic.raycastTarget, Is.False);
            Assert.That(mesh.vertexCount, Is.InRange(12, 400));
            Assert.That(mesh.colors32.Distinct().Count(), Is.GreaterThanOrEqualTo(2));
            foreach (Vector2 uv in mesh.uv)
            {
                Assert.That(uv.x, Is.InRange(0f, 1f));
                Assert.That(uv.y, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void FootContact_HasFeatheredTransparentEdgesAndDoesNotObstructInput()
        {
            BoardingTimberGraphic shadow = Create(BoardingTimberKind.FootShadow);
            Mesh mesh = Populate(shadow);
            Assert.That(shadow.mainTexture, Is.EqualTo(Texture2D.whiteTexture));
            Assert.That(shadow.raycastTarget, Is.False);
            Assert.That(mesh.colors32.First().a, Is.GreaterThan(0));
            Assert.That(mesh.colors32.Skip(1).All(color => color.a == 0), Is.True);
        }

        private BoardingTimberGraphic Create(BoardingTimberKind kind)
        {
            GameObject host = new GameObject("Boarding Timber Test", typeof(RectTransform), typeof(BoardingTimberGraphic));
            temporary.Add(host);
            BoardingTimberGraphic graphic = host.GetComponent<BoardingTimberGraphic>();
            graphic.rectTransform.sizeDelta = new Vector2(500f, 52f);
            graphic.Configure(kind, Color.white);
            return graphic;
        }

        private Mesh Populate(BoardingTimberGraphic graphic)
        {
            Mesh mesh = new Mesh();
            temporary.Add(mesh);
            using (VertexHelper helper = new VertexHelper())
            {
                typeof(BoardingTimberGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null)
                    .Invoke(graphic, new object[] { helper });
                helper.FillMesh(mesh);
            }
            return mesh;
        }
    }
}
