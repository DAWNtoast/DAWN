// Task-only evidence: copy into the owned isolated project's Editor folder.
// Builds production card faces and keyword bubbles in an unsaved preview scene.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    public static class CardKeywordTooltipCapture
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        public static void Run()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (!project.EndsWith("/tmp/card-keyword-tooltip-20261023-validation", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Use the owned tooltip validation project.");
            string output = "D:/Game/Artifacts/CardKeywordTooltip20261023/Capture";
            Directory.CreateDirectory(output);
            Scene scene = EditorSceneManager.NewPreviewScene();
            var controllerHost = new GameObject("Inactive capture controller");
            controllerHost.SetActive(false);
            SceneManager.MoveGameObjectToScene(controllerHost, scene);
            CoreGameplayDemoController controller = controllerHost.AddComponent<CoreGameplayDemoController>();
            try
            {
                foreach (string id in new[] { "card_load_shot", "card_boarding_fire_11", "card_boarding_fire_12" })
                {
                    var canvasHost = new GameObject("Production Card Hover", typeof(RectTransform), typeof(Canvas));
                    SceneManager.MoveGameObjectToScene(canvasHost, scene);
                    Canvas canvas = canvasHost.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.WorldSpace;
                    RectTransform viewport = canvasHost.GetComponent<RectTransform>();
                    viewport.sizeDelta = new Vector2(1280, 900);
                    CoreBattleCard card = CoreVoyageSession.CardLibrary.Single(value => value.Id == id);
                    RectTransform face = (RectTransform)typeof(CoreGameplayDemoController).GetMethod("CreateCollectionCardFace", Private)
                        .Invoke(controller, new object[] { canvas.transform, card, "Capture", "Card Art", "Card Title", 1.35f, null, null });
                    face.anchoredPosition = new Vector2(180f, 0f);
                    RectTransform bubble = (RectTransform)typeof(CoreGameplayDemoController).GetMethod("CreateCardKeywordBubble", Private)
                        .Invoke(controller, new object[] { face, 0, card });
                    if (bubble == null) throw new InvalidOperationException("Expected keywords for " + id);
                    bubble.gameObject.SetActive(true);
                    bubble.GetComponent<CanvasGroup>().alpha = 1f;
                    bubble.GetComponent<CardKeywordTooltip>().RefreshPosition();
                    Capture(canvas, scene, Path.Combine(output, id + ".png"));
                    string[] expected = CoreBattleCardKeywordText.Build(card).SelectMany(entry => new[] { entry.Keyword, entry.Meaning }).ToArray();
                    string[] actual = bubble.GetComponentsInChildren<Text>().Select(label => label.text).ToArray();
                    if (!expected.SequenceEqual(actual)) throw new InvalidOperationException("Non-glossary text in " + id);
                    foreach (Text text in bubble.GetComponentsInChildren<Text>())
                        if (text.cachedTextGenerator.vertexCount < 4) throw new InvalidOperationException("Missing tooltip glyphs: " + text.text);
                    File.WriteAllLines(Path.Combine(output, id + ".txt"), new[] { card.Name, card.Description, "--- Tooltip ---" }.Concat(actual));
                    Object.DestroyImmediate(canvasHost);
                }
            }
            finally
            {
                Object.DestroyImmediate(controllerHost);
                EditorSceneManager.ClosePreviewScene(scene);
            }
            Debug.Log("CARD_KEYWORD_TOOLTIP_CAPTURE_OK");
        }

        private static void Capture(Canvas canvas, Scene scene, string path)
        {
            const int width = 1280, height = 900, layer = 30;
            var cameraHost = new GameObject("Tooltip capture camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraHost, scene);
            Camera camera = cameraHost.GetComponent<Camera>();
            camera.enabled = false;
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.075f, .09f, .10f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = height * .5f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 2000f;
            camera.transform.position = new Vector3(0, 0, -1000);
            camera.cullingMask = 1 << layer;
            camera.allowHDR = camera.allowMSAA = false;
            camera.aspect = width / (float)height;
            canvas.worldCamera = camera;
            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                target.Create();
                camera.targetTexture = target;
                for (int pass = 0; pass < 3; pass++)
                {
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    camera.Render();
                }
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) Object.DestroyImmediate(pixels);
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraHost);
            }
        }
    }
}
