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
        public IEnumerator ShipyardVesselButtons20261104_CaptureStates()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            ProfileGoldPersistence.Save(9000);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            controller.Session.RestoreProfile(controller.Session.ProfileGold, "灰鸥港");
            yield return ClickShipyardControl("Port Hotspot Shipyard");
            yield return new WaitForSecondsRealtime(.35f);
            NavalBattleVisualCapture.Capture(controller, "01_Normal.png", "Royal shipyard vessel navigation controls", "Shipyard Gallery Plaque");
            Button previous = ActiveButton("Shipyard Previous Vessel");
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(previous.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(.35f);
            NavalBattleVisualCapture.Capture(controller, "02_Hover.png", "Previous vessel pointer hover", "Shipyard Gallery Plaque");
            ExecuteEvents.Execute(previous.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            yield return new WaitForSecondsRealtime(.35f);
            NavalBattleVisualCapture.Capture(controller, "03_Pressed.png", "Previous vessel held pressed", "Shipyard Gallery Plaque");
            ExecuteEvents.Execute(previous.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(previous.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            EventSystem.current.SetSelectedGameObject(null);
            LogAssert.NoUnexpectedReceived();
        }
    }
}