using System.Collections;
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
    // Only installed in the borrowed validation project. Uses production UI and
    // the existing fixture's save preservation and live Unity render capture.
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator DepartureVesselButtons20261104_CaptureStates()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            ProfileGoldPersistence.Save(9000);
            PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(1), 1);
            SeedShipPortLocationsForFixture("断锚港", 0, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return ClickShipyardControl("Port Hotspot Voyage");
            yield return new WaitForSecondsRealtime(.35f);
            NavalBattleVisualCapture.Capture(controller, "01_Normal.png", "Normal vessel navigation controls", "Voyage Setup");
            Button previous = ActiveButton("Departure Previous Vessel");
            Button next = ActiveButton("Departure Next Vessel");
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(previous.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(.35f);
            NavalBattleVisualCapture.Capture(controller, "02_Hover.png", "Previous vessel pointer hover", "Voyage Setup");
            ExecuteEvents.Execute(previous.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            yield return new WaitForSecondsRealtime(.35f);
            NavalBattleVisualCapture.Capture(controller, "03_Pressed.png", "Previous vessel held pressed", "Voyage Setup");
            ExecuteEvents.Execute(previous.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(previous.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            EventSystem.current.SetSelectedGameObject(null);
            previous.interactable = false;
            next.interactable = false;
            yield return new WaitForSecondsRealtime(.35f);
            NavalBattleVisualCapture.Capture(controller, "04_Disabled.png", "Both controls disabled", "Voyage Setup");
            LogAssert.NoUnexpectedReceived();
        }
    }
}