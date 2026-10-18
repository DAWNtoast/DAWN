using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    // Installed only in the isolated test assembly. No persistent scenes are generated.
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator ComponentResale_StyleRuntimeScreensAt1080pAnd720p()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return LoadComponentResaleFixture();
                CoreGameplayDemoController game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                ClickAcceptanceButton("Port Hotspot ComponentShop"); yield return null;
                ClickAcceptanceButton("Open Component Resale"); yield return null;
                Assert.That(VisibleComponentSaleButtons().Length, Is.Zero);
                ComponentResaleStyleScreens.Capture("01_Empty", "空仓状态", "Component Resale Panel");
                ClickAcceptanceButton("Back To Component Shop Sales"); yield return null;
                ComponentArtEntry[] entries = new ComponentArtCatalog().ExpandedEntries
                    .GroupBy(entry => entry.Slot).Select(group => group.Last()).Append(ComponentResaleFixtureEntry()).ToArray();
                for (int index=0; index<6; index++) game.GrantOwnedComponent(entries[index % entries.Length]);
                decimal initialGold = game.Session.ProfileGold;
                int initialCount = game.OwnedComponents.Count;
                ClickAcceptanceButton("Open Component Resale"); yield return null;
                Assert.That(VisibleComponentSaleButtons().Length, Is.EqualTo(5));
                ComponentResaleStyleScreens.Capture("02_FullPage", "五行闲置部件", "Component Resale Panel");
                string[] pageOne = VisibleComponentSaleButtons().Select(button => button.name).ToArray();
                ClickAcceptanceButton("Component Resale Next"); yield return null;
                Assert.That(VisibleComponentSaleButtons().Length, Is.EqualTo(1));
                ComponentResaleStyleScreens.Capture("03_SecondPage", "第二页与分页状态", "Component Resale Panel");
                ClickAcceptanceButton("Component Resale Previous"); yield return null;
                CollectionAssert.AreEquivalent(pageOne, VisibleComponentSaleButtons().Select(button=>button.name).ToArray());
                ClickAcceptanceButton(pageOne.First()); yield return null;
                ComponentResaleStyleScreens.Capture("04_Confirmation", "出售确认弹窗", "Component Sale Confirmation");
                ClickAcceptanceButton("Cancel Component Sale"); yield return null;
                Assert.That(GameObject.Find("Component Sale Confirmation"), Is.Null);
                Assert.That(game.Session.ProfileGold, Is.EqualTo(initialGold));
                Assert.That(game.OwnedComponents.Count, Is.EqualTo(initialCount));
                LogAssert.NoUnexpectedReceived();
            }
        }
    }

    internal static class ComponentResaleStyleScreens
    {
        [Serializable] private sealed class Report
        {
            public string scope="Actual runtime CoreGameplayDemo UI, real entry/paging/cancel callbacks. Four states at 1920x1080 and 1280x720 with the production CanvasScaler enabled. No scene writes.";
            public bool passed;
            public List<Frame> frames=new List<Frame>();
        }
        [Serializable] private sealed class Frame
        {
            public string state, caption, file;
            public int width,height;
            public bool passed, verifiedScaler;
            public float scaleFactor,expectedScaleFactor;
            public Vector2 renderingDisplaySize,logicalCanvasSize;
            public Rect panelBounds;
            public List<string> errors=new List<string>();
            public List<Control> controls=new List<Control>();
            public List<Label> labels=new List<Label>();
        }
        [Serializable] private sealed class Control { public string name,label; public bool interactable; public Rect bounds; }
        [Serializable] private sealed class Label { public string name,text; public int fontSize; public Rect bounds; public float preferredHeight,availableHeight; }
        private static readonly Report report=new Report();
        private static string output;

        internal static void Capture(string state,string caption,string rootName)
        {
            if (output==null)
            {
                string project=Path.GetFullPath(Path.Combine(Application.dataPath,"..")).Replace('\\','/');
                if (!project.StartsWith("D:/Game/tmp/",StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("An isolated project is required.");
                string[] args=Environment.GetCommandLineArgs();
                int index=Array.IndexOf(args,"-componentResaleStyleOutput");
                if(index<0 || index+1>=args.Length) throw new InvalidOperationException("Pass -componentResaleStyleOutput.");
                output=Path.GetFullPath(args[index+1]);
                if(!output.Replace('\\','/').StartsWith("D:/Game/Artifacts/ComponentResaleStyle20261014/",StringComparison.OrdinalIgnoreCase) || Directory.Exists(output))
                    throw new InvalidOperationException("Use a fresh task artifact directory.");
                Directory.CreateDirectory(output);
            }
            foreach(Vector2Int size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720)})
                CaptureFrame(state,caption,rootName,size.x,size.y);
        }

        private static void CaptureFrame(string state,string caption,string rootName,int width,int height)
        {
            var frame=new Frame{state=state,caption=caption,width=width,height=height,file=state+"_"+width+"x"+height+".png"};
            report.frames.Add(frame);
            GameObject root=GameObject.Find(rootName);
            Assert.That(root,Is.Not.Null);
            Canvas canvas=root.GetComponentInParent<Canvas>().rootCanvas;
            RectTransform canvasRect=canvas.GetComponent<RectTransform>();
            CanvasScaler scaler=canvas.GetComponent<CanvasScaler>();
            Assert.That(scaler.enabled,Is.True,"Use the actual production scaler.");
            RenderMode savedMode=canvas.renderMode;
            Camera savedCamera=canvas.worldCamera;
            float savedPlane=canvas.planeDistance;
            Vector2 savedSize=canvasRect.sizeDelta;
            Vector3 savedPosition=canvasRect.localPosition,savedScale=canvasRect.localScale;
            Quaternion savedRotation=canvasRect.localRotation;
            Transform[] descendants=canvas.GetComponentsInChildren<Transform>(true);
            int[] savedLayers=descendants.Select(child=>child.gameObject.layer).ToArray();
            RenderTexture savedActive=RenderTexture.active,target=null;
            Texture2D pixels=null;
            GameObject cameraHost=null;
            MethodInfo handle=typeof(CanvasScaler).GetMethod("Handle",BindingFlags.Instance|BindingFlags.NonPublic);
            try
            {
                cameraHost=new GameObject("Component Resale Capture Camera",typeof(Camera));
                Camera camera=cameraHost.GetComponent<Camera>();
                camera.enabled=false;
                camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=Color.black;
                camera.orthographic=true;
                camera.orthographicSize=5;
                camera.nearClipPlane=.1f;
                camera.farClipPlane=2000;
                camera.transform.position=new Vector3(0,0,-1000);
                camera.cullingMask=1<<31;
                camera.allowHDR=camera.allowMSAA=false;
                target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
                target.Create();
                camera.targetTexture=target;
                camera.rect=new Rect(0,0,1,1);
                camera.aspect=width/(float)height;
                canvas.renderMode=RenderMode.ScreenSpaceCamera;
                canvas.worldCamera=camera;
                canvas.planeDistance=100;
                foreach(Transform child in descendants) child.gameObject.layer=31;
                for(int pass=0;pass<4;pass++)
                {
                    Canvas.ForceUpdateCanvases();
                    handle.Invoke(scaler,null);
                    foreach(Text text in canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if(GraphicsSettings.currentRenderPipeline!=null) RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
                    else camera.Render();
                }
                frame.renderingDisplaySize=canvas.renderingDisplaySize;
                frame.logicalCanvasSize=canvasRect.rect.size;
                frame.scaleFactor=canvas.scaleFactor;
                float wr=width/scaler.referenceResolution.x,hr=height/scaler.referenceResolution.y;
                frame.expectedScaleFactor=scaler.screenMatchMode==CanvasScaler.ScreenMatchMode.Expand?Mathf.Min(wr,hr):
                    scaler.screenMatchMode==CanvasScaler.ScreenMatchMode.Shrink?Mathf.Max(wr,hr):Mathf.Pow(2,Mathf.Lerp(Mathf.Log(wr,2),Mathf.Log(hr,2),scaler.matchWidthOrHeight));
                frame.verifiedScaler=scaler.enabled && Mathf.Abs(frame.renderingDisplaySize.x-width)<.5f && Mathf.Abs(frame.renderingDisplaySize.y-height)<.5f &&
                    Mathf.Abs(canvas.pixelRect.width-width)<.5f && Mathf.Abs(canvas.pixelRect.height-height)<.5f &&
                    Mathf.Abs(camera.pixelRect.width-width)<.5f && Mathf.Abs(camera.pixelRect.height-height)<.5f &&
                    Mathf.Abs(frame.scaleFactor-frame.expectedScaleFactor)<.001f &&
                    Mathf.Abs(frame.logicalCanvasSize.x-width/frame.scaleFactor)<1 && Mathf.Abs(frame.logicalCanvasSize.y-height/frame.scaleFactor)<1;
                if(!frame.verifiedScaler) frame.errors.Add("Production CanvasScaler is not driven by the requested render resolution.");
                frame.panelBounds=Bounds(root.GetComponent<RectTransform>(),camera);
                if(!Inside(frame.panelBounds,new Rect(0,0,width,height))) frame.errors.Add("Panel extends outside the viewport.");
                foreach(Selectable control in root.GetComponentsInChildren<Selectable>(false))
                {
                    Rect bounds=Bounds(control.GetComponent<RectTransform>(),camera);
                    frame.controls.Add(new Control{name=control.name,label=control.GetComponentInChildren<Text>()?.text,interactable=control.IsInteractable(),bounds=bounds});
                    if(!Inside(bounds,frame.panelBounds)) frame.errors.Add("Control outside panel: "+control.name);
                }
                foreach(Text text in root.GetComponentsInChildren<Text>(false))
                {
                    Rect bounds=Bounds(text.rectTransform,camera);
                    frame.labels.Add(new Label{name=text.name,text=text.text,fontSize=text.fontSize,bounds=bounds,preferredHeight=text.preferredHeight,availableHeight=text.rectTransform.rect.height});
                    if(!Inside(bounds,frame.panelBounds)) frame.errors.Add("Text box outside panel: "+text.name);
                    if(text.verticalOverflow==VerticalWrapMode.Truncate && text.preferredHeight>text.rectTransform.rect.height+2)
                        frame.errors.Add("Text height exceeds its box: "+text.name);
                }
                RenderTexture.active=target;
                pixels=new Texture2D(width,height,TextureFormat.RGB24,false,false);
                pixels.ReadPixels(new Rect(0,0,width,height),0,0);
                pixels.Apply(false,false);
                File.WriteAllBytes(Path.Combine(output,frame.file),pixels.EncodeToPNG());
                frame.passed=frame.errors.Count==0;
            }
            finally
            {
                RenderTexture.active=savedActive;
                for(int index=0;index<descendants.Length;index++) if(descendants[index]!=null) descendants[index].gameObject.layer=savedLayers[index];
                canvas.renderMode=savedMode;
                canvas.worldCamera=savedCamera;
                canvas.planeDistance=savedPlane;
                canvasRect.sizeDelta=savedSize;
                canvasRect.localPosition=savedPosition;
                canvasRect.localRotation=savedRotation;
                canvasRect.localScale=savedScale;
                Canvas.ForceUpdateCanvases();
                handle.Invoke(scaler,null);
                Canvas.ForceUpdateCanvases();
                if(pixels!=null) Object.DestroyImmediate(pixels);
                if(cameraHost!=null) Object.DestroyImmediate(cameraHost);
                if(target!=null){target.Release();Object.DestroyImmediate(target);}
                report.passed=report.frames.All(item=>item.passed);
                File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
            }
            Assert.That(frame.errors,Is.Empty,state+" "+width+"x"+height);
        }
        private static bool Inside(Rect inner,Rect outer)=>inner.xMin>=outer.xMin-1 && inner.yMin>=outer.yMin-1 && inner.xMax<=outer.xMax+1 && inner.yMax<=outer.yMax+1;
        private static Rect Bounds(RectTransform rect,Camera camera)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            Vector2 min=new Vector2(float.MaxValue,float.MaxValue),max=new Vector2(float.MinValue,float.MinValue);
            foreach(Vector3 corner in corners){Vector2 point=camera.WorldToScreenPoint(corner);min=Vector2.Min(min,point);max=Vector2.Max(max,point);}
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
    }
}
