using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    // Copied only to a freshly renamed isolated project. The preserving parent
    // fixture and temporary checkpoint scope own all test state.
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator ConfirmationGuide_StyleRuntimeScreensAt1080pAnd720p()
        {
            ConfirmationGuideStyleScreens.AssertIsolatedIdentity();
            using (var storage = new VoyageCargoStorageScope())
            {
                PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
                PlayerPrefs.SetInt(CoreGameplayDemoController.ProfileGoldKey, 75432);
                PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, "断锚港");
                var inventory = new OwnedShipComponentInventory(new ComponentArtCatalog());
                inventory.InitializeEmptyLoadout(0);
                PlayerPrefs.SetString(CoreGameplayDemoController.ComponentInventoryKey, inventory.Export());
                PlayerPrefs.Save();
                SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
                yield return null; yield return null;
                string[] beforeCancel = ConfirmationGuidePrefsFingerprint();
                string[] beforeFiles = ConfirmationGuideFileFingerprint(storage.CheckpointDirectory);
                ClickAcceptanceButton("New Game"); yield return null;
                Assert.That(GameObject.Find("New Game Confirmation"), Is.Not.Null);
                ConfirmationGuideStyleScreens.Capture("01_NewGameConfirmation", "真实主菜单：新航行确认框；只测试取消，未执行确认重置。", "Confirmation Card");
                ClickAcceptanceButton("Cancel New Game"); yield return null;
                Assert.That(GameObject.Find("New Game Confirmation"), Is.Null);
                CollectionAssert.AreEqual(beforeCancel, ConfirmationGuidePrefsFingerprint(), "Cancel must preserve every protected profile value and fixture key.");
                CollectionAssert.AreEqual(beforeFiles, ConfirmationGuideFileFingerprint(storage.CheckpointDirectory), "Cancel must not alter a suspended voyage.");

                ResetSavedProgress();
                PlayerPrefs.SetString(CoreGameplayDemoController.WorldEconomyScopeKey, Guid.NewGuid().ToString("N"));
                yield return LoadGuidedTutorialScene();
                CoreGameplayDemoController game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(game.IsTutorialActive, Is.True);
                Assert.That(game.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
                ConfirmationGuideStyleScreens.Capture("02_PortGuide", "真实新角色港口初始引导，保持生产字号与布局。", "Guided Tutorial Banner");
                ClickGuidedButton("Port Hotspot Voyage"); yield return null;
                ConfirmationGuideStyleScreens.Capture("03_DepartureGuide", "真实出航码头教学，正常点击港口入口。", "Guided Tutorial Banner");
                ClickGuidedButton("Start Voyage"); yield return null; yield return null;
                Assert.That(game.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
                ConfirmationGuideStyleScreens.Capture("04_NavigationGuide", "真实初航航向教学，未执行额外航行。", "Guided Tutorial Banner");

                CoreMapNode firstEvent = game.Session.Map.Nodes.First(node => node.Encounter.Id == "tutorial_first_event");
                ConfirmationGuideStageEncounter(game.Session, firstEvent);
                ConfirmationGuideRender(game); yield return null; yield return null;
                Assert.That(game.CurrentGuidedTutorialStep, Is.EqualTo(GuidedTutorialStep.FirstMandatory));
                ConfirmationGuideStyleScreens.Capture("05_EventGuide_Fixture", "隔离夹具：布置首个强制事件，生产事件界面与教学banner。", "Guided Tutorial Banner");

                var bossSession = new CoreVoyageSession();
                bossSession.Cargo.Water = 200; bossSession.Cargo.Food = 200;
                Assert.That(bossSession.ConfigureGuidedTutorialVoyage(CoreGuidedTutorialContent.BossVoyageLeg), Is.True);
                bossSession.StartVoyage();
                CoreMapNode bossNode = bossSession.Map.Nodes.First(node => node.Encounter.Id == CoreGuidedTutorialContent.BossEventId);
                ConfirmationGuideStageEncounter(bossSession, bossNode);
                Assert.That(bossSession.ResolveChoice(0), Is.True);
                Assert.That(bossSession.IsGuidedBossBattle, Is.True);
                typeof(CoreGameplayDemoController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, bossSession);
                ConfirmationGuideRender(game); yield return null; yield return null;
                yield return WaitForGuidedCaptureScreenReveal(game);
                ConfirmationGuideStyleScreens.Capture("06_BattleGuide_Fixture", "隔离夹具：首领战初始教学，使用真实战斗与引导渲染；非完整通关记录。", "Guided Tutorial Banner");

                bossSession.Battle.EnemyHull = 0;
                typeof(CoreVoyageSession).GetMethod("ResolveBattleOutcome", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bossSession, null);
                Assert.That(bossSession.GuidedTutorialBossDefeated, Is.True);
                ConfirmationGuideRender(game); yield return null; yield return null;
                ConfirmationGuideStyleScreens.Capture("07_VoyageReward_Fixture", "隔离夹具：首领胜利后的航行奖励牌，未修改任何真实玩家进度。", "Guided Tutorial Component Reward");
                bossSession.SafeReturn();
                ConfirmationGuideRender(game); yield return null; yield return null;
                Assert.That(bossSession.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
                Button skipReveal = ActiveButton("Skip Settlement Reveal");
                if (skipReveal != null && skipReveal.IsInteractable())
                {
                    ClickGuidedButton("Skip Settlement Reveal");
                    yield return null;
                }
                ConfirmationGuideStyleScreens.Capture("08_SettlementReward_Fixture", "隔离夹具：初航结算奖励牌与教学提示，未点击入库结算。", "Guided Tutorial Component Reward");
                LogAssert.NoUnexpectedReceived();
            }
        }

        private static void ConfirmationGuideStageEncounter(CoreVoyageSession session, CoreMapNode node)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            typeof(CoreVoyageSession).GetProperty("Position", flags).SetValue(session, node.Position);
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter", flags).SetValue(session, node.Encounter);
            typeof(CoreVoyageSession).GetProperty("Phase", flags).SetValue(session, CoreDemoPhase.Encounter);
            typeof(CoreVoyageSession).GetField("activeNode", flags).SetValue(session, node);
            node.Discovered = true;
        }

        private static void ConfirmationGuideRender(CoreGameplayDemoController game) =>
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);

        private static string[] ConfirmationGuidePrefsFingerprint() => ProfileSaveJournal.ProtectedKeys
            .Concat(new[] { CoreGameplayDemoController.WorldEconomyScopeKey, CoreGameplayDemoController.ComponentInventoryKey,
                CoreGameplayDemoController.ProfileGoldKey, CoreGameplayDemoController.CurrentPortKey,
                CoreGameplayDemoController.TutorialCompletedKey, ProfileGoldPersistence.DecimalKey })
            .Distinct().OrderBy(key => key).Select(key => key + "|" + PlayerPrefs.HasKey(key) + "|" +
                PlayerPrefs.GetString(key, "<absent>") + "|" + PlayerPrefs.GetInt(key, -912345) + "|" +
                PlayerPrefs.GetFloat(key, -912345f).ToString("R", CultureInfo.InvariantCulture)).ToArray();

        private static string[] ConfirmationGuideFileFingerprint(string root)
        {
            if (!Directory.Exists(root)) return Array.Empty<string>();
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            return Directory.GetFiles(fullRoot, "*", SearchOption.AllDirectories).OrderBy(path => path).Select(path =>
            {
                if (!Path.GetFullPath(path).StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Checkpoint fingerprint escaped its isolated directory.");
                using (var hash = SHA256.Create()) using (var stream = File.OpenRead(path))
                    return path.Substring(fullRoot.Length) + "|" + BitConverter.ToString(hash.ComputeHash(stream));
            }).ToArray();
        }
    }
    internal static class ConfirmationGuideStyleScreens
    {
        [Serializable] private sealed class Report
        {
            public string scope="Actual runtime MainMenu and CoreGameplayDemo UI at 1920x1080 and 1280x720 with production CanvasScaler. Confirmation cancel and initial departure use real callbacks. Event, boss and reward are explicitly staged isolated fixtures. No scene writes.";
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

        internal static void AssertIsolatedIdentity()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            const string prefix = "ConfirmationGuideValidation20261014_";
            if (!project.StartsWith("D:/Game/tmp/", StringComparison.OrdinalIgnoreCase) ||
                !Application.companyName.StartsWith(prefix, StringComparison.Ordinal) ||
                !Application.productName.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidOperationException("Run only in the prepared isolated project with its own save identity.");
        }

        internal static void Capture(string state,string caption,string rootName)
        {
            AssertIsolatedIdentity();
            if (Path.GetFileName(state) != state) throw new InvalidOperationException("State must be a simple filename stem.");
            if (output==null)
            {
                string project=Path.GetFullPath(Path.Combine(Application.dataPath,"..")).Replace('\\','/');
                if (!project.StartsWith("D:/Game/tmp/",StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("An isolated project is required.");
                string[] args=Environment.GetCommandLineArgs();
                int index=Array.IndexOf(args,"-confirmationGuideStyleOutput");
                if(index<0 || index+1>=args.Length) throw new InvalidOperationException("Pass -confirmationGuideStyleOutput.");
                output=Path.GetFullPath(args[index+1]);
                if(!output.Replace('\\','/').StartsWith("D:/Game/Artifacts/ConfirmationGuideStyle20261014/",StringComparison.OrdinalIgnoreCase) || Directory.Exists(output))
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
                cameraHost=new GameObject("Confirmation Guide Capture Camera",typeof(Camera));
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
                    foreach (GuidedTutorialBannerLayout layout in canvas.GetComponentsInChildren<GuidedTutorialBannerLayout>(false))
                        typeof(GuidedTutorialBannerLayout).GetMethod("Apply", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(layout, null);
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
