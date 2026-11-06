// Leased validation project only. Renders production UI/mesh motion without saves or scene writes.
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

namespace ShatteredSail.Editor
{
    public static class BoardingActorsCapture20261026
    {
        const int Layer=31;
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        [Serializable] public sealed class Frame { public string file,sequence,view;public float seconds,duration;public int width,height;public string[] actions;public Vector2[] feetOffsets,headOffsets; }
        [Serializable] public sealed class Report
        {
            public string scope="Production boarding UI and card-only actor actions sampled in disposable Editor PreviewScenes. Idle is exactly still. Attack, Cast, Guard, Heal and Prepare return to the original pose; full UI and actual camera closeups. Real card selection and Core behavior are covered separately by PlayMode tests.";
            public int framesPerActionSequence;public List<Frame> frames=new List<Frame>();
        }
        sealed class View
        {
            public string name;public int width,height;public float halfHeight;public Vector3 position;
            public Camera camera;public RenderTexture target;public Texture2D pixels;
        }
        public static void CaptureFromCommandLine()
        {
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,"..")).Replace('\\','/');
            if(project!="D:/Game/tmp/shipyard-assembly-20261017")throw new InvalidOperationException("Use leased isolated project.");
            if(Application.isPlaying||SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("Stopped editor with graphics required.");
            string[] args=Environment.GetCommandLineArgs();
            string output=Path.GetFullPath(Option(args,"-boardingActorsOutput"));
            string evidence=Option(args,"-boardingActorsEvidenceSet","card-actions-final");
            if(!System.Text.RegularExpressions.Regex.IsMatch(evidence,"^[a-zA-Z0-9_-]+$")||!output.Replace('\\','/').StartsWith("D:/Game/outputs/boarding-actors-20261026/"+evidence+"/",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Output escaped task evidence.");
            int frameCount=int.Parse(Option(args,"-boardingActorsFrames","24"));
            if(frameCount<24||frameCount>120)throw new ArgumentException("Use 24 to 120 frames.");
            Directory.CreateDirectory(output);
            if(Directory.EnumerateFiles(output,"*.jpg").Any()||File.Exists(Path.Combine(output,"report.json")))throw new IOException("Preserve earlier capture evidence.");
            var report=new Report{framesPerActionSequence=frameCount};
            CaptureSequence(output,report,"idle",1f,2);
            foreach(string sequence in new[]{"attack_hit","cast_hit","guard","heal","prepare"})
                CaptureSequence(output,report,sequence,1.3f,frameCount);
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
            Debug.Log("Boarding actor capture complete: "+report.frames.Count+" frames.");
        }
        static string Option(string[] args,string key,string fallback=null)
        {
            int i=Array.IndexOf(args,key);if(i>=0&&i+1<args.Length)return args[i+1];
            if(fallback!=null)return fallback;throw new ArgumentException("Missing "+key);
        }
        static void CaptureSequence(string output,Report report,string sequence,float duration,int count)
        {
            Scene scene=default;RenderTexture prior=RenderTexture.active;var views=new List<View>();
            try
            {
                scene=EditorSceneManager.NewPreviewScene();
                var host=new GameObject("Disposable Boarding Actors Capture");host.SetActive(false);SceneManager.MoveGameObjectToScene(host,scene);
                var controller=host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle,(CoreGameplayPreviewPort)0,0);host.SetActive(true);
                controller.Session.Battle.Stage=NavalBattleStage.Boarding;controller.Session.Battle.Hand.Clear();
                foreach(string id in new[]{"card_boarding_fire_01","card_boarding_tide_01","card_boarding_blood_09","card_crew_thrust","card_crew_guard"})
                    controller.Session.Battle.Hand.Add(Enumerable.Range(0,CoreVoyageSession.CardLibrary.Count).First(i=>CoreVoyageSession.CardLibrary[i].Id==id));
                controller.Session.Battle.Energy=controller.Session.Battle.MaxEnergy=12;
                typeof(CoreGameplayDemoController).GetMethod("Render",Private).Invoke(controller,null);
                Canvas canvas=host.GetComponentsInChildren<Canvas>(true).Single(c=>c.name==CoreGameplayDemoController.EditorPreviewCanvasName);
                CanvasScaler scaler=canvas.GetComponent<CanvasScaler>();if(scaler!=null)scaler.enabled=false;
                RectTransform canvasRect=(RectTransform)canvas.transform;canvas.renderMode=RenderMode.WorldSpace;
                canvasRect.sizeDelta=new Vector2(1920,1080);canvasRect.position=Vector3.zero;canvasRect.rotation=Quaternion.identity;canvasRect.localScale=Vector3.one;
                var stage=canvas.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="Battle Card");
                var player=stage.GetComponentsInChildren<BoardingPartyPresentation>(true).Single(p=>p.name=="Player Ship Art");
                var enemy=stage.GetComponentsInChildren<BoardingPartyPresentation>(true).Single(p=>p.name=="Enemy Ship Art");
                var actors=stage.GetComponentsInChildren<BoardingActorGraphic>(true).OrderBy(a=>a.name,StringComparer.Ordinal).ToArray();
                if(actors.Length<2)throw new InvalidOperationException("Both boarding parties need actual animated meshes.");
                foreach(var actor in actors){actor.SetMotionSuspended(false);actor.SetPresentationTime(0f);}
                BoardingActorAction action=sequence=="attack_hit"?BoardingActorAction.Attack:sequence=="cast_hit"?BoardingActorAction.Cast:
                    sequence=="guard"?BoardingActorAction.Guard:sequence=="heal"?BoardingActorAction.Heal:sequence=="prepare"?BoardingActorAction.Prepare:BoardingActorAction.Idle;
                if(action!=BoardingActorAction.Idle)player.PlayAction(action,BoardingCardActorPresentation.Duration(action));
                Canvas.ForceUpdateCanvases();
                Vector3 middle=(player.transform.position+enemy.transform.position)*.5f;
                views.Add(new View{name="full",width=1920,height=1080,halfHeight=540f,position=Vector3.zero});
                views.Add(new View{name="both",width=1536,height=480,halfHeight=240f,position=middle});
                views.Add(new View{name="player",width=640,height=440,halfHeight=160f,position=player.transform.position});
                views.Add(new View{name="enemy",width=640,height=440,halfHeight=160f,position=enemy.transform.position});
                foreach(View view in views)
                {
                    var cameraHost=new GameObject("Actor Capture "+view.name,typeof(Camera));SceneManager.MoveGameObjectToScene(cameraHost,scene);
                    Camera camera=cameraHost.GetComponent<Camera>();view.camera=camera;camera.enabled=false;camera.scene=scene;camera.orthographic=true;
                    camera.orthographicSize=view.halfHeight;camera.aspect=view.width/(float)view.height;camera.nearClipPlane=.1f;camera.farClipPlane=2000f;
                    camera.transform.position=new Vector3(view.position.x,view.position.y,-1000f);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                    camera.cullingMask=1<<Layer;camera.allowHDR=false;camera.allowMSAA=false;
                    view.target=new RenderTexture(view.width,view.height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);view.target.Create();camera.targetTexture=view.target;
                    view.pixels=new Texture2D(view.width,view.height,TextureFormat.RGB24,false,false);
                }
                bool hit=false;float previous=0f;
                for(int index=0;index<count;index++)
                {
                    float seconds=duration*index/count;float delta=seconds-previous;previous=seconds;
                    foreach(var actor in actors)actor.AdvancePresentation(delta);
                    if((sequence=="cast_hit"||sequence=="attack_hit")&&!hit&&seconds>=.34f){enemy.PlayAction(BoardingActorAction.Hit,.5f);hit=true;}
                    foreach(Transform child in canvas.GetComponentsInChildren<Transform>(true))child.gameObject.layer=Layer;
                    foreach(Text label in canvas.GetComponentsInChildren<Text>(true))label.SetAllDirty();Canvas.ForceUpdateCanvases();
                    if(actors.Any(a=>a.GetComponent<Graphic>().raycastTarget))throw new InvalidOperationException("Actor mesh intercepted UI input.");
                    Vector2[] feet=actors.Select(a=>a.SampleOffset(new Vector2(.5f,a.FootV))).ToArray();
                    if(feet.Any(p=>p.sqrMagnitude>.001f))throw new InvalidOperationException("Animated feet moved off their deck contact.");
                    if(sequence=="idle"&&actors.Any(a=>a.Clock!=0f||a.SampleOffset(new Vector2(.5f,.88f))!=Vector2.zero))throw new InvalidOperationException("Idle actors moved without a card.");
                    foreach(View view in views)
                    {
                        canvas.worldCamera=view.camera;Canvas.ForceUpdateCanvases();
                        if(GraphicsSettings.currentRenderPipeline!=null)RenderPipeline.SubmitRenderRequest(view.camera,new RenderPipeline.StandardRequest{destination=view.target});else view.camera.Render();
                        RenderTexture.active=view.target;view.pixels.ReadPixels(new Rect(0,0,view.width,view.height),0,0);view.pixels.Apply(false,false);
                        string file=sequence+"_"+view.name+"_"+index.ToString("00")+".jpg";File.WriteAllBytes(Path.Combine(output,file),view.pixels.EncodeToJPG(90));
                        if(index==(sequence=="idle"?0:count/4))File.WriteAllBytes(Path.Combine(output,sequence+"_"+view.name+"_Keyframe.png"),view.pixels.EncodeToPNG());
                        report.frames.Add(new Frame{file=file,sequence=sequence,view=view.name,seconds=seconds,duration=duration,width=view.width,height=view.height,
                            actions=actors.Select(a=>a.Action.ToString()).ToArray(),feetOffsets=feet,headOffsets=actors.Select(a=>a.SampleOffset(new Vector2(.5f,.88f))).ToArray()});
                    }
                }
            }
            finally
            {
                RenderTexture.active=prior;
                foreach(View view in views){if(view.pixels!=null)Object.DestroyImmediate(view.pixels);if(view.target!=null){view.target.Release();Object.DestroyImmediate(view.target);}}
                if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}