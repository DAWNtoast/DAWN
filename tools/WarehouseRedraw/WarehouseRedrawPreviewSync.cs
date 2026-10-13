using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

namespace ShatteredSail.Editor
{
    public static partial class GameplayPreviewSceneBuilder
    {
        [Serializable] private sealed class WarehouseSyncReport
        {
            public string scope = "Scratch-only property-level presentation refresh of existing Warehouse nodes. Existing objects, sample treasure/inventory text, enabled state, persistent callbacks, preview configuration and non-Warehouse UI are retained. New decoration nodes are copied without callbacks. Summary text is split into heading/capacity. Production copyback is hash guarded.";
            public List<WarehouseSyncScene> scenes = new List<WarehouseSyncScene>();
            public List<string> dependencies = new List<string>();
        }
        [Serializable] private sealed class WarehouseSyncScene
        {
            public string path, beforeSha256, afterSha256, metaSha256, settingsBefore, settingsAfter;
            public int existingObjects, changedNodes, preservedTexts, preservedCallbacks;
            public List<string> added = new List<string>();
        }
        public static void SyncWarehouseRedrawFromCommandLine()
        {
            throw new System.NotSupportedException("UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.");
#if false // Historical implementation; this target is retired.
            if(Application.isPlaying||!Application.dataPath.Replace('\\','/').EndsWith("/tmp/warehouse-redraw-20261012/Assets",StringComparison.Ordinal))
                throw new InvalidOperationException("Dedicated warehouse scratch project in edit mode required.");
            string[] args=Environment.GetCommandLineArgs(); int at=Array.IndexOf(args,"-warehouseRedrawSyncPath");
            if(at<0||at+1>=args.Length||!Path.IsPathRooted(args[at+1])) throw new ArgumentException("Absolute -warehouseRedrawSyncPath required");
            string output=Path.GetFullPath(args[at+1]);
            if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Output must be new");
            Directory.CreateDirectory(output); var report=new WarehouseSyncReport();
            string[] paths=AssetDatabase.FindAssets("t:Scene",new[]{EditableSceneFolder}).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p=>Regex.IsMatch(File.ReadAllText(p),@"(?m)^  editorPreviewScreen: 1\r?$"))
                .OrderBy(p=>p,StringComparer.Ordinal).ToArray();
            foreach(string path in paths)
            {
                var record=new WarehouseSyncScene{path=path,beforeSha256=WarehouseHash(path),metaSha256=WarehouseHash(path+".meta"),settingsBefore=WarehouseSettings(File.ReadAllText(path))};
                report.scenes.Add(record);
                string backup=Path.Combine(output,"originals",path);Directory.CreateDirectory(Path.GetDirectoryName(backup));File.Copy(path,backup);File.Copy(path+".meta",backup+".meta");
                SyncWarehouseRedrawScene(path,record);
                record.afterSha256=WarehouseHash(path);record.settingsAfter=WarehouseSettings(File.ReadAllText(path));
                if(record.settingsBefore!=record.settingsAfter) throw new InvalidOperationException("Preview configuration changed: "+path);
                File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true),new UTF8Encoding(false));
            }
            AssetDatabase.SaveAssets();
            report.dependencies=AssetDatabase.GetDependencies(paths,true).Where(p=>p.StartsWith(EditableVisualAssetFolder+"/",StringComparison.Ordinal)).Distinct().OrderBy(p=>p,StringComparer.Ordinal).ToList();
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true),new UTF8Encoding(false));
            Debug.Log("WAREHOUSE_REDRAW_SAVED_SYNC: "+paths.Length+" authored scenes refreshed without replacement.");

