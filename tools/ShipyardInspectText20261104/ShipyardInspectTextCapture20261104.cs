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
    // Isolated visual capture only. Uses production UI and the existing
    // fixture's save preservation and real Unity render capture.
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator ShipyardInspectText20261104_CaptureStatesAndOpenDossier()
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
            NavalBattleVisualCapture.Capture(controller, "01_Normal.png", "Royal shipyard inspect action without its wooden backing", "Shipyard Gallery Plaque");
            Button inspect = ActiveButton("Shipyard Inspect Vessel");
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(inspect.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(.35f);
            NavalBattleVisualCapture.Capture(controller, "02_Hover.png", "Text-only inspect action pointer hover", "Shipyard Gallery Plaque");
            ExecuteEvents.Execute(inspect.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            yield return ClickShipyardControl("Shipyard Inspect Vessel");
            yield return new WaitForSecondsRealtime(.35f);
            NavalBattleVisualCapture.Capture(controller, "03_DossierOpened.png", "Clicking the text action opens the live vessel dossier", "Shipyard Detail");
            LogAssert.NoUnexpectedReceived();
        }
    }
}