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
    public sealed class NavalCombatFxGraphicTests
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
        public void Atlas_IsRealHighResolutionResourceAndAllRolesRemainFiniteClippedAndNoninteractive()
        {
            Texture2D atlas = Resources.Load<Texture2D>(NavalVfxArtCatalog.TextureResource);
            Assert.That(atlas, Is.Not.Null);
            Assert.That(atlas.width, Is.EqualTo(2048));
            Assert.That(atlas.height, Is.EqualTo(2816));
            foreach (NavalVfxRole role in Enum.GetValues(typeof(NavalVfxRole)))
            foreach (BattleSchoolAttackKind school in Enum.GetValues(typeof(BattleSchoolAttackKind)))
            {
                NavalCombatFxGraphic graphic = Create(role, school);
                Assert.That(graphic.mainTexture, Is.SameAs(atlas));
                Assert.That(graphic.raycastTarget, Is.False);
                Assert.That(graphic.transform.childCount, Is.Zero, "Particles are batched vertices, not child objects.");
                UnityEngine.Random.State state = UnityEngine.Random.state;
                foreach (float progress in new[] { .12f, .35f, .6f, .85f })
                {
                    graphic.SetProgress(progress);
                    Mesh mesh = Populate(graphic);
                    Assert.That(mesh.vertexCount, Is.InRange(1, 6008), role + " " + school + " at " + progress);
                    foreach (Vector3 p in mesh.vertices)
                    {
                        Assert.That(float.IsNaN(p.x) || float.IsInfinity(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.y), Is.False);
                        Assert.That(p.x, Is.InRange(-450.01f, 450.01f));
                        Assert.That(p.y, Is.InRange(-180.01f, 180.01f));
                    }
                    foreach (Vector2 uv in mesh.uv)
                    {
                        Assert.That(uv.x, Is.InRange(0f, 1f));
                        Assert.That(uv.y, Is.InRange(0f, 1f));
                    }
                    CollectionAssert.AreEqual(mesh.vertices, Populate(graphic).vertices);
                }
                Assert.That(UnityEngine.Random.state, Is.EqualTo(state));
                graphic.SetProgress(1f);
                Assert.That(Populate(graphic).vertexCount, Is.Zero);
            }
        }

        [Test]
        public void ContactSprites_DistinguishHullDamageGuardAndWaterMiss_AndDebuffsNeverExplode()
        {
            var hit = Create(NavalVfxRole.Cannon, BattleSchoolAttackKind.Fire, false, BattleSchoolAttackOutcome.Hit);
            var guard = Create(NavalVfxRole.Cannon, BattleSchoolAttackKind.Fire, false, BattleSchoolAttackOutcome.Guarded);
            var miss = Create(NavalVfxRole.Cannon, BattleSchoolAttackKind.Fire, false, BattleSchoolAttackOutcome.Missed);
            var affliction = Create(NavalVfxRole.Affliction, BattleSchoolAttackKind.Fire);
            foreach (var graphic in new[] { hit, guard, miss, affliction }) graphic.SetProgress(.65f);
            Assert.That(SequenceRows(Populate(hit)), Does.Contain(1), "Hull impact plays the fire/smoke sequence.");
            Assert.That(StaticSlots(Populate(guard)), Does.Contain(10), "Absorbed damage lands on the shield.");
            Assert.That(SequenceRows(Populate(guard)), Has.No.Member(1));
            Assert.That(SequenceRows(Populate(miss)), Does.Contain(2), "A miss plays the water splash sequence.");
            Assert.That(SequenceRows(Populate(miss)), Has.No.Member(1));
            Assert.That(StaticSlots(Populate(miss)), Has.No.Member(10));
            Assert.That(Vector2.Distance(hit.ContactPoint, miss.ContactPoint), Is.GreaterThan(40f));
            Assert.That(SequenceRows(Populate(affliction)), Has.No.Member(1), "A fire debuff is not a hull damage explosion.");
            Assert.That(affliction.ContactPoint, Is.EqualTo(affliction.To));
        }

        [Test]
        public void ImpactSequence_AdvancesAuthoredFramesAfterContact_AndKeepsSalvoTiming()
        {
            var graphic = Create(NavalVfxRole.Cannon, BattleSchoolAttackKind.Fire, false);
            graphic.SetProgress(.30f / graphic.Duration);
            Assert.That(SequenceRows(Populate(graphic)), Has.No.Member(1), "No impact frame before contact.");
            graphic.SetProgress(.43f / graphic.Duration);
            int[] early = SequenceColumns(Populate(graphic), 1).ToArray();
            graphic.SetProgress(.76f / graphic.Duration);
            int[] late = SequenceColumns(Populate(graphic), 1).ToArray();
            Assert.That(early, Is.Not.Empty);
            Assert.That(late, Is.Not.Empty);
            Assert.That(late.Min(), Is.GreaterThan(early.Max()), "The image changes across authored frames, not only scale/opacity.");
            graphic.Configure(NavalVfxRole.Cannon, BattleSchoolAttackKind.Fire, false, BattleSchoolAttackOutcome.Hit,
                new Vector2(-270f, 0f), new Vector2(270f, 0f), 4);
            Assert.That(graphic.Duration, Is.EqualTo(1.21f).Within(.00001f));
            Assert.That(graphic.Duration * graphic.ContactAt, Is.EqualTo(.63f).Within(.00001f),
                "Result labels still wait for the fourth shot.");
        }

        [TestCase(BattleSchoolAttackKind.Fire, 3)]
        [TestCase(BattleSchoolAttackKind.Arcane, 4)]
        [TestCase(BattleSchoolAttackKind.Curse, 5)]
        [TestCase(BattleSchoolAttackKind.Tide, 6)]
        [TestCase(BattleSchoolAttackKind.Thunder, 7)]
        [TestCase(BattleSchoolAttackKind.Blood, 8)]
        public void SchoolContact_UsesItsAuthoredSequence(BattleSchoolAttackKind school, int row)
        {
            var graphic = Create(NavalVfxRole.Strike, school);
            graphic.SetProgress(.58f);
            Assert.That(SequenceRows(Populate(graphic)), Does.Contain(row));
            graphic.Configure(NavalVfxRole.Strike, school, true, BattleSchoolAttackOutcome.Missed,
                new Vector2(-270f, 0f), new Vector2(270f, 0f));
            graphic.SetProgress(.65f);
            Assert.That(SequenceRows(Populate(graphic)), Does.Contain(2));
            Assert.That(SequenceRows(Populate(graphic)), Has.No.Member(row), "Misses do not fabricate a school hit.");
        }

        [Test]
        public void NavalAndBoardingUseTheirOwnAuthoredAtlases()
        {
            var naval = Create(NavalVfxRole.Cannon, BattleSchoolAttackKind.Fire, false);
            var go = Track(new GameObject("boarding compatibility", typeof(RectTransform), typeof(BoardingCombatFxGraphic)));
            var boarding = go.GetComponent<BoardingCombatFxGraphic>();
            boarding.Configure(BoardingCombatFxKind.Guard, Color.white);
            RawImage accent = go.GetComponentInChildren<RawImage>();
            Assert.That(accent, Is.Not.Null);
            Assert.That(accent.texture, Is.SameAs(Resources.Load<Texture2D>("BattleVfx/BoardingRemaster/boarding_fx_v3")));
            Assert.That(accent.texture, Is.Not.SameAs(naval.mainTexture));
            Assert.That(accent.uvRect.center.x, Is.EqualTo(.5f / 8f).Within(.001f));
            Assert.That(accent.uvRect.center.y, Is.EqualTo(1f - 2.5f / 12f).Within(.001f));
            boarding.Progress = .65f;
            Assert.That(accent.uvRect.center.x, Is.GreaterThan(.5f / 8f));
        }

        [Test]
        public void ExplicitFieldRegion_DrawsOnlyCommittedCells_AndEmptyRegionHasNoCentroidHit()
        {
            var graphic = Create(NavalVfxRole.Area, BattleSchoolAttackKind.Fire);
            var cells = new[] { new Vector2(-220f, -60f), new Vector2(170f, 60f) };
            graphic.ConfigureAreaTargets(cells, 80f);
            graphic.SetProgress(.6f);
            Mesh mesh = Populate(graphic);
            Assert.That(mesh.vertexCount, Is.GreaterThan(0));
            foreach (Vector3 p in mesh.vertices)
                Assert.That(cells.Any(c => Mathf.Abs(p.x - c.x) <= 40.01f && Mathf.Abs(p.y - c.y) <= 40.01f), Is.True,
                    "No visual coverage is invented between separate selected cells.");
            Assert.That(mesh.vertices.Any(p => p.x < 0), Is.True);
            Assert.That(mesh.vertices.Any(p => p.x > 0), Is.True);
            cells[0] = Vector2.zero;
            CollectionAssert.AreEqual(mesh.vertices, Populate(graphic).vertices, "The committed region is copied, not a mutable caller reference.");
            graphic.ConfigureAreaTargets(Array.Empty<Vector2>(), 80f);
            Assert.That(graphic.HasExplicitAreaTargets, Is.True);
            Assert.That(Populate(graphic).vertexCount, Is.Zero);
        }

        [Test]
        public void MaximumFieldBatch_RendersAll128CellsWithinOneBoundedMesh()
        {
            var graphic = Create(NavalVfxRole.Area, BattleSchoolAttackKind.Thunder);
            graphic.rectTransform.sizeDelta = new Vector2(2400f, 1600f);
            var cells = Enumerable.Range(0, 128).Select(i => new Vector2((i % 16 - 7.5f) * 100f, (i / 16 - 3.5f) * 100f)).ToArray();
            graphic.ConfigureAreaTargets(cells, 80f);
            graphic.SetProgress(.65f);
            Mesh mesh = Populate(graphic);
            Assert.That(mesh.vertexCount, Is.InRange(128 * 12, 6008));
            foreach (Vector2 cell in cells)
                Assert.That(mesh.vertices.Any(p => Mathf.Abs(p.x - cell.x) < 40.01f && Mathf.Abs(p.y - cell.y) < 40.01f), Is.True,
                    "Every committed cell must render, including the final budget cell.");
        }

        [Test]
        public void Timeline_UsesElapsedTimeAndCancelsOwnerDisableWithoutRestart()
        {
            var smallSteps = Create(NavalVfxRole.Strike, BattleSchoolAttackKind.Thunder);
            var largeStep = Create(NavalVfxRole.Strike, BattleSchoolAttackKind.Thunder);
            Canvas owner = Track(new GameObject("naval owner", typeof(Canvas))).GetComponent<Canvas>();
            smallSteps.Play(owner); largeStep.Play(owner);
            for (int i = 0; i < 30; i++) smallSteps.AdvancePresentation(.01f);
            largeStep.AdvancePresentation(.3f);
            Assert.That(smallSteps.Progress, Is.EqualTo(largeStep.Progress).Within(.00001f));
            Assert.That(smallSteps.IsPlaying, Is.True);
            owner.enabled = false;
            smallSteps.AdvancePresentation(.01f);
            Assert.That(smallSteps.IsPlaying, Is.False);
            Assert.That(Populate(smallSteps).vertexCount, Is.Zero);
            owner.enabled = true;
            smallSteps.AdvancePresentation(.01f);
            Assert.That(smallSteps.IsPlaying, Is.False);
            largeStep.AdvancePresentation(float.NaN);
            Assert.That(float.IsNaN(largeStep.Progress), Is.False);
            largeStep.AdvancePresentation(2f);
            Assert.That(largeStep.IsPlaying, Is.False);
            largeStep.SetProgress(float.NaN);
            Assert.That(largeStep.Progress, Is.Zero);
            largeStep.Configure(NavalVfxRole.Strike, BattleSchoolAttackKind.Fire, true, BattleSchoolAttackOutcome.Hit,
                new Vector2(float.NaN, float.PositiveInfinity), new Vector2(float.NegativeInfinity, float.NaN));
            Assert.That(largeStep.From, Is.EqualTo(Vector2.zero));
            Assert.That(largeStep.To, Is.EqualTo(Vector2.zero));
            largeStep.SetProgress(.5f);
            Assert.That(Populate(largeStep).vertices.All(p => !float.IsNaN(p.x) && !float.IsNaN(p.y)), Is.True);
        }

        private NavalCombatFxGraphic Create(NavalVfxRole role, BattleSchoolAttackKind school, bool hasSchool = true,
            BattleSchoolAttackOutcome outcome = BattleSchoolAttackOutcome.Hit)
        {
            var go = Track(new GameObject("naval fx test", typeof(RectTransform), typeof(NavalCombatFxGraphic)));
            var graphic = go.GetComponent<NavalCombatFxGraphic>();
            graphic.rectTransform.sizeDelta = new Vector2(900f, 360f);
            graphic.Configure(role, school, hasSchool, outcome, new Vector2(-270f, 0), new Vector2(270f, 0));
            return graphic;
        }

        private Mesh Populate(NavalCombatFxGraphic graphic)
        {
            Mesh mesh = Track(new Mesh());
            using (var helper = new VertexHelper())
            {
                typeof(NavalCombatFxGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null)
                    .Invoke(graphic, new object[] { helper });
                helper.FillMesh(mesh);
            }
            return mesh;
        }

        private static IEnumerable<int> Cells(Mesh mesh) => mesh.uv.Select(uv =>
            Mathf.Clamp(Mathf.FloorToInt(uv.x * 8f), 0, 7) + (10 - Mathf.Clamp(Mathf.FloorToInt(uv.y * 11f), 0, 10)) * 8).Distinct();
        private static IEnumerable<int> StaticSlots(Mesh mesh) => Cells(mesh).Where(cell => cell >= 72).Select(cell => cell - 72);
        private static IEnumerable<int> SequenceRows(Mesh mesh) => Cells(mesh).Where(cell => cell < 72).Select(cell => cell / 8).Distinct();
        private static IEnumerable<int> SequenceColumns(Mesh mesh, int row) => Cells(mesh).Where(cell => cell / 8 == row).Select(cell => cell % 8).Distinct();
        private T Track<T>(T value) where T : Object { temporary.Add(value); return value; }
    }
}
