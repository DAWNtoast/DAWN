// Editor-only audit entry. Copy into an isolated validation project's Editor folder.
// Capture is disposable; Sync patches only the nine authored settlement references.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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
    public static class SettlementAlignmentCapture
    {
        private const int Layer = 31;
        private const string TextCenteringGuid = "043b033ac63e43ceaaaef28cef9b11b5";
        private static readonly Regex Blocks = new Regex(@"(?ms)^--- !u!(\d+) &(-?\d+)[^\r\n]*\r?\n.*?(?=^--- !u!|\z)");
        private static readonly string[] Actions = { "Return To Port", "Draw Sea Chart", "Skip Settlement Reveal" };

        // -executeMethod ShatteredSail.Editor.SettlementAlignmentCapture.CaptureFromCommandLine
        // -uiAuditPath NEW_ABSOLUTE_OUTPUT_DIR (below an Artifacts directory); omit -nographics.
        public static void CaptureFromCommandLine()
        {
            RequireStoppedEditor();
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Graphics required; omit -nographics.");
            string output = Output();
            var report = new CaptureReport
            {
                capturedAtUtc = DateTime.UtcNow.ToString("O"),
                scope = "15 disposable production settlement renders: arrival sample 0, featured wood sample 4, failure sample 2, " +
                    "large rewards sample 5, and an isolated +10 gold/zero materials fixture matching the first-voyage screenshot. " +
                    "The first-voyage fixture is presentation data, not earned loot. BuildSettlementPresentation is called with previewSample=true. " +
                    "No gameplay callbacks, PlayMode, scene writes or player-save writes. Enabled production CanvasScaler and actual target geometry are verified. " +
                    "Text mesh bounds are rendered vertex bounds in target pixels, not glyph opacity measurements."
            };
            foreach (string state in new[] { "Arrival", "Materials", "Failure", "LargeValue", "FirstVoyage" })
                foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(1280, 1024) })
                {
                    var frame = new CaptureFrame { state = state, width = size.x, height = size.y, file = state + "_" + size.x + "x" + size.y + ".png" };
                    report.frames.Add(frame);
                    try { Capture(output, frame); }
                    catch (Exception exception) { frame.error = exception.ToString(); Debug.LogWarning(frame.file + ": " + exception.Message); }
                    File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                }
            WriteTextReport(output, report);
            if (report.frames.Count != 15 || report.frames.Any(frame => !frame.verifiedScaler || !string.IsNullOrEmpty(frame.error)))
                throw new InvalidOperationException("Settlement alignment capture failed; inspect report.json.");
            Debug.Log("SETTLEMENT_ALIGNMENT_CAPTURE_OK: 15 verified production captures at " + output);
        }

        private static VoyageSettlement Fixture(string state)
        {
            if (state == "FirstVoyage")
                return new VoyageSettlement
                {
                    Outcome = VoyageOutcome.SafeReturn, Title = "安全返航", Destination = "断锚港",
                    ProtectionRatio = .85f, DistanceNm = 360, Hours = 23.1, Coverage = 19,
                    CompletedEvents = 3, TotalEvents = 3, BankedGold = 10,
                    BankedWood = 0, BankedIron = 0, BankedFame = 0, ChartSaved = false
                };
            int scenario = state == "Materials" ? 4 : state == "Failure" ? 2 : state == "LargeValue" ? 5 : 0;
            return SettlementPreviewSamples.Create(scenario);
        }

        private static void Capture(string output, CaptureFrame frame)
        {
            Scene scene = default;
            RenderTexture rt = null, previousActive = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                GameObject host = new GameObject("Disposable Settlement Alignment Audit");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Settlement, CoreGameplayPreviewPort.GrayGull, 0);
                host.SetActive(true);
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(item => item.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null || !scaler.enabled) throw new InvalidOperationException("Production CanvasScaler unexpectedly disabled.");
                Transform content = FixedGameViewport.Bounds(canvas).Find("Phase Content");
                if (content == null) throw new InvalidOperationException("Missing production Phase Content.");
                foreach (Transform child in content.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                decimal goldBefore = controller.Session.ProfileGold;
                CoreDemoPhase phaseBefore = controller.Session.Phase;
                VoyageSettlement result = Fixture(frame.state);
                int[] rewardsBefore = { result.BankedGold, result.BankedWood, result.BankedIron, result.BankedFame };
                MethodInfo builder = typeof(CoreGameplayDemoController).GetMethod("BuildSettlementPresentation", BindingFlags.Instance | BindingFlags.NonPublic);
                if (builder == null) throw new MissingMethodException("CoreGameplayDemoController.BuildSettlementPresentation");
                builder.Invoke(controller, new object[] { result, true });
                host.GetComponentsInChildren<SettlementRevealController>(true).Single().CompleteImmediately();
                if (controller.Session.ProfileGold != goldBefore || controller.Session.Phase != phaseBefore ||
                    !rewardsBefore.SequenceEqual(new[] { result.BankedGold, result.BankedWood, result.BankedIron, result.BankedFame }))
                    throw new InvalidOperationException("The disposable presentation mutated its settlement/profile.");
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = Layer;
                GameObject cameraHost = new GameObject("Disposable Settlement Alignment Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false; camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.orthographicSize = 5f;
                camera.nearClipPlane = .1f; camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0, 0, -1000);
                camera.cullingMask = 1 << Layer; camera.allowHDR = false; camera.allowMSAA = false;
                rt = new RenderTexture(frame.width, frame.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                rt.Create(); camera.targetTexture = rt; camera.rect = new Rect(0, 0, 1, 1);
                camera.aspect = frame.width / (float)frame.height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 100f;
                // Preserve production scaling; never assign canvas scaleFactor, size or localScale.
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
                if (handle == null) throw new MissingMethodException("CanvasScaler.Handle");
                for (int pass = 0; pass < 4; pass++)
                {
                    Canvas.ForceUpdateCanvases(); handle.Invoke(scaler, null);
                    foreach (Text label in canvas.GetComponentsInChildren<Text>(true)) label.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = rt });
                    else camera.Render();
                }
                VerifyScaler(canvas, scaler, camera, frame);
                foreach (Text label in canvas.GetComponentsInChildren<Text>(false))
                {
                    if (!label.enabled || string.IsNullOrEmpty(label.text)) continue;
                    var geometry = new TextGeometry
                    {
                        path = PathOf(label.transform, canvas.transform), content = label.text, fontSize = label.fontSize,
                        alignment = label.alignment.ToString(), alignByGeometry = label.alignByGeometry,
                        authoredPosition = label.rectTransform.anchoredPosition, authoredSize = label.rectTransform.sizeDelta
                    };
                    geometry.rectPixels = ScreenBounds(label.rectTransform, camera);
                    geometry.parentPixels = ScreenBounds(label.transform.parent as RectTransform, camera);
                    geometry.rectOutsideTarget = Outside(geometry.rectPixels, frame.width, frame.height);
                    Mesh mesh = label.canvasRenderer.GetMesh();
                    geometry.meshVertexCount = mesh == null ? 0 : mesh.vertexCount;
                    geometry.meshAvailable = geometry.meshVertexCount > 0;
                    if (geometry.meshAvailable)
                    {
                        geometry.meshPixels = Bounds(mesh.vertices.Select(vertex => (Vector2)camera.WorldToScreenPoint(label.transform.TransformPoint(vertex))));
                        geometry.meshOutsideTarget = Outside(geometry.meshPixels, frame.width, frame.height);
                        geometry.meshCenterOffsetFromTextRect = geometry.meshPixels.center - geometry.rectPixels.center;
                        geometry.meshCenterOffsetFromParent = geometry.meshPixels.center - geometry.parentPixels.center;
                    }
                    // CanvasRenderer owns the mesh returned by GetMesh().
                    frame.text.Add(geometry);
                }
                foreach (TextGeometry label in frame.text)
                {
                    if (!label.meshAvailable || label.meshOutsideTarget || label.rectOutsideTarget)
                        throw new InvalidOperationException("Missing or off-screen text: " + label.path);
                    if (label.meshPixels.xMin < label.rectPixels.xMin - 1f || label.meshPixels.xMax > label.rectPixels.xMax + 1f ||
                        label.meshPixels.yMin < label.rectPixels.yMin - 1f || label.meshPixels.yMax > label.rectPixels.yMax + 1f)
                        throw new InvalidOperationException("Text exceeds its authored rectangle: " + label.path);
                    if (Mathf.Abs(label.meshCenterOffsetFromTextRect.y) > 1f ||
                        (label.alignment == "MiddleCenter" && Mathf.Abs(label.meshCenterOffsetFromTextRect.x) > 1f))
                        throw new InvalidOperationException("Visible text is not centered: " + label.path);
                    if (label.content.Contains("船体修缮") || label.content.Contains("铸造储备") || label.content.Contains("远航声名"))
                        throw new InvalidOperationException("Removed resource hint is still visible: " + label.path);
                }
                RenderTexture.active = rt;
                pixels = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0); pixels.Apply(false, false);
                Color32[] samples = pixels.GetPixels32(); int low = 255, high = 0;
                for (int i = 0; i < samples.Length; i += 997)
                {
                    int brightness = (samples[i].r + samples[i].g + samples[i].b) / 3;
                    low = Math.Min(low, brightness); high = Math.Max(high, brightness);
                }
                if (high - low < 20) throw new InvalidOperationException("Nearly blank frame; capture refused.");
                File.WriteAllBytes(Path.Combine(output, frame.file), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
            }
        }

        private static void VerifyScaler(Canvas canvas, CanvasScaler scaler, Camera camera, CaptureFrame frame)
        {
            frame.renderingDisplaySize = canvas.renderingDisplaySize; frame.canvasPixelRect = canvas.pixelRect;
            frame.cameraPixelRect = camera.pixelRect; frame.logicalCanvasSize = canvas.GetComponent<RectTransform>().rect.size;
            frame.scaleFactor = canvas.scaleFactor; frame.screenMatchMode = scaler.screenMatchMode.ToString();
            float wr = frame.width / scaler.referenceResolution.x, hr = frame.height / scaler.referenceResolution.y;
            frame.expectedScaleFactor = scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand ? Mathf.Min(wr, hr) :
                scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Shrink ? Mathf.Max(wr, hr) :
                Mathf.Pow(2, Mathf.Lerp(Mathf.Log(wr, 2), Mathf.Log(hr, 2), scaler.matchWidthOrHeight));
            frame.verifiedScaler = scaler.enabled &&
                Mathf.Abs(frame.renderingDisplaySize.x - frame.width) < .5f && Mathf.Abs(frame.renderingDisplaySize.y - frame.height) < .5f &&
                Mathf.Abs(frame.canvasPixelRect.width - frame.width) < .5f && Mathf.Abs(frame.canvasPixelRect.height - frame.height) < .5f &&
                Mathf.Abs(frame.cameraPixelRect.width - frame.width) < .5f && Mathf.Abs(frame.cameraPixelRect.height - frame.height) < .5f &&
                Mathf.Abs(frame.scaleFactor - frame.expectedScaleFactor) < .001f &&
                Mathf.Abs(frame.logicalCanvasSize.x - frame.width / frame.scaleFactor) < 1f &&
                Mathf.Abs(frame.logicalCanvasSize.y - frame.height / frame.scaleFactor) < 1f;
            if (!frame.verifiedScaler)
                throw new InvalidOperationException("Target texture did not drive the actual scaler/display geometry; capture refused.");
        }

        // -executeMethod ShatteredSail.Editor.SettlementAlignmentCapture.SyncFromCommandLine
        // -uiAuditPath NEW_ABSOLUTE_OUTPUT_DIR; backups/report are written under this directory.
        public static void SyncFromCommandLine()
        {
            throw new System.NotSupportedException("UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.");
#if false // Historical implementation; this target is retired.
            RequireStoppedEditor();
            string output = Output();
            FieldInfo referencesField = typeof(GameplayPreviewSceneBuilder).GetField("StorageRoutingSettlementReferences", BindingFlags.Static | BindingFlags.NonPublic);
            if (referencesField == null) throw new MissingFieldException("GameplayPreviewSceneBuilder.StorageRoutingSettlementReferences");
            string[] references = (string[])referencesField.GetValue(null);
            if (references == null || references.Length != 9 || references.Distinct().Count() != 9)
                throw new InvalidOperationException("Expected exactly nine settlement references.");
            Patch[] patches = references.Select(reference => PreparePatch(GameplayPreviewSceneBuilder.EditableSceneFolder + "/" + reference)).ToArray();
            // Fully prepare and validate all nine before any scene mutation.
            foreach (Patch patch in patches)
            {
                if (!File.ReadAllBytes(patch.absolute).SequenceEqual(patch.beforeBytes))
                    throw new IOException("Authored scene changed during patch preparation: " + patch.path);
                string backup = Path.Combine(output, "scene-backup", patch.path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(backup)); File.WriteAllBytes(backup, patch.beforeBytes);
                if (File.Exists(patch.absolute + ".meta")) File.Copy(patch.absolute + ".meta", backup + ".meta");
            }
            var report = new SyncReport
            {
                syncedAtUtc = DateTime.UtcNow.ToString("O"), backupRoot = Path.Combine(output, "scene-backup"),
                scope = "Only the nine StorageRoutingSettlementReferences. Raw YAML patch retains authored values, artwork, object IDs, tutorial overlays, assets and all unedited blocks. " +
                    "Changes are settlement text rectangles/alignment and the production SettlementTextCentering mesh effect, trailing empty event lines, " +
                    "and removal of wood/iron/fame detail labels. Gold hints remain. Tutorial overlay text has no added mesh effect."
            };
            foreach (Patch patch in patches)
            {
                byte[] after = Encoding.UTF8.GetBytes(patch.after);
                if (patch.before != patch.after) File.WriteAllBytes(patch.absolute, after);
                if (!File.ReadAllBytes(patch.absolute).SequenceEqual(after)) throw new IOException("Write verification failed: " + patch.path);
                report.scenes.Add(new SceneResult
                {
                    path = patch.path, updated = patch.before != patch.after, changedBlocks = patch.changedBlocks,
                    removedObjects = patch.removedObjects, addedCenteringEffects = patch.addedCenteringEffects,
                    beforeSha256 = Hash(patch.beforeBytes), afterSha256 = Hash(after)
                });
                File.WriteAllText(Path.Combine(output, "scene-sync.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            }
            AssetDatabase.Refresh();
            Debug.Log("SETTLEMENT_ALIGNMENT_SYNC_OK: nine authored references; backup/report=" + output);

#endif
        }

        private static Patch PreparePatch(string path)
        {
            string absolute = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolute)) throw new FileNotFoundException("Missing settlement reference", absolute);
            Scene loaded = SceneManager.GetSceneByPath(path);
            if (loaded.IsValid() && loaded.isLoaded && loaded.isDirty) throw new InvalidOperationException("Unsaved scene edits: " + path);
            byte[] bytes = File.ReadAllBytes(absolute);
            string before = new UTF8Encoding(false, true).GetString(bytes);
            Dictionary<string, string> blocks = Blocks.Matches(before).Cast<Match>().ToDictionary(match => match.Groups[2].Value, match => match.Value);
            var edits = new Dictionary<string, string>(); var removed = new HashSet<string>();
            var added = new Dictionary<string, string>();
            var objects = blocks.Where(pair => pair.Value.StartsWith("--- !u!1 ", StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => Scalar(pair.Value, "m_Name", 2));
            var rects = blocks.Where(pair => pair.Value.StartsWith("--- !u!224 ", StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => Reference(pair.Value, "m_GameObject"));
            string ObjectNamed(string name) => objects.Single(pair => pair.Value == name).Key;
            string RectId(string objectId) => rects.Single(pair => pair.Value == objectId).Key;
            string TextId(string objectId) => blocks.Single(pair => ReferenceOrEmpty(pair.Value, "m_GameObject") == objectId &&
                pair.Value.Contains("UnityEngine.UI::UnityEngine.UI.Text")).Key;
            string Current(string id) => edits.TryGetValue(id, out string edited) ? edited : blocks[id];
            void EditRect(string objectId, Vector2 position, Vector2? size = null)
            {
                string id = RectId(objectId), block = Current(id);
                block = VectorField(block, "m_AnchoredPosition", position);
                if (size.HasValue) block = VectorField(block, "m_SizeDelta", size.Value);
                edits[id] = block;
            }
            void DeleteLabel(string objectId)
            {
                string rectId = RectId(objectId), rect = blocks[rectId];
                if (!Regex.IsMatch(rect, @"(?m)^  m_Children: \[\]\r?$")) throw new InvalidDataException("Removed hint must be a leaf: " + path);
                string parentId = Reference(rect, "m_Father");
                var childReference = new Regex(@"(?m)^  - \{fileID: " + Regex.Escape(rectId) + @"\}\r?\n");
                string parent = Current(parentId);
                if (childReference.Matches(parent).Count != 1) throw new InvalidDataException("Missing parent hint reference: " + path);
                edits[parentId] = childReference.Replace(parent, "");
                removed.Add(objectId);
                foreach (Match match in Regex.Matches(blocks[objectId], @"(?m)^  - component: \{fileID: (-?\d+)\}")) removed.Add(match.Groups[1].Value);
            }

            string cardId = RectId(ObjectNamed("Settlement Card"));
            var settlementRects = new HashSet<string> { cardId };
            bool extended;
            do
            {
                extended = false;
                foreach (string id in rects.Keys)
                    if (!settlementRects.Contains(id) && settlementRects.Contains(Reference(blocks[id], "m_Father")))
                        extended |= settlementRects.Add(id);
            } while (extended);
            foreach (string id in settlementRects)
            {
                string objectId = rects[id];
                string textId = blocks.Where(pair => ReferenceOrEmpty(pair.Value, "m_GameObject") == objectId &&
                    pair.Value.Contains("UnityEngine.UI::UnityEngine.UI.Text")).Select(pair => pair.Key).SingleOrDefault();
                if (textId == null) continue;
                string block = Current(textId);
                bool actionLabel = objects[objectId] == "Label" && Actions.Contains(objects[rects[Reference(blocks[id], "m_Father")]]);
                if (Scalar(block, "m_Alignment", 4) == "4" || actionLabel)
                    edits[textId] = ScalarField(block, "m_AlignByGeometry", "1", 4);
            }

            string featuredId = RectId(ObjectNamed("Settlement Featured Reward"));
            string featuredValueObject = objects.Single(pair => pair.Value.StartsWith("Settlement Reward ", StringComparison.Ordinal) &&
                rects.Any(rect => rect.Value == pair.Key && Reference(blocks[rect.Key], "m_Father") == featuredId)).Key;
            bool featuredGold = objects[featuredValueObject] == "Settlement Reward Gold";
            bool featuredIcon = objects.Any(pair => pair.Value.StartsWith("Settlement Featured Icon ", StringComparison.Ordinal) &&
                rects.Any(rect => rect.Value == pair.Key && Reference(blocks[rect.Key], "m_Father") == featuredId));
            EditRect(ObjectNamed("Settlement Featured Name"), new Vector2(0, 76), new Vector2(featuredIcon ? 680 : 790, 48));
            EditRect(featuredValueObject, new Vector2(0, featuredGold ? -4 : -19));
            string featuredDetail = objects.Where(pair => pair.Value == "Settlement Featured Detail").Select(pair => pair.Key).SingleOrDefault();
            if (featuredDetail != null)
            {
                if (featuredGold) EditRect(featuredDetail, new Vector2(0, -79));
                else DeleteLabel(featuredDetail);
            }
            foreach (string resource in new[] { "Gold", "Wood", "Iron", "Fame" })
            {
                string tileObject = objects.Where(pair => pair.Value == "Settlement Reward Tile " + resource).Select(pair => pair.Key).SingleOrDefault();
                if (tileObject == null) continue;
                string tileRect = RectId(tileObject);
                bool resourceIcon = objects.Any(pair => pair.Value == "Settlement Resource Icon " + resource &&
                    rects.Any(rect => rect.Value == pair.Key && Reference(blocks[rect.Key], "m_Father") == tileRect));
                EditRect(ObjectNamed("Settlement Resource " + resource), new Vector2(0, 46), new Vector2(resourceIcon ? 168 : 246, 30));
                EditRect(ObjectNamed("Settlement Reward " + resource), new Vector2(0, resource == "Gold" ? 0 : -10));
            }
            foreach (var pair in objects.Where(pair => pair.Value == "Settlement Resource Hint Wood" || pair.Value == "Settlement Resource Hint Iron" || pair.Value == "Settlement Resource Hint Fame"))
                DeleteLabel(pair.Key);
            string storeRect = RectId(ObjectNamed("Settlement Store All Cargo"));
            string captionObject = objects.Single(pair => pair.Value == "Label" && rects.Any(rect => rect.Value == pair.Key && Reference(blocks[rect.Key], "m_Father") == storeRect)).Key;
            EditRect(captionObject, new Vector2(18, 0), new Vector2(530, 36));
            string captionId = TextId(captionObject);
            edits[captionId] = ScalarField(Current(captionId), "m_Alignment", "3", 4);
            string noteObject = ObjectNamed("Settlement Note"), noteRect = RectId(noteObject);
            Vector2 notePosition = VectorValue(blocks[noteRect], "m_AnchoredPosition"), noteSize = VectorValue(blocks[noteRect], "m_SizeDelta");
            EditRect(noteObject, new Vector2(-495, notePosition.y), new Vector2(570, noteSize.y));
            string eventsId = TextId(ObjectNamed("Settlement Events"));
            string events = Current(eventsId);
            var trailingEmptyLine = new Regex(@"(?m)^(?<prefix>  m_Text: ""[^\r\n]*?)(?:\\n)+""(?<suffix>\r?)$");
            edits[eventsId] = trailingEmptyLine.Replace(events, "${prefix}\"${suffix}");

            // Use the same production effect as SettlementText and action/store
            // labels. It moves mesh vertices inside the authored rect, so no
            // extra y-position adjustment or tutorial-overlay change is needed.
            string newline = before.Contains("\r\n") ? "\r\n" : "\n";
            // Unity reserves long.MaxValue for SceneRoots; choose unused IDs.
            long nextId = 0;
            foreach (string rectId in settlementRects)
            {
                string objectId = rects[rectId];
                if (removed.Contains(objectId)) continue;
                bool isText = blocks.Any(pair => ReferenceOrEmpty(pair.Value, "m_GameObject") == objectId &&
                    pair.Value.Contains("UnityEngine.UI::UnityEngine.UI.Text"));
                if (!isText) continue;
                string name = objects[objectId];
                string parentId = Reference(blocks[rectId], "m_Father");
                string parentName = rects.TryGetValue(parentId, out string parentObject) ? objects[parentObject] : "";
                bool relevant = name.StartsWith("Settlement", StringComparison.Ordinal) ||
                    (name == "Label" && (Actions.Contains(parentName) || parentName == "Settlement Store All Cargo"));
                if (!relevant) continue;
                string relevantTextId = TextId(objectId);
                edits[relevantTextId] = ScalarField(Current(relevantTextId), "m_AlignByGeometry", "1", 4);
                string[] existingEffects = blocks.Where(pair => ReferenceOrEmpty(pair.Value, "m_GameObject") == objectId &&
                    pair.Value.Contains("guid: " + TextCenteringGuid + ",")).Select(pair => pair.Key).ToArray();
                if (existingEffects.Length > 1) throw new InvalidDataException("Duplicate settlement centering effects in " + path);
                string objectBlock = Current(objectId);
                if (existingEffects.Length == 1)
                {
                    if (!objectBlock.Contains("component: {fileID: " + existingEffects[0] + "}"))
                        throw new InvalidDataException("Centering effect is absent from its GameObject component list in " + path);
                    continue;
                }
                string effectId;
                do { effectId = checked(++nextId).ToString(CultureInfo.InvariantCulture); }
                while (blocks.ContainsKey(effectId) || added.ContainsKey(effectId));
                var componentList = new Regex(@"(?m)^  m_Component:\r?\n(?:  - component: \{fileID: -?\d+\}\r?\n)+");
                if (componentList.Matches(objectBlock).Count != 1) throw new InvalidDataException("Invalid GameObject component list in " + path);
                edits[objectId] = componentList.Replace(objectBlock, match => match.Value + "  - component: {fileID: " + effectId + "}" + newline);
                added[effectId] = string.Join(newline, new[]
                {
                    "--- !u!114 &" + effectId,
                    "MonoBehaviour:",
                    "  m_ObjectHideFlags: 0",
                    "  m_CorrespondingSourceObject: {fileID: 0}",
                    "  m_PrefabInstance: {fileID: 0}",
                    "  m_PrefabAsset: {fileID: 0}",
                    "  m_GameObject: {fileID: " + objectId + "}",
                    "  m_Enabled: 1",
                    "  m_EditorHideFlags: 0",
                    "  m_Script: {fileID: 11500000, guid: " + TextCenteringGuid + ", type: 3}",
                    "  m_Name: ",
                    "  m_EditorClassIdentifier: ShatteredSail.Presentation::ShatteredSail.Presentation.SettlementTextCentering",
                    ""
                });
            }

            string after = Blocks.Replace(before, match => removed.Contains(match.Groups[2].Value) ? "" :
                edits.TryGetValue(match.Groups[2].Value, out string edit) ? edit : match.Value);
            if (added.Count > 0)
            {
                if (!after.EndsWith("\n", StringComparison.Ordinal)) after += newline;
                after += string.Concat(added.Values);
            }
            foreach (string id in removed)
                if (Regex.IsMatch(after, @"\{fileID: " + Regex.Escape(id) + @"(?:[,}])"))
                    throw new InvalidDataException("A retained object references a removed hint in " + path);
            Dictionary<string, string> retained = Blocks.Matches(after).Cast<Match>().ToDictionary(match => match.Groups[2].Value, match => match.Value);
            if (retained.Count != blocks.Count - removed.Count + added.Count || blocks.Any(pair => !removed.Contains(pair.Key) &&
                (!retained.TryGetValue(pair.Key, out string block) || block != (edits.TryGetValue(pair.Key, out string edit) ? edit : pair.Value))))
                throw new InvalidDataException("Unexpected retained YAML changes: " + path);
            foreach (var pair in added)
                if (!retained.TryGetValue(pair.Key, out string block) || block != pair.Value)
                    throw new InvalidDataException("Unexpected new centering component serialization in " + path);
            return new Patch
            {
                path = path, absolute = absolute, beforeBytes = bytes, before = before, after = after,
                changedBlocks = edits.Count(pair => !removed.Contains(pair.Key) && pair.Value != blocks[pair.Key]),
                removedObjects = removed.Count, addedCenteringEffects = added.Count
            };
        }

        private static string Scalar(string block, string name, int indent)
        {
            Match match = Regex.Match(block, @"(?m)^" + new string(' ', indent) + Regex.Escape(name) + @": (?<value>[^\r\n]*)\r?$");
            if (!match.Success) throw new InvalidDataException("Missing serialized field " + name);
            return match.Groups["value"].Value;
        }
        private static string ReferenceOrEmpty(string block, string name)
        {
            Match match = Regex.Match(block, @"(?m)^  " + Regex.Escape(name) + @": \{fileID: (-?\d+)\}");
            return match.Success ? match.Groups[1].Value : "";
        }
        private static string Reference(string block, string name)
        {
            string value = ReferenceOrEmpty(block, name);
            if (value.Length == 0) throw new InvalidDataException("Missing serialized reference " + name);
            return value;
        }
        private static string ScalarField(string block, string name, string value, int indent)
        {
            var pattern = new Regex(@"(?m)^(?<prefix>" + new string(' ', indent) + Regex.Escape(name) + @": )[^\r\n]*");
            if (pattern.Matches(block).Count != 1) throw new InvalidDataException("Invalid serialized field " + name);
            return pattern.Replace(block, match => match.Groups["prefix"].Value + value);
        }
        private static Vector2 VectorValue(string block, string name)
        {
            Match match = Regex.Match(block, @"(?m)^  " + Regex.Escape(name) + @": \{x: ([^,]+), y: ([^}\r\n]+)\}");
            if (!match.Success) throw new InvalidDataException("Invalid serialized vector " + name);
            return new Vector2(float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
        }
        private static string VectorField(string block, string name, Vector2 value)
        {
            var pattern = new Regex(@"(?m)^  " + Regex.Escape(name) + @": \{x: [^,]+, y: [^}\r\n]+\}");
            if (pattern.Matches(block).Count != 1) throw new InvalidDataException("Invalid serialized vector " + name);
            return pattern.Replace(block, "  " + name + ": {x: " + value.x.ToString("R", CultureInfo.InvariantCulture) + ", y: " + value.y.ToString("R", CultureInfo.InvariantCulture) + "}");
        }
        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        private static string Output()
        {
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-uiAuditPath");
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1])) throw new ArgumentException("Pass -uiAuditPath NEW_ABSOLUTE_OUTPUT_DIR.");
            string output = Path.GetFullPath(args[index + 1]);
            if (!output.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => string.Equals(part, "Artifacts", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Output must be below an Artifacts directory.");
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Use a new or empty output directory.");
            Directory.CreateDirectory(output); return output;
        }
        private static void RequireStoppedEditor()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stopped Editor required.");
        }
        private static string PathOf(Transform item, Transform root)
        {
            var names = new List<string>(); for (Transform t = item; t != null && t != root; t = t.parent) names.Insert(0, t.name); return string.Join("/", names);
        }
        private static Rect ScreenBounds(RectTransform rect, Camera camera)
        {
            if (rect == null) return default;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners); return Bounds(corners.Select(point => (Vector2)camera.WorldToScreenPoint(point)));
        }
        private static Rect Bounds(IEnumerable<Vector2> points)
        {
            Vector2[] array = points.ToArray(); return Rect.MinMaxRect(array.Min(point => point.x), array.Min(point => point.y), array.Max(point => point.x), array.Max(point => point.y));
        }
        private static bool Outside(Rect rect, int width, int height) => rect.xMin < -.5f || rect.yMin < -.5f || rect.xMax > width + .5f || rect.yMax > height + .5f;
        private static void WriteTextReport(string output, CaptureReport report)
        {
            var text = new StringBuilder(report.scope).AppendLine();
            foreach (CaptureFrame frame in report.frames)
            {
                text.AppendLine(frame.file + " scaler=" + frame.verifiedScaler + " error=" + (frame.error ?? ""));
                foreach (TextGeometry label in frame.text)
                    text.AppendLine(label.path + " | text=" + label.content.Replace("\n", "\\n") + " | mesh=" + label.meshPixels +
                        " | center offset/text=" + label.meshCenterOffsetFromTextRect + " | center offset/parent=" + label.meshCenterOffsetFromParent);
            }
            File.WriteAllText(Path.Combine(output, "text-geometry.txt"), text.ToString(), new UTF8Encoding(false));
        }
        [Serializable] private sealed class CaptureReport
        {
            public string capturedAtUtc, scope; public List<CaptureFrame> frames = new List<CaptureFrame>();
        }
        [Serializable] private sealed class CaptureFrame
        {
            public string state, file, error, screenMatchMode; public int width, height; public bool verifiedScaler;
            public Vector2 renderingDisplaySize, logicalCanvasSize; public Rect canvasPixelRect, cameraPixelRect;
            public float scaleFactor, expectedScaleFactor; public List<TextGeometry> text = new List<TextGeometry>();
        }
        [Serializable] private sealed class TextGeometry
        {
            public string path, content, alignment; public int fontSize, meshVertexCount;
            public bool alignByGeometry, meshAvailable, meshOutsideTarget, rectOutsideTarget;
            public Vector2 authoredPosition, authoredSize, meshCenterOffsetFromTextRect, meshCenterOffsetFromParent;
            public Rect rectPixels, parentPixels, meshPixels;
        }
        private sealed class Patch
        {
            public string path, absolute, before, after; public byte[] beforeBytes; public int changedBlocks, removedObjects, addedCenteringEffects;
        }
        [Serializable] private sealed class SyncReport
        {
            public string syncedAtUtc, backupRoot, scope; public List<SceneResult> scenes = new List<SceneResult>();
        }
        [Serializable] private sealed class SceneResult
        {
            public string path, beforeSha256, afterSha256; public bool updated; public int changedBlocks, removedObjects, addedCenteringEffects;
        }
    }
}
#endif
