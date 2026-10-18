using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private bool departureDockTreasureMode;
        private bool DepartureDockTreasureModeActive => UsesDepartureDockScene && !tutorialActive &&
            (departureDockTreasureMode || session.SelectedTreasureMap != null);

        private void OpenDepartureDockTreasureSelection()
        {
            departureDockTreasureMode = true;
            departureUseChartSeed = false;
            session.ClearChartSeedSelection();
            Render();
        }

        private void LeaveDepartureDockTreasureSelection(bool useChart)
        {
            departureDockTreasureMode = false;
            session.ClearSelectedTreasureMap();
            session.ClearChartSeedSelection();
            departureUseChartSeed = useChart;
            SaveProgress();
            Render();
        }

        private void BuildDepartureDockTreasureSelection(RectTransform routes, RectTransform view,
            CoreVoyageSession owner, int revision)
        {
            if (!DepartureDockTreasureModeActive) return;
            RectTransform panel = CreateRect("Departure Dock Treasure Selection", routes,
                new Vector2(765, 172.5f), DepartureDockSceneLayout.ArtPosition(443, 798));
            CoreTreasureMapRecord selected = session.SelectedTreasureMap;
            // Preserve the full folio illustration without stretching or cropping it.
            RawImage preview = CreateRawImage("Departure Dock Treasure Preview", panel, new Vector2(306, 166),
                new Vector2(-226, 0), selected == null ? null : TreasureChartArt(selected.RankId), new Rect(0, 0, 1, 1));
            preview.raycastTarget = false;
            preview.gameObject.SetActive(selected != null);
            if (selected != null) UiImageAspect.Fit(preview, new Vector2(306, 166));
            Text empty = DepartureTreasureSeedText("Departure Dock Treasure Empty", panel, new Vector2(294, 140),
                new Vector2(-226, 0), 19, FontStyle.Normal, TextAnchor.MiddleCenter, DepartureSkin.Ink,
                session.TreasureMaps.Count == 0 ? "暂无藏宝海图\n前往藏宝图桌鉴定碎片并合成" : "选择右侧藏宝图种子\n查看海图缩略图");
            empty.gameObject.SetActive(selected == null);

            const float width = 400;
            RectTransform control = CreateRect("Departure Dock Treasure Dropdown", panel,
                new Vector2(width, 52), new Vector2(176, 56));
            Image face = control.gameObject.AddComponent<Image>();
            face.color = new Color(.94f, .86f, .67f);
            Outline border = control.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(.48f, .31f, .14f, .8f);
            border.effectDistance = new Vector2(1, -1);
            Dropdown dropdown = control.gameObject.AddComponent<Dropdown>();
            dropdown.targetGraphic = face;
            Text caption = DepartureTreasureSeedText("Label", control, new Vector2(width - 48, 46),
                new Vector2(-12, 0), 18, FontStyle.Normal, TextAnchor.MiddleLeft, DepartureSkin.Ink, string.Empty);
            caption.resizeTextForBestFit = true;
            caption.resizeTextMinSize = 13;
            caption.resizeTextMaxSize = 18;
            DepartureTreasureSeedText("Arrow", control, new Vector2(26, 36), new Vector2(180, 0),
                20, FontStyle.Normal, TextAnchor.MiddleCenter, DepartureSkin.Ink, "▾");
            dropdown.captionText = caption;
            BuildDepartureDockTreasureDropdownTemplate(dropdown, width);

            var maps = new List<CoreTreasureMapRecord>(session.TreasureMaps);
            var options = new List<string> { maps.Count == 0 ? "暂无可用的藏宝图种子" : "请选择藏宝图种子" };
            int selectedIndex = 0;
            for (int index = 0; index < maps.Count; index++)
            {
                CoreTreasureMapRecord map = maps[index];
                options.Add(map.Name + "\n<size=12>种子：" + map.Seed + "</size>");
                if (selected?.Id == map.Id) selectedIndex = index + 1;
            }
            dropdown.AddOptions(options);
            dropdown.SetValueWithoutNotify(selectedIndex);
            dropdown.interactable = maps.Count > 0;
            Text detail = DepartureTreasureSeedText("Departure Dock Treasure Detail", panel, new Vector2(width, 47),
                new Vector2(176, 2), 16, FontStyle.Normal, TextAnchor.UpperLeft, DepartureSkin.Ink,
                selected == null ? "任意港口均可出发，无需航海士\n选择种子后，可准备补给并出航。" :
                TreasureMapTheme(selected) + " · " + session.CurrentPort + "\n" + TreasureDepartureObjective(selected));
            detail.resizeTextForBestFit = true;
            detail.resizeTextMinSize = 14;
            detail.resizeTextMaxSize = 16;
            DepartureTreasureSeedText("Departure Dock Treasure Consumption", panel, new Vector2(width, 27),
                new Vector2(176, -39), 14, FontStyle.Normal, TextAnchor.MiddleLeft, DepartureSkin.Ink,
                "出航消耗所选种子 · 撤离或失败不返还");
            Text status = DepartureTreasureSeedText("Departure Dock Treasure Status", panel, new Vector2(240, 29),
                new Vector2(96, -69), 12, FontStyle.Normal, TextAnchor.MiddleLeft, DepartureSkin.Ink, string.Empty);
            status.resizeTextForBestFit = true;
            status.resizeTextMinSize = 10;
            status.resizeTextMaxSize = 12;
            Button table = CreateButton("Departure Treasure Seed Table", panel, new Vector2(152, 26),
                new Vector2(300, -69), PanelRaised, DepartureSkin.Ink, 15);
            table.GetComponentInChildren<Text>().text = "前往藏宝图桌";
            MarketShopSkin.ApplyLightButton(table);
            bool Current() => panel != null && panel.gameObject.activeInHierarchy &&
                DepartureDockTreasureModeActive && DepartureChartViewCurrent(view, owner, revision);
            dropdown.onValueChanged.AddListener(index =>
            {
                if (!Current() || !dropdown.IsInteractable()) return;
                if (index == 0)
                    session.ClearSelectedTreasureMap();
                else if (index < 0 || index > maps.Count || !session.TrySelectTreasureSeed(maps[index - 1].Seed, out _))
                {
                    detail.text = "该种子已不可用，请重新选择。";
                    dropdown.SetValueWithoutNotify(maps.FindIndex(map => map.Id == session.SelectedTreasureMap?.Id) + 1);
                    RefreshDepartureChartControls();
                    return;
                }
                departureDockTreasureMode = true;
                departureUseChartSeed = false;
                session.ClearChartSeedSelection();
                SaveProgress();
                Render();
            });
            table.onClick.AddListener(() =>
            {
                if (!Current() || !table.IsInteractable()) return;
                OpenFacilityFromDeparture(PortFacilityArtCatalog.TreasureFacilityIndex, view);
                Render();
            });
        }

        private static string TreasureDepartureObjective(CoreTreasureMapRecord map)
        {
            switch (map.RankId)
            {
                case "weathered": return "修复绞盘与潜钟 · 获取古代材料与航海器具";
                case "gilded": return "经营信用、控制通缉 · 换取商会珍宝";
                default: return "按时观星、净化诅咒 · 获取遗物与黑珍珠";
            }
        }

        private void BuildDepartureDockTreasureDropdownTemplate(Dropdown dropdown, float width)
        {
            RectTransform template = CreateRect("Template", dropdown.transform, new Vector2(width, 242), Vector2.zero);
            template.anchorMin = template.anchorMax = new Vector2(.5f, 0);
            template.pivot = new Vector2(.5f, 1);
            template.anchoredPosition = new Vector2(0, -3);
            Image paper = template.gameObject.AddComponent<Image>();
            paper.color = new Color(.97f, .91f, .78f);
            ScrollRect scroll = template.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 38;
            RectTransform viewport = CreateRect("Viewport", template, Vector2.zero, Vector2.zero);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(4, 4);
            viewport.offsetMax = new Vector2(-16, -4);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = CreateRect("Content", viewport, new Vector2(0, 46), Vector2.zero);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            RectTransform item = CreateRect("Item", content, new Vector2(0, 46), Vector2.zero);
            item.anchorMin = new Vector2(0, .5f);
            item.anchorMax = new Vector2(1, .5f);
            Toggle toggle = item.gameObject.AddComponent<Toggle>();
            Image background = item.gameObject.AddComponent<Image>();
            background.color = Color.white;
            toggle.targetGraphic = background;
            ColorBlock colors = toggle.colors;
            colors.normalColor = new Color(.97f, .91f, .78f);
            colors.highlightedColor = colors.selectedColor = new Color(.77f, .83f, .68f);
            toggle.colors = colors;
            Text label = DepartureTreasureSeedText("Item Label", item, new Vector2(width - 38, 44),
                Vector2.zero, 17, FontStyle.Normal, TextAnchor.MiddleLeft, DepartureSkin.Ink, string.Empty);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = 17;
            scroll.viewport = viewport;
            scroll.content = content;
            RectTransform bar = CreateRect("Scrollbar", template, new Vector2(8, 234), new Vector2(-6, 0));
            bar.anchorMin = bar.anchorMax = new Vector2(1, .5f);
            Image track = bar.gameObject.AddComponent<Image>();
            track.color = new Color(.49f, .34f, .19f, .18f);
            Scrollbar scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            RectTransform handle = CreateRect("Handle", bar, Vector2.zero, Vector2.zero);
            Image thumb = handle.gameObject.AddComponent<Image>();
            thumb.color = new Color(.49f, .34f, .19f, .8f);
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = thumb;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            dropdown.template = template;
            dropdown.itemText = label;
            template.gameObject.SetActive(false);
        }

        private void RefreshDepartureDockTreasureReadiness()
        {
            if (!DepartureDockTreasureModeActive) return;
            Text status = FindActiveGuidedContent("Route Selection")?
                .Find("Departure Dock Treasure Selection/Departure Dock Treasure Status")?.GetComponent<Text>();
            if (status == null) return;
            bool ready = session.CanStartTreasureVoyage(out string reason);
            status.text = session.SelectedTreasureMap == null ? "浏览与切换不会消耗种子" : ready ? "整备完成 · 无需航海士" : reason;
        }
    }
}
