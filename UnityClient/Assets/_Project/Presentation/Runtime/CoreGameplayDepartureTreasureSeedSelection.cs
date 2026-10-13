using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private RectTransform departureTreasureSeedPicker;
        private object departureTreasureSeedPickerIdentity;

        private void BuildDepartureTreasureSeedControl(RectTransform routePanel, RectTransform view,
            CoreVoyageSession owner, int revision)
        {
            Button picker = CreateButton("Departure Treasure Seed Picker", routePanel,
                new Vector2(510f, 36f), new Vector2(290f, 438f), PanelRaised, Gold, 18);
            picker.GetComponentInChildren<Text>().text = tutorialActive ? "藏宝海图种子 · 教学结束后可选" :
                session.SelectedTreasureMap != null ? "更换藏宝海图 · " + session.SelectedTreasureMap.Name + " ▼" :
                "藏宝海图种子 · " + session.TreasureMaps.Count + " 张可用 ▼";
            picker.interactable = !tutorialActive;
            picker.onClick.AddListener(() =>
            {
                if (!DepartureChartViewCurrent(view, owner, revision) || !picker.IsInteractable() || tutorialActive) return;
                OpenDepartureTreasureSeedPicker(view, owner, revision);
            });
        }

        private void OpenDepartureTreasureSeedPicker(RectTransform view, CoreVoyageSession owner, int revision)
        {
            CloseDepartureTreasureSeedPicker();
            CloseDepartureChartWarning();
            object identity = departureTreasureSeedPickerIdentity = new object();
            departureTreasureSeedPicker = CreateFixedPanel("Departure Treasure Seed Selection", contentRoot,
                PhaseSurfaceLayout.ContentSize, Vector2.zero, new Color(0f, 0f, 0f, .80f));
            Image shade = departureTreasureSeedPicker.GetComponent<Image>();
            shade.sprite = null;
            shade.overrideSprite = null;
            shade.type = Image.Type.Simple;
            shade.color = new Color(0f, 0f, 0f, .80f);
            shade.raycastTarget = true;
            departureTreasureSeedPicker.SetAsLastSibling();
            RectTransform card = CreateFixedPanel("Departure Treasure Seed Card", departureTreasureSeedPicker,
                new Vector2(1160f, 790f), Vector2.zero, PanelRaised);
            CreateText("Departure Treasure Seed Title", card, new Vector2(1060f, 46f), new Vector2(0f, 321f),
                29, FontStyle.Bold, TextAnchor.MiddleCenter, Gold).text = "选择藏宝海图种子";
            Text notice = CreateText("Departure Treasure Seed Notice", card, new Vector2(1060f, 50f), new Vector2(0f, 266f),
                19, FontStyle.Normal, TextAnchor.MiddleCenter, Ink);
            notice.text = "可从任意港口出发，无需航海士；出航时消耗所选种子，探险结束返回出发港。";
            notice.resizeTextForBestFit = true;
            notice.resizeTextMinSize = 16;
            notice.resizeTextMaxSize = 19;

            bool Current() => ReferenceEquals(identity, departureTreasureSeedPickerIdentity) &&
                departureTreasureSeedPicker != null && departureTreasureSeedPicker.gameObject.activeInHierarchy &&
                DepartureChartViewCurrent(view, owner, revision) && !tutorialActive;

            if (session.TreasureMaps.Count == 0)
            {
                CreateText("Departure Treasure Seed Empty", card, new Vector2(1000f, 200f), Vector2.zero,
                    25, FontStyle.Normal, TextAnchor.MiddleCenter, Ink).text =
                    "暂无可用的藏宝海图种子\n在藏宝图桌鉴定碎片，拼齐四片后点击对应海图合成。";
            }
            for (int index = 0; index < session.TreasureMaps.Count; index++)
            {
                CoreTreasureMapRecord map = session.TreasureMaps[index];
                bool selected = session.SelectedTreasureMap?.Id == map.Id;
                Button option = CreateButton("Departure Treasure Seed Option " + index, card,
                    new Vector2(1060f, 78f), new Vector2(0f, 196f - index * 86f), PanelRaised, Ink, 18);
                Text caption = option.GetComponentInChildren<Text>();
                caption.alignment = TextAnchor.MiddleLeft;
                caption.rectTransform.sizeDelta = new Vector2(1010f, 72f);
                caption.text = (selected ? "◆ 已选择 · " : "◇ ") + map.Name +
                    "\n种子：" + map.Seed;
                caption.resizeTextForBestFit = true;
                caption.resizeTextMinSize = 14;
                caption.resizeTextMaxSize = 18;
                WoodButtonSkin.SetSelected(option, selected);
                option.onClick.AddListener(() =>
                {
                    if (!Current() || !option.IsInteractable()) return;
                    if (!session.TrySelectTreasureSeed(map.Seed, out string reason))
                    {
                        notice.text = reason;
                        notice.color = NauticalSurfaceSkin.ReadableTextColor(notice.transform.parent, Coral);
                        return;
                    }
                    departureUseChartSeed = false;
                    session.ClearChartSeedSelection();
                    SaveProgress();
                    Render();
                });
            }

            Button ordinary = CreateButton("Departure Clear Treasure Seed", card, new Vector2(330f, 52f),
                new Vector2(-356f, -317f), PanelRaised, Ink, 19);
            ordinary.GetComponentInChildren<Text>().text = "不使用种子 · 普通航线";
            ordinary.onClick.AddListener(() =>
            {
                if (!Current() || !ordinary.IsInteractable()) return;
                session.ClearSelectedTreasureMap();
                session.ClearChartSeedSelection();
                departureUseChartSeed = false;
                SaveProgress();
                Render();
            });
            Button table = CreateButton("Departure Treasure Seed Table", card, new Vector2(330f, 52f),
                new Vector2(0f, -317f), PanelRaised, Gold, 19);
            table.GetComponentInChildren<Text>().text = "前往藏宝图桌";
            table.onClick.AddListener(() =>
            {
                if (!Current() || !table.IsInteractable()) return;
                CommitDepartureSeed(view);
                activePortFacility = PortFacilityArtCatalog.TreasureFacilityIndex;
                Render();
            });
            Button close = CreateButton("Close Departure Treasure Seed Selection", card, new Vector2(330f, 52f),
                new Vector2(356f, -317f), PanelRaised, Ink, 19);
            close.GetComponentInChildren<Text>().text = "返回出航配置";
            close.onClick.AddListener(() =>
            {
                if (Current()) CloseDepartureTreasureSeedPicker();
            });
        }

        private void CloseDepartureTreasureSeedPicker()
        {
            departureTreasureSeedPickerIdentity = null;
            if (departureTreasureSeedPicker == null) return;
            departureTreasureSeedPicker.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(departureTreasureSeedPicker.gameObject);
            else DestroyImmediate(departureTreasureSeedPicker.gameObject);
            departureTreasureSeedPicker = null;
        }
    }
}
