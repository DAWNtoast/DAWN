using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator BoardingGroundShadow_TutorialTurnKeepsGroundAnchorsAndCommitsOnce()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return LoadResponsiveBattleScene();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CoreVoyageSession session = PrepareGroundShadowBattle(controller);
                yield return WaitResponsiveBattle(controller);
                AssertGroundShadowActors();
                List<GroundShadowPose> poses = SnapshotGroundShadowFigures();
                int turnBefore = session.Battle.Turn;
                var click = RequiredResponsive<Button>("End Battle Turn").onClick;
                click.Invoke(); click.Invoke();
                bool sawImpact = false;
                float deadline = Time.realtimeSinceStartup + 20f;
                while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
                {
                    foreach (GroundShadowPose pose in poses) pose.AssertUnchanged();
                    Assert.That(Object.FindObjectsByType<TutorialBoardingBossAttack>(FindObjectsSortMode.None), Is.Empty,
                        "The canceled captain animation must not be attached to the live battle.");
                    sawImpact |= GameObject.Find("Player Melee Hit Boarding Impact") != null;
                    yield return null;
                }
                Assert.That(controller.IsBattleAnimationPlaying, Is.False);
                Assert.That(sawImpact, Is.True, "The original hit feedback still accompanies an enemy attack.");
                Assert.That(session.Battle.Turn, Is.EqualTo(turnBefore + 1));
                Assert.That(session.Battle.BoardingEnemy.LastAttackExecuted, Is.True);
                Assert.That(session.Battle.BoardingEnemy.LastAttackHealthLost, Is.GreaterThan(0));
                AssertGroundShadowActors();
                string after = GroundShadowRules(session);
                click.Invoke(); yield return null;
                Assert.That(GroundShadowRules(session), Is.EqualTo(after), "A retired callback cannot commit another enemy turn.");
                NavalBattleVisualCapture.Capture(controller, "GroundShadow_StaticTurn_Return.png",
                    "教学Boss真实回合结束，攻击结算一次，双方角色回到待机。");
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator BoardingGroundShadow_ProductionBootContactsAndBeforeAfterCapture()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return LoadResponsiveBattleScene();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CoreVoyageSession session = PrepareGroundShadowBattle(controller);
                yield return WaitResponsiveBattle(controller);
                AssertGroundShadowActors();
                var shadows = Object.FindObjectsByType<BoardingContactShadowGraphic>(FindObjectsSortMode.None);
                Assert.That(shadows.Length, Is.EqualTo(PlayerActors().Length + EnemyActors().Length));
                var poses = SnapshotGroundShadowFigures();
                foreach (BoardingContactShadowGraphic shadow in shadows)
                {
                    RawImage figure = RequiredResponsive<RawImage>(shadow.name.Replace(" Deck Contact Shadow", ""));
                    Assert.That(shadow.ContactCount, Is.EqualTo(figure.GetComponent<BoardingActorGraphic>().Formation ? 6 : 2), figure.name);
                    AssertGroundShadowContacts(shadow, figure);
                    Assert.That(shadow.raycastTarget, Is.False);
                    Assert.That(shadow.transform.GetSiblingIndex(), Is.LessThan(figure.transform.GetSiblingIndex()),
                        "Contact shadows must render behind the original character illustration.");
                }
                string rules = GroundShadowRules(session);
                try
                {
                    foreach (BoardingContactShadowGraphic shadow in shadows) shadow.enabled = false;
                    Canvas.ForceUpdateCanvases();
                    NavalBattleVisualCapture.Capture(controller, "GroundShadow_Before_Hidden.png",
                        "最终紧凑构图的阴影开关对照：隐藏新增脚底阴影，保留同一原角色图与战斗界面。");
                    WriteGroundShadowCloseup("GroundShadow_Before_Hidden.png", "GroundShadow_Before_Feet.png");
                }
                finally { foreach (BoardingContactShadowGraphic shadow in shadows) if (shadow != null) shadow.enabled = true; }
                Canvas.ForceUpdateCanvases();
                NavalBattleVisualCapture.Capture(controller, "GroundShadow_After_Visible.png",
                    "生产接舷阵容与落脚阴影。");
                WriteGroundShadowCloseup("GroundShadow_After_Visible.png", "GroundShadow_After_Feet.png");
                foreach (GroundShadowPose pose in poses) pose.AssertUnchanged();
                Assert.That(GroundShadowRules(session), Is.EqualTo(rules));
                string directory = GroundShadowCaptureDirectory();
                if (directory != null)
                    Assert.That(File.ReadAllBytes(Path.Combine(directory, "GroundShadow_Before_Hidden.png"))
                        .SequenceEqual(File.ReadAllBytes(Path.Combine(directory, "GroundShadow_After_Visible.png"))), Is.False,
                        "The enabled contact-shadow mesh must visibly change the actual Unity image.");
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator BoardingGroundShadow_SpecialistDefeatFadesEachShadowAndRestoresOnCancel()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return LoadResponsiveBattleScene();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                var crew = new[] { "crew_salt_crown_diver_isha", "crew_white_shell_doctor_mirei" }.Select(CrewNarrativeCatalog.FindById).ToArray();
                CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Preparation,
                    crewIds: crew.Select(member => member.Id).ToArray());
                var selected = (List<string>)RamComicField("selectedDepartureSpecialCrew").GetValue(controller);
                selected.Clear();
                foreach (CrewVariantDefinition member in crew)
                {
                    PlayerPrefs.SetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(member.Id), 1);
                    PlayerPrefs.SetInt(CoreGameplayDemoController.CrewRecruitmentKey(member.ProfessionIndex), 1);
                    PlayerPrefs.SetString(CoreGameplayDemoController.ActiveCrewVariantKey(member.ProfessionIndex), member.Id);
                    PlayerPrefs.SetString(CoreGameplayDemoController.CrewLocationKeyForReset(member.Id), session.CurrentPort);
                    selected.Add(member.Id);
                }
                typeof(CoreGameplayDemoController).GetMethod("PersistCrewAtSea", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                typeof(CoreVoyageSession).GetMethod("EnterBattleStage", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(session, new object[] { NavalBattleStage.Boarding });
                RenderResponsiveBattle(controller); yield return WaitResponsiveBattle(controller);
                RectTransform party = RequiredResponsive<RectTransform>("Player Ship Art");
                RawImage[] figures = party.GetComponentsInChildren<RawImage>();
                Assert.That(figures, Has.Length.EqualTo(3));
                var shadows = figures.Select(figure => party.Find(figure.name + " Deck Contact Shadow")
                    .GetComponent<BoardingContactShadowGraphic>()).ToArray();
                for (int index = 0; index < figures.Length; index++)
                    AssertGroundShadowContacts(shadows[index], figures[index]);
                NavalBattleVisualCapture.Capture(controller, "GroundShadow_Specialists_Visible.png",
                    "固定船长和两名特殊船员的落脚阴影。");
                WriteGroundShadowCloseup("GroundShadow_Specialists_Visible.png", "GroundShadow_Specialists_Feet.png");
                var originalShadowGroup = shadows[0].gameObject.AddComponent<CanvasGroup>();
                originalShadowGroup.alpha = .63f;
                var originalFigureGroup = figures[0].gameObject.AddComponent<CanvasGroup>();
                originalFigureGroup.alpha = .72f;
                var poses = figures.Select(figure => new GroundShadowPose(figure.rectTransform))
                    .Concat(shadows.Select(shadow => new GroundShadowPose(shadow.rectTransform))).ToArray();
                var host = new GameObject("Ground shadow resolution fixture", typeof(RectTransform), typeof(CanvasRenderer));
                host.transform.SetParent(party.parent, false);
                try
                {
                    var motion = host.AddComponent<BattleResolutionMotion>();
                    motion.Configure((RectTransform)party.parent, party, controller, BattleResolutionKind.BoardingDefeat);
                    motion.AdvancePresentation(motion.Duration * .85f);
                    var fades = new float[figures.Length];
                    for (int index = 0; index < figures.Length; index++)
                    {
                        fades[index] = figures[index].GetComponent<CanvasGroup>().alpha / (index == 0 ? .72f : 1f);
                        float shadowFade = shadows[index].GetComponent<CanvasGroup>().alpha / (index == 0 ? .63f : 1f);
                        Assert.That(shadowFade, Is.EqualTo(fades[index]).Within(.0001f), "A shadow must fade with its own staggered figure.");
                    }
                    Assert.That(Mathf.Abs(fades[0] - fades[2]), Is.GreaterThan(.01f));
                    foreach (GroundShadowPose pose in poses) pose.AssertUnchanged();
                    motion.CancelAndRestore(); yield return null;
                    Assert.That(originalShadowGroup.alpha, Is.EqualTo(.63f).Within(.0001f));
                    Assert.That(originalFigureGroup.alpha, Is.EqualTo(.72f).Within(.0001f));
                    for (int index = 1; index < figures.Length; index++)
                    {
                        Assert.That(shadows[index].GetComponent<CanvasGroup>(), Is.Null);
                        Assert.That(figures[index].GetComponent<CanvasGroup>(), Is.Null);
                    }
                    foreach (GroundShadowPose pose in poses) pose.AssertUnchanged();
                }
                finally { Object.DestroyImmediate(host); }
                LogAssert.NoUnexpectedReceived();
            }
        }

        private static void AssertGroundShadowContacts(BoardingContactShadowGraphic shadow, RawImage figure)
        {
            Vector3[] profile = BoardingFootShadowProfiles.For(figure.texture, figure.uvRect);
            Vector2[] points = shadow.ContactPoints;
            if (profile == null || profile.Length == 0)
            {
                Assert.That(shadow.ContactCount, Is.EqualTo(2), "Unmeasured individual crew art still needs a separate contact for each foot.");
                Assert.That(points, Has.Length.EqualTo(2));
                Assert.That(Vector2.Distance(points[0], points[1]), Is.GreaterThan(2f));
                foreach (Vector2 point in points)
                {
                    Vector3 world = shadow.rectTransform.TransformPoint(point);
                    float floorY = figure.transform.parent.InverseTransformPoint(world).y;
                    Assert.That(floorY, Is.EqualTo(-BoardingBattleLayout.PartySize.y * .5f).Within(.02f),
                        "Fallback contacts must use GroundFigure's actual floor, not the transparent image margin.");
                }
                return;
            }
            Assert.That(points.Length, Is.EqualTo(profile.Length));
            Assert.That(shadow.ContactCount, Is.EqualTo(points.Length));
            Rect rect = figure.rectTransform.rect;
            for (int index = 0; index < points.Length; index++)
            {
                Assert.That(profile[index].x, Is.InRange(0f, 1f));
                Assert.That(profile[index].y, Is.InRange(0f, .30f), "Boot profiles must describe the feet, not the coat or torso.");
                Assert.That(profile[index].z, Is.GreaterThan(0f));
                Vector3 expected = figure.rectTransform.TransformPoint(new Vector3(
                    rect.xMin + profile[index].x * rect.width, rect.yMin + profile[index].y * rect.height, 0f));
                Vector3 actual = shadow.rectTransform.TransformPoint(points[index]);
                Assert.That(Vector3.Distance(expected, actual), Is.LessThan(.02f), figure.name + " boot " + index);
                for (int previous = 0; previous < index; previous++)
                    Assert.That(Vector2.Distance(points[previous], points[index]), Is.GreaterThan(2f), "Each boot needs a distinct contact.");
            }
        }

        private static void AssertGroundShadowActors()
        {
            Assert.That(Object.FindObjectsByType<TutorialBoardingBossAttack>(FindObjectsSortMode.None), Is.Empty);
            foreach (string side in new[] { "Player", "Enemy" })
            {
                var actors = (side == "Player" ? PlayerActors() : EnemyActors());
                if (side == "Player") Assert.That(actors.Select(a => a.Identity), Does.Contain("captain"));
                else CollectionAssert.AreEquivalent(new[] { "enemy_party_left", "enemy_party_center", "enemy_party_right" }, actors.Select(a => a.Identity).ToArray());
                foreach (var actor in actors)
                {
                    Assert.That(actor.Action, Is.EqualTo(BoardingActorAction.Idle));
                    var figure = actor.GetComponent<RawImage>();
                    Assert.That(figure.enabled, Is.True);
                    Assert.That(figure.texture, Is.SameAs(Resources.Load<Texture2D>("BoardingBattle/Actions/" + actor.Identity)));
                }
            }
        }

        private static CoreVoyageSession PrepareGroundShadowBattle(CoreGameplayDemoController controller)
        {
            var session = new CoreVoyageSession();
            session.ConfigureGuidedTutorialVoyage(CoreGuidedTutorialContent.BossVoyageLeg);
            session.StartVoyage(); session.SetHeading(SailingDirection.East); session.SetDistance(120);
            for (int step = 0; step < 5 && session.Phase == CoreDemoPhase.Sailing; step++) Assert.That(session.Sail(), Is.True);
            Assert.That(session.ActiveEncounter.Id, Is.EqualTo(CoreGuidedTutorialContent.BossEventId));
            Assert.That(session.ResolveChoice(0), Is.True);
            typeof(CoreVoyageSession).GetMethod("StartGuidedBossLesson", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { GuidedBossLesson.FinalBattle });
            typeof(CoreVoyageSession).GetMethod("EnterBattleStage", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { NavalBattleStage.Boarding });
            session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth = 200;
            session.Battle.EnemyBoardingHealth = session.Battle.EnemyMaxBoardingHealth = 200;
            session.Battle.PlayerBlock = session.Battle.EnemyBlock = 0; session.Battle.PhaseTurn = 1;
            typeof(CoreVoyageSession).GetProperty("Morale").SetValue(session, 0);
            typeof(CoreGameplayDemoController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, session);
            Assert.That(session.IsGuidedBossBattle, Is.True);
            Assert.That(session.GuidedBossStep, Is.EqualTo(GuidedBossBattleStep.CourseFinal));
            RenderResponsiveBattle(controller); return session;
        }

        private static string GroundShadowRules(CoreVoyageSession session) => session.Battle.Turn + "|" + session.Battle.PhaseTurn + "|" +
            session.Battle.PlayerBoardingHealth + "|" + session.Battle.EnemyBoardingHealth + "|" + session.Battle.PlayerBlock + "|" +
            session.Battle.EnemyBlock + "|" + session.Battle.CardsPlayedThisTurn + "|" + session.Battle.Energy + "|" + session.Battle.Boarding.ResolvedAttackSequence;

        private sealed class GroundShadowPose
        {
            readonly RectTransform rect; readonly Vector3 position, scale; readonly Quaternion rotation;
            public GroundShadowPose(RectTransform target) { rect = target; position = target.position; scale = target.lossyScale; rotation = target.rotation; }
            public void AssertUnchanged()
            {
                Assert.That(rect, Is.Not.Null);
                Assert.That(Vector3.Distance(position, rect.position), Is.LessThan(.01f), rect.name);
                Assert.That(Vector3.Distance(scale, rect.lossyScale), Is.LessThan(.001f), rect.name);
                Assert.That(Quaternion.Angle(rotation, rect.rotation), Is.LessThan(.01f), rect.name);
            }
        }
        private static List<GroundShadowPose> SnapshotGroundShadowFigures() => new[] { "Player Ship Art", "Enemy Ship Art" }
            .SelectMany(name => RequiredResponsive<RectTransform>(name).GetComponentsInChildren<RawImage>())
            .Select(figure => new GroundShadowPose(figure.rectTransform)).ToList();
        private static string GroundShadowCaptureDirectory()
        {
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-navalVisualCapturePath");
            return index >= 0 && index + 1 < args.Length ? Path.GetFullPath(args[index + 1]) : null;
        }
        private static void WriteGroundShadowCloseup(string full, string close)
        {
            string directory = GroundShadowCaptureDirectory(); if (directory == null) return;
            var source = new Texture2D(2, 2, TextureFormat.RGB24, false); Texture2D crop = null;
            try
            {
                source.LoadImage(File.ReadAllBytes(Path.Combine(directory, full)), false);
                const int x = 80, y = 340, width = 1760, height = 300;
                crop = new Texture2D(width, height, TextureFormat.RGB24, false);
                crop.SetPixels(source.GetPixels(x, y, width, height)); crop.Apply();
                File.WriteAllBytes(Path.Combine(directory, close), crop.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(source); if (crop != null) Object.DestroyImmediate(crop); }
        }
    }
}