#endif
        }
        private static void SyncWarehouseRedrawScene(string path,WarehouseSyncScene report)
        {
            Scene scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            var destination=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
            Transform target=destination.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Phase Content");
            Transform[] originals=target.GetComponentsInChildren<Transform>(true);
            report.existingObjects=originals.Length;
            var originalActive=originals.ToDictionary(t=>t,t=>t.gameObject.activeSelf);
            var texts=target.GetComponentsInChildren<Text>(true).ToDictionary(t=>t,t=>t.text);
            string authoredCapacity=texts.Where(p=>p.Key.name=="Warehouse Storage Summary"||p.Key.name=="Warehouse Rare Summary")
                .Select(p=>p.Value.Split('\n').Skip(1).FirstOrDefault()).FirstOrDefault();
            var callbacks=target.GetComponentsInChildren<Button>(true).ToDictionary(b=>b,b=>WarehouseCallbacks(b));
            var interactable=target.GetComponentsInChildren<Button>(true).ToDictionary(b=>b,b=>b.interactable);
            Scene stagingScene=EditorSceneManager.NewPreviewScene();GameObject host=null;
            try
            {
                host=new GameObject("__WarehouseRedrawPresentationStaging");host.SetActive(false);SceneManager.MoveGameObjectToScene(host,stagingScene);
                var source=host.AddComponent<CoreGameplayDemoController>();CopyProvisionPreviewSettings(destination,source);host.SetActive(true);source.RefreshEditorPreview();
                bool rare=originals.Any(t=>t.name=="Warehouse Rare Summary"),basic=originals.Any(t=>t.name=="Warehouse Storage Summary");
                int state=path.Contains("AdjustCargo")?1:path.Contains("SellCargo")?2:0;
                if(rare) source.ConfigureEditorPreviewWarehouseRare(state);
                else if(basic){source.Session.SelectShip(1);source.ConfigureEditorPreviewWarehouseStorage(state);}
                else
                {
                    string[] owned=originals.Where(t=>t.name.StartsWith("Warehouse Treasure ",StringComparison.Ordinal)&&t.GetComponent<Button>()!=null&&CoreWarehouseCollection.Catalog.Any(d=>"Warehouse Treasure "+d.Id==t.name)).Select(t=>t.name.Substring("Warehouse Treasure ".Length)).ToArray();
                    var displayed=new string[6];
                    for(int i=0;i<6;i++)
                    {
                        Text label=originals.Where(t=>t.name=="Warehouse Display Label "+i).Select(t=>t.GetComponent<Text>()).Single();
                        displayed[i]=CoreWarehouseCollection.Catalog.FirstOrDefault(d=>d.Name==label.text)?.Id??string.Empty;
                    }
                    source.WarehouseCollection.Import(string.Join(",",owned),string.Join(",",displayed));
                    string selectedTitle=originals.Where(t=>t.name=="Warehouse Treasure Title").Select(t=>t.GetComponent<Text>().text).FirstOrDefault();
                    string selectedId=CoreWarehouseCollection.Catalog.FirstOrDefault(d=>d.Name==selectedTitle)?.Id??string.Empty;
                    typeof(CoreGameplayDemoController).GetField("selectedWarehouseTreasureId",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(source,selectedId);
                    Text displayAction=originals.Where(t=>t.name=="Warehouse Display Selected").Select(t=>t.GetComponentInChildren<Text>(true)).FirstOrDefault();
                    Match selectedSlot=Regex.Match(displayAction?.text??"",@"展位\s*(\d+)");
                    if(selectedSlot.Success)typeof(CoreGameplayDemoController).GetField("selectedWarehouseDisplaySlot",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(source,int.Parse(selectedSlot.Groups[1].Value)-1);
                    typeof(CoreGameplayDemoController).GetMethod("Render",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(source,null);
                }
                PreviewSceneSpec spec=Array.Find(Specs,s=>s.Screen==CoreGameplayPreviewScreen.Warehouse);
                PersistGeneratedSceneVisuals(host.transform,spec);
                Transform sourceRoot=host.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Phase Content");
                var map=new Dictionary<Transform,Transform>{{sourceRoot,target}};
                var addedSources=new HashSet<Transform>();
                foreach(Transform src in sourceRoot.GetComponentsInChildren<Transform>(true).Skip(1))
                {
                    bool scoped=src.name.StartsWith("Warehouse ",StringComparison.Ordinal)||AncestorsWarehouse(src,sourceRoot);
                    if(!scoped) continue;
                    if(!map.TryGetValue(src.parent,out Transform parent))
                    {
                        parent=target.Find(RelativeWarehousePath(src.parent,sourceRoot));
                        if(parent==null) continue;
                    }
                    Transform dst=parent.Find(src.name);
                    if(dst==null)
                    {
                        Transform[] unique=originals.Where(t=>t.name==src.name).ToArray();
                        if(unique.Length==1) dst=unique[0];
                    }
                    if(dst==null&&(WarehouseDecoration(src)||addedSources.Contains(src.parent)))
                    {
                        GameObject clone=UnityEngine.Object.Instantiate(src.gameObject,parent,false);clone.name=src.name;
                        for(int i=clone.transform.childCount-1;i>=0;i--) UnityEngine.Object.DestroyImmediate(clone.transform.GetChild(i).gameObject);
                        dst=clone.transform;foreach(Transform clean in dst.GetComponentsInChildren<Transform>(true)) { clean.gameObject.hideFlags=HideFlags.None; foreach(Component c in clean.GetComponents<Component>()) if(c!=null)c.hideFlags=HideFlags.None; }report.added.Add(RelativeWarehousePath(src,sourceRoot));
                        addedSources.Add(src);
                    }
                    if(dst==null) continue;
                    if(dst.parent!=parent) dst.SetParent(parent,false);
                    map[src]=dst;WarehouseCopyStyle(src,dst);report.changedNodes++;
                    if(src.name=="Warehouse Storage Summary"||src.name=="Warehouse Rare Summary")
                    {
                        Text label=dst.GetComponent<Text>();string value=texts.TryGetValue(label,out string authored)?authored:label.text;
                        string[] lines=value.Split('\n');label.text=lines[0];texts[label]=label.text;
                    }
                }
                foreach(var pair in map.Where(p=>p.Key!=sourceRoot))
                {
                    // Position decoration and readable overlays in runtime order within
                    // the scoped Warehouse parent. Existing user objects are never deleted.
                    pair.Value.SetSiblingIndex(Math.Min(pair.Key.GetSiblingIndex(),pair.Value.parent.childCount-1));
                }
                Transform targetLedger=target.Find("Warehouse Panel/Warehouse Ledger");
                Text capacity=targetLedger?.GetComponentsInChildren<Text>(true).FirstOrDefault(t=>t.name=="Warehouse Cargo Capacity");
                if(capacity!=null&&!string.IsNullOrEmpty(authoredCapacity))
                {
                    capacity.text=authoredCapacity;
                    Match amounts=Regex.Match(authoredCapacity,@"货舱\s+([\d.]+)\s*/\s*([\d.]+)");
                    if(!amounts.Success) throw new InvalidOperationException("Cannot preserve authored cargo capacity gauge: "+path);
                    float used=float.Parse(amounts.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture);
                    float maximum=float.Parse(amounts.Groups[2].Value,System.Globalization.CultureInfo.InvariantCulture);
                    var track=targetLedger.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="Warehouse Cargo Capacity Track");
                    var fill=targetLedger.GetComponentsInChildren<Image>(true).Single(t=>t.name=="Warehouse Cargo Capacity Fill");
                    float available=track.rect.width-6f,ratio=maximum>0f?Mathf.Clamp01(used/maximum):0f,width=available*ratio;
                    fill.rectTransform.sizeDelta=new Vector2(width,14f);fill.rectTransform.anchoredPosition=new Vector2(-(available-width)*.5f,0f);
                    fill.enabled=ratio>0f;fill.color=ratio>=.95f?new Color(.73f,.44f,.18f):new Color(.06f,.51f,.55f);
                }
                foreach(var item in texts) if(item.Key!=null&&item.Key.text!=item.Value) throw new InvalidOperationException("Authored text changed: "+item.Key.name);
                foreach(var item in callbacks) if(WarehouseCallbacks(item.Key)!=item.Value||item.Key.interactable!=interactable[item.Key]) throw new InvalidOperationException("Authored action changed: "+item.Key.name);
                foreach(var item in originalActive) if(item.Key==null||item.Key.gameObject.activeSelf!=item.Value) throw new InvalidOperationException("Authored object removed or state changed");
                report.preservedTexts=texts.Count;report.preservedCallbacks=callbacks.Count;
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene,path))throw new InvalidOperationException("Failed to save "+path);
                RepairSavedNavigationCanvasScale(AbsolutePath(path));AssertSavedNavigationCanvasScale(AbsolutePath(path));
            }
            finally{if(host!=null)UnityEngine.Object.DestroyImmediate(host);EditorSceneManager.ClosePreviewScene(stagingScene);SceneManager.SetActiveScene(scene);}
        }
        private static bool AncestorsWarehouse(Transform item,Transform root){for(Transform p=item.parent;p!=root&&p!=null;p=p.parent)if(p.name.StartsWith("Warehouse ",StringComparison.Ordinal))return true;return false;}
        private static string RelativeWarehousePath(Transform item,Transform root){var list=new List<string>();for(Transform p=item;p!=root;p=p.parent)list.Add(p.name);list.Reverse();return string.Join("/",list);}
        private static bool WarehouseDecoration(Transform item)
        {
            string name=item.name;
            return name=="Warehouse Summary Bar"||name.StartsWith("Warehouse Cargo Capacity",StringComparison.Ordinal)||
                name.StartsWith("Warehouse Display Plaque ",StringComparison.Ordinal)||name.StartsWith("Warehouse Collection Card Back ",StringComparison.Ordinal)||
                name.StartsWith("Warehouse Treasure Details",StringComparison.Ordinal)||name.StartsWith("Warehouse Cargo Table",StringComparison.Ordinal)||
                name.StartsWith("Warehouse Cargo Divider ",StringComparison.Ordinal)||name=="Warehouse Inventory Rim"||
                name.StartsWith("Warehouse Storage Icon ",StringComparison.Ordinal)||name.StartsWith("Warehouse Rare Icon ",StringComparison.Ordinal)||
                name.StartsWith("Warehouse Treasure Page",StringComparison.Ordinal)||name=="Warehouse Collection Catalog";
        }
        private static void WarehouseCopyStyle(Transform source,Transform target)
        {
            if(source is RectTransform a&&target is RectTransform b)
            {b.anchorMin=a.anchorMin;b.anchorMax=a.anchorMax;b.pivot=a.pivot;b.anchoredPosition=a.anchoredPosition;b.sizeDelta=a.sizeDelta;b.localScale=a.localScale;b.localRotation=a.localRotation;}
            if(source.GetComponent<Graphic>() is Graphic sg&&target.GetComponent<Graphic>() is Graphic dg)
            {
                dg.color=sg.color;dg.material=sg.material;dg.raycastTarget=sg.raycastTarget;
                if(sg is Image si&&dg is Image di){di.sprite=si.sprite;di.overrideSprite=si.overrideSprite;di.type=si.type;di.fillCenter=si.fillCenter;di.pixelsPerUnitMultiplier=si.pixelsPerUnitMultiplier;di.preserveAspect=si.preserveAspect;}
                if(sg is RawImage sr&&dg is RawImage dr)
                {
                    string existing=AssetDatabase.GetAssetPath(dr.texture);
                    // Upgrade known catalog art for the same item ID, leaving bespoke
                    // scene-only artwork untouched if it is outside the project catalog.
                    if(sr.name=="Warehouse Background"||sr.name=="Warehouse Cabinet Art"||existing.StartsWith("Assets/_Project/Resources/WarehouseV2/",StringComparison.Ordinal)||existing.StartsWith("Assets/_Project/Resources/ItemIcons/",StringComparison.Ordinal))
                    {dr.texture=sr.texture;dr.uvRect=sr.uvRect;}
                }
                if(sg is Text st&&dg is Text dt){dt.font=st.font;dt.fontSize=st.fontSize;dt.fontStyle=st.fontStyle;dt.alignment=st.alignment;dt.lineSpacing=st.lineSpacing;dt.resizeTextForBestFit=st.resizeTextForBestFit;dt.resizeTextMinSize=st.resizeTextMinSize;dt.resizeTextMaxSize=st.resizeTextMaxSize;dt.horizontalOverflow=st.horizontalOverflow;dt.verticalOverflow=st.verticalOverflow;}
            }
            if(source.GetComponent<Button>() is Button sb&&target.GetComponent<Button>() is Button db){db.colors=sb.colors;db.transition=sb.transition;db.spriteState=sb.spriteState;}
            if(source.GetComponent<WoodButtonRoleState>() is WoodButtonRoleState sw&&target.GetComponent<WoodButtonRoleState>() is WoodButtonRoleState dw)dw.enabled=sw.enabled;
            EditorUtility.SetDirty(target);
        }
        private static string WarehouseCallbacks(Button button)
        {
            var field=new SerializedObject(button).FindProperty("m_OnClick");return field==null?"":string.Join("|",Enumerable.Range(0,button.onClick.GetPersistentEventCount()).Select(i=>button.onClick.GetPersistentTarget(i)+"/"+button.onClick.GetPersistentMethodName(i)+"/"+button.onClick.GetPersistentListenerState(i)));
        }
        private static string WarehouseSettings(string source)=>string.Join("\n",Regex.Matches(source,@"(?m)^  editorPreview[^\r\n]*").Cast<Match>().Select(m=>m.Value));
        private static string WarehouseHash(string path){using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
    }
}
