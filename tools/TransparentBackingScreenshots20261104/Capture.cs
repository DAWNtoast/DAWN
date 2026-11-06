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
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    public static partial class TransparentBackingCapture
    {
        private const int Layer = 31;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] public sealed class Report
        {
            public string capturedAtUtc = DateTime.UtcNow.ToString("O");
            public string scope = "Current production UI, rendered in disposable Unity PreviewScenes at 1920x1080. In-memory sample states only. No player saves, purchases, transactions, scene saves or main project writes. Boxes identify the audited visible Image bounds.";
            public List<Frame> frames = new List<Frame>();
        }
        [Serializable] public sealed class Frame
        {
            public int index, width = 1920, height = 1080;
            public string title, state, variant, file, error;
            public string[] targets;
            public bool verifiedScaler;
            public List<Region> regions = new List<Region>();
            public List<string> visibleText = new List<string>();
        }
        [Serializable] public sealed class Region
        {
            public string name, path, sprite;
            public Rect bounds;
            public float alpha;
        }
        private static Frame Item(string title, string state, string variant, params string[] targets) =>
            new Frame {title=title,state=state,variant=variant,targets=targets};
        public static void Run()
        {
            if(Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stopped editor required.");
            if(SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("Graphics required.");
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,"..")).Replace('\\','/');
            if(!project.StartsWith("D:/Game/tmp/",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Isolated project required.");
            string[] args=Environment.GetCommandLineArgs(); int option=Array.IndexOf(args,"-transparentOutput");
            if(option<0 || option+1>=args.Length)throw new ArgumentException("Missing -transparentOutput.");
            string output=Path.GetFullPath(args[option+1]);
            if(!output.Replace('\\','/').StartsWith("D:/Game/outputs/transparent-backing-screenshots-20261104/",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Unexpected output path.");
            Directory.CreateDirectory(output);
            if(Directory.EnumerateFiles(output,"*.png").Any())throw new IOException("Preserve prior screenshots.");
            var report=new Report();
            report.frames.AddRange(new[]{
                Item("市场交易反馈条","Market","MarketNotice","Market Trade Notice Backing"),
                Item("归航结算：港口照片标题条","Settlement","SettlementCaption","Settlement Harbor Caption Shade"),
                Item("开箱动画：已获得奖励横带","Settlement","SettlementChest","Settlement Collected Shade"),
                Item("结算：保护率与折算说明条","Settlement","SettlementProtection","Settlement Protection Tint"),
                Item("出航：海图记录列表行底","VoyageDeparture","ChartRecords","Departure Dock Chart Record *"),
                Item("仓库：顶部汇总与空藏品格","Warehouse","Warehouse","Warehouse Summary Bar","Warehouse Collection Card Back 2"),
                Item("仓库：藏品来历详情底","Warehouse","WarehouseDetail","Warehouse Treasure Details"),
                Item("部件商店：闲置部件出售列表","ComponentShop","ComponentResale","Component Resale Row transparent-capture-spare-*"),
                Item("主菜单：成就列表行底","Achievements","Achievements","Achievement Row *"),
                Item("卡牌：关键词悬停说明","DeckBuilder","KeywordTooltip","Keyword Entry"),
                Item("战斗：状态悬停说明","Battle","StatusTooltip","Battle Status Tooltip"),
                Item("右上角：成就解锁通知","PortHub","AchievementToast","Achievement Notification"),
                Item("海战：卡牌施放参数面板","Battle","NavalChoice","Naval choice controls")
            });
            int onlyIndex=Array.IndexOf(args,"-transparentOnly");string only=onlyIndex>=0&&onlyIndex+1<args.Length?args[onlyIndex+1]:"";
            for(int i=0;i<report.frames.Count;i++){report.frames[i].index=i+1;report.frames[i].file=(i+1).ToString("D2")+"_"+report.frames[i].variant+".png";}
            if(!string.IsNullOrEmpty(only))report.frames=report.frames.Where(f=>only.Split(',').Contains(f.variant)).ToList();
            foreach(Frame frame in report.frames)
            {
                try{Capture(output,frame);Debug.Log("TRANSPARENT_CAPTURE_OK: "+frame.file+" regions="+frame.regions.Count);}
                catch(Exception error){frame.error=error.ToString();Debug.LogError("TRANSPARENT_CAPTURE_FAILED: "+frame.variant+"\n"+error);}
                File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
                WriteGallery(output,report);
            }
            Debug.Log("TRANSPARENT_CAPTURE_COMPLETE: "+report.frames.Count(f=>string.IsNullOrEmpty(f.error))+" / "+report.frames.Count);
        }
        private static void Capture(string output,Frame frame)
        {
            Scene scene=default;RenderTexture target=null,previous=RenderTexture.active;Texture2D pixels=null;
            try
            {
                scene=EditorSceneManager.NewPreviewScene();
                var host=new GameObject("Disposable Transparent Backing Capture");host.SetActive(false);SceneManager.MoveGameObjectToScene(host,scene);
                bool gameplay=Enum.TryParse(frame.state,out CoreGameplayPreviewScreen screen);
                if(gameplay)host.AddComponent<CoreGameplayDemoController>().ConfigureEditorPreview(screen,CoreGameplayPreviewPort.BrokenAnchor,0);else host.AddComponent<MainMenuController>();
                host.SetActive(true);
                if(!ArrangePort(host,frame.variant)&&!ArrangeSettlement(host,frame.variant)&&!ArrangeShared(host,frame.variant))throw new InvalidOperationException("Unhandled variant: "+frame.variant);
                string canvasName=gameplay?CoreGameplayDemoController.EditorPreviewCanvasName:MainMenuController.EditorPreviewCanvasName;
                Canvas canvas=host.GetComponentsInChildren<Canvas>(true).Single(c=>c.name==canvasName);
                CanvasScaler scaler=canvas.GetComponent<CanvasScaler>();if(!scaler.enabled)throw new InvalidOperationException("Production scaler disabled.");
                var cameraHost=new GameObject("Disposable Capture Camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraHost,scene);
                Camera camera=cameraHost.GetComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                camera.orthographic=true;camera.orthographicSize=5;camera.nearClipPlane=.1f;camera.farClipPlane=2000;camera.transform.position=new Vector3(0,0,-1000);camera.cullingMask=1<<Layer;camera.allowHDR=false;camera.allowMSAA=false;
                target=new RenderTexture(frame.width,frame.height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);target.Create();camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);camera.aspect=frame.width/(float)frame.height;
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=100;
                MethodInfo handle=typeof(CanvasScaler).GetMethod("Handle",Private);
                for(int pass=0;pass<4;pass++)
                {
                    Canvas.ForceUpdateCanvases();handle.Invoke(scaler,null);BeforeRenderShared(host,frame.variant,camera,pass);
                    foreach(Text text in canvas.GetComponentsInChildren<Text>(true))text.SetAllDirty();
                    foreach(Transform t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=Layer;
                    Canvas.ForceUpdateCanvases();
                    if(GraphicsSettings.currentRenderPipeline!=null)RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});else camera.Render();
                }
                float wr=frame.width/scaler.referenceResolution.x,hr=frame.height/scaler.referenceResolution.y;
                float expected=scaler.screenMatchMode==CanvasScaler.ScreenMatchMode.Expand?Mathf.Min(wr,hr):scaler.screenMatchMode==CanvasScaler.ScreenMatchMode.Shrink?Mathf.Max(wr,hr):Mathf.Pow(2,Mathf.Lerp(Mathf.Log(wr,2),Mathf.Log(hr,2),scaler.matchWidthOrHeight));
                frame.verifiedScaler=Mathf.Abs(canvas.renderingDisplaySize.x-frame.width)<.5f&&Mathf.Abs(canvas.renderingDisplaySize.y-frame.height)<.5f&&Mathf.Abs(canvas.scaleFactor-expected)<.001f;
                if(!frame.verifiedScaler)throw new InvalidOperationException("Render target did not drive production scaler.");
                foreach(Image image in canvas.GetComponentsInChildren<Image>(false))
                {
                    if(!image.enabled || EffectiveAlpha(image)<=.001f || !frame.targets.Any(n=>n.EndsWith("*")?image.name.StartsWith(n.TrimEnd('*'),StringComparison.Ordinal):image.name==n))continue;
                    if(image.sprite!=null)continue;
                    var corners=new Vector3[4];image.rectTransform.GetWorldCorners(corners);Vector2[] p=corners.Select(v=>(Vector2)camera.WorldToScreenPoint(v)).ToArray();
                    Rect rect=Rect.MinMaxRect(p.Min(v=>v.x),frame.height-p.Max(v=>v.y),p.Max(v=>v.x),frame.height-p.Min(v=>v.y));
                    if(rect.width<=0||rect.height<=0||!rect.Overlaps(new Rect(0,0,frame.width,frame.height)))continue;
                    frame.regions.Add(new Region{name=image.name,path=PathOf(image.transform,canvas.transform),bounds=rect,alpha=EffectiveAlpha(image),sprite=image.sprite!=null?image.sprite.name:null});
                }
                if(frame.regions.Count==0)throw new InvalidOperationException("No visible target backing: "+string.Join(", ",frame.targets));
                frame.visibleText=canvas.GetComponentsInChildren<Text>(false).Where(t=>t.enabled&&!string.IsNullOrEmpty(t.text)&&EffectiveAlpha(t)>.01f).Select(t=>t.name+": "+t.text).ToList();
                RenderTexture.active=target;pixels=new Texture2D(frame.width,frame.height,TextureFormat.RGB24,false,false);pixels.ReadPixels(new Rect(0,0,frame.width,frame.height),0,0);pixels.Apply(false,false);
                File.WriteAllBytes(Path.Combine(output,frame.file),pixels.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;if(pixels!=null)Object.DestroyImmediate(pixels);if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);if(target!=null){target.Release();Object.DestroyImmediate(target);}}
        }
        private static void Field(object target,string name,object value)
        {
            FieldInfo field=target.GetType().GetField(name,Private);
            if(field!=null){field.SetValue(target,value);return;}
            PropertyInfo property=target.GetType().GetProperty(name,Private);
            if(property==null)throw new MissingMemberException(target.GetType().Name,name);
            property.SetValue(target,value);
        }
        private static object Call(object target,string name,params object[] args)
        {
            var method=target.GetType().GetMethods(Private).Single(m=>m.Name==name&&m.GetParameters().Length==args.Length);
            return method.Invoke(target,args);
        }
        private static void Rebuild(CoreGameplayDemoController controller)
        {
            Field(controller,"editorPreviewBuilding",true);
            try{Call(controller,"BuildSelectedEditorPreview");}finally{Field(controller,"editorPreviewBuilding",false);}
        }
        private static float EffectiveAlpha(Graphic graphic)
        {
            float alpha=graphic.color.a;for(Transform t=graphic.transform;t!=null;t=t.parent){var group=t.GetComponent<CanvasGroup>();if(group==null)continue;alpha*=group.alpha;if(group.ignoreParentGroups)break;}return alpha;
        }
        private static string PathOf(Transform child,Transform root){var parts=new List<string>();for(Transform t=child;t!=null&&t!=root;t=t.parent)parts.Add(t.name);parts.Reverse();return string.Join("/",parts);}
        private static string H(string text)=>(text??"").Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;").Replace("\"","&quot;");
        private static void WriteGallery(string output,Report report)
        {
            var html=new StringBuilder("<!doctype html><html lang=zh-CN><meta charset=utf-8><meta name=viewport content='width=device-width,initial-scale=1'><title>半透明方框逐项截图</title><style>body{font:16px system-ui;background:#15191c;color:#eee;max-width:1600px;margin:24px auto;padding:0 20px}a{color:#e8c583}nav{columns:2;line-height:1.9}section{margin:40px 0}.shot{position:relative}.shot img{width:100%;display:block}.box{position:absolute;border:3px solid #ff4b45;box-sizing:border-box;pointer-events:none}.hide .box{display:none}small{color:#bbc1c4}pre{white-space:pre-wrap}button{padding:10px 16px;background:#383f46;color:white;border:1px solid #777;cursor:pointer}</style><h1>半透明方框逐项截图</h1><p>当前源码的 Unity 实际界面，1920×1080。使用独立内存样本；红框标出对应衬底。点击原图可查看未标注截图。</p><button onclick=\"document.body.classList.toggle('hide')\">显示／隐藏标记</button><nav>");
            foreach(Frame frame in report.frames)html.Append("<div><a href='#f").Append(frame.index).Append("'>").Append(frame.index.ToString("D2")).Append(" ").Append(H(frame.title)).Append("</a></div>");html.Append("</nav>");
            foreach(Frame frame in report.frames)
            {
                html.Append("<section id='f").Append(frame.index).Append("'><h2>").Append(frame.index.ToString("D2")).Append(" · ").Append(H(frame.title)).Append("</h2>");
                if(string.IsNullOrEmpty(frame.error)&&File.Exists(Path.Combine(output,frame.file)))
                {
                    html.Append("<div class=shot><img loading=lazy src='").Append(H(frame.file)).Append("' alt='").Append(H(frame.title)).Append("'>");
                    foreach(Region region in frame.regions)html.Append("<i class=box style='left:").Append((region.bounds.x/frame.width*100).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)).Append("%;top:").Append((region.bounds.y/frame.height*100).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)).Append("%;width:").Append((region.bounds.width/frame.width*100).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)).Append("%;height:").Append((region.bounds.height/frame.height*100).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)).Append("%'></i>");
                    html.Append("</div><p><a target=_blank href='").Append(H(frame.file)).Append("'>查看原图</a></p>");
                }
                else html.Append("<pre>").Append(H(frame.error??"等待截图")).Append("</pre>");
                html.Append("</section>");
            }
            html.Append("</html>");File.WriteAllText(Path.Combine(output,"index.html"),html.ToString(),new UTF8Encoding(false));
        }
    }
}
