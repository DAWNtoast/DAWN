using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    /// <summary>Patch only audited UI objects; retain authored scene art and placements.</summary>
    public static class UiReadabilitySceneSync
    {
        public static void SyncFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stopped editor required.");
            int changed = 0;
            foreach (string file in Directory.GetFiles("Assets/_Project/Scenes/UIPreviewsForMe", "*.unity", SearchOption.AllDirectories))
            {
                string path = file.Replace('\\', '/');
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var hosts = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CoreGameplayDemoController>(true)).ToArray();
                bool touched = UiImageQualitySceneSync.RepairScene(scene) > 0;
                foreach (var host in hosts)
                {
                    foreach (Canvas canvas in host.GetComponentsInChildren<Canvas>(true))
                        if (canvas.name == CoreGameplayDemoController.EditorPreviewCanvasName && !canvas.enabled)
                        { canvas.enabled = true; touched = true; }
                    foreach (RawImage image in host.GetComponentsInChildren<RawImage>(true))
                    {
                        if (image.name.StartsWith("Route Preview ") || image.name == "Dispatch Deployment Chart" || image.name.StartsWith("Event Icon "))
                        { UiImageAspect.Fit(image, image.rectTransform.sizeDelta); touched = true; }
                    }
                    foreach (Text label in host.GetComponentsInChildren<Text>(true))
                    {
                        string name = label.name;
                        if (name == "Label" && label.transform.parent.name == "Settlement Store All Cargo")
                        { RepairSettlementCheckbox(label); touched = true; }
                        if (name.StartsWith("Port Label ") || name == "Port Hub Hint" || name.StartsWith("Tavern Crew Name ") ||
                            name.StartsWith("Tavern Crew Status ") || name.StartsWith("Card Workshop Chest Name "))
                        { UiLabelBacking.Apply(label, name.StartsWith("Tavern Crew Status ") || name.StartsWith("Card Workshop Chest Name ")); touched = true; }
                        if (name == "Port Investment Level")
                        { label.color = new Color32(0x24, 0x17, 0x0F, 255); foreach (Shadow shadow in label.GetComponents<Shadow>()) shadow.enabled = false; touched = true; }
                        if (name.StartsWith("Component Shop Plaque Name ")) { label.resizeTextMinSize = 18; UiLabelBacking.Apply(label, true); touched = true; }
                        if (name.StartsWith("Component Shop Plaque Status ")) { label.resizeTextMinSize = 14; UiLabelBacking.Apply(label, true); touched = true; }
                        if (name == "Battle Card Cost Rule" || name == "Battle Curse Status")
                        { UiLabelBacking.Apply(label, true); touched = true; }
                        if (name.StartsWith("Component Shop Offer Suitability "))
                        { label.fontSize = label.resizeTextMaxSize = 16; label.resizeTextMinSize = 14; label.rectTransform.sizeDelta = new Vector2(310,34); UiLabelBacking.Apply(label); touched = true; }
                        if (name == "Keyword Bubble Title")
                        {
                            float height = label.transform.parent.GetComponent<RectTransform>().rect.height;
                            float extra = Mathf.Abs(height - 432) < 1 || Mathf.Abs(height - 662) < 1 ? 110 : 0;
                            label.rectTransform.sizeDelta = new Vector2(384,64); label.rectTransform.anchoredPosition = new Vector2(0,232 + extra*.5f);
                            label.fontSize = label.resizeTextMaxSize = 24; label.resizeTextMinSize = 22;
                            label.text = label.text.Replace("<size=12>","<size=18>"); touched = true;
                        }
                        if (name == "Keyword Bubble Text")
                        {
                            float height = label.transform.parent.GetComponent<RectTransform>().rect.height;
                            float extra = Mathf.Abs(height - 432) < 1 || Mathf.Abs(height - 662) < 1 ? 110 : 0;
                            label.rectTransform.sizeDelta = new Vector2(384,456 + extra); label.rectTransform.anchoredPosition = new Vector2(0,-36);
                            label.fontSize = label.resizeTextMaxSize = 20; label.resizeTextMinSize = 18; touched = true;
                        }
                        if (label.transform.parent.name == "Voyage Setup")
                        {
                            if (name == "Ship Stats") SetLabel(label, new Vector2(240,108), new Vector2(125,180),18);
                            if (name == "Cargo") SetLabel(label, new Vector2(520,52), new Vector2(0,-122),16);
                            if (name == "Departure Provision Quote") SetLabel(label,new Vector2(520,62),new Vector2(0,-182),16);
                            touched = true;
                        }
                    }
                    foreach (RectTransform rect in host.GetComponentsInChildren<RectTransform>(true))
                    {
                        if (rect.name.StartsWith("Component Shop Plaque Writing Area ") ||
                            rect.name == "Battle Card Cost Rule Panel" || rect.name == "Battle Curse Panel")
                        {
                            Image backing = rect.GetComponent<Image>();
                            if (backing != null) { backing.enabled = false; backing.raycastTarget = false; touched = true; }
                        }
                        if (rect.name.StartsWith("Card Keyword Bubble "))
                        {
                            // Old normal/healing bubbles are 342/432px; repeated runs
                            // recognize the new 552/662px sizes without growing again.
                            float extra = Mathf.Abs(rect.sizeDelta.y - 432) < 1 || Mathf.Abs(rect.sizeDelta.y - 662) < 1 ? 110 : 0;
                            bool right = rect.anchoredPosition.x > 0;
                            rect.sizeDelta = new Vector2(420,552 + extra); rect.anchoredPosition = new Vector2(right ? 322 : -322,118 + extra*.5f);
                            RectTransform pointer = rect.Find("Keyword Bubble Pointer") as RectTransform;
                            if (pointer != null) pointer.anchoredPosition = new Vector2(right ? -200 : 200,-78 - extra*.5f);
                            touched = true;
                        }
                        if (rect.name == "Voyage Setup")
                        { Image bg = rect.GetComponent<Image>(); if (bg != null) { bg.sprite = null; bg.color = new Color(.075f,.045f,.025f,.97f); } touched = true; }
                        if (rect.parent != null && rect.parent.name == "Voyage Setup" &&
                            (rect.name == "Departure Buy Provisions" || rect.name == "Adjust Voyage Supplies"))
                        { rect.anchoredPosition = new Vector2(rect.anchoredPosition.x,-237); touched = true; }
                        if (rect.name == "Navigation Chart" || rect.name == "Navigation Controls" ||
                            rect.name == "Forward View Panel" || rect.name == "Navigation Journal")
                        {
                            float fit = Mathf.Min(rect.localScale.x, rect.localScale.y);
                            if (Mathf.Abs(rect.localScale.x - rect.localScale.y) > .0001f)
                            { rect.anchoredPosition = new Vector2(rect.anchoredPosition.x * fit / rect.localScale.x,rect.anchoredPosition.y); rect.localScale = Vector3.one * fit; touched = true; }
                        }
                    }
                    foreach (ScrollRect scroll in host.GetComponentsInChildren<ScrollRect>(true))
                        if (scroll.name == "Deck Builder Deck Scroll Viewport" && scroll.verticalScrollbar == null)
                        { AddScrollAffordance(scroll); touched = true; }
                }
                if (!touched) continue;
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene,path)) throw new IOException("Could not save " + path);
                // In batch mode SaveScene can serialize the driven overlay scale
                // as zero. Use the project's targeted root-scale repair after saving.
                foreach (string method in new[] { "RepairSavedNavigationCanvasScale", "AssertSavedNavigationCanvasScale" })
                    typeof(GameplayPreviewSceneBuilder).GetMethod(method,BindingFlags.NonPublic | BindingFlags.Static)
                        .Invoke(null,new object[] { Path.GetFullPath(path) });
                changed++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("UI_READABILITY_SYNC_OK: " + changed + " authored scenes patched.");
        }

        private static void SetLabel(Text label, Vector2 size, Vector2 position, int fontSize)
        {
            label.rectTransform.sizeDelta = size;
            label.rectTransform.anchoredPosition = position;
            label.fontSize = fontSize;
            label.resizeTextForBestFit = false;
            label.color = new Color(.96f,.92f,.82f,1);
        }

        private static void RepairSettlementCheckbox(Text caption)
        {
            Transform parent = caption.transform.parent;
            bool selected = caption.text.StartsWith("☑",StringComparison.Ordinal);
            caption.text = "是否将所有货物置入仓库";
            caption.rectTransform.anchoredPosition = new Vector2(18,0);
            caption.rectTransform.sizeDelta = new Vector2(530,36);
            if (parent.Find("Settlement Cargo Checkbox") != null) return;
            Image box = CheckboxImage("Settlement Cargo Checkbox",parent,new Vector2(24,24),new Vector2(-267,0),new Color32(232,225,199,255));
            Image inset = CheckboxImage("Settlement Cargo Checkbox Interior",box.transform,new Vector2(18,18),Vector2.zero,new Color32(10,29,37,255));
            Image fill = CheckboxImage("Settlement Cargo Selected",inset.transform,new Vector2(12,12),Vector2.zero,new Color32(216,165,65,255));
            fill.gameObject.SetActive(selected);
        }
        private static Image CheckboxImage(string name,Transform parent,Vector2 size,Vector2 position,Color color)
        {
            var host = new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            RectTransform rect = host.GetComponent<RectTransform>(); rect.SetParent(parent,false); rect.sizeDelta = size; rect.anchoredPosition = position;
            Image image = host.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }

        private static void AddScrollAffordance(ScrollRect scroll)
        {
            RectTransform rail = scroll.transform.parent as RectTransform;
            var track = new GameObject("Deck Builder Scrollbar",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Scrollbar));
            var rect = track.GetComponent<RectTransform>(); rect.SetParent(rail,false);
            rect.sizeDelta = new Vector2(10,scroll.viewport.rect.height); rect.anchoredPosition = new Vector2(182,-17);
            var image = track.GetComponent<Image>(); image.color = new Color(.18f,.12f,.07f,.3f);
            var handleHost = new GameObject("Handle",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            var handle = handleHost.GetComponent<RectTransform>(); handle.SetParent(rect,false); handle.sizeDelta = Vector2.zero;
            var handleImage = handleHost.GetComponent<Image>(); handleImage.color = new Color32(96,66,34,255);
            var bar = track.GetComponent<Scrollbar>(); bar.handleRect = handle; bar.targetGraphic = handleImage; bar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar;
            track.SetActive(scroll.content.rect.height > scroll.viewport.rect.height);
        }
    }
}
