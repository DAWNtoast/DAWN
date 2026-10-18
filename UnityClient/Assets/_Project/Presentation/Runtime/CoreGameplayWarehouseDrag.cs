using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private readonly Button[] warehouseDropSlots = new Button[CoreWarehouseCollection.DisplaySlotCount];
        private readonly RectTransform[] warehouseDropHighlights = new RectTransform[CoreWarehouseCollection.DisplaySlotCount];
        private RectTransform warehouseDragCabinet;
        private Text warehouseDragFeedback;

        private void BindWarehouseTreasureDrag(Button source, string treasureId, int sourceSlot = -1)
        {
            WarehouseTreasureDrag drag = source.GetComponent<WarehouseTreasureDrag>();
            if (drag == null) drag = source.gameObject.AddComponent<WarehouseTreasureDrag>();
            CoreVoyageSession quotedSession = session;
            CoreWarehouseCollection quotedCollection = warehouseCollection;
            string quotedPort = CurrentWarehousePort;
            int quotedRevision = warehouseViewRevision;
            RectTransform surface = warehouseDragCabinet.parent as RectTransform;
            bool CurrentGesture() => Application.isPlaying && !editorPreviewBuilding &&
                ReferenceEquals(session, quotedSession) && CurrentWarehousePort == quotedPort &&
                warehouseViewRevision == quotedRevision && session != null &&
                session.Phase == CoreDemoPhase.Port && activePortFacility == 0 && !warehouseCatalogOpen &&
                !profileSaveBlocked && !returnMenuSaving && !IsEscapeLayerActive(portSettingsOverlay) &&
                !IsEscapeLayerActive(gameMasterOverlay) && !IsEscapeLayerActive(returnMenuConfirmation) &&
                surface != null && surface.gameObject.activeInHierarchy &&
                quotedCollection.Owns(treasureId) &&
                (sourceSlot < 0 || quotedCollection.DisplayedAt(sourceSlot) == treasureId);
            drag.Bind(CurrentGesture,
                () => CreateWarehouseDragGhost(treasureId),
                pointer => ResolveWarehouseDrop(pointer, surface, sourceSlot),
                target =>
                {
                    if (!CurrentGesture()) return;
                    bool changed = target >= 0
                        ? quotedCollection.TryDisplay(treasureId, target)
                        : target == -1 && sourceSlot >= 0 && quotedCollection.TryRemoveDisplay(sourceSlot);
                    if (!changed) return;
                    selectedWarehouseTreasureId = treasureId;
                    selectedWarehouseDisplaySlot = target >= 0 ? target : sourceSlot;
                    warehouseStorageOpen = false;
                    if (target >= 0)
                    {
                        // A single drag can select and display the tutorial compass.
                        UpdateGuidedTutorialProgress();
                        OnWarehouseTreasureDisplayed();
                    }
                    SaveProgress();
                    Render();
                }, SetWarehouseDropHighlight);
        }

        private int ResolveWarehouseDrop(PointerEventData pointer, RectTransform surface, int sourceSlot)
        {
            if (pointer == null || !WarehouseContainsPointer(surface, pointer)) return -2;
            for (int slot = 0; slot < warehouseDropSlots.Length; slot++)
            {
                Button target = warehouseDropSlots[slot];
                if (target != null && target.gameObject.activeInHierarchy &&
                    WarehouseContainsPointer((RectTransform)target.transform, pointer)) return slot;
            }
            // Dropping on a shelf edge is a cancelled move. Return an exhibit to
            // storage only once it has been deliberately dragged out of the cabinet.
            return sourceSlot >= 0 && !WarehouseContainsPointer(warehouseDragCabinet, pointer) ? -1 : -2;
        }

        private static bool WarehouseContainsPointer(RectTransform rect, PointerEventData pointer)
        {
            if (rect == null) return false;
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null :
                canvas != null && canvas.worldCamera != null ? canvas.worldCamera : pointer.pressEventCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, pointer.position, camera);
        }

        private RectTransform CreateWarehouseDragGhost(string treasureId)
        {
            CoreWarehouseTreasureDefinition treasure = FindWarehouseTreasure(treasureId);
            if (treasure == null) return null;
            RectTransform ghost = CreateRect("Warehouse Treasure Drag Ghost", contentRoot,
                new Vector2(186f, 186f), Vector2.zero);
            ItemIconArt art = ItemIconCatalog.ForTreasure(treasure);
            RawImage icon = CreateRawImage("Warehouse Drag Art", ghost,
                UiImageAspect.FitSize(art.Texture, art.Uv, new Vector2(166f, 166f)),
                Vector2.zero, art.Texture, art.Uv);
            icon.raycastTarget = false;
            RectTransform caption = CreateFixedPanel("Warehouse Drag Caption", ghost,
                new Vector2(300f, 38f), new Vector2(0f, -112f), new Color(.08f, .055f, .025f, .94f));
            warehouseDragFeedback = CreateText("Warehouse Drag Feedback", caption,
                new Vector2(286f, 34f), Vector2.zero, 18, FontStyle.Normal, TextAnchor.MiddleCenter, Gold);
            warehouseDragFeedback.text = "拖入展位陈列";
            return ghost;
        }

        private RectTransform CreateWarehouseDropHighlight(Button target)
        {
            Transform existing = target.transform.Find("Warehouse Drop Highlight");
            if (existing != null) return (RectTransform)existing;
            RectTransform frame = CreateRect("Warehouse Drop Highlight", target.transform,
                new Vector2(280f, 186f), Vector2.zero);
            CreateImage("Wash", frame, frame.sizeDelta, Vector2.zero, new Color(1f, .76f, .3f, .12f)).raycastTarget = false;
            Color edge = new Color(1f, .78f, .35f, .85f);
            CreateImage("Top", frame, new Vector2(280f, 2f), new Vector2(0f, 92f), edge).raycastTarget = false;
            CreateImage("Bottom", frame, new Vector2(280f, 2f), new Vector2(0f, -92f), edge).raycastTarget = false;
            CreateImage("Left", frame, new Vector2(2f, 186f), new Vector2(-139f, 0f), edge).raycastTarget = false;
            CreateImage("Right", frame, new Vector2(2f, 186f), new Vector2(139f, 0f), edge).raycastTarget = false;
            frame.gameObject.SetActive(false);
            return frame;
        }

        private void SetWarehouseDropHighlight(int target)
        {
            for (int slot = 0; slot < warehouseDropHighlights.Length; slot++)
                if (warehouseDropHighlights[slot] != null)
                    warehouseDropHighlights[slot].gameObject.SetActive(slot == target);
            if (warehouseDragFeedback == null) return;
            warehouseDragFeedback.text = target >= 0
                ? "松手陈列到展位 " + (target + 1) +
                    (string.IsNullOrEmpty(warehouseCollection.DisplayedAt(target)) ? string.Empty : " · 替换原藏品")
                : target == -1 ? "松手收回仓库" : "拖入展位陈列 · Esc 取消";
        }
    }
}
