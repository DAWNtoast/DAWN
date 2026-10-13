using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    /// <summary>Upgrades only authored chart grid/fog layers. All other serialized
    /// scene objects and the original exploration knowledge remain unchanged.</summary>
    public static class ChartDetailPreviewSceneSync
    {
        private static readonly Regex Blocks = new Regex(@"(?ms)^--- !u!\d+ &(\d+)\r?\n.*?(?=^--- !u!|\z)");
        private static readonly Regex Children = new Regex(@"(?m)^  m_Children:(?: \[\])?\r?\n(?:  - \{fileID: \d+\}\r?\n)*");
        private static readonly Regex FogName = new Regex(@"^Fog (\d+) (\d+)$");

        public static void SyncFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stopped Editor required.");
            string[] args = Environment.GetCommandLineArgs();
            string output = Argument(args, "-chartDetailOutput");
            string backupRoot = Path.Combine(output, "scene-backup");
            if (Directory.Exists(backupRoot)) throw new IOException("Use a new scene backup directory.");
            Directory.CreateDirectory(backupRoot);
            var report = new Report { backupRoot = backupRoot };
            string[] folders = { GameplayPreviewSceneBuilder.SceneFolder };
            string[] paths = folders.Where(AssetDatabase.IsValidFolder).SelectMany(folder =>
                AssetDatabase.FindAssets("t:Scene", new[] { folder })).Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(x => x).ToArray();
            foreach (string path in paths)
            {
                report.scannedScenes++;
                byte[] before = File.ReadAllBytes(Absolute(path));
                string text = Encoding.UTF8.GetString(before);
                if (!text.Contains("m_Name: Chart Grid Layer") || !text.Contains("m_Name: Visibility Mask Layer")) continue;
                report.matchedScenes++;
                string backup = Path.Combine(backupRoot, path);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                File.WriteAllBytes(backup, before);
                File.Copy(Absolute(path) + ".meta", backup + ".meta");
                byte[] after = Upgrade(path, before);
                bool updated = !before.SequenceEqual(after);
                if (updated) report.updatedScenes++;
                report.scenes.Add(new SceneResult { path = path, updated = updated,
                    beforeSha256 = Hash(before), afterSha256 = Hash(after),
                    metaSha256 = Hash(File.ReadAllBytes(Absolute(path) + ".meta")) });
                File.WriteAllText(Path.Combine(output, "scene-sync.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            }
            File.WriteAllText(Path.Combine(output, "scene-sync.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log("CHART_DETAIL_PREVIEW_SYNC_OK: " + report.updatedScenes + " chart grid/fog layers updated; other scene blocks retained.");
        }

        private static byte[] Upgrade(string path, byte[] original)
        {
            Scene existing = SceneManager.GetSceneByPath(path);
            if (existing.IsValid() && existing.isLoaded) throw new InvalidOperationException("Close authored scene before synchronizing: " + path);
            string before = Encoding.UTF8.GetString(original);
            Dictionary<string, string> originalBlocks = Parse(before);
            Scene scene = default;
            byte[] result = original;
            try
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                RectTransform[] transforms = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RectTransform>(true)).ToArray();
                RectTransform grid = transforms.Single(rect => rect.name == "Chart Grid Layer");
                RectTransform fog = transforms.Single(rect => rect.name == "Visibility Mask Layer");
                if (fog.GetComponentsInChildren<VoyageMapTileLayerGraphic>(true).Any())
                {
                    VerifyLayers(grid, fog);
                    return original;
                }
                Image[] oldFog = fog.GetComponentsInChildren<Image>(true).Where(image => FogName.IsMatch(image.name)).ToArray();
                if (oldFog.Length != CoreVoyageMap.GridColumns * CoreVoyageMap.GridRows)
                    throw new InvalidDataException("Expected complete authored coarse fog in " + path);
                var colors = new Color[oldFog.Length];
                var found = new HashSet<int>();
                foreach (Image image in oldFog)
                {
                    Match match = FogName.Match(image.name);
                    int x = int.Parse(match.Groups[1].Value), y = int.Parse(match.Groups[2].Value);
                    int index = y * CoreVoyageMap.GridColumns + x;
                    if (x >= CoreVoyageMap.GridColumns || y >= CoreVoyageMap.GridRows || !found.Add(index))
                        throw new InvalidDataException("Invalid authored fog coordinate in " + path);
                    colors[index] = image.color;
                }
                var removed = new HashSet<string>();
                foreach (RectTransform layer in new[] { grid, fog })
                    foreach (Transform child in layer.Cast<Transform>().ToArray())
                    {
                        foreach (Transform item in child.GetComponentsInChildren<Transform>(true))
                        {
                            removed.Add(Id(item.gameObject));
                            foreach (Component component in item.GetComponents<Component>()) removed.Add(Id(component));
                        }
                        Object.DestroyImmediate(child.gameObject);
                    }
                if (removed.Any(id => !originalBlocks.ContainsKey(id))) throw new InvalidDataException("Authored fog IDs missing in " + path);
                BuildLayers(grid, fog, colors);
                VerifyLayers(grid, fog);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Could not serialize chart layers: " + path);
                Dictionary<string, string> serialized = Parse(File.ReadAllText(Absolute(path)));
                var additions = new HashSet<string>();
                var parents = new Dictionary<string, string>();
                foreach (RectTransform layer in new[] { grid, fog })
                {
                    string id = Id(layer);
                    if (Children.Matches(originalBlocks[id]).Count != 1 || Children.Matches(serialized[id]).Count != 1)
                        throw new InvalidDataException("Invalid chart children block in " + path);
                    parents[id] = Children.Replace(originalBlocks[id], Children.Match(serialized[id]).Value);
                    foreach (Transform child in layer.Cast<Transform>())
                        foreach (Transform item in child.GetComponentsInChildren<Transform>(true))
                        {
                            additions.Add(Id(item.gameObject));
                            foreach (Component component in item.GetComponents<Component>()) additions.Add(Id(component));
                        }
                }
                if (additions.Any(originalBlocks.ContainsKey)) throw new InvalidDataException("New layer IDs overlap authored IDs: " + path);
                string after = Blocks.Replace(before, match => removed.Contains(match.Groups[1].Value) ? "" :
                    parents.TryGetValue(match.Groups[1].Value, out string changed) ? changed : match.Value);
                after += string.Concat(additions.OrderBy(id => long.Parse(id)).Select(id => serialized[id]));
                foreach (string id in removed)
                    if (Regex.IsMatch(after, @"\{fileID: " + id + @"(?:[,}])")) throw new InvalidDataException("Retained reference to obsolete fog object: " + path);
                Dictionary<string, string> finalBlocks = Parse(after);
                if (finalBlocks.Count != originalBlocks.Count - removed.Count + additions.Count ||
                    originalBlocks.Any(pair => !removed.Contains(pair.Key) && !parents.ContainsKey(pair.Key) && finalBlocks[pair.Key] != pair.Value))
                    throw new InvalidDataException("Unrelated authored blocks changed: " + path);
                result = new UTF8Encoding(false).GetBytes(after);
            }
            finally
            {
                try { if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
                finally { File.WriteAllBytes(Absolute(path), result); }
            }
            if (!File.ReadAllBytes(Absolute(path)).SequenceEqual(result)) throw new IOException("Chart scene write verification failed: " + path);
            return result;
        }

        private static void BuildLayers(RectTransform grid, RectTransform fog, Color[] colors)
        {
            Vector2 area = grid.rect.size;
            if ((area - fog.rect.size).sqrMagnitude > .01f || Mathf.Abs(area.x - 1160f) > .1f || Mathf.Abs(area.y - 630f) > .1f)
                throw new InvalidDataException("Unexpected authored chart size.");
            Vector2 cell = new Vector2(area.x / CoreVoyageMap.ChartColumns, area.y / CoreVoyageMap.ChartRows);
            VoyageMapTileLayerGraphic lines = Layer("Chart Grid Lines", grid, area);
            for (int x = 0; x <= CoreVoyageMap.ChartColumns; x++)
                lines.AddTile(new Vector2(-area.x * .5f + x * cell.x, 0), new Vector2(x % 16 == 0 ? 1.15f : .55f, area.y),
                    x % 16 == 0 ? new Color(.72f, .87f, .82f, .044f) : new Color(.58f, .78f, .78f, .018f));
            for (int y = 0; y <= CoreVoyageMap.ChartRows; y++)
                lines.AddTile(new Vector2(0, -area.y * .5f + y * cell.y), new Vector2(area.x, y % 8 == 0 ? 1.15f : .55f),
                    y % 8 == 0 ? new Color(.72f, .87f, .82f, .044f) : new Color(.58f, .78f, .78f, .018f));
            VoyageMapTileLayerGraphic tiles = Layer("Chart Fog Tiles", fog, area);
            VoyageMapTileLayerGraphic frontier = Layer("Chart Fog Frontier", fog, area);
            Color CellColor(int x, int y)
            {
                Color color = colors[(y / 2) * CoreVoyageMap.GridColumns + x / 2];
                if (color.a >= .5f) color.a = 1f;
                return color;
            }
            bool Explored(int x, int y) => CellColor(x, y).a < .5f;
            for (int y = 0; y < CoreVoyageMap.ChartRows; y++)
            for (int x = 0; x < CoreVoyageMap.ChartColumns; x++)
            {
                Vector2 center = -area * .5f + new Vector2((x + .5f) * cell.x, (y + .5f) * cell.y);
                Vector2 min = -area * .5f + new Vector2(x * cell.x, y * cell.y);
                Vector2 max = -area * .5f + new Vector2((x + 1) * cell.x, (y + 1) * cell.y);
                tiles.AddTileBounds(min, max, CellColor(x, y));
                if (!Explored(x, y)) continue;
                frontier.AddCellFrontier(center, cell, 1.1f, new Color(.3f, .54f, .57f, .22f),
                    y + 1 < CoreVoyageMap.ChartRows && !Explored(x, y + 1),
                    x + 1 < CoreVoyageMap.ChartColumns && !Explored(x + 1, y),
                    y > 0 && !Explored(x, y - 1), x > 0 && !Explored(x - 1, y));
            }
            lines.Complete(); tiles.Complete(); frontier.Complete();
        }

        private static VoyageMapTileLayerGraphic Layer(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(VoyageMapTileLayerGraphic));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false); rect.sizeDelta = size;
            var graphic = go.GetComponent<VoyageMapTileLayerGraphic>();
            graphic.Initialize(null);
            return graphic;
        }

        private static void VerifyLayers(Transform grid, Transform fog)
        {
            VoyageMapTileLayerGraphic lines = grid.GetComponentsInChildren<VoyageMapTileLayerGraphic>(true).Single();
            VoyageMapTileLayerGraphic tiles = fog.GetComponentsInChildren<VoyageMapTileLayerGraphic>(true).Single(g => g.name == "Chart Fog Tiles");
            if (lines.TileCount != CoreVoyageMap.ChartColumns + CoreVoyageMap.ChartRows + 2 || tiles.TileCount != CoreVoyageMap.ChartColumns * CoreVoyageMap.ChartRows)
                throw new InvalidDataException("Serialized chart grid/fog is incomplete.");
        }

        private static Dictionary<string, string> Parse(string text) => Blocks.Matches(text).Cast<Match>().ToDictionary(m => m.Groups[1].Value, m => m.Value);
        private static string Id(Object value) => GlobalObjectId.GetGlobalObjectIdSlow(value).targetObjectId.ToString(CultureInfo.InvariantCulture);
        private static string Absolute(string path) => Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
        private static string Argument(string[] args, string flag)
        {
            int index = Array.IndexOf(args, flag);
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1])) throw new ArgumentException(flag + " requires absolute output directory.");
            return Path.GetFullPath(args[index + 1]);
        }
        private static string Hash(byte[] bytes)
        { using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        [Serializable] private sealed class Report
        {
            public string backupRoot;
            public int scannedScenes, matchedScenes, updatedScenes;
            public List<SceneResult> scenes = new List<SceneResult>();
        }
        [Serializable] private sealed class SceneResult
        {
            public string path, beforeSha256, afterSha256, metaSha256;
            public bool updated;
        }
    }
}
