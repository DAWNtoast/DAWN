using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace ShatteredSail.Editor {
public static class CrewReviewCapture {
const int W=1920,H=1080,Layer=31;
static BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
static string[] Names={"Background","Story","Skills","SpecialCards"};
static string[] Keys={"background","story","skills","specialCards"};
static string[] Labels={"人物背景","航海异闻","职业技能","专属卡牌"};
[Serializable] public class Config { public string Output,Evidence,InputFingerprint,Company,Product; }
[Serializable] public class Page { public string key,label,path,sha256; public int width=W,height=H; public bool scrollable; }
[Serializable] public class Crew {public string id,name,port,tavernScenePath; public List<Page> pages=new List<Page>();}
[Serializable] public class Port {public string port,slug,path,sha256;}
[Serializable] public class Report { public string generatedUtc,inputFingerprint,method; public int crewCount,pageCount; public List<Crew> crew=new List<Crew>();public List<Port> ports=new List<Port>(); public List<string> warnings=new List<string>(); }
static void Check(bool ok,string m){if(!ok)throw new InvalidOperationException(m);}
static string Hash(byte[] bytes){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
public static void Run(){
string project=Path.GetFullPath(Path.Combine(Application.dataPath,"..")).Replace('\\','/');
Check(System.Text.RegularExpressions.Regex.IsMatch(project,@"/tmp/crew-review-20261030-[a-f0-9]{32}$")&&File.Exists(Path.Combine(project,"OWNED_CREW_REVIEW.txt")),"Not task-owned isolated project.");
var cfg=JsonUtility.FromJson<Config>(File.ReadAllText(Path.Combine(project,"capture-config.json")));
Check(Application.companyName==cfg.Company&&Application.productName==cfg.Product,"Independent preference identity required.");
Directory.CreateDirectory(Path.Combine(cfg.Output,"game-view"));
Check(Resources.Load<Font>(GameTypography.FontResourcePath)!=null&&Resources.Load<Font>(GameTypography.BodyFontResourcePath)!=null,"Production fonts missing.");
var report=new Report{generatedUtc=DateTime.UtcNow.ToString("o"),inputFingerprint=cfg.InputFingerprint,method="Current production CoreGameplayDemoController, native Unity preview Canvas / camera at 1920x1080. No scene, save or main project edits; page callbacks only."};
foreach(CoreGameplayPreviewPort port in Enum.GetValues(typeof(CoreGameplayPreviewPort))){
string portName;string scenePath;
using(var view=new Preview(port,CoreGameplayPreviewScreen.Tavern)){
portName=view.Controller.Session.CurrentPort;
string resource=PortFacilityArtCatalog.RegionalBackgroundResourcePath(2,portName);
Check(Resources.Load<Texture2D>(resource)!=null,"Missing actual tavern art: "+resource);
string slug=Path.GetFileName(resource).Replace("tavern_","").Replace("_v1","");scenePath="game-view/tavern_"+slug+".png";
view.Render();report.ports.Add(new Port{port=portName,slug=slug,path=scenePath,sha256=view.Save(Path.Combine(cfg.Output,scenePath))});
}
using(var view=new Preview(port,CoreGameplayPreviewScreen.TavernCrewDetail)){
foreach(var person in CrewNarrativeCatalog.ForPort(portName)){
var record=new Crew{id=person.Id,name=person.DisplayName,port=portName,tavernScenePath=scenePath};
view.SelectCrew(person.ProfessionIndex);
var special=CoreSpecialCardCatalog.ForCrew(person.Id); if(special!=null){Check(Resources.Load<Texture2D>(CardSchoolArtCatalog.DedicatedIllustrationPath(special.Card))!=null,"Missing crew special card art: "+person.Id); Check(CardSchoolArtCatalog.Frame(special.Card,NavalBattleStage.Preparation)!=null,"Missing crew special card frame: "+person.Id);}
var portrait=view.Root.GetComponentsInChildren<RawImage>(true).Single(x=>x.name=="Tavern Dossier Background Portrait");
Check(portrait.texture==Resources.Load<Texture2D>("PortArt/CrewDossiers/"+person.Id+"_portrait_v2"),"Wrong production portrait: "+person.Id);
Check(view.Root.GetComponentsInChildren<Text>(true).Single(x=>x.name=="Tavern Crew Detail Name").text==person.DisplayName,"Wrong crew label");
for(int page=0;page<4;page++){
view.SelectPage(Names[page]);view.Render();
var pageRow=new Page{key=Keys[page],label=Labels[page],path="game-view/"+person.Id+"_"+Names[page]+".png",scrollable=view.Root.GetComponentsInChildren<ScrollRect>(false).Any()};
pageRow.sha256=view.Save(Path.Combine(cfg.Output,pageRow.path));record.pages.Add(pageRow);report.pageCount++;
if(pageRow.scrollable)report.warnings.Add(person.Id+"/"+Keys[page]+": page includes a production scroll area; screenshot shows initial scroll position, full text is provided in the review data.");
}
report.crew.Add(record);report.crewCount++;Debug.Log("CREW_CAPTURE "+person.Id+" 4 pages complete");
}
}
EditorUtility.UnloadUnusedAssetsImmediate();GC.Collect();
}
Check(report.crewCount==36&&report.pageCount==144&&report.ports.Count==6,"Incomplete crew capture");
File.WriteAllText(Path.Combine(cfg.Output,"capture-manifest.json"),JsonUtility.ToJson(report,true),new System.Text.UTF8Encoding(false));
Debug.Log("CREW_REVIEW_CAPTURE_OK 36 crew / 144 dossier pages / 6 taverns");
}
sealed class Preview:IDisposable {
Scene scene;GameObject host,camHost;Camera camera;Canvas canvas;RenderTexture target,previous;
public CoreGameplayDemoController Controller;public Transform Root=>canvas.transform;
public Preview(CoreGameplayPreviewPort port,CoreGameplayPreviewScreen screen){
previous=RenderTexture.active;scene=EditorSceneManager.NewPreviewScene();host=new GameObject("Task crew capture"){hideFlags=HideFlags.HideAndDontSave};host.SetActive(false);SceneManager.MoveGameObjectToScene(host,scene);
Controller=host.AddComponent<CoreGameplayDemoController>();Controller.ConfigureEditorPreview(screen,port,0);host.SetActive(true);Check(Controller.Session!=null,"No preview session");
canvas=host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName).GetComponent<Canvas>();
camHost=new GameObject("Task camera",typeof(Camera)){hideFlags=HideFlags.HideAndDontSave};SceneManager.MoveGameObjectToScene(camHost,scene);camera=camHost.GetComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.06f,.075f,1);camera.orthographic=true;camera.orthographicSize=H*.5f;camera.aspect=(float)W/H;camera.nearClipPlane=.1f;camera.farClipPlane=2000;camera.transform.position=new Vector3(0,0,-1000);camera.cullingMask=1<<Layer;camera.allowHDR=false;camera.allowMSAA=false;
target=new RenderTexture(W,H,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);target.Create();camera.targetTexture=target;canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;var rect=canvas.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(W,H);rect.position=Vector3.zero;rect.rotation=Quaternion.identity;rect.localScale=Vector3.one;AssignLayers();
}
void AssignLayers(){foreach(var x in canvas.GetComponentsInChildren<Transform>(true))x.gameObject.layer=Layer;}
public void SelectCrew(int profession){typeof(CoreGameplayDemoController).GetField("selectedTavernCandidate",Private).SetValue(Controller,profession);typeof(CoreGameplayDemoController).GetMethod("Render",Private).Invoke(Controller,null);AssignLayers();}
public void SelectPage(string page){Check(Names.Contains(page),"Unknown page");Root.GetComponentsInChildren<Button>(true).Single(b=>b.name=="DossierPage"+page).onClick.Invoke();}
public void Render(){for(int i=0;i<3;i++){foreach(Text text in canvas.GetComponentsInChildren<Text>(true))text.SetAllDirty();Canvas.ForceUpdateCanvases();camera.Render();}}
public string Save(string path){RenderTexture.active=target;var image=new Texture2D(W,H,TextureFormat.RGB24,false,false);try{image.ReadPixels(new Rect(0,0,W,H),0,0);image.Apply(false,false);var pixels=image.GetPixels32();int min=255,max=0;for(int i=0;i<pixels.Length;i+=997){int b=(pixels[i].r+pixels[i].g+pixels[i].b)/3;min=Math.Min(min,b);max=Math.Max(max,b);}Check(max-min>20,"Uniform image");var bytes=image.EncodeToPNG();File.WriteAllBytes(path,bytes);return Hash(bytes);}finally{Object.DestroyImmediate(image);}}
public void Dispose(){RenderTexture.active=previous;if(camHost!=null)Object.DestroyImmediate(camHost);if(host!=null)Object.DestroyImmediate(host);if(target!=null){target.Release();Object.DestroyImmediate(target);}if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);}
}
}
}
