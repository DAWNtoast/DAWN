using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace ShatteredSail.Editor {
public static class ResolutionUpgradeCapture {
 [Serializable] public class Report { public string status="running",scope="3840x2160 actual ScreenSpaceCamera/production CanvasScaler; active nontext graphics intersecting screen, no occlusion guarantee. Sliced/tiled/custom graphics require manual review; texture size is actual imported size, source dimensions from separate inventory."; public List<Frame> frames=new List<Frame>(); public List<Row> rows=new List<Row>(); public List<string> errors=new List<string>(); }
 [Serializable] public class Frame { public string state,port; public float canvasScale; public int graphics; }
 [Serializable] public class Row { public string state,port,path,type,mode,asset,texture,format; public int textureWidth,textureHeight; public Rect screenRect,uv; public float displayedWidth,displayedHeight,availableWidth,availableHeight,upscale; public bool repeat,culled; }
 static string output="D:/Game/Artifacts/ResolutionUpgradeAudit20261013/runtime-4k-verified.json";
 static BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
 public static void Run() {
  if(!Application.dataPath.Replace('\\','/').StartsWith("D:/Game/tmp/"))throw new Exception("Use isolated project");
  var report=new Report();
  try {
   var asset=GraphicsSettings.currentRenderPipeline;
   if(asset!=null && RenderPipelineManager.currentPipeline==null) typeof(RenderPipelineManager).GetMethod("TryPrepareRenderPipeline",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,new object[]{asset});
   foreach(string menu in new[]{"Home","Settings"}) Capture(report,menu,CoreGameplayPreviewPort.GrayGull);
   string[] regional={"PortHub","Warehouse","Tavern","ComponentShop","Maintenance","VoyageDeparture"};
   foreach(CoreGameplayPreviewScreen screen in Enum.GetValues(typeof(CoreGameplayPreviewScreen))) {
    IEnumerable<CoreGameplayPreviewPort> ports=regional.Contains(screen.ToString()) ? Enum.GetValues(typeof(CoreGameplayPreviewPort)).Cast<CoreGameplayPreviewPort>() : new[]{CoreGameplayPreviewPort.GrayGull};
    foreach(var port in ports) { Capture(report,screen.ToString(),port); File.WriteAllText(output,JsonUtility.ToJson(report,true)); }
   }
   report.status=report.errors.Count==0?"passed":"partial";
  } catch(Exception ex){report.errors.Add(ex.ToString());report.status="failed";}
  File.WriteAllText(output,JsonUtility.ToJson(report,true));
  Debug.Log("RESOLUTION_AUDIT "+report.status+" frames="+report.frames.Count+" rows="+report.rows.Count);
 }
 static void Capture(Report report,string state,CoreGameplayPreviewPort port) {
  Scene scene=default;RenderTexture rt=null;
  try {
   scene=EditorSceneManager.NewPreviewScene(); var host=new GameObject("ResolutionAudit") {hideFlags=HideFlags.HideAndDontSave};host.SetActive(false);SceneManager.MoveGameObjectToScene(host,scene);
   bool menu=state=="Home"||state=="Settings";
   if(menu)host.AddComponent<MainMenuController>(); else host.AddComponent<CoreGameplayDemoController>().ConfigureEditorPreview((CoreGameplayPreviewScreen)Enum.Parse(typeof(CoreGameplayPreviewScreen),state),port,0);
   host.SetActive(true);
   Canvas canvas=host.GetComponentsInChildren<Canvas>(true).Single(c=>c.name==(menu?MainMenuController.EditorPreviewCanvasName:CoreGameplayDemoController.EditorPreviewCanvasName));
   if(menu)foreach(Transform c in canvas.GetComponentsInChildren<Transform>(true)) {
    if(c.name=="Settings Panel")c.gameObject.SetActive(state=="Settings");
    if(c.name=="Achievements Panel"||c.name=="New Game Confirmation")c.gameObject.SetActive(false);
   }
   foreach(Transform c in canvas.GetComponentsInChildren<Transform>(true))c.gameObject.layer=30;
   var cameraHost=new GameObject("AuditCamera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraHost,scene);var camera=cameraHost.GetComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.orthographic=true;camera.orthographicSize=5;camera.transform.position=new Vector3(0,0,-1000);camera.nearClipPlane=.1f;camera.farClipPlane=2000;camera.cullingMask=1<<30;camera.allowHDR=false;camera.allowMSAA=false;
   rt=new RenderTexture(3840,2160,24,RenderTextureFormat.ARGB32);rt.Create();camera.targetTexture=rt;camera.aspect=3840f/2160;
   canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=100;
   CanvasScaler scaler=canvas.GetComponent<CanvasScaler>();
   for(int i=0;i<3;i++){Canvas.ForceUpdateCanvases();typeof(CanvasScaler).GetMethod("Handle",flags).Invoke(scaler,null);Canvas.ForceUpdateCanvases();if(GraphicsSettings.currentRenderPipeline!=null)RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});else camera.Render();}
   if(Mathf.Abs(canvas.pixelRect.width-3840)>.1f||Mathf.Abs(canvas.scaleFactor-2)>.001f)throw new Exception("Canvas target mismatch");
   var frame=new Frame{state=state,port=port.ToString(),canvasScale=canvas.scaleFactor};report.frames.Add(frame);
   foreach(Graphic graphic in canvas.GetComponentsInChildren<Graphic>(false)) {
    if(graphic is Text||!graphic.enabled||graphic.GetComponent<CanvasRenderer>()==null||graphic.color.a<=.001f||graphic.canvasRenderer.GetInheritedAlpha()<=.001f)continue;
    Texture tex=graphic.mainTexture;if(tex==null||tex.width<=4||tex.height<=4)continue;
    Mesh mesh=graphic.canvasRenderer.GetMesh();if(mesh==null||mesh.vertexCount<3)continue;
    var points=mesh.vertices.Select(v=>(Vector2)camera.WorldToScreenPoint(graphic.transform.TransformPoint(v))).ToArray();Rect screen=Bounds(points);if(!screen.Overlaps(new Rect(0,0,3840,2160)))continue;
    var uvs=mesh.uv;Rect uv=Bounds(uvs);var row=new Row{state=state,port=port.ToString(),path=PathOf(graphic.transform,canvas.transform),type=graphic.GetType().Name,mode=graphic is Image img?img.type.ToString():graphic is RawImage?"RawImage":"Custom",asset=AssetDatabase.GetAssetPath(tex),texture=tex.name,textureWidth=tex.width,textureHeight=tex.height,format=tex is Texture2D t?t.format.ToString():"",screenRect=screen,uv=uv,displayedWidth=screen.width,displayedHeight=screen.height,availableWidth=tex.width*Mathf.Abs(uv.width),availableHeight=tex.height*Mathf.Abs(uv.height),culled=graphic.canvasRenderer.cull};
    row.repeat=row.mode=="Sliced"||row.mode=="Tiled"||uv.width>1.001f||uv.height>1.001f;
    row.upscale=Mathf.Max(screen.width/Mathf.Max(1,row.availableWidth),screen.height/Mathf.Max(1,row.availableHeight));report.rows.Add(row);frame.graphics++;
   }
  }catch(Exception ex){report.errors.Add(state+"/"+port+": "+ex);}
  finally{if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);if(rt!=null){rt.Release();Object.DestroyImmediate(rt);}}
 }
 static string PathOf(Transform t,Transform root){string s=t.name;while(t.parent!=null&&t.parent!=root){t=t.parent;s=t.name+"/"+s;}return s;}
 static Rect Bounds(IEnumerable<Vector2> ps){var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);foreach(var p in ps){min=Vector2.Min(min,p);max=Vector2.Max(max,p);}return new Rect(min,max-min);}
}}
