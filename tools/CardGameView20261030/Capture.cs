// Task-only exporter. This calls the production Unity card factory and native UI renderer.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace ShatteredSail.Editor
{
    public static class CardGameViewCapture
    {
        [Serializable] public sealed class Row { public string Id,Name,Stage,StageId,ArtResourcePath,FrameResourcePath,DefaultFrameResourcePath,Effect; public int Level,Index; }
        [Serializable] public sealed class Plan { public int CardCount,StageCount,RowCount; public Row[] Cards; }
        [Serializable] public sealed class Config { public string Output,Evidence,InputFingerprint,Company,Product; public int Limit; }
        [Serializable] public sealed class ImageRow { public string Id,Name,Stage,StageId,Image,ImageSha256,FrameResourcePath,ArtResourcePath,Effect; public int Level,Width,Height; public float DesignWidth,DesignHeight; }
        [Serializable] public sealed class Report { public string Method,InputFingerprint,ReferenceResolution,Scope,GeneratedUtc; public int CardCount,StageCount,RowCount,PixelsPerDesignUnit; public ImageRow[] Cards; }
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private const int Width=592,Height=992,Layer=30;
        private const float DesignWidth=296,DesignHeight=496;
        private static string Hash(byte[] bytes){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        private static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}

        public static void Run()
        {
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,"..")).Replace('\\','/');
            Require(System.Text.RegularExpressions.Regex.IsMatch(project,@"/tmp/card-game-view-20261030-[a-f0-9]{32}$"),"Only the owned export project is allowed.");
            Require(File.Exists(Path.Combine(project,"OWNED_CARD_GAME_VIEW.txt")),"Ownership marker is missing.");
            Config config=JsonUtility.FromJson<Config>(File.ReadAllText(Path.Combine(project,"capture-config.json")));
            Require(Application.companyName==config.Company&&Application.productName==config.Product,"Independent player-preference identity differs.");
            Plan plan=JsonUtility.FromJson<Plan>(File.ReadAllText(Path.Combine(project,"capture-plan.json")));
            Require(Resources.Load<Font>(GameTypography.FontResourcePath)!=null&&GameTypography.InterfaceFont==Resources.Load<Font>(GameTypography.FontResourcePath),"Actual title font is missing.");
            Require(Resources.Load<Font>(GameTypography.BodyFontResourcePath)!=null,"Actual body font is missing.");
            Directory.CreateDirectory(Path.Combine(config.Output,"game-view"));
            var rows=new List<ImageRow>();
            Scene scene=EditorSceneManager.NewPreviewScene();
            var host=new GameObject("Inactive capture controller");host.SetActive(false);SceneManager.MoveGameObjectToScene(host,scene);
            var controller=host.AddComponent<CoreGameplayDemoController>();
            var session=new CoreVoyageSession();
            typeof(CoreGameplayDemoController).GetField("session",Private).SetValue(controller,session);
            MethodInfo create=typeof(CoreGameplayDemoController).GetMethod("CreateCollectionCardFace",Private);
            MethodInfo refresh=typeof(CoreGameplayDemoController).GetMethod("RefreshCardSupplyCost",Private);
            var canvasHost=new GameObject("Production Card Canvas",typeof(RectTransform),typeof(Canvas));SceneManager.MoveGameObjectToScene(canvasHost,scene);
            Canvas canvas=canvasHost.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            // Match glyph rasterization density to the 2x render target while
            // keeping every production RectTransform and font size unchanged.
            canvas.scaleFactor=2f;
            canvasHost.GetComponent<RectTransform>().sizeDelta=new Vector2(DesignWidth,DesignHeight);
            var cameraHost=new GameObject("Card capture camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraHost,scene);
            Camera camera=cameraHost.GetComponent<Camera>();camera.enabled=false;camera.scene=scene;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.075f,.09f,.10f,1);
            camera.orthographic=true;camera.orthographicSize=DesignHeight*.5f;camera.nearClipPlane=.1f;camera.farClipPlane=2000f;
            camera.transform.position=new Vector3(0,0,-1000);camera.cullingMask=1<<Layer;camera.allowHDR=camera.allowMSAA=false;camera.aspect=DesignWidth/DesignHeight;
            canvas.worldCamera=camera;
            var target=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);target.Create();camera.targetTexture=target;
            var pixels=new Texture2D(Width,Height,TextureFormat.RGB24,false);
            RenderTexture previous=RenderTexture.active;
            try
            {
                foreach(Row row in plan.Cards.Take(config.Limit>0?config.Limit:int.MaxValue))
                {
                    NavalBattleStage stage=(NavalBattleStage)Enum.Parse(typeof(NavalBattleStage),row.StageId);
                    typeof(CoreGameplayDemoController).GetField("selectedBattleDeckStage",Private).SetValue(controller,stage);
                    CoreBattleCard card=session.GetBattleCardAtLevel(row.Index,row.Level);
                    if(stage==NavalBattleStage.Boarding)card=CoreBoardingCardCatalog.ForBoarding(card);
                    Require(card.Id==row.Id,"Card identity changed: "+row.Id);
                    Require(CardSchoolArtCatalog.FrameResourcePath(card,stage)==row.FrameResourcePath,"Frame mapping changed: "+row.Id);
                    Require(CardSchoolArtCatalog.DedicatedIllustrationPath(card)==row.ArtResourcePath,"Art mapping changed: "+row.Id);
                    Require(Resources.Load<Texture2D>(row.ArtResourcePath)!=null,"Actual artwork missing: "+row.ArtResourcePath);
                    Require(CardSchoolArtCatalog.Frame(card,stage)!=null,"Actual complete frame missing: "+row.FrameResourcePath);
                    string effect=session.BattleCardFaceDescription(card,stage);
                    Require(effect==row.Effect,"Effect changed: "+row.Id+"/"+row.Level);
                    RectTransform face=(RectTransform)create.Invoke(controller,new object[]{canvas.transform,card,"Capture","Card Art","Card Title",CardFaceLayout.DeckPreviewScale,effect,"Card Effect"});
                    try
                    {
                        face.anchoredPosition=Vector2.zero;
                        refresh.Invoke(controller,new object[]{face,card,null,stage});
                        var frame=face.GetComponentInChildren<CardSchoolFrameGraphic>();
                        Require(frame.HasAuthoredTexture,"No authored frame on "+row.Id);
                        foreach(Transform child in canvas.GetComponentsInChildren<Transform>(true))child.gameObject.layer=Layer;
                        for(int pass=0;pass<3;pass++)
                        {
                            foreach(Text text in face.GetComponentsInChildren<Text>(true))text.SetAllDirty();
                            Canvas.ForceUpdateCanvases();camera.Render();
                        }
                        foreach(Text text in face.GetComponentsInChildren<Text>())
                            Require(string.IsNullOrWhiteSpace(text.text)||text.cachedTextGenerator.vertexCount>=4,"Missing visible text: "+row.Id+" / "+text.name);
                        RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,Width,Height),0,0);pixels.Apply();
                        byte[] png=pixels.EncodeToPNG();Require(png.Length>10000,"Screenshot unexpectedly empty: "+row.Id);
                        string image="game-view/"+row.Id+"-"+row.StageId.ToLowerInvariant()+"-"+row.Level+".png";
                        File.WriteAllBytes(Path.Combine(config.Output,image),png);
                        rows.Add(new ImageRow{Id=row.Id,Name=row.Name,Stage=row.Stage,StageId=row.StageId,Level=row.Level,Image=image,
                            ImageSha256=Hash(png),Width=Width,Height=Height,DesignWidth=DesignWidth,DesignHeight=DesignHeight,
                            ArtResourcePath=row.ArtResourcePath,FrameResourcePath=row.FrameResourcePath,Effect=effect});
                        if(rows.Count%25==0)Debug.Log("CARD_GAME_VIEW_PROGRESS "+rows.Count+"/"+plan.RowCount);
                    }
                    finally {Object.DestroyImmediate(face.gameObject);}
                }
                Require(config.Limit>0||rows.Count==plan.RowCount,"Missing capture rows.");
                var report=new Report{Method="Production Unity CreateCollectionCardFace + RefreshCardSupplyCost; native Canvas and camera rendering; no HTML reconstruction.",
                    InputFingerprint=config.InputFingerprint,ReferenceResolution="1920x1080",Scope="Standard deck/workshop card face at DeckPreviewScale=1, no temporary battle buffs; screenshot includes 8 design-unit framing margin.",
                    GeneratedUtc=DateTime.UtcNow.ToString("o"),CardCount=rows.Select(r=>r.Id).Distinct().Count(),StageCount=rows.Select(r=>r.Id+"/"+r.StageId).Distinct().Count(),
                    RowCount=rows.Count,PixelsPerDesignUnit=2,Cards=rows.ToArray()};
                string filename=config.Limit>0?"sample-card-game-view-data.json":"card-game-view-data.json";
                File.WriteAllText(Path.Combine(config.Output,filename),JsonUtility.ToJson(report,true));
                File.WriteAllText(Path.Combine(config.Evidence,"capture-validation.txt"),"PASS "+rows.Count+" actual Unity card faces; current art/frame/font/effect checked for every row.\n");
                Debug.Log("CARD_GAME_VIEW_CAPTURE_OK "+rows.Count);
            }
            finally
            {
                RenderTexture.active=previous;camera.targetTexture=null;target.Release();Object.DestroyImmediate(pixels);Object.DestroyImmediate(target);
                Object.DestroyImmediate(canvasHost);Object.DestroyImmediate(cameraHost);Object.DestroyImmediate(host);EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
