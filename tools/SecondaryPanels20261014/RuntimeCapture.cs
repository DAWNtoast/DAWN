using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    // Installed only in the isolated project's test assembly. Existing preserving
    // SetUp/TearDown and checkpoint scope own all fixture data and restoration.
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator SecondaryPanels_RuntimeProductionScreens()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
                yield return null; yield return null;
                ClickAcceptanceButton("Settings"); yield return null;
                Assert.That(GameObject.Find("Settings Card"), Is.Not.Null);
                NavalBattleVisualCapture.Capture(null,"01_MainSettings.png","运行态主菜单设置：市场纸面与控件，无已删除的说明。","Settings Card");
                ClickAcceptanceButton("Back From Settings"); yield return null;
                ClickAcceptanceButton("Achievements"); yield return null;
                NavalBattleVisualCapture.Capture(null,"02_Achievements.png","运行态船长成就：八行徽章、分类筛选和分页。","Achievements Card");
                ClickAcceptanceButton("Back From Achievements"); yield return null;

                yield return LoadProvisioningFixture();
                var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                ClickAcceptanceButton("Port Hub Settings"); yield return null;
                Assert.That(game.GetComponentsInChildren<Text>(false).Any(label => label.text.Contains("应用后生效；关闭放弃调整")), Is.False);
                NavalBattleVisualCapture.Capture(game,"04_PortSettings.png","运行态港口设置：市场纸面与控件，无已删除的说明。","Port Settings Sheet");
                ClickAcceptanceButton("Close Port Settings"); yield return null;

                yield return LoadVoyageCargoFixture();
                game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                ClickAcceptanceButton("Voyage Cargo"); yield return null;
                NavalBattleVisualCapture.Capture(game,"03_VoyageCargo.png","运行态船舱：普通物品九宫格，无选择状态。","Voyage Cargo Sheet");
                ClickAcceptanceButton("Voyage Cargo Item water"); yield return null;
                NavalBattleVisualCapture.Capture(game,"05_VoyageCargoSelected.png","运行态船舱：选择淡水，显示数量、体积与丢弃控件；未执行丢弃。","Voyage Cargo Sheet");
                ClickAcceptanceButton("Close Voyage Cargo"); yield return null;
                LogAssert.NoUnexpectedReceived();
            }
        }
    }
}
