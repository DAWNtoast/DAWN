using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    // Installed only into the dedicated disposable project by run-validation.ps1.
    public static class WarehouseRedrawCapture
    {
        private const int Layer = 31;
        [Serializable] private sealed class Review
        {
            public string utc, scope = "Production-built isolated editor fixtures: six ports x three tabs; broken-anchor dialogs and catalog; responsive views. No gameplay save writes.";
            public List<Page> pages = new List<Page>();
            public List<string> errors = new List<string>();
        }
        [Serializable] private sealed class Page
        {
            public string port, state, file, backgroundPath;
            public int width, height, basicRows, rareRows, slots;
            public Rect backgroundUv, statusBounds, ledgerBounds;
            public List<Measure> elements = new List<Measure>();
        }
        [Serializable] private sealed class Measure { public string name, text; public Rect bounds; public bool interactable; }
        private static string Output(string argument)
        {
            if (!Application.dataPath.Replace('\\','/').EndsWith("/tmp/warehouse-redraw-20261012/Assets",StringComparison.Ordinal))
                throw new InvalidOperationException("Dedicated warehouse scratch project required.");
            string[] args=Environment.GetCommandLineArgs(); int at=Array.IndexOf(args,argument);
            if(at<0||at+1>=args.Length||!Path.IsPathRooted(args[at+1])) throw new ArgumentException(argument+" ABSOLUTE_DIRECTORY required.");
            string output=Path.GetFullPath(args[at+1]);
            if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Output must be new or empty.");
            Directory.CreateDirectory(output); return output;
        }
        public static void CaptureSavedFromCommandLine()
        {
            throw new System.NotSupportedException("UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.");
#if false // Historical implementation; this target is retired.
            string output=Output("-warehouseSavedCapturePath");
            MethodInfo capture=typeof(UIPreviewSceneAcceptance).GetMethod("Capture",BindingFlags.NonPublic|BindingFlags.Static,
                null,new[]{typeof(string),typeof(string),typeof(string[]),typeof(string)},null);
            string root="Assets/_Project/Scenes/UIPreviewsForMe";
            string[] references=Directory.GetFiles(root,"*.unity",SearchOption.AllDirectories)
                .Where(p=>System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(p),@"(?m)^  editorPreviewScreen: 1\r?$"))
                .Select(p=>p.Replace('\\','/').Substring(root.Length+1)).ToArray();
            capture.Invoke(null,new object[]{output,root,references,null});

#endif
        }
        public static void CaptureFromCommandLine()
        {
            string output=Output("-warehouseRedrawCapturePath");
            if(Application.isPlaying||SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null) throw new InvalidOperationException("Non-Play graphics editor required.");
            var report=new Review{utc=DateTime.UtcNow.ToString("O")};
            foreach(CoreGameplayPreviewPort port in Enum.GetValues(typeof(CoreGameplayPreviewPort)))
                foreach(string state in new[]{"Collection","Basic","Rare"}) Capture(output,report,port,state,1920,1080);
            foreach(string state in new[]{"BasicActions","BasicAdjust","BasicSale","RareActions","RareAdjust","RareSale","Catalog"})
                Capture(output,report,CoreGameplayPreviewPort.BrokenAnchor,state,1920,1080);
            foreach(Vector2Int size in new[]{new Vector2Int(1280,720),new Vector2Int(1280,1024)})
                foreach(string state in new[]{"Collection","Basic","Rare"}) Capture(output,report,CoreGameplayPreviewPort.BrokenAnchor,state,size.x,size.y);
            foreach(var group in report.pages.Where(p=>p.state=="Collection"||p.state=="Basic"||p.state=="Rare").GroupBy(p=>p.width+"x"+p.height))
            {
                Rect first=group.First().statusBounds;
                foreach(Page page in group)
                    if(Mathf.Abs(first.x-page.statusBounds.x)>.1f||Mathf.Abs(first.y-page.statusBounds.y)>.1f||Mathf.Abs(first.width-page.statusBounds.width)>.1f||Mathf.Abs(first.height-page.statusBounds.height)>.1f)
                        report.errors.Add(page.file+": status bar alignment differs from "+group.First().file);
            }
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true),new UTF8Encoding(false));
            var html=new StringBuilder("<!doctype html><meta charset=utf-8><title>Warehouse redraw review</title><style>body{background:#152123;color:#efdfbd;font:16px system-ui;margin:24px}figure{max-width:1440px;margin:32px auto}img{width:100%;display:block}a{color:#e5bb72}</style><h1>Warehouse redraw review</h1><p>Six ports, all tabs, dialogs, catalog and responsive views. <a href=report.json>Geometry report</a></p>");
            foreach(Page page in report.pages) html.Append("<figure><figcaption>").Append(page.port).Append(" · ").Append(page.state).Append(" · ").Append(page.width).Append('×').Append(page.height).Append("</figcaption><img loading=lazy src=\"").Append(page.file).Append("\"></figure>");
            File.WriteAllText(Path.Combine(output,"index.html"),html.ToString(),new UTF8Encoding(false));
            if(report.errors.Count>0) throw new InvalidOperationException("Warehouse capture failed: "+string.Join("\n",report.errors));
            Debug.Log("WAREHOUSE_REDRAW_REVIEW: "+report.pages.Count+" frames; all shared status bars aligned. "+output);
        }
        private static void Capture(string output,Review report,CoreGameplayPreviewPort port,string state,int width,int height)
        {
            var page=new Page{state=state,width=width,height=height,file=port+"_"+state+"_"+width+"x"+height+".png"}; report.pages.Add(page);
            Scene scene=EditorSceneManager.NewPreviewScene(); GameObject host=null,cameraHost=null; RenderTexture target=null,old=RenderTexture.active;
            try
            {
                host=new GameObject("Warehouse Redraw Review Host"); host.SetActive(false); SceneManager.MoveGameObjectToScene(host,scene);
                var controller=host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Warehouse,port,0); host.SetActive(true);
                if(!controller.HasCompleteEditorPreview) throw new InvalidOperationException("Incomplete warehouse preview");
                page.port=controller.Session.CurrentPort;
                if(state.StartsWith("Basic",StringComparison.Ordinal))
                {
                    controller.Session.SelectShip(1);
                    controller.ConfigureEditorPreviewWarehouseStorage(state=="BasicAdjust"?1:state=="BasicSale"?2:0);
                }
                else if(state.StartsWith("Rare",StringComparison.Ordinal)) controller.ConfigureEditorPreviewWarehouseRare(state=="RareAdjust"?1:state=="RareSale"?2:0);
                if(state=="BasicActions"||state=="RareActions")
                {
                    var type=typeof(CoreGameplayDemoController);
                    var quote=type.GetMethod("CreateWarehouseCargoQuote",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(string),typeof(bool)},null).Invoke(controller,new object[]{state=="RareActions"?"gem":"wood",true});
                    type.GetMethod("BuildWarehouseCargoActions",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(controller,new[]{quote});
                }
                if(state=="Catalog") Find<Button>(host,"Warehouse Collection Catalog").onClick.Invoke();
                cameraHost=new GameObject("Warehouse Redraw Camera",typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraHost,scene);
                var camera=cameraHost.GetComponent<Camera>(); camera.enabled=false; camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(.025f,.04f,.05f); camera.orthographic=true;
                float scale=Mathf.Min(width/1920f,height/1080f); camera.orthographicSize=height/scale/2f;
                camera.aspect=width/(float)height; camera.nearClipPlane=.1f; camera.farClipPlane=2000f;
                camera.transform.position=new Vector3(0,0,-1000); camera.cullingMask=1<<Layer; camera.scene=scene; camera.allowHDR=camera.allowMSAA=false;
                target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB); target.Create(); camera.targetTexture=target;
                var canvas=host.GetComponentInChildren<Canvas>(); canvas.renderMode=RenderMode.WorldSpace; canvas.worldCamera=camera;
                var rect=canvas.GetComponent<RectTransform>(); rect.sizeDelta=new Vector2(width/scale,height/scale); rect.position=Vector3.zero; rect.localScale=Vector3.one;
                foreach(Transform item in canvas.GetComponentsInChildren<Transform>(true)) item.gameObject.layer=Layer;
                Canvas.ForceUpdateCanvases();
                var background=Find<RawImage>(host,"Warehouse Background"); page.backgroundPath=AssetDatabase.GetAssetPath(background.texture); page.backgroundUv=background.uvRect;
                if(background.uvRect!=new Rect(0,0,1,1)) throw new InvalidOperationException("Regional warehouse background is cropped");
                if(background.color!=Color.white) throw new InvalidOperationException("Warehouse background unexpectedly tinted");
                if(state!="Catalog")
                {
                    page.statusBounds=Bounds(Find<RectTransform>(host,"Warehouse Summary Bar"),camera,height);
                    page.ledgerBounds=Bounds(Find<RectTransform>(host,"Warehouse Ledger"),camera,height);
                    page.basicRows=host.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("Warehouse Storage Row ",StringComparison.Ordinal));
                    page.rareRows=host.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("Warehouse Rare Row ",StringComparison.Ordinal));
                    page.slots=host.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("Warehouse Display Slot ",StringComparison.Ordinal));
                    if(page.slots!=6) throw new InvalidOperationException("Expected six display slots");
                    if(state.StartsWith("Basic",StringComparison.Ordinal)&&page.basicRows!=9) throw new InvalidOperationException("Expected nine basic rows");
                    if(state.StartsWith("Rare",StringComparison.Ordinal)&&page.rareRows!=6) throw new InvalidOperationException("Expected six rare rows");
                }
                foreach(RectTransform item in host.GetComponentsInChildren<RectTransform>().Where(t=>t.name.StartsWith("Warehouse ",StringComparison.Ordinal)))
                {
                    var text=item.GetComponent<Text>(); var button=item.GetComponent<Button>();
                    if(text!=null||button!=null||item.name=="Warehouse Summary Bar") page.elements.Add(new Measure{name=item.name,text=text==null?null:text.text,bounds=Bounds(item,camera,height),interactable=button!=null&&button.interactable});
                }
                if(GraphicsSettings.currentRenderPipeline!=null) RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target}); else camera.Render();
                RenderTexture.active=target; var image=new Texture2D(width,height,TextureFormat.RGB24,false,false);
                try { image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply(false,false); File.WriteAllBytes(Path.Combine(output,page.file),image.EncodeToPNG()); }
                finally { UnityEngine.Object.DestroyImmediate(image); }
            }
            catch(Exception error) { report.errors.Add(page.file+": "+error); Debug.LogException(error); }
            finally
            {
                RenderTexture.active=old;
                if(cameraHost!=null) UnityEngine.Object.DestroyImmediate(cameraHost); if(host!=null) UnityEngine.Object.DestroyImmediate(host);
                if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);} EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        private static T Find<T>(GameObject host,string name) where T:Component => host.GetComponentsInChildren<T>(true).Single(item=>item.name==name&&item.gameObject.activeInHierarchy);
        private static Rect Bounds(RectTransform rect,Camera camera,int height)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);var a=camera.WorldToScreenPoint(corners[0]);var b=camera.WorldToScreenPoint(corners[2]);return new Rect(a.x,height-b.y,b.x-a.x,b.y-a.y);
        }
    }
}

