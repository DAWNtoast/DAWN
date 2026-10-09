using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    public static partial class GameplayPreviewSceneBuilder
    {
        private const string MonthlyCalendarBackup = "Artifacts/MonthlyCalendar20261003/preview-backup";

        private static readonly string[] MonthlyComponentReferences =
        {
            "UI_ComponentShop.unity", "UI_ComponentDetail.unity",
            "UIComponentShop/UIComponentShop_OfferBoard.unity",
            "UIComponentShop/UIComponentShop_OfferSoldOut.unity",
            "UIComponentShop/UIComponentShop_RefreshNotice.unity",
            "UIComponentDetail/UIComponentDetail_Stats.unity",
            "UIComponentDetail/UIComponentDetail_PurchaseConfirm.unity",
            "UIComponentDetail/UIComponentDetail_OwnedEquipped.unity",
        };

        private static readonly string[] MonthlyDispatchReferences =
        {
            "UI_Dispatch.unity", "UI_DispatchDeployment.unity",
            "UI_DispatchDeployment_Escort.unity", "UI_DispatchDeployment_Survey.unity",
            "UIDispatch/UIDispatch_MapSelection.unity",
            "UIDispatch/UIDispatch_DispatchReady.unity",
            "UIDispatch/UIDispatch_CrewAssignment.unity",
            "UIDispatchDeployment/UIDispatchDeployment_MissionBriefing.unity",
            "UIDispatchDeployment/UIDispatchDeployment_CrewSelection.unity",
            "UIDispatchDeployment/UIDispatchDeployment_ReadyToLaunch.unity",
            "UIDispatchDeploymentEscort/UIDispatchDeploymentEscort_Briefing.unity",
            "UIDispatchDeploymentEscort/UIDispatchDeploymentEscort_CrewSelected.unity",
            "UIDispatchDeploymentEscort/UIDispatchDeploymentEscort_ReadyToLaunch.unity",
            "UIDispatchDeploymentSurvey/UIDispatchDeploymentSurvey_Briefing.unity",
            "UIDispatchDeploymentSurvey/UIDispatchDeploymentSurvey_CrewSelected.unity",
            "UIDispatchDeploymentSurvey/UIDispatchDeploymentSurvey_ReadyToLaunch.unity",
        };

        private static readonly string[] MonthlyHeaderReferences =
        {
            "UI_PortHub.unity", "UIPortHub/UIPortHub_Overview.unity",
            "UIPortHub/UIPortHub_FacilityFocus.unity", "UIPortHub/UIPortHub_FacilitySettings.unity",
        };

        public static string[] MonthlyCalendarReferencePaths => DepartureProvisionReferencePaths
            .Concat(MonthlyComponentReferences).Concat(NavigationEnduranceReferencePaths)
            .Concat(MonthlyDispatchReferences).Concat(MonthlyHeaderReferences).Distinct().ToArray();

        public static string[] MonthlyCalendarCaptureReferencePaths => DepartureProvisionReferencePaths
            .Concat(ComponentResaleReferencePaths).Concat(new[]
            {
                "UI_ComponentDetail.unity", "UI_Navigation.unity",
                "UINavigation/UINavigation_TreasureExploration.unity",
                "UI_Dispatch.unity", "UI_DispatchDeployment.unity",
                "UI_DispatchDeployment_Escort.unity", "UI_DispatchDeployment_Survey.unity", "UI_PortHub.unity",
            }).Distinct().ToArray();

        public static void SyncMonthlyCalendarFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Monthly calendar synchronization requires stopped edit mode.");
            foreach (string reference in DepartureProvisionReferencePaths)
                SyncDepartureProvisionScene(reference, MonthlyCalendarBackup);
            foreach (string reference in MonthlyCalendarReferencePaths.Except(DepartureProvisionReferencePaths))
                SyncMonthlyCalendarScene(reference);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MONTHLY_CALENDAR_PREVIEW_SYNC: updated " + MonthlyCalendarReferencePaths.Length +
                " authored scenes; room art, charts, encounters and saved scene variants retained.");
        }

        // -executeMethod ShatteredSail.Editor.GameplayPreviewSceneBuilder.SyncAndCaptureMonthlyCalendarFromCommandLine
        // -navalVisualCapturePath <new empty directory>. Graphics must remain enabled.
        public static void SyncAndCaptureMonthlyCalendarFromCommandLine()
        {
            SyncMonthlyCalendarFromCommandLine();
            CaptureMonthlyCalendarFromCommandLine();
        }

        public static void CaptureMonthlyCalendarFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int argument = Array.IndexOf(args, "-navalVisualCapturePath");
            if (argument < 0) argument = Array.IndexOf(args, "-uiPreviewCapturePath");
            if (argument < 0 || argument + 1 >= args.Length)
                throw new ArgumentException("Provide a fresh -navalVisualCapturePath for monthly calendar captures.");
            // Reuse the saved-scene acceptance runner without broadening its other
            // public capture sets or rebuilding any authored reference at capture time.
            MethodInfo capture = typeof(UIPreviewSceneAcceptance).GetMethod("Capture",
                BindingFlags.Static | BindingFlags.NonPublic, null,
                new[] { typeof(string), typeof(string), typeof(string[]) }, null);
            if (capture == null) throw new MissingMethodException("Saved-scene capture overload is unavailable.");
            string result = (string)capture.Invoke(null, new object[]
                { args[argument + 1], EditableSceneFolder, MonthlyCalendarCaptureReferencePaths });
            Debug.Log("MONTHLY_CALENDAR_UI_ACCEPTANCE: " + result);
        }

        private static void SyncMonthlyCalendarScene(string reference)
        {
            string path = EditableSceneFolder + "/" + reference;
            string absolute = AbsolutePath(path);
            if (!File.Exists(absolute)) throw new FileNotFoundException("Missing authored calendar preview", path);
            string backup = AbsolutePath(MonthlyCalendarBackup + "/" + path);
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(absolute, backup);
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            CoreGameplayDemoController destination = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
            // Backdrops and painted chart samples are never replaced by this sync.
            Dictionary<RawImage, string> retainedArt = destination.GetComponentsInChildren<RawImage>(true)
                .Where(image => image.name.EndsWith("Background", StringComparison.Ordinal) ||
                    image.name == "Lookout Ocean" || image.name == "Dispatch Deployment Chart")
                .ToDictionary(image => image, MonthlyArtIdentity);
            Scene stagingScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject staging = null;
            try
            {
                staging = new GameObject("__MonthlyCalendarPreviewStaging");
                SceneManager.MoveGameObjectToScene(staging, stagingScene);
                CoreGameplayDemoController source = staging.AddComponent<CoreGameplayDemoController>();
                CopyProvisionPreviewSettings(destination, source);
                source.RefreshEditorPreview();
                if (!source.HasCompleteEditorPreview)
                    throw new InvalidOperationException("Incomplete generated calendar sample for " + path);
                if (reference.EndsWith("UIComponentShop_RefreshNotice.unity", StringComparison.Ordinal))
                {
                    DateTime current = source.Session.GameDateTime;
                    DateTime nextMonth = new DateTime(current.Year, current.Month, 1, 8, 0, 0).AddMonths(1);
                    source.Session.RestoreGameClock((nextMonth - CoreVoyageSession.GameClockOrigin).TotalHours);
                    MonthlyPreviewRender(source);
                }
                SyncMonthlyHeader(source, destination);
                if (destination.EditorPreviewScreen == CoreGameplayPreviewScreen.ComponentShop)
                    SyncMonthlyComponentOffers(source, destination, reference);
                if (destination.EditorPreviewScreen == CoreGameplayPreviewScreen.Navigation ||
                    destination.EditorPreviewScreen == CoreGameplayPreviewScreen.Encounter)
                    SyncMonthlyNavigationClock(source, destination);
                if (destination.EditorPreviewScreen == CoreGameplayPreviewScreen.Dispatch ||
                    destination.EditorPreviewScreen == CoreGameplayPreviewScreen.DispatchDeployment)
                    SyncMonthlyDispatchText(source, destination);
                foreach (KeyValuePair<RawImage, string> image in retainedArt)
                    if (image.Key == null || MonthlyArtIdentity(image.Key) != image.Value)
                        throw new InvalidOperationException("Authored calendar backdrop changed in " + path);
            }
            finally
            {
                if (staging != null) UnityEngine.Object.DestroyImmediate(staging);
                if (stagingScene.IsValid() && stagingScene.isLoaded) EditorSceneManager.CloseScene(stagingScene, true);
                if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Failed to save " + path);
            RepairSavedNavigationCanvasScale(absolute);
            AssertSavedNavigationCanvasScale(absolute);
        }

        private static string MonthlyArtIdentity(RawImage image) =>
            EditorJsonUtility.ToJson(image) + EditorJsonUtility.ToJson(image.rectTransform);

        private static Transform MonthlyFind(Component root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == name);

        private static void MonthlyPreviewRender(CoreGameplayDemoController controller)
        {
            MethodInfo render = typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic);
            if (render == null) throw new MissingMethodException("Calendar preview render seam is unavailable.");
            render.Invoke(controller, null);
        }

        private static void SyncMonthlyHeader(CoreGameplayDemoController source, CoreGameplayDemoController destination)
        {
            foreach (string name in new[] { "Port Facility Time", "Port Hub Time" })
            {
                Text target = MonthlyFind(destination, name)?.GetComponent<Text>();
                Text generated = MonthlyFind(source, name)?.GetComponent<Text>();
                if (target != null && generated != null) target.text = generated.text;
            }
        }

        private static bool IsMonthlyOfferControl(string name) =>
            name.StartsWith("Component Shop Offer ", StringComparison.Ordinal) ||
            name == "Component Shop Regional Rule" || name == "Component Shop Previous Display" ||
            name == "Component Shop Next Display" || name == "Component Shop Display Page";

        private static void SyncMonthlyComponentOffers(CoreGameplayDemoController source,
            CoreGameplayDemoController destination, string reference)
        {
            Transform generated = MonthlyFind(source, "Component Shop Panel");
            Transform target = MonthlyFind(destination, "Component Shop Panel");
            if (generated == null || target == null) throw new InvalidOperationException("Missing monthly showroom panel.");
            PreviewSceneSpec spec = Array.Find(Specs, item => item.Screen == CoreGameplayPreviewScreen.ComponentShop);
            foreach (Transform old in target.Cast<Transform>().Where(item => IsMonthlyOfferControl(item.name)).ToArray())
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            foreach (Transform control in generated.Cast<Transform>().Where(item => IsMonthlyOfferControl(item.name)))
            {
                PersistGeneratedSceneVisuals(control, spec);
                CopyTreasureLayer(control, target, control.name);
            }
            if (reference.EndsWith("UIComponentShop_OfferSoldOut.unity", StringComparison.Ordinal))
            {
                Transform sold = target.Cast<Transform>().First(item => item.name.StartsWith("Component Shop Offer ", StringComparison.Ordinal));
                sold.GetComponent<Button>().interactable = false;
                Text status = sold.GetComponentsInChildren<Text>(true)
                    .First(item => item.name.StartsWith("Component Shop Plaque Status ", StringComparison.Ordinal));
                status.text = "本月已售出";
            }
        }

        private static void SyncMonthlyNavigationClock(CoreGameplayDemoController source, CoreGameplayDemoController destination)
        {
            Transform generated = MonthlyFind(source, "Lookout Time Ribbon");
            Transform targetHeader = MonthlyFind(destination, "Voyage Header");
            Transform previousClock = MonthlyFind(destination, "Lookout Time Ribbon");
            Image targetOverlay = MonthlyFind(destination, "Lookout Time Overlay")?.GetComponent<Image>();
            Image generatedOverlay = MonthlyFind(source, "Lookout Time Overlay")?.GetComponent<Image>();
            // The clock belongs to the header in both sailing and encounters;
            // encounters may omit the lookout overlay without omitting the clock.
            if (targetHeader == null && previousClock == null)
            {
                Debug.Log("MONTHLY_CALENDAR_PREVIEW_SYNC: no authored navigation header in " +
                    destination.gameObject.scene.path + "; calendar-only page retained.");
                return;
            }
            if (generated == null || targetHeader == null || targetOverlay != null && generatedOverlay == null)
                throw new InvalidOperationException("Missing navigation clock surface.");
            PreviewSceneSpec spec = Array.Find(Specs, item => item.Screen == source.EditorPreviewScreen);
            PersistGeneratedSceneVisuals(generated, spec);
            if (previousClock != null && previousClock.parent != targetHeader)
                UnityEngine.Object.DestroyImmediate(previousClock.gameObject);
            CopyTreasureLayer(generated, targetHeader, "Lookout Time Ribbon");
            NavigationDashboardLayout.Attach((RectTransform)targetHeader.parent);
            if (targetOverlay != null) targetOverlay.color = generatedOverlay.color;
            Text heading = MonthlyFind(destination, "Lookout Heading")?.GetComponent<Text>();
            if (heading != null)
            {
                int separator = heading.text.LastIndexOf('·');
                if (separator >= 0 && Enum.GetValues(typeof(NavigationTimeOfDay)).Cast<NavigationTimeOfDay>()
                    .Any(time => heading.text.Substring(separator + 1).Trim() == NavigationTimeOfDayUtility.Label(time)))
                    heading.text = heading.text.Substring(0, separator).TrimEnd();
            }
        }

        private static void SyncMonthlyDispatchText(CoreGameplayDemoController source, CoreGameplayDemoController destination)
        {
            foreach (Text target in destination.GetComponentsInChildren<Text>(true))
            {
                if (!target.name.StartsWith("Dispatch ", StringComparison.Ordinal) &&
                    !target.name.StartsWith("Active Dispatch ", StringComparison.Ordinal)) continue;
                if (target.name == "Dispatch Deployment Mission Rule" || target.name == "Active Dispatch Badge")
                {
                    Text generated = MonthlyFind(source, target.name)?.GetComponent<Text>();
                    if (generated != null) target.text = generated.text;
                }
                // Keep authored crew selections, mission identity and chosen duration.
                target.text = target.text.Replace(" 游戏小时", " 小时").Replace(" 现实小时", " 小时").Replace(" 小时", " 现实小时");
            }
        }
    }
}
