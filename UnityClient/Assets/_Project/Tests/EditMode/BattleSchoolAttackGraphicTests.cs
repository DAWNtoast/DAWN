using System;
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
    public sealed class BattleSchoolAttackGraphicTests
    {
        private readonly List<Object> temporary = new List<Object>();
        [TearDown] public void Cleanup()
        {
            for (int i = temporary.Count - 1; i >= 0; i--) if (temporary[i] != null) Object.DestroyImmediate(temporary[i]);
            temporary.Clear();
        }

        [TestCaseSource(nameof(Schools))]
        public void SchoolTrajectoriesAndContact_AreFiniteBoundedDeterministicAndNoninteractive(BattleSchoolAttackKind school)
        {
            foreach (BattleSchoolAttackOutcome outcome in Enum.GetValues(typeof(BattleSchoolAttackOutcome)))
            {
                BattleSchoolAttackGraphic graphic = Create(school, outcome);
                Assert.That(graphic.raycastTarget, Is.False);
                foreach (float progress in new[] { .15f, .45f, .7f, .98f })
                {
                    graphic.SetProgress(progress);
                    UnityEngine.Random.State random = UnityEngine.Random.state;
                    Mesh mesh = Populate(graphic);
                    Assert.That(mesh.vertexCount, Is.InRange(1, 1600));
                    foreach (Vector3 point in mesh.vertices)
                    {
                        Assert.That(float.IsNaN(point.x) || float.IsInfinity(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.y), Is.False);
                        Assert.That(point.x, Is.InRange(-450f, 450f));
                        Assert.That(point.y, Is.InRange(-180f, 180f));
                    }
                    CollectionAssert.AreEqual(mesh.vertices, Populate(graphic).vertices);
                    Assert.That(UnityEngine.Random.state, Is.EqualTo(random));
                }
                graphic.SetProgress(1f);
                Assert.That(Populate(graphic).vertexCount, Is.Zero);
            }
        }

        [Test]
        public void SchoolGeometryAndOutcomes_AreDistinctWithoutChangingOnlyTheColor()
        {
            var signatures = new HashSet<string>();
            foreach (BattleSchoolAttackKind school in Schools)
            {
                BattleSchoolAttackGraphic graphic = Create(school, BattleSchoolAttackOutcome.Hit);
                graphic.SetProgress(.65f);
                Assert.That(signatures.Add(string.Join(";", Populate(graphic).vertices.Select(v => v.ToString("F2")))), Is.True,
                    school + " must have its own trajectory and impact geometry.");
            }
            BattleSchoolAttackGraphic hit = Create(BattleSchoolAttackKind.Arcane, BattleSchoolAttackOutcome.Hit);
            BattleSchoolAttackGraphic block = Create(BattleSchoolAttackKind.Arcane, BattleSchoolAttackOutcome.Guarded);
            BattleSchoolAttackGraphic miss = Create(BattleSchoolAttackKind.Arcane, BattleSchoolAttackOutcome.Missed);
            hit.SetProgress(.65f); block.SetProgress(.65f); miss.SetProgress(.65f);
            CollectionAssert.AreNotEqual(Populate(hit).vertices, Populate(block).vertices);
            Assert.That(Vector2.Distance(hit.ContactPoint, miss.ContactPoint), Is.GreaterThan(60f));
            Assert.That(Populate(miss).vertexCount, Is.LessThan(Populate(hit).vertexCount), "A miss has no target contact seal.");
        }

        [Test]
        public void LifetimeAndOwnerDisable_CancelWithoutReplayOrNonfiniteProgress()
        {
            BattleSchoolAttackGraphic graphic = Create(BattleSchoolAttackKind.Thunder, BattleSchoolAttackOutcome.Hit);
            Canvas owner = Track(new GameObject("school owner", typeof(Canvas))).GetComponent<Canvas>();
            graphic.Play(owner);
            graphic.AdvancePresentation(.2f);
            Assert.That(graphic.IsPlaying, Is.True);
            owner.enabled = false;
            graphic.AdvancePresentation(.01f);
            Assert.That(graphic.IsPlaying, Is.False);
            Assert.That(graphic.Progress, Is.EqualTo(1f));
            Assert.That(Populate(graphic).vertexCount, Is.Zero);
            owner.enabled = true;
            graphic.AdvancePresentation(.1f);
            Assert.That(graphic.IsPlaying, Is.False);
            graphic.SetProgress(float.NaN); Assert.That(graphic.Progress, Is.Zero);
            graphic.SetProgress(float.PositiveInfinity); Assert.That(graphic.Progress, Is.Zero);
            graphic.Play(owner); graphic.AdvancePresentation(BattleSchoolAttackGraphic.Duration + .01f);
            Assert.That(graphic.IsPlaying, Is.False);
        }

        [Test]
        public void Classification_RejectsNeutralBuffsAndUnresolvedDelayedAttacks()
        {
            foreach (string key in new[] { "basic_01", "arcane_03", "arcane_11", "fire_03", "thunder_05", "tide_01", "blood_03", "curse_08" })
                Assert.That(BattleSchoolAttackPresentation.TryPlayer(CoreBoardingCardCatalog.FindByKey(key), true,
                    9, 7, out _, out _), Is.False, key + " must not impersonate an immediate school attack.");
            foreach (string key in new[] { "fire_01", "arcane_01", "curse_01", "tide_04", "thunder_02", "blood_01" })
            {
                var card = CoreBoardingCardCatalog.FindByKey(key);
                Assert.That(BattleSchoolAttackPresentation.TryPlayer(card, true, 4, 8, out _, out var hit), Is.True);
                Assert.That(hit, Is.EqualTo(BattleSchoolAttackOutcome.Hit));
                Assert.That(BattleSchoolAttackPresentation.TryPlayer(card, true, 0, 8, out _, out var block), Is.True);
                Assert.That(block, Is.EqualTo(BattleSchoolAttackOutcome.Guarded));
                Assert.That(BattleSchoolAttackPresentation.TryPlayer(card, true, 0, 0, out _, out var miss), Is.True);
                Assert.That(miss, Is.EqualTo(BattleSchoolAttackOutcome.Missed));
            }
            var action = new CoreBoardingEnemyActionResult();
            Assert.That(BattleSchoolAttackPresentation.TryEnemy("fire", action, out _, out _), Is.False);
            typeof(CoreBoardingEnemyActionResult).GetProperty("RawAttack").SetValue(action, 8);
            typeof(CoreBoardingEnemyActionResult).GetProperty("AbsorbedDamage").SetValue(action, 8);
            Assert.That(BattleSchoolAttackPresentation.TryEnemy("fire", action, out _, out var enemyBlock), Is.True);
            Assert.That(enemyBlock, Is.EqualTo(BattleSchoolAttackOutcome.Guarded));
            Assert.That(BattleSchoolAttackPresentation.TryEnemy("neutral", action, out _, out _), Is.False);
        }

        [Test]
        public void ZeroPrintedDamage_RealResolveOldestDelayedCreatesOneArcaneContactOnlyWhenItLands()
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            session.SetSeed("SCHOOL-ATTACK-DELAYED-RESOLUTION");
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, 1000);
            Assert.That(session.EndBattleTurn(), Is.True);
            session.Battle.DistanceMeters = 100f;
            typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(session.Battle, session.Battle.EnemyHeadingDegrees);
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(session.TryBoard(), Is.True);
            typeof(CoreVoyageSession).GetProperty("Morale").SetValue(session, 0);
            session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth = 1000;
            session.Battle.EnemyBoardingHealth = session.Battle.EnemyMaxBoardingHealth = 10000;
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 10000;
            session.Battle.PlayerBlock = session.Battle.EnemyBlock = 0;
            session.Battle.Hand.Clear(); session.Battle.DrawPile.Clear(); session.Battle.DiscardPile.Clear();
            foreach (string key in new[] { "arcane_11", "arcane_03", "arcane_15" })
                session.Battle.Hand.Add(Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                    .Single(i => CoreVoyageSession.CardLibrary[i].Id == "card_boarding_" + key));
            session.Battle.Energy = 3;
            typeof(CoreSchoolBattleState).GetProperty("Mana").SetValue(session.Battle.Schools, 0);
            typeof(CoreSchoolBattleState).GetProperty("TemporaryMana").SetValue(session.Battle.Schools, 0);
            int before = session.Battle.EnemyBoardingHealth;
            Assert.That(session.PlayCard(0), Is.True); // Queue 36; no attack yet.
            Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(before));
            Assert.That(session.PlayCard(0), Is.True); // Actual gain of 3 mana.
            CoreBattleCard resolve = session.GetBattleCard(session.Battle.Hand[0]);
            Assert.That(resolve.Damage, Is.Zero);
            Assert.That(BattleSchoolAttackPresentation.TryPlayer(resolve, true, 0, 0, out _, out _), Is.False);
            Assert.That(session.PlayCard(0), Is.True);
            int actual = before - session.Battle.EnemyBoardingHealth;
            Assert.That(actual, Is.EqualTo(36));
            Assert.That(BattleSchoolAttackPresentation.TryPlayer(resolve, true, actual, 0, out _, out _), Is.False,
                "The resolver is not an additional Arcane attack; the queued source supplies the one contact.");
            Assert.That(session.Battle.Boarding.ResolvedAttacks.Count, Is.EqualTo(1));
            Assert.That(BattleSchoolAttackPresentation.TryResolved(session.Battle.Boarding.ResolvedAttacks[0], out var school, out var outcome), Is.True);
            Assert.That(school, Is.EqualTo(BattleSchoolAttackKind.Arcane));
            Assert.That(outcome, Is.EqualTo(BattleSchoolAttackOutcome.Hit));
            Assert.That(session.Battle.Boarding.DelayedDamage, Is.Empty);
        }

        private static IEnumerable<BattleSchoolAttackKind> Schools => (BattleSchoolAttackKind[])Enum.GetValues(typeof(BattleSchoolAttackKind));
        private BattleSchoolAttackGraphic Create(BattleSchoolAttackKind school, BattleSchoolAttackOutcome outcome)
        {
            GameObject go = Track(new GameObject("school graphic test", typeof(RectTransform), typeof(BattleSchoolAttackGraphic)));
            var graphic = go.GetComponent<BattleSchoolAttackGraphic>();
            graphic.rectTransform.sizeDelta = new Vector2(900f, 360f);
            graphic.Configure(school, outcome, new Vector2(-270f, 0), new Vector2(270f, 0), false);
            return graphic;
        }
        private Mesh Populate(BattleSchoolAttackGraphic graphic)
        {
            Mesh mesh = Track(new Mesh());
            using (var helper = new VertexHelper())
            {
                typeof(BattleSchoolAttackGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { helper });
                helper.FillMesh(mesh);
            }
            return mesh;
        }
        private T Track<T>(T value) where T : Object { temporary.Add(value); return value; }
    }
}
