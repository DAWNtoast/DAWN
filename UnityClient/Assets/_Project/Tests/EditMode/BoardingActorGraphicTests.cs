using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Core.Tests
{
    public sealed class BoardingActorGraphicTests
    {
        private readonly List<Object> temporary = new List<Object>();
        private const float Foot = .04f;
        private const float LockedTop = Foot + .10f * (1f - Foot);

        [TearDown]
        public void Cleanup()
        {
            for (int i = temporary.Count - 1; i >= 0; i--)
                if (temporary[i] != null) Object.DestroyImmediate(temporary[i]);
            temporary.Clear();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FeetStayPlantedDuringIdleAndEveryShortAction(bool formation)
        {
            BoardingActorGraphic actor = Create(formation);
            foreach (BoardingActorAction action in Enum.GetValues(typeof(BoardingActorAction)))
            {
                actor.PlayAction(BoardingActorAction.Idle);
                actor.SetPresentationTime(1.37f);
                if (action != BoardingActorAction.Idle) actor.PlayAction(action, .6f);
                actor.AdvancePresentation(.24f);
                foreach (float x in new[] { 0f, .12f, .22f, .38f, .52f, .67f, .82f, 1f })
                    foreach (float y in new[] { 0f, Foot, .08f, LockedTop - .0001f })
                        Assert.That(actor.SampleOffset(new Vector2(x, y)).sqrMagnitude, Is.LessThan(.000001f),
                            action + " must not lift or slide the boot-contact region at " + x + ", " + y);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LongIdleKeepsEveryVertexClockAndRectTransformCompletelyStill(bool formation)
        {
            BoardingActorGraphic actor = Create(formation);
            RectTransform rect = actor.GetComponent<RectTransform>();
            Vector3 position = rect.localPosition, scale = rect.localScale;
            Quaternion rotation = rect.localRotation;
            Mesh initial = Populate(actor);
            int dirtyCount = 0;
            actor.GetComponent<RawImage>().RegisterDirtyVerticesCallback(() => dirtyCount++);
            float clock = actor.Clock;
            foreach (float elapsed in new[] { .016f, .5f, 10f, 100f, 3600f })
            {
                actor.AdvancePresentation(elapsed);
                Assert.That(actor.Action, Is.EqualTo(BoardingActorAction.Idle));
                Assert.That(actor.Clock, Is.EqualTo(clock), "Waiting without a card action must not advance an idle animation clock.");
                CollectionAssert.AreEqual(initial.vertices, Populate(actor).vertices, "Idle must preserve the exact resting artwork mesh.");
                foreach (Vector2 point in UpperBodyPoints) Assert.That(actor.SampleOffset(point), Is.EqualTo(Vector2.zero));
            }
            Assert.That(dirtyCount, Is.Zero, "An idle frame must not request a mesh rebuild.");
            Assert.That(rect.localPosition, Is.EqualTo(position));
            Assert.That(rect.localScale, Is.EqualTo(scale));
            Assert.That(rect.localRotation, Is.EqualTo(rotation));
        }

        [Test]
        public void FormationActionDeformationRemainsContinuousAcrossNeighbouringCharacters()
        {
            BoardingActorGraphic actor = Create(true);
            foreach (BoardingActorAction action in Enum.GetValues(typeof(BoardingActorAction)))
            {
                if (action == BoardingActorAction.Idle) continue;
                actor.PlayAction(BoardingActorAction.Idle);
                actor.PlayAction(action, .6f); actor.AdvancePresentation(.24f);
                foreach (float seam in new[] { .33f, .37f, .66f, .67f })
                {
                    Vector2 a = actor.SampleOffset(new Vector2(seam - .0001f, .83f));
                    Vector2 b = actor.SampleOffset(new Vector2(seam + .0001f, .83f));
                    Assert.That(Vector2.Distance(a, b), Is.LessThan(.12f), action + " must not tear a weapon or coat at an influence boundary.");
                }
            }
        }
        [TestCase(BoardingActorAction.Attack)]
        [TestCase(BoardingActorAction.Cast)]
        [TestCase(BoardingActorAction.Hit)]
        [TestCase(BoardingActorAction.Guard)]
        [TestCase(BoardingActorAction.Heal)]
        [TestCase(BoardingActorAction.Prepare)]
        public void ActionChangesTheBodyAndReturnsToTheExactRestingPose(BoardingActorAction action)
        {
            BoardingActorGraphic actor = Create(true);
            BoardingActorGraphic idle = Create(true);
            actor.SetPresentationTime(2f); idle.SetPresentationTime(2f);
            Mesh resting = Populate(actor);
            actor.PlayAction(action, .6f);
            actor.AdvancePresentation(.24f); idle.AdvancePresentation(.24f);
            float largestDifference = 0f;
            foreach (Vector2 point in UpperBodyPoints)
                largestDifference = Mathf.Max(largestDifference, Vector2.Distance(actor.SampleOffset(point), idle.SampleOffset(point)));
            Assert.That(largestDifference, Is.GreaterThan(.1f), action + " must have a body response distinct from idle.");
            actor.AdvancePresentation(.5f);
            Assert.That(actor.Action, Is.EqualTo(BoardingActorAction.Idle), "A finished response must not hold or replay the attack pose.");
            CollectionAssert.AreEqual(resting.vertices, Populate(actor).vertices);
            foreach (Vector2 point in UpperBodyPoints) Assert.That(actor.SampleOffset(point), Is.EqualTo(Vector2.zero));
            float stoppedClock = actor.Clock; actor.AdvancePresentation(30f);
            Assert.That(actor.Clock, Is.EqualTo(stoppedClock));
            CollectionAssert.AreEqual(resting.vertices, Populate(actor).vertices);
        }

        [Test]
        public void OpposingPartiesMirrorActionLeanAndRecoilWithoutChangingVerticalMotion()
        {
            BoardingActorGraphic right = Create(false, 1f), left = Create(false, -1f), idle = Create(false);
            Vector2 sample = new Vector2(.52f, .80f);
            foreach (BoardingActorAction action in new[] { BoardingActorAction.Attack, BoardingActorAction.Cast,
                BoardingActorAction.Hit, BoardingActorAction.Guard, BoardingActorAction.Heal })
            {
                right.PlayAction(BoardingActorAction.Idle); left.PlayAction(BoardingActorAction.Idle);
                right.SetPresentationTime(2f); left.SetPresentationTime(2f); idle.SetPresentationTime(2f);
                right.PlayAction(action, .6f); left.PlayAction(action, .6f);
                right.AdvancePresentation(.24f); left.AdvancePresentation(.24f); idle.AdvancePresentation(.24f);
                Vector2 baseline = idle.SampleOffset(sample);
                Vector2 forward = right.SampleOffset(sample) - baseline;
                Vector2 reverse = left.SampleOffset(sample) - baseline;
                if (action == BoardingActorAction.Attack || action == BoardingActorAction.Hit)
                    Assert.That(Mathf.Abs(forward.x), Is.GreaterThan(.1f), action + " needs a visible directional reaction.");
                Assert.That(reverse.x, Is.EqualTo(-forward.x).Within(.001f), action + " must reverse with the attacking side.");
                Assert.That(reverse.y, Is.EqualTo(forward.y).Within(.001f), "Mirroring the parties must not invert crouching or healing lift.");
            }
        }
        [TestCase(false)]
        [TestCase(true)]
        public void MeshRetainsAtlasUvAndColorWhileItsVerticesAnimate(bool mirrored)
        {
            BoardingActorGraphic actor = Create(false);
            RawImage image = actor.GetComponent<RawImage>();
            image.color = new Color(.72f, .83f, .91f, .63f);
            image.uvRect = mirrored ? new Rect(.65f, .515f, -.31f, .47f) : new Rect(.34f, .515f, .31f, .47f);
            actor.PlayAction(BoardingActorAction.Attack, .6f);
            actor.AdvancePresentation(.12f);
            Mesh first = Populate(actor);
            actor.AdvancePresentation(.15f);
            Mesh second = Populate(actor);
            Assert.That(first.vertexCount, Is.GreaterThan(4), "A quad can only move the whole card; articulated motion needs interior vertices.");
            Assert.That(first.vertexCount, Is.LessThanOrEqualTo(8192));
            Assert.That(second.vertexCount, Is.EqualTo(first.vertexCount));
            CollectionAssert.AreEqual(first.uv, second.uv, "Motion must not scroll into adjacent atlas characters.");
            CollectionAssert.AreEqual(first.colors32, second.colors32);
            CollectionAssert.AreNotEqual(first.vertices, second.vertices);
            float minU = Mathf.Min(image.uvRect.x, image.uvRect.x + image.uvRect.width);
            float maxU = Mathf.Max(image.uvRect.x, image.uvRect.x + image.uvRect.width);
            foreach (Vector2 uv in second.uv)
            {
                Assert.That(uv.x, Is.InRange(minU - .00001f, maxU + .00001f));
                Assert.That(uv.y, Is.InRange(.515f - .00001f, .985f + .00001f));
            }
            foreach (Color32 color in second.colors32)
                Assert.That(color, Is.EqualTo((Color32)image.color), "Keep deck lighting and defeat fade supplied by the original RawImage.");
        }

        [Test]
        public void ActionMeshesAreFiniteDeterministicAndDoNotConsumeCombatRandom()
        {
            BoardingActorGraphic actor = Create(true);
            UnityEngine.Random.State random = UnityEngine.Random.state;
            foreach (BoardingActorAction action in Enum.GetValues(typeof(BoardingActorAction)))
                foreach (float progress in new[] { 0f, .12f, .36f, .78f })
                {
                    actor.PlayAction(BoardingActorAction.Idle);
                    actor.PlayAction(action, .6f); actor.AdvancePresentation(progress * .6f);
                    Mesh first = Populate(actor), second = Populate(actor);
                    CollectionAssert.AreEqual(first.vertices, second.vertices);
                    CollectionAssert.AreEqual(first.uv, second.uv);
                    foreach (Vector3 point in first.vertices)
                    {
                        Assert.That(float.IsNaN(point.x) || float.IsInfinity(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.y), Is.False);
                        Assert.That(point.x, Is.InRange(-340f, 340f));
                        Assert.That(point.y, Is.InRange(-170f, 180f));
                    }
                }
            Assert.That(UnityEngine.Random.state, Is.EqualTo(random));
        }

        [Test]
        public void EachCardActionHasItsOwnBodyPoseInsteadOfAUniversalWobble()
        {
            BoardingActorGraphic actor = Create(true);
            var poses = new Dictionary<BoardingActorAction, Vector2[]>();
            foreach (BoardingActorAction action in Enum.GetValues(typeof(BoardingActorAction)))
            {
                if (action == BoardingActorAction.Idle) continue;
                actor.PlayAction(BoardingActorAction.Idle);
                actor.PlayAction(action, .6f); actor.AdvancePresentation(.24f);
                var pose = new Vector2[UpperBodyPoints.Length];
                for (int i = 0; i < pose.Length; i++) pose[i] = actor.SampleOffset(UpperBodyPoints[i]);
                foreach (var previous in poses)
                {
                    float largestDifference = 0f;
                    for (int i = 0; i < pose.Length; i++) largestDifference = Mathf.Max(largestDifference, Vector2.Distance(pose[i], previous.Value[i]));
                    Assert.That(largestDifference, Is.GreaterThan(.1f), action + " must be visibly distinct from " + previous.Key);
                }
                poses.Add(action, pose);
            }
        }

        [Test]
        public void BladeDrawsBackThenCutsDownWhileSpellRaisesHandsThenReleasesForward()
        {
            BoardingActorGraphic actor = Create(false);
            Vector2 hand = new Vector2(.5f, Foot + .47f * (1f - Foot));
            actor.PlayAction(BoardingActorAction.Attack, 1f);
            actor.SetPresentationTime(.18f);
            Vector2 drawnBlade = actor.SampleOffset(hand);
            actor.SetPresentationTime(.52f);
            Vector2 cuttingBlade = actor.SampleOffset(hand);
            Assert.That(drawnBlade.x, Is.LessThan(-.1f), "The blade first draws back.");
            Assert.That(drawnBlade.y, Is.GreaterThan(.1f));
            Assert.That(cuttingBlade.x, Is.GreaterThan(.1f), "The strike drives toward the opponent.");
            Assert.That(cuttingBlade.y, Is.LessThan(-.1f), "The blade follows a downward cutting arc.");

            actor.PlayAction(BoardingActorAction.Idle);
            actor.PlayAction(BoardingActorAction.Cast, 1f);
            actor.SetPresentationTime(.4f);
            Vector2 gatheredSpell = actor.SampleOffset(hand);
            actor.SetPresentationTime(.7f);
            Vector2 releasedSpell = actor.SampleOffset(hand);
            Assert.That(gatheredSpell.y, Is.GreaterThan(.1f), "Spell gathering lifts the hands.");
            Assert.That(releasedSpell.y, Is.GreaterThan(.1f), "A spell release stays raised rather than cutting down.");
            Assert.That(releasedSpell.x, Is.GreaterThan(gatheredSpell.x + .1f), "Casting has a separate forward release.");
        }

        [Test]
        public void ActionPoseDoesNotDependOnIdleClockOrLegacyBreathingPhase()
        {
            BoardingActorGraphic first = Create(true), later = Create(true);
            first.Configure(true, 1f, .1f, Foot); later.Configure(true, 1f, 4.7f, Foot);
            first.SetPresentationTime(0f); later.SetPresentationTime(1500f);
            first.PlayAction(BoardingActorAction.Cast, .6f); later.PlayAction(BoardingActorAction.Cast, .6f);
            first.AdvancePresentation(.24f); later.AdvancePresentation(.24f);
            foreach (Vector2 point in UpperBodyPoints)
                Assert.That(Vector2.Distance(first.SampleOffset(point), later.SampleOffset(point)), Is.LessThan(.00001f),
                    "Action timing must come from the card, not a hidden continuous breathing cycle.");
        }
        [Test]
        public void SuspensionStopsMotionAndClearsTheShortActionForDefeat()
        {
            BoardingActorGraphic actor = Create(true);
            actor.SetPresentationTime(.71f);
            actor.PlayAction(BoardingActorAction.Attack, .6f);
            actor.AdvancePresentation(.2f);
            actor.SetMotionSuspended(true);
            Assert.That(actor.MotionSuspended, Is.True);
            Assert.That(actor.Action, Is.EqualTo(BoardingActorAction.Idle));
            float clock = actor.Clock;
            foreach (Vector2 point in UpperBodyPoints) Assert.That(actor.SampleOffset(point), Is.EqualTo(Vector2.zero));
            actor.AdvancePresentation(.3f);
            Assert.That(actor.Clock, Is.EqualTo(clock));
            foreach (Vector2 point in UpperBodyPoints) Assert.That(actor.SampleOffset(point), Is.EqualTo(Vector2.zero));
            actor.SetMotionSuspended(false);
            actor.AdvancePresentation(.1f);
            Assert.That(actor.Action, Is.EqualTo(BoardingActorAction.Idle), "Resuming the view must not replay the cancelled attack.");
        }

        [Test]
        public void DisableClearsPendingReactionAndDoesNotAdvanceItInTheBackground()
        {
            BoardingActorGraphic actor = Create(true);
            actor.PlayAction(BoardingActorAction.Hit, .6f);
            actor.AdvancePresentation(.16f);
            actor.enabled = false;
            Assert.That(actor.Action, Is.EqualTo(BoardingActorAction.Idle));
            float clock = actor.Clock;
            actor.AdvancePresentation(.25f);
            Assert.That(actor.Clock, Is.EqualTo(clock));
            actor.enabled = true;
            actor.AdvancePresentation(.1f);
            Assert.That(actor.Action, Is.EqualTo(BoardingActorAction.Idle));
        }

        [TestCase(1f)]
        [TestCase(-1f)]
        public void SavedConfigurationRestoresFormationFootAndFacingWithoutReplayingAction(float facing)
        {
            const float fixedPartyFoot = 33.5f / 1024f;
            BoardingActorGraphic original = Create(true, facing), restored = Create(false, -facing);
            original.Configure(true, facing, .73f, fixedPartyFoot);
            original.SetPresentationTime(2.2f);
            original.PlayAction(BoardingActorAction.Cast, .6f);
            original.AdvancePresentation(.2f);
            string saved = EditorJsonUtility.ToJson(original);
            EditorJsonUtility.FromJsonOverwrite(saved, restored);
            Assert.That(restored.Formation, Is.True, "A saved full formation must not reload as a single character influence.");
            Assert.That(restored.FootV, Is.EqualTo(fixedPartyFoot).Within(.000001f));
            Assert.That(restored.Action, Is.EqualTo(BoardingActorAction.Idle), "A saved preview must not replay a transient cast.");
            Assert.That(restored.Clock, Is.Zero, "Only authored configuration belongs to the saved component.");
            original.PlayAction(BoardingActorAction.Idle);
            foreach (float time in new[] { .19f, .81f, 2.3f })
            {
                original.SetPresentationTime(time); restored.SetPresentationTime(time);
                foreach (Vector2 point in UpperBodyPoints)
                    Assert.That(Vector2.Distance(original.SampleOffset(point), restored.SampleOffset(point)), Is.LessThan(.00001f),
                        "Saved idle actors must preserve the exact resting pose.");
            }
            original.PlayAction(BoardingActorAction.Attack, .6f); restored.PlayAction(BoardingActorAction.Attack, .6f);
            original.AdvancePresentation(.21f); restored.AdvancePresentation(.21f);
            BoardingActorGraphic idle = Create(true, facing);
            idle.Configure(true, facing, .73f, fixedPartyFoot); idle.SetPresentationTime(original.Clock);
            Vector2 shoulder = new Vector2(.52f, .8f);
            foreach (BoardingActorGraphic actor in new[] { original, restored })
                Assert.That((actor.SampleOffset(shoulder).x - idle.SampleOffset(shoulder).x) * facing, Is.GreaterThan(.1f),
                    "Restored player and enemy actors must lean toward their own opponent.");
            foreach (Vector2 point in UpperBodyPoints)
                Assert.That(Vector2.Distance(original.SampleOffset(point), restored.SampleOffset(point)), Is.LessThan(.00001f));
        }

        [Test]
        public void MainCastSurvivesSecondaryHealAndGuardButIncomingHitInterruptsIt()
        {
            BoardingActorGraphic actor = Create(true);
            actor.PlayAction(BoardingActorAction.Cast, .8f); actor.AdvancePresentation(.2f);
            float progress = actor.ActionProgress;
            actor.PlayAction(BoardingActorAction.Heal, .2f);
            Assert.That(actor.Action, Is.EqualTo(BoardingActorAction.Cast));
            Assert.That(actor.ActionProgress, Is.EqualTo(progress), "Secondary healing must not reset the committed cast clock.");
            actor.PlayAction(BoardingActorAction.Guard, .2f);
            Assert.That(actor.Action, Is.EqualTo(BoardingActorAction.Cast));
            Assert.That(actor.ActionProgress, Is.EqualTo(progress));
            actor.PlayAction(BoardingActorAction.Hit, .35f);
            Assert.That(actor.Action, Is.EqualTo(BoardingActorAction.Hit));
            Assert.That(actor.ActionProgress, Is.Zero);
            actor.AdvancePresentation(.14f);
            progress = actor.ActionProgress;
            actor.PlayAction(BoardingActorAction.Heal, .2f);
            Assert.That(actor.Action, Is.EqualTo(BoardingActorAction.Hit));
            Assert.That(actor.ActionProgress, Is.EqualTo(progress));
            actor.AdvancePresentation(.3f);
            Assert.That(actor.Action, Is.EqualTo(BoardingActorAction.Idle));
            actor.PlayAction(BoardingActorAction.Guard, .3f);
            Assert.That(actor.Action, Is.EqualTo(BoardingActorAction.Guard), "The lower-priority response must work after the hit ends.");
        }

        [Test]
        public void PartyReactivationClearsOldActionsAndAcceptsNewOnes()
        {
            var parent = new GameObject("restored boarding party", typeof(RectTransform), typeof(BoardingPartyPresentation));
            temporary.Add(parent);
            parent.SetActive(false);
            BoardingActorGraphic actor = Create(true, -1f);
            actor.transform.SetParent(parent.transform, false);
            parent.SetActive(true);
            var party = parent.GetComponent<BoardingPartyPresentation>();
            RectTransform rect = actor.GetComponent<RectTransform>();
            Vector2 position = rect.anchoredPosition;
            Vector3 scale = rect.localScale;
            Quaternion rotation = rect.localRotation;
            Assert.That(actor.enabled, Is.True);
            party.PlayAction(BoardingActorAction.Attack, .5f);
            actor.AdvancePresentation(.2f);
            Assert.That(actor.Action, Is.EqualTo(BoardingActorAction.Attack));
            parent.SetActive(false); parent.SetActive(true);
            Assert.That(actor.Action, Is.EqualTo(BoardingActorAction.Idle));
            party.SetMotionSuspended(true);
            party.PlayAction(BoardingActorAction.Cast, .5f);
            Assert.That(actor.MotionSuspended, Is.True);
            Assert.That(actor.Action, Is.EqualTo(BoardingActorAction.Idle));
            party.SetMotionSuspended(false);
            party.PlayAction(BoardingActorAction.Guard, .5f);
            actor.AdvancePresentation(.2f);
            Assert.That(actor.MotionSuspended, Is.False);
            Assert.That(actor.Action, Is.EqualTo(BoardingActorAction.Guard));
            Assert.That(rect.anchoredPosition, Is.EqualTo(position));
            Assert.That(rect.localScale, Is.EqualTo(scale));
            Assert.That(rect.localRotation, Is.EqualTo(rotation));
        }
        private static readonly Vector2[] UpperBodyPoints =
        {
            new Vector2(.22f, .55f), new Vector2(.22f, .83f),
            new Vector2(.52f, .58f), new Vector2(.52f, .90f),
            new Vector2(.82f, .55f), new Vector2(.82f, .82f)
        };

        private BoardingActorGraphic Create(bool formation, float facing = 1f)
        {
            var go = new GameObject("boarding actor mesh test", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(BoardingActorGraphic));
            temporary.Add(go);
            var image = go.GetComponent<RawImage>();
            image.rectTransform.sizeDelta = new Vector2(600f, 280f);
            image.raycastTarget = false;
            var texture = new Texture2D(8, 8); temporary.Add(texture); image.texture = texture;
            var actor = go.GetComponent<BoardingActorGraphic>();
            actor.Configure(formation, facing, .27f, Foot);
            return actor;
        }

        private Mesh Populate(BoardingActorGraphic actor)
        {
            RawImage image = actor.GetComponent<RawImage>();
            Rect r = image.rectTransform.rect, uv = image.uvRect;
            var mesh = new Mesh(); temporary.Add(mesh);
            using (var helper = new VertexHelper())
            {
                helper.AddVert(new Vector3(r.xMin, r.yMin), image.color, new Vector2(uv.x, uv.y));
                helper.AddVert(new Vector3(r.xMin, r.yMax), image.color, new Vector2(uv.x, uv.y + uv.height));
                helper.AddVert(new Vector3(r.xMax, r.yMax), image.color, new Vector2(uv.x + uv.width, uv.y + uv.height));
                helper.AddVert(new Vector3(r.xMax, r.yMin), image.color, new Vector2(uv.x + uv.width, uv.y));
                helper.AddTriangle(0, 1, 2); helper.AddTriangle(2, 3, 0);
                actor.ModifyMesh(helper); helper.FillMesh(mesh);
            }
            return mesh;
        }
    }
}
