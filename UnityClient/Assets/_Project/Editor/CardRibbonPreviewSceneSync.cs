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
        [Serializable] private sealed class RibbonReviewInput { public RibbonMigrationInput persistentPreviewMigration; }
        [Serializable] private sealed class RibbonMigrationInput { public RibbonSceneInput[] scenes; }
        [Serializable] private sealed class RibbonSceneInput { public string path, group, alignment; }
        [Serializable] private sealed class RibbonMigrationReport
        {
            public string scope = "Only the 23 audited card-bearing saved previews. Existing serialized variants retained; boarding restored explicitly. Three old alignment previews use the current legal preparation hand because their old Orlan card has since changed school/stage. No player saves.";
            public List<RibbonSceneResult> scenes = new List<RibbonSceneResult>();
        }
        [Serializable] private sealed class RibbonSceneResult
        {
            public string scene, method;
            public int titles, minimumFontSize;
            public bool serializedVariantPreserved, allTitlesFit;
        }

        public static void SyncCardRibbonPreviewsFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode ||
                !Path.GetFullPath(Path.GetDirectoryName(Application.dataPath)).StartsWith(@"D:\Game\tmp\", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Run only in the isolated stopped validation editor.");
            string[] args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-cardTitlePreviewAuditPath");
            if (option < 0 || option + 1 >= args.Length) throw new ArgumentException("Pass -cardTitlePreviewAuditPath.");
            string output = Path.GetFullPath(args[option + 1]);
            if (!output.StartsWith(@"D:\Game\Artifacts\", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Artifacts output required.");
            var input = JsonUtility.FromJson<RibbonReviewInput>(File.ReadAllText(Path.Combine(output, "code-review.json")));
            if (input.persistentPreviewMigration.scenes.Length != 23) throw new InvalidOperationException("Expected the audited 23-scene scope.");
            var report = new RibbonMigrationReport();
            foreach (RibbonSceneInput entry in input.persistentPreviewMigration.scenes)
            {
                string normalized = entry.path.Replace('\\', '/');
                int assetStart = normalized.IndexOf("Assets/_Project/Scenes/UIPreviewsForMe/", StringComparison.Ordinal);
                if (assetStart < 0) throw new ArgumentException("Scene escaped approved preview scope.");
                string path = normalized.Substring(assetStart);
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                CoreGameplayDemoController controller = RibbonSceneController(scene);
                string variant = RibbonVariant(controller);
                if (path.Contains("/UIBattle_Boarding"))
                {
                    SyncBoardingCrewScene(path.Substring(EditableSceneFolder.Length + 1));
                    scene = SceneManager.GetActiveScene();
                    controller = RibbonSceneController(scene);
                }
                else if (entry.group == "explicit_battle_variant_restoration_required")
                    RefreshRibbonBattleVariant(controller, entry.alignment);
                else controller.RefreshEditorPreview();
                if (RibbonVariant(controller) != variant) throw new InvalidOperationException("Serialized variant changed: " + path);
                PreviewSceneSpec spec = Array.Find(Specs, item => item.Screen == controller.EditorPreviewScreen) ??
                    Array.Find(Specs, item => item.Screen == CoreGameplayPreviewScreen.CardWorkshop);
                PersistGeneratedSceneVisuals(controller.transform, spec);
                ValidateRibbonScene(controller, path, entry.group, report);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Failed to save " + path);
                RepairSavedNavigationCanvasScale(AbsolutePath(path));
                File.WriteAllText(Path.Combine(output, "persistent-preview-migration.json"), JsonUtility.ToJson(report, true));
            }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            // Verify the serialized result, including the effect's saved frame ID,
            // instead of treating the transient rebuilt hierarchy as the result.
            foreach (RibbonSceneResult record in report.scenes)
            {
                Scene scene = EditorSceneManager.OpenScene(record.scene, OpenSceneMode.Single);
                ValidateRibbonScene(RibbonSceneController(scene), record.scene, record.method, null);
            }
            string capture = Path.Combine(output, "PersistentPreviewsFinal02");
            string[] references = report.scenes.Select(item => item.scene.Substring(EditableSceneFolder.Length + 1)).ToArray();
            typeof(UIPreviewSceneAcceptance).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic, null,
                new[] { typeof(string), typeof(string), typeof(string[]), typeof(string) }, null)
                .Invoke(null, new object[] { capture, EditableSceneFolder, references, null });
            Debug.Log("CARD_RIBBON_SAVED_PREVIEWS_OK: " + report.scenes.Count + " scoped scenes saved, reloaded and captured.");
        }

        private static CoreGameplayDemoController RibbonSceneController(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();

        private static string RibbonVariant(CoreGameplayDemoController controller)
        {
            var serialized = new SerializedObject(controller);
            string[] fields = { "editorPreviewScreen", "editorPreviewPort", "editorPreviewRouteIndex", "editorPreviewCrewIndex", "editorPreviewCrewPage", "editorPreviewFlowState" };
            return string.Join("/", fields.Select(field => serialized.FindProperty(field).intValue));
        }

        private static void ValidateRibbonScene(CoreGameplayDemoController controller, string path, string method, RibbonMigrationReport report)
        {
            CardRibbonTitleEffect[] titles = controller.GetComponentsInChildren<CardRibbonTitleEffect>(true);
            if (titles.Length == 0) throw new InvalidOperationException("No actual card titles after migration: " + path);
            foreach (CardRibbonTitleEffect title in titles)
            {
                Text text = title.GetComponent<Text>();
                if (text.font != GameTypography.InterfaceFont || text.fontStyle != FontStyle.Normal || text.fontSize < 15 ||
                    text.rectTransform.sizeDelta != CardFaceLayout.NameSize || !title.GeneratedTextFits(text))
                    throw new InvalidOperationException("Saved title failed: " + path + " / " + text.name + " / " + text.text);
                if (title.isActiveAndEnabled)
                    using (var mesh = new VertexHelper())
                    {
                        title.ModifyMesh(mesh);
                        if (!title.FitsAuthoredRibbon || title.RenderedBounds.height < 16f)
                            throw new InvalidOperationException("Saved curved mesh failed: " + path + " / " + text.text);
                    }
            }
            report?.scenes.Add(new RibbonSceneResult { scene = path, method = method, titles = titles.Length,
                minimumFontSize = titles.Min(title => title.GetComponent<Text>().fontSize), serializedVariantPreserved = true, allTitlesFit = true });
        }

        private static void RefreshRibbonBattleVariant(CoreGameplayDemoController controller, string alignment)
        {
            controller.RefreshEditorPreview();
            CoreVoyageSession session = controller.Session;
            CoreBattleState battle = session.Battle;
            if (battle == null || battle.Stage != NavalBattleStage.Preparation) throw new InvalidOperationException("Expected authored preparation fixture.");
            battle.Alignment = (NavalBattleAlignment)Enum.Parse(typeof(NavalBattleAlignment), alignment);
            battle.DistanceMeters = 600f;
            // The old serialized Orlan face predates the school conversion and
            // is no longer legal in preparation. Keep the current constructor's
            // real opening hand, preserving the purpose and posture of this
            // editor sample instead of inventing a playable obsolete card.
            if (battle.Hand.Count == 0 || battle.Hand.Any(index => !session.GetBattleCard(index).IsAllowedInStage(battle.Stage)))
                throw new InvalidOperationException("The refreshed alignment sample must use a legal current-stage hand.");
            BoardingPreviewInvoke(controller, "Render");
        }
    }
}
