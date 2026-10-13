// Install only in the disposable validation project's Editor folder.
// Reuses the existing shipyard panel sync; naval repairs retain authored UI.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
        [Serializable] private sealed class ShipSyncReport
        {
            public string scope = "Scratch-only targeted saved preview synchronization. Existing SyncRegionalShipyard refreshes only saved Shipyard Panels, preserving preview port/ship/page settings and outer chrome. Battle atlas RawImages are repaired in place: full cell inset, aspect, water contact/width and source-alpha material. Other HUD, cards, portraits and backgrounds remain authored. No PlayMode, player save or package.";
            public List<ShipSyncScene> scenes = new List<ShipSyncScene>();
            public List<string> dependencies = new List<string>();
        }
        [Serializable] private sealed class ShipSyncScene
        {
            public string path, kind, previewSettingsBefore, previewSettingsAfter;
            public int changedShipImages;
            public bool saved;
            public List<ShipSyncImage> images = new List<ShipSyncImage>();
        }
        [Serializable] private sealed class ShipSyncImage
        {
            public string name, texture, material;
            public Rect beforeUv, afterUv;
            public Vector2 beforeSize, afterSize, beforePosition, afterPosition;
            public float waterContactError;
        }
        public static void SyncShipArtSavedPreviewsFromCommandLine()
        {
            throw new System.NotSupportedException("UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.");
#if false // Historical implementation; this target is retired.
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
                throw new InvalidOperationException("Stopped edit mode required.");
            string normalized = Application.dataPath.Replace('\\','/');
            if (!normalized.Contains("/tmp/shipyard-detail-clean-20261011/Assets"))
                throw new InvalidOperationException("This helper must run in the authorized cached scratch project only.");
            string[] args = Environment.GetCommandLineArgs();
            int argument = Array.IndexOf(args,"-shipArtSyncPath");
            if (argument < 0 || argument + 1 >= args.Length || !Path.IsPathRooted(args[argument+1]))
                throw new ArgumentException("Pass -shipArtSyncPath NEW_ABSOLUTE_DIRECTORY");
            string output = Path.GetFullPath(args[argument+1]);
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Use an empty sync report directory.");
            Directory.CreateDirectory(output);
            var report = new ShipSyncReport();
            string[] paths = AssetDatabase.FindAssets("t:Scene",new[]{EditableSceneFolder})
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
            foreach (string path in paths)
            {
                string original = File.ReadAllText(AbsolutePath(path));
                Match screen = Regex.Match(original,@"(?m)^  editorPreviewScreen: (\d+)\r?$");
                if (!screen.Success) continue;
                int value = int.Parse(screen.Groups[1].Value);
                if (value != (int)CoreGameplayPreviewScreen.Shipyard && value != (int)CoreGameplayPreviewScreen.ShipyardDetail &&
                    value != (int)CoreGameplayPreviewScreen.Battle) continue;
                var record = new ShipSyncScene {path=path,kind=((CoreGameplayPreviewScreen)value).ToString(),previewSettingsBefore=Settings(original)};
                report.scenes.Add(record);
                if (value != (int)CoreGameplayPreviewScreen.Battle)
                {
                    SyncRegionalShipyard(path.Substring(EditableSceneFolder.Length+1),-1,-1);
                    record.saved = true;
                }
                else RepairSavedBattleShipArt(path,record);
                record.previewSettingsAfter = Settings(File.ReadAllText(AbsolutePath(path)));
                if (record.previewSettingsBefore != record.previewSettingsAfter)
                    throw new InvalidOperationException("Preview settings changed: "+path);
                File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true),new UTF8Encoding(false));
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            string[] saved = report.scenes.Where(s=>s.saved).Select(s=>s.path).ToArray();
            report.dependencies = AssetDatabase.GetDependencies(saved,true)
                .Where(p=>p.StartsWith(EditableVisualAssetFolder+"/",StringComparison.Ordinal)).Distinct().OrderBy(p=>p,StringComparer.Ordinal).ToList();
            File.WriteAllLines(Path.Combine(output,"synced-scenes.txt"),saved,new UTF8Encoding(false));
            File.WriteAllLines(Path.Combine(output,"preview-dependencies.txt"),report.dependencies,new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true),new UTF8Encoding(false));
            Debug.Log("SHIP_ART_SAVED_PREVIEWS_SYNCED: "+saved.Length+" saved; "+report.scenes.Count+" scoped scenes audited; "+report.dependencies.Count+" editable dependencies.");

