using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    /// <summary>Repairs only the drawing lesson, whose saved template was incorrectly the treasure room.</summary>
    public static class GuidedTutorialPreviewSceneBuilder
    {
        private const string Folder = "Assets/_Project/Scenes/UIPreviewsForMe/TutorialVoyage/";
        public const string DrawChartPath = Folder + "28_DrawChart.unity";
        private const string SettlementPath = Folder + "27_ThirdSettlement.unity";

        public static void RepairDrawChartFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before repairing the drawing preview.");
            Scene alreadyOpen = SceneManager.GetSceneByPath(DrawChartPath);
            if (alreadyOpen.IsValid() && alreadyOpen.isLoaded)
                throw new InvalidOperationException("Close the drawing preview before its targeted repair.");

            string destination = Absolute(DrawChartPath);
            string current = File.ReadAllText(destination);
            // A corrected preview may contain later manual layout edits. Never
            // reset those: only the known wrong treasure-room copy is replaced.
            if (!current.Contains("m_Name: Settlement Card"))
            {
                if (!current.Contains("m_Name: Treasure Panel") || !current.Contains("editorPreviewScreen: 8"))
                    throw new InvalidOperationException("The drawing preview is neither the known wrong template nor a settlement.");
                string source = File.ReadAllText(Absolute(SettlementPath));
                if (!source.Contains("m_Name: Settlement Card") || !source.Contains("editorPreviewScreen: 14"))
                    throw new InvalidOperationException("The authored settlement reference is invalid.");
                File.WriteAllText(destination, source.Replace("27_ThirdSettlement", "28_DrawChart"));
                AssetDatabase.ImportAsset(DrawChartPath, ImportAssetOptions.ForceSynchronousImport);
            }

            Scene scene = EditorSceneManager.OpenScene(DrawChartPath, OpenSceneMode.Additive);
            try
            {
                CoreGameplayDemoController controller = scene.GetRootGameObjects()
                    .Select(root => root.GetComponent<CoreGameplayDemoController>()).Single(item => item != null);
                if (controller.EditorPreviewScreen != CoreGameplayPreviewScreen.Settlement || !controller.EditorPreviewPersistsGeneratedUi)
                    throw new InvalidOperationException("The drawing lesson must remain an editable settlement preview.");
                Transform[] objects = controller.GetComponentsInChildren<Transform>(true);
                RectTransform card = (RectTransform)objects.Single(item => item.name == "Settlement Card");
                Button finish = objects.Single(item => item.name == "Return To Port").GetComponent<Button>();
                if (card.Find("Draw Sea Chart") == null)
                {
                    // Use the real button factory and the exact drawing action
                    // geometry from BuildSettlementPresentation, not a mock face.
                    MethodInfo factory = typeof(CoreGameplayDemoController).GetMethod("CreateButton", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?? throw new MissingMethodException(typeof(CoreGameplayDemoController).FullName, "CreateButton");
                    Button draw = (Button)factory.Invoke(controller, new object[] {
                        "Draw Sea Chart", card, new Vector2(350f, 72f), new Vector2(-18f, -410f),
                        (Color)new Color32(0x17, 0x3A, 0x43, 0xFF), (Color)new Color32(0xE8, 0xE1, 0xC7, 0xFF), 23
                    });
                    draw.GetComponentInChildren<Text>().text = "航海士 · 绘制海图";
                    draw.onClick.RemoveAllListeners();
                    // The runtime skin creates a transient sprite. Reuse this
                    // scene's serialized copy of the identical wood face/material.
                    Image reference = finish.GetComponent<Image>();
                    Image face = draw.GetComponent<Image>();
                    face.sprite = reference.sprite;
                    face.material = reference.material;
                    draw.transform.SetSiblingIndex(finish.transform.GetSiblingIndex());
                    Text note = objects.Single(item => item.name == "Settlement Note").GetComponent<Text>();
                    note.rectTransform.sizeDelta = new Vector2(560f, 56f);
                    note.rectTransform.anchoredPosition = new Vector2(-525f, -412f);
                }
                objects.Single(item => item.name == "Settlement Badge").GetComponent<Text>().text =
                    "新手引导 · 绘制海图 / 演示数据，不写入存档";
                objects.Single(item => item.name == "Settlement Title").GetComponent<Text>().text = "战利归仓";
                objects.Single(item => item.name == "Settlement Destination").GetComponent<Text>().text = "首胜返航 · 断锚港";
                objects.Single(item => item.name == "Settlement Protection").GetComponent<Text>().text =
                    "保全 85% 战利品　 /　主动返航 · 收获留存　 /　航海士可绘制本程海图";
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("Could not save the drawing preview.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
            Debug.Log("Repaired only " + DrawChartPath + "; source settlement and other authored scenes were preserved.");
        }

        private static string Absolute(string path) => Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
    }
}
