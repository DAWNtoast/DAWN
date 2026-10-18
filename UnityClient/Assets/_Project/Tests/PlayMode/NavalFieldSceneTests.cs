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
        public IEnumerator NavalFieldCard_LiveHandCancelThenConfirmUsesTheChosenSeaCells()
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
            overlay.SelectCell(3, 0);
            Assert.That(RequiredResponsive<Button>("Confirm Naval Card Choice").interactable, Is.False);
            NavalBattleVisualCapture.Capture(controller, "NavalField_01_InvalidPlacement.png", "火场选区逐格标错：远角超出四格，禁止确认且未扣费用");
            RequiredResponsive<Button>("Cancel Naval Card Choice").onClick.Invoke(); yield return null;
            Assert.That(controller.IsNavalCardChoiceOpen, Is.False);
            Assert.That(session.Battle.Energy, Is.EqualTo(energy)); Assert.That(session.Cargo.Powder, Is.EqualTo(powder));
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke(); yield return null;
            overlay = Object.FindFirstObjectByType<NavalCardChoiceOverlay>(); overlay.SelectCell(0, 1);
            RequiredResponsive<Button>("Confirm Naval Card Choice").onClick.Invoke();
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.NavalFields.Cells.Count, Is.EqualTo(4));
            Assert.That(session.Battle.Energy, Is.EqualTo(energy - 2)); Assert.That(session.Cargo.Powder, Is.EqualTo(powder - 1));
            NavalFieldGraphic fields = Object.FindObjectsByType<NavalFieldGraphic>(FindObjectsSortMode.None).Single(g => g.name == "Persistent Naval Fields");
            Assert.That(fields.Cells.Count, Is.EqualTo(4));
            Assert.That(fields.GetComponentsInChildren<Text>().All(t => t.text == "我·火2"), Is.True);
            NavalBattleVisualCapture.Capture(controller, "NavalField_02_CommittedFire.png", "现役战斗确认铺火：四格持久显示归属、体系纹理和剩余期限");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavalFields_AllSixKindsArePersistentAndWaterDirectionIsSelectable()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval, pirateLevel: 10);
            SetNavalFieldTestPose(session, true, 0, 0); SetNavalFieldTestPose(session, false, 2, 0);
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Fire, true, -2, 0, false, 0, 0), Is.True);
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Arcane, true, -1, -2, false, 0, 0), Is.True);
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Curse, true, 1, 0, false, 0, 0), Is.True);
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Thunder, true, -3, -1, false, 0, 0), Is.True);
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Blood, true, 1, -2, false, 0, 0), Is.True);
            SetResponsiveHand(session, "card_school_tide_n01", "card_school_tide_n06", "card_school_thunder_n06");
            typeof(CoreSchoolBattleState).GetProperty("TidePower").SetValue(session.Battle.Schools, 8);
            RenderResponsiveBattle(controller); yield return null;
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke(); yield return null;
            RequiredResponsive<Button>("Naval field direction 1").onClick.Invoke();
            Object.FindFirstObjectByType<NavalCardChoiceOverlay>().SelectCell(0, 2);
            NavalBattleVisualCapture.Capture(controller, "NavalField_03_WaterChoice.png", "水流选择世界向北；四格形状、箭头和合法锚点随实际海图投影");
            RequiredResponsive<Button>("Confirm Naval Card Choice").onClick.Invoke();
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.NavalFields.Cells.Select(c => c.Kind).Distinct().Count(), Is.EqualTo(6));
            Assert.That(session.Battle.NavalFields.Cells.Where(c => c.Kind == CoreNavalFieldKind.Tide).All(c => c.DirectionX == 0 && c.DirectionY == 1), Is.True);
            NavalBattleVisualCapture.Capture(controller, "NavalField_04_SixSchools.png", "六种海面场地同图：颜色、纹理、图标、方向和剩余回合可区分");
            LogAssert.NoUnexpectedReceived();
        }

        private static void SetNavalFieldTestPose(CoreVoyageSession session, bool player, int x, int y) =>
            typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session.Battle, new object[] { player, new VoyagePoint(x * 100f, y * 100f), player ? 0f : 180f });
    }
}
