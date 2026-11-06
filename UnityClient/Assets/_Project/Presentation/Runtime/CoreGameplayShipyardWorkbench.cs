using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private static readonly Color ShipyardWorkbenchInk = Html("#302115");
        private static readonly Color ShipyardWorkbenchMuted = Html("#715337");
        private static readonly Color ShipyardWorkbenchBrass = Html("#665231");
        private static readonly Color ShipyardWorkbenchSurface = Color.white;
        private static readonly Color ShipyardWorkbenchBorder = Html("#AA8350");
        private void BuildShipyardEquipmentWindow(int index)
        {
            ExpandFacilityToWindow();
            RectTransform overlay = CreateRect("Shipyard Equipment Overlay", contentRoot, Vector2.zero, Vector2.zero);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            // An opaque backing consumes clicks outside controls; the shared
            // market paper supplies the visible secondary-window surface.
            Image backdrop = overlay.gameObject.AddComponent<Image>();
            backdrop.color = Html("#382619");
            backdrop.raycastTarget = true;
            RectTransform paper = CreateRect("Shipyard Equipment Paper", overlay, Vector2.zero, Vector2.zero);
            paper.anchorMin = Vector2.zero;
            paper.anchorMax = Vector2.one;
            paper.offsetMin = Vector2.zero;
            paper.offsetMax = new Vector2(0f, -PhaseSurfaceLayout.GetHeaderHeight(tutorialActive));
            Image paperFace = paper.gameObject.AddComponent<Image>();
            MarketShopSkin.ApplyPanel(paperFace);
            // Match the workshop's un-tinted warm parchment and wood rim.
            paperFace.color = Color.white;
            paperFace.raycastTarget = false;
            RectTransform detail = CreateRect("Shipyard Detail", paper, Vector2.zero, Vector2.zero);
            detail.anchorMin = Vector2.zero;
            detail.anchorMax = Vector2.one;
            detail.offsetMin = detail.offsetMax = Vector2.zero;
            BuildShipyardEquipmentPage(detail, index, ShipyardWorkbenchInk, ShipyardWorkbenchMuted);
            detail.gameObject.AddComponent<ShipyardEquipmentLayout>().Apply();
            // Retain the regional navigation, date, money and settings header.
            contentRoot.Find("Port Facility Header")?.SetAsLastSibling();
        }

        private Button CreateShipyardEquipmentButton(string name, Transform parent, Vector2 size,
            Vector2 position, int fontSize, bool selected = false, bool primary = false, bool enabled = true)
        {
            Button button = CreateButton(name, parent, size, position, Gold, ShipyardWorkbenchInk, fontSize);
            button.interactable = enabled;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            WoodButtonSkin.ApplyAction(button, primary ? WoodButtonRole.Primary : WoodButtonRole.Secondary, selected);
            return button;
        }

        private void BuildShipyardEquipmentVessel(RectTransform page, int index)
        {
            // Keep layout bounds without a second paper surface or an inner frame.
            CreateRect("Shipyard Equipment Vessel Surface", page,
                new Vector2(760f, 922f), new Vector2(-550f, -58f));
            RectTransform window = CreateRect("Shipyard Dossier Ship Window", page,
                new Vector2(700f, 386f), new Vector2(-550f, -40f));
            window.gameObject.AddComponent<RectMask2D>();
            RawImage ship = (RawImage)BuildShipyardVessel(window, index, Vector2.zero, 1f, "Shipyard Dossier");
            RectTransform vessel = (RectTransform)ship.transform.parent;
            Vector2 available = window.sizeDelta - new Vector2(24f, 24f);
            Vector2 source = ship.rectTransform.sizeDelta;
            float fit = Mathf.Min(available.x / source.x, available.y / source.y);
            vessel.anchoredPosition = Vector2.zero;
            vessel.localScale = new Vector3(fit, fit, 1f);
            // Draw over the ship so each line visibly reaches its attachment,
            // while the slot cards built next stay above the guide lines.
            for (int slot = 0; slot < ComponentArtCatalog.SlotCount; slot++)
                CreateLine(page, Vector2.zero, Vector2.right, ShipyardWorkbenchBorder, 2f).name = "Shipyard Equipment Slot Link " + slot;
        }

        private void BuildShipyardWorkbenchFrame(RectTransform art, int index)
        {
            RectTransform surface = CreateShipyardWorkbenchPanel("Shipyard Workbench", art,
                new Vector2(1696f, 664f), new Vector2(0f, -4f), Html("#0D1B20"), Html("#7F7053"));
            WorkbenchRule("Shipyard Workbench Top Rail", surface, new Vector2(1676f, 3f),
                new Vector2(0f, 321f), ShipyardWorkbenchBrass);
            WorkbenchRule("Shipyard Workbench Divider", surface, new Vector2(1f, 592f),
                new Vector2(-305f, 0f), Html("#43514C"));
            foreach (float x in new[] { -836f, 836f })
                foreach (float y in new[] { -320f, 320f })
                    WorkbenchRule("Shipyard Workbench Fastener", surface, new Vector2(5f, 5f),
                        new Vector2(x, y), ShipyardWorkbenchBrass);

            Vector2 window = TavernDossierLayerWindowPosition;
            CreateShipyardWorkbenchPanel("Shipyard Workbench Ship Rim", art,
                TavernDossierLayerWindowSize + new Vector2(12f, 12f), window,
                Html("#172D33"), Html("#7F7053"));
            Text station = ShipyardText("Shipyard Workbench Station", art, new Vector2(450f, 30f),
                new Vector2(window.x, 286f), 20, ShipyardWorkbenchBrass);
            station.text = "皇家船坞  /  装配工位";
            ShipyardText("Shipyard Workbench Vessel Caption", art, new Vector2(450f, 28f),
                new Vector2(window.x, -282f), 20, ShipyardWorkbenchInk).text = "船只预览";
            ShipyardText("Shipyard Workbench Vessel Hint", art, new Vector2(450f, 24f),
                new Vector2(window.x, -310f), 17, ShipyardWorkbenchMuted).text =
                OwnsShipyardVessel(index) ? "查看配件，调整本船装配" : "原装配置预览 · 购船后可改装";
        }

        private void BuildShipyardWorkbenchShipGrid(RectTransform window)
        {
            Color grid = new Color(.43f, .65f, .64f, .07f);
            for (float x = -180f; x <= 180f; x += 45f)
                WorkbenchRule("Shipyard Workbench Grid", window, new Vector2(1f, window.sizeDelta.y), new Vector2(x, 0f), grid);
            for (float y = -225f; y <= 225f; y += 45f)
                WorkbenchRule("Shipyard Workbench Grid", window, new Vector2(window.sizeDelta.x, 1f), new Vector2(0f, y), grid);
        }

        private RectTransform CreateShipyardWorkbenchPanel(string name, Transform parent, Vector2 size,
            Vector2 position, Color fill, Color border)
        {
            Image face = CreateImage(name, parent, size, position, fill);
            if (name.StartsWith("Shipyard Equipment "))
            {
                NauticalSurfaceSkin.ApplyReadingPanel(face);
                face.pixelsPerUnitMultiplier = 4f;
                face.color = fill;
            }
            face.raycastTarget = false;
            RectTransform rect = face.rectTransform;
            WorkbenchRule(name + " Top", rect, new Vector2(size.x, 1f), new Vector2(0f, (size.y - 1f) * .5f), border);
            WorkbenchRule(name + " Bottom", rect, new Vector2(size.x, 1f), new Vector2(0f, -(size.y - 1f) * .5f), border);
            WorkbenchRule(name + " Left", rect, new Vector2(1f, size.y), new Vector2(-(size.x - 1f) * .5f, 0f), border);
            WorkbenchRule(name + " Right", rect, new Vector2(1f, size.y), new Vector2((size.x - 1f) * .5f, 0f), border);
            return rect;
        }

        private void WorkbenchRule(string name, Transform parent, Vector2 size, Vector2 position, Color color)
        {
            Image face = CreateImage(name, parent, size, position, color);
            if (name.EndsWith(" Heading Surface") || name.EndsWith(" Reading Surface"))
            {
                face.sprite = NauticalSurfaceSkin.Paper;
                face.type = Image.Type.Tiled;
                if (face.sprite != null) face.pixelsPerUnitMultiplier = face.sprite.rect.width / 340f;
            }
            face.raycastTarget = false;
        }

    }
}