#endif
        }
        private static string Settings(string text) => string.Join("\n",Regex.Matches(text,@"(?m)^  editorPreview[^\r\n]*").Cast<Match>().Select(m=>m.Value));
        private static void RepairSavedBattleShipArt(string path,ShipSyncScene record)
        {
            Scene scene = EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            CoreGameplayDemoController controller = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
            foreach (RawImage image in controller.GetComponentsInChildren<RawImage>(true))
            {
                if (image.texture == null || (image.texture.name != "ship_roster_sheet" && image.texture.name != "ship_sprite_sheet")) continue;
                // Modern boarding scenes use their authored full-deck cast and
                // contain no atlas hulls. Leave that UI intact.
                bool enemy = image.name == "Enemy Ship Art";
                bool player = image.name == "Player Ship Art Hull";
                if (!enemy && !player) throw new InvalidOperationException("Unrecognized battle atlas hull: "+path+" / "+image.name);
                RectTransform ship = enemy ? image.rectTransform : image.transform.parent as RectTransform;
                RectTransform card = ship.parent as RectTransform;
                RectTransform contact = card.Find(enemy ? "Enemy Ship Water Contact" : "Player Ship Water Contact") as RectTransform;
                if (contact == null) throw new InvalidOperationException("Missing authored ship water contact: "+path+" / "+image.name);
                var item = new ShipSyncImage {name=image.name,texture=image.texture.name,beforeUv=image.uvRect,
                    beforeSize=image.rectTransform.rect.size,beforePosition=ship.anchoredPosition};
                Rect uv = image.uvRect;
                int columns = image.texture.name == "ship_roster_sheet" ? 2 : 4;
                int column = Mathf.Clamp(Mathf.FloorToInt(uv.center.x*columns),0,columns-1);
                int row = Mathf.Clamp(Mathf.FloorToInt(uv.center.y*2),0,1);
                float sx = Mathf.Sign(uv.width), sy = Mathf.Sign(uv.height);
                Rect cell = new Rect((column+(sx<0?1:0))/(float)columns,(row+(sy<0?1:0))*.5f,sx/columns,sy*.5f);
                image.uvRect = UiImageAspect.InsetUv(image.texture,cell,10f);
                UiImageAspect.Fit(image,enemy ? new Vector2(320f,240f) : ship.rect.size);
                Vector3 keel = ShipSyncPointInRoot(image.transform,ShipWaterlineLayout.HullContactLocal(image),card);
                Vector3 water = ShipSyncPointInRoot(contact,Vector3.zero,card);
                ship.anchoredPosition += Vector2.up*(water.y-keel.y);
                float projected = enemy ? Mathf.Max(.16f,Mathf.Abs(Mathf.Cos(ship.localEulerAngles.y*Mathf.Deg2Rad))) : 1f;
                foreach(string suffix in new[]{"Shadow","Contact"})
                {
                    RectTransform wake = card.Find((enemy?"Enemy":"Player")+" Ship Water "+suffix) as RectTransform;
                    if(wake == null) throw new InvalidOperationException("Missing authored ship wake: "+path);
                    wake.sizeDelta = new Vector2(image.rectTransform.rect.width*ship.localScale.x*.93f*projected,wake.sizeDelta.y);
                    EditorUtility.SetDirty(wake);
                }
                Material material = image.material;
                item.material = AssetDatabase.GetAssetPath(material);
                if (!item.material.StartsWith(EditableVisualAssetFolder+"/",StringComparison.Ordinal))
                    throw new InvalidOperationException("Ship material must remain a persisted editable asset: "+path);
                if(material.HasProperty("_UseColorKey")) material.SetFloat("_UseColorKey",0f);
                if(material.HasProperty("_SmoothCutout")) material.SetFloat("_SmoothCutout",1f);
                EditorUtility.SetDirty(material);
                EditorUtility.SetDirty(image);
                EditorUtility.SetDirty(image.rectTransform);
                EditorUtility.SetDirty(ship);
                item.afterUv=image.uvRect; item.afterSize=image.rectTransform.rect.size; item.afterPosition=ship.anchoredPosition;
                item.waterContactError = Mathf.Abs(ShipSyncPointInRoot(image.transform,ShipWaterlineLayout.HullContactLocal(image),card).y-water.y);
                if(item.waterContactError>.01f) throw new InvalidOperationException("Saved ship keel missed waterline: "+path);
                record.images.Add(item);
                record.changedShipImages++;
            }
            if(record.changedShipImages==0) return;
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene,path)) throw new InvalidOperationException("Cannot save "+path);
            RepairSavedNavigationCanvasScale(AbsolutePath(path));
            AssertSavedNavigationCanvasScale(AbsolutePath(path));
            record.saved=true;
        }
        private static Vector3 ShipSyncPointInRoot(Transform item,Vector3 point,Transform root)
        {
            for(Transform current=item;current!=root;current=current.parent)
            {
                if(current==null) throw new InvalidOperationException("Ship geometry left its battle card.");
                point=Matrix4x4.TRS(current.localPosition,current.localRotation,current.localScale).MultiplyPoint3x4(point);
            }
            return point;
        }
    }
}
