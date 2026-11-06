using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Opt-in deterministic capture/acceptance inside the actual standalone player.</summary>
    public sealed class Boarding3DTrialCapture : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public string result, unityVersion;
            public int width, height, frameCount, attacks, hits;
            public float minimumSwordTargetDistance;
            public List<string> checks = new List<string>();
            public List<string> screenshots = new List<string>();
            public List<string> errors = new List<string>();
        }
        readonly Report report = new Report();
        Boarding3DTrial trial;
        string output;
        int frame;
        bool frames;
        RenderTexture captureTarget;
        Camera uiCamera;
        IEnumerator Start()
        {
            string[] args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length;i++)
            {
                if(args[i]=="-trialCapturePath"&&i+1<args.Length)output=Path.GetFullPath(args[++i]);
                if(args[i]=="-trialCaptureFrames")frames=true;
            }
            if(string.IsNullOrEmpty(output))yield break;
            Directory.CreateDirectory(output);
            if(frames)Directory.CreateDirectory(Path.Combine(output,"frames"));
            Application.logMessageReceived+=OnLog;
            Application.runInBackground=true;
            trial=GetComponent<Boarding3DTrial>();trial.Initialize();trial.ExternalClock=true;trial.SetAutoDemo(false);
            SetupCapture();
            yield return null;
            yield return new WaitForEndOfFrame();
            Validate();
            trial.ResetTrial();
            yield return Sequence("idle",30,15,"01-idle.png");
            yield return Sequence("walk",90,26,"02-walk.png");
            yield return Sequence("turn",78,36,"03-turn.png");
            yield return Sequence("attack",66,25,"04-attack.png");
            yield return Sequence("counter",66,26,"05-hit.png");
            yield return Sequence("cast",66,28,"06-cast.png");
            yield return Sequence("guard",66,26,"07-guard.png");
            yield return Sequence("heal",60,27,"08-heal.png");
            yield return Sequence("death",78,59,"09-death.png");
            trial.ResetTrial(false);
            yield return Sequence("idle",24,12,"10-reset.png");
            report.width=Screen.width;report.height=Screen.height;report.frameCount=frame;
            report.attacks=trial.CompletedAttacks;report.hits=trial.ReceivedHits;
            report.unityVersion=Application.unityVersion;
            report.result=report.errors.Count==0?"PASS":"FAIL";
            File.WriteAllText(Path.Combine(output,"acceptance.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=OnLog;
            Application.Quit(report.errors.Count==0?0:1);
        }
        IEnumerator Sequence(string beat,int count,int screenshotFrame,string screenshot)
        {
            if(beat!="idle")trial.BeginBeat(beat);
            for(int i=0;i<count;i++)
            {
                trial.Step(1f/30f);
                yield return new WaitForEndOfFrame();
                if(frames||i==screenshotFrame)
                {
                    Texture2D image=CaptureFrame();
                    if(frames)File.WriteAllBytes(Path.Combine(output,"frames",frame.ToString("D5")+".jpg"),image.EncodeToJPG(91));
                    if(i==screenshotFrame)
                    {
                        File.WriteAllBytes(Path.Combine(output,screenshot),image.EncodeToPNG());report.screenshots.Add(screenshot);
                        Check(ImageHasDetail(image),"Rendered frame contains visible scene detail: "+beat);
                    }
                    Destroy(image);
                }
                frame++;
            }
        }
        void Validate()
        {
            Check(trial.ViewCamera.orthographic,"Fixed orthographic camera");
            Check(trial.Player.GetComponentsInChildren<MeshRenderer>().Length>20,"Player uses actual 3D mesh parts");
            Check(trial.Enemy.GetComponentsInChildren<MeshRenderer>().Length>20,"Enemy uses actual 3D mesh parts");
            Vector3 cameraPosition=trial.ViewCamera.transform.position;
            Quaternion cameraRotation=trial.ViewCamera.transform.rotation;
            trial.BeginBeat("attack");trial.Step(.2f);
            Vector3 weaponBefore=trial.Player.WeaponTip.position;
            trial.Step(.3f);
            Check(Vector3.Distance(weaponBefore,trial.Player.WeaponTip.position)>.05f,"Articulated sword changes position during attack");
            float before=trial.SimulationTime;Vector3 pausedPosition=trial.Player.WeaponTip.position;
            trial.SetPaused(true);trial.Step(.5f);
            Check(trial.SimulationTime==before&&trial.Player.WeaponTip.position==pausedPosition,"Pause freezes both action time and rendered pose");
            trial.SetPaused(false);
            float closestStrike=float.MaxValue;
            for(int i=0;i<80;i++)
            {
                trial.Step(1f/30f);
                if(trial.CurrentBeat=="attack")
                {
                    Vector3 grip=trial.Player.WeaponTip.parent.position,blade=trial.Player.WeaponTip.position-grip;
                    Vector3 target=trial.Enemy.transform.position+Vector3.up*1.25f;
                    float u=Mathf.Clamp01(Vector3.Dot(target-grip,blade)/Mathf.Max(.0001f,blade.sqrMagnitude));
                    closestStrike=Mathf.Min(closestStrike,Vector3.Distance(target,grip+blade*u));
                }
            }
            report.minimumSwordTargetDistance=closestStrike;
            Check(closestStrike<.65f,"Sword sweep reaches the target's torso region");
            Check(trial.CurrentBeat=="idle"&&trial.CompletedAttacks==1&&trial.ReceivedHits==1,"One attack produces exactly one timed hit and recovers");
            Vector3 home=trial.Player.transform.localPosition;
            trial.BeginBeat("walk");for(int i=0;i<100;i++)trial.Step(1f/30f);
            Check(Vector3.Distance(home,trial.Player.transform.localPosition)<.001f,"Walking returns to its original footing without drift");
            trial.SetSpeed(.5f);before=trial.SimulationTime;trial.Step(.4f);
            Check(Mathf.Abs(trial.SimulationTime-before-.2f)<.001f,"Half speed advances the simulation at 0.5x");trial.SetSpeed(1f);
            trial.BeginBeat("death");for(int i=0;i<80;i++)trial.Step(1f/30f);
            Check(trial.Enemy.Action==Boarding3DAction.Death&&trial.Enemy.Progress>=1,"Death pose remains after the animation ends");
            trial.ResetTrial();Check(trial.Enemy.Action==Boarding3DAction.Idle,"Reset restores a fallen character");
            foreach(Button button in GetComponentsInChildren<Button>())
                if(button.name=="挥砍"){button.onClick.Invoke();break;}
            Check(trial.CurrentBeat=="attack"&&!trial.AutoDemo,"UI attack button starts the real action and stops autoplay");
            Check(trial.ViewCamera.transform.position==cameraPosition&&trial.ViewCamera.transform.rotation==cameraRotation,"Camera stays fixed across all actions");
            Check(trial.Player.WeaponTip.GetComponentInParent<Boarding3DActor>()==trial.Player,"Weapon stays attached to the character hierarchy");
            trial.Step(.3f);Vector3 beforeSwitch=trial.Player.WeaponTip.position;
            trial.BeginBeat("cast");
            Check(Vector3.Distance(beforeSwitch,trial.Player.WeaponTip.position)<.001f,"Interrupting an action preserves its starting pose for blending");
            trial.enabled=false;before=trial.SimulationTime;trial.Step(.2f);
            Check(trial.SimulationTime==before,"Disabled trial does not advance through its public clock");trial.enabled=true;
        }
        void SetupCapture()
        {
            // Explicit offscreen rendering also works with a hidden/minimized player window.
            captureTarget=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){antiAliasing=4};captureTarget.Create();
            trial.ViewCamera.targetTexture=captureTarget;trial.ViewCamera.enabled=false;trial.ViewCamera.cullingMask=~(1<<5);
            var go=new GameObject("Capture UI camera");go.transform.SetParent(transform,false);go.transform.position=new Vector3(0,0,-100);
            uiCamera=go.AddComponent<Camera>();uiCamera.orthographic=true;uiCamera.orthographicSize=5;
            uiCamera.clearFlags=CameraClearFlags.Depth;uiCamera.cullingMask=1<<5;uiCamera.nearClipPlane=.1f;uiCamera.farClipPlane=10;
            uiCamera.targetTexture=captureTarget;uiCamera.enabled=false;
            Canvas canvas=GetComponentInChildren<Canvas>();
            foreach(Transform child in canvas.GetComponentsInChildren<Transform>(true))child.gameObject.layer=5;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=uiCamera;canvas.planeDistance=1;
            Canvas.ForceUpdateCanvases();
        }
        Texture2D CaptureFrame()
        {
            Canvas.ForceUpdateCanvases();
            RenderTexture previous=RenderTexture.active;RenderTexture.active=captureTarget;GL.Clear(true,true,Color.black);
            trial.ViewCamera.Render();uiCamera.Render();
            RenderTexture.active=captureTarget;
            var image=new Texture2D(captureTarget.width,captureTarget.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,captureTarget.width,captureTarget.height),0,0);image.Apply();RenderTexture.active=previous;
            return image;
        }
        bool ImageHasDetail(Texture2D image)
        {
            Color first=image.GetPixel(image.width/3,image.height/2);float difference=0;
            for(int y=image.height/3;y<image.height*3/4;y+=37)
                for(int x=image.width/5;x<image.width*4/5;x+=43)
                {Color c=image.GetPixel(x,y);difference+=Mathf.Abs(c.r-first.r)+Mathf.Abs(c.g-first.g)+Mathf.Abs(c.b-first.b);}
            return difference>2f;
        }
        void Check(bool passed,string name){if(passed)report.checks.Add(name);else report.errors.Add(name);}
        void OnLog(string condition,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors.Add(condition);}
        void OnDestroy()
        {
            Application.logMessageReceived-=OnLog;
            if(trial!=null&&trial.ViewCamera!=null)trial.ViewCamera.targetTexture=null;
            if(uiCamera!=null)uiCamera.targetTexture=null;
            if(captureTarget!=null){captureTarget.Release();Destroy(captureTarget);}
        }
    }
}
