// Scratch-only PlayMode audit. Install alongside the production
// NavigationInterfaceRefreshTests.cs in the isolated validation project.
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator NavigationInterfaceRefreshCapture_RepeatedMapClicks()
        {
            // Run all production assertions first. The capture deliberately
            // cannot cause an extra canvas layout before regression inspection.
            yield return NavigationInterfaceRefresh_RepeatedMapClicksKeepCurrentDashboardSkin();
            yield return new WaitForSecondsRealtime(.35f);
            NavigationInterfaceRefreshVisualCapture.Capture(
                Object.FindFirstObjectByType<CoreGameplayDemoController>(),
                "AfterTwoMapClicks_1920x1080.png", "连续两次海图点击后的实际航海界面");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavigationInterfaceRefreshCapture_CompassClick()
        {
            yield return NavigationInterfaceRefresh_CompassPointerClickKeepsSkinAndChangesHeading();
            yield return new WaitForSecondsRealtime(.35f);
            NavigationInterfaceRefreshVisualCapture.Capture(
                Object.FindFirstObjectByType<CoreGameplayDemoController>(),
                "AfterCompassClick_1920x1080.png", "点击罗盘南向后的实际航海界面");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
