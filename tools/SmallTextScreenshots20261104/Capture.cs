using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace ShatteredSail.Editor {
public static partial class SmallTextCapture {
 const int Layer=31;
 const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 [Serializable] public class Report { public string capturedAtUtc=DateTime.UtcNow.ToString("O"); public string scope="Current production Unity UI rendered at 1920x1080 in disposable PreviewScenes with in-memory sample states. No authored scenes, player saves or production files changed. Red boxes are separate review annotations."; public List<Frame> frames=new List<Frame>(); }
 [Serializable] public class Frame { public int index,width=1920,height=1080; public string title,state,variant,file,error,condition; public string[] targets; public bool verifiedScaler; public List<Region> regions=new List<Region>(); public List<string> visibleText=new List<string>(); }
 [Serializable] public class Region { public string name,text; public int fontSize; public Rect bounds; }
 static Frame Item(string title,string state,string variant,string condition,params string[] targets)=>new Frame{title=title,state=state,variant=variant,condition=condition,targets=targets};
 public static void Run() {
  if(Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode || SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("Stopped graphics-enabled editor required.");
  string project=Path.GetFullPath(Path.Combine(Application.dataPath,"..")).Replace('\\','/'); if(!project.StartsWith("D:/Game/tmp/",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Isolated project required.");
  string[] args=Environment.GetCommandLineArgs(); int option=Array.IndexOf(args,"-smallTextOutput"); if(option<0)throw new ArgumentException("Missing output.");
  string output=Path.GetFullPath(args[option+1]); if(!output.Replace('\\','/').StartsWith("D:/Game/outputs/small-text-screenshots-20261104/",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Unexpected output.");
  Directory.CreateDirectory(output); if(Directory.EnumerateFiles(output,"*.png").Any())throw new IOException("Preserve previous images.");
  var report=new Report(); report.frames.AddRange(new[]{
   Item("组建牌组底部","DeckBuilder","DeckBuilder","组建牌组页面；底部常驻说明","Deck Builder Interaction Hint"),
   Item("开包结果页底部","CardWorkshop","PackResults","独立样本中呈现已开包结果；未实际消耗卡包","Workshop Packs Results Hint"),
   Item("卡牌强化左栏","CardUpgrade","Upgrade","卡牌强化页面；左栏标题下方","Workshop Upgrade Library Hint"),
   Item("出航码头：未选普通海图","VoyageDeparture","DepartureChart","普通海图模式，尚未选定有效海图","Departure Route Summary"),
   Item("出航码头：未选藏宝海图","VoyageDeparture","DepartureTreasure","藏宝海图模式，尚未选定种子","Departure Route Summary"),
   Item("海图种子输入框","VoyageDeparture","ChartPlaceholder","普通海图输入框为空时的占位文字","Placeholder"),
   Item("航行海图：封锁首领标记","Navigation","BossGuidance","已发现封锁首领的独立航行样本","Destination Boss Guidance Caption"),
   Item("藏宝图桌：可合成提示","TreasureMap","TreasureCombine","碎片达到4/4，可以合成","Treasure Route Progress 0"),
   Item("市场：选择收货位置","Market","MarketChoice","打开收货位置弹窗；未执行付款","Market Purchase Choice Quote"),
   Item("闲置部件回收：空库存","ComponentShop","ComponentResaleEmpty","闲置部件库存为空","Component Resale Empty Detail"),
   Item("船坞配件：空库存","ShipyardDetail","ShipyardEmpty","所选类别没有闲置配件","Shipyard Equipment Empty Inventory"),
   Item("海战：场地牌选择弹层","Battle","NavalChoice","场地牌选格过程中；未确认出牌","Naval choice instruction")
  });
  for(int i=0;i<report.frames.Count;i++){var f=report.frames[i];f.index=i+1;f.file=f.index.ToString("D2")+"_"+f.variant+".png";}
  int oi=Array.IndexOf(args,"-smallTextOnly");if(oi>=0)report.frames=report.frames.Where(f=>args[oi+1].Split(',').Contains(f.variant)).ToList();
  foreach(var frame in report.frames){try{Capture(output,frame);Debug.Log("SMALL_TEXT_OK: "+frame.file);}catch(Exception e){while(e is TargetInvocationException&&e.InnerException!=null)e=e.InnerException;frame.error=e.ToString();Debug.LogWarning("SMALL_TEXT_FAILED: "+frame.variant+" "+e);}File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));}
  Debug.Log("SMALL_TEXT_COMPLETE: "+report.frames.Count(f=>string.IsNullOrEmpty(f.error))+" / "+report.frames.Count);
 }
 static void Capture(string output,Frame frame){
  Scene scene=default; RenderTexture target=null,previous=RenderTexture.active;Texture2D pixels=null;
  try{
   scene=EditorSceneManager.NewPreviewScene();var host=new GameObject("Disposable Small Text Screenshot");host.SetActive(false);SceneManager.MoveGameObjectToScene(host,scene);
   var controller=host.AddComponent<CoreGameplayDemoController>();controller.ConfigureEditorPreview((CoreGameplayPreviewScreen)Enum.Parse(typeof(CoreGameplayPreviewScreen),frame.state),CoreGameplayPreviewPort.BrokenAnchor,0);host.SetActive(true);
   if(!ArrangeWorkshop(host,frame.variant)&&!ArrangePort(host,frame.variant)&&!ArrangeOther(host,frame.variant))throw new InvalidOperationException("Unhandled variant "+frame.variant);
   Canvas canvas=host.GetComponentsInChildren<Canvas>(true).Single(c=>c.name==CoreGameplayDemoController.EditorPreviewCanvasName);CanvasScaler scaler=canvas.GetComponent<CanvasScaler>();
   var cameraHost=new GameObject("Disposable Camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraHost,scene);var camera=cameraHost.GetComponent<Camera>();
   camera.enabled=false;camera.scene=scene;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.orthographic=true;camera.orthographicSize=5;camera.nearClipPlane=.1f;camera.farClipPlane=2000;camera.transform.position=new Vector3(0,0,-1000);camera.cullingMask=1<<Layer;camera.allowHDR=false;camera.allowMSAA=false;
   target=new RenderTexture(frame.width,frame.height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);target.Create();camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);camera.aspect=frame.width/(float)frame.height;
   canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=100;MethodInfo handle=typeof(CanvasScaler).GetMethod("Handle",BindingFlags.Instance|BindingFlags.NonPublic);
   for(int pass=0;pass<4;pass++){Canvas.ForceUpdateCanvases();handle.Invoke(scaler,null);foreach(Text text in canvas.GetComponentsInChildren<Text>(true))text.SetAllDirty();foreach(Transform t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=Layer;Canvas.ForceUpdateCanvases();if(GraphicsSettings.currentRenderPipeline!=null)RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});else camera.Render();}
   float wr=frame.width/scaler.referenceResolution.x,hr=frame.height/scaler.referenceResolution.y;float expected=scaler.screenMatchMode==CanvasScaler.ScreenMatchMode.Expand?Mathf.Min(wr,hr):scaler.screenMatchMode==CanvasScaler.ScreenMatchMode.Shrink?Mathf.Max(wr,hr):Mathf.Pow(2,Mathf.Lerp(Mathf.Log(wr,2),Mathf.Log(hr,2),scaler.matchWidthOrHeight));
   frame.verifiedScaler=scaler.enabled&&Mathf.Abs(canvas.renderingDisplaySize.x-frame.width)<.5f&&Mathf.Abs(canvas.renderingDisplaySize.y-frame.height)<.5f&&Mathf.Abs(canvas.scaleFactor-expected)<.001f;if(!frame.verifiedScaler)throw new InvalidOperationException("Production scaler mismatch.");
   foreach(Text text in canvas.GetComponentsInChildren<Text>(false)){
    if(!text.enabled||string.IsNullOrEmpty(text.text)||EffectiveAlpha(text)<.01f)continue;
    frame.visibleText.Add(text.name+": "+text.text);
    if(!frame.targets.Any(n=>n.EndsWith("*")?text.name.StartsWith(n.TrimEnd('*'),StringComparison.Ordinal):text.name==n))continue;
    if(frame.variant=="ChartPlaceholder"&&!text.text.Contains("粘贴完整种子"))continue;
    var corners=new Vector3[4];text.rectTransform.GetWorldCorners(corners);Vector2[] p=corners.Select(v=>(Vector2)camera.WorldToScreenPoint(v)).ToArray();Rect bounds=Rect.MinMaxRect(p.Min(v=>v.x),frame.height-p.Max(v=>v.y),p.Max(v=>v.x),frame.height-p.Min(v=>v.y));
    if(bounds.width<=0||bounds.height<=0||!bounds.Overlaps(new Rect(0,0,frame.width,frame.height)))continue;
    frame.regions.Add(new Region{name=text.name,text=text.text,fontSize=text.fontSize,bounds=bounds});
   }
   if(frame.regions.Count==0)throw new InvalidOperationException("Visible target not found: "+string.Join(",",frame.targets)+"; available: "+string.Join(" | ",frame.visibleText));
   RenderTexture.active=target;pixels=new Texture2D(frame.width,frame.height,TextureFormat.RGB24,false,false);pixels.ReadPixels(new Rect(0,0,frame.width,frame.height),0,0);pixels.Apply(false,false);File.WriteAllBytes(Path.Combine(output,frame.file),pixels.EncodeToPNG());
  }finally{RenderTexture.active=previous;if(pixels!=null)Object.DestroyImmediate(pixels);if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);if(target!=null){target.Release();Object.DestroyImmediate(target);}}
 }
 static bool ArrangeWorkshop(GameObject host,string variant){
  if(variant=="DeckBuilder"||variant=="Upgrade")return true;if(variant!="PackResults")return false;
  var c=host.GetComponent<CoreGameplayDemoController>();
  Field(c,"workshopPacksOpen",true);Field(c,"workshopPackViewSession",c.Session);Field(c,"workshopPackViewPort",c.Session.CurrentPort);Field(c,"workshopOpenedPackId","neutral");Field(c,"workshopPackResultCards",new[]{0,1,2});
  Field(c,"editorPreviewBuilding",true);try{Call(c,"ClearPhaseContentForRender");Call(c,"BuildPort");}finally{Field(c,"editorPreviewBuilding",false);}return true;
 }
 static void Field(object target,string name,object value){var f=target.GetType().GetField(name,Private);if(f!=null){f.SetValue(target,value);return;}var p=target.GetType().GetProperty(name,Private);if(p==null)throw new MissingMemberException(target.GetType().Name,name);p.SetValue(target,value);}
 static object Get(object target,string name){var f=target.GetType().GetField(name,Private);if(f!=null)return f.GetValue(target);var p=target.GetType().GetProperty(name,Private);if(p==null)throw new MissingMemberException(target.GetType().Name,name);return p.GetValue(target);}
 static object Call(object target,string name,params object[] args){return target.GetType().GetMethods(Private).Single(m=>m.Name==name&&m.GetParameters().Length==args.Length).Invoke(target,args);}
 static void Rebuild(CoreGameplayDemoController c){Field(c,"editorPreviewBuilding",true);try{Call(c,"BuildSelectedEditorPreview");}finally{Field(c,"editorPreviewBuilding",false);}}
 static float EffectiveAlpha(Graphic graphic){float a=graphic.color.a;for(Transform t=graphic.transform;t!=null;t=t.parent){var g=t.GetComponent<CanvasGroup>();if(g==null)continue;a*=g.alpha;if(g.ignoreParentGroups)break;}return a;}
}
}
