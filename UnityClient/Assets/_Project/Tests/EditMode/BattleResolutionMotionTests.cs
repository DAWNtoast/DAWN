using System;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Core.Tests
{
    public sealed class BattleResolutionMotionTests
    {
        private GameObject root;
        private RectTransform stage;
        private Canvas owner;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Resolution fixture", typeof(RectTransform), typeof(Canvas));
            stage = (RectTransform)root.transform;
            stage.sizeDelta = new Vector2(1920, 1080);
            owner = root.GetComponent<Canvas>();
        }

        [TearDown]
        public void TearDown() { if (root != null) Object.DestroyImmediate(root); }

        [TestCase(false)]
        [TestCase(true)]
        public void NavalSinking_OnlyMovesDefeatedShipAndRestoresExactPose(bool player)
        {
            RectTransform target = Rect(player ? "Player Ship Art" : "Enemy Ship Art", stage,
                new Vector2(player ? -400 : 400, 80), new Vector2(320, 200));
            target.gameObject.AddComponent<RawImage>().raycastTarget = false;
            target.localRotation = Quaternion.Euler(0, 0, 7);
            target.localScale = new Vector3(.8f, .9f, 1);
            CanvasGroup group = target.gameObject.AddComponent<CanvasGroup>(); group.alpha = .63f;
            group.blocksRaycasts = false; group.interactable = false;
            RectTransform survivor = Rect("Survivor", stage, new Vector2(player ? 400 : -400, 80), new Vector2(320, 200));
            Vector2 origin = target.anchoredPosition, otherOrigin = survivor.anchoredPosition;
            Quaternion rotation = target.localRotation; Vector3 scale = target.localScale;
            BattleResolutionMotion motion = CreateMotion(target);
            Assert.That(motion.Kind, Is.EqualTo(BattleResolutionKind.NavalSinking));
            motion.AdvancePresentation(motion.Duration * .55f);
            Assert.That(target.anchoredPosition.y, Is.LessThan(origin.y - 10));
            Assert.That(Quaternion.Angle(target.localRotation, rotation), Is.GreaterThan(1));
            Assert.That(group.alpha, Is.LessThan(.63f)); Assert.That(group.alpha, Is.GreaterThan(0));
            Assert.That(survivor.anchoredPosition, Is.EqualTo(otherOrigin));
            Assert.That(motion.GetComponent<BattleResolutionGraphic>().raycastTarget, Is.False);
            motion.CancelAndRestore(); motion.CancelAndRestore();
            Assert.That(target.anchoredPosition, Is.EqualTo(origin));
            Assert.That(target.localRotation, Is.EqualTo(rotation)); Assert.That(target.localScale, Is.EqualTo(scale));
            Assert.That(group.alpha, Is.EqualTo(.63f)); Assert.That(group.blocksRaycasts, Is.False);
            Assert.That(group.interactable, Is.False); Assert.That(target.GetComponent<CanvasGroup>(), Is.SameAs(group));
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void BoardingDefeat_UsesDisplayedFormationAndKeepsLabelsFixed(bool player, bool componentMarker)
        {
            RectTransform target = Rect(player ? "Player Ship Art" : "Enemy Ship Art", stage,
                new Vector2(player ? -400 : 400, 80), new Vector2(440, 260));
            RectTransform label = null;
            if (componentMarker)
            {
                target.gameObject.AddComponent<BoardingPartyPresentation>();
                Assert.That(target.Find(target.name + " Formation Label"), Is.Null);
            }
            else
            {
                label = Rect(target.name + " Formation Label", target, new Vector2(0, 150), new Vector2(440, 20));
                label.gameObject.AddComponent<Text>();
            }
            RectTransform role = Rect("Role", target, new Vector2(0, -145), new Vector2(170, 18)); role.gameObject.AddComponent<Text>();
            RectTransform figure = Rect(target.name + " Sailor 0", target, new Vector2(0, -6), new Vector2(200, 240));
            figure.gameObject.AddComponent<RawImage>();
            Vector2 targetOrigin = target.anchoredPosition, figureOrigin = figure.anchoredPosition;
            Assert.That(BattleResolutionMotion.DisplayedKind(target), Is.EqualTo(BattleResolutionKind.BoardingDefeat));
            BattleResolutionMotion motion = CreateMotion(target);
            motion.AdvancePresentation(motion.Duration * .6f);
            Assert.That(target.anchoredPosition, Is.EqualTo(targetOrigin));
            if (label != null) Assert.That(label.anchoredPosition, Is.EqualTo(new Vector2(0, 150)));
            Assert.That(role.anchoredPosition.y, Is.EqualTo(-145));
            Assert.That(figure.anchoredPosition.y, Is.LessThan(figureOrigin.y));
            Assert.That(figure.GetComponent<CanvasGroup>().alpha, Is.LessThan(1f));
            Assert.That(target.GetComponent<CanvasGroup>(), Is.Null, "Do not fade formation labels through a new group on the party root.");
            motion.CancelAndRestore();
            Assert.That(figure.anchoredPosition, Is.EqualTo(figureOrigin));
            Assert.That(figure.GetComponent<CanvasGroup>(), Is.Null, "Only the group's temporary component is removed.");
        }

        [Test]
        public void Completion_KeepsFinalTargetHiddenUntilAuthoritativeViewIsRebuilt()
        {
            RectTransform target = Rect("Enemy Ship Art", stage, new Vector2(400, 80), new Vector2(320, 200));
            BattleResolutionMotion motion = CreateMotion(target);
            motion.AdvancePresentation(2f);
            Assert.That(motion.IsPlaying, Is.False); Assert.That(motion.Progress, Is.EqualTo(1f));
            Assert.That(target.GetComponent<CanvasGroup>().alpha, Is.Zero);
            Assert.That(motion.HasRestoredPose, Is.False, "The defeated ship must not reappear before the caller renders settlement.");
            // EditMode does not dispatch this component's runtime destruction callback.
            Invoke(motion, "OnDestroy");
            Assert.That(motion.HasRestoredPose, Is.True);
            Object.DestroyImmediate(motion);
            Assert.That(target.anchoredPosition, Is.EqualTo(new Vector2(400, 80)));
            Assert.That(target.GetComponent<CanvasGroup>(), Is.Null);
        }

        [TestCase(0f)]
        [TestCase(45f)]
        [TestCase(90f)]
        public void TacticalSinking_StaysOnItsWorldCellAndPreservesDisplayedHeading(float angle)
        {
            Rect("Naval Tactical Board", stage, Vector2.zero, new Vector2(1860, 900))
                .gameObject.AddComponent<NavalTacticalBoardView>();
            RectTransform target = Rect("Enemy Ship Art", stage, new Vector2(200, 170), new Vector2(112, 168));
            target.gameObject.AddComponent<Image>();
            target.localRotation = Quaternion.Euler(0, 0, angle);
            Quaternion heading = target.localRotation;
            BattleResolutionMotion motion = CreateMotion(target);
            Assert.That(motion.IsTopDown, Is.True);
            Assert.That(((RectTransform)motion.transform).anchoredPosition, Is.EqualTo(target.anchoredPosition));
            motion.AdvancePresentation(motion.Duration * .6f);
            Assert.That(target.anchoredPosition, Is.EqualTo(new Vector2(200, 170)));
            Assert.That(target.localRotation, Is.EqualTo(heading));
            Assert.That(target.localScale.x, Is.LessThan(1));
            Assert.That(target.GetComponent<CanvasGroup>().alpha, Is.LessThan(1));
            BattleResolutionGraphic graphic = motion.GetComponent<BattleResolutionGraphic>();
            Assert.That(graphic.IsTopDown, Is.True);
            motion.CancelAndRestore();
            Assert.That(target.localScale, Is.EqualTo(Vector3.one));
            Assert.That(target.localRotation, Is.EqualTo(heading));
        }

        [Test]
        public void PauseDisableAndDestroyedOwner_CancelWithoutLeavingChangedTargetOrBanner()
        {
            RectTransform target = Rect("Player Ship Art", stage, new Vector2(-400, 80), new Vector2(320, 200));
            BattleResolutionMotion motion = CreateMotion(target);
            Text banner = Rect("Banner", motion.transform, Vector2.zero, new Vector2(600, 72)).gameObject.AddComponent<Text>();
            motion.AttachBanner(banner);
            motion.AdvancePresentation(.3f);
            Invoke(motion, "OnApplicationPause", true);
            Assert.That(motion.IsPlaying, Is.False); Assert.That(motion.HasRestoredPose, Is.True);
            Assert.That(banner.gameObject.activeSelf, Is.False);
            Assert.That(target.anchoredPosition, Is.EqualTo(new Vector2(-400, 80)));
            Assert.That(target.GetComponent<CanvasGroup>(), Is.Null);
            motion = CreateMotion(target); motion.AdvancePresentation(.3f);
            Invoke(motion, "OnDisable");
            Assert.That(target.GetComponent<CanvasGroup>(), Is.Null);
            motion = CreateMotion(target); motion.AdvancePresentation(.3f);
            Object.DestroyImmediate(owner);
            motion.AdvancePresentation(.2f);
            Assert.That(motion.IsPlaying, Is.False); Assert.That(motion.HasRestoredPose, Is.True);
            Assert.That(target.anchoredPosition, Is.EqualTo(new Vector2(-400, 80)));
        }

        [Test]
        public void DestroyedTarget_DoesNotKeepResolutionAliveOrThrowDuringCleanup()
        {
            RectTransform target = Rect("Enemy Ship Art", stage, Vector2.zero, new Vector2(320, 200));
            BattleResolutionMotion motion = CreateMotion(target);
            Object.DestroyImmediate(target.gameObject);
            Assert.DoesNotThrow(() => motion.AdvancePresentation(.2f));
            Assert.That(motion.IsPlaying, Is.False);
            Assert.DoesNotThrow(motion.CancelAndRestore);
        }

        [Test]
        public void DisplayedKind_FollowsTheActualTargetAndNeverGlobalBattleStage()
        {
            RectTransform naval = Rect("Player Ship Art", stage, Vector2.zero, new Vector2(320, 200));
            RectTransform boarding = Rect("Enemy Ship Art", stage, Vector2.zero, new Vector2(440, 260));
            boarding.gameObject.AddComponent<BoardingPartyPresentation>();
            Assert.That(boarding.Find(boarding.name + " Formation Label"), Is.Null);
            var model = new CoreBattleState { Stage = NavalBattleStage.Boarding };
            Assert.That(BattleResolutionMotion.DisplayedKind(boarding), Is.EqualTo(BattleResolutionKind.BoardingDefeat));
            // Rules can already have moved on while the losing formation is still displayed.
            model.Stage = NavalBattleStage.Naval;
            BattleResolutionMotion motion = CreateMotion(boarding);
            Assert.That(BattleResolutionMotion.DisplayedKind(naval), Is.EqualTo(BattleResolutionKind.NavalSinking));
            Assert.That(BattleResolutionMotion.DisplayedKind(boarding), Is.EqualTo(BattleResolutionKind.BoardingDefeat));
            Assert.That(motion.Kind, Is.EqualTo(BattleResolutionKind.BoardingDefeat));
            Assert.That(model.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(BattleResolutionMotion.DisplayedKind(null), Is.EqualTo(BattleResolutionKind.NavalSinking));
        }

        [Test]
        public void SurrenderGraphic_LowersItsFlagAndBothStylesStayLocalAndFinite()
        {
            RectTransform host = Rect("Graphic", stage, Vector2.zero, new Vector2(500, 400));
            BattleResolutionGraphic graphic = host.gameObject.AddComponent<BattleResolutionGraphic>();
            graphic.Configure(BattleResolutionKind.BoardingDefeat, new Vector2(300, 200), 1);
            graphic.SetProgress(.2f); UIVertex[] early = Vertices(graphic);
            graphic.SetProgress(.7f); UIVertex[] late = Vertices(graphic);
            for (int i = 4; i < 8; i++) Assert.That(late[i].position.y, Is.LessThan(early[i].position.y), "The four cloth corners lower along the pole.");
            graphic.Configure(BattleResolutionKind.NavalSinking, new Vector2(300, 200), 1);
            graphic.SetProgress(.5f); UIVertex[] naval = Vertices(graphic);
            Assert.That(naval.Length, Is.GreaterThan(late.Length));
            foreach (UIVertex vertex in naval)
            {
                Assert.That(float.IsNaN(vertex.position.x) || float.IsNaN(vertex.position.y), Is.False);
                Assert.That(Mathf.Abs(vertex.position.x), Is.LessThan(350));
                Assert.That(Mathf.Abs(vertex.position.y), Is.LessThan(250));
            }
            graphic.SetProgress(1f); Assert.That(Vertices(graphic), Is.Empty);
        }

        private BattleResolutionMotion CreateMotion(RectTransform target)
        {
            BattleResolutionMotion motion = Rect("Battle Resolution Effects", stage, Vector2.zero, Vector2.one)
                .gameObject.AddComponent<BattleResolutionMotion>();
            motion.Configure(stage, target, owner, BattleResolutionMotion.DisplayedKind(target));
            return motion;
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static void Invoke(BattleResolutionMotion motion, string name, params object[] arguments) =>
            typeof(BattleResolutionMotion).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Invoke(motion, arguments);
        private static UIVertex[] Vertices(BattleResolutionGraphic graphic)
        {
            using (var helper = new VertexHelper())
            {
                typeof(BattleResolutionGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { helper });
                var result = new UIVertex[helper.currentVertCount];
                for (int i = 0; i < result.Length; i++) helper.PopulateUIVertex(ref result[i], i);
                return result;
            }
        }
    }
}
