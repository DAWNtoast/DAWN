using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class BattleHealthSegmentsGraphicTests
    {
        private readonly List<Object> temporary = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = temporary.Count - 1; i >= 0; i--)
                if (temporary[i] != null) Object.DestroyImmediate(temporary[i]);
            temporary.Clear();
        }

        [TestCase(27, 2)]
        [TestCase(20, 1)]
        [TestCase(10, 0)]
        [TestCase(1, 0)]
        public void TenHealthDividers_RepresentTheFinalPartialSegmentWithoutAnExtraBoundary(int maximum, int count)
        {
            BattleHealthSegmentsGraphic graphic = Create(maximum);
            Mesh mesh = Populate(graphic);
            Assert.That(BattleHealthSegmentsGraphic.HealthPerSegment, Is.EqualTo(10));
            Assert.That(mesh.vertexCount, Is.EqualTo(count * 4));
            Assert.That(mesh.triangles.Length, Is.EqualTo(count * 6));
            Vector3[] vertices = mesh.vertices;
            Rect rect = graphic.rectTransform.rect;
            for (int line = 0; line < count; line++)
            {
                float center = 0f;
                for (int corner = 0; corner < 4; corner++) center += vertices[line * 4 + corner].x * .25f;
                float fraction = (center - rect.xMin) / rect.width;
                Assert.That(fraction, Is.EqualTo((line + 1) * 10f / maximum).Within(.0001f));
                Assert.That(fraction, Is.InRange(.0001f, .9999f));
            }
            Assert.That(graphic.raycastTarget, Is.False);
        }

        [TestCase(0)]
        [TestCase(-10)]
        [TestCase(int.MinValue)]
        public void InvalidMaximum_ClampsToOneAndHasNoDividers(int maximum)
        {
            BattleHealthSegmentsGraphic graphic = Create(maximum);
            Assert.That(graphic.MaximumHealth, Is.EqualTo(1));
            Assert.That(Populate(graphic).vertexCount, Is.Zero);
        }

        [Test]
        public void ExtremeMaximum_HasABoundedFiniteMeshInsideTheBar()
        {
            BattleHealthSegmentsGraphic graphic = Create(int.MaxValue);
            Mesh mesh = Populate(graphic);
            Assert.That(graphic.MaximumHealth, Is.EqualTo(int.MaxValue));
            Assert.That(mesh.vertexCount, Is.InRange(4, 1024 * 4));
            Assert.That(mesh.triangles.Length, Is.EqualTo(mesh.vertexCount / 4 * 6));
            Rect rect = graphic.rectTransform.rect;
            foreach (Vector3 vertex in mesh.vertices)
            {
                Assert.That(float.IsNaN(vertex.x) || float.IsInfinity(vertex.x), Is.False);
                Assert.That(float.IsNaN(vertex.y) || float.IsInfinity(vertex.y), Is.False);
                Assert.That(vertex.x, Is.InRange(rect.xMin - .001f, rect.xMax + .001f));
                Assert.That(vertex.y, Is.InRange(rect.yMin - .001f, rect.yMax + .001f));
            }
            Assert.That(graphic.raycastTarget, Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(11)]
        [TestCase(26)]
        [TestCase(27)]
        [TestCase(-1)]
        [TestCase(int.MaxValue)]
        public void Hud_UsesContinuousFillAcrossTenHealthBoundariesAndIndependentDamageTrail(int current)
        {
            Image hull = MakeImage("Hull"), chip = MakeImage("Chip");
            BattleDefenseHud hud = hull.gameObject.AddComponent<BattleDefenseHud>();
            hud.Configure(hull, chip, null, null, null, null, null, 27, 27, 0, 0);
            BattleHealthSegmentsGraphic grid = hull.GetComponentInChildren<BattleHealthSegmentsGraphic>(true);
            Assert.That(grid, Is.Not.Null);
            hud.SetHull(current, 19f);
            Assert.That(hud.DisplayedHull, Is.EqualTo(Mathf.Clamp((float)current, 0f, 27f)));
            Assert.That(hull.fillAmount, Is.EqualTo(hud.DisplayedHull / 27f).Within(.00001f));
            Assert.That(chip.fillAmount, Is.EqualTo(19f / 27f).Within(.00001f));
            Assert.That(hull.type, Is.EqualTo(Image.Type.Filled));
            Assert.That(hull.GetComponentsInChildren<BattleHealthSegmentsGraphic>(true), Has.Length.EqualTo(1));
            Assert.That(hull.GetComponentInChildren<BattleHealthSegmentsGraphic>(true), Is.SameAs(grid));
            Assert.That(grid.MaximumHealth, Is.EqualTo(27));
        }

        [TestCase(0)]
        [TestCase(4)]
        public void GuardAndPermanentArmor_PreserveWhiteShellWithoutChangingHealth(int armor)
        {
            Image hull = MakeImage("Hull"), chip = MakeImage("Chip");
            Image top = MakeImage("Guard Top"), bottom = MakeImage("Guard Bottom"), edge = MakeImage("Armor Frame");
            BattleDefenseHud hud = hull.gameObject.AddComponent<BattleDefenseHud>();
            hud.Configure(hull, chip, top, bottom, new[] { edge }, null, null, 21, 27, 0, armor);
            foreach (int guard in new[] { 0, 12, 5, 0 })
            {
                hud.SetGuard(guard);
                Assert.That(hud.DisplayedHull, Is.EqualTo(21));
                Assert.That(hull.fillAmount, Is.EqualTo(21f / 27f).Within(.00001f));
                Assert.That(chip.fillAmount, Is.EqualTo(21f / 27f).Within(.00001f));
                Assert.That(hud.PermanentArmor, Is.EqualTo(armor));
                Assert.That(hud.DisplayedGuard, Is.EqualTo(guard));
                Assert.That(top.fillAmount, Is.EqualTo(guard / 12f).Within(.00001f));
                Assert.That(bottom.fillAmount, Is.EqualTo(top.fillAmount));
                Assert.That(edge.color.r, Is.GreaterThan(.9f));
                Assert.That(edge.color.g, Is.GreaterThan(.9f));
                Assert.That(edge.color.b, Is.GreaterThan(.9f));
                Assert.That(edge.color.a, Is.EqualTo(guard > 0 || armor > 0 ? .95f : .12f).Within(.001f));
            }
        }

        [Test]
        public void Reconfigure_ReusesOneStretchedNonBlockingGridWithTheNewMaximum()
        {
            Image hull = MakeImage("Hull");
            BattleDefenseHud hud = hull.gameObject.AddComponent<BattleDefenseHud>();
            hud.Configure(hull, null, null, null, null, null, null, 27, 27, 0, 0);
            BattleHealthSegmentsGraphic first = hull.GetComponentInChildren<BattleHealthSegmentsGraphic>(true);
            Assert.That(first, Is.Not.Null);
            hud.Configure(hull, null, null, null, null, null, null, 55, 55, 0, 0);
            Assert.That(BattleHealthSegmentsGraphic.Attach(hull, 55), Is.SameAs(first));
            Assert.That(hull.GetComponentsInChildren<BattleHealthSegmentsGraphic>(true), Has.Length.EqualTo(1));
            Assert.That(first.MaximumHealth, Is.EqualTo(55));
            Assert.That(first.transform.parent, Is.SameAs(hull.transform));
            Assert.That(first.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(first.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(first.rectTransform.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(first.rectTransform.offsetMax, Is.EqualTo(Vector2.zero));
            Assert.That(first.raycastTarget, Is.False);
        }

        private BattleHealthSegmentsGraphic Create(int maximum)
        {
            GameObject host = new GameObject("Health Segments", typeof(RectTransform), typeof(BattleHealthSegmentsGraphic));
            temporary.Add(host);
            BattleHealthSegmentsGraphic graphic = host.GetComponent<BattleHealthSegmentsGraphic>();
            graphic.rectTransform.sizeDelta = new Vector2(270f, 20f);
            graphic.Configure(maximum);
            return graphic;
        }

        private Image MakeImage(string name)
        {
            GameObject host = new GameObject(name, typeof(RectTransform), typeof(Image));
            temporary.Add(host);
            Image image = host.GetComponent<Image>();
            image.rectTransform.sizeDelta = new Vector2(270f, 20f);
            image.sprite = BattleDefenseHud.FillSprite;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            return image;
        }

        private Mesh Populate(BattleHealthSegmentsGraphic graphic)
        {
            Mesh mesh = new Mesh();
            temporary.Add(mesh);
            using (VertexHelper helper = new VertexHelper())
            {
                typeof(BattleHealthSegmentsGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { helper });
                helper.FillMesh(mesh);
            }
            return mesh;
        }
    }
}
