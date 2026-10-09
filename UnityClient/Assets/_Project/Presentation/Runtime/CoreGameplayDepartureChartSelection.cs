using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private bool departureUseChartSeed;
        private string departureChartDraft = string.Empty;
        private bool departureChartValid;
        private string departureChartFailure = string.Empty;
        private CoreVoyageSession departureChartOwner;
        private string departureChartPort;
        private RectTransform departureChartView;
        private RectTransform departureChartSelection;
        private int departureChartRevision;
        private InputField departureChartInput;
        private Dropdown departureChartDropdown;
        private Toggle departureChartToggle;
        private Text departureChartValidation;
        private RectTransform departureChartWarning;
        private object departureChartWarningIdentity;
        private bool DepartureChartModeActive => departureUseChartSeed && session != null &&
            session.CanDrawSeaChart && !tutorialActive && session.SelectedTreasureMap == null;

        private void BeginDepartureChartSelectionView()
        {
            if (!ReferenceEquals(departureChartOwner, session))
            {
                departureUseChartSeed = false;
                departureChartDraft = string.Empty;
            }
            departureChartOwner = session;
            departureChartPort = session.CurrentPort;
            departureChartRevision++;
            departureChartView = null;
            departureChartSelection = null;
            departureChartInput = null;
            departureChartDropdown = null;
            departureChartToggle = null;
            departureChartValidation = null;
            departureChartWarning = null;
            departureChartWarningIdentity = null;
            if (!session.CanDrawSeaChart || tutorialActive || session.SelectedTreasureMap != null)
                departureUseChartSeed = false;
            if (!DepartureChartModeActive) session.ClearChartSeedSelection();
        }

        private bool DepartureChartViewCurrent(RectTransform view, CoreVoyageSession owner, int revision)
        {
            return session != null && ReferenceEquals(session, owner) &&
                ReferenceEquals(departureChartView, view) && revision == departureChartRevision &&
                view != null && view.gameObject.activeInHierarchy && session.Phase == CoreDemoPhase.Port &&
                session.CurrentPort == departureChartPort &&
                (activePortFacility == 8 || !Application.isPlaying && editorPreviewScreen == CoreGameplayPreviewScreen.VoyageDeparture);
        }

        private void BuildDepartureChartControls(RectTransform routePanel, RectTransform panel)
        {
            departureChartView = panel;
            CoreVoyageSession owner = session;
            int revision = departureChartRevision;
            if (session.SelectedTreasureMap != null)
            {
                Text label = CreateText("Seed Label", routePanel, new Vector2(500f, 34f), new Vector2(-280f, 373f),
                    18, FontStyle.Bold, TextAnchor.MiddleLeft, Ink);
                label.text = "藏宝图种子 · 拼合后固定";
                UiLabelBacking.Apply(label, true);
                Text treasureSeed = CreateText("Treasure Seed", routePanel, new Vector2(520f, 34f), new Vector2(290f, 373f),
                    16, FontStyle.Bold, TextAnchor.MiddleRight, Ink);
                treasureSeed.text = session.SelectedTreasureMap.Seed;
                UiLabelBacking.Apply(treasureSeed, true);
                return;
            }
            RectTransform toggleRoot = CreateRect("Use Chart Seed", routePanel, new Vector2(500f, 34f), new Vector2(-280f, 373f));
            Image toggleHit = toggleRoot.gameObject.AddComponent<Image>();
            toggleHit.color = Color.clear;
            Image toggleBackground = CreateImage("Background", toggleRoot, new Vector2(32f, 32f), new Vector2(-234f, 0f), DeepSea);
            toggleBackground.raycastTarget = false;
            departureChartToggle = toggleRoot.gameObject.AddComponent<Toggle>();
            departureChartToggle.targetGraphic = toggleBackground;
            Text check = CreateText("Checkmark", toggleBackground.transform, new Vector2(32f, 32f), Vector2.zero,
                27, FontStyle.Bold, TextAnchor.MiddleCenter, Gold);
            check.text = "✓";
            check.raycastTarget = false;
            Text caption = CreateText("Seed Label", toggleRoot, new Vector2(450f, 34f), new Vector2(25f, 0f),
                20, FontStyle.Bold, TextAnchor.MiddleLeft, Ink);
            caption.text = session.CanDrawSeaChart ? "使用海图种子 · 选择已绘海图" : "海图种子 · 待绘制";
            caption.raycastTarget = false;
            UiLabelBacking.Apply(caption, true);
            departureChartToggle.graphic = check;
            departureChartToggle.SetIsOnWithoutNotify(DepartureChartModeActive);
            departureChartToggle.interactable = session.CanDrawSeaChart && !tutorialActive;
            departureChartToggle.onValueChanged.AddListener(value =>
            {
                if (!DepartureChartViewCurrent(panel, owner, revision) || !session.CanDrawSeaChart || tutorialActive) return;
                departureUseChartSeed = value;
                if (!value)
                {
                    session.ClearChartSeedSelection();
                    CloseDepartureChartWarning();
                }
                else ValidateDepartureChartDraft(false);
                RefreshDepartureChartControls();
            });
            Text hint = CreateText("Seed Locked Hint", routePanel, new Vector2(520f, 34f), new Vector2(290f, 373f),
                14, FontStyle.Normal, TextAnchor.MiddleRight, MutedInk);
            hint.text = tutorialActive ? "教学使用指定航程；结束后可选海图" : session.CanDrawSeaChart
                ? "未勾选时可自选航线与目的港" : "航海士随船可复用海图";
            hint.raycastTarget = false;
            UiLabelBacking.Apply(hint, true);
            departureChartSelection = CreateRect("Departure Chart Selection", routePanel, new Vector2(1060f, 650f), Vector2.zero);
            if (session.CanDrawSeaChart)
            {
                departureChartDropdown = CreateSeedDropdown("Seed Input", departureChartSelection, new Vector2(1000f, 48f),
                    new Vector2(0f, 236f), departureChartDraft, out departureChartInput);
            }
            BuildDepartureChartPreview(departureChartSelection);
            departureChartValidation = CreateText("Chart Seed Validation", departureChartSelection, new Vector2(1000f, 24f),
                new Vector2(0f, -330f), 16, FontStyle.Normal, TextAnchor.MiddleCenter, MutedInk);
            departureChartValidation.resizeTextForBestFit = true;
            departureChartValidation.resizeTextMinSize = 12;
            departureChartValidation.resizeTextMaxSize = 16;
            if (DepartureChartModeActive) ValidateDepartureChartDraft(false);
            RefreshDepartureChartControls();
        }

        private Dropdown CreateDepartureChartSeedInput(string name, Transform parent, Vector2 size, Vector2 position,
            string selectedSeed, out InputField input)
        {
            RectTransform root = CreateRect(name, parent, size, position);
            Image background = root.gameObject.AddComponent<Image>();
            background.color = DeepSea;
            NauticalSurfaceSkin.ApplyPanel(background, background.color);
            background.sprite = null;
            background.overrideSprite = null;
            background.type = Image.Type.Simple;
            background.color = DeepSea;
            Text text = CreateText("Text", root, new Vector2(size.x - 168f, size.y - 8f), new Vector2(-68f, 0f),
                15, FontStyle.Normal, TextAnchor.MiddleLeft, Ink);
            Text placeholder = CreateText("Placeholder", root, new Vector2(size.x - 168f, size.y - 8f), new Vector2(-68f, 0f),
                14, FontStyle.Italic, TextAnchor.MiddleLeft, MutedInk);
            placeholder.text = "选择已绘海图，或粘贴完整种子";
            text.raycastTarget = placeholder.raycastTarget = false;
            input = root.gameObject.AddComponent<InputField>();
            input.targetGraphic = background;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = 1024;
            input.SetTextWithoutNotify(selectedSeed ?? string.Empty);
            input.enabled = true;
            RectTransform dropdownRoot = CreateRect("Seed Input Dropdown", root, new Vector2(148f, size.y),
                new Vector2((size.x - 148f) * .5f, 0f));
            Image surface = dropdownRoot.gameObject.AddComponent<Image>();
            surface.color = PanelRaised;
            Dropdown dropdown = dropdownRoot.gameObject.AddComponent<Dropdown>();
            dropdown.targetGraphic = surface;
            Text caption = CreateText("Label", dropdownRoot, new Vector2(140f, size.y - 6f), Vector2.zero,
                14, FontStyle.Bold, TextAnchor.MiddleCenter, Gold);
            caption.text = "选择海图 ▼";
            caption.raycastTarget = false;
            // Keep this short caption independent of the selected option label.
            // The complete selected seed belongs to the editable text field.
            RectTransform template = CreateRect("Template", dropdownRoot, new Vector2(size.x, 230f), Vector2.zero);
            template.anchorMin = template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(1f, 1f);
            template.anchoredPosition = new Vector2(0f, -4f);
            Image templateBackground = template.gameObject.AddComponent<Image>();
            templateBackground.color = Panel;
            ScrollRect scroll = template.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            RectTransform viewport = CreateRect("Viewport", template, new Vector2(size.x - 8f, 222f), Vector2.zero);
            viewport.anchorMin = viewport.anchorMax = new Vector2(.5f, .5f);
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.clear;
            Mask mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            scroll.viewport = viewport;
            RectTransform content = CreateRect("Content", viewport, new Vector2(size.x - 8f, 220f), Vector2.zero);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(.5f, 1f);
            content.sizeDelta = new Vector2(0f, 220f);
            scroll.content = content;
            RectTransform item = CreateRect("Item", content, new Vector2(size.x - 8f, 52f), Vector2.zero);
            item.anchorMin = new Vector2(0f, 1f);
            item.anchorMax = new Vector2(1f, 1f);
            item.pivot = new Vector2(.5f, 1f);
            item.sizeDelta = new Vector2(0f, 52f);
            Toggle option = item.gameObject.AddComponent<Toggle>();
            Image optionFace = item.gameObject.AddComponent<Image>();
            optionFace.color = PanelRaised;
            option.targetGraphic = optionFace;
            Text optionText = CreateText("Item Label", item, new Vector2(size.x - 28f, 48f), Vector2.zero,
                14, FontStyle.Normal, TextAnchor.MiddleLeft, Ink);
            optionText.raycastTarget = false;
            dropdown.template = template;
            dropdown.itemText = optionText;
            template.gameObject.SetActive(false);
            var records = new List<DepartureChartRecord>();
            if (session != null)
                foreach (DepartureChartRecord chart in RecordedDepartureCharts)
                    if (chart != null && !string.IsNullOrWhiteSpace(chart.seed)) records.Add(chart);
            var options = new List<string> { records.Count == 0 ? "尚未记录海图 · 结算时由航海士绘制" : "选择已绘海图" };
            foreach (DepartureChartRecord chart in records)
            {
                string shortSeed = chart.seed.Length > 30 ? chart.seed.Substring(0, 30) + "…" : chart.seed;
                options.Add(chart.routeName + " · " + chart.departurePort + " ↔ " + chart.destinationPort +
                    (chart.tutorialWaters ? " · 教学海域" : string.Empty) + "\n" + shortSeed);
            }
            dropdown.AddOptions(options);
            dropdown.SetValueWithoutNotify(0);
            InputField editable = input;
            RectTransform view = departureChartView;
            CoreVoyageSession owner = session;
            int revision = departureChartRevision;
            editable.onValueChanged.AddListener(value =>
            {
                if (!DepartureChartViewCurrent(view, owner, revision) || !DepartureChartModeActive) return;
                departureChartDraft = value;
                dropdown.SetValueWithoutNotify(0);
                ValidateDepartureChartDraft(false);
            });
            editable.onEndEdit.AddListener(value =>
            {
                if (!DepartureChartViewCurrent(view, owner, revision) || !DepartureChartModeActive) return;
                departureChartDraft = value;
                ValidateDepartureChartDraft(true);
            });
            dropdown.onValueChanged.AddListener(index =>
            {
                if (!DepartureChartViewCurrent(view, owner, revision) || !DepartureChartModeActive ||
                    index <= 0 || index > records.Count) return;
                departureChartDraft = records[index - 1].seed;
                editable.SetTextWithoutNotify(departureChartDraft);
                ValidateDepartureChartDraft(true);
                dropdown.SetValueWithoutNotify(0);
            });
            input.interactable = DepartureChartModeActive;
            dropdown.interactable = DepartureChartModeActive && records.Count > 0;
            return dropdown;
        }

        private bool ValidateDepartureChartDraft(bool showWarning)
        {
            if (!DepartureChartModeActive) return true;
            int previousRoute = session.SelectedRouteIndex;
            departureChartValid = session.TrySelectChartSeed(departureChartDraft, out departureChartFailure);
            if (departureChartValid)
            {
                departureChartFailure = string.Empty;
                CloseDepartureChartWarning();
            }
            else session.ClearChartSeedSelection();
            RefreshDepartureChartControls();
            if (previousRoute != session.SelectedRouteIndex) RefreshDepartureChartProvisionQuote();
            if (!departureChartValid && showWarning) ShowDepartureChartWarning(departureChartFailure);
            return departureChartValid;
        }

        private void RefreshDepartureChartControls()
        {
            if (departureChartView == null || session == null) return;
            bool mode = DepartureChartModeActive;
            if (departureChartSelection != null) departureChartSelection.gameObject.SetActive(mode);
            if (departureChartInput != null) departureChartInput.interactable = mode;
            if (departureChartDropdown != null)
                departureChartDropdown.interactable = mode && departureChartDropdown.options.Count > 1;
            if (departureChartToggle != null)
            {
                departureChartToggle.SetIsOnWithoutNotify(mode);
                departureChartToggle.interactable = session.CanDrawSeaChart && !tutorialActive && session.SelectedTreasureMap == null;
            }
            if (departureChartValidation != null)
            {
                bool empty = string.IsNullOrWhiteSpace(departureChartDraft);
                departureChartValidation.color = mode && !departureChartValid && !empty ? Coral : MutedInk;
                departureChartValidation.text = tutorialActive ? "教学使用指定航程；结束后可选海图。" : mode
                    ? departureChartValid ? "海图已确认，备好补给后出航。"
                        : empty ? "选择已绘海图，或输入完整种子。" : "无法出航：" + departureChartFailure
                    : departureChartDropdown != null && departureChartDropdown.options.Count <= 1
                        ? "暂无已绘海图；勾选后可输入完整种子。"
                        : "出航时生成随机海图；勾选可复用已绘海图。";
            }
            Transform routePanel = contentRoot == null ? null : FindActiveGuidedContent("Route Selection");
            if (routePanel != null)
            {
                for (int index = 0; index < CoreVoyageSession.Routes.Count; index++)
                {
                    Button route = routePanel.Find("Route " + index)?.GetComponent<Button>();
                    if (route != null)
                    {
                        route.interactable = !mode && (!tutorialActive || index == 0);
                        route.gameObject.SetActive(!mode);
                        bool selected = session.SelectedRouteIndex == index;
                        WoodButtonSkin.SetSelected(route, selected);
                        Text routeText = route.GetComponentInChildren<Text>();
                        if (routeText != null && routeText.text.Length > 0)
                            routeText.text = (selected ? "◆" : "◇") + routeText.text.Substring(1);
                    }
                }
            }
            RefreshDepartureDestinationSelection();
            RefreshDepartureChartPreview();
            Text destinationSummary = DepartureLayout.Find(departureChartView, "Destination Summary")?.GetComponent<Text>();
            if (destinationSummary != null && session.SelectedTreasureMap == null)
                destinationSummary.text = "到达港口：" + session.SelectedDestinationPort + "　（当前港：" + session.CurrentPort + "）";
            Text cargo = DepartureLayout.Find(departureChartView, "Cargo")?.GetComponent<Text>();
            if (cargo != null && session.SelectedTreasureMap == null)
            {
                int routeLabel = cargo.text.LastIndexOf("航线：", StringComparison.Ordinal);
                if (routeLabel >= 0) cargo.text = cargo.text.Substring(0, routeLabel) + "航线：" + CoreVoyageSession.Routes[session.SelectedRouteIndex].Name;
            }
            Button start = departureChartView.Find("Start Voyage")?.GetComponent<Button>();
            if (start != null) start.interactable = !mode || departureChartValid;
            DepartureLayout.ApplySetup(departureChartView);
        }

        private void RefreshDepartureChartProvisionQuote()
        {
            if (departureChartView == null || DepartureLayout.Find(departureChartView, "Departure Provision Quote") == null) return;
            foreach (string name in new[] { "Departure Provision Quote", "Departure Buy Provisions" })
            {
                Transform previous = DepartureLayout.Find(departureChartView, name);
                if (previous == null) continue;
                previous.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(previous.gameObject);
                else DestroyImmediate(previous.gameObject);
            }
            BuildDepartureProvisionPurchase(departureChartView);
        }

        private bool PrepareDepartureChartForVoyage(RectTransform panel)
        {
            if (tutorialActive || session.SelectedTreasureMap != null) return true;
            if (departureUseChartSeed && !session.CanDrawSeaChart)
            {
                departureUseChartSeed = false;
                session.ClearChartSeedSelection();
                RefreshDepartureChartControls();
                ShowDepartureChartWarning("未配置航海士。请编入航海士后重选海图，或再次确认随机出航。");
                return false;
            }
            if (DepartureChartModeActive)
            {
                CommitDepartureSeed(panel);
                if (!ValidateDepartureChartDraft(true)) return false;
            }
            else
            {
                session.ClearChartSeedSelection();
                string fresh = CoreVoyageSession.CreateChartSeed(session.SelectedRouteIndex, session.CurrentPort, session.SelectedDestinationPort);
                if (!session.TrySelectChartSeed(fresh, out string randomFailure))
                {
                    ShowDepartureChartWarning(randomFailure);
                    return false;
                }
            }
            if (!session.CanStartSelectedChart(out string failure))
            {
                ShowDepartureChartWarning(failure);
                return false;
            }
            return true;
        }

        private void ShowDepartureChartWarning(string reason)
        {
            CloseDepartureChartWarning();
            departureChartWarningIdentity = new object();
            object identity = departureChartWarningIdentity;
            departureChartWarning = CreateFixedPanel("Chart Seed Warning", contentRoot,
                PhaseSurfaceLayout.ContentSize, Vector2.zero, new Color(0f, 0f, 0f, .76f));
            Image shade = departureChartWarning.GetComponent<Image>();
            shade.sprite = null;
            shade.overrideSprite = null;
            shade.type = Image.Type.Simple;
            shade.color = new Color(0f, 0f, 0f, .76f);
            shade.raycastTarget = true;
            departureChartWarning.SetAsLastSibling();
            float readingScale = GameReadability.Scale;
            RectTransform card = CreateFixedPanel("Chart Seed Warning Card", departureChartWarning,
                new Vector2(1000f, 360f * readingScale), Vector2.zero, PanelRaised);
            Text title = CreateText("Chart Seed Warning Title", card, new Vector2(920f, 56f * readingScale), new Vector2(0f, 118f * readingScale),
                32, FontStyle.Normal, TextAnchor.MiddleCenter, Html("#352415"));
            GameReadability.Body(title, 32);
            title.font = GameTypography.InterfaceFont;
            title.text = "当前海图种子不可用";
            Text description = CreateText("Chart Seed Warning Reason", card, new Vector2(910f, 146f * readingScale), new Vector2(0f, 12f * readingScale),
                22, FontStyle.Normal, TextAnchor.MiddleCenter, Html("#49351F"));
            description.text = reason + "\n重新输入或选图，或取消勾选以随机出航。";
            GameReadability.Body(description, 22);
            Button close = CreateButton("Close Chart Seed Warning", card, new Vector2(400f, 56f * readingScale),
                new Vector2(0f, -122f * readingScale), Gold, DeepSea, 22);
            close.GetComponentInChildren<Text>().text = "返回修改";
            GameReadability.Body(close.GetComponentInChildren<Text>(), 22);
            close.onClick.AddListener(() =>
            {
                if (!ReferenceEquals(identity, departureChartWarningIdentity)) return;
                CloseDepartureChartWarning();
            });
        }

        private void CloseDepartureChartWarning()
        {
            departureChartWarningIdentity = null;
            if (departureChartWarning == null) return;
            departureChartWarning.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(departureChartWarning.gameObject);
            else DestroyImmediate(departureChartWarning.gameObject);
            departureChartWarning = null;
        }

        // Called on an editor staging controller only; sample records remain
        // in memory and never read or write the player's chart archive.
        public void ConfigureEditorPreviewDepartureChart(int state)
        {
            if (Application.isPlaying || session == null || session.Phase != CoreDemoPhase.Port) return;
            string current = session.CurrentPort;
            string other = string.Empty;
            var foreign = new List<string>();
            foreach (string port in CoreVoyageSession.DestinationPorts)
                if (port != current)
                {
                    if (other.Length == 0) other = port;
                    foreign.Add(port);
                }
            int route = session.SelectedRouteIndex;
            string sample = CoreVoyageSession.CreateChartSeed(route, current, other);
            recordedDepartureCharts.Clear();
            recordedDepartureCharts.Add(new DepartureChartRecord
            {
                seed = sample, routeIndex = route, routeName = CoreVoyageSession.Routes[route].Name,
                departurePort = current, destinationPort = other, tutorialWaters = false,
            });
            departureChartOwner = session;
            departureUseChartSeed = state != 0;
            departureChartDraft = state == 2
                ? CoreVoyageSession.CreateChartSeed(route, foreign[0], foreign[1]) : sample;
            activePortFacility = 8;
            departureCrewConfigurationOpen = false;
            Render();
            if (state == 2) ValidateDepartureChartDraft(true);
        }
    }
}
