using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator ArtOptimization02_SixSeasShareClockAndRefreshRetainedLighting()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            SeaTerrainType[] terrains = { SeaTerrainType.OpenWater, SeaTerrainType.Beach, SeaTerrainType.Reef,
                SeaTerrainType.Strait, SeaTerrainType.FogBank, SeaTerrainType.StormFront };
            double[] hours = { 0d, 10.5d, 14d };
            string[] bands = { "Day", "Dusk", "Night" };
            MethodInfo refreshRetained = typeof(CoreGameplayDemoController).GetMethod("TryRefreshRetainedBattleView",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(refreshRetained, Is.Not.Null);
            foreach (SeaTerrainType terrain in terrains)
            {
                CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval, gameHours: 0d);
                ArtOptimizationSet(session, "BattleTerrain", terrain);
                RenderResponsiveBattle(controller);
                yield return ArtOptimizationWaitForStableBattle(controller);
                NavalTacticalBoardView retained = RequiredResponsive<NavalTacticalBoardView>("Naval Tactical Board");
                Vector3 playerPose = RequiredResponsive<Image>("Player Ship Art").transform.localPosition;
                for (int index = 0; index < hours.Length; index++)
                {
                    ArtOptimizationSet(session, "ElapsedGameHours", hours[index]);
                    // Render deliberately rebuilds the phase; exercise the existing
                    // production refresh used after actions to retain this board.
                    Assert.That((bool)refreshRetained.Invoke(controller, null), Is.True,
                        "The ordinary battle refresh must retain the current battle view.");
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    var board = RequiredResponsive<NavalTacticalBoardView>("Naval Tactical Board");
                    Assert.That(board, Is.SameAs(retained), "The retained battle must refresh without reconstruction.");
                    Assert.That(board.EnvironmentLight.Phase, Is.EqualTo(NavigationTimeOfDayUtility.FromVoyageHours(hours[index])));
                    Assert.That(RequiredResponsive<RawImage>("Shared tactical sea").texture,
                        Is.SameAs(NavalTacticalArtCatalog.ForSea(terrain)));
                    Assert.That(RequiredResponsive<Image>("Player Ship Art").transform.localPosition, Is.EqualTo(playerPose));
                    Assert.That(RequiredResponsive<Image>("Player Ship Art").color, Is.EqualTo(board.EnvironmentLight.ShipTint));
                    Assert.That(RequiredResponsive<Image>("Enemy Ship Art").color, Is.EqualTo(board.EnvironmentLight.ShipTint));
                    NavalBattleVisualCapture.Capture(controller, "Art02_Naval_" + terrain + "_" + bands[index] + ".png",
                        terrain + " / " + NavigationTimeOfDayUtility.ClockLabel(hours[index]) + " / cached battle lighting");
                }
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ArtOptimization02_SameSessionClockSurvivesSailingNavalAndBoarding()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            double[] hours = { 0d, 10.5d, 14d };
            string[] bands = { "Day", "Dusk", "Night" };
            for (int index = 0; index < hours.Length; index++)
            {
                CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval, gameHours: hours[index]);
                ArtOptimizationSet(session, "ElapsedGameHours", hours[index]);
                SeaTerrainType terrain = session.Map.TerrainAt(session.Position);
                ArtOptimizationSet(session, "BattleTerrain", terrain);
                ArtOptimizationSet(session, "Phase", CoreDemoPhase.Sailing);
                RenderResponsiveBattle(controller);
                yield return null;
                yield return null;
                NavalBattleVisualCapture.Capture(controller, "Art02_Transition_" + bands[index] + "_1_Sailing.png",
                    "Same session / " + terrain + " / " + NavigationTimeOfDayUtility.ClockLabel(hours[index]), "Forward Viewport");

                ArtOptimizationSet(session, "Phase", CoreDemoPhase.Battle);
                RenderResponsiveBattle(controller);
                yield return ArtOptimizationWaitForStableBattle(controller);
                NavalBattleVisualCapture.Capture(controller, "Art02_Transition_" + bands[index] + "_2_Naval.png",
                    "Same session / " + terrain + " / " + NavigationTimeOfDayUtility.ClockLabel(hours[index]));

                session.Battle.Stage = NavalBattleStage.Boarding;
                RenderResponsiveBattle(controller);
                yield return ArtOptimizationWaitForStableBattle(controller);
                NavalBattleVisualCapture.Capture(controller, "Art02_Transition_" + bands[index] + "_3_Boarding.png",
                    "Same session / " + terrain + " / " + NavigationTimeOfDayUtility.ClockLabel(hours[index]));
                Assert.That(session.ElapsedGameHours, Is.EqualTo(hours[index]), "Visual transitions must not advance the voyage clock.");
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static void ArtOptimizationSet(object target, string property, object value) =>
            target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .GetSetMethod(true).Invoke(target, new[] { value });

        [UnityTest]
        public IEnumerator ArtOptimization07_BoardingFeedbackKeepsStaticActorsAndOriginalTimeline()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval, gameHours: 0d);
            session.Battle.Stage = NavalBattleStage.Boarding;
            RenderResponsiveBattle(controller);
            yield return ArtOptimizationWaitForStableBattle(controller);
            RectTransform stage = RequiredResponsive<RectTransform>("Battle Card");
            RectTransform player = RequiredResponsive<RectTransform>("Player Ship Art");
            RectTransform enemy = RequiredResponsive<RectTransform>("Enemy Ship Art");
            Vector3 playerPosition = player.localPosition, enemyPosition = enemy.localPosition;
            foreach (BoardingCombatFxKind kind in new[] { BoardingCombatFxKind.Slash, BoardingCombatFxKind.Guard,
                BoardingCombatFxKind.GuardBreak, BoardingCombatFxKind.Heal })
            {
                RectTransform target = kind == BoardingCombatFxKind.Heal || kind == BoardingCombatFxKind.Guard ? player : enemy;
                var fx = (BoardingCombatFxGraphic)typeof(CoreGameplayDemoController)
                    .GetMethod("CreateBoardingCombatFx", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { "Art07 " + kind, stage, target, kind, Color.white, 1f });
                int children = fx.GetComponentsInChildren<Transform>(true).Length;
                foreach (float progress in new[] { .18f, .50f, .82f })
                {
                    // Sample the production effect's own normalized clock, without changing durations or Core state.
                    fx.Progress = progress;
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    Assert.That(player.localPosition, Is.EqualTo(playerPosition));
                    Assert.That(enemy.localPosition, Is.EqualTo(enemyPosition));
                    Assert.That(fx.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(children));
                    RawImage accent = fx.GetComponentInChildren<RawImage>();
                    Assert.That(accent, Is.Not.Null);
                    Assert.That(accent.texture, Is.SameAs(Resources.Load<Texture2D>("BattleVfx/BoardingRemaster/boarding_fx_v3")));
                    Assert.That(accent.raycastTarget, Is.False);
                    NavalBattleVisualCapture.Capture(controller, "Art07_" + kind + "_" + Mathf.RoundToInt(progress * 100f) + ".png",
                        "Production boarding effect at normalized progress " + progress);
                }
                fx.Progress = 1f;
                Assert.That(fx.GetComponentInChildren<RawImage>().color.a, Is.Zero);
                Object.Destroy(fx.gameObject);
                yield return null;
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ArtOptimization08_CompactStatusMarksKeepValuesAndHoverAt34Units()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval, gameHours: 0d);
            session.Battle.Stage = NavalBattleStage.Boarding;
            RenderResponsiveBattle(controller);
            yield return ArtOptimizationWaitForStableBattle(controller);
            var statuses = new[] {
                new CoreBattleStatus("retained_guard", "保留格挡", "12", "保留格挡角标验收样本。", CoreBattleStatusKind.Guard),
                new CoreBattleStatus("retained_cards", "保留手牌", "2", "保留手牌角标验收样本。", CoreBattleStatusKind.Draw),
                new CoreBattleStatus("fire_retaliation", "燃烧反击", "4", "火焰来源角标验收样本。", CoreBattleStatusKind.Retaliation),
                new CoreBattleStatus("curse_counter", "咒术反击", "3", "骷髅来源角标验收样本。", CoreBattleStatusKind.Curse),
                new CoreBattleStatus("tide_guard", "潮汐格挡", "8", "护盾来源角标验收样本。", CoreBattleStatusKind.Tide),
                new CoreBattleStatus("precharge", "预充能", "2", "连锁来源角标验收样本。", CoreBattleStatusKind.Thunder)
            };
            BattleStatusRow row = RequiredResponsive<BattleStatusRow>("Player Battle Status Row");
            row.SetStatuses(statuses);
            yield return null;
            Assert.That(row.VisibleStatusCount, Is.EqualTo(statuses.Length));
            foreach (BattleStatusIconGraphic icon in row.GetComponentsInChildren<BattleStatusIconGraphic>())
                Assert.That(icon.rectTransform.rect.width, Is.EqualTo(BattleStatusRow.DefaultIconSize).Within(.1f));
            NavalBattleVisualCapture.Capture(controller, "Art08_StatusMarks_34.png", "34-unit status row / larger primary art and simple corner marks");
            Transform badge = row.transform.Find("Status retained_guard");
            Assert.That(badge, Is.Not.Null);
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, badge.position) };
            ExecuteEvents.Execute(badge.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return null;
            var tooltip = RequiredResponsive<RectTransform>("Battle Status Tooltip");
            Assert.That(string.Join(" ", tooltip.GetComponentsInChildren<Text>().Select(text => text.text)), Does.Contain("保留格挡"));
            NavalBattleVisualCapture.Capture(controller, "Art08_StatusMarks_Hover.png", "Status explanation retained; resources are untouched");
            ExecuteEvents.Execute(badge.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ArtOptimization09_MenuCheckmarkAndPaperDropdown()
        {
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
            yield return null;
            yield return null;
            ActiveButton("Settings").onClick.Invoke();
            yield return null;
            Toggle toggle = RequiredResponsive<Toggle>("Full Screen");
            bool original = toggle.isOn;
            Sprite check = ((Image)toggle.graphic).sprite;
            Assert.That(check.name, Is.EqualTo("Paper Control Checkmark"));
            foreach (bool selected in new[] { false, true })
            {
                toggle.SetIsOnWithoutNotify(selected);
                yield return new WaitForSecondsRealtime(.15f);
                NavalBattleVisualCapture.Capture(null, "Art09_Settings_" + (selected ? "Checked" : "Unchecked") + ".png",
                    "Main-menu settings / native checkmark / settings not applied", "Settings Card");
            }
            toggle.SetIsOnWithoutNotify(original);
            ActiveButton("Back From Settings").onClick.Invoke();
            yield return null;
            ActiveButton("Achievements").onClick.Invoke();
            yield return null;
            Dropdown category = RequiredResponsive<Dropdown>("Achievements Category");
            Assert.That(category.GetComponent<Image>().sprite.name, Is.EqualTo("Market Light Control"));
            NavalBattleVisualCapture.Capture(null, "Art09_Achievements_Closed.png", "Achievements / shared paper controls", "Achievements Card");
            category.Show();
            yield return new WaitForSecondsRealtime(.2f);
            NavalBattleVisualCapture.Capture(null, "Art09_Achievements_Open.png", "Achievements / category options and selection check", "Achievements Card");
            category.Hide();
            ActiveButton("Back From Achievements").onClick.Invoke();
            yield return null;
            Assert.That(ActiveButton("Settings"), Is.Not.Null, "The main menu retains its text Settings action.");
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator ArtOptimizationWaitForStableBattle(CoreGameplayDemoController controller)
        {
            yield return WaitResponsiveBattle(controller);
            float deadline = Time.realtimeSinceStartup + 6f;
            while (controller.GetComponentsInChildren<BattleHandFlightMotion>(false).Any(motion => motion.IsPlaying) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.GetComponentsInChildren<BattleHandFlightMotion>(false).Any(motion => motion.IsPlaying), Is.False);
            Canvas.ForceUpdateCanvases();
        }
    }
}
