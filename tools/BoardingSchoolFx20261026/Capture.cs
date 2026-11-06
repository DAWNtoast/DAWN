// Validation-only: installed in the leased isolated project, never in the game.
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
    public static class BoardingSchoolFxCapture20261026
    {
        const int Width=1280, Height=720, Layer=31;
        static int frameCount=24;
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        [Serializable] public sealed class Frame { public string file,school,role; public float progress,seconds,duration,skyY; public Vector2 sourceDeck,targetDeck; public bool inputPassthrough; }
        [Serializable] public sealed class Report
        {
            public string scope="Actual production boarding UI rendered in disposable Editor PreviewScenes, with production FX sampled at deterministic timeline positions. These are visual inspection frames; real button actions are covered separately by PlayMode tests.";
            public int width=Width,height=Height,framesPerSequence;
            public List<Frame> frames=new List<Frame>();
        }
        public static void CaptureFromCommandLine()
        {
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,"..")).Replace('\\','/');
            if(project!="D:/Game/tmp/shipyard-assembly-20261017")throw new InvalidOperationException("Use leased validation project.");
            if(Application.isPlaying||SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("Stopped editor with GPU required.");
            string[] args=Environment.GetCommandLineArgs(); int option=Array.IndexOf(args,"-boardingSchoolFxOutput");
            if(option<0||option+1>=args.Length)throw new ArgumentException("Missing capture destination.");
            string output=Path.GetFullPath(args[option+1]);
            int evidenceOption=Array.IndexOf(args,"-boardingSchoolFxEvidenceSet");
            string evidence=evidenceOption>=0&&evidenceOption+1<args.Length?args[evidenceOption+1]:"validation";
            if(!System.Text.RegularExpressions.Regex.IsMatch(evidence,"^[a-zA-Z0-9_-]+$"))throw new ArgumentException("Invalid evidence set.");
            int countOption=Array.IndexOf(args,"-boardingSchoolFxFrames");
            if(countOption>=0&&countOption+1<args.Length)frameCount=int.Parse(args[countOption+1]);
            if(frameCount<24||frameCount>96)throw new ArgumentException("Use 24 to 96 frames per sequence.");
            if(!output.Replace('\\','/').StartsWith("D:/Game/outputs/boarding-school-fx-20261026/"+evidence+"/",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Output outside task.");
            Directory.CreateDirectory(output);
            if((Directory.EnumerateFiles(output,"*.png").Any()||Directory.EnumerateFiles(output,"*.jpg").Any()))throw new IOException("Existing evidence, choose a new run.");
            var report=new Report { framesPerSequence=frameCount };
            foreach(BattleSchoolAttackKind school in Enum.GetValues(typeof(BattleSchoolAttackKind)))
            {
                CaptureSequence(output,report,school,BoardingSchoolCastRole.Attack);
                BoardingSchoolCastRole role=school==BattleSchoolAttackKind.Fire||school==BattleSchoolAttackKind.Curse?BoardingSchoolCastRole.Affliction:
                    school==BattleSchoolAttackKind.Arcane?BoardingSchoolCastRole.Charge:school==BattleSchoolAttackKind.Tide?BoardingSchoolCastRole.Guard:
                    school==BattleSchoolAttackKind.Blood?BoardingSchoolCastRole.Heal:BoardingSchoolCastRole.Empower;
                CaptureSequence(output,report,school,role);
            }
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
            Debug.Log("Boarding school FX capture complete: "+report.frames.Count+" production UI frames.");
        }
        static void CaptureSequence(string output,Report report,BattleSchoolAttackKind school,BoardingSchoolCastRole role)
        {
            Scene scene=default; RenderTexture target=null,previous=RenderTexture.active; Texture2D pixels=null;
            try
            {
                scene=EditorSceneManager.NewPreviewScene();
                var host=new GameObject("Disposable Boarding School FX Capture"); host.SetActive(false); SceneManager.MoveGameObjectToScene(host,scene);
                var controller=host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle,(CoreGameplayPreviewPort)0,0); host.SetActive(true);
                controller.Session.Battle.Stage=NavalBattleStage.Boarding;
                // Show the actual boarding hand for this school, rather than the naval preview hand.
                controller.Session.Battle.Hand.Clear();
                string schoolId=school.ToString().ToLowerInvariant();
                controller.Session.Battle.Hand.AddRange(Enumerable.Range(0,CoreVoyageSession.CardLibrary.Count)
                    .Where(index=>CoreVoyageSession.CardLibrary[index].SchoolId==schoolId&&CoreVoyageSession.CardLibrary[index].BoardingSpec!=null).Take(5));
                controller.Session.Battle.Energy=controller.Session.Battle.MaxEnergy=12;
                typeof(CoreGameplayDemoController).GetMethod("Render",Private).Invoke(controller,null);
                Canvas canvas=host.GetComponentsInChildren<Canvas>(true).Single(c=>c.name==CoreGameplayDemoController.EditorPreviewCanvasName);
                var stage=canvas.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="Battle Card");
                RectTransform player=stage.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="Player Ship Art");
                RectTransform enemy=stage.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="Enemy Ship Art");
                CanvasScaler scaler=canvas.GetComponent<CanvasScaler>();
                var cameraHost=new GameObject("Disposable School FX Camera",typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraHost,scene);
                Camera camera=cameraHost.GetComponent<Camera>(); camera.enabled=false; camera.scene=scene; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
                camera.orthographic=true; camera.orthographicSize=5; camera.nearClipPlane=.1f; camera.farClipPlane=2000; camera.transform.position=new Vector3(0,0,-1000); camera.cullingMask=1<<Layer; camera.allowHDR=false; camera.allowMSAA=false;
                target=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB); target.Create(); camera.targetTexture=target; camera.aspect=Width/(float)Height;
                canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=100;
                MethodInfo handle=typeof(CanvasScaler).GetMethod("Handle",Private);
                Canvas.ForceUpdateCanvases(); handle.Invoke(scaler,null); Canvas.ForceUpdateCanvases();
                Vector2 from=stage.InverseTransformPoint(player.position),to=stage.InverseTransformPoint(enemy.position);
                Vector2 sourceDeck=stage.InverseTransformPoint(player.TransformPoint(new Vector3(0,player.rect.yMin,0)));
                Vector2 targetDeck=stage.InverseTransformPoint(enemy.TransformPoint(new Vector3(0,enemy.rect.yMin,0)));
                float skyY=stage.rect.yMax-12f;
                var castGo=new GameObject("Boarding School Cast "+school,typeof(RectTransform),typeof(BoardingSchoolCastGraphic)); castGo.transform.SetParent(stage,false);
                var cast=castGo.GetComponent<BoardingSchoolCastGraphic>(); cast.rectTransform.sizeDelta=stage.rect.size;
                cast.Configure(school,role,from,role==BoardingSchoolCastRole.Attack||role==BoardingSchoolCastRole.Affliction?to:from);
                cast.ConfigureEnvironment(role==BoardingSchoolCastRole.Attack||role==BoardingSchoolCastRole.Affliction?targetDeck:sourceDeck,skyY);
                BattleSchoolAttackGraphic attack=null;
                if(role==BoardingSchoolCastRole.Attack)
                {
                    var attackGo=new GameObject("Battle School Attack "+school,typeof(RectTransform),typeof(BattleSchoolAttackGraphic)); attackGo.transform.SetParent(stage,false);
                    attack=attackGo.GetComponent<BattleSchoolAttackGraphic>(); attack.rectTransform.sizeDelta=stage.rect.size;
                    attack.Configure(school,BattleSchoolAttackOutcome.Hit,from,to,true);
                    attack.ConfigureBoardingEnvironment(sourceDeck,targetDeck,skyY);
                }
                float duration=attack==null?BoardingSchoolCastGraphic.Duration:attack.PresentationDuration;
                pixels=new Texture2D(Width,Height,TextureFormat.RGB24,false,false);
                for(int i=0;i<frameCount;i++)
                {
                    float progress=i/(float)(frameCount-1);
                    float seconds=duration*progress;
                    cast.SetProgress(Mathf.Clamp01(seconds/BoardingSchoolCastGraphic.Duration));
                    if(attack!=null)attack.SetProgress(Mathf.Clamp01(seconds/attack.PresentationDuration));
                    foreach(Transform t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=Layer;
                    for(int pass=0;pass<2;pass++)
                    {
                        Canvas.ForceUpdateCanvases(); handle.Invoke(scaler,null);
                        foreach(Text label in canvas.GetComponentsInChildren<Text>(true))label.SetAllDirty();
                        Canvas.ForceUpdateCanvases();
                        if(GraphicsSettings.currentRenderPipeline!=null)RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});else camera.Render();
                    }
                    if(cast.GetComponentsInChildren<Graphic>(true).Any(g=>g.raycastTarget)||(attack!=null&&attack.GetComponentsInChildren<Graphic>(true).Any(g=>g.raycastTarget)))throw new InvalidOperationException("FX intercepts input.");
                    RenderTexture.active=target; pixels.ReadPixels(new Rect(0,0,Width,Height),0,0); pixels.Apply(false,false);
                    string file=school+"_"+role+"_"+i.ToString("00")+".jpg";
                    File.WriteAllBytes(Path.Combine(output,file),pixels.EncodeToJPG(90));
                    if(role==BoardingSchoolCastRole.Attack&&i==frameCount/2)
                        File.WriteAllBytes(Path.Combine(output,school+"_Attack_Keyframe.png"),pixels.EncodeToPNG());
                    report.frames.Add(new Frame{file=file,school=school.ToString(),role=role.ToString(),progress=progress,seconds=seconds,duration=duration,sourceDeck=sourceDeck,targetDeck=targetDeck,skyY=skyY,inputPassthrough=true});
                }
            }
            finally { RenderTexture.active=previous; if(pixels!=null)Object.DestroyImmediate(pixels); if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene); if(target!=null){target.Release();Object.DestroyImmediate(target);} }
        }
    }
}