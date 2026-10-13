using System.Collections;
using System.Linq;
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
        public IEnumerator ChartSeed_EntryUnlocksAfterRecruitmentButNavigationKeepsItHidden_AndOnboardIncidentsNeverBecomeMarkers()
        {
            const string internalSeed = "CHART-DISCLOSURE-FLOW";
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            controller.Session.SetSeed(internalSeed);
            ActiveButton("Port Hotspot Voyage").onClick.Invoke();
            yield return null;
            Assert.That(controller.Session.CanDrawSeaChart, Is.False,
                "The departure panel must apply the actual selected crew before deciding whether to expose chart entry.");
            Assert.That(GameObject.Find("Seed Input"), Is.Null);
            NavalBattleVisualCapture.Capture(controller, "30_Chart_Seed_Locked_Departure.png",
                "没有随船航海士时不提前公开种子，仍可正常出航", "Voyage Setup");

            ActiveButton("Start Voyage").onClick.Invoke();
            yield return null;
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(controller.Session.Map.Seed, Is.Not.EqualTo(internalSeed), "The default random departure must create a fresh chart even without a navigator.");
            string hiddenDepartureSeed = controller.Session.Map.Seed;
            CoreMapNode leak = controller.Session.Map.Nodes.Single(node => node.Encounter.Id == "event_reef_saltwater_leak");
            // Defend against old or incorrectly restored disclosure flags as well.
            leak.Discovered = true;
            leak.IntelRevealed = true;
            leak.Visited = true;
            ActiveButton("Heading East").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Event Node " + leak.Id), Is.Null);
            Assert.That(GameObject.Find("Event Label " + leak.Id), Is.Null);
            Assert.That(GameObject.Find("Event Icon " + leak.Id), Is.Null);
            Assert.That(GameObject.Find("Map Seed"), Is.Null);
            string hash = controller.Session.Map.Hash.ToString("X8");
            foreach (Text visibleText in controller.GetComponentsInChildren<Text>())
            {
                Assert.That(visibleText.text, Does.Not.Contain(internalSeed), visibleText.name);
                Assert.That(visibleText.text, Does.Not.Contain(hiddenDepartureSeed), visibleText.name);
                Assert.That(visibleText.text, Does.Not.Contain(hash), visibleText.name);
            }
            NavalBattleVisualCapture.Capture(controller, "31_Chart_Seed_Locked_Navigation.png",
                "航海标题和日志均不泄露种子；船内进水不作为固定位置标记", "Navigation Chart");

            ActiveButton("Safe Return").onClick.Invoke();
            yield return null;
            ActiveButton("Return To Port").onClick.Invoke();
            yield return null;
            ActiveButton("Port Hotspot Tavern").onClick.Invoke();
            yield return null;
            ActiveButton("Tavern Crew 0").onClick.Invoke();
            yield return null;
            ActiveButton("Recruit Tavern Crew 0").onClick.Invoke();
            yield return null;
            Assert.That(controller.Session.CanDrawSeaChart, Is.True);
            // The first back action closes the crew dossier, retaining the tavern.
            ActiveButton("Return To Port Hub").onClick.Invoke();
            yield return null;
            Assert.That(controller.ActivePortSection, Is.EqualTo("灰鸥酒馆"));
            ActiveButton("Return To Port Hub").onClick.Invoke();
            yield return null;
            ActiveButton("Port Hotspot Voyage").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Use Chart Seed").GetComponent<Toggle>().isOn, Is.False);
            string seed = CoreVoyageSession.CreateChartSeed(0, controller.Session.CurrentPort, "白贝港");
            yield return EnterDepartureChartSeed(seed);
            ActiveButton("Start Voyage").onClick.Invoke();
            yield return null;
            Assert.That(controller.Session.Map.Seed, Is.EqualTo(seed));
            Assert.That(GameObject.Find("Map Seed"), Is.Null);
            foreach (Text visibleText in controller.GetComponentsInChildren<Text>())
            {
                Assert.That(visibleText.text, Does.Not.Contain(seed), visibleText.name);
                Assert.That(visibleText.text, Does.Not.Contain(controller.Session.Map.Hash.ToString("X8")), visibleText.name);
            }
            NavalBattleVisualCapture.Capture(controller, "32_Chart_Seed_Hidden_With_Navigator.png",
                "随船航海士允许输入海图种子，航海界面仍不显示种子与识别码", "Navigation Chart");
        }
    }
}
