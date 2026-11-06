using System;
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
    public sealed class NavalSupplementFxGraphicTests
    {
        private readonly List<Object> temporary = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            for (int i = temporary.Count - 1; i >= 0; i--)
                if (temporary[i] != null) Object.DestroyImmediate(temporary[i]);
            temporary.Clear();
        }

        [Test]
        public void LegacyProjectile_RetainsSmokeAfterContactAndThenFullyExpires()
        {
            var graphic = Create(NavalSupplementFxKind.Projectile);
            graphic.SetElapsed(.10f);
            Assert.That(Cells(Populate(graphic)).Any(cell => cell / 8 == 0), Is.True, "Authored muzzle frames play at launch.");
            graphic.SetElapsed(.45f);
            int[] afterContact = Cells(Populate(graphic)).ToArray();
            Assert.That(afterContact, Does.Contain(74), "Static smoke remains after the .40s projectile has arrived.");
            Assert.That(afterContact, Has.No.Member(84), "The projectile itself cannot linger after contact.");
            graphic.SetElapsed(.67f);
            Assert.That(Populate(graphic).vertexCount, Is.Zero);
        }

        [TestCase(NavalSupplementFxKind.Impact, 1)]
        [TestCase(NavalSupplementFxKind.Missed, 2)]
        [TestCase(NavalSupplementFxKind.Curse, 5)]
        public void LegacyContact_UsesAuthoredFramesAndDoesNotConfuseMissesWithHits(NavalSupplementFxKind kind, int row)
        {
            var graphic = Create(kind);
            graphic.SetElapsed(.10f);
            int[] early = Cells(Populate(graphic)).Where(cell => cell / 8 == row).ToArray();
            graphic.SetElapsed(.30f);
            int[] late = Cells(Populate(graphic)).Where(cell => cell / 8 == row).ToArray();
            Assert.That(early, Is.Not.Empty);
            Assert.That(late, Is.Not.Empty);
            Assert.That(late.Min(), Is.GreaterThan(early.Max()));
            if (kind == NavalSupplementFxKind.Missed)
                Assert.That(Cells(Populate(graphic)).Any(cell => cell / 8 == 1), Is.False);
        }

        [Test]
        public void LegacyGuardedContact_ShowsShieldWithoutHullDamageOrMissSplash()
        {
            var graphic = Create(NavalSupplementFxKind.Guarded);
            foreach (float elapsed in new[] { .04f, .15f, .30f })
            {
                graphic.SetElapsed(elapsed);
                int[] drawn = Cells(Populate(graphic)).ToArray();
                Assert.That(drawn, Does.Contain(82), "A blocked impact must retain the blue shield accent.");
                Assert.That(drawn, Does.Contain(72), "A blocked impact uses a brief flash at the shield surface.");
                Assert.That(drawn, Has.No.Member(85), "Static wood splinters falsely signal hull damage on a block.");
                Assert.That(drawn.Any(cell => cell / 8 == 1), Is.False, "A block cannot play the hull damage sequence.");
                Assert.That(drawn.Any(cell => cell / 8 == 2), Is.False, "A blocked projectile is distinct from a water miss.");
            }
        }

        [Test]
        public void AllSupplementKinds_AreFiniteClippedNoninteractiveAndStopAtTheirOwnLifetime()
        {
            foreach (NavalSupplementFxKind kind in Enum.GetValues(typeof(NavalSupplementFxKind)))
            {
                var graphic = Create(kind, new Vector2(-430f, -160f), new Vector2(445f, 170f));
                Assert.That(graphic.mainTexture, Is.SameAs(Resources.Load<Texture2D>(NavalVfxArtCatalog.TextureResource)));
                Assert.That(graphic.raycastTarget, Is.False);
                graphic.SetElapsed(.15f);
                Mesh mesh = Populate(graphic);
                Assert.That(mesh.vertexCount, Is.GreaterThan(0), kind.ToString());
                foreach (Vector3 p in mesh.vertices)
                {
                    Assert.That(float.IsNaN(p.x) || float.IsInfinity(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.y), Is.False);
                    Assert.That(p.x, Is.InRange(-450.01f, 450.01f));
                    Assert.That(p.y, Is.InRange(-180.01f, 180.01f));
                }
                Assert.That(mesh.uv.All(uv => uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f), Is.True);
                graphic.SetElapsed(1f);
                Assert.That(Populate(graphic).vertexCount, Is.Zero);
                graphic.SetElapsed(float.NaN);
                Assert.That(Populate(graphic).vertexCount, Is.Zero);
            }
        }

        private NavalSupplementFxGraphic Create(NavalSupplementFxKind kind, Vector2? from = null, Vector2? to = null)
        {
            var go = Track(new GameObject("supplement fx test", typeof(RectTransform), typeof(NavalSupplementFxGraphic)));
            var graphic = go.GetComponent<NavalSupplementFxGraphic>();
            graphic.rectTransform.sizeDelta = new Vector2(900f, 360f);
            graphic.Configure(kind, from ?? new Vector2(-270f, 0f), to ?? new Vector2(270f, 0f), .4f, Color.white);
            return graphic;
        }

        private Mesh Populate(NavalSupplementFxGraphic graphic)
        {
            Mesh mesh = Track(new Mesh());
            using (var helper = new VertexHelper())
            {
                typeof(NavalSupplementFxGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { helper });
                helper.FillMesh(mesh);
            }
            return mesh;
        }

        private static IEnumerable<int> Cells(Mesh mesh) => mesh.uv.Select(uv =>
            Mathf.Clamp(Mathf.FloorToInt(uv.x * 8f), 0, 7) + (10 - Mathf.Clamp(Mathf.FloorToInt(uv.y * 11f), 0, 10)) * 8).Distinct();
        private T Track<T>(T value) where T : Object { temporary.Add(value); return value; }
    }
}
