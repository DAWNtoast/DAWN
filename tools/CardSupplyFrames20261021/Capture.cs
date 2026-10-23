// Task-only verification. Copy to an isolated project's Editor directory.
// Builds production card faces in memory; no scene, profile or asset writes.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
    public static class CardSupplyFrameCapture
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const int Layer = 30;
        [Serializable] private sealed class CardRecord
        {
            public string id, name, school, stage, frame, special, supply, illustration;
            public int rank, energy, powder, cannonballs;
            public bool frameAvailable;
            public List<string> supplyObjects = new List<string>(), supplyNumbers = new List<string>();
        }
        [Serializable] private sealed class Report
        {
            public string generatedUtc = DateTime.UtcNow.ToString("O");
            public string scope = "Production CreateCollectionCardFace, complete active catalog at every authored rank and allowed stage; temporary preview scene, no profile or scene saves.";
            public int activeCards, checks;
            public List<CardRecord> catalog = new List<CardRecord>();
            public List<string> errors = new List<string>(), captures = new List<string>();
        }
        public static void CaptureFromCommandLine()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stopped editor required.");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (!project.StartsWith("D:/Game/tmp/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Use an isolated project below D:/Game/tmp.");
            string[] arguments = Environment.GetCommandLineArgs();
            int key = Array.IndexOf(arguments, "-cardSupplyOutput");
            if (key < 0 || key + 1 >= arguments.Length) throw new ArgumentException("Pass -cardSupplyOutput.");
            string output = Path.GetFullPath(arguments[key + 1]);
            if (!output.Replace('\\', '/').StartsWith("D:/Game/Artifacts/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Output must be below D:/Game/Artifacts.");
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Preserve existing evidence: use a new output directory.");
            Directory.CreateDirectory(output);
            var report = new Report();
            Scene scene = EditorSceneManager.NewPreviewScene();
            GameObject host = null;
            try
            {
                host = new GameObject("Disposable Supply Frame Audit");
                host.SetActive(false); SceneManager.MoveGameObjectToScene(host, scene);
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle, CoreGameplayPreviewPort.GrayGull, 0);
                host.SetActive(true);
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).First(c => c.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                foreach (Transform child in canvas.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                CoreVoyageSession session = controller.Session;
                session.Cargo.Wood = session.Cargo.Medicine = 20;
                typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, session.MaxHull - 20);
                if (session.Battle != null)
                    session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth - 20;
                int[] active = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                    .Where(i => CoreVoyageSession.CardLibrary[i].AllowedStages != NavalBattleStages.None).ToArray();
                report.activeCards = active.Length;
                foreach (int rank in new[] { 0, 1, 2, 3 })
                {
                    foreach (int index in active)
                    foreach (NavalBattleStage stage in (NavalBattleStage[])Enum.GetValues(typeof(NavalBattleStage)))
                    {
                        if (rank > session.GetCardMaximumUpgradeLevel(index)) continue;
                        CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                        if (!card.IsAllowedInStage(stage)) continue;
                        if (session.Battle != null) session.Battle.Stage = stage;
                        if (stage == NavalBattleStage.Boarding) card = CoreBoardingCardCatalog.ForBoarding(card);
                        string supply = CardSupplyCostPresentation.ResourceType(card, stage);
                        string special = CardSpecialCostPresentation.ResourceType(card);
                        var record = new CardRecord { id = card.Id, name = card.Name, school = card.SchoolId,
                            stage = stage.ToString(), supply = supply,
                            rank = card.UpgradeLevel, energy = card.Energy, powder = card.PowderCost,
                            cannonballs = card.CannonballCost, special = special,
                            illustration = CardSchoolArtCatalog.DedicatedIllustrationPath(card),
                            frame = CardSchoolArtCatalog.FrameResourcePath(card, stage),
                            frameAvailable = CardSchoolArtCatalog.Frame(card, stage) != null };
                        if (!record.frameAvailable) report.errors.Add(card.Id + " rank " + rank + " missing frame: " + record.frame);
                        RectTransform face = CreateFace(controller, canvas.transform, card, stage);
                        CardSchoolFrameGraphic renderedFrame = face.GetComponentInChildren<CardSchoolFrameGraphic>();
                        Texture2D expectedFrame = CardSchoolArtCatalog.Frame(card, stage);
                        if (renderedFrame == null || !renderedFrame.HasAuthoredTexture || renderedFrame.mainTexture != expectedFrame)
                            report.errors.Add(card.Id + " " + stage + " rendered frame differs from its required cost combination");
                        CardSupplyCostView supplyView = face.GetComponentInChildren<CardSupplyCostView>();
                        if (supply.Length > 0 && (supplyView == null || supplyView.ResourceType != supply))
                            report.errors.Add(card.Id + " " + stage + " expected supply " + supply);
                        if (supplyView != null && supplyView.Amount.text != CardSupplyCostPresentation.BaseCost(card, stage).ToString())
                            report.errors.Add(card.Id + " " + stage + " incorrect static supply amount " + supplyView.Amount.text);
                        if (supplyView != null && supplyView.GetComponentInChildren<RawImage>(true).enabled == CardSchoolArtCatalog.HasBakedSupplyIcon(card, stage))
                            report.errors.Add(card.Id + " " + stage + " supply icon does not match the baked frame");
                        if (supply.Length > 0 && (record.frame.EndsWith("-supply", StringComparison.Ordinal) ||
                            !CardSchoolArtCatalog.HasBakedSupplyIcon(card, stage)))
                            report.errors.Add(card.Id + " " + stage + " did not select a resource-specific complete frame");
                        if (supplyView != null && (supplyView.Amount.fontSize != 24 ||
                            supplyView.Amount.rectTransform.anchoredPosition != Vector2.zero ||
                            ((RectTransform)supplyView.transform).anchoredPosition != CardSchoolArtCatalog.SupplyPosition(card, stage)))
                            report.errors.Add(card.Id + " " + stage + " supply amount is not centered on the painted icon");
                        if (supply.Length == 0 && supplyView != null)
                            report.errors.Add(card.Id + " " + stage + " has an unnecessary supply badge");
                        foreach (Transform part in face.GetComponentsInChildren<Transform>(true))
                            if (part.name.IndexOf("Supply", StringComparison.OrdinalIgnoreCase) >= 0 && part.gameObject.activeInHierarchy)
                            {
                                record.supplyObjects.Add(part.name);
                                Text label = part.GetComponent<Text>();
                                if (label != null) record.supplyNumbers.Add(label.text);
                            }
                        foreach (RawImage item in face.GetComponentsInChildren<RawImage>(false))
                            if (item.name.IndexOf("Supply", StringComparison.OrdinalIgnoreCase) >= 0 && item.texture == null)
                                report.errors.Add(card.Id + " missing supply texture: " + item.name);
                        report.catalog.Add(record); report.checks++;
                        Object.DestroyImmediate(face.gameObject);
                    }
                }
                session.Battle.Stage = NavalBattleStage.Naval;
                CoreBattleCard repair = session.GetBattleCardAtLevel(active.Single(i => CoreVoyageSession.CardLibrary[i].Id == "card_emergency_repair"), 0);
                RectTransform repairFace = CreateFace(controller, canvas.transform, repair, NavalBattleStage.Naval);
                repairFace.localScale = Vector3.one * 2.4f;
                repairFace.anchoredPosition = -CardSchoolArtCatalog.FrameBounds(repair, NavalBattleStage.Naval).center * 2.4f;
                Capture(canvas, scene, Path.Combine(output, "emergency-repair.png"), 900, 1250);
                report.captures.Add("emergency-repair.png");
                Object.DestroyImmediate(repairFace.gameObject);
                session.Battle.Stage = NavalBattleStage.Boarding;
                // Use the same bandaging illustration as the neutral-medicine
                // gallery example. The newer basic_06 identity deliberately
                // reuses emergency-repair art in the production catalog.
                CoreBattleCard medicine = session.GetBattleCardAtLevel(active.Single(i => CoreVoyageSession.CardLibrary[i].Id == "card_crew_bandage"), 0);
                medicine = CoreBoardingCardCatalog.ForBoarding(medicine);
                RectTransform medicineFace = CreateFace(controller, canvas.transform, medicine, NavalBattleStage.Boarding);
                medicineFace.localScale = Vector3.one * 2.4f;
                medicineFace.anchoredPosition = -CardSchoolArtCatalog.FrameBounds(medicine, NavalBattleStage.Boarding).center * 2.4f;
                Capture(canvas, scene, Path.Combine(output, "field-bandage.png"), 900, 1250);
                report.captures.Add("field-bandage.png");
                Object.DestroyImmediate(medicineFace.gameObject);
                string[] supplyFrameIds = { "frame-neutral-ap-wood", "frame-neutral-ap-medicine", "frame-fire-powder-medicine",
                    "frame-arcane-ap-mana-medicine", "frame-curse-ap-curse-medicine", "frame-tide-ap-tide-medicine",
                    "frame-blood-ap-medicine", "frame-tide-ap-wood", "frame-arcane-ap-mana-wood" };
                for (int cell = 0; cell < supplyFrameIds.Length; cell++)
                {
                    CardRecord example = report.catalog.First(c => c.frame.EndsWith("/" + supplyFrameIds[cell], StringComparison.Ordinal));
                    int index = active.First(i => CoreVoyageSession.CardLibrary[i].Id == example.id);
                    NavalBattleStage stage = (NavalBattleStage)Enum.Parse(typeof(NavalBattleStage), example.stage);
                    session.Battle.Stage = stage;
                    CoreBattleCard data = session.GetBattleCardAtLevel(index, example.rank);
                    if (stage == NavalBattleStage.Boarding) data = CoreBoardingCardCatalog.ForBoarding(data);
                    RectTransform face = CreateFace(controller, canvas.transform, data, stage);
                    Vector2 center = new Vector2((cell % 3 - 1) * 420f, (1 - cell / 3) * 640f);
                    face.localScale = Vector3.one * 1.3f;
                    face.anchoredPosition = center - CardSchoolArtCatalog.FrameBounds(data, stage).center * 1.3f;
                    Text title = face.GetComponentsInChildren<Text>().First(t => t.name == "Audit Title");
                    RectTransform caption = new GameObject("Integrated Supply Caption", typeof(RectTransform)).GetComponent<RectTransform>();
                    caption.SetParent(canvas.transform, false); caption.sizeDelta = new Vector2(410, 50);
                    caption.anchoredPosition = center + new Vector2(0, -300);
                    Text label = caption.gameObject.AddComponent<Text>(); label.font = title.font; label.fontSize = 22;
                    label.alignment = TextAnchor.MiddleCenter; label.color = Color.white;
                    label.text = (example.supply == "wood" ? "木板" : "药品") + " · " + example.name;
                }
                Capture(canvas, scene, Path.Combine(output, "integrated-supply-gallery.png"), 1300, 1940);
                report.captures.Add("integrated-supply-gallery.png");
                foreach (Transform child in canvas.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                List<CardRecord> examples = new List<CardRecord>();
                foreach (string id in new[] { "card_emergency_repair", "card_load_shot", "card_chain_shot" })
                    examples.Add(report.catalog.First(c => c.id == id && c.rank == 0 && c.stage == "Naval"));
                examples.AddRange(report.catalog.Where(c => c.rank == 0 && c.supply.Length > 0)
                    .GroupBy(c => c.school + "/" + c.special + "/" + c.supply).Select(c => c.First()));
                examples = examples.GroupBy(c => c.id + "/" + c.stage).Select(c => c.First()).Take(16).ToList();
                for (int offset = 0; offset < examples.Count; offset += 4)
                {
                    CardRecord[] selected = examples.Skip(offset).Take(4).ToArray();
                    for (int column = 0; column < selected.Length; column++)
                    {
                        CardRecord example = selected[column];
                        int index = active.First(i => CoreVoyageSession.CardLibrary[i].Id == example.id);
                        NavalBattleStage stage = (NavalBattleStage)Enum.Parse(typeof(NavalBattleStage), example.stage);
                        if (session.Battle != null) session.Battle.Stage = stage;
                        CoreBattleCard data = session.GetBattleCardAtLevel(index, 0);
                        if (stage == NavalBattleStage.Boarding) data = CoreBoardingCardCatalog.ForBoarding(data);
                        RectTransform face = CreateFace(controller, canvas.transform, data, stage);
                        face.anchoredPosition = new Vector2(-570 + column * 380, 0);
                        face.localScale = Vector3.one * 1.3f;
                        Text title = face.GetComponentsInChildren<Text>().First(t => t.name == "Audit Title");
                        RectTransform caption = new GameObject("Capture Caption", typeof(RectTransform)).GetComponent<RectTransform>();
                        caption.SetParent(canvas.transform, false); caption.sizeDelta = new Vector2(360, 50);
                        caption.anchoredPosition = new Vector2(-570 + column * 380, -365);
                        Text label = caption.gameObject.AddComponent<Text>(); label.font = title.font; label.fontSize = 21;
                        label.alignment = TextAnchor.MiddleCenter; label.color = Color.white;
                        label.text = (stage == NavalBattleStage.Boarding ? "白刃战" : stage == NavalBattleStage.Naval ? "炮战" : "准备") +
                            (example.supply == "wood" ? " · 木板" : example.supply == "medicine" ? " · 药品" : "");
                    }
                    string filename = "card-supply-" + (offset / 4 + 1) + ".png";
                    Capture(canvas, scene, Path.Combine(output, filename), 1600, 900);
                    foreach (CardSupplyCostView view in canvas.GetComponentsInChildren<CardSupplyCostView>())
                        if (view.Amount.cachedTextGenerator.vertexCount < 4)
                            report.errors.Add("Supply amount produced no visible glyphs: " + view.transform.parent.name + " = " + view.Amount.text);
                    report.captures.Add(filename);
                    foreach (Transform child in canvas.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                }
                string[] battleIds = { "card_emergency_repair", "card_boarding_basic_06", "card_boarding_fire_05", "card_boarding_arcane_05" };
                for (int column = 0; column < battleIds.Length; column++)
                {
                    int index = active.Single(i => CoreVoyageSession.CardLibrary[i].Id == battleIds[column]);
                    CoreBattleCard card = CoreVoyageSession.CardLibrary[index];
                    session.Battle.Stage = card.IsAllowedInStage(NavalBattleStage.Boarding) ? NavalBattleStage.Boarding : NavalBattleStage.Naval;
                    session.Battle.Hand.Clear(); session.Battle.Hand.Add(index); session.Battle.Energy = 10;
                    session.Cargo.Powder = session.Cargo.Cannonballs = 20;
                    object view = typeof(CoreGameplayDemoController).GetMethod("CreateRetainedBattleHandCard", Private)
                        .Invoke(controller, new object[] { canvas.transform, session.Battle, 0 });
                    RectTransform face = ((Button)view.GetType().GetField("Button").GetValue(view)).GetComponent<RectTransform>();
                    face.anchoredPosition = new Vector2(-570 + column * 380, 0); face.localScale = Vector3.one * 1.3f;
                }
                Capture(canvas, scene, Path.Combine(output, "battle-supply.png"), 1600, 900);
                report.captures.Add("battle-supply.png");
                foreach (CardSupplyCostView view in canvas.GetComponentsInChildren<CardSupplyCostView>())
                    if (view.Amount.cachedTextGenerator.vertexCount < 4)
                        report.errors.Add("Battle supply amount produced no visible glyphs: " + view.transform.parent.name);
            }
            catch (Exception exception) { report.errors.Add(exception.ToString()); }
            finally
            {
                if (host != null) Object.DestroyImmediate(host);
                EditorSceneManager.ClosePreviewScene(scene);
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            }
            if (report.errors.Count > 0) throw new InvalidOperationException("Supply frame audit failed; inspect report.json: " + report.errors.Count);
            Debug.Log("CARD_SUPPLY_FRAME_AUDIT_OK: " + report.checks + " catalog checks, " + report.captures.Count + " captures.");
        }
        private static RectTransform CreateFace(CoreGameplayDemoController controller, Transform parent, CoreBattleCard card, NavalBattleStage stage)
        {
            var face = (RectTransform)typeof(CoreGameplayDemoController).GetMethod("CreateCollectionCardFace", Private)
                .Invoke(controller, new object[] { parent, card, "Audit " + card.Id, "Audit Art", "Audit Title", 1f, null, null });
            typeof(CoreGameplayDemoController).GetMethod("RefreshCardSupplyCost", Private)
                .Invoke(controller, new object[] { face, card, null, stage });
            return face;
        }
        private static void Capture(Canvas canvas, Scene scene, string path, int width, int height)
        {
            GameObject cameraHost = new GameObject("Disposable Supply Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraHost, scene);
            Camera camera = cameraHost.GetComponent<Camera>();
            camera.enabled = false; camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.08f, .105f, .13f, 1f); camera.orthographic = true;
            camera.orthographicSize = height * .5f; camera.nearClipPlane = .1f; camera.farClipPlane = 2000;
            camera.transform.position = new Vector3(0, 0, -1000); camera.cullingMask = 1 << Layer;
            camera.allowHDR = camera.allowMSAA = false; camera.aspect = width / (float)height;
            RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                target.Create(); camera.targetTexture = target;
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>(); if (scaler != null) scaler.enabled = false;
                canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
                RectTransform rect = canvas.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(width, height);
                rect.position = Vector3.zero; rect.rotation = Quaternion.identity; rect.localScale = Vector3.one;
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = Layer;
                for (int pass = 0; pass < 3; pass++)
                {
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                RenderTexture.active = target; pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply(); File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; if (pixels != null) Object.DestroyImmediate(pixels);
                camera.targetTexture = null; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(cameraHost);
            }
        }
    }
}
