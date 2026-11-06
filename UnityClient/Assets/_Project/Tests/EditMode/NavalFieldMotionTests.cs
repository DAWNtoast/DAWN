using System;
using System.Collections;
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
    public sealed class NavalFieldMotionTests
    {
        private GameObject root;
        private CoreVoyageSession session;
        private NavalTacticalBoardView board;
        private NavalFieldGraphic graphic;
        private NavalFieldMotion motion;
        private Canvas owner;

        [SetUp]
        public void SetUp()
        {
            session = new CoreVoyageSession();
            session.SetSeed("NAVAL-FIELD-MOTION"); session.StartVoyage();
            var encounter = new CoreEncounter("motion_test", "测试敌舰", "海战", "场地动效", true, false, 1, 70);
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { encounter, false });
            session.Battle.Stage = NavalBattleStage.Naval;
            Pose(true, 0, 0); Pose(false, 500, 0);
            session.Battle.PlayerBlock = session.Battle.EnemyBlock = 0;
            root = new GameObject("Field motion fixture", typeof(RectTransform), typeof(Canvas));
            owner = root.GetComponent<Canvas>();
            ((RectTransform)root.transform).sizeDelta = new Vector2(1920, 1080);
            board = Rect("Board").gameObject.AddComponent<NavalTacticalBoardView>();
            board.Configure(session.Battle, Rect("Player"), Rect("Enemy"), Rect("Player Hull"), Rect("Enemy Hull"),
                Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            graphic = Rect("Field graphic").gameObject.AddComponent<NavalFieldGraphic>();
            graphic.Configure(board, null);
            motion = graphic.gameObject.AddComponent<NavalFieldMotion>();
            motion.Configure(graphic, session.Battle.NavalFields, owner, board);
        }

        [TearDown]
        public void TearDown() { if (root != null) Object.DestroyImmediate(root); }

        [Test]
        public void Facts_KeepOverwriteCoordinatesAndExpireExactlyOnceWithoutFalseTrigger()
        {
            Place(CoreNavalFieldKind.Arcane, 0, 0);
            CoreNavalFieldState state = session.Battle.NavalFields;
            Assert.That(state.PresentationEventCount, Is.EqualTo(4));
            for (int i = 0; i < 4; i++) Assert.That(state.PresentationEventAt(i).Change, Is.EqualTo(CoreNavalFieldChangeKind.Placed));
            int oldInstance = state.Cells[0].InstanceId;
            Place(CoreNavalFieldKind.Tide, 0, 0, 0, 1);
            Assert.That(state.PresentationEventCount, Is.EqualTo(24));
            CoreNavalFieldPresentationEvent old = state.PresentationEventAt(4), replacement = state.PresentationEventAt(5);
            Assert.That(old.Change, Is.EqualTo(CoreNavalFieldChangeKind.Overwritten));
            Assert.That(old.Kind, Is.EqualTo(CoreNavalFieldKind.Arcane));
            Assert.That(old.InstanceId, Is.EqualTo(oldInstance));
            Assert.That(old.X, Is.EqualTo(replacement.X)); Assert.That(old.Y, Is.EqualTo(replacement.Y));
            Assert.That(replacement.Kind, Is.EqualTo(CoreNavalFieldKind.Tide));
            Assert.That(replacement.DirectionY, Is.EqualTo(1));
            for (int turn = 0; turn < 5; turn++)
            { session.NavalFieldsBeginTurn(false); session.NavalFieldsEndTurn(false); }
            Assert.That(state.Cells, Is.Empty);
            Assert.That(Count(state, CoreNavalFieldChangeKind.Expired), Is.EqualTo(16));
            Assert.That(Count(state, CoreNavalFieldChangeKind.Triggered), Is.Zero, "Placement itself must not invent a rule trigger.");
            long end = state.PresentationSequence;
            session.NavalFieldsBeginTurn(true);
            Assert.That(state.PresentationSequence, Is.EqualTo(end));
        }

        [Test]
        public void Facts_KeepTrueStandingTriggerAfterLedgerClearAndExcludeCopyHistory()
        {
            Place(CoreNavalFieldKind.Fire, 0, 0);
            int hull = session.Hull;
            session.NavalFieldsEndTurn(true); session.NavalFieldsEndTurn(true);
            CoreNavalFieldState state = session.Battle.NavalFields;
            Assert.That(session.Hull, Is.EqualTo(hull));
            Assert.That(session.Battle.PlayerBurnStacks, Is.EqualTo(5));
            Assert.That(Count(state, CoreNavalFieldChangeKind.Triggered), Is.EqualTo(1));
            CoreNavalFieldPresentationEvent trigger = state.PresentationEventAt(9);
            Assert.That(trigger.TargetPlayer, Is.True); Assert.That(trigger.MovementTrigger, Is.False);
            session.NavalFieldsBeginTurn(true);
            Assert.That(state.TriggerLedger, Is.Empty);
            Assert.That(Count(state, CoreNavalFieldChangeKind.Triggered), Is.EqualTo(1));
            CoreNavalFieldState copy = state.Copy();
            Assert.That(copy.Cells.Count, Is.EqualTo(state.Cells.Count));
            Assert.That(copy.PresentationEventCount, Is.Zero); Assert.That(copy.PresentationSequence, Is.Zero);
            Assert.That(state.PresentationEventCount, Is.EqualTo(10));
        }

        [Test]
        public void ThunderFacts_RecordEveryLandingIncludingRepeatedCellsWithoutStoppingMovement()
        {
            Place(CoreNavalFieldKind.Thunder, 1, 0);
            CoreNavalFieldState state = session.Battle.NavalFields;
            Assert.That(Count(state, CoreNavalFieldChangeKind.Placed), Is.EqualTo(49));
            Assert.That(Count(state, CoreNavalFieldChangeKind.Triggered), Is.Zero);
            int hull = session.Hull;
            Assert.That(session.PushNavalShip(true, 1, 0), Is.True);
            Assert.That(session.PushNavalShip(true, 1, 0), Is.True);
            Assert.That(session.PushNavalShip(true, -1, 0), Is.True);
            Assert.That(session.Battle.Schools.ThunderChain, Is.EqualTo(15));
            Assert.That(session.Hull, Is.EqualTo(hull));
            Assert.That(state.TriggerLedger, Is.Empty);
            Assert.That(Count(state, CoreNavalFieldChangeKind.Triggered), Is.EqualTo(3));
            for (int index = 49; index < 52; index++)
            {
                CoreNavalFieldPresentationEvent fact = state.PresentationEventAt(index);
                Assert.That(fact.Kind, Is.EqualTo(CoreNavalFieldKind.Thunder));
                Assert.That(fact.Change, Is.EqualTo(CoreNavalFieldChangeKind.Triggered));
                Assert.That(fact.MovementTrigger, Is.True); Assert.That(fact.TargetPlayer, Is.True);
                Assert.That(fact.X, Is.EqualTo(index == 50 ? 2 : 1));
            }
        }

        [Test]
        public void Facts_BoundedRingRemainsOrderedAndPreviewDoesNotMutateIt()
        {
            for (int i = 0; i < 24; i++) Place(i % 2 == 0 ? CoreNavalFieldKind.Fire : CoreNavalFieldKind.Arcane, 1, 0);
            CoreNavalFieldState state = session.Battle.NavalFields;
            Assert.That(state.PresentationEventCount, Is.EqualTo(CoreNavalFieldState.PresentationCapacity));
            long sequence = state.PresentationSequence;
            for (int i = 0; i < state.PresentationEventCount; i++)
                Assert.That(state.PresentationEventAt(i).Sequence, Is.EqualTo(sequence - state.PresentationEventCount + i + 1));
            Assert.That(session.TryAppendNavalMovementStep(new VoyagePoint(100, 0), out string reason), Is.True, reason);
            CoreNavalMovementFieldPreview preview = session.NavalMovementFieldPreview;
            Assert.That(preview, Is.Not.Null);
            Assert.That(state.PresentationSequence, Is.EqualTo(sequence));
            Assert.That(state.TriggerLedger, Is.Empty);
        }

        [Test]
        public void MovementTrigger_WaitsForDisplayedRevisionAndShipArrivalWhileRulesSettleImmediately()
        {
            Place(CoreNavalFieldKind.Thunder, 1, 0);
            motion.Synchronize(session.Battle.NavalFields, true, session.Battle.TacticalRevision);
            motion.AdvancePresentation(1f);
            Assert.That(session.TryAppendNavalMovementStep(new VoyagePoint(100, 0), out string reason), Is.True, reason);
            Assert.That(session.CommitNavalMovement(out reason), Is.True, reason);
            Assert.That(session.Battle.Schools.ThunderChain, Is.EqualTo(5));
            CoreNavalFieldState state = session.Battle.NavalFields;
            Assert.That(state.PresentationEventAt(state.PresentationEventCount - 1).MovementTrigger, Is.True);
            // Even an old displayed pose at the target must not defeat the revision gate.
            board.ApplyFrame(new CoreNavalTacticalFrame(new VoyagePoint(100, 0), new VoyagePoint(500, 0), 0, 180));
            motion.Synchronize(state, true, session.Battle.TacticalRevision);
            motion.AdvancePresentation(.1f);
            Assert.That(motion.PendingTriggerCount, Is.EqualTo(1));
            Assert.That(motion.TotalTriggeredCues, Is.Zero);
            IEnumerator playback = board.AnimateMovement(session.Battle.LastTacticalMovement);
            Assert.That(playback.MoveNext(), Is.True);
            motion.AdvancePresentation(.01f);
            Assert.That(motion.TotalTriggeredCues, Is.Zero, "At the first displayed frame the ship remains a full cell away.");
            board.ApplyFrame(new CoreNavalTacticalFrame(new VoyagePoint(50, 0), new VoyagePoint(500, 0), 0, 180));
            motion.AdvancePresentation(.01f);
            Assert.That(motion.PendingTriggerCount, Is.Zero); Assert.That(motion.TotalTriggeredCues, Is.EqualTo(1));
            motion.Synchronize(state, true, session.Battle.TacticalRevision);
            Assert.That(motion.TotalTriggeredCues, Is.EqualTo(1));
            board.SnapToBattle(session.Battle);
        }

        [Test]
        public void Refraction_OnlyQueuesStandingStartCueAndNeverAMovementCue()
        {
            Place(CoreNavalFieldKind.Arcane, 1, 0);
            motion.Synchronize(session.Battle.NavalFields, true, session.Battle.TacticalRevision);
            motion.AdvancePresentation(1f);
            int mana = session.Battle.Schools.Mana;
            Assert.That(session.TryAppendNavalMovementStep(new VoyagePoint(100, 0), out string reason), Is.True, reason);
            Assert.That(session.CommitNavalMovement(out reason), Is.True, reason);
            motion.Synchronize(session.Battle.NavalFields, true, session.Battle.TacticalRevision);
            Assert.That(session.Battle.Schools.Mana, Is.EqualTo(mana));
            Assert.That(motion.PendingTriggerCount, Is.Zero);
            Assert.That(motion.TotalTriggeredCues, Is.Zero);
            Assert.That(Count(session.Battle.NavalFields, CoreNavalFieldChangeKind.Triggered), Is.Zero);
            session.NavalFieldsBeginTurn(false); session.NavalFieldsEndTurn(false);
            session.NavalFieldsBeginTurn(true);
            Assert.That(session.Battle.Schools.Mana, Is.EqualTo(mana + 2));
            CoreNavalFieldState state = session.Battle.NavalFields;
            CoreNavalFieldPresentationEvent trigger = state.PresentationEventAt(state.PresentationEventCount - 1);
            Assert.That(trigger.Kind, Is.EqualTo(CoreNavalFieldKind.Arcane));
            Assert.That(trigger.TargetPlayer, Is.True);
            Assert.That(trigger.MovementTrigger, Is.False);
            board.SnapToBattle(session.Battle);
            motion.Synchronize(state, true, session.Battle.TacticalRevision);
            motion.AdvancePresentation(.1f);
            Assert.That(motion.TotalTriggeredCues, Is.EqualTo(1));
        }

        [Test]
        public void BloodPool_EntryQueuesNoCueAndHealthPaymentQueuesOneStandingCue()
        {
            Place(CoreNavalFieldKind.Blood, 1, 0);
            motion.Synchronize(session.Battle.NavalFields, true, session.Battle.TacticalRevision);
            motion.AdvancePresentation(1f);
            Assert.That(session.TryAppendNavalMovementStep(new VoyagePoint(100, 0), out string reason), Is.True, reason);
            Assert.That(session.CommitNavalMovement(out reason), Is.True, reason);
            board.SnapToBattle(session.Battle);
            motion.Synchronize(session.Battle.NavalFields, true, session.Battle.TacticalRevision);
            motion.AdvancePresentation(.1f);
            Assert.That(motion.TotalTriggeredCues, Is.Zero);
            Assert.That(session.Battle.PlayerBlock, Is.Zero);
            typeof(CoreVoyageSession).GetMethod("ApplyNavalBloodPoolGuard", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { true, 4 });
            motion.Synchronize(session.Battle.NavalFields, true, session.Battle.TacticalRevision);
            motion.AdvancePresentation(.1f);
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(8),
                "A standing blood pool grants two guard per point of actively paid health.");
            Assert.That(motion.TotalTriggeredCues, Is.EqualTo(1));
            Assert.That(motion.PendingTriggerCount, Is.Zero);
        }

        [Test]
        public void Motion_DisabledOwnerAndPauseCancelWithoutReplayOrRuleWrites()
        {
            Place(CoreNavalFieldKind.Fire, 0, 0);
            motion.Synchronize(session.Battle.NavalFields, true, session.Battle.TacticalRevision);
            Assert.That(motion.TotalPlacedCues, Is.EqualTo(9));
            int hull = session.Hull; long sequence = session.Battle.NavalFields.PresentationSequence;
            owner.enabled = false;
            motion.AdvancePresentation(.1f);
            Assert.That(motion.ActiveCues, Is.Empty);
            float frozen = motion.AmbientTime;
            owner.enabled = true;
            motion.Synchronize(session.Battle.NavalFields, true, session.Battle.TacticalRevision);
            Assert.That(motion.TotalPlacedCues, Is.EqualTo(9));
            MethodInfo pauseCallback = typeof(NavalFieldMotion).GetMethod("OnApplicationPause",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(bool) }, null);
            pauseCallback.Invoke(motion, new object[] { true });
            Place(CoreNavalFieldKind.Blood, 0, 0);
            motion.Synchronize(session.Battle.NavalFields, true, session.Battle.TacticalRevision);
            motion.AdvancePresentation(.4f);
            Assert.That(motion.AmbientTime, Is.EqualTo(frozen));
            pauseCallback.Invoke(motion, new object[] { false });
            motion.Synchronize(session.Battle.NavalFields, true, session.Battle.TacticalRevision);
            Assert.That(motion.ActiveCues, Is.Empty);
            Assert.That(motion.TotalPlacedCues, Is.EqualTo(9));
            Assert.That(session.Hull, Is.EqualTo(hull));
            Assert.That(session.Battle.NavalFields.PresentationSequence, Is.EqualTo(sequence + 8));
            motion.gameObject.SetActive(false);
            Place(CoreNavalFieldKind.Curse, 0, 0);
            motion.gameObject.SetActive(true);
            // EditMode does not dispatch the runtime activation callback for this component.
            typeof(NavalFieldMotion).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null).Invoke(motion, null);
            motion.Synchronize(session.Battle.NavalFields, true, session.Battle.TacticalRevision);
            Assert.That(motion.ActiveCues, Is.Empty, "Reactivation adopts facts committed while the hierarchy was inactive.");
            Assert.That(motion.TotalPlacedCues, Is.EqualTo(9));
            Object.DestroyImmediate(owner);
            Assert.That(motion.Suspended, Is.True, "Destroyed Unity owners must not resurrect ambient motion.");
        }

        [Test]
        public void EmptyBoard_DoesNotDirtyGeometryEveryPresentationTick()
        {
            int dirties = 0;
            graphic.RegisterDirtyVerticesCallback(() => dirties++);
            for (int i = 0; i < 10; i++)
            {
                motion.Synchronize(session.Battle.NavalFields, true, session.Battle.TacticalRevision);
                motion.AdvancePresentation(.02f);
            }
            Assert.That(dirties, Is.Zero);
        }

        [TestCase(CoreNavalFieldKind.Fire)]
        [TestCase(CoreNavalFieldKind.Arcane)]
        [TestCase(CoreNavalFieldKind.Curse)]
        [TestCase(CoreNavalFieldKind.Tide)]
        [TestCase(CoreNavalFieldKind.Thunder)]
        [TestCase(CoreNavalFieldKind.Blood)]
        public void Ambient_ChangesOnlyInteriorPatternAndKeepsProjectedBoundaryAndDirection(CoreNavalFieldKind kind)
        {
            Color tint = NavalFieldGraphic.Tint(kind), fill = tint; fill.a = .23f;
            var position = new VoyagePoint(100, 100);
            graphic.Configure(board, new[] { new NavalFieldVisualCell(position, kind, fill, tint, true, 0, 1) });
            NavalFieldPatternGraphic painting = graphic.GetComponentsInChildren<NavalFieldPatternGraphic>().Single();
            Assert.That(painting.Kind, Is.EqualTo(kind));
            Assert.That(painting.mainTexture, Is.SameAs(NavalFieldArtCatalog.ForField(kind)));
            Assert.That(painting.mainTexture.width, Is.EqualTo(512));
            Assert.That(painting.mainTexture.height, Is.EqualTo(512));
            Assert.That(painting.raycastTarget, Is.False);
            graphic.SetMotion(0f, null); UIVertex[] first = Vertices(), firstPainting = Vertices(painting);
            graphic.SetMotion(1.83f, null); UIVertex[] later = Vertices(), laterPainting = Vertices(painting);
            Assert.That(later.Length, Is.EqualTo(first.Length));
            Vector2 expected = board.WorldToBoard(new VoyagePoint(54, 54));
            Assert.That(Vector2.Distance(first[0].position, expected), Is.LessThan(.01f));
            for (int i = 0; i < 20; i++)
            {
                Assert.That(later[i].position, Is.EqualTo(first[i].position));
                Assert.That(later[i].color, Is.EqualTo(first[i].color));
            }
            Assert.That(firstPainting.Length, Is.EqualTo(4), "The complete transparent painting uses one quad per cell.");
            Assert.That(firstPainting[0].uv0, Is.EqualTo(new Vector4(0, 0, 0, 0)));
            Assert.That(firstPainting[2].uv0, Is.EqualTo(new Vector4(1, 1, 0, 0)), "Authored transparent padding must not be trimmed.");
            bool interiorChanged = false;
            for (int i = 0; i < firstPainting.Length; i++)
                interiorChanged |= firstPainting[i].position != laterPainting[i].position || !firstPainting[i].color.Equals(laterPainting[i].color);
            Assert.That(interiorChanged, Is.True);
            for (int i = first.Length - 12; i < first.Length; i++)
                Assert.That(later[i].position, Is.EqualTo(first[i].position), "World direction arrow must stay fixed.");
            Assert.That(graphic.raycastTarget, Is.False);
        }

        [Test]
        public void PersistentPatterns_ShowEachTilesOwnSchoolBetweenClearSeaIntervals()
        {
            Color fire = NavalFieldGraphic.Tint(CoreNavalFieldKind.Fire), tide = NavalFieldGraphic.Tint(CoreNavalFieldKind.Tide);
            var firstCell = new NavalFieldVisualCell(new VoyagePoint(-100, 0), CoreNavalFieldKind.Fire, fire, fire, true);
            var secondCell = new NavalFieldVisualCell(new VoyagePoint(100, 0), CoreNavalFieldKind.Tide, tide, tide, true, 0, 1);
            graphic.Configure(board, new[] { firstCell }, cyclePatterns: true);
            graphic.SetMotion(.6f, null);
            int fireVertexCount = Vertices().Length;
            graphic.Configure(board, new[] { secondCell }, cyclePatterns: true);
            int tideVertexCount = Vertices().Length;
            graphic.Configure(board, new[] { firstCell, secondCell }, cyclePatterns: true);
            graphic.SetMotion(.6f, null);
            UIVertex[] patternFrame = Vertices();
            Assert.That(patternFrame.Length, Is.EqualTo(fireVertexCount + tideVertexCount),
                "Adding another field kind must not suppress either tile's own pattern or direction arrow.");
            Assert.That(patternFrame[0].color.a, Is.GreaterThan(0));
            Assert.That(patternFrame[fireVertexCount].color.a, Is.GreaterThan(0));
            NavalFieldPatternGraphic[] paintings = graphic.GetComponentsInChildren<NavalFieldPatternGraphic>();
            Assert.That(paintings.Length, Is.EqualTo(2));
            Assert.That(paintings.All(p => p.PatternCellCount == 1 && Vertices(p).Length == 4), Is.True,
                "Different schools each draw their own transparent painting in the same frame.");

            graphic.SetMotion(1.9f, null);
            UIVertex[] seaFrame = Vertices();
            Assert.That(seaFrame.Length, Is.EqualTo(40), "Only faint cell boundaries remain: no patterns or direction arrows.");
            Assert.That(seaFrame[0].color.a, Is.Zero);
            Assert.That(seaFrame[20].color.a, Is.Zero);
            Assert.That(seaFrame[4].color.a, Is.LessThan(patternFrame[4].color.a));
            Assert.That(paintings.All(p => Vertices(p).Length == 0), Is.True, "The sea interval also hides the new textured patterns.");

            graphic.SetMotion(3f, null);
            UIVertex[] nextFrame = Vertices();
            Assert.That(nextFrame.Length, Is.EqualTo(patternFrame.Length));
            Assert.That(nextFrame[0].color.a, Is.GreaterThan(0));
            Assert.That(nextFrame[fireVertexCount].color.a, Is.GreaterThan(0), "Every kind returns together with its corresponding pattern.");
            Assert.That(graphic.Cells.Count, Is.EqualTo(2), "Cycling is presentation only and never removes a field.");
            Assert.That(graphic.Cells[1].DirectionY, Is.EqualTo(1));
            Assert.That(paintings.All(p => Vertices(p).Length == 4), Is.True);

            graphic.SetMotion(4.3f, null);
            Assert.That(Vertices().Length, Is.EqualTo(40));
            graphic.SetMotion(5.4f, null);
            Assert.That(Vertices()[0].color.a, Is.GreaterThan(0));

            graphic.Configure(board, new[] { firstCell }, cyclePatterns: true);
            graphic.SetMotion(1.9f, null);
            Assert.That(Vertices().Length, Is.EqualTo(20), "A lone field still takes a sea interval.");
            graphic.SetMotion(3f, null);
            Assert.That(Vertices()[0].color.a, Is.GreaterThan(0));
            Assert.That(graphic.raycastTarget, Is.False);
        }

        [TestCase(1, 0)]
        [TestCase(0, 1)]
        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        public void TidePainting_FollowsItsWorldDirectionAndBoardRotation(int dx, int dy)
        {
            Color tint = NavalFieldGraphic.Tint(CoreNavalFieldKind.Tide);
            graphic.Configure(board, new[] { new NavalFieldVisualCell(new VoyagePoint(100, 100),
                CoreNavalFieldKind.Tide, tint, tint, true, dx, dy) });
            NavalFieldPatternGraphic painting = graphic.GetComponentInChildren<NavalFieldPatternGraphic>();
            board.ApplyFrame(new CoreNavalTacticalFrame(new VoyagePoint(0, -200), new VoyagePoint(0, 0), 90, 180));
            graphic.SetMotion(.6f, null);
            UIVertex[] vertices = Vertices(painting);
            Vector2 textureEast = ((Vector2)vertices[3].position - (Vector2)vertices[0].position).normalized;
            Vector2 expected = (board.WorldToBoard(new VoyagePoint(100 + dx * 100, 100 + dy * 100)) -
                board.WorldToBoard(new VoyagePoint(100, 100))).normalized;
            Assert.That(Vector2.Dot(textureEast, expected), Is.GreaterThan(.999f));
        }

        private UIVertex[] Vertices(Graphic target = null)
        {
            if (target == null) target = graphic;
            using (var helper = new VertexHelper())
            {
                target.GetType().GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null)
                    .Invoke(target, new object[] { helper });
                var result = new UIVertex[helper.currentVertCount];
                for (int i = 0; i < result.Length; i++) helper.PopulateUIVertex(ref result[i], i);
                return result;
            }
        }
        private static int Count(CoreNavalFieldState state, CoreNavalFieldChangeKind change)
        {
            int count = 0;
            for (int i = 0; i < state.PresentationEventCount; i++) if (state.PresentationEventAt(i).Change == change) count++;
            return count;
        }
        private void Place(CoreNavalFieldKind kind, int x, int y, int dx = 0, int dy = 0)
        {
            VoyagePoint original = session.Battle.PlayerPositionMeters;
            if (kind == CoreNavalFieldKind.Fire) Pose(true, (x + 1) * 100, (y + 1) * 100);
            Assert.That(session.PlaceNavalField(kind, true, x, y, false, dx, dy), Is.True);
            if (kind == CoreNavalFieldKind.Fire) Pose(true, original.X, original.Y);
        }
        private RectTransform Rect(string name)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(root.transform, false); rect.sizeDelta = new Vector2(1860, 900); return rect;
        }
        private void Pose(bool player, float x, float y) => typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(session.Battle, new object[] { player, new VoyagePoint(x, y), player ? 0f : 180f });
    }
}
