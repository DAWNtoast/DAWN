using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private static readonly Color ShipyardWorkbenchInk = Html("#222E2A");
        private static readonly Color ShipyardWorkbenchMuted = Html("#405047");
        private static readonly Color ShipyardWorkbenchBrass = Html("#665231");
        private static readonly Color ShipyardWorkbenchAccent = Html("#31594E");
        private static readonly Color ShipyardWorkbenchSurface = Html("#283631");
        private static readonly Color ShipyardWorkbenchVesselSurface = Html("#303D37");
        private static readonly Color ShipyardWorkbenchLight = Html("#F3F0E6");
        private static readonly Color ShipyardWorkbenchLightMuted = Html("#D1DBD3");
        private static readonly Color ShipyardWorkbenchBorder = Html("#7C887A");
        private RectTransform shipyardEquipmentViewportParent;
        private GameObject shipyardEquipmentLetterbox;
        private bool shipyardEquipmentLetterboxWasActive;

        private void RestoreShipyardEquipmentViewport()
        {
            if (shipyardEquipmentViewportParent != null && contentRoot != null)
            {
                contentRoot.SetParent(shipyardEquipmentViewportParent, false);
                contentRoot.anchorMin = contentRoot.anchorMax = Vector2.one * .5f;
                contentRoot.sizeDelta = PhaseSurfaceLayout.ContentSize;
                contentRoot.anchoredPosition = Vector2.zero;
            }
            shipyardEquipmentViewportParent = null;
            if (shipyardEquipmentLetterbox != null)
                shipyardEquipmentLetterbox.SetActive(shipyardEquipmentLetterboxWasActive);
            shipyardEquipmentLetterbox = null;
        }

        private void BuildShipyardEquipmentWindow(int index)
        {
            // Fill the actual canvas, including extra space on tall or ultrawide
            // screens. Leave the fixed viewport mask while this secondary window
            // is open; normal pages regain their original framing on rebuild.
            Canvas canvas = contentRoot.GetComponentInParent<Canvas>().rootCanvas;
            shipyardEquipmentViewportParent = contentRoot.parent as RectTransform;
            contentRoot.SetParent(canvas.transform, false);
            Transform letterbox = canvas.transform.Find("Game Letterbox Overlay");
            if (letterbox != null)
            {
                shipyardEquipmentLetterbox = letterbox.gameObject;
                shipyardEquipmentLetterboxWasActive = letterbox.gameObject.activeSelf;
                letterbox.gameObject.SetActive(false);
            }
            contentRoot.anchorMin = Vector2.zero;
            contentRoot.anchorMax = Vector2.one;
            contentRoot.sizeDelta = Vector2.zero;
            contentRoot.anchoredPosition = Vector2.zero;
            RectTransform overlay = CreateRect("Shipyard Equipment Overlay", contentRoot, Vector2.zero, Vector2.zero);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            // An opaque backing consumes clicks outside controls; the shared
            // market paper supplies the visible secondary-window surface.
            Image backdrop = overlay.gameObject.AddComponent<Image>();
            backdrop.color = Html("#3B423B");
            backdrop.raycastTarget = true;
            RectTransform paper = CreateRect("Shipyard Equipment Paper", overlay, Vector2.zero, Vector2.zero);
            paper.anchorMin = Vector2.zero;
            paper.anchorMax = Vector2.one;
            paper.offsetMin = new Vector2(10f, 10f);
            paper.offsetMax = new Vector2(-10f, -PhaseSurfaceLayout.GetHeaderHeight(tutorialActive) - 10f);
            Image paperFace = paper.gameObject.AddComponent<Image>();
            MarketShopSkin.ApplyPanel(paperFace);
            // Retain the market's paper grain and wood rim, with a quieter,
            // cooler tint around the contrasting equipment work areas.
            paperFace.color = Html("#B8C8CC");
            paperFace.raycastTarget = false;
            RectTransform detail = CreateRect("Shipyard Detail", overlay, PhaseSurfaceLayout.ContentSize, Vector2.zero);
            float headerExtra = PhaseSurfaceLayout.GetHeaderHeight(tutorialActive) - PhaseSurfaceLayout.HeaderHeight;
            WorkbenchRule("Shipyard Equipment Header Rule", detail, new Vector2(1690f, 1f), new Vector2(0f, 334f - headerExtra), ShipyardWorkbenchBorder);
            ShipyardText("Shipyard Equipment Window Title", detail, new Vector2(560f, 50f),
                new Vector2(0f, 372f - headerExtra), 32, ShipyardWorkbenchInk).text = "配件装配";
            Button close = CreateShipyardEquipmentButton("Close Shipyard Equipment", detail,
                new Vector2(260f, 50f), new Vector2(-715f, 372f - headerExtra), 23);
            close.GetComponentInChildren<Text>().text = HasWorkshopSpecialCardReturn ? "← 返回特殊卡牌" :
                shipyardReturnToDeparture ? "← 返回出航码头" : "← 返回船只档案";
            CoreVoyageSession owner = session;
            string port = session.CurrentPort;
            int revision = shipyardViewRevision;
            close.onClick.AddListener(() =>
            {
                if (!IsCurrentShipyardControl(close, owner, port, revision) || !IsCurrentShipyardEquipmentView(index)) return;
                if (HasWorkshopSpecialCardReturn || shipyardReturnToDeparture) { ReturnFromCachedFacility(); return; }
                shipyardDossierTab = shipyardEquipmentReturnTab == ShipyardDossierTab.Background
                    ? ShipyardDossierTab.Background : ShipyardDossierTab.Performance;
                ResetShipyardEquipmentSelection();
                RefreshShipyardDossierView();
            });
            BuildShipyardEquipmentPage(detail, index, ShipyardWorkbenchInk, ShipyardWorkbenchMuted);
            // Retain the regional navigation, date, money and settings header.
            contentRoot.Find("Port Facility Header")?.SetAsLastSibling();
        }

        private Button CreateShipyardEquipmentButton(string name, Transform parent, Vector2 size,
            Vector2 position, int fontSize, bool selected = false, bool primary = false, bool enabled = true)
        {
            Button button = CreateButton(name, parent, size, position, Gold, ShipyardWorkbenchInk, fontSize);
            button.interactable = enabled;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            StyleShipyardWorkbenchButton(button, selected, primary);
            return button;
        }

        private void BuildShipyardEquipmentVessel(RectTransform page, int index)
        {
            CreateShipyardWorkbenchPanel("Shipyard Equipment Vessel Surface", page,
                new Vector2(760f, 922f), new Vector2(-550f, -58f), ShipyardWorkbenchVesselSurface, ShipyardWorkbenchBorder);
            ShipyardText("Shipyard Dossier Name", page, new Vector2(730f, 50f),
                new Vector2(-550f, 374f), 34, ShipyardWorkbenchLight).text = CoreVoyageSession.Ships[index].Name;
            ShipyardText("Shipyard Dossier Role", page, new Vector2(730f, 32f),
                new Vector2(-550f, 328f), 21, ShipyardWorkbenchLightMuted).text =
                ShipyardVesselHomePort(index) + " · " + CoreShipMarketCatalog.Role(index);
            Vector2[] slots = { new Vector2(-350f, -303f), new Vector2(-350f, 236f),
                new Vector2(-750f, -303f), new Vector2(-550f, -454f), new Vector2(-750f, 236f) };
            Vector2[] attachment = { new Vector2(-320f, -100f), new Vector2(-440f, 90f),
                new Vector2(-760f, -100f), new Vector2(-550f, -195f), new Vector2(-660f, 90f) };
            for (int slot = 0; slot < ComponentArtCatalog.SlotCount; slot++)
                CreateLine(page, slots[slot], attachment[slot], Html("#849C89"), 1.5f);
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
            CreateImage(name, parent, size, position, color).raycastTarget = false;
        }

        private void StyleShipyardWorkbenchButton(Button button, bool selected = false, bool primary = false)
        {
            if (primary)
            {
                WoodButtonSkin.Apply(button, Gold);
                WoodButtonSkin.SetRole(button, WoodButtonRole.Primary);
            }
            else
            {
                MarketShopSkin.ApplyLightButton(button, selected);
                ColorBlock colors = button.colors;
                colors.normalColor = selected ? ShipyardWorkbenchAccent : Html("#DBDED4");
                colors.highlightedColor = colors.selectedColor = selected ? Html("#3D6657") : Html("#ECEEE5");
                colors.pressedColor = selected ? Html("#25473D") : Html("#BCC6B8");
                colors.disabledColor = selected ? Html("#3E5A4F") : Html("#BAC1B5");
                button.colors = colors;
            }
            Text label = button.GetComponentInChildren<Text>();
            if (!primary) label.color = selected ? ShipyardWorkbenchLight : ShipyardWorkbenchInk;
            label.resizeTextForBestFit = false;
        }
    }
}
