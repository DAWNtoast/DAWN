using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private sealed class WarehouseRetainedNode
        {
            public RectTransform Rect;
            public int Revision;
        }

        // This cache belongs to one open warehouse. Stable item keys keep the
        // same row/card objects when sorted, paged or switched between tabs.
        // Entries absent from a binding pass are hidden, not destroyed.
        private readonly Dictionary<Transform, Dictionary<string, WarehouseRetainedNode>> warehouseRetainedNodes =
            new Dictionary<Transform, Dictionary<string, WarehouseRetainedNode>>();
        private RectTransform warehouseRetainedRoot;
        private RectTransform warehouseRetainedContent;
        private CoreVoyageSession warehouseRetainedSession;
        private string warehouseRetainedPort;
        private bool warehouseRetainedBuilding;

        private void ReleaseWarehouseRetainedView()
        {
            warehouseRetainedBuilding = false;
            warehouseRetainedNodes.Clear();
            warehouseRetainedRoot = null;
            warehouseRetainedContent = null;
            warehouseRetainedSession = null;
            warehouseRetainedPort = null;
        }

        private bool TryRefreshWarehousePresentation()
        {
            if (!Application.isPlaying || editorPreviewBuilding || session == null ||
                session.Phase != CoreDemoPhase.Port || activePortFacility != 0 ||
                !ReferenceEquals(session, warehouseRetainedSession) || CurrentWarehousePort != warehouseRetainedPort ||
                warehouseRetainedContent != contentRoot || warehouseRetainedRoot == null ||
                warehouseRetainedRoot.parent != contentRoot || !warehouseRetainedRoot.gameObject.activeInHierarchy)
                return false;
            BuildWarehouseFacility();
            return true;
        }

        private void BeginWarehouseRetainedView()
        {
            warehouseRetainedBuilding = Application.isPlaying && !editorPreviewBuilding;
            if (!warehouseRetainedBuilding) return;
            // ClearChildren disables old objects before Unity's deferred Destroy.
            // Never adopt those objects into a newly built phase or port view.
            if (warehouseRetainedRoot == null || !warehouseRetainedRoot.gameObject.activeInHierarchy ||
                warehouseRetainedRoot.parent != contentRoot || warehouseRetainedContent != contentRoot ||
                !ReferenceEquals(warehouseRetainedSession, session) || warehouseRetainedPort != CurrentWarehousePort)
                warehouseRetainedNodes.Clear();
            warehouseRetainedContent = contentRoot;
            warehouseRetainedSession = session;
            warehouseRetainedPort = CurrentWarehousePort;
            // Dialog transactions retain their own quoted identity. A normal
            // refresh closes that dialog without removing its inventory view.
            if (warehouseCargoModal != null)
            {
                warehouseCargoModal.gameObject.SetActive(false);
                warehouseCargoModal.SetParent(null, false);
                Destroy(warehouseCargoModal.gameObject);
            }
        }

        private void EndWarehouseRetainedView(RectTransform inventory)
        {
            if (!warehouseRetainedBuilding) return;
            warehouseRetainedRoot = inventory;
            CanvasGroup group = inventory.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.interactable = true;
                group.blocksRaycasts = true;
            }
            foreach (Dictionary<string, WarehouseRetainedNode> children in warehouseRetainedNodes.Values)
                foreach (WarehouseRetainedNode node in children.Values)
                    if (node.Rect != null && node.Revision != warehouseViewRevision && node.Rect.gameObject.activeSelf)
                        node.Rect.gameObject.SetActive(false);
            warehouseRetainedBuilding = false;
        }

        private RectTransform FindWarehouseNode(string name, Transform parent, Vector2 size, Vector2 position)
        {
            if (!warehouseRetainedBuilding || !warehouseRetainedNodes.TryGetValue(parent, out var children) ||
                !children.TryGetValue(name, out WarehouseRetainedNode node) || node.Rect == null) return null;
            node.Revision = warehouseViewRevision;
            if (node.Rect.sizeDelta != size) node.Rect.sizeDelta = size;
            if (node.Rect.anchoredPosition != position) node.Rect.anchoredPosition = position;
            if (!node.Rect.gameObject.activeSelf) node.Rect.gameObject.SetActive(true);
            return node.Rect;
        }

        private void RememberWarehouseNode(RectTransform rect)
        {
            if (!warehouseRetainedBuilding) return;
            if (!warehouseRetainedNodes.TryGetValue(rect.parent, out var children))
            {
                children = new Dictionary<string, WarehouseRetainedNode>();
                warehouseRetainedNodes.Add(rect.parent, children);
            }
            children[rect.name] = new WarehouseRetainedNode { Rect = rect, Revision = warehouseViewRevision };
        }

        private RectTransform WarehouseRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            RectTransform rect = FindWarehouseNode(name, parent, size, position);
            if (rect != null) return rect;
            rect = CreateRect(name, parent, size, position);
            RememberWarehouseNode(rect);
            return rect;
        }

        private RectTransform WarehousePanel(string name, Transform parent, Vector2 size, Vector2 position, Color color)
        {
            RectTransform rect = FindWarehouseNode(name, parent, size, position);
            if (rect != null) return rect;
            rect = CreateFixedPanel(name, parent, size, position, color);
            RememberWarehouseNode(rect);
            return rect;
        }

        private Image WarehouseImage(string name, Transform parent, Vector2 size, Vector2 position, Color color)
        {
            RectTransform rect = FindWarehouseNode(name, parent, size, position);
            if (rect != null)
            {
                Image retained = rect.GetComponent<Image>();
                retained.color = color;
                return retained;
            }
            Image image = CreateImage(name, parent, size, position, color);
            RememberWarehouseNode(image.rectTransform);
            return image;
        }

        private RawImage WarehouseRawImage(string name, Transform parent, Vector2 size, Vector2 position, Texture texture, Rect uv)
        {
            RectTransform rect = FindWarehouseNode(name, parent, size, position);
            RawImage image;
            if (rect == null)
            {
                image = CreateRawImage(name, parent, size, position, texture, uv);
                RememberWarehouseNode(image.rectTransform);
            }
            else image = rect.GetComponent<RawImage>();
            if (image.texture != texture) image.texture = texture;
            if (image.uvRect != uv) image.uvRect = uv;
            image.color = Color.white;
            return image;
        }

        private RawImage WarehouseItemIcon(string name, Transform parent, ItemIconArt art, Vector2 position, float size)
        {
            Vector2 fittedSize = UiImageAspect.FitSize(art.Texture, art.Uv, new Vector2(size, size));
            RawImage image = WarehouseRawImage(name, parent, fittedSize, position, art.Texture, art.Uv);
            image.raycastTarget = false;
            image.enabled = art.IsAvailable;
            return image;
        }

        private Text WarehouseText(string name, Transform parent, Vector2 size, Vector2 position,
            int fontSize, FontStyle style, TextAnchor alignment, Color color)
        {
            RectTransform rect = FindWarehouseNode(name, parent, size, position);
            if (rect == null)
            {
                Text created = CreateText(name, parent, size, position, fontSize, style, alignment, color);
                RememberWarehouseNode(created.rectTransform);
                return created;
            }
            Text text = rect.GetComponent<Text>();
            GameTypography.Apply(text, fontSize, style);
            text.alignment = alignment;
            text.color = NauticalSurfaceSkin.ReadableTextColor(parent, color);
            return text;
        }

        private Button WarehouseButton(string name, Transform parent, Vector2 size, Vector2 position,
            Color normalColor, Color textColor, int fontSize)
        {
            RectTransform rect = FindWarehouseNode(name, parent, size, position);
            Button button;
            if (rect == null)
            {
                button = CreateButton(name, parent, size, position, normalColor, textColor, fontSize);
                RememberWarehouseNode((RectTransform)button.transform);
            }
            else
            {
                button = rect.GetComponent<Button>();
                // Replace the event object so a saved callback from an older binding
                // still carries its old revision and cannot act on the refreshed UI.
                button.onClick = new Button.ButtonClickedEvent();
            }
            button.interactable = true;
            if (name.StartsWith("Warehouse Storage Header ", System.StringComparison.Ordinal) ||
                name.StartsWith("Warehouse Rare Header ", System.StringComparison.Ordinal))
            {
                Text header = button.GetComponentInChildren<Text>();
                header.color = WarehouseRoomTheme.Ink;
                GameTypography.Apply(header, 18, FontStyle.Normal);
            }
            return button;
        }
    }
}
