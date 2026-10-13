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
        [UnityTest, Timeout(120000)]
        public IEnumerator TreasureFragments_IdentifyAssembleAndChooseSeedFromDeparture()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 100, fragments = 0 });
            typeof(CoreGameplayDemoController).GetMethod("SaveTreasureProgress", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
            ClickAcceptanceButton("Port Hotspot Treasure");
            yield return null;
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(ActiveButton("Treasure Identify Fragment").IsInteractable(), Is.False);
            typeof(CoreGameplayDemoController).GetMethod("OpenGameMaster", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
            yield return null;
            GameObject.Find("Game Master Unlock").GetComponent<Toggle>().isOn = true;
            GameObject.Find("Game Master Treasure Fragments Input").GetComponent<InputField>().text = "4";
            ClickAcceptanceButton("Game Master Set Treasure Fragments");
            Assert.That(session.TreasureFragments, Is.EqualTo(4));
            NavalBattleVisualCapture.Capture(controller, "Treasure_00_GmFragments.png", "GM 设置4片未鉴定碎片并保存，关闭后继续鉴定与拼合", "Game Master Sheet");
            ClickAcceptanceButton("Close Game Master");
            yield return null;
            Assert.That(GameObject.Find("Treasure Workbench Status").GetComponent<Text>().text, Does.Contain("未鉴定碎片 4"));
            Assert.That(ActiveButton("Treasure Identify Fragment").IsInteractable(), Is.True);
            int originalRoute = session.SelectedRouteIndex;
            NavalBattleVisualCapture.Capture(controller, "Treasure_01_Unidentified.png", "隔离测试夹具：4片未鉴定，三图均为半透明底图", "Treasure Panel");
            for (int piece = 0; piece < 4; piece++)
            {
                ClickAcceptanceButton("Treasure Identify Fragment");
                yield return null;
                Assert.That(session.TreasureFragments, Is.EqualTo(3 - piece));
                Assert.That(session.GetIdentifiedTreasureFragments("weathered"), Is.EqualTo(piece + 1));
                Assert.That(GameObject.Find("Treasure Fragment 0 " + piece).GetComponent<RawImage>().color.a, Is.EqualTo(1f));
                Assert.That(session.SelectedRouteIndex, Is.EqualTo(originalRoute));
                Assert.That(session.TreasureMaps, Is.Empty);
                if (piece == 1)
                    NavalBattleVisualCapture.Capture(controller, "Treasure_02_HalfAssembled.png", "两片鉴定归属残旧海图，另两片保持半透明", "Treasure Panel");
            }
            Assert.That(ActiveButton("Treasure Identify Fragment").IsInteractable(), Is.False);
            Assert.That(GameObject.Find("Treasure Route Name 0").GetComponent<Text>().text, Is.EqualTo("残旧藏宝图"));
            Assert.That(GameObject.Find("Treasure Route Progress 0").GetComponent<Text>().text, Does.Contain("4/4").And.Contain("点击合成"));
            NavalBattleVisualCapture.Capture(controller, "Treasure_03_ReadyToAssemble.png", "残旧藏宝图4/4，点击海图即可合成", "Treasure Panel");
            ClickAcceptanceButton("Treasure Route 0");
            yield return null;
            Assert.That(session.GetIdentifiedTreasureFragments("weathered"), Is.Zero);
            Assert.That(session.TreasureMaps.Count, Is.EqualTo(1));
            Assert.That(session.SelectedTreasureMap, Is.Null);
            string seed = session.TreasureMaps[0].Seed;
            Assert.That(GameObject.Find("Treasure Map Details 0").GetComponent<Text>().text, Does.Contain(seed));
            NavalBattleVisualCapture.Capture(controller, "Treasure_04_SeedCreated.png", "合成生成固定种子，尚未选择用于出航", "Treasure Folio");
            ClickAcceptanceButton("Treasure Close Folio");
            yield return null;
            ClickAcceptanceButton("Return To Port Hub");
            yield return null;
            ClickAcceptanceButton("Port Hotspot Voyage");
            yield return null;
            ClickAcceptanceButton("Departure Treasure Seed Picker");
            yield return null;
            NavalBattleVisualCapture.Capture(controller, "Treasure_05_DepartureSeedPicker.png", "出航界面从藏图册选择种子", "Departure Treasure Seed Card");
            ClickAcceptanceButton("Departure Treasure Seed Option 0");
            yield return null;
            Assert.That(session.SelectedTreasureMap.Seed, Is.EqualTo(seed));
            Assert.That(session.TreasureMaps.Count, Is.EqualTo(1), "Selection alone never consumes the map.");
            Assert.That(GameObject.Find("Treasure Departure Art").GetComponent<RawImage>().texture.name, Is.EqualTo("treasure_chart_weathered"));
            yield return PurchaseRecommendedDepartureWaterFood();
            Assert.That(session.CanStartTreasureVoyage(out string reason), Is.True, reason);
            NavalBattleVisualCapture.Capture(controller, "Treasure_06_DepartureReady.png", "已选择藏宝种子，从当前港口出发并返回原港", "Voyage Setup");
            ClickAcceptanceButton("Start Voyage");
            yield return null;
            Assert.That(session.IsTreasureVoyage, Is.True);
            Assert.That(session.ActiveTreasureMap.Seed, Is.EqualTo(seed));
            Assert.That(session.TreasureMaps, Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TreasureFragments_AllThreeRankBoardsShowIndependentProgress()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            controller.Session.RestoreTreasureProgress(new CoreTreasureProgress {
                pirateExperience = 900, fragments = 2,
                identifiedWeatheredFragments = 4, identifiedGildedFragments = 2, identifiedMoonlitFragments = 1
            });
            ClickAcceptanceButton("Port Hotspot Treasure");
            yield return null;
            yield return new WaitForSecondsRealtime(.4f);
            int[] counts = { 4, 2, 1 };
            for (int rank = 0; rank < 3; rank++)
            {
                Assert.That(GameObject.Find("Treasure Route Name " + rank).GetComponent<Text>().text,
                    Is.EqualTo(CoreTreasureHuntCatalog.Ranks[rank].Name + "藏宝图"));
                Assert.That(GameObject.Find("Treasure Route Progress " + rank).GetComponent<Text>().text,
                    Does.Contain(counts[rank] + "/4"));
                for (int piece = 0; piece < 4; piece++)
                    Assert.That(GameObject.Find("Treasure Fragment " + rank + " " + piece).GetComponent<RawImage>().color.a,
                        Is.EqualTo(piece < counts[rank] ? 1f : 0f));
            }
            NavalBattleVisualCapture.Capture(controller, "Treasure_07_ThreeBoards.png", "隔离展示夹具：三种藏宝图分别4/4、2/4、1/4，每图独立计数", "Treasure Panel");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
