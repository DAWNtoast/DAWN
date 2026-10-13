// Scratch-only diagnostic. Never install or run this repair in the production project.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEngine;

namespace ShatteredSail.Editor
{
    public static class ComponentShopResourceRepairAudit
    {
        [Serializable] private sealed class AssetRecord
        {
            public string id, role, resourcePath, assetPath, guid, guidPath, importer, mainAssetType;
            public bool fileExists, metaExists, assetDatabaseLoaded, resourcesLoaded;
            public int width, height;
            public string[] objectTypes;
        }
        [Serializable] private sealed class Report
        {
            public string project, createdUtc;
            public int equipment;
            public bool catalogAvailable, expandedAvailable;
            public AssetRecord[] before, after;
            public string[] reimported, remainingFailures;
            public bool portArtAvailableBefore, portArtAvailableAfter;
            public TextureProperty[] portPropertiesBefore, portPropertiesAfter;
        }
        [Serializable] private sealed class TextureProperty
        {
            public string property, textureName;
            public bool loaded;
        }

        // -executeMethod ShatteredSail.Editor.ComponentShopResourceRepairAudit.CheckAndRepairPortArtFromCommandLine
        // Same output option as equipment audit; default suffix is port-resource-audit.
        public static void CheckAndRepairPortArtFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Only run this audit in stopped scratch edit mode.");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (!project.StartsWith("D:/Game/tmp/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Resource repair is restricted to D:/Game/tmp projects.");
            string[] arguments = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(arguments, "-componentShopResourceReportRoot");
            string output = option >= 0 && option + 1 < arguments.Length ? arguments[option + 1]
                : "D:/Game/Artifacts/ComponentShopImplementation20261012/port-resource-audit";
            output = Path.GetFullPath(output).Replace('\\', '/');
            if (!output.StartsWith("D:/Game/Artifacts/ComponentShopImplementation20261012/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Audit output must remain inside the component-shop artifact directory.");
            Directory.CreateDirectory(output);
            AssetRecord[] before = InspectPortArt(out bool beforeAvailable, out TextureProperty[] beforeProperties);
            string[] reimport = before.Where(Failed).Select(item => item.assetPath).Distinct().ToArray();
            var report = new Report { project = project, createdUtc = DateTime.UtcNow.ToString("O"), before = before,
                reimported = reimport, portArtAvailableBefore = beforeAvailable, portPropertiesBefore = beforeProperties };
            Save(output, report);
            foreach (string path in reimport)
                if (File.Exists(path)) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            if (reimport.Length > 0) AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            report.after = InspectPortArt(out bool afterAvailable, out TextureProperty[] afterProperties);
            report.portArtAvailableAfter = afterAvailable;
            report.portPropertiesAfter = afterProperties;
            report.remainingFailures = report.after.Where(Failed).Select(item => item.resourcePath).ToArray();
            Save(output, report);
            if (!afterAvailable || report.remainingFailures.Length > 0)
                throw new InvalidOperationException("PORT_RESOURCE_AUDIT: IsAvailable=" + afterAvailable + "; " +
                    report.remainingFailures.Length + " remaining failed loads; " + output);
            Debug.Log("PORT_RESOURCE_AUDIT: " + report.after.Length + " PNG loads verified; reimported " + reimport.Length + "; " + output);
        }

