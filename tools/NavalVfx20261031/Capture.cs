// Installed only in the leased validation project; never saved into game scenes.
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
    public static class NavalVfxCapture20261031
    {
        const int Width=1920, Height=1080, Layer=31;
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static int frameCount=24;
        [Serializable] public sealed class Mapping
        {
            public string id,name,school,role,action,mechanic,description,target;
            public int rank,salvoCount,chainBefore;
            public bool isAttack,usesSelectedArea,affectsFields;
        }
        [Serializable] public sealed class Frame { public string file; public float progress,seconds; }
        [Serializable] public sealed class Sequence
        {
            public string id,cardId,cardName,school,role,outcome,video,poster;
            public float duration,contactAt;
            public int salvoCount;
            public Vector2 from,to;
            public List<Frame> frames=new List<Frame>();
            public List<string> keyframes=new List<string>();
        }
        [Serializable] public sealed class Report
        {
            public string scope="Production naval UI and production VFX sampled in disposable Editor PreviewScenes. Card mappings use final production definitions at every legal upgrade level. These frames inspect visual timelines; actual UI commits and resource consumption are verified separately by PlayMode tests.";
            public int width=Width,height=Height,framesPerSequence;
            public List<Sequence> sequences=new List<Sequence>();
            public List<Mapping> mappings=new List<Mapping>();
        }

        public static void CaptureFromCommandLine()
        {
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,"..")).Replace('\\','/');
            if(project!="D:/Game/tmp/shipyard-assembly-20261017")throw new InvalidOperationException("Use leased validation project.");
            if(Application.isPlaying||SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("Stopped editor with graphics device required.");
            string[] args=Environment.GetCommandLineArgs();
            string output=Path.GetFullPath(Argument(args,"-navalVfxOutput"));
            string evidence=Argument(args,"-navalVfxEvidenceSet","validation");
            frameCount=int.Parse(Argument(args,"-navalVfxFrames","24"));
            if(frameCount<24||frameCount>96)throw new ArgumentException("Use 24 to 96 frames.");
            if(!System.Text.RegularExpressions.Regex.IsMatch(evidence,"^[a-zA-Z0-9_-]+$")||
                !output.Replace('\\','/').StartsWith("D:/Game/outputs/naval-vfx-20261031/"+evidence+"/",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Output outside task.");
            Directory.CreateDirectory(output);
            if(Directory.EnumerateFiles(output,"*.jpg").Any()||Directory.EnumerateFiles(output,"*.png").Any())throw new IOException("Preserve existing frames; choose a new run.");
            var report=new Report { framesPerSequence=frameCount };
            var catalog=new CoreVoyageSession();
            var legal=new List<CoreBattleCard>();
            for(int index=0;index<CoreVoyageSession.CardLibrary.Count;index++)
            {
                CoreBattleCard original=CoreVoyageSession.CardLibrary[index];
                if(!original.IsAllowedInStage(NavalBattleStage.Naval))continue;
                legal.Add(original);
                for(int rank=0;rank<=catalog.GetCardMaximumUpgradeLevel(index);rank++)
                {
                    CoreBattleCard card=catalog.GetBattleCardAtLevel(index,rank);
                    if(card==null||card.Id!=original.Id)continue;
                    report.mappings.Add(Map(card,0));
                    if(NavalCardVfxPlan.For(card,2).SalvoCount!=NavalCardVfxPlan.For(card).SalvoCount)report.mappings.Add(Map(card,2));
                }
            }
            // Six authored schools, followed by the eight remaining cast intentions.
            string[] attacks={"card_school_fire_n07","card_school_arcane_b03","card_school_curse_b05","card_school_tide_b03","card_school_thunder_n07","card_school_blood_b03"};
            foreach(BattleSchoolAttackKind school in Enum.GetValues(typeof(BattleSchoolAttackKind)))
            {
                string preferred=attacks[(int)school];
                CoreBattleCard card=legal.FirstOrDefault(c=>c.Id==preferred&&NavalCardVfxPlan.For(c).IsAttack)??
                    legal.First(c=>NavalCardVfxPlan.For(c).IsAttack&&NavalCardVfxPlan.For(c).HasSchool&&NavalCardVfxPlan.For(c).School==school);
                CaptureSequence(output,report,card,school==BattleSchoolAttackKind.Thunder?2:0,BattleSchoolAttackOutcome.Hit);
            }
            string[] supports={"card_school_fire_n03","card_school_arcane_n06","card_emergency_repair","card_load_shot","card_school_tide_n01","card_school_fire_n06","card_school_tide_n05","card_school_tide_n04"};
            foreach(string id in supports)CaptureSequence(output,report,legal.First(c=>c.Id==id),0,BattleSchoolAttackOutcome.Hit);
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
            Debug.Log("Naval VFX capture complete: "+report.sequences.Count+" sequences, "+report.mappings.Count+" final card mappings.");
        }

        static Mapping Map(CoreBattleCard card,int chain)
        {
            NavalCardVfxPlan plan=NavalCardVfxPlan.For(card,chain);
            bool allTideCells=CoreNavalSchoolExpansionCatalog.FindSpec(card.Id)?.Mechanic==CoreNavalExpansionMechanic.GreatWave;
            return new Mapping {id=card.Id,name=card.Name,rank=card.UpgradeLevel,school=plan.HasSchool?plan.School.ToString():"Neutral",
                role=plan.Role.ToString(),action=card.Action.ToString(),mechanic=CoreNavalSchoolExpansionCatalog.FindSpec(card.Id)?.Mechanic.ToString()??card.SchoolEffect?.Mechanic.ToString()??"None",
                description=card.Description,salvoCount=plan.SalvoCount,chainBefore=chain,isAttack=plan.IsAttack,usesSelectedArea=plan.UsesSelectedArea,affectsFields=plan.AffectsFields,
                target=plan.UsesSelectedArea?"Selected cells":allTideCells?"All existing tide cells":plan.Role==NavalVfxRole.Field?"Existing owned school cells":plan.TargetsEnemy?"Enemy ship":"Player ship"};
        }

        static void CaptureSequence(string output,Report report,CoreBattleCard card,int chain,BattleSchoolAttackOutcome outcome)
        {
            Scene scene=default; RenderTexture target=null,previous=RenderTexture.active; Texture2D pixels=null;
            try
            {
                NavalCardVfxPlan plan=NavalCardVfxPlan.For(card,chain);
                scene=EditorSceneManager.NewPreviewScene();
                var host=new GameObject("Disposable Naval VFX Capture");host.SetActive(false);SceneManager.MoveGameObjectToScene(host,scene);
                var controller=host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle,(CoreGameplayPreviewPort)0,0);host.SetActive(true);
                CoreVoyageSession session=controller.Session;
                session.Battle.Stage=NavalBattleStage.Naval;
                session.Battle.DistanceMeters=200f;
                session.Battle.Alignment=NavalBattleAlignment.Parallel;
                session.Battle.Hand.Clear();
                foreach(string id in new[]{card.Id,"card_brace","card_chain_shot","card_emergency_repair"}.Distinct())
                    session.Battle.Hand.Add(Enumerable.Range(0,CoreVoyageSession.CardLibrary.Count).First(i=>CoreVoyageSession.CardLibrary[i].Id==id));
                session.Battle.Energy=session.Battle.MaxEnergy=12;
                session.Cargo.Powder=session.Cargo.Cannonballs=session.Cargo.Wood=20;
                // Real field placement API creates a visible target area for field/area examples.
                VoyagePoint areaWorld=session.Battle.EnemyPositionMeters;
                CoreNavalFieldCell[] areaCells=Array.Empty<CoreNavalFieldCell>();
                if(plan.Role==NavalVfxRole.Field||plan.Role==NavalVfxRole.Area)
                {
                    int x=Mathf.RoundToInt(areaWorld.X/CoreVoyageSession.NavalGridCellMeters),y=Mathf.RoundToInt(areaWorld.Y/CoreVoyageSession.NavalGridCellMeters);
                    CoreNavalFieldKind kind=(CoreNavalFieldKind)(int)plan.School;
                    if(!session.PlaceNavalField(kind,true,x,y,false,1,0))throw new InvalidOperationException("Expected legal in-range field sample.");
                    areaCells=session.Battle.NavalFields.Cells.Where(c=>c.PlayerOwned&&c.Kind==kind).ToArray();
                    if(plan.Role==NavalVfxRole.Area&&plan.UsesSelectedArea)
                    {
                        CoreNavalCardChoice choice=session.GetNavalCardChoice(0);
                        int width=choice?.ShapeWidth??2,height=choice?.ShapeHeight??2;
                        areaCells=areaCells.Where(c=>c.X>=x&&c.X<x+width&&c.Y>=y&&c.Y<y+height).ToArray();
                        if(areaCells.Length!=width*height)throw new InvalidOperationException("Area preview must match the selected card shape.");
                    }
                    areaWorld=new VoyagePoint(areaCells.Average(c=>c.PositionMeters.X),areaCells.Average(c=>c.PositionMeters.Y));
                }
                typeof(CoreGameplayDemoController).GetMethod("Render",Private).Invoke(controller,null);
                Canvas canvas=host.GetComponentsInChildren<Canvas>(true).Single(c=>c.name==CoreGameplayDemoController.EditorPreviewCanvasName);
                RectTransform stage=canvas.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="Battle Card");
                RectTransform player=stage.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="Player Ship Art");
                RectTransform enemy=stage.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="Enemy Ship Art");
                CanvasScaler scaler=canvas.GetComponent<CanvasScaler>();
                var cameraHost=new GameObject("Disposable Naval VFX Camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraHost,scene);
                Camera camera=cameraHost.GetComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                camera.orthographic=true;camera.orthographicSize=5;camera.nearClipPlane=.1f;camera.farClipPlane=2000;camera.transform.position=new Vector3(0,0,-1000);camera.cullingMask=1<<Layer;camera.allowHDR=false;camera.allowMSAA=false;
                target=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);target.Create();camera.targetTexture=target;camera.aspect=Width/(float)Height;
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=100;
                MethodInfo handle=typeof(CanvasScaler).GetMethod("Handle",Private);
                Canvas.ForceUpdateCanvases();handle.Invoke(scaler,null);Canvas.ForceUpdateCanvases();
                Vector2 from=stage.InverseTransformPoint(player.position),to=stage.InverseTransformPoint(plan.TargetsEnemy?enemy.position:player.position);
                if(plan.Role==NavalVfxRole.Field||plan.Role==NavalVfxRole.Area)
                {
                    NavalTacticalBoardView board=stage.GetComponentInChildren<NavalTacticalBoardView>();
                    to=stage.InverseTransformPoint(board.transform.TransformPoint(board.WorldToBoard(areaWorld)));
                }
                var fxGo=new GameObject("Naval FX Capture "+plan.Role,typeof(RectTransform),typeof(NavalCombatFxGraphic));fxGo.transform.SetParent(stage,false);
                NavalCombatFxGraphic fx=fxGo.GetComponent<NavalCombatFxGraphic>();fx.rectTransform.sizeDelta=stage.rect.size;
                fx.Configure(plan.Role,plan.School,plan.HasSchool,outcome,from,to,plan.SalvoCount);
                if(plan.Role==NavalVfxRole.Field||plan.Role==NavalVfxRole.Area)
                {
                    NavalTacticalBoardView board=stage.GetComponentInChildren<NavalTacticalBoardView>();
                    Vector2[] cellTargets=areaCells
                        .Select(c=>(Vector2)stage.InverseTransformPoint(board.transform.TransformPoint(board.WorldToBoard(c.PositionMeters)))).ToArray();
                    fx.ConfigureAreaTargets(cellTargets,board.CellSizePixels);
                }
                string sequenceId=report.sequences.Count.ToString("00")+"_"+plan.Role+"_"+(plan.HasSchool?plan.School.ToString():"Neutral");
                var sequence=new Sequence {id=sequenceId,cardId=card.Id,cardName=card.Name,role=plan.Role.ToString(),school=plan.HasSchool?plan.School.ToString():"Neutral",outcome=outcome.ToString(),duration=fx.Duration,contactAt=fx.ContactAt,salvoCount=plan.SalvoCount,from=from,to=to};
                int startFrame=Mathf.RoundToInt(.15f*(frameCount-1));
                int contactFrame=Mathf.RoundToInt(.45f*(frameCount-1));
                int afterFrame=Mathf.RoundToInt(.70f*(frameCount-1));
                pixels=new Texture2D(Width,Height,TextureFormat.RGB24,false,false);
                for(int i=0;i<frameCount;i++)
                {
                    float progress=i/(float)(frameCount-1);fx.SetProgress(progress);
                    foreach(Transform t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=Layer;
                    for(int pass=0;pass<2;pass++)
                    {
                        Canvas.ForceUpdateCanvases();handle.Invoke(scaler,null);
                        foreach(Text label in canvas.GetComponentsInChildren<Text>(true))label.SetAllDirty();
                        Canvas.ForceUpdateCanvases();
                        if(GraphicsSettings.currentRenderPipeline!=null)RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});else camera.Render();
                    }
                    if(fx.raycastTarget)throw new InvalidOperationException("FX intercepts input.");
                    RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,Width,Height),0,0);pixels.Apply(false,false);
                    string file=sequenceId+"_"+i.ToString("000")+".jpg";
                    File.WriteAllBytes(Path.Combine(output,file),pixels.EncodeToJPG(93));
                    sequence.frames.Add(new Frame{file=file,progress=progress,seconds=progress*fx.Duration});
                    if(i==startFrame||i==contactFrame||i==afterFrame)
                    {
                        string key=sequenceId+"_"+(i==startFrame?"start":i==contactFrame?"contact":"after")+".png";
                        File.WriteAllBytes(Path.Combine(output,key),pixels.EncodeToPNG());sequence.keyframes.Add(key);
                        if(i==contactFrame)sequence.poster=key;
                    }
                }
                report.sequences.Add(sequence);
            }
            finally {RenderTexture.active=previous;if(pixels!=null)Object.DestroyImmediate(pixels);if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);if(target!=null){target.Release();Object.DestroyImmediate(target);}}
        }
        static string Argument(string[] args,string key,string fallback=null)
        {
            int index=Array.IndexOf(args,key);
            if(index>=0&&index+1<args.Length)return args[index+1];
            if(fallback!=null)return fallback;
            throw new ArgumentException("Missing "+key);
        }
    }
}
