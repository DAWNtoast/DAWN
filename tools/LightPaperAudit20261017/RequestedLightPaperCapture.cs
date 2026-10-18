using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Core.Tutorial;
using ShatteredSail.Presentation;
using UnityEngine;

namespace ShatteredSail.Editor
{
    public static class RequestedLightPaperCapture
    {
        public static void Capture()
        {
            string output = @"D:\Game\Artifacts\LightPaperAudit20261017\RequestedScreens";
            Directory.CreateDirectory(output);
            var report = new UiAuditReadOnlyCapture.MenuReport();
            report.capturedAtUtc = DateTime.UtcNow.ToString("O");
            var method = typeof(UiAuditReadOnlyCapture).GetMethod("CaptureMenu", BindingFlags.Static | BindingFlags.NonPublic);
            var frames = new[]
            {
                new UiAuditReadOnlyCapture.MenuFrame { state = "Encounter", variant = "TutorialEncounter", file = "TutorialEncounter_1920x1080.png", width = 1920, height = 1080 },
                new UiAuditReadOnlyCapture.MenuFrame { state = "Market", variant = "MarketBuy", file = "MarketPurchaseQuantity_1920x1080.png", width = 1920, height = 1080 }
            };
            foreach (var frame in frames)
            {
                try { method.Invoke(null, new object[] { output, frame }); }
                catch (Exception ex) { frame.error = (ex.InnerException ?? ex).ToString(); }
                report.frames.Add(frame);
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
                if (!string.IsNullOrEmpty(frame.error)) throw new Exception(frame.error);
            }
            Debug.Log("REQUESTED_LIGHT_PAPER_CAPTURE_COMPLETE: " + output);
        }

        public static void ConfigureTutorial(CoreGameplayDemoController controller)
        {
            controller.TutorialSession.BeginFirstLeg();
            var encounter = new TutorialEncounter(
                "tutorial_crew_requirement_event", "陌生海图暗语", "特殊船员需求",
                "瞭望手发现海盗正借礁影包抄。首次航行不适合接战，只能立刻转向并返回断锚港。",
                true, false,
                new TutorialChoice("航海士校准星位", "显示隐藏地点，后续 2 航段航速 +1", 2,
                    new TutorialEffect { RevealHidden = true, SpeedBoost = 1f, SpeedBoostLegs = 2 }, "navigator"),
                new TutorialChoice("让普通水手逐行核对", "金币 +12", 4, new TutorialEffect { Gold = 12 }));
            var session = controller.TutorialSession;
            session.GetType().GetProperty("CurrentEncounter", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(session, encounter);
            session.GetType().GetProperty("Phase", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(session, TutorialVoyagePhase.ResolvingEncounter);
            var content = controller.GetComponentsInChildren<RectTransform>(true).Single(x => x.name == "Phase Content");
            for (int i = content.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(content.GetChild(i).gameObject);
            typeof(CoreGameplayDemoController).GetMethod("BuildTutorialEncounterOverlay", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
        }
    }
}