        private static AssetRecord[] InspectPortArt(out bool available, out TextureProperty[] properties)
        {
            var requested = new HashSet<string>(StringComparer.Ordinal);
            Func<string, Texture2D> loader = path => { requested.Add(path); return Resources.Load<Texture2D>(path); };
            ConstructorInfo constructor = typeof(PortArtCatalog).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(Func<string, Texture2D>) }, null);
            if (constructor == null) throw new MissingMethodException("PortArtCatalog traced constructor");
            var catalog = (PortArtCatalog)constructor.Invoke(new object[] { loader });
            available = catalog.IsAvailable;
            var observed = new List<TextureProperty>();
            foreach (PropertyInfo property in typeof(PortArtCatalog).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(item => item.PropertyType == typeof(Texture2D) && item.GetIndexParameters().Length == 0))
            {
                Texture2D texture = (Texture2D)property.GetValue(catalog);
                observed.Add(new TextureProperty { property = property.Name, loaded = texture != null, textureName = texture == null ? "<null>" : texture.name });
            }
            foreach (string port in new[] { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" })
            {
                Texture2D texture = catalog.PortBackgroundFor(port);
                observed.Add(new TextureProperty { property = "PortBackgroundFor(" + port + ")", loaded = texture != null, textureName = texture == null ? "<null>" : texture.name });
            }
            for (int index = 0; index < catalog.CrewFullBodyCount; index++)
            {
                Texture2D texture = catalog.CrewFullBody(index);
                observed.Add(new TextureProperty { property = "CrewFullBody(" + index + ")", loaded = texture != null, textureName = texture == null ? "<null>" : texture.name });
            }
            properties = observed.ToArray();
            return requested.OrderBy(path => path).Select(path => Inspect(path, "port", path)).ToArray();
        }

        // -executeMethod ShatteredSail.Editor.ComponentShopResourceRepairAudit.CheckAndRepairFromCommandLine
        // -componentShopResourceReportRoot D:/Game/Artifacts/ComponentShopImplementation20261012/resource-audit
        public static void CheckAndRepairFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Only run this audit in stopped scratch edit mode.");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (!project.StartsWith("D:/Game/tmp/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Resource repair is restricted to D:/Game/tmp projects.");
            string[] arguments = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(arguments, "-componentShopResourceReportRoot");
            string output = option >= 0 && option + 1 < arguments.Length ? arguments[option + 1]
                : "D:/Game/Artifacts/ComponentShopImplementation20261012/resource-audit";
            output = Path.GetFullPath(output).Replace('\\', '/');
            if (!output.StartsWith("D:/Game/Artifacts/ComponentShopImplementation20261012/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Audit output must remain inside the component-shop artifact directory.");
            Directory.CreateDirectory(output);
            var catalog = new ComponentArtCatalog();
            AssetRecord[] before = Inspect(catalog);
            string[] reimport = before.Where(Failed).Select(item => item.assetPath).Distinct().ToArray();
            // Save observations before any repair, even if an import aborts.
            Save(output, new Report { project = project, createdUtc = DateTime.UtcNow.ToString("O"),
                equipment = catalog.ExpandedEntries.Count, before = before, reimported = reimport });
            foreach (string path in reimport)
            {
                if (!File.Exists(path)) continue;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
            if (reimport.Length > 0)
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            catalog.ReleaseLoadedTextures();
            var refreshed = new ComponentArtCatalog();
            AssetRecord[] after = Inspect(refreshed);
            string[] failures = after.Where(Failed).Select(item => item.resourcePath).ToArray();
            bool available = refreshed.IsAvailable, expanded = refreshed.ExpandedIsAvailable;
            Save(output, new Report { project = project, createdUtc = DateTime.UtcNow.ToString("O"),
                equipment = refreshed.ExpandedEntries.Count, before = before, after = after,
                reimported = reimport, remainingFailures = failures,
                catalogAvailable = available, expandedAvailable = expanded });
            if (failures.Length > 0 || !available || !expanded)
                throw new InvalidOperationException("COMPONENT_RESOURCE_AUDIT: " + failures.Length +
                    " missing loads; catalog=" + available + "; expanded=" + expanded + ". See " + output);
            Debug.Log("COMPONENT_RESOURCE_AUDIT: " + after.Length + " PNG loads verified; reimported " + reimport.Length + "; " + output);
        }

        private static bool Failed(AssetRecord item) => !item.fileExists || !item.metaExists ||
            !item.assetDatabaseLoaded || !item.resourcesLoaded;

        private static AssetRecord[] Inspect(ComponentArtCatalog catalog)
        {
            var result = new List<AssetRecord>();
            foreach (ComponentArtEntry entry in catalog.ExpandedEntries)
            {
                result.Add(Inspect(entry.Id, "icon", "ComponentArt/" + entry.ArtSourceId));
                result.Add(Inspect(entry.Id, "layer", "ModularShip/Layers/" + entry.ArtSourceId));
            }
            return result.ToArray();
        }

        private static AssetRecord Inspect(string id, string role, string resourcePath)
        {
            string path = "Assets/_Project/Resources/" + resourcePath + ".png";
            Texture2D database = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Texture2D resource = Resources.Load<Texture2D>(resourcePath);
            string guid = AssetDatabase.AssetPathToGUID(path);
            AssetImporter importer = AssetImporter.GetAtPath(path);
            return new AssetRecord
            {
                id = id, role = role, resourcePath = resourcePath, assetPath = path,
                fileExists = File.Exists(path), metaExists = File.Exists(path + ".meta"),
                guid = guid, guidPath = AssetDatabase.GUIDToAssetPath(guid),
                importer = importer == null ? "<null>" : importer.GetType().FullName,
                mainAssetType = AssetDatabase.GetMainAssetTypeAtPath(path)?.FullName ?? "<null>",
                assetDatabaseLoaded = database != null, resourcesLoaded = resource != null,
                width = resource == null ? 0 : resource.width, height = resource == null ? 0 : resource.height,
                objectTypes = AssetDatabase.LoadAllAssetsAtPath(path).Where(item => item != null)
                    .Select(item => item.GetType().FullName + ":" + item.name).ToArray()
            };
        }

        private static void Save(string directory, Report report) =>
            File.WriteAllText(Path.Combine(directory, "resource-audit.json"), JsonUtility.ToJson(report, true));
    }
}
