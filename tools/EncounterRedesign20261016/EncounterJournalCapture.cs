// Temporary Editor-only entry. Install only in the isolated validation project.
// The renderer retains the production CanvasScaler and actual render-target geometry.
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
using MenuFrame = ShatteredSail.Editor.UiAuditReadOnlyCapture.MenuFrame;
using TextGeometry = ShatteredSail.Editor.UiAuditReadOnlyCapture.TextGeometry;

namespace ShatteredSail.Editor
{
    public static class EncounterJournalCapture
    {
        private const int Layer = 31;
        private static readonly string[] States = {
            "GuidedOptional", "Mandatory", "CrewLocked", "ManyChoicesTop", "ManyChoicesBottom", "TreasureCoreLocked"
        };
        public static void CaptureFromCommandLine()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stopped Editor required.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Graphics required; omit -nographics.");
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-uiAuditPath");
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
                throw new ArgumentException("Pass -uiAuditPath NEW_ABSOLUTE_OUTPUT_DIR.");
            string output = Path.GetFullPath(args[index + 1]);
            if (!output.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Any(part => string.Equals(part, "Artifacts", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Captures must be below an Artifacts directory.");
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Use a new or empty directory; existing captures are preserved.");
            Directory.CreateDirectory(output);
            var report = new UiAuditReadOnlyCapture.MenuReport {
                capturedAtUtc = DateTime.UtcNow.ToString("O"),
                scope = "Disposable production journal encounters with authored events: guided optional supply crate, mandatory reef passage, locked navigator note, four treasure choices at top and bottom with a fixed skip action, locked treasure core. " +
                    "In-memory fixture staging only; no choice callbacks, no player saves, no scene regeneration, no performance measurement. " +
                    "Treasure chapter UI is closed without advancing the story so that the authored encounter remains visible; voyage state is unchanged by this UI-only suppression. " +
                    "Four render targets use actual enabled production CanvasScaler and ScreenSpaceCamera geometry. " +
                    "Validates two visible voyage panels, hidden sailing controls, fixed secondary actions, independently scrolling narrative, readable requirements, and the scrolled last ordinary choice. " +
                    "Text geometry includes masked off-viewport choices; it is not an occlusion-aware accessibility result."
            };
            foreach (string state in States)
                foreach (Vector2Int size in new[] { new Vector2Int(1920,1080), new Vector2Int(1280,720),
                    new Vector2Int(1280,1024), new Vector2Int(3440,1440) })
                {
                    var frame = new MenuFrame { state=state, width=size.x, height=size.y, file=state+"_"+size.x+"x"+size.y+".png" };
                    report.frames.Add(frame);
                    try { CaptureMenu(output, frame); }
                    catch (Exception exception) {
                        Exception actual = exception is TargetInvocationException && exception.InnerException != null ? exception.InnerException : exception;
                        frame.error=actual.ToString(); Debug.LogWarning(frame.file+": "+actual.Message);
                    }
                    File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
                }
            if (report.frames.Count != 24 || report.frames.Any(frame => !frame.verifiedScaler || !string.IsNullOrEmpty(frame.error)))
                throw new InvalidOperationException("Encounter journal capture failed; inspect report.json.");
            Debug.Log("ENCOUNTER_JOURNAL_CAPTURE_OK: 24 verified production UI captures at "+output);
        }

        private static void PrepareEncounter(CoreGameplayDemoController controller, string state)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Preview-only arrangement.");
            if (state == "TreasureCoreLocked") {
                controller.ConfigureEditorFlowState(GameplayFlowPreviewState.TreasureCoreLocked);
                return;
            }
            if (state.StartsWith("ManyChoices", StringComparison.Ordinal))
                controller.ConfigureEditorFlowState(GameplayFlowPreviewState.TreasureClue);
            CoreVoyageSession session = controller.Session;
            SetField(controller, "editorPreviewBuilding", true);
            try {
                CoreMapNode node;
                if (state == "GuidedOptional" || state == "CrewLocked") {
                    SetProperty(session, "Phase", CoreDemoPhase.Port);
                    session.ConfigureCrewCapabilities(false, false, false, false, false, false);
                    if (!session.ConfigureGuidedTutorialVoyage(1)) throw new InvalidOperationException("Guided fixture setup failed.");
                    session.Cargo.Water = 100; session.Cargo.Food = 100;
                    session.StartVoyage();
                    string id = state == "GuidedOptional" ? CoreGuidedTutorialContent.OptionalEventId : CoreGuidedTutorialContent.CrewRequirementEventId;
                    node = session.Map.Nodes.Single(item => item.Encounter.Id == id);
                    if (state == "GuidedOptional") {
                        SetField(controller, "tutorialActive", true);
                        SetField(controller, "guidedTutorialStep", GuidedTutorialStep.FirstOptional);
                    }
                } else if (state.StartsWith("ManyChoices", StringComparison.Ordinal)) {
                    session.Cargo.Gem = 1;
                    session.Cargo.Gold = 100;
                    node = new CoreMapNode { Id = 990, Position = session.Position,
                        TerrainConstraint = EventTerrainConstraint.Any,
                        Encounter = CoreTreasureHuntCatalog.CreateEncounters(CoreTreasureHuntCatalog.FindRank("weathered"))
                            .Single(item => item.Id == "treasure_rival_sloop") };
                    session.Map.Nodes.Add(node);
                } else {
                    node = session.Map.Nodes.Single(item => item.Encounter.Id == "event_reef_shortcut");
                }
                SetProperty(session, "Position", node.Position);
                SetProperty(session, "ActiveEncounter", node.Encounter);
                SetProperty(session, "Phase", CoreDemoPhase.Encounter);
                SetField(session, "activeNode", node);
                node.Discovered = true;
                Invoke(session, "RevealAt", node.Position);
                Invoke(controller, "Render");
            } finally { SetField(controller, "editorPreviewBuilding", false); }
        }

        private static void ValidateEncounter(CoreGameplayDemoController controller, string state)
        {
            Transform[] active = controller.GetComponentsInChildren<Transform>(false);
            if (active.Any(item => item.name == "Encounter Themed Frame"))
                throw new InvalidOperationException("Decorative route frame remains in the embedded encounter.");
            RectTransform card = Find<RectTransform>(controller, "Encounter Card");
            RectTransform content = Find<RectTransform>(controller, "Encounter Paper Content");
            RectTransform secondary = Find<RectTransform>(controller, "Encounter Secondary Actions");
            RectTransform controls = Find<RectTransform>(controller, "Navigation Controls");
            CanvasGroup hidden = controls.GetComponent<CanvasGroup>();
            if (hidden == null || hidden.alpha != 0f || hidden.interactable || hidden.blocksRaycasts || !controls.gameObject.activeSelf)
                throw new InvalidOperationException("Encounter sailing controls must stay discoverable while visually hidden and noninteractive.");
            Image paper = Find<Image>(controller, "Encounter Parchment");
            RawImage art = Find<RawImage>(controller, "Demo Event Art");
            ScrollRect scroll = Find<ScrollRect>(controller, "Encounter Decision Viewport");
            ScrollRect storyScroll = Find<ScrollRect>(controller, "Encounter Description Viewport");
            Text title = Find<Text>(controller, "Encounter Title");
            Text narrative = Find<Text>(controller, "Encounter Description");
            Text badge = Find<Text>(controller, "Encounter Badge");
            Text location = Find<Text>(controller, "Encounter Location");
            if (paper.sprite == null || paper.sprite != NauticalSurfaceSkin.ReadingPaper || paper.type != Image.Type.Sliced ||
                paper.raycastTarget || art.texture == null || art.raycastTarget)
                throw new InvalidOperationException("Parchment and event art must remain visible and nonblocking.");
            float sourceAspect = art.texture.width * art.uvRect.width / (art.texture.height * art.uvRect.height);
            if (Math.Abs(art.rectTransform.rect.width / art.rectTransform.rect.height - sourceAspect) > .02f)
                throw new InvalidOperationException("Event illustration must retain its source crop aspect ratio.");
            RectTransform mount = Find<RectTransform>(controller, "Encounter Illustration Mount");
            RectTransform border = Find<RectTransform>(controller, "Illustration Border");
            if (Vector2.Distance(mount.rect.size, border.rect.size) > .1f)
                throw new InvalidOperationException("Illustration border must follow the resized mount.");
            Rect artBounds = InParent(art.rectTransform, content);
            Rect titleBounds = InParent(title.rectTransform, content);
            // The full narrative may deliberately extend below its own mask.
            // Check its visible reading region against the rest of the journal.
            Rect bodyBounds = InParent(storyScroll.viewport, content);
            Rect decisionBounds = InParent(scroll.viewport, content);
            Rect badgeBounds = InParent(badge.rectTransform, content);
            Rect locationBounds = InParent(location.rectTransform, content);
            var regions = new List<Rect> { artBounds, titleBounds, bodyBounds, decisionBounds, badgeBounds, locationBounds };
            if (secondary.rect.height > .1f) regions.Add(InParent(secondary, content));
            for (int i = 0; i < regions.Count; i++)
            {
                AssertContained(content.rect, regions[i], "Journal region " + i);
                for (int j = i + 1; j < regions.Count; j++)
                    if (regions[i].Overlaps(regions[j]))
                        throw new InvalidOperationException("Journal regions overlap: " + i + " / " + j);
            }
            if (scroll.viewport.rect.width < content.rect.width - 40f || scroll.viewport.rect.width > content.rect.width)
                throw new InvalidOperationException("Choices must span the paper apart from the dedicated scrollbar gutter.");
            foreach (Text text in new[] { title, narrative, badge, location }) AssertTextFits(text);
            if (storyScroll.viewport.GetComponent<RectMask2D>() == null || storyScroll.horizontal ||
                storyScroll.vertical != (storyScroll.content.rect.height > storyScroll.viewport.rect.height + .5f))
                throw new InvalidOperationException("Narrative must scroll within its own masked reading area only when needed.");
            if (storyScroll.vertical && storyScroll.verticalNormalizedPosition < .99f)
                throw new InvalidOperationException("Capture must leave the independently scrolling narrative at its beginning.");
            CoreEncounter encounter = controller.Session.ActiveEncounter;
            Button[] choices = scroll.content.GetComponentsInChildren<Button>(false);
            if (choices.Length != encounter.Choices.Count)
                throw new InvalidOperationException("Only ordinary authored choices belong to the scrolling list.");
            for (int i=0;i<encounter.Choices.Count;i++) {
                Button choice=choices.Single(button => button.name == "Encounter Choice " + i);
                if (choice.interactable != controller.Session.IsChoiceAvailable(i))
                    throw new InvalidOperationException("Rendered choice availability differs from the voyage rule.");
                RectTransform row = choice.GetComponent<RectTransform>();
                AssertContained(scroll.content.rect, InParent(row, scroll.content), choice.name);
                Text label = choice.GetComponentsInChildren<Text>(false).Single(text => text.name == "Label");
                if (!label.text.Contains(encounter.Choices[i].Label))
                    throw new InvalidOperationException("Choice label lost its authored text: " + choice.name);
                foreach (Text text in choice.GetComponentsInChildren<Text>(false))
                {
                    if (string.IsNullOrEmpty(text.text)) continue;
                    AssertTextFits(text);
                    AssertContained(row.rect, InParent(text.rectTransform, row), choice.name + " / " + text.name);
                }
                Text requirement = choice.GetComponentsInChildren<Text>(false).Single(text => text.name == "Requirement");
                if (!controller.Session.IsChoiceAvailable(i) && !requirement.text.Contains(controller.Session.ChoiceUnavailableReason(i)))
                    throw new InvalidOperationException("Locked choice is missing its complete rule explanation: " + choice.name);
            }
            Button[] allButtons = card.GetComponentsInChildren<Button>(false);
            foreach (Button button in allButtons)
            {
                Image surface = button.targetGraphic as Image;
                if (surface == null || surface.sprite != WoodButtonSkin.Face || surface.type != Image.Type.Sliced)
                    throw new InvalidOperationException("Encounter choices must use the shared secondary-interface wood skin: " + button.name);
            }
            Button skip = allButtons.SingleOrDefault(button => button.name == "Skip Encounter");
            if ((skip != null) == encounter.Mandatory)
                throw new InvalidOperationException("Skip action violates the event mandatory rule.");
            foreach (Button button in secondary.GetComponentsInChildren<Button>(false))
            {
                if (button.transform.parent != secondary)
                    throw new InvalidOperationException("Secondary actions must remain in the fixed footer.");
                RectTransform row = button.GetComponent<RectTransform>();
                AssertContained(secondary.rect, InParent(row, secondary), button.name);
                AssertContained(content.rect, InParent(row, content), button.name + " visible footer");
                foreach (Text text in button.GetComponentsInChildren<Text>(false))
                {
                    if (string.IsNullOrEmpty(text.text)) continue;
                    AssertTextFits(text);
                    AssertContained(row.rect, InParent(text.rectTransform, row), button.name + " / " + text.name);
                }
            }
            if (skip != null && skip.transform.parent != secondary)
                throw new InvalidOperationException("Skipping must stay visible outside the scrolling choice list.");
            Scrollbar bar = controller.GetComponentsInChildren<Scrollbar>(true).Single(item => item.name == "Encounter Decision Scrollbar");
            if (scroll.verticalScrollbar != bar || bar.gameObject.activeSelf != scroll.vertical)
                throw new InvalidOperationException("The dedicated scrollbar must reflect actual choice overflow.");
            if (state == "CrewLocked" && choices.Single(button => button.name == "Encounter Choice 0").interactable)
                throw new InvalidOperationException("Missing navigator should keep the first crew choice unavailable.");
            if (state.StartsWith("ManyChoices", StringComparison.Ordinal)) {
                Rect[] rows = choices.Select(button => InParent(button.GetComponent<RectTransform>(), scroll.content)).ToArray();
                float rowsHeight = rows.Max(row => row.yMax) - rows.Min(row => row.yMin);
                bool needsScroll = rowsHeight > scroll.viewport.rect.height + .1f;
                if (encounter.Choices.Count != 4 || scroll.vertical != needsScroll)
                    throw new InvalidOperationException("Many-choice fixture must preserve four authored choices and scroll exactly when its rows overflow.");
                RectTransform edge = (state == "ManyChoicesBottom" ? choices.Last() : choices.First()).GetComponent<RectTransform>();
                AssertContained(scroll.viewport.rect, InParent(edge, scroll.viewport), state + " visible ordinary choice", 1f);
            }
        }

        private static void AssertTextFits(Text text)
        {
            if (text.raycastTarget || text.preferredHeight > text.rectTransform.rect.height + 1f)
                throw new InvalidOperationException("Text must be complete and nonblocking: " + text.name + " / " + text.text +
                    " [preferred=" + text.preferredHeight + ", height=" + text.rectTransform.rect.height + ", raycast=" + text.raycastTarget + "]");
        }

        private static void AssertContained(Rect outer, Rect inner, string context, float tolerance = .2f)
        {
            if (inner.xMin < outer.xMin - tolerance || inner.xMax > outer.xMax + tolerance ||
                inner.yMin < outer.yMin - tolerance || inner.yMax > outer.yMax + tolerance)
                throw new InvalidOperationException("Outside its readable bounds: " + context);
        }
        private static T Find<T>(CoreGameplayDemoController controller, string name) where T : Component =>
            controller.GetComponentsInChildren<T>(false).Single(item => item.name == name);
        private static Rect InParent(RectTransform child, RectTransform parent) {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            return Bounds(corners.Select(point => (Vector2)parent.InverseTransformPoint(point)));
        }
        private static string VoyageSnapshot(CoreVoyageSession session) => session.Phase+";"+session.TotalHours+";"+
            session.ActiveEncounter?.Id+";"+string.Join(",",CoreWarehouseItemCatalog.All.Select(item => item.Key+"="+session.GetWarehouseCargoAmount(item.Key)))+
            ";gold="+session.Cargo.Gold;
        private static void SetProperty(object target,string name,object value) =>
            target.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public).GetSetMethod(true).Invoke(target,new[]{value});
        private static void SetField(object target,string name,object value) =>
            target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
        private static void Invoke(object target,string name,params object[] args) =>
            target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
        private static void CaptureMenu(string output, MenuFrame frame)
        {
            Scene scene = default;
            RenderTexture rt = null;
            RenderTexture previousActive = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                GameObject host = new GameObject("Disposable Encounter Journal Audit");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                CoreGameplayPreviewScreen screen = CoreGameplayPreviewScreen.Encounter;
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(screen, CoreGameplayPreviewPort.GrayGull, 0);
                host.SetActive(true);
                PrepareEncounter(controller, frame.state);
                string voyageBefore = VoyageSnapshot(controller.Session);
                Invoke(controller, "CloseTreasureStoryOverlay");
                if (controller.GetComponentsInChildren<Transform>(false).Any(item => item.name == "Treasure Story Overlay"))
                    throw new InvalidOperationException("Treasure story UI still obscures the encounter capture.");
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(c => c.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (!scaler.enabled) throw new InvalidOperationException("Production CanvasScaler unexpectedly disabled.");
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == "Settings Panel") child.gameObject.SetActive(frame.state == "Settings");
                    if (child.name == "Achievements Panel") child.gameObject.SetActive(frame.state == "Achievements");
                    if (child.name == "New Game Confirmation") child.gameObject.SetActive(false);
                    child.gameObject.layer = Layer;
                }
                GameObject cameraHost = new GameObject("Disposable UI Audit Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0,0,-1000);
                camera.cullingMask = 1 << Layer;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                rt = new RenderTexture(frame.width, frame.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                rt.Create();
                camera.targetTexture = rt;
                camera.rect = new Rect(0,0,1,1);
                camera.aspect = frame.width / (float)frame.height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 100f;
                // No assignment to canvas.scaleFactor, RectTransform size or localScale.
                // Calling the original scaler Handle preserves production calculations.
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
                if (handle == null) throw new MissingMethodException("CanvasScaler.Handle");
                for (int pass = 0; pass < 4; pass++)
                {
                    Canvas.ForceUpdateCanvases();
                    handle.Invoke(scaler, null);
                    // The capture executes synchronously, without an editor
                    // frame. Reproduce the production scale-change layout pass.
                    foreach (NavigationDashboardLayout layout in host.GetComponentsInChildren<NavigationDashboardLayout>(false))
                        layout.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
                    foreach (Text label in canvas.GetComponentsInChildren<Text>(true)) label.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (frame.state == "ManyChoicesBottom")
                    {
                        Find<ScrollRect>(controller, "Encounter Decision Viewport").verticalNormalizedPosition = 0f;
                        Canvas.ForceUpdateCanvases();
                    }
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = rt });
                    else camera.Render();
                }
                frame.renderingDisplaySize = canvas.renderingDisplaySize;
                frame.canvasPixelRect = canvas.pixelRect;
                frame.cameraPixelRect = camera.pixelRect;
                frame.logicalCanvasSize = canvas.GetComponent<RectTransform>().rect.size;
                frame.scaleFactor = canvas.scaleFactor;
                frame.screenMatchMode = scaler.screenMatchMode.ToString();
                float wr = frame.width / scaler.referenceResolution.x, hr = frame.height / scaler.referenceResolution.y;
                frame.expectedScaleFactor = scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand ? Mathf.Min(wr,hr) :
                    scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Shrink ? Mathf.Max(wr,hr) :
                    Mathf.Pow(2, Mathf.Lerp(Mathf.Log(wr,2), Mathf.Log(hr,2), scaler.matchWidthOrHeight));
                frame.verifiedScaler = scaler.enabled &&
                    Mathf.Abs(frame.renderingDisplaySize.x-frame.width)<0.5f && Mathf.Abs(frame.renderingDisplaySize.y-frame.height)<0.5f &&
                    Mathf.Abs(frame.canvasPixelRect.width-frame.width)<0.5f && Mathf.Abs(frame.canvasPixelRect.height-frame.height)<0.5f &&
                    Mathf.Abs(frame.cameraPixelRect.width-frame.width)<0.5f && Mathf.Abs(frame.cameraPixelRect.height-frame.height)<0.5f &&
                    Mathf.Abs(frame.scaleFactor-frame.expectedScaleFactor)<0.001f &&
                    Mathf.Abs(frame.logicalCanvasSize.x-frame.width/frame.scaleFactor)<1f &&
                    Mathf.Abs(frame.logicalCanvasSize.y-frame.height/frame.scaleFactor)<1f;
                if (!frame.verifiedScaler)
                    throw new InvalidOperationException("Target texture did not drive actual scaler/display geometry. Refuse false multi-resolution capture. Read recorded display/scale fields and use fixed Game View instead.");
                ValidateEncounter(controller, frame.state);
                if (VoyageSnapshot(controller.Session) != voyageBefore) throw new InvalidOperationException("Read-only capture changed voyage state.");
                foreach (Text label in canvas.GetComponentsInChildren<Text>(false))
                {
                    if (!label.enabled || string.IsNullOrEmpty(label.text)) continue;
                    var geometry = new TextGeometry { path = PathOf(label.transform, canvas.transform), content = label.text, fontSize = label.fontSize };
                    var corners = new Vector3[4]; label.rectTransform.GetWorldCorners(corners);
                    geometry.rectPixels = Bounds(corners.Select(v => (Vector2)camera.WorldToScreenPoint(v)));
                    geometry.rectOutsideTarget = Outside(geometry.rectPixels, frame.width, frame.height);
                    Mesh mesh = label.canvasRenderer.GetMesh();
                    try
                    {
                        geometry.meshVertexCount = mesh == null ? 0 : mesh.vertexCount;
                        geometry.meshAvailable = mesh != null && mesh.vertexCount > 0;
                        if (geometry.meshAvailable)
                        {
                            geometry.meshPixels = Bounds(mesh.vertices.Select(v => (Vector2)camera.WorldToScreenPoint(label.transform.TransformPoint(v))));
                            geometry.meshOutsideTarget = Outside(geometry.meshPixels, frame.width, frame.height);
                        }
                    }
                    finally { /* CanvasRenderer owns the returned mesh. */ }
                    frame.text.Add(geometry);
                }
                RenderTexture.active = rt;
                pixels = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0,0,frame.width,frame.height), 0,0);
                pixels.Apply(false,false);
                Color32[] samples = pixels.GetPixels32(); int low=255,high=0;
                for (int i=0;i<samples.Length;i+=997) { int b=(samples[i].r+samples[i].g+samples[i].b)/3; low=Math.Min(low,b); high=Math.Max(high,b); }
                if (high-low<20) throw new InvalidOperationException("Nearly blank frame; render not accepted.");
                File.WriteAllBytes(Path.Combine(output,frame.file), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
            }
        }
        private static string PathOf(Transform item, Transform root)
        {
            var names = new List<string>(); for (Transform t=item;t!=null && t!=root;t=t.parent) names.Insert(0,t.name); return string.Join("/",names);
        }
        private static Rect Bounds(IEnumerable<Vector2> points)
        {
            Vector2[] p=points.ToArray(); return Rect.MinMaxRect(p.Min(v=>v.x),p.Min(v=>v.y),p.Max(v=>v.x),p.Max(v=>v.y));
        }
        private static bool Outside(Rect r,int width,int height) { return r.xMin < -0.5f || r.yMin < -0.5f || r.xMax > width+0.5f || r.yMax > height+0.5f; }
    }
}
