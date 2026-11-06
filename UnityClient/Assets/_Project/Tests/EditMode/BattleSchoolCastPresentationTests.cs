using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Core.Tests
{
    public sealed class BattleSchoolCastPresentationTests
    {
        private readonly List<Object> temporary = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            for (int index = temporary.Count - 1; index >= 0; index--)
                if (temporary[index] != null) Object.DestroyImmediate(temporary[index]);
            temporary.Clear();
        }

        // Expectations are the visible intent of every authored boarding card, including
        // mixed guard/debuff cards and delayed attacks that must not look like a hit.
        private static IEnumerable<TestCaseData> AuthoredCasts()
        {
            string[] schools = { "fire", "arcane", "curse", "tide", "thunder", "blood" };
            string[] roles =
            {
                "Attack Attack Affliction Affliction Heal Attack Affliction Guard Empower Empower Attack Empower Guard Empower Affliction",
                "Attack Guard Empower Attack Heal Attack Attack Guard Empower Empower Charge Empower Empower Empower Empower",
                "Attack Affliction Affliction Empower Empower Attack Affliction Affliction Affliction Affliction Attack Heal Empower Empower Guard",
                "Guard Guard Guard Attack Empower Attack Attack Attack Affliction Attack Heal Heal Empower Empower Guard",
                "Attack Attack Attack Attack Charge Empower Empower Empower Empower Empower Attack Attack Attack Attack Attack",
                "Attack Attack Guard Empower Empower Attack Attack Empower Heal Attack Attack Guard Charge Empower Empower",
            };
            for (int school = 0; school < schools.Length; school++)
            {
                string[] expected = roles[school].Split(' ');
                for (int index = 0; index < expected.Length; index++)
                    yield return new TestCaseData(schools[school] + "_" + (index + 1).ToString("00"),
                        (BattleSchoolAttackKind)school,
                        (BoardingSchoolCastRole)System.Enum.Parse(typeof(BoardingSchoolCastRole), expected[index]));
            }
        }

        [TestCaseSource(nameof(AuthoredCasts))]
        public void EveryAuthoredSchoolCard_HasItsExpectedCastIntent(string key, BattleSchoolAttackKind expectedSchool,
            BoardingSchoolCastRole expectedRole)
        {
            CoreBattleCard card = CoreBoardingCardCatalog.FindByKey(key);
            Assert.That(card, Is.Not.Null, key);
            Assert.That(BattleSchoolAttackPresentation.TryBoardingCast(card, out var school, out var role), Is.True);
            Assert.That(school, Is.EqualTo(expectedSchool), key);
            Assert.That(role, Is.EqualTo(expectedRole), key);
            Assert.That(BattleSchoolAttackPresentation.TargetsEnemy(role),
                Is.EqualTo(expectedRole == BoardingSchoolCastRole.Attack || expectedRole == BoardingSchoolCastRole.Affliction));
        }

        [TestCase("arcane_11")]
        [TestCase("thunder_05")]
        [TestCase("blood_13")]
        public void QueuedAttack_OnlyChargesEvenWhenAnotherEffectDealsDamage(string key)
        {
            CoreBattleCard card = CoreBoardingCardCatalog.FindByKey(key);
            Assert.That(BattleSchoolAttackPresentation.TryBoardingCast(card, out _, out var role), Is.True);
            Assert.That(role, Is.EqualTo(BoardingSchoolCastRole.Charge));
            Assert.That(BattleSchoolAttackPresentation.TargetsEnemy(role), Is.False);
            Assert.That(BattleSchoolAttackPresentation.TryPlayer(card, true, 20, 12, out _, out _), Is.False,
                "A passive effect or a resolved queue must not turn a newly queued attack into an immediate hit.");
        }

        [Test]
        public void CurseFinisher_UsesCurseContactButOtherAfflictionsDoNotInventAttacks()
        {
            var finisher = CoreBoardingCardCatalog.FindByKey("curse_11");
            Assert.That(finisher.BoardingSpec.IsAttack, Is.False, "Curse spell damage intentionally is not an attack in the rules.");
            Assert.That(BattleSchoolAttackPresentation.TryPlayer(finisher, true, 48, 0, out var school, out var outcome), Is.True);
            Assert.That(school, Is.EqualTo(BattleSchoolAttackKind.Curse));
            Assert.That(outcome, Is.EqualTo(BattleSchoolAttackOutcome.Hit));
            Assert.That(BattleSchoolAttackPresentation.TryPlayer(finisher, true, 0, 0, out _, out _), Is.False);
            foreach (string key in new[] { "fire_03", "fire_07", "curse_02", "curse_03", "curse_08", "curse_10", "arcane_03", "arcane_15" })
                Assert.That(BattleSchoolAttackPresentation.TryPlayer(CoreBoardingCardCatalog.FindByKey(key), true,
                    23, 4, out _, out _), Is.False, key + " must not claim unrelated passive or queued damage.");
        }

        [Test]
        public void StableBoardingSpec_WinsOverTranslatedNameTypeAndSourceIdentity()
        {
            var canonical = CoreBoardingCardCatalog.FindByKey("curse_03");
            var renamed = new CoreBattleCard("source_crew_test", "Any localized name", "Unrelated localized type", 1,
                0, canonical.Block, 0, 0, 0, "No rule text is parsed", NavalBattleStages.Boarding,
                schoolId: "curse", boardingSpec: canonical.BoardingSpec);
            Assert.That(BattleSchoolAttackPresentation.TryBoardingCast(renamed, out var school, out var role), Is.True);
            Assert.That(school, Is.EqualTo(BattleSchoolAttackKind.Curse));
            Assert.That(role, Is.EqualTo(BoardingSchoolCastRole.Affliction), "The target receives a curse as well as the player receiving guard.");

            // A shared legacy identity is normalized to its boarding definition before classification.
            var legacy = new CoreBattleCard("card_school_arcane_b09", "Legacy name", "Old type", 1,
                99, 0, 0, 0, 0, "", NavalBattleStages.All, schoolId: "arcane");
            Assert.That(BattleSchoolAttackPresentation.TryBoardingCast(legacy, out _, out var legacyRole), Is.True);
            Assert.That(legacyRole, Is.EqualTo(BoardingSchoolCastRole.Guard));
        }

        [TestCase("fire")]
        [TestCase("arcane")]
        [TestCase("curse")]
        [TestCase("tide")]
        [TestCase("thunder")]
        [TestCase("blood")]
        public void SharedFieldCard_UsesFieldCastEvenWhenItAlsoHasDamage(string schoolId)
        {
            var card = new CoreBattleCard("field_presentation_probe", "Field", "Field", 1, 8, 3, 0, 0, 0, "",
                NavalBattleStages.All, schoolId: schoolId,
                schoolEffect: new CoreSchoolCardEffect(CoreSchoolMechanic.ComponentFieldExpand));
            Assert.That(BattleSchoolAttackPresentation.TryBoardingCast(card, out _, out var role), Is.True);
            Assert.That(role, Is.EqualTo(BoardingSchoolCastRole.Field));
            Assert.That(BattleSchoolAttackPresentation.TargetsEnemy(role), Is.False);
        }

        [Test]
        public void NeutralUnknownAndNavalOnlyCards_DoNotGetBoardingSchoolEffects()
        {
            Assert.That(BattleSchoolAttackPresentation.TryBoardingCast(null, out _, out _), Is.False);
            Assert.That(BattleSchoolAttackPresentation.TryBoardingCast(CoreBoardingCardCatalog.FindByKey("basic_01"), out _, out _), Is.False);
            foreach (var card in new[] {
                new CoreBattleCard("unknown", "Fire", "Attack", 1, 8, 0, 0, 0, 0, "", schoolId: "unknown"),
                new CoreBattleCard("naval", "Fire", "Attack", 1, 8, 0, 0, 0, 0, "", NavalBattleStages.Ship, schoolId: "fire") })
                Assert.That(BattleSchoolAttackPresentation.TryBoardingCast(card, out _, out _), Is.False);
        }

        private static IEnumerable<TestCaseData> SchoolRoles()
        {
            foreach (BattleSchoolAttackKind school in System.Enum.GetValues(typeof(BattleSchoolAttackKind)))
                foreach (BoardingSchoolCastRole role in System.Enum.GetValues(typeof(BoardingSchoolCastRole)))
                    yield return new TestCaseData(school, role);
        }

        [TestCaseSource(nameof(SchoolRoles))]
        public void CastMesh_IsFiniteBoundedDeterministicAndDoesNotConsumeCombatRandomness(BattleSchoolAttackKind school,
            BoardingSchoolCastRole role)
        {
            BoardingSchoolCastGraphic graphic = CreateGraphic(school, role);
            Assert.That(graphic.Kind, Is.EqualTo(school));
            Assert.That(graphic.Role, Is.EqualTo(role));
            Assert.That(graphic.raycastTarget, Is.False);
            foreach (float progress in new[] { .15f, .45f, .75f, 1f })
            {
                graphic.SetProgress(progress);
                UnityEngine.Random.State random = UnityEngine.Random.state;
                Mesh first = Populate(graphic), second = Populate(graphic);
                Assert.That(first.vertexCount, Is.LessThan(3000), school + "/" + role);
                if (role == BoardingSchoolCastRole.Attack) Assert.That(first.vertexCount, Is.Zero, "Attack contact owns its full presentation; no duplicate cast emblem.");
                else if (progress < .5f) Assert.That(first.vertexCount, Is.GreaterThan(0));
                if (progress == 1f) Assert.That(first.vertexCount, Is.Zero);
                foreach (Vector3 point in first.vertices)
                {
                    Assert.That(float.IsNaN(point.x) || float.IsInfinity(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.y), Is.False);
                    Assert.That(point.x, Is.InRange(-450f, 450f));
                    Assert.That(point.y, Is.InRange(-180f, 180f));
                }
                CollectionAssert.AreEqual(first.vertices, second.vertices);
                CollectionAssert.AreEqual(first.colors32, second.colors32);
                Assert.That(UnityEngine.Random.state, Is.EqualTo(random));
            }
        }

        [Test]
        public void CastLifetime_ExplicitCancelOwnerDisableAndCompletionLeaveNoVisibleMesh()
        {
            BoardingSchoolCastGraphic graphic = CreateGraphic(BattleSchoolAttackKind.Arcane, BoardingSchoolCastRole.Charge);
            Canvas owner = Track(new GameObject("cast owner", typeof(Canvas))).GetComponent<Canvas>();
            graphic.Play(owner);
            graphic.AdvancePresentation(.2f);
            Assert.That(graphic.IsPlaying, Is.True);
            Assert.That(Populate(graphic).vertexCount, Is.GreaterThan(0));
            graphic.Cancel();
            AssertCancelled(graphic);
            graphic.AdvancePresentation(.1f);
            AssertCancelled(graphic);

            graphic.Play(owner);
            owner.enabled = false;
            graphic.AdvancePresentation(.01f);
            AssertCancelled(graphic);
            owner.enabled = true;
            graphic.AdvancePresentation(.1f);
            AssertCancelled(graphic);

            graphic.Play(owner);
            graphic.AdvancePresentation(BoardingSchoolCastGraphic.Duration + .01f);
            AssertCancelled(graphic);
            graphic.SetProgress(float.NaN);
            Assert.That(graphic.Progress, Is.Zero);
            graphic.SetProgress(float.PositiveInfinity);
            Assert.That(graphic.Progress, Is.Zero);
        }

        private void AssertCancelled(BoardingSchoolCastGraphic graphic)
        {
            Assert.That(graphic.IsPlaying, Is.False);
            Assert.That(graphic.Progress, Is.EqualTo(1f));
            Assert.That(Populate(graphic).vertexCount, Is.Zero);
        }

        private BoardingSchoolCastGraphic CreateGraphic(BattleSchoolAttackKind school, BoardingSchoolCastRole role)
        {
            var graphic = Track(new GameObject("boarding cast test", typeof(RectTransform), typeof(BoardingSchoolCastGraphic)))
                .GetComponent<BoardingSchoolCastGraphic>();
            graphic.rectTransform.sizeDelta = new Vector2(900f, 360f);
            graphic.Configure(school, role, new Vector2(0f, -30f),
                BattleSchoolAttackPresentation.TargetsEnemy(role) ? new Vector2(270f, 0f) : new Vector2(-270f, 0f));
            return graphic;
        }

        private Mesh Populate(BoardingSchoolCastGraphic graphic)
        {
            Mesh mesh = Track(new Mesh());
            using (var helper = new VertexHelper())
            {
                typeof(BoardingSchoolCastGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { helper });
                helper.FillMesh(mesh);
            }
            return mesh;
        }

        private T Track<T>(T value) where T : Object { temporary.Add(value); return value; }
    }
}
