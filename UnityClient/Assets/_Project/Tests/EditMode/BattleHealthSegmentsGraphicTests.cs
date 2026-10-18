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

        [TestCase(27)]
        [TestCase(20)]
        [TestCase(10)]
        [TestCase(1)]
        [TestCase(int.MaxValue)]
        public void LegacyOverlay_NeverDrawsDivisionsOverContinuousHealth(int maximum)
        {
            BattleHealthSegmentsGraphic graphic = Create(maximum);
            Mesh mesh = Populate(graphic);
            Assert.That(mesh.vertexCount, Is.Zero);
            Assert.That(mesh.triangles, Is.Empty);
            Assert.That(graphic.raycastTarget, Is.False);
        }

        [TestCase(0)]
        [TestCase(-10)]
        [TestCase(int.MinValue)]
        public void Hud_InvalidMaximumStillProducesFiniteContinuousFill(int maximum)
        {
            Image hull = MakeImage("Hull"), chip = MakeImage("Chip");
            BattleDefenseHud hud = hull.gameObject.AddComponent<BattleDefenseHud>();
            hud.Configure(hull, chip, null, null, null, null, null, 5, maximum, 0, 0);
            Assert.That(hud.MaximumHull, Is.EqualTo(1));
            Assert.That(hull.fillAmount, Is.EqualTo(1f));
            Assert.That(chip.fillAmount, Is.EqualTo(1f));
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
            hud.SetHull(current, 19f);
            Assert.That(hud.DisplayedHull, Is.EqualTo(Mathf.Clamp((float)current, 0f, 27f)));
            Assert.That(hull.fillAmount, Is.EqualTo(hud.DisplayedHull / 27f).Within(.00001f));
            Assert.That(chip.fillAmount, Is.EqualTo(19f / 27f).Within(.00001f));
            Assert.That(hull.type, Is.EqualTo(Image.Type.Filled));
            Assert.That(hull.sprite, Is.Not.Null);
            Assert.That(hull.fillMethod, Is.EqualTo(Image.FillMethod.Horizontal));
            Assert.That(hull.fillOrigin, Is.EqualTo((int)Image.OriginHorizontal.Left));
            Assert.That(hull.color.r, Is.GreaterThan(hull.color.g * 3f));
            Assert.That(hull.GetComponentsInChildren<BattleHealthSegmentsGraphic>(true), Is.Empty);
            Assert.That(hull.raycastTarget, Is.False);
            Assert.That(chip.raycastTarget, Is.False);
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
        public void HealthLabel_ShowsCurrentAndMaximumCenteredAndReusesItsOutline()
        {
            Image hull = MakeImage("Hull");
            GameObject labelHost = new GameObject("Health Value", typeof(RectTransform), typeof(Text));
            temporary.Add(labelHost);
            Text label = labelHost.GetComponent<Text>();
            BattleDefenseHud hud = hull.gameObject.AddComponent<BattleDefenseHud>();
            hud.Configure(hull, null, null, null, null, label, null, 21, 27, 0, 0);
            Assert.That(label.text, Is.EqualTo("21 / 27"));
            Assert.That(label.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
            Assert.That(label.raycastTarget, Is.False);
            Assert.That(label.GetComponents<Outline>(), Has.Length.EqualTo(1));
            hud.SetHull(10.2f, 19f);
            Assert.That(label.text, Is.EqualTo("11 / 27"));
            hud.Configure(hull, null, null, null, null, label, null, 35, 55, 0, 0);
            Assert.That(label.text, Is.EqualTo("35 / 55"));
            Assert.That(label.GetComponents<Outline>(), Has.Length.EqualTo(1));
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
