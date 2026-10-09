using System;
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
        public static readonly string[] BoardingCrewReferencePaths =
        {
            "UIBattle/UIBattle_BoardingHook.unity",
            "UIBattle/UIBattle_BoardingLadder.unity",
            "UIBattle/UIBattle_BoardingRam.unity",
        };

        // These old authored variants did not serialize a battle stage. Restore
        // their specific connection in a disposable session, never by refreshing
        // the destination controller (which would replace the saved artwork).
        public static void SyncAndCaptureBoardingCrewFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Boarding preview synchronization requires stopped edit mode.");
            foreach (string reference in BoardingCrewReferencePaths) SyncBoardingCrewScene(reference);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UIPreviewSceneAcceptance.CaptureBoardingCrewFromCommandLine();
        }

        private static void SyncBoardingCrewScene(string reference)
        {
            string path = EditableSceneFolder + "/" + reference;
            string absolute = AbsolutePath(path);
            if (!File.Exists(absolute)) throw new FileNotFoundException("Missing authored boarding preview", path);
            string backup = AbsolutePath("Artifacts/ShipProgression20261003/boarding-preview-backup/" + reference);
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(absolute, backup);

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var destination = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
            Transform content = destination.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Phase Content");
            Transform target = content.Find("Battle Overlay/Battle Card");
            if (target == null) throw new InvalidOperationException("Missing saved battle surface in " + path);
            if (target.Find("Battle Scene Art") == null || target.Find("Battle Scene Tint") == null)
                throw new InvalidOperationException("Missing authored art layers in " + path);

            Scene stagingScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject staging = null;
            try
            {
                staging = new GameObject("__BoardingCrewPreviewStaging");
                SceneManager.MoveGameObjectToScene(staging, stagingScene);
                var source = staging.AddComponent<CoreGameplayDemoController>();
                CopyProvisionPreviewSettings(destination, source);
                source.RefreshEditorPreview();
                CoreVoyageSession session = source.Session;
                if (session == null || session.Phase != CoreDemoPhase.Battle || session.Battle == null)
                    throw new InvalidOperationException("Could not initialize the disposable battle for " + path);
                BoardingPreviewInvoke(session, "EnterBattleStage", NavalBattleStage.Boarding);
                CoreBattleState battle = session.Battle;
                bool hook = reference.Contains("BoardingHook");
                bool ram = reference.Contains("BoardingRam");
                battle.BoardingApproach = hook || ram ? NavalBoardingApproach.Quarter : NavalBoardingApproach.Alongside;
                battle.PlannedBoardingApproach = battle.BoardingApproach;
                battle.BoardingConnection = ram ? NavalBoardingConnection.RamBowToBroadside : NavalBoardingConnection.Planned;
                battle.Alignment = ram ? NavalBattleAlignment.EnemyBroadside : NavalBattleAlignment.Parallel;
                battle.DistanceMeters = 0f;
                battle.PlayerMovingTowardEnemy = false;
                battle.EnemyMovingTowardPlayer = false;
                battle.PlayerBoardingHealth = Math.Max(1, battle.PlayerMaxBoardingHealth - 8);
                battle.EnemyBoardingHealth = Math.Max(1, battle.EnemyMaxBoardingHealth - 8);
                battle.LastAction = "接舷预览 · " + (ram ? "冲撞锁住侧舷" : hook ? "斜后钩索登船" : "并舷跳板登船") +
                    " · 卡面显示当前普通水手编制的实际效果。";
                // A declared visual sample covers damage, guard, healing and draw;
                // it is not a played voyage and grants no inventory or progression.
                battle.Hand.Clear();
                foreach (string id in new[] { "card_boarding_hook", "card_cutlass_raid", "card_crew_guard", "card_crew_bandage", "card_crew_rally" })
                {
                    int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                        .Single(item => CoreVoyageSession.CardLibrary[item].Id == id);
                    if (!session.GetBattleCard(index).IsAllowedInStage(NavalBattleStage.Boarding))
                        throw new InvalidOperationException("Wrong-stage boarding sample card " + id);
                    battle.Hand.Add(index);
                }
                BoardingPreviewInvoke(source, "Render");
                Transform generatedContent = source.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Phase Content");
                Transform generated = generatedContent.Find("Battle Overlay/Battle Card");
                if (generated == null || generated.Find("Player Ship Art") == null || generated.Find("Enemy Ship Art") == null ||
                    generated.Find("Player Boarding Deck") != null || generated.Find("Enemy Boarding Deck") != null)
                    throw new InvalidOperationException("The source did not render boarding in " + path);
                RawImage generatedPainting = generated.Find("Battle Scene Art")?.GetComponent<RawImage>();
                Texture2D expectedPainting = BoardingBattleArtCatalog.ForEnvironment(session.BattleTerrain,
                    NavigationTimeOfDayUtility.FromVoyageHours(session.ElapsedGameHours));
                if (expectedPainting == null || generatedPainting == null || generatedPainting.texture != expectedPainting ||
                    generated.Find("Player Ship Art").GetComponentInChildren<RawImage>()?.texture == null ||
                    generated.Find("Enemy Ship Art").GetComponentInChildren<RawImage>()?.texture == null)
                    throw new InvalidOperationException("The new full-deck background or boarding cast is missing in " + path);
                PreviewSceneSpec spec = Array.Find(Specs, item => item.Screen == CoreGameplayPreviewScreen.Battle);
                PersistGeneratedSceneVisuals(generated, spec);
                Transform readouts = generatedContent.Find("Battle Readouts Overlay");
                if (readouts == null) throw new InvalidOperationException("Missing boarding readouts in " + path);
                PersistGeneratedSceneVisuals(readouts, spec);

                // Keep the saved room, controller and outer battle root. The
                // new full-deck scene owns its terrain/time painting and cover
                // UV, so replace that payload together with combat UI/actors.
                foreach (Transform child in target.Cast<Transform>().ToArray())
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                foreach (Transform child in generated)
                    CopyTreasureLayer(child, target, child.name);
                CopyTreasureLayer(readouts, content, "Battle Readouts Overlay");
                if (target.Find("Battle Scene Art").GetComponent<RawImage>().texture != expectedPainting)
                    throw new InvalidOperationException("The new boarding painting was not copied: " + path);
                if (!target.GetComponentsInChildren<Text>(true).Any(label => label.text.Contains("卡牌效果")))
                    throw new InvalidOperationException("The saved boarding UI lacks its ordinary-crew effect label: " + path);
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

        private static void BoardingPreviewInvoke(object instance, string method, params object[] arguments)
        {
            MethodInfo callback = instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            if (callback == null) throw new MissingMethodException(instance.GetType().FullName, method);
            callback.Invoke(instance, arguments);
        }
    }
}
