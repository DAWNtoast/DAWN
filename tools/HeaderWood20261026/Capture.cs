// Installed only into a borrowed, isolated validation project by Validate.ps1.
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
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    public static class HeaderWoodCapture20261026
    {
        private const int Layer = 31;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Dictionary<string, string> PortTextures = new Dictionary<string, string>(StringComparer.Ordinal);
        [Serializable] public sealed class Report
        {
            public string capturedAtUtc = DateTime.UtcNow.ToString("O");
            public string scope = "Six ports and voyage origins, actual production UI in disposable Editor PreviewScenes; no scene/save/PlayerPrefs writes. CurrentPort mismatch is an in-memory fixture only. Shared CanvasScaler remains enabled.";
            public string project;
            public bool sixIndependentTextures;
            public List<Frame> frames = new List<Frame>();
        }
        [Serializable] public sealed class Frame
        {
            public string state, port, variant, file, currentPort, departurePort, battleStage, headerTexture, expectedTexture, error;
            public int width=1920, height=1080;
            public bool verifiedScaler, verifiedRegionalTexture, verifiedInputPassthrough, verifiedSettingsSize, verifiedMissingMapFallback, verifiedEmptyDepartureFallback;
            public float scaleFactor;
            public Vector2 displaySize;
            public Rect headerBounds;
        }
        private static Frame Item(CoreGameplayPreviewScreen state, CoreGameplayPreviewPort port, string variant="", int width=1920)
            => new Frame { state=state.ToString(), port=port.ToString(), variant=variant, width=width };
        public static void CaptureFromCommandLine()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stopped editor required.");
            if (SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null) throw new InvalidOperationException("Graphics required; omit -nographics.");
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,"..")).Replace('\\','/');
            if (!project.Equals("D:/Game/tmp/shipyard-assembly-20261017",StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Use the assigned borrowed validation project.");
            string[] args=Environment.GetCommandLineArgs(); int option=Array.IndexOf(args,"-headerWoodOutput");
            if(option<0 || option+1>=args.Length || !Path.IsPathRooted(args[option+1])) throw new ArgumentException("Pass -headerWoodOutput ABSOLUTE_DIRECTORY.");
            string output=Path.GetFullPath(args[option+1]);
            bool battleMode=Array.IndexOf(args,"-headerWoodBattle")>=0;
            string expectedOutputRoot=battleMode?"D:/Game/outputs/header-wood-20261026/battle-validation/":"D:/Game/outputs/header-wood-20261026/validation/";
            if(!output.Replace('\\','/').StartsWith(expectedOutputRoot,StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Output escaped task evidence directory.");
            Directory.CreateDirectory(output);
            if(Directory.EnumerateFiles(output,"*.png").Any() || File.Exists(Path.Combine(output,"report.json"))) throw new IOException("Preserve previous evidence; choose a new run.");
            PortTextures.Clear();
            var report=new Report { project=project };
            if(battleMode)
            {
                report.scope="Six departure ports in naval and boarding combat, actual production UI in disposable Editor PreviewScenes. CurrentPort mismatch plus missing/empty voyage origin fallback use in-memory fixtures only; no scene/save/PlayerPrefs writes.";
                string[] portNames={"断锚港","白贝港","铜钟港","盐冠港","暮潮港","灰鸥港"};
                foreach(CoreGameplayPreviewPort port in Enum.GetValues(typeof(CoreGameplayPreviewPort)))
                {
                    string portName=portNames[(int)port];
                    Texture2D texture=Resources.Load<Texture2D>(NauticalHeaderGraphic.ResourcePathForPort(portName));
                    if(texture==null)throw new InvalidOperationException("Missing regional wood: "+portName);
                    PortTextures.Add(portName,AssetDatabase.GetAssetPath(texture));
                    report.frames.Add(Item(CoreGameplayPreviewScreen.Battle,port,"Naval"));
                    report.frames.Add(Item(CoreGameplayPreviewScreen.Battle,port,"Boarding"));
                }
                report.frames.Add(Item(CoreGameplayPreviewScreen.Battle,CoreGameplayPreviewPort.WhiteShell,"NavalCurrentPortMismatch"));
                report.frames.Add(Item(CoreGameplayPreviewScreen.Battle,CoreGameplayPreviewPort.WhiteShell,"BoardingCurrentPortMismatch"));
            }
            else
            {
                foreach(CoreGameplayPreviewPort port in Enum.GetValues(typeof(CoreGameplayPreviewPort))) report.frames.Add(Item(CoreGameplayPreviewScreen.PortHub,port));
                report.frames.Add(Item(CoreGameplayPreviewScreen.Tavern,CoreGameplayPreviewPort.BrokenAnchor));
                report.frames.Add(Item(CoreGameplayPreviewScreen.Shipyard,CoreGameplayPreviewPort.BrokenAnchor));
                foreach(CoreGameplayPreviewPort port in Enum.GetValues(typeof(CoreGameplayPreviewPort))) report.frames.Add(Item(CoreGameplayPreviewScreen.Navigation,port));
                report.frames.Add(Item(CoreGameplayPreviewScreen.Navigation,CoreGameplayPreviewPort.WhiteShell,"CurrentPortMismatch"));
                report.frames.Add(Item(CoreGameplayPreviewScreen.PortHub,CoreGameplayPreviewPort.BrokenAnchor,"Wide",2560));
            }
            for(int i=0;i<report.frames.Count;i++)
            {
                Frame frame=report.frames[i]; frame.file=(i+1).ToString("D2")+"_"+frame.state+"_"+frame.port+(string.IsNullOrEmpty(frame.variant)?"":"_"+frame.variant)+".png";
                try { Capture(output,frame); Debug.Log("HEADER_WOOD_OK: "+frame.file+" texture="+frame.headerTexture); }
                catch(Exception e) { while(e is TargetInvocationException && e.InnerException!=null)e=e.InnerException; frame.error=e.ToString(); Debug.LogWarning("HEADER_WOOD_FAILED: "+frame.file+" "+e.Message); }
                report.sixIndependentTextures=PortTextures.Count==6 && PortTextures.Values.Distinct(StringComparer.Ordinal).Count()==6;
                File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
            }
            int failures=report.frames.Count(f=>!string.IsNullOrEmpty(f.error));
            Debug.Log("HEADER_WOOD_COMPLETE: "+(report.frames.Count-failures)+" / "+report.frames.Count+"; six unique="+report.sixIndependentTextures);
            if(failures>0 || !report.sixIndependentTextures) throw new InvalidOperationException("Header wood capture verification failed; inspect report.json.");
        }
        private static void VerifyFallbacks(CoreGameplayDemoController controller,Frame frame)
        {
            PropertyInfo origin=typeof(CoreGameplayDemoController).GetProperty("VoyageHeaderPort",Private);
            PropertyInfo mapProperty=typeof(CoreVoyageSession).GetProperty("Map");
            CoreVoyageMap map=controller.Session.Map;
            string departure=map.DepartureName;
            try
            {
                map.DepartureName=string.Empty;
                frame.verifiedEmptyDepartureFallback=(string)origin.GetValue(controller)==controller.Session.CurrentPort;
                if(!frame.verifiedEmptyDepartureFallback)throw new InvalidOperationException("Empty departure does not fall back to CurrentPort.");
                mapProperty.GetSetMethod(true).Invoke(controller.Session,new object[]{null});
                frame.verifiedMissingMapFallback=(string)origin.GetValue(controller)==controller.Session.CurrentPort;
                if(!frame.verifiedMissingMapFallback)throw new InvalidOperationException("Missing map does not fall back to CurrentPort.");
            }
            finally
            {
                map.DepartureName=departure;
                mapProperty.GetSetMethod(true).Invoke(controller.Session,new object[]{map});
            }
        }
        private static void Capture(string output,Frame frame)
        {
            Scene scene=default; RenderTexture target=null,previous=RenderTexture.active; Texture2D pixels=null;
            try
            {
                scene=EditorSceneManager.NewPreviewScene();
                var host=new GameObject("Disposable Header Wood Capture"); host.SetActive(false); SceneManager.MoveGameObjectToScene(host,scene);
                var controller=host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview((CoreGameplayPreviewScreen)Enum.Parse(typeof(CoreGameplayPreviewScreen),frame.state),(CoreGameplayPreviewPort)Enum.Parse(typeof(CoreGameplayPreviewPort),frame.port),0);
                host.SetActive(true);
                if(frame.state=="Battle")
                {
                    controller.Session.Battle.Stage=frame.variant.StartsWith("Boarding",StringComparison.Ordinal)?NavalBattleStage.Boarding:NavalBattleStage.Naval;
                    if(frame.variant.EndsWith("CurrentPortMismatch",StringComparison.Ordinal))
                    {
                        string departure=controller.Session.Map.DepartureName;
                        string other=departure=="断锚港"?"白贝港":"断锚港";
                        typeof(CoreVoyageSession).GetProperty("CurrentPort").GetSetMethod(true).Invoke(controller.Session,new object[]{other});
                        if(controller.Session.CurrentPort==departure)throw new InvalidOperationException("Battle mismatch fixture did not separate current and departure ports.");
                    }
                    VerifyFallbacks(controller,frame);
                    typeof(CoreGameplayDemoController).GetMethod("Render",Private).Invoke(controller,null);
                    frame.battleStage=controller.Session.Battle.Stage.ToString();
                }
                if(frame.variant=="CurrentPortMismatch")
                {
                    string departure=controller.Session.Map.DepartureName;
                    string other=departure=="断锚港"?"白贝港":"断锚港";
                    typeof(CoreVoyageSession).GetProperty("CurrentPort").GetSetMethod(true).Invoke(controller.Session,new object[]{other});
                    foreach(Transform old in host.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Voyage Header").ToArray()) Object.DestroyImmediate(old.gameObject);
                    typeof(CoreGameplayDemoController).GetMethod("BuildVoyageHeader",Private).Invoke(controller,new object[]{"航海探索"});
                    if(controller.Session.CurrentPort==controller.Session.Map.DepartureName) throw new InvalidOperationException("Mismatch fixture did not separate current and departure ports.");
                }
                Canvas canvas=host.GetComponentsInChildren<Canvas>(true).Single(c=>c.name==CoreGameplayDemoController.EditorPreviewCanvasName);
                CanvasScaler scaler=canvas.GetComponent<CanvasScaler>();
                if(!scaler.enabled)throw new InvalidOperationException("Production CanvasScaler disabled.");
                foreach(Transform t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=Layer;
                var cameraHost=new GameObject("Disposable Header Wood Camera",typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraHost,scene);
                Camera camera=cameraHost.GetComponent<Camera>(); camera.enabled=false; camera.scene=scene; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
                camera.orthographic=true; camera.orthographicSize=5; camera.nearClipPlane=.1f; camera.farClipPlane=2000; camera.transform.position=new Vector3(0,0,-1000); camera.cullingMask=1<<Layer; camera.allowHDR=false; camera.allowMSAA=false;
                target=new RenderTexture(frame.width,frame.height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB); target.Create(); camera.targetTexture=target; camera.rect=new Rect(0,0,1,1); camera.aspect=frame.width/(float)frame.height;
                canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=100;
                MethodInfo handle=typeof(CanvasScaler).GetMethod("Handle",Private);
                for(int pass=0;pass<4;pass++)
                {
                    Canvas.ForceUpdateCanvases(); handle.Invoke(scaler,null);
                    foreach(NavigationDashboardLayout layout in host.GetComponentsInChildren<NavigationDashboardLayout>(false))layout.SendMessage("LateUpdate",SendMessageOptions.DontRequireReceiver);
                    foreach(Text text in canvas.GetComponentsInChildren<Text>(true))text.SetAllDirty();
                    foreach(Transform t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=Layer;
                    Canvas.ForceUpdateCanvases();
                    if(GraphicsSettings.currentRenderPipeline!=null)RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target}); else camera.Render();
                }
                frame.displaySize=canvas.renderingDisplaySize; frame.scaleFactor=canvas.scaleFactor;
                float wr=frame.width/scaler.referenceResolution.x,hr=frame.height/scaler.referenceResolution.y;
                float expected=scaler.screenMatchMode==CanvasScaler.ScreenMatchMode.Expand?Mathf.Min(wr,hr):scaler.screenMatchMode==CanvasScaler.ScreenMatchMode.Shrink?Mathf.Max(wr,hr):Mathf.Pow(2,Mathf.Lerp(Mathf.Log(wr,2),Mathf.Log(hr,2),scaler.matchWidthOrHeight));
                frame.verifiedScaler=Mathf.Abs(frame.displaySize.x-frame.width)<.5f&&Mathf.Abs(frame.displaySize.y-frame.height)<.5f&&Mathf.Abs(canvas.pixelRect.width-frame.width)<.5f&&Mathf.Abs(canvas.pixelRect.height-frame.height)<.5f&&Mathf.Abs(frame.scaleFactor-expected)<.001f;
                if(!frame.verifiedScaler)throw new InvalidOperationException("Render target did not drive the production CanvasScaler.");
                NauticalHeaderGraphic[] surfaces=canvas.GetComponentsInChildren<NauticalHeaderGraphic>(false).Where(g=>g.enabled&&g.name=="Header Brass Rules").ToArray();
                if(surfaces.Length!=1)throw new InvalidOperationException("Expected one visible shared header surface, found "+surfaces.Length);
                NauticalHeaderGraphic surface=surfaces[0];
                frame.currentPort=controller.Session.CurrentPort; frame.departurePort=controller.Session.Map==null?null:controller.Session.Map.DepartureName;
                frame.headerTexture=AssetDatabase.GetAssetPath(surface.mainTexture);
                if(string.IsNullOrEmpty(frame.headerTexture))throw new InvalidOperationException("Header has no imported wood texture.");
                if(frame.state=="PortHub" && frame.variant!="Wide")PortTextures.Add(frame.currentPort,frame.headerTexture);
                string expectedPort=frame.state=="Navigation"||frame.state=="Battle"?frame.departurePort:frame.currentPort;
                if(!PortTextures.TryGetValue(expectedPort,out frame.expectedTexture))throw new InvalidOperationException("No hub texture recorded for "+expectedPort);
                frame.verifiedRegionalTexture=frame.expectedTexture==frame.headerTexture;
                if(!frame.verifiedRegionalTexture)throw new InvalidOperationException("Header texture did not match port/origin: "+expectedPort);
                frame.verifiedInputPassthrough=!surface.raycastTarget;
                if(!frame.verifiedInputPassthrough)throw new InvalidOperationException("Header backing intercepts input.");
                var corners=new Vector3[4]; surface.rectTransform.GetWorldCorners(corners); Vector2 lower=RectTransformUtility.WorldToScreenPoint(camera,corners[0]),upper=RectTransformUtility.WorldToScreenPoint(camera,corners[2]); frame.headerBounds=new Rect(lower,upper-lower);
                Button settings=canvas.GetComponentsInChildren<Button>(false).Single(b=>b.name.EndsWith("Settings",StringComparison.Ordinal));
                RectTransform buttonRect=(RectTransform)settings.transform;
                frame.verifiedSettingsSize=Mathf.Abs(buttonRect.rect.width-NauticalHeaderSkin.IconSize)<.01f&&Mathf.Abs(buttonRect.rect.height-NauticalHeaderSkin.IconSize)<.01f&&Mathf.Abs(buttonRect.anchoredPosition.x+NauticalHeaderSkin.SettingsRightOffset)<.01f&&Mathf.Abs(buttonRect.anchoredPosition.y+NauticalHeaderSkin.SettingsTopOffset)<.01f;
                if(!frame.verifiedSettingsSize)throw new InvalidOperationException("Settings control no longer uses shared 80x80 coordinates.");
                RenderTexture.active=target; pixels=new Texture2D(frame.width,frame.height,TextureFormat.RGB24,false,false); pixels.ReadPixels(new Rect(0,0,frame.width,frame.height),0,0); pixels.Apply(false,false);
                Color32[] samples=pixels.GetPixels32(); int low=255,high=0; for(int i=0;i<samples.Length;i+=997){int b=(samples[i].r+samples[i].g+samples[i].b)/3;low=Math.Min(low,b);high=Math.Max(high,b);} if(high-low<20)throw new InvalidOperationException("Nearly blank frame.");
                File.WriteAllBytes(Path.Combine(output,frame.file),pixels.EncodeToPNG());
            }
            finally { RenderTexture.active=previous; if(pixels!=null)Object.DestroyImmediate(pixels); if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene); if(target!=null){target.Release();Object.DestroyImmediate(target);} }
        }
    }
}
