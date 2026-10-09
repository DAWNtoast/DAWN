// Temporary Editor-only audit. Import into the isolated project's Editor assembly.
// Builds disposable production previews; never saves a scene, asset or PlayerPrefs.
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
    public static class TextPolishNarrativeLayoutAudit
    {
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        public static void RunFromCommandLine()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Narrative layout audit requires a stopped Editor.");
            string output = ReportPath();
            var report = new Report { startedUtc = DateTime.UtcNow.ToString("O") };
            try
            {
                AuditComponents(report);
                AuditShips(report);
                AuditTreasures(report);
                report.failed = report.items.Count(item => !item.fit);
                if (report.items.Count != 269)
                    throw new InvalidOperationException("Expected 269 narrative items; observed " + report.items.Count + ".");
                if (report.failed > 0)
                    throw new InvalidOperationException(report.failed + " text layouts failed: " +
                        string.Join(", ", report.items.Where(item => !item.fit).Select(item => item.category + "/" + item.id)));
                report.status = "passed";
            }
            catch (Exception error)
            {
                report.status = "failed";
                report.failed = report.items.Count(item => !item.fit);
                report.error = error.ToString();
                throw;
            }
            finally
            {
                report.completedUtc = DateTime.UtcNow.ToString("O");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                // CreateNew protects previous evidence even if another process
                // creates this path after initial argument validation.
                using (var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    writer.Write(JsonUtility.ToJson(report, true));
            }
            Debug.Log("TEXT_NARRATIVE_LAYOUT_OK: " + report.items.Count + " items; " + output);
        }

        private static void AuditComponents(Report report)
        {
            using (var preview = new Preview(CoreGameplayPreviewScreen.ComponentShopDetail))
            {
                ComponentArtCatalog catalog = Read<ComponentArtCatalog>(preview.Controller, "componentArt");
                Require(catalog.ExpandedEntries.Count == 150, "Expected all 150 expanded components.");
                Text description = preview.Find<Text>("Component Detail Description");
                MeasureGroup(report, preview, description, "component-description",
                    catalog.ExpandedEntries.Select(entry => Pair(entry.Id, entry.Description)));

                preview.Find<Button>("ComponentDossierPageStory").onClick.Invoke();
                Text story = preview.Find<Text>("Component Detail Story Body");
                Require(story.gameObject.activeInHierarchy, "Production component story page did not become active.");
                var chapters = catalog.ExpandedEntries.GroupBy(entry => entry.StoryChapter).ToArray();
                Require(chapters.Length == 15, "Expected 15 shared component story chapters.");
                var stories = new List<KeyValuePair<string, string>>();
                foreach (var chapter in chapters)
                {
                    string[] variants = chapter.Select(entry => entry.StoryBody).Distinct().ToArray();
                    Require(variants.Length == 1, "Component chapter has inconsistent shared text: " + chapter.Key);
                    stories.Add(Pair(chapter.Key.ToString(), variants[0]));
                }
                MeasureGroup(report, preview, story, "component-story", stories);
            }
        }

        private static void AuditShips(Report report)
        {
            Require(CoreVoyageSession.Ships.Count == 12, "Expected all 12 ships.");
            using (var preview = new Preview(CoreGameplayPreviewScreen.ShipyardDetail))
            {
                Text specialty = preview.Find<Text>("Shipyard Dossier Specialization");
                MeasureGroup(report, preview, specialty, "ship-specialty", Enumerable.Range(0, CoreVoyageSession.Ships.Count)
                    .Select(index => Pair(index + ":" + CoreVoyageSession.Ships[index].Name, CoreShipMarketCatalog.Specialty(index))));

                preview.Controller.ConfigureEditorShipyard(0, true, 1);
                Text story = preview.Find<Text>("Shipyard Dossier Story");
                var stories = new List<KeyValuePair<string, string>>();
                for (int index = 0; index < CoreVoyageSession.Ships.Count; index++)
                {
                    Require(!string.IsNullOrWhiteSpace(CoreVoyageSession.Ships[index].Lore), "Missing ship Lore: " + index);
                    stories.Add(Pair(index + ":" + CoreVoyageSession.Ships[index].Name, CoreVoyageSession.Ships[index].Lore));
                }
                MeasureGroup(report, preview, story, "ship-lore", stories);
            }
        }

        private static void AuditTreasures(Report report)
        {
            Require(CoreWarehouseCollection.Catalog.Count == 40, "Expected all 40 collectible treasures.");
            using (var preview = new Preview(CoreGameplayPreviewScreen.Warehouse))
            {
                // The description paragraph is a production one-item inventory
                // state. Change only this disposable preview's in-memory sample.
                CoreWarehouseTreasureDefinition first = CoreWarehouseCollection.Catalog[0];
                preview.Controller.WarehouseCollection.Import(first.Id, string.Empty);
                Set(preview.Controller, "selectedWarehouseTreasureId", first.Id);
                Set(preview.Controller, "warehouseCatalogOpen", false);
                Set(preview.Controller, "warehouseStorageOpen", false);
                Set(preview.Controller, "warehouseTreasurePage", 0);
                Invoke(preview.Controller, "Render");
                Text description = preview.Find<Text>("Warehouse Collection Hint");
                Text story = preview.Find<Text>("Warehouse Treasure Story");
                Require(description.text == first.Description && story.text == first.Story,
                    "Warehouse paragraphs were not produced by the real selected-treasure view.");
                MeasureGroup(report, preview, description, "treasure-description",
                    CoreWarehouseCollection.Catalog.Select(treasure => Pair(treasure.Id, treasure.Description)));
                MeasureGroup(report, preview, story, "treasure-story",
                    CoreWarehouseCollection.Catalog.Select(treasure => Pair(treasure.Id, treasure.Story)));
            }
        }

        private static KeyValuePair<string, string> Pair(string id, string text) => new KeyValuePair<string, string>(id, text);

        private static void MeasureGroup(Report report, Preview preview, Text label, string category,
            IEnumerable<KeyValuePair<string, string>> values)
        {
            int start = report.items.Count;
            preview.SetTarget(1920, 1080);
            foreach (var pair in values) report.items.Add(Measure(label, category, pair.Key, pair.Value));
            preview.SetTarget(1280, 720);
            for (int index = start; index < report.items.Count; index++)
            {
                Item item = report.items[index];
                label.text = item.text;
                Canvas.ForceUpdateCanvases();
                item.at720 = CheckGeneration(label, label.GetGenerationSettings(label.rectTransform.rect.size));
                item.fit = item.at1080.fit && item.at720.fit;
            }
        }

        private static Item Measure(Text label, string category, string id, string text)
        {
            Require(label != null && label.font != null && label.gameObject.activeInHierarchy,
                "Missing active production text: " + category + "/" + id);
            Require(!string.IsNullOrWhiteSpace(text), "Empty narrative: " + category + "/" + id);
            label.text = text;
            Canvas.ForceUpdateCanvases();
            var item = new Item
            {
                category = category, id = id, label = label.name, text = text, textLength = text.Length,
                tail = text.Substring(Math.Max(0, text.Length - 24)), font = label.font.name,
                authoredFontSize = label.fontSize, bestFit = label.resizeTextForBestFit,
                minFontSize = label.resizeTextMinSize, maxFontSize = label.resizeTextMaxSize,
                width = label.rectTransform.rect.width, height = label.rectTransform.rect.height,
                lineSpacing = label.lineSpacing, preferredHeight = label.preferredHeight,
                alignment = label.alignment.ToString(), verticalOverflow = label.verticalOverflow.ToString()
            };
            TextGenerationSettings actual = label.GetGenerationSettings(label.rectTransform.rect.size);
            item.at1080 = CheckGeneration(label, actual);
            return item;
        }

        private static Generation CheckGeneration(Text label, TextGenerationSettings settings)
        {
            float scale = Mathf.Max(.01f, settings.scaleFactor);
            TextGenerator visible = label.cachedTextGenerator;
            visible.Invalidate();
            bool generated = visible.Populate(label.text, settings);
            int nativeBestFitSize = settings.resizeTextForBestFit ? visible.fontSizeUsedForBestFit : 0;
            Vector3[] visiblePositions = visible.verts.Select(vertex => vertex.position).ToArray();
            var result = new Generation
            {
                scale = scale, rawBestFitFontSize = nativeBestFitSize, visibleCharacters = visible.characterCountVisible,
                visibleVertices = visible.vertexCount, safeTop = SafeTop(label)
            };
            // Native best-fit reports a raster-space size (e.g. 10 at a
            // 2/3 Canvas scale for a logical size of 16). Feeding that value
            // back as fontSize scales it twice. Resolve the actual logical
            // size by reproducing the production mesh, without assuming the
            // native integer rounding rule or choosing a smaller font.
            int fontSize = settings.resizeTextForBestFit
                ? ResolveLogicalFontSize(label.text, settings, result, visiblePositions)
                : settings.fontSize;
            result.fontSize = fontSize;
            result.fontSizeMatchesActualMesh = fontSize > 0;
            if (fontSize <= 0) return result;
            Vector2 horizontal = SafeHorizontalBounds(label);
            result.safeMinX = horizontal.x;
            result.safeMaxX = horizontal.y;
            // Freeze only the size selected by the production generator. The
            // complete reference must not auto-shrink differently with overflow.
            TextGenerationSettings completeSettings = settings;
            completeSettings.resizeTextForBestFit = false;
            completeSettings.fontSize = fontSize;
            completeSettings.verticalOverflow = VerticalWrapMode.Overflow;
            completeSettings.generateOutOfBounds = true;
            using (var complete = new TextGenerator())
            using (var preferred = new TextGenerator())
            {
                bool fullGenerated = complete.Populate(label.text, completeSettings);
                result.completeCharacters = complete.characterCountVisible;
                result.completeVertices = complete.vertexCount;
                result.effectivePreferredHeight = preferred.GetPreferredHeight(label.text, completeSettings) / scale;
                Rect rect = label.rectTransform.rect;
                Bounds glyphs = GlyphBounds(complete, scale, out bool hasGlyphs);
                result.glyphMinX = glyphs.min.x; result.glyphMaxX = glyphs.max.x;
                result.glyphMinY = glyphs.min.y; result.glyphMaxY = glyphs.max.y;
                bool validSize = fontSize > 0 && (!settings.resizeTextForBestFit ||
                    fontSize >= settings.resizeTextMinSize && fontSize <= settings.resizeTextMaxSize);
                result.completeTail = result.visibleCharacters == result.completeCharacters &&
                    result.visibleVertices == result.completeVertices;
                // Raised CJK glyphs can extend above a Text's nominal top.
                // Keep them below the actual preceding paragraph/parent, just
                // as the existing dossier tests do, rather than masking them.
                result.boundsFit = hasGlyphs && glyphs.min.x >= horizontal.x - 1f && glyphs.max.x <= horizontal.y + 1f &&
                    glyphs.min.y >= rect.yMin - 1f && glyphs.max.y <= result.safeTop + .5f;
                result.fit = generated && fullGenerated && validSize && result.completeTail && result.boundsFit &&
                    result.effectivePreferredHeight <= rect.height + .5f;
            }
            return result;
        }

        private static int ResolveLogicalFontSize(string text, TextGenerationSettings settings,
            Generation visible, Vector3[] actualPositions)
        {
            TextGenerationSettings candidateSettings = settings;
            candidateSettings.resizeTextForBestFit = false;
            using (var candidate = new TextGenerator())
            {
                for (int logicalSize = settings.resizeTextMinSize; logicalSize <= settings.resizeTextMaxSize; logicalSize++)
                {
                    candidateSettings.fontSize = logicalSize;
                    if (!candidate.Populate(text, candidateSettings) || candidate.characterCountVisible != visible.visibleCharacters ||
                        candidate.vertexCount != visible.visibleVertices || candidate.verts.Count != actualPositions.Length) continue;
                    bool matches = true;
                    for (int index = 0; index < actualPositions.Length; index++)
                    {
                        if ((candidate.verts[index].position - actualPositions[index]).sqrMagnitude <= .00000001f) continue;
                        matches = false;
                        break;
                    }
                    if (matches) return logicalSize;
                }
            }
            return 0;
        }

        private static Bounds GlyphBounds(TextGenerator generator, float scale, out bool found)
        {
            Bounds bounds = default;
            found = false;
            IList<UIVertex> vertices = generator.verts;
            for (int index = 0; index + 3 < vertices.Count; index += 4)
            {
                // Legacy TextGenerator may append a zero-area sentinel quad.
                bool area = false;
                for (int corner = 1; corner < 4; corner++)
                    area |= (vertices[index + corner].position - vertices[index].position).sqrMagnitude > .000001f;
                if (!area) continue;
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector3 point = vertices[index + corner].position / scale;
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }
            return bounds;
        }

        private static float SafeTop(Text label)
        {
            RectTransform rect = label.rectTransform;
            RectTransform parent = rect.parent as RectTransform;
            if (parent == null) return rect.rect.yMax;
            float top = rect.InverseTransformPoint(parent.TransformPoint(new Vector3(0f, parent.rect.yMax))).y;
            var corners = new Vector3[4];
            foreach (Text other in parent.GetComponentsInChildren<Text>(false))
            {
                if (other == label || other.rectTransform.parent != parent) continue;
                other.rectTransform.GetWorldCorners(corners);
                Vector3 a = rect.InverseTransformPoint(corners[0]);
                Vector3 b = rect.InverseTransformPoint(corners[2]);
                if ((a.y + b.y) * .5f <= rect.rect.center.y || b.x <= rect.rect.xMin || a.x >= rect.rect.xMax) continue;
                top = Mathf.Min(top, a.y);
            }
            return top;
        }

        private static Vector2 SafeHorizontalBounds(Text label)
        {
            string surfaceName = label.name.StartsWith("Component ", StringComparison.Ordinal) ? "Component Detail Parchment" :
                label.name == "Shipyard Dossier Story" ? "Shipyard Dossier Parchment" :
                label.name == "Shipyard Dossier Specialization" ? "Shipyard Description Panel" : "Warehouse Ledger";
            RectTransform surface = label.transform.root.GetComponentsInChildren<RectTransform>(true)
                .Single(rect => rect.name == surfaceName);
            Rect available = BoundsIn(label.rectTransform, surface);
            float left = available.xMin, right = available.xMax;
            // Paragraph rects do not clip their glyph side-bearings. Actual
            // paper/panel edges and any active clip masks remain hard limits.
            foreach (RectMask2D mask in label.GetComponentsInParent<RectMask2D>(false))
                if (mask.isActiveAndEnabled) { Rect b = BoundsIn(label.rectTransform, mask.rectTransform); left = Mathf.Max(left, b.xMin); right = Mathf.Min(right, b.xMax); }
            foreach (Mask mask in label.GetComponentsInParent<Mask>(false))
                if (mask.isActiveAndEnabled) { Rect b = BoundsIn(label.rectTransform, mask.rectTransform); left = Mathf.Max(left, b.xMin); right = Mathf.Min(right, b.xMax); }
            Rect paragraph = label.rectTransform.rect;
            foreach (RectTransform sibling in label.transform.parent.Cast<Transform>().OfType<RectTransform>())
            {
                if (sibling == label.rectTransform || !sibling.gameObject.activeInHierarchy || sibling.GetComponent<Graphic>() == null) continue;
                Rect b = BoundsIn(label.rectTransform, sibling);
                if (b.yMax <= paragraph.yMin || b.yMin >= paragraph.yMax) continue;
                if (b.xMax <= paragraph.xMin) left = Mathf.Max(left, b.xMax);
                else if (b.xMin >= paragraph.xMax) right = Mathf.Min(right, b.xMin);
            }
            return new Vector2(left, right);
        }

        private static Rect BoundsIn(RectTransform relative, RectTransform item)
        {
            var corners = new Vector3[4];
            item.GetWorldCorners(corners);
            Vector3 a = relative.InverseTransformPoint(corners[0]);
            Vector3 b = relative.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        private sealed class Preview : IDisposable
        {
            private readonly Scene scene;
            private readonly GameObject host;
            private Camera camera;
            private RenderTexture target1080, target720;
            public readonly CoreGameplayDemoController Controller;
            public Preview(CoreGameplayPreviewScreen screen)
            {
                scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    host = new GameObject("Disposable Narrative Layout " + screen);
                    host.SetActive(false);
                    SceneManager.MoveGameObjectToScene(host, scene);
                    Controller = host.AddComponent<CoreGameplayDemoController>();
                    Controller.ConfigureEditorPreview(screen, CoreGameplayPreviewPort.GrayGull, 0);
                    host.SetActive(true);
                    Controller.RefreshEditorPreview();
                    Canvas.ForceUpdateCanvases();
                }
                catch { Dispose(); throw; }
            }
            public T Find<T>(string name) where T : Component => host.GetComponentsInChildren<T>(true).Single(item => item.name == name);
            public void SetTarget(int width, int height)
            {
                Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Graphics required for real CanvasScaler targets; omit -nographics.");
                Canvas canvas = Find<Canvas>(CoreGameplayDemoController.EditorPreviewCanvasName);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                Require(scaler != null && scaler.enabled, "Production CanvasScaler must stay enabled.");
                if (camera == null)
                {
                    var cameraHost = new GameObject("Disposable Narrative Camera", typeof(Camera));
                    SceneManager.MoveGameObjectToScene(cameraHost, scene);
                    camera = cameraHost.GetComponent<Camera>();
                    camera.enabled = false; camera.scene = scene;
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                    camera.orthographic = true; camera.orthographicSize = 5f;
                    camera.nearClipPlane = .1f; camera.farClipPlane = 2000f;
                    camera.transform.position = new Vector3(0f, 0f, -1000f);
                    camera.cullingMask = 1 << 31; camera.allowHDR = false; camera.allowMSAA = false;
                }
                RenderTexture target = width == 1920 ? target1080 : target720;
                if (target == null)
                {
                    target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    Require(target.Create(), "Cannot create narrative audit render target.");
                    if (width == 1920) target1080 = target; else target720 = target;
                }
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                camera.targetTexture = target; camera.rect = new Rect(0f, 0f, 1f, 1f); camera.aspect = width / (float)height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 100f;
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", PrivateInstance);
                Require(handle != null, "CanvasScaler.Handle missing.");
                for (int pass = 0; pass < 4; pass++)
                {
                    Canvas.ForceUpdateCanvases(); handle.Invoke(scaler, null);
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                float expected = Mathf.Min(width / 1920f, height / 1080f);
                Require(Mathf.Abs(canvas.renderingDisplaySize.x - width) <= .5f && Mathf.Abs(canvas.renderingDisplaySize.y - height) <= .5f &&
                    Mathf.Abs(canvas.pixelRect.width - width) <= .5f && Mathf.Abs(canvas.pixelRect.height - height) <= .5f &&
                    Mathf.Abs(canvas.scaleFactor - expected) < .001f, "Narrative target did not drive the actual production CanvasScaler.");
            }
            public void Dispose()
            {
                if (host != null) Object.DestroyImmediate(host);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                foreach (RenderTexture target in new[] { target1080, target720 })
                    if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }

        private static T Read<T>(object owner, string field) => (T)owner.GetType().GetField(field, PrivateInstance).GetValue(owner);
        private static void Set(object owner, string name, object value)
        {
            FieldInfo field = owner.GetType().GetField(name, PrivateInstance);
            if (field != null) { field.SetValue(owner, value); return; }
            PropertyInfo property = owner.GetType().GetProperty(name, PrivateInstance);
            if (property != null && property.GetSetMethod(true) != null) { property.SetValue(owner, value); return; }
            throw new MissingMemberException(owner.GetType().FullName, name + " (writable field/property required)");
        }
        private static void Invoke(object owner, string method) => owner.GetType().GetMethod(method, PrivateInstance).Invoke(owner, null);
        private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
        private static string ReportPath()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-textLayoutReport");
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
                throw new ArgumentException("-textLayoutReport requires a new absolute JSON path under Artifacts.");
            string path = Path.GetFullPath(args[index + 1]);
            if (!path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => string.Equals(part, "Artifacts", StringComparison.OrdinalIgnoreCase)) ||
                !string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase) || File.Exists(path))
                throw new ArgumentException("Use a new .json report under Artifacts; existing evidence is never overwritten.");
            return path;
        }

        [Serializable] private sealed class Report
        {
            public string status = "running", startedUtc, completedUtc, error;
            public string scope = "269 catalogue strings bound to six real production Text layouts in disposable EditorPreview scenes. No font/size/geometry edits; production best-fit only. Native raster-space best-fit size is recorded separately; the logical reference size must reproduce every actual production mesh vertex within the authored size range. Actual ScreenSpaceCamera/RenderTexture targets 1920x1080 and 1280x720 drive and verify the enabled production CanvasScaler. Checks complete generated characters/vertices, preferred height, vertical paragraph/neighbour bounds, horizontal paper/panel and clip boundaries. No scene/asset/PlayerPrefs writes.";
            public int failed;
            public List<Item> items = new List<Item>();
        }
        [Serializable] private sealed class Item
        {
            public string category, id, label, text, tail, font, alignment, verticalOverflow;
            public int textLength, authoredFontSize, minFontSize, maxFontSize;
            public bool bestFit, fit;
            public float width, height, lineSpacing, preferredHeight;
            public Generation at1080, at720;
        }
        [Serializable] private sealed class Generation
        {
            public float scale, effectivePreferredHeight, glyphMinX, glyphMaxX, glyphMinY, glyphMaxY, safeTop, safeMinX, safeMaxX;
            public int fontSize, rawBestFitFontSize, visibleCharacters, completeCharacters, visibleVertices, completeVertices;
            public bool fontSizeMatchesActualMesh, completeTail, boundsFit, fit;
        }
    }
}
