using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator NavigationSupplyEndurance_RefreshesAfterSailingAndEncounterRationChanges()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var session = controller.Session;
            session.SetSeed("SUPPLY-ENDURANCE-FLOW");
            session.Cargo.Water = 80;
            session.Cargo.Food = 120;
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            ClickAcceptanceButton("Start Voyage"); yield return null;
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            AssertEnduranceReadout(controller);
            string initialReadout = GameObject.Find("Water Food Endurance").GetComponent<Text>().text;
            NavalBattleVisualCapture.Capture(controller, "01_Navigation_Supply_Endurance.png",
                "左上海图标题旁分别显示水和食物剩余供给时间", "Navigation Chart");

            // Retain the generated sea and real sailing costs, while keeping
            // this UI regression independent of random encounter choices.
            foreach (CoreMapNode node in session.Map.Nodes) node.Visited = true;
            session.SetCourseTo(session.Map.Destination);
            GameObject.Find("Distance Slider").GetComponent<Slider>().value = 170f;
            double before = session.WaterSupplyHours;
            ClickAcceptanceButton("Execute Sail");
            yield return WaitForNavigationMotionCompletion(controller);
            Assert.That(session.TotalHours, Is.GreaterThan(1d));
            Assert.That(session.WaterSupplyHours, Is.EqualTo(before - session.TotalHours).Within(0.00001d));
            AssertEnduranceReadout(controller);
            Assert.That(GameObject.Find("Water Food Endurance").GetComponent<Text>().text, Is.Not.EqualTo(initialReadout));
            NavalBattleVisualCapture.Capture(controller, "02_Endurance_After_Sailing.png",
                "真实执行航行后同步扣减供给时间", "Navigation Chart");

            // An authored encounter fixture exercises the actual choice button,
            // elapsed game time and its independent water/food cargo effects.
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session,
                new CoreEncounter("endurance-ui-event", "补给检查", "事件", "检查饮水，并整理可食用物资。", true, false, 0, 0,
                    new CoreEncounterChoice("检查物资", "耗时 4 小时，淡水 -2，食物 +5", 4d,
                        new VoyageEffect { Water = -2, Food = 5 })));
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, CoreDemoPhase.Encounter);
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
            yield return null;
            AssertEnduranceReadout(controller);
            NavalBattleVisualCapture.Capture(controller, "03_Encounter_Supply_Endurance.png",
                "处理事件时左上海图标题旁继续显示供给时间", "Navigation Chart");
            double waterBeforeEvent = session.WaterSupplyHours;
            double foodBeforeEvent = session.FoodSupplyHours;
            double hoursPerRation = 24d / session.EstimateDailyRationConsumption(session.EffectiveCrewCount, session.HasCook);
            ClickAcceptanceButton("Encounter Choice 0"); yield return null;
            Assert.That(session.WaterSupplyHours, Is.EqualTo(waterBeforeEvent - 4d - 2d * hoursPerRation).Within(0.00001d));
            Assert.That(session.FoodSupplyHours, Is.EqualTo(foodBeforeEvent - 4d + 5d * hoursPerRation).Within(0.00001d));
            AssertEnduranceReadout(controller);
            NavalBattleVisualCapture.Capture(controller, "04_Endurance_After_Encounter.png",
                "事件耗时与水粮增减同时反映在供给时间", "Navigation Chart");
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertEnduranceReadout(CoreGameplayDemoController controller)
        {
            Text text = GameObject.Find("Water Food Endurance").GetComponent<Text>();
            var formatter = typeof(CoreGameplayDemoController).GetMethod("FormatSupplyEnduranceDuration",
                BindingFlags.Static | BindingFlags.NonPublic);
            string water = (string)formatter.Invoke(null, new object[] { controller.Session.WaterSupplyHours });
            string food = (string)formatter.Invoke(null, new object[] { controller.Session.FoodSupplyHours });
            Assert.That(text.text, Is.EqualTo("淡水 " + water + "　·　食物 " + food));
            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1f));
            Assert.That(text.transform.parent.parent.name, Is.EqualTo("Navigation Chart"));
            Assert.That(GameObject.Find("Supply Endurance Summary"), Is.Null);
            Assert.That(GameObject.Find("Cargo Status"), Is.Null);
            foreach (string retired in new[] { "Navigation Journal", "Voyage Journal Scroll", "Voyage Log", "Log Title", "Voyage Cargo Summary" })
                Assert.That(GameObject.Find(retired), Is.Null, retired + " must stay removed after sailing and event resolution.");
        }

    }
}
