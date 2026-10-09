using System;
using System.Linq;
using System.Reflection;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    /// <summary>Repairs audited geometry in place, retaining scene objects and authored placements.</summary>
    public static class UiImageQualitySceneSync
    {
        private static readonly string[] ShipRoots = { "Player Ship", "Player Ship Art", "Starter Modular Ship",
            "Maintenance Modular Ship", "Shipyard Modular Ship", "Shipyard Dossier Modular Ship" };

        public static int RepairScene(Scene scene)
        {
            int repaired = 0;
            foreach (var host in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CoreGameplayDemoController>(true)))
            {
                foreach (RectTransform root in host.GetComponentsInChildren<RectTransform>(true))
                {
                    if (ShipRoots.Contains(root.name))
                    {
                        RawImage hull = root.GetComponentsInChildren<RawImage>(true).FirstOrDefault(i => i.name == root.name + " Hull" || i.name == root.name + " Base");
                        if (hull != null)
                        {
                            Vector2 oldSize = hull.rectTransform.rect.size;
                            if (hull.name.EndsWith(" Hull") && hull.texture != null)
                            {
                                Rect uv = hull.uvRect;
                                int columns = hull.texture.name == "ship_sprite_sheet" ? 4 : 2;
                                float width = 1f / columns, height = .5f;
                                float left = Mathf.Clamp(Mathf.FloorToInt(uv.center.x * columns),0,columns-1) * width;
                                float bottom = Mathf.Clamp(Mathf.FloorToInt(uv.center.y * 2),0,1) * height;
                                Rect cell = new Rect(uv.width < 0 ? left + width : left,uv.height < 0 ? bottom + height : bottom,
                                    (uv.width < 0 ? -1 : 1)*width,(uv.height < 0 ? -1 : 1)*height);
                                hull.uvRect = UiImageAspect.InsetUv(hull.texture,cell,10);
                            }
                            UiImageAspect.Fit(hull, root.rect.size);
                            Vector2 size = hull.rectTransform.rect.size;
                            float sx = size.x / Mathf.Max(.001f,oldSize.x), sy = size.y / Mathf.Max(.001f,oldSize.y);
                            foreach (RawImage layer in root.GetComponentsInChildren<RawImage>(true).Where(i => i.name.StartsWith(root.name + " Layer ",StringComparison.Ordinal)))
                            {
                                Vector3 origin = layer.transform.parent.InverseTransformPoint(hull.transform.position);
                                Vector3 delta = layer.transform.localPosition - origin;
                                layer.transform.localPosition = origin + new Vector3(delta.x*sx,delta.y*sy,delta.z);
                                layer.rectTransform.sizeDelta *= sx;
                            }
                            if (root.name == "Player Ship Art") ScaleWater(root.parent,"Player Ship Water ",sx);
                            repaired++;
                        }
                    }
                    if (root.name == "Boarding Clash Zone") { RepairMarker(root); repaired++; }
                }
                foreach (RawImage image in host.GetComponentsInChildren<RawImage>(true))
                {
                    string name = image.name;
                    if (name.StartsWith("Warehouse Inventory Art ")) { UiImageAspect.Fit(image,new Vector2(126,126)); repaired++; }
                    if (name.StartsWith("Warehouse Displayed Art ")) { FitAtBottom(image,new Vector2(176,176)); repaired++; }
                    if (name == "Enemy Ship Art")
                    {
                        float oldWidth = image.rectTransform.rect.width;
                        UiImageAspect.Fit(image,new Vector2(320,240));
                        ScaleWater(image.transform.parent,"Enemy Ship Water ",image.rectTransform.rect.width/Mathf.Max(.001f,oldWidth)); repaired++;
                    }
                    if (name == "Player Boarding Deck Hull Silhouette" || name == "Enemy Boarding Deck Hull Silhouette")
                    {
                        float oldWidth = image.rectTransform.rect.width;
                        FitAtBottom(image,new Vector2(650,184));
                        Transform water = image.transform.parent.parent.Find(name.StartsWith("Player") ? "Player Boarding Water Contact" : "Enemy Boarding Water Contact");
                        if (water is RectTransform waterRect) waterRect.sizeDelta = new Vector2(waterRect.sizeDelta.x * image.rectTransform.rect.width / Mathf.Max(.001f,oldWidth),waterRect.sizeDelta.y);
                        typeof(CoreGameplayDemoController).GetMethod("ConfigureBoardingDeckGeometry",BindingFlags.NonPublic | BindingFlags.Static)
                            .Invoke(null,new object[] { image.transform.parent as RectTransform,image.rectTransform.rect.width,name.StartsWith("Enemy") });
                        repaired++;
                    }
                    if (name.StartsWith("Player Ship Art Sailor ") || name.StartsWith("Enemy Ship Art Sailor "))
                    { FitAtBottom(image, Mathf.Abs(image.uvRect.width) > .99f && Mathf.Abs(image.uvRect.height) > .99f ? new Vector2(80,124) : new Vector2(88,106)); repaired++; }
                }
                foreach (RectTransform battleRoot in host.GetComponentsInChildren<RectTransform>(true).Where(r => r.name == "Battle Card" && r.Find("Player Boarding Deck") != null))
                {
                    typeof(CoreGameplayDemoController).GetMethod("UpdateBoardingConnections",BindingFlags.NonPublic | BindingFlags.Static)
                        .Invoke(null,new object[] { battleRoot });
                    PersistBoardingConnectionAnchors(battleRoot);
                }
            }
            return repaired;
        }

        public static void RepairSavedBoardingFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stopped editor required.");
            foreach (string suffix in new[] { "Hook", "Ladder", "Ram" })
            {
                string path = "Assets/_Project/Scenes/UIPreviewsForMe/UIBattle/UIBattle_Boarding" + suffix + ".unity";
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                RectTransform root = BoardingRoot(scene);
                PersistBoardingConnectionAnchors(root);
                AssertBoardingConnections(root, path + " before save");
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene,path)) throw new InvalidOperationException("Cannot save " + path);
                foreach (string method in new[] { "RepairSavedNavigationCanvasScale", "AssertSavedNavigationCanvasScale" })
                    typeof(GameplayPreviewSceneBuilder).GetMethod(method,BindingFlags.NonPublic | BindingFlags.Static)
                        .Invoke(null,new object[] { System.IO.Path.GetFullPath(path) });
                scene = EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                AssertBoardingConnections(BoardingRoot(scene),path + " after reload");
                Debug.Log("UI_SAVED_BOARDING_ENDPOINTS_OK: " + path);
            }
        }

        private static RectTransform BoardingRoot(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
            .Single(rect => rect.name == "Battle Card" && rect.Find("Player Boarding Deck") != null);

        private static void VisitBoardingConnections(RectTransform root, Action<RectTransform,Vector3,Vector3> action)
        {
            RectTransform player = (RectTransform)root.Find("Player Boarding Deck");
            RectTransform enemy = (RectTransform)root.Find("Enemy Boarding Deck");
            Vector3 playerMiddle = Vector3.zero, enemyMiddle = Vector3.zero;
            for (int index = 0; index < 2; index++)
            {
                RectTransform playerCap = (RectTransform)player.Find(player.name + " Cleat Cap " + index);
                RectTransform enemyCap = (RectTransform)enemy.Find(enemy.name + " Cleat Cap " + index);
                Vector3 from = PointInBoardingRoot(playerCap,playerCap.rect.center,root);
                Vector3 to = PointInBoardingRoot(enemyCap,enemyCap.rect.center,root);
                playerMiddle += from * .5f; enemyMiddle += to * .5f;
                action((RectTransform)root.Find("Boarding Grapple Rope " + index),from,to);
            }
            action((RectTransform)root.Find("Boarding Gangplank"),playerMiddle,enemyMiddle);
        }

        private static Vector3 PointInBoardingRoot(Transform item,Vector3 point,Transform root)
        {
            // Batch-mode overlay canvases can temporarily have a driven zero
            // scale. Work within the battle card instead of inverting that scale.
            for (Transform current = item; current != root; current = current.parent)
            {
                if (current == null) throw new InvalidOperationException("Boarding object left its card hierarchy.");
                point = Matrix4x4.TRS(current.localPosition,current.localRotation,current.localScale).MultiplyPoint3x4(point);
            }
            return point;
        }

        private static void PersistBoardingConnectionAnchors(RectTransform root)
        {
            VisitBoardingConnections(root,(connection,from,to) =>
            {
                RectTransform parent = (RectTransform)connection.parent;
                if (parent != root) throw new InvalidOperationException("Boarding connection must belong to its battle card.");
                Vector2 middle = (from + to) * .5f;
                Vector2 anchor = connection.anchorMin + Vector2.Scale(connection.anchorMax - connection.anchorMin,connection.pivot);
                Vector2 anchorOrigin = parent.rect.min + Vector2.Scale(parent.rect.size,anchor);
                // Write the serialized anchor representation explicitly after
                // in-place localPosition geometry changes in an editor scene.
                connection.anchoredPosition = middle - anchorOrigin;
                EditorUtility.SetDirty(connection);
            });
        }

        private static void AssertBoardingConnections(RectTransform root,string context)
        {
            VisitBoardingConnections(root,(connection,from,to) =>
            {
                float first = Vector3.Distance(PointInBoardingRoot(connection,new Vector3(connection.rect.xMin,0,0),root),from);
                float last = Vector3.Distance(PointInBoardingRoot(connection,new Vector3(connection.rect.xMax,0,0),root),to);
                if (first > .02f || last > .02f || float.IsNaN(first) || float.IsNaN(last))
                    throw new InvalidOperationException(context + ": " + connection.name + " endpoint error=" + first + "," + last);
            });
        }

        private static void ScaleWater(Transform parent,string prefix,float scale)
        {
            foreach (string suffix in new[] { "Shadow", "Contact" })
                if (parent.Find(prefix + suffix) is RectTransform water)
                    water.sizeDelta = new Vector2(water.sizeDelta.x * scale,water.sizeDelta.y);
        }

        private static void FitAtBottom(RawImage image,Vector2 bounds)
        {
            RectTransform rect = image.rectTransform;
            float bottom = rect.anchoredPosition.y - rect.rect.height * rect.pivot.y;
            UiImageAspect.Fit(image,bounds);
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x,bottom + rect.rect.height * rect.pivot.y);
        }

        private static void RepairMarker(RectTransform zone)
        {
            zone.sizeDelta = new Vector2(244,68);
            Image background = zone.GetComponent<Image>();
            background.sprite = background.overrideSprite = null; background.type = Image.Type.Simple;
            background.color = new Color(.02f,.04f,.05f,.96f); background.raycastTarget = false;
            Text label = zone.Find("Boarding Clash Label")?.GetComponent<Text>();
            if (label == null) throw new InvalidOperationException("Boarding marker is missing its label.");
            label.rectTransform.anchoredPosition = new Vector2(29,0); label.rectTransform.sizeDelta = new Vector2(158,50);
            label.fontSize = 17; label.lineSpacing = 1.1f; label.alignment = TextAnchor.MiddleLeft;
            label.color = new Color32(232,225,199,255); label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate; label.raycastTarget = false;
            Transform oldGlyph = zone.Find("Boarding Clash Glyph"); if (oldGlyph != null) oldGlyph.gameObject.SetActive(false);
            RectTransform icon = zone.Find("Boarding Clash Mark") as RectTransform;
            if (icon == null) icon = MakeRect("Boarding Clash Mark",zone,new Vector2(40,42),new Vector2(-88,0));
            icon.sizeDelta = new Vector2(40,42); icon.anchoredPosition = new Vector2(-88,0);
            Image oldCircle = icon.GetComponent<Image>(); if (oldCircle != null) oldCircle.enabled = false;
            Color accent = label.text.Contains("侧舷") ? new Color32(206,103,80,255) : new Color32(216,165,65,255);
            for (int index=0;index<2;index++)
            {
                if (icon.Find("Boarding Clash Sword " + index) != null) continue;
                RectTransform sword = MakeRect("Boarding Clash Sword " + index,icon,new Vector2(12,38),Vector2.zero);
                sword.localRotation = Quaternion.Euler(0,0,index == 0 ? -40 : 40);
                MakePart("Blade",sword,new Vector2(4,26),new Vector2(0,5),label.color);
                MakePart("Guard",sword,new Vector2(13,3),new Vector2(0,-8),accent);
                MakePart("Grip",sword,new Vector2(4,9),new Vector2(0,-13),accent);
            }
        }
        private static RectTransform MakeRect(string name,Transform parent,Vector2 size,Vector2 position)
        {
            var host = new GameObject(name,typeof(RectTransform)); var rect = host.GetComponent<RectTransform>();
            rect.SetParent(parent,false); rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        private static void MakePart(string name,Transform parent,Vector2 size,Vector2 position,Color color)
        { var image = MakeRect(name,parent,size,position).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; }
    }
}
