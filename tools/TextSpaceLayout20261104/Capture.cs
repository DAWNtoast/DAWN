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
public static partial class TextSpaceLayoutCapture20261104 {
 const int Layer=31;
 const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 [Serializable] public class Report { public string capturedAtUtc=DateTime.UtcNow.ToString("O"); public string scope="Current production Unity UI rendered at 1920x1080 in disposable PreviewScenes with in-memory sample states. No authored scenes, player saves or production files changed. Screenshots are unmodified production renders."; public List<Frame> frames=new List<Frame>(); }
 [Serializable] public class Frame { public int index,width=1920,height=1080; public string title,state,variant,file,error,condition; public string[] targets; public bool verifiedScaler; public List<Region> regions=new List<Region>(); public List<string> visibleText=new List<string>(); }
 [Serializable] public class Region { public string name,text; public int fontSize; public Rect bounds; }
 static Frame Item(string title,string state,string variant,string condition,params string[] targets)=>new Frame{title=title,state=state,variant=variant,condition=condition,targets=targets};
 public static void Run() {
  if(Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode || SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("Stopped graphics-enabled editor required.");
  string project=Path.GetFullPath(Path.Combine(Application.dataPath,"..")).Replace('\\','/'); if(!project.StartsWith("D:/Game/tmp/",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Isolated project required.");
  string[] args=Environment.GetCommandLineArgs(); int option=Array.IndexOf(args,"-textSpaceLayoutOutput"); if(option<0)throw new ArgumentException("Missing output.");
  string output=Path.GetFullPath(args[option+1]); if(!output.Replace('\\','/').StartsWith("D:/Game/outputs/text-space-layout-20261104/",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Unexpected output.");
  Directory.CreateDirectory(output); if(Directory.EnumerateFiles(output,"*.png").Any())throw new IOException("Preserve previous images.");
  var report=new Report(); report.frames.AddRange(new[]{
 Item("组建牌组：重新排版","DeckBuilder","DeckBuilder","独立内存样本，真实生产界面","Deck Builder Deck Title","Deck Builder Capacity Count"),
 Item("卡包选择：重新排版","CardWorkshop","PackSelection","独立内存样本，未开包","Workshop Packs Heading","Workshop Packs Empty Title"),
 Item("开包结果：重新排版","CardWorkshop","PackResults","内存展示样本，不消耗卡包","Workshop Packs Results Heading","Workshop Pack Name *"),
 Item("卡牌强化：重新排版","CardUpgrade","Upgrade","独立内存样本，未执行强化","Upgrade List Name *","Upgrade Selected Name"),
 Item("市场收货：紧凑弹窗","Market","MarketChoice","仅展示收货弹窗，不发生交易","Market Purchase Choice Quote"),
 Item("闲置部件：空库存排版","ComponentShop","ComponentResaleEmpty","独立空库存样本","Component Resale Empty"),
 Item("船坞配件：空库存","ShipyardDetail","ShipyardEmpty","独立空库存样本","Shipyard Equipment Inventory Heading"),
 Item("场地牌：紧凑选格框","Battle","NavalChoice","独立内存样本，未确认出牌","Naval choice title","Cancel Naval Card Choice label"),
 Item("水流方向：控件排版","Battle","NavalDirection","独立内存样本，仅选择锚点","Naval choice title","Cancel Naval Card Choice label")
 });
  for(int i=0;i<report.frames.Count;i++){var f=report.frames[i];f.index=i+1;f.file=f.index.ToString("D2")+"_"+f.variant+".png";}
  int oi=Array.IndexOf(args,"-smallTextOnly");if(oi>=0)report.frames=report.frames.Where(f=>args[oi+1].Split(',').Contains(f.variant)).ToList();
  foreach(var frame in report.frames){try{Capture(output,frame);Debug.Log("SMALL_TEXT_OK: "+frame.file);}catch(Exception e){while(e is TargetInvocationException&&e.InnerException!=null)e=e.InnerException;frame.error=e.ToString();Debug.LogWarning("SMALL_TEXT_FAILED: "+frame.variant+" "+e);}File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));}
  Debug.Log("SMALL_TEXT_COMPLETE: "+report.frames.Count(f=>string.IsNullOrEmpty(f.error))+" / "+report.frames.Count);
  if(report.frames.Any(f=>!string.IsNullOrEmpty(f.error)))throw new InvalidOperationException("Capture failed; inspect report.json.");
 }
 static void Capture(string output,Frame frame){
  Scene scene=default; RenderTexture target=null,previous=RenderTexture.active;Texture2D pixels=null;
  try{
   scene=EditorSceneManager.NewPreviewScene();var host=new GameObject("Disposable Small Text Screenshot");host.SetActive(false);SceneManager.MoveGameObjectToScene(host,scene);
   var controller=host.AddComponent<CoreGameplayDemoController>();controller.ConfigureEditorPreview((CoreGameplayPreviewScreen)Enum.Parse(typeof(CoreGameplayPreviewScreen),frame.state),CoreGameplayPreviewPort.BrokenAnchor,0);host.SetActive(true);
   if(!ArrangeWorkshop(host,frame.variant))throw new InvalidOperationException("Unhandled variant "+frame.variant);
   Canvas canvas=host.GetComponentsInChildren<Canvas>(true).Single(c=>c.name==CoreGameplayDemoController.EditorPreviewCanvasName);CanvasScaler scaler=canvas.GetComponent<CanvasScaler>();
   var cameraHost=new GameObject("Disposable Camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraHost,scene);var camera=cameraHost.GetComponent<Camera>();
   camera.enabled=false;camera.scene=scene;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.orthographic=true;camera.orthographicSize=5;camera.nearClipPlane=.1f;camera.farClipPlane=2000;camera.transform.position=new Vector3(0,0,-1000);camera.cullingMask=1<<Layer;camera.allowHDR=false;camera.allowMSAA=false;
   target=new RenderTexture(frame.width,frame.height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);target.Create();camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);camera.aspect=frame.width/(float)frame.height;
   canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=100;MethodInfo handle=typeof(CanvasScaler).GetMethod("Handle",BindingFlags.Instance|BindingFlags.NonPublic);
   for(int pass=0;pass<4;pass++){Canvas.ForceUpdateCanvases();handle.Invoke(scaler,null);foreach(Text text in canvas.GetComponentsInChildren<Text>(true))text.SetAllDirty();foreach(Transform t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=Layer;Canvas.ForceUpdateCanvases();if(GraphicsSettings.currentRenderPipeline!=null)RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});else camera.Render();}
   float wr=frame.width/scaler.referenceResolution.x,hr=frame.height/scaler.referenceResolution.y;float expected=scaler.screenMatchMode==CanvasScaler.ScreenMatchMode.Expand?Mathf.Min(wr,hr):scaler.screenMatchMode==CanvasScaler.ScreenMatchMode.Shrink?Mathf.Max(wr,hr):Mathf.Pow(2,Mathf.Lerp(Mathf.Log(wr,2),Mathf.Log(hr,2),scaler.matchWidthOrHeight));
   frame.verifiedScaler=scaler.enabled&&Mathf.Abs(canvas.renderingDisplaySize.x-frame.width)<.5f&&Mathf.Abs(canvas.renderingDisplaySize.y-frame.height)<.5f&&Mathf.Abs(canvas.scaleFactor-expected)<.001f;if(!frame.verifiedScaler)throw new InvalidOperationException("Production scaler mismatch.");
   VerifyRemovedText(host);
   if(frame.variant.StartsWith("Naval",StringComparison.Ordinal))VerifyChoiceLayout(host);
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
  if(variant=="DeckBuilder")return true;
  return ArrangePacks(host,variant)||ArrangeUpgrade(host,variant)||ArrangeMarket(host,variant)||ArrangeResale(host,variant)||ArrangeShipyard(host,variant)||ArrangeNaval(host,variant);
 }
 static void VerifyRemovedText(GameObject host){
  string[] removed={"Deck Builder Hint","Deck Builder Capacity Rule","Deck Builder Capacity Minimum","Deck Builder Capacity State","Workshop Packs Owned Count","Workshop Packs Library Footer","Workshop Packs Results Pack Name","Workshop Packs Results Hint","Workshop Packs Table Caption","Workshop Upgrade Library Hint","Upgrade Availability","Workshop Upgrade Library Heading","Card Upgrade Heading","Upgrade Gold Balance","Workshop Upgrade Header Divider","Component Resale Empty Detail","Shipyard Equipment Empty Inventory","Naval choice instruction","Naval choice validation"};
  foreach(Transform node in host.GetComponentsInChildren<Transform>(true))
   if(removed.Contains(node.name)||node.name.StartsWith("Upgrade List Status ",StringComparison.Ordinal)||node.name.StartsWith("Workshop Pack Pool Label ",StringComparison.Ordinal))throw new InvalidOperationException("Removed explanatory node remains: "+node.name);
  foreach(Text text in host.GetComponentsInChildren<Text>(true))
   if(text.name=="Market Purchase Choice Quote"&&(text.text.Contains("选好位置后付款")||text.text.Contains("取消不扣款")||text.text.Contains("\n")||!text.text.Contains("总价")||text.preferredWidth>text.rectTransform.rect.width+.5f))throw new InvalidOperationException("Market quote does not fit or contains removed explanation.");
 }
 static bool ArrangePacks(GameObject host,string variant){
  if(variant!="PackResults"&&variant!="PackSelection")return false;
  var c=host.GetComponent<CoreGameplayDemoController>();
  Field(c,"workshopPacksOpen",true);Field(c,"workshopPackViewSession",c.Session);Field(c,"workshopPackViewPort",c.Session.CurrentPort);if(variant=="PackResults"){Field(c,"workshopOpenedPackId","neutral");Field(c,"workshopPackResultCards",new[]{0,1,2});}
  Field(c,"editorPreviewBuilding",true);try{Call(c,"ClearPhaseContentForRender");Call(c,"BuildPort");}finally{Field(c,"editorPreviewBuilding",false);}
  string[] removed={"Workshop Packs Owned Count","Workshop Packs Library Footer","Workshop Packs Results Pack Name","Workshop Packs Results Hint"};
  if(host.GetComponentsInChildren<Transform>(true).Any(t=>removed.Contains(t.name)||t.name.StartsWith("Workshop Pack Pool Label ",StringComparison.Ordinal)))throw new InvalidOperationException("Removed pack text still exists.");
  foreach(Text name in host.GetComponentsInChildren<Text>().Where(t=>t.name.StartsWith("Workshop Pack Name ",StringComparison.Ordinal)))if(Mathf.Abs(name.rectTransform.anchoredPosition.y)>.01f)throw new InvalidOperationException("Pack names must remain vertically centered.");
  return true;
 }

 static bool ArrangeMarket(GameObject host,string variant){
  if(variant!="MarketChoice")return false;
  var c=host.GetComponent<CoreGameplayDemoController>();
  Field(c,"editorPreviewBuilding",true);try{
   Field(c,"marketSelectedShop",(MarketShopKind?)MarketShopKind.Supplies);Field(c,"marketSelectedItem",(PortMarketItem?)PortMarketItem.Water);
   Field(c,"marketTradeSelecting",true);Field(c,"marketTradeBuying",true);Field(c,"marketInventoryExpanded",false);
   Call(c,"RefreshMarketFacilityContents");DateTime now=c.Session.GameDateTime;
   int quantity=(int)Call(c,"MarketQuantity",PortMarketItem.Water),level=(int)Call(c,"PortInvestmentLevel",c.Session.CurrentPort);
   object quote=Call(c,"CreateMarketPurchaseQuote",PortMarketItem.Water,quantity,PortMarketCatalog.CalculateBuyCost(c.Session.CurrentPort,PortMarketItem.Water,now,quantity,c.Session.Cargo.Fame),now,level,0,(int)Call(c,"MarketRemaining",c.Session.CurrentPort,PortMarketItem.Water,now,level));
   Call(c,"BuildMarketPurchaseChoice",quote);
  }finally{Field(c,"editorPreviewBuilding",false);}
  return true;
 }

 static bool ArrangeResale(GameObject host,string variant){
  if(variant!="ComponentResaleEmpty")return false;
  var c=host.GetComponent<CoreGameplayDemoController>();
  Field(c,"editorPreviewBuilding",true);try{
   var inventory=new OwnedShipComponentInventory((ComponentArtCatalog)Get(c,"componentArt"));inventory.EnsureLoadout(0);
   Field(c,"componentInventory",inventory);Field(c,"componentResaleOpen",true);Field(c,"componentResalePage",0);
   Field(c,"pendingComponentSaleId",null);Field(c,"selectedComponentResaleInstanceId",null);
   Call(c,"BuildSelectedEditorPreview");
   if(host.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Component Resale Empty Detail"))throw new InvalidOperationException("Removed component detail still exists.");
  }finally{Field(c,"editorPreviewBuilding",false);}
  return true;
 }

 static bool ArrangeShipyard(GameObject host,string variant){
  if(variant!="ShipyardEmpty")return false;
  var c=host.GetComponent<CoreGameplayDemoController>();
  Field(c,"editorPreviewBuilding",true);try{
   var inventory=new OwnedShipComponentInventory((ComponentArtCatalog)Get(c,"componentArt"));inventory.EnsureLoadout(0);
   Field(c,"componentInventory",inventory);Field(c,"editorPreviewScreen",CoreGameplayPreviewScreen.ShipyardDetail);
   Field(c,"editorPreviewShipIndex",0);Field(c,"editorPreviewShipPage",2);Field(c,"selectedShipyardComponentInstanceId",null);
   Field(c,"shipyardInventoryShowAll",true);Field(c,"shipyardInventoryPage",0);
   Call(c,"BuildSelectedEditorPreview");
   if(host.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Shipyard Equipment Empty Inventory"))throw new InvalidOperationException("Removed shipyard empty description still exists.");
  }finally{Field(c,"editorPreviewBuilding",false);}
  return true;
 }

 static bool ArrangeUpgrade(GameObject host,string variant){
  if(variant!="Upgrade")return false;
  string[] removed={"Workshop Upgrade Library Hint","Upgrade Availability","Workshop Upgrade Library Heading","Card Upgrade Heading","Upgrade Gold Balance","Workshop Upgrade Header Divider"};
  if(host.GetComponentsInChildren<Transform>(true).Any(t=>removed.Contains(t.name)||t.name.StartsWith("Upgrade List Status ",StringComparison.Ordinal)))throw new InvalidOperationException("Removed upgrade text still exists.");
  foreach(Text name in host.GetComponentsInChildren<Text>().Where(t=>t.name.StartsWith("Upgrade List Name ",StringComparison.Ordinal)))if(Mathf.Abs(name.rectTransform.anchoredPosition.y)>.01f)throw new InvalidOperationException("Upgrade names must remain vertically centered.");
  return true;
 }
 static bool ArrangeNaval(GameObject host,string variant){
  if(variant!="NavalChoice"&&variant!="NavalDirection"&&variant!="NavalConfirm")return false;
  var c=host.GetComponent<CoreGameplayDemoController>();
  CoreBattleState battle=c.Session.Battle;
  if(battle==null||battle.Stage==NavalBattleStage.Boarding)throw new InvalidOperationException("Naval Battle preview required.");
  string id=variant=="NavalDirection"?"card_school_tide_n01":variant=="NavalConfirm"?"card_school_fire_n06":"card_school_fire_n01";
  int cardIndex=Enumerable.Range(0,CoreVoyageSession.CardLibrary.Count).First(i=>CoreVoyageSession.CardLibrary[i].Id==id);
  CoreCardPackProgress ownership=c.Session.CaptureCardPackProgress();var copies=ownership.cards.ToList();
  CoreCardCopySave existing=copies.FirstOrDefault(item=>item.id==id);
  if(existing==null)copies.Add(new CoreCardCopySave{id=id,count=1});else existing.count=Math.Max(existing.count,1);
  ownership.cards=copies.ToArray();if(!c.Session.RestoreCardPackProgress(ownership))throw new InvalidOperationException("Sample ownership rejected.");
  if(battle.Hand.Count==0)battle.Hand.Add(cardIndex);else battle.Hand[0]=cardIndex;
  battle.Energy=Math.Max(battle.Energy,3);
  Field(c,"editorPreviewBuilding",true);try{
   Call(c,"ClearPhaseContentForRender");Call(c,"BuildBattleOverlay");Call(c,"RemoveLegacyBattleScaffolding");
  }finally{Field(c,"editorPreviewBuilding",false);}
  Button source=host.GetComponentsInChildren<Button>().Single(b=>b.name=="Battle Card 0");
  if(!(bool)Call(c,"TryBeginNavalCardChoice",source,0,c.Session.GetBattleCard(cardIndex)))throw new InvalidOperationException("Choice did not open.");
  if(variant=="NavalDirection"){
   var overlay=host.GetComponentInChildren<NavalCardChoiceOverlay>();
   overlay.SelectCell(Mathf.RoundToInt(battle.PlayerPositionMeters.X/100f),Mathf.RoundToInt(battle.PlayerPositionMeters.Y/100f));
   if(!overlay.IsSelectingDirection)throw new InvalidOperationException("Direction controls did not open.");
  }
  return true;
 }
 static void VerifyChoiceLayout(GameObject host){
  if(host.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Naval choice instruction"||t.name=="Naval choice validation"))throw new InvalidOperationException("Removed instructions remain.");
  RectTransform panel=host.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Naval choice controls");
  if(panel.rect.width>=1060f||panel.rect.height>=238f)throw new InvalidOperationException("Panel was not reduced.");
  Button[] buttons=panel.GetComponentsInChildren<Button>().Where(b=>b.gameObject.activeInHierarchy).ToArray();
  var bounds=buttons.Select(b=>BoundsIn(panel,(RectTransform)b.transform)).ToArray();
  for(int i=0;i<buttons.Length;i++){
   Rect box=bounds[i];if(box.xMin<panel.rect.xMin-.5f||box.xMax>panel.rect.xMax+.5f||box.yMin<panel.rect.yMin-.5f||box.yMax>panel.rect.yMax+.5f)throw new InvalidOperationException("Button outside panel: "+buttons[i].name);
   for(int j=0;j<i;j++)if(box.Overlaps(bounds[j]))throw new InvalidOperationException("Buttons overlap: "+buttons[i].name+" / "+buttons[j].name);
   if(buttons[i].name=="Cancel Naval Card Choice"&&(Mathf.Abs(panel.rect.xMax-box.xMax-20f)>.5f||Mathf.Abs(box.yMin-panel.rect.yMin-16f)>.5f))throw new InvalidOperationException("Cancel is not at lower right.");
  }
 }
 static Rect BoundsIn(RectTransform parent,RectTransform child){var corners=new Vector3[4];child.GetWorldCorners(corners);Vector3[] points=corners.Select(parent.InverseTransformPoint).ToArray();return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));}
 static void Field(object target,string name,object value){var f=target.GetType().GetField(name,Private);if(f!=null){f.SetValue(target,value);return;}var p=target.GetType().GetProperty(name,Private);if(p==null)throw new MissingMemberException(target.GetType().Name,name);p.SetValue(target,value);}
 static object Get(object target,string name){var f=target.GetType().GetField(name,Private);if(f!=null)return f.GetValue(target);var p=target.GetType().GetProperty(name,Private);if(p==null)throw new MissingMemberException(target.GetType().Name,name);return p.GetValue(target);}
 static object Call(object target,string name,params object[] args){return target.GetType().GetMethods(Private).Single(m=>m.Name==name&&m.GetParameters().Length==args.Length).Invoke(target,args);}
 static void Rebuild(CoreGameplayDemoController c){Field(c,"editorPreviewBuilding",true);try{Call(c,"BuildSelectedEditorPreview");}finally{Field(c,"editorPreviewBuilding",false);}}
 static float EffectiveAlpha(Graphic graphic){float a=graphic.color.a;for(Transform t=graphic.transform;t!=null;t=t.parent){var g=t.GetComponent<CanvasGroup>();if(g==null)continue;a*=g.alpha;if(g.ignoreParentGroups)break;}return a;}
}
}
