using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEngine;

namespace ShatteredSail.Editor
{
    public static class CardKeywordTooltipIntegration
    {
        public static void Prepare()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (!project.EndsWith("/tmp/card-keyword-tooltip-20261023-validation", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Use the owned tooltip validation project.");
            MonoScript controller = AssetDatabase.LoadAllAssetsAtPath("Assets/Plugins/ShatteredSail.Presentation.dll")
                .OfType<MonoScript>().Single(script => script.GetClass() == typeof(CoreGameplayDemoController));
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(controller, out string guid, out long fileId);
            // Copied UI prefabs reference source MonoScript GUIDs in production.
            // Bind those same component types to this isolated project's fresh DLL.
            var bindings = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (MonoScript script in AssetDatabase.LoadAllAssetsAtPath("Assets/Plugins/ShatteredSail.Presentation.dll").OfType<MonoScript>())
            {
                string metadata = "D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/" + script.name + ".cs.meta";
                if (!File.Exists(metadata)) continue;
                Match sourceGuid = Regex.Match(File.ReadAllText(metadata), @"guid:\s*([a-f0-9]+)");
                if (!sourceGuid.Success) continue;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string assemblyGuid, out long scriptId);
                bindings[sourceGuid.Groups[1].Value] = "m_Script: {fileID: " + scriptId + ", guid: " + assemblyGuid + ", type: 3}";
            }
            foreach (string prefab in Directory.GetFiles(Path.Combine(Application.dataPath, "Resources"), "*.prefab", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(prefab);
                string mapped = Regex.Replace(text, @"m_Script: \{fileID: 11500000, guid: ([a-f0-9]+), type: 3\}",
                    match => bindings.TryGetValue(match.Groups[1].Value, out string binding) ? binding : match.Value);
                if (mapped != text) File.WriteAllText(prefab, mapped);
            }
            string scenePath = Path.Combine(Application.dataPath, "Scenes/CoreGameplayDemo.unity");
            string source = File.ReadAllText("D:/Game/UnityClient/Assets/_Project/Scenes/CoreGameplayDemo.unity");
            const string original = "m_Script: {fileID: 11500000, guid: 7f1caef9d0b34bf7837094e1bb17c8a2, type: 3}";
            if (!source.Contains(original)) throw new InvalidOperationException("Production scene bootstrap changed; inspect its controller binding.");
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            File.WriteAllText(scenePath, source.Replace(original, "m_Script: {fileID: " + fileId + ", guid: " + guid + ", type: 3}"));
            AssetDatabase.Refresh();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/CoreGameplayDemo.unity", true) };
            PlayerSettings.companyName = "CodexIsolatedValidation";
            PlayerSettings.productName = "CardKeywordTooltip20261023";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            AssetDatabase.SaveAssets();
            Debug.Log("CARD_KEYWORD_TOOLTIP_INTEGRATION_READY");
        }
    }
}
