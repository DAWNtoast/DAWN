// Install in an isolated validation project's Editor folder only.
// Renders current production UI in disposable PreviewScenes; no scenes/assets/saves are written.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    public static class WoodFrameCapture
    {
        private const int Layer = 31;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] public sealed class Report
        {
            public string capturedAtUtc = DateTime.UtcNow.ToString("O");
            public string scope = "Current production UI in disposable Editor PreviewScenes at 1920x1080. In-memory sample state only; no scenes, assets, player saves, transactions or PlayerPrefs writes. Wood bounds identify enabled graphic candidates intersecting the frame, not an occlusion proof. MainMenu settings and modal visibility use production presentation methods. Retired ForMe scenes are never read or written.";
            public string project;
            public List<Frame> frames = new List<Frame>();
            public string[] excluded = { "Legacy maintenance supply board: BuildMaintenanceFacility has no current caller; Maintenance now opens port investment. Current supplies are captured through the departure modal." };
        }
        [Serializable] public sealed class Frame
        {
            public int index, width = 1920, height = 1080;
            public string title, state, variant, file, error;
            public bool verifiedScaler;
            public float scaleFactor;
            public Vector2 displaySize;
            public List<WoodElement> wood = new List<WoodElement>();
            public List<string> visibleText = new List<string>();
        }
        [Serializable] public sealed class WoodElement
        {
            public string path, type, label, texture, sprite;
            public Rect screenBounds;
            public bool interactable;
            public float effectiveAlpha;
        }
        private static Frame Item(string title, string state, string variant = "") => new Frame { title = title, state = state, variant = variant };
        public static void CaptureFromCommandLine()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stopped editor required.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("Graphics required; omit -nographics.");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath,"..")).Replace('\\','/');
            if (!project.StartsWith("D:/Game/tmp/",StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Use a scratch project below D:/Game/tmp.");
            string[] args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args,"-woodFrameOutput");
            if (option < 0 || option + 1 >= args.Length || !Path.IsPathRooted(args[option+1])) throw new ArgumentException("Pass -woodFrameOutput ABSOLUTE_DIRECTORY.");
            string output = Path.GetFullPath(args[option+1]);
            if (!output.Replace('\\','/').StartsWith("D:/Game/outputs/wood-frame-screenshots-20261025/",StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Use the task's outputs/wood-frame-screenshots-20261025 subdirectory.");
            Directory.CreateDirectory(output);
            if (Directory.EnumerateFiles(output,"*.png").Any() || File.Exists(Path.Combine(output,"report.json"))) throw new IOException("Preserve captures; choose a new run directory.");
            var report = new Report { project = project };
            report.frames.AddRange(new[] {
                Item("造船厂：查看船只档案与切换船只","Shipyard"),
                Item("船只档案：性能与购买","ShipyardDetail"),
                Item("船只档案：背景标签","ShipyardDetail","ShipBackground"),
                Item("船只档案：部件装配","ShipyardDetail","ShipEquipment"),
                Item("部件商店：分类与商品翻页","ComponentShop"),
                Item("部件档案：标签与购买","ComponentShopDetail"),
                Item("部件商店：出售闲置部件","ComponentShop","ComponentResale"),
                Item("部件商店：确认出售","ComponentShop","ComponentSaleConfirm"),
                Item("酒馆：招募入口与导航","Tavern"),
                Item("船员档案：标签与招募","TavernCrewDetail"),
                Item("船员档案：职业技能","TavernCrewDetail","CrewSkills"),
                Item("卡牌工坊：阶段与功能入口","CardWorkshop"),
                Item("卡组编辑：阶段与翻页","DeckBuilder"),
                Item("卡牌强化：确认强化","CardUpgrade"),
                Item("特殊卡牌：来源操作","SpecialCardCollection","SpecialSourceActions"),
                Item("港口投资：升级与市场仓储入口","Maintenance"),
                Item("市场：商店总览","Market"),
                Item("市场：购买模式与确认交易","Market","MarketBuy"),
                Item("市场：出售模式与确认交易","Market","MarketSell"),
                Item("市场：购买存放去向","Market","MarketChoice"),
                Item("仓库：藏品陈列","Warehouse"),
                Item("仓库：基础货物标签","Warehouse","Storage"),
                Item("仓库：珍稀物品标签","Warehouse","Rare"),
                Item("仓库：确认调整货物","Warehouse","Adjustment"),
                Item("仓库：确认出售货物","Warehouse","Sale"),
                Item("出航码头：切换船只和船员加减","VoyageDeparture"),
                Item("出航：购买基础补给","VoyageDeparture","BasicSupplies"),
                Item("出航：确认船员与头像木框","VoyageDeparture","Crew"),
                Item("出航：海图种子警告","VoyageDeparture","ChartWarning"),
                Item("出航：藏宝海图选择","VoyageDeparture","TreasureSeed"),
                Item("航海：执行航行与返航","Navigation"),
                Item("航海：预报详情","Navigation","Forecast"),
                Item("航海船舱：分类与确认操作","Navigation","Cargo"),
                Item("海战：名称底牌与行动按钮","Battle"),
                Item("接舷战：名称底牌与回合按钮","Battle","Boarding"),
                Item("战斗：卡组查看标签","Battle","BattleDeck"),
                Item("海战：确定航行与清空路线","Battle","NavalMovement"),
                Item("藏图册：未使用藏宝图","TreasureMap","Folio"),
                Item("藏图册：已使用藏宝图","TreasureMap","History"),
                Item("结算：返回港口","Settlement"),
                Item("结算开箱：外围木框与跳过","Settlement","SettlementChest"),
                Item("游戏内设置：应用设置","PortHub","PortSettings"),
                Item("主菜单设置：应用设置","Settings"),
                Item("成就：筛选标签与返回","Achievements"),
                Item("主菜单：确认新游戏","NewGameConfirm"),
                Item("主菜单：航程存档提示","SaveNotice"),
                Item("游戏内：保存并返回主菜单确认","PortHub","ReturnMenu"),
                Item("通知：新体系解锁","PortHub","SchoolUnlock"),
                Item("调试：GM面板","PortHub","GameMaster"),
                Item("通知：存档恢复提示","PortHub","SaveRecovery")
            });
            for(int i=0;i<report.frames.Count;i++) { var f=report.frames[i]; f.index=i+1; f.file=f.index.ToString("D2")+"_"+f.state+(string.IsNullOrEmpty(f.variant)?"":"_"+f.variant)+".png"; }
            int onlyIndex=Array.IndexOf(args,"-woodFrameOnly");
            if(onlyIndex>=0 && onlyIndex+1<args.Length) { var only=new HashSet<string>(args[onlyIndex+1].Split(','),StringComparer.Ordinal); report.frames=report.frames.Where(f=>only.Contains(f.index.ToString())||only.Contains(f.state)||only.Contains(f.variant)).ToList(); }
            foreach(Frame frame in report.frames)
            {
                try { Capture(output,frame); Debug.Log("WOOD_CAPTURE_OK: "+frame.file+" wood="+frame.wood.Count); }
                catch(Exception e) { while(e is TargetInvocationException && e.InnerException!=null)e=e.InnerException; frame.error=e.ToString(); Debug.LogWarning("WOOD_CAPTURE_FAILED: "+frame.file+" "+e.Message); }
                File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true),new UTF8Encoding(false));
                WriteGallery(output,report);
            }
            Debug.Log("WOOD_CAPTURE_COMPLETE: "+report.frames.Count(f=>string.IsNullOrEmpty(f.error))+" / "+report.frames.Count+"; "+output);
        }
        private static void Capture(string output, Frame frame)
        {
            Scene scene=default; RenderTexture target=null,previous=RenderTexture.active; Texture2D pixels=null;
            try
            {
                scene=EditorSceneManager.NewPreviewScene();
                var host=new GameObject("Disposable Wood Frame UI Capture"); host.SetActive(false); SceneManager.MoveGameObjectToScene(host,scene);
                bool gameplay=Enum.TryParse(frame.state,out CoreGameplayPreviewScreen screen);
                if(gameplay)host.AddComponent<CoreGameplayDemoController>().ConfigureEditorPreview(screen,CoreGameplayPreviewPort.BrokenAnchor,0); else host.AddComponent<MainMenuController>();
                host.SetActive(true);
                if(!gameplay && frame.state=="Achievements") host.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Achievements").onClick.Invoke();
                Arrange(host,frame);
                string canvasName=gameplay?CoreGameplayDemoController.EditorPreviewCanvasName:MainMenuController.EditorPreviewCanvasName;
                if(frame.variant=="SaveRecovery")canvasName="Profile Save Failure Canvas";
                Canvas canvas=host.GetComponentsInChildren<Canvas>(true).Single(c=>c.name==canvasName);
                if(frame.state=="SaveNotice")
                {
                    string savedName=canvas.name;
                    try { canvas.name="Main Menu Canvas"; typeof(MainMenuController).GetMethod("ShowSaveNotice",Private).Invoke(host.GetComponent<MainMenuController>(),new object[]{"航程记录暂不可用，请返回菜单重新读取。\n截图使用独立样本，不读取或改写玩家存档。"}); }
                    finally {canvas.name=savedName;}
                }
                CanvasScaler scaler=canvas.GetComponent<CanvasScaler>();
                if(!scaler.enabled)throw new InvalidOperationException("Production CanvasScaler disabled.");
                foreach(Transform t in canvas.GetComponentsInChildren<Transform>(true))
                {
                    if(t.name=="Settings Panel")t.gameObject.SetActive(frame.state=="Settings");
                    if(t.name=="Achievements Panel")t.gameObject.SetActive(frame.state=="Achievements");
                    if(t.name=="New Game Confirmation")t.gameObject.SetActive(frame.state=="NewGameConfirm");
                    t.gameObject.layer=Layer;
                }
                var cameraHost=new GameObject("Disposable Wood Frame Camera",typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraHost,scene);
                Camera camera=cameraHost.GetComponent<Camera>(); camera.enabled=false; camera.scene=scene; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
                camera.orthographic=true; camera.orthographicSize=5; camera.nearClipPlane=.1f; camera.farClipPlane=2000; camera.transform.position=new Vector3(0,0,-1000); camera.cullingMask=1<<Layer; camera.allowHDR=false; camera.allowMSAA=false;
                target=new RenderTexture(frame.width,frame.height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB); target.Create(); camera.targetTexture=target; camera.rect=new Rect(0,0,1,1); camera.aspect=frame.width/(float)frame.height;
                canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=100;
                MethodInfo handle=typeof(CanvasScaler).GetMethod("Handle",Private);
                for(int pass=0;pass<4;pass++)
                {
                    Canvas.ForceUpdateCanvases(); handle.Invoke(scaler,null); BeforeRender(host,frame,camera,pass);
                    foreach(Text text in canvas.GetComponentsInChildren<Text>(true))text.SetAllDirty();
                    foreach(Transform t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=Layer;
                    Canvas.ForceUpdateCanvases();
                    if(GraphicsSettings.currentRenderPipeline!=null)RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target}); else camera.Render();
                }
                frame.displaySize=canvas.renderingDisplaySize; frame.scaleFactor=canvas.scaleFactor;
                float wr=frame.width/scaler.referenceResolution.x,hr=frame.height/scaler.referenceResolution.y;
                float expected=scaler.screenMatchMode==CanvasScaler.ScreenMatchMode.Expand?Mathf.Min(wr,hr):scaler.screenMatchMode==CanvasScaler.ScreenMatchMode.Shrink?Mathf.Max(wr,hr):Mathf.Pow(2,Mathf.Lerp(Mathf.Log(wr,2),Mathf.Log(hr,2),scaler.matchWidthOrHeight));
                frame.verifiedScaler=Mathf.Abs(frame.displaySize.x-frame.width)<.5f&&Mathf.Abs(frame.displaySize.y-frame.height)<.5f&&Mathf.Abs(canvas.pixelRect.width-frame.width)<.5f&&Mathf.Abs(canvas.pixelRect.height-frame.height)<.5f&&Mathf.Abs(frame.scaleFactor-expected)<.001f;
                if(!frame.verifiedScaler)throw new InvalidOperationException("Requested render target did not drive production CanvasScaler.");
                RecordWood(canvas,camera,frame);
                frame.visibleText=canvas.GetComponentsInChildren<Text>(false).Where(t=>t.enabled&&!string.IsNullOrEmpty(t.text)&&EffectiveAlpha(t)>.01f).Select(t=>t.name+": "+t.text).ToList();
                RenderTexture.active=target; pixels=new Texture2D(frame.width,frame.height,TextureFormat.RGB24,false,false); pixels.ReadPixels(new Rect(0,0,frame.width,frame.height),0,0); pixels.Apply(false,false);
                Color32[] samples=pixels.GetPixels32(); int low=255,high=0; for(int i=0;i<samples.Length;i+=997){int b=(samples[i].r+samples[i].g+samples[i].b)/3;low=Math.Min(low,b);high=Math.Max(high,b);} if(high-low<20)throw new InvalidOperationException("Nearly blank rendered frame.");
                File.WriteAllBytes(Path.Combine(output,frame.file),pixels.EncodeToPNG());
            }
            finally { RenderTexture.active=previous; if(pixels!=null)Object.DestroyImmediate(pixels); if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene); if(target!=null){target.Release();Object.DestroyImmediate(target);} }
        }
        private static void Arrange(GameObject host,Frame frame)
        {
            if(string.IsNullOrEmpty(frame.variant))return;
            if(WoodFrameExtraVariants.TryArrange(host,frame.variant))return;
            var c=host.GetComponent<CoreGameplayDemoController>();
            switch(frame.variant)
            {
                case "SpecialSourceActions":
                    c.Session.ConfigureSpecialCardSources(Array.Empty<string>(),Array.Empty<string>());
                    RenderFixture(c);return;
                case "Folio": case "History":
                    c.ConfigureEditorTreasureState(frame.variant=="History"?TreasureMapPreviewState.RewardClaim:TreasureMapPreviewState.MapDecoded);return;
                case "MarketBuy":case "MarketSell":case "MarketChoice":
                    SetField(c,"marketSelectedShop",(MarketShopKind?)MarketShopKind.Supplies);break;
                case "ShipBackground":c.ConfigureEditorShipyard(0,true,1);return;
                case "ShipEquipment":c.ConfigureEditorShipyard(0,true,2);return;
                case "CrewSkills":c.ConfigureEditorCrewDossier(0,2);return;
                case "Forecast":host.GetComponentsInChildren<Button>(false).Single(b=>b.name=="Forecast Details").onClick.Invoke();return;
                case "Boarding":c.Session.Battle.Stage=NavalBattleStage.Boarding;Invoke(c,"Render");return;
                case "NavalMovement":c.Session.Battle.Stage=NavalBattleStage.Naval;typeof(CoreBattleState).GetProperty("NavalActionPoints").GetSetMethod(true).Invoke(c.Session.Battle,new object[]{3});Invoke(c,"Render");return;
            }
            var legacy=new UiAuditReadOnlyCapture.MenuFrame{state=frame.state,variant=frame.variant,width=frame.width,height=frame.height};
            typeof(UiAuditReadOnlyCapture).GetMethod("ArrangeParchmentVariant",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{host,legacy});
        }
        private static void BeforeRender(GameObject host,Frame frame,Camera camera,int pass)
        {
            WoodFrameExtraVariants.BeforeRender(host,frame.variant,camera,pass);
            foreach(NavigationDashboardLayout layout in host.GetComponentsInChildren<NavigationDashboardLayout>(false))layout.SendMessage("LateUpdate",SendMessageOptions.DontRequireReceiver);
            if(frame.variant!="NavalMovement"||pass!=2)return;
            var eventHost=new GameObject("Disposable Capture Events",typeof(EventSystem));SceneManager.MoveGameObjectToScene(eventHost,host.scene);
            Transform ship=host.GetComponentsInChildren<Transform>(false).Single(t=>t.name=="Player Ship Art");
            var pointer=new PointerEventData(eventHost.GetComponent<EventSystem>()){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(camera,ship.position)};
            if(!ExecuteEvents.Execute(ship.gameObject,pointer,ExecuteEvents.pointerClickHandler))throw new InvalidOperationException("Ship did not accept movement selection.");
            var movement=host.GetComponentInChildren<NavalTacticalMovementInput>();movement.RefreshNow();if(!movement.IsShipSelected)throw new InvalidOperationException("Movement controls not activated.");
        }
        private static void SetField(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
        private static void RenderFixture(CoreGameplayDemoController controller)
        {
            // Keep editor-only sample guards active while rebuilding the current presentation.
            SetField(controller,"editorPreviewBuilding",true);
            try {Invoke(controller,"BuildSelectedEditorPreview");}
            finally {SetField(controller,"editorPreviewBuilding",false);}
        }
        private static void Invoke(object target,string method)=>target.GetType().GetMethod(method,Private,null,Type.EmptyTypes,null).Invoke(target,null);
        private static float EffectiveAlpha(Graphic graphic)
        {
            float a=graphic.color.a;for(Transform t=graphic.transform;t!=null;t=t.parent){var group=t.GetComponent<CanvasGroup>();if(group==null)continue;a*=group.alpha;if(group.ignoreParentGroups)break;}return a;
        }
        private static void RecordWood(Canvas canvas,Camera camera,Frame frame)
        {
            Texture texture=WoodButtonSkin.Face==null?null:WoodButtonSkin.Face.texture;if(texture==null)return;
            foreach(Graphic g in canvas.GetComponentsInChildren<Graphic>(false))
            {
                if(!g.enabled||g.mainTexture!=texture||g.canvasRenderer.cull||EffectiveAlpha(g)<=.01f)continue;
                var corners=new Vector3[4];g.rectTransform.GetWorldCorners(corners);Vector2[] p=corners.Select(v=>(Vector2)camera.WorldToScreenPoint(v)).ToArray();
                Rect r=Rect.MinMaxRect(p.Min(v=>v.x),p.Min(v=>v.y),p.Max(v=>v.x),p.Max(v=>v.y));
                if(r.width<=0||r.height<=0||!r.Overlaps(new Rect(0,0,frame.width,frame.height)))continue;
                Button button=g.GetComponent<Button>()??g.GetComponentInParent<Button>();Image image=g as Image;
                frame.wood.Add(new WoodElement{path=PathOf(g.transform,canvas.transform),type=g.GetType().Name,label=button==null?string.Join(" | ",g.GetComponentsInChildren<Text>(false).Select(t=>t.text)):string.Join(" | ",button.GetComponentsInChildren<Text>(false).Select(t=>t.text)),texture=texture.name,sprite=image?.sprite?.name,screenBounds=r,interactable=button!=null&&button.IsInteractable(),effectiveAlpha=EffectiveAlpha(g)});
            }
        }
        private static string PathOf(Transform child,Transform root){var parts=new List<string>();for(Transform t=child;t!=null&&t!=root;t=t.parent)parts.Add(t.name);parts.Reverse();return string.Join("/",parts);}
        private static string H(string value)=>(value??"").Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;").Replace("\"","&quot;");
        private static void WriteGallery(string output,Report report)
        {
            var s=new StringBuilder("<!doctype html><html lang=zh-CN><meta charset=utf-8><meta name=viewport content='width=device-width,initial-scale=1'><title>木板底框界面截图</title><style>body{font:16px system-ui;background:#131b22;color:#eee;margin:24px auto;max-width:1600px;padding:0 20px}a{color:#dfbd7f}nav{columns:3;line-height:1.8}section{margin:38px 0}img{display:block;width:100%;height:auto;border:1px solid #78613d}small{color:#abb6bd}pre{white-space:pre-wrap;color:#efa59b}</style><h1>木板底框界面截图</h1><p>1920×1080，当前项目代码独立预览。点击图片查看原图。截图按界面依次编号；弹窗及游戏状态使用内存样本，不修改玩家进度。</p><nav>");
            foreach(Frame f in report.frames)s.Append("<div><a href='#f").Append(f.index).Append("'>").Append(f.index.ToString("D2")).Append(" ").Append(H(f.title)).Append("</a></div>");s.Append("</nav>");
            foreach(Frame f in report.frames){s.Append("<section id='f").Append(f.index).Append("'><h2>").Append(f.index.ToString("D2")).Append(" · ").Append(H(f.title)).Append("</h2>");if(string.IsNullOrEmpty(f.error)&&File.Exists(Path.Combine(output,f.file)))s.Append("<a target=_blank href='").Append(H(f.file)).Append("'><img loading=lazy src='").Append(H(f.file)).Append("' alt='").Append(H(f.title)).Append("'></a><small>木板素材图形候选 ").Append(f.wood.Count).Append(" 个；动态遮挡以画面为准。</small>");else s.Append("<pre>").Append(H(f.error??"等待截图")).Append("</pre>");s.Append("</section>");}
            s.Append("<p>旧维修补给板已无现役入口；当前补给操作参见出航基础补给截图。</p></html>");File.WriteAllText(Path.Combine(output,"index.html"),s.ToString(),new UTF8Encoding(false));
        }
    }
}