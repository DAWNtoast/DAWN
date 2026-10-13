using System;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ShatteredSail.Editor
{
    /// <summary>Explicit migration only. Normal builds validate and never rewrite artist-edited prefabs.</summary>
    public sealed class FixedUiPrefabBuilder : IPreprocessBuildWithReport
    {
        public int callbackOrder => 10;

        [MenuItem("Shattered Sail/UI/Create Missing Fixed UI Prefabs and Bake Coverage")]
        public static void CreateMissingPrefabsAndBakeCoverage()
        {
            RegionalShopFurnitureCoverageBaker.BakeAll();
            MainMenuPrefabBuilder.CreateMissingPrefabs();
            PortSettingsPrefabBuilder.CreateMissingPrefabs();
            VoyageHeaderPrefabBuilder.CreateMissingPrefab();
            ValidatePrefabs();
            AssetDatabase.SaveAssets();
        }

        public static void ValidatePrefabs()
        {
            foreach (MainMenuPrefabKind kind in Enum.GetValues(typeof(MainMenuPrefabKind)))
            {
                MainMenuPrefabView view = AssetDatabase.LoadAssetAtPath<MainMenuPrefabView>(
                    "Assets/_Project/Resources/UI/MainMenu" + kind + ".prefab");
                if (view == null || view.Kind != kind || !view.IsComplete)
                    throw new BuildFailedException("Missing or incomplete main menu prefab: " + kind);
            }
            var settings = AssetDatabase.LoadAssetAtPath<PortSettingsView>("Assets/_Project/Resources/UI/PortSettings.prefab");
            var confirm = AssetDatabase.LoadAssetAtPath<ReturnToMenuConfirmationView>("Assets/_Project/Resources/UI/ReturnToMenuConfirmation.prefab");
            if (settings == null || !settings.IsComplete || settings.gameObject.activeSelf || settings.Parchment.texture != null ||
                confirm == null || !confirm.IsComplete || confirm.gameObject.activeSelf)
                throw new BuildFailedException("Missing/invalid gameplay settings prefabs or retained large-paper dependency.");
            var voyage = AssetDatabase.LoadAssetAtPath<VoyageHeaderView>("Assets/_Project/Resources/UI/VoyageHeader.prefab");
            if (voyage == null || !voyage.IsComplete || voyage.Frame.sprite != null || voyage.Compass.sprite != null)
                throw new BuildFailedException("Missing/invalid voyage header prefab or retained navigation art dependency.");
        }

        public void OnPreprocessBuild(BuildReport report) => ValidatePrefabs();
    }
}
