using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Core.Tests
{
    public sealed class BattleCardSurfaceGraphicTests
    {
        private readonly List<Object> temporary = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = temporary.Count - 1; i >= 0; i--)
                if (temporary[i] != null) Object.DestroyImmediate(temporary[i]);
            temporary.Clear();
        }

        [Test]
        public void SixCardTypes_HaveDifferentMaterialsAndDistinctOpaquePalettes()
        {
            var materials = new HashSet<BattleCardSurfaceMaterial>();
            var surfaces = new HashSet<Color>();
            var parchments = new HashSet<Color>();
            foreach (string type in new[] { "炮击", "机动", "登船", "维修", "防御", "策略" })
            {
                BattleCardPalette palette = BattleCardStylePalette.ForType(type);
                materials.Add(palette.Material);
                surfaces.Add(palette.Surface);
                parchments.Add(palette.Parchment);
                Assert.That(palette.Surface.a, Is.EqualTo(1f));
                Assert.That(palette.Parchment.a, Is.EqualTo(1f));
                Assert.That(palette.Parchment.grayscale, Is.GreaterThan(0.75f), type + " needs a light writing surface.");
                Assert.That(palette.Ink.grayscale, Is.LessThan(0.23f), type + " needs legible dark ink.");
                Assert.That(palette.Surface.grayscale, Is.GreaterThan(0.24f), type + " must not return to a black backing.");
            }
            Assert.That(materials.Count, Is.EqualTo(6));
            Assert.That(surfaces.Count, Is.EqualTo(6));
            Assert.That(parchments.Count, Is.EqualTo(6));
        }

        [TestCase(null)]
        [TestCase("未分类")]
        public void UnrecognizedType_StillHasALegibleFallback(string type)
        {
            BattleCardPalette palette = BattleCardStylePalette.ForType(type);
            Assert.That(palette.Parchment.grayscale - palette.Ink.grayscale, Is.GreaterThan(0.5f));
        }

        [TestCase("炮击")]
        [TestCase("机动")]
        [TestCase("登船")]
        [TestCase("维修")]
        [TestCase("防御")]
        [TestCase("策略")]
        public void AllDecorativeGeometry_IsOpaqueBoundedAndDoesNotInterceptPointer(string type)
        {
            BattleCardSurfaceGraphic graphic = CreateSurface(type, true, new Vector2(202f, 296f));
            Assert.That(graphic.GetComponent<CanvasRenderer>(), Is.Not.Null,
                "A populated custom Graphic mesh still needs a CanvasRenderer to be visible.");
            Mesh mesh = Populate(graphic);
            Assert.That(graphic.raycastTarget, Is.False);
            Assert.That(mesh.vertexCount, Is.GreaterThan(560));
            Assert.That(mesh.vertexCount, Is.LessThan(2000));
            Rect bounds = graphic.rectTransform.rect;
            foreach (Vector3 vertex in mesh.vertices)
            {
                Assert.That(vertex.x, Is.InRange(bounds.xMin - 0.001f, bounds.xMax + 0.001f));
                Assert.That(vertex.y, Is.InRange(bounds.yMin - 0.001f, bounds.yMax + 0.001f));
            }
            foreach (Color32 color in mesh.colors32) Assert.That(color.a, Is.EqualTo(255));
        }

        [Test]
        public void UnavailableCard_KeepsItsCategoryAndParchmentInsteadOfTurningBlack()
        {
            Mesh available = Populate(CreateSurface("登船", true, new Vector2(202f, 296f)));
            Mesh unavailable = Populate(CreateSurface("登船", false, new Vector2(202f, 296f)));
            CollectionAssert.AreEqual(available.vertices, unavailable.vertices);
            CollectionAssert.AreNotEqual(available.colors32, unavailable.colors32);
            bool hasLightPaper = false;
            foreach (Color32 color in unavailable.colors32)
            {
                Color value = color;
                hasLightPaper |= value.grayscale > 0.70f;
            }
            Assert.That(hasLightPaper, Is.True);
        }

        [Test]
        public void Rebuilds_UseDeterministicGeometryWithoutGlobalRandomState()
        {
            BattleCardSurfaceGraphic graphic = CreateSurface("维修", true, new Vector2(202f, 296f));
            UnityEngine.Random.State original = UnityEngine.Random.state;
            Mesh first = Populate(graphic);
            Mesh second = Populate(graphic);
            CollectionAssert.AreEqual(first.vertices, second.vertices);
            CollectionAssert.AreEqual(first.colors32, second.colors32);
            Assert.That(UnityEngine.Random.state, Is.EqualTo(original));
        }

        [Test]
        public void ApplyStyle_UsesNeutralButtonTintAndPutsDecorationBehindExistingChildren()
        {
            GameObject controllerHost = Track(new GameObject("Inactive Battle Style Controller"));
            controllerHost.SetActive(false);
            CoreGameplayDemoController controller = controllerHost.AddComponent<CoreGameplayDemoController>();
            GameObject host = Track(new GameObject("Card Style Integration", typeof(RectTransform), typeof(Image), typeof(Button)));
            host.GetComponent<RectTransform>().sizeDelta = new Vector2(206f, 300f);
            Button button = host.GetComponent<Button>();
            button.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.04f, 1f);
            Text label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(host.transform, false);
            RawImage illustration = new GameObject("Card Art 0", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            illustration.transform.SetParent(host.transform, false);
            CoreBattleCard card = new CoreBattleCard("style-test", "船首战旗", "登船", 1, 0, 3, 0, 0, 0, "获得 3 点格挡");
            MethodInfo method = typeof(CoreGameplayDemoController).GetMethod("ApplyBattleCardStyle", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
            method.Invoke(controller, new object[] { button, card, true, 0 });

            Assert.That(button.colors.normalColor, Is.EqualTo(Color.white));
            Assert.That(button.colors.highlightedColor, Is.EqualTo(Color.white));
            Assert.That(button.colors.disabledColor.grayscale, Is.GreaterThan(0.7f));
            Assert.That(button.GetComponent<Image>().color.a, Is.Zero,
                "The complete wooden frame PNG owns the visible backing of an ordinary card.");
            Assert.That(host.GetComponentInChildren<CardSchoolFrameGraphic>().SchoolId, Is.EqualTo("neutral"));
            Assert.That(host.transform.GetChild(0).GetComponent<BattleCardSurfaceGraphic>(), Is.Not.Null);
            Assert.That(label.transform.GetSiblingIndex(), Is.GreaterThan(0));
            Assert.That(illustration.transform.GetSiblingIndex(), Is.GreaterThan(0));
            Assert.That(label.color, Is.EqualTo(CardSchoolArtCatalog.Palette(card.SchoolId).Ink));
            Assert.That(label.text, Is.EqualTo("获得3点格挡"));
            Assert.That(host.GetComponentsInChildren<Text>(true).Single(item => item.name == "Card Name 0").text,
                Is.EqualTo("船首战旗"));
            Assert.That(host.GetComponentsInChildren<Text>(true).Any(item => item.name == "Card Type 0"), Is.False);
            Assert.That(host.transform.Find("Card Type Plate 0"), Is.Null);
            StringAssert.DoesNotContain("#D6AA48", label.text);
        }

        private BattleCardSurfaceGraphic CreateSurface(string type, bool available, Vector2 size)
        {
            GameObject host = Track(new GameObject("Battle Card Material Test", typeof(RectTransform), typeof(BattleCardSurfaceGraphic)));
            BattleCardSurfaceGraphic graphic = host.GetComponent<BattleCardSurfaceGraphic>();
            graphic.rectTransform.sizeDelta = size;
            graphic.Configure(type, available);
            return graphic;
        }

        private T Track<T>(T item) where T : Object
        {
            temporary.Add(item);
            return item;
        }

        private Mesh Populate(BattleCardSurfaceGraphic graphic)
        {
            MethodInfo method = typeof(BattleCardSurfaceGraphic).GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(VertexHelper) }, null);
            Mesh mesh = Track(new Mesh());
            using (VertexHelper helper = new VertexHelper())
            {
                method.Invoke(graphic, new object[] { helper });
                helper.FillMesh(mesh);
            }
            return mesh;
        }
    }
}
