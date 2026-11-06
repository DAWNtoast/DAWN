using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator NavalFieldCard_LiveHandCancelThenClickUsesTheChosenSeaCells()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval, pirateLevel: 10);
            SetNavalFieldTestPose(session, true, 0, 0); SetNavalFieldTestPose(session, false, 2, 0);
            SetResponsiveHand(session, "card_school_fire_n01", "card_brace");
            session.Cargo.Powder = 20;
            RenderResponsiveBattle(controller); yield return null;
            int energy = session.Battle.Energy, powder = session.Cargo.Powder;
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke(); yield return null;
            Assert.That(controller.IsNavalCardChoiceOpen, Is.True);
            NavalCardChoiceOverlay overlay = Object.FindFirstObjectByType<NavalCardChoiceOverlay>();
            overlay.SelectCell(5, 0);
            Assert.That(overlay.Draft.HasAnchor, Is.False);
            NavalBattleVisualCapture.Capture(controller, "NavalField_01_InvalidPlacement.png", "火场锚点超出四格，禁止铺设且未扣费用");
            RequiredResponsive<Button>("Cancel Naval Card Choice").onClick.Invoke(); yield return null;
            Assert.That(controller.IsNavalCardChoiceOpen, Is.False);
            Assert.That(session.Battle.Energy, Is.EqualTo(energy)); Assert.That(session.Cargo.Powder, Is.EqualTo(powder));
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke(); yield return null;
            overlay = Object.FindFirstObjectByType<NavalCardChoiceOverlay>(); overlay.SelectCell(0, 1);
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.NavalFields.Cells.Count, Is.EqualTo(9));
            Assert.That(session.Battle.Energy, Is.EqualTo(energy - 2)); Assert.That(session.Cargo.Powder, Is.EqualTo(powder - 1));
            NavalFieldGraphic fields = Object.FindObjectsByType<NavalFieldGraphic>(FindObjectsSortMode.None).Single(g => g.name == "Persistent Naval Fields");
            Assert.That(fields.Cells.Count, Is.EqualTo(9));
            Assert.That(fields.GetComponentsInChildren<Text>().All(t => t.text == "5" && t.alignment == TextAnchor.LowerRight), Is.True);
            fields.SetMotion(.6f, null);
            NavalBattleVisualCapture.Capture(controller, "NavalField_02_CommittedFire.png", "场地右下角仅显示剩余回合，图案与海面交替");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavalFields_AllSixKindsArePersistentAndWaterDirectionIsSelectable()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval, pirateLevel: 10);
            SetNavalFieldTestPose(session, true, 0, 0); SetNavalFieldTestPose(session, false, 2, 0);
            // Place the largest field first so the smaller school fields remain visible.
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Thunder, true, -3, -1, false, 0, 0), Is.True);
            Assert.That(session.Battle.NavalFields.Cells.Count, Is.EqualTo(49));
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Fire, true, -2, 0, false, 0, 0), Is.True);
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Arcane, true, -1, -2, false, 0, 0), Is.True);
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Curse, true, 1, 0, false, 0, 0), Is.True);
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Blood, true, 1, -2, false, 0, 0), Is.True);
            SetResponsiveHand(session, "card_school_tide_n01", "card_school_tide_n06", "card_school_thunder_n06");
            typeof(CoreSchoolBattleState).GetProperty("TidePower").SetValue(session.Battle.Schools, 8);
            RenderResponsiveBattle(controller); yield return null;
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke(); yield return null;
            Object.FindFirstObjectByType<NavalCardChoiceOverlay>().SelectCell(0, 2);
            NavalBattleVisualCapture.Capture(controller, "NavalField_03_WaterChoice.png", "水流先选锚点，预览十六格，再选择世界方向");
            RequiredResponsive<Button>("Naval field direction 1").onClick.Invoke();
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.NavalFields.Cells.Select(c => c.Kind).Distinct().Count(), Is.EqualTo(6));
            Assert.That(session.Battle.NavalFields.Cells.Where(c => c.Kind == CoreNavalFieldKind.Tide).All(c => c.DirectionX == 0 && c.DirectionY == 1), Is.True);
            NavalFieldGraphic fields = Object.FindObjectsByType<NavalFieldGraphic>(FindObjectsSortMode.None).Single(g => g.name == "Persistent Naval Fields");
            Assert.That(fields.GetComponentsInChildren<Text>().All(t => int.TryParse(t.text, out _) && t.alignment == TextAnchor.LowerRight), Is.True);
            fields.SetMotion(.6f, null);
            NavalFieldPatternGraphic[] paintings = fields.GetComponentsInChildren<NavalFieldPatternGraphic>();
            Assert.That(paintings.Count(p => p.PatternCellCount > 0), Is.EqualTo(6));
            Assert.That(paintings.Where(p => p.PatternCellCount > 0).All(p =>
                p.mainTexture == NavalFieldArtCatalog.ForField(p.Kind) && p.PatternOpacity > 0f && !p.raycastTarget), Is.True);
            NavalBattleVisualCapture.Capture(controller, "NavalField_04_SixSchools.png", "每格显示自身场地图案与右下角回合数，只移除归属和类型文字");
            fields.SetMotion(1.9f, null);
            Assert.That(paintings.All(p => p.PatternOpacity == 0f), Is.True);
            NavalBattleVisualCapture.Capture(controller, "NavalField_05_ClearSea.png", "场地图案间歇隐藏，海面保留轻边界与右下角剩余回合");
            fields.SetMotion(3f, null);
            NavalBattleVisualCapture.Capture(controller, "NavalField_06_PatternsReturn.png", "海面间歇结束后，各格恢复显示对应场地图案");
            LogAssert.NoUnexpectedReceived();
        }

        private static void SetNavalFieldTestPose(CoreVoyageSession session, bool player, int x, int y) =>
            typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session.Battle, new object[] { player, new VoyagePoint(x * 100f, y * 100f), player ? 0f : 180f });
    }
}
